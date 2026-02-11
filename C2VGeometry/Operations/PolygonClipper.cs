using System;
using System.Collections.Generic;
using System.Linq;

namespace C2VGeometry;

/// <summary>
/// Vertex in a clipping polygon linked list, used by Greiner-Hormann algorithm.
/// </summary>
internal class ClipVertex
{
    public VPoint Point { get; set; }
    public bool IsIntersection { get; set; }
    public bool IsEntry { get; set; }
    public ClipVertex? Neighbor { get; set; }
    public ClipVertex? Next { get; set; }
    public ClipVertex? Prev { get; set; }
    public bool Visited { get; set; }
    public double Alpha { get; set; } // Parameter along edge (0-1) for sorting intersections

    public ClipVertex(VPoint point, bool isIntersection = false)
    {
        Point = point;
        IsIntersection = isIntersection;
    }
}

/// <summary>
/// Provides polygon boolean operations using the Greiner-Hormann algorithm.
/// </summary>
internal static class PolygonClipper
{
    private const double Epsilon = GeometryTolerance.Epsilon;

    #region MakeSimple - Resolve Self-Intersections

    /// <summary>
    /// Converts a potentially self-intersecting polygon into one or more simple polygons.
    /// Uses self-union to resolve self-intersections.
    /// </summary>
    public static List<VPolygon> MakeSimple(VPolygon polygon)
    {
        if (polygon.Points.Count < 3)
            return new List<VPolygon> { (VPolygon)polygon.Clone() };

        // Check if polygon has self-intersections
        var selfIntersections = SpatialAccelerator.FindSelfIntersections(polygon);

        if (selfIntersections.Count == 0)
        {
            // Already simple
            return new List<VPolygon> { (VPolygon)polygon.Clone() };
        }

        // Self-union to resolve intersections
        // We split the polygon at intersection points and rebuild
        return SplitAtSelfIntersections(polygon, selfIntersections);
    }

    /// <summary>
    /// Splits a self-intersecting polygon at its intersection points.
    /// </summary>
    private static List<VPolygon> SplitAtSelfIntersections(VPolygon polygon, List<SelfIntersection> intersections)
    {
        var result = new List<VPolygon>();

        if (intersections.Count == 0)
        {
            result.Add((VPolygon)polygon.Clone());
            return result;
        }

        // Build augmented point list with intersection points inserted
        var augmented = new List<(VPoint point, bool isIntersection, int intersectionIdx, int originalEdge)>();

        // Sort intersections by edge index, then by alpha
        var sortedIntersections = intersections
            .Select((si, idx) => new { si, idx })
            .OrderBy(x => x.si.EdgeIndex1)
            .ThenBy(x => x.si.Alpha1)
            .ToList();

        int intIdx = 0;
        for (int edgeIdx = 0; edgeIdx < polygon.Points.Count; edgeIdx++)
        {
            // Add the vertex
            augmented.Add((polygon.Points[edgeIdx], false, -1, edgeIdx));

            // Add any intersections on this edge (sorted by alpha)
            var edgeIntersections = sortedIntersections
                .Where(x => x.si.EdgeIndex1 == edgeIdx)
                .OrderBy(x => x.si.Alpha1)
                .ToList();

            foreach (var ei in edgeIntersections)
            {
                augmented.Add((ei.si.Point, true, ei.idx, edgeIdx));
            }

            // Also check if this edge is EdgeIndex2 for any intersection
            var edge2Intersections = sortedIntersections
                .Where(x => x.si.EdgeIndex2 == edgeIdx)
                .OrderBy(x => x.si.Alpha2)
                .ToList();

            foreach (var ei in edge2Intersections)
            {
                // Don't add duplicates - check if already added
                bool alreadyAdded = augmented.Any(a =>
                    a.isIntersection && a.intersectionIdx == ei.idx);
                if (!alreadyAdded)
                {
                    augmented.Add((ei.si.Point, true, ei.idx, edgeIdx));
                }
            }
        }

        // Now trace polygons starting from intersection points
        var visited = new HashSet<int>();

        for (int startIdx = 0; startIdx < augmented.Count; startIdx++)
        {
            if (!augmented[startIdx].isIntersection || visited.Contains(startIdx))
                continue;

            var polyPoints = new List<VPoint>();
            int currentIdx = startIdx;
            int iterations = 0;
            int maxIterations = augmented.Count * 2;

            do
            {
                if (iterations++ > maxIterations)
                    break;

                polyPoints.Add(VPoint.Internal(augmented[currentIdx].point.X, augmented[currentIdx].point.Y));

                if (augmented[currentIdx].isIntersection)
                {
                    visited.Add(currentIdx);

                    // Find the paired intersection point (same intersection, different location in list)
                    int pairedIdx = -1;
                    for (int i = 0; i < augmented.Count; i++)
                    {
                        if (i != currentIdx && augmented[i].isIntersection &&
                            augmented[i].intersectionIdx == augmented[currentIdx].intersectionIdx)
                        {
                            pairedIdx = i;
                            break;
                        }
                    }

                    if (pairedIdx >= 0 && !visited.Contains(pairedIdx))
                    {
                        // Jump to paired intersection point
                        currentIdx = pairedIdx;
                        visited.Add(pairedIdx);
                    }
                }

                currentIdx = (currentIdx + 1) % augmented.Count;

            } while (currentIdx != startIdx && iterations < maxIterations);

            // Clean up and add if valid
            if (polyPoints.Count >= 3)
            {
                var cleanedPoints = CleanupPoints(polyPoints);
                if (cleanedPoints.Count >= 3)
                {
                    result.Add(new VPolygon(cleanedPoints));
                }
            }
        }

        // If no valid polygons found, return original
        if (result.Count == 0)
        {
            result.Add((VPolygon)polygon.Clone());
        }

        return result;
    }

    private static List<VPoint> CleanupPoints(List<VPoint> points)
    {
        var cleaned = new List<VPoint>();
        for (int i = 0; i < points.Count; i++)
        {
            if (cleaned.Count == 0 || !GeometryTolerance.PointsAreEqual(points[i], cleaned[^1]))
            {
                cleaned.Add(points[i]);
            }
        }

        if (cleaned.Count > 1 && GeometryTolerance.PointsAreEqual(cleaned[0], cleaned[^1]))
        {
            cleaned.RemoveAt(cleaned.Count - 1);
        }

        return cleaned;
    }

    #endregion

    #region Boolean Operations with Hole Support

    /// <summary>
    /// Computes the intersection of two polygons, returning results with hole information.
    /// </summary>
    public static List<PolygonWithHoles> IntersectWithHoles(VPolygon subject, VPolygon clip)
    {
        var polygons = ClipPolygons(subject, clip, ClipOperation.Intersection);
        return PolygonWithHoles.FromPolygonList(polygons);
    }

    /// <summary>
    /// Computes the union of two polygons, returning results with hole information.
    /// </summary>
    public static List<PolygonWithHoles> UnionWithHoles(VPolygon subject, VPolygon clip)
    {
        var polygons = ClipPolygons(subject, clip, ClipOperation.Union);
        return PolygonWithHoles.FromPolygonList(polygons);
    }

    /// <summary>
    /// Computes the difference of two polygons, returning results with hole information.
    /// </summary>
    public static List<PolygonWithHoles> DifferenceWithHoles(VPolygon subject, VPolygon clip)
    {
        var polygons = ClipPolygons(subject, clip, ClipOperation.Difference);
        return PolygonWithHoles.FromPolygonList(polygons);
    }

    #endregion

    #region Standard Boolean Operations

    /// <summary>
    /// Computes the intersection of two polygons.
    /// </summary>
    public static List<VPolygon> Intersect(VPolygon subject, VPolygon clip)
    {
        return ClipPolygons(subject, clip, ClipOperation.Intersection);
    }

    /// <summary>
    /// Computes the union of two polygons.
    /// </summary>
    public static List<VPolygon> Union(VPolygon subject, VPolygon clip)
    {
        return ClipPolygons(subject, clip, ClipOperation.Union);
    }

    /// <summary>
    /// Computes the difference of two polygons (subject - clip).
    /// </summary>
    public static List<VPolygon> Difference(VPolygon subject, VPolygon clip)
    {
        return ClipPolygons(subject, clip, ClipOperation.Difference);
    }

    /// <summary>
    /// Computes the symmetric difference (XOR) of two polygons.
    /// </summary>
    public static List<VPolygon> Xor(VPolygon subject, VPolygon clip)
    {
        // XOR = Union - Intersection = (A - B) + (B - A)
        var aMinusB = ClipPolygons(subject, clip, ClipOperation.Difference);
        var bMinusA = ClipPolygons(clip, subject, ClipOperation.Difference);

        var result = new List<VPolygon>();
        result.AddRange(aMinusB);
        result.AddRange(bMinusA);
        return result;
    }

    #endregion

    #region Internal Implementation

    private enum ClipOperation
    {
        Intersection,
        Union,
        Difference
    }

    private static List<VPolygon> ClipPolygons(VPolygon subject, VPolygon clip, ClipOperation operation)
    {
        var result = new List<VPolygon>();

        // Handle edge cases
        if (subject.Points.Count < 3 || clip.Points.Count < 3)
        {
            return result;
        }

        // Quick bounding box check
        var subjectBounds = subject.GetBounds();
        var clipBounds = clip.GetBounds();

        if (!BoundsOverlap(subjectBounds, clipBounds))
        {
            // No overlap - handle based on operation
            switch (operation)
            {
                case ClipOperation.Intersection:
                    return result; // Empty result
                case ClipOperation.Union:
                    result.Add((VPolygon)subject.Clone());
                    result.Add((VPolygon)clip.Clone());
                    return result;
                case ClipOperation.Difference:
                    result.Add((VPolygon)subject.Clone());
                    return result;
            }
        }

        // Check for containment
        bool subjectInsideClip = AllPointsInsidePolygon(subject.Points, clip);
        bool clipInsideSubject = AllPointsInsidePolygon(clip.Points, subject);

        if (subjectInsideClip && !HasEdgeIntersections(subject, clip))
        {
            switch (operation)
            {
                case ClipOperation.Intersection:
                    result.Add((VPolygon)subject.Clone());
                    return result;
                case ClipOperation.Union:
                    result.Add((VPolygon)clip.Clone());
                    return result;
                case ClipOperation.Difference:
                    return result; // Empty - subject is entirely inside clip
            }
        }

        if (clipInsideSubject && !HasEdgeIntersections(subject, clip))
        {
            switch (operation)
            {
                case ClipOperation.Intersection:
                    result.Add((VPolygon)clip.Clone());
                    return result;
                case ClipOperation.Union:
                    result.Add((VPolygon)subject.Clone());
                    return result;
                case ClipOperation.Difference:
                    // Subject with hole - we don't support holes, return subject only
                    result.Add((VPolygon)subject.Clone());
                    return result;
            }
        }

        // Build vertex lists
        var subjectList = BuildVertexList(subject.Points);
        var clipList = BuildVertexList(clip.Points);

        // Find and insert intersection points
        int intersectionCount = FindIntersections(subjectList, clipList);

        if (intersectionCount == 0)
        {
            // No intersections - check containment again
            bool subjectStartInClip = PointInPolygonTest(subject.Points[0], clip.Points);
            bool clipStartInSubject = PointInPolygonTest(clip.Points[0], subject.Points);

            switch (operation)
            {
                case ClipOperation.Intersection:
                    if (subjectStartInClip)
                        result.Add((VPolygon)subject.Clone());
                    else if (clipStartInSubject)
                        result.Add((VPolygon)clip.Clone());
                    return result;
                case ClipOperation.Union:
                    if (subjectStartInClip)
                        result.Add((VPolygon)clip.Clone());
                    else if (clipStartInSubject)
                        result.Add((VPolygon)subject.Clone());
                    else
                    {
                        result.Add((VPolygon)subject.Clone());
                        result.Add((VPolygon)clip.Clone());
                    }
                    return result;
                case ClipOperation.Difference:
                    if (subjectStartInClip)
                        return result; // Empty
                    result.Add((VPolygon)subject.Clone());
                    return result;
            }
        }

        // Mark entry/exit points
        MarkEntryExitPoints(subjectList, clip.Points, operation == ClipOperation.Difference);
        MarkEntryExitPoints(clipList, subject.Points, false);

        // Trace result polygons
        result = TraceResultPolygons(subjectList, clipList, operation);

        return result;
    }

    private static bool BoundsOverlap(BoundingBox a, BoundingBox b)
    {
        return !(a.Max.X < b.Min.X - Epsilon || b.Max.X < a.Min.X - Epsilon ||
                 a.Max.Y < b.Min.Y - Epsilon || b.Max.Y < a.Min.Y - Epsilon);
    }

    private static bool AllPointsInsidePolygon(List<VPoint> points, VPolygon polygon)
    {
        foreach (var point in points)
        {
            if (!PointInPolygonTest(point, polygon.Points))
                return false;
        }
        return true;
    }

    private static bool HasEdgeIntersections(VPolygon a, VPolygon b)
    {
        // Use spatial acceleration for larger polygons
        int totalEdges = a.Points.Count + b.Points.Count;
        if (totalEdges > 50)
        {
            var intersections = SpatialAccelerator.FindIntersections(a, b);
            return intersections.Count > 0;
        }

        // Direct O(n*m) check for small polygons
        for (int i = 0; i < a.Points.Count; i++)
        {
            var a1 = a.Points[i];
            var a2 = a.Points[(i + 1) % a.Points.Count];

            for (int j = 0; j < b.Points.Count; j++)
            {
                var b1 = b.Points[j];
                var b2 = b.Points[(j + 1) % b.Points.Count];

                if (EdgesIntersect(a1, a2, b1, b2, out _, out _))
                    return true;
            }
        }
        return false;
    }

    private static ClipVertex BuildVertexList(List<VPoint> points)
    {
        if (points.Count == 0)
            throw new ArgumentException("Polygon must have at least one point");

        var first = new ClipVertex(points[0]);
        var current = first;

        for (int i = 1; i < points.Count; i++)
        {
            var next = new ClipVertex(points[i]);
            current.Next = next;
            next.Prev = current;
            current = next;
        }

        // Close the loop
        current.Next = first;
        first.Prev = current;

        return first;
    }

    private static int FindIntersections(ClipVertex subjectStart, ClipVertex clipStart)
    {
        int count = 0;
        var subjectCurrent = subjectStart;

        do
        {
            var subjectNext = subjectCurrent.Next!;
            var clipCurrent = clipStart;

            do
            {
                var clipNext = clipCurrent.Next!;

                if (EdgesIntersect(subjectCurrent.Point, subjectNext.Point,
                                   clipCurrent.Point, clipNext.Point,
                                   out double alphaS, out double alphaC))
                {
                    // Create intersection point
                    double ix = subjectCurrent.Point.X + alphaS * (subjectNext.Point.X - subjectCurrent.Point.X);
                    double iy = subjectCurrent.Point.Y + alphaS * (subjectNext.Point.Y - subjectCurrent.Point.Y);
                    var intersectionPoint = VPoint.Internal(ix, iy);

                    // Create intersection vertices for both lists
                    var subjectIntersection = new ClipVertex(intersectionPoint, true) { Alpha = alphaS };
                    var clipIntersection = new ClipVertex(intersectionPoint, true) { Alpha = alphaC };

                    // Link them as neighbors
                    subjectIntersection.Neighbor = clipIntersection;
                    clipIntersection.Neighbor = subjectIntersection;

                    // Insert into subject list (sorted by alpha)
                    InsertIntersection(subjectCurrent, subjectNext, subjectIntersection);

                    // Insert into clip list (sorted by alpha)
                    InsertIntersection(clipCurrent, clipNext, clipIntersection);

                    count++;
                }

                clipCurrent = clipNext;
            } while (clipCurrent != clipStart);

            subjectCurrent = subjectNext;
        } while (subjectCurrent != subjectStart);

        return count;
    }

    private static bool EdgesIntersect(VPoint p1, VPoint p2, VPoint p3, VPoint p4,
                                        out double alphaS, out double alphaC)
    {
        alphaS = 0;
        alphaC = 0;

        double d1x = p2.X - p1.X;
        double d1y = p2.Y - p1.Y;
        double d2x = p4.X - p3.X;
        double d2y = p4.Y - p3.Y;

        double cross = d1x * d2y - d1y * d2x;

        if (Math.Abs(cross) < Epsilon)
            return false; // Parallel or collinear

        double dx = p3.X - p1.X;
        double dy = p3.Y - p1.Y;

        alphaS = (dx * d2y - dy * d2x) / cross;
        alphaC = (dx * d1y - dy * d1x) / cross;

        // Check if intersection is within both segments (exclusive of endpoints to avoid duplicate intersections)
        const double margin = 1e-9;
        return alphaS > margin && alphaS < 1 - margin &&
               alphaC > margin && alphaC < 1 - margin;
    }

    private static void InsertIntersection(ClipVertex start, ClipVertex end, ClipVertex intersection)
    {
        var current = start;

        // Find the right position based on alpha
        while (current.Next != end && current.Next!.IsIntersection && current.Next.Alpha < intersection.Alpha)
        {
            current = current.Next;
        }

        // Insert after current
        intersection.Next = current.Next;
        intersection.Prev = current;
        current.Next!.Prev = intersection;
        current.Next = intersection;
    }

    private static void MarkEntryExitPoints(ClipVertex start, List<VPoint> otherPolygon, bool invertEntryExit)
    {
        var current = start;

        // Find first non-intersection vertex to determine initial inside/outside status
        while (current.IsIntersection)
        {
            current = current.Next!;
            if (current == start) break;
        }

        bool isInside = PointInPolygonTest(current.Point, otherPolygon);

        // Walk through and mark entry/exit
        current = start;
        do
        {
            if (current.IsIntersection)
            {
                current.IsEntry = invertEntryExit ? isInside : !isInside;
                isInside = !isInside;
            }
            current = current.Next!;
        } while (current != start);
    }

    private static List<VPolygon> TraceResultPolygons(ClipVertex subjectStart, ClipVertex clipStart, ClipOperation operation)
    {
        var result = new List<VPolygon>();

        // Reset visited flags
        ResetVisited(subjectStart);
        ResetVisited(clipStart);

        // Find unvisited intersection points and trace polygons
        var current = subjectStart;
        do
        {
            if (current.IsIntersection && !current.Visited)
            {
                bool shouldStart = operation switch
                {
                    ClipOperation.Intersection => current.IsEntry,
                    ClipOperation.Union => !current.IsEntry,
                    ClipOperation.Difference => !current.IsEntry,
                    _ => false
                };

                if (shouldStart)
                {
                    var polygon = TracePolygon(current, operation);
                    if (polygon != null && polygon.Points.Count >= 3)
                    {
                        result.Add(polygon);
                    }
                }
            }
            current = current.Next!;
        } while (current != subjectStart);

        return result;
    }

    private static void ResetVisited(ClipVertex start)
    {
        var current = start;
        do
        {
            current.Visited = false;
            current = current.Next!;
        } while (current != start);
    }

    private static VPolygon? TracePolygon(ClipVertex start, ClipOperation operation)
    {
        var points = new List<VPoint>();
        var current = start;
        bool onSubject = true;

        int maxIterations = 10000; // Safety limit
        int iterations = 0;

        do
        {
            if (iterations++ > maxIterations)
            {
                // Prevent infinite loop
                return null;
            }

            points.Add(VPoint.Internal(current.Point.X, current.Point.Y));
            current.Visited = true;

            if (current.IsIntersection)
            {
                // Mark neighbor as visited too
                if (current.Neighbor != null)
                {
                    current.Neighbor.Visited = true;
                }

                // Decide whether to switch based on operation and entry/exit status
                bool shouldSwitch = operation switch
                {
                    // For intersection: switch when exiting (to follow interior boundary)
                    ClipOperation.Intersection => !current.IsEntry,
                    // For union: switch when entering (to follow exterior boundary)
                    ClipOperation.Union => current.IsEntry,
                    // For difference: switch when entering clip polygon
                    ClipOperation.Difference => current.IsEntry,
                    _ => true
                };

                if (shouldSwitch && current.Neighbor != null)
                {
                    current = current.Neighbor;
                    onSubject = !onSubject;
                }
            }

            // Move to next vertex
            // For Difference: traverse clip polygon in reverse direction
            if (operation == ClipOperation.Difference && !onSubject)
            {
                current = current.Prev!;
            }
            else
            {
                current = current.Next!;
            }

        } while (current != start && !current.Visited);

        if (points.Count < 3)
            return null;

        // Remove duplicate consecutive points
        var cleanedPoints = new List<VPoint>();
        for (int i = 0; i < points.Count; i++)
        {
            if (cleanedPoints.Count == 0 ||
                !GeometryTolerance.PointsAreEqual(points[i], cleanedPoints[^1]))
            {
                cleanedPoints.Add(points[i]);
            }
        }

        // Check if last point equals first and remove
        if (cleanedPoints.Count > 1 &&
            GeometryTolerance.PointsAreEqual(cleanedPoints[0], cleanedPoints[^1]))
        {
            cleanedPoints.RemoveAt(cleanedPoints.Count - 1);
        }

        if (cleanedPoints.Count < 3)
            return null;

        return new VPolygon(cleanedPoints);
    }

    /// <summary>
    /// Tests if a point is inside a polygon using the ray casting algorithm.
    /// </summary>
    public static bool PointInPolygonTest(VPoint point, List<VPoint> polygon)
    {
        if (polygon.Count < 3)
            return false;

        int n = polygon.Count;
        bool inside = false;

        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            var pi = polygon[i];
            var pj = polygon[j];

            // Check if point is exactly on an edge
            if (IsPointOnEdge(point, pi, pj))
                return true;

            // Ray casting algorithm
            if (((pi.Y > point.Y) != (pj.Y > point.Y)) &&
                (point.X < (pj.X - pi.X) * (point.Y - pi.Y) / (pj.Y - pi.Y) + pi.X))
            {
                inside = !inside;
            }
        }

        return inside;
    }

    private static bool IsPointOnEdge(VPoint point, VPoint edgeStart, VPoint edgeEnd)
    {
        // Check if point is collinear with edge
        double cross = (point.X - edgeStart.X) * (edgeEnd.Y - edgeStart.Y) -
                       (point.Y - edgeStart.Y) * (edgeEnd.X - edgeStart.X);

        if (Math.Abs(cross) > Epsilon * 100)
            return false;

        // Check if point is within edge bounds
        double minX = Math.Min(edgeStart.X, edgeEnd.X);
        double maxX = Math.Max(edgeStart.X, edgeEnd.X);
        double minY = Math.Min(edgeStart.Y, edgeEnd.Y);
        double maxY = Math.Max(edgeStart.Y, edgeEnd.Y);

        return point.X >= minX - Epsilon && point.X <= maxX + Epsilon &&
               point.Y >= minY - Epsilon && point.Y <= maxY + Epsilon;
    }

    #endregion
}
