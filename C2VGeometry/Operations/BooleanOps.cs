using System;
using System.Collections.Generic;
using System.Linq;

namespace C2VGeometry;

/// <summary>
/// Provides boolean operations (union, intersection, difference) on polygons.
/// Delegates to the Clipper2 library via <see cref="PolygonClipper"/>.
/// </summary>
public static class BooleanOps
{
    #region Standard Boolean Operations

    /// <summary>
    /// Computes the union of multiple polygons.
    /// Returns a single polygon if successful, or null if a single polygon cannot be formed.
    /// </summary>
    public static VPolygon? Union(params VPolygon[] polygons)
    {
        if (polygons.Length == 0)
        {
            Console.WriteLine("BooleanOps.Union: No polygons provided.");
            return null;
        }
        if (polygons.Length == 1)
        {
            return (VPolygon)polygons[0].Clone();
        }

        // Chain union operations
        var current = polygons[0];
        for (int i = 1; i < polygons.Length; i++)
        {
            var results = PolygonClipper.Union(current, polygons[i]);
            if (results.Count == 0)
            {
                Console.WriteLine("BooleanOps.Union: Operation resulted in no polygon (empty result).");
                return null;
            }
            if (results.Count > 1)
            {
                Console.WriteLine($"BooleanOps.Union: Cannot form a single polygon. Result contains {results.Count} disjoint regions (polygons do not overlap or touch).");
                return null;
            }
            current = results[0];
        }

        return current;
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
        return PolygonClipper.Intersect(a, b);
    }

    /// <summary>
    /// Computes the difference of two polygons (a - b).
    /// </summary>
    public static List<VPolygon> Difference(VPolygon a, VPolygon b)
    {
        return PolygonClipper.Difference(a, b);
    }

    /// <summary>
    /// Computes the symmetric difference (XOR) of two polygons.
    /// </summary>
    public static List<VPolygon> Xor(VPolygon a, VPolygon b)
    {
        return PolygonClipper.Xor(a, b);
    }

    #endregion

    #region Region Operations (delegate to RegionBooleanOps)

    // Convenience overloads so Region boolean ops can be reached through the same BooleanOps
    // entry point. These simply forward to RegionBooleanOps (the canonical region API).

    /// <summary>Computes the union of two regions (single Region, or null if disjoint).</summary>
    public static Region? Union(Region a, Region b) => RegionBooleanOps.Union(a, b);

    /// <summary>Computes the union of a collection of regions (e.g. a List&lt;Region&gt;).</summary>
    public static Region? Union(IEnumerable<Region> regions) => RegionBooleanOps.Union(regions);

    /// <summary>Computes the intersection of two regions.</summary>
    public static List<Region> Intersect(Region a, Region b) => RegionBooleanOps.Intersect(a, b);

    /// <summary>Computes the intersection common to a collection of regions.</summary>
    public static List<Region> Intersect(IEnumerable<Region> regions) => RegionBooleanOps.Intersect(regions);

    /// <summary>Computes the difference of two regions (a - b).</summary>
    public static List<Region> Difference(Region a, Region b) => RegionBooleanOps.Difference(a, b);

    /// <summary>Computes the first region minus every subsequent region in the collection.</summary>
    public static List<Region> Difference(IEnumerable<Region> regions) => RegionBooleanOps.Difference(regions);

    /// <summary>Computes the symmetric difference (XOR) of two regions.</summary>
    public static List<Region> Xor(Region a, Region b) => RegionBooleanOps.Xor(a, b);

    /// <summary>Computes the running symmetric difference (XOR) of a collection of regions.</summary>
    public static List<Region> Xor(IEnumerable<Region> regions) => RegionBooleanOps.Xor(regions);

    #endregion

    #region Boolean Operations with Hole Support

    /// <summary>
    /// Computes the intersection of two polygons, returning results with hole information.
    /// </summary>
    public static List<PolygonWithHoles> IntersectWithHoles(VPolygon a, VPolygon b)
    {
        return PolygonClipper.IntersectWithHoles(a, b);
    }

    /// <summary>
    /// Computes the union of two polygons, returning results with hole information.
    /// </summary>
    public static List<PolygonWithHoles> UnionWithHoles(VPolygon a, VPolygon b)
    {
        return PolygonClipper.UnionWithHoles(a, b);
    }

    /// <summary>
    /// Computes the difference of two polygons, returning results with hole information.
    /// </summary>
    public static List<PolygonWithHoles> DifferenceWithHoles(VPolygon a, VPolygon b)
    {
        return PolygonClipper.DifferenceWithHoles(a, b);
    }

    #endregion

    #region Self-Intersection Handling

    /// <summary>
    /// Converts a potentially self-intersecting polygon into one or more simple polygons.
    /// Uses self-union to resolve self-intersections.
    /// </summary>
    public static List<VPolygon> MakeSimple(VPolygon polygon)
    {
        return PolygonClipper.MakeSimple(polygon);
    }

    /// <summary>
    /// Checks if a polygon has self-intersections using spatial acceleration.
    /// </summary>
    public static bool HasSelfIntersections(VPolygon polygon)
    {
        var intersections = SpatialAccelerator.FindSelfIntersections(polygon);
        return intersections.Count > 0;
    }

    #endregion

    #region Offset Operations

    /// <summary>
    /// Offsets a polygon by a distance. Positive = outward, negative = inward.
    /// Self-intersections in the result are automatically resolved.
    /// </summary>
    public static List<VPolygon> OffsetPolygon(VPolygon polygon, double distance,
        JoinType joinType = JoinType.Miter, EndType endType = EndType.Polygon)
    {
        return PolygonOffset.Offset(polygon, distance, joinType, endType);
    }

    /// <summary>
    /// Offsets a polygon with safe inward offset handling.
    /// Automatically caps inward offsets at the maximum safe distance to prevent collapse.
    /// </summary>
    public static List<VPolygon> OffsetPolygonSafe(VPolygon polygon, double distance,
        JoinType joinType = JoinType.Miter, EndType endType = EndType.Polygon)
    {
        return PolygonOffset.OffsetSafe(polygon, distance, joinType, endType);
    }

    /// <summary>
    /// Computes the maximum safe inward offset distance for a polygon.
    /// This is the distance at which the polygon would collapse or self-intersect.
    /// </summary>
    public static double MaxSafeInwardOffset(VPolygon polygon)
    {
        return PolygonOffset.ComputeMaxSafeInwardOffset(polygon);
    }

    #endregion

    #region Simplification and Analysis

    /// <summary>
    /// Simplifies a polygon by removing redundant points using the Douglas-Peucker algorithm.
    /// </summary>
    public static VPolygon Simplify(VPolygon polygon, double tolerance = 0.1)
    {
        return PolygonSimplify.Simplify(polygon, tolerance);
    }

    /// <summary>
    /// Calculates the area of a polygon (positive = counter-clockwise, negative = clockwise).
    /// </summary>
    public static double Area(VPolygon polygon)
    {
        return polygon.SignedArea;
    }

    /// <summary>
    /// Checks if a point is inside a polygon.
    /// Returns true if the point is inside or on the boundary.
    /// </summary>
    public static bool PointInPolygon(VPolygon polygon, VXYZ point)
    {
        return PolygonClipper.PointInPolygonTest(point, polygon.Points);
    }

    #endregion
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
    /// Offsets this polygon by a distance with safe inward offset handling.
    /// </summary>
    public static List<VPolygon> OffsetPolygonSafe(this VPolygon polygon, double distance)
    {
        return BooleanOps.OffsetPolygonSafe(polygon, distance);
    }

    /// <summary>
    /// Converts this potentially self-intersecting polygon into simple polygons.
    /// </summary>
    public static List<VPolygon> MakeSimple(this VPolygon polygon)
    {
        return BooleanOps.MakeSimple(polygon);
    }

    /// <summary>
    /// Checks if this polygon has self-intersections.
    /// </summary>
    public static bool HasSelfIntersections(this VPolygon polygon)
    {
        return BooleanOps.HasSelfIntersections(polygon);
    }

    /// <summary>
    /// Gets the maximum safe inward offset distance for this polygon.
    /// </summary>
    public static double MaxSafeInwardOffset(this VPolygon polygon)
    {
        return BooleanOps.MaxSafeInwardOffset(polygon);
    }

    /// <summary>
    /// Checks if a point is inside this polygon.
    /// </summary>
    public static bool Contains(this VPolygon polygon, VXYZ point)
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
