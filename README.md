<p align="center">
  <img src="img/logo.png" alt="Code2Viz Logo" width="200">
</p>

# Code2Viz - 2D Geometry Visualizer

A WPF application for visualizing 2D geometric shapes through C# or F# code execution with animation support.

## Overview

Code2Viz is a visual programming environment that lets you write C# or F# code to create, style, and animate 2D geometric shapes on an interactive canvas. It combines a code editor with syntax highlighting, a real-time rendering canvas with zoom and pan capabilities, and a timeline-based animation system with GIF export.

## Features

- **Live Preview**: Canvas updates automatically as you type (debounced auto-run)
- **No Draw() Required**: Shapes appear automatically when created
- **Multi-language Support**: Write code in C# or F# with full syntax highlighting
- **Rich Shape Library**: Points, lines, circles, rectangles, ellipses, arcs, polygons, polylines, Bezier curves, splines, regions (curve-bounded areas), hatches (pattern fills), text, arrows, and dimension annotations
- **Shape Editing**: Select shapes and drag shape-specific control points (vertices, radius handles, curve controls) with live code sync
- **Properties Panel**: Floating or dockable panel to edit geometry and style properties (color, fill, weight, opacity, visibility, name) with full code sync — changes persist as code lines
- **Animation System**: Create timeline-based animations with draw, move, rotate, flip, and fade effects
- **Interactive Canvas**: Zoom with mouse wheel, pan with middle-click, toggle grid display
- **Export Options**: Save visualizations as PNG images, animated GIFs, or MP4 videos
- **Project Management**: Organize multiple code files into projects with tabbed editing, drag-and-drop file organization, and "Go to Location" to open files in Windows Explorer
- **NuGet Integration**: Add external packages to extend functionality
- **Built-in Help**: Comprehensive API documentation with examples
- **Code Minimap**: VSCode-style minimap with syntax coloring, viewport indicator, and error marker navigation

---

## Quick Start

### 1. Create a New Project
File > New Project (Ctrl+Shift+N) creates a new project with a starter template.

### 2. Write Your Code
The entry point is `StartViz.Viz.Main()` in `StartViz.vizcode`:

```csharp
using Code2Viz.Geometry;

namespace StartViz
{
    public class Viz
    {
        public static void Main()
        {
            // Shapes appear automatically when created - no Draw() needed!
            var circle = new VCircle(0, 0, 50);
            circle.Color = "Cyan";
            circle.FillColor = "#4000FFFF";
        }
    }
}
```

### 3. See Results Instantly
With **Auto-update Canvas** enabled (default), the canvas updates automatically as you type - no need to press Run!

- **Auto-update**: Canvas refreshes 500ms after you stop typing
- **Manual mode**: Disable auto-update in Settings to use F5/Run button instead
- **Auto-Draw Shapes**: Toggle in Settings > Canvas Settings to control whether shapes auto-register on construction
- **Draw() is optional**: Shapes appear when created; `Draw()` is kept for backwards compatibility

---

## Supported Shapes

| Shape | Description | Constructor Examples |
|-------|-------------|---------------------|
| **VXYZ** | Coordinate/vector type (like Revit's XYZ) | `new VXYZ(x, y)` or `new VXYZ(x, y, z)` |
| **VPoint** | A visible point marker on the canvas | `new VPoint(x, y)` or `point.PolarPoint(angle, distance)` |
| **VLine** | A line segment | `new VLine(p1, p2)` or `new VLine(x1, y1, x2, y2)` |
| **VXLine** | An infinite construction line | `new VXLine(basePoint, direction)` or `new VXLine(p1, p2)` |
| **VRay** | A semi-infinite ray | `new VRay(origin, direction)` or `new VRay(origin, throughPoint)` |
| **VCircle** | A circle | `new VCircle(center, radius)` or `new VCircle(x, y, radius)` or `new VCircle(p1, p2, p3)` (circumcircle) |
| **VRectangle** | A rectangle (inherits from VPolygon) | `new VRectangle(corner, width, height)` or `new VRectangle(bottomLeft, topRight)` |
| **VEllipse** | An ellipse | `new VEllipse(center, radiusX, radiusY)` |
| **VArc** | A circular arc | `new VArc(center, radius, startAngle, endAngle)` |
| **VPolygon** | A closed polygon | `new VPolygon(p1, p2, p3, ...)` |
| **VPolyline** | Open connected segments | `new VPolyline(p1, p2, p3, ...)` |
| **VBezier** | Cubic Bezier curve | `new VBezier(start, ctrl1, ctrl2, end)` |
| **VSpline** | Smooth spline curve | `new VSpline(p1, p2, p3, ...)` |
| **VText** | Text at a position | `new VText(position, "text")` or `new VText(x, y, "text", height)` |
| **VArrow** | Arrow with head | `new VArrow(start, end)` |
| **VDimension** | Dimension annotation with arrowheads | `new VDimension(p1, p2)` or `new VDimension(x1, y1, x2, y2)` |
| **VRadialDimension** | Radial/diameter dimension | `new VRadialDimension(circle)` or `new VRadialDimension(arc)` |
| **VGroup** | Group of shapes | `new VGroup(shape1, shape2, ...)` or `new VGroup(shapeList)` |
| **VGrid** | Grid of visible VPoints | `new VGrid(location, xcount, ycount, spacing, centered)` |
| **VCell** | Square cell with neighbours | Created by `VSpatialGrid` |
| **VSpatialGrid** | Grid of cells with A* pathfinding | `new VSpatialGrid(location, xCount, yCount, cellSize)` |
| **Region** | Curve-bounded region | `new Region(curves)` or `new Region(outerCurves, holes)` |
| **VHatch** | Pattern fill within boundary | `new VHatch(polygon, BuiltInHatch.ANSI31, scale)` |

> **VXYZ vs VPoint**: `VXYZ` is the coordinate/vector type used for all position parameters, properties, and return types (e.g., `new VXYZ(10, 20)`). `VPoint` is a visible shape that draws a dot on the canvas. Use `new VXYZ(x, y)` where you previously used `VPoint.Internal(x, y)`.

---

## Dimensions (VDimension)

VDimension creates AutoCAD-style dimension annotations with arrowheads, extension lines, and distance text.

### Basic Dimension

```csharp
// Dimension between two points
var dim = new VDimension(new VXYZ(0, 0), new VXYZ(100, 0));
dim.Offset = 20;          // Distance of dimension line from the measured points
dim.TextHeight = 14;
dim.Draw();

// Shorthand constructor
var dim2 = new VDimension(0, 50, 80, 50);
dim2.Draw();
```

### Extension Line Control

```csharp
var dim = new VDimension(0, 0, 100, 0);
dim.Offset = 25;
dim.ExtendBeyondDimLines = 2.0; // How far extensions go past the dimension line
dim.OffsetFromOrigin = 1.0;     // Gap between the point and extension line start
dim.Draw();

// Suppress individual extension lines
var dim2 = new VDimension(0, -40, 100, -40);
dim2.Offset = 20;
dim2.SuppressExtLine1 = true;   // Hide first extension line
dim2.Draw();
```

### Text Formatting

```csharp
var dim = new VDimension(0, 0, 100, 0);
dim.Offset = 20;
dim.DecimalPlaces = 1;    // Show 1 decimal place
dim.Prefix = "L=";        // Text before the value
dim.Suffix = "mm";        // Text after the value
dim.Draw();               // Shows "L=100.0mm"

// Custom text overrides the calculated distance
var dim2 = new VDimension(0, -40, 80, -40);
dim2.Offset = 20;
dim2.CustomText = "TYP.";
dim2.Draw();
```

### Dimension Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Offset` | double | 20 | Distance of dimension line from measured points |
| `ArrowSize` | double | 8 | Size of arrowheads |
| `TextHeight` | double | 12 | Height of dimension text |
| `DecimalPlaces` | int | 2 | Decimal places for distance display |
| `ExtendBeyondDimLines` | double | 1.25 | How far extension lines extend past the dimension line |
| `OffsetFromOrigin` | double | 0.625 | Gap between origin point and extension line start |
| `SuppressExtLine1` | bool | false | Hide the first extension line (at Point1) |
| `SuppressExtLine2` | bool | false | Hide the second extension line (at Point2) |
| `SuppressDimensionLine` | bool | false | Hide the dimension line and arrowheads |
| `Prefix` | string | "" | Text prepended to the dimension value |
| `Suffix` | string | "" | Text appended to the dimension value |
| `TextBackgroundOpaque` | bool | false | Draw an opaque background behind dimension text |
| `ExtensionLineColor` | string? | null | Color for extension lines (null = use Color) |
| `DimensionLineColor` | string? | null | Color for dimension line & arrowheads (null = use Color) |
| `TextColor` | string? | null | Color for dimension text (null = use Color) |
| `CustomText` | string? | null | Custom text (overrides calculated distance) |
| `Distance` | double | — | Calculated distance between points (read-only) |
| `DisplayText` | string | — | Final display text with prefix/suffix (read-only) |

### Dimension Style Defaults

Dimension defaults can be configured per-project in the **Settings** tab under **Dimension Style**. When set, all new `VDimension` shapes created in code will use these values instead of the built-in defaults.

---

## Radial Dimensions (VRadialDimension)

VRadialDimension annotates the radius or diameter of circles and arcs with a leader line, arrowhead, and text.

### Basic Radial Dimension

```csharp
// Radius dimension for a circle
var circle = new VCircle(0, 0, 50);
var dim = new VRadialDimension(circle);
dim.LeaderAngle = 45;   // Angle of the leader line (degrees)

// Radius dimension for an arc
var arc = new VArc(0, 0, 80, 30, 150);
var dimArc = new VRadialDimension(arc);
```

### Diameter Mode

```csharp
var circle = new VCircle(0, 0, 50);
var dim = new VRadialDimension(circle);
dim.ShowDiameter = true;   // Shows diameter line through center
dim.LeaderAngle = 30;
// Displays: "⌀100.00"
```

### Text Formatting

```csharp
var circle = new VCircle(0, 0, 50);
var dim = new VRadialDimension(circle);
dim.DecimalPlaces = 1;
dim.Prefix = "";
dim.Suffix = "mm";
// Displays: "R50.0mm"

// Custom text overrides automatic label
var dim2 = new VRadialDimension(circle);
dim2.CustomText = "TYP.";
dim2.LeaderAngle = -45;
```

### VRadialDimension Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Center` | VXYZ | — | Center of the circle/arc |
| `Radius` | double | — | Radius of the circle/arc |
| `LeaderAngle` | double | 45 | Angle (degrees) of the leader line direction |
| `ShowDiameter` | bool | false | Show diameter instead of radius |
| `ArrowSize` | double | 8 | Size of the arrowhead |
| `TextHeight` | double | 12 | Height of the dimension text |
| `DecimalPlaces` | int | 2 | Decimal places for the value |
| `Prefix` | string | "" | Text prepended to the dimension value |
| `Suffix` | string | "" | Text appended to the dimension value |
| `TextBackgroundOpaque` | bool | false | Draw opaque background behind text |
| `DimensionLineColor` | string? | null | Color for leader line & arrowhead (null = use Color) |
| `TextColor` | string? | null | Color for dimension text (null = use Color) |
| `CustomText` | string? | null | Custom text (overrides calculated value) |
| `Value` | double | — | Calculated radius or diameter (read-only) |
| `DisplayText` | string | — | Final display text (read-only) |

---

## Text (VText)

VText renders text at a specified position on the canvas.

### Basic Text

```csharp
// Simple text
var label = new VText(new VXYZ(0, 0), "Hello World");

// With font height
var title = new VText(0, 50, "Title", 32);
title.Color = "Cyan";

// Font and weight
var bold = new VText(0, -50, "Bold Consolas", 20);
bold.Font = VFont.Consolas;
bold.FontWeight = VFontWeight.Bold;
```

### Text Anchor (Alignment)

The `Anchor` property controls which point of the text bounding box is placed at the text's `Location`. Default is `BottomLeft`.

```csharp
// Center text on a point
var centered = new VText(0, 0, "Centered", 20);
centered.Anchor = VTextAnchor.MiddleCenter;

// Right-align text
var right = new VText(100, 0, "Right-aligned", 16);
right.Anchor = VTextAnchor.MiddleRight;

// Top-center (text hangs below the point)
var header = new VText(0, 100, "Header", 24);
header.Anchor = VTextAnchor.TopCenter;
```

**All 9 anchor values:**

| | Left | Center | Right |
|---|---|---|---|
| **Top** | `TopLeft` | `TopCenter` | `TopRight` |
| **Middle** | `MiddleLeft` | `MiddleCenter` | `MiddleRight` |
| **Bottom** | `BottomLeft` (default) | `BottomCenter` | `BottomRight` |

### VText Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Location` | VXYZ | — | Position of the text anchor point |
| `Content` | string | — | Text content to display |
| `Height` | double | 12 | Font height in world units |
| `Width` | double | 0 | Text width (0 = auto-measured) |
| `Font` | VFont | Arial | Font family enum |
| `FontWeight` | VFontWeight | Normal | Normal or Bold |
| `Anchor` | VTextAnchor | BottomLeft | Which point of the text is placed at Location |

**VFont values**: Arial, TimesNewRoman, CourierNew, Verdana, Georgia, Tahoma, TrebuchetMS, Consolas, Calibri, Cambria, SegoeUI, ComicSansMS, Impact, LucidaConsole

---

## Shape Grouping (VGroup)

VGroup allows you to combine multiple shapes into a single unit that can be transformed and selected together.

### Creating Groups

```csharp
// Empty group, add shapes later
var group = new VGroup();
group.Add(new VCircle(0, 0, 20));
group.Add(new VLine(-30, 0, 30, 0));

// From params
var group2 = new VGroup(
    new VCircle(0, 0, 20),
    new VLine(-30, 0, 30, 0),
    new VLine(0, -30, 0, 30)
);

// From collection
var shapes = new List<Shape> { circle, line1, line2 };
var group3 = new VGroup(shapes);
```

### Group Transformations

All transformations apply to every shape in the group:

```csharp
var group = new VGroup(circle, line1, line2);

// Move entire group
group.Move(new VXYZ(100, 50, 0));

// Rotate around a pivot point
group.Rotate(new VXYZ(0, 0), 45);

// Scale from center
group.Scale(group.GetCenter(), 2.0);

// Draw the group (renders as a single selectable entity)
group.Draw();
```

### Group Styling

Apply styles to all shapes at once:

```csharp
var group = new VGroup(shape1, shape2, shape3);
group.Color = "Cyan";
group.FillColor = "#4000FFFF";
group.LineWeight = 2;

// Apply group style to all children
group.ApplyStyle();

// Or apply individual properties
group.ApplyColor();
group.ApplyFillColor();
group.ApplyLineWeight();

// Set opacity for all shapes
group.SetOpacity(0.5);
```

### Group Utilities

```csharp
// Access shapes
int count = group.Count;
Shape first = group[0];
bool hasCircle = group.ContainsShape(myCircle);

// Query shapes by type
var allCircles = group.GetShapesOfType<VCircle>();

// Flatten nested groups
List<Shape> allShapes = group.Flatten();

// Iterate with action
group.ForEach(s => s.Color = "Yellow");

// Filter to new group
var filtered = group.Where(s => s is VCircle);

// Get bounds and center
BoundingBox bounds = group.GetBounds();
VXYZ center = group.GetCenter();
// bounds.Min, bounds.Max, bounds.Width, bounds.Height, bounds.Center
```

---

## Point Grids (VGrid)

VGrid creates a rectangular grid of VPoints, useful for creating patterns, matrices, or reference grids.

### Creating Grids

```csharp
// Centered grid at origin: 5 columns x 3 rows, spacing 10 units
var grid = new VGrid(new VXYZ(0, 0), 5, 3, 10, true);
grid.Draw();

// Grid with bottom-left corner at (-100, -50)
var grid2 = new VGrid(new VXYZ(-100, -50), 4, 4, 20, false);
grid2.Draw();

// Different X and Y spacing: 15 horizontal, 10 vertical
var grid3 = new VGrid(new VXYZ(0, 0), 6, 4, 15, 10, true);
grid3.Draw();
```

### Constructor Options

| Constructor | Description |
|-------------|-------------|
| `VGrid(location, xcount, ycount, centered)` | Default spacing of 1.0 |
| `VGrid(location, xcount, ycount, spacing, centered)` | Uniform spacing |
| `VGrid(location, xcount, ycount, xSpacing, ySpacing, centered)` | Different X/Y spacing |

### Grid Properties

```csharp
var grid = new VGrid(new VXYZ(0, 0), 5, 3, 10, true);

// Access points
List<VPoint> allPoints = grid.Points;
int totalCount = grid.Count;           // 15 (5 x 3)
VPoint point = grid[0];                // First point (by index)
VPoint cell = grid[2, 1];              // Column 2, Row 1

// Grid info
int cols = grid.XCount;                // 5
int rows = grid.YCount;                // 3
double xSpace = grid.XSpacing;         // 10
double ySpace = grid.YSpacing;         // 10
bool centered = grid.Centered;         // true
```

### Grid Operations

```csharp
var grid = new VGrid(new VXYZ(0, 0), 5, 3, 10, true);

// Style all points
grid.Color = "White";
grid.FillColor = "Cyan";
grid.ApplyStyle();  // Apply to all points

// Get rows and columns
List<VPoint> row0 = grid.GetRow(0);      // Bottom row
List<VPoint> col2 = grid.GetColumn(2);   // Third column

// Geometry
VXYZ center = grid.GetCenter();
BoundingBox bounds = grid.GetBounds();
// bounds.Min, bounds.Max, bounds.Width, bounds.Height

// Transform entire grid
grid.Move(new VXYZ(50, 25, 0));
grid.Rotate(new VXYZ(0, 0), 45);
grid.Scale(grid.GetCenter(), 2.0);

grid.Draw();
```

---

## Spatial Grid (VCell & VSpatialGrid)

VSpatialGrid creates a grid of square VCell instances with automatic neighbour connectivity (4-way: left, right, below, above) and built-in A* pathfinding.

### Creating a Spatial Grid

```csharp
// 10x10 grid of cells, each 5 units wide, starting at origin
var grid = new VSpatialGrid(new VXYZ(0, 0), 10, 10, 5);
```

The `location` parameter is the **center of the bottom-left cell** (cell[0,0]).

### Cell Properties

```csharp
VCell cell = grid[3, 4];           // Access by (col, row)
int id = cell.UniqueId;            // 0-based sequential ID
VXYZ center = cell.Center;         // Center point of the cell
double size = cell.CellSize;       // Side length
int col = cell.Column;             // Column index
int row = cell.Row;                // Row index
List<VCell> neighbours = cell.Neighbours; // Adjacent cells
bool blocked = cell.Blocked;       // Whether cell is impassable
```

### A* Pathfinding

```csharp
var grid = new VSpatialGrid(new VXYZ(0, 0), 20, 20, 5);

// Block cells to create obstacles
for (int i = 5; i < 15; i++)
    grid[10, i].Blocked = true;

// Find shortest path around obstacles
VCell start = grid[0, 0];
VCell end = grid[19, 19];
List<VCell> path = grid.FindPath(start, end);

// Visualize the path
foreach (var cell in path)
    cell.FillColor = "LimeGreen";
```

### Nearest Cell Lookup

```csharp
// O(log n) lookup using KD-tree
VCell closest = grid.GetClosestCell(new VPoint(12.5, 7.3));
```

### Grid Operations

```csharp
List<VCell> row0 = grid.GetRow(0);       // Bottom row
List<VCell> col2 = grid.GetColumn(2);    // Third column
VXYZ center = grid.GetCenter();          // Grid center
VCell? hit = grid.GetCellAt(new VXYZ(12, 8)); // Cell containing point

// Style and transform
grid.Color = "DarkGray";
grid.ApplyStyle();
grid.Move(new VXYZ(50, 0, 0));
grid.Rotate(new VXYZ(0, 0), 45);
```

---

## Regions (Curve-Bounded Areas)

Region represents an enclosed 2D area bounded by curves (lines, arcs, splines, beziers). Unlike VPolygon which only supports straight edges, Region preserves the original curve geometry in its boundary loops.

### Creating Regions

```csharp
// Region from lines (rectangle)
var p0 = new VXYZ(0, 0);
var p1 = new VXYZ(100, 0);
var p2 = new VXYZ(100, 80);
var p3 = new VXYZ(0, 80);

var region = new Region(new List<ICurve> {
    new VLine(p0, p1),
    new VLine(p1, p2),
    new VLine(p2, p3),
    new VLine(p3, p0)
});
region.Color = "Cyan";
region.FillColor = "#4000FFFF";

// Region with mixed curves (D-shape: line + arc)
var bottom = new VXYZ(0, 0);
var top = new VXYZ(0, 60);
var arc = VArc.FromStartEndRadius(top, bottom, 40, false);
var dShape = new Region(new List<ICurve> { new VLine(bottom, top), arc });

// Curves can be provided in any order - they are auto-ordered into a loop
```

### Regions with Holes

```csharp
// Create outer boundary
var outer = new Region(new List<ICurve> {
    new VLine(new VXYZ(0,0), new VXYZ(100,0)),
    new VLine(new VXYZ(100,0), new VXYZ(100,100)),
    new VLine(new VXYZ(100,100), new VXYZ(0,100)),
    new VLine(new VXYZ(0,100), new VXYZ(0,0))
});

// Add a hole
outer.AddHole(new List<ICurve> {
    new VLine(new VXYZ(30,30), new VXYZ(70,30)),
    new VLine(new VXYZ(70,30), new VXYZ(70,70)),
    new VLine(new VXYZ(70,70), new VXYZ(30,70)),
    new VLine(new VXYZ(30,70), new VXYZ(30,30))
});

// Or provide holes in constructor
var regionWithHoles = new Region(outerCurves, new List<List<ICurve>> { holeCurves });
```

### Region Properties

```csharp
var region = new Region(curves);

double area = region.Area;           // Outer area minus hole areas
double signed = region.SignedArea;   // Positive for CCW, negative for CW
double perimeter = region.Perimeter; // Total length (outer + holes)

List<ICurve> outer = region.OuterLoop;      // Outer boundary curves
List<List<ICurve>> holes = region.Holes;    // Inner hole loops

bool inside = region.Contains(new VXYZ(50, 40));  // Point containment
BoundingBox bounds = region.GetBounds();
```

### Converting Between Region and Polygon

```csharp
// Region to Polygon
var poly = region.ToPolygon();              // Low-fidelity (curve endpoints only)
var hires = region.ToPolygonHighRes(32);    // High-fidelity (32 segments per curve)
var pwh = region.ToPolygonWithHoles(32);    // With holes, high-fidelity

// Polygon to Region
var fromPoly = Region.FromPolygon(polygon);
var fromPwh = Region.FromPolygonWithHoles(polygonWithHoles);
```

### Region Boolean Operations

Region supports boolean operations via `RegionBooleanOps` or extension methods:

```csharp
// Static methods
var union = RegionBooleanOps.Union(regionA, regionB);            // Region?
var intersection = RegionBooleanOps.Intersect(regionA, regionB); // List<Region>
var difference = RegionBooleanOps.Difference(regionA, regionB);  // List<Region>
var xor = RegionBooleanOps.Xor(regionA, regionB);               // List<Region>

// Multi-region union
var combined = RegionBooleanOps.Union(region1, region2, region3);

// Extension method syntax
var union = regionA.Union(regionB);
var diff = regionA.Difference(regionB);

// With holes support
var results = RegionBooleanOps.DifferenceWithHoles(regionA, regionB);
```

> **Note**: Use `RegionBooleanOps.Intersect(a, b)` instead of `a.Intersect(b)` to avoid collision with `Shape.Intersect`.

---

## Hatch Patterns (VHatch)

VHatch fills a closed polygon boundary with a repeating line pattern. It supports 73 built-in AutoCAD-standard patterns and custom patterns defined using the `.pat` format.

### Built-in Patterns

```csharp
// Use enum for built-in patterns
var rect = new VRectangle(0, 0, 100, 80);
var hatch = new VHatch(rect, BuiltInHatch.ANSI31, scale: 10);
hatch.Color = "Cyan";

// Use string name (case-insensitive)
var hatch2 = new VHatch(rect, "BRICK", scale: 5);
```

### Pattern Scale and Angle

```csharp
var poly = new VPolygon(new VXYZ(0,0), new VXYZ(100,0),
                        new VXYZ(100,80), new VXYZ(0,80));

// Scale controls pattern density, angle rotates the entire pattern
var hatch = new VHatch(poly, BuiltInHatch.ANSI37, scale: 15, angle: 30);
hatch.Color = "Yellow";
```

### Custom Patterns from String

Define custom patterns using the AutoCAD `.pat` format:
`angle, x-origin, y-origin, delta-x, delta-y [, dash1, dash2, ...]`

```csharp
// Custom crosshatch pattern
var hatch = VHatch.FromDefinition(polygon, @"
  *CROSSHATCH, Custom crosshatch
  0, 0,0, 0,10
  90, 0,0, 0,10
", scale: 1.0);
hatch.Color = "Lime";
```

### Custom HatchType Object

```csharp
// Build a pattern programmatically
var pattern = new HatchType("MyPattern", "Diagonal lines", new List<HatchPatternLine> {
    new HatchPatternLine(45, 0, 0, 0, 5),
    new HatchPatternLine(135, 0, 0, 0, 5)
});
var hatch = new VHatch(polygon, pattern, scale: 2.0);
```

### Available Built-in Patterns

Common patterns include: `SOLID`, `ANSI31`-`ANSI38`, `ANGLE`, `BRICK`, `BRSTONE`, `CLAY`, `CORK`, `CROSS`, `DASH`, `DOTS`, `EARTH`, `ESCHER`, `GRASS`, `GRATE`, `HEX`, `HONEY`, `LINE`, `NET`, `NET3`, `SQUARE`, `STARS`, `STEEL`, `TRIANG`, `ZIGZAG`, `AR-HBONE`, `AR-BRSTD`, `AR-CONC`, `AR-SAND`, and more. Use `BuiltInHatches.GetAllNames()` to list all 73 patterns.

### VHatch Properties

```csharp
var hatch = new VHatch(polygon, BuiltInHatch.ANSI31, scale: 10);
hatch.Color = "Cyan";           // Hatch line color
hatch.LineWeight = 1.0;         // Hatch line thickness
hatch.PatternScale = 10;        // Pattern scale factor
hatch.PatternAngle = 45;        // Additional rotation (degrees)
hatch.Opacity = 0.5;            // Transparency
```

---

## Shape Styling

All shapes support customizable styling through these properties:

```csharp
var circle = new VCircle(0, 0, 50);
circle.Color = "Cyan";           // Outline color
circle.FillColor = "#4000FFFF";        // Fill color (with transparency)
circle.LineWeight = 2.5;               // Border thickness
circle.StrokeStyle = StrokeStyle.Dashed;  // Line pattern
circle.Draw();
```

### Stroke Styles

The `StrokeStyle` property controls the line pattern for shape outlines:

| Style | Description | Pattern |
|-------|-------------|---------|
| `Continuous` | Solid line (default) | ───────── |
| `Dashed` | Long dashes | ── ── ── |
| `Dotted` | Short dots | · · · · · |
| `DashDot` | Dash-dot alternating | ── · ── · |
| `DashDotDot` | Dash-dot-dot pattern | ── · · ── |
| `Center` | Center line (long-short) | ─── ─ ─── |
| `Phantom` | Phantom line | ─── ─ ─ ─── |
| `Hidden` | Hidden line (short dashes) | - - - - - |

```csharp
var line1 = new VLine(0, 0, 100, 0);
line1.StrokeStyle = StrokeStyle.Dashed;
line1.Draw();

var line2 = new VLine(0, 20, 100, 20);
line2.StrokeStyle = StrokeStyle.DashDot;
line2.Draw();
```

### Color Formats
- **Named colors**: `"Red"`, `"Blue"`, `"Cyan"`, `"LimeGreen"`, etc.
- **Hex RGB**: `"#FF0000"` (red)
- **Hex ARGB**: `"#80FF0000"` (semi-transparent red, where 80 is alpha)

### VColor Utility Class

Use `VColor` for easy color access and random color generation:

```csharp
// Static color properties
circle.Color = VColor.Red;
circle.FillColor = VColor.LimeGreen;

// Random colors
shape.Color = VColor.GetRandomColor();        // pastel (default)
shape.Color = VColor.GetRandomColor(false);   // vibrant
shape.FillColor = VColor.GetRandomPastelColor();    // shorthand for pastel
shape.Color = VColor.GetRandomVibrantColor(); // shorthand for vibrant

// Custom RGB colors
shape.FillColor = VColor.FromRgb(255, 128, 0);      // orange
shape.FillColor = VColor.FromArgb(128, 255, 0, 0);  // semi-transparent red

// From enum
shape.Color = VColor.FromEnum(ColorName.Coral);
```

**Color categories:**
- **Vibrant colors** (25): Bright colors good for strokes - Red, Lime, Cyan, HotPink, Gold, etc.
- **Pastel colors** (25): Soft colors good for fills - LightBlue, Lavender, PaleGreen, etc.

### Global Defaults

Set default styling for all new shapes:

```csharp
ShapeDefaults.GlobalColor = "Cyan";
ShapeDefaults.GlobalFillColor = "Transparent";
ShapeDefaults.GlobalLineWeight = 2.0;
ShapeDefaults.GlobalStrokeStyle = StrokeStyle.Continuous;

// All shapes created after this use these defaults
var circle = new VCircle(0, 0, 50);
circle.Draw();  // Uses Cyan stroke

// Reset to original defaults
ShapeDefaults.Reset();
```

### Geometric Properties

Circles and ellipses provide computed geometric properties:

```csharp
// Circle properties
var circle = new VCircle(0, 0, 50);
double area = circle.Area;               // π × r² = ~7853.98
double circumference = circle.Circumference;  // 2π × r = ~314.16

// Ellipse properties
var ellipse = new VEllipse(0, 0, 60, 40);
double ellipseArea = ellipse.Area;             // π × rx × ry = ~7539.82
double ellipseCircum = ellipse.Circumference;  // Ramanujan approximation = ~318.49
```

---

## Animation System

Code2Viz includes an animation system using the `Animator` class for creating animated visualizations with automatic sequencing.

> **Note**: The animation timeline panel is automatically hidden when your code has no animations. It appears automatically when you run code that creates animations.

### Basic Animation Example

```csharp
using Code2Viz.Geometry;
using Code2Viz.Animation;

namespace StartViz
{
    public class Viz
    {
        public static void Main()
        {
            // Create shapes
            var line = new VLine(0, 0, 100, 50);
            var circle = new VCircle(50, 50, 30);
            circle.Color = "Yellow";

            // Create animator
            var anim = new Animator();
            anim.Repeat = true;  // Loop animation
            anim.Fps = 30;       // Limit to 30 frames per second (1-120, default 60)

            // Add animations - they play sequentially
            anim.AddToAnimations(new DrawAnimation(line, 2.0));           // 0-2s
            anim.AddToAnimations(new DrawAnimation(circle, 2.0));         // 2-4s
            anim.AddToAnimations(new MoveAnimation(circle, new VXYZ(50, 0, 0), 2.0)); // 4-6s

            // Start playback
            anim.Animate();
        }
    }
}
```

### Parallel Animations

Add multiple animations as a list to play them simultaneously:

```csharp
var anim = new Animator();

// These play in parallel (both start at 0s, both last 2s)
anim.AddToAnimations(new List<Animation> {
    new FadeInAnimation(shape1, 2.0),
    new FadeInAnimation(shape2, 2.0)
});

// This plays after the parallel group finishes (starts at 2s)
anim.AddToAnimations(new DrawAnimation(line, 1.0));  // 2-3s

anim.Animate();
```

### Animation Types

| Animation | Description | Constructor |
|-----------|-------------|-------------|
| **DrawAnimation** | Progressive drawing (0% to 100%) | `new DrawAnimation(shape, duration)` |
| **MoveAnimation** | Move by displacement vector | `new MoveAnimation(shape, displacement, duration)` |
| **PathAnimation** | Move along any ICurve path | `new PathAnimation(shape, path, duration)` |
| **RotateAnimation** | Rotate around a pivot point | `new RotateAnimation(shape, pivot, angleDegrees, duration)` |
| **FlipAnimation** | Mirror across an axis line | `new FlipAnimation(shape, mirrorAxis, duration)` |
| **FadeInAnimation** | Fade from transparent to opaque | `new FadeInAnimation(shape, duration)` |
| **FadeOutAnimation** | Fade from opaque to transparent | `new FadeOutAnimation(shape, duration, targetOpacity)` |
| **ValueAnimation\<T\>** | Animate any numeric property on a shape | `new ValueAnimation<VCircle>(circle, c => c.Radius, 0, 50, 3.0)` |
| **ValueAnimation\<T\>** | Animate through a sequence of values | `new ValueAnimation<VCircle>(circle, c => c.Radius, new List<double> { 10, 50, 20, 80 }, 3.0)` |
| **ObjectPropertyAnimation\<T\>** | Animate any numeric property on any object | `new ObjectPropertyAnimation<Wheel>(wheel, w => w.Rotation, 0, 360, 1.0)` |

### ValueAnimation Example

`ValueAnimation<T>` animates any numeric (`double`) property on a shape. The property is specified with a lambda expression. You can animate between two values, or through a sequence of values:

```csharp
// Pulsing circle — animate radius from 10 to 80
var circle = new VCircle(0, 0, 10);
var anim = new Animator();
anim.AddToAnimations(new ValueAnimation<VCircle>(circle, c => c.Radius, 10, 80, 2.0));
anim.Repeat = true;
anim.Animate();

// Growing rectangle — animate width
var rect = new VRectangle(0, 0, 20, 50);
var anim2 = new Animator();
anim2.AddToAnimations(new ValueAnimation<VRectangle>(rect, r => r.Width, 20, 200, 3.0));
anim2.Animate();

// With easing for smooth motion
var circle2 = new VCircle(100, 0, 5);
var valAnim = new ValueAnimation<VCircle>(circle2, c => c.Radius, 5, 60, 2.0);
valAnim.EasingFunction = EasingFunctions.EaseInOutCubic;
var anim3 = new Animator();
anim3.AddToAnimations(valAnim);
anim3.Animate();

// Animate through multiple values — radius goes 10 → 50 → 20 → 80
var circle3 = new VCircle(-100, 0, 10);
var anim4 = new Animator();
anim4.AddToAnimations(new ValueAnimation<VCircle>(
    circle3, c => c.Radius, new List<double> { 10, 50, 20, 80 }, 3.0));
anim4.Animate();
```

### ObjectPropertyAnimation Example

`ObjectPropertyAnimation` works like `ValueAnimation` but targets any object, not just shapes. This is useful for animating properties on user-defined classes:

```csharp
public class Wheel
{
    VCircle c = new VCircle(0, 0, 100);
    VCircle hub = new VCircle(new VXYZ(40, 40), 10);

    private double rotation = 0.0;
    public double Rotation
    {
        get { return rotation; }
        set { set_rotation(value); rotation = value; }
    }

    private void set_rotation(double value)
    {
        hub.Rotate(new VXYZ(0, 0), value - rotation);
    }
}

// In Main():
var wheel = new Wheel();
var anim = new Animator();
anim.AddToAnimations(new ObjectPropertyAnimation<Wheel>(wheel, w => w.Rotation, 0.0, 359.0, 1));
anim.Repeat = true;
anim.Animate();
```

### PathAnimation Example

`PathAnimation` moves a shape along any `ICurve` path (bezier, arc, spline, polyline, etc.):

```csharp
var dot = new VCircle(0, 0, 5) { Color = "Yellow" };
var path = new VBezier(0, 0, 50, 100, 150, 100, 200, 0) { Color = "Gray" };

var anim = new Animator();
anim.AddToAnimations(new PathAnimation(dot, path, 3.0));
anim.Repeat = true;
anim.Animate();
```

### Pausing Between Animations

Insert a time gap between sequential animations:

```csharp
var anim = new Animator();

anim.AddToAnimations(new DrawAnimation(line, 2.0));    // 0-2s
anim.Pause(5);                                          // 2-7s: nothing happens
anim.AddToAnimations(new DrawAnimation(circle, 2.0));  // 7-9s

anim.Animate();
```

### Easing Functions

Smooth animations with built-in easing:

```csharp
var anim = new MoveAnimation(shape, displacement, start, duration);
anim.EasingFunction = EasingFunctions.EaseInOutQuad;  // Smooth start and end
```

#### Available Easing Functions

| Function | Formula | Effect |
|----------|---------|--------|
| `Linear` | t | Constant speed |
| `EaseInQuad` | t² | Slow start, accelerates |
| `EaseOutQuad` | t(2-t) | Fast start, decelerates |
| `EaseInOutQuad` | Piecewise | Slow start & end |
| `EaseInCubic` | t³ | Slower start |
| `EaseOutCubic` | (t-1)³+1 | Slower end |
| `EaseInOutCubic` | Piecewise | Smooth start & end |

---

## Console Output

Use `VizConsole` to output debug messages:

```csharp
VizConsole.Log("Starting visualization...");
VizConsole.Log($"Circle radius: {circle.Radius}");

// Collections are itemized by default
var nums = new List<int> { 1, 2, 3 };
VizConsole.Log(nums);           // Prints each item on its own line
VizConsole.Log(nums, false);    // Prints "System.Collections.Generic.List`1[System.Int32]"

// Empty collections show "(empty)" instead of no output
var empty = new List<int>();
VizConsole.Log(empty);          // Prints "(empty)"
```

Output appears in the console panel below the canvas with file and line number tracking:
```
[StartViz:15] Starting visualization...
[StartViz:16] Circle radius: 50
[StartViz:19] 1
[StartViz:19] 2
[StartViz:19] 3
```

---

## Canvas Features

### Interactive Controls
- **Mouse Wheel**: Zoom in/out centered on cursor position
- **Middle-Click Drag**: Pan the canvas view
- **Grid Toggle**: Show/hide reference grid lines (View menu)
- **Auto Zoom Extents**: Automatically fits all shapes after execution

### Coordinate System
Code2Viz uses a **mathematical coordinate system**:
- Origin (0, 0) is at the center of the canvas
- X-axis increases to the right (+X = right)
- Y-axis increases upward (+Y = up, not down like screen coordinates)
- Angles are measured in degrees, counter-clockwise from the positive X-axis

---

## Shape Editing

### Selecting Shapes
- **Click** a shape on the canvas to select it
- **Shift+Click** to add to selection
- **Ctrl+Click** to toggle selection
- **Drag right** on empty area for **Window Selection** (blue solid box, selects shapes fully inside)
- **Drag left** on empty area for **Crossing Selection** (green dashed box, selects shapes that intersect)
- **Ctrl+A** to select all shapes
- **Escape** to deselect

### Control Points
When a shape is selected, control point handles appear for interactive editing. Each shape type has specific control points:

| Shape | Control Points |
|-------|---------------|
| **VPoint** | Move handle at position |
| **VLine** | Move at midpoint, vertices at start/end |
| **VCircle** | Move at center, radius handle |
| **VArc** | Move at center, radius handle, vertices at start/end angles |
| **VRectangle** | Move at center, vertices at corners |
| **VEllipse** | Move at center, RadiusX and RadiusY handles |
| **VPolygon** | Move at centroid, vertex at each point |
| **VPolyline** | Move at centroid, vertex at each point |
| **VBezier** | Move at midpoint, vertices at P0/P3, curve controls at P1/P2 |
| **VSpline** | Move at centroid, curve control at each point |
| **VArrow** | Move at midpoint, vertices at start/end |
| **VText** | Move at location |
| **VDimension** | Move at midpoint, vertices at Point1/Point2 |
| **VRadialDimension** | Move at center, vertex at leader end |

Drag any control point to edit the shape geometry. The source code updates automatically when you release.

### Properties Panel
Open via **Windows > Properties** menu. The panel shows:
- **Shape info**: Type, ID, and editable Name
- **Geometry**: Shape-specific numeric properties (coordinates, radii, dimensions)
- **Style**: Color and fill color (with color picker), line weight slider, opacity slider, visibility toggle

The panel can be **floated** as a separate window or **docked** to the right side of the main window using the Dock/Float button in the panel header. Multi-selection shows common style properties.

---

## Drawing Tools

Code2Viz includes an interactive drawing toolbar that lets you create shapes directly on the canvas with automatic C#/F# code generation.

### Toolbar Location
The drawing toolbar appears below the menu bar with buttons for all shape types.

### Drawing Methods

| Shape | Method | Clicks |
|-------|--------|--------|
| **Point** | Single click | 1 |
| **Line** | Click start, click end | 2 |
| **Circle** | Click center, click radius point | 2 |
| **Rectangle** | Click corner, click opposite corner | 2 |
| **Ellipse** | Click center, drag for radii | 2 |
| **Arc** | Click center, click start, click end | 3 |
| **Polygon** | Click vertices, double-click to close | N + double-click |
| **Polyline** | Click points, double-click to finish | N + double-click |
| **Bezier** | Click start, ctrl1, ctrl2, end | 4 |
| **Spline** | Click control points, double-click | N + double-click |
| **Arrow** | Click start, click end | 2 |
| **Text** | Click position | 1 |

### Snap Support
While drawing, the tool automatically snaps to various geometric features. Visual indicators show snap points as you move the cursor.

#### Basic Snap Types
| Snap Type | Marker | Description |
|-----------|--------|-------------|
| **Endpoint** | Yellow square | Start/end points of lines, arcs, polylines |
| **Midpoint** | Cyan triangle | Middle point of lines and curves |
| **Center** | Magenta circle | Center of circles, arcs, ellipses |
| **Intersection** | Red X | Where two shapes cross |
| **Nearest** | Green diamond | Closest point on any curve |

#### Advanced Snap Types

##### Extension Snap
When placing the second point (or subsequent points), the **Extension** snap shows a dotted line extending from endpoints of existing lines, polylines, polygons, and rectangles.

- **Visual**: A dotted cyan line extends along the direction of the edge
- **Label**: Shows "Extension: [distance] < [angle]°" with the distance from the endpoint and the angle
- **Magnetic Effect**: The cursor stays snapped to the extension line within a tolerance, allowing you to draw precise aligned lines
- **Reach**: Extension lines are detected up to 300 pixels from the source endpoint

##### Perpendicular Snap
When picking the second point, the **Perpendicular** snap shows the point that creates a perpendicular relationship from your first click to an existing line or curve.

- **Visual**: An orange dotted line from your first point to the perpendicular point on the target shape
- **Use Case**: Perfect for drawing lines at 90° to existing geometry

##### Tangent Snap
When picking the second point near a circle or arc, the **Tangent** snap shows the tangent point where a line from your first click would touch the circle.

- **Visual**: A violet dotted line from your first point to the tangent point on the circle/arc
- **Use Case**: Drawing lines that touch circles at exactly one point

### Precise Distance and Angle Input

While drawing (after placing the first point), you can type precise values for distance and angle instead of clicking.

#### How to Use
1. **Start drawing** (e.g., Line tool) and click to place the first point
2. **Move cursor** over the canvas - you'll see the preview line
3. **Type a number** (e.g., "100") - Distance input mode activates automatically
   - The current distance is shown pre-selected; typing replaces it
4. **Press Tab** to switch to Angle input mode
5. **Type the angle** in degrees (e.g., "45")
6. **Press Enter** to place the point at the specified distance and angle
7. **Press Escape** to cancel input mode

#### Input Mode Indicators
- When typing distance: `Extension: [100_] < 45°` (brackets show active field)
- When typing angle: `Extension: 100.00 < [45_]°`

#### Keys in Input Mode
| Key | Action |
|-----|--------|
| `0-9`, `.`, `-` | Type value (first keystroke replaces pre-selected value) |
| `Tab` | Cycle through modes: Distance → Angle → None |
| `Backspace` | Delete last character (or clear all if value is selected) |
| `Enter` | Confirm and place point at specified distance/angle |
| `Escape` | Cancel input mode |

This feature works for all multi-point drawing tools (Line, Polyline, Polygon, etc.) and enables CAD-style precise drawing without needing to calculate coordinates manually.

### Orthogonal Constraint (Shift Key)
When drawing lines, polylines, polygons, splines, arrows, or bezier curves:
- Hold **Shift** after placing the first point to constrain the line to horizontal or vertical
- The constraint automatically chooses the axis with the larger movement
- Status bar shows "(Shift: ortho)" hint when the feature is available
- Works with snap points - the constraint is applied before snapping

### Automatic Code Generation
When you complete drawing a shape, the corresponding code is automatically inserted into the `Main()` method of your entry point file:

```csharp
// Generated when you draw a line from (100, 50) to (200, 150)
new VLine(100.00, 50.00, 200.00, 150.00).Draw();

// Generated when you draw a circle at (150, 100) with radius 75.5
new VCircle(150.00, 100.00, 75.50).Draw();
```

### Drawing Tool Shortcuts
When the editor is not focused:
| Shortcut | Action |
|----------|--------|
| `P` | Point tool |
| `L` | Line tool |
| `C` | Circle tool |
| `R` | Rectangle tool |
| `Shift` (hold) | Orthogonal constraint (H/V lock) |
| `Esc` | Cancel drawing / Return to select mode |

---

## Shape IDs and Outliner

### Unique Shape IDs
Every shape has a unique `Id` property (long integer) automatically assigned when created. The ID counter resets on each code execution, so IDs always start from 1:

```csharp
var circle = new VCircle(0, 0, 50);
var line = new VLine(0, 0, 100, 100);
VizConsole.Log($"Circle ID: {circle.Id}");  // 1
VizConsole.Log($"Line ID: {line.Id}");      // 2
```

### Outliner Panel
The Outliner panel (below the Explorer) displays all shapes grouped by type:
- Shows shape count per type: "VCircle (3)"
- Each shape displays its name and clickable ID
- Click an ID to zoom the canvas to that shape
- **Hover over any shape** to highlight it on the canvas with a colored overlay
- Right-click for **Expand All** / **Collapse All** options

### Highlight Settings
The Outliner hover highlight can be customized in the Settings tab (Application Settings):
- **Highlight Color**: Choose any color for the highlight overlay (default: Yellow)
- **Highlight Opacity**: Adjust transparency from 10% to 100% (default: 40%)

### Zoom To Shape
Use **View > Zoom To Shape** (or `Ctrl+G`) to zoom to a specific shape by entering its ID.

---

## Measuring Tape Tool

Code2Viz includes a precision measuring tool with AutoCAD-style snap features.

### Activating the Tool
Press **Ctrl+M** to toggle the Measuring Tape tool. Press **Esc** to cancel.

### How to Measure
1. Press **Ctrl+M** to activate the tool
2. Move the mouse - snap indicators appear near snap points
3. **Click first point** (snaps if within tolerance)
4. Move mouse - a dashed measuring line shows with live distance
5. **Click second point** - measurement displayed in status bar
6. Tool stays active for additional measurements; press **Esc** to exit

### Snap Types
The measuring and drawing tools support 8 snap types (configurable in Settings):

| Snap Type | Marker | Description |
|-----------|--------|-------------|
| **Endpoint** | Yellow square | Start/end points of lines, arcs, polylines |
| **Midpoint** | Cyan triangle | Middle point of lines and curves |
| **Center** | Magenta circle | Center of circles, arcs, ellipses |
| **Intersection** | Red X | Where two shapes cross |
| **Nearest** | Green diamond | Closest point on any curve |
| **Perpendicular** | Orange right-angle | Perpendicular from first click point to existing geometry |
| **Extension** | Cyan dotted line | Extended line along existing edges |
| **Tangent** | Violet line | Tangent point from first click to circles/arcs |

### Snap Settings
Configure snap behavior in the Settings tab (Application Settings > Snap Settings):
- Toggle each snap type on/off individually
- All 8 snap types can be independently enabled/disabled
- Settings are saved globally and persist across sessions

---

## Export Options

### PNG Export
File > Export > PNG (or Ctrl+E) exports the current canvas view as a PNG image.

### GIF Export
File > Export GIF Animation exports animations as animated GIF files with options:
- **Duration**: Animation length in seconds (1-30s)
- **Frame Rate**: 5-30 FPS
- **Background**: Current canvas, white, or black
- **Include Grid**: Optionally include grid and axes

### Video Export (MP4)
File > Export Video (MP4) exports animations as H.264 MP4 video files. The export renders vector graphics at the target resolution for crisp, sharp output.

#### Animation Settings
| Setting | Range | Description |
|---------|-------|-------------|
| **Duration** | 1-60 seconds | Length of the exported video |
| **Frame Rate** | 15, 30, 45, 60 FPS | Higher = smoother motion, larger file |
| **Bitrate** | 1-20 Mbps | Higher = better quality, larger file |

#### Resolution Presets
| Preset | Dimensions | Use Case |
|--------|------------|----------|
| **Canvas Size** | Current window size | Quick export at screen resolution |
| **720p** | 1280×720 | Web/social media, smaller files |
| **1080p** | 1920×1080 | Full HD, good balance of quality/size |
| **4K** | 3840×2160 | Maximum quality, large files |
| **Custom** | User-defined | Any resolution from 64 to 4096 pixels |

#### Background Options
- **Current Canvas Background**: Uses your canvas background color
- **White**: Clean white background
- **Black**: Dark background for contrast

#### Additional Options
- **Include Grid & Axes**: Toggle grid lines in the export

#### Technical Notes
- Uses Windows Media Foundation H.264 encoder (no external dependencies)
- Renders vectors at target resolution using high DPI for sharp lines and text
- Aspect ratio is preserved; letterbox/pillarbox filled with background color
- Dimensions automatically adjusted to even numbers (H.264 requirement)

### DXF Export
File > Export > DXF exports shapes to AutoCAD DXF format (R12 ASCII):
- Compatible with AutoCAD, LibreCAD, and other CAD software
- Supports all shape types (lines, circles, arcs, polygons, text, etc.)
- Preserves geometry with high precision

### PDF Export
File > Export > PDF exports shapes to vector PDF format:
- High-quality vector graphics output
- Preserves colors and stroke styles
- Suitable for printing and documentation

### SVG Export
File > Export > SVG exports shapes to SVG (Scalable Vector Graphics) format:
- Web-compatible vector format
- Opens in any browser or vector editor (Inkscape, Illustrator)
- XML-based, can be edited as text
- Supports all shape types with full styling

---

## Boolean Operations

Code2Viz provides polygon boolean operations using the Clipper2 library. For curve-bounded regions, see also [Region Boolean Operations](#region-boolean-operations).

### Available Operations

```csharp
var poly1 = new VPolygon(new VXYZ(0,0), new VXYZ(100,0), new VXYZ(100,100), new VXYZ(0,100));
var poly2 = new VPolygon(new VXYZ(50,50), new VXYZ(150,50), new VXYZ(150,150), new VXYZ(50,150));

// Union - combine two polygons (returns single VPolygon or null if disjoint)
var union = poly1.Union(poly2);
union?.Draw();

// Intersection - get overlapping area
var intersection = poly1.Intersect(poly2);

// Difference - subtract poly2 from poly1
var difference = poly1.Difference(poly2);

// XOR - symmetric difference
var xor = poly1.Xor(poly2);
```

### Utility Methods

```csharp
// Check if point is inside polygon
bool inside = polygon.Contains(new VXYZ(50, 50));

// Calculate polygon area
double area = polygon.GetArea();

// Offset polygon (positive = outward, negative = inward)
var offsetPolygons = BooleanOps.OffsetPolygon(polygon, 10);

// Simplify polygon (remove redundant points)
var simplified = BooleanOps.Simplify(polygon, tolerance: 0.1);
```

---

## Array/Pattern Operations

Create arrays and patterns of shapes with built-in array operations.

### Linear Array

```csharp
var circle = new VCircle(0, 0, 20);

// Array along X axis: 5 copies, 50 units apart
circle.LinearArrayX(5, 50).DrawAll();

// Array along Y axis: 4 copies, 40 units apart
circle.LinearArrayY(4, 40).DrawAll();

// Array along custom direction
circle.LinearArray(new VXYZ(1, 1, 0), 6, 30).DrawAll();
```

### Rectangular Array

```csharp
var rect = new VRectangle(0, 0, 30, 20);

// 3 rows, 4 columns with spacing
rect.RectangularArray(rows: 3, cols: 4, rowSpacing: 40, colSpacing: 50).DrawAll();
```

### Circular Array

```csharp
var shape = new VCircle(50, 0, 10);
var center = new VXYZ(0, 0);

// 8 copies in full circle
shape.CircularArray(center, count: 8).DrawAll();

// 6 copies spanning 180 degrees
shape.CircularArray(center, count: 6, totalAngleDegrees: 180).DrawAll();
```

### Path Array

```csharp
var marker = new VCircle(0, 0, 5);
var path = new VSpline(new VXYZ(0,0), new VXYZ(50,100), new VXYZ(100,0));

// 10 markers along the path, aligned to path direction
marker.PathArray(path, count: 10, alignToPath: true).DrawAll();
```

### Spiral Array

```csharp
var dot = new VCircle(0, 0, 3);
var center = new VXYZ(0, 0);

// 30 dots in a spiral from radius 20 to 100, 2 revolutions
dot.SpiralArray(center, count: 30, startRadius: 20, endRadius: 100, totalRevolutions: 2).DrawAll();
```

### Mirror

```csharp
var triangle = new VPolygon(new VXYZ(0,0), new VXYZ(50,0), new VXYZ(25,40));
var mirrorAxis = new VLine(0, -50, 0, 50);  // Y-axis

// Creates original + mirrored copy
triangle.Mirror(mirrorAxis).DrawAll();
```

---

## Keyboard Shortcuts

### Running Code
| Shortcut | Action |
|----------|--------|
| `F5` | Run code |
| `Ctrl+Enter` | Run code |

### File Operations
| Shortcut | Action |
|----------|--------|
| `Ctrl+Shift+N` | New project |
| `Ctrl+N` | New file |
| `Ctrl+O` | Open project |
| `Ctrl+S` | Save all files |

### Editor Operations
| Shortcut | Action |
|----------|--------|
| `Ctrl+Shift+F` | Format code |
| `Ctrl+/` | Toggle comment |
| `Tab` / `Shift+Tab` | Indent / Unindent |

### Find and Replace
| Shortcut | Action |
|----------|--------|
| `Ctrl+F` | Open Find dialog |
| `Ctrl+H` | Open Find and Replace dialog |
| `F3` | Find Next (in dialog) |
| `Shift+F3` | Find Previous (in dialog) |

### Line Operations
| Shortcut | Action |
|----------|--------|
| `Alt+Up` | Move line up |
| `Alt+Down` | Move line down |
| `Shift+Alt+Up` | Copy line up |
| `Shift+Alt+Down` | Copy line down |
| `Ctrl+Shift+D` | Delete line |

### Selection Operations
| Shortcut | Action |
|----------|--------|
| `Shift+Alt+Right` | Expand selection (word → brackets → line → block) |
| `Shift+Alt+Left` | Shrink selection (undo expand) |
| `Ctrl+D` | Add next occurrence with multi-cursor support |
| `Ctrl+Shift+L` | Select all occurrences |
| `Ctrl+Alt+Up` | Add cursor above |
| `Ctrl+Alt+Down` | Add cursor below |
| `Esc` | Exit multi-cursor mode |

### Multi-Cursor Editing
Code2Viz supports VS Code-style multi-cursor editing:
1. **Ctrl+D**: Selects word at cursor, then adds next occurrences
2. **Ctrl+Alt+Up/Down**: Adds cursors vertically above/below
3. **Type**: Text is inserted at ALL cursor positions simultaneously
4. **Ctrl+V**: Paste at all cursor positions
5. **Backspace/Delete**: Works at all cursor positions
6. **Arrow Keys**: Move all cursors (Left/Right/Up/Down)
7. **Home/End**: Move all cursors to line start/end
8. **Shift+Arrow/Home/End**: Extend selections at all cursors
9. **Escape**: Exits multi-cursor mode
10. **Click elsewhere**: Clears all multi-cursors

All cursors are visually indicated with white caret lines, and selections are highlighted.

### Canvas & Tools
| Shortcut | Action |
|----------|--------|
| `Mouse Wheel` | Zoom |
| `Middle Click` | Pan |
| `Double-click` (empty space) | Zoom to fit all shapes |
| `Ctrl+G` | Zoom to shape by ID |
| `Ctrl+M` | Toggle Measuring Tape tool |
| `F4` | Toggle Properties panel |
| `F9` | Toggle Snap to Grid |
| `Ctrl+Shift+M` | Toggle Minimap |
| `Esc` | Cancel current tool/operation |

### Code Navigation & Intellisense
| Shortcut | Action |
|----------|--------|
| `F12` | Go to Definition |
| `Shift+F12` | Find All References |
| `Alt+F12` | Peek Definition |
| `Ctrl+.` | Quick Fix (add missing using) |
| `Ctrl+Shift+O` | Document Symbols (outline) |
| `Ctrl+T` | Workspace Symbols (search all files) |
| `Ctrl+Shift+H` | Call Hierarchy |
| `Ctrl+Shift+T` | Type Hierarchy |
| `F2` | Rename Symbol |

---

## Intellisense & Code Editor Features

Code2Viz includes a full-featured code editor with VSCode-like intellisense powered by Roslyn.

### Autocomplete
- **Automatic**: Triggered on typing `.`, `(`, `<`, `{`, `[`, or `Ctrl+Space`
- **Fuzzy matching**: Type partial names (e.g., "clr" matches "Color", "VPt" matches "VPoint") with intelligent scoring that rewards prefix matches, camelCase alignment, and consecutive character runs
- **Context-aware**: Completions adapt to context -- object initializer properties, generic type arguments, attribute types, and more
- **Scope-prioritized**: Local variables and parameters appear first, followed by class members, then imported types
- **Documentation sidecar**: A documentation panel appears beside the completion list showing the signature, summary, parameters, and return type of the selected item
- **Incremental compilation**: Uses a cached Roslyn workspace that incrementally updates only changed files, keeping completions responsive even in large projects
- **Recently-used tracking**: Recently selected completions are boosted in future rankings
- **Signature Help**: Parameter info displayed when typing method calls
- **Snippets**: Code snippets for common patterns (if, for, foreach, etc.)

### Code Navigation
| Feature | Shortcut | Description |
|---------|----------|-------------|
| **Go to Definition** | `F12` | Jump to the definition of a symbol |
| **Peek Definition** | `Alt+F12` | View definition in an inline popup without leaving current location |
| **Find All References** | `Shift+F12` | Find all usages of a symbol (results in console) |
| **Document Symbols** | `Ctrl+Shift+O` | Quick outline of current file (classes, methods, properties) |
| **Workspace Symbols** | `Ctrl+T` | Search symbols across all project files |

### Code Analysis
| Feature | Description |
|---------|-------------|
| **Error Squiggles** | Real-time error highlighting with red underlines |
| **Hover Tooltips** | Documentation and type info on mouse hover |
| **Quick Fixes** | `Ctrl+.` suggests fixes like adding missing `using` statements |

### Refactoring
| Feature | Shortcut | Description |
|---------|----------|-------------|
| **Rename** | `F2` | Rename symbol across all usages |
| **Format Document** | `Ctrl+Shift+F` | Auto-format entire document |
| **Format on Type** | Automatic | Formats line when typing `;` or `}` |

### Find and Replace
| Feature | Shortcut | Description |
|---------|----------|-------------|
| **Find** | `Ctrl+F` | Search in current file |
| **Find and Replace** | `Ctrl+H` | Search and replace in current file |
| **Find in Files** | Edit menu | Search across all project files |
| **Find Next** | `F3` | Jump to next match |
| **Find Previous** | `Shift+F3` | Jump to previous match |

**Search Options:**
- **Case sensitive**: Match exact case
- **Whole word**: Match complete words only
- **Regular expressions**: Use regex patterns for advanced searches

**Find Results Panel:**
- Results displayed in a tabbed panel below the canvas
- Click any result to navigate to that location
- Results show file name, line number, and matching text

### Advanced Features
| Feature | Toggle | Description |
|---------|--------|-------------|
| **Semantic Highlighting** | View menu | Colors identifiers by semantic meaning (parameters, fields, methods, types) |
| **Inlay Hints** | View menu | Shows parameter names and inferred types inline |
| **Code Lens** | View menu | Shows reference counts above methods and types |
| **Breadcrumb Navigation** | Always on | Shows current location (namespace > class > method) at top of editor |
| **Call Hierarchy** | `Ctrl+Shift+H` | Shows callers and callees of a method |
| **Type Hierarchy** | `Ctrl+Shift+T` | Shows base types and derived types |

### Semantic Highlighting Colors
When enabled, identifiers are colored based on their meaning:
- **Light Blue**: Local variables, parameters, fields, properties
- **Light Yellow**: Methods
- **Teal**: Classes, structs
- **Light Green**: Interfaces, enums, type parameters
- **Cyan**: Constants, enum members, static fields

---

## Project Structure

### File Format
Code2Viz projects use `.vizcode` files. All files in the same directory (and subdirectories) are compiled together.

### Entry Point
The entry point must be `StartViz.Viz.Main()` in `StartViz.vizcode`:

```csharp
namespace StartViz
{
    public class Viz
    {
        public static void Main()
        {
            // Your code here
        }
    }
}
```

### Project Explorer
The Project Explorer panel (right side) shows all files and folders in your project.

**Drag and Drop**: Move files and folders between directories by dragging them in the tree view. Entry point files (`StartViz.cs`/`StartViz.fs`) and the root project node cannot be moved. Open file tabs and references update automatically after a move.

**Context Menu** (right-click any file or folder):
- **New File** / **New Folder** - Create new items
- **Rename** - Rename files or folders
- **Delete** - Delete files or folders (with confirmation)
- **Go to Location** - Open the file or folder location in Windows File Explorer

### Available Namespaces
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Code2Viz.Geometry;    // Shapes: VPoint, VLine, VCircle, etc.
using Code2Viz.Animation;   // Timeline, DrawAnimation, MoveAnimation, etc.
using Code2Viz.Console;     // VizConsole.Log()
```

---

## NuGet Package Manager

Code2Viz includes a built-in NuGet Package Manager to add external libraries to your projects.

### Opening the Package Manager
Tools > NuGet Package Manager (or use the toolbar button)

### Features
- **Search**: Search the NuGet repository for packages by name
- **Install**: Select a package and version, then click Install to add it to your project
- **Update**: If a newer version is available, you can update existing packages
- **Remove**: Remove packages you no longer need

### Using Installed Packages
After installing a package, add its namespace to your code:

```csharp
using Newtonsoft.Json;  // Example: after installing Newtonsoft.Json

namespace StartViz
{
    public class Viz
    {
        public static void Main()
        {
            var obj = new { Name = "Test", Value = 42 };
            var json = JsonConvert.SerializeObject(obj);
            VizConsole.Log(json);
        }
    }
}
```

### Package Storage
Packages are stored in a `.packages` folder within your project directory. This folder is created automatically when you install your first package.

---

## Geometry Utilities

### VXYZ - 3D Vector
```csharp
var v = new VXYZ(10, 20, 0);
double length = v.GetLength();
var normalized = v.Normalize();
var cross = v1.CrossProduct(v2);
var dot = v1.DotProduct(v2);

// Rotate a vector around the Z-axis
var rotated = v.Rotate(90);  // Returns new VXYZ rotated 90 degrees

// Static basis vectors
var x = VXYZ.BasisX;  // (1, 0, 0)
var y = VXYZ.BasisY;  // (0, 1, 0)
var z = VXYZ.BasisZ;  // (0, 0, 1)
```

### Common Shape Methods
All shapes inherit from `Shape` and support these methods:
```csharp
// Shapes appear automatically - Draw() is optional (kept for backwards compatibility)
var copy = shape.Clone();        // Create a copy
shape.Move(new VXYZ(10, 20, 0)); // Translate
shape.Rotate(pivot, 45);         // Rotate 45 degrees around pivot
shape.Scale(center, 2.0);        // Scale by factor
BoundingBox bounds = shape.GetBounds();  // Get bounding box
// bounds.Min, bounds.Max, bounds.Width, bounds.Height, bounds.Center, bounds.Area
bool hit = shape.Contains(point);// Point containment test
double d = shape.DistanceTo(pt); // Distance to point
shape.Hide();                    // Hide shape from canvas
shape.Show();                    // Show hidden shape
shape.BringAbove(otherShape);   // Render on top of otherShape
shape.SendBehind(otherShape);   // Render behind otherShape
```

### BoundingBox
The `GetBounds()` method returns a `BoundingBox` object with Min and Max corner points:
```csharp
BoundingBox bounds = shape.GetBounds();
VXYZ min = bounds.Min;        // Lower-left corner
VXYZ max = bounds.Max;        // Upper-right corner
double w = bounds.Width;      // Width (Max.X - Min.X)
double h = bounds.Height;     // Height (Max.Y - Min.Y)
VXYZ c = bounds.Center;       // Center point
double a = bounds.Area;       // Width * Height

// Methods
bool inside = bounds.Contains(point);        // Point containment
bool overlaps = bounds.Intersects(other);    // Intersection test
BoundingBox combined = bounds.Union(other);  // Combine bounds
BoundingBox bigger = bounds.Expand(10);      // Expand by distance

// Tuple deconstruction (backwards compatible)
var (minPt, maxPt) = shape.GetBounds();
```

### ICurve Interface
Shapes that represent curves (VLine, VCircle, VArc, VEllipse, VPolyline, VBezier, VSpline) implement the `ICurve` interface. Since `ICurve` extends `IDrawable`, all curves can be drawn directly:

```csharp
// Work with curves generically
ICurve curve = new VLine(0, 0, 100, 50);
curve.Draw();  // ICurve extends IDrawable

// Curve operations
VXYZ start = curve.StartPoint;
VXYZ end = curve.EndPoint;
List<VXYZ> vertices = curve.Vertices;  // Key vertices/control points
double length = curve.GetLength();

// Divide curve into segments
List<VXYZ> points = curve.Divide(10);  // 11 points (including start/end)

// Measure points at fixed intervals
List<VXYZ> measured = curve.Measure(25);  // Points every 25 units

// Project a point onto the curve
VXYZ closest = curve.Project(new VXYZ(50, 50));

// Get point at specific distance along curve
VXYZ midPoint = curve.PointAtSegmentLength(length / 2);

// Get point at normalized parameter (0 to 1)
VXYZ quarterPoint = curve.PointAtParameter(0.25);  // 25% along the curve

// Get parameter for a point on the curve (inverse of PointAtParameter)
double param = curve.ParameterAtPoint(quarterPoint);  // Returns ~0.25

// Create offset curve
ICurve offset = curve.Offset(10);
offset.Draw();

// Split curve at a point
var (first, second) = curve.SplitAtPoint(midPoint);

// Get normal vector at a point
VXYZ normal = curve.NormalAtPoint(midPoint);

// Check if curve is self-intersecting
bool selfIntersects = curve.SelfIntersecting;

// Intersect with another curve
IntersectionResult result = curve.Intersect(otherCurve);
if (result.HasIntersection)
{
    foreach (var pt in result.Points)
        new VPoint(pt.X, pt.Y).Draw();  // Draw intersection points
}
```

### Curve Intersection
All ICurve types support intersection detection:

```csharp
var line1 = new VLine(0, 0, 100, 100);
var line2 = new VLine(0, 100, 100, 0);
var circle = new VCircle(50, 50, 30);

// Line-Line intersection
var result = line1.Intersect(line2);
if (result.IsSinglePoint)
    VizConsole.Log($"Lines cross at: {result.Points[0]}");

// Line-Circle intersection (may have 0, 1, or 2 points)
var circleResult = line1.Intersect(circle);
VizConsole.Log($"Found {circleResult.Points.Count} intersections");

// Check for overlapping segments (collinear lines)
if (result.HasOverlap)
    foreach (var overlapCurve in result.Curves)
        overlapCurve.Draw();
```

### Self-Intersection Detection
The `SelfIntersecting` property indicates whether a curve crosses itself:

```csharp
// Simple curves are never self-intersecting
var line = new VLine(0, 0, 100, 100);
VizConsole.Log($"Line self-intersects: {line.SelfIntersecting}");  // false

// Complex curves may self-intersect
var polyline = new VPolyline(
    new VXYZ(0, 0),
    new VXYZ(100, 0),
    new VXYZ(50, 50),
    new VXYZ(50, -50)  // crosses back over
);
VizConsole.Log($"Polyline self-intersects: {polyline.SelfIntersecting}");  // true
```

---

## Example: Complete Drawing

```csharp
using Code2Viz.Geometry;
using System;

namespace StartViz
{
    public class Viz
    {
        public static void Main()
        {
            // Set global styling
            ShapeDefaults.GlobalColor = "Cyan";
            ShapeDefaults.GlobalLineWeight = 2;

            // Draw coordinate axes
            new VArrow(-150, 0, 150, 0).Draw();  // X-axis
            new VArrow(0, -150, 0, 150).Draw();  // Y-axis

            // Draw a house
            var house = new VPolygon(
                new VXYZ(-50, -50),
                new VXYZ(50, -50),
                new VXYZ(50, 30),
                new VXYZ(0, 70),
                new VXYZ(-50, 30)
            );
            house.FillColor = "#40FFFF00";
            house.Draw();

            // Door
            var door = new VRectangle(-15, -50, 30, 50);
            door.FillColor = "#80804000";
            door.Draw();

            // Window
            var window = new VCircle(25, 0, 15);
            window.FillColor = "#8000FFFF";
            window.Draw();

            // Sun
            var sun = new VCircle(100, 100, 25);
            sun.Color = "Yellow";
            sun.FillColor = "#80FFFF00";
            sun.Draw();

            // Sun rays
            for (int i = 0; i < 8; i++)
            {
                double angle = i * 45 * Math.PI / 180;
                double x1 = 100 + 35 * Math.Cos(angle);
                double y1 = 100 + 35 * Math.Sin(angle);
                double x2 = 100 + 50 * Math.Cos(angle);
                double y2 = 100 + 50 * Math.Sin(angle);
                var ray = new VLine(x1, y1, x2, y2);
                ray.Color = "Yellow";
                ray.Draw();
            }

            VizConsole.Log("House drawing complete!");
        }
    }
}
```

---

## Building and Running

### Prerequisites
- .NET 9.0 SDK
- Windows (WPF application)

### Build
```bash
cd Code2Viz
dotnet restore
dotnet build
```

### Run
```bash
dotnet run
```

---

## Dependencies

- **AvalonEdit** (6.3.0.90) - Code editor with syntax highlighting
- **Microsoft.CodeAnalysis.CSharp** (4.8.0) - Roslyn compilation for C# code execution
- **FSharp.Compiler.Service** - F# compilation support
- **NuGet.Protocol** - Package management integration

---

## Getting Help

- **Built-in Help**: Help > API Reference (F1) opens comprehensive documentation
- **Welcome Page**: The Help window shows a getting-started guide by default
- **Console Output**: Use `VizConsole.Log()` for debugging

---

## License

This project is for experimental/educational purposes.
