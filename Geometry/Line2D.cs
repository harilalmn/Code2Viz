using Code2Viz.Canvas;

namespace Code2Viz.Geometry;

public class VLine : Shape, ICurve
{
    public VPoint Start { get; set; }
    public VPoint End { get; set; }

    /// <summary>Gets the start point of the line.</summary>
    public VPoint StartPoint => Start;

    /// <summary>Gets the end point of the line.</summary>
    public VPoint EndPoint => End;

    /// <summary>A line is never self-intersecting.</summary>
    public bool SelfIntersecting => false;

    /// <summary>Gets the vertices of the line (start and end points).</summary>
    public List<VPoint> Vertices => new List<VPoint> { Start, End };

    /// <summary>Gets the midpoint of the line.</summary>
    public VPoint MidPoint => Evaluate(0.5);

    public VLine(VPoint start, VPoint end)
    {
        Start = start;
        End = end;
        Color = ShapeDefaults.GlobalColor ?? "Cyan";
    }

    public VLine(double x1, double y1, double x2, double y2)
    {
        // Use Internal() to avoid auto-registering intermediate points
        Start = VPoint.Internal(x1, y1);
        End = VPoint.Internal(x2, y2);
        Color = ShapeDefaults.GlobalColor ?? "Cyan";
    }

    /// <summary>
    /// Evaluates a point along the line at the given normalized parameter.
    /// </summary>
    public VPoint Evaluate(double parameter)
    {
        double x = Start.X + (End.X - Start.X) * parameter;
        double y = Start.Y + (End.Y - Start.Y) * parameter;
        // Use Internal() to avoid auto-registering intermediate points
        return VPoint.Internal(x, y);
    }

    public VXYZ NormalAtPoint(VPoint p)
    {
        double dx = End.X - Start.X;
        double dy = End.Y - Start.Y;
        return new VXYZ(dy, -dx, 0).Normalize();
    }

    public double GetLength()
    {
        return Start.DistanceTo(End);
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
        if (segmentLength <= 1e-9) return points;

        double totalLength = GetLength();
        if (totalLength < 1e-9) return points;

        int count = (int)(totalLength / segmentLength);
        for (int i = 0; i <= count; i++)
        {
            points.Add(Evaluate((i * segmentLength) / totalLength));
        }
        return points;
    }



    public override List<ControlPoint> GetControlPoints()
    {
        var mid = MidPoint;
        return new List<ControlPoint>
        {
            new ControlPoint(ControlPointType.Move, mid.X, mid.Y, "Center"),
            new ControlPoint(ControlPointType.Vertex, Start.X, Start.Y, "Start"),
            new ControlPoint(ControlPointType.Vertex, End.X, End.Y, "End")
        };
    }

    public override void MoveControlPoint(int index, VPoint newPosition)
    {
        switch (index)
        {
            case 0:
                var mid = MidPoint;
                var delta = new VXYZ(newPosition.X - mid.X, newPosition.Y - mid.Y, 0);
                Move(delta);
                break;
            case 1:
                Start.X = newPosition.X;
                Start.Y = newPosition.Y;
                break;
            case 2:
                End.X = newPosition.X;
                End.Y = newPosition.Y;
                break;
        }
    }

    public override Shape Clone()
    {
        var clone = new VLine((VPoint)Start.Clone(), (VPoint)End.Clone());
        CopyStyleTo(clone);
        return clone;
    }

    public override void Move(VXYZ vector)
    {
        Start.Move(vector);
        End.Move(vector);
    }

    public override void Rotate(VPoint pivot, double angleDegrees)
    {
        Start.Rotate(pivot, angleDegrees);
        End.Rotate(pivot, angleDegrees);
    }

    public override void Flip(VLine mirrorLine)
    {
        Start.Flip(mirrorLine);
        End.Flip(mirrorLine);
    }

    public override void Scale(VPoint center, double factor)
    {
        Start.Scale(center, factor);
        End.Scale(center, factor);
    }

    public override (VPoint min, VPoint max) GetBounds()
    {
        // Use Internal() to avoid auto-registering intermediate points
        return (
            VPoint.Internal(Math.Min(Start.X, End.X), Math.Min(Start.Y, End.Y)),
            VPoint.Internal(Math.Max(Start.X, End.X), Math.Max(Start.Y, End.Y))
        );
    }

    /// <summary> Gets the direction vector of the line. </summary>
    public VXYZ Direction => (End.AsVXYZ() - Start.AsVXYZ()).Normalize();

    public VPoint Project(VPoint point)
    {
        var v = End.AsVXYZ() - Start.AsVXYZ();
        var u = point.AsVXYZ() - Start.AsVXYZ();
        var t = u.DotProduct(v) / v.DotProduct(v);
        
        if (t < 0) return Start;
        if (t > 1) return End;
        
        return (Start.AsVXYZ() + v * t).AsVPoint();
    }

    public VPoint PointAtSegmentLength(double segmentLength)
    {
        var dir = Direction;
        return (Start.AsVXYZ() + dir * segmentLength).AsVPoint();
    }

    public ICurve Offset(double distance)
    {
        var normal = NormalAtPoint(Start); // Normal is constant for a line
        var offsetVector = normal * distance;
        
        return new VLine(
            (Start.AsVXYZ() + offsetVector).AsVPoint(),
            (End.AsVXYZ() + offsetVector).AsVPoint()
        );
    }

    public List<ICurve> Offset(List<double> distances)
    {
        var result = new List<ICurve>();
        foreach (var d in distances)
        {
            result.Add(Offset(d));
        }
        return result;
    }

    public List<VPoint> PointsAtChordLengthFromPoint(VPoint point, double chordLength)
    {
        // 1. Project point to curve to get reference point on curve
        var projected = Project(point);
        
        // 2. Find points at distance = chordLength from projected point along the line geometry
        // Since it's a line, we can just go forward and backward from the projected point
        var dir = Direction;
        var p1 = (projected.AsVXYZ() + dir * chordLength).AsVPoint();
        var p2 = (projected.AsVXYZ() - dir * chordLength).AsVPoint();

        var results = new List<VPoint>();
        
        // Check if points are within the line segment
        if (IsOnSegment(p1)) results.Add(p1);
        if (IsOnSegment(p2)) results.Add(p2);
        
        return results;
    }
    
    private bool IsOnSegment(VPoint p)
    {
        // Simple check: dist(Start, p) + dist(p, End) == dist(Start, End) within tolerance
        double d1 = Start.DistanceTo(p);
        double d2 = p.DistanceTo(End);
        double total = Start.DistanceTo(End);
        return GeometryTolerance.IsZero((d1 + d2) - total);
    }

    public (ICurve, ICurve) SplitAtPoint(VPoint point)
    {
        // Assuming point is on the line. If not, maybe we should project it first?
        // The requirement says "spliting a curve at a given point".
        // Robustness: Project point to line segment first.
        var splitPoint = Project(point);

        // Clone all points to ensure independent curves
        return (new VLine((VPoint)Start.Clone(), (VPoint)splitPoint.Clone()),
                new VLine((VPoint)splitPoint.Clone(), (VPoint)End.Clone()));
    }

    public override Shape? Intersect(Shape other)
    {
        if (other is VLine otherLine)
        {
            return GeometryHelper.IntersectLineLine(this, otherLine);
        }
        else if (other is VRectangle rect)
        {
            return GeometryHelper.IntersectLineRect(this, rect);
        }
        return base.Intersect(other);
    }

    /// <summary>
    /// Computes the intersection between this line and another curve.
    /// </summary>
    public IntersectionResult Intersect(ICurve other)
    {
        return CurveIntersection.Intersect(this, other);
    }

    /// <summary>
    /// Returns a point on the line at the given normalized parameter.
    /// </summary>
    public VPoint PointAtParameter(double parameter) => Evaluate(parameter);
}

