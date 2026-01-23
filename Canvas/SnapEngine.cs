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
    Perpendicular,
    Extension,
    Tangent
}

/// <summary>
/// Represents a detected snap point with its type and distance from cursor.
/// </summary>
public class SnapResult
{
    public VPoint Point { get; set; }
    public SnapType Type { get; set; }
    public double Distance { get; set; }

    /// <summary>
    /// For Extension snaps: the endpoint from which the extension originates.
    /// </summary>
    public VPoint? ExtensionSource { get; set; }

    /// <summary>
    /// For Extension snaps: the angle of the line being extended (in degrees).
    /// </summary>
    public double ExtensionAngle { get; set; }

    /// <summary>
    /// For Perpendicular/Tangent snaps: the reference point (first click) from which the relationship is measured.
    /// </summary>
    public VPoint? ReferenceSource { get; set; }

    /// <summary>
    /// For Perpendicular snaps: the point on the shape where perpendicular meets.
    /// For Tangent snaps: the tangent point on the circle/arc.
    /// </summary>
    public VPoint? ConstraintPoint { get; set; }

    /// <summary>
    /// For Tangent snaps: the center of the circle/arc being tangent to.
    /// </summary>
    public VPoint? TangentCenter { get; set; }

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
    public bool ExtensionSnapEnabled { get; set; } = true;
    public bool TangentSnapEnabled { get; set; } = true;

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
        // Store scale for extension reach calculations
        _currentScale = scale;

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
        // Store scale for extension reach calculations
        _currentScale = scale;

        // Convert screen tolerance to world tolerance
        var worldTolerance = DefaultSnapTolerance / scale;

        // If no spatial index, return null (caller should use the other overload)
        if (spatialIndex == null)
            return null;

        // Query shapes within an expanded area for extension snaps
        // Extensions can reach further than normal snaps
        var extensionReach = ExtensionSnapEnabled ? ExtensionMaxReachPixels / scale : 0;
        var queryRadius = Math.Max(worldTolerance, extensionReach);

        var queryBounds = new AABB(
            cursorWorld.X - queryRadius,
            cursorWorld.Y - queryRadius,
            cursorWorld.X + queryRadius,
            cursorWorld.Y + queryRadius
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
        SnapType.Tangent => 6,
        SnapType.Extension => 7,
        SnapType.Nearest => 8,
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
                AddPerpendicularSnapCandidate(cursor, perp, ReferencePoint, tolerance, candidates);
            }
        }

        // Extension snap (project onto infinite line, beyond endpoints)
        if (ExtensionSnapEnabled)
        {
            CollectExtensionSnapPoints(cursor, line.Start, line.End, tolerance, candidates);
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

        // Tangent snap (from reference point)
        if (TangentSnapEnabled && ReferencePoint != null)
        {
            CollectTangentSnapPoints(cursor, ReferencePoint, arc.Center, arc.Radius, tolerance, candidates, arc);
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

        // Tangent snap (from reference point)
        if (TangentSnapEnabled && ReferencePoint != null)
        {
            CollectTangentSnapPoints(cursor, ReferencePoint, circle.Center, circle.Radius, tolerance, candidates);
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

        // Extension snap (extend from edge endpoints)
        if (ExtensionSnapEnabled)
        {
            for (int i = 0; i < 4; i++)
            {
                CollectExtensionSnapPoints(cursor, corners[i], corners[(i + 1) % 4], tolerance, candidates);
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

        // Extension snap (extend from edge endpoints)
        if (ExtensionSnapEnabled)
        {
            for (int i = 0; i < polygon.Points.Count; i++)
            {
                var next = (i + 1) % polygon.Points.Count;
                CollectExtensionSnapPoints(cursor, polygon.Points[i], polygon.Points[next], tolerance, candidates);
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

        // Extension snap (extend from segment endpoints)
        if (ExtensionSnapEnabled)
        {
            for (int i = 0; i < polyline.Points.Count - 1; i++)
            {
                CollectExtensionSnapPoints(cursor, polyline.Points[i], polyline.Points[i + 1], tolerance, candidates);
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
    /// Adds a perpendicular snap candidate with visualization data.
    /// </summary>
    private void AddPerpendicularSnapCandidate(VPoint cursor, VPoint snapPoint, VPoint referencePoint, double tolerance, List<SnapResult> candidates)
    {
        var distance = cursor.DistanceTo(snapPoint);
        if (distance <= tolerance)
        {
            var result = new SnapResult(snapPoint, SnapType.Perpendicular, distance)
            {
                ReferenceSource = referencePoint,
                ConstraintPoint = snapPoint
            };
            candidates.Add(result);
        }
    }

    /// <summary>
    /// Collects tangent snap points from reference point to a circle/arc.
    /// </summary>
    private void CollectTangentSnapPoints(VPoint cursor, VPoint referencePoint, VPoint center, double radius, double tolerance, List<SnapResult> candidates, VArc? arc = null)
    {
        // Distance from reference point to center
        var dx = center.X - referencePoint.X;
        var dy = center.Y - referencePoint.Y;
        var distToCenter = Math.Sqrt(dx * dx + dy * dy);

        // Reference point must be outside the circle for tangent to exist
        if (distToCenter <= radius + 1e-9) return;

        // Calculate tangent points using geometry:
        // The tangent point forms a right angle with the radius at that point
        // Distance from center to tangent point along the line from ref to center
        var distAlongLine = radius * radius / distToCenter;
        var distPerpendicular = Math.Sqrt(radius * radius - distAlongLine * distAlongLine);

        // Direction from reference to center (normalized)
        var dirX = dx / distToCenter;
        var dirY = dy / distToCenter;

        // Point along the line from center toward reference, at distance (radius^2/distToCenter)
        var midX = center.X - dirX * distAlongLine;
        var midY = center.Y - dirY * distAlongLine;

        // Perpendicular direction
        var perpX = -dirY;
        var perpY = dirX;

        // Two tangent points
        var tangent1 = new VPoint(midX + perpX * distPerpendicular, midY + perpY * distPerpendicular);
        var tangent2 = new VPoint(midX - perpX * distPerpendicular, midY - perpY * distPerpendicular);

        // Check if tangent points are on the arc (if it's an arc, not full circle)
        if (arc != null)
        {
            if (IsPointOnArc(tangent1, arc))
                AddTangentSnapCandidate(cursor, tangent1, referencePoint, center, tolerance, candidates);
            if (IsPointOnArc(tangent2, arc))
                AddTangentSnapCandidate(cursor, tangent2, referencePoint, center, tolerance, candidates);
        }
        else
        {
            // Full circle - both tangent points are valid
            AddTangentSnapCandidate(cursor, tangent1, referencePoint, center, tolerance, candidates);
            AddTangentSnapCandidate(cursor, tangent2, referencePoint, center, tolerance, candidates);
        }
    }

    /// <summary>
    /// Checks if a point lies on an arc's angular range.
    /// </summary>
    private bool IsPointOnArc(VPoint point, VArc arc)
    {
        var dx = point.X - arc.Center.X;
        var dy = point.Y - arc.Center.Y;
        var angle = Math.Atan2(dy, dx) * 180 / Math.PI;

        // Normalize angle to 0-360
        if (angle < 0) angle += 360;

        var startAngle = arc.StartAngle;
        var endAngle = arc.EndAngle;

        // Normalize to 0-360
        while (startAngle < 0) startAngle += 360;
        while (endAngle < 0) endAngle += 360;
        while (startAngle >= 360) startAngle -= 360;
        while (endAngle >= 360) endAngle -= 360;

        // Check if angle is within the arc's range
        if (startAngle <= endAngle)
        {
            return angle >= startAngle && angle <= endAngle;
        }
        else
        {
            // Arc crosses 0 degrees
            return angle >= startAngle || angle <= endAngle;
        }
    }

    /// <summary>
    /// Adds a tangent snap candidate with visualization data.
    /// </summary>
    private void AddTangentSnapCandidate(VPoint cursor, VPoint tangentPoint, VPoint referencePoint, VPoint center, double tolerance, List<SnapResult> candidates)
    {
        var distance = cursor.DistanceTo(tangentPoint);
        if (distance <= tolerance)
        {
            var result = new SnapResult(tangentPoint, SnapType.Tangent, distance)
            {
                ReferenceSource = referencePoint,
                ConstraintPoint = tangentPoint,
                TangentCenter = center
            };
            candidates.Add(result);
        }
    }

    // Maximum extension reach in screen pixels
    private const double ExtensionMaxReachPixels = 300.0;

    // Store current scale for extension calculations
    private double _currentScale = 1.0;

    /// <summary>
    /// Collects extension snap points for a line segment.
    /// Extension snaps occur when the cursor is near the infinite line extending beyond the segment endpoints.
    /// Uses perpendicular distance for magnetic effect along the extension line.
    /// </summary>
    private void CollectExtensionSnapPoints(VPoint cursor, VPoint segStart, VPoint segEnd, double tolerance, List<SnapResult> candidates)
    {
        var dx = segEnd.X - segStart.X;
        var dy = segEnd.Y - segStart.Y;
        var lengthSq = dx * dx + dy * dy;

        if (lengthSq < 1e-9) return; // Degenerate segment

        var length = Math.Sqrt(lengthSq);

        // Normalized direction vector
        var dirX = dx / length;
        var dirY = dy / length;

        // Project cursor onto the infinite line
        var t = ((cursor.X - segStart.X) * dx + (cursor.Y - segStart.Y) * dy) / lengthSq;

        // Only consider extensions beyond the segment (t < 0 or t > 1)
        if (t >= 0 && t <= 1) return; // Projection is within segment, not an extension

        // Calculate projected point on infinite line
        var projX = segStart.X + t * dx;
        var projY = segStart.Y + t * dy;
        var projected = new VPoint(projX, projY);

        // Calculate PERPENDICULAR distance from cursor to the infinite line
        // This is what creates the "magnetic" effect - cursor snaps to line when close perpendicular
        var perpDistance = Math.Abs((cursor.X - segStart.X) * (-dirY) + (cursor.Y - segStart.Y) * dirX);

        // Check perpendicular tolerance (use normal snap tolerance for perpendicular distance)
        if (perpDistance > tolerance) return;

        // Determine which endpoint is the source of the extension
        VPoint extensionSource;
        double extensionDistance;
        if (t < 0)
        {
            // Extension from start point (going backwards)
            extensionSource = segStart;
            extensionDistance = extensionSource.DistanceTo(projected);
        }
        else
        {
            // Extension from end point (going forwards)
            extensionSource = segEnd;
            extensionDistance = extensionSource.DistanceTo(projected);
        }

        // Limit extension reach (in world units based on screen pixels)
        var maxReachWorld = ExtensionMaxReachPixels / _currentScale;
        if (extensionDistance > maxReachWorld) return;

        // Calculate angle of the line (in degrees)
        var angle = Math.Atan2(dy, dx) * 180 / Math.PI;

        // Create extension snap result
        // Use perpendicular distance as the "distance" for priority sorting
        var result = new SnapResult(projected, SnapType.Extension, perpDistance)
        {
            ExtensionSource = extensionSource,
            ExtensionAngle = angle
        };
        candidates.Add(result);
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
        ExtensionSnapEnabled = settings.SnapExtensionEnabled;
        TangentSnapEnabled = settings.SnapTangentEnabled;
    }
}
