using System;
using System.Collections.Generic;

namespace C2VGeometry;

public class VLine : Shape, ICurve
{
    public VXYZ Start { get; set; }
    public VXYZ End { get; set; }

    /// <summary>Gets the start point of the line.</summary>
    public VXYZ StartPoint => Start;

    /// <summary>Gets the end point of the line.</summary>
    public VXYZ EndPoint => End;

    /// <summary>A line is never self-intersecting.</summary>
    public bool SelfIntersecting => false;

    /// <summary>Gets the vertices of the line (start and end points).</summary>
    public List<VXYZ> Vertices => new List<VXYZ> { Start, End };

    /// <summary>Gets the midpoint of the line.</summary>
    public VXYZ MidPoint => Evaluate(0.5);

    public VLine(VXYZ start, VXYZ end)
    {
        Start = start;
        End = end;
        Color = Shape.DefaultColor;
    }

    internal VLine(VXYZ start, VXYZ end, bool register) : base(register)
    {
        Start = start;
        End = end;
        Color = Shape.DefaultColor;
    }

    /// <summary>
    /// Creates an internal VLine that is not auto-registered with the default
    /// registry. Use in utility code (e.g. GetSegments, tessellation,
    /// self-intersection checks) where the VLine is just a data container.
    /// </summary>
    internal static VLine Internal(VXYZ start, VXYZ end) => new VLine(start, end, false);

    public VLine(double x1, double y1, double x2, double y2)
    {
        Start = new VXYZ(x1, y1);
        End = new VXYZ(x2, y2);
        Color = Shape.DefaultColor;
    }

    public VLine(VXYZ startPoint, double angleInDegrees, double length)
    {
        double radians = angleInDegrees * Math.PI / 180.0;
        Start = startPoint;
        End = new VXYZ(startPoint.X + length * Math.Cos(radians), startPoint.Y + length * Math.Sin(radians));
        Color = Shape.DefaultColor;
    }

    /// <summary>
    /// Evaluates a point along the line at the given normalized parameter.
    /// </summary>
    public VXYZ Evaluate(double parameter)
    {
        double x = Start.X + (End.X - Start.X) * parameter;
        double y = Start.Y + (End.Y - Start.Y) * parameter;
        return new VXYZ(x, y);
    }

    public VXYZ NormalAtPoint(VXYZ p)
    {
        double dx = End.X - Start.X;
        double dy = End.Y - Start.Y;
        return new VXYZ(dy, -dx, 0).Normalize();
    }

    public double GetLength()
    {
        return Start.DistanceTo(End);
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

    public override void MoveControlPoint(int index, VXYZ newPosition)
    {
        switch (index)
        {
            case 0:
                var mid = MidPoint;
                var delta = new VXYZ(newPosition.X - mid.X, newPosition.Y - mid.Y, 0);
                Move(delta);
                break;
            case 1:
                Start = new VXYZ(newPosition.X, newPosition.Y);
                break;
            case 2:
                End = new VXYZ(newPosition.X, newPosition.Y);
                break;
        }
    }

    public override VLine Clone()
    {
        var clone = new VLine(Start.Clone(), End.Clone());
        CopyStyleTo(clone);
        return clone;
    }

    public override void Move(VXYZ vector)
    {
        Start = Start + vector;
        End = End + vector;
    }

    public override void Rotate(VXYZ pivot, double angleDegrees)
    {
        Start = GeometryHelper.RotatePoint(Start, pivot, angleDegrees);
        End = GeometryHelper.RotatePoint(End, pivot, angleDegrees);
    }

    public override void Flip(VLine mirrorLine)
    {
        Start = GeometryHelper.FlipPoint(Start, mirrorLine);
        End = GeometryHelper.FlipPoint(End, mirrorLine);
    }

    public override void Scale(VXYZ center, double factor)
    {
        Start = GeometryHelper.ScalePoint(Start, center, factor);
        End = GeometryHelper.ScalePoint(End, center, factor);
    }

    public override BoundingBox GetBounds()
    {
        return new BoundingBox(
            new VXYZ(Math.Min(Start.X, End.X), Math.Min(Start.Y, End.Y)),
            new VXYZ(Math.Max(Start.X, End.X), Math.Max(Start.Y, End.Y))
        );
    }

    /// <summary> Gets the direction vector of the line. </summary>
    public VXYZ Direction => (End - Start).Normalize();

    public VXYZ Project(VXYZ point)
    {
        var v = End - Start;
        var u = point - Start;
        var t = u.DotProduct(v) / v.DotProduct(v);

        if (t < 0) return Start;
        if (t > 1) return End;

        return Start + v * t;
    }

    public VXYZ PointAtSegmentLength(double segmentLength)
    {
        var dir = Direction;
        return Start + dir * segmentLength;
    }

    public ICurve Offset(double distance)
    {
        var normal = NormalAtPoint(Start); // Normal is constant for a line
        var offsetVector = normal * distance;

        return new VLine(Start + offsetVector, End + offsetVector);
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

    public List<VXYZ> PointsAtChordLengthFromPoint(VXYZ point, double chordLength)
    {
        // 1. Project point to curve to get reference point on curve
        var projected = Project(point);

        // 2. Find points at distance = chordLength from projected point along the line geometry
        var dir = Direction;
        var p1 = projected + dir * chordLength;
        var p2 = projected - dir * chordLength;

        var results = new List<VXYZ>();

        // Check if points are within the line segment
        if (IsOnSegment(p1)) results.Add(p1);
        if (IsOnSegment(p2)) results.Add(p2);

        return results;
    }

    private bool IsOnSegment(VXYZ p)
    {
        // Simple check: dist(Start, p) + dist(p, End) == dist(Start, End) within tolerance
        double d1 = Start.DistanceTo(p);
        double d2 = p.DistanceTo(End);
        double total = Start.DistanceTo(End);
        return GeometryTolerance.IsZero((d1 + d2) - total);
    }

    public (ICurve, ICurve) SplitAtPoint(VXYZ point)
    {
        var splitPoint = Project(point);

        return (new VLine(Start.Clone(), splitPoint.Clone()),
                new VLine(splitPoint.Clone(), End.Clone()));
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
    public VXYZ PointAtParameter(double parameter) => Evaluate(parameter);

    /// <summary>
    /// Returns the normalized parameter (0 to 1) for the closest point on the line to the given point.
    /// </summary>
    public double ParameterAtPoint(VXYZ point)
    {
        double dx = End.X - Start.X;
        double dy = End.Y - Start.Y;
        double lengthSq = dx * dx + dy * dy;
        if (lengthSq < 1e-10) return 0;

        double t = ((point.X - Start.X) * dx + (point.Y - Start.Y) * dy) / lengthSq;
        return Math.Clamp(t, 0, 1);
    }
}
