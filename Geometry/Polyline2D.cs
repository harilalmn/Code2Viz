using Code2Viz.Canvas;

namespace Code2Viz.Geometry;

public class VPolyline : Shape, ICurve
{
    public List<VPoint> Points { get; set; }

    public VPolyline(params VPoint[] points)
    {
        Points = points.ToList();
        StrokeColor = ShapeDefaults.GlobalStrokeColor ?? "LightGreen";
    }

    public VPolyline(IEnumerable<VPoint> points)
    {
        Points = points.ToList();
        StrokeColor = ShapeDefaults.GlobalStrokeColor ?? "LightGreen";
    }

    public void AddPoint(VPoint point)
    {
        Points.Add(point);
    }

    public void AddPoint(double x, double y)
    {
        Points.Add(new VPoint(x, y));
    }

    public override void Draw()
    {
        CanvasRenderer.Instance.AddShape(this);
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
        if (Points.Count == 0) return (new VPoint(0, 0), new VPoint(0, 0));
        double minX = Points.Min(p => p.X), minY = Points.Min(p => p.Y);
        double maxX = Points.Max(p => p.X), maxY = Points.Max(p => p.Y);
        return (new VPoint(minX, minY), new VPoint(maxX, maxY));
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
                result.Add(new VPoint(x, y));
                
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
        if (segmentLength <= 0) return Points.FirstOrDefault() ?? new VPoint(0,0);
        
        double currentLen = 0;
        for (int i = 0; i < Points.Count - 1; i++)
        {
            double d = Points[i].DistanceTo(Points[i+1]);
            if (currentLen + d >= segmentLength)
            {
                double rem = segmentLength - currentLen;
                double r = rem / d;
                return new VPoint(
                    Points[i].X + (Points[i+1].X - Points[i].X) * r,
                    Points[i].Y + (Points[i+1].Y - Points[i].Y) * r
                );
            }
            currentLen += d;
        }
        return Points.LastOrDefault() ?? new VPoint(0,0);
    }

    public ICurve Offset(double distance)
    {
        if (Points.Count < 2) return (ICurve)this.Clone();

        var newPoints = new List<VPoint>();
        
        // Simple offset by vertex normal (average of adjacent segment normals)
        for (int i = 0; i < Points.Count; i++)
        {
            VXYZ n1 = new VXYZ(0,0,0);
            VXYZ n2 = new VXYZ(0,0,0);
            
            if (i > 0)
            {
                var dir = (Points[i].AsVXYZ() - Points[i-1].AsVXYZ()).Normalize();
                n1 = new VXYZ(-dir.Y, dir.X, 0); // Normal left
            }
            
            if (i < Points.Count - 1)
            {
                var dir = (Points[i+1].AsVXYZ() - Points[i].AsVXYZ()).Normalize();
                n2 = new VXYZ(-dir.Y, dir.X, 0); // Normal left
            }
            
            VXYZ normal;
            if (i == 0) normal = n2;
            else if (i == Points.Count - 1) normal = n1;
            else 
            {
                normal = (n1 + n2).Normalize();
                 // Create miter? For now simple average normalized. 
                 // Note: strict miter offset is distance / sin(angle/2).
            }

            // Approximate miter adjustment: valid for small angles 
            // but let's just use perpendicular offset for robustness in this pass
            newPoints.Add((Points[i].AsVXYZ() + normal * distance).AsVPoint());
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
        
        var l1 = new List<VPoint>();
        for(int i=0; i<=segmentIndex; i++) l1.Add(Points[i]);
        l1.Add(p);
        
        var l2 = new List<VPoint>();
        l2.Add(p);
        for(int i=segmentIndex+1; i<Points.Count; i++) l2.Add(Points[i]);
        
        return (new VPolyline(l1), new VPolyline(l2));
    }

    public VXYZ NormalAtPoint(VPoint p)
    {
        return GeometryHelper.GetPolylineNormalAtPoint(Points, p, false);
    }
}
