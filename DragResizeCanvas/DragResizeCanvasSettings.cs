using System;
using System.Threading.Tasks;
using Mono.Addins;
using Pinta.Core;

namespace DragResizeCanvas;

public enum SelectionFillMode
{
	Transparent,
	EdgePixels,
}

public sealed class DragResizeCanvasSettings
{
	private const string CanvasResizeKey = "drag-resize-canvas.enable-canvas-resize";
	private const string SelectionResizeKey = "drag-resize-canvas.enable-selection-resize";
	private const string PlaceImageKey = "drag-resize-canvas.enable-place-image";
	private const string FillModeKey = "drag-resize-canvas.selection-fill-mode";
	private const string ResamplingKey = "drag-resize-canvas.selection-resampling";

	private readonly ISettingsService settings;
	private readonly Action changed;

	public DragResizeCanvasSettings (ISettingsService settings, Action changed)
	{
		this.settings = settings;
		this.changed = changed;
	}

	public bool EnableCanvasResize {
		get => settings.GetSetting (CanvasResizeKey, true);
		set => Put (CanvasResizeKey, value);
	}

	public bool EnableSelectionResize {
		get => settings.GetSetting (SelectionResizeKey, true);
		set => Put (SelectionResizeKey, value);
	}

	public bool EnablePlaceImage {
		get => settings.GetSetting (PlaceImageKey, true);
		set => Put (PlaceImageKey, value);
	}

	public SelectionFillMode FillMode {
		get => Enum.TryParse (settings.GetSetting (FillModeKey, SelectionFillMode.Transparent.ToString ()), out SelectionFillMode value)
			? value : SelectionFillMode.Transparent;
		set => Put (FillModeKey, value);
	}

	public ResamplingMode Resampling {
		get => Enum.TryParse (settings.GetSetting (ResamplingKey, ResamplingMode.Bilinear.ToString ()), out ResamplingMode value)
			? value : ResamplingMode.Bilinear;
		set => Put (ResamplingKey, value);
	}

	private void Put<T> (string key, T value)
	{
		settings.PutSetting (key, value is Enum e ? e.ToString () : value!);
		changed ();
	}

	public async Task ShowDialogAsync ()
	{
		using SettingsDialog dialog = new (this);
		Gtk.ResponseType response = await dialog.RunAsync ();
		if (response == Gtk.ResponseType.Ok)
			dialog.Apply ();
	}

	private sealed class SettingsDialog : Gtk.Dialog
	{
		private readonly DragResizeCanvasSettings owner;
		private readonly Gtk.CheckButton canvas_resize;
		private readonly Gtk.CheckButton selection_resize;
		private readonly Gtk.CheckButton place_image;
		private readonly Gtk.ComboBoxText fill_mode;
		private readonly Gtk.ComboBoxText resampling;

		public SettingsDialog (DragResizeCanvasSettings owner)
		{
			this.owner = owner;
			canvas_resize = Gtk.CheckButton.NewWithLabel (AddinManager.CurrentLocalizer.GetString ("Enable Canvas Resize"));
			selection_resize = Gtk.CheckButton.NewWithLabel (AddinManager.CurrentLocalizer.GetString ("Enable Selection Resize"));
			place_image = Gtk.CheckButton.NewWithLabel (AddinManager.CurrentLocalizer.GetString ("Enable Place Image"));
			canvas_resize.Active = owner.EnableCanvasResize;
			selection_resize.Active = owner.EnableSelectionResize;
			place_image.Active = owner.EnablePlaceImage;

			fill_mode = new ();
			fill_mode.Append (SelectionFillMode.Transparent.ToString (), AddinManager.CurrentLocalizer.GetString ("Transparent"));
			fill_mode.Append (SelectionFillMode.EdgePixels.ToString (), AddinManager.CurrentLocalizer.GetString ("Repeat Edge Pixels"));
			fill_mode.ActiveId = owner.FillMode.ToString ();

			resampling = new ();
			resampling.Append (ResamplingMode.NearestNeighbor.ToString (), AddinManager.CurrentLocalizer.GetString ("Nearest Neighbor"));
			resampling.Append (ResamplingMode.Bilinear.ToString (), AddinManager.CurrentLocalizer.GetString ("Bilinear"));
			resampling.ActiveId = owner.Resampling.ToString ();

			Gtk.Grid grid = new () { RowSpacing = 6, ColumnSpacing = 12 };
			grid.Attach (canvas_resize, 0, 0, 2, 1);
			grid.Attach (selection_resize, 0, 1, 2, 1);
			grid.Attach (place_image, 0, 2, 2, 1);
			grid.Attach (Gtk.Label.New (AddinManager.CurrentLocalizer.GetString ("Pixel Fill:")), 0, 3, 1, 1);
			grid.Attach (fill_mode, 1, 3, 1, 1);
			grid.Attach (Gtk.Label.New (AddinManager.CurrentLocalizer.GetString ("Interpolation:")), 0, 4, 1, 1);
			grid.Attach (resampling, 1, 4, 1, 1);

			Gtk.Box content = this.GetContentAreaBox ();
			content.SetAllMargins (12);
			content.Append (grid);
			Title = AddinManager.CurrentLocalizer.GetString ("Drag Resize Canvas Settings");
			TransientFor = PintaCore.Chrome.MainWindow;
			Modal = true;
			IconName = Pinta.Resources.Icons.ImageResizeCanvas;
			this.AddCancelOkButtons ();
			SetDefaultResponse ((int) Gtk.ResponseType.Ok);
		}

		public void Apply ()
		{
			owner.EnableCanvasResize = canvas_resize.Active;
			owner.EnableSelectionResize = selection_resize.Active;
			owner.EnablePlaceImage = place_image.Active;
			if (Enum.TryParse (fill_mode.ActiveId, out SelectionFillMode fill))
				owner.FillMode = fill;
			if (Enum.TryParse (resampling.ActiveId, out ResamplingMode mode))
				owner.Resampling = mode;
		}
	}
}

public sealed class DragResizeCanvasSettingsTool : BaseTool
{
	private readonly DragResizeCanvasSettings settings;
	private bool opening;

	public DragResizeCanvasSettingsTool (IServiceProvider services, DragResizeCanvasSettings settings) : base (services)
	{
		this.settings = settings;
	}

	public override string Name => AddinManager.CurrentLocalizer.GetString ("Drag Resize Canvas Settings");
	public override string Icon => "drag-resize-canvas-settings-symbolic";
	public override string StatusBarText => AddinManager.CurrentLocalizer.GetString ("Configure Drag Resize Canvas tools.");
	public override int Priority => 7;

	protected override void OnActivated (Document? document)
	{
		base.OnActivated (document);
		if (!opening)
			OpenDialog ();
	}

	private async void OpenDialog ()
	{
		opening = true;
		try {
			await settings.ShowDialogAsync ();
		} finally {
			opening = false;
		}
	}
}
