using System;
using System.Collections.Generic;

namespace C2VGeometry;

/// <summary>
/// A spatial grid for accelerating edge-edge intersection detection.
/// Divides space into cells and only checks edges in same/adjacent cells.
/// </summary>
internal class SpatialGrid<T>
{
    private readonly double _cellSize;
    private readonly Dictionary<(int, int), List<T>> _cells = new();
    private readonly Func<T, (double minX, double minY, double maxX, double maxY)> _getBounds;

    /// <summary>
    /// Creates a spatial grid with the specified cell size.
    /// </summary>
    /// <param name="cellSize">Size of each grid cell</param>
    /// <param name="getBounds">Function to get bounding box of an item</param>
    public SpatialGrid(double cellSize, Func<T, (double minX, double minY, double maxX, double maxY)> getBounds)
    {
        _cellSize = cellSize > 0 ? cellSize : 10.0;
        _getBounds = getBounds;
    }

    /// <summary>
    /// Inserts an item into all cells it overlaps.
    /// </summary>
    public void Insert(T item)
    {
        var (minX, minY, maxX, maxY) = _getBounds(item);

        int minCellX = (int)Math.Floor(minX / _cellSize);
        int maxCellX = (int)Math.Floor(maxX / _cellSize);
        int minCellY = (int)Math.Floor(minY / _cellSize);
        int maxCellY = (int)Math.Floor(maxY / _cellSize);

        for (int x = minCellX; x <= maxCellX; x++)
        {
            for (int y = minCellY; y <= maxCellY; y++)
            {
                var key = (x, y);
                if (!_cells.TryGetValue(key, out var list))
                {
                    list = new List<T>();
                    _cells[key] = list;
                }
                list.Add(item);
            }
        }
    }

    /// <summary>
    /// Queries for all items that might intersect with the given bounds.
    /// </summary>
    public IEnumerable<T> Query(double minX, double minY, double maxX, double maxY)
    {
        var seen = new HashSet<T>();

        int minCellX = (int)Math.Floor(minX / _cellSize);
        int maxCellX = (int)Math.Floor(maxX / _cellSize);
        int minCellY = (int)Math.Floor(minY / _cellSize);
        int maxCellY = (int)Math.Floor(maxY / _cellSize);

        for (int x = minCellX; x <= maxCellX; x++)
        {
            for (int y = minCellY; y <= maxCellY; y++)
            {
                if (_cells.TryGetValue((x, y), out var list))
                {
                    foreach (var item in list)
                    {
                        if (seen.Add(item))
                        {
                            yield return item;
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Clears all items from the grid.
    /// </summary>
    public void Clear()
    {
        _cells.Clear();
    }
}

/// <summary>
/// Represents an edge with its endpoints for spatial indexing.
/// </summary>
internal class IndexedEdge
{
    public VXYZ Start { get; }
    public VXYZ End { get; }
    public int PolygonIndex { get; }
    public int EdgeIndex { get; }

    public double MinX => Math.Min(Start.X, End.X);
    public double MaxX => Math.Max(Start.X, End.X);
    public double MinY => Math.Min(Start.Y, End.Y);
    public double MaxY => Math.Max(Start.Y, End.Y);

    public IndexedEdge(VXYZ start, VXYZ end, int polygonIndex, int edgeIndex)
    {
        Start = start;
        End = end;
        PolygonIndex = polygonIndex;
        EdgeIndex = edgeIndex;
    }

    public (double minX, double minY, double maxX, double maxY) GetBounds()
    {
        return (MinX, MinY, MaxX, MaxY);
    }
}

/// <summary>
/// Provides spatial acceleration for polygon edge intersection detection.
/// </summary>
internal static class SpatialAccelerator
{
    /// <summary>
    /// Finds all intersection points between edges of two polygons using spatial acceleration.
    /// </summary>
    public static List<EdgeIntersection> FindIntersections(VPolygon polygonA, VPolygon polygonB)
    {
        var results = new List<EdgeIntersection>();

        if (polygonA.Points.Count < 3 || polygonB.Points.Count < 3)
            return results;

        // Compute optimal cell size based on polygon extents
        var boundsA = polygonA.GetBounds();
        var boundsB = polygonB.GetBounds();

        double extentX = Math.Max(boundsA.Max.X - boundsA.Min.X, boundsB.Max.X - boundsB.Min.X);
        double extentY = Math.Max(boundsA.Max.Y - boundsA.Min.Y, boundsB.Max.Y - boundsB.Min.Y);
        double avgExtent = (extentX + extentY) / 2.0;

        // Cell size ~ extent / sqrt(n) for optimal performance
        int totalEdges = polygonA.Points.Count + polygonB.Points.Count;
        double cellSize = Math.Max(avgExtent / Math.Sqrt(totalEdges), GeometryTolerance.Epsilon * 1000);

        // Build spatial grid with polygon B edges
        var grid = new SpatialGrid<IndexedEdge>(cellSize, e => e.GetBounds());

        for (int i = 0; i < polygonB.Points.Count; i++)
        {
            var start = polygonB.Points[i];
            var end = polygonB.Points[(i + 1) % polygonB.Points.Count];
            grid.Insert(new IndexedEdge(start, end, 1, i));
        }

        // Query for each edge of polygon A
        for (int i = 0; i < polygonA.Points.Count; i++)
        {
            var startA = polygonA.Points[i];
            var endA = polygonA.Points[(i + 1) % polygonA.Points.Count];

            double minX = Math.Min(startA.X, endA.X);
            double maxX = Math.Max(startA.X, endA.X);
            double minY = Math.Min(startA.Y, endA.Y);
            double maxY = Math.Max(startA.Y, endA.Y);

            foreach (var edgeB in grid.Query(minX, minY, maxX, maxY))
            {
                if (TryGetIntersection(startA, endA, edgeB.Start, edgeB.End,
                    out double alphaA, out double alphaB, out VXYZ intersection))
                {
                    results.Add(new EdgeIntersection
                    {
                        Point = intersection,
                        EdgeIndexA = i,
                        EdgeIndexB = edgeB.EdgeIndex,
                        AlphaA = alphaA,
                        AlphaB = alphaB
                    });
                }
            }
        }

        return results;
    }

    /// <summary>
    /// Finds all self-intersection points within a single polygon.
    /// </summary>
    public static List<SelfIntersection> FindSelfIntersections(VPolygon polygon)
    {
        var results = new List<SelfIntersection>();

        if (polygon.Points.Count < 4)
            return results;

        var bounds = polygon.GetBounds();
        double extentX = bounds.Max.X - bounds.Min.X;
        double extentY = bounds.Max.Y - bounds.Min.Y;
        double avgExtent = (extentX + extentY) / 2.0;

        double cellSize = Math.Max(avgExtent / Math.Sqrt(polygon.Points.Count), GeometryTolerance.Epsilon * 1000);

        var grid = new SpatialGrid<IndexedEdge>(cellSize, e => e.GetBounds());

        // Insert all edges
        for (int i = 0; i < polygon.Points.Count; i++)
        {
            var start = polygon.Points[i];
            var end = polygon.Points[(i + 1) % polygon.Points.Count];
            grid.Insert(new IndexedEdge(start, end, 0, i));
        }

        // Check each edge against non-adjacent edges
        for (int i = 0; i < polygon.Points.Count; i++)
        {
            var startA = polygon.Points[i];
            var endA = polygon.Points[(i + 1) % polygon.Points.Count];

            double minX = Math.Min(startA.X, endA.X);
            double maxX = Math.Max(startA.X, endA.X);
            double minY = Math.Min(startA.Y, endA.Y);
            double maxY = Math.Max(startA.Y, endA.Y);

            foreach (var edgeB in grid.Query(minX, minY, maxX, maxY))
            {
                // Skip same edge and adjacent edges
                int j = edgeB.EdgeIndex;
                if (j <= i + 1) continue; // Already checked or adjacent
                if (i == 0 && j == polygon.Points.Count - 1) continue; // First and last are adjacent

                if (TryGetIntersection(startA, endA, edgeB.Start, edgeB.End,
                    out double alphaA, out double alphaB, out VXYZ intersection))
                {
                    results.Add(new SelfIntersection
                    {
                        Point = intersection,
                        EdgeIndex1 = i,
                        EdgeIndex2 = j,
                        Alpha1 = alphaA,
                        Alpha2 = alphaB
                    });
                }
            }
        }

        return results;
    }

    private static bool TryGetIntersection(VXYZ p1, VXYZ p2, VXYZ p3, VXYZ p4,
        out double alphaA, out double alphaB, out VXYZ intersection)
    {
        alphaA = 0;
        alphaB = 0;
        intersection = new VXYZ(0, 0);

        double d1x = p2.X - p1.X;
        double d1y = p2.Y - p1.Y;
        double d2x = p4.X - p3.X;
        double d2y = p4.Y - p3.Y;

        double cross = d1x * d2y - d1y * d2x;

        if (Math.Abs(cross) < GeometryTolerance.Epsilon)
            return false;

        double dx = p3.X - p1.X;
        double dy = p3.Y - p1.Y;

        alphaA = (dx * d2y - dy * d2x) / cross;
        alphaB = (dx * d1y - dy * d1x) / cross;

        const double margin = 1e-9;
        if (alphaA > margin && alphaA < 1 - margin &&
            alphaB > margin && alphaB < 1 - margin)
        {
            intersection = new VXYZ(p1.X + alphaA * d1x, p1.Y + alphaA * d1y);
            return true;
        }

        return false;
    }
}

/// <summary>
/// Represents an intersection between edges of two different polygons.
/// </summary>
internal class EdgeIntersection
{
    public VXYZ Point { get; set; } = new VXYZ(0, 0);
    public int EdgeIndexA { get; set; }
    public int EdgeIndexB { get; set; }
    public double AlphaA { get; set; }
    public double AlphaB { get; set; }
}

/// <summary>
/// Represents a self-intersection within a single polygon.
/// </summary>
internal class SelfIntersection
{
    public VXYZ Point { get; set; } = new VXYZ(0, 0);
    public int EdgeIndex1 { get; set; }
    public int EdgeIndex2 { get; set; }
    public double Alpha1 { get; set; }
    public double Alpha2 { get; set; }
}
