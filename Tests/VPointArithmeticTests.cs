using Xunit;
using C2VGeometry;

namespace Code2Viz.Tests;

// Constructing a VPoint can auto-register on the canvas singleton, so this
// class lives in the shared CanvasState collection (see C2VRayCasterTests).
[Collection("CanvasState")]
public class VPointArithmeticTests
{
    private static void AssertXyz(VXYZ v, double x, double y, double z = 0)
    {
        Assert.Equal(x, v.X, 9);
        Assert.Equal(y, v.Y, 9);
        Assert.Equal(z, v.Z, 9);
    }

    // --- Addition ---

    [Fact]
    public void Add_PointPlusPoint_ReturnsVxyz()
        => AssertXyz(new VPoint(1, 2) + new VPoint(3, 4), 4, 6);

    [Fact]
    public void Add_PointPlusVxyz_AndReverse()
    {
        AssertXyz(new VPoint(1, 2) + new VXYZ(3, 4, 5), 4, 6, 5);
        AssertXyz(new VXYZ(3, 4, 5) + new VPoint(1, 2), 4, 6, 5);
    }

    // --- Subtraction ---

    [Fact]
    public void Subtract_PointMinusPoint_ReturnsDisplacement()
        => AssertXyz(new VPoint(5, 7) - new VPoint(1, 2), 4, 5);

    [Fact]
    public void Subtract_MixedWithVxyz_PreservesZ()
    {
        AssertXyz(new VXYZ(5, 7, 9) - new VPoint(1, 2), 4, 5, 9);
        AssertXyz(new VPoint(5, 7) - new VXYZ(1, 2, 9), 4, 5, -9);
    }

    // --- Scalar multiply / divide ---

    [Fact]
    public void Multiply_PointByScalar_BothOrders()
    {
        AssertXyz(new VPoint(2, 3) * 2.0, 4, 6);
        AssertXyz(2.0 * new VPoint(2, 3), 4, 6);
    }

    [Fact]
    public void Divide_PointByScalar()
        => AssertXyz(new VPoint(4, 6) / 2.0, 2, 3);

    // --- Component-wise (Hadamard) multiply / divide ---

    [Fact]
    public void Multiply_ComponentWise_BetweenVxyzAndPoint()
    {
        AssertXyz(new VXYZ(2, 3) * new VPoint(4, 5), 8, 15);
        AssertXyz(new VPoint(4, 5) * new VXYZ(2, 3), 8, 15);
        AssertXyz(new VPoint(2, 3) * new VPoint(4, 5), 8, 15);
    }

    [Fact]
    public void Divide_ComponentWise_BetweenVxyzAndPoint()
    {
        AssertXyz(new VXYZ(8, 15) / new VPoint(4, 5), 2, 3);
        AssertXyz(new VPoint(8, 15) / new VXYZ(4, 5), 2, 3);
        AssertXyz(new VPoint(8, 15) / new VPoint(4, 5), 2, 3);
    }

    [Fact]
    public void Result_IsPlainVxyz_NotDrawable()
    {
        // The result is a coordinate value type, never a drawable VPoint.
        object r = new VPoint(1, 2) + new VPoint(3, 4);
        Assert.IsType<VXYZ>(r);
    }
}
