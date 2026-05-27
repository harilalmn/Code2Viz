# Manual Testing Guide — Geometry Unification Repoint

Validates the `feature/geometry-unification` branch, where the whole app was repointed
from `Code2Viz.Geometry` to the unified `C2VGeometry` namespace (`VPoint`-as-coordinate →
`VXYZ`, `CanvasRenderer` became the `IShapeRegistry`, `RenderCanvas`/tools/exporters/
sketch-runtime all retyped, `Code2Viz.Geometry` deleted).

**Automated coverage already passing:** 131 unit tests, headless render of circle/rectangle/
polygon, and a polygon-edge-pollution regression. This guide covers what those can't:
interactive behavior, every shape on screen, tools, animation, export, and both apps' UX.

**Why focus here:** the heaviest churn was coordinate handling (`VPoint`→`VXYZ`) in the
canvas, snap engine, selection/edit tools, measuring tool, exporters, and properties panel —
plus the sketch-runtime rework. Items tagged **⚠️REPOINT-RISK** touched that code directly.

## Build & launch
```
dotnet build Code2Viz.csproj -c Debug
dotnet build Animator/Animator.csproj -c Debug
# Code2Viz:  bin\Debug\net9.0-windows\Code2Viz.exe
# Animator:  Animator\bin\Debug\net9.0-windows\Animator.exe
```

## Pass/fail legend
For each case: **PASS** = expected result observed · **FAIL** = note what happened. The single
most important regression to watch everywhere: **shape outlines must render in the shape's own
color** (not cyan) and there must be **no phantom duplicate shapes** — that was the polygon-edge
pollution bug.

---

# ⚡ 60-second smoke test (do this first)

1. Launch Code2Viz → open/create a project. Paste into `Main()` and Run (F5):
   ```csharp
   new VCircle(0, 0, 100);                                   // expect Yellow
   new VRectangle(new VXYZ(150, -50), 120, 90);              // expect Magenta outline
   new VArc(new VXYZ(-160, 0), 80, 0, 120);                  // expect Orange
   new VPolygon(new[]{ new VXYZ(-80,120), new VXYZ(80,120), new VXYZ(0,220) }); // closed triangle
   new VLine(new VXYZ(-200,-180), new VXYZ(200,-180));
   ```
2. Expect: 5 shapes, **each in its own default color**, rectangle and triangle as **single
   clean outlines** (not cyan, no doubled/segmented edges). Zoom (wheel) and pan (middle-drag)
   work. → If this passes, the core repoint is sound; continue below for depth.

---

# Part A — Code2Viz (changed the most)

## A1. Rendering — every shape type ⚠️REPOINT-RISK
Every `DrawX` method was retyped. Render one of each and confirm correct shape, position, color.
```csharp
new VPoint(-200, 150);
new VLine(new VXYZ(-150,150), new VXYZ(-50,150));
new VCircle(0, 150, 40);
new VArc(new VXYZ(120,150), 40, 30, 300);
new VEllipse(new VXYZ(220,150), 50, 25);
new VRectangle(new VXYZ(-220,0), 80, 60);
new VPolygon(new[]{ new VXYZ(-60,0), new VXYZ(40,0), new VXYZ(-10,70) });
new VPolyline(new[]{ new VXYZ(100,0), new VXYZ(160,60), new VXYZ(220,0) });
new VBezier(new VXYZ(-220,-120), new VXYZ(-160,-40), new VXYZ(-80,-200), new VXYZ(-20,-120));
new VArrow(new VXYZ(40,-120), new VXYZ(160,-60));
new VText("Repoint OK", new VXYZ(-220,-200));
```
- [ ] Every shape appears at the right place, right color, no phantom duplicates, axes (red X / green Y) and grid look normal.
- [ ] **Dimensions:** add `new VDimension(0,0,200,0);` and a radial dimension — text, arrows, extension lines render (dimension style members were added to C2V).

## A2. Polygon / rectangle / region pollution regression ⚠️REPOINT-RISK (the bug we fixed)
- [ ] A `VRectangle` renders as **one** rectangle in its color — not 4 cyan line segments.
- [ ] A `VPolygon` renders as one closed outline in its color — not N cyan edges.
- [ ] Open the **Outliner / shape list** (if available): a single rectangle = **1** entry, not 5. (No edge `VLine`s leaked into the scene.)
- [ ] `Tools → Zoom to shape by ID` (Ctrl+G): IDs are contiguous (no skipped IDs from phantom shapes).

## A3. Interactive canvas tools ⚠️REPOINT-RISK (heaviest VPoint→VXYZ churn)
- [ ] **Snap (F9 on):** draw/hover near endpoints, midpoints, centers, intersections — snap markers appear at the correct world points.
- [ ] **Select & move:** click a shape, drag it — it moves to the cursor correctly (no offset/mirror/scale error from a bad coordinate conversion).
- [ ] **Edit control points:** select a circle → drag its radius handle; a rectangle → drag a corner; a line → drag an endpoint. The shape updates correctly. *(MoveControlPoint signatures changed VPoint→VXYZ — watch for handles that jump or invert.)*
- [ ] **Measuring tape (Ctrl+M):** measure a known distance — value is correct.
- [ ] **Drawing tools** (P/L/C/R when editor not focused): draw a point/line/circle/rectangle; hold Shift for orthogonal constraint.
- [ ] Zoom-to-fit on run, mouse-wheel zoom, middle-drag pan all behave.

## A4. Properties panel ⚠️REPOINT-RISK (immutable VXYZ setters)
- [ ] Select a shape, open Properties (F4). Edit a coordinate field (e.g. circle center X, rectangle corner). The shape moves to the new value correctly. *(VXYZ is immutable now — setters reassign the whole point; watch for edits that don't "take" or reset.)*
- [ ] Edit color / line weight / fill in the panel — updates live.

## A5. Sketch mode in Code2Viz ⚠️REPOINT-RISK (SketchRuntime reworked)
Write a `Sketch` subclass in the project (no adapter conversion now — shapes register straight onto the canvas):
```csharp
public class Spin : Code2Viz.Sketching.Sketch {
    double a;
    public override void Setup(){ Size(800,600); Background("Black"); }
    public override void Draw(){
        a += 2;
        var c = new VCircle(150*Math.Cos(a*Math.PI/180), 150*Math.Sin(a*Math.PI/180), 30){ Color="Cyan" };
    }
}
```
- [ ] Run → enters sketch mode, animates smoothly, **canvas clears each frame** (no buildup of stale circles — the clear-before-Setup/Draw change).
- [ ] Stop ends cleanly; running a normal `Main()` project afterward still works (registry restored, shapes still auto-register).
- [ ] A sketch that draws a **polygon/rectangle each frame** shows no cyan-edge pollution and no per-frame shape leak.

## A6. ShapeDefaults / project settings ⚠️REPOINT-RISK (rewired to C2VGeometry.ShapeDefaults)
- [ ] In project settings, set a default color/line-weight → new shapes pick it up on Run.
- [ ] In code: `ShapeDefaults.GlobalColor = "Red";` then create shapes → all come out red. Reset → back to per-type defaults.

## A7. Animations
```csharp
var c = new VCircle(0,0,40);
// then animate via the Animator API (Move/Rotate/Fade/Draw)
```
- [ ] Move, Rotate (uses VXYZ pivot now), Fade, and progressive Draw animations all play correctly. Timeline panel scrubbing works.

## A8. Export
- [ ] **PNG/PDF/DXF/SVG export** of a scene with mixed shapes → open the output; geometry positions/sizes match the canvas (exporters were retyped to VXYZ).

## A9. Project & app plumbing
- [ ] Save / open / new project; multi-file project compiles & runs.
- [ ] **IntelliSense:** typing `V` suggests geometry types; `VCircle(` shows signature help; geometry types complete even without an explicit `using` (the "add C2VGeometry types" feature now targets the C2VGeometry namespace).
- [ ] **Switch to Animator** button launches Animator.

---

# Part B — Animator

Animator already used `C2VGeometry`, so its app code was unchanged — but the **C2VGeometry
library itself changed** (ShapeDefaults mechanism, and the polygon-edge pollution fix). So the
key checks are rendering correctness for polygons/rectangles and that sketches still run.

## B1. Run a sketch ⚠️REPOINT-RISK (library changed under it)
Open/paste a sketch (Animator uses `Animator.Sketching.Sketch`, C2VGeometry shapes, `VXYZ` coords):
```csharp
using System; using System.Linq;
namespace Sketches;
public class Dots : Animator.Sketching.Sketch {
    public override void Setup(){ Size(800,600); Background("Black"); }
    public override void Draw(){
        for (int i=0;i<8;i++){
            double an = i*45 + Environment.TickCount/20.0;
            new VCircle(160*Math.Cos(an*Math.PI/180), 160*Math.Sin(an*Math.PI/180), 20){ Color="Cyan" };
        }
        new VRectangle(new VXYZ(-60,-40), 120, 80){ Color="Magenta" };   // ⚠️ must be ONE magenta rect, not cyan edges
        new VPolygon(new[]{ new VXYZ(-200,-200), new VXYZ(200,-200), new VXYZ(0,-120) }){ Color="Orange" };
    }
}
```
- [ ] Sketch runs; the **rectangle is one magenta outline** and the **polygon one orange outline** (no cyan-edge pollution — the exact library bug we fixed).
- [ ] Animation loop is smooth; pan (middle-drag) works (Animator is pan-only, no zoom).
- [ ] **Stop** button ends the sketch; typing any non-modifier key in the editor stops it; Ctrl+S / Shift+F5 do NOT stop it.

## B2. Editor (shared code)
- [ ] CodeLens reference counts show and stay stable while typing (incl. the earlier blink fix).
- [ ] Completion, signature help, semantic highlighting, folding, minimap work.

## B3. Plumbing
- [ ] New / Open / Save / Save As `.cs` sketches; recent-animations list.
- [ ] **Switch to Project** launches Code2Viz.
- [ ] Export GIF/MP4 of a sketch.

---

# Known non-blockers (do NOT log as repoint failures)
- **Editor squiggle "type … defined in an assembly that is not referenced" (CS0012)** on projects that use **NuGet packages** — a pre-existing NuGet transitive-dependency limitation, not caused by the repoint. The project still compiles and runs.
- **Docs/MCP still say `Code2Viz.Geometry`** (help text, `SKILL.md`, `ApiReferenceResource`, and the MCP "Available imports" string) — these are task 6 (docs sweep), not yet done. If you use the MCP integration, user code should now import `C2VGeometry`, not `Code2Viz.Geometry`.

# If you find a failure
Note: which app, which case ID (e.g. A3), the exact code/steps, and what rendered vs expected.
A wrong outline color or a phantom/duplicate shape almost always means another internal
`new V…(...)` that should be a non-registering `…​.Internal(...)` (CLAUDE.md #10) — same class
as the polygon-edge bug.
