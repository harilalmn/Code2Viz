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
├── Editor/             # Code editor features: IntelliSenseProvider, SemanticHighlighter, CodeLensProvider, HierarchyProvider
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
- Curve shapes (VLine, VCircle, VArc, etc.) also implement `ICurve` interface
- `ICurve` extends `IDrawable`, so all curves can be drawn via the interface
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

## Drawing Tools

### Overview
Interactive drawing toolbar allows creating shapes directly on the canvas with automatic code generation.

### Toolbar Location
Below the menu bar, contains buttons for all 12 shape types:
- **Basic Shapes**: Point, Line, Circle, Rectangle, Ellipse, Arc
- **Multi-Point Shapes**: Polygon, Polyline, Bezier, Spline
- **Special Shapes**: Arrow, Text

### Drawing Methods
| Shape | Method | Clicks |
|-------|--------|--------|
| Point | Single click | 1 |
| Line | Click start, click end | 2 |
| Circle | Click center, click radius point | 2 |
| Rectangle | Click corner, click opposite corner | 2 |
| Ellipse | Click center, drag for radii | 2 |
| Arc | Click center, click start, click end | 3 |
| Polygon | Click vertices, double-click to close | N+double |
| Polyline | Click points, double-click to finish | N+double |
| Bezier | Click start, ctrl1, ctrl2, end | 4 |
| Spline | Click control points, double-click | N+double |
| Arrow | Click start, click end | 2 |
| Text | Click position | 1 |

### Snap Support
- Snaps to existing shape endpoints, midpoints, centers
- Snaps to intersections and nearest points
- Visual indicators show snap points during drawing

### Orthogonal Constraint
- Hold **Shift** after first point to constrain to horizontal/vertical
- Works with Line, Polyline, Polygon, Spline, Bezier, Arrow tools
- Status bar shows "(Shift: ortho)" hint when available

### Code Generation
When a shape is completed, corresponding C# code is automatically inserted into the `Main()` method of the entry point file.

Example generated code:
```csharp
new VLine(100.00, 50.00, 200.00, 150.00).Draw();
new VCircle(150.00, 100.00, 75.50).Draw();
```

### Key Files
- `Canvas/DrawingTool.cs` - Tool state machine and shape creation logic
- `Canvas/CodeGenerator.cs` - Generates C# code strings for shapes

## Current State (v2.0)
- Module system with multi-file support
- Tabbed code editor
- Console output with line tracking
- All basic and extended shapes implemented
- Shape styling works (colors, thickness)
- Canvas zoom/pan/grid working
- Code editor with syntax highlighting
- PNG export functional
- Drawing toolbar with auto code generation
- All keyboard shortcuts working

## Known Issues
- None currently

## Future Plans (see docs/TODO.md)
- SVG export
- Shape selection
- Undo/redo for drawing operations

## Keyboard Shortcuts
### File Operations
- `F5` - Run code
- `Ctrl+Enter` - Run code
- `Ctrl+Shift+N` - New project
- `Ctrl+N` - New file
- `Ctrl+O` - Open project
- `Ctrl+S` - Save all files
- `Ctrl+Shift+F` - Format code

### Editor Operations
- `Ctrl+/` - Toggle comment (single/multi-line)

### Line Operations
- `Alt+Up` - Move line up
- `Alt+Down` - Move line down
- `Shift+Alt+Up` - Copy line up
- `Shift+Alt+Down` - Copy line down
- `Ctrl+Shift+D` - Delete line

### Selection Operations
- `Shift+Alt+Right` - Expand selection (word → brackets → line → block)
- `Shift+Alt+Left` - Shrink selection (undo expand)
- `Ctrl+D` - Add next occurrence (multi-cursor support)
- `Ctrl+Shift+L` - Select all occurrences
- `Esc` - Exit multi-cursor mode

### Multi-Cursor Editing
- First `Ctrl+D` selects word at cursor
- Subsequent `Ctrl+D` adds next occurrence with new cursor
- Type to insert at all cursors simultaneously
- Backspace/Delete work at all cursor positions
- Click elsewhere or `Esc` to exit multi-cursor mode

### Canvas & Tools
- `Ctrl+M` - Toggle Measuring Tape tool
- `Ctrl+G` - Zoom to shape by ID
- `Esc` - Cancel current tool/operation

### Drawing Tools (when editor not focused)
- `P` - Point tool
- `L` - Line tool
- `C` - Circle tool
- `R` - Rectangle tool
- `Shift` (hold) - Orthogonal constraint (H/V lock)
- `Esc` - Cancel drawing / Return to select mode

### Code Navigation & Intellisense
- `F12` - Go to Definition
- `Shift+F12` - Find All References
- `Alt+F12` - Peek Definition (inline popup)
- `Ctrl+.` - Quick Fix (add missing using)
- `Ctrl+Shift+O` - Document Symbols (outline)
- `Ctrl+T` - Workspace Symbols (search all files)
- `Ctrl+Shift+H` - Call Hierarchy
- `Ctrl+Shift+T` - Type Hierarchy
- `F2` - Rename Symbol

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
