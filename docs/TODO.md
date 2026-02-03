# TODO - Viz2d Future Development

## High Priority (P0) - Interactive Editing

### Shape Selection System
- [x] **Click to select** - Single shape selection on canvas click
- [x] **Multi-select with Shift** - Add to selection with Shift+Click
- [x] **Multi-select with Ctrl** - Toggle selection with Ctrl+Click
- [x] **Selection box** - Drag rectangle to select multiple shapes
- [x] **Crossing/Window selection** - Drag right = Window (fully inside), Drag left = Crossing (intersecting)
- [x] **Select All** - Ctrl+A to select all shapes
- [x] **Deselect** - Escape or click on empty canvas
- [x] **Visual feedback** - Highlight selected shapes with handles

### Shape Editing
- [x] **Control point handles** - Shape-specific control points for all 13 shape types
- [x] **Drag to modify** - Move control points to edit shape geometry (vertex, radius, curve handles)
- [x] **Move selected shapes** - Drag move handle to reposition
- [x] **Resize handles** - Corner/edge/radius handles for resizing
- [ ] **Rotation handle** - Rotate selected shapes
- [x] **Sync to code** - Update source code when shapes are edited

### Properties Panel
- [x] **Panel UI** - Floating/dockable panel showing shape properties
- [x] **Coordinate editing** - Edit X, Y, Width, Height, Radius, etc.
- [x] **Color picker** - Visual color selection for Stroke/Fill via ColorPickerDialog
- [x] **Thickness slider** - Adjust stroke thickness (0.5-20)
- [x] **Opacity slider** - Adjust shape opacity (0-100%)
- [x] **Visibility toggle** - Show/hide shapes with code sync (`IsVisible = false`)
- [x] **Name/ID display** - Show shape identifier and editable name with variable rename in code
- [x] **Style code sync** - All style property changes (Color, Fill, Weight, Opacity, Visible) persist as code lines
- [x] **Multi-selection** - Edit common style properties of multiple shapes
- [x] **Dock/Float toggle** - Switch between docked column and floating window
- [x] **Auto-deselect** - Selection cleared on Run and when clicking code editor

### Delete Shape
- [x] **Delete key** - Remove selected shapes
- [x] **Right-click context menu** - Delete option
- [x] **Code sync** - Remove corresponding code when shape deleted
- [ ] **Undo support** - Restore deleted shapes

---

## High Priority (P0) - Animation UI

### Timeline Panel
- [ ] **Timeline UI** - Visual timeline at bottom of window
- [ ] **Time ruler** - Displays time in seconds
- [ ] **Playhead** - Draggable position indicator
- [ ] **Shape tracks** - Row per animated shape
- [ ] **Keyframe markers** - Visual keyframe indicators
- [ ] **Duration handles** - Resize animation duration
- [ ] **Zoom timeline** - Zoom in/out on timeline

### Animation Preview
- [ ] **Play button** - Start animation playback
- [ ] **Pause button** - Pause at current frame
- [ ] **Stop button** - Reset to beginning
- [ ] **Loop toggle** - Enable/disable repeat
- [ ] **Speed control** - Playback speed slider (0.25x - 4x)
- [ ] **Frame stepping** - Step forward/backward one frame
- [ ] **Current time display** - Show current time position

---

## High Priority (P0) - Export

### DXF Export
- [ ] **DXF file format** - AutoCAD DXF R12/R14 format
- [ ] **Layer mapping** - Map shape types to DXF layers
- [ ] **Color mapping** - Map colors to DXF color indices
- [ ] **Line type support** - Solid, dashed, dotted
- [ ] **All shape types** - Export all supported shapes
- [ ] **Scale/units** - Configurable export units

### PDF Export
- [ ] **Vector PDF** - PDF/A format for archiving
- [ ] **Page size options** - A4, Letter, Custom
- [ ] **Margins** - Configurable page margins
- [ ] **Fit to page** - Auto-scale to fit
- [ ] **Multi-page** - Split large drawings across pages
- [ ] **Metadata** - Title, author, date

---

## Medium Priority (P1) - Geometry Operations

### Boolean Operations
- [ ] **Union** - Combine two or more polygons
- [ ] **Intersection** - Get overlapping area of polygons
- [ ] **Difference** - Subtract one polygon from another
- [ ] **XOR** - Symmetric difference
- [ ] **Clipper library** - Use Clipper2 for robust operations
- [ ] **API exposure** - VPolygon.Union(other), etc.

### Array/Pattern Operations
- [ ] **Linear array** - Repeat shape along vector
  ```csharp
  shape.LinearArray(direction, count, spacing);
  ```
- [ ] **Rectangular array** - Grid of copies
  ```csharp
  shape.RectangularArray(rows, cols, rowSpacing, colSpacing);
  ```
- [ ] **Circular array** - Copies around center point
  ```csharp
  shape.CircularArray(center, count, angleSpan);
  ```
- [ ] **Path array** - Distribute along curve
  ```csharp
  shape.PathArray(curve, count, alignToPath);
  ```

---

## Medium Priority (P1) - Bug Fixes & Performance

### Bug Fixes
- [ ] Test arc rendering for edge cases (360 arc, negative angles)
- [ ] Verify polygon rendering with self-intersecting polygons
- [ ] Test zoom limits at extreme scales

### Performance
- [ ] Optimize redraw for large shape counts (> 1000)
- [ ] Cache brushes instead of creating new ones per shape
- [ ] Implement shape culling for off-screen shapes

---

## Low Priority (P2) - Styling Enhancements

### Shape Styling
- [ ] **Dash patterns** - Dashed/dotted lines
  ```csharp
  line.DashPattern = "Dash"; // Dash, Dot, DashDot, DashDotDot
  ```
- [ ] **Line caps** - Round, Square, Flat
- [ ] **Line joins** - Miter, Bevel, Round
- [ ] **Gradient fills** - Linear and radial gradients
- [ ] **Pattern fills** - Hatch patterns (diagonal, cross, dots)

### Canvas Features
- [x] **Snap to grid** - Snap coordinates to grid intersections (F9 toggle, adaptive spacing)
- [ ] **Ruler display** - Show rulers along canvas edges
- [ ] **Zoom slider** - Visual zoom control in UI
- [ ] **Mini-map** - Overview of entire canvas

---

## Low Priority (P2) - Additional Features

### Export Features
- [ ] **Copy to clipboard** - As image or SVG

### Layer System
- [ ] **Named layers** - Create/rename layers
- [ ] **Visibility toggle** - Show/hide layers
- [ ] **Lock layers** - Prevent editing
- [ ] **Z-order** - Bring to front, send to back

### UI Enhancements
- [ ] **Customizable theme** - Light/Dark mode toggle
- [ ] **Full screen mode** - Maximize canvas
- [ ] **Undo/Redo for drawing** - Undo interactive drawing operations

---

## Technical Debt

### Code Quality
- [ ] Add XML documentation comments to all public APIs
- [ ] Add unit tests for geometry calculations
- [ ] Add integration tests for script execution
- [ ] Implement proper MVVM pattern

### Architecture
- [ ] Consider separating geometry library for reuse
- [ ] Add dependency injection for testability
- [ ] Implement plugin system for custom shapes

---

## Completed Features

### Shapes (14 total)
- [x] VPoint, VLine, VCircle, VRectangle, VEllipse, VArc
- [x] VPolygon, VPolyline, VBezier, VSpline
- [x] VArrow, VText, VDimension, VGroup

### Drawing Tools (12 total)
- [x] All shape types with click-based creation
- [x] Code generation for drawn shapes

### Snap System (9 types)
- [x] Endpoint, Midpoint, Center, Intersection, Perpendicular, Nearest, Extension, Tangent, Grid

### Animation System
- [x] Draw, Move, Rotate, Flip, FadeIn, FadeOut animations
- [x] Timeline class with easing functions
- [x] ObjectPropertyAnimation<T> for animating numeric properties on any object
- [x] Animator.Fps property (1-120, default 60) for frame rate control
- [x] CompositionTarget.Rendering-based animation loop (vsync-aligned)

### Export
- [x] PNG export
- [x] SVG export
- [x] GIF animation export

### Editor
- [x] Syntax highlighting (C# and F#)
- [x] Code completion and IntelliSense
- [x] Code folding and bracket matching
- [x] Code snippets

### Canvas
- [x] Zoom and pan
- [x] Grid and axes
- [x] Coordinate display
- [x] Measuring tool
- [x] Snap to grid (F9 toggle)
- [x] Crossing/Window selection (drag direction determines mode)
- [x] Shape ID counter reset on each execution

### Shape Editing
- [x] Shape-specific control points (13 shape types)
- [x] Drag control points to edit geometry
- [x] Code sync on drag end
- [x] Properties panel (floating/dockable)
- [x] Style property code sync (Color, FillColor, LineWeight, Opacity, IsVisible)
- [x] Variable rename from Properties panel
- [x] Auto-deselect on Run and editor click

---

## Notes

- Coordinate system: Mathematical (Y-up) - DO NOT CHANGE
- Grid spacing: Currently fixed at 50 units - make configurable
- Color parsing: Uses WPF ColorConverter - supports all named colors
- Script execution: Uses Roslyn - any C# syntax works
