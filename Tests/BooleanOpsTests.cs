using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using C2VGeometry;

namespace Code2Viz.Tests;

public class BooleanOpsTests
{
    private static VPolygon MakePoly(params (double x, double y)[] coords)
    {
        var points = coords.Select(c => new VXYZ(c.x, c.y)).ToList();
        return new VPolygon(points);
    }

    private static double PolyArea(VPolygon p) => Math.Abs(p.SignedArea);

    #region Union Tests

    [Fact]
    public void Union_TwoOverlappingCCWSquares_ReturnsMergedPolygon()
    {
        // Two 4x4 squares overlapping at corner (2x2 overlap)
        var a = MakePoly((0, 0), (4, 0), (4, 4), (0, 4));   // CCW
        var b = MakePoly((2, 2), (6, 2), (6, 6), (2, 6));   // CCW

        var result = BooleanOps.Union(a, b);

        Assert.NotNull(result);
        // Union area = 16 + 16 - 4 (overlap) = 28
        Assert.Equal(28.0, PolyArea(result), precision: 1);
    }

    [Fact]
    public void Union_TwoOverlappingMixedWinding_ReturnsMergedPolygon()
    {
        // CCW square
        var a = MakePoly((0, 0), (4, 0), (4, 4), (0, 4));
        // CW square (reversed winding)
        var b = MakePoly((2, 6), (6, 6), (6, 2), (2, 2));

        var result = BooleanOps.Union(a, b);

        Assert.NotNull(result);
        Assert.Equal(28.0, PolyArea(result), precision: 1);
    }

    [Fact]
    public void Union_AreaIsLargerThanEitherInput()
    {
        var a = MakePoly((0, 0), (4, 0), (4, 4), (0, 4));
        var b = MakePoly((2, 2), (6, 2), (6, 6), (2, 6));

        var result = BooleanOps.Union(a, b);

        Assert.NotNull(result);
        double areaA = PolyArea(a);
        double areaB = PolyArea(b);
        double resultArea = PolyArea(result);
        // Union must be >= each individual polygon
        Assert.True(resultArea >= areaA - 0.01);
        Assert.True(resultArea >= areaB - 0.01);
        // Union area = 16 + 16 - 4 (2x2 overlap) = 28
        Assert.Equal(28.0, resultArea, precision: 1);
    }

    [Fact]
    public void Union_ContainedPolygon_ReturnsOuter()
    {
        var outer = MakePoly((0, 0), (10, 0), (10, 10), (0, 10));
        var inner = MakePoly((2, 2), (8, 2), (8, 8), (2, 8));

        var result = BooleanOps.Union(outer, inner);

        Assert.NotNull(result);
        // Result should equal the outer polygon area
        Assert.Equal(100.0, PolyArea(result), precision: 1);
    }

    [Fact]
    public void Union_NonOverlapping_ReturnsNull()
    {
        var a = MakePoly((0, 0), (2, 0), (2, 2), (0, 2));
        var b = MakePoly((5, 5), (7, 5), (7, 7), (5, 7));

        // Non-overlapping polygons can't form a single polygon
        var result = BooleanOps.Union(a, b);

        Assert.Null(result);
    }

    [Fact]
    public void Union_ListOverload_ChainsCorrectly()
    {
        var a = MakePoly((0, 0), (4, 0), (4, 4), (0, 4));
        var b = MakePoly((2, 2), (6, 2), (6, 6), (2, 6));

        var result = BooleanOps.Union(new List<VPolygon> { a, b });

        Assert.NotNull(result);
        Assert.Equal(28.0, PolyArea(result), precision: 1);
    }

    [Fact]
    public void Union_BothCW_ReturnsMergedPolygon()
    {
        // Both CW (reversed from standard CCW)
        var a = MakePoly((0, 4), (4, 4), (4, 0), (0, 0));
        var b = MakePoly((2, 6), (6, 6), (6, 2), (2, 2));

        var result = BooleanOps.Union(a, b);

        Assert.NotNull(result);
        Assert.Equal(28.0, PolyArea(result), precision: 1);
    }

    [Fact]
    public void Union_LargeOverlap_ReturnsMergedPolygon()
    {
        // 6x4 rectangles overlapping in middle (4x2 overlap area)
        var a = MakePoly((0, 0), (6, 0), (6, 4), (0, 4));   // area 24
        var b = MakePoly((2, 1), (8, 1), (8, 5), (2, 5));   // area 24

        var result = BooleanOps.Union(a, b);

        Assert.NotNull(result);
        // Overlap: x from 2 to 6, y from 1 to 4, area = 4*3 = 12
        // Union = 24 + 24 - 12 = 36
        Assert.Equal(36.0, PolyArea(result), precision: 1);
    }

    [Fact]
    public void Union_RectanglesSharingFullCollinearEdge_MergeIntoOne()
    {
        // Regression: the old hand-rolled clipper mis-unioned two rectangles that shared a full
        // collinear edge band, returning a single rectangle's area (see CLAUDE.md note 31).
        // [0,4]×[0,4] and [4,0]×[8,4] share the entire edge x=4, y∈[0,4].
        var a = MakePoly((0, 0), (4, 0), (4, 4), (0, 4));   // area 16
        var b = MakePoly((4, 0), (8, 0), (8, 4), (4, 4));   // area 16, shares full edge x=4

        var result = BooleanOps.Union(a, b);

        Assert.NotNull(result);
        // Merged into one 8×4 rectangle = 32 (NOT 16).
        Assert.Equal(32.0, PolyArea(result), precision: 1);
    }

    #endregion

    #region Difference Tests

    [Fact]
    public void Difference_OverlappingSquares_RemovesOverlap()
    {
        var a = MakePoly((0, 0), (4, 0), (4, 4), (0, 4));
        var b = MakePoly((2, 2), (6, 2), (6, 6), (2, 6));

        var result = BooleanOps.Difference(a, b);

        Assert.NotEmpty(result);
        double totalArea = result.Sum(p => PolyArea(p));
        // a - b = area of a minus the 2x2 overlap = 16 - 4 = 12
        Assert.Equal(12.0, totalArea, precision: 1);
    }

    [Fact]
    public void Difference_MixedWinding_RemovesOverlap()
    {
        var a = MakePoly((0, 0), (4, 0), (4, 4), (0, 4));   // CCW
        var b = MakePoly((2, 6), (6, 6), (6, 2), (2, 2));   // CW

        var result = BooleanOps.Difference(a, b);

        Assert.NotEmpty(result);
        double totalArea = result.Sum(p => PolyArea(p));
        // Same overlap as CCW case: 16 - 4 = 12
        Assert.Equal(12.0, totalArea, precision: 1);
    }

    [Fact]
    public void Difference_ContainedSubject_ReturnsEmpty()
    {
        var inner = MakePoly((2, 2), (8, 2), (8, 8), (2, 8));
        var outer = MakePoly((0, 0), (10, 0), (10, 10), (0, 10));

        var result = BooleanOps.Difference(inner, outer);

        Assert.Empty(result);
    }

    [Fact]
    public void Difference_NoOverlap_ReturnsSubject()
    {
        var a = MakePoly((0, 0), (2, 0), (2, 2), (0, 2));
        var b = MakePoly((5, 5), (7, 5), (7, 7), (5, 7));

        var result = BooleanOps.Difference(a, b);

        Assert.Single(result);
        Assert.Equal(4.0, PolyArea(result[0]), precision: 1);
    }

    #endregion

    #region Intersection Tests

    [Fact]
    public void Intersect_OverlappingSquares_ReturnsOverlapRegion()
    {
        var a = MakePoly((0, 0), (4, 0), (4, 4), (0, 4));
        var b = MakePoly((2, 2), (6, 2), (6, 6), (2, 6));

        var result = BooleanOps.Intersect(a, b);

        Assert.NotEmpty(result);
        double totalArea = result.Sum(p => PolyArea(p));
        // Overlap is 2x2 = 4
        Assert.Equal(4.0, totalArea, precision: 1);
    }

    [Fact]
    public void Intersect_MixedWinding_ReturnsOverlapRegion()
    {
        var a = MakePoly((0, 0), (4, 0), (4, 4), (0, 4));   // CCW
        var b = MakePoly((2, 6), (6, 6), (6, 2), (2, 2));   // CW

        var result = BooleanOps.Intersect(a, b);

        Assert.NotEmpty(result);
        double totalArea = result.Sum(p => PolyArea(p));
        Assert.Equal(4.0, totalArea, precision: 1);
    }

    [Fact]
    public void Intersect_NoOverlap_ReturnsEmpty()
    {
        var a = MakePoly((0, 0), (2, 0), (2, 2), (0, 2));
        var b = MakePoly((5, 5), (7, 5), (7, 7), (5, 7));

        var result = BooleanOps.Intersect(a, b);

        Assert.Empty(result);
    }

    #endregion

    #region Consistency Tests

    [Fact]
    public void Union_IsNotDifference_AreaIsLarger()
    {
        // The original bug: Union was producing Difference results
        var a = MakePoly((0, 0), (4, 0), (4, 4), (0, 4));
        var b = MakePoly((2, 2), (6, 2), (6, 6), (2, 6));

        var union = BooleanOps.Union(a, b);
        var diff = BooleanOps.Difference(a, b);

        Assert.NotNull(union);
        Assert.NotEmpty(diff);

        double unionArea = PolyArea(union);
        double diffArea = diff.Sum(p => PolyArea(p));

        // Union area (28) must be much larger than Difference area (12)
        Assert.True(unionArea > diffArea * 2,
            $"Union area ({unionArea}) should be much larger than Difference area ({diffArea})");
    }

    [Fact]
    public void BooleanOps_AreaInvariant_UnionPlusIntersectEqualsSumOfAreas()
    {
        // |A ∪ B| = |A| + |B| - |A ∩ B|
        var a = MakePoly((0, 0), (4, 0), (4, 4), (0, 4));
        var b = MakePoly((2, 2), (6, 2), (6, 6), (2, 6));

        var union = BooleanOps.Union(a, b);
        var intersection = BooleanOps.Intersect(a, b);

        Assert.NotNull(union);
        Assert.NotEmpty(intersection);

        double areaA = PolyArea(a);
        double areaB = PolyArea(b);
        double unionArea = PolyArea(union);
        double intersectArea = intersection.Sum(p => PolyArea(p));

        // |A| + |B| - |A ∩ B| should equal |A ∪ B|
        double expected = areaA + areaB - intersectArea;
        Assert.Equal(expected, unionArea, precision: 1);
    }

    [Fact]
    public void BooleanOps_DifferencePlusIntersectEqualsOriginal()
    {
        // |A - B| + |A ∩ B| = |A|
        var a = MakePoly((0, 0), (4, 0), (4, 4), (0, 4));
        var b = MakePoly((2, 2), (6, 2), (6, 6), (2, 6));

        var diff = BooleanOps.Difference(a, b);
        var intersection = BooleanOps.Intersect(a, b);

        Assert.NotEmpty(diff);
        Assert.NotEmpty(intersection);

        double areaA = PolyArea(a);
        double diffArea = diff.Sum(p => PolyArea(p));
        double intersectArea = intersection.Sum(p => PolyArea(p));

        Assert.Equal(areaA, diffArea + intersectArea, precision: 1);
    }

    #endregion

    #region Holes

    [Fact]
    public void DifferenceWithHoles_ContainedClipInsideSubject_ProducesHole()
    {
        // A big square minus a smaller fully-contained square is a donut: one outer with one hole.
        var big = MakePoly((0, 0), (10, 0), (10, 10), (0, 10));   // area 100
        var small = MakePoly((3, 3), (7, 3), (7, 7), (3, 7));     // area 16, fully inside

        var result = BooleanOps.DifferenceWithHoles(big, small);

        Assert.Single(result);
        Assert.Single(result[0].Holes);
        Assert.Equal(84.0, result[0].Area, precision: 1);   // 100 - 16
    }

    #endregion
}
