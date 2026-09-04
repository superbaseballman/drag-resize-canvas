# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.3.1] - 2026-09-04

### Fixed

- Dragging the canvas resize handles on the top or left edges (and the
  top-left / top-right / bottom-left corners) no longer lets the fixed
  anchor edge drift while the canvas is resized live. The drag is now
  computed from a stable reference captured when the drag starts.
- Resizing the selected pixels a second time no longer distorts or tears
  the image; each drag now uses the current selection as its source.

## [0.3.0] - 2026-09-04

- Added independent enable/disable settings for each plugin tool.
- Added custom symbolic icons for the plugin tools.
- Added nearest-neighbor and bilinear interpolation options for selection resizing.
- Added transparent and repeated edge-pixel fill modes for selection resizing.

## [0.2.1] - 2026-09-03

### Fixed

- Resizing the canvas by dragging the top or left handles (and the
  top-left / top-right / bottom-left corners) no longer leaves the image
  content offset from the handles. The eight handles are now re-aligned to
  the new canvas bounds when a resize drag ends.

### Changed

- The canvas is resized live again while dragging (real-time preview). All
  resizes during a single drag are still merged into one undo step.

## [0.2.0] - 2026-09-03

### Added

- **Place Image** tool: places an image file onto the currently selected
  layer at its native size, aligned to the top-left corner, replacing the
  layer's existing content in a single undoable step.

## [0.1.0] - 2026-09-02

### Added

- Initial release.
- **Canvas Resize** tool: resize the canvas by dragging any of the eight
  handles (4 corners + 4 edge midpoints) on the canvas edges. The opposite
  edge/corner stays fixed, and a whole drag is one undo step.

[0.3.1]: https://github.com/superbaseballman/drag-resize-canvas/releases/tag/v0.3.1
[0.3.0]: https://github.com/superbaseballman/drag-resize-canvas/releases/tag/v0.3.0
[0.2.1]: https://github.com/superbaseballman/drag-resize-canvas/releases/tag/v0.2.1
[0.2.0]: https://github.com/superbaseballman/drag-resize-canvas/releases/tag/v0.2.0
[0.1.0]: https://github.com/superbaseballman/drag-resize-canvas/releases/tag/v0.1.0
