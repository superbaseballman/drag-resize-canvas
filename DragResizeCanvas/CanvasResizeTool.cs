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

		// Cancel any in-progress drag: push the merged history item if the
		// canvas was resized, and snap the handles back to the canvas bounds.
		handle.Active = false;
		handle.EndDrag ();

		if (document is not null) {
			if (hist is not null) {
				document.History.PushNewItem (hist);
				hist = null;
			}

			ResetHandlesToCanvas (document);
		}
	}

	protected override void OnMouseDown (Document document, ToolMouseEventArgs e)
	{
		// Ignore extra button clicks while dragging.
		if (handle.IsDragging)
			return;

		// Only start a drag if the mouse is on top of a handle. The canvas
		// is resized live while dragging.
		handle.BeginDrag (e.PointDouble);
	}

	protected override void OnMouseMove (Document document, ToolMouseEventArgs e)
	{
		if (!handle.IsDragging) {
			UpdateCursor (e.WindowPoint);
			return;
		}

		RectangleI handleDirtyRegion = handle.UpdateDrag (e.PointDouble, e.IsShiftPressed);
		document.Workspace.InvalidateWindowRect (handleDirtyRegion);

		// Resize the canvas live so the dragged edge follows the mouse.
		ResizeCanvas (document);
	}

	protected override void OnMouseUp (Document document, ToolMouseEventArgs e)
	{
		if (handle.IsDragging) {
			// Final resize to the exact mouse position.
			ResizeCanvas (document);
			handle.EndDrag ();

			// Push the merged history item.
			if (hist is not null) {
				document.History.PushNewItem (hist);
				hist = null;
			}

			// Snap all eight handles back to the new canvas bounds.
			ResetHandlesToCanvas (document);
		}

		// Update the mouse cursor.
		UpdateCursor (e.WindowPoint);
	}

	/// <summary>
	/// Resizes the canvas so that the dragged edge/corner follows the current
	/// handle rectangle. All resizes during one drag are merged into a single
	/// history item (created lazily on the first actual size change).
	/// </summary>
	private void ResizeCanvas (Document document)
	{
		RectangleD rect = handle.Rectangle;

		Size newSize = new (
			Width: (int) Math.Round (rect.Width),
			Height: (int) Math.Round (rect.Height));

		if (newSize == document.ImageSize)
			return;

		// Determine which edge(s) are fixed, based on which handle is being dragged.
		Anchor anchor = handle.GetAnchor ();

		hist ??= new CompoundHistoryItem (
			Pinta.Resources.Icons.ImageResizeCanvas,
			AddinManager.CurrentLocalizer.GetString ("Resize Canvas"));

		document.ResizeCanvas (newSize, anchor, hist);

		// Refresh the whole canvas so the resized content is shown live.
		document.Workspace.Invalidate ();
	}

	/// <summary>
	/// Moves all eight handles so they sit exactly on the canvas bounds.
	/// </summary>
	private void ResetHandlesToCanvas (Document document)
	{
		handle.Rectangle = new RectangleD (0, 0, document.ImageSize.Width, document.ImageSize.Height);
		document.Workspace.Invalidate ();
	}

	private void UpdateCursor (PointD viewPos)
	{
		Gdk.Cursor? cursor = handle.Active ?
			handle.GetCursorAtPoint (viewPos) :
			null;

		SetCursor (cursor ?? DefaultCursor);
	}
}