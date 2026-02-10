using Code2Viz.Canvas;

namespace Code2Viz.Geometry;

public class VArc : Shape, ICurve
{
    public VPoint Center { get; set; }
    public double Radius { get; set; }
    public double StartAngle { get; set; }  // In degrees
    public double EndAngle { get; set; }    // In degrees

    /// <summary>Gets the start point of the arc.</summary>
    public VPoint StartPoint => Evaluate(0);

    /// <summary>Gets the end point of the arc.</summary>
    public VPoint EndPoint => Evaluate(1);

    /// <summary>An arc is never self-intersecting.</summary>
    public bool SelfIntersecting => false;

    /// <summary>Gets the vertices of the arc (center, start point, end point).</summary>
    public List<VPoint> Vertices => new List<VPoint> { Center, StartPoint, EndPoint };

    /// <summary>Gets the midpoint of the arc.</summary>
    public VPoint MidPoint => Evaluate(0.5);

    public VArc(VPoint center, double radius, double startAngle, double endAngle)
    {
        Center = center;
        Radius = radius;
        StartAngle = startAngle;
        EndAngle = endAngle;
        Color = ShapeDefaults.GlobalColor ?? "Orange";
    }

    public VArc(double centerX, double centerY, double radius, double startAngle, double endAngle)
    {
        Center = VPoint.Internal(centerX, centerY);
        Radius = radius;
        StartAngle = startAngle;
        EndAngle = endAngle;
        Color = ShapeDefaults.GlobalColor ?? "Orange";
    }

    /// <summary>
    /// Creates an arc passing through three points.
    /// </summary>
    public VArc(VPoint start, VPoint mid, VPoint end)
    {
        // Check collinearity via determinant (2 * signed area)
        double D = 2 * (start.X * (mid.Y - end.Y) + mid.X * (end.Y - start.Y) + end.X * (start.Y - mid.Y));
        
        if (GeometryTolerance.IsZero(D))
        {
            // Collinear - theoretically a straight line/infinite radius. 
            // Fallback to a valid but flat arc or throw? 
            // Construct a flat arc (huge radius)? Or just throw.
            throw new ArgumentException("Points are collinear, cannot define a unique arc.");
        }

        // Calculate Center
        double s1 = start.X * start.X + start.Y * start.Y;
        double s2 = mid.X * mid.X + mid.Y * mid.Y;
        double s3 = end.X * end.X + end.Y * end.Y;

        double cx = (s1 * (mid.Y - end.Y) + s2 * (end.Y - start.Y) + s3 * (start.Y - mid.Y)) / D;
        double cy = (s1 * (end.X - mid.X) + s2 * (start.X - end.X) + s3 * (mid.X - start.X)) / D;
        
        Center = VPoint.Internal(cx, cy);
        Radius = Center.DistanceTo(start);
        Color = ShapeDefaults.GlobalColor ?? "Orange";

        // Calculate Angles
        double a1 = Math.Atan2(start.Y - cy, start.X - cx) * 180.0 / Math.PI;
        double a2 = Math.Atan2(mid.Y - cy, mid.X - cx) * 180.0 / Math.PI;
        double a3 = Math.Atan2(end.Y - cy, end.X - cx) * 180.0 / Math.PI;

        StartAngle = a1;

        // D > 0 implies CCW (in standard system). D < 0 implies CW.
        // We accumulate sweep from Start towards End via Mid.
        
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
    /// The arc goes from start to end through the shorter path.
    /// </summary>
    public static VArc FromStartCenterEnd(VPoint start, VPoint center, VPoint end)
    {
        double radius = center.DistanceTo(start);
        double startAngle = Math.Atan2(start.Y - center.Y, start.X - center.X) * 180.0 / Math.PI;
        double endAngle = Math.Atan2(end.Y - center.Y, end.X - center.X) * 180.0 / Math.PI;
        return new VArc(new VPoint(center.X, center.Y), radius, startAngle, endAngle);
    }

    /// <summary>
    /// Creates an arc from center, start point, and end point.
    /// Same as FromStartCenterEnd but with different parameter order.
    /// </summary>
    public static VArc FromCenterStartEnd(VPoint center, VPoint start, VPoint end)
    {
        return FromStartCenterEnd(start, center, end);
    }

    /// <summary>
    /// Creates an arc from start point, center, and sweep angle (in degrees).
    /// </summary>
    public static VArc FromStartCenterAngle(VPoint start, VPoint center, double sweepAngleDegrees)
    {
        double radius = center.DistanceTo(start);
        double startAngle = Math.Atan2(start.Y - center.Y, start.X - center.X) * 180.0 / Math.PI;
        double endAngle = startAngle + sweepAngleDegrees;
        return new VArc(new VPoint(center.X, center.Y), radius, startAngle, endAngle);
    }

    /// <summary>
    /// Creates an arc from center, start point, and sweep angle (in degrees).
    /// </summary>
    public static VArc FromCenterStartAngle(VPoint center, VPoint start, double sweepAngleDegrees)
    {
        return FromStartCenterAngle(start, center, sweepAngleDegrees);
    }

    /// <summary>
    /// Creates an arc from start point, center, and arc length.
    /// </summary>
    public static VArc FromStartCenterLength(VPoint start, VPoint center, double arcLength)
    {
        double radius = center.DistanceTo(start);
        double sweepAngleRad = arcLength / radius;
        double sweepAngleDeg = sweepAngleRad * 180.0 / Math.PI;
        return FromStartCenterAngle(start, center, sweepAngleDeg);
    }

    /// <summary>
    /// Creates an arc from center, start point, and arc length.
    /// </summary>
    public static VArc FromCenterStartLength(VPoint center, VPoint start, double arcLength)
    {
        return FromStartCenterLength(start, center, arcLength);
    }

    /// <summary>
    /// Creates an arc from start point, end point, and radius.
    /// </summary>
    /// <param name="largeArc">If true, creates the larger arc; otherwise the smaller arc.</param>
    public static VArc FromStartEndRadius(VPoint start, VPoint end, double radius, bool largeArc = false)
    {
        // Find the two possible centers
        double d = start.DistanceTo(end);
        if (d > 2 * radius)
            throw new ArgumentException("Radius too small for the given points.");

        double midX = (start.X + end.X) / 2.0;
        double midY = (start.Y + end.Y) / 2.0;

        // Distance from midpoint to center
        double h = Math.Sqrt(radius * radius - (d / 2.0) * (d / 2.0));

        // Perpendicular direction
        double dx = end.X - start.X;
        double dy = end.Y - start.Y;
        double perpX = -dy / d;
        double perpY = dx / d;

        // Two possible centers
        double cx1 = midX + h * perpX;
        double cy1 = midY + h * perpY;
        double cx2 = midX - h * perpX;
        double cy2 = midY - h * perpY;

        // Choose center based on largeArc flag
        // For simplicity, use center1 for small arc, center2 for large arc
        VPoint center = largeArc ? new VPoint(cx2, cy2) : new VPoint(cx1, cy1);

        double startAngle = Math.Atan2(start.Y - center.Y, start.X - center.X) * 180.0 / Math.PI;
        double endAngle = Math.Atan2(end.Y - center.Y, end.X - center.X) * 180.0 / Math.PI;

        return new VArc(center, radius, startAngle, endAngle);
    }

    /// <summary>
    /// Creates an arc from start point, end point, and sweep angle.
    /// </summary>
    public static VArc FromStartEndAngle(VPoint start, VPoint end, double sweepAngleDegrees)
    {
        // Calculate radius from chord length and sweep angle
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
        // Rotate 90 degrees to get tangent direction
        var direction = new VXYZ(-tangent.Y, tangent.X, 0);

        // Create arc with arbitrary radius, adjusted by arc length
        double radius = arcLength / Math.PI; // Semicircle as default
        var center = new VPoint(start.X - direction.X * radius, start.Y - direction.Y * radius);

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
    public VPoint Evaluate(double parameter)
    {
        double startRad = StartAngle * Math.PI / 180.0;
        double endRad = EndAngle * Math.PI / 180.0;
        
        // Handle wrapping if needed? Usually angle is not wrapped for Arc unless specified.
        // Assuming simple sweep for now.
        double angleRad = startRad + (endRad - startRad) * parameter;
        
        double x = Center.X + Radius * Math.Cos(angleRad);
        double y = Center.Y + Radius * Math.Sin(angleRad);
        return VPoint.Internal(x, y);
    }

    public VXYZ NormalAtPoint(VPoint p)
    {
        return new VXYZ(p.X - Center.X, p.Y - Center.Y, 0).Normalize();
    }

    public double GetLength()
    {
        double angleDiff = Math.Abs(EndAngle - StartAngle);
        return Radius * angleDiff * Math.PI / 180.0;
    }

    public List<VPoint> Divide(int numberOfSegments)
    {
        var points = new List<VPoint>();
        if (numberOfSegments <= 0) return points;

        for (int i = 0; i <= numberOfSegments; i++)
        {
            points.Add(Evaluate((double)i / numberOfSegments));
        }
        return points;
    }

    public List<VPoint> Measure(double segmentLength)
    {
        var points = new List<VPoint>();
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

    public VPoint Project(VPoint point)
    {
        // Vector from center to point
        VXYZ cp = (point.AsVXYZ() - Center.AsVXYZ());
        if (cp.IsZeroLength()) cp = new VXYZ(1, 0, 0); // Arbitrary if point is center
        
        // Angle of the point
        double angle = Math.Atan2(cp.Y, cp.X) * 180.0 / Math.PI;
        
        // Normalize angles to be compatible with Start/End sweep logic
        // This can be complex depending on how Start/End are defined (e.g., crossing 0/360)
        // For simplicity: Assuming StartAngle <= EndAngle logic handled in normalization
        
        // Clamp angle to arc range?
        // A simple way is to check if angle is within range, if not, snap to closest end.
        if (!IsAngleInArc(angle))
        {
            double distStart = GeometryHelper.AngleDifference(angle, StartAngle);
            double distEnd = GeometryHelper.AngleDifference(angle, EndAngle);
            angle = (distStart < distEnd) ? StartAngle : EndAngle;
        }

        double rad = angle * Math.PI / 180.0;
        return VPoint.Internal(Center.X + Radius * Math.Cos(rad), Center.Y + Radius * Math.Sin(rad));
    }

    private bool IsAngleInArc(double angle)
    {
        // Normalize all angles to [0, 360)
        double s = GeometryHelper.NormalizeAngle(StartAngle);
        double e = GeometryHelper.NormalizeAngle(EndAngle);
        double a = GeometryHelper.NormalizeAngle(angle);
        
        if (s < e) return a >= s && a <= e;
        return a >= s || a <= e; // Arc crosses 0
    }

    public VPoint PointAtSegmentLength(double segmentLength)
    {
        // L = R * theta(rad)
        // theta = L / R
        double angleRad = segmentLength / Radius;
        double angleDeg = angleRad * 180.0 / Math.PI;
        
        // Direction of sweep depends on EndAngle - StartAngle sign?
        // Usually arcs are CCW, but EndAngle can be smaller than StartAngle effectively?
        // Using explicit diff
        double totalSweep = EndAngle - StartAngle;
        double dir = Math.Sign(totalSweep);
        if (dir == 0) dir = 1;
        
        double targetAngle = StartAngle + dir * angleDeg;
        
        // Clamp to end
        if (Math.Abs(targetAngle - StartAngle) > Math.Abs(EndAngle - StartAngle))
            targetAngle = EndAngle;

        double rad = targetAngle * Math.PI / 180.0;
        return VPoint.Internal(Center.X + Radius * Math.Cos(rad), Center.Y + Radius * Math.Sin(rad));
    }

    public ICurve Offset(double distance)
    {
        double newRadius = Radius + distance;
        if (newRadius < 0) newRadius = 0; // Or flip?
        return new VArc(new VPoint(Center.X, Center.Y), newRadius, StartAngle, EndAngle);
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
        // Intersection of two circles: 
        // 1. Center=this.Center, Radius=this.Radius
        // 2. Center=projected, Radius=chordLength
        
        var points = GeometryHelper.IntersectCircleCircle(Center, Radius, projected, chordLength);
        
        // Filter points on arc
        var results = new List<VPoint>();
        foreach (var p in points)
        {
            VXYZ cp = (p.AsVXYZ() - Center.AsVXYZ());
            double angle = Math.Atan2(cp.Y, cp.X) * 180.0 / Math.PI;
            if (IsAngleInArc(angle)) results.Add(p);
        }
        return results;
    }

    public (ICurve, ICurve) SplitAtPoint(VPoint point)
    {
        var proj = Project(point);
        VXYZ cp = (proj.AsVXYZ() - Center.AsVXYZ());
        double angle = Math.Atan2(cp.Y, cp.X) * 180.0 / Math.PI;
        
        // Assuming angle is within arc
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

    public override void MoveControlPoint(int index, VPoint newPosition)
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
        Center.Move(vector);
    }

    public override void Rotate(VPoint pivot, double angleDegrees)
    {
        Center.Rotate(pivot, angleDegrees);
        StartAngle = GeometryHelper.NormalizeAngle(StartAngle + angleDegrees);
        EndAngle = GeometryHelper.NormalizeAngle(EndAngle + angleDegrees);
    }

    public override void Flip(VLine mirrorLine)
    {
        Center.Flip(mirrorLine);
        double temp = StartAngle;
        StartAngle = -EndAngle;
        EndAngle = -temp;
    }

    public override void Scale(VPoint center, double factor)
    {
        Center.Scale(center, factor);
        Radius *= Math.Abs(factor);
    }

    public override BoundingBox GetBounds()
    {
        return new BoundingBox(
            VPoint.Internal(Center.X - Radius, Center.Y - Radius),
            VPoint.Internal(Center.X + Radius, Center.Y + Radius)
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
    public VPoint PointAtParameter(double parameter) => Evaluate(parameter);

    /// <summary>
    /// Returns the normalized parameter (0 to 1) for the closest point on the arc to the given point.
    /// </summary>
    public double ParameterAtPoint(VPoint point)
    {
        double angle = Math.Atan2(point.Y - Center.Y, point.X - Center.X) * 180.0 / Math.PI;
        double startRad = StartAngle;
        double endRad = EndAngle;

        // Normalize angle to arc range
        double sweep = endRad - startRad;
        double relativeAngle = angle - startRad;

        // Handle angle wrapping
        while (relativeAngle < 0) relativeAngle += 360;
        while (relativeAngle > 360) relativeAngle -= 360;

        if (sweep < 0)
        {
            // Arc goes clockwise
            if (relativeAngle > -sweep) relativeAngle = -sweep;
            return Math.Clamp(relativeAngle / -sweep, 0, 1);
        }
        else
        {
            // Arc goes counter-clockwise
            if (relativeAngle > sweep) relativeAngle = sweep;
            return Math.Clamp(relativeAngle / sweep, 0, 1);
        }
    }
}

