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
Code2Viz/
├── Geometry/           # Shape classes (Point, Line, Arc, Circle, Rectangle, Ellipse, Polygon, Polyline, Grid, Group)
├── Canvas/             # RenderCanvas (zoom/pan), CanvasRenderer (shape collection)
├── Console/            # VizConsole (output), ConsoleOutput (singleton collector)
├── Editor/             # Code editor features: IntelliSenseProvider, SemanticHighlighter, CodeLensProvider, HierarchyProvider
├── Execution/          # ModuleCompiler (Roslyn CSharpCompilation)
├── Project/            # VizCodeFile, VizCodeProject, Templates
├── Mcp/                # McpBridgeHost (named pipe listener, command handlers for MCP)
├── McpBridge/          # Shared IPC library (IpcClient, IpcServer, IpcMessages)
├── McpServer/          # MCP Server console app (stdio transport, tools, resources, SKILL.md)
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
using Viz2d.Geometry;    // Point, Line, Circle, Arc, Rectangle, Ellipse, Polygon, Polyline, Grid, Group
using Viz2d.Console;     // VizConsole.Log()
```

## Console Output

### VizConsole Class
```csharp
VizConsole.Log("message");        // Only method - logs with auto file/line tracking
VizConsole.Log(42);               // Accepts any object
```

### Output Format
```
[ModuleName:LineNumber] message
```
Example: `[StartViz:15] Hello World`

### Features
- `Log()` is the only public method (no `Write()` or `WriteLine()`)
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
- Shapes have: `Color`, `FillColor`, `LineWeight`, `LineType`, `LineTypeScale`, `IsVisible`, `Opacity`, `Name`, `Id`
- **Shapes auto-register on construction** - no need to call `Draw()`
- `Draw()` is kept for backwards compatibility but is a no-op
- Use `Show()` and `Hide()` methods to control visibility
- **Shape-specific control points**: Each shape overrides `GetControlPoints()` and `MoveControlPoint()` for vertex/radius/curve editing

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
The drawing tool supports 8 snap types (configurable in Settings):

| Snap Type | Marker | Description |
|-----------|--------|-------------|
| Endpoint | Yellow square | Start/end points of lines, arcs, polylines |
| Midpoint | Cyan triangle | Middle point of segments |
| Center | Magenta circle | Center of circles, arcs, ellipses |
| Intersection | Red X | Where two shapes cross |
| Nearest | Green diamond | Closest point on any curve |
| Perpendicular | Orange line | 90° from first click to existing geometry |
| Extension | Cyan dotted line | Extended line along existing edges |
| Tangent | Violet line | Tangent point from first click to circles/arcs |

### Advanced Snaps (Second Point)
When picking the second point:
- **Extension**: Shows dotted line extending from endpoints with label "Extension: [dist] < [angle]°"
- **Perpendicular**: Shows perpendicular relationship from first point to existing shapes
- **Tangent**: Shows tangent point when near circles/arcs

### Precise Distance/Angle Input
While drawing after the first point, type numbers to enter precise values:
- **Type any digit**: Starts Distance input mode (value pre-selected, typing replaces it)
- **Tab**: Cycle through None → Distance → Angle → None
- **Backspace**: Delete character (or clear all if selected)
- **Enter**: Confirm and place point at specified distance/angle
- **Escape**: Cancel input mode

Key files: `Canvas/SnapEngine.cs`, `Canvas/DrawingTool.cs` (InputMode, InputBuffer, StartDistanceInput)

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

## Properties Panel
- Floating or dockable panel showing selected shape properties
- **Files:** `PropertiesPanel.xaml/cs` (UserControl), `PropertiesWindow.xaml/cs` (floating container)
- **Sections:** Shape info (type, ID, name), Geometry (per-shape numeric properties), Style (color, fill, weight, opacity, visibility)
- **Dock/Float toggle:** Button in header switches between floating window and docked column 6 in MainWindow
- **Events:** `ShapePropertyChanged` triggers code sync and canvas refresh (includes `PropertyName` and `OldValue` for targeted sync)
- **Code sync:** Style property changes (Color, FillColor, LineWeight, Opacity, IsVisible) insert or update assignment lines in code (e.g., `c1.LineWeight = 18.0;`)
- **Variable rename:** Changing Name in properties renames the variable throughout the code via whole-word replacement
- **Settings:** `ShowProperties` and `PropertiesDocked` in `ApplicationSettings`
- **Menu:** Windows > Properties (checkable toggle)

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
- **Auto-update canvas**: Canvas updates automatically when code changes (debounced 500ms)
- **Shapes auto-register**: No need to call `Draw()` - shapes appear when created
- **Find and Replace**: Full find/replace with RegEx support, project-wide search, tabbed results panel
- **Shape-specific control points**: All 13 shape types have custom control points for vertex, radius, and curve editing
- **Properties panel**: Floating/dockable panel for editing shape geometry and style properties with full code sync
- **Style code sync**: Property changes in Properties panel persist to code (Color, FillColor, LineWeight, Opacity, IsVisible, Name)
- **Auto-deselect**: Selection cleared on Run and when clicking into the code editor
- **Snap to Grid**: Locks cursor to grid intersections during drawing/measuring/selection (F9 toggle, adaptive spacing)
- **Working directory**: Set to project folder before code execution so relative file paths resolve correctly

## Known Issues
- None currently

## Future Plans (see docs/TODO.md)
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

### Find and Replace
- `Ctrl+F` - Open Find dialog
- `Ctrl+H` - Open Find and Replace dialog
- `F3` - Find Next (in dialog)
- `Shift+F3` - Find Previous (in dialog)

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
- `Ctrl+Alt+Up` - Add cursor above
- `Ctrl+Alt+Down` - Add cursor below
- `Esc` - Exit multi-cursor mode

### Multi-Cursor Editing
- `Ctrl+D` selects word at cursor, then adds next occurrences
- `Ctrl+Alt+Up/Down` adds cursors vertically above/below
- Type to insert at all cursors simultaneously
- Backspace/Delete work at all cursor positions
- Click elsewhere or `Esc` to exit multi-cursor mode

### Canvas & Tools
- `Double-click` (empty space) - Zoom to fit all shapes
- `Ctrl+M` - Toggle Measuring Tape tool
- `Ctrl+G` - Zoom to shape by ID
- `F4` - Toggle Properties panel
- `F9` - Toggle Snap to Grid
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
7. Working directory is set to project folder during execution - relative paths resolve from there

## Documentation Policy (MANDATORY)
**After ANY code change that affects the public API (new classes, methods, properties, or signature changes), you MUST update ALL of the following:**

1. **README.md** - Update examples, API tables, and feature descriptions
2. **Help Documentation (DocGenerator.cs)** - Update `_summaries`, `_csharpSamples`, `_fsharpSamples`, and `_memberDescriptions` dictionaries with descriptions and sample code for new/changed members
3. **MCP Server Documentation** - Update both:
   - `McpServer/SKILL.md` - Update the skill reference with new types, constructors, and examples
   - `McpServer/Resources/ApiReferenceResource.cs` - Update the API reference resource content
4. **CLAUDE.md** - Update if the change affects project structure or key implementation details
5. **Commit and Push** - After all documentation is updated, commit all changes and push to remote

This is non-negotiable. No compromise on documentation, ever.
