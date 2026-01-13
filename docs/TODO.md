# TODO - Viz2d Future Development

## High Priority (P0)

### Bug Fixes
- [ ] Test arc rendering for edge cases (360° arc, negative angles)
- [ ] Verify polygon rendering with self-intersecting polygons
- [ ] Test zoom limits at extreme scales

### Performance
- [ ] Optimize redraw for large shape counts (> 1000)
- [ ] Cache brushes instead of creating new ones per shape
- [ ] Implement shape culling for off-screen shapes

---

## Medium Priority (P1)

### New Shapes
- [ ] **Bezier Curve** - Cubic Bezier with 4 control points
  ```csharp
  Bezier bezier = new Bezier(p0, p1, p2, p3);
  bezier.Draw();
  ```
- [ ] **Spline** - Smooth curve through multiple points
  ```csharp
  Spline spline = new Spline(points);
  spline.Draw();
  ```
- [ ] **Text** - Text labels at specified positions
  ```csharp
  Text text = new Text("Label", x, y);
  text.FontSize = 14;
  text.Draw();
  ```

### Shape Styling
- [ ] **Dash patterns** - Dashed/dotted lines
  ```csharp
  line.DashPattern = "Dash"; // Dash, Dot, DashDot, DashDotDot
  ```
- [ ] **Opacity** - Transparency support
  ```csharp
  circle.Opacity = 0.5;
  ```
- [ ] **Arrow heads** - For lines and polylines
  ```csharp
  line.StartArrow = true;
  line.EndArrow = true;
  ```

### Canvas Features
- [ ] **Snap to grid** - Snap coordinates to grid intersections
- [ ] **Ruler display** - Show rulers along canvas edges
- [ ] **Zoom slider** - Visual zoom control in UI
- [ ] **Mini-map** - Overview of entire canvas

### Editor Features
- [ ] **Autocomplete** - IntelliSense for geometry classes
- [ ] **Error highlighting** - Red squiggles for errors
- [ ] **Code snippets** - Quick templates for common shapes
- [ ] **Undo/Redo** - Already in AvalonEdit, just expose

---

## Low Priority (P2)

### Export Features
- [ ] **SVG export** - Vector graphics format
- [ ] **DXF export** - CAD interchange format
- [ ] **Copy to clipboard** - As image or SVG

### Advanced Features
- [ ] **Shape selection** - Click to select shapes
- [ ] **Properties panel** - View/edit selected shape properties
- [ ] **Layer support** - Organize shapes into layers
- [ ] **Animation** - Animate shape properties over time

### Code Features
- [ ] **Multiple files** - Tab-based file editing
- [ ] **Code templates** - Predefined starting templates
- [ ] **Share/Import** - Share code snippets

### UI Enhancements
- [ ] **Customizable theme** - Light/Dark mode toggle
- [ ] **Resizable panels** - Remember panel sizes
- [ ] **Full screen mode** - Maximize canvas
- [ ] **Recent files** - Quick access to recent files

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

## Ideas for Future Versions

### Version 1.1
- Bezier curves and splines
- Basic autocomplete
- SVG export

### Version 1.2
- Shape selection and properties panel
- Layers support
- Animation basics

### Version 2.0
- Full CAD-like features
- Constraints system
- Parametric shapes

---

## Notes

- Coordinate system: Mathematical (Y-up) - DO NOT CHANGE
- Grid spacing: Currently fixed at 50 units - make configurable
- Color parsing: Uses WPF ColorConverter - supports all named colors
- Script execution: Uses Roslyn - any C# syntax works
