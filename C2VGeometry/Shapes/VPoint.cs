using System;
using System.Collections.Generic;

namespace C2VGeometry;

/// <summary>
/// Represents a visible point marker on the canvas.
/// For coordinate storage, use VXYZ instead.
/// </summary>
public class VPoint : Shape
{
    public double X { get; set; }
    public double Y { get; set; }

    public VPoint(double x, double y)
    {
        X = x;
        Y = y;
        Color = "White";
        FillColor = "LimeGreen";
    }

    public VPoint(VXYZ position)
    {
        X = position.X;
        Y = position.Y;
        Color = "White";
        FillColor = "LimeGreen";
    }

    /// <summary>
    /// Converts this VPoint to a VXYZ coordinate.
    /// </summary>
    public VXYZ AsVXYZ() => new VXYZ(X, Y);

    public override VPoint Clone()
    {
        var clone = new VPoint(X, Y);
        CopyStyleTo(clone);
        return clone;
    }

    public override void Move(VXYZ vector)
    {
        X += vector.X;
        Y += vector.Y;
    }

    public override void Rotate(VXYZ pivot, double angleDegrees)
    {
        var rotated = GeometryHelper.RotatePoint(new VXYZ(X, Y), pivot, angleDegrees);
        X = rotated.X;
        Y = rotated.Y;
    }

    public override void Flip(VLine mirrorLine)
    {
        var flipped = GeometryHelper.FlipPoint(new VXYZ(X, Y), mirrorLine);
        X = flipped.X;
        Y = flipped.Y;
    }

    public override void Scale(VXYZ center, double factor)
    {
        X = center.X + (X - center.X) * factor;
        Y = center.Y + (Y - center.Y) * factor;
    }

    public override BoundingBox GetBounds() => new BoundingBox(new VXYZ(X, Y), new VXYZ(X, Y));

    public override double DistanceTo(VXYZ point)
    {
        var dx = point.X - X;
        var dy = point.Y - Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    public override Shape? Intersect(Shape other)
    {
        if (other.Contains(new VXYZ(X, Y)))
        {
            return (Shape)this.Clone();
        }
        return null;
    }

    public override List<ControlPoint> GetControlPoints()
    {
        return new List<ControlPoint>
        {
            new ControlPoint(ControlPointType.Move, X, Y, "Position")
        };
    }

    public override void MoveControlPoint(int index, VXYZ newPosition)
    {
        if (index == 0)
        {
            X = newPosition.X;
            Y = newPosition.Y;
        }
    }

    public override string ToString() => $"VPoint({X}, {Y})";
}
