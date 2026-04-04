namespace C2VGeometry;

public class VEllipse : Shape, ICurve
{
    public VXYZ Center { get; set; }
    public double RadiusX { get; set; }
    public double RadiusY { get; set; }

    public double StartAngle { get; set; } = 0;
    public double EndAngle { get; set; } = 360;

    /// <summary>Gets the area of the ellipse (π * RadiusX * RadiusY).</summary>
    public double Area => Math.PI * RadiusX * RadiusY;

    /// <summary>
    /// Gets the approximate circumference of the ellipse using Ramanujan's formula.
    /// </summary>
    public double Circumference
    {
        get
        {
            double a = RadiusX;
            double b = RadiusY;
            double h = Math.Pow(a - b, 2) / Math.Pow(a + b, 2);
            return Math.PI * (a + b) * (1 + 3 * h / (10 + Math.Sqrt(4 - 3 * h)));
        }
    }

    public VEllipse(VXYZ center, double radiusX, double radiusY)
    {
        Center = center;
        RadiusX = radiusX;
        RadiusY = radiusY;
        Color = "Pink";
    }

    public VEllipse(double centerX, double centerY, double radiusX, double radiusY)
    {
        Center = new VXYZ(centerX, centerY);
        RadiusX = radiusX;
        RadiusY = radiusY;
        Color = "Pink";
    }

    public VEllipse(VXYZ center, double radiusX, double radiusY, double startAngle, double endAngle)
        : this(center, radiusX, radiusY)
    {
        StartAngle = startAngle;
        EndAngle = endAngle;
    }



    public override List<ControlPoint> GetControlPoints()
    {
        return new List<ControlPoint>
        {
            new ControlPoint(ControlPointType.Move, Center.X, Center.Y, "Center"),
            new ControlPoint(ControlPointType.Radius, Center.X + RadiusX, Center.Y, "RadiusX"),
            new ControlPoint(ControlPointType.Radius, Center.X, Center.Y + RadiusY, "RadiusY")
        };
    }

    public override void MoveControlPoint(int index, VXYZ newPosition)
    {
        switch (index)
        {
            case 0:
                var delta = new VXYZ(newPosition.X - Center.X, newPosition.Y - Center.Y, 0);
                Move(delta);
                break;
            case 1:
                RadiusX = Math.Abs(newPosition.X - Center.X);
                break;
            case 2:
                RadiusY = Math.Abs(newPosition.Y - Center.Y);
                break;
        }
    }

    public override VEllipse Clone()
    {
        var clone = new VEllipse(Center.Clone(), RadiusX, RadiusY, StartAngle, EndAngle);
        CopyStyleTo(clone);
        return clone;
    }

    public override void Move(VXYZ vector)
    {
        Center = Center + vector;
    }

    public override void Rotate(VXYZ pivot, double angleDegrees)
    {
        Center = GeometryHelper.RotatePoint(Center, pivot, angleDegrees);
    }

    public override void Flip(VLine mirrorLine)
    {
        Center = GeometryHelper.FlipPoint(Center, mirrorLine);
    }

    public override void Scale(VXYZ center, double factor)
    {
        Center = GeometryHelper.ScalePoint(Center, center, factor);
        RadiusX *= Math.Abs(factor);
        RadiusY *= Math.Abs(factor);
    }

    public override BoundingBox GetBounds()
    {
        return new BoundingBox(
            new VXYZ(Center.X - RadiusX, Center.Y - RadiusY),
            new VXYZ(Center.X + RadiusX, Center.Y + RadiusY)
        );
    }

    public override string ToString() => $"VEllipse({Center}, RX:{RadiusX}, RY:{RadiusY}, {StartAngle}-{EndAngle})";

    public VXYZ Evaluate(double parameter)
    {
        double angleDeg = StartAngle + (EndAngle - StartAngle) * parameter;
        double angleRad = angleDeg * Math.PI / 180.0;

        double x = Center.X + RadiusX * Math.Cos(angleRad);
        double y = Center.Y + RadiusY * Math.Sin(angleRad);
        return new VXYZ(x, y);
    }

    public VXYZ NormalAtPoint(VXYZ p)
    {
        double dx = (p.X - Center.X) / (RadiusX * RadiusX);
        double dy = (p.Y - Center.Y) / (RadiusY * RadiusY);
        return new VXYZ(dx, dy, 0).Normalize();
    }

    // ICurve Impl

    public VXYZ Project(VXYZ point)
    {
        VXYZ bestP = Evaluate(0);
        double minD = point.DistanceTo(bestP);

        int steps = 100;
        for (int i = 1; i <= steps; i++)
        {
            VXYZ p = Evaluate((double)i / steps);
            double d = point.DistanceTo(p);
            if (d < minD)
            {
                minD = d;
                bestP = p;
            }
        }

        return bestP;
    }

    public VXYZ PointAtSegmentLength(double segmentLength)
    {
        var points = Measure(segmentLength < 1.0 ? 1.0 : segmentLength / 10.0);
        double dist = 0;
        for(int i=0; i<points.Count-1; i++)
        {
            double d = points[i].DistanceTo(points[i+1]);
            if (dist + d >= segmentLength)
            {
                double rem = segmentLength - dist;
                VXYZ dir = (points[i+1] - points[i]).Normalize();
                return points[i] + dir * rem;
            }
            dist += d;
        }
        return EndPoint;
    }

    public double GetLength()
    {
        return Measure(RadiusX / 10.0).Count * (RadiusX / 10.0);
    }

    private double GetLengthNumerical()
    {
         double len = 0;
         int steps = 100;
         VXYZ prev = Evaluate(0);
         for(int i=1; i<=steps; i++){
             VXYZ curr = Evaluate((double)i/steps);
             len += prev.DistanceTo(curr);
             prev = curr;
         }
         return len;
    }

    double ICurve.GetLength() => GetLengthNumerical();

    public ICurve Offset(double distance)
    {
        return new VEllipse(Center.Clone(), RadiusX + distance, RadiusY + distance, StartAngle, EndAngle);
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
        VXYZ p = Project(point);
        double nx = (p.X - Center.X) / RadiusX;
        double ny = (p.Y - Center.Y) / RadiusY;
        double angle = Math.Atan2(ny, nx) * 180.0 / Math.PI;
        angle = GeometryHelper.NormalizeAngle(angle);

        return (
             new VEllipse(Center, RadiusX, RadiusY, StartAngle, angle),
             new VEllipse(Center, RadiusX, RadiusY, angle, EndAngle)
        );
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

        double totalLen = GetLengthNumerical();
        int count = (int)(totalLen / segmentLength);
        for(int i=0; i<=count; i++)
        {
             points.Add(Evaluate((double)i * segmentLength / totalLen));
        }
        return points;
    }

    public VXYZ StartPoint => Evaluate(0);
    public VXYZ EndPoint => Evaluate(1);

    /// <summary>An ellipse is never self-intersecting.</summary>
    public bool SelfIntersecting => false;

    /// <summary>Gets the vertices of the ellipse (center point).</summary>
    public List<VXYZ> Vertices => new List<VXYZ> { Center };

    /// <summary>
    /// Computes the intersection between this ellipse and another curve.
    /// </summary>
    public IntersectionResult Intersect(ICurve other)
    {
        return CurveIntersection.Intersect(this, other);
    }

    /// <summary>
    /// Returns a point on the ellipse at the given normalized parameter.
    /// </summary>
    public VXYZ PointAtParameter(double parameter) => Evaluate(parameter);

    /// <summary>
    /// Returns the normalized parameter (0 to 1) for the closest point on the ellipse to the given point.
    /// </summary>
    public double ParameterAtPoint(VXYZ point)
    {
        double angle = Math.Atan2((point.Y - Center.Y) / RadiusY, (point.X - Center.X) / RadiusX);
        double angleDeg = angle * 180.0 / Math.PI;

        if (angleDeg < 0) angleDeg += 360;

        double sweep = EndAngle - StartAngle;
        if (Math.Abs(sweep) < 1e-10) return 0;

        double relativeAngle = angleDeg - StartAngle;
        while (relativeAngle < 0) relativeAngle += 360;
        while (relativeAngle > 360) relativeAngle -= 360;

        return Math.Clamp(relativeAngle / sweep, 0, 1);
    }
}
