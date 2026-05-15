using System;
using System.Collections.Generic;
using Code2Viz.Canvas;

namespace Code2Viz.Geometry;

/// <summary>
/// A grid of square VCell instances with neighbour connectivity and A* pathfinding.
/// </summary>
public class VSpatialGrid : Shape
{
    /// <summary>
    /// All cells in the grid, stored in row-major order.
    /// </summary>
    public List<VCell> Cells { get; } = new();

    private KDTree<VCell>? _kdTree;

    /// <summary>
    /// The location point (center of the bottom-left cell).
    /// </summary>
    public VPoint Location { get; private set; }

    /// <summary>
    /// Number of cells along the X axis.
    /// </summary>
    public int XCount { get; private set; }

    /// <summary>
    /// Number of cells along the Y axis.
    /// </summary>
    public int YCount { get; private set; }

    /// <summary>
    /// Side length of each square cell.
    /// </summary>
    public double CellSize { get; private set; }

    /// <summary>
    /// Gets the total number of cells in the grid.
    /// </summary>
    public int Count => Cells.Count;

    /// <summary>
    /// Gets a cell by flat index.
    /// </summary>
    public VCell this[int index] => Cells[index];

    /// <summary>
    /// Gets a cell by column and row indices.
    /// </summary>
    public VCell this[int col, int row] => Cells[row * XCount + col];

    /// <summary>
    /// Creates a spatial grid of square cells with neighbour connectivity.
    /// </summary>
    /// <param name="location">Center of the bottom-left cell (first cell)</param>
    /// <param name="xCount">Number of cells along the X axis</param>
    /// <param name="yCount">Number of cells along the Y axis</param>
    /// <param name="cellSize">Side length of each square cell</param>
    public VSpatialGrid(VPoint location, int xCount, int yCount, double cellSize)
    {
        Location = location;
        XCount = Math.Max(1, xCount);
        YCount = Math.Max(1, yCount);
        CellSize = Math.Max(cellSize, GeometryTolerance.Epsilon);

        Color = ShapeDefaults.GlobalColor ?? "Gray";
        FillColor = ShapeDefaults.GlobalFillColor ?? "Transparent";

        GenerateCells();
        AssignNeighbours();
        RebuildKDTree();
    }

    private void GenerateCells()
    {
        Cells.Clear();

        for (int row = 0; row < YCount; row++)
        {
            for (int col = 0; col < XCount; col++)
            {
                double x = Location.X + col * CellSize;
                double y = Location.Y + row * CellSize;
                var center = VPoint.Internal(x, y);
                int uniqueId = row * XCount + col;

                var cell = new VCell(center, CellSize, uniqueId, col, row);
                cell.Color = Color;
                cell.FillColor = FillColor;
                cell.LineWeight = LineWeight;
                Cells.Add(cell);
            }
        }
    }

    private void AssignNeighbours()
    {
        for (int row = 0; row < YCount; row++)
        {
            for (int col = 0; col < XCount; col++)
            {
                var cell = Cells[row * XCount + col];

                if (col > 0)
                    cell.Neighbours.Add(Cells[row * XCount + (col - 1)]);
                if (col < XCount - 1)
                    cell.Neighbours.Add(Cells[row * XCount + (col + 1)]);
                if (row > 0)
                    cell.Neighbours.Add(Cells[(row - 1) * XCount + col]);
                if (row < YCount - 1)
                    cell.Neighbours.Add(Cells[(row + 1) * XCount + col]);
            }
        }
    }

    private void RebuildKDTree()
    {
        _kdTree = new KDTree<VCell>(c => c.Center.X, c => c.Center.Y);
        _kdTree.Build(Cells);
    }

    private void InvalidateKDTree()
    {
        _kdTree = null;
    }

    private KDTree<VCell> GetKDTree()
    {
        if (_kdTree == null)
            RebuildKDTree();
        return _kdTree!;
    }

    public override VSpatialGrid Clone()
    {
        var clone = new VSpatialGrid(
            VPoint.Internal(Location.X, Location.Y),
            XCount, YCount, CellSize);
        CopyStyleTo(clone);
        clone.ApplyStyle();
        return clone;
    }

    public override void Move(VXYZ vector)
    {
        Location = VPoint.Internal(Location.X + vector.X, Location.Y + vector.Y);
        foreach (var cell in Cells)
            cell.Move(vector);
        InvalidateKDTree();
    }

    public override void Rotate(VPoint pivot, double angleDegrees)
    {
        Location = GeometryHelper.RotatePoint(Location, pivot, angleDegrees);
        foreach (var cell in Cells)
            cell.Rotate(pivot, angleDegrees);
        InvalidateKDTree();
    }

    public override void Flip(VLine mirrorLine)
    {
        Location = GeometryHelper.FlipPoint(Location, mirrorLine);
        foreach (var cell in Cells)
            cell.Flip(mirrorLine);
        InvalidateKDTree();
    }

    public override void Scale(VPoint center, double factor)
    {
        double dx = Location.X - center.X;
        double dy = Location.Y - center.Y;
        Location = VPoint.Internal(center.X + dx * factor, center.Y + dy * factor);
        CellSize *= Math.Abs(factor);
        foreach (var cell in Cells)
            cell.Scale(center, factor);
        InvalidateKDTree();
    }

    public override BoundingBox GetBounds()
    {
        if (Cells.Count == 0)
            return new BoundingBox(VPoint.Internal(Location.X, Location.Y), VPoint.Internal(Location.X, Location.Y));

        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;

        foreach (var cell in Cells)
        {
            var bounds = cell.GetBounds();
            minX = Math.Min(minX, bounds.Min.X);
            minY = Math.Min(minY, bounds.Min.Y);
            maxX = Math.Max(maxX, bounds.Max.X);
            maxY = Math.Max(maxY, bounds.Max.Y);
        }

        return new BoundingBox(VPoint.Internal(minX, minY), VPoint.Internal(maxX, maxY));
    }

    public override double DistanceTo(VPoint point)
    {
        if (Cells.Count == 0)
            return Location.DistanceTo(point);

        double minDistance = double.MaxValue;
        foreach (var cell in Cells)
        {
            double dist = cell.DistanceTo(point);
            minDistance = Math.Min(minDistance, dist);
        }
        return minDistance;
    }

    /// <summary>
    /// Finds the cell whose center is closest to the given point.
    /// Uses a KD-tree for O(log n) lookup.
    /// </summary>
    public VCell GetClosestCell(VPoint point)
    {
        return GetKDTree().Nearest(point.X, point.Y);
    }

    /// <summary>
    /// Finds the cell containing the given point, or null if none.
    /// </summary>
    public VCell? GetCellAt(VPoint point)
    {
        foreach (var cell in Cells)
        {
            if (cell.Contains(point))
                return cell;
        }
        return null;
    }

    /// <summary>
    /// Finds the shortest path between two cells using the A* algorithm.
    /// Skips cells where Blocked is true. Returns an empty list if no path exists.
    /// </summary>
    public List<VCell> FindPath(VCell start, VCell end)
    {
        if (start == end)
            return new List<VCell> { start };

        if (start.Blocked || end.Blocked)
            return new List<VCell>();

        var openSet = new SortedSet<(double f, int id)>();
        var gScore = new Dictionary<int, double>();
        var cameFrom = new Dictionary<int, VCell>();
        var inOpen = new HashSet<int>();

        gScore[start.UniqueId] = 0;
        double h = Heuristic(start, end);
        openSet.Add((h, start.UniqueId));
        inOpen.Add(start.UniqueId);

        while (openSet.Count > 0)
        {
            var (_, currentId) = openSet.Min;
            openSet.Remove(openSet.Min);
            inOpen.Remove(currentId);

            var current = Cells[currentId];

            if (current == end)
                return ReconstructPath(cameFrom, current);

            foreach (var neighbour in current.Neighbours)
            {
                if (neighbour.Blocked)
                    continue;

                double tentativeG = gScore[currentId] + Heuristic(current, neighbour);
                int nid = neighbour.UniqueId;

                if (!gScore.TryGetValue(nid, out double existingG) || tentativeG < existingG)
                {
                    cameFrom[nid] = current;
                    gScore[nid] = tentativeG;
                    double f = tentativeG + Heuristic(neighbour, end);

                    if (inOpen.Contains(nid))
                        openSet.RemoveWhere(e => e.id == nid);

                    openSet.Add((f, nid));
                    inOpen.Add(nid);
                }
            }
        }

        return new List<VCell>();
    }

    private static double Heuristic(VCell a, VCell b)
    {
        double dx = a.Center.X - b.Center.X;
        double dy = a.Center.Y - b.Center.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static List<VCell> ReconstructPath(Dictionary<int, VCell> cameFrom, VCell current)
    {
        var path = new List<VCell> { current };
        while (cameFrom.TryGetValue(current.UniqueId, out var prev))
        {
            path.Add(prev);
            current = prev;
        }
        path.Reverse();
        return path;
    }

    /// <summary>
    /// Applies the grid's style to all contained cells.
    /// </summary>
    public void ApplyStyle()
    {
        foreach (var cell in Cells)
        {
            cell.Color = Color;
            cell.FillColor = FillColor;
            cell.LineWeight = LineWeight;
        }
    }

    /// <summary>
    /// Gets a row of cells by row index (0-based, from bottom).
    /// </summary>
    public List<VCell> GetRow(int row)
    {
        if (row < 0 || row >= YCount)
            throw new ArgumentOutOfRangeException(nameof(row));

        var result = new List<VCell>();
        for (int col = 0; col < XCount; col++)
        {
            result.Add(Cells[row * XCount + col]);
        }
        return result;
    }

    /// <summary>
    /// Gets a column of cells by column index (0-based, from left).
    /// </summary>
    public List<VCell> GetColumn(int col)
    {
        if (col < 0 || col >= XCount)
            throw new ArgumentOutOfRangeException(nameof(col));

        var result = new List<VCell>();
        for (int row = 0; row < YCount; row++)
        {
            result.Add(Cells[row * XCount + col]);
        }
        return result;
    }

    /// <summary>
    /// Gets the center point of the grid.
    /// </summary>
    public VPoint GetCenter()
    {
        var bounds = GetBounds();
        return VPoint.Internal((bounds.Min.X + bounds.Max.X) / 2, (bounds.Min.Y + bounds.Max.Y) / 2);
    }

    public override string ToString() => $"VSpatialGrid({XCount}x{YCount}, CellSize={CellSize}, Location=({Location.X:F2}, {Location.Y:F2}))";
}
