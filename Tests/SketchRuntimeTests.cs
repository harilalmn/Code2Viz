using System;
using System.Runtime.Loader;
using Xunit;
using Code2Viz.Canvas;
using Code2Viz.Sketching;
using C2V = C2VGeometry;

namespace Code2Viz.Tests;

[Collection("CanvasState")]
public class SketchRuntimeTests : IDisposable
{
    public SketchRuntimeTests()
    {
        // Each test gets a clean slate.
        SketchRuntime.Instance.Stop();
        CanvasRenderer.Instance.Clear();
        C2V.Shape.DefaultRegistry = null;
    }

    public void Dispose()
    {
        SketchRuntime.Instance.Stop();
        CanvasRenderer.Instance.Clear();
        C2V.Shape.DefaultRegistry = null;
    }

    // ── Test fixtures: tiny in-test Sketches we drive directly ────────────────────────────────
    // SketchRuntime.Start normally takes an AssemblyLoadContext from ModuleCompiler.
    // For unit tests we use the AssemblyLoadContext.Default so we don't need to recompile.

    public class MinimalSketch : Sketch
    {
        public int SetupCount;
        public int DrawCount;

        public override void Setup() => SetupCount++;
        public override void Draw() => DrawCount++;
    }

    public class SizingSketch : Sketch
    {
        public override void Setup() => Size(400, 200);
    }

    public class ThrowingSketch : Sketch
    {
        public override void Draw() => throw new InvalidOperationException("boom");
    }

    public class CircleSketch : Sketch
    {
        public override void Draw()
        {
            new C2V.VCircle(new C2V.VXYZ(0, 0), 10);
        }
    }

    // ── Tests ─────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void NotRunning_ByDefault()
    {
        Assert.False(SketchRuntime.Instance.IsRunning);
        Assert.Null(SketchRuntime.Instance.Active);
    }

    [Fact]
    public void Start_InvokesSetup_AndIsRunningGoesTrue()
    {
        SketchRuntime.Instance.Start(typeof(MinimalSketch), AssemblyLoadContext.Default);

        Assert.True(SketchRuntime.Instance.IsRunning);
        var sketch = (MinimalSketch)SketchRuntime.Instance.Active!;
        Assert.Equal(1, sketch.SetupCount);
        Assert.Equal(0, sketch.DrawCount);

        SketchRuntime.Instance.Stop();   // don't unload Default; Stop() handles "no context"
    }

    [Fact]
    public void Tick_IncrementsFrameAndCallsDraw()
    {
        SketchRuntime.Instance.Start(typeof(MinimalSketch), AssemblyLoadContext.Default);
        var sketch = (MinimalSketch)SketchRuntime.Instance.Active!;

        SketchRuntime.Instance.Tick();
        SketchRuntime.Instance.Tick();
        SketchRuntime.Instance.Tick();

        Assert.Equal(3, sketch.DrawCount);
        Assert.Equal(3, sketch.FrameCount);
    }

    [Fact]
    public void Stop_StopsAndClearsActive()
    {
        SketchRuntime.Instance.Start(typeof(MinimalSketch), AssemblyLoadContext.Default);
        SketchRuntime.Instance.Stop();

        Assert.False(SketchRuntime.Instance.IsRunning);
        Assert.Null(SketchRuntime.Instance.Active);
    }

    [Fact]
    public void Size_RequestsZoom_ConsumedOnceThenNullified()
    {
        SketchRuntime.Instance.Start(typeof(SizingSketch), AssemblyLoadContext.Default);

        Assert.True(SketchRuntime.Instance.TryConsumeZoomRequest());
        Assert.False(SketchRuntime.Instance.TryConsumeZoomRequest());

        Assert.Equal(400, SketchRuntime.Instance.Active!.Width, 6);
        Assert.Equal(200, SketchRuntime.Instance.Active!.Height, 6);
    }

    [Fact]
    public void RegistryClearedBetweenFrames_OnlyCurrentFrameOnCanvas()
    {
        SketchRuntime.Instance.Start(typeof(CircleSketch), AssemblyLoadContext.Default);

        // Setup() did not draw anything for CircleSketch — first Draw() happens in Tick().
        SketchRuntime.Instance.Tick();
        var shapesAfter1 = CanvasRenderer.Instance.GetShapes().Count;

        SketchRuntime.Instance.Tick();
        SketchRuntime.Instance.Tick();
        var shapesAfter3 = CanvasRenderer.Instance.GetShapes().Count;

        // Each frame produces the same number of shapes (user circle + boundary rect),
        // proving the registry is drained and the canvas is cleared between frames.
        Assert.Equal(shapesAfter1, shapesAfter3);
        Assert.True(shapesAfter1 >= 1);
    }

    [Fact]
    public void Draw_ThrowingException_StopsTheSketch()
    {
        SketchRuntime.Instance.Start(typeof(ThrowingSketch), AssemblyLoadContext.Default);
        Assert.True(SketchRuntime.Instance.IsRunning);

        SketchRuntime.Instance.Tick();   // user Draw throws

        Assert.False(SketchRuntime.Instance.IsRunning);
    }

    [Fact]
    public void StartTwice_StopsPreviousAndRunsNew()
    {
        SketchRuntime.Instance.Start(typeof(MinimalSketch), AssemblyLoadContext.Default);
        var first = SketchRuntime.Instance.Active;

        SketchRuntime.Instance.Start(typeof(MinimalSketch), AssemblyLoadContext.Default);
        var second = SketchRuntime.Instance.Active;

        Assert.NotSame(first, second);
        Assert.True(SketchRuntime.Instance.IsRunning);
    }

    public class NoLoopSketch : Sketch
    {
        public override void Setup() => NoLoop();
        public override void Draw() => DrawCount++;
        public int DrawCount;
    }

    [Fact]
    public void NoLoop_PausesTickWithoutChangingIsRunning()
    {
        SketchRuntime.Instance.Start(typeof(NoLoopSketch), AssemblyLoadContext.Default);
        var sketch = (NoLoopSketch)SketchRuntime.Instance.Active!;

        SketchRuntime.Instance.Tick();
        SketchRuntime.Instance.Tick();

        Assert.Equal(0, sketch.DrawCount);   // ticks dropped while not looping
        Assert.True(SketchRuntime.Instance.IsRunning);
    }
}
