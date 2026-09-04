using Pinta.Core;

namespace DragResizeCanvas;

[Mono.Addins.Extension]
public sealed class DragResizeCanvasExtension : IExtension
{
	public void Initialize ()
	{
		PintaCore.Tools.AddTool (new CanvasResizeTool (PintaCore.Services));
		PintaCore.Tools.AddTool (new SelectionResizeTool (PintaCore.Services));
		PintaCore.Tools.AddTool (new PlaceImageTool (PintaCore.Services));
	}

	public void Uninitialize ()
	{
		PintaCore.Tools.RemoveInstanceOfTool<CanvasResizeTool> ();
		PintaCore.Tools.RemoveInstanceOfTool<SelectionResizeTool> ();
		PintaCore.Tools.RemoveInstanceOfTool<PlaceImageTool> ();
	}
}
