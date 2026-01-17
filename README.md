<p align="center">
  <img src="img/logo.png" alt="Code2Viz Logo" width="200">
</p>

# Code2Viz - 2D Geometry Visualizer

A WPF application for visualizing 2D geometric shapes through C# or F# code execution with animation support.

## Overview

Code2Viz is a visual programming environment that lets you write C# or F# code to create, style, and animate 2D geometric shapes on an interactive canvas. It combines a code editor with syntax highlighting, a real-time rendering canvas with zoom and pan capabilities, and a timeline-based animation system with GIF export.

## Features

- **Multi-language Support**: Write code in C# or F# with full syntax highlighting
- **Rich Shape Library**: Points, lines, circles, rectangles, ellipses, arcs, polygons, polylines, Bezier curves, splines, text, arrows, and dimension annotations
- **Animation System**: Create timeline-based animations with draw, move, rotate, and flip effects
- **Interactive Canvas**: Zoom with mouse wheel, pan with middle-click, toggle grid display
- **Export Options**: Save visualizations as PNG images or animated GIFs
- **Project Management**: Organize multiple code files into projects with tabbed editing
- **NuGet Integration**: Add external packages to extend functionality
- **Built-in Help**: Comprehensive API documentation with examples

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
            // Create and draw a circle
            var circle = new VCircle(0, 0, 50);
            circle.StrokeColor = "Cyan";
            circle.FillColor = "#4000FFFF";
            circle.Draw();
        }
    }
}
```

### 3. Run Your Code
Press **F5** or click the **Run** button to execute and see results on the canvas.

---

## Supported Shapes

| Shape | Description | Constructor Examples |
|-------|-------------|---------------------|
| **VPoint** | A point marker | `new VPoint(x, y)` |
| **VLine** | A line segment | `new VLine(p1, p2)` or `new VLine(x1, y1, x2, y2)` |
| **VCircle** | A circle | `new VCircle(center, radius)` or `new VCircle(x, y, radius)` or `new VCircle(p1, p2, p3)` (circumcircle) |
| **VRectangle** | A rectangle | `new VRectangle(corner, width, height)` |
| **VEllipse** | An ellipse | `new VEllipse(center, radiusX, radiusY)` |
| **VArc** | A circular arc | `new VArc(center, radius, startAngle, endAngle)` |
| **VPolygon** | A closed polygon | `new VPolygon(p1, p2, p3, ...)` |
| **VPolyline** | Open connected segments | `new VPolyline(p1, p2, p3, ...)` |
| **VBezier** | Cubic Bezier curve | `new VBezier(start, ctrl1, ctrl2, end)` |
| **VSpline** | Smooth spline curve | `new VSpline(p1, p2, p3, ...)` |
| **VText** | Text at a position | `new VText(position, "text")` |
| **VArrow** | Arrow with head | `new VArrow(start, end)` |
| **VDimension** | Dimension annotation | `new VDimension(p1, p2)` |
| **VGroup** | Group of shapes | `new VGroup()` then `.Add(shape)` |

---

## Shape Styling

All shapes support customizable styling through these properties:

```csharp
var circle = new VCircle(0, 0, 50);
circle.StrokeColor = "Cyan";           // Outline color
circle.FillColor = "#4000FFFF";        // Fill color (with transparency)
circle.StrokeThickness = 2.5;          // Border thickness
circle.Draw();
```

### Color Formats
- **Named colors**: `"Red"`, `"Blue"`, `"Cyan"`, `"LimeGreen"`, etc.
- **Hex RGB**: `"#FF0000"` (red)
- **Hex ARGB**: `"#80FF0000"` (semi-transparent red, where 80 is alpha)

### Global Defaults

Set default styling for all new shapes:

```csharp
ShapeDefaults.GlobalStrokeColor = "Cyan";
ShapeDefaults.GlobalFillColor = "Transparent";
ShapeDefaults.GlobalStrokeThickness = 2.0;

// All shapes created after this use these defaults
var circle = new VCircle(0, 0, 50);
circle.Draw();  // Uses Cyan stroke

// Reset to original defaults
ShapeDefaults.Reset();
```

---

## Animation System

Code2Viz includes a timeline-based animation system for creating animated visualizations.

### Basic Animation Example

```csharp
using Code2Viz.Geometry;
using Code2Viz.Animation;
using System.Collections.Generic;

namespace StartViz
{
    public class Viz
    {
        public static void Main()
        {
            // Create shapes
            var line = new VLine(0, 0, 100, 50);
            var circle = new VCircle(50, 50, 30);
            circle.StrokeColor = "Yellow";

            // Create timeline with shapes
            var shapes = new List<Shape> { line, circle };
            var timeline = new Timeline(shapes);
            timeline.Duration = 5.0;  // 5 seconds
            timeline.Repeat = true;   // Loop animation

            // Add animations
            timeline.AddAnimation(new DrawAnimation(line, startTime: 0.0, duration: 2.0));
            timeline.AddAnimation(new DrawAnimation(circle, startTime: 0.5, duration: 2.0));
            timeline.AddAnimation(new MoveAnimation(circle, new VXYZ(50, 0, 0), startTime: 2.0, duration: 2.0));

            // Start playback
            timeline.Play();
        }
    }
}
```

### Animation Types

| Animation | Description | Constructor |
|-----------|-------------|-------------|
| **DrawAnimation** | Progressive drawing (0% to 100%) | `new DrawAnimation(shape, startTime, duration)` |
| **MoveAnimation** | Move by displacement vector | `new MoveAnimation(shape, displacement, startTime, duration)` |
| **RotateAnimation** | Rotate around a pivot point | `new RotateAnimation(shape, pivot, angleDegrees, startTime, duration)` |
| **FlipAnimation** | Mirror across an axis line | `new FlipAnimation(shape, mirrorAxis, startTime, duration)` |

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
```

Output appears in the console panel below the canvas with file and line number tracking:
```
[StartViz:15] Starting visualization...
[StartViz:16] Circle radius: 50
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
While drawing, the tool automatically snaps to:
- **Endpoints** - Start/end points of existing shapes
- **Midpoints** - Middle points of lines and curves
- **Centers** - Center of circles, arcs, ellipses
- **Intersections** - Where two shapes cross
- **Nearest** - Closest point on any curve

Visual indicators show snap points as you move the cursor.

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
| `Esc` | Cancel drawing / Return to select mode |

---

## Shape IDs and Outliner

### Unique Shape IDs
Every shape has a unique `Id` property (long integer) automatically assigned when created:

```csharp
var circle = new VCircle(0, 0, 50);
var line = new VLine(0, 0, 100, 100);
VizConsole.Log($"Circle ID: {circle.Id}");  // e.g., 1
VizConsole.Log($"Line ID: {line.Id}");      // e.g., 2
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
The measuring tool supports 6 snap types (configurable in Settings):

| Snap Type | Marker | Description |
|-----------|--------|-------------|
| **Endpoint** | Yellow square | Start/end points of lines, arcs, polylines |
| **Midpoint** | Cyan triangle | Middle point of lines and curves |
| **Center** | Magenta circle | Center of circles, arcs, ellipses |
| **Intersection** | Red X | Where two shapes cross |
| **Nearest** | Green diamond | Closest point on any curve |
| **Perpendicular** | Orange right-angle | Perpendicular from first click point |

### Snap Settings
Configure snap behavior in the Settings tab (Application Settings > Snap Settings):
- Toggle each snap type on/off individually
- Settings are saved globally and persist across sessions

---

## Export Options

### PNG Export
File > Export > PNG (or Ctrl+E) exports the current canvas view as a PNG image.

### GIF Export
File > Export > GIF exports animations as animated GIF files with options:
- **Width/Height**: Output dimensions
- **Frame Rate**: 10-60 FPS
- **Duration**: Animation length in seconds
- **Loop**: Enable/disable infinite looping

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
| `Ctrl+D` | Add next occurrence (select word, then find next) |
| `Ctrl+Shift+L` | Select all occurrences |

### Canvas & Tools
| Shortcut | Action |
|----------|--------|
| `Mouse Wheel` | Zoom |
| `Middle Click` | Pan |
| `Ctrl+G` | Zoom to shape by ID |
| `Ctrl+M` | Toggle Measuring Tape tool |
| `Esc` | Cancel current tool/operation |

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

// Static basis vectors
var x = VXYZ.BasisX;  // (1, 0, 0)
var y = VXYZ.BasisY;  // (0, 1, 0)
var z = VXYZ.BasisZ;  // (0, 0, 1)
```

### Common Shape Methods
All shapes inherit from `Shape` and support these methods:
```csharp
shape.Draw();                    // Render to canvas
var copy = shape.Clone();        // Create a copy
shape.Move(new VXYZ(10, 20, 0)); // Translate
shape.Rotate(pivot, 45);         // Rotate 45 degrees around pivot
shape.Scale(center, 2.0);        // Scale by factor
var bounds = shape.GetBounds();  // Get bounding box
bool hit = shape.Contains(point);// Point containment test
double d = shape.DistanceTo(pt); // Distance to point
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
            ShapeDefaults.GlobalStrokeColor = "Cyan";
            ShapeDefaults.GlobalStrokeThickness = 2;

            // Draw coordinate axes
            new VArrow(-150, 0, 150, 0).Draw();  // X-axis
            new VArrow(0, -150, 0, 150).Draw();  // Y-axis

            // Draw a house
            var house = new VPolygon(
                new VPoint(-50, -50),
                new VPoint(50, -50),
                new VPoint(50, 30),
                new VPoint(0, 70),
                new VPoint(-50, 30)
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
            sun.StrokeColor = "Yellow";
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
                ray.StrokeColor = "Yellow";
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
