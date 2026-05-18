using System;
using Xunit;
using Code2Viz.Canvas;
using Code2Viz.Geometry;

namespace Code2Viz.Tests;

// Touches the singleton CanvasRenderer because most shapes auto-register.
// See RayCasterTests for the collection rationale.
[Collection("CanvasState")]
public class SetBoundsTests : IDisposable
{
    public SetBoundsTests()
    {
        Shape.AutoRegister = true;
        CanvasRenderer.Instance.Clear();
    }

    public void Dispose()
    {
        CanvasRenderer.Instance.Clear();
    }

    // --- VLine ---

    [Fact]
    public void VLine_SetBounds_TrimsToSubrange()
    {
        var line = new VLine(0, 0, 10, 0);
        line.SetBounds(0.25, 0.75);

        Assert.Equal(2.5, line.Start.X, 6);
        Assert.Equal(0.0, line.Start.Y, 6);
        Assert.Equal(7.5, line.End.X, 6);
        Assert.Equal(0.0, line.End.Y, 6);
        Assert.Equal(5.0, line.GetLength(), 6);
    }

    [Fact]
    public void VLine_SetBounds_FullRange_IsIdentity()
    {
        var line = new VLine(1, 2, 7, 8);
        line.SetBounds(0, 1);

        Assert.Equal(1.0, line.Start.X, 6);
        Assert.Equal(2.0, line.Start.Y, 6);
        Assert.Equal(7.0, line.End.X, 6);
        Assert.Equal(8.0, line.End.Y, 6);
    }

    [Fact]
    public void VLine_SetBounds_PreservesPointInstance()
    {
        var line = new VLine(0, 0, 10, 0);
        var startRef = line.Start;
        var endRef = line.End;

        line.SetBounds(0.2, 0.8);

        Assert.Same(startRef, line.Start);
        Assert.Same(endRef, line.End);
    }

    [Fact]
    public void VLine_SetBounds_SwapsReversedParameters()
    {
        var line = new VLine(0, 0, 10, 0);
        line.SetBounds(0.75, 0.25);

        Assert.Equal(2.5, line.Start.X, 6);
        Assert.Equal(7.5, line.End.X, 6);
    }

    [Fact]
    public void VLine_SetBounds_ClampsToUnitRange()
    {
        var line = new VLine(0, 0, 10, 0);
        line.SetBounds(-0.5, 1.5);

        Assert.Equal(0.0, line.Start.X, 6);
        Assert.Equal(10.0, line.End.X, 6);
    }

    // --- VArc ---

    [Fact]
    public void VArc_SetBounds_RescalesAngles()
    {
        var arc = new VArc(0, 0, 5, 0, 180);
        arc.SetBounds(0.0, 0.5);

        Assert.Equal(0.0, arc.StartAngle, 6);
        Assert.Equal(90.0, arc.EndAngle, 6);
    }

    [Fact]
    public void VArc_SetBounds_Midband()
    {
        var arc = new VArc(0, 0, 5, 0, 180);
        arc.SetBounds(0.25, 0.75);

        Assert.Equal(45.0, arc.StartAngle, 6);
        Assert.Equal(135.0, arc.EndAngle, 6);
    }

    // --- VEllipse ---

    [Fact]
    public void VEllipse_SetBounds_RescalesAngles()
    {
        var ell = new VEllipse(new VPoint(0, 0), 4, 2, 0, 360);
        ell.SetBounds(0.25, 0.5);

        Assert.Equal(90.0, ell.StartAngle, 6);
        Assert.Equal(180.0, ell.EndAngle, 6);
    }

    // --- VCircle (throws) ---

    [Fact]
    public void VCircle_SetBounds_Throws()
    {
        var circle = new VCircle(0, 0, 5);
        Assert.Throws<NotSupportedException>(() => circle.SetBounds(0.0, 0.5));
    }

    // --- VPolyline ---

    [Fact]
    public void VPolyline_SetBounds_DropsOutOfRangeVertices()
    {
        // 4 segments, 5 vertices at parameters 0, 0.25, 0.5, 0.75, 1
        var poly = new VPolyline(
            new VPoint(0, 0), new VPoint(1, 0),
            new VPoint(2, 0), new VPoint(3, 0),
            new VPoint(4, 0));

        poly.SetBounds(0.25, 0.75);

        // New first vertex should be at original t=0.25 -> x=1
        // Interior vertex at t=0.5 -> x=2 is included
        // New last vertex should be at original t=0.75 -> x=3
        Assert.Equal(3, poly.Points.Count);
        Assert.Equal(1.0, poly.Points[0].X, 6);
        Assert.Equal(2.0, poly.Points[1].X, 6);
        Assert.Equal(3.0, poly.Points[2].X, 6);
    }

    [Fact]
    public void VPolyline_SetBounds_WithinSingleSegment()
    {
        var poly = new VPolyline(
            new VPoint(0, 0), new VPoint(4, 0),
            new VPoint(8, 0));

        // t=[0, 0.5] is the first segment only -> x=0..4
        poly.SetBounds(0.0, 0.25);

        Assert.Equal(2, poly.Points.Count);
        Assert.Equal(0.0, poly.Points[0].X, 6);
        Assert.Equal(2.0, poly.Points[1].X, 6);
    }

    // --- VPolygon (throws) ---

    [Fact]
    public void VPolygon_SetBounds_Throws()
    {
        var poly = new VPolygon(new VPoint(0, 0), new VPoint(1, 0), new VPoint(1, 1), new VPoint(0, 1));
        Assert.Throws<NotSupportedException>(() => poly.SetBounds(0.0, 0.5));
    }

    // --- VBezier ---

    [Fact]
    public void VBezier_SetBounds_TrimmedCurveMatchesOriginal()
    {
        // Trimming should preserve the geometry on [s, e]: evaluating the
        // trimmed bezier at u in [0,1] must equal the original at s + (e-s)*u.
        var bez = new VBezier(0, 0, 1, 2, 2, 2, 3, 0);

        double s = 0.2, e = 0.8;
        var expectedStart = bez.Evaluate(s);
        var expectedMid = bez.Evaluate(s + (e - s) * 0.5);
        var expectedEnd = bez.Evaluate(e);

        bez.SetBounds(s, e);

        Assert.Equal(expectedStart.X, bez.Evaluate(0).X, 6);
        Assert.Equal(expectedStart.Y, bez.Evaluate(0).Y, 6);
        Assert.Equal(expectedMid.X, bez.Evaluate(0.5).X, 6);
        Assert.Equal(expectedMid.Y, bez.Evaluate(0.5).Y, 6);
        Assert.Equal(expectedEnd.X, bez.Evaluate(1).X, 6);
        Assert.Equal(expectedEnd.Y, bez.Evaluate(1).Y, 6);
    }

    [Fact]
    public void VBezier_SetBounds_PreservesPointInstances()
    {
        var bez = new VBezier(0, 0, 1, 2, 2, 2, 3, 0);
        var p0Ref = bez.P0;
        var p3Ref = bez.P3;

        bez.SetBounds(0.1, 0.9);

        Assert.Same(p0Ref, bez.P0);
        Assert.Same(p3Ref, bez.P3);
    }

    // --- VSpline ---

    [Fact]
    public void VSpline_SetBounds_TrimmedCurveTracksOriginal()
    {
        var spline = new VSpline(
            new VPoint(0, 0), new VPoint(2, 1),
            new VPoint(4, -1), new VPoint(6, 0),
            new VPoint(8, 1));

        double s = 0.25, e = 0.75;
        // Sample expected points along the original on the trimmed range, before mutation.
        var expectedStart = spline.PointAtParameter(s);
        var expectedQuarter = spline.PointAtParameter(s + (e - s) * 0.25);
        var expectedMid = spline.PointAtParameter(s + (e - s) * 0.5);
        var expectedThreeQ = spline.PointAtParameter(s + (e - s) * 0.75);
        var expectedEnd = spline.PointAtParameter(e);

        spline.SetBounds(s, e);

        // Endpoints are exact (Catmull-Rom interpolates its first/last CP).
        Assert.Equal(expectedStart.X, spline.PointAtParameter(0).X, 6);
        Assert.Equal(expectedStart.Y, spline.PointAtParameter(0).Y, 6);
        Assert.Equal(expectedEnd.X, spline.PointAtParameter(1).X, 6);
        Assert.Equal(expectedEnd.Y, spline.PointAtParameter(1).Y, 6);

        // Interior samples track the original closely (dense resampling).
        // Tolerance is generous because Catmull-Rom interpolation between
        // resampled points differs slightly from the original tangents.
        Assert.Equal(expectedQuarter.X, spline.PointAtParameter(0.25).X, 1);
        Assert.Equal(expectedQuarter.Y, spline.PointAtParameter(0.25).Y, 1);
        Assert.Equal(expectedMid.X, spline.PointAtParameter(0.5).X, 1);
        Assert.Equal(expectedMid.Y, spline.PointAtParameter(0.5).Y, 1);
        Assert.Equal(expectedThreeQ.X, spline.PointAtParameter(0.75).X, 1);
        Assert.Equal(expectedThreeQ.Y, spline.PointAtParameter(0.75).Y, 1);
    }

    // --- VRay / VXLine (throw) ---

    [Fact]
    public void VRay_SetBounds_Throws()
    {
        var ray = new VRay(new VPoint(0, 0), new VXYZ(1, 0, 0));
        Assert.Throws<NotSupportedException>(() => ray.SetBounds(0.0, 0.5));
    }

    [Fact]
    public void VXLine_SetBounds_Throws()
    {
        var xline = new VXLine(new VPoint(0, 0), new VXYZ(1, 0, 0));
        Assert.Throws<NotSupportedException>(() => xline.SetBounds(0.0, 0.5));
    }
}
