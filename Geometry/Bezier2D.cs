using Code2Viz.Canvas;

namespace Code2Viz.Geometry;

/// <summary>
/// A cubic Bezier curve defined by 4 control points.
/// </summary>
public class VBezier : Shape, ICurve
{
    public VPoint P0 { get; set; }  // Start point
    public VPoint P1 { get; set; }  // Control point 1
    public VPoint P2 { get; set; }  // Control point 2
    public VPoint P3 { get; set; }  // End point
    private bool _selfIntersecting;

    /// <summary>Number of segments for rendering (higher = smoother)</summary>
    public int Segments { get; set; } = 32;

    public VPoint StartPoint => P0;
    public VPoint EndPoint => P3;
    public VPoint MidPoint => Evaluate(0.5);

    /// <summary>Indicates whether the bezier curve intersects itself.</summary>
    public bool SelfIntersecting => _selfIntersecting;

    /// <summary>Gets the control points of the bezier curve.</summary>
    public List<VPoint> Vertices => new List<VPoint> { P0, P1, P2, P3 };

    public VBezier(VPoint p0, VPoint p1, VPoint p2, VPoint p3)
    {
        P0 = p0;
        P1 = p1;
        P2 = p2;
        P3 = p3;
        Color = ShapeDefaults.GlobalColor ?? "Purple";
        _selfIntersecting = CurveIntersection.IsSelfIntersecting(this);
    }

    public VBezier(double x0, double y0, double x1, double y1, double x2, double y2, double x3, double y3)
    {
        P0 = VPoint.Internal(x0, y0);
        P1 = VPoint.Internal(x1, y1);
        P2 = VPoint.Internal(x2, y2);
        P3 = VPoint.Internal(x3, y3);
        Color = ShapeDefaults.GlobalColor ?? "Purple";
        _selfIntersecting = CurveIntersection.IsSelfIntersecting(this);
    }

    /// <summary>
    /// Evaluates a point on the Bezier curve at parameter t [0,1].
    /// </summary>
    public VPoint Evaluate(double t)
    {
        double u = 1 - t;
        double tt = t * t;
        double uu = u * u;
        double uuu = uu * u;
        double ttt = tt * t;

        double x = uuu * P0.X + 3 * uu * t * P1.X + 3 * u * tt * P2.X + ttt * P3.X;
        double y = uuu * P0.Y + 3 * uu * t * P1.Y + 3 * u * tt * P2.Y + ttt * P3.Y;

        return VPoint.Internal(x, y);
    }

    /// <summary>
    /// Gets all points along the curve for rendering.
    /// </summary>
    public List<VPoint> GetRenderPoints()
    {
        var points = new List<VPoint>();
        for (int i = 0; i <= Segments; i++)
        {
            double t = (double)i / Segments;
            points.Add(Evaluate(t));
        }
        return points;
    }



    public override List<ControlPoint> GetControlPoints()
    {
        var mid = MidPoint;
        return new List<ControlPoint>
        {
            new ControlPoint(ControlPointType.Move, mid.X, mid.Y, "Center"),
            new ControlPoint(ControlPointType.Vertex, P0.X, P0.Y, "P0"),
            new ControlPoint(ControlPointType.CurveControl, P1.X, P1.Y, "P1"),
            new ControlPoint(ControlPointType.CurveControl, P2.X, P2.Y, "P2"),
            new ControlPoint(ControlPointType.Vertex, P3.X, P3.Y, "P3")
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
                P0.X = newPosition.X;
                P0.Y = newPosition.Y;
                break;
            case 2:
                P1.X = newPosition.X;
                P1.Y = newPosition.Y;
                break;
            case 3:
                P2.X = newPosition.X;
                P2.Y = newPosition.Y;
                break;
            case 4:
                P3.X = newPosition.X;
                P3.Y = newPosition.Y;
                break;
        }
    }

    public override VBezier Clone()
    {
        var clone = new VBezier(P0.Clone(), P1.Clone(), P2.Clone(), P3.Clone())
        {
            Segments = Segments
        };
        CopyStyleTo(clone);
        return clone;
    }

    public override void Move(VXYZ vector)
    {
        P0.Move(vector);
        P1.Move(vector);
        P2.Move(vector);
        P3.Move(vector);
    }

    public override void Rotate(VPoint pivot, double angleDegrees)
    {
        P0.Rotate(pivot, angleDegrees);
        P1.Rotate(pivot, angleDegrees);
        P2.Rotate(pivot, angleDegrees);
        P3.Rotate(pivot, angleDegrees);
    }

    public override void Flip(VLine mirrorLine)
    {
        P0.Flip(mirrorLine);
        P1.Flip(mirrorLine);
        P2.Flip(mirrorLine);
        P3.Flip(mirrorLine);
    }

    public override void Scale(VPoint center, double factor)
    {
        P0.Scale(center, factor);
        P1.Scale(center, factor);
        P2.Scale(center, factor);
        P3.Scale(center, factor);
    }

    public override BoundingBox GetBounds()
    {
        var pts = new[] { P0, P1, P2, P3 };
        return new BoundingBox(
            VPoint.Internal(pts.Min(p => p.X), pts.Min(p => p.Y)),
            VPoint.Internal(pts.Max(p => p.X), pts.Max(p => p.Y))
        );
    }

    public override string ToString() => $"VBezier({P0} -> {P3})";

    public double GetLength()
    {
        // Approximate length by subdividing
        double length = 0;
        VPoint prev = P0;
        int steps = 100;
        for (int i = 1; i <= steps; i++)
        {
            VPoint curr = Evaluate((double)i / steps);
            length += prev.DistanceTo(curr);
            prev = curr;
        }
        return length;
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

        result.Add(Evaluate(0)); // Start point

        double remainingStep = segmentLength;
        VPoint p1 = Evaluate(0);
        int steps = 200; // Resolution for measurement
        
        for (int i = 1; i <= steps; i++)
        {
            double t = (double)i / steps;
            VPoint p2 = Evaluate(t);
            double segLen = p1.DistanceTo(p2);
            
            double distOnSeg = 0;
            while (distOnSeg + remainingStep <= segLen + 1e-9)
            {
                distOnSeg += remainingStep;
                
                // Interpolate
                double subT = distOnSeg / segLen;
                double x = p1.X + (p2.X - p1.X) * subT;
                double y = p1.Y + (p2.Y - p1.Y) * subT;
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
        // Walk for length
        // This is expensive if done repeatedly. 
        // Cached length parameterization would be better.
        
        double totalLen = GetLength();
        if (segmentLength <= 0) return P0;
        if (segmentLength >= totalLen) return P3;

        // Binary search or walk w/ Measure
        // Let's use 100 sample walk
        int steps = 100;
        double len = 0;
        VPoint prev = Evaluate(0);
        for(int i=1; i<=steps; i++)
        {
            double t = (double)i / steps;
            VPoint curr = Evaluate(t);
            double segDescrip = prev.DistanceTo(curr);
            if (len + segDescrip >= segmentLength)
            {
                // Interpolate t
                double rem = segmentLength - len;
                double ratio = rem / segDescrip;
                double targetT = ((i-1) + ratio) / steps;
                return Evaluate(targetT);
            }
            len += segDescrip;
            prev = curr;
        }
        return P3;
    }

    public ICurve Offset(double distance)
    {
        // Simple approximation: Offset control points
        // Tiller-Hansonish approach: Offset legs
        
        // Tangents
        VXYZ t0 = (P1.AsVXYZ() - P0.AsVXYZ()).Normalize();
        if (t0.IsZeroLength()) t0 = (P2.AsVXYZ() - P0.AsVXYZ()).Normalize();
        
        VXYZ t3 = (P3.AsVXYZ() - P2.AsVXYZ()).Normalize();
        if (t3.IsZeroLength()) t3 = (P3.AsVXYZ() - P1.AsVXYZ()).Normalize();
        
        // Normals (2D: -y, x)
        VXYZ n0 = new VXYZ(-t0.Y, t0.X, 0).Normalize();
        VXYZ n3 = new VXYZ(-t3.Y, t3.X, 0).Normalize();
        
        // Offset start/end
        VPoint q0 = (P0.AsVXYZ() + n0 * distance).AsVPoint();
        VPoint q3 = (P3.AsVXYZ() + n3 * distance).AsVPoint();
        
        // For P1, P2: Intersect offset tangent lines? 
        // Or just move by normal at endpoints?
        // Simple shift:
        VPoint q1 = (P1.AsVXYZ() + n0 * distance).AsVPoint();
        VPoint q2 = (P2.AsVXYZ() + n3 * distance).AsVPoint();
        
        return new VBezier(q0, q1, q2, q3);
    }

    public List<ICurve> Offset(List<double> distances)
    {
        var list = new List<ICurve>();
        foreach(var d in distances) list.Add(Offset(d));
        return list;
    }

    public List<VPoint> PointsAtChordLengthFromPoint(VPoint point, double chordLength)
    {
        // Numerical circle intersection
        var results = new List<VPoint>();
        int steps = 100;
        VPoint prev = Evaluate(0);
        double r2 = chordLength;
        VPoint c2 = Project(point); 
        
        for(int i=1; i<=steps; i++){
             VPoint curr = Evaluate((double)i/steps);
             double d1 = curr.DistanceTo(c2);
             double d2 = prev.DistanceTo(c2);
             
             if ((d1 < r2 && d2 > r2) || (d1 > r2 && d2 < r2))
             {
                 results.Add(VPoint.Internal((curr.X+prev.X)/2, (curr.Y+prev.Y)/2));
             }
             prev = curr;
        }
        return results;
    }

    public (ICurve, ICurve) SplitAtPoint(VPoint point)
    {
        double t = GetClosestParameter(point);
        // De Casteljau subdivision at t

        // Clone original control points to ensure independent curves
        VPoint p0 = (VPoint)P0.Clone();
        VPoint p1 = P1;
        VPoint p2 = P2;
        VPoint p3 = (VPoint)P3.Clone();

        VPoint p01 = Lerp(p0, p1, t);
        VPoint p12 = Lerp(p1, p2, t);
        VPoint p23 = Lerp(p2, p3, t);

        VPoint p012 = Lerp(p01, p12, t);
        VPoint p123 = Lerp(p12, p23, t);

        VPoint p0123 = Lerp(p012, p123, t); // This is point at t

        // Clone shared split point for second curve
        var c1 = new VBezier(p0, p01, p012, (VPoint)p0123.Clone());
        var c2 = new VBezier((VPoint)p0123.Clone(), p123, p23, p3);

        return (c1, c2);
    }
    
    private VPoint Lerp(VPoint a, VPoint b, double t)
    {
        return VPoint.Internal(
            a.X + (b.X - a.X) * t,
            a.Y + (b.Y - a.Y) * t
        );
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
        int steps = 100;
        
        for (int i = 0; i <= steps; i++)
        {
            double t = (double)i / steps;
            double dx = Evaluate(t).X - p.X;
            double dy = Evaluate(t).Y - p.Y;
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
        double u = 1 - t;
        double uu = u * u;
        double tt = t * t;
        double tu = t * u;
        
        // Derivative of cubic Bezier: 
        // 3(1-t)^2(P1-P0) + 6(1-t)t(P2-P1) + 3t^2(P3-P2)
        
        double term1 = 3 * uu;
        double term2 = 6 * u * t; // Note: was 6 * tu
        double term3 = 3 * tt;
        
        double x = term1 * (P1.X - P0.X) + term2 * (P2.X - P1.X) + term3 * (P3.X - P2.X);
        double y = term1 * (P1.Y - P0.Y) + term2 * (P2.Y - P1.Y) + term3 * (P3.Y - P2.Y);

        return VPoint.Internal(x, y);
    }

    /// <summary>
    /// Computes the intersection between this bezier curve and another curve.
    /// </summary>
    public IntersectionResult Intersect(ICurve other)
    {
        return CurveIntersection.Intersect(this, other);
    }

    /// <summary>
    /// Returns a point on the bezier curve at the given normalized parameter.
    /// </summary>
    public VPoint PointAtParameter(double parameter) => Evaluate(parameter);

    /// <summary>
    /// Returns the normalized parameter (0 to 1) for the closest point on the bezier curve to the given point.
    /// </summary>
    public double ParameterAtPoint(VPoint point) => GetClosestParameter(point);

    /// <summary>
    /// Trims this Bezier in place so that the parameter range [startParameter, endParameter]
    /// becomes the new [0, 1] range. Uses De Casteljau subdivision: split at endParameter to get
    /// the left piece, then split that piece at startParameter/endParameter and keep its right piece.
    /// Control point P0..P3 instances are preserved (their X/Y are updated).
    /// </summary>
    public void SetBounds(double startParameter, double endParameter)
    {
        double s = Math.Clamp(startParameter, 0.0, 1.0);
        double e = Math.Clamp(endParameter, 0.0, 1.0);
        if (s > e) (s, e) = (e, s);

        // Step 1: split at e, keep left piece -> control points cover original [0, e].
        var p01 = Lerp(P0, P1, e);
        var p12 = Lerp(P1, P2, e);
        var p23 = Lerp(P2, P3, e);
        var p012 = Lerp(p01, p12, e);
        var p123 = Lerp(p12, p23, e);
        var p0123 = Lerp(p012, p123, e);
        // Left piece control points: (P0, p01, p012, p0123)
        var l1 = p01;
        var l2 = p012;
        var l3 = p0123;

        // Step 2: within left piece, split at u = s/e, keep right piece -> control points cover [s, e].
        double u = e > 1e-12 ? s / e : 0;
        var q01 = Lerp(P0, l1, u);
        var q12 = Lerp(l1, l2, u);
        var q23 = Lerp(l2, l3, u);
        var q012 = Lerp(q01, q12, u);
        var q123 = Lerp(q12, q23, u);
        var q0123 = Lerp(q012, q123, u);
        // Right piece control points: (q0123, q123, q23, l3)

        P0.X = q0123.X; P0.Y = q0123.Y;
        P1.X = q123.X;  P1.Y = q123.Y;
        P2.X = q23.X;   P2.Y = q23.Y;
        P3.X = l3.X;    P3.Y = l3.Y;

        _selfIntersecting = CurveIntersection.IsSelfIntersecting(this);
    }
}
