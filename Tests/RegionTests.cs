using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using C2VGeometry;

namespace Code2Viz.Tests;

public class RegionTests
{
    #region Helpers

    /// <summary>
    /// Creates a rectangular region from lines.
    /// </summary>
    private static Region MakeRectRegion(double x, double y, double w, double h)
    {
        var p0 = new VXYZ(x, y);
        var p1 = new VXYZ(x + w, y);
        var p2 = new VXYZ(x + w, y + h);
        var p3 = new VXYZ(x, y + h);

        var curves = new List<ICurve>
        {
            new VLine(p0, p1),
            new VLine(p1, p2),
            new VLine(p2, p3),
            new VLine(p3, p0)
        };

        return new Region(curves);
    }

    #endregion

    #region Construction Tests

    [Fact]
    public void Constructor_RectangleFromLines_CreatesRegionWithFourCurves()
    {
        var region = MakeRectRegion(0, 0, 4, 3);

        Assert.Equal(4, region.OuterLoop.Count);
        Assert.Empty(region.Holes);
    }

    [Fact]
    public void Constructor_TriangleFromLines_CreatesRegionWithThreeCurves()
    {
        var p0 = new VXYZ(0, 0);
        var p1 = new VXYZ(4, 0);
        var p2 = new VXYZ(2, 3);

        var curves = new List<ICurve>
        {
            new VLine(p0, p1),
            new VLine(p1, p2),
            new VLine(p2, p0)
        };

        var region = new Region(curves);

        Assert.Equal(3, region.OuterLoop.Count);
    }

    [Fact]
    public void Constructor_MixedLinesAndArc_CreatesRegion()
    {
        // Create a D-shape: straight left side + arc on the right
        var p0 = new VXYZ(0, 0);
        var p1 = new VXYZ(0, 4);

        // Arc from p1 to p0 (right side)
        var arc = VArc.FromStartEndRadius(p1, p0, 3, false);

        var curves = new List<ICurve>
        {
            new VLine(p0, p1),
            arc
        };

        var region = new Region(curves);

        Assert.Equal(2, region.OuterLoop.Count);
    }

    [Fact]
    public void Constructor_UnorderedCurves_OrdersAutomatically()
    {
        var p0 = new VXYZ(0, 0);
        var p1 = new VXYZ(4, 0);
        var p2 = new VXYZ(4, 3);
        var p3 = new VXYZ(0, 3);

        // Provide curves out of order
        var curves = new List<ICurve>
        {
            new VLine(p2, p3),
            new VLine(p0, p1),
            new VLine(p3, p0),
            new VLine(p1, p2)
        };

        var region = new Region(curves);

        Assert.Equal(4, region.OuterLoop.Count);
    }

    [Fact]
    public void Constructor_DisconnectedCurves_ThrowsArgumentException()
    {
        var curves = new List<ICurve>
        {
            new VLine(new VXYZ(0, 0), new VXYZ(1, 0)),
            new VLine(new VXYZ(5, 5), new VXYZ(6, 5))
        };

        Assert.Throws<ArgumentException>(() => new Region(curves));
    }

    [Fact]
    public void Constructor_ClosedCurve_ThrowsArgumentException()
    {
        // A full circle is a closed curve — not accepted
        var circle = new VCircle(new VXYZ(0, 0), 5);
        var curves = new List<ICurve> { circle };

        Assert.Throws<ArgumentException>(() => new Region(curves));
    }

    [Fact]
    public void Constructor_SingleCurve_ThrowsArgumentException()
    {
        // Need at least 2 curves to form a loop
        var curves = new List<ICurve>
        {
            new VLine(new VXYZ(0, 0), new VXYZ(4, 0))
        };

        Assert.Throws<ArgumentException>(() => new Region(curves));
    }

    #endregion

    #region Area Tests

    [Fact]
    public void Area_Rectangle_ReturnsCorrectArea()
    {
        var region = MakeRectRegion(0, 0, 4, 3);

        Assert.Equal(12.0, region.Area, precision: 1);
    }

    [Fact]
    public void Area_RectangleWithHole_SubtractsHoleArea()
    {
        var outer = MakeRectRegion(0, 0, 10, 10);

        // Add a 2x2 hole inside
        var p0 = new VXYZ(3, 3);
        var p1 = new VXYZ(5, 3);
        var p2 = new VXYZ(5, 5);
        var p3 = new VXYZ(3, 5);
        var holeCurves = new List<ICurve>
        {
            new VLine(p0, p1),
            new VLine(p1, p2),
            new VLine(p2, p3),
            new VLine(p3, p0)
        };

        outer.AddHole(holeCurves);

        // 100 - 4 = 96
        Assert.Equal(96.0, outer.Area, precision: 1);
    }

    #endregion

    #region Containment Tests

    [Fact]
    public void Contains_PointInsideRectangle_ReturnsTrue()
    {
        var region = MakeRectRegion(0, 0, 4, 3);

        Assert.True(region.Contains(new VXYZ(2, 1.5)));
    }

    [Fact]
    public void Contains_PointOutsideRectangle_ReturnsFalse()
    {
        var region = MakeRectRegion(0, 0, 4, 3);

        Assert.False(region.Contains(new VXYZ(5, 5)));
    }

    [Fact]
    public void Contains_PointInsideHole_ReturnsFalse()
    {
        var outer = MakeRectRegion(0, 0, 10, 10);

        var p0 = new VXYZ(3, 3);
        var p1 = new VXYZ(7, 3);
        var p2 = new VXYZ(7, 7);
        var p3 = new VXYZ(3, 7);
        outer.AddHole(new List<ICurve>
        {
            new VLine(p0, p1),
            new VLine(p1, p2),
            new VLine(p2, p3),
            new VLine(p3, p0)
        });

        // Point in hole
        Assert.False(outer.Contains(new VXYZ(5, 5)));
        // Point in outer but outside hole
        Assert.True(outer.Contains(new VXYZ(1, 1)));
    }

    #endregion

    #region Conversion Tests

    [Fact]
    public void ToPolygon_Rectangle_ReturnsFourPointPolygon()
    {
        var region = MakeRectRegion(0, 0, 4, 3);

        var poly = region.ToPolygon();

        Assert.Equal(4, poly.Points.Count);
        Assert.Equal(12.0, poly.Area, precision: 1);
    }

    [Fact]
    public void FromPolygon_RoundTrips_SameArea()
    {
        var originalPoly = new VPolygon(
            new VXYZ(0, 0),
            new VXYZ(4, 0),
            new VXYZ(4, 3),
            new VXYZ(0, 3)
        );

        var region = Region.FromPolygon(originalPoly);
        var resultPoly = region.ToPolygon();

        Assert.Equal(originalPoly.Area, resultPoly.Area, precision: 1);
    }

    #endregion

    #region Transform Tests

    [Fact]
    public void Clone_ReturnsIndependentCopy()
    {
        var region = MakeRectRegion(0, 0, 4, 3);
        var clone = (Region)region.Clone();

        clone.Move(new VXYZ(10, 10, 0));

        // Original should be unchanged
        Assert.Equal(12.0, region.Area, precision: 1);
        Assert.Equal(12.0, clone.Area, precision: 1);
    }

    [Fact]
    public void Move_ShiftsRegion()
    {
        var region = MakeRectRegion(0, 0, 4, 3);
        region.Move(new VXYZ(5, 5, 0));

        // Area should not change
        Assert.Equal(12.0, region.Area, precision: 1);

        // Center should be at (7, 6.5) roughly
        Assert.True(region.Contains(new VXYZ(7, 6.5)));
        Assert.False(region.Contains(new VXYZ(0, 0)));
    }

    #endregion

    #region Boolean Operation Tests

    [Fact]
    public void Union_OverlappingRectangles_ReturnsMergedRegion()
    {
        var a = MakeRectRegion(0, 0, 4, 4);
        var b = MakeRectRegion(2, 2, 4, 4);

        var result = RegionBooleanOps.Union(a, b);

        Assert.NotNull(result);
        // 16 + 16 - 4 = 28
        Assert.Equal(28.0, result.Area, precision: 0);
    }

    [Fact]
    public void Intersect_OverlappingRectangles_ReturnsOverlapRegion()
    {
        var a = MakeRectRegion(0, 0, 4, 4);
        var b = MakeRectRegion(2, 2, 4, 4);

        var result = RegionBooleanOps.Intersect(a, b);

        Assert.NotEmpty(result);
        double totalArea = result.Sum(r => r.Area);
        // Overlap is 2x2 = 4
        Assert.Equal(4.0, totalArea, precision: 0);
    }

    [Fact]
    public void Difference_OverlappingRectangles_RemovesOverlap()
    {
        var a = MakeRectRegion(0, 0, 4, 4);
        var b = MakeRectRegion(2, 2, 4, 4);

        var result = RegionBooleanOps.Difference(a, b);

        Assert.NotEmpty(result);
        double totalArea = result.Sum(r => r.Area);
        // 16 - 4 = 12
        Assert.Equal(12.0, totalArea, precision: 0);
    }

    [Fact]
    public void Intersect_NoOverlap_ReturnsEmpty()
    {
        var a = MakeRectRegion(0, 0, 2, 2);
        var b = MakeRectRegion(5, 5, 2, 2);

        var result = RegionBooleanOps.Intersect(a, b);

        Assert.Empty(result);
    }

    [Fact]
    public void BooleanOps_AreaInvariant_UnionPlusIntersectEqualsSumOfAreas()
    {
        var a = MakeRectRegion(0, 0, 4, 4);
        var b = MakeRectRegion(2, 2, 4, 4);

        var union = RegionBooleanOps.Union(a, b);
        var intersection = RegionBooleanOps.Intersect(a, b);

        Assert.NotNull(union);
        Assert.NotEmpty(intersection);

        double areaA = a.Area;
        double areaB = b.Area;
        double unionArea = union.Area;
        double intersectArea = intersection.Sum(r => r.Area);

        // |A| + |B| - |A ∩ B| = |A ∪ B|
        double expected = areaA + areaB - intersectArea;
        Assert.Equal(expected, unionArea, precision: 0);
    }

    [Fact]
    public void ExtensionMethod_Union_Works()
    {
        var a = MakeRectRegion(0, 0, 4, 4);
        var b = MakeRectRegion(2, 2, 4, 4);

        var result = a.Union(b);

        Assert.NotNull(result);
        Assert.Equal(28.0, result!.Area, precision: 0);
    }

    #endregion

    #region ToString

    [Fact]
    public void ToString_ContainsCurveCount()
    {
        var region = MakeRectRegion(0, 0, 4, 3);

        string str = region.ToString();

        Assert.Contains("4 curves", str);
        Assert.Contains("Holes: 0", str);
    }

    #endregion
}
