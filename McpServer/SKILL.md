---
name: code2viz
description: "Create and visualize 2D geometry on an interactive canvas using C# code. Use this skill when the user asks to draw shapes, create geometric visualizations, generate 2D diagrams, or visualize geometry. Supports points, lines, circles, arcs, rectangles, ellipses, polygons, polylines, beziers, splines, arrows, text, groups, grids, dimensions, and construction lines. Shapes auto-register when created. Coordinate system is mathematical (Y-up, origin at center)."
---

# Code2Viz - 2D Geometry Visualization

You have access to the Code2Viz MCP server which lets you create and visualize 2D geometry on an interactive canvas. The Code2Viz WPF application must be running for tools to work.

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

## Quick Start

To draw shapes, call `execute_vizcode` with C# code. **Always assign shapes to variables** — unnamed shapes are hidden by the animation system.

```csharp
// CORRECT - assign to variable
var circle = new VCircle(0, 0, 100) { Color = "Red", FillColor = "Yellow" };
var line = new VLine(-100, -100, 100, 100) { Color = "Green", LineWeight = 3 };

// WRONG - shapes will be hidden (no variable name)
// new VCircle(0, 0, 100);
```

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
using Code2Viz.Geometry;
using Code2Viz.Console;
using Code2Viz.Animation;
```

## Shapes

### VPoint
```csharp
new VPoint(x, y);
```
Methods: `DistanceTo(VPoint other)`, `AsVXYZ()`, `Add(VPoint)`, `Add(VXYZ)`
Operators: `+`, `-`, `*`, `/` (with VPoint, VXYZ, or double)

### VLine
```csharp
new VLine(x1, y1, x2, y2);
new VLine(startPoint, endPoint);
```

### VCircle
```csharp
new VCircle(centerX, centerY, radius);
```

### VArc
```csharp
new VArc(centerX, centerY, radius, startAngleDeg, endAngleDeg);
// Counter-clockwise from start to end
```

### VRectangle
```csharp
new VRectangle(x, y, width, height);           // from bottom-left corner
new VRectangle(x1, y1, x2, y2, fromCorners: true); // from two corners
```

### VEllipse
```csharp
new VEllipse(centerX, centerY, radiusX, radiusY);
```

### VPolygon
```csharp
new VPolygon(new VPoint(0,100), new VPoint(95,31), new VPoint(59,-81), new VPoint(-59,-81), new VPoint(-95,31));
new VPolygon(listOfPoints);
// Auto-closes
```

### VPolyline
```csharp
new VPolyline(new VPoint(0,0), new VPoint(50,50), new VPoint(100,0));
// Open path
```

### VBezier
```csharp
new VBezier(x1,y1, cx1,cy1, cx2,cy2, x2,y2);
```

### VSpline
```csharp
new VSpline(new VPoint(0,0), new VPoint(50,80), new VPoint(100,0), new VPoint(150,80));
// Catmull-Rom through all points
```

### VText
```csharp
new VText(x, y, "Hello World");
new VText(x, y, "Big Text", 24);   // with font height
```

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| Content | string | | Text content |
| Height | double | 12 | Font height |
| Width | double | 0 | Text width (0 = auto) |
| Font | VFont | Arial | Font family |
| FontWeight | VFontWeight | Normal | Normal or Bold |

**VFont enum values**: Arial, TimesNewRoman, CourierNew, Verdana, Georgia, Tahoma, TrebuchetMS, Consolas, Calibri, Cambria, SegoeUI, ComicSansMS, Impact, LucidaConsole

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
| MidPoint | VPoint | | Midpoint (read-only) |

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
| `GetCenter()` | VPoint | Group center |
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
new VGrid(new VPoint(0,0), xCount: 5, yCount: 5, xSpacing: 20, ySpacing: 20, centered: true);
```

### VDimension
```csharp
new VDimension(x1, y1, x2, y2);
new VDimension(point1, point2);
```

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| Offset | double | 20 | Dimension line offset |
| ExtensionLength | double | 10 | Extension line length |
| ArrowSize | double | 8 | Arrowhead size |
| CustomText | string? | null | Custom text (null = distance) |
| DecimalPlaces | int | 2 | Distance decimal places |
| TextHeight | double | 12 | Text height |
| Distance | double | | Calculated distance (read-only) |
| DisplayText | string | | Display text (read-only) |

### VXLine (infinite construction line)
```csharp
VXLine.Horizontal(0);   // horizontal through y=0
VXLine.Vertical(0);     // vertical through x=0
```

### VRay (semi-infinite ray)
```csharp
VRay.AtAngle(0, 0, 45);  // ray from origin at 45 degrees
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
| `Rotate(pivot, angleDeg)` | Rotate around a VPoint pivot |
| `Scale(center, factor)` | Scale around a VPoint center |
| `Flip(mirrorLine)` | Mirror across a VLine |
| `Clone()` | Deep copy |
| `GetBounds()` | Returns `(VPoint min, VPoint max)` bounding box |
| `Show()` | Make visible |
| `Hide()` | Make invisible |
| `Remove()` | Remove from canvas |
| `Contains(point)` | Check if point is inside shape |
| `DistanceTo(point)` | Distance from shape to point |
| `DoesIntersect(other)` | Check intersection with another shape |
| `Intersect(other)` | Get intersection shape (or null) |

```csharp
// Move examples
shape.Move(new VXYZ(50, 0, 0));      // move right 50
shape.Move(new VXYZ(0, 100, 0));     // move up 100

// Rotate examples
shape.Rotate(new VPoint(0, 0), 45);  // rotate 45 degrees around origin

// Scale examples
shape.Scale(new VPoint(0, 0), 2.0);  // double size around origin

// Flip examples
shape.Flip(new VLine(0, 0, 0, 100)); // flip across Y axis
shape.Flip(new VLine(0, 0, 100, 0)); // flip across X axis
```

## ICurve Interface

Implemented by: VLine, VCircle, VArc, VEllipse, VPolyline, VPolygon, VBezier, VSpline

### Properties
| Property | Type | Description |
|----------|------|-------------|
| StartPoint | VPoint | Start point of curve |
| EndPoint | VPoint | End point (same as start for closed curves) |
| Vertices | List\<VPoint\> | Key vertices/control points |
| SelfIntersecting | bool | Whether curve self-intersects |

### Methods
| Method | Returns | Description |
|--------|---------|-------------|
| `GetLength()` | double | Total arc length |
| `Divide(n)` | List\<VPoint\> | Divide into n equal segments |
| `Measure(segmentLength)` | List\<VPoint\> | Points at fixed distance intervals |
| `PointAtSegmentLength(len)` | VPoint | Point at distance along curve |
| `PointAtParameter(t)` | VPoint | Point at parameter (0.0 to 1.0) |
| `Project(point)` | VPoint | Closest point on curve |
| `Offset(distance)` | ICurve | Parallel offset curve |
| `Offset(List<double> distances)` | List\<ICurve\> | Multiple offsets |
| `Intersect(otherCurve)` | IntersectionResult | Find intersection points |
| `SplitAtPoint(point)` | (ICurve, ICurve) | Split curve at point |
| `NormalAtPoint(point)` | VXYZ | Normal vector at point |
| `PointsAtChordLengthFromPoint(point, chordLength)` | List\<VPoint\> | Points at chord distance |

### IntersectionResult
| Property | Type | Description |
|----------|------|-------------|
| Points | List\<VPoint\> | Intersection points |
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
var ring = shape.CircularArray(new VPoint(0, 0), 8);          // 8 copies, full circle
var arc = shape.CircularArray(new VPoint(0, 0), 6, 180);      // 6 copies, half circle
var noRot = shape.CircularArray(new VPoint(0, 0), 8, 360, false); // don't rotate items

// Path array
var along = shape.PathArray(curve, 10);         // 10 copies along curve
var noAlign = shape.PathArray(curve, 10, false); // don't align to path

// Mirror
var mirrored = shape.Mirror(new VLine(0, -100, 0, 100)); // mirror across Y axis

// Spiral array
var spiral = shape.SpiralArray(new VPoint(0, 0), 20, 30, 150, 3); // 20 items, radius 30→150, 3 revolutions
```

## Boolean Operations (VPolygon only)

```csharp
// Union two polygons
var united = polygon1.Union(polygon2);

// Intersection
var intersected = polygon1.Intersect(polygon2); // returns List<VPolygon>

// Difference (subtract)
var diff = polygon1.Difference(polygon2); // returns List<VPolygon>

// Symmetric difference
var xored = polygon1.Xor(polygon2); // returns List<VPolygon>

// Offset polygon edges
var offset = polygon.OffsetPolygon(10.0); // returns List<VPolygon>

// Point in polygon
bool inside = polygon.Contains(new VPoint(5, 5));

// Area
double area = polygon.GetArea();
```

Static methods also available: `BooleanOps.Union(params VPolygon[])`, `BooleanOps.Intersect(a, b)`, `BooleanOps.Difference(a, b)`, etc.

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
ShapeDefaults.Reset();                       // reset all to defaults
```

## Console Output

```csharp
VizConsole.Log("message");      // only method available
VizConsole.Log(42);             // accepts any object
VizConsole.Log(myVariable);     // auto-tracks calling file and line number
// Output format: [ModuleName:LineNumber] message
```

**Important**: `VizConsole.Log()` is the only console method. There is no `Write()` or `WriteLine()`.

## Animation (using Code2Viz.Animation)

The animation system lets you animate shapes over time.

### Animator
```csharp
var animator = new Animator();
animator.Repeat = true;      // Loop
animator.Speed = 1.5;        // Playback speed

// Sequential: each starts after the previous ends
animator.AddToAnimations(new DrawAnimation(shape, 2.0));
animator.AddToAnimations(new MoveAnimation(shape, new VXYZ(100, 0, 0), 2.0));

// Parallel: all start at the same time
animator.AddToAnimations(new List<Animation> {
    new FadeInAnimation(shape1, 1.0),
    new FadeInAnimation(shape2, 1.0)
});

animator.Animate();  // Start playback
```

### Animation Types

| Type | Constructor | Description |
|------|-------------|-------------|
| DrawAnimation | `(Shape target, double duration)` | Progressively draws shape (0% to 100%) |
| MoveAnimation | `(Shape target, VXYZ displacement, double duration)` | Translates by displacement vector |
| RotateAnimation | `(Shape target, VPoint pivot, double angleDeg, double duration)` | Rotates around pivot |
| FlipAnimation | `(Shape target, VLine mirrorAxis, double duration)` | Flips across mirror axis |
| FadeInAnimation | `(Shape target, double duration)` | Fades from transparent to opaque |
| FadeOutAnimation | `(Shape target, double duration)` | Fades from opaque to transparent |
| FadeOutAnimation | `(Shape target, double duration, double targetOpacity)` | Fades to target opacity |

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
var points = new List<VPoint>();
for (int i = 0; i < 5; i++)
{
    double angle = Math.PI / 2 + i * 4 * Math.PI / 5;
    points.Add(new VPoint(Math.Cos(angle) * 100, Math.Sin(angle) * 100));
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
    return new VPoint(Math.Cos(a) * 20, Math.Sin(a) * 20);
}).ToArray()) { Color = "Cyan", FillColor = "#1a3a4a" };

var ring = hex.CircularArray(new VPoint(0, 0), 12);

var animator = new Animator { Repeat = true };
foreach (var s in ring)
    animator.AddToAnimations(new DrawAnimation((Shape)s, 0.3));
animator.Animate();
```

### Offset curves
```csharp
var spline = new VSpline(
    new VPoint(-100, 0), new VPoint(-50, 80),
    new VPoint(50, -80), new VPoint(100, 0)) { Color = "White" };

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
