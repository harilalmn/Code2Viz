# TODO - Viz2d Future Development

## High Priority (P0) - Interactive Editing

### Shape Selection System
- [x] **Click to select** - Single shape selection on canvas click
- [x] **Multi-select with Shift** - Add to selection with Shift+Click
- [x] **Multi-select with Ctrl** - Toggle selection with Ctrl+Click
- [x] **Selection box** - Drag rectangle to select multiple shapes
- [x] **Crossing/Window selection** - Drag right = Window (fully inside), Drag left = Crossing (intersecting)
- [x] **Select All** - Ctrl+A to select all shapes
- [x] **Deselect** - Escape or click on empty canvas
- [x] **Visual feedback** - Highlight selected shapes with handles

### Shape Editing
- [x] **Control point handles** - Shape-specific control points for all 13 shape types
- [x] **Drag to modify** - Move control points to edit shape geometry (vertex, radius, curve handles)
- [x] **Move selected shapes** - Drag move handle to reposition
- [x] **Resize handles** - Corner/edge/radius handles for resizing
- [ ] **Rotation handle** - Rotate selected shapes
- [x] **Sync to code** - Update source code when shapes are edited

### Properties Panel
- [x] **Panel UI** - Floating/dockable panel showing shape properties
- [x] **Coordinate editing** - Edit X, Y, Width, Height, Radius, etc.
- [x] **Color picker** - Visual color selection for Stroke/Fill via ColorPickerDialog
- [x] **Thickness slider** - Adjust stroke thickness (0.5-20)
- [x] **Opacity slider** - Adjust shape opacity (0-100%)
- [x] **Visibility toggle** - Show/hide shapes with code sync (`IsVisible = false`)
- [x] **Name/ID display** - Show shape identifier and editable name with variable rename in code
- [x] **Style code sync** - All style property changes (Color, Fill, Weight, Opacity, Visible) persist as code lines
- [x] **Multi-selection** - Edit common style properties of multiple shapes
- [x] **Dock/Float toggle** - Switch between docked column and floating window
- [x] **Auto-deselect** - Selection cleared on Run and when clicking code editor

### Delete Shape
- [x] **Delete key** - Remove selected shapes
- [x] **Right-click context menu** - Delete option
- [x] **Code sync** - Remove corresponding code when shape deleted
- [ ] **Undo support** - Restore deleted shapes

---

## High Priority (P0) - Animation UI Enhancements

Core timeline playback is implemented; items below are advanced timeline UX polish.

### Timeline Panel
- [ ] **Timeline UI** - Visual timeline at bottom of window
- [ ] **Time ruler** - Displays time in seconds
- [ ] **Playhead** - Draggable position indicator
- [ ] **Shape tracks** - Row per animated shape
- [ ] **Keyframe markers** - Visual keyframe indicators
- [ ] **Duration handles** - Resize animation duration
- [ ] **Zoom timeline** - Zoom in/out on timeline

### Animation Preview
- [ ] **Play button** - Start animation playback
- [ ] **Pause button** - Pause at current frame
- [ ] **Stop button** - Reset to beginning
- [ ] **Loop toggle** - Enable/disable repeat
- [ ] **Speed control** - Playback speed slider (0.25x - 4x)
- [ ] **Frame stepping** - Step forward/backward one frame
- [ ] **Current time display** - Show current time position

---

## High Priority (P0) - Export Enhancements

### DXF Export
- [x] **DXF file format** - AutoCAD DXF R12/R14 format
- [ ] **Layer mapping** - Map shape types to DXF layers
- [ ] **Color mapping** - Map colors to DXF color indices
- [ ] **Line type support** - Solid, dashed, dotted
- [ ] **All shape types** - Export all supported shapes
- [ ] **Scale/units** - Configurable export units

### PDF Export
- [x] **Vector PDF** - PDF/A format for archiving
- [x] **Page size options** - A4, Letter, Custom
- [x] **Margins** - Configurable page margins
- [x] **Fit to page** - Auto-scale to fit
- [ ] **Multi-page** - Split large drawings across pages
- [ ] **Metadata** - Title, author, date

---

## Medium Priority (P1) - Geometry Operations

### Boolean Operations
- [x] **Union** - Combine two or more polygons
- [x] **Intersection** - Get overlapping area of polygons
- [x] **Difference** - Subtract one polygon from another
- [x] **XOR** - Symmetric difference
- [x] **Clipper library** - Use Clipper2 for robust operations
- [x] **API exposure** - VPolygon.Union(other), etc.

### Array/Pattern Operations
- [ ] **Linear array** - Repeat shape along vector
  ```csharp
  shape.LinearArray(direction, count, spacing);
  ```
- [ ] **Rectangular array** - Grid of copies
  ```csharp
  shape.RectangularArray(rows, cols, rowSpacing, colSpacing);
  ```
- [ ] **Circular array** - Copies around center point
  ```csharp
  shape.CircularArray(center, count, angleSpan);
  ```
- [ ] **Path array** - Distribute along curve
  ```csharp
  shape.PathArray(curve, count, alignToPath);
  ```

---

## Medium Priority (P1) - Bug Fixes & Performance

### Bug Fixes
- [x] Fix console resize and scroll with multiline content (Auto row span layout issue)
- [ ] Test arc rendering for edge cases (360 arc, negative angles)
- [ ] Verify polygon rendering with self-intersecting polygons
- [ ] Test zoom limits at extreme scales

### Performance
- [ ] Optimize redraw for large shape counts (> 1000)
- [ ] Cache brushes instead of creating new ones per shape
- [ ] Implement shape culling for off-screen shapes
- [x] Spatial acceleration for ray queries (`RayCaster`: flat BVH + SAH split, allocation-free queries, parallel batch, in-place refit)

---

## Low Priority (P2) - Styling Enhancements

### Shape Styling
- [x] **Dash patterns** - Dashed/dotted lines via LineType property
  ```csharp
  line.LineType = LineType.Dashed; // Dashed, Dotted, DashDot, DashDotDot, Center, Phantom, Hidden
  ```
- [ ] **Line caps** - Round, Square, Flat
- [ ] **Line joins** - Miter, Bevel, Round
- [ ] **Gradient fills** - Linear and radial gradients
- [ ] **Pattern fills** - Hatch patterns (diagonal, cross, dots)

### Canvas Features
- [x] **Snap to grid** - Snap coordinates to grid intersections (F9 toggle, adaptive spacing)
- [ ] **Ruler display** - Show rulers along canvas edges
- [ ] **Zoom slider** - Visual zoom control in UI
- [x] **Mini-map** - Overview of entire canvas (Ctrl+Shift+M toggle, syntax coloring, viewport indicator)

---

## Low Priority (P2) - Additional Features

### Export Features
- [ ] **Copy to clipboard** - As image or SVG

### Layer System
- [ ] **Named layers** - Create/rename layers
- [ ] **Visibility toggle** - Show/hide layers
- [ ] **Lock layers** - Prevent editing
- [ ] **Z-order** - Bring to front, send to back

### UI Enhancements
- [x] **Drag-and-drop in Project Explorer** - Move files and folders between directories via drag-and-drop
- [x] **Go to Location** - Context menu option to open file/folder location in Windows File Explorer
- [ ] **Customizable theme** - Light/Dark mode toggle
- [ ] **Full screen mode** - Maximize canvas
- [ ] **Undo/Redo for drawing** - Undo interactive drawing operations

---

## Technical Debt

### Code Quality
- [ ] Add XML documentation comments to all public APIs
- [x] Add unit tests for geometry calculations
- [ ] Add integration tests for script execution
- [ ] Implement proper MVVM pattern

### Architecture
- [x] Separate geometry library for reuse (C2VGeometry)
- [x] Unify on a single geometry namespace — `Code2Viz.Geometry` retired; `C2VGeometry` is the one geometry namespace shared by both Code2Viz and Animator; coordinates are `VXYZ`, `VPoint` is only a drawable marker
- [ ] Migrate Code2Viz's inlined editor wiring in MainWindow.xaml.cs to SharedEditorController (Animator already uses it)
- [ ] Finish Animator process isolation (`SKETCH_ISOLATION_PLAN.md` steps 4–6): throughput pass, bundle `SketchHost.exe` into the installer + release workflow, then make `ANIMATOR_ISOLATE` the default and delete the in-process path
- [ ] Add dependency injection for testability
- [ ] Implement plugin system for custom shapes

---

## Completed Features

### Shapes (15 total)
- [x] VPoint, VLine, VCircle, VRectangle, VEllipse, VArc
- [x] VPolygon, VPolyline, VBezier, VSpline
- [x] VArrow, VText, VDimension, VGroup
- [x] Region (curve-bounded areas with holes, boolean ops)

### Drawing Tools (12 total)
- [x] All shape types with click-based creation
- [x] Code generation for drawn shapes

### Snap System (9 types)
- [x] Endpoint, Midpoint, Center, Intersection, Perpendicular, Nearest, Extension, Tangent, Grid

### Animation System
- [x] Draw, Move, Rotate, Flip, FadeIn, FadeOut animations
- [x] Timeline class with easing functions
- [x] ObjectPropertyAnimation<T> for animating numeric properties on any object
- [x] Animator.Fps property (1-120, default 60) for frame rate control
- [x] CompositionTarget.Rendering-based animation loop (vsync-aligned)

### Boolean Operations
- [x] Union, Intersection, Difference, XOR (Clipper2)
- [x] VPolygon.Union/Intersect/Difference/Xor methods
- [x] Region boolean ops (RegionBooleanOps)

### Export
- [x] PNG export
- [x] SVG export
- [x] GIF animation export
- [x] MP4 video export
- [x] DXF export (AutoCAD R12 ASCII)
- [x] PDF export (vector graphics)

### Editor
- [x] Syntax highlighting (C#)
- [x] Code completion and IntelliSense
- [x] Code folding and bracket matching
- [x] Code snippets
- [x] Shared editor host glue (`Editor/SharedEditorController`) — Animator wires up TextMarkerService, syntax-check timer, bracket/selection renderers, semantic highlighter, inlay hints, code lens, snippets, folding, hover tooltips, multi-cursor, F12/F2/Ctrl+. key bindings in one Initialize() call. Code2Viz still has its parallel inlined implementation in MainWindow.xaml.cs.
- [x] Visible realtime syntax-error squiggles — TextMarkerService draws a 2px-amplitude red zigzag tucked under the text baseline (was 1px in the inter-line gap and effectively invisible).

### Canvas
- [x] Zoom and pan
- [x] Grid and axes
- [x] Coordinate display
- [x] Measuring tool
- [x] Snap to grid (F9 toggle)
- [x] Crossing/Window selection (drag direction determines mode)
- [x] Shape ID counter reset on each execution
- [x] Minimap with syntax coloring and viewport indicator

### Shape Editing
- [x] Shape-specific control points (13 shape types)
- [x] Drag control points to edit geometry
- [x] Code sync on drag end
- [x] Properties panel (floating/dockable)
- [x] Style property code sync (Color, FillColor, LineWeight, Opacity, IsVisible)
- [x] Variable rename from Properties panel
- [x] Auto-deselect on Run and editor click

### Curve Operations
- [x] `ICurve.SetBounds(start, end)` — in-place parameter-range trim for VLine/VArc/VEllipse/VPolyline/VBezier/VSpline (VBezier uses De Casteljau, VSpline dense-resamples); throws on VCircle/VPolygon/VRay/VXLine. Mirrored in C2VGeometry. 17 xUnit tests.

### Animator Sub-Project
- [x] **In-process Sketch mode** in Code2Viz — `Code2Viz.Sketching.Sketch` base, `SketchRuntime`, adapter for C2VGeometry → Code2Viz.Geometry shapes (`Sketch/`), entry probe in `ModuleCompiler`, frame-loop integration in `MainWindow`.
- [x] **Standalone Animator app** (`Animator.exe`) — separate project under `Animator/`, depends only on `C2VGeometry.csproj`. Direct C2VGeometry renderer (`AnimCanvas`), single-file Roslyn compiler, AvalonEdit + Code2Viz dark theme, IntelliSense (Ctrl+Space, auto-popup), colored console, save-on-close prompts, Ctrl+Enter toggle, cross-app Switch buttons.

### Recently Completed (2026-05-27)
- [x] **Sketch crash-resilience — stack-overflow guard** — `Execution/StackGuardRewriter.cs` injects `RuntimeHelpers.EnsureSufficientExecutionStack()` into every user method so runaway recursion (e.g. mutual `Grow`/`Shrink`) throws a catchable `InsufficientExecutionStackException` instead of the uncatchable `StackOverflowException` that fail-fasts the whole process. Wired into both compilers (Code2Viz `ModuleCompiler` execute path + Animator `SketchCompiler`); the sketch stops with a "runaway recursion" console message and the app stays alive. Guarded by `Tests/StackGuardRewriterTests.cs`.
- [x] **Process-isolation foundation (Phase 2 POC)** — `SketchHost.exe` runs user sketches out-of-process so an infinite loop / OOM / native crash can't freeze or crash Animator. Protocol in `Animator/Ipc/` (binary stdio framing, shape codec, parent client + frame-starvation watchdog); UI wired behind the `ANIMATOR_ISOLATE=1` flag (default off). Proven live: the watchdog kills an infinite-loop child while Animator survives. Full design + remaining steps in `SKETCH_ISOLATION_PLAN.md`.
- [x] **Animator completion unified on Code2Viz's Roslyn engine** — removed the `#if ANIMATOR → CompletionEngine` fork; Animator now has fuzzy matching + highlighting, scope/expected-type sorting, and the documentation sidecar, same as Code2Viz.
- [x] **Animator branding ribbon** — logo + "Animator" + tagline + version strip above the menu bar, matching Code2Viz's header.
- [x] **Animator sketch origin at frame bottom-left** — `AnimCanvas` anchors the `Size()` frame at `(0,0)` (bottom-left) instead of the centre, so sketch coordinates run `x ∈ [0,Width]`, `y ∈ [0,Height]`, Y-up. The origin X/Y axis lines are no longer drawn, and the default boilerplate (`Templates.DefaultSketch`) gained `width`/`height` fields and centres its orbiting circle. Code2Viz's own canvas stays centre-origin.
- [x] **Calendar versioning (`YEAR.MONTH.PATCH`)** — `scripts/release.ps1` no longer takes `-Bump`; it stamps year/month from the release date and bumps patch within a month (resets on a new month/year). Version sources (`Directory.Build.props`, `installer.iss`) moved `2.0.0` → `2026.5.0`; the `/release` section in `CLAUDE.md` was updated to match.
- [x] **Geometry unification — single `C2VGeometry` namespace** — `Code2Viz.Geometry` is retired and deleted along with the C2VGeometry↔Code2Viz.Geometry adapter and parity-test scaffolding. `RayCaster` and `ShapeDefaults` were ported into `C2VGeometry`, and `CanvasRenderer` is now the canonical `IShapeRegistry` shapes auto-register against (Animator keeps `DefaultRegistry`). The whole app and all user scripts now `using C2VGeometry;`. Coordinates/positions/vectors are the `VXYZ` value type; `VPoint` is now only a drawable point marker. Docs swept across README, DocGenerator, SKILL.md, ApiReferenceResource.cs, and VizCodeTools.cs.
- [x] **CodeLens blink-on-broken-syntax fix** — `Editor/CodeLensProvider.cs`: a nearby structural syntax error made Roslyn error-recovery intermittently fail to re-parse the following declaration as a method, so alternating recomputes added/dropped its 2×-tall CodeLens row and blinked it in/out. `UpdateCodeLens` now swaps `_items` only on a clean parse; on a broken parse it merges (keeps prior items, only adds new, never removes) and a failed build leaves `_items` untouched. Shared `Editor/` source — flows to both apps.
- [x] **Canvas-focus fix for drawing-tool shortcuts** — `RenderCanvas.OnMouseDown` now grabs keyboard focus on click, so single-key drawing-tool shortcuts (P/L/C/R, plus Delete/A/Esc) fire on the canvas instead of typing into the code editor. Click the canvas first to focus it, then press the key.

### Recently Completed (2026-05-26)
- [x] **CodeLens vertical-jitter fix** — `Editor/CodeLensProvider.cs` stored each CodeLens row as a frozen absolute offset and only recomputed on the 500 ms semantic-update debounce, but AvalonEdit redraws touched lines on every keystroke. Typing above a code-lensed declaration shifted the real offsets while the cached ones went stale, so the 2×-tall CodeLens row rendered on the wrong line and snapped back after the debounce. Each item now holds a live `TextAnchor` (`AfterInsertion`, `SurviveDeletion`) and the element generator reads `CurrentOffset`, so the row tracks edits instead of snapping; only the debounced count text lags, which causes no movement. Flows to both apps via the shared `Editor/` source.

### Recently Completed (2026-05-25)
- [x] **Welcome dialog: Code / Animate mode toggle** — radio buttons in `WelcomeWindow.xaml` switch the action buttons' labels (driven via `Tag` + `TemplateBinding`) and the recent-list source between `RecentProjectsManager` and the new `RecentAnimationsManager`. Animate actions launch `Animator.exe` and pass the selected `.cs` file as a command-line argument. Window widened to 900px with 1.2:1 column ratio so longer animation labels fit on one line.
- [x] **`Project/RecentAnimationsManager`** — `%AppData%\Code2Viz\recent_animations.json` store mirroring `RecentProjectsManager`. Linked into Animator so its Open / Save / SaveAs / `MainWindow(string? initialFile)` constructor record entries automatically — the welcome list now stays in sync with whatever the user opens from Animator's own File menu.
- [x] **Animator startup accepts a `.cs` file via CLI** — `App.xaml` drops `StartupUri`; `App.OnStartup` walks `e.Args`, picks the first existing-file argument, and passes it to a new `MainWindow(string? initialFile)` overload that loads the file into the editor and seeds `_currentPath`.
- [x] **Animator stops the running sketch on keypress in the editor** — `Editor.PreviewKeyDown` → `Editor_PreviewKeyDown_StopSketch` calls `StopSketch()` for any non-modifier keypress; pure modifier keys and Ctrl/Alt/Win shortcuts (Ctrl+S, Shift+F5, etc.) are skipped.

### Recently Completed (2026-05-24)
- [x] **`VText.Rotate(pivot, angle)` fix** — used to only rotate `Location` around the pivot; the text's own `Angle` field was never updated, so a rotation with `pivot == Location` produced no visible change. Now matches the convention of `Rectangle2D` / `Arc2D` / `Hatch2D` / `RadialDimension2D`: both `Location` (around pivot) and `Angle` (`+= angleDegrees`) are updated. Mirrored across `Code2Viz.Geometry` and `C2VGeometry`.
- [x] **`VText.DoesIntersect`** — text-aware intersection: builds the (possibly rotated, anchor-aware) bounding quad and tests it against the other shape's bounding box via SAT. Mirrored in both `Code2Viz.Geometry` and `C2VGeometry`. `Shape.DoesIntersect` falls back to `other.DoesIntersect(this)` when `other is VText` so the check is symmetric.
- [x] **Windows menu polish** — Run no longer force-shows the console when the user has hidden it; `Windows > Console` toggle now persists; Console menu checkmark no longer shows stale-checked at launch; collapsing both Console and Canvas now reclaims their shared column so the editor fills the row; new `Windows > Ribbon` toggle hides/shows the top logo/version panel (persisted).

### Recently Completed (2026-05-21)
- [x] **F# support removed** — `FSharp.Compiler.Service` / `FSharp.Core` package refs gone; `FSharpModuleCompiler`, `FSharpTemplates`, `FSharpHighlighting.xshd`, `FSharp/VizDsl.fs` deleted; `ProjectLanguage` enum + `VizProjectFile.Language` removed; all `isFSharp` / `== ProjectLanguage.FSharp` branches stripped from `MainWindow`, `Canvas/CodeGenerator`, `Canvas/CodeSyncManager`, `Execution/ModuleCompiler`, `Editor/SharedEditorController`, `Project/*`; F# tab removed from `HelpWindow`; Welcome / New-Project language ComboBox gone. Net: 25 files, +186 / −1942.
- [x] **`SharedEditorController` host glue** — single `Initialize()` call wires every editor feature for Animator; Code2Viz's `MainWindow` still has its parallel inlined version.
- [x] **Squiggle visibility fix** — `Editor/TextMarkerService.Draw` now uses amplitude 2 / pen 1.2 / position `r.Bottom - amplitude` instead of amplitude 1 in the inter-line gap.
- [x] **`VCircle(VXYZ, double)` overload** added to `Code2Viz.Geometry.VCircle` so the help-sample-style `new VCircle(new VXYZ(50, 50), 30)` now compiles in Code2Viz projects. Uses `VPoint.Internal(center.X, center.Y)` to avoid auto-registering a stray marker.
- [x] **F1 Help crash fix** — `DocGenerator.InitializeSummaries` had two `"Animator"` keys (one for the sub-project namespace, one for the `Animation.Animator` class) which threw `ArgumentException` from the constructor; removed the unreachable namespace-keyed entry plus its `"Animator.Sketching.Sketch"` sibling (dict is keyed by `type.Name`).

---

## Notes

- Coordinate system: Mathematical (Y-up) - DO NOT CHANGE
- Grid spacing: Currently fixed at 50 units - make configurable
- Color parsing: Uses WPF ColorConverter - supports all named colors
- Script execution: Uses Roslyn - any C# syntax works
