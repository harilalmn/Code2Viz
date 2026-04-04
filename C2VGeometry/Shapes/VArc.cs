namespace C2VGeometry;

public class VArc : Shape, ICurve
{
    public VXYZ Center { get; set; }
    public double Radius { get; set; }
    public double StartAngle { get; set; }  // In degrees
    public double EndAngle { get; set; }    // In degrees

    /// <summary>Gets the start point of the arc.</summary>
    public VXYZ StartPoint => Evaluate(0);

    /// <summary>Gets the end point of the arc.</summary>
    public VXYZ EndPoint => Evaluate(1);

    /// <summary>An arc is never self-intersecting.</summary>
    public bool SelfIntersecting => false;

    /// <summary>Gets the vertices of the arc (center, start point, end point).</summary>
    public List<VXYZ> Vertices => new List<VXYZ> { Center, StartPoint, EndPoint };

    /// <summary>Gets the midpoint of the arc.</summary>
    public VXYZ MidPoint => Evaluate(0.5);

    public VArc(VXYZ center, double radius, double startAngle, double endAngle)
    {
        Center = center;
        Radius = radius;
        StartAngle = startAngle;
        EndAngle = endAngle;
        Color = "Orange";
    }

    public VArc(double centerX, double centerY, double radius, double startAngle, double endAngle)
    {
        Center = new VXYZ(centerX, centerY);
        Radius = radius;
        StartAngle = startAngle;
        EndAngle = endAngle;
        Color = "Orange";
    }

    /// <summary>
    /// Creates an arc passing through three points.
    /// </summary>
    public VArc(VXYZ start, VXYZ mid, VXYZ end)
    {
        // Check collinearity via determinant (2 * signed area)
        double D = 2 * (start.X * (mid.Y - end.Y) + mid.X * (end.Y - start.Y) + end.X * (start.Y - mid.Y));

        if (GeometryTolerance.IsZero(D))
        {
            throw new ArgumentException("Points are collinear, cannot define a unique arc.");
        }

        // Calculate Center
        double s1 = start.X * start.X + start.Y * start.Y;
        double s2 = mid.X * mid.X + mid.Y * mid.Y;
        double s3 = end.X * end.X + end.Y * end.Y;

        double cx = (s1 * (mid.Y - end.Y) + s2 * (end.Y - start.Y) + s3 * (start.Y - mid.Y)) / D;
        double cy = (s1 * (end.X - mid.X) + s2 * (start.X - end.X) + s3 * (mid.X - start.X)) / D;

        Center = new VXYZ(cx, cy);
        Radius = Center.DistanceTo(start);
        Color = "Orange";

        // Calculate Angles
        double a1 = Math.Atan2(start.Y - cy, start.X - cx) * 180.0 / Math.PI;
        double a2 = Math.Atan2(mid.Y - cy, mid.X - cx) * 180.0 / Math.PI;
        double a3 = Math.Atan2(end.Y - cy, end.X - cx) * 180.0 / Math.PI;

        StartAngle = a1;

        double sweep1, sweep2;

        if (D > 0) // CCW
        {
            sweep1 = NormalizePositive(a2 - a1);
            sweep2 = NormalizePositive(a3 - a2);
        }
        else // CW
        {
            sweep1 = NormalizeNegative(a2 - a1);
            sweep2 = NormalizeNegative(a3 - a2);
        }

        EndAngle = StartAngle + sweep1 + sweep2;
    }

    /// <summary>
    /// Creates an arc from start point, center, and end point.
    /// </summary>
    public static VArc FromStartCenterEnd(VXYZ start, VXYZ center, VXYZ end)
    {
        double radius = center.DistanceTo(start);
        double startAngle = Math.Atan2(start.Y - center.Y, start.X - center.X) * 180.0 / Math.PI;
        double endAngle = Math.Atan2(end.Y - center.Y, end.X - center.X) * 180.0 / Math.PI;
        return new VArc(new VXYZ(center.X, center.Y), radius, startAngle, endAngle);
    }

    /// <summary>
    /// Creates an arc from center, start point, and end point.
    /// </summary>
    public static VArc FromCenterStartEnd(VXYZ center, VXYZ start, VXYZ end)
    {
        return FromStartCenterEnd(start, center, end);
    }

    /// <summary>
    /// Creates an arc from start point, center, and sweep angle (in degrees).
    /// </summary>
    public static VArc FromStartCenterAngle(VXYZ start, VXYZ center, double sweepAngleDegrees)
    {
        double radius = center.DistanceTo(start);
        double startAngle = Math.Atan2(start.Y - center.Y, start.X - center.X) * 180.0 / Math.PI;
        double endAngle = startAngle + sweepAngleDegrees;
        return new VArc(new VXYZ(center.X, center.Y), radius, startAngle, endAngle);
    }

    /// <summary>
    /// Creates an arc from center, start point, and sweep angle (in degrees).
    /// </summary>
    public static VArc FromCenterStartAngle(VXYZ center, VXYZ start, double sweepAngleDegrees)
    {
        return FromStartCenterAngle(start, center, sweepAngleDegrees);
    }

    /// <summary>
    /// Creates an arc from start point, center, and arc length.
    /// </summary>
    public static VArc FromStartCenterLength(VXYZ start, VXYZ center, double arcLength)
    {
        double radius = center.DistanceTo(start);
        double sweepAngleRad = arcLength / radius;
        double sweepAngleDeg = sweepAngleRad * 180.0 / Math.PI;
        return FromStartCenterAngle(start, center, sweepAngleDeg);
    }

    /// <summary>
    /// Creates an arc from center, start point, and arc length.
    /// </summary>
    public static VArc FromCenterStartLength(VXYZ center, VXYZ start, double arcLength)
    {
        return FromStartCenterLength(start, center, arcLength);
    }

    /// <summary>
    /// Creates an arc from start point, end point, and radius.
    /// </summary>
    /// <param name="largeArc">If true, creates the larger arc; otherwise the smaller arc.</param>
    public static VArc FromStartEndRadius(VXYZ start, VXYZ end, double radius, bool largeArc = false)
    {
        double d = start.DistanceTo(end);
        if (d > 2 * radius)
            throw new ArgumentException("Radius too small for the given points.");

        double midX = (start.X + end.X) / 2.0;
        double midY = (start.Y + end.Y) / 2.0;

        double h = Math.Sqrt(radius * radius - (d / 2.0) * (d / 2.0));

        double dx = end.X - start.X;
        double dy = end.Y - start.Y;
        double perpX = -dy / d;
        double perpY = dx / d;

        double cx1 = midX + h * perpX;
        double cy1 = midY + h * perpY;
        double cx2 = midX - h * perpX;
        double cy2 = midY - h * perpY;

        VXYZ center = largeArc ? new VXYZ(cx2, cy2) : new VXYZ(cx1, cy1);

        double startAngle = Math.Atan2(start.Y - center.Y, start.X - center.X) * 180.0 / Math.PI;
        double endAngle = Math.Atan2(end.Y - center.Y, end.X - center.X) * 180.0 / Math.PI;

        return new VArc(center, radius, startAngle, endAngle);
    }

    /// <summary>
    /// Creates an arc from start point, end point, and sweep angle.
    /// </summary>
    public static VArc FromStartEndAngle(VXYZ start, VXYZ end, double sweepAngleDegrees)
    {
        double chordLength = start.DistanceTo(end);
        double sweepRad = Math.Abs(sweepAngleDegrees) * Math.PI / 180.0;
        double radius = chordLength / (2 * Math.Sin(sweepRad / 2));

        return FromStartEndRadius(start, end, radius, Math.Abs(sweepAngleDegrees) > 180);
    }

    /// <summary>
    /// Creates an arc tangent to a previous curve, continuing from its end point.
    /// </summary>
    public static VArc Continue(ICurve previous, double arcLength)
    {
        var start = previous.EndPoint;
        var tangent = previous.NormalAtPoint(start);
        var direction = new VXYZ(-tangent.Y, tangent.X, 0);

        double radius = arcLength / Math.PI;
        var center = new VXYZ(start.X - direction.X * radius, start.Y - direction.Y * radius);

        return FromStartCenterLength(start, center, arcLength);
    }

    private double NormalizePositive(double angle)
    {
        angle %= 360;
        if (angle <= 0) angle += 360;
        return angle;
    }

    private double NormalizeNegative(double angle)
    {
        angle %= 360;
        if (angle >= 0) angle -= 360;
        return angle;
    }

    /// <summary>
    /// Evaluates a point along the arc at the given normalized parameter.
    /// </summary>
    public VXYZ Evaluate(double parameter)
    {
        double startRad = StartAngle * Math.PI / 180.0;
        double endRad = EndAngle * Math.PI / 180.0;

        double angleRad = startRad + (endRad - startRad) * parameter;

        double x = Center.X + Radius * Math.Cos(angleRad);
        double y = Center.Y + Radius * Math.Sin(angleRad);
        return new VXYZ(x, y);
    }

    public VXYZ NormalAtPoint(VXYZ p)
    {
        return new VXYZ(p.X - Center.X, p.Y - Center.Y, 0).Normalize();
    }

    public double GetLength()
    {
        double angleDiff = Math.Abs(EndAngle - StartAngle);
        return Radius * angleDiff * Math.PI / 180.0;
    }

    public List<VXYZ> Divide(int numberOfSegments)
    {
        var points = new List<VXYZ>();
        if (numberOfSegments <= 0) return points;

        for (int i = 0; i <= numberOfSegments; i++)
        {
            points.Add(Evaluate((double)i / numberOfSegments));
        }
        return points;
    }

    public List<VXYZ> Measure(double segmentLength)
    {
        var points = new List<VXYZ>();
        if (segmentLength <= 0) return points;

        double totalLength = GetLength();
        if (totalLength < 1e-9)
        {
             points.Add(StartPoint);
             return points;
        }

        points.Add(StartPoint);

        double currentLength = segmentLength;
        while (currentLength <= totalLength)
        {
             points.Add(Evaluate(currentLength / totalLength));
             currentLength += segmentLength;
        }

        return points;
    }

    public VXYZ Project(VXYZ point)
    {
        VXYZ cp = point - Center;
        if (cp.IsZeroLength()) cp = new VXYZ(1, 0, 0);

        double angle = Math.Atan2(cp.Y, cp.X) * 180.0 / Math.PI;

        if (!IsAngleInArc(angle))
        {
            double distStart = GeometryHelper.AngleDifference(angle, StartAngle);
            double distEnd = GeometryHelper.AngleDifference(angle, EndAngle);
            angle = (distStart < distEnd) ? StartAngle : EndAngle;
        }

        double rad = angle * Math.PI / 180.0;
        return new VXYZ(Center.X + Radius * Math.Cos(rad), Center.Y + Radius * Math.Sin(rad));
    }

    private bool IsAngleInArc(double angle)
    {
        double s = GeometryHelper.NormalizeAngle(StartAngle);
        double e = GeometryHelper.NormalizeAngle(EndAngle);
        double a = GeometryHelper.NormalizeAngle(angle);

        if (s < e) return a >= s && a <= e;
        return a >= s || a <= e;
    }

    public VXYZ PointAtSegmentLength(double segmentLength)
    {
        double angleRad = segmentLength / Radius;
        double angleDeg = angleRad * 180.0 / Math.PI;

        double totalSweep = EndAngle - StartAngle;
        double dir = Math.Sign(totalSweep);
        if (dir == 0) dir = 1;

        double targetAngle = StartAngle + dir * angleDeg;

        if (Math.Abs(targetAngle - StartAngle) > Math.Abs(EndAngle - StartAngle))
            targetAngle = EndAngle;

        double rad = targetAngle * Math.PI / 180.0;
        return new VXYZ(Center.X + Radius * Math.Cos(rad), Center.Y + Radius * Math.Sin(rad));
    }

    public ICurve Offset(double distance)
    {
        double newRadius = Radius + distance;
        if (newRadius < 0) newRadius = 0;
        return new VArc(new VXYZ(Center.X, Center.Y), newRadius, StartAngle, EndAngle);
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
        var points = GeometryHelper.IntersectCircleCircle(Center, Radius, projected, chordLength);

        var results = new List<VXYZ>();
        foreach (var p in points)
        {
            VXYZ cp = p - Center;
            double angle = Math.Atan2(cp.Y, cp.X) * 180.0 / Math.PI;
            if (IsAngleInArc(angle)) results.Add(p);
        }
        return results;
    }

    public (ICurve, ICurve) SplitAtPoint(VXYZ point)
    {
        var proj = Project(point);
        VXYZ cp = proj - Center;
        double angle = Math.Atan2(cp.Y, cp.X) * 180.0 / Math.PI;

        return (
            new VArc(Center, Radius, StartAngle, angle),
            new VArc(Center, Radius, angle, EndAngle)
        );
    }



    public override List<ControlPoint> GetControlPoints()
    {
        var startPt = StartPoint;
        var endPt = EndPoint;
        double midAngleRad = (StartAngle + EndAngle) / 2.0 * Math.PI / 180.0;
        double radiusHandleX = Center.X + Radius * Math.Cos(midAngleRad);
        double radiusHandleY = Center.Y + Radius * Math.Sin(midAngleRad);

        return new List<ControlPoint>
        {
            new ControlPoint(ControlPointType.Move, Center.X, Center.Y, "Center"),
            new ControlPoint(ControlPointType.Radius, radiusHandleX, radiusHandleY, "Radius"),
            new ControlPoint(ControlPointType.Vertex, startPt.X, startPt.Y, "Start"),
            new ControlPoint(ControlPointType.Vertex, endPt.X, endPt.Y, "End")
        };
    }

    public override void MoveControlPoint(int index, VXYZ newPosition)
    {
        switch (index)
        {
            case 0: // Move center
                var delta = new VXYZ(newPosition.X - Center.X, newPosition.Y - Center.Y, 0);
                Move(delta);
                break;
            case 1: // Radius handle
                Radius = Center.DistanceTo(newPosition);
                break;
            case 2: // Start point - update start angle
                StartAngle = Math.Atan2(newPosition.Y - Center.Y, newPosition.X - Center.X) * 180.0 / Math.PI;
                Radius = Center.DistanceTo(newPosition);
                break;
            case 3: // End point - update end angle
                EndAngle = Math.Atan2(newPosition.Y - Center.Y, newPosition.X - Center.X) * 180.0 / Math.PI;
                Radius = Center.DistanceTo(newPosition);
                break;
        }
    }

    public override VArc Clone()
    {
        var clone = new VArc(Center.Clone(), Radius, StartAngle, EndAngle);
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
        StartAngle = GeometryHelper.NormalizeAngle(StartAngle + angleDegrees);
        EndAngle = GeometryHelper.NormalizeAngle(EndAngle + angleDegrees);
    }

    public override void Flip(VLine mirrorLine)
    {
        Center = GeometryHelper.FlipPoint(Center, mirrorLine);
        double temp = StartAngle;
        StartAngle = -EndAngle;
        EndAngle = -temp;
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

    public override string ToString() => $"VArc(Center: {Center}, R: {Radius}, {StartAngle}° to {EndAngle}°)";

    /// <summary>
    /// Computes the intersection between this arc and another curve.
    /// </summary>
    public IntersectionResult Intersect(ICurve other)
    {
        return CurveIntersection.Intersect(this, other);
    }

    /// <summary>
    /// Returns a point on the arc at the given normalized parameter.
    /// </summary>
    public VXYZ PointAtParameter(double parameter) => Evaluate(parameter);

    /// <summary>
    /// Returns the normalized parameter (0 to 1) for the closest point on the arc to the given point.
    /// </summary>
    public double ParameterAtPoint(VXYZ point)
    {
        double angle = Math.Atan2(point.Y - Center.Y, point.X - Center.X) * 180.0 / Math.PI;
        double startRad = StartAngle;
        double endRad = EndAngle;

        double sweep = endRad - startRad;
        double relativeAngle = angle - startRad;

        while (relativeAngle < 0) relativeAngle += 360;
        while (relativeAngle > 360) relativeAngle -= 360;

        if (sweep < 0)
        {
            if (relativeAngle > -sweep) relativeAngle = -sweep;
            return Math.Clamp(relativeAngle / -sweep, 0, 1);
        }
        else
        {
            if (relativeAngle > sweep) relativeAngle = sweep;
            return Math.Clamp(relativeAngle / sweep, 0, 1);
        }
    }
}
