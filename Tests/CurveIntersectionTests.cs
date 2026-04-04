using System.Collections.Generic;
using Xunit;
using C2VGeometry;

namespace Code2Viz.Tests;

public class CurveIntersectionTests
{
    [Fact]
    public void Intersect_TouchingRectangles_ReturnsNoOverlap()
    {
        var r1 = new VRectangle(new VXYZ(0,0), 10, 10);
        var r2 = new VRectangle(new VXYZ(10,0), 10, 10);

        var result = CurveIntersection.Intersect(r1, r2);

        Assert.False(result.HasOverlap, "Touching rectangles should not have overlap");
        Assert.True(result.HasIntersection, "Touching rectangles should have intersection");
        // Expect 2 points (corners of the shared edge)
        Assert.Equal(2, result.Points.Count);
    }

    [Fact]
    public void Intersect_RoomScenario_ReturnsNoOverlap()
    {
        // Mimic User's Room logic
        // Room 1: 10x12
        double w1 = 10, d1 = 12;
        var p1_1 = new VXYZ(w1 / 2, d1 / 2);
        var p2_1 = new VXYZ(-w1 / 2, d1 / 2);
        var p3_1 = new VXYZ(-w1 / 2, -d1 / 2);
        var p4_1 = new VXYZ(w1 / 2, -d1 / 2);
        var room1Poly = new VPolygon(new List<VXYZ> { p1_1, p2_1, p3_1, p4_1 });

        // Room 2: 8x12
        double w2 = 8, d2 = 12;
        var p1_2 = new VXYZ(w2 / 2, d2 / 2);
        var p2_2 = new VXYZ(-w2 / 2, d2 / 2);
        var p3_2 = new VXYZ(-w2 / 2, -d2 / 2);
        var p4_2 = new VXYZ(w2 / 2, -d2 / 2);
        var room2Poly = new VPolygon(new List<VXYZ> { p1_2, p2_2, p3_2, p4_2 });

        // Move Room 2 so its Points[2] aligns with Room 1 Points[3]
        // Points[2] is p3 (BL), Points[3] is p4 (BR)
        var from = room2Poly.Points[2];
        var to = room1Poly.Points[3];
        var vector = (to - from);
        room2Poly.Move(vector);

        var result = CurveIntersection.Intersect(room1Poly, room2Poly);

        Assert.False(result.HasOverlap, $"Room scenario should not overlap. Curves: {result.Curves.Count}");
        Assert.True(result.HasIntersection);
    }
}
