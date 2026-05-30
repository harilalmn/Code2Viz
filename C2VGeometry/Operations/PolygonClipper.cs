using System;
using System.Collections.Generic;
using System.Linq;
using Clipper2Lib;

namespace C2VGeometry;

/// <summary>
/// Provides polygon boolean operations (intersection, union, difference, XOR) by delegating to
/// the Clipper2 library (a robust Vatti-based clipper).
/// <para>
/// This replaced a hand-rolled Greiner-Hormann implementation that failed on degenerate
/// configurations — notably <i>vertex-on-edge</i> touches (e.g. a circle's polygon vertex landing
/// exactly on a rectangle edge, which produced zero detected crossings and a spurious "disjoint"
/// result) and full collinear shared-edge bands (which mis-unioned to a single input). Clipper2
/// handles all of these robustly.
/// </para>
/// <para>
/// Clipper2 works in scaled integer space; the <c>ClipperD</c> (double) API does the scaling for us.
/// <see cref="Precision"/> sets the number of preserved decimal places. The point-in-polygon test
/// remains a local ray-cast (unchanged) because it is correct and is used widely outside boolean ops.
/// </para>
/// </summary>
internal static class PolygonClipper
{
    private const double Epsilon = GeometryTolerance.Epsilon;

    /// <summary>
    /// Decimal places preserved when Clipper2 scales doubles to its internal integer grid.
    /// 6 places (micro-unit resolution) is ample for this app's coordinate range while keeping
    /// scaled values far inside Int64 limits.
    /// </summary>
    private const int Precision = 6;

    #region MakeSimple - Resolve Self-Intersections

    /// <summary>
    /// Converts a potentially self-intersecting polygon into one or more simple polygons by
    /// self-unioning it under the non-zero fill rule (which resolves the self-intersections).
    /// </summary>
    public static List<VPolygon> MakeSimple(VPolygon polygon)
    {
        if (polygon.Points.Count < 3)
            return new List<VPolygon> { (VPolygon)polygon.Clone() };

        var clipper = new ClipperD(Precision);
        clipper.AddSubject(new PathsD { ToPathD(polygon.Points) });

        var tree = new PolyTreeD();
        clipper.Execute(ClipType.Union, FillRule.NonZero, tree);

        var result = new List<VPolygon>();
        CollectOuterContours(tree, result);

        // If nothing came back (e.g. zero-area degenerate input), return the original unchanged.
        if (result.Count == 0)
            result.Add((VPolygon)polygon.Clone());

        return result;
    }

    #endregion

    #region Boolean Operations with Hole Support

    /// <summary>
    /// Computes the intersection of two polygons, returning results with hole information.
    /// </summary>
    public static List<PolygonWithHoles> IntersectWithHoles(VPolygon subject, VPolygon clip)
        => ExecuteWithHoles(subject, clip, ClipType.Intersection);

    /// <summary>
    /// Computes the union of two polygons, returning results with hole information.
    /// </summary>
    public static List<PolygonWithHoles> UnionWithHoles(VPolygon subject, VPolygon clip)
        => ExecuteWithHoles(subject, clip, ClipType.Union);

    /// <summary>
    /// Computes the difference of two polygons (subject - clip), returning results with hole information.
    /// </summary>
    public static List<PolygonWithHoles> DifferenceWithHoles(VPolygon subject, VPolygon clip)
        => ExecuteWithHoles(subject, clip, ClipType.Difference);

    #endregion

    #region Standard Boolean Operations

    /// <summary>
    /// Computes the intersection of two polygons. Returns each solid piece as a separate polygon
    /// (holes are not represented — use <see cref="IntersectWithHoles"/> for hole support).
    /// </summary>
    public static List<VPolygon> Intersect(VPolygon subject, VPolygon clip)
        => Execute(subject, clip, ClipType.Intersection);

    /// <summary>
    /// Computes the union of two polygons. A disjoint union yields more than one polygon.
    /// </summary>
    public static List<VPolygon> Union(VPolygon subject, VPolygon clip)
        => Execute(subject, clip, ClipType.Union);

    /// <summary>
    /// Computes the difference of two polygons (subject - clip).
    /// </summary>
    public static List<VPolygon> Difference(VPolygon subject, VPolygon clip)
        => Execute(subject, clip, ClipType.Difference);

    /// <summary>
    /// Computes the symmetric difference (XOR) of two polygons as the flat list of solid pieces.
    /// Implemented as (subject - clip) plus (clip - subject) so the result is a set of hole-free
    /// outer contours (matching the historical contract used by callers that wrap each piece as a
    /// separate Region). A ring-with-hole XOR is therefore expressed as its two crescent solids.
    /// </summary>
    public static List<VPolygon> Xor(VPolygon subject, VPolygon clip)
    {
        var result = new List<VPolygon>();
        result.AddRange(Execute(subject, clip, ClipType.Difference));
        result.AddRange(Execute(clip, subject, ClipType.Difference));
        return result;
    }

    #endregion

    #region Internal Implementation (Clipper2)

    private static List<VPolygon> Execute(VPolygon subject, VPolygon clip, ClipType clipType)
    {
        var result = new List<VPolygon>();
        if (subject.Points.Count < 3 || clip.Points.Count < 3)
            return result;

        var clipper = new ClipperD(Precision);
        clipper.AddSubject(new PathsD { ToPathD(subject.Points) });
        clipper.AddClip(new PathsD { ToPathD(clip.Points) });

        var tree = new PolyTreeD();
        clipper.Execute(clipType, FillRule.NonZero, tree);

        CollectOuterContours(tree, result);
        return result;
    }

    private static List<PolygonWithHoles> ExecuteWithHoles(VPolygon subject, VPolygon clip, ClipType clipType)
    {
        var result = new List<PolygonWithHoles>();
        if (subject.Points.Count < 3 || clip.Points.Count < 3)
            return result;

        var clipper = new ClipperD(Precision);
        clipper.AddSubject(new PathsD { ToPathD(subject.Points) });
        clipper.AddClip(new PathsD { ToPathD(clip.Points) });

        var tree = new PolyTreeD();
        clipper.Execute(clipType, FillRule.NonZero, tree);

        BuildPolygonsWithHoles(tree, result);
        return result;
    }

    /// <summary>
    /// Walks the Clipper2 PolyTree and collects every solid (non-hole) contour, at any depth, as a
    /// flat list of polygons. Islands nested inside holes are included; hole rings are dropped.
    /// </summary>
    private static void CollectOuterContours(PolyPathD node, List<VPolygon> outers)
    {
        for (int i = 0; i < node.Count; i++)
        {
            var child = node[i];
            if (!child.IsHole && child.Polygon is { Count: >= 3 } poly)
                outers.Add(ToVPolygon(poly));

            CollectOuterContours(child, outers);
        }
    }

    /// <summary>
    /// Walks the Clipper2 PolyTree building <see cref="PolygonWithHoles"/>: each solid contour
    /// becomes an outer with its immediate hole children attached; islands inside those holes are
    /// recursively emitted as further outers.
    /// </summary>
    private static void BuildPolygonsWithHoles(PolyPathD node, List<PolygonWithHoles> result)
    {
        for (int i = 0; i < node.Count; i++)
        {
            var outer = node[i];

            if (outer.IsHole)
            {
                // Defensive: a hole encountered at an unexpected level — recurse for its islands.
                BuildPolygonsWithHoles(outer, result);
                continue;
            }

            if (outer.Polygon is not { Count: >= 3 } outerPoly)
            {
                BuildPolygonsWithHoles(outer, result);
                continue;
            }

            var pwh = new PolygonWithHoles(ToVPolygon(outerPoly));
            for (int j = 0; j < outer.Count; j++)
            {
                var hole = outer[j];
                if (hole.Polygon is { Count: >= 3 } holePoly)
                    pwh.AddHole(ToVPolygon(holePoly));

                // Solid islands inside this hole become their own outer polygons.
                BuildPolygonsWithHoles(hole, result);
            }
            result.Add(pwh);
        }
    }

    private static PathD ToPathD(List<VXYZ> points)
    {
        var path = new PathD(points.Count);
        foreach (var p in points)
            path.Add(new PointD(p.X, p.Y));
        return path;
    }

    private static VPolygon ToVPolygon(PathD path)
    {
        var points = new List<VXYZ>(path.Count);
        foreach (var p in path)
            points.Add(new VXYZ(p.x, p.y));
        return new VPolygon(points);
    }

    #endregion

    #region Point-in-Polygon (local ray-cast, unchanged)

    /// <summary>
    /// Tests if a point is inside a polygon using the ray casting algorithm.
    /// Returns true for points on the boundary.
    /// </summary>
    public static bool PointInPolygonTest(VXYZ point, List<VXYZ> polygon)
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

    private static bool IsPointOnEdge(VXYZ point, VXYZ edgeStart, VXYZ edgeEnd)
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
