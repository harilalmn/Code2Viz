using Code2Viz.Canvas;

namespace Code2Viz.Geometry;

public class VPoint : Shape
{
    public double X { get; set; }
    public double Y { get; set; }

    public VPoint(double x, double y)
    {
        X = x;
        Y = y;
        Color = ShapeDefaults.GlobalColor ?? "White";
        FillColor = ShapeDefaults.GlobalFillColor ?? "LimeGreen";
    }

    /// <summary>
    /// Internal constructor for creating points without auto-registration.
    /// Used by geometry classes for intermediate calculations.
    /// </summary>
    internal VPoint(double x, double y, bool register) : base(register)
    {
        X = x;
        Y = y;
        Color = ShapeDefaults.GlobalColor ?? "White";
        FillColor = ShapeDefaults.GlobalFillColor ?? "LimeGreen";
    }

    /// <summary>
    /// Creates an internal VPoint that is not auto-registered with the canvas.
    /// Use this for intermediate calculations to avoid polluting the shape list.
    /// </summary>
    internal static VPoint Internal(double x, double y) => new VPoint(x, y, false);

    /// <summary>
    /// Converts this VPoint to a VXYZ.
    /// </summary>
    public VXYZ AsVXYZ() => new VXYZ(X, Y, 0);

    /// <summary>
    /// Adds another VPoint's components to this VPoint, returning a new VPoint.
    /// </summary>
    public VPoint Add(VPoint other) => Internal(X + other.X, Y + other.Y);

    /// <summary>
    /// Adds a VXYZ to this VPoint, returning a new VPoint.
    /// Ignore Z component for 2D Point.
    /// </summary>
    public VPoint Add(VXYZ vector) => Internal(X + vector.X, Y + vector.Y);

    // Operator overloads - Addition (use Internal() to avoid auto-registering)
    public static VPoint operator +(VPoint a, VPoint b) => Internal(a.X + b.X, a.Y + b.Y);
    public static VPoint operator +(VPoint a, VXYZ b) => Internal(a.X + b.X, a.Y + b.Y);

    // Subtraction
    public static VPoint operator -(VPoint a, VPoint b) => Internal(a.X - b.X, a.Y - b.Y);
    public static VPoint operator -(VPoint a, VXYZ b) => Internal(a.X - b.X, a.Y - b.Y);
    public static VPoint operator -(VPoint a) => Internal(-a.X, -a.Y); // Unary negation

    // Scalar multiplication
    public static VPoint operator *(VPoint a, double scalar) => Internal(a.X * scalar, a.Y * scalar);
    public static VPoint operator *(double scalar, VPoint a) => Internal(a.X * scalar, a.Y * scalar);

    // Scalar division
    public static VPoint operator /(VPoint a, double scalar) => Internal(a.X / scalar, a.Y / scalar);



    public override Shape Clone()
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

    public override void Rotate(VPoint pivot, double angleDegrees)
    {
        var rotated = GeometryHelper.RotatePoint(this, pivot, angleDegrees);
        X = rotated.X;
        Y = rotated.Y;
    }

    public override void Flip(VLine mirrorLine)
    {
        var flipped = GeometryHelper.FlipPoint(this, mirrorLine);
        X = flipped.X;
        Y = flipped.Y;
    }

    public override void Scale(VPoint center, double factor)
    {
        X = center.X + (X - center.X) * factor;
        Y = center.Y + (Y - center.Y) * factor;
    }

    // Use Internal() to avoid auto-registering intermediate points
    public override BoundingBox GetBounds() => new BoundingBox(VPoint.Internal(X, Y), VPoint.Internal(X, Y));

    public override double DistanceTo(VPoint point)
    {
        var dx = point.X - X;
        var dy = point.Y - Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    public override Shape? Intersect(Shape other)
    {
        if (other.Contains(this))
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

    public override void MoveControlPoint(int index, VPoint newPosition)
    {
        if (index == 0)
        {
            X = newPosition.X;
            Y = newPosition.Y;
        }
    }

    public override string ToString() => $"VPoint({X}, {Y})";
}

