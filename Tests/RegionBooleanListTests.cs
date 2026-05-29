using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using C2VGeometry;

namespace Code2Viz.Tests;

/// <summary>
/// Tests for the collection (List&lt;Region&gt;) overloads of the region boolean operations,
/// on both RegionBooleanOps and the BooleanOps facade. Creates Regions (which can auto-register),
/// so this joins the serialized "CanvasState" collection and clears the registry (see CLAUDE.md note 9).
/// Uses corner-overlapping squares (the clipper-friendly geometry the existing RegionTests use).
/// </summary>
[Collection("CanvasState")]
public class RegionBooleanListTests : IDisposable
{
    public RegionBooleanListTests() => Shape.DefaultRegistry = null;
    public void Dispose() => Shape.DefaultRegistry = null;

    // Axis-aligned square region [x, x+w] × [y, y+h] (straight edges → exact areas).
    private static Region Sq(double x, double y, double w, double h) =>
        new Region(new VPolygon(
            new VXYZ(x, y), new VXYZ(x + w, y), new VXYZ(x + w, y + h), new VXYZ(x, y + h)));

    [Fact]
    public void Union_OfList_MergesOverlappingSquares()
    {
        // [0,4]² ∪ [2,6]² → 16 + 16 − 4 (corner overlap) = 28
        var union = RegionBooleanOps.Union(new List<Region> { Sq(0, 0, 4, 4), Sq(2, 2, 4, 4) });

        Assert.NotNull(union);
        Assert.Equal(28, union!.Area, 0.5);
    }

    [Fact]
    public void BooleanOps_Facade_AcceptsListOfRegions()
    {
        // The exact call shape from the feature request — must compile and run.
        var union = BooleanOps.Union(new List<Region> { Sq(0, 0, 4, 4), Sq(2, 2, 4, 4) });
        Assert.NotNull(union);
        Assert.Equal(28, union!.Area, 0.5);
    }

    [Fact]
    public void Intersect_OfList_IsAreaCommonToAll()
    {
        // [0,6]² ∩ [2,8]² ∩ [4,10]² = [4,6]² = 4
        var list = new List<Region> { Sq(0, 0, 6, 6), Sq(2, 2, 6, 6), Sq(4, 4, 6, 6) };
        var result = RegionBooleanOps.Intersect(list);

        Assert.Single(result);
        Assert.Equal(4, result[0].Area, 0.5);
    }

    [Fact]
    public void Difference_OfList_IsFirstMinusRest()
    {
        // [0,6]² − [2,8]² − [4,10]² = 36 − |A ∩ (B ∪ C)| = 36 − 16 = 20
        var list = new List<Region> { Sq(0, 0, 6, 6), Sq(2, 2, 6, 6), Sq(4, 4, 6, 6) };
        var result = RegionBooleanOps.Difference(list);

        double area = result.Sum(r => r.Area);
        Assert.Equal(20, area, 0.5);
    }

    [Fact]
    public void Xor_OfTwo_IsSymmetricDifference()
    {
        // [0,4]² ⊕ [2,6]² = (A−B) + (B−A) = 12 + 12 = 24
        var result = RegionBooleanOps.Xor(new List<Region> { Sq(0, 0, 4, 4), Sq(2, 2, 4, 4) });
        double area = result.Sum(r => r.Area);
        Assert.Equal(24, area, 0.5);
    }

    [Fact]
    public void ParamsOverloads_Work()
    {
        var union = RegionBooleanOps.Union(Sq(0, 0, 4, 4), Sq(2, 2, 4, 4));
        Assert.NotNull(union);
        Assert.Equal(28, union!.Area, 0.5);

        var inter = RegionBooleanOps.Intersect(Sq(0, 0, 6, 6), Sq(2, 2, 6, 6), Sq(4, 4, 6, 6));
        Assert.Single(inter);
        Assert.Equal(4, inter[0].Area, 0.5);
    }

    [Fact]
    public void Union_OfSingleton_ReturnsClone()
    {
        var union = RegionBooleanOps.Union(new List<Region> { Sq(0, 0, 4, 4) });
        Assert.NotNull(union);
        Assert.Equal(16, union!.Area, 0.5);
    }

    [Fact]
    public void Intersect_OfDisjointList_IsEmpty()
    {
        var result = RegionBooleanOps.Intersect(new List<Region> { Sq(0, 0, 2, 2), Sq(10, 10, 2, 2) });
        Assert.Empty(result);
    }
}
