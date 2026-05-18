namespace C2VGeometry;

public class VCircle : Shape, ICurve
{
    public VXYZ Center { get; set; }
    public double Radius { get; set; }

    /// <summary>Gets the area of the circle (π * r²).</summary>
    public double Area => Math.PI * Radius * Radius;

    /// <summary>Gets the circumference of the circle (2 * π * r).</summary>
    public double Circumference => 2 * Math.PI * Radius;

    /// <summary>Gets the start point of the circle (at angle 0).</summary>
    public VXYZ StartPoint => new VXYZ(Center.X + Radius, Center.Y);

    /// <summary>Gets the end point of the circle (same as StartPoint, since it's closed).</summary>
    public VXYZ EndPoint => new VXYZ(Center.X + Radius, Center.Y);

    /// <summary>A circle is never self-intersecting.</summary>
    public bool SelfIntersecting => false;

    /// <summary>Gets the vertices of the circle (center point).</summary>
    public List<VXYZ> Vertices => new List<VXYZ> { Center };

    public VCircle(VXYZ center, double radius)
    {
        Center = center;
        Radius = radius;
        Color = "Yellow";
    }

    public VCircle(double centerX, double centerY, double radius)
    {
        Center = new VXYZ(centerX, centerY);
        Radius = radius;
        Color = "Yellow";
    }

    /// <summary>
    /// Creates a circle passing through three points (circumcircle).
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the three points are collinear.</exception>
    public VCircle(VXYZ p1, VXYZ p2, VXYZ p3)
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

        Center = new VXYZ(cx, cy);
        Radius = Math.Sqrt((x1 - cx) * (x1 - cx) + (y1 - cy) * (y1 - cy));
        Color = "Yellow";
    }

    /// <summary>
    /// Creates a circle from center point and diameter.
    /// </summary>
    public static VCircle FromCenterDiameter(VXYZ center, double diameter)
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
    public static VCircle FromTwoPoints(VXYZ p1, VXYZ p2)
    {
        var center = new VXYZ((p1.X + p2.X) / 2.0, (p1.Y + p2.Y) / 2.0);
        double radius = p1.DistanceTo(p2) / 2.0;
        return new VCircle(center, radius);
    }



    public override List<ControlPoint> GetControlPoints()
    {
        return new List<ControlPoint>
        {
            new ControlPoint(ControlPointType.Move, Center.X, Center.Y, "Center"),
            new ControlPoint(ControlPointType.Radius, Center.X + Radius, Center.Y, "Radius")
        };
    }

    public override void MoveControlPoint(int index, VXYZ newPosition)
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

    public override VCircle Clone()
    {
        var clone = new VCircle(Center.Clone(), Radius);
        CopyStyleTo(clone);
        return clone;
    }

    public override void Move(VXYZ vector)
    {
        Center = Center + vector;
    }

    public override void Rotate(VXYZ pivot, double angleDegrees)
    {
        Center = GeometryHelper.RotatePoint(Center, pivot, angleDegrees);
    }

    public override void Flip(VLine mirrorLine)
    {
        Center = GeometryHelper.FlipPoint(Center, mirrorLine);
    }

    public override void Scale(VXYZ center, double factor)
    {
        Center = GeometryHelper.ScalePoint(Center, center, factor);
        Radius *= Math.Abs(factor);
    }

    public override BoundingBox GetBounds()
    {
        return new BoundingBox(
            new VXYZ(Center.X - Radius, Center.Y - Radius),
            new VXYZ(Center.X + Radius, Center.Y + Radius)
        );
    }

    public override bool Contains(VXYZ point)
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

    public List<VXYZ> Divide(int numberOfSegments)
    {
        var points = new List<VXYZ>();
        if (numberOfSegments <= 0) return points;

        for (int i = 0; i <= numberOfSegments; i++)
        {
            double angle = (i * 2 * Math.PI) / numberOfSegments;
            points.Add(GetPointAtAngle(angle));
        }
        return points;
    }

    public List<VXYZ> Measure(double segmentLength)
    {
        var points = new List<VXYZ>();
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

    private VXYZ GetPointAtAngle(double angleRadians)
    {
        double x = Center.X + Radius * Math.Cos(angleRadians);
        double y = Center.Y + Radius * Math.Sin(angleRadians);
        return new VXYZ(x, y);
    }

    public VXYZ Project(VXYZ point)
    {
        VXYZ cp = point - Center;
        if (cp.IsZeroLength()) cp = new VXYZ(1, 0, 0);

        double angle = Math.Atan2(cp.Y, cp.X);
        return new VXYZ(Center.X + Radius * Math.Cos(angle), Center.Y + Radius * Math.Sin(angle));
    }

    public VXYZ PointAtSegmentLength(double segmentLength)
    {
        double circumference = GetLength();
        double angleRad = (segmentLength / circumference) * 2 * Math.PI;
        return GetPointAtAngle(angleRad);
    }

    public ICurve Offset(double distance)
    {
        double newRadius = Radius + distance;
        if (newRadius < 0) newRadius = 0;
        return new VCircle(Center.Clone(), newRadius);
    }

    public List<ICurve> Offset(List<double> distances)
    {
        var result = new List<ICurve>();
        foreach (var d in distances) result.Add(Offset(d));
        return result;
    }

    public List<VXYZ> PointsAtChordLengthFromPoint(VXYZ point, double chordLength)
    {
        var projected = Project(point);
        return GeometryHelper.IntersectCircleCircle(Center, Radius, projected, chordLength);
    }

    public (ICurve, ICurve) SplitAtPoint(VXYZ point)
    {
        var proj = Project(point);
        VXYZ cp = proj - Center;
        double angle = Math.Atan2(cp.Y, cp.X) * 180.0 / Math.PI;
        angle = GeometryHelper.NormalizeAngle(angle);

        return (
            new VArc(Center.Clone(), Radius, 0, angle),
            new VArc(Center.Clone(), Radius, angle, 360)
        );
    }

    public VXYZ NormalAtPoint(VXYZ p)
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
    /// </summary>
    public VXYZ PointAtParameter(double parameter)
    {
        double angle = parameter * 2 * Math.PI;
        return GetPointAtAngle(angle);
    }

    /// <summary>
    /// Returns the normalized parameter (0 to 1) for the closest point on the circle to the given point.
    /// </summary>
    public double ParameterAtPoint(VXYZ point)
    {
        double angle = Math.Atan2(point.Y - Center.Y, point.X - Center.X);
        if (angle < 0) angle += 2 * Math.PI;
        return angle / (2 * Math.PI);
    }

    /// <summary>
    /// Not supported: trimming a circle produces an arc, which is a different shape type.
    /// Use <see cref="SplitAtPoint"/> to obtain <see cref="VArc"/> segments instead.
    /// </summary>
    /// <exception cref="NotSupportedException">Always thrown.</exception>
    public void SetBounds(double startParameter, double endParameter)
    {
        throw new NotSupportedException(
            "VCircle.SetBounds is not supported because a trimmed circle is an arc, not a circle. " +
            "Use SplitAtPoint to obtain VArc segments instead.");
    }
}
