using System.Collections.Generic;

namespace Code2Viz.Geometry;

public interface ICurve : IDrawable
{
    /// <summary>
    /// Gets the start point of the curve.
    /// </summary>
    VPoint StartPoint { get; }

    /// <summary>
    /// Gets the end point of the curve.
    /// For closed curves, this returns the same point as StartPoint.
    /// </summary>
    VPoint EndPoint { get; }

    /// <summary>
    /// Indicates whether the curve intersects itself.
    /// Simple curves (Line, Circle, Arc, Ellipse) are never self-intersecting.
    /// Complex curves (Polyline, Polygon, Bezier, Spline) may be self-intersecting.
    /// </summary>
    bool SelfIntersecting { get; }

    /// <summary>
    /// Divides the curve into the specified number of segments.
    /// </summary>
    /// <returns>A list of points including the start and end points.</returns>
    List<VPoint> Divide(int numberOfSegments);

    /// <summary>
    /// Measures points along the curve at fixed intervals.
    /// </summary>
    /// <returns>A list of points separated by the specified length.</returns>
    List<VPoint> Measure(double segmentLength);

    /// <summary>
    /// Gets the total length of the curve.
    /// </summary>
    double GetLength();

    /// <summary>
    /// Projects a point onto the curve.
    /// </summary>
    VPoint Project(VPoint point);

    /// <summary>
    /// Returns a point at a given distance along the curve from the start.
    /// </summary>
    VPoint PointAtSegmentLength(double segmentLength);

    /// <summary>
    /// Creates an offset curve at the specified distance.
    /// </summary>
    ICurve Offset(double distance);

    /// <summary>
    /// Creates multiple offset curves at the specified distances.
    /// </summary>
    List<ICurve> Offset(List<double> distances);

    /// <summary>
    /// Finds points on the curve that are at a specific chord length from a given point.
    /// If the point is not on the curve, it is projected first.
    /// </summary>
    List<VPoint> PointsAtChordLengthFromPoint(VPoint point, double chordLength);

    /// <summary>
    /// Splits the curve at the specified point.
    /// Returns a tuple of two segments.
    /// </summary>
    (ICurve, ICurve) SplitAtPoint(VPoint point);

    /// <summary>
    /// Calculates the normal vector at a specific point on the curve.
    /// </summary>
    VXYZ NormalAtPoint(VPoint p);

    /// <summary>
    /// Computes the intersection between this curve and another curve.
    /// Returns an IntersectionResult containing points and/or overlapping curves.
    /// </summary>
    IntersectionResult Intersect(ICurve other);

    /// <summary>
    /// Returns a point on the curve at the given normalized parameter.
    /// </summary>
    /// <param name="parameter">A value from 0 to 1, where 0 is the start and 1 is the end of the curve.</param>
    /// <returns>The point on the curve at the specified parameter.</returns>
    VPoint PointAtParameter(double parameter);
}
