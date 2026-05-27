# Sketch Isolation Plan — making Animator crash-proof against user code

## Goal

A user sketch is arbitrary C# we compile and run. **No blunder in a sketch should ever take
down `Animator.exe`.** This document records what is already done (Phase 1) and the design for
full immunity (Phase 2).

## Threat model — how user code can kill the host

| Blunder | Mechanism | Catchable in-process? |
|---|---|---|
| Null deref, div-by-zero, index out of range, `throw` | normal managed exception | ✅ Already caught in `SketchRuntime.Tick`/`Start` |
| **Infinite / deep recursion** | `StackOverflowException` | ❌ CLR fails fast — **uncatchable** |
| Runaway allocation | `OutOfMemoryException` | ⚠️ Sometimes catchable, process often left unstable |
| Bad interop / unsafe code | `AccessViolationException` (corrupted state) | ❌ Not delivered to managed `catch` by default |
| Explicit `Environment.FailFast(...)` | immediate termination | ❌ By design |
| `while(true){}` (no recursion, no allocation) | never throws | ❌ Hangs the render thread forever |

Since .NET Core removed AppDomains, **the only hard isolation boundary left is an OS process.**
That is the core conclusion driving Phase 2.

## Phase 1 — Stack-guard injection (DONE)

Kills the entire `StackOverflowException` class — which is the most common real-world sketch
blunder (the reported `circlesFill.cs` crash was mutual `Grow`↔`Shrink` recursion).

- `Execution/StackGuardRewriter.cs` (namespace `Code2Viz.Execution`) — a `CSharpSyntaxRewriter`
  that injects `RuntimeHelpers.EnsureSufficientExecutionStack()` at the top of every method-like
  body (methods, local functions, constructors, operators, accessors, expression-bodied
  properties/indexers). That API throws a **catchable** `InsufficientExecutionStackException`
  *before* the stack actually overflows. Single-sourced here and **linked** into Animator
  (`Animator.csproj`) — same convention as `Editor/`.
- Wired into **both** compilers:
  - Code2Viz `ModuleCompiler.CreateCompilationAsync(project, injectStackGuards: true)` — covers
    Main() mode and Code2Viz sketch mode. The param defaults to `false` so shared callers that map
    editor offsets onto the compilation (`CheckSyntaxAsync`, `RefactoringProvider`, MainWindow) are
    unaffected (the guard shifts in-line offsets).
  - Animator `SketchCompiler.CompileAndRunAsync` — always injects (sketch-only, no offset-based
    features); recreates the tree with the original path + UTF-8 encoding for PDB line mapping.
- `SketchRuntime.ReportError` (Animator) gives a friendly "runaway recursion" message; Code2Viz's
  Main() path reports it via the normal runtime-error formatter.
- Tests: `Tests/StackGuardRewriterTests.cs` (reaches the rewriter via the Code2Viz project
  reference). The behavioral tests compile guarded mutual/expression-body recursion and assert it
  throws the catchable exception instead of crashing the test host.

**Phase 1 does NOT cover:** infinite loops, OOM, native AVs, or `FailFast`. Those need Phase 2.

## Phase 2 — Process isolation (PLANNED)

Run the compiled sketch in a **separate child process**. If the child dies (StackOverflow, OOM,
AV, FailFast) or hangs (watchdog timeout → kill), the UI process (`Animator.exe`) detects the
exit, reports it in the console, and stays alive. This is the only way to be immune to *every*
blunder, including infinite loops.

### Architecture

```
┌────────────────────────┐         named pipe          ┌──────────────────────────┐
│  Animator.exe (UI)     │  ── source / control ──▶    │  SketchHost.exe (child)   │
│  - AnimCanvas          │                             │  - compiles the sketch    │
│  - editor / console    │  ◀── frame shape data ──    │  - runs Setup/Draw loop   │
│  - watchdog + restart  │  ◀── console / errors ──    │  - DefaultRegistry sink   │
└────────────────────────┘                             └──────────────────────────┘
```

- **New project `SketchHost/` (`SketchHost.exe`, console).** Owns the `AssemblyLoadContext`,
  the `SketchRuntime`, and the `ShapeRegistry`. Reuses `SketchCompiler` + `StackGuardRewriter`
  (keep the stack guard — cheaper than respawning on every recursion bug).
- **IPC over the existing named-pipe stack (`McpBridge/`).** Reuse the framing/host code that
  already drives the MCP bridge rather than inventing a new transport.
- **UI → host messages:** `Compile(source, path)`, `Stop`, `SetLooping`, input state
  (`mouseX/Y`, `mousePressed`, `lastKey`), export-frame requests.
- **Host → UI messages:** per-frame shape batch, `Size`/`Background`/zoom requests, console
  lines, and a terminal `Error(phase, message)`.
- **Watchdog (UI side):** if a `Draw` frame exceeds a budget (e.g. 2–4 s) the UI kills and
  respawns the child and prints "sketch stopped: frame took too long (likely an infinite loop)".
  Catches the one class Phase 1 can't — `while(true){}`.
- **Crash handling (UI side):** subscribe to `Process.Exited`; a non-zero exit (or signal) →
  report "sketch process crashed: <exit reason>" and return the canvas to idle. The UI never
  shares a faulting stack/heap with the sketch, so it cannot be dragged down.

### Hard problem: per-frame geometry throughput

At 60 fps with thousands of shapes, serializing the full shape set every frame is the main risk.
Options, cheapest first:

1. **Compact binary frame protocol** — a flat per-shape record (type tag + numeric fields +
   color), length-prefixed, written to a `MemoryMappedFile` ring buffer; the pipe only carries a
   "frame N ready" signal. Avoids per-shape allocation and large pipe writes.
2. **Delta frames** — only send shapes that changed since last frame. Bigger win for mostly-static
   scenes, more bookkeeping.
3. **Render in the child, ship pixels** — child draws to an offscreen bitmap, ships the framebuffer
   (or a shared D3D/WIC surface). Trivially bounded bandwidth, but loses vector fidelity / canvas
   pan and complicates hit-testing.

Recommended: start with **(1)**; measure; add **(2)** only if a real sketch needs it.

### Migration steps

1. Extract `SketchRuntime` + `ShapeRegistry` + `SketchCompiler` references behind an interface so
   they can run host-side unchanged.
2. Stand up `SketchHost.exe` with the pipe loop; prove a trivial sketch round-trips one frame.
3. Implement the binary frame protocol + shared-memory ring; port `AnimCanvas` to render from a
   decoded frame batch instead of a live `Shape` list.
4. Add the watchdog + crash/restart UX on the UI side.
5. Bundle `SketchHost.exe` into the installer next to `Animator.exe` (mirror the existing
   `{app}\Animator\` packaging rule — see `installer.iss`).
6. Delete the in-process `Start`/`Tick` path once parity is verified.

### Cost / risk

- **Effort:** medium-large (new exe, IPC protocol, canvas render-from-batch rewrite, packaging).
- **Risk:** frame-serialization throughput is the make-or-break; everything else is plumbing.
- **Payoff:** complete immunity — infinite loops, OOM, native crashes, and `FailFast` all become
  "the sketch process died, here's why" instead of a dead Animator.
