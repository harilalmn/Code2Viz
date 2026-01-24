using Code2Viz.Canvas;

namespace Code2Viz.Geometry;

/// <summary>
/// Represents a semi-infinite ray (like AutoCAD's Ray).
/// The ray starts at an origin point and extends infinitely in one direction.
/// </summary>
public class VRay : Shape, ICurve
{
    /// <summary>Gets or sets the origin point where the ray starts.</summary>
    public VPoint Origin { get; set; }

    /// <summary>Gets or sets the direction vector of the ray (will be normalized).</summary>
    public VXYZ Direction { get; set; }

    /// <summary>Gets the start point of the ray (same as Origin).</summary>
    public VPoint StartPoint => Origin;

    /// <summary>Gets the end point (for rendering, returns a point far in the direction).</summary>
    public VPoint EndPoint => GetPointAtDistance(RenderExtent);

    /// <summary>A ray is never self-intersecting.</summary>
    public bool SelfIntersecting => false;

    /// <summary>Gets the origin as the only vertex.</summary>
    public List<VPoint> Vertices => new List<VPoint> { Origin };

    /// <summary>
    /// The extent used for rendering and bounds calculation.
    /// The ray is rendered from Origin to Origin + Direction * RenderExtent.
    /// </summary>
    public double RenderExtent { get; set; } = 10000;

    /// <summary>
    /// Creates a ray starting at the origin in the specified direction.
    /// </summary>
    /// <param name="origin">The starting point of the ray.</param>
    /// <param name="direction">The direction the ray extends (will be normalized).</param>
    public VRay(VPoint origin, VXYZ direction)
    {
        Origin = origin;
        Direction = direction.Normalize();
        StrokeColor = ShapeDefaults.GlobalStrokeColor ?? "Gray";
    }

    /// <summary>
    /// Creates a ray starting at the first point and passing through the second point.
    /// </summary>
    /// <param name="origin">The starting point of the ray.</param>
    /// <param name="throughPoint">A point the ray passes through.</param>
    public VRay(VPoint origin, VPoint throughPoint)
    {
        Origin = origin;
        var dir = throughPoint.AsVXYZ() - origin.AsVXYZ();
        Direction = dir.Normalize();
        StrokeColor = ShapeDefaults.GlobalStrokeColor ?? "Gray";
    }

    /// <summary>
    /// Creates a ray using coordinates.
    /// </summary>
    public VRay(double originX, double originY, double throughX, double throughY)
        : this(new VPoint(originX, originY), new VPoint(throughX, throughY))
    {
    }

    /// <summary>
    /// Creates a horizontal ray pointing right from the specified point.
    /// </summary>
    public static VRay HorizontalRight(VPoint origin) => new VRay(origin, VXYZ.BasisX);

    /// <summary>
    /// Creates a horizontal ray pointing left from the specified point.
    /// </summary>
    public static VRay HorizontalLeft(VPoint origin) => new VRay(origin, VXYZ.BasisX * -1);

    /// <summary>
    /// Creates a vertical ray pointing up from the specified point.
    /// </summary>
    public static VRay VerticalUp(VPoint origin) => new VRay(origin, VXYZ.BasisY);

    /// <summary>
    /// Creates a vertical ray pointing down from the specified point.
    /// </summary>
    public static VRay VerticalDown(VPoint origin) => new VRay(origin, VXYZ.BasisY * -1);

    /// <summary>
    /// Creates a ray at a specified angle from the origin.
    /// </summary>
    /// <param name="origin">The starting point of the ray.</param>
    /// <param name="angleDegrees">The angle in degrees (0 = positive X axis, counter-clockwise).</param>
    public static VRay AtAngle(VPoint origin, double angleDegrees)
    {
        double radians = angleDegrees * Math.PI / 180.0;
        var direction = new VXYZ(Math.Cos(radians), Math.Sin(radians), 0);
        return new VRay(origin, direction);
    }

    /// <summary>
    /// Gets a point on the ray at the specified distance from origin.
    /// </summary>
    public VPoint GetPointAtDistance(double distance)
    {
        return (Origin.AsVXYZ() + Direction * distance).AsVPoint();
    }

    /// <summary>
    /// Returns a point on the ray at the given normalized parameter.
    /// Parameter 0 is at Origin, parameter 1 is at RenderExtent distance.
    /// </summary>
    public VPoint PointAtParameter(double parameter)
    {
        return GetPointAtDistance(parameter * RenderExtent);
    }

    public override void Draw()
    {
        CanvasRenderer.Instance.AddShape(this);
    }

    public override Shape Clone()
    {
        var clone = new VRay((VPoint)Origin.Clone(), Direction);
        clone.RenderExtent = RenderExtent;
        CopyStyleTo(clone);
        return clone;
    }

    public override void Move(VXYZ vector)
    {
        Origin.Move(vector);
    }

    public override void Rotate(VPoint pivot, double angleDegrees)
    {
        Origin.Rotate(pivot, angleDegrees);
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
        Origin.Flip(mirrorLine);
        // Reflect direction across the mirror line
        var mirrorDir = mirrorLine.Direction;
        double dot = Direction.X * mirrorDir.X + Direction.Y * mirrorDir.Y;
        double newX = 2 * dot * mirrorDir.X - Direction.X;
        double newY = 2 * dot * mirrorDir.Y - Direction.Y;
        Direction = new VXYZ(newX, newY, 0).Normalize();
    }

    public override void Scale(VPoint center, double factor)
    {
        Origin.Scale(center, factor);
        // Direction remains unchanged for ray scaling
    }

    public override (VPoint min, VPoint max) GetBounds()
    {
        var p1 = StartPoint;
        var p2 = EndPoint;
        return (
            new VPoint(Math.Min(p1.X, p2.X), Math.Min(p1.Y, p2.Y)),
            new VPoint(Math.Max(p1.X, p2.X), Math.Max(p1.Y, p2.Y))
        );
    }

    /// <summary>
    /// Returns positive infinity since the ray extends infinitely in one direction.
    /// </summary>
    public double GetLength() => double.PositiveInfinity;

    /// <summary>
    /// Projects a point onto the ray.
    /// If the projection falls behind the origin, returns the origin.
    /// </summary>
    public VPoint Project(VPoint point)
    {
        var v = Direction;
        var u = point.AsVXYZ() - Origin.AsVXYZ();
        var t = u.DotProduct(v) / v.DotProduct(v);

        // Ray only extends in positive direction
        if (t < 0) return Origin;

        return (Origin.AsVXYZ() + v * t).AsVPoint();
    }

    /// <summary>
    /// Returns the normal vector at any point (constant for a ray).
    /// </summary>
    public VXYZ NormalAtPoint(VPoint p)
    {
        return new VXYZ(Direction.Y, -Direction.X, 0).Normalize();
    }

    /// <summary>
    /// Returns a point at the specified distance from Origin along the direction.
    /// </summary>
    public VPoint PointAtSegmentLength(double segmentLength)
    {
        if (segmentLength < 0) return Origin;
        return (Origin.AsVXYZ() + Direction * segmentLength).AsVPoint();
    }

    /// <summary>
    /// Creates an offset ray parallel to this one.
    /// </summary>
    public ICurve Offset(double distance)
    {
        var normal = NormalAtPoint(Origin);
        var offsetVector = normal * distance;
        var clone = new VRay((Origin.AsVXYZ() + offsetVector).AsVPoint(), Direction);
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
    /// Divides the rendered portion of the ray into equal segments.
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
    /// Returns points at fixed intervals starting from Origin.
    /// </summary>
    public List<VPoint> Measure(double segmentLength)
    {
        var points = new List<VPoint>();
        if (segmentLength <= 1e-9) return points;

        for (double d = 0; d <= RenderExtent; d += segmentLength)
        {
            points.Add(PointAtSegmentLength(d));
        }
        return points;
    }

    /// <summary>
    /// Returns points at chord length from a given point (on the ray).
    /// Only returns points that are on the ray (distance >= 0 from origin).
    /// </summary>
    public List<VPoint> PointsAtChordLengthFromPoint(VPoint point, double chordLength)
    {
        var projected = Project(point);
        var distFromOrigin = (projected.AsVXYZ() - Origin.AsVXYZ()).DotProduct(Direction);

        var results = new List<VPoint>();

        // Point in positive direction
        results.Add((projected.AsVXYZ() + Direction * chordLength).AsVPoint());

        // Point in negative direction (only if it's still on the ray)
        double negDist = distFromOrigin - chordLength;
        if (negDist >= 0)
        {
            results.Add((projected.AsVXYZ() - Direction * chordLength).AsVPoint());
        }

        return results;
    }

    /// <summary>
    /// Splits the ray at a point, returning a line segment and a ray.
    /// </summary>
    public (ICurve, ICurve) SplitAtPoint(VPoint point)
    {
        var splitPoint = Project(point);
        // First part is a line from Origin to splitPoint
        var line = new VLine((VPoint)Origin.Clone(), (VPoint)splitPoint.Clone());
        // Second part is a ray from splitPoint in the same direction
        var ray = new VRay((VPoint)splitPoint.Clone(), Direction);
        return (line, ray);
    }

    /// <summary>
    /// Computes the intersection between this ray and another curve.
    /// </summary>
    public IntersectionResult Intersect(ICurve other)
    {
        return CurveIntersection.Intersect(this, other);
    }

    /// <summary>
    /// Converts this ray to a finite VLine segment for intersection calculations.
    /// </summary>
    public VLine ToFiniteLine()
    {
        return new VLine(StartPoint, EndPoint);
    }

    /// <summary>
    /// Converts this ray to an infinite VXLine.
    /// </summary>
    public VXLine ToXLine()
    {
        return new VXLine(Origin, Direction);
    }

    /// <summary>
    /// Checks if a point is on the ray (within tolerance).
    /// </summary>
    public bool ContainsPoint(VPoint point)
    {
        var projected = Project(point);
        return projected.DistanceTo(point) < GeometryTolerance.Epsilon;
    }

    public override string ToString() => $"VRay(Origin:{Origin}, Dir:{Direction})";
}
