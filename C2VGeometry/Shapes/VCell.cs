using System;
using System.Collections.Generic;

namespace C2VGeometry;

/// <summary>
/// A square cell with a VPolygon boundary, used as a building block for VSpatialGrid.
/// Each cell tracks its neighbours and grid position.
/// </summary>
public class VCell : VPolygon
{
    /// <summary>
    /// Unique identifier within the grid (0-based sequential).
    /// Distinct from Shape.Id which is a global auto-incremented value.
    /// </summary>
    public int UniqueId { get; }

    /// <summary>
    /// Adjacent cells in the grid (4-connectivity: left, right, below, above).
    /// </summary>
    public List<VCell> Neighbours { get; } = new();

    /// <summary>
    /// The center point of this cell.
    /// </summary>
    public VXYZ Center { get; private set; }

    /// <summary>
    /// Whether this cell is blocked (impassable).
    /// </summary>
    public bool Blocked { get; set; }

    /// <summary>
    /// The side length of this square cell.
    /// </summary>
    public double CellSize { get; private set; }

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
    /// <param name="center">Center of the cell</param>
    /// <param name="cellSize">Side length of the square</param>
    /// <param name="uniqueId">Sequential ID within the grid</param>
    /// <param name="column">Column index in the grid</param>
    /// <param name="row">Row index in the grid</param>
    public VCell(VXYZ center, double cellSize, int uniqueId, int column, int row)
        : base(ComputeCorners(center, cellSize))
    {
        Center = center;
        CellSize = cellSize;
        UniqueId = uniqueId;
        Column = column;
        Row = row;
        Color = "Gray";
        FillColor = "Transparent";
    }

    private static VXYZ[] ComputeCorners(VXYZ center, double cellSize)
    {
        double half = cellSize / 2.0;
        return new[]
        {
            new VXYZ(center.X - half, center.Y - half), // bottom-left
            new VXYZ(center.X + half, center.Y - half), // bottom-right
            new VXYZ(center.X + half, center.Y + half), // top-right
            new VXYZ(center.X - half, center.Y + half)  // top-left
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
        Center = new VXYZ(Center.X + vector.X, Center.Y + vector.Y);
    }

    public override void Rotate(VXYZ pivot, double angleDegrees)
    {
        base.Rotate(pivot, angleDegrees);
        Center = GeometryHelper.RotatePoint(Center, pivot, angleDegrees);
    }

    public override void Flip(VLine mirrorLine)
    {
        base.Flip(mirrorLine);
        Center = GeometryHelper.FlipPoint(Center, mirrorLine);
    }

    public override void Scale(VXYZ center, double factor)
    {
        base.Scale(center, factor);
        Center = GeometryHelper.ScalePoint(Center, center, factor);
        CellSize *= Math.Abs(factor);
    }

    public override string ToString() => $"VCell(Id={UniqueId}, Col={Column}, Row={Row}, Center={Center})";
}
