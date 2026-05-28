using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using C2VGeometry;

namespace Code2Viz.Tests;

/// <summary>
/// Chart helper produces a single registered VGroup; the many child shapes it builds
/// internally (axes, ticks, labels, bars, ...) must NOT register individually, or the
/// canvas would contain hundreds of orphan shapes per chart.
/// </summary>
[Collection("CanvasState")]
public class ChartTests : IDisposable
{
    private sealed class CountingRegistry : IShapeRegistry
    {
        public readonly List<Shape> Shapes = new();
        public void Register(Shape s) => Shapes.Add(s);
        public void Unregister(Shape s) => Shapes.Remove(s);
        public void MoveAbove(Shape s, Shape r) { }
        public void MoveBehind(Shape s, Shape r) { }
    }

    private readonly CountingRegistry _reg = new();
    private readonly bool _prevAuto;

    public ChartTests()
    {
        _prevAuto = Shape.AutoRegister;
        Shape.AutoRegister = true;
        Shape.DefaultRegistry = _reg;
    }

    public void Dispose()
    {
        Shape.DefaultRegistry = null;
        Shape.AutoRegister = _prevAuto;
    }

    [Fact]
    public void Bar_RegistersOnlyTheGroup()
    {
        _reg.Shapes.Clear();
        var grp = Chart.Bar(new[] { "A", "B", "C" }, new[] { 10.0, 20, 15 });
        Assert.Single(_reg.Shapes);
        Assert.Same(grp, _reg.Shapes[0]);
        Assert.True(grp.Shapes.Count > 0);
    }

    [Fact]
    public void Line_RegistersOnlyTheGroup()
    {
        _reg.Shapes.Clear();
        var grp = Chart.Line(new[] { 0.0, 1, 2, 3 }, new[] { 5.0, 8, 6, 12 });
        Assert.Single(_reg.Shapes);
        Assert.Same(grp, _reg.Shapes[0]);
    }

    [Fact]
    public void Scatter_RegistersOnlyTheGroup()
    {
        _reg.Shapes.Clear();
        var grp = Chart.Scatter(new[] { new VXYZ(1, 2), new VXYZ(3, 4), new VXYZ(5, 6) });
        Assert.Single(_reg.Shapes);
        Assert.Same(grp, _reg.Shapes[0]);
    }

    [Fact]
    public void Pie_RegistersOnlyTheGroup()
    {
        _reg.Shapes.Clear();
        var grp = Chart.Pie(new[] { 30.0, 50, 20 }, new[] { "A", "B", "C" });
        Assert.Single(_reg.Shapes);
        Assert.Same(grp, _reg.Shapes[0]);
        // 3 slices, each at least a VPolygon, plus 3 labels
        Assert.True(grp.Shapes.Count >= 6);
    }

    [Fact]
    public void Area_RegistersOnlyTheGroup()
    {
        _reg.Shapes.Clear();
        var grp = Chart.Area(new[] { 0.0, 1, 2, 3 }, new[] { 5.0, 8, 6, 12 });
        Assert.Single(_reg.Shapes);
        Assert.Same(grp, _reg.Shapes[0]);
    }

    [Fact]
    public void RestoresAutoRegisterStateOnExit()
    {
        Shape.AutoRegister = false;
        try
        {
            _ = Chart.Bar(new[] { "X" }, new[] { 1.0 });
            Assert.False(Shape.AutoRegister); // not flipped to true
        }
        finally
        {
            Shape.AutoRegister = true;
        }

        Shape.AutoRegister = true;
        _ = Chart.Bar(new[] { "X" }, new[] { 1.0 });
        Assert.True(Shape.AutoRegister); // not flipped to false
    }

    [Fact]
    public void Bar_DoesNotRegister_WhenAutoRegisterOff()
    {
        _reg.Shapes.Clear();
        Shape.AutoRegister = false;
        try
        {
            var grp = Chart.Bar(new[] { "A", "B" }, new[] { 1.0, 2.0 });
            Assert.NotNull(grp);
            Assert.Empty(_reg.Shapes);
        }
        finally
        {
            Shape.AutoRegister = true;
        }
    }

    [Fact]
    public void Bar_LengthMismatch_Throws()
    {
        Assert.Throws<ArgumentException>(() => Chart.Bar(new[] { "A", "B" }, new[] { 1.0 }));
    }

    [Fact]
    public void Bar_AppliesOptions_OriginAndSize()
    {
        var opts = new ChartOptions { Origin = new VXYZ(100, 50), Width = 300, Height = 200 };
        var grp = Chart.Bar(new[] { "A", "B" }, new[] { 10.0, 20 }, opts);
        var b = grp.GetBounds();
        // Plot frame should sit roughly within the requested rect (allow padding for labels/title)
        Assert.True(b.Min.X < 100); // labels extend left of origin
        Assert.True(b.Max.X > 100 + 300 - 5);
        Assert.True(b.Max.Y > 50 + 200 - 5);
    }
}
