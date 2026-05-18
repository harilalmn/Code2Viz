namespace C2VGeometry;

/// <summary>
/// Represents an infinite construction line (like AutoCAD's XLine).
/// The line extends infinitely in both directions through a base point along a direction.
/// </summary>
public class VXLine : Shape, ICurve
{
    /// <summary>Gets or sets the base point that the line passes through.</summary>
    public VXYZ BasePoint { get; set; }

    /// <summary>Gets or sets the direction vector of the line (will be normalized).</summary>
    public VXYZ Direction { get; set; }

    /// <summary>Gets the start point (for rendering, returns a point far in the negative direction).</summary>
    public VXYZ StartPoint => GetPointAtParameter(-RenderExtent);

    /// <summary>Gets the end point (for rendering, returns a point far in the positive direction).</summary>
    public VXYZ EndPoint => GetPointAtParameter(RenderExtent);

    /// <summary>An infinite line is never self-intersecting.</summary>
    public bool SelfIntersecting => false;

    /// <summary>Gets the base point as the only vertex.</summary>
    public List<VXYZ> Vertices => new List<VXYZ> { BasePoint };

    /// <summary>
    /// The extent used for rendering and bounds calculation.
    /// Points at parameter -RenderExtent and +RenderExtent define the visual segment.
    /// </summary>
    public double RenderExtent { get; set; } = 10000;

    /// <summary>
    /// Creates an infinite line through a base point in the specified direction.
    /// </summary>
    /// <param name="basePoint">A point that the line passes through.</param>
    /// <param name="direction">The direction of the line (will be normalized).</param>
    public VXLine(VXYZ basePoint, VXYZ direction)
    {
        BasePoint = basePoint;
        Direction = direction.Normalize();
        Color = "Gray";
    }

    /// <summary>
    /// Creates an infinite line passing through two points specified by coordinates.
    /// </summary>
    public VXLine(double x1, double y1, double x2, double y2)
        : this(new VXYZ(x1, y1), new VXYZ(x2 - x1, y2 - y1))
    {
    }

    /// <summary>
    /// Creates a horizontal infinite line at the specified Y coordinate.
    /// </summary>
    public static VXLine Horizontal(double y) => new VXLine(new VXYZ(0, y), VXYZ.BasisX);

    /// <summary>
    /// Creates a vertical infinite line at the specified X coordinate.
    /// </summary>
    public static VXLine Vertical(double x) => new VXLine(new VXYZ(x, 0), VXYZ.BasisY);

    /// <summary>
    /// Gets a point on the line at the specified parameter.
    /// Parameter 0 is at BasePoint, positive goes in Direction, negative goes opposite.
    /// </summary>
    public VXYZ GetPointAtParameter(double parameter)
    {
        return BasePoint + Direction * parameter;
    }

    /// <summary>
    /// Returns a point on the line at the given parameter.
    /// </summary>
    public VXYZ PointAtParameter(double parameter)
    {
        // Map [0, 1] to [-RenderExtent, RenderExtent] for consistency with other curves
        double mappedParam = (parameter - 0.5) * 2 * RenderExtent;
        return GetPointAtParameter(mappedParam);
    }



    public override VXLine Clone()
    {
        var clone = new VXLine(BasePoint.Clone(), Direction);
        clone.RenderExtent = RenderExtent;
        CopyStyleTo(clone);
        return clone;
    }

    public override void Move(VXYZ vector)
    {
        BasePoint = BasePoint + vector;
    }

    public override void Rotate(VXYZ pivot, double angleDegrees)
    {
        BasePoint = GeometryHelper.RotatePoint(BasePoint, pivot, angleDegrees);
        // Rotate direction vector
        double radians = angleDegrees * Math.PI / 180.0;
        double cos = Math.Cos(radians);
        double sin = Math.Sin(radians);
        double newX = Direction.X * cos - Direction.Y * sin;
        double newY = Direction.X * sin + Direction.Y * cos;
        Direction = new VXYZ(newX, newY, 0).Normalize();
    }

    public override void Flip(VLine mirrorLine)
    {
        BasePoint = GeometryHelper.FlipPoint(BasePoint, mirrorLine);
        // Reflect direction across the mirror line
        var mirrorDir = mirrorLine.Direction;
        double dot = Direction.X * mirrorDir.X + Direction.Y * mirrorDir.Y;
        double newX = 2 * dot * mirrorDir.X - Direction.X;
        double newY = 2 * dot * mirrorDir.Y - Direction.Y;
        Direction = new VXYZ(newX, newY, 0).Normalize();
    }

    public override void Scale(VXYZ center, double factor)
    {
        BasePoint = GeometryHelper.ScalePoint(BasePoint, center, factor);
        // Direction remains unchanged for infinite line scaling
    }

    public override BoundingBox GetBounds()
    {
        // Return bounds based on render extent
        var p1 = StartPoint;
        var p2 = EndPoint;
        return new BoundingBox(
            new VXYZ(Math.Min(p1.X, p2.X), Math.Min(p1.Y, p2.Y)),
            new VXYZ(Math.Max(p1.X, p2.X), Math.Max(p1.Y, p2.Y))
        );
    }

    /// <summary>
    /// Returns positive infinity since the line extends infinitely in both directions.
    /// </summary>
    public double GetLength() => double.PositiveInfinity;

    /// <summary>
    /// Projects a point onto the infinite line.
    /// </summary>
    public VXYZ Project(VXYZ point)
    {
        var v = Direction;
        var u = point - BasePoint;
        var t = u.DotProduct(v) / v.DotProduct(v);
        return BasePoint + v * t;
    }

    /// <summary>
    /// Returns the normal vector at any point (constant for a line).
    /// </summary>
    public VXYZ NormalAtPoint(VXYZ p)
    {
        return new VXYZ(Direction.Y, -Direction.X, 0).Normalize();
    }

    /// <summary>
    /// Returns a point at the specified distance from BasePoint along the direction.
    /// </summary>
    public VXYZ PointAtSegmentLength(double segmentLength)
    {
        return BasePoint + Direction * segmentLength;
    }

    /// <summary>
    /// Creates an offset line parallel to this one.
    /// </summary>
    public ICurve Offset(double distance)
    {
        var normal = NormalAtPoint(BasePoint);
        var offsetVector = normal * distance;
        var clone = new VXLine(BasePoint + offsetVector, Direction);
        clone.RenderExtent = RenderExtent;
        return clone;
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

    /// <summary>
    /// Divides the rendered portion of the line into equal segments.
    /// </summary>
    public List<VXYZ> Divide(int numberOfSegments)
    {
        var points = new List<VXYZ>();
        if (numberOfSegments <= 0) return points;

        for (int i = 0; i <= numberOfSegments; i++)
        {
            double t = (double)i / numberOfSegments;
            points.Add(PointAtParameter(t));
        }
        return points;
    }

    /// <summary>
    /// Returns points at fixed intervals starting from BasePoint.
    /// </summary>
    public List<VXYZ> Measure(double segmentLength)
    {
        var points = new List<VXYZ>();
        if (segmentLength <= 1e-9) return points;

        // Measure in both directions from BasePoint
        for (double d = 0; d <= RenderExtent; d += segmentLength)
        {
            points.Add(PointAtSegmentLength(d));
            if (d > 0)
                points.Add(PointAtSegmentLength(-d));
        }
        return points.OrderBy(p => (p - BasePoint).DotProduct(Direction)).ToList();
    }

    /// <summary>
    /// Returns points at chord length from a given point (on the infinite line).
    /// </summary>
    public List<VXYZ> PointsAtChordLengthFromPoint(VXYZ point, double chordLength)
    {
        var projected = Project(point);
        var p1 = projected + Direction * chordLength;
        var p2 = projected - Direction * chordLength;
        return new List<VXYZ> { p1, p2 };
    }

    /// <summary>
    /// Splits the line at a point, returning two rays.
    /// </summary>
    public (ICurve, ICurve) SplitAtPoint(VXYZ point)
    {
        var splitPoint = Project(point);
        // Return two rays going in opposite directions from the split point
        var ray1 = new VRay(splitPoint, Direction * -1);
        var ray2 = new VRay(splitPoint, Direction);
        return (ray1, ray2);
    }

    /// <summary>
    /// Computes the intersection between this infinite line and another curve.
    /// </summary>
    public IntersectionResult Intersect(ICurve other)
    {
        return CurveIntersection.Intersect(this, other);
    }

    /// <summary>
    /// Converts this XLine to a finite VLine segment for intersection calculations.
    /// </summary>
    public VLine ToFiniteLine()
    {
        return new VLine(StartPoint, EndPoint);
    }

    /// <summary>
    /// Gets two distinct points on the line for use in algorithms requiring two points.
    /// </summary>
    public (VXYZ, VXYZ) GetTwoPoints()
    {
        return (BasePoint, GetPointAtParameter(1));
    }

    public override string ToString() => $"VXLine(Base:{BasePoint}, Dir:{Direction})";

    /// <summary>
    /// Returns the normalized parameter (0 to 1) for the closest point on the construction line to the given point.
    /// Note: For infinite lines, this projects onto the rendered extent and normalizes to [0, 1].
    /// </summary>
    public double ParameterAtPoint(VXYZ point)
    {
        // Project point onto the infinite line
        double dx = Direction.X;
        double dy = Direction.Y;
        double lengthSq = dx * dx + dy * dy;
        if (lengthSq < 1e-10) return 0.5;

        double t = ((point.X - BasePoint.X) * dx + (point.Y - BasePoint.Y) * dy) / lengthSq;

        // Map from parameter space [-RenderExtent, RenderExtent] to [0, 1]
        double normalizedT = (t / RenderExtent + 1) / 2;
        return Math.Clamp(normalizedT, 0, 1);
    }

    /// <summary>
    /// Not supported: trimming an infinite construction line produces a finite line, which is a different shape type.
    /// Use <see cref="SplitAtPoint"/> to obtain ray segments instead.
    /// </summary>
    /// <exception cref="NotSupportedException">Always thrown.</exception>
    public void SetBounds(double startParameter, double endParameter)
    {
        throw new NotSupportedException(
            "VXLine.SetBounds is not supported because a trimmed infinite line is a finite line, not an XLine. " +
            "Use SplitAtPoint to obtain ray segments instead.");
    }
}
