# Changelog

All notable user-facing changes to Code2Viz (and its Animator sub-project) are
documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project uses
calendar versioning (`YEAR.MONTH.PATCH`).

Each GitHub release also carries auto-generated notes built from the commit log
between tags; this file is the curated, human-friendly summary.

## [Unreleased]

## [2026.5.7] - 2026-05-30

### Fixed
- **Boolean operations on overlapping shapes no longer fail in degenerate cases.** Union,
  intersection, difference and XOR — for both polygons (`BooleanOps`) and regions
  (`RegionBooleanOps`) — are now powered by the robust [Clipper2](https://github.com/AngusJohnson/Clipper2)
  library. Previously a hand-rolled clipper could wrongly report two clearly overlapping shapes as
  "disjoint" and return `null`/empty — most visibly when a circle was centered on a rectangle's
  corner (its sampled vertices landed exactly on the rectangle's edges) or when two rectangles
  shared a full edge. Those now union/intersect correctly, and results with holes (e.g. a square
  minus a fully-enclosed square) are represented properly via the `*WithHoles` variants.

## [2026.5.6] - 2026-05-29

### Added
- **Build a `Region` from a single closed curve.** `new Region(closedCurve)` accepts any
  circle, ellipse, closed polygon, or closed polyline/spline/bezier and turns it into a
  filled, curve-bounded region — no more hand-assembling a list of edge curves. Circles and
  ellipses keep their true curve geometry; polygons/polylines decompose into edges. The source
  curve is *consumed* (removed from the canvas) so its outline isn't drawn twice. A matching
  `AddHole(closedCurve)` overload lets you punch a hole from another closed curve.
- **"Flex" sliders in the Properties panel.** Every numeric geometry property (radius, angle,
  coordinates, …) now has a slider beneath its value, with small editable min/max boxes on each
  end. Drag to sweep the value and watch the canvas update live; release to commit the change
  back to your source code. Typing in the value box moves the slider (range auto-expands), and
  the min/max boxes retarget the slider's range.
- **Boolean operations accept a `List<Region>`.** `RegionBooleanOps.Union/Intersect/Difference/Xor`
  now take a whole collection (a `List<Region>`, array, or `params`) in addition to a pair —
  Union merges all, Intersect keeps the area common to every region, Difference subtracts the rest
  from the first, and Xor folds the symmetric difference. The `BooleanOps` facade also accepts
  regions now (it forwards to `RegionBooleanOps`), so `BooleanOps.Union(listOfRegions)` works.

## [2026.5.5] - 2026-05-29

### Added
- **Animator remembers View-menu state across runs.** The Inlay Hints, Semantic
  Highlighting, Code Lens, Minimap and Console toggles (plus the console
  splitter height) now persist to `%AppData%\Code2Viz\animator_settings.json`.
- **Completion auto-triggers after `new` / `is` / `as`.** Pressing space after
  one of those keywords pops up the candidate type list immediately, and the
  first letter you type then continues to match — no more waiting for the
  second character or hitting `Ctrl+Space` manually.
- **PathAnimation works on `VGroup` targets.** A whole group can now ride along
  any `ICurve` path; previously the animation drove the group's offset but the
  renderer ignored it, so nothing moved. `MoveAnimation` against a group works
  for the same reason.

### Changed
- **Animator IntelliSense matches Code2Viz's reference set.** The editor's
  Roslyn workspace now sees `System.IO`, `System.Text.Json`,
  `System.Collections.Immutable`, `System.Linq.Expressions`,
  `System.Threading.Tasks` and the rest — types in those namespaces resolve in
  completion lookups instead of silently dropping out.
- **Animator boilerplate includes the common usings.** New sketches start with
  `using System; using System.Linq; using System.Collections.Generic; using C2VGeometry;`
  visible at the top of the file; `System.Linq` and `System.Collections.Generic`
  also became global usings, so `List<VXYZ>` resolves even in older sketches.
- **Canvas drawing tool emits `VXYZ` vertex args.** Polygons, polylines and
  splines drawn directly on the canvas now generate
  `new VPolygon(new VXYZ(...), ...)` instead of `new VPolygon(new VPoint(...), ...)`.
  This stops every polygon vertex from auto-registering as a phantom point
  marker on the canvas. The built-in code snippets (`vpoly`, `vbezier`,
  `vspline`, `vlinea`, `star`, `wave`) were updated to match.

### Fixed
- **Old projects that used `new VPoint(...)` as vertex args still compile.**
  Added an implicit conversion from `VPoint` to `VXYZ` so legacy code like
  `new VPolygon(new VPoint(...), new VPoint(...))` keeps working against the
  current `VXYZ`-based constructors. New code should still use `VXYZ` directly
  because constructing a `VPoint` auto-registers it on the canvas as a visible
  marker.

## [2026.5.4] - 2026-05-28

### Added
- **Charts** — new `Chart` static helper builds Chart.js-style charts (`Chart.Bar`,
  `Chart.Line`, `Chart.Scatter`, `Chart.Pie`, `Chart.Area`) out of standard
  C2VGeometry primitives. Each call returns a `VGroup` containing axes, gridlines,
  ticks, labels and data shapes, with auto-fit "nice" axis ranges and a 10-color
  default palette. Configurable through a new `ChartOptions` record (plot
  origin/size, axis ranges, tick counts, label rotation, title, gridlines,
  per-element colors, palette, decimal places). Works unchanged in Animator
  because it emits only existing shapes — no canvas changes required.

### Fixed
- **F1 Help now lists C2VGeometry types again.** When `C2VGeometry` was extracted
  into a separate assembly, the documentation generator was still scanning only
  the Code2Viz assembly, so the geometry tree (VLine, VCircle, VHatch, …) showed
  up empty. The generator now scans both assemblies.

## [2026.5.3] - 2026-05-27

### Added
- This changelog.

### Changed
- GitHub release notes are now generated automatically from the commit log
  between tags, so every release has real notes (the body was previously only a
  bare "compare" link).

## [2026.5.2] - 2026-05-27

### Added
- **Animator branding ribbon** — a logo + title + "Sketch in dotnet." tagline +
  version strip above the menu bar, matching Code2Viz's header.
- **Out-of-process sketch host (foundation, off by default)** — `SketchHost.exe`
  can run sketches in a separate process so an infinite loop, out-of-memory, or
  native crash can't take down the app. Enabled with the `ANIMATOR_ISOLATE=1`
  environment variable; off by default while the remaining packaging is finished.

### Changed
- **Animator code completion now matches Code2Viz** — the editor uses the same
  Roslyn completion engine, gaining fuzzy matching with match highlighting,
  scope/expected-type ranking, per-kind icons, and a documentation sidecar that
  shows the selected member's signature and summary.

### Fixed
- **Sketches no longer crash the app on runaway recursion** — infinite or mutual
  recursion (previously an uncatchable stack overflow that killed the process) is
  now caught and stopped with a clear console message; the app keeps running.
  Applies to both Code2Viz and Animator.

---

Releases before 2026.5.2 predate this changelog; see the
[Releases page](https://github.com/harilalmn/Code2Viz/releases) and git history.

[Unreleased]: https://github.com/harilalmn/Code2Viz/compare/v2026.5.3...HEAD
[2026.5.3]: https://github.com/harilalmn/Code2Viz/releases/tag/v2026.5.3
[2026.5.2]: https://github.com/harilalmn/Code2Viz/releases/tag/v2026.5.2
