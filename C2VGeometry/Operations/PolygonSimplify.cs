using System;
using System.Collections.Generic;

namespace C2VGeometry;

/// <summary>
/// Provides polygon simplification using the Douglas-Peucker algorithm.
/// </summary>
internal static class PolygonSimplify
{
    /// <summary>
    /// Simplifies a polygon by removing points that are within the specified tolerance
    /// of the line between their neighbors, using the Douglas-Peucker algorithm.
    /// </summary>
    public static VPolygon Simplify(VPolygon polygon, double tolerance = 0.1)
    {
        if (polygon.Points.Count <= 3 || tolerance <= 0)
        {
            return (VPolygon)polygon.Clone();
        }

        var points = polygon.Points;

        // For a closed polygon, we need to handle the wrap-around case
        // Find the point with max distance from line (first to last)
        // Then recursively simplify

        // Since it's a closed polygon, we simplify in a circular manner
        // Find the two points furthest apart
        int maxIdx1 = 0, maxIdx2 = 0;
        double maxDist = 0;

        for (int i = 0; i < points.Count; i++)
        {
            for (int j = i + 1; j < points.Count; j++)
            {
                double dist = points[i].DistanceTo(points[j]);
                if (dist > maxDist)
                {
                    maxDist = dist;
                    maxIdx1 = i;
                    maxIdx2 = j;
                }
            }
        }

        // Build two chains: maxIdx1 -> maxIdx2 and maxIdx2 -> maxIdx1 (wrapping)
        var chain1 = new List<VXYZ>();
        for (int i = maxIdx1; i <= maxIdx2; i++)
        {
            chain1.Add(points[i]);
        }

        var chain2 = new List<VXYZ>();
        for (int i = maxIdx2; i < points.Count; i++)
        {
            chain2.Add(points[i]);
        }
        for (int i = 0; i <= maxIdx1; i++)
        {
            chain2.Add(points[i]);
        }

        // Simplify both chains
        var simplified1 = DouglasPeucker(chain1, tolerance);
        var simplified2 = DouglasPeucker(chain2, tolerance);

        // Combine the simplified chains (remove duplicate endpoints)
        var result = new List<VXYZ>();
        result.AddRange(simplified1);

        // Add chain2 points except the first (it's the same as last of chain1)
        // and the last (it's the same as first of chain1)
        for (int i = 1; i < simplified2.Count - 1; i++)
        {
            result.Add(simplified2[i]);
        }

        if (result.Count < 3)
        {
            return (VPolygon)polygon.Clone();
        }

        return new VPolygon(result);
    }

    /// <summary>
    /// Douglas-Peucker algorithm for polyline simplification.
    /// </summary>
    private static List<VXYZ> DouglasPeucker(List<VXYZ> points, double tolerance)
    {
        if (points.Count <= 2)
        {
            return new List<VXYZ>(points);
        }

        // Find the point with maximum distance from the line between first and last
        double maxDist = 0;
        int maxIdx = 0;

        var first = points[0];
        var last = points[^1];

        for (int i = 1; i < points.Count - 1; i++)
        {
            double dist = PerpendicularDistance(points[i], first, last);
            if (dist > maxDist)
            {
                maxDist = dist;
                maxIdx = i;
            }
        }

        // If max distance is greater than tolerance, recursively simplify
        if (maxDist > tolerance)
        {
            // Recursively simplify both halves
            var left = DouglasPeucker(points.GetRange(0, maxIdx + 1), tolerance);
            var right = DouglasPeucker(points.GetRange(maxIdx, points.Count - maxIdx), tolerance);

            // Combine results (remove duplicate at junction)
            var result = new List<VXYZ>(left);
            result.AddRange(right.GetRange(1, right.Count - 1));
            return result;
        }
        else
        {
            // Just return endpoints
            return new List<VXYZ> { first, last };
        }
    }

    /// <summary>
    /// Computes the perpendicular distance from a point to a line defined by two points.
    /// </summary>
    private static double PerpendicularDistance(VXYZ point, VXYZ lineStart, VXYZ lineEnd)
    {
        double dx = lineEnd.X - lineStart.X;
        double dy = lineEnd.Y - lineStart.Y;

        double lengthSq = dx * dx + dy * dy;

        if (lengthSq < GeometryTolerance.Epsilon)
        {
            // Degenerate line, return distance to start point
            return point.DistanceTo(lineStart);
        }

        // Perpendicular distance = |cross product| / |line length|
        double cross = Math.Abs((point.X - lineStart.X) * dy - (point.Y - lineStart.Y) * dx);
        return cross / Math.Sqrt(lengthSq);
    }
}
