---
name: code2viz
description: "Create and visualize 2D geometry on an interactive canvas using C# code. Use this skill when the user asks to draw shapes, create geometric visualizations, generate 2D diagrams, or visualize geometry. Supports points, lines, circles, arcs, rectangles, ellipses, polygons, polylines, beziers, splines, arrows, text, groups, grids, dimensions, regions, hatches (pattern fills), and construction lines. Shapes auto-register when created. Coordinate system is mathematical (Y-up, origin at center)."
---

# Code2Viz - 2D Geometry Visualization

You have access to the Code2Viz MCP server which lets you create and visualize 2D geometry on an interactive canvas. The Code2Viz WPF application must be running for tools to work.

> **Sub-project: Animator.** Code2Viz ships with a sibling app `Animator.exe` (folder: `Animator/`) for p5.js-style animation sketches with `Setup()`/`Draw()` and `C2VGeometry`. The MCP tools below target Code2Viz only — Animator is launched from Code2Viz via the **Switch to Animator** button or from a desktop shortcut. See the Animator section near the end of this skill for the sketch API.

## Available Tools

### execute_vizcode
Insert and execute C# code as the body of `Main()`. Code appears in the editor so the user can see and modify it.

### clear_canvas
Remove all shapes from the canvas.

### get_canvas_state
Get a JSON snapshot of all shapes on the canvas (type, name, color, bounds).

### export_png
Export the current canvas to a PNG image file.

### get_console_output
Retrieve console output from the last code execution.

### get_project_context
Get all C# source files in the current project as JSON. Returns each file's name, content, and whether it is the entry point. **Call this first** when working with an existing project to discover custom classes, helpers, and data defined across multiple files.

### update_file
Create or update a source file in the project. Pass a file name (e.g. `Room.cs`) and its full content. If the file exists it is replaced; if not, a new file is created. The editor refreshes automatically. Use this for helper classes and utility code — for the entry point (`StartViz.cs`), use `execute_vizcode` instead.

## Quick Start

To draw shapes, call `execute_vizcode` with C# code. **Always assign shapes to variables** — unnamed shapes are hidden by the animation system.

```csharp
// CORRECT - assign to variable
var circle = new VCircle(0, 0, 100) { Color = "Red", FillColor = "Yellow" };
var line = new VLine(-100, -100, 100, 100) { Color = "Green", LineWeight = 3 };

// WRONG - shapes will be hidden (no variable name)
// new VCircle(0, 0, 100);
```

### When auto-naming fails — set `Name` explicitly

Code2Viz auto-fills `Shape.Name` from the variable name for `var x = new VShape(...)` declarations and field declarations only. After the script runs, any shape with empty `Name` (and not explicitly `.Draw()`-ed) is hidden. These patterns slip past the rewriter and need an explicit `Name`:

```csharp
// list.Add: rewriter does not see the construction
var trails = new List<VLine>();
trails.Add(new VLine(0, 0, 100, 100) { Color = "Cyan", Name = "trail" });

// array slot assignment: not a var declaration
var hulls = new VPolygon[3];
hulls[0] = new VPolygon(pts) { Color = "Lime", Name = "hull0" };

// helper function return: the returned shape has no variable name
VLine MakeTrail(VPoint a, VPoint b) =>
    new VLine(a, b) { Color = "Gold", Name = "trail" };
```

The Code2Viz console will warn: `Warning: N unnamed shape(s) hidden (...)`. If you see it, add `Name = "..."` to the construction.

## Coordinate System

- Origin (0,0) is at the canvas center
- Y-axis points UP (mathematical, not screen)
- All units are in canvas units (no specific real-world unit)

## Available Imports (auto-included)

```csharp
using System;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;
using C2VGeometry;
using Code2Viz.Console;
using Code2Viz.Animation;
```

## Shapes

### VXYZ (Coordinate Type)
VXYZ is the coordinate type used for all positions, vectors, and parameters (similar to Revit's XYZ). VPoint is a separate drawable shape (visible marker on canvas).

```csharp
new VXYZ(x, y);        // Z defaults to 0
new VXYZ(x, y, z);     // explicit Z
VXYZ.Zero;              // (0, 0, 0)
VXYZ.BasisX;            // (1, 0, 0)
VXYZ.BasisY;            // (0, 1, 0)
// Properties: X, Y, Z
// Operators: +, -, *, /, unary -, ==, !=
```

| Method | Returns | Description |
|--------|---------|-------------|
| `GetLength()` | double | Magnitude |
| `Normalize()` | VXYZ | Unit vector |
| `DotProduct(other)` | double | Dot product |
| `CrossProduct(other)` | VXYZ | Cross product |
| `AngleTo(other)` | double | Angle in radians |
| `DistanceTo(other)` | double | Distance |
| `AsVPoint()` | VPoint | Convert to drawable VPoint (drops Z) |
| `Rotate(angleDeg)` | VXYZ | Rotate around Z-axis by angle in degrees |
| `IsZeroLength()` | bool | Check if zero |
| `IsUnitLength()` | bool | Check if unit length |

### VPoint (Drawable Point Marker)
VPoint is a visible point marker drawn on the canvas. For coordinates and vectors, use VXYZ instead.

```csharp
new VPoint(x, y);
// Properties: X, Y (read-only position)
// Methods: DistanceTo(VPoint other), AsVXYZ(), PolarPoint(angleDeg, distance)
// Operators: +, -, *, / (with VPoint, VXYZ, or double)

// PolarPoint: create a new point at angle and distance from this point
var center = new VPoint(0, 0);
var p = center.PolarPoint(45, 100);  // point at 45 degrees, distance 100
```

### VLine
```csharp
new VLine(x1, y1, x2, y2);
new VLine(VXYZ start, VXYZ end);
new VLine(VXYZ startPoint, angleInDegrees, length);  // from point + angle + length
// Properties: Start, End, MidPoint, Direction (all VXYZ)
```

### VCircle
```csharp
new VCircle(centerX, centerY, radius);
new VCircle(VXYZ center, radius);                      // VXYZ center
new VCircle(VXYZ p1, VXYZ p2, VXYZ p3);               // circumcircle through 3 points
VCircle.FromCenterDiameter(VXYZ center, diameter);     // from center + diameter
VCircle.FromCenterDiameter(cx, cy, diameter);          // from coordinates + diameter
VCircle.FromTwoPoints(VXYZ p1, VXYZ p2);              // circle with p1,p2 as diameter endpoints
// Properties: Center (VXYZ), Radius, Area, Circumference
```

### VArc
```csharp
new VArc(centerX, centerY, radius, startAngleDeg, endAngleDeg);
new VArc(VXYZ center, radius, startAngleDeg, endAngleDeg);  // VXYZ center
new VArc(VXYZ start, VXYZ mid, VXYZ end);                   // arc through 3 points
// Counter-clockwise from start to end

// Factory methods
VArc.FromStartCenterEnd(start, center, end);
VArc.FromCenterStartEnd(center, start, end);
VArc.FromStartCenterAngle(start, center, sweepAngleDeg);
VArc.FromCenterStartAngle(center, start, sweepAngleDeg);
VArc.FromStartCenterLength(start, center, arcLength);
VArc.FromCenterStartLength(center, start, arcLength);
VArc.FromStartEndRadius(start, end, radius, largeArc: false);
VArc.FromStartEndAngle(start, end, sweepAngleDeg);
VArc.Continue(previousCurve, arcLength);  // tangent continuation from ICurve
```

### VRectangle
```csharp
new VRectangle(x, y, width, height);           // from bottom-left corner
new VRectangle(x1, y1, x2, y2, fromCorners: true); // from two corners
```

### VEllipse
```csharp
new VEllipse(centerX, centerY, radiusX, radiusY);
new VEllipse(VXYZ center, radiusX, radiusY);                      // VXYZ center
new VEllipse(VXYZ center, radiusX, radiusY, startAngle, endAngle);  // partial ellipse (angles in degrees)
// Properties: Center (VXYZ), RadiusX, RadiusY, StartAngle (default 0), EndAngle (default 360), Area, Circumference
```

### VPolygon
```csharp
new VPolygon(new VXYZ(0,100), new VXYZ(95,31), new VXYZ(59,-81), new VXYZ(-59,-81), new VXYZ(-95,31));
new VPolygon(List<VXYZ> points);
new VPolygon(List<ICurve> curves);  // from ordered curves forming closed loop
// Auto-closes
// Properties: Points (List<VXYZ>), Area, SignedArea (positive=CCW, negative=CW)
// Methods: AddPoint(VXYZ point), AddPoint(x, y)
// Slice: polygon.Slice(point1, point2) / polygon.Slice(xline) / polygon.Slice(ray) — returns List<VPolygon>
```

### VPolyline
```csharp
new VPolyline(new VXYZ(0,0), new VXYZ(50,50), new VXYZ(100,0));
// Open path
```

### VBezier
```csharp
new VBezier(x1,y1, cx1,cy1, cx2,cy2, x2,y2);
```

### VSpline
```csharp
new VSpline(new VXYZ(0,0), new VXYZ(50,80), new VXYZ(100,0), new VXYZ(150,80));
// Catmull-Rom through all points
// Properties: ControlPoints, SegmentsPerSpan (default 16), Tension (default 0.5, range 0=sharp to 1=loose)
```

### VText
```csharp
new VText(x, y, "Hello World");
new VText(x, y, "Big Text", 24);   // with font height

// Anchor controls alignment at position
var t = new VText(0, 0, "Centered", 20);
t.Anchor = VTextAnchor.MiddleCenter;  // center text on position

// Angle rotates the entire text block (CCW degrees around Location)
var vertical = new VText(0, 0, "Vertical", 16);
vertical.Angle = 90;  // reads bottom-to-top
```

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| Content | string | | Text content |
| Height | double | 12 | Font height |
| Width | double | 0 | Text width (0 = auto) |
| Font | VFont | Arial | Font family |
| FontWeight | VFontWeight | Normal | Normal or Bold |
| Anchor | VTextAnchor | BottomLeft | Text anchor/alignment point |
| Angle | double | 0 | Rotation in degrees, CCW around Location (Excel-style block rotation) |

**VFont enum values**: Arial, TimesNewRoman, CourierNew, Verdana, Georgia, Tahoma, TrebuchetMS, Consolas, Calibri, Cambria, SegoeUI, ComicSansMS, Impact, LucidaConsole

**VTextAnchor enum values**: `BottomLeft` (default), `BottomCenter`, `BottomRight`, `MiddleLeft`, `MiddleCenter`, `MiddleRight`, `TopLeft`, `TopCenter`, `TopRight`

`VText.DoesIntersect(other)` is text-aware: it tests the text's (possibly rotated, anchor-aware) bounding quad against the other shape's bounding box using the Separating Axis Theorem. The fallback in `Shape.DoesIntersect` mirrors the test, so `other.DoesIntersect(text)` returns the same result.

### VArrow
```csharp
new VArrow(x1, y1, x2, y2);
new VArrow(startPoint, endPoint);
new VArrow(startPoint, direction, length);  // from point + direction vector + length
```

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| HeadLength | double | 15 | Arrowhead length |
| HeadAngle | double | 30 | Arrowhead angle (degrees) |
| DoubleEnded | bool | false | Arrowhead at both ends |
| MidPoint | VXYZ | | Midpoint (read-only) |

### VGroup
```csharp
var g = new VGroup(shape1, shape2, shape3);
var g = new VGroup(listOfShapes);
var g = new VGroup(); // empty, then add
```

| Method | Returns | Description |
|--------|---------|-------------|
| `Add(shape)` | VGroup | Add shape (fluent) |
| `AddRange(shapes)` | VGroup | Add multiple (fluent) |
| `Remove(shape)` | bool | Remove shape |
| `RemoveAt(index)` | void | Remove by index |
| `Clear()` | void | Remove all |
| `ContainsShape(shape)` | bool | Check containment |
| `GetCenter()` | VXYZ | Group center |
| `GetShapesOfType<T>()` | IEnumerable\<T\> | Filter by type |
| `Flatten()` | List\<Shape\> | Flatten nested groups |
| `ForEach(action)` | VGroup | Apply action (fluent) |
| `Where(predicate)` | VGroup | Filter shapes (fluent) |
| `ApplyColor()` | VGroup | Apply group Color to all (fluent) |
| `ApplyFillColor()` | VGroup | Apply group FillColor to all (fluent) |
| `ApplyLineWeight()` | VGroup | Apply group LineWeight to all (fluent) |
| `ApplyStyle()` | VGroup | Apply all styling (fluent) |
| `SetOpacity(val)` | VGroup | Set opacity on all (fluent) |

Properties: `Shapes` (List\<Shape\>), `Count` (int), indexer `[int]`

### VGrid
```csharp
new VGrid(new VXYZ(0,0), xCount: 5, yCount: 5, xSpacing: 20, ySpacing: 20, centered: true);
// Location property is VXYZ, but grid creates VPoint shapes (drawable markers)
// Properties: Points (List<VPoint>), Location (VXYZ)
```

### VCell
```csharp
// Created by VSpatialGrid, not directly
// Properties: UniqueId (int), Neighbours (List<VCell>), Center (VXYZ), CellSize (double)
// Column (int), Row (int), Blocked (bool)
// Extends VPolygon (square boundary)
```

### VSpatialGrid
```csharp
new VSpatialGrid(new VXYZ(0, 0), xCount: 10, yCount: 10, cellSize: 5.0);
// Location = center of bottom-left cell
// Properties: Cells (List<VCell>), Location (VXYZ), XCount, YCount, CellSize, Count
// Indexers: [index], [col, row]
```

| Method | Return | Description |
|--------|--------|-------------|
| `FindPath(start, end)` | `List<VCell>` | A* shortest path (respects Blocked cells) |
| `GetClosestCell(point)` | `VCell` | O(log n) nearest cell via KD-tree |
| `GetCellAt(point)` | `VCell?` | Cell containing point, or null |
| `GetRow(row)` | `List<VCell>` | All cells in a row |
| `GetColumn(col)` | `List<VCell>` | All cells in a column |
| `GetCenter()` | `VXYZ` | Grid center point |
| `ApplyStyle()` | `void` | Propagate style to all cells |

### VDimension
```csharp
new VDimension(x1, y1, x2, y2);
new VDimension(point1, point2);
```

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| Offset | double | 20 | Dimension line offset from points |
| ExtensionLength | double | 10 | Extension line length |
| ArrowSize | double | 8 | Arrowhead size |
| CustomText | string? | null | Custom text (null = distance) |
| DecimalPlaces | int | 2 | Distance decimal places |
| TextHeight | double | 12 | Text height |
| ExtendBeyondDimLines | double | 1.25 | Extension past dimension line |
| OffsetFromOrigin | double | 0.625 | Gap from origin to extension start |
| SuppressExtLine1 | bool | false | Hide first extension line |
| SuppressExtLine2 | bool | false | Hide second extension line |
| SuppressDimensionLine | bool | false | Hide dimension line and arrowheads |
| Prefix | string | "" | Text prefix (e.g. "L=") |
| Suffix | string | "" | Text suffix (e.g. "mm") |
| TextBackgroundOpaque | bool | false | Opaque background behind dimension text |
| ExtensionLineColor | string? | null | Color for extension lines (null = use Color) |
| DimensionLineColor | string? | null | Color for dimension line & arrowheads (null = use Color) |
| TextColor | string? | null | Color for dimension text (null = use Color) |
| Distance | double | | Calculated distance (read-only) |
| DisplayText | string | | Display text with prefix/suffix (read-only) |

### VRadialDimension
```csharp
new VRadialDimension(circle);          // from VCircle
new VRadialDimension(arc);             // from VArc
new VRadialDimension(VXYZ center, radius);  // from VXYZ + radius
```

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| Center | VXYZ | | Center of circle/arc |
| Radius | double | | Radius of circle/arc |
| LeaderAngle | double | 45 | Angle of leader line (degrees) |
| ShowDiameter | bool | false | Show diameter instead of radius |
| ArrowSize | double | 8 | Arrowhead size |
| TextHeight | double | 12 | Text height |
| DecimalPlaces | int | 2 | Value decimal places |
| Prefix | string | "" | Text prefix |
| Suffix | string | "" | Text suffix |
| CustomText | string? | null | Custom text (null = auto R/dia) |
| TextBackgroundOpaque | bool | false | Opaque background behind text |
| DimensionLineColor | string? | null | Leader line & arrowhead color |
| TextColor | string? | null | Text color |
| Value | double | | Radius or diameter value (read-only) |
| DisplayText | string | | Display text with symbol (read-only) |

### VXLine (infinite construction line)
```csharp
new VXLine(VXYZ basePoint, VXYZ direction);  // VXYZ + VXYZ direction
new VXLine(VXYZ point1, VXYZ point2);      // through two VXYZ points
new VXLine(x1, y1, x2, y2);           // through two coordinate pairs
VXLine.Horizontal(y);                  // horizontal through y
VXLine.Vertical(x);                    // vertical through x
```

### VRay (semi-infinite ray)
```csharp
new VRay(VXYZ origin, VXYZ direction);      // VXYZ origin + VXYZ direction
new VRay(VXYZ origin, VXYZ throughPoint);  // VXYZ origin + VXYZ it passes through
new VRay(ox, oy, tx, ty);              // coordinates for origin and through-point
VRay.AtAngle(VXYZ origin, angleDeg);   // ray from VXYZ origin at angle
VRay.HorizontalRight(VXYZ origin);     // ray pointing right
VRay.HorizontalLeft(VXYZ origin);      // ray pointing left
VRay.VerticalUp(VXYZ origin);          // ray pointing up
VRay.VerticalDown(VXYZ origin);        // ray pointing down
// Properties: Origin (VXYZ), Direction (VXYZ), RenderExtent (default 10000)
// Methods: GetPointAtDistance(distance), ContainsPoint(point), ToFiniteLine(), ToXLine()
```

### Region
A Region represents an enclosed 2D area bounded by curves (lines, arcs, beziers, splines). Unlike VPolygon which only supports straight edges, Region preserves the original curve geometry. Supports holes and boolean operations.

```csharp
// From a list of curves forming a closed loop (auto-orders and validates)
var region = new Region(new List<ICurve> {
    new VLine(new VXYZ(0, 0), new VXYZ(10, 0)),
    VArc.FromStartEndRadius(new VXYZ(10, 0), new VXYZ(10, 10), 5),
    new VLine(new VXYZ(10, 10), new VXYZ(0, 10)),
    new VLine(new VXYZ(0, 10), new VXYZ(0, 0))
});

// With holes
var regionWithHole = new Region(outerCurves, new List<List<ICurve>> { holeCurves });

// From existing polygon
var regionFromPoly = Region.FromPolygon(polygon);
var regionFromPwh = Region.FromPolygonWithHoles(pwh);
```

| Property | Type | Description |
|----------|------|-------------|
| OuterLoop | List\<ICurve\> | Outer boundary curves |
| Holes | List\<List\<ICurve\>\> | Inner hole boundaries |
| Area | double | Outer area minus hole areas |
| SignedArea | double | Signed area of outer loop (positive=CCW) |
| Perimeter | double | Total perimeter (outer + holes) |

| Method | Returns | Description |
|--------|---------|-------------|
| `AddHole(curves)` | void | Add a hole (list of curves forming closed loop) |
| `Contains(VXYZ point)` | bool | Point inside region (outside holes) |
| `ToPolygon()` | VPolygon | Low-fidelity polygon (endpoints only) |
| `ToPolygonHighRes(n)` | VPolygon | High-fidelity polygon (n segments/curve) |
| `ToPolygonWithHoles(n)` | PolygonWithHoles | With holes, high-fidelity |
| `FromPolygon(poly)` | Region | Create from VPolygon (static) |
| `FromPolygonWithHoles(pwh)` | Region | Create from PolygonWithHoles (static) |

### VHatch (Pattern Fill)
Fills a closed polygon boundary with a repeating hatch pattern. Supports 73 built-in AutoCAD-standard patterns and custom patterns from `.pat` format strings.

```csharp
// Built-in pattern with enum
var rect = new VRectangle(0, 0, 100, 80);
var hatch = new VHatch(rect, BuiltInHatch.ANSI31, scale: 10);
hatch.Color = "Cyan";

// Built-in pattern by name
var hatch2 = new VHatch(rect, "BRICK", scale: 5, angle: 45);

// Custom pattern from string (.pat format)
var custom = VHatch.FromDefinition(rect, @"
  *CROSSHATCH, Custom crosshatch
  0, 0,0, 0,10
  90, 0,0, 0,10
", scale: 1.0);

// Custom HatchType object
var pattern = new HatchType("MyPat", "Diagonal", new List<HatchPatternLine> {
    new HatchPatternLine(45, 0, 0, 0, 5),
    new HatchPatternLine(135, 0, 0, 0, 5)
});
var hatch3 = new VHatch(rect, pattern, scale: 2.0);
```

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| Boundary | List\<VXYZ\> | - | Closed boundary polygon points |
| Pattern | HatchType | - | Hatch pattern definition |
| PatternScale | double | 1.0 | Scale factor for the pattern |
| PatternAngle | double | 0 | Additional rotation (degrees) |
| Color | string | "Cyan" | Hatch line color |
| LineWeight | double | 1.0 | Hatch line thickness |

| Method | Returns | Description |
|--------|---------|-------------|
| `GenerateLines()` | List\<(VXYZ,VXYZ)\> | Generate clipped line segments |
| `FromDefinition(poly, patString, scale, angle)` | VHatch | Create from .pat format string (static) |

**Built-in patterns (BuiltInHatch enum)**: SOLID, ANGLE, ANSI31-ANSI38, AR_B816, AR_BRSTD, AR_CONC, AR_HBONE, AR_SAND, BOX, BRASS, BRICK, BRSTONE, CLAY, CORK, CROSS, DASH, DOTS, EARTH, ESCHER, GRASS, GRATE, HEX, HONEY, LINE, NET, NET3, SQUARE, STARS, STEEL, TRIANG, ZIGZAG, and more (73 total). Use `BuiltInHatches.GetAllNames()` to list all.

### Chart (Charts & Graphs)
Build Chart.js-style charts from your data. Each method returns a `VGroup` containing axes, gridlines, ticks, tick labels and the data shapes. The chart auto-fits its axis range to the data, picks "nice" round-number tick spacing, and uses a 10-color palette for series / bars / slices. The whole chart can be moved/rotated/scaled as one unit because it is a VGroup.

| Chart.* Method | Returns | Description |
|--------|---------|-------------|
| `Bar(labels, values, opts)` | VGroup | Categorical bars with numeric Y axis |
| `Line(xs, ys, opts)` | VGroup | Line chart with point markers |
| `Scatter(points, opts)` | VGroup | Scatter plot from VXYZ points |
| `Pie(values, labels?, opts)` | VGroup | Pie chart (no axes); polygon-approximated sectors |
| `Area(xs, ys, opts)` | VGroup | Filled area chart with stroked top edge |

| ChartOptions Property | Default | Description |
|----------|------|-------------|
| Origin | (0, 0) | Bottom-left of plot area in world coords |
| Width / Height | 400 / 250 | Plot area size |
| Title | null | Chart title (above plot) |
| XAxisTitle / YAxisTitle | null | Axis titles |
| XMin/XMax/YMin/YMax | null (auto-fit) | Pin a fixed axis range |
| XTickCount / YTickCount | 6 / 6 | Approximate tick count |
| ShowGrid | true | Light gridlines |
| XLabelRotation | 0 | Rotate X tick labels (good for long category names) |
| LabelFontSize / TitleFontSize | 10 / 14 | Text sizes |
| AxisColor / GridColor / TextColor | "White" / "DimGray" / "White" | Colors |
| Palette | 10-color qualitative | Series/bar/slice colors |
| TickDecimalPlaces | null (auto) | Numeric tick precision |

#### Bar — categorical values with a numeric Y axis

```csharp
var labels = new[] { "Q1", "Q2", "Q3", "Q4" };
var values = new[] { 120.0, 150, 95, 180 };

var revenue = Chart.Bar(labels, values, new ChartOptions
{
    Origin = new VXYZ(-250, -150),
    Width = 500,
    Height = 300,
    Title = "Quarterly Revenue (M$)",
    YAxisTitle = "Revenue",
    YMin = 0,                       // pin Y to zero (otherwise auto-fits)
    TickDecimalPlaces = 0
});
```

#### Line — computed time series, auto-fit ranges

```csharp
var xs = Enumerable.Range(0, 60).Select(i => i * 0.1).ToArray();
var ys = xs.Select(x => Math.Exp(-0.3 * x) * Math.Sin(2 * x)).ToArray();

var trace = Chart.Line(xs, ys, new ChartOptions
{
    Origin = new VXYZ(-300, -150),
    Width = 600,
    Height = 300,
    Title = "Damped Oscillator",
    XAxisTitle = "Time (s)",
    YAxisTitle = "Amplitude"
});
```

#### Scatter — correlated random sample

```csharp
var rng = new Random(42);
var sample = Enumerable.Range(0, 80).Select(_ =>
{
    double age = rng.NextDouble() * 40 + 20;
    double height = age * 0.4 + 150 + rng.NextDouble() * 20;
    return new VXYZ(age, height);
}).ToArray();

var scatter = Chart.Scatter(sample, new ChartOptions
{
    Origin = new VXYZ(-250, -150),
    Width = 500,
    Height = 300,
    Title = "Height vs Age",
    XAxisTitle = "Age",
    YAxisTitle = "Height (cm)"
});
```

#### Pie — named slices, custom palette

```csharp
var share    = new[] { 64.7, 19.5, 9.3, 3.5, 3.0 };
var browsers = new[] { "Chrome", "Safari", "Edge", "Firefox", "Other" };

var pie = Chart.Pie(share, browsers, new ChartOptions
{
    Origin = new VXYZ(-150, -150),
    Width = 300,
    Height = 300,
    Title = "Browser Market Share",
    Palette = new[] { "DodgerBlue", "Tomato", "MediumSeaGreen", "Gold", "Gray" }
});
```

#### Area — filled trend with axis titles

```csharp
var months = Enumerable.Range(0, 12).Select(i => (double)(i + 1)).ToArray();
var mau    = new[] { 4.2, 5.1, 6.0, 7.3, 8.1, 8.8, 9.4, 9.7, 10.2, 10.5, 11.0, 11.6 };

var growth = Chart.Area(months, mau, new ChartOptions
{
    Origin = new VXYZ(-300, -150),
    Width = 600,
    Height = 300,
    Title = "Monthly Active Users",
    XAxisTitle = "Month",
    YAxisTitle = "MAU (millions)",
    YMin = 0
});

// The chart is a VGroup — move/rotate/scale as one unit
growth.Move(new VXYZ(0, 50));
```

## Shape Properties (all shapes)

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| Color | string | "Cyan" | Stroke color (named color or hex) |
| FillColor | string | "Transparent" | Fill color |
| LineWeight | double | 2 | Stroke width |
| LineType | LineType | Continuous | Stroke style (see enum below) |
| LineTypeScale | double | 1.0 | Dash pattern scale |
| Name | string | "" | Identifier |
| IsVisible | bool | true | Visibility |
| Opacity | double | 1.0 | Transparency (0 to 1) |
| Id | long | | Unique ID (read-only) |

**LineType enum values**: `Continuous`, `Dashed`, `Dotted`, `DashDot`, `DashDotDot`, `Center`, `Phantom`, `Hidden`

## Shape Methods (all shapes)

| Method | Description |
|--------|-------------|
| `Move(new VXYZ(dx, dy, 0))` | Translate by vector |
| `Rotate(VXYZ pivot, angleDeg)` | Rotate around a VXYZ pivot |
| `Scale(VXYZ center, factor)` | Scale around a VXYZ center |
| `Flip(mirrorLine)` | Mirror across a VLine |
| `Clone()` | Deep copy (returns same type, no cast needed) |
| `GetBounds()` | Returns `BoundingBox` with `Min`, `Max`, `Width`, `Height`, `Center`, `Area` |
| `Show()` | Make visible |
| `Hide()` | Make invisible |
| `Remove()` | Remove from canvas |
| `BringAbove(otherShape)` | Move above another shape in draw order (renders on top) |
| `SendBehind(otherShape)` | Move behind another shape in draw order (renders underneath) |
| `Contains(VXYZ point)` | Check if point is inside shape |
| `DistanceTo(VXYZ point)` | Distance from shape to point |
| `DoesIntersect(other)` | Check intersection with another shape |
| `Intersect(other)` | Get intersection shape (or null) |

```csharp
// Move examples
shape.Move(new VXYZ(50, 0, 0));      // move right 50
shape.Move(new VXYZ(0, 100, 0));     // move up 100

// Rotate examples
shape.Rotate(new VXYZ(0, 0), 45);   // rotate 45 degrees around origin

// Scale examples
shape.Scale(new VXYZ(0, 0), 2.0);   // double size around origin

// Flip examples
shape.Flip(new VLine(0, 0, 0, 100)); // flip across Y axis
shape.Flip(new VLine(0, 0, 100, 0)); // flip across X axis

// GetBounds example
BoundingBox bounds = shape.GetBounds();
VXYZ min = bounds.Min;        // lower-left corner
VXYZ max = bounds.Max;        // upper-right corner
double w = bounds.Width;      // width
double h = bounds.Height;     // height
VXYZ c = bounds.Center;       // center point
```

## BoundingBox

Returned by `GetBounds()` method on all shapes.

### Properties
| Property | Type | Description |
|----------|------|-------------|
| Min | VXYZ | Lower-left corner |
| Max | VXYZ | Upper-right corner |
| Width | double | Max.X - Min.X |
| Height | double | Max.Y - Min.Y |
| Center | VXYZ | Center point |
| Area | double | Width * Height |

### Methods
| Method | Returns | Description |
|--------|---------|-------------|
| `Contains(point)` | bool | Point inside bounding box |
| `Intersects(other)` | bool | Overlaps with another BoundingBox |
| `Union(other)` | BoundingBox | Combined bounding box |
| `Expand(distance)` | BoundingBox | Expanded by distance |

```csharp
BoundingBox bounds = circle.GetBounds();
bool hit = bounds.Contains(new VXYZ(10, 10));
BoundingBox combined = bounds.Union(otherBounds);
var (min, max) = bounds;  // tuple deconstruction
```

## ICurve Interface

Implemented by: VLine, VCircle, VArc, VEllipse, VPolyline, VPolygon, VBezier, VSpline

### Properties
| Property | Type | Description |
|----------|------|-------------|
| StartPoint | VXYZ | Start point of curve |
| EndPoint | VXYZ | End point (same as start for closed curves) |
| Vertices | List\<VXYZ\> | Key vertices/control points |
| SelfIntersecting | bool | Whether curve self-intersects |

### Methods
| Method | Returns | Description |
|--------|---------|-------------|
| `GetLength()` | double | Total arc length |
| `Divide(n)` | List\<VXYZ\> | Divide into n equal segments |
| `Measure(segmentLength)` | List\<VXYZ\> | Points at fixed distance intervals |
| `PointAtSegmentLength(len)` | VXYZ | Point at distance along curve |
| `PointAtParameter(t)` | VXYZ | Point at parameter (0.0 to 1.0) |
| `ParameterAtPoint(point)` | double | Get parameter (0-1) for closest point on curve |
| `Project(point)` | VXYZ | Closest point on curve |
| `Offset(distance)` | ICurve | Parallel offset curve |
| `Offset(List<double> distances)` | List\<ICurve\> | Multiple offsets |
| `Intersect(otherCurve)` | IntersectionResult | Find intersection points |
| `SplitAtPoint(point)` | (ICurve, ICurve) | Split curve at point |
| `SetBounds(startParam, endParam)` | void | Trim in place to parameter sub-range; new [0,1] spans [startParam, endParam] |
| `NormalAtPoint(point)` | VXYZ | Normal vector at point |
| `PointsAtChordLengthFromPoint(point, chordLength)` | List\<VXYZ\> | Points at chord distance |

**SetBounds notes:** Parameters are clamped to [0,1] and swapped if reversed. Supported for VLine, VArc, VEllipse, VPolyline, VBezier, VSpline. **Throws `NotSupportedException`** for closed/infinite curves whose trimmed form changes type: VCircle (→arc), VPolygon (→polyline), VRay/VXLine (→line). Use `SplitAtPoint` on those instead. VSpline resamples densely so the trimmed Catmull-Rom tracks the original path closely; VBezier uses De Casteljau for an exact trim.

### IntersectionResult
| Property | Type | Description |
|----------|------|-------------|
| Points | List\<VXYZ\> | Intersection points |
| Curves | List\<ICurve\> | Overlapping curve segments |
| HasIntersection | bool | Any intersection exists |
| IsSinglePoint | bool | Exactly one point |
| HasOverlap | bool | Has overlapping segments |
| Count | int | Total elements |

## VXYZ (3D Vector)

```csharp
new VXYZ(x, y, 0)      // Z is unused in 2D, set to 0
VXYZ.Zero               // (0, 0, 0)
VXYZ.BasisX             // (1, 0, 0)
VXYZ.BasisY             // (0, 1, 0)
```

| Method | Returns | Description |
|--------|---------|-------------|
| `GetLength()` | double | Magnitude |
| `Normalize()` | VXYZ | Unit vector |
| `DotProduct(other)` | double | Dot product |
| `CrossProduct(other)` | VXYZ | Cross product |
| `AngleTo(other)` | double | Angle in radians |
| `DistanceTo(other)` | double | Distance |
| `AsVPoint()` | VPoint | Convert to VPoint (drops Z) |
| `Rotate(angleDeg)` | VXYZ | Rotate around Z-axis by angle in degrees |
| `IsZeroLength()` | bool | Check if zero |
| `IsUnitLength()` | bool | Check if unit length |

Operators: `+`, `-`, `*`, `/`, unary `-`, `==`, `!=`

## Array Operations

Extension methods for creating patterns from shapes. All return `List<Shape>`.

```csharp
// Linear arrays
var row = shape.LinearArrayX(5, 30);     // 5 copies, 30 apart in X
var col = shape.LinearArrayY(5, 30);     // 5 copies, 30 apart in Y
var arr = shape.LinearArray(new VXYZ(1, 1, 0), 5, 30); // diagonal

// Rectangular array
var grid = shape.RectangularArray(3, 4, 40, 40); // 3 rows, 4 cols

// Circular array
var ring = shape.CircularArray(new VXYZ(0, 0), 8);          // 8 copies, full circle
var arc = shape.CircularArray(new VXYZ(0, 0), 6, 180);      // 6 copies, half circle
var noRot = shape.CircularArray(new VXYZ(0, 0), 8, 360, false); // don't rotate items

// Path array
var along = shape.PathArray(curve, 10);         // 10 copies along curve
var noAlign = shape.PathArray(curve, 10, false); // don't align to path

// Mirror
var mirrored = shape.Mirror(new VLine(0, -100, 0, 100)); // mirror across Y axis

// Spiral array
var spiral = shape.SpiralArray(new VXYZ(0, 0), 20, 30, 150, 3); // 20 items, radius 30→150, 3 revolutions
```

## Boolean Operations (VPolygon only)

```csharp
// Basic operations (extension methods)
var united = polygon1.Union(polygon2);           // VPolygon?
var intersected = polygon1.Intersect(polygon2);  // List<VPolygon>
var diff = polygon1.Difference(polygon2);        // List<VPolygon>
var xored = polygon1.Xor(polygon2);             // List<VPolygon>

// Offset polygon edges
var offset = polygon.OffsetPolygon(10.0);        // List<VPolygon>

// Safe offset (caps at max distance to prevent collapse)
var safeOffset = polygon.OffsetPolygonSafe(-5.0); // List<VPolygon>
double maxInward = polygon.MaxSafeInwardOffset(); // max safe inward distance

// Self-intersection handling
bool selfIntersects = polygon.HasSelfIntersections();
var simple = polygon.MakeSimple();               // List<VPolygon> - resolves self-intersections

// Point in polygon / Area
bool inside = polygon.Contains(new VXYZ(5, 5));
double area = polygon.GetArea();
```

### Static BooleanOps Methods
```csharp
BooleanOps.Union(params VPolygon[] polygons);
BooleanOps.Union(IEnumerable<VPolygon> polygons);
BooleanOps.Intersect(a, b);
BooleanOps.Difference(a, b);
BooleanOps.Xor(a, b);

// Offset with join/end type control
BooleanOps.OffsetPolygon(polygon, distance, JoinType.Round, EndType.Polygon);

// Simplification (Douglas-Peucker)
BooleanOps.Simplify(polygon, tolerance: 0.1);
```

### PolygonWithHoles
Operations that produce holes return `PolygonWithHoles` objects:
```csharp
var results = BooleanOps.DifferenceWithHoles(a, b);  // List<PolygonWithHoles>
var results = BooleanOps.IntersectWithHoles(a, b);
var results = BooleanOps.UnionWithHoles(a, b);

// PolygonWithHoles
var pwh = new PolygonWithHoles(outerPolygon);
pwh.AddHole(holePolygon);
// Properties: Outer (VPolygon), Holes (List<VPolygon>), Area (outer minus holes)
// Methods: Contains(point), Clone()
```

### JoinType Enum (for offset operations)
`JoinType.Miter` (default, sharp), `JoinType.Round` (rounded), `JoinType.Square` (squared-off)

### EndType Enum (for offset operations)
`EndType.Polygon` (default, closed), `EndType.OpenRound`, `EndType.OpenSquare`, `EndType.OpenButt`

## Boolean Operations (Region)

Region supports boolean operations with any curve types (lines, arcs, beziers, splines). Operations approximate boundaries to high-resolution polygons, perform clipping, and wrap results back as Regions.

```csharp
// Static methods (preferred for Intersect to avoid collision with Shape.Intersect)
var union = RegionBooleanOps.Union(regionA, regionB);           // Region?
var intersection = RegionBooleanOps.Intersect(regionA, regionB); // List<Region>
var difference = RegionBooleanOps.Difference(regionA, regionB);  // List<Region>
var xor = RegionBooleanOps.Xor(regionA, regionB);               // List<Region>

// Union of multiple regions
var multiUnion = RegionBooleanOps.Union(region1, region2, region3); // Region?
var listUnion = RegionBooleanOps.Union(listOfRegions);              // Region?

// Extension methods (use static for Intersect)
var union = regionA.Union(regionB);           // Region?
var diff = regionA.Difference(regionB);       // List<Region>
var xor = regionA.Xor(regionB);              // List<Region>

// With holes support
var results = RegionBooleanOps.UnionWithHoles(a, b);       // List<Region>
var results = RegionBooleanOps.IntersectWithHoles(a, b);   // List<Region>
var results = RegionBooleanOps.DifferenceWithHoles(a, b);  // List<Region>

// Point containment and area
bool inside = region.Contains(new VXYZ(5, 5));
double area = region.Area;
```

> **Note**: Use `RegionBooleanOps.Intersect(a, b)` instead of `a.Intersect(b)` because the extension method collides with `Shape.Intersect(Shape)`.

## Ray Casting (RayCaster)

`RayCaster` accelerates ray-vs-shape queries over large 2D scenes. It snapshots all visible shapes on the canvas (every `Shape` in `CanvasRenderer.Instance.GetShapes()` with `IsVisible == true`) at construction, builds a flat-array BVH (Surface Area Heuristic split), then each query traverses iteratively with a stack-allocated stack and inline ray-vs-shape math for `VLine`, `VCircle`, `VArc`, `VEllipse`, `VPolygon` (covers `VRectangle`), and `VPolyline`. Other shape types fall back to AABB hit. The hot path is allocation-free, and queries are thread-safe after construction.

**`VPoint` markers are always excluded** from the index — they're zero-area visual labels, not meaningful ray targets. This holds regardless of `IsVisible` or how the `VPoint` was registered (any standalone `new VPoint(...)` marker on the canvas).

```csharp
// Build once over every visible shape currently on the canvas
// (millions of shapes are fine).
var caster = new RayCaster();                // default leafSize = 8
var caster2 = new RayCaster(leafSize: 16);

// Closest hit
RayHit? hit = caster.FindIntersection(
    location:  new VXYZ(0, 0, 0),
    direction: new VXYZ(1, 0, 0));           // need not be normalised
if (hit is { } h)
{
    Shape s   = h.Shape;
    VXYZ  pt  = h.Point;
    double d  = h.Distance;
}

// Closest hit within a distance cap (prunes BVH sub-trees).
RayHit? near = caster.FindIntersection(origin, direction, maxDistance: 50);

// Exclude specific shapes — useful when casting from inside / off a known
// shape, or for "find the next hit past these" queries.
RayHit? past = caster.FindIntersection(
    origin, direction,
    exclusionList: new List<Shape> { sourceShape });
RayHit? pastCapped = caster.FindIntersection(
    origin, direction, maxDistance: 100,
    exclusionList: new List<Shape> { sourceShape });

// Any-hit / "is anything blocking?" — faster than closest-hit.
bool blocked = caster.HasIntersection(origin, direction);
bool nearby  = caster.HasIntersection(origin, direction, maxDistance: 100);

// Batch (parallel by default — BVH is read-only after construction).
var queries = new[]
{
    new RayQuery(new VXYZ(0,0,0), new VXYZ(1,0,0)),
    new RayQuery(new VXYZ(0,0,0), new VXYZ(0,1,0))
};
RayHit?[] results = caster.FindIntersections(queries);
RayHit?[] seq     = caster.FindIntersections(queries, parallel: false);

// After shapes move, refit AABBs in O(N) without rebuilding the tree.
circle.Center = new VXYZ(50, 0);
caster.Refit();
```

| Type | Description |
|------|-------------|
| `RayCaster(int leafSize = 8)` | Snapshots all visible canvas shapes and builds the BVH. `VPoint` is always excluded; shapes with `IsVisible == false` or non-finite bounds (`VRay`, `VXLine`) are also excluded. |
| `RayHit(Shape Shape, VXYZ Point, double Distance)` | `readonly record struct` returned by closest-hit queries. |
| `RayQuery(VXYZ Origin, VXYZ Direction)` | `readonly record struct` for batch input. |

| Method | Returns | Description |
|--------|---------|-------------|
| `FindIntersection(location, direction, exclusionList = null)` | `RayHit?` | Closest hit (XY plane; Z ignored). Optional `List<Shape>` excludes specific shapes from the candidate set. |
| `FindIntersection(location, direction, maxDistance, exclusionList = null)` | `RayHit?` | Closest hit, capped by distance, with optional exclusion list. |
| `HasIntersection(location, direction, maxDistance = +∞)` | `bool` | Any-hit early-out (shadow-ray style). |
| `FindIntersections(queries, parallel = true)` | `RayHit?[]` | Batch query aligned with input. |
| `Refit()` | `void` | In-place AABB refresh after shape movement. |
| `Count` | `int` | Number of indexed shapes. |

Notes:
- The canvas state is snapshotted at construction — shapes added or removed afterwards are not reflected. Build a new `RayCaster` to pick them up.
- Queries run on the XY plane — the Z component of `location`/`direction` is ignored.
- Direction need not be normalised; degenerate (zero-length XY) direction returns null/false.
- `Refit()` preserves the tree topology — good for small movements; rebuild (new `RayCaster`) after large scene changes.

## Angle Conversion Extensions

Extension methods on `double` for converting between degrees and radians. Lets you skip the `* Math.PI / 180.0` boilerplate when calling trig functions.

```csharp
double rad = 45.0.ToRadians();      // degrees → radians
double deg = Math.PI.ToDegrees();   // radians → degrees

// Idiomatic usage
double y = Math.Sin(30.0.ToRadians());      // 0.5
double a = Math.Atan2(dy, dx).ToDegrees();   // angle in degrees
```

## VColor Utility

```csharp
// Named color properties
VColor.Red    VColor.Blue     VColor.Green     VColor.Yellow
VColor.Orange VColor.Purple   VColor.Pink      VColor.Cyan
VColor.Gold   VColor.Magenta  VColor.White     VColor.Black
// ... and 60+ more named colors

// Create from RGB
VColor.FromRgb(255, 128, 0)           // orange
VColor.FromArgb(128, 255, 0, 0)       // semi-transparent red
VColor.WithOpacity(255, 0, 0, 0.5)    // red at 50% opacity

// Random colors
VColor.GetRandomColor()               // random pastel (default)
VColor.GetRandomVibrantColor()        // random vibrant
VColor.GetRandomPastelColor()         // random pastel
VColor.GetVibrantColors()             // string[] of all vibrant colors
VColor.GetPastelColors()              // string[] of all pastel colors
```

## Style Defaults

```csharp
ShapeDefaults.GlobalColor = "Red";           // all new shapes will be Red
ShapeDefaults.GlobalFillColor = "Yellow";
ShapeDefaults.GlobalLineWeight = 3;
ShapeDefaults.GlobalLineType = LineType.Dashed;
ShapeDefaults.GlobalLineTypeScale = 2.0;

// Dimension style defaults (apply to new VDimension shapes)
ShapeDefaults.DimOffset = 15.0;
ShapeDefaults.DimArrowSize = 6.0;
ShapeDefaults.DimTextHeight = 10.0;
ShapeDefaults.DimDecimalPlaces = 1;
ShapeDefaults.DimExtendBeyondDimLines = 2.0;
ShapeDefaults.DimOffsetFromOrigin = 1.0;
ShapeDefaults.DimPrefix = "L=";
ShapeDefaults.DimSuffix = "mm";
ShapeDefaults.DimTextBgOpaque = true;
ShapeDefaults.DimExtensionLineColor = "Green";   // extension line color
ShapeDefaults.DimDimensionLineColor = "Red";     // dimension line & arrowhead color
ShapeDefaults.DimTextColor = "Blue";             // text color
ShapeDefaults.DimSuppressDimensionLine = true;   // hide dimension line & arrowheads

ShapeDefaults.Reset();                       // reset all to defaults
```

## Console Output

```csharp
VizConsole.Log("message");      // only method available
VizConsole.Log(42);             // accepts any object
VizConsole.Log(myVariable);     // auto-tracks calling file and line number
VizConsole.Log(myList);         // itemizes collections (prints each item)
VizConsole.Log(myList, false);  // prints collection's ToString() instead
VizConsole.Log(emptyList);      // prints "(empty)" for empty collections
// Output format: [ModuleName:LineNumber] message
```

**Important**: `VizConsole.Log()` is the only console method. There is no `Write()` or `WriteLine()`.
The optional second parameter `itemize` (default `true`) controls whether collections are printed item-by-item or as their type.

## Animation (using Code2Viz.Animation)

The animation system lets you animate shapes over time.

### Animator
```csharp
var animator = new Animator();
animator.Repeat = true;      // Loop (each animation loops independently at its own duration)
animator.Speed = 1.5;        // Playback speed
animator.Fps = 30;           // Target frame rate (1-120, default 60)

// Sequential: each starts after the previous ends
animator.AddToAnimations(new DrawAnimation(shape, 2.0));
animator.AddToAnimations(new MoveAnimation(shape, new VXYZ(100, 0, 0), 2.0));

// Insert a time gap before next animation
animator.Pause(1.5);  // 1.5 second pause

// Parallel: all start at the same time
animator.AddToAnimations(new List<Animation> {
    new FadeInAnimation(shape1, 1.0),
    new FadeInAnimation(shape2, 1.0)
});

animator.Animate();  // Start playback
animator.Stop();     // Stop all playback
```

### Animation Types

| Type | Constructor | Description |
|------|-------------|-------------|
| DrawAnimation | `(Shape target, double duration)` | Progressively draws shape (0% to 100%) |
| MoveAnimation | `(Shape target, VXYZ displacement, double duration)` | Translates by displacement vector |
| PathAnimation | `(Shape target, ICurve path, double duration)` | Moves shape along any ICurve path |
| RotateAnimation | `(Shape target, VXYZ pivot, double angleDeg, double duration)` | Rotates around pivot |
| FlipAnimation | `(Shape target, VLine mirrorAxis, double duration)` | Flips across mirror axis |
| FadeInAnimation | `(Shape target, double duration)` | Fades from transparent to opaque |
| FadeOutAnimation | `(Shape target, double duration)` | Fades from opaque to transparent |
| FadeOutAnimation | `(Shape target, double duration, double targetOpacity)` | Fades to target opacity |
| ValueAnimation\<T\> | `(T target, Expression<Func<T, double>> prop, double start, double end, double duration)` | Animate any numeric property on a Shape (T : Shape) |
| ValueAnimation\<T\> | `(T target, Expression<Func<T, double>> prop, List<double> values, double duration)` | Animate through a sequence of values evenly spaced over the duration |
| ObjectPropertyAnimation\<T\> | `(T target, Expression<Func<T, double>> prop, double start, double end, double duration)` | Animate any numeric property on any object (T : class) |

### ObjectPropertyAnimation Example
```csharp
// Animate a property on a user-defined class (not a Shape)
public class Wheel
{
    VCircle c = new VCircle(0, 0, 100);
    VCircle hub = new VCircle(new VXYZ(40, 40), 10);
    private double rotation = 0.0;
    public double Rotation
    {
        get { return rotation; }
        set { hub.Rotate(new VXYZ(0, 0), value - rotation); rotation = value; }
    }
}

var wheel = new Wheel();
var anim = new Animator { Repeat = true };
anim.AddToAnimations(new ObjectPropertyAnimation<Wheel>(wheel, w => w.Rotation, 0.0, 359.0, 1.0));
anim.Animate();
```

### Easing Functions
```csharp
var anim = new MoveAnimation(shape, new VXYZ(100, 0, 0), 2.0);
anim.EasingFunction = EasingFunctions.EaseInOutCubic;
```

Available: `Linear`, `EaseInQuad`, `EaseOutQuad`, `EaseInOutQuad`, `EaseInCubic`, `EaseOutCubic`, `EaseInOutCubic`

### Animation Example
```csharp
var line = new VLine(0, 0, 100, 0) { Color = "Cyan" };
var circle = new VCircle(50, 50, 30) { Color = "Yellow" };

var animator = new Animator();
animator.Repeat = true;

// Draw line over 2 seconds, then draw circle
animator.AddToAnimations(new DrawAnimation(line, 2.0));
animator.AddToAnimations(new DrawAnimation(circle, 2.0));

// Move circle with easing
var move = new MoveAnimation(circle, new VXYZ(100, 0, 0), 2.0);
move.EasingFunction = EasingFunctions.EaseInOutCubic;
animator.AddToAnimations(move);

// Fade both in parallel
animator.AddToAnimations(new List<Animation> {
    new FadeOutAnimation(line, 1.5),
    new FadeOutAnimation(circle, 1.5)
});

animator.Animate();
```

## Examples

### Concentric circles
```csharp
var circles = new List<VCircle>();
for (int r = 20; r <= 200; r += 20)
    circles.Add(new VCircle(0, 0, r) { Color = "Cyan" });
```

### Star polygon
```csharp
var points = new List<VXYZ>();
for (int i = 0; i < 5; i++)
{
    double angle = Math.PI / 2 + i * 4 * Math.PI / 5;
    points.Add(new VXYZ(Math.Cos(angle) * 100, Math.Sin(angle) * 100));
}
var star = new VPolygon(points) { Color = "Gold", FillColor = "DarkGoldenrod" };
```

### Grid of colored squares
```csharp
string[] colors = { "Red", "Orange", "Yellow", "Green", "Blue", "Purple" };
int idx = 0;
var squares = new List<VRectangle>();
for (int x = -150; x <= 150; x += 50)
    for (int y = -150; y <= 150; y += 50)
        squares.Add(new VRectangle(x, y, 40, 40) { FillColor = colors[idx++ % colors.Length] });
```

### Circular array with animation
```csharp
var hex = new VPolygon(Enumerable.Range(0, 6).Select(i => {
    double a = Math.PI / 3 * i;
    return new VXYZ(Math.Cos(a) * 20, Math.Sin(a) * 20);
}).ToArray()) { Color = "Cyan", FillColor = "#1a3a4a" };

var ring = hex.CircularArray(new VXYZ(0, 0), 12);

var animator = new Animator { Repeat = true };
foreach (var s in ring)
    animator.AddToAnimations(new DrawAnimation((Shape)s, 0.3));
animator.Animate();
```

### Offset curves
```csharp
var spline = new VSpline(
    new VXYZ(-100, 0), new VXYZ(-50, 80),
    new VXYZ(50, -80), new VXYZ(100, 0)) { Color = "White" };

for (int i = 1; i <= 5; i++)
{
    var offset = spline.Offset(i * 15);
    ((Shape)offset).Color = VColor.GetRandomVibrantColor();
}
```

## Tips

- Shapes appear on the canvas immediately when constructed. No `Draw()` call needed.
- Use `clear_canvas` before `execute_vizcode` if you want a fresh canvas.
- The code you send replaces the entire `Main()` body each time.
- Use `get_canvas_state` to inspect what's currently drawn.
- Use `export_png` to save the result to a file.
- All WPF named colors work: Red, Blue, Green, Cyan, Magenta, Yellow, Orange, Gold, etc.
- Hex colors work too: "#FF5733", "#3498DB".
- `VizConsole.Log()` is the only console output method. Do NOT use `Write()` or `WriteLine()`.
- Shape methods use VXYZ vectors: `shape.Move(new VXYZ(dx, dy, 0))` not `shape.Move(dx, dy)`.
- ICurve Offset returns ICurve — cast to Shape to set styling: `((Shape)curve.Offset(10)).Color = "Red"`.
- For Region boolean intersections, use `RegionBooleanOps.Intersect(a, b)` (static) instead of `a.Intersect(b)` (extension) to avoid collision with `Shape.Intersect`.
- **Multi-file projects**: Call `get_project_context` first to read all files. Other files may define classes, helpers, or data that `Main()` in StartViz.cs can use. Your code in `execute_vizcode` replaces the `Main()` body but is compiled alongside all other project files, so you can reference any types defined in them.

---

## Animator (sibling app, no MCP tools)

`Animator.exe` lives at `Animator/bin/{Config}/net9.0-windows/Animator.exe` and is launched manually or via Code2Viz's **Switch to Animator** button. The MCP tools above do **not** target Animator — they're Code2Viz-only. Animator is a separate WPF app for p5.js-style sketches.

### Sketch model

User code subclasses `Animator.Sketching.Sketch` and overrides `Setup()` (called once) and `Draw()` (called every frame). Geometry uses **`C2VGeometry`** types — the same single geometry namespace that Code2Viz uses. Shapes auto-register each frame; the canvas re-renders the current registry contents.

```csharp
using System;
using C2VGeometry;
using Animator.Sketching;
using Animator.Console;

public class MySketch : Sketch
{
    public override void Setup()
    {
        Size(800, 600);          // logical drawing area centered on origin
        Background("Black");
    }

    public override void Draw()
    {
        var r = 200.0;
        var x = r * Math.Sin(ElapsedSeconds);
        var y = r * Math.Cos(ElapsedSeconds);
        new VCircle(new VXYZ(x, y), 12) { FillColor = "Cyan" };
    }
}
```

### Sketch API surface

| Member | Type | Purpose |
|---|---|---|
| `Setup()` | virtual void | One-time init; called once when **Run** is pressed |
| `Draw()` | virtual void | Per-frame loop body |
| `Size(w, h)` | protected | Declare sketch dimensions; canvas auto-zooms to fit |
| `Background(color)` | protected | Set the canvas background colour |
| `NoLoop()` / `Loop()` | protected | Pause / resume the frame loop |
| `FrameCount` | int | Current frame number (0-based) |
| `ElapsedSeconds` | double | Seconds since `Setup()` returned |
| `DeltaSeconds` | double | Seconds since the previous frame |
| `Width` / `Height` | double | Last `Size()` call (default 800×600) |
| `MouseX` / `MouseY` | double | Mouse position in world coordinates (Y-up) |
| `MousePressed` | bool | Any mouse button is pressed |
| `KeyPressed` / `LastKey` | bool / string | Keyboard state |
| `VizConsole.Log/Warn/Error(msg)` | static | Print to the Animator console (Warn = yellow, Error = red) |

### Differences from Code2Viz mode

- Single `.cs` file (open / save / save-as), not a multi-file project
- Uses the same `C2VGeometry` types as Code2Viz; Animator does not import `Code2Viz` at all
- Per-frame fresh-object semantics: don't try to hold references between frames; put persistent state in fields on the sketch class
- No `Main()` — only Sketch subclasses are executed
- No properties panel, no drawing tools, no dimension annotations, no timeline scrubber — this app is for the frame loop, not static editing

### Switching between apps

- **In Code2Viz**: click `Switch to Animator` (top-right, next to Run)
- **In Animator**: click `Switch to Project` (top-right of the toolbar)
- Both apps prompt to save unsaved edits before switching
