# Drag Resize Canvas

A [Pinta](https://www.pinta-project.com/) add-in that provides canvas-editing tools:
resizing the canvas by dragging handles on its edges and corners, and placing image
files onto the selected layer.

## Features

- **8 drag handles** on the canvas — 4 corners + 4 edge midpoints.
- Resize by **expanding or shrinking** the canvas; the opposite edge/corner stays fixed while you drag.
- **Live preview** — the canvas updates in real time as you drag, and the handles snap back to the new canvas edges when you release.
- A single drag is **one undo step**, so Undo restores the original canvas size.
- Handles are drawn at a constant on-screen size regardless of zoom, with resize cursors on hover.
- **Resize Selected Pixels** — freely scale the selected region with the same 8 handles, with the opposite edge or corner fixed.
- **Place Image onto Layer** — place an image file onto the currently selected layer, replacing its content in a single undoable step.

## Installation

1. Build the add-in package (see below), or download a released `DragResizeCanvas.*.mpack`.
2. In Pinta, open **Add-ins > Add-in Manager** and click **Install Extension Package...**.
3. Select the `.mpack` file and confirm the installation.

> The add-in targets Pinta 3.1+.

## Building

The repository uses the Pinta source as a git submodule for compiling against.

```bash
# Clone with submodules
git clone --recurse-submodules https://github.com/superbaseballman/drag-resize-canvas.git

# Or update an existing clone
git submodule update --init --depth 1

# Build
dotnet build -c Release

# Create the .mpack package (requires the mautil tool)
dotnet tool install --global Mono.Addins.UtilTool
mautil pack DragResizeCanvas/bin/Release/net8.0/DragResizeCanvas.dll
```

## Usage

### Resizing the canvas

1. Select the **Canvas Resize** tool from the toolbox.
2. Drag any of the 8 handles on the canvas edges:
   - **Corner handles** resize both width and height at once.
   - **Edge handles** resize a single dimension.
3. Release the mouse button to finish. Use **Ctrl+Z** to undo back to the original canvas size.

### Placing an image onto a layer

1. Select the target layer in the **Layers** panel.
2. Select the **Place Image** tool from the toolbox — a file picker opens automatically. (Clicking the canvas re-opens the picker if it was cancelled.)
3. Choose an image file. It is placed onto the selected layer at its native size, aligned to the top-left corner, replacing any existing content. Use **Ctrl+Z** to undo.

### Resizing selected pixels

1. Make a selection and select the **Resize Selected Pixels** tool.
2. Drag any of the 8 handles around the selection to scale it. Corner handles scale both dimensions; edge handles scale one dimension.
3. Release the mouse button to finish. Use **Ctrl+Z** to undo the whole resize.

## License

MIT — see [LICENSE](LICENSE).
