using System;
using Xunit;
using Code2Viz.Canvas;
using Code2Viz.Sketching;
using C2V = C2VGeometry;
using Geom = Code2Viz.Geometry;

namespace Code2Viz.Tests;

[Collection("CanvasState")]
public class C2VGeometryAdapterTests : IDisposable
{
    public C2VGeometryAdapterTests()
    {
        // Adapter construction registers Geom shapes on the canvas; isolate by clearing.
        Geom.Shape.AutoRegister = true;
        CanvasRenderer.Instance.Clear();
        C2V.Shape.DefaultRegistry = null;          // adapter source shapes shouldn't fan out
    }

    public void Dispose()
    {
        CanvasRenderer.Instance.Clear();
        C2V.Shape.DefaultRegistry = null;
    }

    [Fact]
    public void Convert_VCircle_PreservesCenterAndRadius()
    {
        var src = new C2V.VCircle(new C2V.VXYZ(3, 4), 12.5);
        src.Color = "Red";
        src.FillColor = "Yellow";
        src.LineWeight = 2.5;

        var dst = (Geom.VCircle)C2VGeometryAdapter.Convert(src)!;

        Assert.Equal(3, dst.Center.X, 6);
        Assert.Equal(4, dst.Center.Y, 6);
        Assert.Equal(12.5, dst.Radius, 6);
        Assert.Equal("Red", dst.Color);
        Assert.Equal("Yellow", dst.FillColor);
        Assert.Equal(2.5, dst.LineWeight, 6);
    }

    [Fact]
    public void Convert_VLine_PreservesEndpoints()
    {
        var src = new C2V.VLine(new C2V.VXYZ(0, 0), new C2V.VXYZ(10, 5));

        var dst = (Geom.VLine)C2VGeometryAdapter.Convert(src)!;

        Assert.Equal(0, dst.Start.X, 6);
        Assert.Equal(0, dst.Start.Y, 6);
        Assert.Equal(10, dst.End.X, 6);
        Assert.Equal(5, dst.End.Y, 6);
    }

    [Fact]
    public void Convert_VRectangle_PreservesCornerSizeRotation()
    {
        var src = new C2V.VRectangle(new C2V.VXYZ(-5, -3), 10, 6)
        {
            RotationAngle = 30
        };

        var dst = (Geom.VRectangle)C2VGeometryAdapter.Convert(src)!;

        Assert.Equal(-5, dst.Corner.X, 6);
        Assert.Equal(-3, dst.Corner.Y, 6);
        Assert.Equal(10, dst.Width, 6);
        Assert.Equal(6, dst.Height, 6);
        Assert.Equal(30, dst.RotationAngle, 6);
    }

    [Fact]
    public void Convert_VEllipse_PreservesRadii()
    {
        var src = new C2V.VEllipse(new C2V.VXYZ(1, 2), 4, 7);

        var dst = (Geom.VEllipse)C2VGeometryAdapter.Convert(src)!;

        Assert.Equal(1, dst.Center.X, 6);
        Assert.Equal(2, dst.Center.Y, 6);
        Assert.Equal(4, dst.RadiusX, 6);
        Assert.Equal(7, dst.RadiusY, 6);
    }

    [Fact]
    public void Convert_VArc_PreservesAngles()
    {
        var src = new C2V.VArc(new C2V.VXYZ(0, 0), 10, 30, 120);

        var dst = (Geom.VArc)C2VGeometryAdapter.Convert(src)!;

        Assert.Equal(10, dst.Radius, 6);
        Assert.Equal(30, dst.StartAngle, 6);
        Assert.Equal(120, dst.EndAngle, 6);
    }

    [Fact]
    public void Convert_VPolygon_PreservesPoints()
    {
        var src = new C2V.VPolygon(
            new C2V.VXYZ(0, 0),
            new C2V.VXYZ(10, 0),
            new C2V.VXYZ(10, 10),
            new C2V.VXYZ(0, 10));

        var dst = (Geom.VPolygon)C2VGeometryAdapter.Convert(src)!;

        Assert.Equal(4, dst.Points.Count);
        Assert.Equal(10, dst.Points[1].X, 6);
        Assert.Equal(10, dst.Points[2].Y, 6);
    }

    [Fact]
    public void Convert_VPolyline_PreservesPoints()
    {
        var src = new C2V.VPolyline(new C2V.VXYZ(0, 0), new C2V.VXYZ(5, 5), new C2V.VXYZ(10, 0));

        var dst = (Geom.VPolyline)C2VGeometryAdapter.Convert(src)!;

        Assert.Equal(3, dst.Points.Count);
        Assert.Equal(5, dst.Points[1].X, 6);
    }

    [Fact]
    public void Convert_VBezier_PreservesControlPoints()
    {
        var src = new C2V.VBezier(
            new C2V.VXYZ(0, 0), new C2V.VXYZ(1, 2),
            new C2V.VXYZ(3, 2), new C2V.VXYZ(4, 0));

        var dst = (Geom.VBezier)C2VGeometryAdapter.Convert(src)!;

        Assert.Equal(0, dst.P0.X, 6);
        Assert.Equal(4, dst.P3.X, 6);
        Assert.Equal(2, dst.P1.Y, 6);
    }

    [Fact]
    public void Convert_VSpline_PreservesControlPoints()
    {
        var src = new C2V.VSpline(new C2V.VXYZ(0, 0), new C2V.VXYZ(2, 1), new C2V.VXYZ(4, -1));

        var dst = (Geom.VSpline)C2VGeometryAdapter.Convert(src)!;

        Assert.Equal(3, dst.ControlPoints.Count);
        Assert.Equal(2, dst.ControlPoints[1].X, 6);
    }

    [Fact]
    public void Convert_CopiesStyleFields()
    {
        var src = new C2V.VLine(new C2V.VXYZ(0, 0), new C2V.VXYZ(1, 1))
        {
            Color = "Magenta",
            FillColor = "Pink",
            LineWeight = 3.5,
            LineType = C2V.LineType.Dashed,
            LineTypeScale = 2.0,
            Opacity = 0.5,
            IsVisible = true,
            DrawFactor = 0.8,
            OffsetX = 5,
            OffsetY = -3
        };

        var dst = (Geom.VLine)C2VGeometryAdapter.Convert(src)!;

        Assert.Equal("Magenta", dst.Color);
        Assert.Equal("Pink", dst.FillColor);
        Assert.Equal(3.5, dst.LineWeight, 6);
        Assert.Equal(Geom.LineType.Dashed, dst.LineType);
        Assert.Equal(2.0, dst.LineTypeScale, 6);
        Assert.Equal(0.5, dst.Opacity, 6);
        Assert.True(dst.IsVisible);
        Assert.Equal(0.8, dst.DrawFactor, 6);
        Assert.Equal(5, dst.OffsetX, 6);
        Assert.Equal(-3, dst.OffsetY, 6);
    }

    [Fact]
    public void Convert_UnsupportedType_ReturnsNull()
    {
        // VRay isn't in the adapter switch (yet) — should log a warning and return null.
        var src = new C2V.VRay(new C2V.VXYZ(0, 0), new C2V.VXYZ(1, 0));

        var dst = C2VGeometryAdapter.Convert(src);

        Assert.Null(dst);
    }

    [Fact]
    public void Registry_CollectsConstructedShapes_AndDrainProducesAdaptedDrawables()
    {
        var registry = new C2VGeometryRegistry();
        C2V.Shape.DefaultRegistry = registry;
        C2V.Shape.AutoRegister = true;

        new C2V.VCircle(new C2V.VXYZ(0, 0), 5);
        new C2V.VLine(new C2V.VXYZ(0, 0), new C2V.VXYZ(10, 0));

        Assert.Equal(2, registry.FrameShapes.Count);

        int count = 0;
        foreach (var d in registry.DrainConverted())
        {
            Assert.NotNull(d);
            count++;
        }
        Assert.Equal(2, count);
        Assert.Empty(registry.FrameShapes);    // drain clears
    }
}
