using System.Collections.Generic;
using System.Linq;

namespace Code2Viz.Geometry;

/// <summary>
/// Represents the result of an intersection operation between curves.
/// Can contain points (for crossing intersections) and/or curves (for overlapping segments).
/// </summary>
public class IntersectionResult
{
    /// <summary>
    /// Points where the curves cross or touch.
    /// </summary>
    public List<VPoint> Points { get; } = new();

    /// <summary>
    /// Curves representing overlapping segments (e.g., when two lines are collinear and overlap).
    /// </summary>
    public List<ICurve> Curves { get; } = new();

    /// <summary>
    /// Returns true if there is at least one intersection (point or curve).
    /// </summary>
    public bool HasIntersection => Points.Count > 0 || Curves.Count > 0;

    /// <summary>
    /// Returns true if the intersection is exactly one point.
    /// </summary>
    public bool IsSinglePoint => Points.Count == 1 && Curves.Count == 0;

    /// <summary>
    /// Returns true if the curves overlap (share a segment, not just touch at points).
    /// </summary>
    public bool HasOverlap => Curves.Count > 0;

    /// <summary>
    /// Total count of intersection elements (points + curves).
    /// </summary>
    public int Count => Points.Count + Curves.Count;

    /// <summary>
    /// Creates an empty intersection result (no intersection).
    /// </summary>
    public static IntersectionResult None => new();

    /// <summary>
    /// Creates an intersection result with a single point.
    /// </summary>
    public static IntersectionResult FromPoint(VPoint point)
    {
        var result = new IntersectionResult();
        result.Points.Add(point);
        return result;
    }

    /// <summary>
    /// Creates an intersection result with multiple points.
    /// </summary>
    public static IntersectionResult FromPoints(IEnumerable<VPoint> points)
    {
        var result = new IntersectionResult();
        result.Points.AddRange(points);
        return result;
    }

    /// <summary>
    /// Creates an intersection result with an overlapping curve.
    /// </summary>
    public static IntersectionResult FromCurve(ICurve curve)
    {
        var result = new IntersectionResult();
        result.Curves.Add(curve);
        return result;
    }

    /// <summary>
    /// Creates an intersection result with multiple overlapping curves.
    /// </summary>
    public static IntersectionResult FromCurves(IEnumerable<ICurve> curves)
    {
        var result = new IntersectionResult();
        result.Curves.AddRange(curves);
        return result;
    }

    /// <summary>
    /// Merges another intersection result into this one.
    /// </summary>
    public void Merge(IntersectionResult other)
    {
        if (other == null) return;
        Points.AddRange(other.Points);
        Curves.AddRange(other.Curves);
    }

    /// <summary>
    /// Removes duplicate points within a tolerance.
    /// </summary>
    public void RemoveDuplicatePoints(double tolerance = 1e-6)
    {
        var unique = new List<VPoint>();
        foreach (var p in Points)
        {
            if (!unique.Any(existing => existing.DistanceTo(p) < tolerance))
            {
                unique.Add(p);
            }
        }
        Points.Clear();
        Points.AddRange(unique);
    }
}
