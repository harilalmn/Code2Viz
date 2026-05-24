using System;
using Xunit;
using Code2Viz.Canvas;
using C2V = C2VGeometry;
using Geom = Code2Viz.Geometry;

namespace Code2Viz.Tests;

public class VTextDoesIntersectC2VTests
{
    [Fact]
    public void AxisAlignedText_OverlapsRectangle_ReturnsTrue()
    {
        var text = new C2V.VText(0, 0, "Hello", 10) { Anchor = C2V.VTextAnchor.BottomLeft };
        var rect = new C2V.VRectangle(new C2V.VXYZ(5, 2), 4, 4);

        Assert.True(text.DoesIntersect(rect));
    }

    [Fact]
    public void AxisAlignedText_SeparatedFromRectangle_ReturnsFalse()
    {
        var text = new C2V.VText(0, 0, "Hello", 10) { Anchor = C2V.VTextAnchor.BottomLeft };
        var rect = new C2V.VRectangle(new C2V.VXYZ(100, 100), 4, 4);

        Assert.False(text.DoesIntersect(rect));
    }

    [Fact]
    public void RotatedText_OBBRejectsTargetInsideAxisAlignedAABB()
    {
        // Text: anchored at center, width auto (~Height*len*0.6 = 2*5*0.6 = 6), height=2.
        // Unrotated AABB: [-3,3] x [-1,1].  Rotated 90° OBB: [-1,1] x [-3,3].
        var text = new C2V.VText(0, 0, "Hello", 2)
        {
            Anchor = C2V.VTextAnchor.MiddleCenter,
            Angle = 90
        };

        // Target sits inside the unrotated AABB on the +X side but outside the rotated OBB.
        var target = new C2V.VRectangle(new C2V.VXYZ(2, -0.25), 0.8, 0.5);

        Assert.False(text.DoesIntersect(target));

        // Same target with Angle = 0 should overlap.
        text.Angle = 0;
        Assert.True(text.DoesIntersect(target));
    }

    [Fact]
    public void RotatedText_OBBStillIntersectsTargetInsideOBB()
    {
        var text = new C2V.VText(0, 0, "Hello", 2)
        {
            Anchor = C2V.VTextAnchor.MiddleCenter,
            Angle = 90
        };

        // Target inside the rotated OBB ([-1,1] x [-3,3]).
        var target = new C2V.VRectangle(new C2V.VXYZ(-0.25, 1.5), 0.5, 0.5);

        Assert.True(text.DoesIntersect(target));
    }

    [Fact]
    public void AnchorChangesIntersection_BottomLeftVsTopRight()
    {
        // Same Location & content; different anchor moves the text rect entirely.
        var probe = new C2V.VRectangle(new C2V.VXYZ(1, 1), 2, 2);

        var bottomLeft = new C2V.VText(0, 0, "AB", 10) { Anchor = C2V.VTextAnchor.BottomLeft };
        Assert.True(bottomLeft.DoesIntersect(probe));

        var topRight = new C2V.VText(0, 0, "AB", 10) { Anchor = C2V.VTextAnchor.TopRight };
        // Anchored top-right at (0,0): rect occupies x in [-12, 0], y in [-10, 0] → no overlap with probe at (1..3, 1..3).
        Assert.False(topRight.DoesIntersect(probe));
    }
}

[Collection("CanvasState")]
public class VTextDoesIntersectGeomTests : IDisposable
{
    public VTextDoesIntersectGeomTests()
    {
        Geom.Shape.AutoRegister = true;
        CanvasRenderer.Instance.Clear();
    }

    public void Dispose()
    {
        CanvasRenderer.Instance.Clear();
    }

    [Fact]
    public void AxisAlignedText_OverlapsRectangle_ReturnsTrue()
    {
        var text = new Geom.VText(0, 0, "Hello", 10) { Anchor = Geom.VTextAnchor.BottomLeft };
        var rect = new Geom.VRectangle(new Geom.VPoint(5, 2), 4, 4);

        Assert.True(text.DoesIntersect(rect));
    }

    [Fact]
    public void AxisAlignedText_SeparatedFromRectangle_ReturnsFalse()
    {
        var text = new Geom.VText(0, 0, "Hello", 10) { Anchor = Geom.VTextAnchor.BottomLeft };
        var rect = new Geom.VRectangle(new Geom.VPoint(100, 100), 4, 4);

        Assert.False(text.DoesIntersect(rect));
    }

    [Fact]
    public void RotatedText_OBBRejectsTargetInsideAxisAlignedAABB()
    {
        var text = new Geom.VText(0, 0, "Hello", 2)
        {
            Anchor = Geom.VTextAnchor.MiddleCenter,
            Angle = 90
        };

        var target = new Geom.VRectangle(new Geom.VPoint(2, -0.25), 0.8, 0.5);

        Assert.False(text.DoesIntersect(target));

        text.Angle = 0;
        Assert.True(text.DoesIntersect(target));
    }
}
