# Product Requirements Document (PRD)
## Viz2d - 2D Geometry Visualizer

### Document Information
- **Version**: 1.0
- **Last Updated**: February 2026
- **Status**: Implemented

---

## 1. Product Overview

### 1.1 Purpose
Viz2d is a desktop application that enables users to visualize 2D geometric shapes by writing and executing C# code. It serves as an educational tool for learning geometry concepts and a prototyping tool for geometric algorithms.

### 1.2 Target Users
- Students learning computational geometry
- Developers prototyping geometric algorithms
- Educators teaching geometry concepts
- Anyone needing quick 2D shape visualization

### 1.3 Key Value Propositions
- **Code-driven visualization**: Write C# code to create shapes programmatically
- **Interactive canvas**: Zoom, pan, and explore geometric constructions
- **Immediate feedback**: Execute code and see results instantly
- **Familiar syntax**: Standard C# with intuitive geometry classes

---

## 2. Functional Requirements

### 2.1 Shape Support

#### 2.1.1 Basic Shapes (P0 - Must Have)
| ID | Shape | Status | Description |
|----|-------|--------|-------------|
| FR-001 | Point | Done | Single point marker with coordinates |
| FR-002 | Line | Done | Line segment between two points |
| FR-003 | Circle | Done | Circle with center and radius |
| FR-004 | Arc | Done | Circular arc with start/end angles |

#### 2.1.2 Extended Shapes (P1 - Should Have)
| ID | Shape | Status | Description |
|----|-------|--------|-------------|
| FR-005 | Rectangle | Done | Axis-aligned rectangle |
| FR-006 | Ellipse | Done | Ellipse with two radii |
| FR-007 | Polygon | Done | Closed polygon with N vertices |
| FR-008 | Polyline | Done | Open polyline with N vertices |

#### 2.1.3 Future Shapes (P2 - Nice to Have)
| ID | Shape | Status | Description |
|----|-------|--------|-------------|
| FR-009 | Bezier Curve | Done | Cubic Bezier curve |
| FR-010 | Spline | Done | B-spline or Catmull-Rom |
| FR-011 | Text | Done | Text labels on canvas |

### 2.2 Shape Styling

| ID | Feature | Status | Description |
|----|---------|--------|-------------|
| FR-020 | Stroke Color | Done | Customizable border color |
| FR-021 | Fill Color | Done | Customizable fill color |
| FR-022 | Stroke Thickness | Done | Customizable line width |
| FR-023 | Dash Pattern | Done | LineType property (Dashed, Dotted, DashDot, etc.) |
| FR-024 | Opacity | Done | Transparency support |

### 2.3 Canvas Features

| ID | Feature | Status | Description |
|----|---------|--------|-------------|
| FR-030 | Mouse Wheel Zoom | Done | Zoom centered on cursor |
| FR-031 | Middle-Click Pan | Done | Drag to pan view |
| FR-032 | Zoom Extents | Done | Auto-fit all shapes |
| FR-033 | Grid Lines | Done | Toggleable grid display |
| FR-034 | Coordinate Axes | Done | X/Y axes at origin |
| FR-035 | Coordinate Display | Done | Real-time mouse coords |
| FR-036 | Snap to Grid | Done | Snap points to grid (F9 toggle) |

### 2.4 Code Editor

| ID | Feature | Status | Description |
|----|---------|--------|-------------|
| FR-040 | Syntax Highlighting | Done | C# syntax colors |
| FR-041 | Line Numbers | Done | Visible line numbers |
| FR-042 | Code Formatting | Done | Auto-format with Ctrl+Shift+F |
| FR-043 | Error Display | Done | Errors shown in footer |
| FR-044 | Autocomplete | Done | IntelliSense for geometry |
| FR-045 | Error Highlighting | Done | Inline error markers |

### 2.5 File Operations

| ID | Feature | Status | Description |
|----|---------|--------|-------------|
| FR-050 | New File | Done | Create new code file |
| FR-051 | Open File | Done | Open existing .cs/.viz files |
| FR-052 | Save File | Done | Save current code |
| FR-053 | Export PNG | Done | Export canvas to PNG |
| FR-054 | Export SVG | Done | Export as vector graphics |

---

## 3. Non-Functional Requirements

### 3.1 Performance
| ID | Requirement | Target |
|----|-------------|--------|
| NFR-001 | Code execution time | < 2 seconds for typical scripts |
| NFR-002 | Canvas redraw | < 100ms for < 1000 shapes |
| NFR-003 | Zoom/Pan responsiveness | < 50ms latency |

### 3.2 Usability
| ID | Requirement | Description |
|----|-------------|-------------|
| NFR-010 | Learning curve | New users productive within 5 minutes |
| NFR-011 | Error messages | Clear, actionable error descriptions |
| NFR-012 | Keyboard shortcuts | Standard shortcuts for common actions |

### 3.3 Compatibility
| ID | Requirement | Status |
|----|-------------|--------|
| NFR-020 | Windows 10/11 | Supported |
| NFR-021 | .NET 9.0 | Required |

---

## 4. User Interface Requirements

### 4.1 Layout
```
┌─────────────────────────────────────────────────────────────┐
│ [New] [Open] [Save] | [Run] [Clear] | [Format] | [Export] □ Grid │  <- Ribbon
├────────────────────────────────┬────────────────────────────┤
│                                │                            │
│                                │    // Code Editor          │
│         Canvas Area            │    Point p = new Point();  │
│         (2/3 width)            │    p.Draw();               │
│                                │                            │
│                                │    (1/3 width)             │
├────────────────────────────────┴────────────────────────────┤
│ Status: Ready              X: 50.00  Y: 25.00    Scroll: Zoom │  <- Footer
└─────────────────────────────────────────────────────────────┘
```

### 4.2 Theme
- Dark theme for canvas area (reduces eye strain)
- Light theme for code editor (better code readability)
- Accent color: Blue (#007ACC)

---

## 5. Technical Architecture

### 5.1 Technology Stack
- **Framework**: WPF (.NET 9.0)
- **Code Editor**: AvalonEdit
- **Script Execution**: Roslyn (Microsoft.CodeAnalysis.CSharp.Scripting)
- **Coordinate System**: Mathematical (Y-up, origin at center)

### 5.2 Key Components
1. **Geometry Module**: Shape classes with Draw() methods
2. **Canvas Module**: Custom WPF canvas with transforms
3. **Editor Module**: Syntax highlighting and formatting
4. **Execution Module**: Roslyn-based C# script runner

---

## 6. Success Metrics

| Metric | Target |
|--------|--------|
| Shape rendering accuracy | 100% geometric correctness |
| Code execution success rate | > 95% for valid code |
| User error recovery | Clear guidance within 1 error message |

---

## 7. Release History

### Version 1.0 (Current)
- Core shapes: Point, Line, Arc, Circle
- Extended shapes: Rectangle, Ellipse, Polygon, Polyline
- Shape styling: Colors, thickness
- Canvas: Zoom, Pan, Grid, Coordinates
- Editor: Syntax highlighting, formatting
- Export: PNG

### Version 1.1 (Implemented)
- Bezier curves and splines
- Autocomplete and IntelliSense in editor
- SVG, DXF, PDF, MP4 export
- Snap to grid with 8 snap types
- Region shape (curve-bounded areas with boolean ops)
- Animation system (Draw, Move, Rotate, Flip, Fade, ValueAnimation)
- Properties panel, shape editing, minimap
- Code navigation (Go to Definition, Find References, Rename)
- Boolean operations (Union, Intersect, Difference, Xor)

### Version 1.2 (Implemented)
- VDimension with AutoCAD-style arrowheads, extension lines, and text formatting
- Drag-and-drop file/folder moving in Project Explorer
- "Go to Location" context menu to open file/folder in Windows File Explorer
- MCP tools: get_project_context, update_file

### Version 1.3 (Implemented)
- RayCaster: accelerated 2D ray-casting against large shape collections (flat BVH with Surface Area Heuristic split, iterative traversal, allocation-free hot path, scales to millions of shapes)
- Query API: `FindIntersection` (closest hit, optional `maxDistance`), `HasIntersection` (any-hit early-out), `FindIntersections` (parallel batch), `Refit` (in-place AABB refresh after shape movement)
- Inline ray-vs-shape math for VLine, VCircle, VArc, VEllipse, VPolygon (and VRectangle), VPolyline; AABB fallback for other shape types
- `RayHit` and `RayQuery` record structs for ergonomic results and batching

### Version 1.3.1 (Implemented)
- RayCaster constructor now snapshots every visible Shape on `CanvasRenderer.Instance` (`new RayCaster()` — no explicit shape collection arg)
- `VPoint` markers are always excluded from the index (zero-area visual labels; not useful ray targets)
- Optional `List<Shape>? exclusionList` on `FindIntersection` — skip specified shapes from the candidate set (useful for casting off a source shape or finding the next hit past a known set)
- Slab-test robustness fix: zero direction components on a perpendicular degenerate AABB no longer poison the comparison chain with NaN

### Version 1.3.2 (Implemented)
- `CurveIntersection.IsPolylineSelfIntersecting`, `IsPolygonSelfIntersecting`, and `GetSegments` no longer allocate canvas-registered `VLine` objects in their inner loops. Discovered while debugging an isovist ray-cast workload that took ~5 s wall-clock: the slowness was not in `FindIntersection` but in the trailing `new VPolygon(points.ToArray())`, whose self-intersection check was dumping ~65k phantom `VLine` shapes onto the canvas (one per inner-loop iteration of an O(N²) test). Construction of a 360-vertex polygon now takes <1 ms and adds zero phantom shapes.
- Internal `VLine.Internal(VPoint, VPoint)` factory added (mirrors `VPoint.Internal`) for utility code that needs a `VLine` as a data container, not a drawn shape.
- Fixes mirrored to the parallel `C2VGeometry` namespace (which has the same auto-register pattern against `DefaultRegistry`).

### Version 1.4 (Implemented) — `ICurve.SetBounds`
- New `void SetBounds(double startParameter, double endParameter)` on `ICurve`: trims the curve in place so its parameter sub-range [s, e] becomes the new [0, 1]. Inputs clamped to [0, 1] and swapped if reversed.
- **Open curves** are trimmed in place:
  - **VLine** — `Start`/`End` mutated via `Evaluate`; the VPoint instances are preserved so external references stay live.
  - **VArc / VEllipse** — `StartAngle`/`EndAngle` rescaled.
  - **VBezier** — exact trim via two De Casteljau subdivisions (split at end, then within that piece at start/end); P0..P3 instances preserved.
  - **VPolyline** — Points list rebuilt with trimmed endpoints plus interior vertices strictly within [s, e]; `_selfIntersecting` recomputed.
  - **VSpline** — dense resample at the original render resolution. Catmull-Rom tangents depend on neighboring control points, so simply retaining inner CPs visibly bent away from the original path; resampling at `numSpans × SegmentsPerSpan × (e - s)` points keeps the trimmed Catmull-Rom passing through enough interpolating samples that it tracks the original closely.
- **Closed/infinite curves throw `NotSupportedException`** because their trimmed form would be a different shape type: VCircle → arc, VPolygon → polyline, VRay/VXLine → line. The exception message points callers to `SplitAtPoint` instead.
- All changes mirrored to the parallel `C2VGeometry` namespace.
- Test coverage: 17 cases in `Tests/SetBoundsTests.cs` covering trim correctness, instance preservation, parameter clamping/swap, fidelity (Bezier exact, Spline resample), and the throw paths. Full suite passes (117/117).

### Version 1.5 (Implemented) — Animator Sub-Project
- New sub-application **Animator** under `Animator/`, builds to `Animator.exe`. Standalone WPF app focused on p5.js-style `Setup()` + `Draw()` sketches. Depends only on `C2VGeometry.csproj` (no Code2Viz.dll dependency).
- **Sketch model** — Override `Animator.Sketching.Sketch.Setup()` (once) and `Draw()` (every frame). Sketch base provides `Size(w,h)`, `Background(color)`, `Loop()`/`NoLoop()`, plus per-frame state: `FrameCount`, `ElapsedSeconds`, `DeltaSeconds`, `Width`/`Height`, `MouseX`/`MouseY`/`MousePressed`, `KeyPressed`/`LastKey`. Persistent state lives in fields on the sketch class; shapes are constructed fresh each frame using `C2VGeometry` types and auto-register via `C2VGeometry.Shape.DefaultRegistry`.
- **Direct C2VGeometry renderer** — `Animator/Canvas/AnimCanvas.cs` switches over each shape type in a single DrawingVisual / DrawingContext pass. No adapter to `Code2Viz.Geometry`; the renderer accepts C2VGeometry types natively. Pan-only (mouse-wheel zoom intentionally disabled); the sketch boundary is drawn as a faint centered outline so the working area is visible.
- **Editor with IntelliSense** — AvalonEdit with Code2Viz's `CSharpHighlighting.xshd` embedded. Roslyn-backed completion via `SemanticModel.LookupSymbols`; auto-trigger on `.` and identifier characters; manual via `Ctrl+Space`. Console renders errors red, warnings yellow via typed `ConsoleLine(Level, Source, Message)` and WPF `DataTrigger`s.
- **Cross-app switch** — `Switch to Animator` in Code2Viz and `Switch to Project` in Animator launch the sibling exe and close the current window; each app finds the other by probing `bin\{Debug|Release}\net9.0-windows\` paths. Both apps prompt to save unsaved changes before switching or closing.
- **UX** — Ctrl+Enter toggles Run/Stop; standard Ctrl+S/O/N file ops; F5 / Shift+F5 for explicit run/stop. Dirty-flag tracking with a Yes/No/Cancel prompt on New, Open, Switch, and window close. Save-as cancellation aborts the surrounding flow.
- **Architecture invariants** — `Code2Viz.csproj` excludes `Animator\**` from its compile sets (Compile/Page/ApplicationDefinition/Resource/None) so the WPF compile of Code2Viz doesn't pick up Animator's XAML. The Animator namespace is `Animator.Sketching` (not `Animator.Sketch`) to avoid colliding with the `Sketch` class name in `using` statements.

### Version 1.6 (Implemented) — Geometry Unification
- **Single geometry namespace** — `Code2Viz.Geometry` is retired and deleted; `C2VGeometry` is now the one geometry namespace, shared by both Code2Viz and Animator. The C2VGeometry↔Code2Viz.Geometry adapter and the parity-test scaffolding that bridged the two namespaces are removed. User scripts import `using C2VGeometry;`.
- **`VXYZ` is the coordinate type; `VPoint` is only a drawable marker** — all positions, vectors, and coordinate parameters/properties/return types (circle centers, line endpoints, polygon vertices, `BoundingBox.Min/Max`, `ICurve.Divide` results, etc.) are the `VXYZ` value type. `VPoint` is now reserved exclusively for a visible point marker drawn on the canvas.
- **`RayCaster` ported into `C2VGeometry`** — the accelerated 2D ray caster (flat BVH + SAH split, allocation-free hot path, parallel batch, in-place `Refit`) lives in the unified namespace; `VPoint` markers and infinite-bounds shapes (VRay/VXLine) remain excluded from the index.
- **`ShapeDefaults` reconciled into `C2VGeometry` construction** — global style and dimension-style defaults are applied at shape-construction time in the unified namespace.
- **`CanvasRenderer` is the canonical `IShapeRegistry`** — shapes auto-register against `CanvasRenderer.Instance` through the registry interface in Code2Viz, while Animator continues to register against `DefaultRegistry`. One registry abstraction backs both hosts.

### Version 1.6.1 (Implemented) — Editor & Canvas Fixes
- **CodeLens no longer blinks on broken syntax** — a nearby structural syntax error made Roslyn error-recovery intermittently drop the following declaration's method classification, so alternating recomputes added/dropped its 2×-tall CodeLens row and bounced the code below. `UpdateCodeLens` now only swaps the item set on a clean parse, merges (never removes) on a broken parse, and leaves the set untouched on a failed build. Shared `Editor/` source, so both apps benefit.
- **Canvas click grabs keyboard focus** — `RenderCanvas.OnMouseDown` now takes focus on any click, so single-key drawing-tool shortcuts (P/L/C/R, plus Delete/A/Esc) activate the tool instead of typing the letter into the code editor. Click the canvas to focus it first.
