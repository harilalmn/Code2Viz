# Geometry Strategy

**As of the geometry-unification work (May 2026), Code2Viz has ONE geometry namespace: `C2VGeometry`.**

The old `Code2Viz.Geometry` namespace (WPF-coupled, `VPoint`-as-coordinate, hard-wired to
`CanvasRenderer.Instance`) was retired and deleted. Both the Code2Viz app and the Animator
sub-project now reference the single, WPF-free `C2VGeometry` library project.

See [`GEOMETRY_MERGE_SPIKE.md`](GEOMETRY_MERGE_SPIKE.md) for the migration plan, the spike that
de-risked it, and the per-increment record.

## Key points

- **`C2VGeometry`** is the single source of truth for all geometry: shapes, the `VXYZ`
  coordinate value-type, `RayCaster`, `SpatialGrid`/`KDTree`, `CurveIntersection`, `Region`,
  hatch patterns, and `ShapeDefaults`. It has no WPF/runtime dependency.
- **Coordinates are `VXYZ`** (a value type). **`VPoint` is only a drawable point marker.**
- **`CanvasRenderer` implements `C2VGeometry.IShapeRegistry`** and is registered as
  `C2VGeometry.Shape.DefaultRegistry`, so shapes auto-register onto the canvas on construction.
  There is no `C2VGeometryAdapter` / conversion layer anymore.

## Drift guardrail (historical)

`Tests/GeometryParityTests.cs` previously cross-checked the two libraries to detect behavioral
drift. With a single library there is nothing to mirror, so that suite was removed. New geometry
regressions are guarded by the regular `Tests/` suite — e.g. `GeometryRegistryPollutionTests.cs`
(internal edge curves must use `VLine.Internal`, never auto-register), `C2VRayCasterTests.cs`,
and `C2VShapeDefaultsTests.cs`.
