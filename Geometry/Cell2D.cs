using System;
using System.Collections.Generic;
using Code2Viz.Canvas;

namespace Code2Viz.Geometry;

/// <summary>
/// A square cell with a VPolygon boundary, used as a building block for VSpatialGrid.
/// Each cell tracks its neighbours and grid position.
/// </summary>
public class VCell : VPolygon
{
    /// <summary>
    /// Unique identifier within the grid (0-based sequential).
    /// </summary>
    public int UniqueId { get; }

    /// <summary>
    /// Adjacent cells in the grid (4-connectivity: left, right, below, above).
    /// </summary>
    public List<VCell> Neighbours { get; } = new();

    /// <summary>
    /// The center point of this cell.
    /// </summary>
    public VPoint Center { get; private set; }

    /// <summary>
    /// The side length of this square cell.
    /// </summary>
    public double CellSize { get; private set; }

    /// <summary>
    /// Whether this cell is blocked (impassable).
    /// </summary>
    public bool Blocked { get; set; }

    /// <summary>
    /// Column index in the grid (0-based, along X axis).
    /// </summary>
    public int Column { get; }

    /// <summary>
    /// Row index in the grid (0-based, along Y axis).
    /// </summary>
    public int Row { get; }

    /// <summary>
    /// Creates a square cell centered at the given point.
    /// </summary>
    public VCell(VPoint center, double cellSize, int uniqueId, int column, int row)
        : base(ComputeCorners(center, cellSize))
    {
        Center = center;
        CellSize = cellSize;
        UniqueId = uniqueId;
        Column = column;
        Row = row;
        Color = ShapeDefaults.GlobalColor ?? "Gray";
        FillColor = ShapeDefaults.GlobalFillColor ?? "Transparent";
    }

    private static VPoint[] ComputeCorners(VPoint center, double cellSize)
    {
        double half = cellSize / 2.0;
        return new[]
        {
            VPoint.Internal(center.X - half, center.Y - half), // bottom-left
            VPoint.Internal(center.X + half, center.Y - half), // bottom-right
            VPoint.Internal(center.X + half, center.Y + half), // top-right
            VPoint.Internal(center.X - half, center.Y + half)  // top-left
        };
    }

    public override VCell Clone()
    {
        var clone = new VCell(Center.Clone(), CellSize, UniqueId, Column, Row);
        CopyStyleTo(clone);
        return clone;
    }

    public override void Move(VXYZ vector)
    {
        base.Move(vector);
        Center = VPoint.Internal(Center.X + vector.X, Center.Y + vector.Y);
    }

    public override void Rotate(VPoint pivot, double angleDegrees)
    {
        base.Rotate(pivot, angleDegrees);
        Center = GeometryHelper.RotatePoint(Center, pivot, angleDegrees);
    }

    public override void Flip(VLine mirrorLine)
    {
        base.Flip(mirrorLine);
        Center = GeometryHelper.FlipPoint(Center, mirrorLine);
    }

    public override void Scale(VPoint center, double factor)
    {
        base.Scale(center, factor);
        double dx = Center.X - center.X;
        double dy = Center.Y - center.Y;
        Center = VPoint.Internal(center.X + dx * factor, center.Y + dy * factor);
        CellSize *= Math.Abs(factor);
    }

    public override string ToString() => $"VCell(Id={UniqueId}, Col={Column}, Row={Row}, Center=({Center.X:F2}, {Center.Y:F2}))";
}
