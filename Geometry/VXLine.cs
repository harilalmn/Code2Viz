using Code2Viz.Canvas;

namespace Code2Viz.Geometry;

/// <summary>
/// Represents an infinite construction line (like AutoCAD's XLine).
/// The line extends infinitely in both directions through a base point along a direction.
/// </summary>
public class VXLine : Shape, ICurve
{
    /// <summary>Gets or sets the base point that the line passes through.</summary>
    public VPoint BasePoint { get; set; }

    /// <summary>Gets or sets the direction vector of the line (will be normalized).</summary>
    public VXYZ Direction { get; set; }

    /// <summary>Gets the start point (for rendering, returns a point far in the negative direction).</summary>
    public VPoint StartPoint => GetPointAtParameter(-RenderExtent);

    /// <summary>Gets the end point (for rendering, returns a point far in the positive direction).</summary>
    public VPoint EndPoint => GetPointAtParameter(RenderExtent);

    /// <summary>An infinite line is never self-intersecting.</summary>
    public bool SelfIntersecting => false;

    /// <summary>Gets the base point as the only vertex.</summary>
    public List<VPoint> Vertices => new List<VPoint> { BasePoint };

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
    public VXLine(VPoint basePoint, VXYZ direction)
    {
        BasePoint = basePoint;
        Direction = direction.Normalize();
        Color = ShapeDefaults.GlobalColor ?? "Gray";
    }

    /// <summary>
    /// Creates an infinite line passing through two points.
    /// </summary>
    /// <param name="point1">First point on the line.</param>
    /// <param name="point2">Second point on the line.</param>
    public VXLine(VPoint point1, VPoint point2)
    {
        BasePoint = point1;
        var dir = point2.AsVXYZ() - point1.AsVXYZ();
        Direction = dir.Normalize();
        Color = ShapeDefaults.GlobalColor ?? "Gray";
    }

    /// <summary>
    /// Creates an infinite line passing through two points specified by coordinates.
    /// </summary>
    public VXLine(double x1, double y1, double x2, double y2)
        : this(new VPoint(x1, y1), new VPoint(x2, y2))
    {
    }

    /// <summary>
    /// Creates a horizontal infinite line at the specified Y coordinate.
    /// </summary>
    public static VXLine Horizontal(double y) => new VXLine(new VPoint(0, y), VXYZ.BasisX);

    /// <summary>
    /// Creates a vertical infinite line at the specified X coordinate.
    /// </summary>
    public static VXLine Vertical(double x) => new VXLine(new VPoint(x, 0), VXYZ.BasisY);

    /// <summary>
    /// Gets a point on the line at the specified parameter.
    /// Parameter 0 is at BasePoint, positive goes in Direction, negative goes opposite.
    /// </summary>
    public VPoint GetPointAtParameter(double parameter)
    {
        return (BasePoint.AsVXYZ() + Direction * parameter).AsVPoint();
    }

    /// <summary>
    /// Returns a point on the line at the given parameter.
    /// </summary>
    public VPoint PointAtParameter(double parameter)
    {
        // Map [0, 1] to [-RenderExtent, RenderExtent] for consistency with other curves
        double mappedParam = (parameter - 0.5) * 2 * RenderExtent;
        return GetPointAtParameter(mappedParam);
    }



    public override Shape Clone()
    {
        var clone = new VXLine((VPoint)BasePoint.Clone(), Direction);
        clone.RenderExtent = RenderExtent;
        CopyStyleTo(clone);
        return clone;
    }

    public override void Move(VXYZ vector)
    {
        BasePoint.Move(vector);
    }

    public override void Rotate(VPoint pivot, double angleDegrees)
    {
        BasePoint.Rotate(pivot, angleDegrees);
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
        BasePoint.Flip(mirrorLine);
        // Reflect direction across the mirror line
        var mirrorDir = mirrorLine.Direction;
        double dot = Direction.X * mirrorDir.X + Direction.Y * mirrorDir.Y;
        double newX = 2 * dot * mirrorDir.X - Direction.X;
        double newY = 2 * dot * mirrorDir.Y - Direction.Y;
        Direction = new VXYZ(newX, newY, 0).Normalize();
    }

    public override void Scale(VPoint center, double factor)
    {
        BasePoint.Scale(center, factor);
        // Direction remains unchanged for infinite line scaling
    }

    public override (VPoint min, VPoint max) GetBounds()
    {
        // Return bounds based on render extent
        var p1 = StartPoint;
        var p2 = EndPoint;
        return (
            new VPoint(Math.Min(p1.X, p2.X), Math.Min(p1.Y, p2.Y)),
            new VPoint(Math.Max(p1.X, p2.X), Math.Max(p1.Y, p2.Y))
        );
    }

    /// <summary>
    /// Returns positive infinity since the line extends infinitely in both directions.
    /// </summary>
    public double GetLength() => double.PositiveInfinity;

    /// <summary>
    /// Projects a point onto the infinite line.
    /// </summary>
    public VPoint Project(VPoint point)
    {
        var v = Direction;
        var u = point.AsVXYZ() - BasePoint.AsVXYZ();
        var t = u.DotProduct(v) / v.DotProduct(v);
        return (BasePoint.AsVXYZ() + v * t).AsVPoint();
    }

    /// <summary>
    /// Returns the normal vector at any point (constant for a line).
    /// </summary>
    public VXYZ NormalAtPoint(VPoint p)
    {
        return new VXYZ(Direction.Y, -Direction.X, 0).Normalize();
    }

    /// <summary>
    /// Returns a point at the specified distance from BasePoint along the direction.
    /// </summary>
    public VPoint PointAtSegmentLength(double segmentLength)
    {
        return (BasePoint.AsVXYZ() + Direction * segmentLength).AsVPoint();
    }

    /// <summary>
    /// Creates an offset line parallel to this one.
    /// </summary>
    public ICurve Offset(double distance)
    {
        var normal = NormalAtPoint(BasePoint);
        var offsetVector = normal * distance;
        var clone = new VXLine((BasePoint.AsVXYZ() + offsetVector).AsVPoint(), Direction);
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
    public List<VPoint> Divide(int numberOfSegments)
    {
        var points = new List<VPoint>();
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
    public List<VPoint> Measure(double segmentLength)
    {
        var points = new List<VPoint>();
        if (segmentLength <= 1e-9) return points;

        // Measure in both directions from BasePoint
        for (double d = 0; d <= RenderExtent; d += segmentLength)
        {
            points.Add(PointAtSegmentLength(d));
            if (d > 0)
                points.Add(PointAtSegmentLength(-d));
        }
        return points.OrderBy(p => (p.AsVXYZ() - BasePoint.AsVXYZ()).DotProduct(Direction)).ToList();
    }

    /// <summary>
    /// Returns points at chord length from a given point (on the infinite line).
    /// </summary>
    public List<VPoint> PointsAtChordLengthFromPoint(VPoint point, double chordLength)
    {
        var projected = Project(point);
        var p1 = (projected.AsVXYZ() + Direction * chordLength).AsVPoint();
        var p2 = (projected.AsVXYZ() - Direction * chordLength).AsVPoint();
        return new List<VPoint> { p1, p2 };
    }

    /// <summary>
    /// Splits the line at a point, returning two rays.
    /// </summary>
    public (ICurve, ICurve) SplitAtPoint(VPoint point)
    {
        var splitPoint = Project(point);
        // Return two rays going in opposite directions from the split point
        var ray1 = new VRay((VPoint)splitPoint.Clone(), Direction * -1);
        var ray2 = new VRay((VPoint)splitPoint.Clone(), Direction);
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
    public (VPoint, VPoint) GetTwoPoints()
    {
        return (BasePoint, GetPointAtParameter(1));
    }

    public override string ToString() => $"VXLine(Base:{BasePoint}, Dir:{Direction})";

    /// <summary>
    /// Returns the normalized parameter (0 to 1) for the closest point on the construction line to the given point.
    /// Note: For infinite lines, this projects onto the rendered extent and normalizes to [0, 1].
    /// </summary>
    public double ParameterAtPoint(VPoint point)
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
}
