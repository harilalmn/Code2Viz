# Code2Viz - 2D Geometry Visualizer

A WPF application for visualizing 2D geometric shapes through C# code execution.

## Overview

Code2Viz is a simple yet powerful tool that allows users to write C# code to create and visualize 2D geometric shapes on an interactive canvas. It combines a code editor with syntax highlighting and a real-time rendering canvas with zoom and pan capabilities.

## Features

### Supported Shapes

| Shape | Description | Constructor |
|-------|-------------|-------------|
| **Point** | A single point marker | `Point(x, y)` |
| **Line** | A line segment | `Line(p1, p2)` or `Line(x1, y1, x2, y2)` |
| **Arc** | A circular arc | `Arc(center, radius, startAngle, endAngle)` |
| **Circle** | A complete circle | `Circle(center, radius)` or `Circle(x, y, radius)` |
| **Rectangle** | A rectangle | `Rectangle(x, y, width, height)` |
| **Ellipse** | An ellipse | `Ellipse(centerX, centerY, radiusX, radiusY)` |
| **Polygon** | A closed polygon | `Polygon(p1, p2, p3, ...)` |
| **Polyline** | Connected line segments | `Polyline(p1, p2, p3, ...)` |

### Shape Styling

All shapes support customizable styling:

```csharp
Circle circle = new Circle(0, 0, 50);
circle.StrokeColor = "Red";           // Border color
circle.FillColor = "LightYellow";     // Fill color
circle.StrokeThickness = 3;           // Border thickness
circle.Draw();
```

**Supported Colors**: Any named color (Red, Blue, LimeGreen, etc.) or hex values (#FF0000)

### Canvas Features

- **Mouse Wheel Zoom**: Scroll to zoom in/out, centered on cursor position
- **Middle-Click Pan**: Hold middle mouse button and drag to pan
- **Grid Display**: Toggleable grid lines (50 unit spacing)
- **Coordinate Axes**: X and Y axes displayed at origin
- **Auto Zoom Extents**: Automatically fits all shapes after execution
- **Real-time Coordinates**: Mouse position displayed in footer

### Code Editor

- **Syntax Highlighting**: Full C# syntax highlighting with custom colors for geometry classes
- **Line Numbers**: Visible line numbers for easy reference
- **Code Formatting**: Press `Ctrl+Shift+F` to auto-format code
- **File Operations**: New, Open, Save functionality

### Export

- **PNG Export**: Export the current canvas view to a PNG image file

## Keyboard Shortcuts

### File Operations
| Shortcut | Action |
|----------|--------|
| `F5` | Run code |
| `Ctrl+Enter` | Run code |
| `Ctrl+New` | New file |
| `Ctrl+O` | Open file |
| `Ctrl+S` | Save file |
| `Ctrl+Shift+F` | Format code |

### Editor Operations
| Shortcut | Action |
|----------|--------|
| `Ctrl+C` | Copy |
| `Ctrl+V` | Paste |
| `Ctrl+X` | Cut |
| `Ctrl+Z` | Undo |
| `Ctrl+Y` | Redo |
| `Ctrl+A` | Select all |
| `Ctrl+D` | Duplicate line |
| `Ctrl+Shift+D` | Delete line |
| `Ctrl+Up` | Move line up |
| `Ctrl+Down` | Move line down |
| `Ctrl+/` | Toggle comment |
| `Tab` | Indent |
| `Shift+Tab` | Unindent |

## Usage Example

```csharp
// Create a square with points
Point p1 = new Point(0, 0);
Point p2 = new Point(100, 0);
Point p3 = new Point(100, 100);
Point p4 = new Point(0, 100);

// Draw the square outline
new Line(p1, p2).Draw();
new Line(p2, p3).Draw();
new Line(p3, p4).Draw();
new Line(p4, p1).Draw();

// Add a circle in the center
Circle circle = new Circle(50, 50, 30);
circle.StrokeColor = "Blue";
circle.FillColor = "LightBlue";
circle.Draw();

// Draw corner points
p1.Draw();
p2.Draw();
p3.Draw();
p4.Draw();
```

## Coordinate System

Code2Viz uses a **mathematical coordinate system**:
- Origin (0, 0) is at the center of the canvas
- X-axis increases to the right
- Y-axis increases upward (not downward like screen coordinates)
- Angles are measured in degrees, counter-clockwise from the positive X-axis

## Building and Running

### Prerequisites

- .NET 8.0 SDK
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

## Project Structure

```
Code2Viz/
├── Geometry/           # Shape classes
│   ├── IDrawable.cs    # Base interface and Shape class
│   ├── Point2D.cs      # Point shape
│   ├── Line2D.cs       # Line shape
│   ├── Arc2D.cs        # Arc shape
│   ├── Circle2D.cs     # Circle shape
│   ├── Rectangle2D.cs  # Rectangle shape
│   ├── Ellipse2D.cs    # Ellipse shape
│   ├── Polygon2D.cs    # Polygon shape
│   └── Polyline2D.cs   # Polyline shape
├── Canvas/             # Rendering components
│   ├── RenderCanvas.cs # Custom canvas with zoom/pan
│   └── CanvasRenderer.cs # Shape collection manager
├── Editor/             # Code editor components
│   ├── CSharpHighlighting.xshd # Syntax highlighting
│   └── CodeFormatter.cs # Code formatting
├── Execution/          # Script execution
│   └── ScriptRunner.cs # Roslyn-based C# execution
├── MainWindow.xaml     # Main UI layout
├── MainWindow.xaml.cs  # Main window logic
└── App.xaml            # Application resources
```

## Dependencies

- **AvalonEdit** (6.3.0.90) - Code editor with syntax highlighting
- **Microsoft.CodeAnalysis.CSharp.Scripting** (4.8.0) - Roslyn scripting for C# code execution

## License

This project is for experimental/educational purposes.
