using Code2Viz.Canvas;

namespace Code2Viz.Geometry;

public class VPolyline : Shape, ICurve
{
    public List<VPoint> Points { get; set; }
    private readonly bool _selfIntersecting;

    /// <summary>Gets the start point of the polyline.</summary>
    public VPoint StartPoint => Points.Count > 0 ? Points[0] : VPoint.Internal(0, 0);

    /// <summary>Gets the end point of the polyline.</summary>
    public VPoint EndPoint => Points.Count > 0 ? Points[^1] : VPoint.Internal(0, 0);

    /// <summary>Indicates whether the polyline intersects itself.</summary>
    public bool SelfIntersecting => _selfIntersecting;

    /// <summary>Gets the vertices of the polyline.</summary>
    public List<VPoint> Vertices => Points;

    public VPolyline(params VPoint[] points)
    {
        Points = points.ToList();
        Color = ShapeDefaults.GlobalColor ?? "LightGreen";
        _selfIntersecting = CurveIntersection.IsPolylineSelfIntersecting(Points);
    }

    public VPolyline(IEnumerable<VPoint> points)
    {
        Points = points.ToList();
        Color = ShapeDefaults.GlobalColor ?? "LightGreen";
        _selfIntersecting = CurveIntersection.IsPolylineSelfIntersecting(Points);
    }

    public void AddPoint(VPoint point)
    {
        Points.Add(point);
    }

    public void AddPoint(double x, double y)
    {
        Points.Add(VPoint.Internal(x, y));
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

    public override void MoveControlPoint(int index, VPoint newPosition)
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
            Points[ptIdx].X = newPosition.X;
            Points[ptIdx].Y = newPosition.Y;
        }
    }

    public override Shape Clone()
    {
        var clone = new VPolyline(Points.Select(p => (VPoint)p.Clone()));
        CopyStyleTo(clone);
        return clone;
    }

    public override void Move(VXYZ vector)
    {
        foreach (var point in Points)
            point.Move(vector);
    }

    public override void Rotate(VPoint pivot, double angleDegrees)
    {
        foreach (var point in Points)
            point.Rotate(pivot, angleDegrees);
    }

    public override void Flip(VLine mirrorLine)
    {
        foreach (var point in Points)
            point.Flip(mirrorLine);
    }

    public override void Scale(VPoint center, double factor)
    {
        foreach (var point in Points)
            point.Scale(center, factor);
    }

    public override (VPoint min, VPoint max) GetBounds()
    {
        if (Points.Count == 0) return (VPoint.Internal(0, 0), VPoint.Internal(0, 0));
        double minX = Points.Min(p => p.X), minY = Points.Min(p => p.Y);
        double maxX = Points.Max(p => p.X), maxY = Points.Max(p => p.Y);
        return (VPoint.Internal(minX, minY), VPoint.Internal(maxX, maxY));
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
        if (segmentLength <= 1e-9 || Points.Count < 2) return result;

        // Always include start
        result.Add(Points[0]);


        double remainingStep = segmentLength;
        
        for (int i = 0; i < Points.Count - 1; i++)
        {
            VPoint p1 = Points[i];
            VPoint p2 = Points[i + 1];
            double segLen = p1.DistanceTo(p2);
            
            double distOnSeg = 0;
            
            // While we can fit another step on this segment (including carry-over)
            while (distOnSeg + remainingStep <= segLen + 1e-9)
            {
                distOnSeg += remainingStep;
                
                // Interpolate
                double t = distOnSeg / segLen;
                double x = p1.X + (p2.X - p1.X) * t;
                double y = p1.Y + (p2.Y - p1.Y) * t;
                result.Add(VPoint.Internal(x, y));
                
                // Reset step for next point
                remainingStep = segmentLength;
            }
            
            // Whatever is left on this segment reduces the step needed for next segment
            remainingStep -= (segLen - distOnSeg);
        }

        return result;
    }

    // ICurve Implementation

    public VPoint Project(VPoint point)
    {
        // Find closest point on any segment
        VPoint closest = Points[0];
        double minK = double.MaxValue;
        
        for (int i = 0; i < Points.Count - 1; i++)
        {
            VPoint p1 = Points[i];
            VPoint p2 = Points[i+1];
            VPoint proj = ProjectOnSegment(p1, p2, point);
            double d = proj.DistanceTo(point);
            if (d < minK)
            {
                minK = d;
                closest = proj;
            }
        }
        return closest;
    }
    
    private VPoint ProjectOnSegment(VPoint s, VPoint e, VPoint p)
    {
        var v = e.AsVXYZ() - s.AsVXYZ();
        var u = p.AsVXYZ() - s.AsVXYZ();
        var t = u.DotProduct(v) / v.DotProduct(v);
        if (t < 0) return s;
        if (t > 1) return e;
        return (s.AsVXYZ() + v * t).AsVPoint();
    }

    public VPoint PointAtSegmentLength(double segmentLength)
    {
        if (segmentLength <= 0) return Points.FirstOrDefault() ?? VPoint.Internal(0, 0);
        
        double currentLen = 0;
        for (int i = 0; i < Points.Count - 1; i++)
        {
            double d = Points[i].DistanceTo(Points[i+1]);
            if (currentLen + d >= segmentLength)
            {
                double rem = segmentLength - currentLen;
                double r = rem / d;
                return VPoint.Internal(
                    Points[i].X + (Points[i+1].X - Points[i].X) * r,
                    Points[i].Y + (Points[i+1].Y - Points[i].Y) * r
                );
            }
            currentLen += d;
        }
        return Points.LastOrDefault() ?? VPoint.Internal(0, 0);
    }

    public ICurve Offset(double distance)
    {
        if (Points.Count < 2) return (ICurve)this.Clone();

        var newPoints = new List<VPoint>();

        // Check if polyline is closed (first and last points are the same)
        bool isClosed = Points.Count > 2 && Points[0].DistanceTo(Points[Points.Count - 1]) < 1e-6;

        // For closed polylines, don't process the duplicate end point
        int pointCount = isClosed ? Points.Count - 1 : Points.Count;

        // Offset by vertex normal with proper miter calculation at corners
        for (int i = 0; i < pointCount; i++)
        {
            VXYZ n1 = new VXYZ(0, 0, 0);
            VXYZ n2 = new VXYZ(0, 0, 0);

            // Get normal from previous segment
            int prevIdx = isClosed ? (i - 1 + pointCount) % pointCount : i - 1;
            if (i > 0 || isClosed)
            {
                var dir = (Points[i].AsVXYZ() - Points[prevIdx].AsVXYZ()).Normalize();
                n1 = new VXYZ(-dir.Y, dir.X, 0); // Normal left
            }

            // Get normal from next segment
            int nextIdx = isClosed ? (i + 1) % pointCount : i + 1;
            if (i < Points.Count - 1 || isClosed)
            {
                var dir = (Points[nextIdx].AsVXYZ() - Points[i].AsVXYZ()).Normalize();
                n2 = new VXYZ(-dir.Y, dir.X, 0); // Normal left
            }

            VXYZ offsetVector;
            bool isEndpoint = !isClosed && (i == 0 || i == Points.Count - 1);

            if (isEndpoint)
            {
                // Open polyline endpoints - use single segment normal
                offsetVector = (i == 0 ? n2 : n1) * distance;
            }
            else
            {
                // Interior vertex (or any vertex on closed polyline) - calculate miter
                var miterDir = (n1 + n2);
                double miterLength = miterDir.GetLength();

                if (miterLength < 1e-10)
                {
                    // Segments are parallel (180 degree turn) - use perpendicular offset
                    offsetVector = n1 * distance;
                }
                else
                {
                    miterDir = miterDir.Normalize();
                    // cos(theta) = dot(miterDir, n1) where theta is angle between miter and normal
                    double cosTheta = miterDir.DotProduct(n1);

                    // Limit miter to avoid extreme spikes at very sharp angles (miter limit)
                    const double miterLimit = 4.0; // Standard miter limit
                    double miterScale = 1.0 / Math.Max(cosTheta, 1.0 / miterLimit);

                    offsetVector = miterDir * distance * miterScale;
                }
            }

            newPoints.Add((Points[i].AsVXYZ() + offsetVector).AsVPoint());
        }

        // For closed polylines, close the offset curve by adding the first point again
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

    public List<VPoint> PointsAtChordLengthFromPoint(VPoint point, double chordLength)
    {
        var results = new List<VPoint>();
        VPoint c = Project(point); 
        double r2 = chordLength;
        
        for (int i=0; i<Points.Count-1; i++)
        {
             double d1 = Points[i].DistanceTo(c);
             double d2 = Points[i+1].DistanceTo(c);
             
             // Simplistic crossing check
             if ((d1 < r2 && d2 > r2) || (d1 > r2 && d2 < r2))
             {
                 // Interpolate
                 double fraction = Math.Abs(d1 - r2) / Math.Abs(d1 - d2); 
                 // Note: distance varies non-linearly, but linear approx is okay for "try projecting" logic
                 // Better: find t where |Lerp(p1,p2,t) - c| = r2. Quadratic equation.
                 
                 // |P(t) - c|^2 = r^2
                 // P(t) = p1 + v*t
                 // |p1-c + v*t|^2 = r^2
                 // Let A = p1-c
                 // |A + v*t|^2 = r^2
                 // (A+vt).(A+vt) = r^2
                 // A.A + 2(A.v)t + (v.v)t^2 = r^2
                 // at^2 + bt + c = 0
                 
                 VXYZ A = Points[i].AsVXYZ() - c.AsVXYZ();
                 VXYZ v = Points[i+1].AsVXYZ() - Points[i].AsVXYZ();
                 
                 double qa = v.DotProduct(v);
                 double qb = 2 * A.DotProduct(v);
                 double qc = A.DotProduct(A) - r2 * r2;
                 
                 double det = qb*qb - 4*qa*qc;
                 if (det >= 0)
                 {
                     double sqrtDet = Math.Sqrt(det);
                     double tA = (-qb - sqrtDet) / (2*qa);
                     double tB = (-qb + sqrtDet) / (2*qa);
                     
                     if (tA >= 0 && tA <= 1) results.Add((Points[i].AsVXYZ() + v * tA).AsVPoint());
                     if (tB >= 0 && tB <= 1) results.Add((Points[i].AsVXYZ() + v * tB).AsVPoint());
                 }
             }
        }
        return results;
    }

    public (ICurve, ICurve) SplitAtPoint(VPoint point)
    {
        VPoint p = Project(point);

        // Find segment
        int segmentIndex = -1;

        // We need to know which segment 'p' lies on.
        // It matches the 'Project' logic if it was robust.
        // Let's re-scan for closest segment
        double minK = double.MaxValue;

        for (int i = 0; i < Points.Count - 1; i++)
        {
            VPoint proj = ProjectOnSegment(Points[i], Points[i+1], p);
            double d = proj.DistanceTo(p);
            if (d < 1e-5) // Tolerance
            {
                segmentIndex = i;
                break;
            }
            if (d < minK)
            {
                minK = d;
                segmentIndex = i; // Fallback to closest
            }
        }

        if (segmentIndex == -1) segmentIndex = 0; // Should not happen

        // Clone all points to ensure independent curves
        var l1 = new List<VPoint>();
        for(int i=0; i<=segmentIndex; i++) l1.Add((VPoint)Points[i].Clone());
        l1.Add((VPoint)p.Clone());

        var l2 = new List<VPoint>();
        l2.Add((VPoint)p.Clone());
        for(int i=segmentIndex+1; i<Points.Count; i++) l2.Add((VPoint)Points[i].Clone());

        return (new VPolyline(l1), new VPolyline(l2));
    }

    public VXYZ NormalAtPoint(VPoint p)
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
    /// Parameter is distributed evenly across segments (not by arc length).
    /// </summary>
    public VPoint PointAtParameter(double parameter)
    {
        if (Points.Count == 0) return VPoint.Internal(0, 0);
        if (Points.Count == 1) return Points[0];
        if (parameter <= 0) return Points[0];
        if (parameter >= 1) return Points[^1];

        int numSegments = Points.Count - 1;
        double scaledT = parameter * numSegments;
        int segmentIndex = Math.Min((int)scaledT, numSegments - 1);
        double localT = scaledT - segmentIndex;

        VPoint p1 = Points[segmentIndex];
        VPoint p2 = Points[segmentIndex + 1];

        return VPoint.Internal(
            p1.X + (p2.X - p1.X) * localT,
            p1.Y + (p2.Y - p1.Y) * localT
        );
    }

    /// <summary>
    /// Returns the normalized parameter (0 to 1) for the closest point on the polyline to the given point.
    /// </summary>
    public double ParameterAtPoint(VPoint point)
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
