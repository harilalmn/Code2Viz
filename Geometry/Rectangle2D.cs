using Code2Viz.Canvas;

namespace Code2Viz.Geometry;

public class VRectangle : Shape, ICurve
{
    public VPoint Corner { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }

    /// <summary>Gets the start point of the rectangle (same as Corner).</summary>
    public VPoint StartPoint => Corner;

    /// <summary>Gets the end point of the rectangle (same as Corner, since it's closed).</summary>
    public VPoint EndPoint => Corner;

    /// <summary>A rectangle is never self-intersecting.</summary>
    public bool SelfIntersecting => false;

    public VRectangle(VPoint corner, double width, double height)
    {
        Corner = corner;
        Width = width;
        Height = height;
        StrokeColor = ShapeDefaults.GlobalStrokeColor ?? "Magenta";
    }

    public VRectangle(double x, double y, double width, double height)
    {
        Corner = new VPoint(x, y);
        Width = width;
        Height = height;
        StrokeColor = ShapeDefaults.GlobalStrokeColor ?? "Magenta";
    }

    public override void Draw()
    {
        CanvasRenderer.Instance.AddShape(this);
    }

    public override Shape Clone()
    {
        var clone = new VRectangle((VPoint)Corner.Clone(), Width, Height);
        CopyStyleTo(clone);
        return clone;
    }

    public override void Move(VXYZ vector)
    {
        Corner.Move(vector);
    }

    public override void Rotate(VPoint pivot, double angleDegrees)
    {
        // Rotating rectangle corner; note that rotated rectangle would need polygon representation
        Corner.Rotate(pivot, angleDegrees);
    }

    public override void Flip(VLine mirrorLine)
    {
        Corner.Flip(mirrorLine);
    }

    public override void Scale(VPoint center, double factor)
    {
        Corner.Scale(center, factor);
        Width *= Math.Abs(factor);
        Height *= Math.Abs(factor);
    }

    public override (VPoint min, VPoint max) GetBounds()
    {
        return (
            new VPoint(Corner.X, Corner.Y),
            new VPoint(Corner.X + Width, Corner.Y + Height)
        );
    }

    public override bool Contains(VPoint point)
    {
        return point.X >= Corner.X && point.X <= Corner.X + Width &&
               point.Y >= Corner.Y && point.Y <= Corner.Y + Height;
    }

    public override Shape? Intersect(Shape other)
    {
        if (other is VRectangle otherRect)
        {
            return GeometryHelper.IntersectRectRect(this, otherRect);
        }
        else if (other is VLine line)
        {
            return GeometryHelper.IntersectLineRect(line, this);
        }
        return base.Intersect(other);
    }

    public override string ToString() => $"VRectangle({Corner}, W:{Width}, H:{Height})";

    public double GetLength()
    {
        return 2 * (Math.Abs(Width) + Math.Abs(Height));
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

        // Define corners
        var p0 = Corner;
        var p1 = new VPoint(Corner.X + Width, Corner.Y);
        var p2 = new VPoint(Corner.X + Width, Corner.Y + Height);
        var p3 = new VPoint(Corner.X, Corner.Y + Height);
        
        // Loop: p0->p1->p2->p3->p0
        var corners = new List<VPoint> { p0, p1, p2, p3, p0 };
        
        result.Add(p0);
        double remainingStep = segmentLength;

        for (int i = 0; i < 4; i++)
        {
            VPoint start = corners[i];
            VPoint end = corners[i+1];
            double segLen = start.DistanceTo(end);
            
            double distOnSeg = 0;
            
            while (distOnSeg + remainingStep <= segLen + 1e-9)
            {
                distOnSeg += remainingStep;
                
                double t = distOnSeg / segLen;
                double x = start.X + (end.X - start.X) * t;
                double y = start.Y + (end.Y - start.Y) * t;
                result.Add(new VPoint(x, y));
                
                remainingStep = segmentLength;
            }
            
            remainingStep -= (segLen - distOnSeg);
        }

        return result;
    }

    public VPoint Project(VPoint point)
    {
        var corners = GetCorners();
        VPoint closest = corners[0];
        double minK = double.MaxValue;
        
        for (int i = 0; i < 4; i++)
        {
            VPoint p1 = corners[i];
            VPoint p2 = corners[i+1];
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
        if (segmentLength <= 0) return Corner;
        double inputLen = segmentLength;

        var corners = GetCorners();
        double currentLen = 0;
        
        for (int i = 0; i < 4; i++)
        {
            double d = corners[i].DistanceTo(corners[i+1]);
            if (currentLen + d >= inputLen)
            {
                double rem = inputLen - currentLen;
                double r = rem / d;
                return new VPoint(
                    corners[i].X + (corners[i+1].X - corners[i].X) * r,
                    corners[i].Y + (corners[i+1].Y - corners[i].Y) * r
                );
            }
            currentLen += d;
        }
        return corners[0]; // Loop back to start
    }

    public ICurve Offset(double distance)
    {
        // Simple inflation for AABB
        double newWidth = Width + 2 * distance;
        double newHeight = Height + 2 * distance;
        
        // If it shrinks to negative, handle?
        // Usually VRectangle allows negative width/height or we normalize.
        // Assuming strict positive W/H usage for now.
        return new VRectangle(
            new VPoint(Corner.X - distance, Corner.Y - distance),
            newWidth, newHeight
        );
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
        var corners = GetCorners();

        for (int i=0; i<4; i++)
        {
             double d1 = corners[i].DistanceTo(c);
             double d2 = corners[i+1].DistanceTo(c);
             
             if ((d1 < r2 && d2 > r2) || (d1 > r2 && d2 < r2))
             {
                 VXYZ A = corners[i].AsVXYZ() - c.AsVXYZ();
                 VXYZ v = corners[i+1].AsVXYZ() - corners[i].AsVXYZ();
                 
                 double qa = v.DotProduct(v);
                 double qb = 2 * A.DotProduct(v);
                 double qc = A.DotProduct(A) - r2 * r2;
                 
                 double det = qb*qb - 4*qa*qc;
                 if (det >= 0)
                 {
                     double sqrtDet = Math.Sqrt(det);
                     double tA = (-qb - sqrtDet) / (2*qa);
                     double tB = (-qb + sqrtDet) / (2*qa);
                     
                     if (tA >= 0 && tA <= 1) results.Add((corners[i].AsVXYZ() + v * tA).AsVPoint());
                     if (tB >= 0 && tB <= 1) results.Add((corners[i].AsVXYZ() + v * tB).AsVPoint());
                 }
             }
        }
        return results;
    }

    public (ICurve, ICurve) SplitAtPoint(VPoint point)
    {
        VPoint p = Project(point);
        var corners = GetCorners();
        
        int segmentIndex = -1;
        double minK = double.MaxValue;
        
        for (int i = 0; i < 4; i++)
        {
            VPoint proj = ProjectOnSegment(corners[i], corners[i+1], p);
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
        
        var l1 = new List<VPoint>();
        for(int i=0; i<=segmentIndex; i++) l1.Add(corners[i]);
        l1.Add(p);
        
        var l2 = new List<VPoint>();
        l2.Add(p);
        for(int i=segmentIndex+1; i<corners.Count; i++) l2.Add(corners[i]); // corners includes duplicate last point
        
        // Ensure loops or open?
        // Start->P is l1.
        // P->End is l2.
        // corners[4] is End (same as Start).
        
        return (new VPolyline(l1), new VPolyline(l2));
    }
    
    private List<VPoint> GetCorners()
    {
        return new List<VPoint>
        {
            Corner,
            new VPoint(Corner.X + Width, Corner.Y),
            new VPoint(Corner.X + Width, Corner.Y + Height),
            new VPoint(Corner.X, Corner.Y + Height),
            Corner 
        };
    }

    public VXYZ NormalAtPoint(VPoint p)
    {
        return GeometryHelper.GetPolylineNormalAtPoint(GetCorners(), p, true);
    }

    /// <summary>
    /// Computes the intersection between this rectangle and another curve.
    /// </summary>
    public IntersectionResult Intersect(ICurve other)
    {
        return CurveIntersection.Intersect(this, other);
    }

    /// <summary>
    /// Returns a point on the rectangle perimeter at the given normalized parameter.
    /// Parameter is distributed evenly across the 4 sides (not by arc length).
    /// Parameter 0 and 1 both return the corner point.
    /// </summary>
    public VPoint PointAtParameter(double parameter)
    {
        if (parameter <= 0 || parameter >= 1) return Corner;

        var corners = GetCorners();
        int numSegments = 4; // Rectangle has 4 sides
        double scaledT = parameter * numSegments;
        int segmentIndex = Math.Min((int)scaledT, numSegments - 1);
        double localT = scaledT - segmentIndex;

        VPoint p1 = corners[segmentIndex];
        VPoint p2 = corners[segmentIndex + 1];

        return new VPoint(
            p1.X + (p2.X - p1.X) * localT,
            p1.Y + (p2.Y - p1.Y) * localT
        );
    }
}
