using Code2Viz.Canvas;

namespace Code2Viz.Geometry;

public class VCircle : Shape, ICurve
{
    public VPoint Center { get; set; }
    public double Radius { get; set; }

    /// <summary>Gets the start point of the circle (at angle 0).</summary>
    public VPoint StartPoint => VPoint.Internal(Center.X + Radius, Center.Y);

    /// <summary>Gets the end point of the circle (same as StartPoint, since it's closed).</summary>
    public VPoint EndPoint => VPoint.Internal(Center.X + Radius, Center.Y);

    /// <summary>A circle is never self-intersecting.</summary>
    public bool SelfIntersecting => false;

    /// <summary>Gets the vertices of the circle (center point).</summary>
    public List<VPoint> Vertices => new List<VPoint> { Center };

    public VCircle(VPoint center, double radius)
    {
        Center = center;
        Radius = radius;
        Color = ShapeDefaults.GlobalColor ?? "Yellow";
    }

    public VCircle(double centerX, double centerY, double radius)
    {
        Center = VPoint.Internal(centerX, centerY);
        Radius = radius;
        Color = ShapeDefaults.GlobalColor ?? "Yellow";
    }

    /// <summary>
    /// Creates a circle passing through three points (circumcircle).
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the three points are collinear.</exception>
    public VCircle(VPoint p1, VPoint p2, VPoint p3)
    {
        double x1 = p1.X, y1 = p1.Y;
        double x2 = p2.X, y2 = p2.Y;
        double x3 = p3.X, y3 = p3.Y;

        double d = 2 * (x1 * (y2 - y3) + x2 * (y3 - y1) + x3 * (y1 - y2));

        if (GeometryTolerance.IsZero(d))
            throw new ArgumentException("The three points are collinear; cannot create a circle.");

        double sq1 = x1 * x1 + y1 * y1;
        double sq2 = x2 * x2 + y2 * y2;
        double sq3 = x3 * x3 + y3 * y3;

        double cx = (sq1 * (y2 - y3) + sq2 * (y3 - y1) + sq3 * (y1 - y2)) / d;
        double cy = (sq1 * (x3 - x2) + sq2 * (x1 - x3) + sq3 * (x2 - x1)) / d;

        Center = VPoint.Internal(cx, cy);
        Radius = Math.Sqrt((x1 - cx) * (x1 - cx) + (y1 - cy) * (y1 - cy));
        Color = ShapeDefaults.GlobalColor ?? "Yellow";
    }

    /// <summary>
    /// Creates a circle from center point and diameter.
    /// </summary>
    public static VCircle FromCenterDiameter(VPoint center, double diameter)
    {
        return new VCircle(center, diameter / 2.0);
    }

    /// <summary>
    /// Creates a circle from center point and diameter (coordinates version).
    /// </summary>
    public static VCircle FromCenterDiameter(double centerX, double centerY, double diameter)
    {
        return new VCircle(centerX, centerY, diameter / 2.0);
    }

    /// <summary>
    /// Creates a circle where p1 and p2 are endpoints of a diameter.
    /// </summary>
    public static VCircle FromTwoPoints(VPoint p1, VPoint p2)
    {
        var center = VPoint.Internal((p1.X + p2.X) / 2.0, (p1.Y + p2.Y) / 2.0);
        double radius = p1.DistanceTo(p2) / 2.0;
        return new VCircle(new VPoint(center.X, center.Y), radius);
    }



    public override List<ControlPoint> GetControlPoints()
    {
        return new List<ControlPoint>
        {
            new ControlPoint(ControlPointType.Move, Center.X, Center.Y, "Center"),
            new ControlPoint(ControlPointType.Radius, Center.X + Radius, Center.Y, "Radius")
        };
    }

    public override void MoveControlPoint(int index, VPoint newPosition)
    {
        switch (index)
        {
            case 0:
                var delta = new VXYZ(newPosition.X - Center.X, newPosition.Y - Center.Y, 0);
                Move(delta);
                break;
            case 1:
                Radius = Center.DistanceTo(newPosition);
                break;
        }
    }

    public override Shape Clone()
    {
        var clone = new VCircle((VPoint)Center.Clone(), Radius);
        CopyStyleTo(clone);
        return clone;
    }

    public override void Move(VXYZ vector)
    {
        Center.Move(vector);
    }

    public override void Rotate(VPoint pivot, double angleDegrees)
    {
        Center.Rotate(pivot, angleDegrees);
    }

    public override void Flip(VLine mirrorLine)
    {
        Center.Flip(mirrorLine);
    }

    public override void Scale(VPoint center, double factor)
    {
        Center.Scale(center, factor);
        Radius *= Math.Abs(factor);
    }

    public override (VPoint min, VPoint max) GetBounds()
    {
        return (
            VPoint.Internal(Center.X - Radius, Center.Y - Radius),
            VPoint.Internal(Center.X + Radius, Center.Y + Radius)
        );
    }

    public override bool Contains(VPoint point)
    {
        var dx = point.X - Center.X;
        var dy = point.Y - Center.Y;
        return dx * dx + dy * dy <= Radius * Radius;
    }

    public override string ToString() => $"VCircle(Center: {Center}, R: {Radius})";

    public double GetLength()
    {
        return 2 * Math.PI * Radius;
    }

    public List<VPoint> Divide(int numberOfSegments)
    {
        var points = new List<VPoint>();
        if (numberOfSegments <= 0) return points;

        for (int i = 0; i <= numberOfSegments; i++)
        {
            double angle = (i * 2 * Math.PI) / numberOfSegments;
            points.Add(GetPointAtAngle(angle));
        }
        return points;
    }

    public List<VPoint> Measure(double segmentLength)
    {
        var points = new List<VPoint>();
        if (segmentLength <= 1e-9 || Radius <= 1e-9) return points;

        double totalLength = GetLength();
        int count = (int)(totalLength / segmentLength);
        double angleStep = segmentLength / Radius;

        for (int i = 0; i <= count; i++)
        {
            points.Add(GetPointAtAngle(i * angleStep));
        }
        return points;
    }

    private VPoint GetPointAtAngle(double angleRadians)
    {
        double x = Center.X + Radius * Math.Cos(angleRadians);
        double y = Center.Y + Radius * Math.Sin(angleRadians);
        return VPoint.Internal(x, y);
    }

    public VPoint Project(VPoint point)
    {
        VXYZ cp = (point.AsVXYZ() - Center.AsVXYZ());
        if (cp.IsZeroLength()) cp = new VXYZ(1, 0, 0);

        double angle = Math.Atan2(cp.Y, cp.X);
        return VPoint.Internal(Center.X + Radius * Math.Cos(angle), Center.Y + Radius * Math.Sin(angle));
    }

    public VPoint PointAtSegmentLength(double segmentLength)
    {
        // Start at 0 degrees (East)
        double circumference = GetLength();
        double angleRad = (segmentLength / circumference) * 2 * Math.PI;
        return GetPointAtAngle(angleRad);
    }

    public ICurve Offset(double distance)
    {
        double newRadius = Radius + distance;
        if (newRadius < 0) newRadius = 0;
        return new VCircle((VPoint)Center.Clone(), newRadius);
    }

    public List<ICurve> Offset(List<double> distances)
    {
        var result = new List<ICurve>();
        foreach (var d in distances) result.Add(Offset(d));
        return result;
    }

    public List<VPoint> PointsAtChordLengthFromPoint(VPoint point, double chordLength)
    {
        var projected = Project(point);
        return GeometryHelper.IntersectCircleCircle(Center, Radius, projected, chordLength);
    }

    public (ICurve, ICurve) SplitAtPoint(VPoint point)
    {
        // Splitting a circle creates two arcs starting/ending at the split point
        // and the implicit start point (0 degrees).
        // 0 to P, and P to 360 (or 0).
        
        var proj = Project(point);
        VXYZ cp = (proj.AsVXYZ() - Center.AsVXYZ());
        double angle = Math.Atan2(cp.Y, cp.X) * 180.0 / Math.PI;
        angle = GeometryHelper.NormalizeAngle(angle);

        // Arc 1: 0 to angle
        // Arc 2: angle to 360
        
        return (
            new VArc((VPoint)Center.Clone(), Radius, 0, angle),
            new VArc((VPoint)Center.Clone(), Radius, angle, 360)
        );
    }

    public VXYZ NormalAtPoint(VPoint p)
    {
        return new VXYZ(p.X - Center.X, p.Y - Center.Y, 0).Normalize();
    }

    /// <summary>
    /// Computes the intersection between this circle and another curve.
    /// </summary>
    public IntersectionResult Intersect(ICurve other)
    {
        return CurveIntersection.Intersect(this, other);
    }

    /// <summary>
    /// Returns a point on the circle at the given normalized parameter.
    /// Parameter 0 corresponds to angle 0 (3 o'clock), parameter 1 returns to the same point.
    /// </summary>
    public VPoint PointAtParameter(double parameter)
    {
        double angle = parameter * 2 * Math.PI;
        return GetPointAtAngle(angle);
    }

    /// <summary>
    /// Returns the normalized parameter (0 to 1) for the closest point on the circle to the given point.
    /// </summary>
    public double ParameterAtPoint(VPoint point)
    {
        double angle = Math.Atan2(point.Y - Center.Y, point.X - Center.X);
        if (angle < 0) angle += 2 * Math.PI;
        return angle / (2 * Math.PI);
    }
}

