using System;
using System.Collections.Generic;
using System.Linq;

namespace C2VGeometry;

/// <summary>
/// A cubic Bezier curve defined by 4 control points.
/// </summary>
public class VBezier : Shape, ICurve
{
    public VXYZ P0 { get; set; }  // Start point
    public VXYZ P1 { get; set; }  // Control point 1
    public VXYZ P2 { get; set; }  // Control point 2
    public VXYZ P3 { get; set; }  // End point
    private bool _selfIntersecting;

    /// <summary>Number of segments for rendering (higher = smoother)</summary>
    public int Segments { get; set; } = 32;

    public VXYZ StartPoint => P0;
    public VXYZ EndPoint => P3;
    public VXYZ MidPoint => Evaluate(0.5);

    /// <summary>Indicates whether the bezier curve intersects itself.</summary>
    public bool SelfIntersecting => _selfIntersecting;

    /// <summary>Gets the control points of the bezier curve.</summary>
    public List<VXYZ> Vertices => new List<VXYZ> { P0, P1, P2, P3 };

    public VBezier(VXYZ p0, VXYZ p1, VXYZ p2, VXYZ p3)
    {
        P0 = p0;
        P1 = p1;
        P2 = p2;
        P3 = p3;
        Color = "Purple";
        _selfIntersecting = CurveIntersection.IsSelfIntersecting(this);
    }

    public VBezier(double x0, double y0, double x1, double y1, double x2, double y2, double x3, double y3)
    {
        P0 = new VXYZ(x0, y0);
        P1 = new VXYZ(x1, y1);
        P2 = new VXYZ(x2, y2);
        P3 = new VXYZ(x3, y3);
        Color = "Purple";
        _selfIntersecting = CurveIntersection.IsSelfIntersecting(this);
    }

    /// <summary>
    /// Evaluates a point on the Bezier curve at parameter t [0,1].
    /// </summary>
    public VXYZ Evaluate(double t)
    {
        double u = 1 - t;
        double tt = t * t;
        double uu = u * u;
        double uuu = uu * u;
        double ttt = tt * t;

        double x = uuu * P0.X + 3 * uu * t * P1.X + 3 * u * tt * P2.X + ttt * P3.X;
        double y = uuu * P0.Y + 3 * uu * t * P1.Y + 3 * u * tt * P2.Y + ttt * P3.Y;

        return new VXYZ(x, y);
    }

    /// <summary>
    /// Gets all points along the curve for rendering.
    /// </summary>
    public List<VXYZ> GetRenderPoints()
    {
        var points = new List<VXYZ>();
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
                P0 = new VXYZ(newPosition.X, newPosition.Y);
                break;
            case 2:
                P1 = new VXYZ(newPosition.X, newPosition.Y);
                break;
            case 3:
                P2 = new VXYZ(newPosition.X, newPosition.Y);
                break;
            case 4:
                P3 = new VXYZ(newPosition.X, newPosition.Y);
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
        P0 = P0 + vector;
        P1 = P1 + vector;
        P2 = P2 + vector;
        P3 = P3 + vector;
    }

    public override void Rotate(VXYZ pivot, double angleDegrees)
    {
        P0 = GeometryHelper.RotatePoint(P0, pivot, angleDegrees);
        P1 = GeometryHelper.RotatePoint(P1, pivot, angleDegrees);
        P2 = GeometryHelper.RotatePoint(P2, pivot, angleDegrees);
        P3 = GeometryHelper.RotatePoint(P3, pivot, angleDegrees);
    }

    public override void Flip(VLine mirrorLine)
    {
        P0 = GeometryHelper.FlipPoint(P0, mirrorLine);
        P1 = GeometryHelper.FlipPoint(P1, mirrorLine);
        P2 = GeometryHelper.FlipPoint(P2, mirrorLine);
        P3 = GeometryHelper.FlipPoint(P3, mirrorLine);
    }

    public override void Scale(VXYZ center, double factor)
    {
        P0 = GeometryHelper.ScalePoint(P0, center, factor);
        P1 = GeometryHelper.ScalePoint(P1, center, factor);
        P2 = GeometryHelper.ScalePoint(P2, center, factor);
        P3 = GeometryHelper.ScalePoint(P3, center, factor);
    }

    public override BoundingBox GetBounds()
    {
        var pts = new[] { P0, P1, P2, P3 };
        return new BoundingBox(
            new VXYZ(pts.Min(p => p.X), pts.Min(p => p.Y)),
            new VXYZ(pts.Max(p => p.X), pts.Max(p => p.Y))
        );
    }

    public override string ToString() => $"VBezier({P0} -> {P3})";

    public double GetLength()
    {
        double length = 0;
        VXYZ prev = P0;
        int steps = 100;
        for (int i = 1; i <= steps; i++)
        {
            VXYZ curr = Evaluate((double)i / steps);
            length += prev.DistanceTo(curr);
            prev = curr;
        }
        return length;
    }

    public List<VXYZ> Divide(int numberOfSegments)
    {
        if (numberOfSegments <= 0) return new List<VXYZ>();
        double totalLength = GetLength();
        if (totalLength < 1e-9) return new List<VXYZ>();

        return Measure(totalLength / numberOfSegments);
    }

    public List<VXYZ> Measure(double segmentLength)
    {
        var result = new List<VXYZ>();
        if (segmentLength <= 1e-9) return result;

        result.Add(Evaluate(0));

        double remainingStep = segmentLength;
        VXYZ p1 = Evaluate(0);
        int steps = 200;

        for (int i = 1; i <= steps; i++)
        {
            double t = (double)i / steps;
            VXYZ p2 = Evaluate(t);
            double segLen = p1.DistanceTo(p2);

            double distOnSeg = 0;
            while (distOnSeg + remainingStep <= segLen + 1e-9)
            {
                distOnSeg += remainingStep;

                double subT = distOnSeg / segLen;
                double x = p1.X + (p2.X - p1.X) * subT;
                double y = p1.Y + (p2.Y - p1.Y) * subT;
                result.Add(new VXYZ(x, y));

                remainingStep = segmentLength;
            }

            remainingStep -= (segLen - distOnSeg);
            p1 = p2;
        }

        return result;
    }

    // ICurve Implementation

    public VXYZ Project(VXYZ point)
    {
        double t = GetClosestParameter(point);
        return Evaluate(t);
    }

    public VXYZ PointAtSegmentLength(double segmentLength)
    {
        double totalLen = GetLength();
        if (segmentLength <= 0) return P0;
        if (segmentLength >= totalLen) return P3;

        int steps = 100;
        double len = 0;
        VXYZ prev = Evaluate(0);
        for(int i=1; i<=steps; i++)
        {
            double t = (double)i / steps;
            VXYZ curr = Evaluate(t);
            double segDescrip = prev.DistanceTo(curr);
            if (len + segDescrip >= segmentLength)
            {
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
        VXYZ t0 = (P1 - P0).Normalize();
        if (t0.IsZeroLength()) t0 = (P2 - P0).Normalize();

        VXYZ t3 = (P3 - P2).Normalize();
        if (t3.IsZeroLength()) t3 = (P3 - P1).Normalize();

        VXYZ n0 = new VXYZ(-t0.Y, t0.X, 0).Normalize();
        VXYZ n3 = new VXYZ(-t3.Y, t3.X, 0).Normalize();

        VXYZ q0 = P0 + n0 * distance;
        VXYZ q3 = P3 + n3 * distance;
        VXYZ q1 = P1 + n0 * distance;
        VXYZ q2 = P2 + n3 * distance;

        return new VBezier(q0, q1, q2, q3);
    }

    public List<ICurve> Offset(List<double> distances)
    {
        var list = new List<ICurve>();
        foreach(var d in distances) list.Add(Offset(d));
        return list;
    }

    public List<VXYZ> PointsAtChordLengthFromPoint(VXYZ point, double chordLength)
    {
        var results = new List<VXYZ>();
        int steps = 100;
        VXYZ prev = Evaluate(0);
        double r2 = chordLength;
        VXYZ c2 = Project(point);

        for(int i=1; i<=steps; i++){
             VXYZ curr = Evaluate((double)i/steps);
             double d1 = curr.DistanceTo(c2);
             double d2 = prev.DistanceTo(c2);

             if ((d1 < r2 && d2 > r2) || (d1 > r2 && d2 < r2))
             {
                 results.Add(new VXYZ((curr.X+prev.X)/2, (curr.Y+prev.Y)/2));
             }
             prev = curr;
        }
        return results;
    }

    public (ICurve, ICurve) SplitAtPoint(VXYZ point)
    {
        double t = GetClosestParameter(point);

        VXYZ p0 = P0.Clone();
        VXYZ p1 = P1;
        VXYZ p2 = P2;
        VXYZ p3 = P3.Clone();

        VXYZ p01 = Lerp(p0, p1, t);
        VXYZ p12 = Lerp(p1, p2, t);
        VXYZ p23 = Lerp(p2, p3, t);

        VXYZ p012 = Lerp(p01, p12, t);
        VXYZ p123 = Lerp(p12, p23, t);

        VXYZ p0123 = Lerp(p012, p123, t);

        var c1 = new VBezier(p0, p01, p012, p0123.Clone());
        var c2 = new VBezier(p0123.Clone(), p123, p23, p3);

        return (c1, c2);
    }

    private VXYZ Lerp(VXYZ a, VXYZ b, double t)
    {
        return new VXYZ(
            a.X + (b.X - a.X) * t,
            a.Y + (b.Y - a.Y) * t
        );
    }

    public VXYZ NormalAtPoint(VXYZ p)
    {
        double t = GetClosestParameter(p);
        VXYZ tangent = EvaluateDerivative(t);
        return new VXYZ(-tangent.Y, tangent.X, 0).Normalize();
    }

    private double GetClosestParameter(VXYZ p)
    {
        double minSqDist = double.MaxValue;
        double bestT = 0;
        int steps = 100;

        for (int i = 0; i <= steps; i++)
        {
            double t = (double)i / steps;
            var pt = Evaluate(t);
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

    private VXYZ EvaluateDerivative(double t)
    {
        double u = 1 - t;
        double uu = u * u;
        double tt = t * t;

        double term1 = 3 * uu;
        double term2 = 6 * u * t;
        double term3 = 3 * tt;

        double x = term1 * (P1.X - P0.X) + term2 * (P2.X - P1.X) + term3 * (P3.X - P2.X);
        double y = term1 * (P1.Y - P0.Y) + term2 * (P2.Y - P1.Y) + term3 * (P3.Y - P2.Y);

        return new VXYZ(x, y);
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
    public VXYZ PointAtParameter(double parameter) => Evaluate(parameter);

    /// <summary>
    /// Returns the normalized parameter (0 to 1) for the closest point on the bezier curve to the given point.
    /// </summary>
    public double ParameterAtPoint(VXYZ point) => GetClosestParameter(point);

    /// <summary>
    /// Trims this Bezier in place so that the parameter range [startParameter, endParameter]
    /// becomes the new [0, 1] range. Uses De Casteljau subdivision: split at endParameter to get
    /// the left piece, then split that piece at startParameter/endParameter and keep its right piece.
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

        P0 = q0123;
        P1 = q123;
        P2 = q23;
        P3 = l3;

        _selfIntersecting = CurveIntersection.IsSelfIntersecting(this);
    }
}
