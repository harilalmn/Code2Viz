using Code2Viz.Canvas;

namespace Code2Viz.Geometry;

/// <summary>
/// A grid of VPoints arranged in rows and columns.
/// </summary>
public class VGrid : Shape
{
    /// <summary>
    /// The collection of points in the grid.
    /// </summary>
    public List<VPoint> Points { get; } = new();

    /// <summary>
    /// The location point (center if centered, bottom-left if not).
    /// </summary>
    public VPoint Location { get; private set; }

    /// <summary>
    /// Number of points along the X axis.
    /// </summary>
    public int XCount { get; private set; }

    /// <summary>
    /// Number of points along the Y axis.
    /// </summary>
    public int YCount { get; private set; }

    /// <summary>
    /// Spacing between points along the X axis.
    /// </summary>
    public double XSpacing { get; private set; }

    /// <summary>
    /// Spacing between points along the Y axis.
    /// </summary>
    public double YSpacing { get; private set; }

    /// <summary>
    /// Whether the grid is centered at the location point.
    /// </summary>
    public bool Centered { get; private set; }

    /// <summary>
    /// Gets the total number of points in the grid.
    /// </summary>
    public int Count => Points.Count;

    /// <summary>
    /// Gets a point by index.
    /// </summary>
    public VPoint this[int index] => Points[index];

    /// <summary>
    /// Gets a point by row and column indices.
    /// </summary>
    /// <param name="col">Column index (0-based, along X axis)</param>
    /// <param name="row">Row index (0-based, along Y axis)</param>
    public VPoint this[int col, int row] => Points[row * XCount + col];

    /// <summary>
    /// Creates a grid of VPoints.
    /// </summary>
    /// <param name="location">The reference location point</param>
    /// <param name="xcount">Number of points along the X axis</param>
    /// <param name="ycount">Number of points along the Y axis</param>
    /// <param name="xSpacing">Spacing between points along X (default: 1.0)</param>
    /// <param name="ySpacing">Spacing between points along Y (default: 1.0)</param>
    /// <param name="centered">If true, grid is centered at location; if false, location is bottom-left corner</param>
    public VGrid(VPoint location, int xcount, int ycount, double xSpacing = 1.0, double ySpacing = 1.0, bool centered = true)
    {
        Location = location;
        XCount = Math.Max(1, xcount);
        YCount = Math.Max(1, ycount);
        XSpacing = xSpacing;
        YSpacing = ySpacing;
        Centered = centered;

        StrokeColor = ShapeDefaults.GlobalStrokeColor ?? "White";
        FillColor = ShapeDefaults.GlobalFillColor ?? "LimeGreen";

        GeneratePoints();
    }

    /// <summary>
    /// Creates a grid of VPoints with uniform spacing.
    /// </summary>
    /// <param name="location">The reference location point</param>
    /// <param name="xcount">Number of points along the X axis</param>
    /// <param name="ycount">Number of points along the Y axis</param>
    /// <param name="spacing">Uniform spacing between points (default: 1.0)</param>
    /// <param name="centered">If true, grid is centered at location; if false, location is bottom-left corner</param>
    public VGrid(VPoint location, int xcount, int ycount, double spacing, bool centered = true)
        : this(location, xcount, ycount, spacing, spacing, centered)
    {
    }

    /// <summary>
    /// Creates a grid of VPoints with default spacing of 1.0.
    /// </summary>
    /// <param name="location">The reference location point</param>
    /// <param name="xcount">Number of points along the X axis</param>
    /// <param name="ycount">Number of points along the Y axis</param>
    /// <param name="centered">If true, grid is centered at location; if false, location is bottom-left corner</param>
    public VGrid(VPoint location, int xcount, int ycount, bool centered)
        : this(location, xcount, ycount, 1.0, 1.0, centered)
    {
    }

    private void GeneratePoints()
    {
        Points.Clear();

        // Calculate the offset for the bottom-left corner of the grid
        double offsetX, offsetY;

        if (Centered)
        {
            // Center the grid at the location
            offsetX = Location.X - (XCount - 1) * XSpacing / 2.0;
            offsetY = Location.Y - (YCount - 1) * YSpacing / 2.0;
        }
        else
        {
            // Location is the bottom-left corner
            offsetX = Location.X;
            offsetY = Location.Y;
        }

        // Generate points row by row, from bottom to top
        for (int row = 0; row < YCount; row++)
        {
            for (int col = 0; col < XCount; col++)
            {
                double x = offsetX + col * XSpacing;
                double y = offsetY + row * YSpacing;
                var point = new VPoint(x, y);
                point.StrokeColor = StrokeColor;
                point.FillColor = FillColor;
                point.StrokeThickness = StrokeThickness;
                Points.Add(point);
            }
        }
    }

    /// <summary>
    /// Draws all points in the grid.
    /// </summary>
    public override void Draw()
    {
        foreach (var point in Points)
        {
            point.Draw();
        }
    }

    /// <summary>
    /// Creates a deep copy of this grid.
    /// </summary>
    public override Shape Clone()
    {
        var clone = new VGrid(
            new VPoint(Location.X, Location.Y),
            XCount, YCount, XSpacing, YSpacing, Centered);
        CopyStyleTo(clone);
        clone.ApplyStyle();
        return clone;
    }

    /// <summary>
    /// Moves all points in the grid by the specified vector.
    /// </summary>
    public override void Move(VXYZ vector)
    {
        Location = new VPoint(Location.X + vector.X, Location.Y + vector.Y);
        foreach (var point in Points)
        {
            point.Move(vector);
        }
    }

    /// <summary>
    /// Rotates all points in the grid around a pivot point.
    /// </summary>
    public override void Rotate(VPoint pivot, double angleDegrees)
    {
        Location = GeometryHelper.RotatePoint(Location, pivot, angleDegrees);
        foreach (var point in Points)
        {
            point.Rotate(pivot, angleDegrees);
        }
    }

    /// <summary>
    /// Flips all points in the grid across a mirror line.
    /// </summary>
    public override void Flip(VLine mirrorLine)
    {
        Location = GeometryHelper.FlipPoint(Location, mirrorLine);
        foreach (var point in Points)
        {
            point.Flip(mirrorLine);
        }
    }

    /// <summary>
    /// Scales all points in the grid from a center point.
    /// </summary>
    public override void Scale(VPoint center, double factor)
    {
        Location = new VPoint(
            center.X + (Location.X - center.X) * factor,
            center.Y + (Location.Y - center.Y) * factor);
        foreach (var point in Points)
        {
            point.Scale(center, factor);
        }
    }

    /// <summary>
    /// Gets the bounding box of all points in the grid.
    /// </summary>
    public override (VPoint min, VPoint max) GetBounds()
    {
        if (Points.Count == 0)
            return (new VPoint(Location.X, Location.Y), new VPoint(Location.X, Location.Y));

        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;

        foreach (var point in Points)
        {
            minX = Math.Min(minX, point.X);
            minY = Math.Min(minY, point.Y);
            maxX = Math.Max(maxX, point.X);
            maxY = Math.Max(maxY, point.Y);
        }

        return (new VPoint(minX, minY), new VPoint(maxX, maxY));
    }

    /// <summary>
    /// Returns the distance from the nearest point in the grid to the specified point.
    /// </summary>
    public override double DistanceTo(VPoint point)
    {
        if (Points.Count == 0)
            return Location.DistanceTo(point);

        double minDistance = double.MaxValue;
        foreach (var gridPoint in Points)
        {
            double dist = gridPoint.DistanceTo(point);
            minDistance = Math.Min(minDistance, dist);
        }
        return minDistance;
    }

    /// <summary>
    /// Applies the grid's style to all contained points.
    /// </summary>
    public void ApplyStyle()
    {
        foreach (var point in Points)
        {
            point.StrokeColor = StrokeColor;
            point.FillColor = FillColor;
            point.StrokeThickness = StrokeThickness;
        }
    }

    /// <summary>
    /// Gets a row of points by row index (0-based, from bottom).
    /// </summary>
    public List<VPoint> GetRow(int row)
    {
        if (row < 0 || row >= YCount)
            throw new ArgumentOutOfRangeException(nameof(row));

        var result = new List<VPoint>();
        for (int col = 0; col < XCount; col++)
        {
            result.Add(Points[row * XCount + col]);
        }
        return result;
    }

    /// <summary>
    /// Gets a column of points by column index (0-based, from left).
    /// </summary>
    public List<VPoint> GetColumn(int col)
    {
        if (col < 0 || col >= XCount)
            throw new ArgumentOutOfRangeException(nameof(col));

        var result = new List<VPoint>();
        for (int row = 0; row < YCount; row++)
        {
            result.Add(Points[row * XCount + col]);
        }
        return result;
    }

    /// <summary>
    /// Gets the center point of the grid.
    /// </summary>
    public VPoint GetCenter()
    {
        var (min, max) = GetBounds();
        return new VPoint((min.X + max.X) / 2, (min.Y + max.Y) / 2);
    }

    public override string ToString() => $"VGrid({XCount}x{YCount}, Location={Location}, Centered={Centered})";
}
