using Code2Viz.Canvas;

namespace Code2Viz.Geometry;

public class VPolygon : Shape, ICurve
{
    public List<VPoint> Points { get; set; }

    public VPolygon(params VPoint[] points)
    {
        Points = points.ToList();
        StrokeColor = ShapeDefaults.GlobalStrokeColor ?? "LightBlue";
        FillColor = ShapeDefaults.GlobalFillColor ?? "Transparent";
    }

    public VPolygon(IEnumerable<VPoint> points)
    {
        Points = points.ToList();
        StrokeColor = "LightBlue";
        FillColor = "Transparent";
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
        var clone = new VPolygon(Points.Select(p => (VPoint)p.Clone()));
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

    public override string ToString() => $"VPolygon({Points.Count} points)";

    public double GetLength()
    {
        double length = 0;
        for (int i = 0; i < Points.Count; i++)
        {
            length += Points[i].DistanceTo(Points[(i + 1) % Points.Count]);
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
        
        for (int i = 0; i < Points.Count; i++)
        {
            VPoint p1 = Points[i];
            VPoint p2 = Points[(i + 1) % Points.Count];
            double segLen = p1.DistanceTo(p2);
            
            double distOnSeg = 0;
            
            while (distOnSeg + remainingStep <= segLen + 1e-9)
            {
                distOnSeg += remainingStep;
                
                // Interpolate
                double t = distOnSeg / segLen;
                double x = p1.X + (p2.X - p1.X) * t;
                double y = p1.Y + (p2.Y - p1.Y) * t;
                result.Add(new VPoint(x, y));
                
                remainingStep = segmentLength;
            }
            
            remainingStep -= (segLen - distOnSeg);
        }

        return result;
    }

    public VPoint Project(VPoint point)
    {
        if (Points.Count == 0) return new VPoint(0,0);
        VPoint closest = Points[0];
        double minK = double.MaxValue;
        
        for (int i = 0; i < Points.Count; i++)
        {
            VPoint p1 = Points[i];
            VPoint p2 = Points[(i+1)%Points.Count];
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
        double lenSq = v.DotProduct(v);
        if (lenSq < 1e-9) return s; 
        
        var t = u.DotProduct(v) / lenSq;
        if (t < 0) return s;
        if (t > 1) return e;
        return (s.AsVXYZ() + v * t).AsVPoint();
    }

    public VPoint PointAtSegmentLength(double segmentLength)
    {
        if (segmentLength <= 0 || Points.Count == 0) return Points.FirstOrDefault() ?? new VPoint(0,0);
        double inputLen = segmentLength;

        double currentLen = 0;
        
        for (int i = 0; i < Points.Count; i++)
        {
            VPoint p1 = Points[i];
            VPoint p2 = Points[(i+1)%Points.Count];
            double d = p1.DistanceTo(p2);
            
            if (currentLen + d >= inputLen)
            {
                double rem = inputLen - currentLen;
                double r = rem / d;
                return new VPoint(
                    p1.X + (p2.X - p1.X) * r,
                    p1.Y + (p2.Y - p1.Y) * r
                );
            }
            currentLen += d;
        }
        return Points[0];
    }

    public ICurve Offset(double distance)
    {
        if (Points.Count < 3) return (ICurve)this.Clone();

        var newPoints = new List<VPoint>();
        
        // Simple offset by vertex normal (average of adjacent segment normals)
        // For polygon, indices wrap.
        for (int i = 0; i < Points.Count; i++)
        {
            // Prev segment: (i-1) -> i
            int prevIdx = (i - 1 + Points.Count) % Points.Count;
            // Next segment: i -> (i+1)
            int nextIdx = (i + 1) % Points.Count;
            
            var dir1 = (Points[i].AsVXYZ() - Points[prevIdx].AsVXYZ()).Normalize();
            VXYZ n1 = new VXYZ(-dir1.Y, dir1.X, 0); 
            
            var dir2 = (Points[nextIdx].AsVXYZ() - Points[i].AsVXYZ()).Normalize();
            VXYZ n2 = new VXYZ(-dir2.Y, dir2.X, 0); 
            
            VXYZ normal = (n1 + n2).Normalize();
            // Miter adjustment could go here
            
            newPoints.Add((Points[i].AsVXYZ() + normal * distance).AsVPoint());
        }
        
        return new VPolygon(newPoints);
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
        
        for (int i=0; i<Points.Count; i++)
        {
             VPoint p1 = Points[i];
             VPoint p2 = Points[(i+1)%Points.Count];
             
             double d1 = p1.DistanceTo(c);
             double d2 = p2.DistanceTo(c);
             
             if ((d1 < r2 && d2 > r2) || (d1 > r2 && d2 < r2))
             {
                 VXYZ A = p1.AsVXYZ() - c.AsVXYZ();
                 VXYZ v = p2.AsVXYZ() - p1.AsVXYZ();
                 
                 double qa = v.DotProduct(v);
                 double qb = 2 * A.DotProduct(v);
                 double qc = A.DotProduct(A) - r2 * r2;
                 
                 double det = qb*qb - 4*qa*qc;
                 if (det >= 0)
                 {
                     double sqrtDet = Math.Sqrt(det);
                     double tA = (-qb - sqrtDet) / (2*qa);
                     double tB = (-qb + sqrtDet) / (2*qa);
                     
                     if (tA >= 0 && tA <= 1) results.Add((p1.AsVXYZ() + v * tA).AsVPoint());
                     if (tB >= 0 && tB <= 1) results.Add((p1.AsVXYZ() + v * tB).AsVPoint());
                 }
             }
        }
        return results;
    }

    public (ICurve, ICurve) SplitAtPoint(VPoint point)
    {
        VPoint p = Project(point);
        
        int segmentIndex = -1;
        double minK = double.MaxValue;
        
        for (int i = 0; i < Points.Count; i++)
        {
            VPoint p1 = Points[i];
            VPoint p2 = Points[(i+1)%Points.Count];
            VPoint proj = ProjectOnSegment(p1, p2, p);
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
        
        // Loop: 0 -> 1 -> ... -> segmentIndex -> P -> segmentIndex+1 -> ... -> 0
        // Split at P means:
        // L1: 0 -> ... -> segmentIndex -> P
        // L2: P -> segmentIndex+1 -> ... -> 0
        
        var l1 = new List<VPoint>();
        for(int i=0; i<=segmentIndex; i++) l1.Add(Points[i]);
        l1.Add(p);
        
        var l2 = new List<VPoint>();
        l2.Add(p);
        for(int i=segmentIndex+1; i<Points.Count; i++) l2.Add(Points[i]);
        l2.Add(Points[0]); // Close the second part back to Start
        
        return (new VPolyline(l1), new VPolyline(l2));
    }

    public VXYZ NormalAtPoint(VPoint p)
    {
        return GeometryHelper.GetPolylineNormalAtPoint(Points, p, true);
    }
}
