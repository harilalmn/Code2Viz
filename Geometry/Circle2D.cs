using Code2Viz.Canvas;

namespace Code2Viz.Geometry;

public class VCircle : Shape, ICurve
{
    public VPoint Center { get; set; }
    public double Radius { get; set; }

    public VCircle(VPoint center, double radius)
    {
        Center = center;
        Radius = radius;
        StrokeColor = ShapeDefaults.GlobalStrokeColor ?? "Yellow";
    }

    public VCircle(double centerX, double centerY, double radius)
    {
        Center = new VPoint(centerX, centerY);
        Radius = radius;
        StrokeColor = ShapeDefaults.GlobalStrokeColor ?? "Yellow";
    }

    public override void Draw()
    {
        CanvasRenderer.Instance.AddShape(this);
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
            new VPoint(Center.X - Radius, Center.Y - Radius),
            new VPoint(Center.X + Radius, Center.Y + Radius)
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
        return new VPoint(x, y);
    }

    public VPoint Project(VPoint point)
    {
        VXYZ cp = (point.AsVXYZ() - Center.AsVXYZ());
        if (cp.IsZeroLength()) cp = new VXYZ(1, 0, 0);
        
        double angle = Math.Atan2(cp.Y, cp.X);
        return new VPoint(Center.X + Radius * Math.Cos(angle), Center.Y + Radius * Math.Sin(angle));
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
}

