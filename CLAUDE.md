# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Code2Viz is a WPF 2D geometry visualization application that allows users to write C# code to create and visualize shapes on an interactive canvas. Users write code in `.cs` files, which are compiled at runtime using Roslyn and executed to render shapes.

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
├── C2VGeometry/        # ── Referenced library (separate project): THE geometry namespace.
│                       #   Shapes (VPoint, VLine, VArc, VCircle, VRectangle, VPolygon, ...),
│                       #   VXYZ coordinate value-type, RayCaster/SpatialGrid/KDTree,
│                       #   CurveIntersection, Region, hatch, ShapeDefaults. Single source of
│                       #   truth — both Code2Viz and Animator reference it. (The old WPF-coupled
│                       #   Code2Viz.Geometry namespace was retired; see GEOMETRY_MERGE_SPIKE.md.)
├── Canvas/             # RenderCanvas (zoom/pan), CanvasRenderer (the C2VGeometry.IShapeRegistry), DrawingTool, SnapEngine
├── Console/            # VizConsole (output), ConsoleOutput (singleton collector)
├── Editor/             # Code editor: IntelliSenseProvider, SemanticHighlighter, CodeLensProvider, Minimap,
│                       #   CachedCompilationWorkspace, FuzzyMatcher, DocumentationSidecar, RoslynCompletionService
├── Execution/          # ModuleCompiler (Roslyn CSharpCompilation)
├── Project/            # VizCodeFile, VizCodeProject, Templates
├── Animator/           # ── Sub-project: p5.js-style Setup/Draw sketch app (Animator.exe)
│   ├── Canvas/         #   AnimCanvas (renders C2VGeometry shapes directly; pan-only, no zoom)
│   ├── Sketch/         #   Sketch base class + SketchRuntime + ShapeRegistry
│   ├── Compiler/       #   Roslyn single-file compiler (sketch-only, no Main() fallback)
│   ├── Editor/         #   CachedCompilationWorkspace + FuzzyMatcher + CompletionEngine + AvalonCompletionData
│   ├── Console/        #   ConsoleOutput, ConsoleLevel (Info/Warning/Error), VizConsole.Log/Warn/Error
│   └── MainWindow.xaml.cs  # AvalonEdit editor + AnimCanvas + console + Switch-to-Project button
├── Animation/          # Animator, animation types (Draw, Move, Rotate, Fade, etc.)
├── Mcp/                # McpBridgeHost (named pipe listener for MCP integration)
├── McpBridge/          # Shared IPC library (separate project)
├── McpServer/          # MCP Server console app (separate project)
├── Tests/              # Unit tests (separate project)
├── MainWindow.xaml     # UI layout (tabbed editor, console panel)
└── App.xaml            # Dark theme resources
```

### Module System
- **Entry Point**: `StartViz.Viz.Main()` in `StartViz.cs`
- All `.cs` files in the same directory are compiled together
- Available imports: `C2VGeometry`, `Code2Viz.Animation`, `Code2Viz.Console`

### Geometry namespace (`C2VGeometry`)
- **Single geometry namespace** for the whole solution (Code2Viz + Animator). The old WPF-coupled `Code2Viz.Geometry` was deleted; everything now uses `C2VGeometry`.
- **`VXYZ` is the coordinate value-type** (immutable-ish, not a `Shape`). **`VPoint` is only a drawable point marker.** Shape coordinates (`Center`, `Start`/`End`, polygon `Points`) are `VXYZ`; methods that take a position take `VXYZ`.
- **`CanvasRenderer` implements `C2VGeometry.IShapeRegistry`** and is set as `C2VGeometry.Shape.DefaultRegistry`, so `new VCircle(...)` auto-registers onto the canvas. There is no longer any `C2VGeometryAdapter`/conversion layer.
- **Charts (`C2VGeometry/Charts/`)** — `Chart` is a static helper that builds Chart.js-style charts (`Bar`/`Line`/`Scatter`/`Pie`/`Area`) by composing existing primitives (VLine, VRectangle, VPolyline, VPolygon, VText) into a single `VGroup`. To prevent the dozens of child shapes from auto-registering individually onto the canvas, the helper flips `Shape.AutoRegister = false` during construction and registers only the outer `VGroup`; the flag is restored in a `finally` block. This is the only sanctioned `Shape.AutoRegister` flip in user-construction code — never reintroduce it in `RayCaster` or other hot paths (see note 9). Pie sectors are polygon-approximated (no `VSector` shape). Because the helper emits only existing primitives, Animator gets charts for free.

### Shape System
- All shapes extend `Shape` abstract class which implements `IDrawable`
- Curve shapes (VLine, VCircle, VArc, etc.) also implement `ICurve` interface
- **Shapes auto-register on construction** - no need to call `Draw()`
- `Draw()` is kept for backwards compatibility but is a no-op
- Each shape overrides `GetControlPoints()` and `MoveControlPoint()` for interactive editing

### Code Execution (ModuleCompiler)
- Compiles all `.cs` files using Roslyn CSharpCompilation
- Creates in-memory assembly with unique name
- Invokes `StartViz.Viz.Main()` via reflection
- Uses collectible AssemblyLoadContext for proper unloading
- Shape ID counter resets on each code execution (IDs start from 1)

### Coordinate System
- Origin (0,0) at canvas center
- Y-axis points UP (mathematical, not screen coordinates)
- WorldToScreen/ScreenToWorld methods handle conversion
- Animation loop uses `CompositionTarget.Rendering` for vsync-aligned frame updates

### IntelliSense Engine (Editor/)
The IntelliSense system uses incremental Roslyn compilation for responsive completions:
- **CachedCompilationWorkspace** - Maintains a cached `CSharpCompilation` with incremental file updates (`UpdateFile`/`RemoveFile`). Avoids rebuilding the full compilation on every keystroke by using `ReplaceSyntaxTree`. Thread-safe.
- **RoslynCompletionService** - Provides context-aware completions via Roslyn's `Recommender` API. Detects context (generic type arguments, object initializers, attributes) and classifies symbol scope (Local/ClassMember/Imported/Global) for priority sorting.
- **FuzzyMatcher** - Subsequence fuzzy matching with scoring. Rewards prefix matches, word-boundary hits, camelCase alignment, and consecutive runs. Used to filter and rank completions as the user types.
- **DocumentationSidecar** - WPF `Popup` that displays XML documentation (signature, summary, parameters, returns) beside the completion window. Tracks the completion window position and updates on selection change.
- **CompletionData** - Extended with `SymbolScope`, `MatchScore`/`MatchPositions`, and `Symbol` properties. Renders match-highlight characters in bold within the completion list.

### WPF type aliases (in RenderCanvas.cs)
`RenderCanvas.cs` uses `C2VGeometry` types **directly** — there are no `*2D` geometry aliases anymore (no name clash). It only aliases the **WPF** types it also needs, to avoid clashing with geometry: `Point` = `System.Windows.Point`, plus `Brush`/`Pen`/`Color`/`Size`/`Rect` etc. from `System.Windows[.Media]`. World coordinates are `C2VGeometry.VXYZ`; screen coordinates are `System.Windows.Point`.

## Key Implementation Notes

1. **In RenderCanvas.cs, alias the WPF types** (Point, Brush, …) so they don't clash; geometry types (`VXYZ`, `VCircle`, …) are used directly
2. **Y-coordinate is inverted** in WorldToScreen (mathematical coords)
3. **Syntax highlighting files** are embedded resources (EmbeddedResource in csproj)
4. **Colors** parsed via WPF ColorConverter - any named color works
5. **Working directory** is set to project folder during execution - relative paths resolve from there
6. **Use `VXYZ` for intermediate coordinates** — it's a plain value type and never registers. **`VLine.Internal(start, end)`** creates a *non-registering* `VLine` for any code that needs a `VLine` purely as a data container — a polygon/region's internal edge segments, tessellation, self-intersection scans. A plain `new VLine(...)` auto-registers with `Shape.DefaultRegistry` (= the canvas) and pollutes the scene with phantom segments rendered in the default color (see note 10 — `VPolygon.BuildCurvesFromPoints` and `Region.FromPolygon` shipped this bug; the polygon's outline rendered as separate default-cyan edge lines until they were switched to `VLine.Internal`).
7. **Every Draw* method in RenderCanvas must handle DrawFactor** - check `DrawFactor <= 0` for early return, implement partial drawing logic, and apply OffsetX/OffsetY for MoveAnimation support. See DrawPolyline as the reference pattern for segment-based shapes.
8. **ConsolePanel must NOT span into Auto grid rows** - placing it at `Grid.Row="4"` only (no RowSpan into the Auto row 3). Spanning into Auto rows causes WPF to measure the ListBox with infinite height, making the console expand to fit all content instead of scrolling.
9. **RayCaster (`C2VGeometry/Operations/RayCaster.cs`)** is the spatial accelerator for ray queries — flat-array BVH with SAH binning, iterative traversal with `stackalloc` stack, inline ray-vs-shape math for VLine/VCircle/VArc/VEllipse/VPolygon/VPolyline. It takes an **explicit shape collection**: `new RayCaster(IEnumerable<Shape> shapes, int leafSize = 8)` — there is no canvas-snapshot constructor (the library has no canvas). App callers pass `CanvasRenderer.Instance.GetShapes()`. **`VPoint` is always excluded** from the index (zero-area visual markers, not useful ray targets); shapes with non-finite bounds (VRay, VXLine) are excluded too. Never reintroduce the `Shape.AutoRegister` flip; the hot path is intentionally allocation-free and reentrant. The slab test branches on `double.IsInfinity(invD)` so degenerate AABBs and axis-aligned rays do not produce NaN — keep this fix even though `VPoint` exclusion makes the most common trigger disappear. Use `Refit()` for small movements; rebuild for large scene changes. Tests that touch the canvas singleton or `Shape.DefaultRegistry` live in the `"CanvasState"` xUnit collection (defined with `DisableParallelization = true`, in `Tests/CanvasStateCollection.cs`) and reset state in setup/teardown — keep new such test classes in the same collection.
10. **CurveIntersection self-intersection scans must not allocate `VLine`** in their inner loops. `IsPolylineSelfIntersecting`, `IsPolygonSelfIntersecting`, and `GetSegments` all use raw-double math via the private `SegmentsIntersectRaw`/`AppendRawSegments` helpers, plus `VLine.Internal` for any real `VLine` data containers. Reason: `VLine` auto-registers on construction, so the original O(N²) inner loop was dumping ~N²/2 phantom shapes onto the canvas — a 360-vertex polygon leaked ~65k shapes and turned a sub-millisecond computation into a multi-second one. Never reintroduce `new VLine(...)` in these paths. `SharedEndpointTouchOnly` preserves the original knot-vertex exemption from `IsOnlyAtSharedEndpoints`. (Single `C2VGeometry` namespace now — geometry auto-registers via `Shape.DefaultRegistry`, which the app sets to `CanvasRenderer.Instance`; there is no second namespace to mirror.) **Same class of bug, also fixed:** `VPolygon.BuildCurvesFromPoints` and `Region.FromPolygon`/`FromPolygonWithHoles` build their internal edge representation — those now use `VLine.Internal`, not `new VLine`, so a polygon/rectangle/region doesn't dump its edges onto the canvas. Guarded by `Tests/GeometryRegistryPollutionTests.cs`.
11. **`ICurve.SetBounds(double startParameter, double endParameter)`** trims a curve in place so [s, e] becomes the new [0, 1]; inputs are clamped to [0, 1] and swapped if reversed. **Open curves** (VLine, VArc, VEllipse, VPolyline, VBezier, VSpline) trim in place; **closed/infinite curves** (VCircle, VPolygon, VRay, VXLine) throw `NotSupportedException` because their trimmed form changes shape type — the message points callers to `SplitAtPoint`. Per-shape contracts that must hold: **VLine** reassigns its `Start`/`End` (now `VXYZ` value coordinates — `VXYZ` is immutable, so trimming sets new endpoint values); **VBezier** uses De Casteljau twice (split at e, then at s/e on the left piece) for an exact trim and likewise mutates P0..P3 in place; **VSpline** resamples at `numSpans × SegmentsPerSpan × (e − s)` points — never just keep the inner control points because Catmull-Rom tangents depend on neighbors and the trimmed curve will visibly bend away from the original. `_selfIntersecting` is non-readonly on VPolyline/VBezier/VSpline so SetBounds can recompute it. (Single `C2VGeometry` namespace.)
12. **Animator sub-project (`Animator/`)** is a self-contained WPF app (`Animator.exe`) for p5.js-style sketches. **Code2Viz.csproj must `<Compile Remove="Animator\**" />` (plus Page/ApplicationDefinition/Resource/None)** or its WPF compile step picks up Animator's XAML and double-defines `MainWindow`. The two apps cross-launch via the `Switch to Animator` button (Code2Viz) and `Switch to Project` button (Animator); each app finds the other's exe by walking `..` up to the solution root and looking under `bin\{Config}\net9.0-windows\` or `Animator\bin\{Config}\net9.0-windows\`. Animator's `Sketch` base class lives in `Animator.Sketching` (the namespace was named `Sketching` not `Sketch` to avoid colliding with the class name `Sketch`). The sketch frame loop captures shapes via `C2VGeometry.Shape.DefaultRegistry`, then the `AnimCanvas` renders them directly (no adapter to `Code2Viz.Geometry`).
13. **Editor source is single-sourced in `Editor/` and *linked* into Animator.** `Animator.csproj` has a block of `<Compile Include="..\Editor\X.cs" Link="Editor\Linked\X.cs" />` items plus a linked `<Page>` for `MinimapControl.xaml`. Linked files keep the `Code2Viz.Editor` namespace — Animator just `using Code2Viz.Editor;`. **Do not fork these into `Animator/Editor/`.** Bug fixes go into `Editor/` once and flow to both apps. Animator-only editor types (e.g. `CompletionEngine`) stay in `Animator/Editor/` under namespace `Animator.Editor`. Because Animator references `Microsoft.CodeAnalysis.CSharp.Workspaces` (Code2Viz doesn't), shared files that reference `TextDocument` need a `using TextDocument = ICSharpCode.AvalonEdit.Document.TextDocument;` alias to avoid colliding with `Microsoft.CodeAnalysis.TextDocument` — applied in `SemanticHighlighter.cs`. Animator skips the multi-file project-model features (rename across files, F12 go-to-def, find-all-refs, call/type hierarchy) because it's single-file; everything else (semantic highlighting, folding, bracket/selection highlight, multi-cursor, snippets, signature help, format, doc sidecar, minimap, inlay hints) is wired in `Animator/MainWindow.xaml.cs:InitializeEditor`. **Completion is now unified:** `SharedEditorController.ShowCompletionWindow` uses `RoslynCompletionService` + `SortCompletions` + the doc-sidecar selection wiring for *both* apps (the old `#if ANIMATOR → CompletionEngine` fork was removed), so Animator gets the same fuzzy matching, scope/expected-type sorting, and per-item docs as Code2Viz. It works because Animator's workspace (`CompletionEngine.Workspace`, still used for its `CachedCompilationWorkspace`) already carries the injected global-usings tree and refs, and `GetActiveFilePath()` returns `CompletionEngine.FileId` — the same key the workspace uses (the file-id agreement from [[sec_workspace_file_id]]). `CompletionEngine.GetCompletions()` is now dead code (the class is kept only for `.Workspace`/`.FileId`).
14. **`SharedEditorController` is the shared editor host glue.** All editor wiring (TextMarkerService, bracket/selection renderers, semantic highlighter, inlay hints, code lens, snippets, folding, **realtime syntax check timer + squiggle markers**, mouse hover tooltips, context menu, F12/Shift+F12/F2/Ctrl+. key bindings, multi-cursor) is set up in one call to `_editorController.Initialize()`. Hosts wire their environment through callbacks: `GetActiveFilePath`, `GetWorkspace`, `GetActiveProject`, `SetStatusMessage`, `NavigateToLocation`, `ShowReferences`, `ApplyRefactoring`, `AutoRunCodeAsync`, `IsAutoUpdateEnabled`, `GetAutoUpdateDelayMs`. Animator uses this; Code2Viz still has its parallel implementation inlined in `MainWindow.xaml.cs` (~lines 823–2200). When you add or change editor behavior, put it in `SharedEditorController` (or another file under `Editor/`) so both apps pick it up automatically. **Squiggle rendering** lives in `Editor/TextMarkerService.cs:Draw` — amplitude is 2px sitting just under the text baseline (not 1px below the line bottom, which was nearly invisible).
15. **Welcome dialog has a Code/Animate mode toggle (`WelcomeWindow.xaml`/.cs).** Two RadioButtons in `GroupName="WelcomeMode"` switch the two action buttons' labels (driven by their `Tag` via `TemplateBinding`) and swap the recent-list source between `RecentProjectsManager` (loads `.vizproj`) and `RecentAnimationsManager` (loads `.cs` sketches). The Animate actions launch `Animator.exe` (located via `FindAnimatorExe`, same candidates as the toolbar `Switch to Animator` button), passing the chosen `.cs` path as a command-line argument when applicable. Window width was bumped from 800→900 with column ratio 1.2:1 so the longer animation labels fit on one line.
16. **`Project/RecentAnimationsManager.cs`** mirrors `RecentProjectsManager` but persists to `%AppData%\Code2Viz\recent_animations.json` and tracks `.cs` files. It is **linked** into Animator via `Animator.csproj` (`<Compile Include="..\Project\RecentAnimationsManager.cs" Link="Linked\Project\RecentAnimationsManager.cs" />`) so Animator's Open / Save / SaveAs / `MainWindow(string? initialFile)` constructor can call `RecentAnimationsManager.AddAnimation(...)`. Do not fork it into Animator.
17. **Animator startup parses command-line args (`Animator/App.xaml.cs`).** `App.xaml` no longer declares `StartupUri`; instead `App.OnStartup` walks `e.Args`, picks the first arg that resolves to an existing file, and passes it to `new MainWindow(initialFile)`. The two-constructor pattern (`MainWindow()` chains to `MainWindow(string?)`) keeps the XAML-instantiable default intact. If you re-add `StartupUri="MainWindow.xaml"`, two MainWindows will be created.
18. **Animator stops the running sketch on any non-modifier keypress in the editor.** `Editor.PreviewKeyDown` is wired to `Editor_PreviewKeyDown_StopSketch` in `Animator/MainWindow.xaml.cs`. The handler skips pure modifier keys (Ctrl/Shift/Alt/Win/Caps/Num/Scroll, and `Key.System` for Alt-combos in WPF) and any combo where Ctrl/Alt/Win is held, so shortcuts like Ctrl+S and Shift+F5 still work. Only typing actually stops the sketch; merely focusing the editor does not.
19. **CodeLens row positions must anchor to the live document, not store frozen offsets (`Editor/CodeLensProvider.cs`).** `CodeLensGenerator` renders the reference-count label by making the declaration line **2× tall** (`CodeLensElement` → `InlineObjectElement`, `Height = lineHeight * 2`) — there is no real document line, just an inline element painted in the empty upper half. `UpdateCodeLens` recompute is debounced on the 500 ms `_semanticUpdateTimer` (`SharedEditorController`), but AvalonEdit redraws touched lines on **every keystroke**. If each `CodeLensItem` holds a frozen absolute `Offset`, typing *above* a code-lensed declaration shifts the real offsets while the cached ones stay stale, so the tall row gets constructed on the wrong line and snaps back at the next debounce — visible vertical jitter while typing. Fix: each item carries a live `TextAnchor` (created via `_document.CreateAnchor`, `MovementType = AfterInsertion` so Enter-at-line-start pushes the row down *with* the declaration, `SurviveDeletion = true` so reading `.Offset` never throws), and `GetFirstInterestedOffset`/`ConstructElement` read `CurrentOffset` (the anchor's live offset). Anchors preserve relative order under edits, so the sorted item list stays sorted. **Never go back to plain `int Offset` lookups in the generator.** Shared `Editor/` source, so this flows to both Code2Viz and Animator. **A second flicker source is *existence* (not position) toggling:** a structural syntax error nearby — e.g. an unclosed generic `List<VLine` mid-type — makes Roslyn's error recovery intermittently fail to parse the *following* `Setup()` as a `MethodDeclarationSyntax`, so on alternating recomputes the item is present/absent, the 2×-tall row blinks in and out, and the code below bounces. Fix in `UpdateCodeLens`: check `syntaxTree.GetDiagnostics().Any(d => d.Severity == DiagnosticSeverity.Error)`; on a **clean** parse swap `_items` outright (authoritative — renames/removals reconcile, counts refresh), on a **broken** parse call `MergePreservingExisting` which keeps every prior item (anchors hold their position) and only *adds* declarations whose `(Kind, SymbolName)` is new — it never removes an item during an error state. A failed build (`buildSucceeded == false`) leaves `_items` untouched rather than blanking the gutter. **Don't reintroduce the unconditional `_items = newItems` swap.**
20. **`RenderCanvas.OnMouseDown` grabs keyboard focus on any click** (`if (!IsKeyboardFocusWithin) Focus();`). The canvas's keyboard shortcuts (P/L/C/R drawing tools, Delete, A=select-all, Esc) are gated on `!CodeEditor.IsKeyboardFocusWithin` in the window key handler — so if a canvas click didn't move focus off the editor, those keys would just type into the editor. The canvas is `Focusable = true`; without the explicit `Focus()` on click, focus only moved mid-draw (a chicken-and-egg). Don't remove it.
21. **Both compilers harden against runaway user recursion via `Execution/StackGuardRewriter.cs` (namespace `Code2Viz.Execution`).** User code runs **in-process**, and the runtimes already wrap it in `catch (Exception)` (`SketchRuntime.Tick`/`Start` in both apps; Main() mode's `TargetInvocationException` catch in `ModuleCompiler`) — but a **`StackOverflowException` is uncatchable** in .NET Core (the CLR fails fast and kills the process; AppDomains are gone). The rewriter injects `global::System.Runtime.CompilerServices.RuntimeHelpers.EnsureSufficientExecutionStack();` at the top of every method-like body (methods, local functions, constructors, operators, accessors, expression-bodied properties/indexers; lambdas/anonymous methods are intentionally left alone to avoid Func-vs-Expression overload shifts). That call throws a **catchable** `InsufficientExecutionStackException` *before* the stack overflows, so the existing `catch` stops execution and reports it (Animator adds a friendly "runaway recursion" message in `SketchRuntime.ReportError`). The injected statement carries **no trivia** so original statements keep their line numbers (runtime stack traces still map to the user's file); it does shift in-line **character offsets**, so it must only touch the **execute** path. **Wiring:** Code2Viz `ModuleCompiler.CreateCompilationAsync(project, injectStackGuards: true)` — the param defaults to `false` so the shared callers that map editor offsets onto the compilation (`CheckSyntaxAsync`, `RefactoringProvider` for F12/rename/find-refs, MainWindow) are unaffected; Animator `SketchCompiler.CompileAndRunAsync` always injects (sketch-only, no offset-based features) and recreates the tree via `CSharpSyntaxTree.Create(root, parseOptions, path, encoding)` for PDB mapping. **The rewriter is single-sourced in `Execution/` and *linked* into Animator** (`Animator.csproj`, `Code2Viz.Execution` namespace — same convention as `Editor/`); the Tests project gets it via the Code2Viz project reference (`Tests/StackGuardRewriterTests.cs`). Don't fork it. This only covers the StackOverflow class — infinite loops, OOM, and native crashes still need process isolation; see **`SKETCH_ISOLATION_PLAN.md`** (Phase 2). Don't remove the rewriter without that replacement in place.
22. **`SketchHost/` (`SketchHost.exe`) is the Phase-2 process-isolation child (POC stage).** A separate console process that runs user sketches out-of-process so an infinite loop, OOM, or native crash can't take down Animator (the killer case the Phase-1 stack guard can't handle). It references `Animator.csproj` (reusing `SketchCompiler`/`SketchRuntime`/`ShapeRegistry`) and runs headless. **Protocol lives in `Animator/Ipc/`** (`HostMessage.cs` = length-prefixed binary framing over the child's stdin/stdout; `ShapeCodec.cs` = encodes the 8 AnimCanvas-rendered shape types; `SketchHostClient.cs` = parent handle + frame-starvation **watchdog** that kills a hung child). Transport is **stdio, not McpBridge** (that pipe is request/response, one-message-per-connection — unfit for a 60 fps stream). **Project-structure trap:** `SketchHost/` sits under the Code2Viz project root, so `Code2Viz.csproj` MUST `<Compile Remove="SketchHost\**" />` (+ Page/Resource/None) — exactly like the `Animator\**` exclusion (see note 12), or Code2Viz's build (and its WPF `_wpftmp` step) sweeps up SketchHost's files and fails with CS0579/CS0246. **Status:** wired into the live UI behind the **`ANIMATOR_ISOLATE=1`** env-var flag (default OFF — in-process stays authoritative). `MainWindow.xaml.cs` branches on `_isolate`: `RunSketch`/`StopSketch`/`OnRendering`/`ToggleRun`/export route through a `SketchHostClient` (events marshaled to the dispatcher) vs the in-process `SketchRuntime`; `SketchIsRunning` unifies the running-state check; `AppSwitcher.FindSketchHostExe()` locates the child. Proven in the real GUI (UI-Automation Run button): isolate-mode Run spawns the child, an infinite-loop sketch is killed by the watchdog while Animator survives, no orphan child on parent exit. Remaining before it can be the default: throughput check, installer bundling, deleting the in-process path — tracked in **`SKETCH_ISOLATION_PLAN.md`**.
23. **Animator persists View-menu toggles via `Animator/AnimatorSettings.cs`** — Inlay Hints, Semantic Highlighting, Code Lens, Minimap, Console visibility + console row height live in `%AppData%\Code2Viz\animator_settings.json`. Loaded in `MainWindow.ApplyPersistedSettings()` (called from `InitializeEditor()` *after* `_editorController.Initialize()` so the controller flags exist), saved on each menu-item click + on window close (which also captures any splitter-dragged console height). Mirrors Code2Viz's `ApplicationSettings` pattern but uses a separate file so the two apps' UI states stay independent. Don't reach into Code2Viz's `Viz2d\appsettings.json` from Animator.
24. **`SketchCompiler.NeededAssemblies` is the single source of truth for the editor's reference set.** Animator's runtime sketch compilation and the editor's `CompletionEngine` workspace both call `SketchCompiler.BuildDefaultReferences()` so IntelliSense and runtime see exactly the same set of trusted-platform assemblies (System.Runtime, Collections.Immutable/Concurrent, Linq.Expressions, IO, Text.Json, Threading.Tasks, ObjectModel, ComponentModel, etc.). Kept in lockstep with Code2Viz's `ModuleCompiler` reference list. If you add a namespace there, mirror it here — divergence breaks Animator IntelliSense for the affected types. The list is in `Animator/Compiler/SketchCompiler.cs`; the helper builds the `List<MetadataReference>` from `AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")` plus the host + C2VGeometry assemblies.
25. **`SketchCompiler.GlobalUsingsSource` injects `System.Linq` and `System.Collections.Generic`** in addition to `System`, `C2VGeometry`, `Animator.Sketching`, `Animator.Console`. This lets `List<VXYZ>`, `IEnumerable<T>`, and Linq extensions resolve in any sketch without explicit `using` lines — important because the editor's `RoslynCompletionService.GetExpectedTypeFromContext` walks the variable declaration's type symbol, and unresolved generics like `List<VXYZ>` drop the expected-type boost so `new ` completions stay empty. Animator's `Templates.DefaultSketch` *also* lists those usings visibly (`using System.Linq; using System.Collections.Generic;`) for educational signal, redundant with the globals.
26. **`DrawGroup` applies the group's `OffsetX`/`OffsetY` as a `TranslateTransform`** before iterating children (`Canvas/RenderCanvas.cs:DrawGroup`). Without this, `PathAnimation`/`MoveAnimation` against a `VGroup` target had no visible effect — the animation set `group.OffsetX/Y` but the renderer drew children with only *their* offsets, ignoring the group's. The translation pushes screen units (`group.OffsetX * _viewport.Scale, -group.OffsetY * _viewport.Scale`); Y is negated because world Y-up vs screen Y-down. Don't move this into the child loop; the transform is a single push/pop wrapping the whole group.
27. **`new VPoint(...)` implicitly converts to `VXYZ`** (`C2VGeometry/Shapes/VPoint.cs`, `public static implicit operator VXYZ`). Backward-compat shim: pre-2026.5.5 projects whose canvas-drawn polygons/polylines/splines were serialized as `new VPolygon(new VPoint(...), ...)` still compile against the current `params VXYZ[]` constructors. New code should construct `VXYZ` directly because a `VPoint` constructor call auto-registers a drawable point marker on the canvas — the conversion fixes compilation but not the phantom-marker pollution. `CodeGenerator.GeneratePolygonConstructor`/`Polyline`/`Spline` and the editor's `vpoly`/`vbezier`/`vspline`/`vlinea`/`star`/`wave` snippets now emit `new VXYZ(...)` so the pollution never reappears in newly-generated code.
28. **Completion auto-trigger fires on space after `new`/`is`/`as` and on the first letter following them** (`Editor/SharedEditorController.OnTextEntered` + `IsAfterCompletionKeyword`). The previous rule waited until the *second* character of an identifier (caret-2 had to be a word char), so `new V` never popped the list — only `new Vc` would. Now the helper walks back over whitespace, pulls the preceding word, and triggers if it's a priming keyword. Shared `Editor/` source, so both apps benefit.

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
2. **Help Documentation (DocGenerator.cs)** - Update `_summaries`, `_csharpSamples`, and `_memberDescriptions` dictionaries
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
   - `CHANGELOG.md` - Add a curated, user-facing section for the upcoming release (Keep a Changelog format: Added/Changed/Fixed). This is the human summary for people browsing the repo or following releases; the GitHub release body is auto-generated from the commit log separately.
   - `DocGenerator.cs` - Update summaries and samples for new/changed members
4. **Report summary** of all updates made

## /release Command

When the user says "/release", "cut a release", "ship a release", or "release":

1. **Run `/update-docs` first** so the release ships with current documentation. Commit + push the doc changes as a *separate* commit before bumping the version.
2. **No version to choose — versioning is calendar-based (`YEAR.MONTH.PATCH`).** The script stamps `YEAR`/`MONTH` from today's date and increments `PATCH` within the same month (resetting to 0 the first time you release in a new month or year). E.g. the second May 2026 release is `2026.5.1`; the first June release is `2026.6.0`.
3. **Run `scripts\release.ps1`** (no `-Bump`) — it guards working-tree cleanliness, computes the calendar version, writes it into `Directory.Build.props` + `installer.iss` (the two version sources, kept in sync because Inno Setup doesn't read MSBuild props), commits as "Release v<new>", tags `v<new>`, and pushes main + tag to origin. Pass `-LocalBuild` to also build Release configs + installer locally for smoke-testing; CI publishes the canonical artifacts regardless.
4. **Tag push triggers `.github/workflows/release.yml`** on `windows-latest`: it verifies `Directory.Build.props` matches the tag, builds `Code2Viz.csproj` + `Animator/Animator.csproj` in Release, runs the test suite, invokes Inno Setup (`ISCC.exe`, pre-installed on the runner) to produce `installer/output/Code2Viz-<new>-Setup.exe`, then publishes the GitHub release with the installer attached. **Release notes are generated from `git log <prev-tag>..<tag>`** (commit subjects, excluding the `Release v*` bump commits) — *not* `--generate-notes`, which produces only a bare compare link here because the repo commits directly to `main` with no PRs. The curated human summary lives in `CHANGELOG.md` (updated during `/update-docs`). Watch progress at `https://github.com/harilalmn/Code2Viz/actions/workflows/release.yml`.

Never bump versions by hand — the script is the only thing that touches both `Directory.Build.props` and `installer.iss`. Never create a `v*` tag by hand either — the workflow's "verify props match tag" step fails fast if `Directory.Build.props` is out of sync with the tag, which is what catches hand-tagged releases.

**Animator ships in every release.** The installer bundles `Animator\bin\Release\net9.0-windows\*` into `{app}\Animator\` (see the `AnimatorBuildOutput` define + the wildcard `Source` line in `installer.iss`), and the release workflow's "Build Animator (Release)" step feeds that. If you ever remove either piece, `Switch to Animator` will break on installed copies — Code2Viz's `FindAnimatorExe` and Animator's `AppSwitcher.FindSiblingApp` both rely on the `{app}\Animator\Animator.exe` layout. Do not gate Animator behind a flag, do not split it into a separate installer.
