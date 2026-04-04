using System.Collections.Generic;
using Xunit;
using AppGeo = Code2Viz.Geometry;
using CoreGeo = C2VGeometry;

namespace Code2Viz.Tests;

public class GeometryParityTests
{
    private static AppGeo.Region MakeAppRectRegion(double x, double y, double w, double h)
    {
        var p0 = new AppGeo.VPoint(x, y);
        var p1 = new AppGeo.VPoint(x + w, y);
        var p2 = new AppGeo.VPoint(x + w, y + h);
        var p3 = new AppGeo.VPoint(x, y + h);

        var curves = new List<AppGeo.ICurve>
        {
            new AppGeo.VLine(p0, p1),
            new AppGeo.VLine(p1, p2),
            new AppGeo.VLine(p2, p3),
            new AppGeo.VLine(p3, p0)
        };

        return new AppGeo.Region(curves);
    }

    private static CoreGeo.Region MakeCoreRectRegion(double x, double y, double w, double h)
    {
        var p0 = new CoreGeo.VXYZ(x, y);
        var p1 = new CoreGeo.VXYZ(x + w, y);
        var p2 = new CoreGeo.VXYZ(x + w, y + h);
        var p3 = new CoreGeo.VXYZ(x, y + h);

        var curves = new List<CoreGeo.ICurve>
        {
            new CoreGeo.VLine(p0, p1),
            new CoreGeo.VLine(p1, p2),
            new CoreGeo.VLine(p2, p3),
            new CoreGeo.VLine(p3, p0)
        };

        return new CoreGeo.Region(curves);
    }

    [Fact]
    public void CircleAreaAndCircumference_MatchBetweenLibraries()
    {
        var appCircle = new AppGeo.VCircle(0, 0, 12.5);
        var coreCircle = new CoreGeo.VCircle(new CoreGeo.VXYZ(0, 0), 12.5);

        Assert.Equal(coreCircle.Area, appCircle.Area, precision: 6);
        Assert.Equal(coreCircle.Circumference, appCircle.Circumference, precision: 6);
    }

    [Fact]
    public void RegionMoveAndContainment_MatchBetweenLibraries()
    {
        var appRegion = MakeAppRectRegion(0, 0, 4, 3);
        var coreRegion = MakeCoreRectRegion(0, 0, 4, 3);

        var move = new CoreGeo.VXYZ(5, 5, 0);
        appRegion.Move(new AppGeo.VXYZ(move.X, move.Y, move.Z));
        coreRegion.Move(move);

        Assert.Equal(coreRegion.Area, appRegion.Area, precision: 6);

        var appInside = appRegion.Contains(new AppGeo.VPoint(7, 6.5));
        var coreInside = coreRegion.Contains(new CoreGeo.VXYZ(7, 6.5));
        Assert.Equal(coreInside, appInside);
        Assert.True(appInside);

        var appOutside = appRegion.Contains(new AppGeo.VPoint(0, 0));
        var coreOutside = coreRegion.Contains(new CoreGeo.VXYZ(0, 0));
        Assert.Equal(coreOutside, appOutside);
        Assert.False(appOutside);
    }

    [Fact]
    public void CurveIntersectionTouchingRectangles_MatchBetweenLibraries()
    {
        var appR1 = new AppGeo.VRectangle(new AppGeo.VPoint(0, 0), 10, 10);
        var appR2 = new AppGeo.VRectangle(new AppGeo.VPoint(10, 0), 10, 10);
        var appResult = AppGeo.CurveIntersection.Intersect(appR1, appR2);

        var coreR1 = new CoreGeo.VRectangle(new CoreGeo.VXYZ(0, 0), 10, 10);
        var coreR2 = new CoreGeo.VRectangle(new CoreGeo.VXYZ(10, 0), 10, 10);
        var coreResult = CoreGeo.CurveIntersection.Intersect(coreR1, coreR2);

        Assert.Equal(coreResult.HasIntersection, appResult.HasIntersection);
        Assert.Equal(coreResult.HasOverlap, appResult.HasOverlap);
        Assert.Equal(coreResult.Points.Count, appResult.Points.Count);
    }

    [Fact]
    public void PolygonUnionArea_MatchBetweenLibraries()
    {
        var appA = new AppGeo.VPolygon(
            new AppGeo.VPoint(0, 0),
            new AppGeo.VPoint(4, 0),
            new AppGeo.VPoint(4, 4),
            new AppGeo.VPoint(0, 4));
        var appB = new AppGeo.VPolygon(
            new AppGeo.VPoint(2, 2),
            new AppGeo.VPoint(6, 2),
            new AppGeo.VPoint(6, 6),
            new AppGeo.VPoint(2, 6));

        var coreA = new CoreGeo.VPolygon(
            new CoreGeo.VXYZ(0, 0),
            new CoreGeo.VXYZ(4, 0),
            new CoreGeo.VXYZ(4, 4),
            new CoreGeo.VXYZ(0, 4));
        var coreB = new CoreGeo.VPolygon(
            new CoreGeo.VXYZ(2, 2),
            new CoreGeo.VXYZ(6, 2),
            new CoreGeo.VXYZ(6, 6),
            new CoreGeo.VXYZ(2, 6));

        var appUnion = AppGeo.BooleanOps.Union(appA, appB);
        var coreUnion = CoreGeo.BooleanOps.Union(coreA, coreB);

        Assert.NotNull(appUnion);
        Assert.NotNull(coreUnion);

        var appArea = System.Math.Abs(appUnion!.SignedArea);
        var coreArea = System.Math.Abs(coreUnion!.SignedArea);
        Assert.Equal(coreArea, appArea, precision: 6);
    }
}
