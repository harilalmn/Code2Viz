using System;

namespace C2VGeometry;

/// <summary>
/// Provides epsilon-based comparison utilities for floating-point geometry operations.
/// Essential for CAD applications where direct equality comparisons fail due to precision errors.
/// </summary>
public static class GeometryTolerance
{
    /// <summary>
    /// Default tolerance for geometric comparisons (1e-9).
    /// Suitable for most CAD operations at typical scales.
    /// </summary>
    public const double Epsilon = 1e-9;

    /// <summary>
    /// Looser tolerance for visual/UI comparisons (1e-6).
    /// Use when pixel-level precision is sufficient.
    /// </summary>
    public const double VisualEpsilon = 1e-6;

    /// <summary>
    /// Angular tolerance in radians (~0.00057 degrees).
    /// </summary>
    public const double AngleEpsilon = 1e-5;

    /// <summary>
    /// Checks if two doubles are equal within epsilon tolerance.
    /// </summary>
    public static bool AreEqual(double a, double b, double epsilon = Epsilon)
        => Math.Abs(a - b) < epsilon;

    /// <summary>
    /// Checks if a value is effectively zero within epsilon tolerance.
    /// </summary>
    public static bool IsZero(double value, double epsilon = Epsilon)
        => Math.Abs(value) < epsilon;

    /// <summary>
    /// Checks if a &lt; b with epsilon consideration (a is strictly less than b).
    /// </summary>
    public static bool IsLessThan(double a, double b, double epsilon = Epsilon)
        => a < b - epsilon;

    /// <summary>
    /// Checks if a &gt; b with epsilon consideration (a is strictly greater than b).
    /// </summary>
    public static bool IsGreaterThan(double a, double b, double epsilon = Epsilon)
        => a > b + epsilon;

    /// <summary>
    /// Checks if a &lt;= b with epsilon consideration.
    /// </summary>
    public static bool IsLessOrEqual(double a, double b, double epsilon = Epsilon)
        => a < b + epsilon;

    /// <summary>
    /// Checks if a &gt;= b with epsilon consideration.
    /// </summary>
    public static bool IsGreaterOrEqual(double a, double b, double epsilon = Epsilon)
        => a > b - epsilon;

    /// <summary>
    /// Checks if a value is within a range [min, max] with epsilon tolerance.
    /// </summary>
    public static bool IsInRange(double value, double min, double max, double epsilon = Epsilon)
        => IsGreaterOrEqual(value, min, epsilon) && IsLessOrEqual(value, max, epsilon);

    /// <summary>
    /// Checks if two 2D points are coincident within tolerance.
    /// </summary>
    public static bool PointsAreEqual(double x1, double y1, double x2, double y2, double epsilon = Epsilon)
        => AreEqual(x1, x2, epsilon) && AreEqual(y1, y2, epsilon);

    /// <summary>
    /// Checks if two VXYZ objects are coincident within tolerance.
    /// </summary>
    public static bool PointsAreEqual(VXYZ p1, VXYZ p2, double epsilon = Epsilon)
        => PointsAreEqual(p1.X, p1.Y, p2.X, p2.Y, epsilon);

    /// <summary>
    /// Checks if two 3D points/vectors are equal within tolerance.
    /// </summary>
    public static bool VectorsAreEqual(VXYZ v1, VXYZ v2, double epsilon = Epsilon)
        => AreEqual(v1.X, v2.X, epsilon) &&
           AreEqual(v1.Y, v2.Y, epsilon) &&
           AreEqual(v1.Z, v2.Z, epsilon);

    /// <summary>
    /// Checks if two angles (in radians) are equal, accounting for 2*PI wraparound.
    /// </summary>
    public static bool AnglesAreEqual(double angle1, double angle2, double epsilon = AngleEpsilon)
    {
        // Normalize both angles to [0, 2*PI)
        double normalized1 = NormalizeAngle(angle1);
        double normalized2 = NormalizeAngle(angle2);

        double diff = Math.Abs(normalized1 - normalized2);

        // Check direct difference or wraparound at 2*PI boundary
        return diff < epsilon || Math.Abs(diff - 2 * Math.PI) < epsilon;
    }

    /// <summary>
    /// Normalizes an angle to the range [0, 2*PI).
    /// </summary>
    public static double NormalizeAngle(double angle)
    {
        const double TwoPi = 2 * Math.PI;
        double result = angle % TwoPi;
        return result < 0 ? result + TwoPi : result;
    }

    /// <summary>
    /// Normalizes an angle in degrees to the range [0, 360).
    /// </summary>
    public static double NormalizeAngleDegrees(double degrees)
    {
        double result = degrees % 360.0;
        return result < 0 ? result + 360.0 : result;
    }

    /// <summary>
    /// Clamps a value to [0, 1] range, useful for parametric curve calculations.
    /// </summary>
    public static double ClampParametric(double t)
        => Math.Max(0.0, Math.Min(1.0, t));

    /// <summary>
    /// Clamps a value to a specified range.
    /// </summary>
    public static double Clamp(double value, double min, double max)
        => Math.Max(min, Math.Min(max, value));

    /// <summary>
    /// Returns the sign of a value with epsilon consideration.
    /// Returns 0 if value is within epsilon of zero.
    /// </summary>
    public static int Sign(double value, double epsilon = Epsilon)
    {
        if (value > epsilon) return 1;
        if (value < -epsilon) return -1;
        return 0;
    }

    /// <summary>
    /// Computes the squared distance between two 2D points.
    /// Use this instead of Distance when only comparing relative distances (avoids sqrt).
    /// </summary>
    public static double DistanceSquared(double x1, double y1, double x2, double y2)
    {
        double dx = x2 - x1;
        double dy = y2 - y1;
        return dx * dx + dy * dy;
    }

    /// <summary>
    /// Computes the squared distance between two VXYZ points.
    /// </summary>
    public static double DistanceSquared(VXYZ p1, VXYZ p2)
        => DistanceSquared(p1.X, p1.Y, p2.X, p2.Y);

    /// <summary>
    /// Computes the distance between two 2D points.
    /// </summary>
    public static double Distance(double x1, double y1, double x2, double y2)
        => Math.Sqrt(DistanceSquared(x1, y1, x2, y2));

    /// <summary>
    /// Computes the distance between two VXYZ points.
    /// </summary>
    public static double Distance(VXYZ p1, VXYZ p2)
        => Math.Sqrt(DistanceSquared(p1, p2));

    /// <summary>
    /// Checks if a point lies on a line segment within tolerance.
    /// Uses perpendicular distance and parametric bounds checking.
    /// </summary>
    public static bool PointOnSegment(VXYZ point, VXYZ lineStart, VXYZ lineEnd, double epsilon = Epsilon)
    {
        double dx = lineEnd.X - lineStart.X;
        double dy = lineEnd.Y - lineStart.Y;
        double lengthSq = dx * dx + dy * dy;

        if (IsZero(lengthSq, epsilon * epsilon))
        {
            // Degenerate line (start == end), check point coincidence
            return PointsAreEqual(point, lineStart, epsilon);
        }

        // Compute parameter t for projection onto line
        double t = ((point.X - lineStart.X) * dx + (point.Y - lineStart.Y) * dy) / lengthSq;

        // Check if projection is within segment bounds
        if (t < -epsilon || t > 1.0 + epsilon)
            return false;

        // Compute closest point on segment
        double closestX = lineStart.X + t * dx;
        double closestY = lineStart.Y + t * dy;

        // Check distance from point to closest point
        return DistanceSquared(point.X, point.Y, closestX, closestY) < epsilon * epsilon;
    }

    /// <summary>
    /// Computes the perpendicular distance from a point to an infinite line.
    /// </summary>
    public static double PointToLineDistance(VXYZ point, VXYZ lineStart, VXYZ lineEnd)
    {
        double dx = lineEnd.X - lineStart.X;
        double dy = lineEnd.Y - lineStart.Y;
        double lengthSq = dx * dx + dy * dy;

        if (IsZero(lengthSq))
            return Distance(point, lineStart);

        // Area of parallelogram = |cross product|, divide by base length
        double cross = Math.Abs((point.X - lineStart.X) * dy - (point.Y - lineStart.Y) * dx);
        return cross / Math.Sqrt(lengthSq);
    }

    /// <summary>
    /// Determines the orientation of three points (counterclockwise, clockwise, or collinear).
    /// Returns: positive = CCW, negative = CW, zero = collinear
    /// </summary>
    public static double Orientation(VXYZ p1, VXYZ p2, VXYZ p3)
    {
        return (p2.X - p1.X) * (p3.Y - p1.Y) - (p2.Y - p1.Y) * (p3.X - p1.X);
    }

    /// <summary>
    /// Checks if three points are collinear within tolerance.
    /// </summary>
    public static bool AreCollinear(VXYZ p1, VXYZ p2, VXYZ p3, double epsilon = Epsilon)
    {
        return IsZero(Orientation(p1, p2, p3), epsilon);
    }
}
