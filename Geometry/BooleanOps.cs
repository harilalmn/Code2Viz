using Clipper2Lib;
using Code2Viz.Console;

namespace Code2Viz.Geometry;

/// <summary>
/// Provides boolean operations (union, intersection, difference) on polygons using Clipper2.
/// </summary>
public static class BooleanOps
{
    private const double Scale = 1000000.0; // Scale factor for integer conversion

    /// <summary>
    /// Computes the union of multiple polygons.
    /// Returns a single polygon if successful, or null if a single polygon cannot be formed.
    /// </summary>
    public static VPolygon? Union(params VPolygon[] polygons)
    {
        if (polygons.Length == 0)
        {
            ConsoleOutput.Instance.AddEntry("BooleanOps.Union: No polygons provided.", isError: true);
            return null;
        }
        if (polygons.Length == 1)
        {
            return (VPolygon)polygons[0].Clone();
        }

        var subject = ToPath64(polygons[0]);
        var clips = new Paths64();
        for (int i = 1; i < polygons.Length; i++)
        {
            clips.Add(ToPath64(polygons[i]));
        }

        var solution = Clipper.Union(new Paths64 { subject }, clips, FillRule.NonZero);
        var results = FromPaths64(solution);

        if (results.Count == 0)
        {
            ConsoleOutput.Instance.AddEntry("BooleanOps.Union: Operation resulted in no polygon (empty result).", isError: true);
            return null;
        }
        if (results.Count > 1)
        {
            ConsoleOutput.Instance.AddEntry($"BooleanOps.Union: Cannot form a single polygon. Result contains {results.Count} disjoint regions (polygons do not overlap or touch).", isError: true);
            return null;
        }

        return results[0];
    }

    /// <summary>
    /// Computes the union of a list of polygons.
    /// Returns a single polygon if successful, or null if a single polygon cannot be formed.
    /// </summary>
    public static VPolygon? Union(IEnumerable<VPolygon> polygons)
    {
        return Union(polygons.ToArray());
    }

    /// <summary>
    /// Computes the intersection of two polygons.
    /// </summary>
    public static List<VPolygon> Intersect(VPolygon a, VPolygon b)
    {
        var subject = new Paths64 { ToPath64(a) };
        var clip = new Paths64 { ToPath64(b) };

        var solution = Clipper.Intersect(subject, clip, FillRule.NonZero);
        return FromPaths64(solution);
    }

    /// <summary>
    /// Computes the difference of two polygons (a - b).
    /// </summary>
    public static List<VPolygon> Difference(VPolygon a, VPolygon b)
    {
        var subject = new Paths64 { ToPath64(a) };
        var clip = new Paths64 { ToPath64(b) };

        var solution = Clipper.Difference(subject, clip, FillRule.NonZero);
        return FromPaths64(solution);
    }

    /// <summary>
    /// Computes the symmetric difference (XOR) of two polygons.
    /// </summary>
    public static List<VPolygon> Xor(VPolygon a, VPolygon b)
    {
        var subject = new Paths64 { ToPath64(a) };
        var clip = new Paths64 { ToPath64(b) };

        var solution = Clipper.Xor(subject, clip, FillRule.NonZero);
        return FromPaths64(solution);
    }

    /// <summary>
    /// Offsets a polygon by a distance. Positive = outward, negative = inward.
    /// </summary>
    public static List<VPolygon> OffsetPolygon(VPolygon polygon, double distance,
        JoinType joinType = JoinType.Miter, EndType endType = EndType.Polygon)
    {
        var path = ToPath64(polygon);
        var solution = Clipper.InflatePaths(new Paths64 { path }, distance * Scale, joinType, endType);
        return FromPaths64(solution);
    }

    /// <summary>
    /// Simplifies a polygon by removing redundant points.
    /// </summary>
    public static VPolygon Simplify(VPolygon polygon, double tolerance = 0.1)
    {
        var path = ToPath64(polygon);
        var simplified = Clipper.SimplifyPath(path, tolerance * Scale);
        var results = FromPaths64(new Paths64 { simplified });
        return results.Count > 0 ? results[0] : (VPolygon)polygon.Clone();
    }

    /// <summary>
    /// Calculates the area of a polygon (positive = counter-clockwise, negative = clockwise).
    /// </summary>
    public static double Area(VPolygon polygon)
    {
        var path = ToPath64(polygon);
        return Clipper.Area(path) / (Scale * Scale);
    }

    /// <summary>
    /// Checks if a point is inside a polygon.
    /// </summary>
    public static bool PointInPolygon(VPolygon polygon, VPoint point)
    {
        var path = ToPath64(polygon);
        var pt = new Point64((long)(point.X * Scale), (long)(point.Y * Scale));
        var result = Clipper.PointInPolygon(pt, path);
        return result != PointInPolygonResult.IsOutside;
    }

    // Conversion helpers
    private static Path64 ToPath64(VPolygon polygon)
    {
        var path = new Path64();
        foreach (var pt in polygon.Points)
        {
            path.Add(new Point64((long)(pt.X * Scale), (long)(pt.Y * Scale)));
        }
        return path;
    }

    private static List<VPolygon> FromPaths64(Paths64 paths)
    {
        var result = new List<VPolygon>();
        foreach (var path in paths)
        {
            if (path.Count < 3) continue;

            var points = new List<VPoint>();
            foreach (var pt in path)
            {
                points.Add(new VPoint(pt.X / Scale, pt.Y / Scale));
            }
            result.Add(new VPolygon(points));
        }
        return result;
    }
}

/// <summary>
/// Extension methods for VPolygon boolean operations.
/// </summary>
public static class VPolygonBooleanExtensions
{
    /// <summary>
    /// Computes the union of this polygon with another.
    /// Returns a single polygon if successful, or null if a single polygon cannot be formed.
    /// </summary>
    public static VPolygon? Union(this VPolygon polygon, VPolygon other)
    {
        return BooleanOps.Union(polygon, other);
    }

    /// <summary>
    /// Computes the intersection of this polygon with another.
    /// </summary>
    public static List<VPolygon> Intersect(this VPolygon polygon, VPolygon other)
    {
        return BooleanOps.Intersect(polygon, other);
    }

    /// <summary>
    /// Computes the difference of this polygon with another (this - other).
    /// </summary>
    public static List<VPolygon> Difference(this VPolygon polygon, VPolygon other)
    {
        return BooleanOps.Difference(polygon, other);
    }

    /// <summary>
    /// Computes the symmetric difference (XOR) of this polygon with another.
    /// </summary>
    public static List<VPolygon> Xor(this VPolygon polygon, VPolygon other)
    {
        return BooleanOps.Xor(polygon, other);
    }

    /// <summary>
    /// Offsets this polygon by a distance.
    /// </summary>
    public static List<VPolygon> OffsetPolygon(this VPolygon polygon, double distance)
    {
        return BooleanOps.OffsetPolygon(polygon, distance);
    }

    /// <summary>
    /// Checks if a point is inside this polygon.
    /// </summary>
    public static bool Contains(this VPolygon polygon, VPoint point)
    {
        return BooleanOps.PointInPolygon(polygon, point);
    }

    /// <summary>
    /// Calculates the area of this polygon.
    /// </summary>
    public static double GetArea(this VPolygon polygon)
    {
        return Math.Abs(BooleanOps.Area(polygon));
    }
}
