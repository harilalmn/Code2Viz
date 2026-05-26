# Geometry Merge Spike & Migration Plan

Companion to [`GEOMETRY_STRATEGY.md`](GEOMETRY_STRATEGY.md). That doc records *why*
two geometry namespaces exist today and the parity-test guardrail. **This** doc records
the plan and the measured results of a spike to **collapse them into one**, plus an
analysis of which Code2Viz features can flow to Animator once that's done.

Status: **spike complete, full migration not started.** Spike proof lives on branch
`worktree-vcircle-geometry-spike` (commit `b74a8f4`) — buildable, 152/152 tests green.

---

## 1. The problem (the real DRY pain)

| Layer | Status |
|---|---|
| **Editor** (`Editor/`, ~13K LOC) | ✅ Already shared — linked into Animator, one `SharedEditorController`. No duplication. |
| **Geometry** | ❌ **The big one.** `Code2Viz.Geometry` (~16K LOC, WPF-coupled, `VPoint` coords, hard-wired to `CanvasRenderer.Instance`) vs `C2VGeometry` (~15K LOC, WPF-free library, `VXYZ` coords, pluggable `IShapeRegistry`). ~99% identical logic. **Every shape/curve/fix is written twice.** |
| **Sketch runtime** | ❌ `Code2Viz.Sketching.*` vs `Animator.Sketching.*` (~470 LOC, near-identical). |
| **Canvas drawing** | ❌ `RenderCanvas` (full editor) vs `AnimCanvas` (pan-only) draw the same primitives. |
| **Console** | ❌ Two near-identical `ConsoleOutput`. |

Everything below the editor is duplicated **because** the two canvases speak different
geometry namespaces. Unify geometry → the rest collapses. Note: Code2Viz *already*
references `C2VGeometry.csproj` and already runs sketches (`ModuleCompiler` detects a
`Sketch` subclass → `Code2Viz.Sketching.SketchRuntime`, converting shapes onto its canvas
via `Sketch/C2VGeometryAdapter.cs`).

**Target:** retire `Code2Viz.Geometry`; make Code2Viz consume `C2VGeometry` directly, with
`CanvasRenderer` implementing `C2VGeometry.IShapeRegistry`. The adapter then disappears.

---

## 2. Measured blast radius (read-only)

- **42 files** reference the `Code2Viz.Geometry` namespace (the migration surface).
- **133 shape-type `switch` arms** across 7 files (`RenderCanvas` has 76).
  - **Key finding:** both namespaces use **identical type names** (`VCircle`, `VPoint`, …),
    so swapping `using Code2Viz.Geometry;` → `using C2VGeometry;` **rebinds most
    `case VCircle:` arms with no textual edit.** The breakage is *not* the switch arms.
- Real edit-points concentrate in: the **coordinate ripple** (`VPoint` → `VXYZ`),
  `ShapeDefaults` (Code2Viz-only), `RayCaster` (Code2Viz-only, ~920 LOC, must be ported),
  and the canvas registry/collection seam.

---

## 3. The VCircle spike — what was done & what it proved

Done in worktree `worktree-vcircle-geometry-spike` (~211 LOC across 3 files):

1. **Registry seam** — `CanvasRenderer : C2VGeometry.IShapeRegistry`
   (`Register`/`Unregister`/`MoveAbove`/`MoveBehind` + a parallel `_c2vShapes` list;
   in the real migration this list *replaces* `_shapes`, not sits beside it).
2. **Render seam** — a draw pass over `GetC2VShapes()` + `DrawCircleC2V` (a **verbatim**
   copy of `DrawCircle` with the parameter retyped to `C2VGeometry.VCircle`).
3. **Test** — `Tests/VCircleRegistrySpikeTests.cs`, 4 facts in the `CanvasState` collection.

### Results
- ✅ Builds clean (0 errors).
- ✅ **4/4 spike tests pass**, **152/152 full suite** (no regressions from the `Clear()` change).
- ✅ A `C2VGeometry.VCircle` auto-registers onto the Code2Viz canvas, reports correct
  `GetBounds()`, `Clear()` unregisters + resets the id counter, and `SendBehind` reorders
  draw order — **all with no `C2VGeometryAdapter`.**

### Measured friction (the whole point of the spike)
| Friction | Severity | Notes |
|---|---|---|
| **Registry seam** | 🟢 None | `C2VGeometry.Shape` already routes registration through `IShapeRegistry` and already has full property parity (Id, Name, IsPlaced, IsSelected, IsVisible, all styling + animation props). `CanvasRenderer` became a registry in ~50 LOC. |
| **Per-shape render port** | 🟢 Mechanical | `DrawCircle`'s body reads only `.Center.X/.Y`, `.Radius`, and shared style/animation props → coordinate-type-agnostic. Each `DrawX` is a retype, not a rewrite. |
| **Enum duplication** | 🟡 Small, pervasive | `circle.LineType` is `C2VGeometry.LineType`; `GetCachedPen` wants `Code2Viz.Geometry.LineType` → needed `(LineType)(int)` cast. Same applies to `ControlPoint`, `BoundingBox`, `ControlPointType` — all duplicated across namespaces. The migration **deletes** the Code2Viz copies, so these casts vanish (they're an artifact of the *parallel* spike, not the end state). |
| **Coordinate ripple** | 🟠 The real cost | `VPoint` → `VXYZ`. `DrawCircle` dodged it (only `.X/.Y`), but `MoveControlPoint(int, VPoint)` signatures, `SnapEngine`, `SelectionTool`, `MeasuringTool`, `PropertiesPanel`, exporters, and `CodeGenerator` all traffic in `VPoint`. This is the bulk of the manual work. |
| **`RayCaster` / `ShapeDefaults`** | 🟠 Port required | Exist only in `Code2Viz.Geometry`. Must be ported into `C2VGeometry` (or kept app-side against `C2VGeometry` types). |
| **Snap coupling** | 🟠 Verify | `SnapEngine` is `Code2Viz.Geometry`-typed; snapping to a native C2V shape needs it retyped. (Flagged in the spike; not yet exercised end-to-end.) |

**Bottom line:** the *seam* is free; the *cost* is the `VPoint`→`VXYZ` coordinate ripple
across ~42 files plus porting `RayCaster`/`ShapeDefaults`. Because type names are shared,
much of it is a find/replace of `using` directives, not logic edits.

---

## 4. Full migration plan (phased)

1. **Coordinate unification first.** Decide `VXYZ` is the canonical coordinate (it is — it's
   a plain value type, not a `Shape`, unlike Code2Viz's overloaded `VPoint`). Port
   `RayCaster` + `ShapeDefaults` into/against `C2VGeometry`. Add `Shape.Default*` ↔
   `ShapeDefaults` reconciliation.
2. **Registry seam (done in spike).** `CanvasRenderer : IShapeRegistry`, `_shapes` becomes
   `List<C2VGeometry.Shape>`, set `C2VGeometry.Shape.DefaultRegistry = CanvasRenderer.Instance`
   on the `Main()` path. Delete `C2VGeometryAdapter` + `C2VGeometryRegistry`.
3. **Repoint the app.** Swap `using Code2Viz.Geometry;` → `using C2VGeometry;` across the 42
   files; fix the `VPoint`→`VXYZ` signature mismatches the compiler flags; delete the
   `Code2Viz.Geometry` folder.
4. **Render port.** `RenderCanvas` `DrawX` methods retype to `C2VGeometry` shapes (switch
   arms rebind for free). Retire `GeometryParityTests` (one library now).
5. **Then** unify the sketch runtime + console, and either share `RenderCanvas`/extract a
   `ShapeRenderer` so `AnimCanvas` and `RenderCanvas` stop duplicating draw code.

Sizing: the spike confirms each shape's render+register port is ~mechanical; the schedule
is dominated by the coordinate ripple and re-testing, not by novel design. Recommend doing
it shape-family by shape-family behind the parity tests until the last `Code2Viz.Geometry`
reference is gone.

---

## 5. Feature analysis — Code2Viz features → Animator

Almost everything Animator lacks is gated by one of three things: **(G)** the geometry
split, **(P)** the project model, or it's **(E)** editor (already shared) / **(S)** portable.

| Feature | Blocker | Path |
|---|---|---|
| **NuGet packages** | **P** | Code2Viz declares packages in the `.vizproj` (`ProjectFile.Packages`), restores to `.packages/` via `Execution/NuGetHelper.cs`, references them as metadata, and resolves them at runtime in the custom `AssemblyLoadContext`; there's a `NuGetPackageManagerWindow` UI. Animator (single file, no project) has nowhere to declare them. Paths: **(a)** adopt the unified 1-file project model so a sketch carries packages, or **(b)** lighter — support in-file `#r "nuget: Id, version"` directives + port `NuGetHelper` + ALC resolution into `Animator/Compiler/SketchCompiler.cs`. **Independent of the geometry merge.** |
| **Multi-file sketches / project tree** | **P** | A sketch is a degenerate 1-file `VizCodeProject` (`VizFileKind.Sketch` already exists). Unifying the project model unlocks this *and* NuGet together. |
| **Zoom / snap / measuring / editing tools / selection** | **G** | Live in `RenderCanvas`, which speaks `Code2Viz.Geometry`; `AnimCanvas` is pan-only. After the geometry merge, Animator can host `RenderCanvas` or share a `ShapeRenderer`. |
| **Zoom-to-shape-by-id, Properties panel** | **G** | Same canvas dependency. |
| **Video / GIF / PDF export** | **S (partly done)** | Animator has a lean `SketchExporter`; Code2Viz's `VideoExporter`/`PdfExporter` could be linked. Mostly standalone. |
| **Undo/redo** | **G** | Built on the editing tools → follows the canvas/geometry work. |
| **Find/replace, minimap, completion, folding, snippets, signature help, semantic highlight** | **E** | **Already shared** via linked `Editor/`. No work. |
| **Templates / welcome flow** | **S** | Welcome dialog already has a mode toggle; templates port easily. |

**Sequencing implication:** two big investments are **independent**. *Geometry merge →
unlocks the whole canvas-feature column.* *Unified project model → unlocks NuGet +
multi-file.* They can proceed in parallel.

---

## 6. Recommendation

- **Do not** merge into one mega-app. Code2Viz already runs sketches with full features;
  Animator's value is the lean window. Keep two thin hosts over a shared core — the pattern
  the linked editor already proves works.
- **Highest-value first move:** the geometry unification (this doc's spike subject). It's the
  ~80% DRY win and the keystone for the canvas-feature column.
- **NuGet for Animator** is a separate, independent track gated on the project model, not on
  geometry — pursue via option (a) unified 1-file project model or (b) in-file `#r "nuget:"`
  directives.
