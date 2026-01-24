using Code2Viz.Canvas;

namespace Code2Viz.Geometry;

public class VPolygon : Shape, ICurve
{
    public List<VPoint> Points { get; set; }
    public List<ICurve> Curves { get; private set; } = new();
    private readonly bool _selfIntersecting;

    /// <summary>Gets the start point of the polygon.</summary>
    public VPoint StartPoint => Points.Count > 0 ? Points[0] : new VPoint(0, 0);

    /// <summary>Gets the end point of the polygon (same as StartPoint, since it's closed).</summary>
    public VPoint EndPoint => StartPoint;

    /// <summary>Indicates whether the polygon intersects itself.</summary>
    public bool SelfIntersecting => _selfIntersecting;

    /// <summary>Gets the vertices of the polygon.</summary>
    public List<VPoint> Vertices => Points;

    /// <summary>
    /// Gets the area of the polygon using the shoelace formula.
    /// Always returns a positive value regardless of vertex winding order.
    /// </summary>
    public double Area
    {
        get
        {
            if (Points.Count < 3) return 0;

            double area = 0;
            for (int i = 0; i < Points.Count; i++)
            {
                int j = (i + 1) % Points.Count;
                area += Points[i].X * Points[j].Y;
                area -= Points[j].X * Points[i].Y;
            }
            return Math.Abs(area / 2.0);
        }
    }

    /// <summary>
    /// Gets the signed area of the polygon using the shoelace formula.
    /// Positive for counter-clockwise vertices, negative for clockwise.
    /// Useful for determining winding order.
    /// </summary>
    public double SignedArea
    {
        get
        {
            if (Points.Count < 3) return 0;

            double area = 0;
            for (int i = 0; i < Points.Count; i++)
            {
                int j = (i + 1) % Points.Count;
                area += Points[i].X * Points[j].Y;
                area -= Points[j].X * Points[i].Y;
            }
            return area / 2.0;
        }
    }

    public VPolygon(params VPoint[] points)
    {
        Points = points.ToList();
        StrokeColor = ShapeDefaults.GlobalStrokeColor ?? "LightBlue";
        FillColor = ShapeDefaults.GlobalFillColor ?? "Transparent";
        _selfIntersecting = CurveIntersection.IsPolylineSelfIntersecting(GetClosedPoints());
        BuildCurvesFromPoints();
    }

    public VPolygon(IEnumerable<VPoint> points)
    {
        Points = points.ToList();
        StrokeColor = "LightBlue";
        FillColor = "Transparent";
        _selfIntersecting = CurveIntersection.IsPolylineSelfIntersecting(GetClosedPoints());
        BuildCurvesFromPoints();
    }

    /// <summary>
    /// Builds the Curves list from the Points list.
    /// Each edge becomes a VLine in the Curves collection.
    /// </summary>
    protected void BuildCurvesFromPoints()
    {
        Curves.Clear();
        if (Points.Count < 2) return;

        for (int i = 0; i < Points.Count; i++)
        {
            int nextIndex = (i + 1) % Points.Count;
            Curves.Add(new VLine(Points[i], Points[nextIndex]));
        }
    }

    private List<VPoint> GetClosedPoints()
    {
        if (Points.Count == 0) return Points;
        var closed = new List<VPoint>(Points);
        if (closed.Count > 0 && closed[0].DistanceTo(closed[^1]) > 1e-6)
        {
            closed.Add(closed[0]);
        }
        return closed;
    }

    /// <summary>
    /// Creates a polygon from a list of open curves.
    /// The curves will be automatically ordered to form a continuous closed loop.
    /// </summary>
    /// <param name="curves">A list of open curves that should form a closed polygon.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when:
    /// - Any curve is closed (StartPoint == EndPoint)
    /// - Curves cannot form a single continuous loop
    /// - Branching is detected (more than 2 curve endpoints meet at a point)
    /// - Self-intersection is detected (curves cross each other)
    /// </exception>
    public VPolygon(List<ICurve> curves)
    {
        if (curves == null || curves.Count == 0)
            throw new ArgumentException("At least one curve is required.", nameof(curves));

        // Validate all curves are open (not closed)
        ValidateOpenCurves(curves);

        // Validate no branching (each endpoint connects to exactly one other endpoint)
        ValidateNoBranching(curves);

        // Try to order the curves to form a continuous loop
        var orderedCurves = OrderCurvesToFormLoop(curves);

        // Validate no self-intersections
        ValidateNoSelfIntersections(orderedCurves);

        // Extract vertices from the ordered curves
        Points = ExtractVerticesFromCurves(orderedCurves);

        // Store the curves (extract just the ICurve from the tuples)
        Curves = orderedCurves.Select(t => t.curve).ToList();

        StrokeColor = ShapeDefaults.GlobalStrokeColor ?? "LightBlue";
        FillColor = ShapeDefaults.GlobalFillColor ?? "Transparent";

        // Validation passed, so no self-intersection
        _selfIntersecting = false;
    }

    private const double ConnectionTolerance = 1e-6;

    /// <summary>
    /// Validates that all curves are open (StartPoint != EndPoint).
    /// </summary>
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

    /// <summary>
    /// Validates that no branching occurs (no more than 2 curve endpoints meet at any point).
    /// </summary>
    private static void ValidateNoBranching(List<ICurve> curves)
    {
        // Collect all endpoints
        var endpoints = new List<(VPoint point, int curveIndex, bool isStart)>();
        for (int i = 0; i < curves.Count; i++)
        {
            endpoints.Add((curves[i].StartPoint, i, true));
            endpoints.Add((curves[i].EndPoint, i, false));
        }

        // Check each endpoint for branching
        for (int i = 0; i < endpoints.Count; i++)
        {
            int connectionCount = 0;
            var (point, curveIdx, _) = endpoints[i];

            for (int j = 0; j < endpoints.Count; j++)
            {
                if (i == j) continue;

                // Don't count the same curve's other endpoint
                if (endpoints[j].curveIndex == curveIdx) continue;

                if (PointsAreClose(point, endpoints[j].point))
                {
                    connectionCount++;
                }
            }

            // Each endpoint should connect to at most 1 other endpoint (from a different curve)
            if (connectionCount > 1)
            {
                throw new ArgumentException(
                    $"Branching detected at point ({point.X:F2}, {point.Y:F2}). " +
                    $"Found {connectionCount + 1} curve endpoints meeting at this location. " +
                    "Each point should connect exactly 2 curves to form a simple loop.");
            }
        }
    }

    /// <summary>
    /// Orders a list of curves to form a continuous loop.
    /// Returns a list of tuples (curve, reversed) where reversed indicates if the curve should be traversed in reverse.
    /// </summary>
    private static List<(ICurve curve, bool reversed)> OrderCurvesToFormLoop(List<ICurve> curves)
    {
        if (curves.Count < 2)
            throw new ArgumentException("At least 2 open curves are required to form a closed loop.");

        var remaining = new List<ICurve>(curves);
        var ordered = new List<(ICurve curve, bool reversed)>();

        // Start with the first curve
        var firstCurve = remaining[0];
        remaining.RemoveAt(0);
        ordered.Add((firstCurve, false));

        VPoint currentEnd = firstCurve.EndPoint;

        // Try to chain all remaining curves
        while (remaining.Count > 0)
        {
            bool found = false;

            for (int i = 0; i < remaining.Count; i++)
            {
                var candidate = remaining[i];

                // Check if candidate's start connects to current end
                if (PointsAreClose(candidate.StartPoint, currentEnd))
                {
                    ordered.Add((candidate, false));
                    currentEnd = candidate.EndPoint;
                    remaining.RemoveAt(i);
                    found = true;
                    break;
                }

                // Check if candidate's end connects to current end (need to reverse it)
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
                // Try building from the start of the chain instead
                var firstOrdered = ordered[0];
                VPoint currentStart = firstOrdered.reversed ? firstOrdered.curve.EndPoint : firstOrdered.curve.StartPoint;

                for (int i = 0; i < remaining.Count; i++)
                {
                    var candidate = remaining[i];

                    // Check if candidate's end connects to current start
                    if (PointsAreClose(candidate.EndPoint, currentStart))
                    {
                        ordered.Insert(0, (candidate, false));
                        remaining.RemoveAt(i);
                        found = true;
                        break;
                    }

                    // Check if candidate's start connects to current start (need to reverse it)
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

    /// <summary>
    /// Validates that there are no intersections between curves except at their shared endpoints.
    /// This includes:
    /// 1. No curve should self-intersect
    /// 2. Non-adjacent curves should not intersect at all
    /// 3. Adjacent curves should only touch at their shared endpoint
    /// </summary>
    private static void ValidateNoSelfIntersections(List<(ICurve curve, bool reversed)> orderedCurves)
    {
        int n = orderedCurves.Count;

        // Get segments for each curve (approximated for curved types)
        var curveSegments = new List<List<(VPoint p1, VPoint p2)>>();
        var curveEndpoints = new List<(VPoint start, VPoint end)>();

        for (int i = 0; i < n; i++)
        {
            var (curve, reversed) = orderedCurves[i];
            var segments = GetCurveSegments(curve, reversed);
            curveSegments.Add(segments);

            // Store the effective start/end points after considering reversal
            VPoint start = reversed ? curve.EndPoint : curve.StartPoint;
            VPoint end = reversed ? curve.StartPoint : curve.EndPoint;
            curveEndpoints.Add((start, end));
        }

        // Check each pair of curves
        for (int i = 0; i < n; i++)
        {
            for (int j = i; j < n; j++)
            {
                bool sameCurve = (i == j);
                bool adjacentCurves = !sameCurve && AreCurvesAdjacent(i, j, n);

                // Determine the allowed intersection point (if any)
                VPoint? allowedIntersection = null;
                if (adjacentCurves)
                {
                    // Adjacent curves share exactly one endpoint
                    allowedIntersection = GetSharedEndpoint(curveEndpoints[i], curveEndpoints[j]);
                }

                // Check all segment pairs between curve i and curve j
                var segments1 = curveSegments[i];
                var segments2 = curveSegments[j];

                int startK = sameCurve ? 0 : 0;
                for (int k = 0; k < segments1.Count; k++)
                {
                    // For same curve, only check non-adjacent segments (skip k+1)
                    int startL = sameCurve ? k + 2 : 0;

                    for (int l = startL; l < segments2.Count; l++)
                    {
                        var seg1 = segments1[k];
                        var seg2 = segments2[l];

                        // Check if these specific segments share an endpoint (adjacent within same curve or at curve boundary)
                        bool segmentsShareEndpoint = SegmentsShareEndpoint(seg1, seg2);

                        if (SegmentsIntersect(seg1.p1, seg1.p2, seg2.p1, seg2.p2,
                                              segmentsShareEndpoint, allowedIntersection,
                                              out VPoint? intersection))
                        {
                            string errorType = sameCurve ? "Self-intersection within a curve" : "Intersection between curves";
                            throw new ArgumentException(
                                $"{errorType} detected at approximately ({intersection!.X:F2}, {intersection.Y:F2}). " +
                                "Curves must not cross each other except at their shared endpoints.");
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Checks if two curves are adjacent in the ordered loop.
    /// </summary>
    private static bool AreCurvesAdjacent(int i, int j, int totalCurves)
    {
        int diff = Math.Abs(i - j);
        return diff == 1 || diff == totalCurves - 1;
    }

    /// <summary>
    /// Gets the shared endpoint between two adjacent curves, if any.
    /// </summary>
    private static VPoint? GetSharedEndpoint((VPoint start, VPoint end) curve1, (VPoint start, VPoint end) curve2)
    {
        if (PointsAreClose(curve1.end, curve2.start)) return curve1.end;
        if (PointsAreClose(curve1.start, curve2.end)) return curve1.start;
        if (PointsAreClose(curve1.end, curve2.end)) return curve1.end;
        if (PointsAreClose(curve1.start, curve2.start)) return curve1.start;
        return null;
    }

    /// <summary>
    /// Checks if two segments share an endpoint.
    /// </summary>
    private static bool SegmentsShareEndpoint((VPoint p1, VPoint p2) seg1, (VPoint p1, VPoint p2) seg2)
    {
        return PointsAreClose(seg1.p1, seg2.p1) || PointsAreClose(seg1.p1, seg2.p2) ||
               PointsAreClose(seg1.p2, seg2.p1) || PointsAreClose(seg1.p2, seg2.p2);
    }

    /// <summary>
    /// Gets line segments approximating the curve.
    /// </summary>
    private static List<(VPoint p1, VPoint p2)> GetCurveSegments(ICurve curve, bool reversed)
    {
        var segments = new List<(VPoint p1, VPoint p2)>();

        // Get points along the curve
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
            // For curved types (Arc, Bezier, Spline, etc.), sample points
            points = curve.Divide(16); // 16 segments for approximation
        }

        if (reversed)
            points.Reverse();

        // Create segments from consecutive points
        for (int i = 0; i < points.Count - 1; i++)
        {
            segments.Add((points[i], points[i + 1]));
        }

        return segments;
    }

    /// <summary>
    /// Checks if two line segments intersect.
    /// </summary>
    /// <param name="p1">First endpoint of segment 1</param>
    /// <param name="p2">Second endpoint of segment 1</param>
    /// <param name="p3">First endpoint of segment 2</param>
    /// <param name="p4">Second endpoint of segment 2</param>
    /// <param name="segmentsShareEndpoint">True if the segments share an endpoint (adjacent segments)</param>
    /// <param name="allowedIntersection">If not null, intersection at this point is allowed (for adjacent curves)</param>
    /// <param name="intersection">The intersection point if found</param>
    /// <returns>True if segments intersect at a disallowed location</returns>
    private static bool SegmentsIntersect(VPoint p1, VPoint p2, VPoint p3, VPoint p4,
                                          bool segmentsShareEndpoint, VPoint? allowedIntersection,
                                          out VPoint? intersection)
    {
        intersection = null;

        double d1x = p2.X - p1.X;
        double d1y = p2.Y - p1.Y;
        double d2x = p4.X - p3.X;
        double d2y = p4.Y - p3.Y;

        double cross = d1x * d2y - d1y * d2x;

        // Parallel or collinear
        if (GeometryTolerance.IsZero(cross))
        {
            // Check for collinear overlap (excluding shared endpoints)
            if (AreCollinearAndOverlapping(p1, p2, p3, p4, segmentsShareEndpoint, allowedIntersection))
            {
                intersection = new VPoint((p1.X + p3.X) / 2, (p1.Y + p3.Y) / 2);
                return true;
            }
            return false;
        }

        double dx = p3.X - p1.X;
        double dy = p3.Y - p1.Y;

        double t = (dx * d2y - dy * d2x) / cross;
        double u = (dx * d1y - dy * d1x) / cross;

        // Check if intersection is within both segments
        if (t >= 0 && t <= 1 && u >= 0 && u <= 1)
        {
            double ix = p1.X + t * d1x;
            double iy = p1.Y + t * d1y;
            intersection = new VPoint(ix, iy);

            // Check if intersection is at an allowed point
            if (allowedIntersection != null && PointsAreClose(intersection, allowedIntersection))
            {
                return false; // Allowed intersection at curve endpoint
            }

            // If segments share an endpoint, allow intersection only at that shared endpoint
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

    /// <summary>
    /// Checks if two collinear segments overlap (excluding allowed intersection points).
    /// </summary>
    private static bool AreCollinearAndOverlapping(VPoint p1, VPoint p2, VPoint p3, VPoint p4,
                                                    bool segmentsShareEndpoint, VPoint? allowedIntersection)
    {
        // Project all points onto the line direction
        double dx = p2.X - p1.X;
        double dy = p2.Y - p1.Y;
        double len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 1e-10) return false;

        dx /= len;
        dy /= len;

        // First, verify the segments are actually collinear (not just parallel)
        // by checking perpendicular distance from p3 and p4 to the line p1-p2
        // Perpendicular distance = |(p3 - p1) x direction| where x is 2D cross product
        double perpDist3 = Math.Abs((p3.X - p1.X) * (-dy) + (p3.Y - p1.Y) * dx);
        double perpDist4 = Math.Abs((p4.X - p1.X) * (-dy) + (p4.Y - p1.Y) * dx);

        // If either point is too far from the line, segments are parallel but not collinear
        if (perpDist3 > ConnectionTolerance || perpDist4 > ConnectionTolerance)
            return false;

        double t1 = 0;
        double t2 = len;
        double t3 = (p3.X - p1.X) * dx + (p3.Y - p1.Y) * dy;
        double t4 = (p4.X - p1.X) * dx + (p4.Y - p1.Y) * dy;

        // Ensure t3 < t4
        if (t3 > t4) (t3, t4) = (t4, t3);

        // Check for overlap
        double overlapStart = Math.Max(t1, t3);
        double overlapEnd = Math.Min(t2, t4);

        if (overlapEnd <= overlapStart + ConnectionTolerance)
            return false; // No overlap or just touching at a point

        // If segments share an endpoint or there's an allowed intersection,
        // touching at a single point is acceptable
        bool canTouchAtPoint = segmentsShareEndpoint || allowedIntersection != null;
        if (canTouchAtPoint && Math.Abs(overlapEnd - overlapStart) < ConnectionTolerance * 2)
            return false;

        return true;
    }

    /// <summary>
    /// Extracts vertices from ordered curves to form a polygon.
    /// </summary>
    private static List<VPoint> ExtractVerticesFromCurves(List<(ICurve curve, bool reversed)> orderedCurves)
    {
        var points = new List<VPoint>();

        foreach (var (curve, reversed) in orderedCurves)
        {
            // Get the starting point of this curve segment
            VPoint startPt = reversed ? curve.EndPoint : curve.StartPoint;

            // Add the start point if it's not already added (or if it's the first point)
            if (points.Count == 0 || !PointsAreClose(points[^1], startPt))
            {
                points.Add(startPt);
            }

            // For curves that have intermediate points (polylines), add those too
            if (curve is VPolyline polyline)
            {
                var polyPoints = reversed
                    ? polyline.Points.Skip(1).Take(polyline.Points.Count - 2).Reverse().ToList()
                    : polyline.Points.Skip(1).Take(polyline.Points.Count - 2).ToList();
                points.AddRange(polyPoints);
            }
            // For other curve types (Line, Arc, Bezier, Spline, etc.),
            // we only use start and end points for the polygon vertices
        }

        // Remove the last point if it duplicates the first (since polygon is implicitly closed)
        if (points.Count > 1 && PointsAreClose(points[0], points[^1]))
        {
            points.RemoveAt(points.Count - 1);
        }

        return points;
    }

    private static bool PointsAreClose(VPoint p1, VPoint p2)
    {
        return p1.DistanceTo(p2) < ConnectionTolerance;
    }

    public void AddPoint(VPoint point)
    {
        Points.Add(point);
        BuildCurvesFromPoints();
    }

    public void AddPoint(double x, double y)
    {
        Points.Add(new VPoint(x, y));
        BuildCurvesFromPoints();
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

        // Clone all points to ensure independent curves
        var l1 = new List<VPoint>();
        for(int i=0; i<=segmentIndex; i++) l1.Add((VPoint)Points[i].Clone());
        l1.Add((VPoint)p.Clone());

        var l2 = new List<VPoint>();
        l2.Add((VPoint)p.Clone());
        for(int i=segmentIndex+1; i<Points.Count; i++) l2.Add((VPoint)Points[i].Clone());
        l2.Add((VPoint)Points[0].Clone()); // Close the second part back to Start

        return (new VPolyline(l1), new VPolyline(l2));
    }

    public VXYZ NormalAtPoint(VPoint p)
    {
        return GeometryHelper.GetPolylineNormalAtPoint(Points, p, true);
    }

    /// <summary>
    /// Computes the intersection between this polygon and another curve.
    /// </summary>
    public IntersectionResult Intersect(ICurve other)
    {
        return CurveIntersection.Intersect(this, other);
    }

    /// <summary>
    /// Returns a point on the polygon perimeter at the given normalized parameter.
    /// Parameter is distributed evenly across segments (not by arc length).
    /// Parameter 0 and 1 both return the first point (closed curve).
    /// </summary>
    public VPoint PointAtParameter(double parameter)
    {
        if (Points.Count == 0) return new VPoint(0, 0);
        if (Points.Count == 1) return Points[0];
        if (parameter <= 0 || parameter >= 1) return Points[0];

        int numSegments = Points.Count; // Closed polygon has N segments for N points
        double scaledT = parameter * numSegments;
        int segmentIndex = Math.Min((int)scaledT, numSegments - 1);
        double localT = scaledT - segmentIndex;

        VPoint p1 = Points[segmentIndex];
        VPoint p2 = Points[(segmentIndex + 1) % Points.Count];

        return new VPoint(
            p1.X + (p2.X - p1.X) * localT,
            p1.Y + (p2.Y - p1.Y) * localT
        );
    }

    /// <summary>
    /// Slices the polygon along an infinite line defined by two points.
    /// Returns a list of resulting polygons. If the line doesn't intersect the polygon
    /// or only touches it at one point, returns a list containing a clone of the original.
    /// </summary>
    /// <param name="linePoint1">First point defining the slice line</param>
    /// <param name="linePoint2">Second point defining the slice line</param>
    /// <returns>List of polygons resulting from the slice operation</returns>
    public List<VPolygon> Slice(VPoint linePoint1, VPoint linePoint2)
    {
        var result = new List<VPolygon>();

        // Need at least 3 points to have a valid polygon
        if (Points.Count < 3)
        {
            result.Add((VPolygon)this.Clone());
            return result;
        }

        // Find all intersection points between the slice line and polygon edges
        var intersections = FindSliceIntersections(linePoint1, linePoint2);

        // Need at least 2 intersection points to slice (and must be even number)
        if (intersections.Count < 2 || intersections.Count % 2 != 0)
        {
            result.Add((VPolygon)this.Clone());
            return result;
        }

        // Sort intersections by their position along the polygon perimeter
        intersections = intersections.OrderBy(x => x.edgeIndex).ThenBy(x => x.t).ToList();

        // For a proper slice, we need pairs of entry/exit points
        // Build the resulting polygons by walking around and splitting at intersections
        result = BuildSlicedPolygons(intersections);

        // Copy styling to all result polygons
        foreach (var poly in result)
        {
            CopyStyleTo(poly);
        }

        return result;
    }

    /// <summary>
    /// Slices the polygon along an infinite construction line (VXLine).
    /// Returns a list of resulting polygons. If the line doesn't intersect the polygon
    /// or only touches it at one point, returns a list containing a clone of the original.
    /// </summary>
    /// <param name="xline">The infinite construction line to slice along</param>
    /// <returns>List of polygons resulting from the slice operation</returns>
    public List<VPolygon> Slice(VXLine xline)
    {
        var (p1, p2) = xline.GetTwoPoints();
        return Slice(p1, p2);
    }

    /// <summary>
    /// Slices the polygon along a ray (VRay).
    /// Returns a list of resulting polygons. If the ray doesn't intersect the polygon
    /// or only touches it at one point, returns a list containing a clone of the original.
    /// </summary>
    /// <param name="ray">The ray to slice along (treated as an infinite line)</param>
    /// <returns>List of polygons resulting from the slice operation</returns>
    public List<VPolygon> Slice(VRay ray)
    {
        // Treat the ray as an infinite line for slicing purposes
        return Slice(ray.Origin, ray.GetPointAtDistance(1));
    }

    /// <summary>
    /// Finds all intersection points between an infinite line and the polygon edges.
    /// </summary>
    private List<(int edgeIndex, double t, VPoint point)> FindSliceIntersections(VPoint p1, VPoint p2)
    {
        var intersections = new List<(int edgeIndex, double t, VPoint point)>();

        // Direction vector of the slice line
        double lineDx = p2.X - p1.X;
        double lineDy = p2.Y - p1.Y;

        for (int i = 0; i < Points.Count; i++)
        {
            VPoint edgeStart = Points[i];
            VPoint edgeEnd = Points[(i + 1) % Points.Count];

            // Edge direction
            double edgeDx = edgeEnd.X - edgeStart.X;
            double edgeDy = edgeEnd.Y - edgeStart.Y;

            // Cross product to check if parallel
            double cross = lineDx * edgeDy - lineDy * edgeDx;

            if (Math.Abs(cross) < 1e-10)
            {
                // Lines are parallel, no intersection (or collinear - ignore for slicing)
                continue;
            }

            // Vector from p1 to edge start
            double dx = edgeStart.X - p1.X;
            double dy = edgeStart.Y - p1.Y;

            // Parameter t on the edge (0 to 1 means intersection is on the edge)
            // Using line-line intersection formula: t = ((P2-P1) × D1) / (D1 × D2)
            double t = (dx * lineDy - dy * lineDx) / cross;

            // Parameter s on the slice line (we don't restrict this - line is infinite)
            double s = (dx * edgeDy - dy * edgeDx) / cross;

            // Check if intersection is within the edge segment (t in [0, 1])
            // Use small epsilon to avoid issues at vertices
            if (t > 1e-9 && t < 1 - 1e-9)
            {
                double ix = edgeStart.X + t * edgeDx;
                double iy = edgeStart.Y + t * edgeDy;
                intersections.Add((i, t, new VPoint(ix, iy)));
            }
            else if (Math.Abs(t) < 1e-9)
            {
                // Intersection at edge start vertex - only count once per vertex
                // We'll handle this by checking if previous edge was also intersected at its end
                double ix = edgeStart.X;
                double iy = edgeStart.Y;
                intersections.Add((i, 0, new VPoint(ix, iy)));
            }
        }

        // Remove duplicate intersection points (can happen at vertices)
        var uniqueIntersections = new List<(int edgeIndex, double t, VPoint point)>();
        foreach (var intersection in intersections)
        {
            bool isDuplicate = false;
            foreach (var existing in uniqueIntersections)
            {
                if (intersection.point.DistanceTo(existing.point) < 1e-6)
                {
                    isDuplicate = true;
                    break;
                }
            }
            if (!isDuplicate)
            {
                uniqueIntersections.Add(intersection);
            }
        }

        return uniqueIntersections;
    }

    /// <summary>
    /// Builds the resulting polygons after slicing.
    /// Points are cloned to ensure independent polygons.
    /// </summary>
    private List<VPolygon> BuildSlicedPolygons(List<(int edgeIndex, double t, VPoint point)> intersections)
    {
        var result = new List<VPolygon>();

        // For simple case with 2 intersections: split into 2 polygons
        if (intersections.Count == 2)
        {
            var int1 = intersections[0];
            var int2 = intersections[1];

            // Polygon 1: from int1 along edges to int2, then back via slice line
            var poly1Points = new List<VPoint>();
            poly1Points.Add((VPoint)int1.point.Clone());

            // Add vertices from after int1's edge to int2's edge
            for (int i = int1.edgeIndex + 1; i <= int2.edgeIndex; i++)
            {
                poly1Points.Add((VPoint)Points[i].Clone());
            }
            poly1Points.Add((VPoint)int2.point.Clone());

            if (poly1Points.Count >= 3)
            {
                result.Add(new VPolygon(poly1Points));
            }

            // Polygon 2: from int2 along remaining edges to int1, then back via slice line
            var poly2Points = new List<VPoint>();
            poly2Points.Add((VPoint)int2.point.Clone());

            // Add vertices from after int2's edge, wrapping around to int1's edge
            for (int i = int2.edgeIndex + 1; i < Points.Count; i++)
            {
                poly2Points.Add((VPoint)Points[i].Clone());
            }
            for (int i = 0; i <= int1.edgeIndex; i++)
            {
                poly2Points.Add((VPoint)Points[i].Clone());
            }
            poly2Points.Add((VPoint)int1.point.Clone());

            if (poly2Points.Count >= 3)
            {
                result.Add(new VPolygon(poly2Points));
            }
        }
        else
        {
            // For more than 2 intersections (concave polygons), use a more general algorithm
            result = BuildSlicedPolygonsGeneral(intersections);
        }

        return result;
    }

    /// <summary>
    /// General algorithm for building sliced polygons with multiple intersection pairs.
    /// Points are cloned to ensure independent polygons.
    /// </summary>
    private List<VPolygon> BuildSlicedPolygonsGeneral(List<(int edgeIndex, double t, VPoint point)> intersections)
    {
        var result = new List<VPolygon>();

        // Create an augmented list of points with intersection points inserted
        var augmentedPoints = new List<(VPoint point, bool isIntersection, int intersectionIndex)>();
        int intIdx = 0;

        for (int i = 0; i < Points.Count; i++)
        {
            // Add intersections that occur on this edge (before the end vertex)
            while (intIdx < intersections.Count && intersections[intIdx].edgeIndex == i)
            {
                var intersection = intersections[intIdx];
                // Only add if not at start vertex (t > 0)
                if (intersection.t > 1e-9)
                {
                    augmentedPoints.Add((intersection.point, true, intIdx));
                }
                intIdx++;
            }

            // Add the vertex
            augmentedPoints.Add((Points[i], false, -1));
        }

        // Add any remaining intersections (shouldn't happen if sorted correctly)
        while (intIdx < intersections.Count)
        {
            var intersection = intersections[intIdx];
            if (intersection.t > 1e-9)
            {
                augmentedPoints.Add((intersection.point, true, intIdx));
            }
            intIdx++;
        }

        // Now walk around the augmented polygon, splitting at intersection pairs
        // Track which intersections have been used
        var used = new HashSet<int>();

        for (int startInt = 0; startInt < intersections.Count; startInt++)
        {
            if (used.Contains(startInt)) continue;

            // Find the starting position in augmented points
            int startPos = -1;
            for (int i = 0; i < augmentedPoints.Count; i++)
            {
                if (augmentedPoints[i].isIntersection && augmentedPoints[i].intersectionIndex == startInt)
                {
                    startPos = i;
                    break;
                }
            }

            if (startPos == -1) continue;

            // Walk forward collecting points until we hit another intersection
            var polyPoints = new List<VPoint>();
            int currentPos = startPos;
            bool foundPair = false;

            do
            {
                // Clone the point to ensure independence
                polyPoints.Add((VPoint)augmentedPoints[currentPos].point.Clone());

                currentPos = (currentPos + 1) % augmentedPoints.Count;

                // Check if we hit another intersection
                if (augmentedPoints[currentPos].isIntersection)
                {
                    int hitInt = augmentedPoints[currentPos].intersectionIndex;
                    if (hitInt != startInt && !used.Contains(hitInt))
                    {
                        // Add this intersection point (cloned) and close the polygon via the slice line
                        polyPoints.Add((VPoint)augmentedPoints[currentPos].point.Clone());
                        used.Add(startInt);
                        used.Add(hitInt);
                        foundPair = true;
                        break;
                    }
                }
            } while (currentPos != startPos);

            if (foundPair && polyPoints.Count >= 3)
            {
                result.Add(new VPolygon(polyPoints));
            }
        }

        // If general algorithm didn't produce results, fall back to returning original
        if (result.Count == 0)
        {
            result.Add((VPolygon)this.Clone());
        }

        return result;
    }
}
