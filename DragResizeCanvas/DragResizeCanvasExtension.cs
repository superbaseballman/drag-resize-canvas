using System.IO;
using System.Reflection;
using Pinta.Core;

namespace DragResizeCanvas;

[Mono.Addins.Extension]
public sealed class DragResizeCanvasExtension : IExtension
{
	private DragResizeCanvasSettings? settings;

	public void Initialize ()
	{
		string? assemblyDirectory = Path.GetDirectoryName (Assembly.GetExecutingAssembly ().Location);
		if (assemblyDirectory is not null)
			GtkExtensions.GetDefaultIconTheme ().AddSearchPath (Path.Combine (assemblyDirectory, "icons"));

		settings = new DragResizeCanvasSettings (PintaCore.Settings, ApplySettings);
		PintaCore.Tools.AddTool (new DragResizeCanvasSettingsTool (PintaCore.Services, settings));
		ApplySettings ();
	}

	public void Uninitialize ()
	{
		PintaCore.Tools.RemoveInstanceOfTool<CanvasResizeTool> ();
		PintaCore.Tools.RemoveInstanceOfTool<SelectionResizeTool> ();
		PintaCore.Tools.RemoveInstanceOfTool<PlaceImageTool> ();
		PintaCore.Tools.RemoveInstanceOfTool<DragResizeCanvasSettingsTool> ();
		settings = null;
	}

	private void ApplySettings ()
	{
		if (settings is null)
			return;

		PintaCore.Tools.RemoveInstanceOfTool<CanvasResizeTool> ();
		PintaCore.Tools.RemoveInstanceOfTool<SelectionResizeTool> ();
		PintaCore.Tools.RemoveInstanceOfTool<PlaceImageTool> ();

		if (settings.EnableCanvasResize)
			PintaCore.Tools.AddTool (new CanvasResizeTool (PintaCore.Services));

		if (settings.EnableSelectionResize)
			PintaCore.Tools.AddTool (new SelectionResizeTool (PintaCore.Services, settings));

		if (settings.EnablePlaceImage)
			PintaCore.Tools.AddTool (new PlaceImageTool (PintaCore.Services));
	}
}
