using System;
using System.Collections.Generic;
using System.Linq;

namespace Code2Viz.Geometry;

/// <summary>
/// Utility class for computing intersections between curves.
/// </summary>
public static class CurveIntersection
{
    private const double Tolerance = GeometryTolerance.Epsilon;

    #region Main Intersection Method

    /// <summary>
    /// Computes the intersection between two curves.
    /// </summary>
    public static IntersectionResult Intersect(ICurve curve1, ICurve curve2)
    {
        // Handle specific type combinations for better accuracy
        return (curve1, curve2) switch
        {
            (VLine l1, VLine l2) => IntersectLineLine(l1, l2),
            (VLine l, VCircle c) => IntersectLineCircle(l, c),
            (VCircle c, VLine l) => IntersectLineCircle(l, c),
            (VLine l, VArc a) => IntersectLineArc(l, a),
            (VArc a, VLine l) => IntersectLineArc(l, a),
            (VLine l, VEllipse e) => IntersectLineEllipse(l, e),
            (VEllipse e, VLine l) => IntersectLineEllipse(l, e),
            (VCircle c1, VCircle c2) => IntersectCircleCircle(c1, c2),
            (VCircle c, VArc a) => IntersectCircleArc(c, a),
            (VArc a, VCircle c) => IntersectCircleArc(c, a),
            (VArc a1, VArc a2) => IntersectArcArc(a1, a2),
            // For polylines and other complex curves, decompose into segments
            _ => IntersectGeneric(curve1, curve2)
        };
    }

    #endregion

    #region Line-Line Intersection

    public static IntersectionResult IntersectLineLine(VLine line1, VLine line2)
    {
        var p1 = line1.StartPoint;
        var p2 = line1.EndPoint;
        var p3 = line2.StartPoint;
        var p4 = line2.EndPoint;

        double d1x = p2.X - p1.X;
        double d1y = p2.Y - p1.Y;
        double d2x = p4.X - p3.X;
        double d2y = p4.Y - p3.Y;

        double cross = d1x * d2y - d1y * d2x;

        // Check for collinear/parallel lines
        if (Math.Abs(cross) < Tolerance)
        {
            // Lines are parallel - check if collinear and overlapping
            return CheckCollinearOverlap(line1, line2);
        }

        // Lines intersect at a point
        double t = ((p3.X - p1.X) * d2y - (p3.Y - p1.Y) * d2x) / cross;
        double u = ((p3.X - p1.X) * d1y - (p3.Y - p1.Y) * d1x) / cross;

        // Check if intersection is within both line segments
        if (t >= -Tolerance && t <= 1 + Tolerance && u >= -Tolerance && u <= 1 + Tolerance)
        {
            var point = VPoint.Internal(p1.X + t * d1x, p1.Y + t * d1y);
            return IntersectionResult.FromPoint(point);
        }

        return IntersectionResult.None;
    }

    private static IntersectionResult CheckCollinearOverlap(VLine line1, VLine line2)
    {
        // Check if lines are collinear
        var p1 = line1.StartPoint;
        var p2 = line1.EndPoint;
        var p3 = line2.StartPoint;

        double d1x = p2.X - p1.X;
        double d1y = p2.Y - p1.Y;

        // Check if p3 is on the line through p1-p2
        double crossP3 = (p3.X - p1.X) * d1y - (p3.Y - p1.Y) * d1x;
        if (Math.Abs(crossP3) > Tolerance * Math.Max(1, Math.Sqrt(d1x * d1x + d1y * d1y)))
        {
            return IntersectionResult.None; // Parallel but not collinear
        }

        // Project all points onto line1's direction
        double len1 = Math.Sqrt(d1x * d1x + d1y * d1y);
        if (len1 < Tolerance) return IntersectionResult.None;

        double t1 = 0;
        double t2 = 1;
        double t3 = ((line2.StartPoint.X - p1.X) * d1x + (line2.StartPoint.Y - p1.Y) * d1y) / (len1 * len1);
        double t4 = ((line2.EndPoint.X - p1.X) * d1x + (line2.EndPoint.Y - p1.Y) * d1y) / (len1 * len1);

        if (t3 > t4) (t3, t4) = (t4, t3);

        double overlapStart = Math.Max(t1, t3);
        double overlapEnd = Math.Min(t2, t4);

        if (overlapStart > overlapEnd + Tolerance)
        {
            return IntersectionResult.None; // No overlap
        }

        if (Math.Abs(overlapStart - overlapEnd) < Tolerance)
        {
            // Single point overlap (lines touch at endpoint)
            var point = VPoint.Internal(p1.X + overlapStart * d1x, p1.Y + overlapStart * d1y);
            return IntersectionResult.FromPoint(point);
        }

        // Overlapping segment
        var startPt = VPoint.Internal(p1.X + overlapStart * d1x, p1.Y + overlapStart * d1y);
        var endPt = VPoint.Internal(p1.X + overlapEnd * d1x, p1.Y + overlapEnd * d1y);

        // Check for antiparallel lines (dot product < 0)
        // If antiparallel, they are touching (boundaries meet) but not overlapping in the polygon sense
        double d2x = line2.EndPoint.X - line2.StartPoint.X;
        double d2y = line2.EndPoint.Y - line2.StartPoint.Y;

        if (d1x * d2x + d1y * d2y < -Tolerance)
        {
            // Touching (antiparallel)
            var result = new IntersectionResult();
            result.Points.Add(startPt);
            if (startPt.DistanceTo(endPt) > Tolerance)
            {
                result.Points.Add(endPt);
            }
            return result;
        }

        return IntersectionResult.FromCurve(new VLine(startPt, endPt));
    }

    #endregion

    #region Line-Circle Intersection

    public static IntersectionResult IntersectLineCircle(VLine line, VCircle circle)
    {
        var p1 = line.StartPoint;
        var p2 = line.EndPoint;
        var center = circle.Center;
        double radius = circle.Radius;

        double dx = p2.X - p1.X;
        double dy = p2.Y - p1.Y;
        double fx = p1.X - center.X;
        double fy = p1.Y - center.Y;

        double a = dx * dx + dy * dy;
        double b = 2 * (fx * dx + fy * dy);
        double c = fx * fx + fy * fy - radius * radius;

        double discriminant = b * b - 4 * a * c;

        if (discriminant < -Tolerance)
        {
            return IntersectionResult.None;
        }

        var result = new IntersectionResult();

        if (Math.Abs(discriminant) < Tolerance)
        {
            // Tangent - one intersection point
            double t = -b / (2 * a);
            if (t >= -Tolerance && t <= 1 + Tolerance)
            {
                result.Points.Add(VPoint.Internal(p1.X + t * dx, p1.Y + t * dy));
            }
        }
        else
        {
            // Two intersection points
            double sqrtDisc = Math.Sqrt(discriminant);
            double t1 = (-b - sqrtDisc) / (2 * a);
            double t2 = (-b + sqrtDisc) / (2 * a);

            if (t1 >= -Tolerance && t1 <= 1 + Tolerance)
            {
                result.Points.Add(VPoint.Internal(p1.X + t1 * dx, p1.Y + t1 * dy));
            }
            if (t2 >= -Tolerance && t2 <= 1 + Tolerance)
            {
                result.Points.Add(VPoint.Internal(p1.X + t2 * dx, p1.Y + t2 * dy));
            }
        }

        return result;
    }

    #endregion

    #region Line-Arc Intersection

    public static IntersectionResult IntersectLineArc(VLine line, VArc arc)
    {
        // First find line-circle intersections
        var tempCircle = new VCircle(arc.Center, arc.Radius);
        var circleResult = IntersectLineCircle(line, tempCircle);

        if (!circleResult.HasIntersection)
        {
            return IntersectionResult.None;
        }

        // Filter points that are on the arc
        var result = new IntersectionResult();
        foreach (var point in circleResult.Points)
        {
            if (IsPointOnArc(point, arc))
            {
                result.Points.Add(point);
            }
        }

        return result;
    }

    private static bool IsPointOnArc(VPoint point, VArc arc)
    {
        double angle = Math.Atan2(point.Y - arc.Center.Y, point.X - arc.Center.X);

        // Normalize angles to [0, 2π]
        double startAngle = NormalizeAngle(arc.StartAngle);
        double endAngle = NormalizeAngle(arc.EndAngle);
        angle = NormalizeAngle(angle);

        if (startAngle <= endAngle)
        {
            return angle >= startAngle - Tolerance && angle <= endAngle + Tolerance;
        }
        else
        {
            // Arc crosses 0/2π
            return angle >= startAngle - Tolerance || angle <= endAngle + Tolerance;
        }
    }

    private static double NormalizeAngle(double angle)
    {
        while (angle < 0) angle += 2 * Math.PI;
        while (angle >= 2 * Math.PI) angle -= 2 * Math.PI;
        return angle;
    }

    #endregion

    #region Line-Ellipse Intersection

    public static IntersectionResult IntersectLineEllipse(VLine line, VEllipse ellipse)
    {
        // Transform to ellipse-centered coordinates
        var p1 = line.StartPoint;
        var p2 = line.EndPoint;
        var center = ellipse.Center;
        double a = ellipse.RadiusX;
        double b = ellipse.RadiusY;

        double dx = p2.X - p1.X;
        double dy = p2.Y - p1.Y;
        double fx = p1.X - center.X;
        double fy = p1.Y - center.Y;

        // Ellipse equation: (x/a)^2 + (y/b)^2 = 1
        // Line: p = p1 + t*(p2-p1)
        // Substitute and solve quadratic
        double A = (dx * dx) / (a * a) + (dy * dy) / (b * b);
        double B = 2 * ((fx * dx) / (a * a) + (fy * dy) / (b * b));
        double C = (fx * fx) / (a * a) + (fy * fy) / (b * b) - 1;

        double discriminant = B * B - 4 * A * C;

        if (discriminant < -Tolerance)
        {
            return IntersectionResult.None;
        }

        var result = new IntersectionResult();

        if (Math.Abs(discriminant) < Tolerance)
        {
            double t = -B / (2 * A);
            if (t >= -Tolerance && t <= 1 + Tolerance)
            {
                result.Points.Add(VPoint.Internal(p1.X + t * dx, p1.Y + t * dy));
            }
        }
        else
        {
            double sqrtDisc = Math.Sqrt(discriminant);
            double t1 = (-B - sqrtDisc) / (2 * A);
            double t2 = (-B + sqrtDisc) / (2 * A);

            if (t1 >= -Tolerance && t1 <= 1 + Tolerance)
            {
                result.Points.Add(VPoint.Internal(p1.X + t1 * dx, p1.Y + t1 * dy));
            }
            if (t2 >= -Tolerance && t2 <= 1 + Tolerance)
            {
                result.Points.Add(VPoint.Internal(p1.X + t2 * dx, p1.Y + t2 * dy));
            }
        }

        return result;
    }

    #endregion

    #region Circle-Circle Intersection

    public static IntersectionResult IntersectCircleCircle(VCircle c1, VCircle c2)
    {
        double d = c1.Center.DistanceTo(c2.Center);
        double r1 = c1.Radius;
        double r2 = c2.Radius;

        // Check for no intersection cases
        if (d > r1 + r2 + Tolerance || d < Math.Abs(r1 - r2) - Tolerance)
        {
            return IntersectionResult.None;
        }

        // Check for coincident circles
        if (d < Tolerance && Math.Abs(r1 - r2) < Tolerance)
        {
            // Circles are the same - return the circle as overlap
            return IntersectionResult.FromCurve(c1);
        }

        // Calculate intersection points
        double a = (r1 * r1 - r2 * r2 + d * d) / (2 * d);
        double h2 = r1 * r1 - a * a;

        if (h2 < -Tolerance)
        {
            return IntersectionResult.None;
        }

        double h = h2 > 0 ? Math.Sqrt(h2) : 0;

        // Point P is on line between centers at distance a from c1.Center
        double px = c1.Center.X + a * (c2.Center.X - c1.Center.X) / d;
        double py = c1.Center.Y + a * (c2.Center.Y - c1.Center.Y) / d;

        var result = new IntersectionResult();

        if (h < Tolerance)
        {
            // Circles are tangent
            result.Points.Add(VPoint.Internal(px, py));
        }
        else
        {
            // Two intersection points
            double offsetX = h * (c2.Center.Y - c1.Center.Y) / d;
            double offsetY = h * (c2.Center.X - c1.Center.X) / d;

            result.Points.Add(VPoint.Internal(px + offsetX, py - offsetY));
            result.Points.Add(VPoint.Internal(px - offsetX, py + offsetY));
        }

        return result;
    }

    #endregion

    #region Circle-Arc Intersection

    public static IntersectionResult IntersectCircleArc(VCircle circle, VArc arc)
    {
        var tempCircle = new VCircle(arc.Center, arc.Radius);
        var circleResult = IntersectCircleCircle(circle, tempCircle);

        if (!circleResult.HasIntersection)
        {
            return IntersectionResult.None;
        }

        var result = new IntersectionResult();
        foreach (var point in circleResult.Points)
        {
            if (IsPointOnArc(point, arc))
            {
                result.Points.Add(point);
            }
        }

        return result;
    }

    #endregion

    #region Arc-Arc Intersection

    public static IntersectionResult IntersectArcArc(VArc arc1, VArc arc2)
    {
        var tempCircle1 = new VCircle(arc1.Center, arc1.Radius);
        var tempCircle2 = new VCircle(arc2.Center, arc2.Radius);
        var circleResult = IntersectCircleCircle(tempCircle1, tempCircle2);

        if (!circleResult.HasIntersection)
        {
            return IntersectionResult.None;
        }

        var result = new IntersectionResult();
        foreach (var point in circleResult.Points)
        {
            if (IsPointOnArc(point, arc1) && IsPointOnArc(point, arc2))
            {
                result.Points.Add(point);
            }
        }

        return result;
    }

    #endregion

    #region Generic Intersection (Segment-based)

    /// <summary>
    /// Generic intersection using segment decomposition.
    /// Works for any curve by approximating with line segments.
    /// </summary>
    public static IntersectionResult IntersectGeneric(ICurve curve1, ICurve curve2)
    {
        var segments1 = GetSegments(curve1);
        var segments2 = GetSegments(curve2);

        var result = new IntersectionResult();

        foreach (var seg1 in segments1)
        {
            foreach (var seg2 in segments2)
            {
                var segResult = IntersectLineLine(seg1, seg2);
                result.Merge(segResult);
            }
        }

        result.RemoveDuplicatePoints();
        return result;
    }

    /// <summary>
    /// Gets line segments approximating the curve.
    /// </summary>
    public static List<VLine> GetSegments(ICurve curve, int segmentsPerUnit = 10)
    {
        var segments = new List<VLine>();

        if (curve is VLine line)
        {
            segments.Add(line);
            return segments;
        }

        // Newly synthesised segments are data for computation, not user-drawn
        // shapes — use VLine.Internal so they don't auto-register on the
        // canvas. Previously a single VPolygon self-intersection check could
        // dump tens of thousands of phantom shapes.
        if (curve is VPolygon poly)
        {
            for (int i = 0; i < poly.Points.Count; i++)
            {
                segments.Add(VLine.Internal(poly.Points[i], poly.Points[(i + 1) % poly.Points.Count]));
            }
            return segments;
        }

        if (curve is VPolyline polyline)
        {
            for (int i = 0; i < polyline.Points.Count - 1; i++)
            {
                segments.Add(VLine.Internal(polyline.Points[i], polyline.Points[i + 1]));
            }
            return segments;
        }

        // For other curves, divide into segments
        double length = curve.GetLength();
        int numSegments = Math.Max(2, (int)(length * segmentsPerUnit));
        numSegments = Math.Min(numSegments, 1000); // Cap at 1000 segments

        var points = curve.Divide(numSegments);
        for (int i = 0; i < points.Count - 1; i++)
        {
            segments.Add(VLine.Internal(points[i], points[i + 1]));
        }

        return segments;
    }

    #endregion

    #region Self-Intersection Detection

    /// <summary>
    /// Checks if a curve is self-intersecting.
    /// </summary>
    public static bool IsSelfIntersecting(ICurve curve)
    {
        return curve switch
        {
            VLine => false,
            VCircle => false,
            VArc => false,
            VEllipse => false,
            VRectangle => false,
            VPolyline polyline => IsPolylineSelfIntersecting(polyline.Points),
            VPolygon polygon => IsPolygonSelfIntersecting(polygon),
            VBezier bezier => IsBezierSelfIntersecting(bezier),
            VSpline spline => IsSplineSelfIntersecting(spline),
            _ => false
        };
    }

    /// <summary>
    /// Checks if a polyline (given as points) is self-intersecting.
    /// Uses raw-double segment math: no <see cref="VLine"/> allocations and
    /// no canvas pollution. For a polyline of N vertices this is O(N²) work
    /// but allocation-free — important because <see cref="VLine"/> auto-
    /// registers on construction, which would otherwise dump N²/2 phantom
    /// shapes onto the canvas (e.g. constructing a 360-vertex VPolygon used
    /// to leak ~65k VLines).
    /// </summary>
    public static bool IsPolylineSelfIntersecting(List<VPoint> points)
    {
        int n = points.Count;
        if (n < 4) return false;

        // When the polyline closes on itself (first vertex coincides with the
        // last), segments (0..1) and (n-2..n-1) are adjacent at the closure
        // and must not flag — preserves the original wrap-around exemption.
        bool closedLoop = points[0].DistanceTo(points[n - 1]) < Tolerance;

        for (int i = 0; i < n - 1; i++)
        {
            double ax = points[i].X, ay = points[i].Y;
            double bx = points[i + 1].X, by = points[i + 1].Y;

            for (int j = i + 2; j < n - 1; j++)
            {
                if (closedLoop && i == 0 && j == n - 2) continue;

                double cx = points[j].X, cy = points[j].Y;
                double dx = points[j + 1].X, dy = points[j + 1].Y;

                if (SegmentsIntersectRaw(ax, ay, bx, by, cx, cy, dx, dy))
                    return true;
            }
        }

        return false;
    }

    // Boolean segment-vs-segment test on raw coordinates. Mirrors the
    // semantics of IntersectLineLine + CheckCollinearOverlap (proper
    // crossing, T-junction touch, or collinear overlap all count as a hit)
    // but returns just true/false and allocates nothing.
    private static bool SegmentsIntersectRaw(
        double ax, double ay, double bx, double by,
        double cx, double cy, double dx, double dy)
    {
        double abx = bx - ax, aby = by - ay;
        double cdx = dx - cx, cdy = dy - cy;
        double cross = abx * cdy - aby * cdx;

        if (Math.Abs(cross) >= Tolerance)
        {
            double acx = cx - ax, acy = cy - ay;
            double t = (acx * cdy - acy * cdx) / cross;
            double u = (acx * aby - acy * abx) / cross;
            return t >= -Tolerance && t <= 1 + Tolerance &&
                   u >= -Tolerance && u <= 1 + Tolerance;
        }

        // Parallel: check collinearity (cross of AB with AC near zero,
        // scaled by AB length to match CheckCollinearOverlap).
        double abLen = Math.Sqrt(abx * abx + aby * aby);
        if (abLen < Tolerance) return false;
        double crossAC = abx * (cy - ay) - aby * (cx - ax);
        if (Math.Abs(crossAC) > Tolerance * Math.Max(1, abLen)) return false;

        // Collinear — project C and D onto AB and check overlap with [0, 1].
        double lenSq = abx * abx + aby * aby;
        double tC = ((cx - ax) * abx + (cy - ay) * aby) / lenSq;
        double tD = ((dx - ax) * abx + (dy - ay) * aby) / lenSq;
        if (tC > tD) (tC, tD) = (tD, tC);
        return tD >= -Tolerance && tC <= 1 + Tolerance;
    }

    private static bool IsPolygonSelfIntersecting(VPolygon polygon)
    {
        // Flatten all curves into raw (sx, sy, ex, ey) segment tuples —
        // no VLine allocation, no canvas pollution. For polygons whose
        // curves include arcs/circles/ellipses, each non-line curve is
        // tessellated via Divide() and added as a run of line segments.
        var segs = new List<(double sx, double sy, double ex, double ey)>();
        for (int c = 0; c < polygon.Curves.Count; c++)
        {
            AppendRawSegments(polygon.Curves[c], segs);
        }

        int n = segs.Count;
        for (int i = 0; i < n; i++)
        {
            var a = segs[i];
            for (int j = i + 2; j < n; j++)
            {
                // Closure adjacency: last segment touches the first at the
                // polygon's wrap-around vertex — that's not a self-intersection.
                if (i == 0 && j == n - 1) continue;

                var b = segs[j];
                if (!SegmentsIntersectRaw(a.sx, a.sy, a.ex, a.ey, b.sx, b.sy, b.ex, b.ey))
                    continue;

                // Preserve the original "endpoint touch only" exemption: if two
                // non-adjacent segments meet only at a knot vertex (their shared
                // endpoint is the only contact point), don't flag it.
                if (SharedEndpointTouchOnly(a.sx, a.sy, a.ex, a.ey, b.sx, b.sy, b.ex, b.ey))
                    continue;

                return true;
            }
        }
        return false;
    }

    private static void AppendRawSegments(ICurve curve,
        List<(double sx, double sy, double ex, double ey)> segs)
    {
        switch (curve)
        {
            case VLine line:
                segs.Add((line.Start.X, line.Start.Y, line.End.X, line.End.Y));
                return;
            case VPolygon poly:
            {
                var pts = poly.Points;
                int m = pts.Count;
                for (int i = 0; i < m; i++)
                {
                    int j = i + 1 == m ? 0 : i + 1;
                    segs.Add((pts[i].X, pts[i].Y, pts[j].X, pts[j].Y));
                }
                return;
            }
            case VPolyline polyline:
            {
                var pts = polyline.Points;
                for (int i = 0; i < pts.Count - 1; i++)
                    segs.Add((pts[i].X, pts[i].Y, pts[i + 1].X, pts[i + 1].Y));
                return;
            }
            default:
            {
                // Match GetSegments tessellation policy (10 segs/unit, cap 1000).
                double length = curve.GetLength();
                int numSegments = Math.Max(2, (int)(length * 10));
                numSegments = Math.Min(numSegments, 1000);
                var pts = curve.Divide(numSegments);
                for (int i = 0; i < pts.Count - 1; i++)
                    segs.Add((pts[i].X, pts[i].Y, pts[i + 1].X, pts[i + 1].Y));
                return;
            }
        }
    }

    // Two non-adjacent segments may share a vertex (a "knot" in a degenerate
    // polygon). Treat that as a touch, not a self-intersection — matches the
    // original IsOnlyAtSharedEndpoints check. Returns true when the segments
    // share exactly one endpoint and don't cross or overlap elsewhere.
    private static bool SharedEndpointTouchOnly(
        double ax, double ay, double bx, double by,
        double cx, double cy, double dx, double dy)
    {
        bool ac = NearEq(ax, ay, cx, cy);
        bool ad = NearEq(ax, ay, dx, dy);
        bool bc = NearEq(bx, by, cx, cy);
        bool bd = NearEq(bx, by, dx, dy);
        if (!(ac || ad || bc || bd)) return false;

        // If collinear and overlapping past the shared endpoint, it's not "just a touch".
        double abx = bx - ax, aby = by - ay;
        double cdx = dx - cx, cdy = dy - cy;
        double cross = abx * cdy - aby * cdx;
        if (Math.Abs(cross) < Tolerance)
        {
            // Parallel — check if collinear and overlapping beyond just the shared point.
            double abLen = Math.Sqrt(abx * abx + aby * aby);
            if (abLen < Tolerance) return true;
            double crossAC = abx * (cy - ay) - aby * (cx - ax);
            if (Math.Abs(crossAC) > Tolerance * Math.Max(1, abLen)) return true; // parallel, not collinear → no overlap
            // Collinear — project both endpoints of CD onto AB. Shared endpoint
            // contributes one of {0, 1}; if the other projects strictly inside
            // (0, 1), the segments overlap → not just a touch.
            double lenSq = abx * abx + aby * aby;
            double tC = ((cx - ax) * abx + (cy - ay) * aby) / lenSq;
            double tD = ((dx - ax) * abx + (dy - ay) * aby) / lenSq;
            bool cInside = tC > Tolerance && tC < 1 - Tolerance;
            bool dInside = tD > Tolerance && tD < 1 - Tolerance;
            return !(cInside || dInside);
        }

        // Not parallel: lines cross at one point. If that point is the shared
        // endpoint, the contact is endpoint-only — count as touch.
        // Solve for parameters and check.
        double acx = cx - ax, acy = cy - ay;
        double t = (acx * cdy - acy * cdx) / cross;
        double u = (acx * aby - acy * abx) / cross;
        // The crossing is at the shared endpoint iff t is 0 or 1 (within tol)
        // AND u is 0 or 1 (within tol).
        bool tAtEnd = t < Tolerance || t > 1 - Tolerance;
        bool uAtEnd = u < Tolerance || u > 1 - Tolerance;
        return tAtEnd && uAtEnd;
    }

    private static bool NearEq(double ax, double ay, double bx, double by)
    {
        double dx = ax - bx, dy = ay - by;
        return dx * dx + dy * dy < Tolerance * Tolerance;
    }

    private static bool IsBezierSelfIntersecting(VBezier bezier)
    {
        // Sample the bezier curve and check segments
        var points = bezier.Divide(50);
        return IsPolylineSelfIntersecting(points);
    }

    private static bool IsSplineSelfIntersecting(VSpline spline)
    {
        // Sample the spline curve and check segments
        var points = spline.Divide(100);
        return IsPolylineSelfIntersecting(points);
    }

    private static bool IsOnlyAtSharedEndpoints(IntersectionResult result, VLine seg1, VLine seg2)
    {
        if (!result.IsSinglePoint) return false;

        var pt = result.Points[0];
        bool atSeg1End = pt.DistanceTo(seg1.StartPoint) < Tolerance || pt.DistanceTo(seg1.EndPoint) < Tolerance;
        bool atSeg2End = pt.DistanceTo(seg2.StartPoint) < Tolerance || pt.DistanceTo(seg2.EndPoint) < Tolerance;

        return atSeg1End && atSeg2End;
    }

    #endregion
}
