# Task History - Viz2d Development

## Completed Tasks

### Phase 1: Project Setup
- [x] Create WPF .NET 8.0 project structure
- [x] Add NuGet packages (AvalonEdit, Roslyn)
- [x] Setup project directories (Geometry, Canvas, Editor, Execution)

### Phase 2: Core Geometry Classes
- [x] Create `IDrawable` interface
- [x] Create `Shape` abstract base class with styling properties
- [x] Implement `Point` class
- [x] Implement `Line` class
- [x] Implement `Arc` class
- [x] Implement `Circle` class

### Phase 3: Extended Geometry Classes
- [x] Implement `Rectangle` class
- [x] Implement `Ellipse` class
- [x] Implement `Polygon` class
- [x] Implement `Polyline` class

### Phase 4: Canvas Implementation
- [x] Create `RenderCanvas` custom control
- [x] Implement world-to-screen coordinate transformation
- [x] Implement screen-to-world coordinate transformation
- [x] Implement mouse wheel zoom (centered on cursor)
- [x] Implement middle-click pan
- [x] Implement `ZoomExtents()` method
- [x] Implement grid line drawing
- [x] Implement coordinate axes drawing
- [x] Add `MouseWorldPositionChanged` event

### Phase 5: Shape Rendering
- [x] Create `CanvasRenderer` singleton
- [x] Implement Point rendering
- [x] Implement Line rendering
- [x] Implement Arc rendering (using PathGeometry)
- [x] Implement Circle rendering
- [x] Implement Rectangle rendering
- [x] Implement Ellipse rendering
- [x] Implement Polygon rendering
- [x] Implement Polyline rendering
- [x] Implement color parsing from string names

### Phase 6: Code Editor
- [x] Integrate AvalonEdit component
- [x] Create C# syntax highlighting definition (XSHD)
- [x] Add geometry class highlighting (Point, Line, etc.)
- [x] Implement `CodeFormatter` class
- [x] Apply light theme to editor

### Phase 7: Script Execution
- [x] Create `ScriptRunner` class
- [x] Configure Roslyn ScriptOptions with geometry imports
- [x] Implement async code execution
- [x] Implement error handling and reporting

### Phase 8: Main Window UI
- [x] Design three-row layout (Ribbon, Content, Footer)
- [x] Implement resizable split view (Canvas | Editor)
- [x] Create ribbon with file operations
- [x] Create ribbon with Run/Clear buttons
- [x] Create ribbon with Format button
- [x] Add Export PNG button
- [x] Add Grid toggle checkbox
- [x] Display coordinates in footer
- [x] Display status messages in footer

### Phase 9: File Operations
- [x] Implement New file functionality
- [x] Implement Open file functionality
- [x] Implement Save file functionality
- [x] Add unsaved changes prompts
- [x] Implement PNG export

### Phase 10: Keyboard Shortcuts
- [x] F5 - Run code
- [x] Ctrl+N - New file
- [x] Ctrl+O - Open file
- [x] Ctrl+S - Save file
- [x] Ctrl+Shift+F - Format code

### Phase 11: Dark Theme
- [x] Define color resources in App.xaml
- [x] Style ribbon buttons
- [x] Style canvas background
- [x] Style footer

### Phase 12: Bug Fixes
- [x] Fix canvas placement issue (transform approach)
- [x] Fix Line type ambiguity (WPF vs Geometry)
- [x] Switch editor to light theme for visibility

---

### Phase 13: Animation & Selection Enhancements
- [x] Add ObjectPropertyAnimation<T> for animating numeric properties on any object
- [x] Add Animator.Fps property (1-120, default 60) for frame rate control
- [x] Switch animation loop to CompositionTarget.Rendering (vsync-aligned)
- [x] Add crossing/window selection (drag direction determines mode)
- [x] Add VizConsole.Log itemize parameter for collection output control
- [x] Add VLine constructor from start point, angle, and length
- [x] Add Auto-Draw Shapes checkbox (moved from status bar to Settings > Canvas Settings)
- [x] Reset Shape ID counter on each code execution

---

### Phase 14: Region Support & Animation Bug Fixes
- [x] Add Region shape (curve-bounded 2D area with holes support)
- [x] Add RegionBooleanOps (Union, Intersect, Difference, Xor)
- [x] Add Region rendering in RenderCanvas (DrawRegion method)
- [x] Fix DrawSpline missing DrawFactor support (broke DrawAnimation for VSpline)
- [x] Fix DrawSpline missing OffsetX/OffsetY support (broke MoveAnimation for VSpline)
- [x] Add Region case in main draw switch and VGroup child draw switch
- [x] Fix polygon Union issue (Greiner-Hormann winding order normalization)
- [x] Add C2VGeometry standalone geometry library
- [x] Add minimap with syntax coloring and viewport indicator
- [x] Add BoundingBox class and refactor Shape.GetBounds() return type
- [x] Add Area and Circumference properties to VCircle and VEllipse

---

### Phase 15: Console & UI Bug Fixes
- [x] Fix console panel resize expanding to maximum height with multiline content
- [x] Fix console scroll behavior with variable-height (multiline) entries
- [x] Remove ConsolePanel Grid.RowSpan spanning into Auto row (root cause of layout issue)
- [x] Add pixel-based virtualized scrolling (VirtualizingPanel.ScrollUnit="Pixel")
- [x] Add HorizontalContentAlignment="Stretch" for full-width selection highlight

---

### Phase 16: Project Explorer Enhancements
- [x] Add drag-and-drop file/folder moving in Project Explorer TreeView
- [x] Prevent dragging root node, entry point files, and reference items
- [x] Validate drop targets (no self-drop, no subtree drop, no same-parent drop)
- [x] Update open file references and tabs after move
- [x] Add "Go to Location" context menu item to open file/folder in Windows File Explorer

### Phase 17: UI & Multi-Cursor Fixes
- [x] Move Auto-Draw Shapes checkbox from status bar to Settings > Canvas Settings
- [x] Fix multi-cursor paste (Ctrl+V) only pasting at first cursor

---

### Phase 18: Ray Casting & Spatial Acceleration
- [x] Add `RayCaster` class with flat-array BVH and Surface Area Heuristic split
- [x] Iterative traversal with `stackalloc` index stack (no per-query heap allocation)
- [x] Inline ray-vs-shape math for VLine, VCircle, VArc, VEllipse, VPolygon (and VRectangle), VPolyline; AABB fallback for other shape types
- [x] `RayHit` and `RayQuery` readonly record structs for results and batch input
- [x] `FindIntersection(location, direction)` and `FindIntersection(location, direction, maxDistance)` for closest-hit queries
- [x] `HasIntersection(location, direction, maxDistance = +∞)` for any-hit / shadow-ray queries
- [x] `FindIntersections(IReadOnlyList<RayQuery>, parallel = true)` for parallel batch queries
- [x] `Refit()` to refresh AABBs in O(N) after shape movement without rebuilding the tree
- [x] Thread-safe queries (BVH is read-only after construction)
- [x] xUnit test coverage: closest/any-hit, max-distance pruning, arc/ellipse angle filter, rectangle/polygon edges, batch parallel-vs-sequential parity, refit correctness

---

### Phase 19: RayCaster Refinements
- [x] Replace the `IEnumerable<Shape>` constructor with a canvas-driven `new RayCaster(leafSize = 8)` that snapshots every visible `Shape` on `CanvasRenderer.Instance` at construction time (no explicit collection arg)
- [x] Always exclude `VPoint` markers from the index (zero-area visual labels, not useful ray targets) — independent of `IsVisible` or how the `VPoint` was registered
- [x] Fix ray-vs-AABB slab-test NaN when ray direction is zero on an axis intersecting a degenerate AABB (kept as defensive code even after the `VPoint` exclusion removes the most common trigger)
- [x] Add optional `List<Shape>? exclusionList = null` parameter to both `FindIntersection` overloads — converted to `HashSet<Shape>` once per query for O(1) per-leaf-shape lookup, useful for casting off a source shape or finding the next hit past a known set
- [x] Move RayCaster tests into a `"CanvasState"` xUnit collection with `DisableParallelization = true` so they don't race against other test classes that auto-register shapes; setup/teardown `Clear()`s `CanvasRenderer.Instance`

---

### Phase 20: CurveIntersection Canvas-Pollution Fix
- [x] Rewrite `IsPolylineSelfIntersecting` to use raw-double segment math via a new private `SegmentsIntersectRaw` helper — eliminates the per-iteration `new VLine(...)` allocations that were auto-registering on the canvas. A 360-vertex polygon used to dump ~65k phantom shapes; now zero. Construction time drops from ~5 s (real-world isovist case) to <1 ms.
- [x] Rewrite `IsPolygonSelfIntersecting` to flatten curves into `(sx, sy, ex, ey)` tuples via a new private `AppendRawSegments` helper and run `SegmentsIntersectRaw` directly — bypasses `GetSegments` entirely on this hot path. `SharedEndpointTouchOnly` preserves the original knot-vertex exemption from `IsOnlyAtSharedEndpoints`.
- [x] Add internal `VLine.Internal(VPoint, VPoint)` factory and `VLine(start, end, bool register)` constructor — mirrors the existing `VPoint.Internal` pattern, lets utility code allocate `VLine` data containers without auto-registering on the canvas
- [x] Update `GetSegments` to use `VLine.Internal` for the synthesised segments (the `VLine`→[line] passthrough is unchanged) — `IntersectGeneric` and any future caller now gets pollution-free segment tessellation for free
- [x] Mirror all four changes to the parallel `C2VGeometry` namespace (uses `VXYZ` instead of `VPoint`, registers with `DefaultRegistry` instead of `CanvasRenderer.Instance`)

---

### Phase 21: ICurve.SetBounds (Parameter-Range Trim)
- [x] Add `void SetBounds(double startParameter, double endParameter)` to the `ICurve` interface in both `Code2Viz.Geometry` and `C2VGeometry`. The parameter sub-range [s, e] becomes the new [0, 1]; inputs are clamped to [0, 1] and swapped if reversed.
- [x] **VLine** — Set `Start`/`End` to `Evaluate(s)`/`Evaluate(e)`. The `VPoint` instances are preserved (X/Y mutated) so external references stay live.
- [x] **VArc, VEllipse** — Rescale `StartAngle`/`EndAngle` so the new endpoints sit at the trimmed parameters.
- [x] **VBezier** — De Casteljau twice: split at `e`, take the left piece, then split that piece at `s/e` and keep its right piece. Exact trim; P0..P3 instances are preserved.
- [x] **VPolyline** — Rebuild `Points`: trimmed start, original interior vertices strictly within [s, e], trimmed end. Recompute `_selfIntersecting`.
- [x] **VSpline** — Dense resample at the original render resolution (`numSpans * SegmentsPerSpan` scaled by `(e - s)`) so the trimmed Catmull-Rom passes through enough interpolating points to track the original path. Catmull-Rom tangents depend on neighboring control points, so simply retaining inner CPs visibly bent away from the original.
- [x] **VCircle / VPolygon / VRay / VXLine** — Throw `NotSupportedException` with a message pointing to `SplitAtPoint`. Their trimmed form would change shape type (circle→arc, polygon→polyline, ray/xline→line).
- [x] `_selfIntersecting` made non-readonly on `VPolyline`, `VBezier`, `VSpline` so it can be recomputed after the trim.
- [x] xUnit coverage: 17 cases in `Tests/SetBoundsTests.cs` — VLine subrange + identity + instance preservation + swap + clamp; VArc/VEllipse rescale; VPolyline drop-out-of-range and within-single-segment; VBezier fidelity (trimmed midpoint matches original at remapped parameter) + instance preservation; VSpline endpoint exactness + interior tracking via dense resample; throw-paths for VCircle/VPolygon/VRay/VXLine. All 117 tests in the suite pass.

---

### Phase 22: Animator Sub-Project (p5.js-style Sketches)
- [x] Code2Viz Sketch mode (in-process, multi-file aware): Sketch base / SketchRuntime / C2VGeometryAdapter / C2VGeometryRegistry in `Sketch/`; ModuleCompiler detects a `Code2Viz.Sketching.Sketch` subclass and routes to the per-frame runtime; `MainWindow` integrates the sketch into the existing `CompositionTarget.Rendering` loop; new file dialog offers Module vs Sketch; XAML tab badge for Sketch files.
- [x] Extract Animator into a separate `Animator.exe` (folder `Animator/`, AssemblyName `Animator`, RootNamespace `Animator`) that depends only on `C2VGeometry.csproj` — no `Code2Viz.dll` reference.
- [x] `Animator/Canvas/AnimCanvas.cs` — direct C2VGeometry renderer (DrawingVisual + DrawingContext); switch over VCircle, VLine, VRectangle, VEllipse, VArc, VPolygon, VPolyline, VPoint; pan-only (zoom disabled by design); no grid (grid drawing intentionally removed for sketches).
- [x] `Animator/Sketch/{Sketch, SketchRuntime, ShapeRegistry}.cs` — sketch base with `Setup`/`Draw`/`Size`/`Background`/`Loop`/`NoLoop`/`FrameCount`/`ElapsedSeconds`/`DeltaSeconds`/Mouse/Key inputs; `FrameProduced` event fires each tick with the frame's `IReadOnlyList<C2VGeometry.Shape>`.
- [x] `Animator/Compiler/SketchCompiler.cs` — Roslyn single-file compile; only Sketch mode (no Main() fallback); collectible `AssemblyLoadContext` retained until Stop.
- [x] `Animator/Editor/{CachedCompilationWorkspace, FuzzyMatcher, CompletionEngine, AvalonCompletionData}.cs` — IntelliSense (Ctrl+Space + auto-popup on `.` / identifier chars), powered by `SemanticModel.LookupSymbols` with member-access detection via `MemberAccessExpressionSyntax` / `QualifiedNameSyntax`.
- [x] Code2Viz dark theme replicated in `Animator/App.xaml`; embedded `CSharpHighlighting.xshd` loaded via `HighlightingLoader.Load`; toolbar (not native Menu) with file/run/stop/switch buttons.
- [x] Cross-app switch — `Switch to Animator` (Code2Viz) and `Switch to Project` (Animator); each launches the other via `AppSwitcher.FindSiblingApp` and closes itself. Prompts to save unsaved edits before switching/closing.
- [x] Console shows errors in red, warnings in yellow via a typed `ConsoleLine(Level, Source, Message)` record and a `DataTemplate` with `DataTrigger`s.
- [x] Save-changes prompt — dirty tracking via `Editor.TextChanged` (gated by `_suppressDirty` during programmatic loads); Yes/No/Cancel dialog on New, Open, Switch, and window close. Save() / SaveAs() return bool so cancel-aborts the surrounding flow.
- [x] `Ctrl+Enter` toggles Run/Stop; `F5` = Run, `Shift+F5` = Stop, `Ctrl+Space` = manual IntelliSense, standard `Ctrl+S/O/N` for file ops.
- [x] Default template orbits a cyan circle inside a centered 800×600 boundary (bounded motion so the demo never drifts off-screen).
- [x] `Code2Viz.csproj` excludes `Animator\**` (Compile + Page + ApplicationDefinition + Resource + None) to keep its WPF compile from picking up Animator's XAML.

---

### Phase 23: Shared Editor + Visible Squiggle + F# Removal (2026-05-21)
- [x] **Editor source single-sourced in `Editor/` and linked into Animator.** `Animator.csproj` references `..\Editor\*.cs` as `<Compile Link=…>` items plus a linked `<Page>` for `MinimapControl.xaml`. Bug fixes flow to both apps from one place. ANIMATOR-conditional shims in `RefactoringProvider` / `TypeInspector` / `CodeLensProvider` so the linked files compile in Animator without the multi-file project model.
- [x] **`Editor/SharedEditorController`** — extracted host glue (~2.5k lines). Wires TextMarkerService, syntax-check timer (800 ms), bracket/selection renderers, semantic highlighter, inlay hints, code lens, snippets, folding, hover tooltips, multi-cursor, F12 / Shift+F12 / F2 / Ctrl+. key bindings, context menu — all in one `Initialize()` call. Hosts plug their environment in through callbacks: `GetActiveFilePath`, `GetWorkspace`, `GetActiveProject`, `ApplyRefactoring`, `AutoRunCodeAsync`, `IsAutoUpdateEnabled`. Animator now uses this; Code2Viz's `MainWindow.xaml.cs` still has its parallel inlined implementation (~1.5k lines) as a follow-up migration.
- [x] **Visible realtime squiggles** — `Editor/TextMarkerService.Draw` was drawing a 1px-amplitude zigzag at `r.Bottom + 1` (i.e., in the inter-line gap, where it's effectively invisible). Now amplitude 2, period 4, pen 1.2, positioned at `r.Bottom - amplitude` so the squiggle tucks under the text baseline.
- [x] **F# support completely removed** — see TODO.md "Recently Completed" for the file-by-file breakdown. Deleted: `FSharp/VizDsl.fs`, `Editor/FSharpHighlighting.xshd`, `Execution/FSharpModuleCompiler.cs`, `Project/FSharpTemplates.cs`. Dropped from `Code2Viz.csproj`: `FSharp.Compiler.Service`, `FSharp.Core`, the two F# embedded resources. Removed: `ProjectLanguage` enum + `VizProjectFile.Language`, `FSharpDiagnosticInfo` + `CompilationResult.FSharpDiagnostics`, every `isFSharp` / `== ProjectLanguage.FSharp` branch in `MainWindow.xaml.cs`, `Canvas/CodeGenerator.cs` (rewritten C#-only), `Canvas/CodeSyncManager.cs`, `Editor/SharedEditorController.cs` (language-detection branch), `Editor/RoslynCompletionService.cs` (FSharp.* namespace filter), `Documentation/DocGenerator.cs` (`_fsharpSamples` + `InitializeFSharpSamples` + `GenerateFSharpDocForType` + `GenerateFSharpMemberTable`), `HelpWindow.xaml(.cs)` (F# Reference tab), `WelcomeWindow.xaml.cs`, `NewProjectDialog.xaml(.cs)` (language ComboBox), `installer.iss` (F# DLL `Source:` lines, sample-project entry), `McpServer/Tools/VizCodeTools.cs` (`.cs/.fs` doc strings). Net diff over the whole F# removal: 25 files, +186 / −1942. All 138 unit tests pass.
- [x] **`VCircle(VXYZ, double)` overload** added in `Geometry/Circle2D.cs`. Internally constructs `VPoint.Internal(center.X, center.Y)` to avoid auto-registering a marker — matches C2VGeometry semantics where coordinates are `VXYZ` and `VPoint` is reserved for visible markers. Existing `(VPoint, double)` ctor remains.
- [x] **F1 Help-window crash fix** — `DocGenerator.InitializeSummaries` threw `ArgumentException: An item with the same key has already been added. Key: Animator` from its constructor (the dict is keyed by `type.Name`, and a namespace-level `"Animator"` entry collided with the `Animation.Animator` class entry). Removed the unreachable namespace-keyed entry plus its `"Animator.Sketching.Sketch"` sibling.
- [x] **`/update_docs` sweep** target list refreshed — see `CLAUDE.md` item #14 for the shared-editor architecture note + squiggle behavior, and TODO.md "Recently Completed" for the F# removal manifest.

---

### Phase 24: Manual Release Flow (2026-05-21)
- [x] **`Directory.Build.props`** at repo root — canonical `<Version>` / `<AssemblyVersion>` / `<FileVersion>` source. Every C# project in the repo (Code2Viz, Animator, Tests, McpBridge, McpServer, C2VGeometry) inherits it automatically.
- [x] **`scripts/release.ps1`** — single entry point for bumping and shipping. Guards a clean working tree on `main` in sync with `origin`, bumps `Directory.Build.props` and mirrors the same version into `installer.iss` (Inno Setup can't read MSBuild props, so the script is the only place that touches both), commits as `Release v<new>`, builds Release config of `Code2Viz.csproj` + `Animator/Animator.csproj`, invokes `ISCC.exe` at `C:\Program Files (x86)\Inno Setup 6\` to produce `installer/output/Code2Viz-<new>-Setup.exe`, tags `v<new>`, pushes `main` + tag, then calls `gh release create` with the installer attached and notes auto-generated from `git log v<prev>..HEAD`. Falls back to printing the manual upload URL when `gh` isn't installed.
- [x] **`CLAUDE.md` `/release` Command** — documents the procedure (run `/update-docs` first as a separate commit so the release ships with current documentation; never bump versions by hand). Mirrored in a `release_command.md` claude memory entry so future sessions follow the same flow.
- [x] **First release: v1.0.0** — tagged the current state without a bump (the script's bump-then-tag flow is for subsequent releases).

---

### Phase 25: Geometry Unification — Single `C2VGeometry` Namespace (2026-05-27)
- [x] **Port `RayCaster` into `C2VGeometry`** — the spatial accelerator (flat-array BVH, SAH split, inline ray-vs-shape math, `Refit`) now lives in the unified namespace and snapshots shapes from the canonical registry. `VPoint` markers and infinite-bounds shapes (VRay/VXLine) stay excluded.
- [x] **Reconcile `ShapeDefaults` into `C2VGeometry` construction** — global style + dimension defaults are applied at shape-construction time in the unified namespace (no parallel copy in the old namespace).
- [x] **Make `CanvasRenderer` the canonical `IShapeRegistry`** — shapes auto-register against `CanvasRenderer.Instance` through the registry interface; the Animator path keeps using `DefaultRegistry`. One registry abstraction, two hosts.
- [x] **Repoint the whole app from `Code2Viz.Geometry` to `C2VGeometry`** — every `using Code2Viz.Geometry;` across Canvas, Editor, Execution, Export, Mcp, Project, Commands, samples, and templates now uses `C2VGeometry`. User scripts import `using C2VGeometry;`.
- [x] **`VPoint` is now only a drawable marker; `VXYZ` is the coordinate type** — coordinates/positions/vectors (circle centers, line endpoints, polygon vertices, `BoundingBox.Min/Max`, `ICurve.Divide` results, etc.) are `VXYZ` value types. `VPoint` is reserved for visible point markers on the canvas.
- [x] **Delete `Code2Viz.Geometry` + adapter/parity scaffolding** — the old namespace, the C2VGeometry↔Code2Viz.Geometry adapter, and the parity-test scaffolding are removed. There is now a single geometry namespace shared by Code2Viz and Animator.
- [x] **Docs swept to the unified namespace** — README, `Documentation/DocGenerator.cs` (namespace list + summaries + sample-code strings), `McpServer/SKILL.md`, `McpServer/Resources/ApiReferenceResource.cs`, and `McpServer/Tools/VizCodeTools.cs` (the functional "Available imports" string) updated to `C2VGeometry` / `VXYZ`.

---

### Phase 26: Editor & Canvas Fixes (2026-05-27)
- [x] **CodeLens blink-on-broken-syntax fix** — a nearby structural syntax error made Roslyn error-recovery intermittently fail to parse the following declaration as a method, so alternating recomputes added/dropped its (2×-tall) CodeLens row, blinking it in/out and bouncing the code below. `UpdateCodeLens` now swaps `_items` outright only on a clean parse; on a broken parse it merges via `MergePreservingExisting` (keeps all prior items, only adds new `(Kind, SymbolName)`, never removes) and a failed build leaves `_items` untouched instead of blanking the gutter. Shared `Editor/` source — flows to both apps.
- [x] **Canvas-focus fix for P/L/C/R drawing-tool shortcuts** — `RenderCanvas.OnMouseDown` never took keyboard focus on click, so focus stayed in the code editor and pressing P/L/C/R (and Delete/A/Esc) typed the letter into the editor instead of activating the drawing tool. Any canvas click now grabs focus if it doesn't already have it. Pre-existing bug, independent of the geometry-unification work.

---

### Phase 27: Animator Bottom-Left Origin + Calendar Versioning (2026-05-27)
- [x] **Animator sketch frame origin moved to bottom-left** — `AnimCanvas.SetBoundary` (plus the resize handler and boundary-outline draw) anchor the `Size()` frame at the origin so `(0,0)` is the frame's bottom-left corner instead of its centre. Valid sketch coordinates run `x ∈ [0,Width]`, `y ∈ [0,Height]`, Y-up. The world↔screen transform and `ZoomToBounds` were already general; only the frame bounds changed. Code2Viz's own canvas is unaffected (still centre-origin).
- [x] **Origin axis lines no longer drawn** — `AnimCanvas.Refresh` stopped calling `DrawAxes`, so the X/Y lines through the origin are gone (matching the already-disabled grid). The faint frame outline still renders.
- [x] **Default sketch boilerplate updated** — `Animator/Templates.cs` `DefaultSketch` declares `width`/`height` fields and offsets the orbiting circle to the frame centre (`+ width/2`, `+ height/2`) so it stays visible under the bottom-left origin.
- [x] **Calendar versioning (`YEAR.MONTH.PATCH`)** — `scripts/release.ps1` drops `-Bump`; it stamps year/month from the release date and increments patch within a month (resets to 0 on a new month/year). `Directory.Build.props` + `installer.iss` moved `2.0.0` → `2026.5.0`, and `CLAUDE.md`'s `/release` section was updated to match.

---

## Implementation Statistics

| Category | Count |
|----------|-------|
| Shape classes | 15 |
| C# files created | 50+ |
| XAML files modified | 10+ |
| NuGet packages | 3 |
| Keyboard shortcuts | 30+ |
| Canvas features | 12+ |

---

## Time Allocation (Estimated)

| Phase | Effort |
|-------|--------|
| Project Setup | 5% |
| Core Geometry | 15% |
| Extended Geometry | 10% |
| Canvas Implementation | 25% |
| Shape Rendering | 15% |
| Code Editor | 10% |
| Script Execution | 5% |
| Main Window UI | 10% |
| File Operations | 3% |
| Bug Fixes | 2% |
