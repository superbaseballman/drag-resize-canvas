using System;
using System.Threading.Tasks;
using Cairo;
using Mono.Addins;
using Pinta.Core;

namespace DragResizeCanvas;

/// <summary>
/// A tool that places an image file onto the currently selected layer,
/// replacing the layer's existing content.
/// </summary>
/// <remarks>
/// When the tool is selected from the toolbox (or the canvas is clicked),
/// a file dialog is shown. After choosing an image, it is drawn at its
/// native size, aligned to the top-left corner of the layer. The operation
/// is recorded as a single history item so that undo restores the layer's
/// original content.
/// </remarks>
public sealed class PlaceImageTool : BaseTool
{
	private readonly ImageConverterManager image_formats;

	private bool is_picking;

	public PlaceImageTool (IServiceProvider services) : base (services)
	{
		image_formats = services.GetService<ImageConverterManager> ();
	}

	public override string Name
		=> AddinManager.CurrentLocalizer.GetString ("Place Image");

	public override string Icon
		=> Pinta.Resources.Icons.LayerImport;

	public override string StatusBarText
		=> AddinManager.CurrentLocalizer.GetString (
			"Select the layer to place the image onto in the Layers panel, then choose an image file.");

	public override int Priority
		=> 6;

	protected override void OnActivated (Document? document)
	{
		base.OnActivated (document);

		if (document is not null)
			StartPickingImage (document);
	}

	protected override void OnMouseDown (Document document, ToolMouseEventArgs e)
	{
		// Only the left button triggers a placement.
		if (e.MouseButton != MouseButton.Left)
			return;

		// Allow repeated placements, or retrying after the dialog was cancelled.
		StartPickingImage (document);
	}

	/// <summary>
	/// Shows the image file picker and places the chosen image onto the
	/// currently selected layer of <paramref name="doc"/>.
	/// </summary>
	private async void StartPickingImage (Document doc)
	{
		// Guard against re-entrancy while the file dialog is open.
		if (is_picking)
			return;

		// The image is placed onto the layer that is selected in the Layers panel.
		UserLayer targetLayer = doc.Layers.CurrentUserLayer;

		is_picking = true;
		try {
			Gio.File? choice = await PickImageFileAsync ();

			// The user cancelled the dialog.
			if (choice is null)
				return;

			PlaceImageOnLayer (doc, targetLayer, choice);
		} catch (Exception ex) {
			try {
				await PintaCore.Chrome.ShowMessageDialog (
					PintaCore.Chrome.MainWindow,
					AddinManager.CurrentLocalizer.GetString ("Place Image"),
					ex.Message);
			} catch {
				// Never let error reporting crash the tool.
			}
		} finally {
			is_picking = false;
		}
	}

	/// <summary>
	/// Shows a file dialog for choosing an image file, with filters for all
	/// image formats that Pinta can import. Returns null if the user cancels.
	/// </summary>
	private async Task<Gio.File?> PickImageFileAsync ()
	{
		using Gtk.FileFilter imagesFilter = CreateImageFileFilter ();

		using Gio.ListStore fileFilters = Gio.ListStore.New (Gtk.FileFilter.GetGType ());
		fileFilters.Append (imagesFilter);

		using Gtk.FileDialog fileDialog = Gtk.FileDialog.New ();
		fileDialog.SetTitle (AddinManager.CurrentLocalizer.GetString ("Open Image File"));
		fileDialog.SetFilters (fileFilters);

		// This extension method returns null when the dialog is cancelled.
		return await fileDialog.OpenFileAsync (PintaCore.Chrome.MainWindow);
	}

	/// <summary>
	/// Replaces the content of <paramref name="layer"/> with the image in
	/// <paramref name="file"/>, drawn at native size in the top-left corner.
	/// </summary>
	private void PlaceImageOnLayer (Document doc, UserLayer layer, Gio.File file)
	{
		GdkPixbuf.Pixbuf image;
		using (Gio.FileInputStream fs = file.Read (null)) {
			// Throws if the file cannot be decoded as an image.
			image = GdkPixbuf.Pixbuf.NewFromStream (fs, cancellable: null)!; // NRT: only null when an error is thrown
		}

		using (image) {
			// Keep a copy of the original layer pixels for undo support.
			ImageSurface oldSurface = layer.Surface.Clone ();

			// Replace the layer's content with the image.
			// The surface is cleared first so the layer contains exactly the image.
			layer.Surface.Clear ();
			using (Cairo.Context context = new (layer.Surface)) {
				context.DrawPixbuf (image, PointD.Zero);
			}

			// Record a single undo step for the whole replacement.
			SimpleHistoryItem hist = new (
				Pinta.Resources.Icons.LayerImport,
				AddinManager.CurrentLocalizer.GetString ("Place Image on Layer"),
				oldSurface,
				doc.Layers.IndexOf (layer));

			doc.History.PushNewItem (hist);
		}

		doc.Workspace.Invalidate ();
	}

	/// <summary>
	/// Builds a file filter matching every image format that Pinta can import.
	/// </summary>
	private Gtk.FileFilter CreateImageFileFilter ()
	{
		Gtk.FileFilter imagesFilter = Gtk.FileFilter.New ();

		foreach (FormatDescriptor format in image_formats.Formats) {
			if (!format.IsImportAvailable ())
				continue;

			foreach (string ext in format.Extensions)
				imagesFilter.AddPattern ($"*.{ext}");
		}

		// On Unix-like systems files can often be identified by MIME type as well,
		// but adding MIME filters on Windows would force the GTK file picker.
		if (SystemManager.GetOperatingSystem () != OS.Windows) {
			foreach (FormatDescriptor format in image_formats.Formats) {
				foreach (string mime in format.Mimes)
					imagesFilter.AddMimeType (mime);
			}
		}

		imagesFilter.Name = AddinManager.CurrentLocalizer.GetString ("Image files");

		return imagesFilter;
	}
}
