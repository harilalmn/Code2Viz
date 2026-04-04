using System;
using System.Collections.Generic;
using System.Linq;

namespace C2VGeometry;

/// <summary>
/// Represents a polygon with optional holes (islands).
/// The outer boundary is counter-clockwise, holes are clockwise.
/// </summary>
public class PolygonWithHoles
{
    /// <summary>
    /// The outer boundary of the polygon (counter-clockwise winding).
    /// </summary>
    public VPolygon Outer { get; set; }

    /// <summary>
    /// List of holes (clockwise winding) inside the outer boundary.
    /// </summary>
    public List<VPolygon> Holes { get; set; } = new();

    /// <summary>
    /// Creates a polygon with holes from an outer boundary.
    /// </summary>
    public PolygonWithHoles(VPolygon outer)
    {
        Outer = EnsureCounterClockwise(outer);
    }

    /// <summary>
    /// Creates a polygon with holes from an outer boundary and hole list.
    /// </summary>
    public PolygonWithHoles(VPolygon outer, IEnumerable<VPolygon> holes)
    {
        Outer = EnsureCounterClockwise(outer);
        foreach (var hole in holes)
        {
            Holes.Add(EnsureClockwise(hole));
        }
    }

    /// <summary>
    /// Adds a hole to the polygon.
    /// </summary>
    public void AddHole(VPolygon hole)
    {
        Holes.Add(EnsureClockwise(hole));
    }

    /// <summary>
    /// Gets the total area (outer area minus hole areas).
    /// </summary>
    public double Area
    {
        get
        {
            double area = Math.Abs(Outer.SignedArea);
            foreach (var hole in Holes)
            {
                area -= Math.Abs(hole.SignedArea);
            }
            return Math.Max(0, area);
        }
    }

    /// <summary>
    /// Checks if a point is inside the polygon (inside outer, outside all holes).
    /// </summary>
    public bool Contains(VXYZ point)
    {
        // Must be inside outer boundary
        if (!PolygonClipper.PointInPolygonTest(point, Outer.Points))
            return false;

        // Must be outside all holes
        foreach (var hole in Holes)
        {
            if (PolygonClipper.PointInPolygonTest(point, hole.Points))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Creates a deep clone of this polygon with holes.
    /// </summary>
    public PolygonWithHoles Clone()
    {
        var clone = new PolygonWithHoles((VPolygon)Outer.Clone());
        foreach (var hole in Holes)
        {
            clone.Holes.Add((VPolygon)hole.Clone());
        }
        return clone;
    }

    /// <summary>
    /// Analyzes a list of polygons from boolean operations and identifies
    /// outer boundaries vs holes based on winding order and containment.
    /// </summary>
    public static List<PolygonWithHoles> FromPolygonList(List<VPolygon> polygons)
    {
        if (polygons.Count == 0)
            return new List<PolygonWithHoles>();

        if (polygons.Count == 1)
            return new List<PolygonWithHoles> { new PolygonWithHoles(polygons[0]) };

        // Separate into potential outers (CCW, positive area) and holes (CW, negative area)
        var outers = new List<VPolygon>();
        var holes = new List<VPolygon>();

        foreach (var poly in polygons)
        {
            if (poly.SignedArea >= 0)
                outers.Add(poly);
            else
                holes.Add(poly);
        }

        // If no clear outers, use containment to determine
        if (outers.Count == 0)
        {
            // All are CW - find the largest (outermost)
            outers = polygons.OrderByDescending(p => Math.Abs(p.SignedArea)).Take(1).ToList();
            holes = polygons.Except(outers).ToList();
        }

        // Build result - assign holes to their containing outer
        var result = new List<PolygonWithHoles>();

        foreach (var outer in outers)
        {
            var pwh = new PolygonWithHoles(outer);

            // Find holes that are inside this outer
            var containedHoles = new List<VPolygon>();
            foreach (var hole in holes)
            {
                if (IsPolygonInsidePolygon(hole, outer))
                {
                    containedHoles.Add(hole);
                }
            }

            // Remove nested holes (hole inside another hole = actually solid)
            foreach (var hole in containedHoles.ToList())
            {
                bool isNestedInAnotherHole = containedHoles.Any(other =>
                    other != hole && IsPolygonInsidePolygon(hole, other));

                if (!isNestedInAnotherHole)
                {
                    pwh.AddHole(hole);
                }
            }

            result.Add(pwh);
        }

        // Handle any remaining unassigned holes as separate outers (edge case)
        var assignedHoles = result.SelectMany(r => r.Holes).ToHashSet();
        foreach (var hole in holes)
        {
            if (!assignedHoles.Contains(hole))
            {
                // Treat as a separate outer polygon
                result.Add(new PolygonWithHoles(hole));
            }
        }

        return result;
    }

    /// <summary>
    /// Checks if polygon A is entirely inside polygon B.
    /// </summary>
    private static bool IsPolygonInsidePolygon(VPolygon inner, VPolygon outer)
    {
        // All points of inner must be inside outer
        foreach (var point in inner.Points)
        {
            if (!PolygonClipper.PointInPolygonTest(point, outer.Points))
                return false;
        }
        return true;
    }

    /// <summary>
    /// Ensures polygon has counter-clockwise winding (positive signed area).
    /// </summary>
    private static VPolygon EnsureCounterClockwise(VPolygon polygon)
    {
        if (polygon.SignedArea < 0)
        {
            return ReversePolygon(polygon);
        }
        return polygon;
    }

    /// <summary>
    /// Ensures polygon has clockwise winding (negative signed area).
    /// </summary>
    private static VPolygon EnsureClockwise(VPolygon polygon)
    {
        if (polygon.SignedArea > 0)
        {
            return ReversePolygon(polygon);
        }
        return polygon;
    }

    /// <summary>
    /// Creates a new polygon with reversed vertex order.
    /// </summary>
    private static VPolygon ReversePolygon(VPolygon polygon)
    {
        var reversedPoints = polygon.Points.AsEnumerable().Reverse().ToList();
        return new VPolygon(reversedPoints);
    }

    public override string ToString()
    {
        return $"PolygonWithHoles(Outer: {Outer.Points.Count} pts, Holes: {Holes.Count})";
    }
}
