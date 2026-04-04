using System;
using System.Collections.Generic;
using System.Linq;

namespace C2VGeometry;

/// <summary>
/// Provides polygon offset operations using vertex normal offset with corner handling.
/// </summary>
internal static class PolygonOffset
{
    private const double Epsilon = GeometryTolerance.Epsilon;
    private const double DefaultMiterLimit = 2.0;
    private const int ArcSegments = 8; // Number of segments for round corners

    #region Public Methods

    /// <summary>
    /// Offsets a polygon by the specified distance.
    /// Positive distance = outward, negative = inward.
    /// </summary>
    public static List<VPolygon> Offset(VPolygon polygon, double distance,
                                         JoinType joinType = JoinType.Miter,
                                         EndType endType = EndType.Polygon)
    {
        var result = OffsetCore(polygon, distance, joinType, endType);

        // Post-process: fix any self-intersections
        return PostProcessOffset(result);
    }

    /// <summary>
    /// Offsets a polygon with safe inward offset handling.
    /// Automatically caps inward offsets at the maximum safe distance to prevent collapse.
    /// </summary>
    public static List<VPolygon> OffsetSafe(VPolygon polygon, double distance,
                                             JoinType joinType = JoinType.Miter,
                                             EndType endType = EndType.Polygon)
    {
        if (polygon.Points.Count < 3)
        {
            return new List<VPolygon> { (VPolygon)polygon.Clone() };
        }

        double actualDistance = distance;

        // For inward offsets, cap at safe distance
        if (distance < 0)
        {
            double maxSafe = ComputeMaxSafeInwardOffset(polygon);
            if (Math.Abs(distance) > maxSafe * 0.99)
            {
                actualDistance = -maxSafe * 0.99;
            }
        }

        var result = OffsetCore(polygon, actualDistance, joinType, endType);

        // Post-process: fix any self-intersections
        return PostProcessOffset(result);
    }

    /// <summary>
    /// Computes the maximum safe inward offset distance.
    /// This is the distance to the closest point of the medial axis,
    /// approximated by half the minimum edge-to-edge distance.
    /// </summary>
    public static double ComputeMaxSafeInwardOffset(VPolygon polygon)
    {
        if (polygon.Points.Count < 3)
            return 0;

        var points = polygon.Points;
        int n = points.Count;

        double minDistance = double.MaxValue;

        // Method 1: Find minimum distance from each vertex to non-adjacent edges
        for (int i = 0; i < n; i++)
        {
            var vertex = points[i];

            for (int j = 0; j < n; j++)
            {
                // Skip adjacent edges
                if (j == i || j == (i - 1 + n) % n || j == (i + 1) % n)
                    continue;

                var edgeStart = points[j];
                var edgeEnd = points[(j + 1) % n];

                double dist = PointToSegmentDistance(vertex, edgeStart, edgeEnd);
                if (dist < minDistance)
                    minDistance = dist;
            }
        }

        // Method 2: Find minimum edge length and use half of it
        for (int i = 0; i < n; i++)
        {
            double edgeLen = points[i].DistanceTo(points[(i + 1) % n]);
            if (edgeLen / 2 < minDistance)
                minDistance = edgeLen / 2;
        }

        // Method 3: For convex polygons, compute apothem (inradius)
        // Area / semi-perimeter gives inradius for convex polygons
        double area = Math.Abs(polygon.SignedArea);
        double perimeter = polygon.GetLength();
        double inradius = 2 * area / perimeter;

        if (inradius < minDistance)
            minDistance = inradius;

        return Math.Max(0, minDistance);
    }

    #endregion

    #region Core Offset Implementation

    private static List<VPolygon> OffsetCore(VPolygon polygon, double distance,
                                              JoinType joinType, EndType endType)
    {
        var result = new List<VPolygon>();

        if (polygon.Points.Count < 3 || Math.Abs(distance) < Epsilon)
        {
            result.Add((VPolygon)polygon.Clone());
            return result;
        }

        var points = polygon.Points;
        int n = points.Count;

        // Determine winding order - if negative area, polygon is clockwise
        bool isClockwise = polygon.SignedArea < 0;

        // For clockwise polygons, we need to invert the offset direction
        double effectiveDistance = isClockwise ? -distance : distance;

        var offsetPoints = new List<VXYZ>();

        for (int i = 0; i < n; i++)
        {
            int prevIdx = (i - 1 + n) % n;
            int nextIdx = (i + 1) % n;

            var prev = points[prevIdx];
            var curr = points[i];
            var next = points[nextIdx];

            // Compute edge directions
            var dir1 = Normalize(curr.X - prev.X, curr.Y - prev.Y);
            var dir2 = Normalize(next.X - curr.X, next.Y - curr.Y);

            // Compute edge normals (perpendicular to the left)
            var n1 = (-dir1.dy, dir1.dx);
            var n2 = (-dir2.dy, dir2.dx);

            // Compute corner points based on join type
            var cornerPoints = ComputeCornerPoints(curr, n1, n2, dir1, dir2,
                                                   effectiveDistance, joinType);
            offsetPoints.AddRange(cornerPoints);
        }

        if (offsetPoints.Count >= 3)
        {
            var cleaned = CleanupOffsetPolygon(offsetPoints);
            if (cleaned.Count >= 3)
            {
                result.Add(new VPolygon(cleaned));
            }
        }

        return result;
    }

    private static List<VPolygon> PostProcessOffset(List<VPolygon> polygons)
    {
        var result = new List<VPolygon>();

        foreach (var polygon in polygons)
        {
            // Check for self-intersections
            var selfIntersections = SpatialAccelerator.FindSelfIntersections(polygon);

            if (selfIntersections.Count == 0)
            {
                // No self-intersections, keep as-is
                result.Add(polygon);
            }
            else
            {
                // Fix self-intersections using MakeSimple
                var fixedPolygons = PolygonClipper.MakeSimple(polygon);

                // Filter out tiny polygons that might result from collapse
                foreach (var p in fixedPolygons)
                {
                    if (Math.Abs(p.SignedArea) > Epsilon * 1000)
                    {
                        result.Add(p);
                    }
                }
            }
        }

        return result;
    }

    #endregion

    #region Helper Methods

    private static double PointToSegmentDistance(VXYZ point, VXYZ segStart, VXYZ segEnd)
    {
        double dx = segEnd.X - segStart.X;
        double dy = segEnd.Y - segStart.Y;
        double lengthSq = dx * dx + dy * dy;

        if (lengthSq < Epsilon)
        {
            return point.DistanceTo(segStart);
        }

        // Project point onto segment
        double t = ((point.X - segStart.X) * dx + (point.Y - segStart.Y) * dy) / lengthSq;
        t = Math.Clamp(t, 0, 1);

        double projX = segStart.X + t * dx;
        double projY = segStart.Y + t * dy;

        return Math.Sqrt((point.X - projX) * (point.X - projX) +
                         (point.Y - projY) * (point.Y - projY));
    }

    private static (double dx, double dy) Normalize(double dx, double dy)
    {
        double len = Math.Sqrt(dx * dx + dy * dy);
        if (len < Epsilon)
            return (0, 0);
        return (dx / len, dy / len);
    }

    private static List<VXYZ> ComputeCornerPoints(VXYZ vertex,
                                                     (double nx, double ny) n1,
                                                     (double nx, double ny) n2,
                                                     (double dx, double dy) dir1,
                                                     (double dx, double dy) dir2,
                                                     double distance,
                                                     JoinType joinType)
    {
        var result = new List<VXYZ>();

        // Compute bisector
        double bisectorX = n1.nx + n2.nx;
        double bisectorY = n1.ny + n2.ny;
        double bisectorLen = Math.Sqrt(bisectorX * bisectorX + bisectorY * bisectorY);

        if (bisectorLen < Epsilon)
        {
            // Normals are opposite (180 degree turn) - use n1
            result.Add(new VXYZ(vertex.X + n1.nx * distance, vertex.Y + n1.ny * distance));
            return result;
        }

        bisectorX /= bisectorLen;
        bisectorY /= bisectorLen;

        // Compute angle between normals
        double dot = n1.nx * n2.nx + n1.ny * n2.ny;
        dot = Math.Clamp(dot, -1.0, 1.0); // Clamp for numerical stability
        double angle = Math.Acos(dot);

        // Cross product to determine if convex or concave corner
        double cross = n1.nx * n2.ny - n1.ny * n2.nx;
        bool isConvex = (distance > 0) ? (cross >= 0) : (cross < 0);

        // Compute half angle for miter calculation
        double halfAngle = angle / 2.0;
        double cosHalfAngle = Math.Cos(halfAngle);

        if (Math.Abs(cosHalfAngle) < Epsilon)
        {
            // Very sharp corner - use simple offset
            result.Add(new VXYZ(vertex.X + n1.nx * distance, vertex.Y + n1.ny * distance));
            return result;
        }

        // Miter distance
        double miterDistance = Math.Abs(distance) / cosHalfAngle;
        double miterRatio = miterDistance / Math.Abs(distance);

        switch (joinType)
        {
            case JoinType.Miter:
                if (miterRatio <= DefaultMiterLimit || !isConvex)
                {
                    // Use miter join
                    result.Add(new VXYZ(vertex.X + bisectorX * miterDistance * Math.Sign(distance),
                                               vertex.Y + bisectorY * miterDistance * Math.Sign(distance)));
                }
                else
                {
                    // Miter limit exceeded - use bevel (square) instead
                    result.Add(new VXYZ(vertex.X + n1.nx * distance, vertex.Y + n1.ny * distance));
                    result.Add(new VXYZ(vertex.X + n2.nx * distance, vertex.Y + n2.ny * distance));
                }
                break;

            case JoinType.Round:
                if (isConvex)
                {
                    // Add arc for convex corners
                    double startAngle = Math.Atan2(n1.ny, n1.nx);
                    double endAngle = Math.Atan2(n2.ny, n2.nx);

                    // Ensure we go the right direction
                    double angleDiff = endAngle - startAngle;
                    if (angleDiff < -Math.PI) angleDiff += 2 * Math.PI;
                    if (angleDiff > Math.PI) angleDiff -= 2 * Math.PI;

                    int segments = Math.Max(2, (int)(Math.Abs(angleDiff) / (Math.PI / 4)));

                    for (int i = 0; i <= segments; i++)
                    {
                        double t = (double)i / segments;
                        double a = startAngle + t * angleDiff;
                        double px = vertex.X + Math.Cos(a) * Math.Abs(distance);
                        double py = vertex.Y + Math.Sin(a) * Math.Abs(distance);
                        result.Add(new VXYZ(px, py));
                    }
                }
                else
                {
                    // Concave corner - use miter
                    result.Add(new VXYZ(vertex.X + bisectorX * miterDistance * Math.Sign(distance),
                                               vertex.Y + bisectorY * miterDistance * Math.Sign(distance)));
                }
                break;

            case JoinType.Square:
                if (isConvex)
                {
                    // Add two points for squared corner
                    result.Add(new VXYZ(vertex.X + n1.nx * distance, vertex.Y + n1.ny * distance));
                    result.Add(new VXYZ(vertex.X + n2.nx * distance, vertex.Y + n2.ny * distance));
                }
                else
                {
                    // Concave corner - use miter
                    result.Add(new VXYZ(vertex.X + bisectorX * miterDistance * Math.Sign(distance),
                                               vertex.Y + bisectorY * miterDistance * Math.Sign(distance)));
                }
                break;
        }

        return result;
    }

    private static List<VXYZ> CleanupOffsetPolygon(List<VXYZ> points)
    {
        // Remove duplicate consecutive points
        var cleaned = new List<VXYZ>();

        for (int i = 0; i < points.Count; i++)
        {
            if (cleaned.Count == 0 ||
                !GeometryTolerance.PointsAreEqual(points[i], cleaned[^1]))
            {
                cleaned.Add(points[i]);
            }
        }

        // Remove last if equals first
        if (cleaned.Count > 1 && GeometryTolerance.PointsAreEqual(cleaned[0], cleaned[^1]))
        {
            cleaned.RemoveAt(cleaned.Count - 1);
        }

        // Remove collinear points
        if (cleaned.Count >= 3)
        {
            var simplified = new List<VXYZ>();
            for (int i = 0; i < cleaned.Count; i++)
            {
                int prev = (i - 1 + cleaned.Count) % cleaned.Count;
                int next = (i + 1) % cleaned.Count;

                if (!GeometryTolerance.AreCollinear(cleaned[prev], cleaned[i], cleaned[next]))
                {
                    simplified.Add(cleaned[i]);
                }
            }

            if (simplified.Count >= 3)
                cleaned = simplified;
        }

        return cleaned;
    }

    #endregion
}
