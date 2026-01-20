using Code2Viz.Geometry;

namespace Code2Viz.Canvas;

/// <summary>
/// Axis-Aligned Bounding Box for spatial queries.
/// </summary>
public struct AABB
{
    public double MinX;
    public double MinY;
    public double MaxX;
    public double MaxY;

    public AABB(double minX, double minY, double maxX, double maxY)
    {
        MinX = minX;
        MinY = minY;
        MaxX = maxX;
        MaxY = maxY;
    }

    /// <summary>
    /// Checks if this AABB intersects with another AABB.
    /// </summary>
    public readonly bool Intersects(AABB other)
    {
        return MinX <= other.MaxX && MaxX >= other.MinX &&
               MinY <= other.MaxY && MaxY >= other.MinY;
    }

    /// <summary>
    /// Checks if this AABB contains a point.
    /// </summary>
    public readonly bool Contains(double x, double y)
    {
        return x >= MinX && x <= MaxX && y >= MinY && y <= MaxY;
    }

    /// <summary>
    /// Checks if this AABB fully contains another AABB.
    /// </summary>
    public readonly bool Contains(AABB other)
    {
        return other.MinX >= MinX && other.MaxX <= MaxX &&
               other.MinY >= MinY && other.MaxY <= MaxY;
    }

    /// <summary>
    /// Creates an AABB from a Shape, including its offset.
    /// </summary>
    public static AABB FromShape(Shape shape)
    {
        var bounds = shape.GetBounds();
        return new AABB(
            bounds.min.X + shape.OffsetX,
            bounds.min.Y + shape.OffsetY,
            bounds.max.X + shape.OffsetX,
            bounds.max.Y + shape.OffsetY
        );
    }

    /// <summary>
    /// Expands this AABB to include another AABB.
    /// </summary>
    public void Expand(AABB other)
    {
        MinX = Math.Min(MinX, other.MinX);
        MinY = Math.Min(MinY, other.MinY);
        MaxX = Math.Max(MaxX, other.MaxX);
        MaxY = Math.Max(MaxY, other.MaxY);
    }

    /// <summary>
    /// Returns the center X coordinate.
    /// </summary>
    public readonly double CenterX => (MinX + MaxX) / 2;

    /// <summary>
    /// Returns the center Y coordinate.
    /// </summary>
    public readonly double CenterY => (MinY + MaxY) / 2;

    /// <summary>
    /// Returns the width of this AABB.
    /// </summary>
    public readonly double Width => MaxX - MinX;

    /// <summary>
    /// Returns the height of this AABB.
    /// </summary>
    public readonly double Height => MaxY - MinY;
}

/// <summary>
/// QuadTree spatial index for efficient viewport culling.
/// Provides O(log n + k) query performance instead of O(n).
/// </summary>
public class QuadTree
{
    private const int MaxCapacity = 8;
    private const int MaxDepth = 10;

    private readonly AABB _bounds;
    private readonly int _depth;
    private readonly List<(IDrawable item, AABB bounds)> _items;
    private QuadTree? _nw, _ne, _sw, _se;
    private bool _isSubdivided;

    /// <summary>
    /// Creates a new QuadTree with the specified bounds.
    /// </summary>
    public QuadTree(AABB bounds, int depth = 0)
    {
        _bounds = bounds;
        _depth = depth;
        _items = new List<(IDrawable, AABB)>(MaxCapacity);
        _isSubdivided = false;
    }

    /// <summary>
    /// Inserts an item with its bounding box into the QuadTree.
    /// </summary>
    public void Insert(IDrawable item, AABB itemBounds)
    {
        // Check if the item intersects this node's bounds
        if (!_bounds.Intersects(itemBounds))
            return;

        // If we have children, try to insert into them
        if (_isSubdivided)
        {
            InsertIntoChildren(item, itemBounds);
            return;
        }

        // Add to this node
        _items.Add((item, itemBounds));

        // Subdivide if we've exceeded capacity and haven't reached max depth
        if (_items.Count > MaxCapacity && _depth < MaxDepth)
        {
            Subdivide();

            // Re-insert all items into children
            var oldItems = _items.ToList();
            _items.Clear();

            foreach (var (oldItem, oldBounds) in oldItems)
            {
                InsertIntoChildren(oldItem, oldBounds);
            }
        }
    }

    private void InsertIntoChildren(IDrawable item, AABB itemBounds)
    {
        // Item may span multiple quadrants, so insert into all that it intersects
        _nw?.Insert(item, itemBounds);
        _ne?.Insert(item, itemBounds);
        _sw?.Insert(item, itemBounds);
        _se?.Insert(item, itemBounds);
    }

    private void Subdivide()
    {
        var halfWidth = _bounds.Width / 2;
        var halfHeight = _bounds.Height / 2;
        var cx = _bounds.CenterX;
        var cy = _bounds.CenterY;

        _nw = new QuadTree(new AABB(_bounds.MinX, cy, cx, _bounds.MaxY), _depth + 1);
        _ne = new QuadTree(new AABB(cx, cy, _bounds.MaxX, _bounds.MaxY), _depth + 1);
        _sw = new QuadTree(new AABB(_bounds.MinX, _bounds.MinY, cx, cy), _depth + 1);
        _se = new QuadTree(new AABB(cx, _bounds.MinY, _bounds.MaxX, cy), _depth + 1);

        _isSubdivided = true;
    }

    /// <summary>
    /// Queries the QuadTree for all items that intersect the given viewport.
    /// </summary>
    public void Query(AABB viewport, HashSet<IDrawable> results)
    {
        // Check if this node intersects the viewport
        if (!_bounds.Intersects(viewport))
            return;

        // Add all items in this node that intersect the viewport
        foreach (var (item, itemBounds) in _items)
        {
            if (viewport.Intersects(itemBounds))
            {
                results.Add(item);
            }
        }

        // Query children if subdivided
        if (_isSubdivided)
        {
            _nw?.Query(viewport, results);
            _ne?.Query(viewport, results);
            _sw?.Query(viewport, results);
            _se?.Query(viewport, results);
        }
    }

    /// <summary>
    /// Queries the QuadTree and returns a list of visible items.
    /// </summary>
    public List<IDrawable> Query(AABB viewport)
    {
        var results = new HashSet<IDrawable>();
        Query(viewport, results);
        return results.ToList();
    }

    /// <summary>
    /// Clears all items from the QuadTree.
    /// </summary>
    public void Clear()
    {
        _items.Clear();
        _nw = _ne = _sw = _se = null;
        _isSubdivided = false;
    }

    /// <summary>
    /// Creates a QuadTree from a collection of shapes, automatically computing bounds.
    /// </summary>
    public static QuadTree? FromShapes(IEnumerable<IDrawable> shapes)
    {
        var shapeList = shapes.ToList();
        if (shapeList.Count == 0)
            return null;

        // Calculate world bounds
        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;

        var shapeBounds = new List<(IDrawable shape, AABB bounds)>();

        foreach (var drawable in shapeList)
        {
            if (drawable is Shape shape)
            {
                var bounds = AABB.FromShape(shape);
                shapeBounds.Add((drawable, bounds));

                minX = Math.Min(minX, bounds.MinX);
                minY = Math.Min(minY, bounds.MinY);
                maxX = Math.Max(maxX, bounds.MaxX);
                maxY = Math.Max(maxY, bounds.MaxY);
            }
        }

        if (shapeBounds.Count == 0)
            return null;

        // Add padding to bounds
        var padding = Math.Max(maxX - minX, maxY - minY) * 0.1;
        if (padding < 100) padding = 100;

        var worldBounds = new AABB(
            minX - padding,
            minY - padding,
            maxX + padding,
            maxY + padding
        );

        var quadTree = new QuadTree(worldBounds);

        foreach (var (shape, bounds) in shapeBounds)
        {
            quadTree.Insert(shape, bounds);
        }

        return quadTree;
    }
}
