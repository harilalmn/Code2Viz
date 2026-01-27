using Code2Viz.Canvas;

namespace Code2Viz.Geometry;

/// <summary>
/// A Catmull-Rom spline that passes through all control points.
/// </summary>
public class VSpline : Shape, ICurve
{
    public List<VPoint> ControlPoints { get; set; }
    private readonly bool _selfIntersecting;

    /// <summary>Number of segments between each pair of control points</summary>
    public int SegmentsPerSpan { get; set; } = 16;

    /// <summary>Tension parameter (0 = sharp corners, 0.5 = standard Catmull-Rom)</summary>
    public double Tension { get; set; } = 0.5;

    public VPoint StartPoint => ControlPoints.Count > 0 ? ControlPoints[0] : VPoint.Internal(0, 0);
    public VPoint EndPoint => ControlPoints.Count > 0 ? ControlPoints[^1] : VPoint.Internal(0, 0);

    /// <summary>Indicates whether the spline intersects itself.</summary>
    public bool SelfIntersecting => _selfIntersecting;

    /// <summary>Gets the control points of the spline.</summary>
    public List<VPoint> Vertices => ControlPoints;

    public VSpline(params VPoint[] points)
    {
        ControlPoints = points.ToList();
        Color = ShapeDefaults.GlobalColor ?? "Violet";
        _selfIntersecting = CurveIntersection.IsSelfIntersecting(this);
    }

    public VSpline(IEnumerable<VPoint> points)
    {
        ControlPoints = points.ToList();
        Color = ShapeDefaults.GlobalColor ?? "Violet";
        _selfIntersecting = CurveIntersection.IsSelfIntersecting(this);
    }

    /// <summary>
    /// Evaluates a point on the spline at parameter t [0, 1].
    /// </summary>
    public VPoint Evaluate(double t)
    {
        // Use Internal() to avoid auto-registering intermediate points
        if (ControlPoints.Count < 2) return ControlPoints.FirstOrDefault() ?? VPoint.Internal(0, 0);
        if (ControlPoints.Count == 2)
        {
            // Linear interpolation for 2 points
            return VPoint.Internal(
                ControlPoints[0].X + t * (ControlPoints[1].X - ControlPoints[0].X),
                ControlPoints[0].Y + t * (ControlPoints[1].Y - ControlPoints[0].Y));
        }

        // Find which segment t falls into
        int numSpans = ControlPoints.Count - 1;
        double scaledT = t * numSpans;
        int spanIndex = Math.Min((int)scaledT, numSpans - 1);
        double localT = scaledT - spanIndex;

        return EvaluateSpan(spanIndex, localT);
    }

    private VPoint EvaluateSpan(int spanIndex, double t)
    {
        // Get 4 points for Catmull-Rom (p0, p1, p2, p3)
        var p0 = ControlPoints[Math.Max(0, spanIndex - 1)];
        var p1 = ControlPoints[spanIndex];
        var p2 = ControlPoints[Math.Min(ControlPoints.Count - 1, spanIndex + 1)];
        var p3 = ControlPoints[Math.Min(ControlPoints.Count - 1, spanIndex + 2)];

        double t2 = t * t;
        double t3 = t2 * t;

        // Catmull-Rom basis functions
        double x = Tension * (
            (-t3 + 2 * t2 - t) * p0.X +
            (3 * t3 - 5 * t2 + 2) * p1.X +
            (-3 * t3 + 4 * t2 + t) * p2.X +
            (t3 - t2) * p3.X);

        double y = Tension * (
            (-t3 + 2 * t2 - t) * p0.Y +
            (3 * t3 - 5 * t2 + 2) * p1.Y +
            (-3 * t3 + 4 * t2 + t) * p2.Y +
            (t3 - t2) * p3.Y);

        // Use Internal() to avoid auto-registering intermediate points
        return VPoint.Internal(x, y);
    }

    /// <summary>
    /// Gets all points along the spline for rendering.
    /// </summary>
    public List<VPoint> GetRenderPoints()
    {
        var points = new List<VPoint>();
        if (ControlPoints.Count < 2)
        {
            points.AddRange(ControlPoints);
            return points;
        }

        int totalSegments = (ControlPoints.Count - 1) * SegmentsPerSpan;
        for (int i = 0; i <= totalSegments; i++)
        {
            double t = (double)i / totalSegments;
            points.Add(Evaluate(t));
        }
        return points;
    }



    public override List<ControlPoint> GetControlPoints()
    {
        var result = new List<ControlPoint>();
        if (ControlPoints.Count > 0)
        {
            double cx = ControlPoints.Average(p => p.X);
            double cy = ControlPoints.Average(p => p.Y);
            result.Add(new ControlPoint(ControlPointType.Move, cx, cy, "Center"));
        }
        for (int i = 0; i < ControlPoints.Count; i++)
        {
            result.Add(new ControlPoint(ControlPointType.CurveControl, ControlPoints[i].X, ControlPoints[i].Y, $"CP{i}"));
        }
        return result;
    }

    public override void MoveControlPoint(int index, VPoint newPosition)
    {
        if (index == 0)
        {
            double cx = ControlPoints.Average(p => p.X);
            double cy = ControlPoints.Average(p => p.Y);
            var delta = new VXYZ(newPosition.X - cx, newPosition.Y - cy, 0);
            Move(delta);
        }
        else if (index > 0 && index <= ControlPoints.Count)
        {
            int ptIdx = index - 1;
            ControlPoints[ptIdx].X = newPosition.X;
            ControlPoints[ptIdx].Y = newPosition.Y;
        }
    }

    public override Shape Clone()
    {
        var clone = new VSpline(ControlPoints.Select(p => (VPoint)p.Clone()))
        {
            SegmentsPerSpan = SegmentsPerSpan,
            Tension = Tension
        };
        CopyStyleTo(clone);
        return clone;
    }

    public override void Move(VXYZ vector)
    {
        foreach (var p in ControlPoints) p.Move(vector);
    }

    public override void Rotate(VPoint pivot, double angleDegrees)
    {
        foreach (var p in ControlPoints) p.Rotate(pivot, angleDegrees);
    }

    public override void Flip(VLine mirrorLine)
    {
        foreach (var p in ControlPoints) p.Flip(mirrorLine);
    }

    public override void Scale(VPoint center, double factor)
    {
        foreach (var p in ControlPoints) p.Scale(center, factor);
    }

    public override (VPoint min, VPoint max) GetBounds()
    {
        // Use Internal() to avoid auto-registering intermediate points
        if (ControlPoints.Count == 0) return (VPoint.Internal(0, 0), VPoint.Internal(0, 0));
        double minX = ControlPoints.Min(p => p.X), minY = ControlPoints.Min(p => p.Y);
        double maxX = ControlPoints.Max(p => p.X), maxY = ControlPoints.Max(p => p.Y);
        return (VPoint.Internal(minX, minY), VPoint.Internal(maxX, maxY));
    }

    public override string ToString() => $"VSpline({ControlPoints.Count} control points)";

    public double GetLength()
    {
        // Approximated length
        var pts = GetRenderPoints();
        double len = 0;
        for (int i = 0; i < pts.Count - 1; i++)
        {
            len += pts[i].DistanceTo(pts[i + 1]);
        }
        return len;
    }

    public List<VPoint> Divide(int numberOfSegments)
    {
        if (numberOfSegments <= 0) return new List<VPoint>();
        double totalLength = GetLength();
        if (totalLength < 1e-9) return new List<VPoint>();
        
        return Measure(totalLength / numberOfSegments);
    }

    public List<VPoint> Measure(double segmentLength)
    {
        var result = new List<VPoint>();
        if (segmentLength <= 1e-9) return result;

        result.Add(Evaluate(0));

        double remainingStep = segmentLength;

        // Use high-resolution points for measurement
        int totalSteps = (ControlPoints.Count - 1) * 32; // same as render points or higher
        VPoint p1 = Evaluate(0);

        for (int i = 1; i <= totalSteps; i++)
        {
            double t = (double)i / totalSteps;
            VPoint p2 = Evaluate(t);
            double segLen = p1.DistanceTo(p2);

            double distOnSeg = 0;

            while (distOnSeg + remainingStep <= segLen + 1e-9)
            {
                distOnSeg += remainingStep;

                double subT = distOnSeg / segLen;
                double x = p1.X + (p2.X - p1.X) * subT;
                double y = p1.Y + (p2.Y - p1.Y) * subT;
                // Use Internal() to avoid auto-registering intermediate points
                result.Add(VPoint.Internal(x, y));

                remainingStep = segmentLength;
            }

            remainingStep -= (segLen - distOnSeg);
            p1 = p2;
        }

        return result;
    }

    // ICurve Implementation

    public VPoint Project(VPoint point)
    {
        double t = GetClosestParameter(point);
        return Evaluate(t);
    }

    public VPoint PointAtSegmentLength(double segmentLength)
    {
        // Simple walk
        double totalLen = GetLength();
        if (segmentLength <= 0) return StartPoint;
        if (segmentLength >= totalLen) return EndPoint;

        var points = GetRenderPoints();
        double currentLen = 0;

        for (int i = 0; i < points.Count - 1; i++)
        {
            double d = points[i].DistanceTo(points[i+1]);
            if (currentLen + d >= segmentLength)
            {
                double rem = segmentLength - currentLen;
                double r = rem / d;
                // Use Internal() to avoid auto-registering intermediate points
                return VPoint.Internal(
                    points[i].X + (points[i+1].X - points[i].X) * r,
                    points[i].Y + (points[i+1].Y - points[i].Y) * r
                );
            }
            currentLen += d;
        }
        return EndPoint;
    }

    public ICurve Offset(double distance)
    {
        // Offset control points by normal at that point
        var newPoints = new List<VPoint>();
        
        for (int i = 0; i < ControlPoints.Count; i++)
        {
            VPoint p = ControlPoints[i];
            
            // Estimate tangent
            VXYZ tangent;
            if (i == 0)
            {
                if (ControlPoints.Count > 1) 
                    tangent = (ControlPoints[1].AsVXYZ() - p.AsVXYZ()).Normalize();
                else 
                    tangent = new VXYZ(1, 0, 0);
            }
            else if (i == ControlPoints.Count - 1)
            {
                if (ControlPoints.Count > 1)
                    tangent = (p.AsVXYZ() - ControlPoints[i-1].AsVXYZ()).Normalize();
                else
                    tangent = new VXYZ(1, 0, 0);
            }
            else
            {
                // Catmull-Rom tangent is parallel to P(i+1) - P(i-1)
                tangent = (ControlPoints[i+1].AsVXYZ() - ControlPoints[i-1].AsVXYZ()).Normalize();
            }
            
            // Normal (-y, x)
            VXYZ normal = new VXYZ(-tangent.Y, tangent.X, 0).Normalize();
            
            newPoints.Add((p.AsVXYZ() + normal * distance).AsVPoint());
        }
        
        return new VSpline(newPoints)
        {
             SegmentsPerSpan = SegmentsPerSpan,
             Tension = Tension
        };
    }

    public List<ICurve> Offset(List<double> distances)
    {
        var list = new List<ICurve>();
        foreach (var d in distances) list.Add(Offset(d));
        return list;
    }

    public List<VPoint> PointsAtChordLengthFromPoint(VPoint point, double chordLength)
    {
        // Use high-res polyline approximation
        var poly = GetRenderPoints();
        var results = new List<VPoint>();
        VPoint c = Project(point);
        double r2 = chordLength;

        for (int i = 0; i < poly.Count - 1; i++)
        {
            // Intersect segment with circle
            // Simplified check
            double d1 = poly[i].DistanceTo(c);
            double d2 = poly[i + 1].DistanceTo(c);

            if ((d1 < r2 && d2 > r2) || (d1 > r2 && d2 < r2))
            {
                // Use Internal() to avoid auto-registering intermediate points
                results.Add(VPoint.Internal((poly[i].X + poly[i + 1].X) / 2, (poly[i].Y + poly[i + 1].Y) / 2));
            }
        }
        return results;
    }

    public (ICurve, ICurve) SplitAtPoint(VPoint point)
    {
        // This is destructive to shape, but split logic:
        // Find segment index
        // We know how Evaluate works: scaledT = t * (count-1)
        double t = GetClosestParameter(point);
        int numSpans = ControlPoints.Count - 1;
        double scaledT = t * numSpans;
        int spanIndex = Math.Min((int)scaledT, numSpans - 1);

        var splitP = Evaluate(t);

        // Curve 1: P0 ... P(spanIndex), splitP
        // Curve 2: splitP, P(spanIndex+1) ... Pn

        // Clone all points to ensure independent curves
        var list1 = new List<VPoint>();
        for(int i=0; i<=spanIndex; i++) list1.Add((VPoint)ControlPoints[i].Clone());
        list1.Add((VPoint)splitP.Clone());

        var list2 = new List<VPoint>();
        list2.Add((VPoint)splitP.Clone());
        for(int i=spanIndex+1; i<ControlPoints.Count; i++) list2.Add((VPoint)ControlPoints[i].Clone());

        return (new VSpline(list1), new VSpline(list2));
    }

    public VXYZ NormalAtPoint(VPoint p)
    {
        double t = GetClosestParameter(p);
        VPoint tangent = EvaluateDerivative(t);
        return new VXYZ(-tangent.Y, tangent.X, 0).Normalize();
    }

    private double GetClosestParameter(VPoint p)
    {
        double minSqDist = double.MaxValue;
        double bestT = 0;
        
        // Scan depending on complexity
        int steps = Math.Max(100, ControlPoints.Count * 20); // Adaptive steps
        
        for (int i = 0; i <= steps; i++)
        {
            double t = (double)i / steps;
            VPoint pt = Evaluate(t);
            double dx = pt.X - p.X;
            double dy = pt.Y - p.Y;
            double sqDist = dx * dx + dy * dy;
            
            if (sqDist < minSqDist)
            {
                minSqDist = sqDist;
                bestT = t;
            }
        }
        return bestT;
    }

    private VPoint EvaluateDerivative(double t)
    {
        // Use Internal() to avoid auto-registering intermediate points
        if (ControlPoints.Count < 2) return VPoint.Internal(0, 0);

        // Find span
        int numSpans = ControlPoints.Count - 1;
        double scaledT = t * numSpans;
        int spanIndex = Math.Min((int)scaledT, numSpans - 1);
        double localT = scaledT - spanIndex;

        return EvaluateSpanDerivative(spanIndex, localT);
    }

    private VPoint EvaluateSpanDerivative(int spanIndex, double t)
    {
        var p0 = ControlPoints[Math.Max(0, spanIndex - 1)];
        var p1 = ControlPoints[spanIndex];
        var p2 = ControlPoints[Math.Min(ControlPoints.Count - 1, spanIndex + 1)];
        var p3 = ControlPoints[Math.Min(ControlPoints.Count - 1, spanIndex + 2)];

        double t2 = t * t;

        // Derivative of Catmull-Rom basis functions
        // Original: -t^3 + 2t^2 - t
        double d1 = -3 * t2 + 4 * t - 1;
        // Original: 3t^3 - 5t^2 + 2
        double d2 = 9 * t2 - 10 * t;
        // Original: -3t^3 + 4t^2 + t
        double d3 = -9 * t2 + 8 * t + 1;
        // Original: t^3 - t^2
        double d4 = 3 * t2 - 2 * t;

        double x = Tension * (d1 * p0.X + d2 * p1.X + d3 * p2.X + d4 * p3.X);
        double y = Tension * (d1 * p0.Y + d2 * p1.Y + d3 * p2.Y + d4 * p3.Y);

        // Use Internal() to avoid auto-registering intermediate points
        return VPoint.Internal(x, y);
    }

    /// <summary>
    /// Computes the intersection between this spline and another curve.
    /// </summary>
    public IntersectionResult Intersect(ICurve other)
    {
        return CurveIntersection.Intersect(this, other);
    }

    /// <summary>
    /// Returns a point on the spline at the given normalized parameter.
    /// </summary>
    public VPoint PointAtParameter(double parameter) => Evaluate(parameter);
}
