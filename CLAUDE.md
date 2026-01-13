# Viz2d - Project Context for Claude

## Project Overview
Viz2d is a WPF 2D geometry visualization application that allows users to write C# code to create and visualize shapes on an interactive canvas.

## Tech Stack
- **Framework**: WPF on .NET 8.0
- **Code Editor**: AvalonEdit (6.3.0.90)
- **Code Compilation**: Roslyn CSharpCompilation (Microsoft.CodeAnalysis.CSharp 4.8.0)
- **Coordinate System**: Mathematical (Y-up, origin at center)

## Project Structure
```
Viz2d/
├── Geometry/           # Shape classes (Point, Line, Arc, Circle, Rectangle, Ellipse, Polygon, Polyline)
├── Canvas/             # RenderCanvas (zoom/pan), CanvasRenderer (shape collection)
├── Console/            # VizConsole (output), ConsoleOutput (singleton collector)
├── Editor/             # Syntax highlighting (XSHD), CodeFormatter
├── Execution/          # ModuleCompiler (Roslyn CSharpCompilation)
├── Project/            # VizCodeFile, VizCodeProject, Templates
├── img/                # Logo assets (logo.png, logo.ico)
├── docs/               # PRD.md, TASKS.md, TODO.md
├── MainWindow.xaml     # UI layout (tabbed editor, console panel)
└── App.xaml            # Dark theme resources
```

## Module System

### File Format
- **Extension**: `.vizcode`
- **Entry Point**: `StartViz.vizcode` containing:
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

### Project Structure
- All `.vizcode` files in same folder (and subfolders) are compiled together
- Opening any `.vizcode` file auto-loads all files from that directory
- Tabbed editor allows switching between multiple open files
- New Project creates a temp directory with `StartViz.vizcode` template

### Available Imports
```csharp
using System;
using System.Collections.Generic;
using Viz2d.Geometry;    // Point, Line, Circle, Arc, Rectangle, Ellipse, Polygon, Polyline
using Viz2d.Console;     // VizConsole.Write(), VizConsole.WriteLine()
```

## Console Output

### VizConsole Class
```csharp
VizConsole.Write("message");      // Output without newline
VizConsole.WriteLine("message");  // Output with newline
VizConsole.WriteLine(42);         // Supports any object
```

### Output Format
```
[ModuleName:LineNumber] message
```
Example: `[StartViz:15] Hello World`

### Features
- Auto-captures calling file name and line number
- Console panel below canvas (resizable)
- Clear button to clear output
- Export button to save as .txt file
- Auto-clears on each Run

## Key Implementation Details

### Coordinate System
- Origin (0,0) at canvas center
- Y-axis points UP (mathematical, not screen coordinates)
- WorldToScreen/ScreenToWorld methods handle conversion

### Shape System
- All shapes extend `Shape` abstract class
- `Shape` implements `IDrawable` interface
- Shapes have: `StrokeColor`, `FillColor`, `StrokeThickness`
- Calling `Draw()` adds shape to `CanvasRenderer.Instance`

### Canvas Features
- Mouse wheel zoom (centered on cursor position)
- Middle-click pan
- Grid lines (50 unit spacing, toggleable)
- Auto ZoomExtents after code execution
- Real-time coordinate display

### Code Execution (ModuleCompiler)
- Compiles all `.vizcode` files using Roslyn CSharpCompilation
- Creates in-memory assembly with unique name
- Finds and invokes `StartViz.Viz.Main()` via reflection
- Uses collectible AssemblyLoadContext for proper unloading
- Clears canvas and console before each run

### Type Aliases (in RenderCanvas.cs)
Due to naming conflicts with WPF types:
- `Point2D` = `Viz2d.Geometry.Point`
- `Line2D` = `Viz2d.Geometry.Line`
- `Rectangle2D` = `Viz2d.Geometry.Rectangle`
- `Ellipse2D` = `Viz2d.Geometry.Ellipse`
- `Polygon2D` = `Viz2d.Geometry.Polygon`
- `Polyline2D` = `Viz2d.Geometry.Polyline`
- `WpfLine`, `WpfRectangle`, etc. = WPF shape types

## Current State (v2.0)
- Module system with multi-file support
- Tabbed code editor
- Console output with line tracking
- All basic and extended shapes implemented
- Shape styling works (colors, thickness)
- Canvas zoom/pan/grid working
- Code editor with syntax highlighting
- PNG export functional
- All keyboard shortcuts working

## Known Issues
- None currently

## Future Plans (see docs/TODO.md)
- Bezier curves and splines
- Autocomplete in editor
- SVG export
- Shape selection

## Keyboard Shortcuts
### File Operations
- `F5` - Run code
- `Ctrl+Enter` - Run code
- `Ctrl+Shift+N` - New project
- `Ctrl+N` - New file
- `Ctrl+O` - Open project
- `Ctrl+S` - Save all files
- `Ctrl+Shift+F` - Format code

### Editor Operations (custom implemented)
- `Shift+Alt+Down` - Duplicate line
- `Ctrl+Shift+D` - Delete line
- `Ctrl+Up` - Move line up
- `Ctrl+Down` - Move line down
- `Ctrl+/` - Toggle comment (single/multi-line)

### Built-in AvalonEdit
- `Ctrl+C/V/X` - Copy/Paste/Cut
- `Ctrl+Z/Y` - Undo/Redo
- `Tab/Shift+Tab` - Indent/Unindent

## Commands
```bash
# Build
dotnet build

# Run
dotnet run

# Test (none yet)
dotnet test
```

## Important Notes
1. Always use type aliases in RenderCanvas.cs to avoid WPF conflicts
2. Y-coordinate is inverted in WorldToScreen (mathematical coords)
3. Syntax highlighting file is embedded resource (EmbeddedResource in csproj)
4. Colors parsed via WPF ColorConverter - any named color works
5. Entry point must be `StartViz.Viz.Main()` in `StartViz.vizcode`
6. Use `VizConsole` instead of `Console` for output with line tracking
