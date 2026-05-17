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
