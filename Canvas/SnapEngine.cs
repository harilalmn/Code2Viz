using Code2Viz.Geometry;

namespace Code2Viz.Canvas;

/// <summary>
/// Types of snap points that can be detected.
/// </summary>
public enum SnapType
{
    None,
    Endpoint,
    Midpoint,
    Center,
    Intersection,
    Nearest,
    Perpendicular
}

/// <summary>
/// Represents a detected snap point with its type and distance from cursor.
/// </summary>
public class SnapResult
{
    public VPoint Point { get; set; }
    public SnapType Type { get; set; }
    public double Distance { get; set; }

    public SnapResult(VPoint point, SnapType type, double distance)
    {
        Point = point;
        Type = type;
        Distance = distance;
    }
}

/// <summary>
/// Engine for detecting snap points on shapes.
/// </summary>
public class SnapEngine
{
    // Snap tolerance in screen pixels
    private const double DefaultSnapTolerance = 15.0;

    // Toggle properties for each snap type
    public bool EndpointSnapEnabled { get; set; } = true;
    public bool MidpointSnapEnabled { get; set; } = true;
    public bool CenterSnapEnabled { get; set; } = true;
    public bool IntersectionSnapEnabled { get; set; } = true;
    public bool NearestSnapEnabled { get; set; } = true;
    public bool PerpendicularSnapEnabled { get; set; } = true;

    // First point for perpendicular snap calculation
    public VPoint? ReferencePoint { get; set; }

    /// <summary>
    /// Finds the best snap point near the cursor position.
    /// </summary>
    /// <param name="cursorWorld">Cursor position in world coordinates.</param>
    /// <param name="shapes">List of shapes to snap to.</param>
    /// <param name="scale">Current canvas scale (zoom level).</param>
    /// <returns>The best snap result, or null if no snap found.</returns>
    public SnapResult? FindSnapPoint(VPoint cursorWorld, IReadOnlyList<IDrawable> shapes, double scale)
    {
        // Convert screen tolerance to world tolerance
        var worldTolerance = DefaultSnapTolerance / scale;

        var candidates = new List<SnapResult>();

        foreach (var shape in shapes)
        {
            // Collect snap points from each shape
            CollectSnapPoints(cursorWorld, shape, worldTolerance, candidates);
        }

        // Also check for intersections between shapes
        if (IntersectionSnapEnabled)
        {
            CollectIntersectionPoints(cursorWorld, shapes, worldTolerance, candidates);
        }

        // Return the closest snap point
        if (candidates.Count == 0)
            return null;

        // Prioritize snap types: Endpoint > Midpoint > Center > Intersection > Perpendicular > Nearest
        var prioritizedCandidates = candidates
            .OrderBy(c => GetSnapPriority(c.Type))
            .ThenBy(c => c.Distance)
            .ToList();

        return prioritizedCandidates.First();
    }

    /// <summary>
    /// Finds the best snap point using spatial index for efficient culling (O(log n + k) instead of O(n)).
    /// </summary>
    /// <param name="cursorWorld">Cursor position in world coordinates.</param>
    /// <param name="spatialIndex">QuadTree spatial index for efficient shape lookup.</param>
    /// <param name="scale">Current canvas scale (zoom level).</param>
    /// <returns>The best snap result, or null if no snap found.</returns>
    public SnapResult? FindSnapPoint(VPoint cursorWorld, QuadTree? spatialIndex, double scale)
    {
        // Convert screen tolerance to world tolerance
        var worldTolerance = DefaultSnapTolerance / scale;

        // If no spatial index, return null (caller should use the other overload)
        if (spatialIndex == null)
            return null;

        // Query only shapes within snap tolerance of cursor
        var queryBounds = new AABB(
            cursorWorld.X - worldTolerance,
            cursorWorld.Y - worldTolerance,
            cursorWorld.X + worldTolerance,
            cursorWorld.Y + worldTolerance
        );

        var nearbyShapes = spatialIndex.Query(queryBounds);

        var candidates = new List<SnapResult>();

        foreach (var shape in nearbyShapes)
        {
            CollectSnapPoints(cursorWorld, shape, worldTolerance, candidates);
        }

        // For intersection snaps, only check pairs of nearby shapes
        if (IntersectionSnapEnabled && nearbyShapes.Count > 1)
        {
            CollectIntersectionPointsFromList(cursorWorld, nearbyShapes, worldTolerance, candidates);
        }

        if (candidates.Count == 0)
            return null;

        var prioritizedCandidates = candidates
            .OrderBy(c => GetSnapPriority(c.Type))
            .ThenBy(c => c.Distance)
            .ToList();

        return prioritizedCandidates.First();
    }

    private void CollectIntersectionPointsFromList(VPoint cursor, List<IDrawable> shapes, double tolerance, List<SnapResult> candidates)
    {
        for (int i = 0; i < shapes.Count; i++)
        {
            for (int j = i + 1; j < shapes.Count; j++)
            {
                var intersections = FindIntersections(shapes[i], shapes[j]);
                foreach (var point in intersections)
                {
                    AddSnapCandidate(cursor, point, SnapType.Intersection, tolerance, candidates);
                }
            }
        }
    }

    private int GetSnapPriority(SnapType type) => type switch
    {
        SnapType.Endpoint => 1,
        SnapType.Midpoint => 2,
        SnapType.Center => 3,
        SnapType.Intersection => 4,
        SnapType.Perpendicular => 5,
        SnapType.Nearest => 6,
        _ => 99
    };

    private void CollectSnapPoints(VPoint cursor, IDrawable shape, double tolerance, List<SnapResult> candidates)
    {
        switch (shape)
        {
            case VLine line:
                CollectLineSnapPoints(cursor, line, tolerance, candidates);
                break;
            case VArc arc:
                CollectArcSnapPoints(cursor, arc, tolerance, candidates);
                break;
            case VCircle circle:
                CollectCircleSnapPoints(cursor, circle, tolerance, candidates);
                break;
            case VEllipse ellipse:
                CollectEllipseSnapPoints(cursor, ellipse, tolerance, candidates);
                break;
            case VRectangle rect:
                CollectRectangleSnapPoints(cursor, rect, tolerance, candidates);
                break;
            case VPolygon polygon:
                CollectPolygonSnapPoints(cursor, polygon, tolerance, candidates);
                break;
            case VPolyline polyline:
                CollectPolylineSnapPoints(cursor, polyline, tolerance, candidates);
                break;
            case VPoint point:
                CollectPointSnapPoints(cursor, point, tolerance, candidates);
                break;
        }
    }

    private void CollectLineSnapPoints(VPoint cursor, VLine line, double tolerance, List<SnapResult> candidates)
    {
        // Endpoint snaps
        if (EndpointSnapEnabled)
        {
            AddSnapCandidate(cursor, line.Start, SnapType.Endpoint, tolerance, candidates);
            AddSnapCandidate(cursor, line.End, SnapType.Endpoint, tolerance, candidates);
        }

        // Midpoint snap
        if (MidpointSnapEnabled)
        {
            AddSnapCandidate(cursor, line.MidPoint, SnapType.Midpoint, tolerance, candidates);
        }

        // Nearest snap (projection onto line)
        if (NearestSnapEnabled)
        {
            var projected = line.Project(cursor);
            AddSnapCandidate(cursor, projected, SnapType.Nearest, tolerance, candidates);
        }

        // Perpendicular snap (from reference point)
        if (PerpendicularSnapEnabled && ReferencePoint != null)
        {
            var perp = GetPerpendicularPoint(ReferencePoint, line);
            if (perp != null)
            {
                AddSnapCandidate(cursor, perp, SnapType.Perpendicular, tolerance, candidates);
            }
        }
    }

    private void CollectArcSnapPoints(VPoint cursor, VArc arc, double tolerance, List<SnapResult> candidates)
    {
        // Endpoint snaps
        if (EndpointSnapEnabled)
        {
            AddSnapCandidate(cursor, arc.StartPoint, SnapType.Endpoint, tolerance, candidates);
            AddSnapCandidate(cursor, arc.EndPoint, SnapType.Endpoint, tolerance, candidates);
        }

        // Midpoint snap
        if (MidpointSnapEnabled)
        {
            AddSnapCandidate(cursor, arc.MidPoint, SnapType.Midpoint, tolerance, candidates);
        }

        // Center snap
        if (CenterSnapEnabled)
        {
            AddSnapCandidate(cursor, arc.Center, SnapType.Center, tolerance, candidates);
        }

        // Nearest snap
        if (NearestSnapEnabled)
        {
            var projected = arc.Project(cursor);
            AddSnapCandidate(cursor, projected, SnapType.Nearest, tolerance, candidates);
        }
    }

    private void CollectCircleSnapPoints(VPoint cursor, VCircle circle, double tolerance, List<SnapResult> candidates)
    {
        // Center snap
        if (CenterSnapEnabled)
        {
            AddSnapCandidate(cursor, circle.Center, SnapType.Center, tolerance, candidates);
        }

        // Quadrant points (0, 90, 180, 270 degrees)
        if (EndpointSnapEnabled)
        {
            AddSnapCandidate(cursor, new VPoint(circle.Center.X + circle.Radius, circle.Center.Y), SnapType.Endpoint, tolerance, candidates);
            AddSnapCandidate(cursor, new VPoint(circle.Center.X - circle.Radius, circle.Center.Y), SnapType.Endpoint, tolerance, candidates);
            AddSnapCandidate(cursor, new VPoint(circle.Center.X, circle.Center.Y + circle.Radius), SnapType.Endpoint, tolerance, candidates);
            AddSnapCandidate(cursor, new VPoint(circle.Center.X, circle.Center.Y - circle.Radius), SnapType.Endpoint, tolerance, candidates);
        }

        // Nearest snap
        if (NearestSnapEnabled)
        {
            var projected = circle.Project(cursor);
            AddSnapCandidate(cursor, projected, SnapType.Nearest, tolerance, candidates);
        }
    }

    private void CollectEllipseSnapPoints(VPoint cursor, VEllipse ellipse, double tolerance, List<SnapResult> candidates)
    {
        // Center snap
        if (CenterSnapEnabled)
        {
            AddSnapCandidate(cursor, ellipse.Center, SnapType.Center, tolerance, candidates);
        }

        // Quadrant points
        if (EndpointSnapEnabled)
        {
            AddSnapCandidate(cursor, new VPoint(ellipse.Center.X + ellipse.RadiusX, ellipse.Center.Y), SnapType.Endpoint, tolerance, candidates);
            AddSnapCandidate(cursor, new VPoint(ellipse.Center.X - ellipse.RadiusX, ellipse.Center.Y), SnapType.Endpoint, tolerance, candidates);
            AddSnapCandidate(cursor, new VPoint(ellipse.Center.X, ellipse.Center.Y + ellipse.RadiusY), SnapType.Endpoint, tolerance, candidates);
            AddSnapCandidate(cursor, new VPoint(ellipse.Center.X, ellipse.Center.Y - ellipse.RadiusY), SnapType.Endpoint, tolerance, candidates);
        }
    }

    private void CollectRectangleSnapPoints(VPoint cursor, VRectangle rect, double tolerance, List<SnapResult> candidates)
    {
        var corners = new[]
        {
            rect.Corner,
            new VPoint(rect.Corner.X + rect.Width, rect.Corner.Y),
            new VPoint(rect.Corner.X + rect.Width, rect.Corner.Y + rect.Height),
            new VPoint(rect.Corner.X, rect.Corner.Y + rect.Height)
        };

        // Endpoint snaps (corners)
        if (EndpointSnapEnabled)
        {
            foreach (var corner in corners)
            {
                AddSnapCandidate(cursor, corner, SnapType.Endpoint, tolerance, candidates);
            }
        }

        // Midpoint snaps (edge midpoints)
        if (MidpointSnapEnabled)
        {
            for (int i = 0; i < 4; i++)
            {
                var mid = new VPoint(
                    (corners[i].X + corners[(i + 1) % 4].X) / 2,
                    (corners[i].Y + corners[(i + 1) % 4].Y) / 2);
                AddSnapCandidate(cursor, mid, SnapType.Midpoint, tolerance, candidates);
            }
        }

        // Center snap
        if (CenterSnapEnabled)
        {
            var center = new VPoint(
                rect.Corner.X + rect.Width / 2,
                rect.Corner.Y + rect.Height / 2);
            AddSnapCandidate(cursor, center, SnapType.Center, tolerance, candidates);
        }

        // Nearest snap (project to edges)
        if (NearestSnapEnabled)
        {
            for (int i = 0; i < 4; i++)
            {
                var edge = new VLine(corners[i], corners[(i + 1) % 4]);
                var projected = edge.Project(cursor);
                AddSnapCandidate(cursor, projected, SnapType.Nearest, tolerance, candidates);
            }
        }
    }

    private void CollectPolygonSnapPoints(VPoint cursor, VPolygon polygon, double tolerance, List<SnapResult> candidates)
    {
        if (polygon.Points.Count < 3) return;

        // Endpoint snaps (vertices)
        if (EndpointSnapEnabled)
        {
            foreach (var point in polygon.Points)
            {
                AddSnapCandidate(cursor, point, SnapType.Endpoint, tolerance, candidates);
            }
        }

        // Midpoint snaps (edge midpoints)
        if (MidpointSnapEnabled)
        {
            for (int i = 0; i < polygon.Points.Count; i++)
            {
                var next = (i + 1) % polygon.Points.Count;
                var mid = new VPoint(
                    (polygon.Points[i].X + polygon.Points[next].X) / 2,
                    (polygon.Points[i].Y + polygon.Points[next].Y) / 2);
                AddSnapCandidate(cursor, mid, SnapType.Midpoint, tolerance, candidates);
            }
        }

        // Center snap (centroid)
        if (CenterSnapEnabled)
        {
            double cx = 0, cy = 0;
            foreach (var p in polygon.Points)
            {
                cx += p.X;
                cy += p.Y;
            }
            cx /= polygon.Points.Count;
            cy /= polygon.Points.Count;
            AddSnapCandidate(cursor, new VPoint(cx, cy), SnapType.Center, tolerance, candidates);
        }

        // Nearest snap (project to edges)
        if (NearestSnapEnabled)
        {
            for (int i = 0; i < polygon.Points.Count; i++)
            {
                var next = (i + 1) % polygon.Points.Count;
                var edge = new VLine(polygon.Points[i], polygon.Points[next]);
                var projected = edge.Project(cursor);
                AddSnapCandidate(cursor, projected, SnapType.Nearest, tolerance, candidates);
            }
        }
    }

    private void CollectPolylineSnapPoints(VPoint cursor, VPolyline polyline, double tolerance, List<SnapResult> candidates)
    {
        if (polyline.Points.Count < 2) return;

        // Endpoint snaps (start and end)
        if (EndpointSnapEnabled)
        {
            AddSnapCandidate(cursor, polyline.Points[0], SnapType.Endpoint, tolerance, candidates);
            AddSnapCandidate(cursor, polyline.Points[^1], SnapType.Endpoint, tolerance, candidates);

            // Also add all vertices
            foreach (var point in polyline.Points)
            {
                AddSnapCandidate(cursor, point, SnapType.Endpoint, tolerance, candidates);
            }
        }

        // Midpoint snaps (segment midpoints)
        if (MidpointSnapEnabled)
        {
            for (int i = 0; i < polyline.Points.Count - 1; i++)
            {
                var mid = new VPoint(
                    (polyline.Points[i].X + polyline.Points[i + 1].X) / 2,
                    (polyline.Points[i].Y + polyline.Points[i + 1].Y) / 2);
                AddSnapCandidate(cursor, mid, SnapType.Midpoint, tolerance, candidates);
            }
        }

        // Nearest snap (project to segments)
        if (NearestSnapEnabled)
        {
            for (int i = 0; i < polyline.Points.Count - 1; i++)
            {
                var segment = new VLine(polyline.Points[i], polyline.Points[i + 1]);
                var projected = segment.Project(cursor);
                AddSnapCandidate(cursor, projected, SnapType.Nearest, tolerance, candidates);
            }
        }
    }

    private void CollectPointSnapPoints(VPoint cursor, VPoint point, double tolerance, List<SnapResult> candidates)
    {
        if (EndpointSnapEnabled)
        {
            AddSnapCandidate(cursor, new VPoint(point.X, point.Y), SnapType.Endpoint, tolerance, candidates);
        }
    }

    private void CollectIntersectionPoints(VPoint cursor, IReadOnlyList<IDrawable> shapes, double tolerance, List<SnapResult> candidates)
    {
        // Check all pairs of shapes for intersections
        for (int i = 0; i < shapes.Count; i++)
        {
            for (int j = i + 1; j < shapes.Count; j++)
            {
                var intersections = FindIntersections(shapes[i], shapes[j]);
                foreach (var point in intersections)
                {
                    AddSnapCandidate(cursor, point, SnapType.Intersection, tolerance, candidates);
                }
            }
        }
    }

    private List<VPoint> FindIntersections(IDrawable shape1, IDrawable shape2)
    {
        var results = new List<VPoint>();

        // Line-Line intersection
        if (shape1 is VLine line1 && shape2 is VLine line2)
        {
            var result = GeometryHelper.IntersectLineLine(line1, line2);
            if (result is VPoint p)
            {
                results.Add(p);
            }
        }

        // Circle-Circle intersection
        if (shape1 is VCircle circle1 && shape2 is VCircle circle2)
        {
            results.AddRange(GeometryHelper.IntersectCircleCircle(circle1.Center, circle1.Radius, circle2.Center, circle2.Radius));
        }

        // Line-Circle intersection
        if (shape1 is VLine lineA && shape2 is VCircle circleA)
        {
            results.AddRange(IntersectLineCircle(lineA, circleA));
        }
        if (shape1 is VCircle circleB && shape2 is VLine lineB)
        {
            results.AddRange(IntersectLineCircle(lineB, circleB));
        }

        return results;
    }

    private List<VPoint> IntersectLineCircle(VLine line, VCircle circle)
    {
        var results = new List<VPoint>();

        double dx = line.End.X - line.Start.X;
        double dy = line.End.Y - line.Start.Y;
        double fx = line.Start.X - circle.Center.X;
        double fy = line.Start.Y - circle.Center.Y;

        double a = dx * dx + dy * dy;
        double b = 2 * (fx * dx + fy * dy);
        double c = fx * fx + fy * fy - circle.Radius * circle.Radius;

        double discriminant = b * b - 4 * a * c;

        if (discriminant < 0)
            return results;

        discriminant = Math.Sqrt(discriminant);

        double t1 = (-b - discriminant) / (2 * a);
        double t2 = (-b + discriminant) / (2 * a);

        if (t1 >= 0 && t1 <= 1)
        {
            results.Add(new VPoint(line.Start.X + t1 * dx, line.Start.Y + t1 * dy));
        }

        if (t2 >= 0 && t2 <= 1 && Math.Abs(t1 - t2) > 1e-9)
        {
            results.Add(new VPoint(line.Start.X + t2 * dx, line.Start.Y + t2 * dy));
        }

        return results;
    }

    private VPoint? GetPerpendicularPoint(VPoint referencePoint, VLine line)
    {
        // Find point on line that is perpendicular to reference point
        var projected = line.Project(referencePoint);

        // Check if projection is on the line segment
        double lineLength = line.GetLength();
        if (lineLength < 1e-9) return null;

        double distStart = referencePoint.DistanceTo(line.Start);
        double distEnd = referencePoint.DistanceTo(line.End);
        double distProj = referencePoint.DistanceTo(projected);

        // If projection is close to perpendicular (not at endpoints)
        if (distProj < distStart && distProj < distEnd)
        {
            return projected;
        }

        return null;
    }

    private void AddSnapCandidate(VPoint cursor, VPoint snapPoint, SnapType type, double tolerance, List<SnapResult> candidates)
    {
        var distance = cursor.DistanceTo(snapPoint);
        if (distance <= tolerance)
        {
            candidates.Add(new SnapResult(snapPoint, type, distance));
        }
    }

    /// <summary>
    /// Syncs snap settings from application settings.
    /// </summary>
    public void SyncFromSettings()
    {
        var settings = ApplicationSettings.Instance;
        EndpointSnapEnabled = settings.SnapEndpointEnabled;
        MidpointSnapEnabled = settings.SnapMidpointEnabled;
        CenterSnapEnabled = settings.SnapCenterEnabled;
        IntersectionSnapEnabled = settings.SnapIntersectionEnabled;
        NearestSnapEnabled = settings.SnapNearestEnabled;
        PerpendicularSnapEnabled = settings.SnapPerpendicularEnabled;
    }
}
