using System;
using System.Collections.Generic;
using Mono.Addins;
using Pinta.Core;

namespace DragResizeCanvas;

/// <summary>
/// A tool that allows resizing the canvas by dragging handles on its edges.
/// </summary>
public sealed class CanvasResizeTool : BaseTool
{
	private readonly IWorkspaceService workspace;
	private readonly CanvasResizeHandle handle;
	private CompoundHistoryItem? hist;

	public CanvasResizeTool (IServiceProvider services) : base (services)
	{
		workspace = services.GetService<IWorkspaceService> ();
		handle = new CanvasResizeHandle (workspace);
	}

	public override string Name
		=> AddinManager.CurrentLocalizer.GetString ("Canvas Resize");

	public override string Icon
		=> Pinta.Resources.Icons.ImageResizeCanvas;

	public override string StatusBarText
		=> AddinManager.CurrentLocalizer.GetString (
			"Drag the handles on the canvas edges to resize the canvas.");

	public override Gdk.Cursor DefaultCursor
		=> GdkExtensions.CursorFromName (Pinta.Resources.StandardCursors.Default);

	public override int Priority
		=> 5;

	public override IEnumerable<IToolHandle> Handles
		=> [handle];

	protected override void OnActivated (Document? document)
	{
		base.OnActivated (document);

		if (document is null)
			return;

		handle.Rectangle = new RectangleD (0, 0, document.ImageSize.Width, document.ImageSize.Height);
		handle.Active = true;
		document.Workspace.Invalidate ();
	}

	protected override void OnDeactivated (Document? document, BaseTool? newTool)
	{
		base.OnDeactivated (document, newTool);

		handle.Active = false;
		handle.EndDrag ();

		// If a drag was in progress, push the merged history item.
		if (hist is not null && document is not null) {
			document.History.PushNewItem (hist);
			hist = null;
		}

		if (document is not null)
			document.Workspace.Invalidate ();
	}

	protected override void OnMouseDown (Document document, ToolMouseEventArgs e)
	{
		// Ignore extra button clicks while dragging
		if (handle.IsDragging)
			return;

		// Only start a drag if the mouse is on top of a handle.
		if (!handle.BeginDrag (e.PointDouble))
			return;

		// All resize operations during this drag are merged into a single
		// history item, so that undo restores the original canvas size.
		hist = new CompoundHistoryItem (
			Pinta.Resources.Icons.ImageResizeCanvas,
			AddinManager.CurrentLocalizer.GetString ("Resize Canvas"));

		// The canvas is resized live while dragging.
		ResizeCanvas (document, e.PointDouble);
	}

	protected override void OnMouseMove (Document document, ToolMouseEventArgs e)
	{
		if (!handle.IsDragging) {
			UpdateCursor (e.WindowPoint);
			return;
		}

		RectangleI handleDirtyRegion = handle.UpdateDrag (e.PointDouble, e.IsShiftPressed);
		document.Workspace.InvalidateWindowRect (handleDirtyRegion);

		ResizeCanvas (document, e.PointDouble);
	}

	protected override void OnMouseUp (Document document, ToolMouseEventArgs e)
	{
		if (handle.IsDragging) {
			// Final resize to the exact mouse position.
			ResizeCanvas (document, e.PointDouble);
			handle.EndDrag ();

			// Push the merged history item.
			if (hist is not null) {
				document.History.PushNewItem (hist);
				hist = null;
			}
		}

		// Update the mouse cursor.
		UpdateCursor (e.WindowPoint);
	}

	/// <summary>
	/// Resizes the canvas so that the dragged edge/corner follows the mouse position.
	/// The opposite edge/corner stays fixed.
	/// </summary>
	private void ResizeCanvas (Document document, PointD canvasPos)
	{
		RectangleD rect = handle.Rectangle;

		Size newSize = new (
			Width: (int) Math.Round (rect.Width),
			Height: (int) Math.Round (rect.Height));

		if (newSize == document.ImageSize)
			return;

		// Determine which edge(s) are fixed, based on which handle is being dragged.
		Anchor anchor = handle.GetAnchor ();

		document.ResizeCanvas (newSize, anchor, hist);
	}

	private void UpdateCursor (PointD viewPos)
	{
		Gdk.Cursor? cursor = handle.Active ?
			handle.GetCursorAtPoint (viewPos) :
			null;

		SetCursor (cursor ?? DefaultCursor);
	}
}