using Pinta.Core;

namespace DragResizeCanvas;

[Mono.Addins.Extension]
public sealed class DragResizeCanvasExtension : IExtension
{
	public void Initialize ()
	{
		PintaCore.Tools.AddTool (new CanvasResizeTool (PintaCore.Services));
	}

	public void Uninitialize ()
	{
		PintaCore.Tools.RemoveInstanceOfTool<CanvasResizeTool> ();
	}
}
