# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Code2Viz is a WPF 2D geometry visualization application that allows users to write C# or F# code to create and visualize shapes on an interactive canvas. Users write code in `.vizcode` files, which are compiled at runtime using Roslyn and executed to render shapes.

## Tech Stack
- **Framework**: WPF on .NET 9.0
- **Code Editor**: AvalonEdit (6.3.0.90)
- **Code Compilation**: Roslyn CSharpCompilation (Microsoft.CodeAnalysis.CSharp 4.8.0)
- **Boolean Operations**: Clipper2
- **PDF Export**: PDFsharp
- **Coordinate System**: Mathematical (Y-up, origin at center)

## Commands
```bash
# Build
dotnet build

# Run
dotnet run

# Run tests
dotnet test Tests/Code2Viz.Tests.csproj
```

## Architecture

### Project Structure
```
Code2Viz/
├── Geometry/           # Shape classes (VPoint, VLine, VArc, VCircle, VRectangle, etc.)
├── Canvas/             # RenderCanvas (zoom/pan), CanvasRenderer, DrawingTool, SnapEngine
├── Console/            # VizConsole (output), ConsoleOutput (singleton collector)
├── Editor/             # Code editor: IntelliSenseProvider, SemanticHighlighter, CodeLensProvider, Minimap
├── Execution/          # ModuleCompiler (Roslyn CSharpCompilation)
├── Project/            # VizCodeFile, VizCodeProject, Templates
├── Animation/          # Animator, animation types (Draw, Move, Rotate, Fade, etc.)
├── Mcp/                # McpBridgeHost (named pipe listener for MCP integration)
├── McpBridge/          # Shared IPC library (separate project)
├── McpServer/          # MCP Server console app (separate project)
├── Tests/              # Unit tests (separate project)
├── MainWindow.xaml     # UI layout (tabbed editor, console panel)
└── App.xaml            # Dark theme resources
```

### Module System
- **Entry Point**: `StartViz.Viz.Main()` in `StartViz.vizcode`
- All `.vizcode` files in the same directory are compiled together
- Available imports: `Code2Viz.Geometry`, `Code2Viz.Animation`, `Code2Viz.Console`

### Shape System
- All shapes extend `Shape` abstract class which implements `IDrawable`
- Curve shapes (VLine, VCircle, VArc, etc.) also implement `ICurve` interface
- **Shapes auto-register on construction** - no need to call `Draw()`
- `Draw()` is kept for backwards compatibility but is a no-op
- Each shape overrides `GetControlPoints()` and `MoveControlPoint()` for interactive editing

### Code Execution (ModuleCompiler)
- Compiles all `.vizcode` files using Roslyn CSharpCompilation
- Creates in-memory assembly with unique name
- Invokes `StartViz.Viz.Main()` via reflection
- Uses collectible AssemblyLoadContext for proper unloading
- Shape ID counter resets on each code execution (IDs start from 1)

### Coordinate System
- Origin (0,0) at canvas center
- Y-axis points UP (mathematical, not screen coordinates)
- WorldToScreen/ScreenToWorld methods handle conversion
- Animation loop uses `CompositionTarget.Rendering` for vsync-aligned frame updates

### Type Aliases (in RenderCanvas.cs)
Due to naming conflicts with WPF types, use these aliases:
- `Point2D` = `Code2Viz.Geometry.VPoint`
- `Line2D` = `Code2Viz.Geometry.VLine`
- `Rectangle2D` = `Code2Viz.Geometry.VRectangle`
- `Ellipse2D` = `Code2Viz.Geometry.VEllipse`
- `Polygon2D` = `Code2Viz.Geometry.VPolygon`
- `Polyline2D` = `Code2Viz.Geometry.VPolyline`

## Key Implementation Notes

1. **Always use type aliases in RenderCanvas.cs** to avoid WPF conflicts
2. **Y-coordinate is inverted** in WorldToScreen (mathematical coords)
3. **Syntax highlighting files** are embedded resources (EmbeddedResource in csproj)
4. **Colors** parsed via WPF ColorConverter - any named color works
5. **Working directory** is set to project folder during execution - relative paths resolve from there
6. **VPoint.Internal(x, y)** creates points without auto-registration (for intermediate calculations)
7. **Every Draw* method in RenderCanvas must handle DrawFactor** - check `DrawFactor <= 0` for early return, implement partial drawing logic, and apply OffsetX/OffsetY for MoveAnimation support. See DrawPolyline as the reference pattern for segment-based shapes.

## Keyboard Shortcuts (Key Bindings)

### File/Run
- `F5` / `Ctrl+Enter` - Run code
- `Ctrl+S` - Save all files
- `Ctrl+Shift+N` - New project
- `Ctrl+N` - New file
- `Ctrl+O` - Open project

### Editor
- `Ctrl+/` - Toggle comment
- `Ctrl+F` - Find, `Ctrl+H` - Find and Replace
- `Ctrl+Shift+F` - Format code
- `Alt+Up/Down` - Move line up/down
- `Ctrl+D` - Add next occurrence (multi-cursor)
- `Ctrl+Shift+L` - Select all occurrences

### Code Navigation
- `F12` - Go to Definition
- `Shift+F12` - Find All References
- `Alt+F12` - Peek Definition
- `F2` - Rename Symbol
- `Ctrl+.` - Quick Fix

### Canvas & Tools
- `Ctrl+M` - Measuring Tape tool
- `Ctrl+G` - Zoom to shape by ID
- `F4` - Toggle Properties panel
- `F9` - Toggle Snap to Grid
- `Ctrl+Shift+M` - Toggle Minimap
- `Esc` - Cancel current tool

### Drawing Tools (when editor not focused)
- `P` - Point, `L` - Line, `C` - Circle, `R` - Rectangle
- `Shift` (hold) - Orthogonal constraint

## Documentation Policy (MANDATORY)

**After ANY code change that affects the public API (new classes, methods, properties, or signature changes), you MUST update ALL of the following:**

1. **README.md** - Update examples, API tables, and feature descriptions
2. **Help Documentation (DocGenerator.cs)** - Update `_summaries`, `_csharpSamples`, `_fsharpSamples`, and `_memberDescriptions` dictionaries
3. **MCP Server Documentation**:
   - `McpServer/SKILL.md` - Update the skill reference with new types, constructors, and examples
   - `McpServer/Resources/ApiReferenceResource.cs` - Update the API reference resource content
4. **CLAUDE.md** - Update if the change affects project structure or key implementation details
5. **Commit and Push** - After all documentation is updated, commit all changes and push to remote

This is non-negotiable. No compromise on documentation, ever.

## /update-docs Command

When the user says "update all documentation", "update docs", or "/update-docs":

1. **Review recent changes**: `git log --oneline -20` and `git diff HEAD~5 --stat`
2. **Read all documentation files** to understand current state
3. **Update each file as needed**:
   - `docs/TASKS.md` - Mark completed tasks, add new tasks
   - `docs/TODO.md` - Move completed items, update current items
   - `docs/PRD.md` - Update feature status
   - `CLAUDE.md` - Update Current State, Known Issues, implementation details
   - `README.md` - Update feature descriptions, examples, API tables, keyboard shortcuts
   - `DocGenerator.cs` - Update summaries and samples for new/changed members
4. **Report summary** of all updates made
