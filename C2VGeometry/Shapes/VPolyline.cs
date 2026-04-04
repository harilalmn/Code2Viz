using System;
using System.Collections.Generic;
using System.Linq;

namespace C2VGeometry;

public class VPolyline : Shape, ICurve
{
    public List<VXYZ> Points { get; set; }
    private readonly bool _selfIntersecting;

    /// <summary>Gets the start point of the polyline.</summary>
    public VXYZ StartPoint => Points.Count > 0 ? Points[0] : new VXYZ(0, 0);

    /// <summary>Gets the end point of the polyline.</summary>
    public VXYZ EndPoint => Points.Count > 0 ? Points[^1] : new VXYZ(0, 0);

    /// <summary>Indicates whether the polyline intersects itself.</summary>
    public bool SelfIntersecting => _selfIntersecting;

    /// <summary>Gets the vertices of the polyline.</summary>
    public List<VXYZ> Vertices => Points;

    public VPolyline(params VXYZ[] points)
    {
        Points = points.ToList();
        Color = "LightGreen";
        _selfIntersecting = CurveIntersection.IsPolylineSelfIntersecting(Points);
    }

    public VPolyline(IEnumerable<VXYZ> points)
    {
        Points = points.ToList();
        Color = "LightGreen";
        _selfIntersecting = CurveIntersection.IsPolylineSelfIntersecting(Points);
    }

    public void AddPoint(VXYZ point)
    {
        Points.Add(point);
    }

    public void AddPoint(double x, double y)
    {
        Points.Add(new VXYZ(x, y));
    }



    public override List<ControlPoint> GetControlPoints()
    {
        var result = new List<ControlPoint>();
        if (Points.Count > 0)
        {
            double cx = Points.Average(p => p.X);
            double cy = Points.Average(p => p.Y);
            result.Add(new ControlPoint(ControlPointType.Move, cx, cy, "Center"));
        }
        for (int i = 0; i < Points.Count; i++)
        {
            result.Add(new ControlPoint(ControlPointType.Vertex, Points[i].X, Points[i].Y, $"P{i}"));
        }
        return result;
    }

    public override void MoveControlPoint(int index, VXYZ newPosition)
    {
        if (index == 0)
        {
            double cx = Points.Average(p => p.X);
            double cy = Points.Average(p => p.Y);
            var delta = new VXYZ(newPosition.X - cx, newPosition.Y - cy, 0);
            Move(delta);
        }
        else if (index > 0 && index <= Points.Count)
        {
            int ptIdx = index - 1;
            Points[ptIdx] = new VXYZ(newPosition.X, newPosition.Y);
        }
    }

    public override VPolyline Clone()
    {
        var clone = new VPolyline(Points.Select(p => p.Clone()));
        CopyStyleTo(clone);
        return clone;
    }

    public override void Move(VXYZ vector)
    {
        for (int i = 0; i < Points.Count; i++)
            Points[i] = Points[i] + vector;
    }

    public override void Rotate(VXYZ pivot, double angleDegrees)
    {
        for (int i = 0; i < Points.Count; i++)
            Points[i] = GeometryHelper.RotatePoint(Points[i], pivot, angleDegrees);
    }

    public override void Flip(VLine mirrorLine)
    {
        for (int i = 0; i < Points.Count; i++)
            Points[i] = GeometryHelper.FlipPoint(Points[i], mirrorLine);
    }

    public override void Scale(VXYZ center, double factor)
    {
        for (int i = 0; i < Points.Count; i++)
            Points[i] = GeometryHelper.ScalePoint(Points[i], center, factor);
    }

    public override BoundingBox GetBounds()
    {
        if (Points.Count == 0) return new BoundingBox(new VXYZ(0, 0), new VXYZ(0, 0));
        double minX = Points.Min(p => p.X), minY = Points.Min(p => p.Y);
        double maxX = Points.Max(p => p.X), maxY = Points.Max(p => p.Y);
        return new BoundingBox(new VXYZ(minX, minY), new VXYZ(maxX, maxY));
    }

    public override string ToString() => $"VPolyline({Points.Count} points)";

    public double GetLength()
    {
        double length = 0;
        for (int i = 0; i < Points.Count - 1; i++)
        {
            length += Points[i].DistanceTo(Points[i + 1]);
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
        if (segmentLength <= 1e-9 || Points.Count < 2) return result;

        result.Add(Points[0]);

        double remainingStep = segmentLength;

        for (int i = 0; i < Points.Count - 1; i++)
        {
            VXYZ p1 = Points[i];
            VXYZ p2 = Points[i + 1];
            double segLen = p1.DistanceTo(p2);

            double distOnSeg = 0;

            while (distOnSeg + remainingStep <= segLen + 1e-9)
            {
                distOnSeg += remainingStep;

                double t = distOnSeg / segLen;
                double x = p1.X + (p2.X - p1.X) * t;
                double y = p1.Y + (p2.Y - p1.Y) * t;
                result.Add(new VXYZ(x, y));

                remainingStep = segmentLength;
            }

            remainingStep -= (segLen - distOnSeg);
        }

        return result;
    }

    // ICurve Implementation

    public VXYZ Project(VXYZ point)
    {
        VXYZ closest = Points[0];
        double minK = double.MaxValue;

        for (int i = 0; i < Points.Count - 1; i++)
        {
            VXYZ p1 = Points[i];
            VXYZ p2 = Points[i+1];
            VXYZ proj = ProjectOnSegment(p1, p2, point);
            double d = proj.DistanceTo(point);
            if (d < minK)
            {
                minK = d;
                closest = proj;
            }
        }
        return closest;
    }

    private VXYZ ProjectOnSegment(VXYZ s, VXYZ e, VXYZ p)
    {
        var v = e - s;
        var u = p - s;
        var t = u.DotProduct(v) / v.DotProduct(v);
        if (t < 0) return s;
        if (t > 1) return e;
        return s + v * t;
    }

    public VXYZ PointAtSegmentLength(double segmentLength)
    {
        if (segmentLength <= 0) return Points.FirstOrDefault() ?? new VXYZ(0, 0);

        double currentLen = 0;
        for (int i = 0; i < Points.Count - 1; i++)
        {
            double d = Points[i].DistanceTo(Points[i+1]);
            if (currentLen + d >= segmentLength)
            {
                double rem = segmentLength - currentLen;
                double r = rem / d;
                return new VXYZ(
                    Points[i].X + (Points[i+1].X - Points[i].X) * r,
                    Points[i].Y + (Points[i+1].Y - Points[i].Y) * r
                );
            }
            currentLen += d;
        }
        return Points.LastOrDefault() ?? new VXYZ(0, 0);
    }

    public ICurve Offset(double distance)
    {
        if (Points.Count < 2) return (ICurve)this.Clone();

        var newPoints = new List<VXYZ>();

        bool isClosed = Points.Count > 2 && Points[0].DistanceTo(Points[Points.Count - 1]) < 1e-6;

        int pointCount = isClosed ? Points.Count - 1 : Points.Count;

        for (int i = 0; i < pointCount; i++)
        {
            VXYZ n1 = new VXYZ(0, 0, 0);
            VXYZ n2 = new VXYZ(0, 0, 0);

            int prevIdx = isClosed ? (i - 1 + pointCount) % pointCount : i - 1;
            if (i > 0 || isClosed)
            {
                var dir = (Points[i] - Points[prevIdx]).Normalize();
                n1 = new VXYZ(-dir.Y, dir.X, 0);
            }

            int nextIdx = isClosed ? (i + 1) % pointCount : i + 1;
            if (i < Points.Count - 1 || isClosed)
            {
                var dir = (Points[nextIdx] - Points[i]).Normalize();
                n2 = new VXYZ(-dir.Y, dir.X, 0);
            }

            VXYZ offsetVector;
            bool isEndpoint = !isClosed && (i == 0 || i == Points.Count - 1);

            if (isEndpoint)
            {
                offsetVector = (i == 0 ? n2 : n1) * distance;
            }
            else
            {
                var miterDir = (n1 + n2);
                double miterLength = miterDir.GetLength();

                if (miterLength < 1e-10)
                {
                    offsetVector = n1 * distance;
                }
                else
                {
                    miterDir = miterDir.Normalize();
                    double cosTheta = miterDir.DotProduct(n1);

                    const double miterLimit = 4.0;
                    double miterScale = 1.0 / Math.Max(cosTheta, 1.0 / miterLimit);

                    offsetVector = miterDir * distance * miterScale;
                }
            }

            newPoints.Add(Points[i] + offsetVector);
        }

        if (isClosed && newPoints.Count > 0)
        {
            newPoints.Add(newPoints[0]);
        }

        return new VPolyline(newPoints);
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
        VXYZ c = Project(point);
        double r2 = chordLength;

        for (int i=0; i<Points.Count-1; i++)
        {
             double d1 = Points[i].DistanceTo(c);
             double d2 = Points[i+1].DistanceTo(c);

             if ((d1 < r2 && d2 > r2) || (d1 > r2 && d2 < r2))
             {
                 VXYZ A = Points[i] - c;
                 VXYZ v = Points[i+1] - Points[i];

                 double qa = v.DotProduct(v);
                 double qb = 2 * A.DotProduct(v);
                 double qc = A.DotProduct(A) - r2 * r2;

                 double det = qb*qb - 4*qa*qc;
                 if (det >= 0)
                 {
                     double sqrtDet = Math.Sqrt(det);
                     double tA = (-qb - sqrtDet) / (2*qa);
                     double tB = (-qb + sqrtDet) / (2*qa);

                     if (tA >= 0 && tA <= 1) results.Add(Points[i] + v * tA);
                     if (tB >= 0 && tB <= 1) results.Add(Points[i] + v * tB);
                 }
             }
        }
        return results;
    }

    public (ICurve, ICurve) SplitAtPoint(VXYZ point)
    {
        VXYZ p = Project(point);

        int segmentIndex = -1;
        double minK = double.MaxValue;

        for (int i = 0; i < Points.Count - 1; i++)
        {
            VXYZ proj = ProjectOnSegment(Points[i], Points[i+1], p);
            double d = proj.DistanceTo(p);
            if (d < 1e-5)
            {
                segmentIndex = i;
                break;
            }
            if (d < minK)
            {
                minK = d;
                segmentIndex = i;
            }
        }

        if (segmentIndex == -1) segmentIndex = 0;

        var l1 = new List<VXYZ>();
        for(int i=0; i<=segmentIndex; i++) l1.Add(Points[i].Clone());
        l1.Add(p.Clone());

        var l2 = new List<VXYZ>();
        l2.Add(p.Clone());
        for(int i=segmentIndex+1; i<Points.Count; i++) l2.Add(Points[i].Clone());

        return (new VPolyline(l1), new VPolyline(l2));
    }

    public VXYZ NormalAtPoint(VXYZ p)
    {
        return GeometryHelper.GetPolylineNormalAtPoint(Points, p, false);
    }

    /// <summary>
    /// Computes the intersection between this polyline and another curve.
    /// </summary>
    public IntersectionResult Intersect(ICurve other)
    {
        return CurveIntersection.Intersect(this, other);
    }

    /// <summary>
    /// Returns a point on the polyline at the given normalized parameter.
    /// </summary>
    public VXYZ PointAtParameter(double parameter)
    {
        if (Points.Count == 0) return new VXYZ(0, 0);
        if (Points.Count == 1) return Points[0];
        if (parameter <= 0) return Points[0];
        if (parameter >= 1) return Points[^1];

        int numSegments = Points.Count - 1;
        double scaledT = parameter * numSegments;
        int segmentIndex = Math.Min((int)scaledT, numSegments - 1);
        double localT = scaledT - segmentIndex;

        VXYZ p1 = Points[segmentIndex];
        VXYZ p2 = Points[segmentIndex + 1];

        return new VXYZ(
            p1.X + (p2.X - p1.X) * localT,
            p1.Y + (p2.Y - p1.Y) * localT
        );
    }

    /// <summary>
    /// Returns the normalized parameter (0 to 1) for the closest point on the polyline to the given point.
    /// </summary>
    public double ParameterAtPoint(VXYZ point)
    {
        if (Points.Count == 0) return 0;
        if (Points.Count == 1) return 0;

        int numSegments = Points.Count - 1;
        double bestParam = 0;
        double bestDistSq = double.MaxValue;

        for (int i = 0; i < numSegments; i++)
        {
            var p1 = Points[i];
            var p2 = Points[i + 1];

            double dx = p2.X - p1.X;
            double dy = p2.Y - p1.Y;
            double lengthSq = dx * dx + dy * dy;

            double t;
            if (lengthSq < 1e-10)
            {
                t = 0;
            }
            else
            {
                t = Math.Clamp(((point.X - p1.X) * dx + (point.Y - p1.Y) * dy) / lengthSq, 0, 1);
            }

            double projX = p1.X + t * dx;
            double projY = p1.Y + t * dy;
            double distSq = (point.X - projX) * (point.X - projX) + (point.Y - projY) * (point.Y - projY);

            if (distSq < bestDistSq)
            {
                bestDistSq = distSq;
                bestParam = (i + t) / numSegments;
            }
        }

        return bestParam;
    }
}
