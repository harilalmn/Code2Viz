using Code2Viz.Canvas;

namespace Code2Viz.Geometry;

public class VRectangle : VPolygon
{
    private VPoint _corner;
    private double _width;
    private double _height;
    private double _rotationAngle;

    public VPoint Corner
    {
        get => _corner;
        set
        {
            _corner = value;
            UpdatePoints();
        }
    }

    public double Width
    {
        get => _width;
        set
        {
            _width = value;
            UpdatePoints();
        }
    }

    public double Height
    {
        get => _height;
        set
        {
            _height = value;
            UpdatePoints();
        }
    }

    /// <summary>Rotation angle in degrees (counter-clockwise) for the rectangle's intrinsic orientation.</summary>
    public new double RotationAngle
    {
        get => _rotationAngle;
        set
        {
            _rotationAngle = value;
            UpdatePoints();
        }
    }

    public VRectangle(VPoint corner, double width, double height)
        : base(ComputeCorners(corner, width, height, 0))
    {
        _corner = corner;
        _width = width;
        _height = height;
        _rotationAngle = 0;
        Color = ShapeDefaults.GlobalColor ?? "Magenta";
        FillColor = ShapeDefaults.GlobalFillColor ?? "Transparent";
    }

    public VRectangle(double x, double y, double width, double height)
        : this(new VPoint(x, y), width, height)
    {
    }

    /// <summary>
    /// Creates a rectangle from two corner points (bottom-left and top-right).
    /// </summary>
    /// <param name="bottomLeft">The bottom-left corner of the rectangle.</param>
    /// <param name="topRight">The top-right corner of the rectangle.</param>
    public VRectangle(VPoint bottomLeft, VPoint topRight)
        : this(bottomLeft, topRight.X - bottomLeft.X, topRight.Y - bottomLeft.Y)
    {
    }

    private static VPoint[] ComputeCorners(VPoint corner, double width, double height, double rotationAngle)
    {
        // Use Internal() to avoid auto-registering intermediate points
        var p0 = VPoint.Internal(corner.X, corner.Y);
        var p1 = VPoint.Internal(corner.X + width, corner.Y);
        var p2 = VPoint.Internal(corner.X + width, corner.Y + height);
        var p3 = VPoint.Internal(corner.X, corner.Y + height);

        if (Math.Abs(rotationAngle) >= 1e-9)
        {
            double centerX = corner.X + width / 2;
            double centerY = corner.Y + height / 2;
            var center = VPoint.Internal(centerX, centerY);

            p0.Rotate(center, rotationAngle);
            p1.Rotate(center, rotationAngle);
            p2.Rotate(center, rotationAngle);
            p3.Rotate(center, rotationAngle);
        }

        return new[] { p0, p1, p2, p3 };
    }

    private void UpdatePoints()
    {
        var corners = ComputeCorners(_corner, _width, _height, _rotationAngle);
        Points.Clear();
        Points.AddRange(corners);
        BuildCurvesFromPoints();
    }

    public override Shape Clone()
    {
        var clone = new VRectangle((VPoint)_corner.Clone(), _width, _height);
        clone._rotationAngle = _rotationAngle;
        clone.UpdatePoints();
        CopyStyleTo(clone);
        return clone;
    }

    public override void Move(VXYZ vector)
    {
        _corner.Move(vector);
        UpdatePoints();
    }

    public override void Rotate(VPoint pivot, double angleDegrees)
    {
        _corner.Rotate(pivot, angleDegrees);
        _rotationAngle += angleDegrees;
        UpdatePoints();
    }

    public override void Flip(VLine mirrorLine)
    {
        _corner.Flip(mirrorLine);
        UpdatePoints();
    }

    public override void Scale(VPoint center, double factor)
    {
        _corner.Scale(center, factor);
        _width *= Math.Abs(factor);
        _height *= Math.Abs(factor);
        UpdatePoints();
    }

    public override bool Contains(VPoint point)
    {
        // For axis-aligned check (no rotation), use simple bounds
        if (Math.Abs(_rotationAngle) < 1e-9)
        {
            return point.X >= _corner.X && point.X <= _corner.X + _width &&
                   point.Y >= _corner.Y && point.Y <= _corner.Y + _height;
        }
        // Otherwise, use polygon containment from base class
        return IsPointInPolygon(point);
    }

    private bool IsPointInPolygon(VPoint point)
    {
        // Ray casting algorithm
        bool inside = false;
        int j = Points.Count - 1;

        for (int i = 0; i < Points.Count; i++)
        {
            if ((Points[i].Y > point.Y) != (Points[j].Y > point.Y) &&
                point.X < (Points[j].X - Points[i].X) * (point.Y - Points[i].Y) / (Points[j].Y - Points[i].Y) + Points[i].X)
            {
                inside = !inside;
            }
            j = i;
        }

        return inside;
    }

    public override Shape? Intersect(Shape other)
    {
        if (other is VRectangle otherRect)
        {
            return GeometryHelper.IntersectRectRect(this, otherRect);
        }
        else if (other is VLine line)
        {
            return GeometryHelper.IntersectLineRect(line, this);
        }
        return base.Intersect(other);
    }

    public override string ToString() => $"VRectangle({_corner}, W:{_width}, H:{_height})";

    public new double GetLength()
    {
        return 2 * (Math.Abs(_width) + Math.Abs(_height));
    }

    public new ICurve Offset(double distance)
    {
        // Simple inflation for axis-aligned rectangle
        if (Math.Abs(_rotationAngle) < 1e-9)
        {
            double newWidth = _width + 2 * distance;
            double newHeight = _height + 2 * distance;
            // Use Internal() for the corner point to avoid registering intermediate
            return new VRectangle(
                VPoint.Internal(_corner.X - distance, _corner.Y - distance),
                newWidth, newHeight
            );
        }
        // For rotated rectangles, use polygon offset
        return base.Offset(distance);
    }

    public new List<ICurve> Offset(List<double> distances)
    {
        var list = new List<ICurve>();
        foreach (var d in distances) list.Add(Offset(d));
        return list;
    }
}
