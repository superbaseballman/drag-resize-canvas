using System;
using System.Collections.Generic;
using Cairo;
using Mono.Addins;
using Pinta.Core;

namespace DragResizeCanvas;

/// <summary>
/// A tool that scales the selected pixels by dragging the selection handles.
/// </summary>
public sealed class SelectionResizeTool : BaseTool
{
	private readonly CanvasResizeHandle handle;
	private DocumentSelection? original_selection;
	private readonly Matrix original_transform = CairoExtensions.CreateIdentityMatrix ();
	private MovePixelsHistoryItem? hist;
	private RectangleD source_rectangle;

	public SelectionResizeTool (IServiceProvider services) : base (services)
	{
		handle = new CanvasResizeHandle (services.GetService<IWorkspaceService> ());
	}

	public override string Name
		=> AddinManager.CurrentLocalizer.GetString ("Resize Selected Pixels");

	public override string Icon
		=> Pinta.Resources.Icons.ImageResizeCanvas;

	public override string StatusBarText
		=> AddinManager.CurrentLocalizer.GetString (
			"Drag the handles around the selection to resize the selected pixels.");

	public override int Priority => 6;

	public override IEnumerable<IToolHandle> Handles => [handle];

	protected override void OnActivated (Document? document)
	{
		base.OnActivated (document);

		if (document is null || document.Selection.SelectionPolygons.Count == 0)
			return;

		source_rectangle = document.Selection.GetBounds ();
		handle.Rectangle = source_rectangle;
		handle.Active = true;
		document.Workspace.Invalidate ();
	}

	protected override void OnDeactivated (Document? document, BaseTool? newTool)
	{
		base.OnDeactivated (document, newTool);

		handle.Active = false;
		handle.EndDrag ();
		if (document is not null && hist is not null) {
			document.History.PushNewItem (hist);
			hist = null;
		}

		document?.FinishSelection ();
		original_selection = null;
		document?.Workspace.Invalidate ();
	}

	protected override void OnMouseDown (Document document, ToolMouseEventArgs e)
	{
		if (e.MouseButton != MouseButton.Left || document.Selection.SelectionPolygons.Count == 0)
			return;

		if (!handle.IsDragging && handle.BeginDrag (e.PointDouble))
			StartTransform (document);
	}

	protected override void OnMouseMove (Document document, ToolMouseEventArgs e)
	{
		if (!handle.IsDragging) {
			UpdateCursor (e.WindowPoint);
			return;
		}

		RectangleI dirty = handle.UpdateDrag (e.PointDouble, e.IsShiftPressed);
		document.Workspace.InvalidateWindowRect (dirty);
		UpdateTransform (document);
	}

	protected override void OnMouseUp (Document document, ToolMouseEventArgs e)
	{
		if (!handle.IsDragging)
			return;

		UpdateTransform (document);
		handle.EndDrag ();

		if (hist is not null) {
			document.History.PushNewItem (hist);
			hist = null;
		}

		if (original_selection is not null)
			handle.Rectangle = document.Selection.HandleBounds;

		document.Workspace.Invalidate ();
		UpdateCursor (e.WindowPoint);
	}

	private void StartTransform (Document document)
	{
		original_selection = document.Selection.Clone ();
		original_transform.InitMatrix (document.Layers.SelectionLayer.Transform);
		hist = new MovePixelsHistoryItem (Icon, Name, document);
		hist.TakeSnapshot (!document.Layers.ShowSelectionLayer);

		if (document.Layers.ShowSelectionLayer)
			return;

		document.Layers.CreateSelectionLayer ();
		document.Layers.ShowSelectionLayer = true;
		document.Layers.SelectionLayer.BlendMode = document.Layers.CurrentUserLayer.BlendMode;
		document.Layers.SelectionLayer.Opacity = document.Layers.CurrentUserLayer.Opacity;
		document.Layers.SelectionLayer.Hidden = document.Layers.CurrentUserLayer.Hidden;

		using Context selectionContext = new (document.Layers.SelectionLayer.Surface);
		document.Selection.Clip (selectionContext);
		selectionContext.SetSourceSurface (document.Layers.CurrentUserLayer.Surface, 0, 0);
		selectionContext.Paint ();

		using Context layerContext = new (document.Layers.CurrentUserLayer.Surface);
		document.Selection.Clip (layerContext);
		layerContext.Operator = Operator.Clear;
		layerContext.Paint ();
	}

	private void UpdateTransform (Document document)
	{
		if (original_selection is null)
			return;

		RectangleD target = handle.Rectangle;
		Matrix transform = CreateTransform (source_rectangle, target, handle.GetAnchor ());

		document.Selection = original_selection.Transform (transform);
		document.Selection.Visible = true;
		document.Layers.SelectionLayer.Transform.InitMatrix (original_transform);
		document.Layers.SelectionLayer.Transform.Multiply (transform);
		document.Workspace.Invalidate ();
	}

	private static Matrix CreateTransform (RectangleD source, RectangleD target, Anchor anchor)
	{
		double scaleX = target.Width / source.Width;
		double scaleY = target.Height / source.Height;
		PointD fixedPoint = anchor switch {
			Anchor.SE => new (source.X + source.Width, source.Y + source.Height),
			Anchor.NE => new (source.X + source.Width, source.Y),
			Anchor.SW => new (source.X, source.Y + source.Height),
			Anchor.NW => source.Location (),
			Anchor.E => new (source.X + source.Width, source.Y + source.Height / 2),
			Anchor.W => new (source.X, source.Y + source.Height / 2),
			Anchor.N => new (source.X + source.Width / 2, source.Y),
			Anchor.S => new (source.X + source.Width / 2, source.Y + source.Height),
			_ => source.GetCenter (),
		};

		Matrix transform = CairoExtensions.CreateIdentityMatrix ();
		transform.Translate (fixedPoint.X, fixedPoint.Y);
		transform.Scale (scaleX, scaleY);
		transform.Translate (-fixedPoint.X, -fixedPoint.Y);
		return transform;
	}

	private void UpdateCursor (PointD viewPos)
	{
		Gdk.Cursor? cursor = handle.Active ? handle.GetCursorAtPoint (viewPos) : null;
		SetCursor (cursor ?? DefaultCursor);
	}
}
