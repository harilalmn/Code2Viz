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
        StrokeColor = ShapeDefaults.GlobalStrokeColor ?? "White";
        FillColor = ShapeDefaults.GlobalFillColor ?? "LimeGreen";
    }

    /// <summary>
    /// Converts this VPoint to a VXYZ.
    /// </summary>
    public VXYZ AsVXYZ() => new VXYZ(X, Y, 0);

    /// <summary>
    /// Adds another VPoint's components to this VPoint, returning a new VPoint.
    /// </summary>
    public VPoint Add(VPoint other) => new VPoint(X + other.X, Y + other.Y);

    /// <summary>
    /// Adds a VXYZ to this VPoint, returning a new VPoint.
    ///Ignore Z component for 2D Point.
    /// </summary>
    public VPoint Add(VXYZ vector) => new VPoint(X + vector.X, Y + vector.Y);

    // Operator overloads
    public static VPoint operator +(VPoint a, VPoint b) => new VPoint(a.X + b.X, a.Y + b.Y);
    public static VPoint operator +(VPoint a, VXYZ b) => new VPoint(a.X + b.X, a.Y + b.Y);

    public override void Draw()
    {
        CanvasRenderer.Instance.AddShape(this);
    }

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

    public override (VPoint min, VPoint max) GetBounds() => (new VPoint(X, Y), new VPoint(X, Y));

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

    public override string ToString() => $"VPoint({X}, {Y})";
}

