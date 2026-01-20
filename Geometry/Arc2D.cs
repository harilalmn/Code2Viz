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

    /// <summary>Gets the midpoint of the arc.</summary>
    public VPoint MidPoint => Evaluate(0.5);

    public VArc(VPoint center, double radius, double startAngle, double endAngle)
    {
        Center = center;
        Radius = radius;
        StartAngle = startAngle;
        EndAngle = endAngle;
        StrokeColor = ShapeDefaults.GlobalStrokeColor ?? "Orange";
    }

    public VArc(double centerX, double centerY, double radius, double startAngle, double endAngle)
    {
        Center = new VPoint(centerX, centerY);
        Radius = radius;
        StartAngle = startAngle;
        EndAngle = endAngle;
        StrokeColor = ShapeDefaults.GlobalStrokeColor ?? "Orange";
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
        
        Center = new VPoint(cx, cy);
        Radius = Center.DistanceTo(start);
        StrokeColor = ShapeDefaults.GlobalStrokeColor ?? "Orange";

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
        return new VPoint(x, y);
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
        return new VPoint(Center.X + Radius * Math.Cos(rad), Center.Y + Radius * Math.Sin(rad));
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
        return new VPoint(Center.X + Radius * Math.Cos(rad), Center.Y + Radius * Math.Sin(rad));
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

    public override void Draw()
    {
        CanvasRenderer.Instance.AddShape(this);
    }

    public override Shape Clone()
    {
        var clone = new VArc((VPoint)Center.Clone(), Radius, StartAngle, EndAngle);
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

    public override (VPoint min, VPoint max) GetBounds()
    {
        return (
            new VPoint(Center.X - Radius, Center.Y - Radius),
            new VPoint(Center.X + Radius, Center.Y + Radius)
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
}

