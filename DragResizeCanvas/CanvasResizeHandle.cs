using System;
using System.Collections.Generic;
using System.Linq;
using Pinta.Core;

namespace DragResizeCanvas;

/// <summary>
/// The corner / edge of the canvas that a handle is attached to.
/// </summary>
internal enum CanvasHandlePoint
{
	UpperLeft = 0,
	LowerLeft = 1,
	UpperRight = 2,
	LowerRight = 3,
	Left = 4,
	Up = 5,
	Right = 6,
	Down = 7,
}

/// <summary>
/// A draggable handle drawn on the canvas window, at a constant size
/// regardless of the image zoom.
/// </summary>
internal sealed class CanvasMoveHandle : IToolHandle
{
	private static readonly Gdk.RGBA fill_color = new () { Red = 0.2f, Green = 0.6f, Blue = 1.0f, Alpha = 1 };
	private static readonly Gdk.RGBA selected_fill_color = new () { Red = 1.0f, Green = 0.8f, Blue = 0.2f, Alpha = 1 };
	private static readonly Gdk.RGBA stroke_color = new () { Red = 1, Green = 1, Blue = 1, Alpha = 0.8f };

	private const double RADIUS = 4.5;

	private readonly IWorkspaceService workspace;

	public CanvasMoveHandle (IWorkspaceService workspace)
	{
		this.workspace = workspace;
	}

	public PointD CanvasPosition { get; set; }

	/// <summary>
	/// Inactive handles are not drawn.
	/// </summary>
	public bool Active { get; set; } = false;

	/// <summary>
	/// A handle that is selected by the user for interaction is drawn in a different color.
	/// </summary>
	public bool Selected { get; set; } = false;

	public Gdk.Cursor Cursor { get; init; } = GdkExtensions.CursorFromName (Pinta.Resources.StandardCursors.Default);

	/// <summary>
	/// Tests whether the window point is inside the handle's area.
	/// The area to grab a handle is a bit larger than the rendered area for easier selection.
	/// </summary>
	public bool ContainsPoint (PointD window_point)
	{
		const int TOLERANCE = 5;

		RectangleD bounds = ComputeWindowRect ().Inflated (TOLERANCE, TOLERANCE);
		return bounds.ContainsPoint (window_point);
	}

	/// <summary>
	/// Draw the handle, at a constant window space size (i.e. not depending on the image zoom or resolution).
	/// </summary>
	public void Draw (Gtk.Snapshot snapshot)
	{
		Gsk.PathBuilder pathBuilder = Gsk.PathBuilder.New ();
		PointD windowPt = workspace.CanvasPointToView (CanvasPosition);
		pathBuilder.AddCircle (windowPt.ToGraphenePoint (), (float) RADIUS);
		Gsk.Path path = pathBuilder.ToPath ();

		Gdk.RGBA fillColor = Selected ? selected_fill_color : fill_color;
		snapshot.AppendFill (path, Gsk.FillRule.EvenOdd, fillColor);

		Gsk.Stroke stroke = Gsk.Stroke.New (lineWidth: 1.0f);
		snapshot.AppendStroke (path, stroke, stroke_color);
	}

	/// <summary>
	/// Bounding rectangle to use with InvalidateWindowRect() when triggering a redraw.
	/// </summary>
	public RectangleI InvalidateRect => ComputeWindowRect ().Inflated (2, 2).ToInt ();

	/// <summary>
	/// Bounding rectangle of the handle (in window space).
	/// </summary>
	private RectangleD ComputeWindowRect ()
	{
		const double DIAMETER = 2 * RADIUS;

		PointD windowPt = workspace.CanvasPointToView (CanvasPosition);
		return new RectangleD (windowPt.X - RADIUS, windowPt.Y - RADIUS, DIAMETER, DIAMETER);
	}

	/// <summary>
	/// Returns the union of the invalidate rectangles for a collection of handles.
	/// </summary>
	public static RectangleI UnionInvalidateRects (IEnumerable<CanvasMoveHandle> handles) =>
		handles
		.Select (c => c.InvalidateRect)
		.DefaultIfEmpty (RectangleI.Zero)
		.Aggregate ((accum, r) => accum.Union (r));
}

/// <summary>
/// A set of handles on the edges and corners of the canvas, used to
/// resize the canvas by dragging.
/// </summary>
public sealed class CanvasResizeHandle : IToolHandle
{
	private readonly IWorkspaceService workspace;

	private PointD start_pt;
	private PointD end_pt;
	private readonly Dictionary<CanvasHandlePoint, CanvasMoveHandle> handles;
	private CanvasMoveHandle? active_handle;
	private PointD? drag_start_pos;

	// The rectangle before the drag began, used to snap the handles back
	// into place when the drag ends.
	private PointD start_snapshot;
	private PointD end_snapshot;

	// The result of the most recent drag (null if no drag has happened).
	private RectangleD? resized_rectangle;

	public CanvasResizeHandle (IWorkspaceService workspace)
	{
		this.workspace = workspace;

		handles = new Dictionary<CanvasHandlePoint, CanvasMoveHandle>
		{
			{ CanvasHandlePoint.UpperLeft, new (workspace) { Cursor = GdkExtensions.CursorFromName (Pinta.Resources.StandardCursors.ResizeNW) } },
			{ CanvasHandlePoint.LowerLeft, new (workspace) { Cursor = GdkExtensions.CursorFromName (Pinta.Resources.StandardCursors.ResizeSW) } },
			{ CanvasHandlePoint.UpperRight, new (workspace) { Cursor = GdkExtensions.CursorFromName (Pinta.Resources.StandardCursors.ResizeNE) } },
			{ CanvasHandlePoint.LowerRight, new (workspace) { Cursor = GdkExtensions.CursorFromName (Pinta.Resources.StandardCursors.ResizeSE) } },
			{ CanvasHandlePoint.Left, new (workspace) { Cursor = GdkExtensions.CursorFromName (Pinta.Resources.StandardCursors.ResizeW) } },
			{ CanvasHandlePoint.Up, new (workspace) { Cursor = GdkExtensions.CursorFromName (Pinta.Resources.StandardCursors.ResizeN) } },
			{ CanvasHandlePoint.Right, new (workspace) { Cursor = GdkExtensions.CursorFromName (Pinta.Resources.StandardCursors.ResizeE) } },
			{ CanvasHandlePoint.Down, new (workspace) { Cursor = GdkExtensions.CursorFromName (Pinta.Resources.StandardCursors.ResizeS) } },
		};

		foreach (var handle in handles.Values)
			handle.Active = true;
	}

	#region IToolHandle Implementation
	public bool Active { get; set; }

	public void Draw (Gtk.Snapshot snapshot)
	{
		foreach (CanvasMoveHandle handle in handles.Values)
			handle.Draw (snapshot);
	}
	#endregion

	/// <summary>
	/// Whether the user is currently dragging a handle.
	/// </summary>
	public bool IsDragging => drag_start_pos is not null;

	/// <summary>
	/// The rectangle covered by the canvas (in canvas coordinates).
	/// </summary>
	public RectangleD Rectangle {
		get => RectangleD.FromPoints (start_pt, end_pt, invertIfNegative: true);
		set {
			start_pt = value.Location ();
			end_pt = value.EndLocation ();
			UpdateHandlePositions ();
		}
	}

	/// <summary>
	/// The result of the most recent drag, in canvas coordinates.
	/// This is the resized rectangle that was previewed while dragging.
	/// </summary>
	public RectangleD? ResizedRectangle => resized_rectangle;

	/// <summary>
	/// Begins a drag operation if the mouse position is on top of a handle.
	/// The current rectangle is saved so the handles can snap back when
	/// the drag ends, leaving the actual resizing to the caller.
	/// </summary>
	public bool BeginDrag (in PointD canvasPos)
	{
		if (IsDragging)
			return false;

		PointD viewPos = workspace.CanvasPointToView (canvasPos);
		UpdateHandleUnderPoint (viewPos);

		if (active_handle is null)
			return false;

		start_snapshot = start_pt;
		end_snapshot = end_pt;
		resized_rectangle = null;

		drag_start_pos = viewPos;
		return true;
	}

	/// <summary>
	/// Updates the rectangle as the mouse is moved.
	/// </summary>
	/// <returns>The region to redraw with InvalidateWindowRect()</returns>
	public RectangleI UpdateDrag (PointD canvasPos, bool shiftPressed)
	{
		if (!IsDragging || active_handle is null)
			throw new InvalidOperationException ("Drag operation has not been started!");

		RectangleI dirty = ComputeInvalidateRect ();

		CanvasHandlePoint activeHandlePoint = handles.First (kv => kv.Value == active_handle).Key;
		MoveActiveHandle (activeHandlePoint, canvasPos.X, canvasPos.Y, shiftPressed);
		UpdateHandlePositions ();

		dirty = dirty.Union (ComputeInvalidateRect ());
		return dirty;
	}

	/// <summary>
	/// Finishes a drag operation. The previewed rectangle is stored in
	/// <see cref="ResizedRectangle"/> and the handles snap back to the
	/// rectangle they were dragged from.
	/// </summary>
	public void EndDrag ()
	{
		if (drag_start_pos is null)
			return;

		resized_rectangle = Rectangle;

		start_pt = start_snapshot;
		end_pt = end_snapshot;
		UpdateHandlePositions ();

		active_handle = null;
		drag_start_pos = null;
	}

	/// <summary>
	/// The cursor to display, if the cursor is over a handle.
	/// </summary>
	public Gdk.Cursor? GetCursorAtPoint (PointD viewPos)
		=> handles.Values.FirstOrDefault (c => c.ContainsPoint (viewPos))?.Cursor;

	/// <summary>
	/// Determines which edge(s) of the canvas are fixed, based on the
	/// handle that is currently being dragged.
	/// </summary>
	public Anchor GetAnchor ()
	{
		if (active_handle is null)
			return Anchor.Center;

		if (active_handle == handles[CanvasHandlePoint.UpperLeft])
			return Anchor.SE;
		if (active_handle == handles[CanvasHandlePoint.LowerLeft])
			return Anchor.NE;
		if (active_handle == handles[CanvasHandlePoint.UpperRight])
			return Anchor.SW;
		if (active_handle == handles[CanvasHandlePoint.LowerRight])
			return Anchor.NW;
		if (active_handle == handles[CanvasHandlePoint.Left])
			return Anchor.E;
		if (active_handle == handles[CanvasHandlePoint.Up])
			return Anchor.S;
		if (active_handle == handles[CanvasHandlePoint.Right])
			return Anchor.W;
		if (active_handle == handles[CanvasHandlePoint.Down])
			return Anchor.N;

		return Anchor.Center;
	}

	private void UpdateHandlePositions ()
	{
		PointD center = Utility.Lerp (start_pt, end_pt, 0.5f);

		handles[CanvasHandlePoint.UpperLeft].CanvasPosition = start_pt;
		handles[CanvasHandlePoint.LowerLeft].CanvasPosition = new PointD (start_pt.X, end_pt.Y);
		handles[CanvasHandlePoint.UpperRight].CanvasPosition = new PointD (end_pt.X, start_pt.Y);
		handles[CanvasHandlePoint.LowerRight].CanvasPosition = end_pt;
		handles[CanvasHandlePoint.Left].CanvasPosition = new PointD (start_pt.X, center.Y);
		handles[CanvasHandlePoint.Up].CanvasPosition = new PointD (center.X, start_pt.Y);
		handles[CanvasHandlePoint.Right].CanvasPosition = new PointD (end_pt.X, center.Y);
		handles[CanvasHandlePoint.Down].CanvasPosition = new PointD (center.X, end_pt.Y);
	}

	private void UpdateHandleUnderPoint (PointD viewPos)
	{
		active_handle = handles.Values.FirstOrDefault (c => c.ContainsPoint (viewPos));
	}

	private void MoveActiveHandle (CanvasHandlePoint handle, double x, double y, bool shiftPressed)
	{
		// Update the rectangle's size depending on which handle was dragged.
		// The rectangle is prevented from inverting: dragging a handle past
		// the opposite edge clamps it to a minimum size of 1 pixel.

		switch (handle) {
			case CanvasHandlePoint.UpperLeft:
				start_pt = new (Math.Min (x, end_pt.X - 1), Math.Min (y, end_pt.Y - 1));
				return;

			case CanvasHandlePoint.LowerLeft:
				start_pt = start_pt with { X = Math.Min (x, end_pt.X - 1) };
				end_pt = end_pt with { Y = Math.Max (y, start_pt.Y + 1) };
				return;

			case CanvasHandlePoint.UpperRight:
				end_pt = end_pt with { X = Math.Max (x, start_pt.X + 1) };
				start_pt = start_pt with { Y = Math.Min (y, end_pt.Y - 1) };
				return;

			case CanvasHandlePoint.LowerRight:
				end_pt = new (Math.Max (x, start_pt.X + 1), Math.Max (y, start_pt.Y + 1));
				return;

			case CanvasHandlePoint.Left:
				start_pt = start_pt with { X = Math.Min (x, end_pt.X - 1) };
				return;

			case CanvasHandlePoint.Up:
				start_pt = start_pt with { Y = Math.Min (y, end_pt.Y - 1) };
				return;

			case CanvasHandlePoint.Right:
				end_pt = end_pt with { X = Math.Max (x, start_pt.X + 1) };
				return;

			case CanvasHandlePoint.Down:
				end_pt = end_pt with { Y = Math.Max (y, start_pt.Y + 1) };
				return;

			default:
				throw new ArgumentOutOfRangeException (nameof (handle));
		}
	}

	/// <summary>
	/// Bounding rectangle to use with InvalidateWindowRect() when triggering a redraw.
	/// </summary>
	private RectangleI ComputeInvalidateRect ()
		=> CanvasMoveHandle.UnionInvalidateRects (handles.Values);
}