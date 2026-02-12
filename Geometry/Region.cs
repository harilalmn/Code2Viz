using System;
using System.Collections.Generic;
using System.Linq;

using Code2Viz.Canvas;

namespace Code2Viz.Geometry;

/// <summary>
/// Represents an enclosed 2D region bounded by curves (lines, arcs, splines, beziers).
/// Unlike VPolygon which only supports straight edges, Region preserves the original
/// curve geometry in its boundary loops.
/// A Region must not self-intersect. It has an OuterLoop and optional Holes.
/// </summary>
public class Region : Shape
{
    private const double ConnectionTolerance = 1e-6;
    private const int DefaultSegmentsPerCurve = 32;

    /// <summary>
    /// The outer boundary of the region as an ordered list of curves forming a closed loop.
    /// Curves are stored in traversal order: the end of each curve connects to the start of the next.
    /// </summary>
    public List<ICurve> OuterLoop { get; private set; }

    /// <summary>
    /// Inner holes of the region. Each hole is an ordered list of curves forming a closed loop.
    /// </summary>
    public List<List<ICurve>> Holes { get; private set; } = new();

    #region Constructors

    /// <summary>
    /// Creates a Region from a list of curves that form a closed, non-self-intersecting outer boundary.
    /// Curves will be automatically ordered to form a continuous closed loop.
    /// </summary>
    /// <param name="curves">A list of open curves that should form a closed loop.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when:
    /// - Any curve is closed (StartPoint == EndPoint)
    /// - Curves cannot form a single continuous loop
    /// - Branching is detected (more than 2 curve endpoints meet at a point)
    /// - Self-intersection is detected
    /// </exception>
    public Region(List<ICurve> curves)
    {
        if (curves == null || curves.Count == 0)
            throw new ArgumentException("At least one curve is required.", nameof(curves));

        ValidateOpenCurves(curves);
        ValidateNoBranching(curves);

        var orderedCurves = OrderCurvesToFormLoop(curves);
        ValidateNoSelfIntersections(orderedCurves);

        OuterLoop = orderedCurves.Select(t => t.reversed ? ReverseCurve(t.curve) : t.curve).ToList();

        Color = "LightBlue";
        FillColor = "Transparent";
    }

    /// <summary>
    /// Creates a Region from an outer boundary and a list of holes.
    /// </summary>
    /// <param name="outerCurves">Curves forming the outer boundary.</param>
    /// <param name="holes">Each item is a list of curves forming a hole.</param>
    public Region(List<ICurve> outerCurves, List<List<ICurve>> holes)
        : this(outerCurves)
    {
        if (holes != null)
        {
            foreach (var holeCurves in holes)
            {
                AddHole(holeCurves);
            }
        }
    }

    /// <summary>
    /// Internal constructor that skips validation — used for boolean operation results
    /// where the loops are already known to be valid.
    /// </summary>
    internal Region(List<ICurve> outerLoop, List<List<ICurve>> holes, bool skipValidation)
        : base(false) // skip auto-registration
    {
        OuterLoop = outerLoop ?? throw new ArgumentNullException(nameof(outerLoop));
        Holes = holes ?? new List<List<ICurve>>();
        Color = "LightBlue";
        FillColor = "Transparent";
    }

    #endregion

    #region Hole Management

    /// <summary>
    /// Adds a hole to the region. The hole curves must form a closed, non-self-intersecting loop.
    /// The hole must be entirely inside the outer boundary.
    /// </summary>
    public void AddHole(List<ICurve> holeCurves)
    {
        if (holeCurves == null || holeCurves.Count == 0)
            throw new ArgumentException("At least one curve is required for a hole.");

        ValidateOpenCurves(holeCurves);
        ValidateNoBranching(holeCurves);

        var orderedCurves = OrderCurvesToFormLoop(holeCurves);
        ValidateNoSelfIntersections(orderedCurves);

        var holeFinal = orderedCurves.Select(t => t.reversed ? ReverseCurve(t.curve) : t.curve).ToList();
        Holes.Add(holeFinal);
    }

    #endregion

    #region Geometric Properties

    /// <summary>
    /// Gets the area of the region (outer area minus hole areas).
    /// Computed via polygon approximation of the boundary curves.
    /// </summary>
    public double Area
    {
        get
        {
            double area = Math.Abs(ComputeSignedArea(OuterLoop));
            foreach (var hole in Holes)
            {
                area -= Math.Abs(ComputeSignedArea(hole));
            }
            return Math.Max(0, area);
        }
    }

    /// <summary>
    /// Gets the signed area of the outer loop.
    /// Positive for counter-clockwise, negative for clockwise.
    /// </summary>
    public double SignedArea => ComputeSignedArea(OuterLoop);

    /// <summary>
    /// Gets the total perimeter length (outer loop + all holes).
    /// </summary>
    public double Perimeter
    {
        get
        {
            double length = GetLoopLength(OuterLoop);
            foreach (var hole in Holes)
            {
                length += GetLoopLength(hole);
            }
            return length;
        }
    }

    #endregion

    #region Point Containment

    /// <summary>
    /// Checks if a point is inside this region (inside the outer loop, outside all holes).
    /// Uses the winding number algorithm on a polygon approximation.
    /// </summary>
    public override bool Contains(VPoint point)
    {
        var outerPoints = SampleLoop(OuterLoop, DefaultSegmentsPerCurve);
        if (!PolygonClipper.PointInPolygonTest(point, outerPoints))
            return false;

        foreach (var hole in Holes)
        {
            var holePoints = SampleLoop(hole, DefaultSegmentsPerCurve);
            if (PolygonClipper.PointInPolygonTest(point, holePoints))
                return false;
        }

        return true;
    }

    #endregion

    #region Conversion

    /// <summary>
    /// Converts the region to a VPolygon using the endpoints of each curve (low-fidelity).
    /// Curved segments become straight edges between their endpoints.
    /// </summary>
    public VPolygon ToPolygon()
    {
        var points = new List<VPoint>();
        foreach (var curve in OuterLoop)
        {
            if (points.Count == 0 || !PointsAreClose(points[^1], curve.StartPoint))
                points.Add(curve.StartPoint);
        }
        // Remove last if it duplicates first
        if (points.Count > 1 && PointsAreClose(points[0], points[^1]))
            points.RemoveAt(points.Count - 1);

        return new VPolygon(points);
    }

    /// <summary>
    /// Converts the region to a VPolygon by densely sampling each curve (high-fidelity).
    /// </summary>
    /// <param name="segmentsPerCurve">Number of line segments per curve in the approximation.</param>
    public VPolygon ToPolygonHighRes(int segmentsPerCurve = DefaultSegmentsPerCurve)
    {
        var points = SampleLoop(OuterLoop, segmentsPerCurve);
        return new VPolygon(points);
    }

    /// <summary>
    /// Converts the region to a PolygonWithHoles (high-fidelity polygon approximation).
    /// </summary>
    public PolygonWithHoles ToPolygonWithHoles(int segmentsPerCurve = DefaultSegmentsPerCurve)
    {
        var outerPoly = new VPolygon(SampleLoop(OuterLoop, segmentsPerCurve));
        var result = new PolygonWithHoles(outerPoly);

        foreach (var hole in Holes)
        {
            var holePoly = new VPolygon(SampleLoop(hole, segmentsPerCurve));
            result.AddHole(holePoly);
        }

        return result;
    }

    /// <summary>
    /// Creates a Region from a VPolygon. Each polygon edge becomes a VLine in the region's OuterLoop.
    /// </summary>
    public static Region FromPolygon(VPolygon polygon)
    {
        var curves = new List<ICurve>();
        for (int i = 0; i < polygon.Points.Count; i++)
        {
            int next = (i + 1) % polygon.Points.Count;
            curves.Add(new VLine(polygon.Points[i], polygon.Points[next]));
        }

        // Use internal constructor (skip validation — polygon is already validated)
        return new Region(curves, new List<List<ICurve>>(), skipValidation: true);
    }

    /// <summary>
    /// Creates a Region from a PolygonWithHoles.
    /// </summary>
    public static Region FromPolygonWithHoles(PolygonWithHoles pwh)
    {
        var region = FromPolygon(pwh.Outer);
        foreach (var hole in pwh.Holes)
        {
            var holeCurves = new List<ICurve>();
            for (int i = 0; i < hole.Points.Count; i++)
            {
                int next = (i + 1) % hole.Points.Count;
                holeCurves.Add(new VLine(hole.Points[i], hole.Points[next]));
            }
            region.Holes.Add(holeCurves);
        }
        return region;
    }

    #endregion

    #region Shape Abstract Methods

    public override Shape Clone()
    {
        var clonedOuter = OuterLoop.Select(CloneCurve).ToList();
        var clonedHoles = Holes.Select(h => h.Select(CloneCurve).ToList()).ToList();
        var clone = new Region(clonedOuter, clonedHoles, skipValidation: true);
        CopyStyleTo(clone);
        return clone;
    }

    public override void Move(VXYZ vector)
    {
        var movedPoints = new HashSet<VPoint>(ReferenceEqualityComparer.Instance);
        MoveUniquePoints(OuterLoop, vector, movedPoints);
        foreach (var hole in Holes)
            MoveUniquePoints(hole, vector, movedPoints);
    }

    public override void Rotate(VPoint pivot, double angleDegrees)
    {
        var rotatedPoints = new HashSet<VPoint>(ReferenceEqualityComparer.Instance);
        RotateUniquePoints(OuterLoop, pivot, angleDegrees, rotatedPoints);
        foreach (var hole in Holes)
            RotateUniquePoints(hole, pivot, angleDegrees, rotatedPoints);
    }

    public override void Flip(VLine mirrorLine)
    {
        var flippedPoints = new HashSet<VPoint>(ReferenceEqualityComparer.Instance);
        FlipUniquePoints(OuterLoop, mirrorLine, flippedPoints);
        foreach (var hole in Holes)
            FlipUniquePoints(hole, mirrorLine, flippedPoints);
    }

    public override void Scale(VPoint center, double factor)
    {
        var scaledPoints = new HashSet<VPoint>(ReferenceEqualityComparer.Instance);
        ScaleUniquePoints(OuterLoop, center, factor, scaledPoints);
        foreach (var hole in Holes)
            ScaleUniquePoints(hole, center, factor, scaledPoints);
    }

    public override BoundingBox GetBounds()
    {
        var allPoints = new List<VPoint>();
        foreach (var curve in OuterLoop)
        {
            var bounds = ((Shape)curve).GetBounds();
            allPoints.Add(bounds.Min);
            allPoints.Add(bounds.Max);
        }

        if (allPoints.Count == 0) return new BoundingBox(VPoint.Internal(0, 0), VPoint.Internal(0, 0));

        double minX = allPoints.Min(p => p.X);
        double minY = allPoints.Min(p => p.Y);
        double maxX = allPoints.Max(p => p.X);
        double maxY = allPoints.Max(p => p.Y);
        return new BoundingBox(VPoint.Internal(minX, minY), VPoint.Internal(maxX, maxY));
    }

    public override string ToString()
    {
        int totalCurves = OuterLoop.Count + Holes.Sum(h => h.Count);
        return $"Region(Outer: {OuterLoop.Count} curves, Holes: {Holes.Count}, Total: {totalCurves} curves)";
    }

    #endregion

    #region Loop Sampling

    /// <summary>
    /// Samples a curve loop into a list of points for polygon approximation.
    /// </summary>
    internal static List<VPoint> SampleLoop(List<ICurve> loop, int segmentsPerCurve)
    {
        var points = new List<VPoint>();

        foreach (var curve in loop)
        {
            List<VPoint> curvePoints;

            if (curve is VLine line)
            {
                curvePoints = new List<VPoint> { line.StartPoint, line.EndPoint };
            }
            else
            {
                curvePoints = curve.Divide(segmentsPerCurve);
            }

            // Add all points except the last (to avoid duplication with next curve's start)
            for (int i = 0; i < curvePoints.Count - 1; i++)
            {
                if (points.Count == 0 || !PointsAreClose(points[^1], curvePoints[i]))
                    points.Add(curvePoints[i]);
            }
        }

        // Remove the last point if it duplicates the first (closed loop)
        if (points.Count > 1 && PointsAreClose(points[0], points[^1]))
            points.RemoveAt(points.Count - 1);

        return points;
    }

    #endregion

    #region Validation (reused from VPolygon pattern)

    private static void ValidateOpenCurves(List<ICurve> curves)
    {
        for (int i = 0; i < curves.Count; i++)
        {
            var curve = curves[i];
            if (PointsAreClose(curve.StartPoint, curve.EndPoint))
            {
                string curveType = curve.GetType().Name;
                throw new ArgumentException(
                    $"Curve at index {i} ({curveType}) is a closed curve. " +
                    "Only open curves (where StartPoint != EndPoint) are accepted.");
            }
        }
    }

    private static void ValidateNoBranching(List<ICurve> curves)
    {
        var endpoints = new List<(VPoint point, int curveIndex, bool isStart)>();
        for (int i = 0; i < curves.Count; i++)
        {
            endpoints.Add((curves[i].StartPoint, i, true));
            endpoints.Add((curves[i].EndPoint, i, false));
        }

        for (int i = 0; i < endpoints.Count; i++)
        {
            int connectionCount = 0;
            var (point, curveIdx, _) = endpoints[i];

            for (int j = 0; j < endpoints.Count; j++)
            {
                if (i == j) continue;
                if (endpoints[j].curveIndex == curveIdx) continue;
                if (PointsAreClose(point, endpoints[j].point))
                    connectionCount++;
            }

            if (connectionCount > 1)
            {
                throw new ArgumentException(
                    $"Branching detected at point ({point.X:F2}, {point.Y:F2}). " +
                    $"Found {connectionCount + 1} curve endpoints meeting at this location. " +
                    "Each point should connect exactly 2 curves to form a simple loop.");
            }
        }
    }

    private static List<(ICurve curve, bool reversed)> OrderCurvesToFormLoop(List<ICurve> curves)
    {
        if (curves.Count == 1)
        {
            // Single curve that closes on itself — already validated as open, so this is just one segment
            // It can't close by itself, need at least 2 curves
            throw new ArgumentException("At least 2 open curves are required to form a closed loop.");
        }

        var remaining = new List<ICurve>(curves);
        var ordered = new List<(ICurve curve, bool reversed)>();

        var firstCurve = remaining[0];
        remaining.RemoveAt(0);
        ordered.Add((firstCurve, false));

        VPoint currentEnd = firstCurve.EndPoint;

        while (remaining.Count > 0)
        {
            bool found = false;

            for (int i = 0; i < remaining.Count; i++)
            {
                var candidate = remaining[i];

                if (PointsAreClose(candidate.StartPoint, currentEnd))
                {
                    ordered.Add((candidate, false));
                    currentEnd = candidate.EndPoint;
                    remaining.RemoveAt(i);
                    found = true;
                    break;
                }

                if (PointsAreClose(candidate.EndPoint, currentEnd))
                {
                    ordered.Add((candidate, true));
                    currentEnd = candidate.StartPoint;
                    remaining.RemoveAt(i);
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                // Try building from the start of the chain
                var firstOrdered = ordered[0];
                VPoint currentStart = firstOrdered.reversed ? firstOrdered.curve.EndPoint : firstOrdered.curve.StartPoint;

                for (int i = 0; i < remaining.Count; i++)
                {
                    var candidate = remaining[i];

                    if (PointsAreClose(candidate.EndPoint, currentStart))
                    {
                        ordered.Insert(0, (candidate, false));
                        remaining.RemoveAt(i);
                        found = true;
                        break;
                    }

                    if (PointsAreClose(candidate.StartPoint, currentStart))
                    {
                        ordered.Insert(0, (candidate, true));
                        remaining.RemoveAt(i);
                        found = true;
                        break;
                    }
                }
            }

            if (!found)
                throw new ArgumentException(
                    "Curves are not connected. Cannot form a continuous loop. " +
                    $"{remaining.Count} curve(s) could not be connected to the chain.");
        }

        // Verify the loop is closed
        var first = ordered[0];
        var last = ordered[^1];
        VPoint loopStart = first.reversed ? first.curve.EndPoint : first.curve.StartPoint;
        VPoint loopEnd = last.reversed ? last.curve.StartPoint : last.curve.EndPoint;

        if (!PointsAreClose(loopStart, loopEnd))
            throw new ArgumentException(
                "Curves do not form a closed loop. " +
                $"Loop start ({loopStart.X:F2}, {loopStart.Y:F2}) does not connect to " +
                $"loop end ({loopEnd.X:F2}, {loopEnd.Y:F2}).");

        return ordered;
    }

    private static void ValidateNoSelfIntersections(List<(ICurve curve, bool reversed)> orderedCurves)
    {
        // Sample all curves and check for segment intersections
        int n = orderedCurves.Count;
        var allSegments = new List<List<(VPoint p1, VPoint p2)>>();
        var curveEndpoints = new List<(VPoint start, VPoint end)>();

        for (int i = 0; i < n; i++)
        {
            var (curve, reversed) = orderedCurves[i];
            var segments = GetCurveSegments(curve, reversed);
            allSegments.Add(segments);

            VPoint start = reversed ? curve.EndPoint : curve.StartPoint;
            VPoint end = reversed ? curve.StartPoint : curve.EndPoint;
            curveEndpoints.Add((start, end));
        }

        for (int i = 0; i < n; i++)
        {
            for (int j = i; j < n; j++)
            {
                bool sameCurve = (i == j);
                bool adjacent = !sameCurve && (Math.Abs(i - j) == 1 || Math.Abs(i - j) == n - 1);

                VPoint? allowedPt = null;
                if (adjacent)
                {
                    allowedPt = GetSharedEndpoint(curveEndpoints[i], curveEndpoints[j]);
                }

                var seg1 = allSegments[i];
                var seg2 = allSegments[j];

                for (int k = 0; k < seg1.Count; k++)
                {
                    int startL = sameCurve ? k + 2 : 0;
                    for (int l = startL; l < seg2.Count; l++)
                    {
                        var s1 = seg1[k];
                        var s2 = seg2[l];

                        bool shareEndpt = SegmentsShareEndpoint(s1, s2);

                        if (SegmentsIntersect(s1.p1, s1.p2, s2.p1, s2.p2, shareEndpt, allowedPt, out VPoint? ix))
                        {
                            string err = sameCurve ? "Self-intersection within a curve" : "Intersection between curves";
                            throw new ArgumentException(
                                $"{err} detected at approximately ({ix!.X:F2}, {ix.Y:F2}). " +
                                "Curves must not cross each other except at their shared endpoints.");
                        }
                    }
                }
            }
        }
    }

    #endregion

    #region Helper Methods

    private static bool PointsAreClose(VPoint p1, VPoint p2)
    {
        return p1.DistanceTo(p2) < ConnectionTolerance;
    }

    private static VPoint? GetSharedEndpoint((VPoint start, VPoint end) c1, (VPoint start, VPoint end) c2)
    {
        if (PointsAreClose(c1.end, c2.start)) return c1.end;
        if (PointsAreClose(c1.start, c2.end)) return c1.start;
        if (PointsAreClose(c1.end, c2.end)) return c1.end;
        if (PointsAreClose(c1.start, c2.start)) return c1.start;
        return null;
    }

    private static bool SegmentsShareEndpoint((VPoint p1, VPoint p2) s1, (VPoint p1, VPoint p2) s2)
    {
        return PointsAreClose(s1.p1, s2.p1) || PointsAreClose(s1.p1, s2.p2) ||
               PointsAreClose(s1.p2, s2.p1) || PointsAreClose(s1.p2, s2.p2);
    }

    private static List<(VPoint p1, VPoint p2)> GetCurveSegments(ICurve curve, bool reversed)
    {
        var segments = new List<(VPoint p1, VPoint p2)>();
        List<VPoint> points;

        if (curve is VLine line)
        {
            points = new List<VPoint> { line.StartPoint, line.EndPoint };
        }
        else if (curve is VPolyline polyline)
        {
            points = polyline.Points.ToList();
        }
        else
        {
            points = curve.Divide(16);
        }

        if (reversed) points.Reverse();

        for (int i = 0; i < points.Count - 1; i++)
            segments.Add((points[i], points[i + 1]));

        return segments;
    }

    private static bool SegmentsIntersect(VPoint p1, VPoint p2, VPoint p3, VPoint p4,
                                          bool segmentsShareEndpoint, VPoint? allowedIntersection,
                                          out VPoint? intersection)
    {
        intersection = null;

        double d1x = p2.X - p1.X, d1y = p2.Y - p1.Y;
        double d2x = p4.X - p3.X, d2y = p4.Y - p3.Y;
        double cross = d1x * d2y - d1y * d2x;

        if (GeometryTolerance.IsZero(cross))
        {
            // Check collinear overlap
            double dx = p2.X - p1.X, dy = p2.Y - p1.Y;
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 1e-10) return false;
            dx /= len; dy /= len;

            double perpDist3 = Math.Abs((p3.X - p1.X) * (-dy) + (p3.Y - p1.Y) * dx);
            double perpDist4 = Math.Abs((p4.X - p1.X) * (-dy) + (p4.Y - p1.Y) * dx);
            if (perpDist3 > ConnectionTolerance || perpDist4 > ConnectionTolerance) return false;

            double t1 = 0, t2 = len;
            double t3 = (p3.X - p1.X) * dx + (p3.Y - p1.Y) * dy;
            double t4 = (p4.X - p1.X) * dx + (p4.Y - p1.Y) * dy;
            if (t3 > t4) (t3, t4) = (t4, t3);

            double overlapStart = Math.Max(t1, t3);
            double overlapEnd = Math.Min(t2, t4);
            if (overlapEnd <= overlapStart + ConnectionTolerance) return false;

            bool canTouch = segmentsShareEndpoint || allowedIntersection != null;
            if (canTouch && Math.Abs(overlapEnd - overlapStart) < ConnectionTolerance * 2) return false;

            intersection = VPoint.Internal((p1.X + p3.X) / 2, (p1.Y + p3.Y) / 2);
            return true;
        }

        double dxC = p3.X - p1.X, dyC = p3.Y - p1.Y;
        double t = (dxC * d2y - dyC * d2x) / cross;
        double u = (dxC * d1y - dyC * d1x) / cross;

        if (t >= 0 && t <= 1 && u >= 0 && u <= 1)
        {
            double ix = p1.X + t * d1x, iy = p1.Y + t * d1y;
            intersection = VPoint.Internal(ix, iy);

            if (allowedIntersection != null && PointsAreClose(intersection, allowedIntersection))
                return false;

            if (segmentsShareEndpoint)
            {
                bool atSharedEndpoint =
                    (PointsAreClose(intersection, p1) || PointsAreClose(intersection, p2)) &&
                    (PointsAreClose(intersection, p3) || PointsAreClose(intersection, p4));
                return !atSharedEndpoint;
            }

            return true;
        }

        return false;
    }

    private static double ComputeSignedArea(List<ICurve> loop)
    {
        var points = SampleLoop(loop, DefaultSegmentsPerCurve);
        if (points.Count < 3) return 0;

        double area = 0;
        for (int i = 0; i < points.Count; i++)
        {
            int j = (i + 1) % points.Count;
            area += points[i].X * points[j].Y;
            area -= points[j].X * points[i].Y;
        }
        return area / 2.0;
    }

    private static double GetLoopLength(List<ICurve> loop)
    {
        return loop.Sum(c => c.GetLength());
    }

    private static ICurve CloneCurve(ICurve curve)
    {
        if (curve is Shape shape)
            return (ICurve)shape.Clone();

        // Fallback: create a VLine from endpoints
        return new VLine(curve.StartPoint.Clone(), curve.EndPoint.Clone());
    }

    private static ICurve ReverseCurve(ICurve curve)
    {
        // For lines, just swap endpoints
        if (curve is VLine line)
            return new VLine(line.EndPoint, line.StartPoint);

        // For arcs, reverse the sweep
        if (curve is VArc arc)
            return new VArc(arc.Center, arc.Radius, arc.EndAngle, arc.StartAngle);

        // For other curves, approximate with a reversed polyline of sampled points
        var points = curve.Divide(DefaultSegmentsPerCurve);
        points.Reverse();

        // If we have just 2 points, return a line
        if (points.Count <= 2)
            return new VLine(points.First(), points.Last());

        return new VPolyline(points);
    }

    /// <summary>
    /// Moves points in a curve loop, tracking already-moved points to avoid double-moves
    /// when adjacent curves share VPoint instances.
    /// </summary>
    private static void MoveUniquePoints(List<ICurve> loop, VXYZ vector, HashSet<VPoint> processed)
    {
        foreach (var curve in loop)
        {
            if (curve is VLine line)
            {
                if (processed.Add(line.Start)) line.Start.Move(vector);
                if (processed.Add(line.End)) line.End.Move(vector);
            }
            else if (curve is Shape shape)
            {
                shape.Move(vector);
            }
        }
    }

    private static void RotateUniquePoints(List<ICurve> loop, VPoint pivot, double angleDegrees, HashSet<VPoint> processed)
    {
        foreach (var curve in loop)
        {
            if (curve is VLine line)
            {
                if (processed.Add(line.Start)) line.Start.Rotate(pivot, angleDegrees);
                if (processed.Add(line.End)) line.End.Rotate(pivot, angleDegrees);
            }
            else if (curve is Shape shape)
            {
                shape.Rotate(pivot, angleDegrees);
            }
        }
    }

    private static void FlipUniquePoints(List<ICurve> loop, VLine mirrorLine, HashSet<VPoint> processed)
    {
        foreach (var curve in loop)
        {
            if (curve is VLine line)
            {
                if (processed.Add(line.Start)) line.Start.Flip(mirrorLine);
                if (processed.Add(line.End)) line.End.Flip(mirrorLine);
            }
            else if (curve is Shape shape)
            {
                shape.Flip(mirrorLine);
            }
        }
    }

    private static void ScaleUniquePoints(List<ICurve> loop, VPoint center, double factor, HashSet<VPoint> processed)
    {
        foreach (var curve in loop)
        {
            if (curve is VLine line)
            {
                if (processed.Add(line.Start)) line.Start.Scale(center, factor);
                if (processed.Add(line.End)) line.End.Scale(center, factor);
            }
            else if (curve is Shape shape)
            {
                shape.Scale(center, factor);
            }
        }
    }

    #endregion
}
