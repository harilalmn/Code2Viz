namespace Code2Viz.Geometry;

/// <summary>
/// Provides array and pattern generation operations for shapes.
/// </summary>
public static class ArrayOps
{
    /// <summary>
    /// Creates a linear array of shapes along a direction.
    /// </summary>
    /// <param name="shape">The shape to array.</param>
    /// <param name="direction">Direction vector for the array.</param>
    /// <param name="count">Number of copies to create.</param>
    /// <param name="spacing">Spacing between copies along the direction.</param>
    /// <returns>List of shapes including the original.</returns>
    public static List<Shape> LinearArray(Shape shape, VXYZ direction, int count, double spacing)
    {
        if (count <= 0) return new List<Shape>();

        var result = new List<Shape> { shape };
        var normalizedDir = direction.Normalize();

        for (int i = 1; i < count; i++)
        {
            var clone = shape.Clone();
            var offset = normalizedDir * (spacing * i);
            clone.Move(offset);
            result.Add(clone);
        }

        return result;
    }

    /// <summary>
    /// Creates a rectangular grid array of shapes.
    /// </summary>
    /// <param name="shape">The shape to array.</param>
    /// <param name="rows">Number of rows.</param>
    /// <param name="cols">Number of columns.</param>
    /// <param name="rowSpacing">Spacing between rows (Y direction).</param>
    /// <param name="colSpacing">Spacing between columns (X direction).</param>
    /// <returns>List of shapes including the original.</returns>
    public static List<Shape> RectangularArray(Shape shape, int rows, int cols, double rowSpacing, double colSpacing)
    {
        if (rows <= 0 || cols <= 0) return new List<Shape>();

        var result = new List<Shape>();

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                if (row == 0 && col == 0)
                {
                    result.Add(shape);
                }
                else
                {
                    var clone = shape.Clone();
                    clone.Move(new VXYZ(col * colSpacing, row * rowSpacing, 0));
                    result.Add(clone);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Creates a circular/polar array of shapes around a center point.
    /// </summary>
    /// <param name="shape">The shape to array.</param>
    /// <param name="center">Center point of the circular array.</param>
    /// <param name="count">Number of copies to create.</param>
    /// <param name="totalAngleDegrees">Total angle span in degrees (360 = full circle).</param>
    /// <param name="rotateItems">Whether to rotate each copy to face outward from center.</param>
    /// <returns>List of shapes including the original.</returns>
    public static List<Shape> CircularArray(Shape shape, VPoint center, int count, double totalAngleDegrees = 360, bool rotateItems = true)
    {
        if (count <= 0) return new List<Shape>();

        var result = new List<Shape> { shape };

        // Calculate angle step
        // If totalAngle is 360, we don't want to overlap first and last, so divide by count
        // If totalAngle < 360, we want count items spanning the arc, so divide by count-1
        double angleStep = Math.Abs(totalAngleDegrees) >= 360
            ? totalAngleDegrees / count
            : totalAngleDegrees / (count - 1);

        for (int i = 1; i < count; i++)
        {
            var clone = shape.Clone();
            double angleDegrees = angleStep * i;

            if (rotateItems)
            {
                // Rotate around center - this moves the shape AND rotates its orientation
                clone.Rotate(center, angleDegrees);
            }
            else
            {
                // Only move the shape without rotating its orientation
                var bounds = clone.GetBounds();
                var shapeCenter = VPoint.Internal(
                    (bounds.Min.X + bounds.Max.X) / 2,
                    (bounds.Min.Y + bounds.Max.Y) / 2
                );

                // Calculate new position by rotating the shape center around the array center
                double angleRadians = angleDegrees * Math.PI / 180;
                double cos = Math.Cos(angleRadians);
                double sin = Math.Sin(angleRadians);

                double dx = shapeCenter.X - center.X;
                double dy = shapeCenter.Y - center.Y;

                double newCenterX = center.X + dx * cos - dy * sin;
                double newCenterY = center.Y + dx * sin + dy * cos;

                // Move by the difference
                clone.Move(new VXYZ(newCenterX - shapeCenter.X, newCenterY - shapeCenter.Y, 0));
            }

            result.Add(clone);
        }

        return result;
    }

    /// <summary>
    /// Creates an array of shapes along a path curve.
    /// </summary>
    /// <param name="shape">The shape to array.</param>
    /// <param name="path">The path curve to follow.</param>
    /// <param name="count">Number of copies to create.</param>
    /// <param name="alignToPath">Whether to rotate items to align with path tangent.</param>
    /// <returns>List of shapes.</returns>
    public static List<Shape> PathArray(Shape shape, ICurve path, int count, bool alignToPath = true)
    {
        if (count <= 0) return new List<Shape>();

        var result = new List<Shape>();
        double pathLength = path.GetLength();

        // Get shape's center for positioning
        var bounds = shape.GetBounds();
        var shapeCenter = VPoint.Internal(
            (bounds.Min.X + bounds.Max.X) / 2,
            (bounds.Min.Y + bounds.Max.Y) / 2
        );

        for (int i = 0; i < count; i++)
        {
            double t = count == 1 ? 0 : (double)i / (count - 1);
            double distance = t * pathLength;

            var point = path.PointAtSegmentLength(distance);
            var clone = shape.Clone();

            // Move shape so its center is at the path point
            var offset = new VXYZ(point.X - shapeCenter.X, point.Y - shapeCenter.Y, 0);
            clone.Move(offset);

            if (alignToPath)
            {
                // Get tangent direction at this point
                var normal = path.NormalAtPoint(point);
                // Tangent is perpendicular to normal
                double tangentAngle = Math.Atan2(-normal.X, normal.Y) * 180 / Math.PI;
                clone.Rotate(point, tangentAngle);
            }

            result.Add(clone);
        }

        return result;
    }

    /// <summary>
    /// Creates a mirrored copy of a shape across a line.
    /// </summary>
    /// <param name="shape">The shape to mirror.</param>
    /// <param name="mirrorLine">The line to mirror across.</param>
    /// <returns>List containing original and mirrored shape.</returns>
    public static List<Shape> Mirror(Shape shape, VLine mirrorLine)
    {
        var clone = shape.Clone();
        clone.Flip(mirrorLine);
        return new List<Shape> { shape, clone };
    }

    /// <summary>
    /// Creates a spiral array of shapes.
    /// </summary>
    /// <param name="shape">The shape to array.</param>
    /// <param name="center">Center point of the spiral.</param>
    /// <param name="count">Number of copies to create.</param>
    /// <param name="startRadius">Starting radius from center.</param>
    /// <param name="endRadius">Ending radius from center.</param>
    /// <param name="totalRevolutions">Number of full revolutions.</param>
    /// <param name="rotateItems">Whether to rotate items to face outward.</param>
    /// <returns>List of shapes.</returns>
    public static List<Shape> SpiralArray(Shape shape, VPoint center, int count,
        double startRadius, double endRadius, double totalRevolutions = 1, bool rotateItems = true)
    {
        if (count <= 0) return new List<Shape>();

        var result = new List<Shape>();

        // Get shape's center for positioning
        var bounds = shape.GetBounds();
        var shapeCenter = VPoint.Internal(
            (bounds.Min.X + bounds.Max.X) / 2,
            (bounds.Min.Y + bounds.Max.Y) / 2
        );

        double totalAngle = totalRevolutions * 360;

        for (int i = 0; i < count; i++)
        {
            double t = count == 1 ? 0 : (double)i / (count - 1);
            double angle = t * totalAngle * Math.PI / 180;
            double radius = startRadius + t * (endRadius - startRadius);

            double x = center.X + radius * Math.Cos(angle);
            double y = center.Y + radius * Math.Sin(angle);

            var clone = shape.Clone();
            var offset = new VXYZ(x - shapeCenter.X, y - shapeCenter.Y, 0);
            clone.Move(offset);

            if (rotateItems)
            {
                double angleDegrees = t * totalAngle;
                clone.Rotate(new VPoint(x, y), angleDegrees);
            }

            result.Add(clone);
        }

        return result;
    }
}

/// <summary>
/// Extension methods for shape array operations.
/// </summary>
public static class ShapeArrayExtensions
{
    /// <summary>
    /// Creates a linear array of this shape along a direction.
    /// </summary>
    public static List<Shape> LinearArray(this Shape shape, VXYZ direction, int count, double spacing)
    {
        return ArrayOps.LinearArray(shape, direction, count, spacing);
    }

    /// <summary>
    /// Creates a linear array of this shape along the X axis.
    /// </summary>
    public static List<Shape> LinearArrayX(this Shape shape, int count, double spacing)
    {
        return ArrayOps.LinearArray(shape, new VXYZ(1, 0, 0), count, spacing);
    }

    /// <summary>
    /// Creates a linear array of this shape along the Y axis.
    /// </summary>
    public static List<Shape> LinearArrayY(this Shape shape, int count, double spacing)
    {
        return ArrayOps.LinearArray(shape, new VXYZ(0, 1, 0), count, spacing);
    }

    /// <summary>
    /// Creates a rectangular grid array of this shape.
    /// </summary>
    public static List<Shape> RectangularArray(this Shape shape, int rows, int cols, double rowSpacing, double colSpacing)
    {
        return ArrayOps.RectangularArray(shape, rows, cols, rowSpacing, colSpacing);
    }

    /// <summary>
    /// Creates a circular/polar array of this shape around a center point.
    /// </summary>
    public static List<Shape> CircularArray(this Shape shape, VPoint center, int count, double totalAngleDegrees = 360, bool rotateItems = true)
    {
        return ArrayOps.CircularArray(shape, center, count, totalAngleDegrees, rotateItems);
    }

    /// <summary>
    /// Creates an array of this shape along a path curve.
    /// </summary>
    public static List<Shape> PathArray(this Shape shape, ICurve path, int count, bool alignToPath = true)
    {
        return ArrayOps.PathArray(shape, path, count, alignToPath);
    }

    /// <summary>
    /// Creates a mirrored copy of this shape across a line.
    /// </summary>
    public static List<Shape> Mirror(this Shape shape, VLine mirrorLine)
    {
        return ArrayOps.Mirror(shape, mirrorLine);
    }

    /// <summary>
    /// Creates a spiral array of this shape.
    /// </summary>
    public static List<Shape> SpiralArray(this Shape shape, VPoint center, int count,
        double startRadius, double endRadius, double totalRevolutions = 1, bool rotateItems = true)
    {
        return ArrayOps.SpiralArray(shape, center, count, startRadius, endRadius, totalRevolutions, rotateItems);
    }

    /// <summary>
    /// Draws all shapes in a list.
    /// </summary>
    public static void DrawAll(this IEnumerable<Shape> shapes)
    {
        foreach (var shape in shapes)
        {
            shape.Draw();
        }
    }
}
