using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using C2VGeometry;

namespace Code2Viz.Tests;

/// <summary>
/// Tests for the <see cref="Region(ICurve)"/> constructor and <see cref="Region.AddHole(ICurve)"/>
/// overload that build a region from a single closed curve. Touches Shape.DefaultRegistry, so this
/// lives in the serialized "CanvasState" collection (see CLAUDE.md note 9).
/// </summary>
[Collection("CanvasState")]
public class RegionFromClosedCurveTests : IDisposable
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

    public RegionFromClosedCurveTests() => Shape.DefaultRegistry = _reg;
    public void Dispose() => Shape.DefaultRegistry = null;

    [Fact]
    public void FromCircle_KeepsCurveWhole_AndApproximatesArea()
    {
        var region = new Region(new VCircle(new VXYZ(0, 0), 5));

        Assert.Single(region.OuterLoop);                 // circle kept whole, not decomposed
        Assert.InRange(region.Area, Math.PI * 25 * 0.97, Math.PI * 25);  // 32-gon under-approximation
        Assert.True(region.Contains(new VXYZ(0, 0)));
        Assert.False(region.Contains(new VXYZ(100, 100)));
    }

    [Fact]
    public void Union_CircleCenteredOnRectangleCorner_MergesNonNull()
    {
        // Regression for the reported bug: a circle whose polygon vertices land EXACTLY on the
        // rectangle's edges (circle centered on the rect's corner) made the old Greiner-Hormann
        // clipper find zero crossings and wrongly report the regions as disjoint, so Union returned
        // null. Clipper2 handles this vertex-on-edge degeneracy. Hiding the regions must not matter.
        var r1 = new Region(new VCircle(27.00, 21.22, 15.57));
        var r2 = new Region(new VRectangle(0.00, 0.00, 27.00, 21.22));
        r1.Hide();
        r2.Hide();

        var union = BooleanOps.Union(new List<Region> { r1, r2 });

        Assert.NotNull(union);
        // Inclusion–exclusion: |A∪B| = |A| + |B| − |A∩B|. The shapes genuinely overlap, so the
        // union must strictly exceed the rectangle's area (572.94).
        Assert.True(union!.Area > 572.94,
            $"Union area ({union.Area:F2}) should exceed the rectangle area (overlap is real).");
    }

    [Fact]
    public void FromCircle_ConsumesSourceCurve()
    {
        var circle = new VCircle(new VXYZ(0, 0), 5);
        Assert.Contains(circle, _reg.Shapes);            // auto-registered on construction

        var region = new Region(circle);

        Assert.DoesNotContain(circle, _reg.Shapes);      // consumed — removed from the canvas
        Assert.Contains(region, _reg.Shapes);            // region itself is registered
    }

    [Fact]
    public void FromEllipse_ApproximatesArea()
    {
        var region = new Region(new VEllipse(new VXYZ(0, 0), 6, 3));
        Assert.Single(region.OuterLoop);
        Assert.InRange(region.Area, Math.PI * 18 * 0.97, Math.PI * 18);
    }

    [Fact]
    public void FromPolygon_DecomposesToEdges_WithoutPollution()
    {
        var poly = new VPolygon(new[] { new VXYZ(0, 0), new VXYZ(10, 0), new VXYZ(5, 8) });
        _reg.Shapes.Clear();

        var region = new Region(poly);

        Assert.Equal(3, region.OuterLoop.Count);         // one VLine per vertex
        Assert.All(region.OuterLoop, c => Assert.IsType<VLine>(c));
        Assert.Single(_reg.Shapes);                      // only the region — no edge VLine pollution
        Assert.Same(region, _reg.Shapes[0]);
    }

    [Fact]
    public void FromClosedPolyline_IsAccepted()
    {
        // last point duplicates the first → closed
        var pl = new VPolyline(new VXYZ(0, 0), new VXYZ(10, 0), new VXYZ(5, 8), new VXYZ(0, 0));
        var region = new Region(pl);
        Assert.Equal(3, region.OuterLoop.Count);         // trailing duplicate dropped
    }

    [Fact]
    public void FromOpenPolyline_Throws()
    {
        var pl = new VPolyline(new VXYZ(0, 0), new VXYZ(10, 0), new VXYZ(5, 8));
        Assert.Throws<ArgumentException>(() => new Region(pl));
    }

    [Fact]
    public void FromClosedSpline_IsAccepted()
    {
        var sp = new VSpline(new VXYZ(0, 0), new VXYZ(10, 0), new VXYZ(5, 8), new VXYZ(0, 0));
        var region = new Region(sp);
        Assert.Single(region.OuterLoop);                 // smooth curve kept whole
        Assert.True(region.Area > 0);
    }

    [Fact]
    public void FromOpenSpline_Throws()
    {
        var sp = new VSpline(new VXYZ(0, 0), new VXYZ(10, 0), new VXYZ(5, 8));
        Assert.Throws<ArgumentException>(() => new Region(sp));
    }

    [Fact]
    public void FromNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new Region((ICurve)null!));
    }

    [Fact]
    public void AddHoleFromCircle_SubtractsArea()
    {
        var region = new Region(new VCircle(new VXYZ(0, 0), 10));
        double outerArea = region.Area;

        region.AddHole(new VCircle(new VXYZ(0, 0), 3));

        Assert.Single(region.Holes);
        Assert.True(region.Area < outerArea);
        // hole area subtracted ≈ 32-gon area of r=3 (≈ π·9)
        Assert.Equal(outerArea - Math.PI * 9, region.Area, 1.0);
    }
}
