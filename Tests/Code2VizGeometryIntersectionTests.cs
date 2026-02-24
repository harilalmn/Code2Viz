using System.Collections.Generic;
using Xunit;
using Code2Viz.Geometry; // Using the App namespace, NOT C2VGeometry

namespace Code2Viz.Tests;

public class Code2VizGeometryIntersectionTests
{
    [Fact]
    public void Intersect_TouchingRectangles_ReturnsNoOverlap_AppNamespace()
    {
        var r1 = new VRectangle(new VPoint(0,0), 10, 10);
        var r2 = new VRectangle(new VPoint(10,0), 10, 10);

        var result = CurveIntersection.Intersect(r1, r2);

        Assert.False(result.HasOverlap, "Touching rectangles should not have overlap in Code2Viz.Geometry");
        Assert.True(result.HasIntersection, "Touching rectangles should have intersection in Code2Viz.Geometry");
        Assert.Equal(2, result.Points.Count); 
    }

    [Fact]
    public void Intersect_RoomScenario_ReturnsNoOverlap_AppNamespace()
    {
         // Mimic User's Room logic using Code2Viz.Geometry types
        double w1 = 10, d1 = 12;
        var p1_1 = new VPoint(w1 / 2, d1 / 2);
        var p2_1 = new VPoint(-w1 / 2, d1 / 2);
        var p3_1 = new VPoint(-w1 / 2, -d1 / 2);
        var p4_1 = new VPoint(w1 / 2, -d1 / 2);
        var room1Poly = new VPolygon(new List<VPoint> { p1_1, p2_1, p3_1, p4_1 });

        double w2 = 8, d2 = 12;
        var p1_2 = new VPoint(w2 / 2, d2 / 2);
        var p2_2 = new VPoint(-w2 / 2, d2 / 2);
        var p3_2 = new VPoint(-w2 / 2, -d2 / 2);
        var p4_2 = new VPoint(w2 / 2, -d2 / 2);
        var room2Poly = new VPolygon(new List<VPoint> { p1_2, p2_2, p3_2, p4_2 });

        var from = room2Poly.Points[2]; 
        var to = room1Poly.Points[3];
        var vector = (to.AsVXYZ() - from.AsVXYZ());
        room2Poly.Move(vector);

        var result = CurveIntersection.Intersect(room1Poly, room2Poly);

        Assert.False(result.HasOverlap, $"Room scenario should not overlap in Code2Viz.Geometry. Curves: {result.Curves.Count}");
        Assert.True(result.HasIntersection);
    }
}
