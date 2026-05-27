# Changelog

All notable user-facing changes to Code2Viz (and its Animator sub-project) are
documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project uses
calendar versioning (`YEAR.MONTH.PATCH`).

Each GitHub release also carries auto-generated notes built from the commit log
between tags; this file is the curated, human-friendly summary.

## [Unreleased]

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

[Unreleased]: https://github.com/harilalmn/Code2Viz/compare/v2026.5.2...HEAD
[2026.5.2]: https://github.com/harilalmn/Code2Viz/releases/tag/v2026.5.2
