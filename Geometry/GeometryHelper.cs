using System;

namespace Code2Viz.Geometry;

/// <summary>
/// Helper methods for geometry transformations.
/// </summary>
public static class GeometryHelper
{
    /// <summary>
    /// Rotates a point around a pivot by the given angle.
    /// </summary>
    public static VPoint RotatePoint(VPoint point, VPoint pivot, double angleDegrees)
    {
        double angleRad = angleDegrees * Math.PI / 180.0;
        double cos = Math.Cos(angleRad);
        double sin = Math.Sin(angleRad);

        double dx = point.X - pivot.X;
        double dy = point.Y - pivot.Y;

        double newX = pivot.X + dx * cos - dy * sin;
        double newY = pivot.Y + dx * sin + dy * cos;

        return new VPoint(newX, newY);
    }

    /// <summary>
    /// Reflects a point across a mirror line.
    /// </summary>
    public static VPoint FlipPoint(VPoint point, VLine mirrorLine)
    {
        // Get line direction vector
        double dx = mirrorLine.End.X - mirrorLine.Start.X;
        double dy = mirrorLine.End.Y - mirrorLine.Start.Y;

        // Normalize
        double len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 1e-10) return point; // Degenerate line
        dx /= len;
        dy /= len;

        // Vector from line start to point
        double px = point.X - mirrorLine.Start.X;
        double py = point.Y - mirrorLine.Start.Y;

        // Project point onto line
        double dot = px * dx + py * dy;
        double projX = mirrorLine.Start.X + dot * dx;
        double projY = mirrorLine.Start.Y + dot * dy;

        // Reflect point across projection
        double newX = 2 * projX - point.X;
        double newY = 2 * projY - point.Y;

        return new VPoint(newX, newY);
    }

    /// <summary>
    /// Moves a point by a vector.
    /// </summary>
    public static VPoint MovePoint(VPoint point, VXYZ vector)
    {
        return new VPoint(point.X + vector.X, point.Y + vector.Y);
    }

    /// <summary>
    /// Normalizes an angle to [0, 360) degrees.
    /// </summary>
    public static double NormalizeAngle(double angleDegrees)
    {
        angleDegrees = angleDegrees % 360;
        if (angleDegrees < 0) angleDegrees += 360;
        return angleDegrees;
    }

    /// <summary>
    /// Calculates interaction between two lines. 
    /// Returns VPoint if crossing, VLine if overlapping, or null.
    /// </summary>
    public static Shape? IntersectLineLine(VLine l1, VLine l2)
    {
        double x1 = l1.Start.X, y1 = l1.Start.Y;
        double x2 = l1.End.X, y2 = l1.End.Y;
        double x3 = l2.Start.X, y3 = l2.Start.Y;
        double x4 = l2.End.X, y4 = l2.End.Y;

        double d = (y4 - y3) * (x2 - x1) - (x4 - x3) * (y2 - y1);
        
        // Parallel lines
        if (GeometryTolerance.IsZero(d))
        {
            // Check for collinearity and overlap
            // This is a bit complex for a simple first pass, returning null for parallel lines for now unless requested otherwise
            // If they are collinear, we should return the overlapping segment.
            if (IsCollinear(l1, l2))
            {
                return GetLineOverlap(l1, l2);
            }
            return null;
        }

        double ua = ((x4 - x3) * (y1 - y3) - (y4 - y3) * (x1 - x3)) / d;
        double ub = ((x2 - x1) * (y1 - y3) - (y2 - y1) * (x1 - x3)) / d;

        if (ua >= 0 && ua <= 1 && ub >= 0 && ub <= 1)
        {
            double ix = x1 + ua * (x2 - x1);
            double iy = y1 + ua * (y2 - y1);
            return new VPoint(ix, iy);
        }

        return null;
    }

    private static bool IsCollinear(VLine l1, VLine l2)
    {
        // Check if l2.Start is on line l1 (infinite)
        double a = (l1.End.Y - l1.Start.Y);
        double b = (l1.Start.X - l1.End.X);
        double c = -a * l1.Start.X - b * l1.Start.Y;
        
        return GeometryTolerance.IsZero(a * l2.Start.X + b * l2.Start.Y + c, GeometryTolerance.VisualEpsilon);
    }

    private static Shape? GetLineOverlap(VLine l1, VLine l2)
    {
        // Project all points onto X axis (or Y if vertical) to find 1D overlap
        bool useX = Math.Abs(l1.Start.X - l1.End.X) > Math.Abs(l1.Start.Y - l1.End.Y);
        
        double GetVal(VPoint p) => useX ? p.X : p.Y;
        
        double min1 = Math.Min(GetVal(l1.Start), GetVal(l1.End));
        double max1 = Math.Max(GetVal(l1.Start), GetVal(l1.End));
        double min2 = Math.Min(GetVal(l2.Start), GetVal(l2.End));
        double max2 = Math.Max(GetVal(l2.Start), GetVal(l2.End));

        double overlapMin = Math.Max(min1, min2);
        double overlapMax = Math.Min(max1, max2);

        if (overlapMin > overlapMax) return null;

        // Reconstruct points
        // This assumes lines are exactly collinear which is checked before
        // Need to un-project. Easiest is to pick the points that bound the overlap.
        // Or just return a new line based on the sorted points along the line vector.
        
        // Simplified approach: Sort all 4 points along the line direction. 
        // If the middle 2 form a valid segment inside both lines, return it.
        var points = new[] { l1.Start, l1.End, l2.Start, l2.End }
            .OrderBy(p => useX ? p.X : p.Y)
            .ToList();
            
        // Check if the middle segment belongs to both
        VPoint pStart = points[1];
        VPoint pEnd = points[2];
        
        // Should verify this segment allows for actual overlap 
        // (already checked via overlapMin/Max but good to be safe)
        if (GeometryTolerance.AreEqual(GetVal(pStart), GetVal(pEnd))) return new VPoint(pStart.X, pStart.Y);
        
        return new VLine(pStart.X, pStart.Y, pEnd.X, pEnd.Y);
    }

    /// <summary>
    /// Calculates intersection between two axis-aligned rectangles.
    /// Returns VRectangle (intersection area) or null.
    /// </summary>
    public static Shape? IntersectRectRect(VRectangle r1, VRectangle r2)
    {
        double left = Math.Max(r1.Corner.X, r2.Corner.X);
        double top = Math.Max(r1.Corner.Y, r2.Corner.Y);
        double right = Math.Min(r1.Corner.X + r1.Width, r2.Corner.X + r2.Width);
        double bottom = Math.Min(r1.Corner.Y + r1.Height, r2.Corner.Y + r2.Height);

        if (left < right && top < bottom)
        {
            return new VRectangle(left, top, right - left, bottom - top);
        }

        return null;
    }

    /// <summary>
    /// Calculates intersection between a line and an axis-aligned rectangle.
    /// Returns VLine (segment inside rect) or null.
    /// </summary>
    public static Shape? IntersectLineRect(VLine line, VRectangle rect)
    {
        // Liang-Barsky algorithm
        double t0 = 0, t1 = 1;
        double x1 = line.Start.X, y1 = line.Start.Y;
        double x2 = line.End.X, y2 = line.End.Y;
        double dx = x2 - x1, dy = y2 - y1;
        double p = 0, q = 0, r = 0;

        for (int edge = 0; edge < 4; edge++)
        {
            if (edge == 0) { p = -dx; q = -(rect.Corner.X - x1); }
            if (edge == 1) { p = dx; q = (rect.Corner.X + rect.Width - x1); }
            if (edge == 2) { p = -dy; q = -(rect.Corner.Y - y1); }
            if (edge == 3) { p = dy; q = (rect.Corner.Y + rect.Height - y1); }

            if (GeometryTolerance.IsZero(p) && q < 0) return null; // Parallel and outside

            r = q / p;

            if (p < 0)
            {
                if (r > t1) return null;
                if (r > t0) t0 = r;
            }
            else if (p > 0)
            {
                if (r < t0) return null;
                if (r < t1) t1 = r;
            }
        }

        if (t0 <= t1)
        {
            double newX1 = x1 + t0 * dx;
            double newY1 = y1 + t0 * dy;
            double newX2 = x1 + t1 * dx;
            double newY2 = y1 + t1 * dy;
            
            // If it's a point
            if (GeometryTolerance.AreEqual(t1, t0)) return new VPoint(newX1, newY1);
            
            return new VLine(newX1, newY1, newX2, newY2);
        }

        return null;
    }

    /// <summary>
    /// Gets the normal vector of the polyline segment closest to the given point.
    /// </summary>
    public static VXYZ GetPolylineNormalAtPoint(System.Collections.Generic.List<VPoint> points, VPoint p, bool isClosed)
    {
        if (points == null || points.Count < 2) return new VXYZ(0, 1, 0); // Default Up

        double minSqDist = double.MaxValue;
        VXYZ bestNormal = new VXYZ(0, 1, 0);

        int count = isClosed ? points.Count : points.Count - 1;

        for (int i = 0; i < count; i++)
        {
            VPoint p1 = points[i];
            VPoint p2 = points[(i + 1) % points.Count];

            double dx = p2.X - p1.X;
            double dy = p2.Y - p1.Y;
            double lenSq = dx * dx + dy * dy;

            double currentSqDist;

            if (lenSq < 1e-10)
            {
                currentSqDist = p.DistanceTo(p1);
                currentSqDist *= currentSqDist;
            }
            else
            {
                double t = ((p.X - p1.X) * dx + (p.Y - p1.Y) * dy) / lenSq;
                t = Math.Max(0, Math.Min(1, t));
                double cx = p1.X + t * dx;
                double cy = p1.Y + t * dy;
                double rx = p.X - cx;
                double ry = p.Y - cy;
                currentSqDist = rx * rx + ry * ry;
            }

            if (currentSqDist < minSqDist)
            {
                minSqDist = currentSqDist;
                // Normal is (dy, -dx)
                bestNormal = new VXYZ(dy, -dx, 0);
            }
        }

        return bestNormal.Normalize();
    }
    /// <summary>
    /// Calculates interaction points of two circles.
    /// </summary>
    public static System.Collections.Generic.List<VPoint> IntersectCircleCircle(VPoint c1, double r1, VPoint c2, double r2)
    {
        var results = new System.Collections.Generic.List<VPoint>();
        
        double dx = c2.X - c1.X;
        double dy = c2.Y - c1.Y;
        double d = Math.Sqrt(dx * dx + dy * dy);

        // Circles are separate or contained within each other
        if (GeometryTolerance.IsGreaterThan(d, r1 + r2) ||
            GeometryTolerance.IsLessThan(d, Math.Abs(r1 - r2)) ||
            GeometryTolerance.IsZero(d))
        {
            return results;
        }

        double a = (r1 * r1 - r2 * r2 + d * d) / (2 * d);
        double h = Math.Sqrt(Math.Max(0, r1 * r1 - a * a));

        double x2 = c1.X + a * (c2.X - c1.X) / d;
        double y2 = c1.Y + a * (c2.Y - c1.Y) / d;

        results.Add(new VPoint(
            x2 + h * (c2.Y - c1.Y) / d,
            y2 - h * (c2.X - c1.X) / d
        ));

        // If not tangent (touching at one point)
        if (!GeometryTolerance.AreEqual(d, r1 + r2))
        {
            results.Add(new VPoint(
                x2 - h * (c2.Y - c1.Y) / d,
                y2 + h * (c2.X - c1.X) / d
            ));
        }

        return results;
    }

    /// <summary>
    /// Calculates the smallest difference between two angles in degrees.
    /// Returns value in range [-180, 180].
    /// </summary>
    public static double AngleDifference(double target, double source)
    {
        double diff = (target - source + 180) % 360 - 180;
        return diff < -180 ? diff + 360 : diff;
    }
}

