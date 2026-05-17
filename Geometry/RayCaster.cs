using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Code2Viz.Canvas;

namespace Code2Viz.Geometry;

/// <summary>
/// A successful ray-cast result: the shape that was hit, the world-space hit
/// point, and the distance from the ray origin.
/// </summary>
public readonly record struct RayHit(Shape Shape, VXYZ Point, double Distance);

/// <summary>
/// A single (origin, direction) ray query, used by the batch API.
/// </summary>
public readonly record struct RayQuery(VXYZ Origin, VXYZ Direction);

/// <summary>
/// Accelerated ray-casting against large 2D shape collections.
///
/// Builds an axis-aligned Bounding Volume Hierarchy (BVH) with Surface Area
/// Heuristic (SAH) splitting once at construction so each subsequent ray
/// query runs in O(log N) average time and scales to millions of shapes.
///
/// Implementation features:
///  - Flat <see cref="Node"/> struct array (no per-node heap objects).
///  - Iterative traversal with a stack-allocated index stack; the hot path
///    is allocation-free except for successful result tuples.
///  - SAH binning (12 bins) along the longest centroid axis for build quality.
///  - Inline ray-vs-shape math for <see cref="VLine"/>, <see cref="VCircle"/>,
///    <see cref="VArc"/>, <see cref="VEllipse"/>, <see cref="VPolygon"/>
///    (covers <see cref="VRectangle"/>), <see cref="VPolyline"/>. Other shape
///    types fall back to an AABB hit test.
///  - <see cref="Refit"/> recomputes node AABBs from each shape's current
///    bounds in O(N) without rebuilding the tree — use when shapes move
///    slightly between queries.
///  - Concurrent <see cref="FindIntersection"/>/<see cref="HasIntersection"/>
///    queries are safe because the BVH is read-only after construction;
///    <see cref="FindIntersections"/> exploits this with <see cref="Parallel.For"/>.
///
/// All queries operate in the XY plane — the Z components of the ray origin
/// and direction are ignored. Shapes with non-finite bounds (e.g. <see cref="VRay"/>,
/// <see cref="VXLine"/>) are silently excluded from the index.
/// </summary>
public class RayCaster
{
    // Per-shape AABB cache. Mutable so Refit() can update entries in place.
    private readonly Shape[] _shapes;
    private readonly double[] _minX;
    private readonly double[] _minY;
    private readonly double[] _maxX;
    private readonly double[] _maxY;
    private readonly int[] _orderedIndices;

    // Flat BVH. For internal nodes, LeftOrStart and RightOrCount are child
    // node indices; for leaves they are (start, count) into _orderedIndices.
    private struct Node
    {
        public double MinX, MinY, MaxX, MaxY;
        public int LeftOrStart;
        public int RightOrCount;
        public bool IsLeaf;
    }

    private const int SahBins = 12;
    private const int MaxDepth = 64;
    // Stack size = MaxDepth + a small safety margin.
    private const int TraversalStackSize = MaxDepth + 8;

    private readonly Node[] _nodes;
    private int _nodeCount;
    private readonly int _rootIndex;
    private readonly int _leafSize;

    /// <summary>Number of shapes indexed by this RayCaster.</summary>
    public int Count => _shapes.Length;

    /// <summary>
    /// Builds a ray-casting accelerator over all visible shapes currently on
    /// the canvas — every <see cref="Shape"/> in
    /// <c>CanvasRenderer.Instance.GetShapes()</c> with
    /// <see cref="Shape.IsVisible"/> set. The canvas state is snapshotted
    /// at construction: shapes added or removed afterwards are not
    /// reflected. Use <see cref="Refit"/> when indexed shapes move; build
    /// a new <see cref="RayCaster"/> when the scene changes structurally.
    /// </summary>
    /// <param name="leafSize">Maximum primitives per BVH leaf. Smaller values
    /// produce a deeper tree (more traversal, fewer leaf tests); larger values
    /// produce a shallower tree (less traversal, more leaf tests). 8 is a
    /// reasonable default for mixed shape sizes.</param>
    public RayCaster(int leafSize = 8)
    {
        _leafSize = leafSize < 1 ? 1 : leafSize;

        var keep = new List<Shape>();
        var bMinX = new List<double>();
        var bMinY = new List<double>();
        var bMaxX = new List<double>();
        var bMaxY = new List<double>();

        foreach (var drawable in CanvasRenderer.Instance.GetShapes())
        {
            if (drawable is not Shape s) continue;
            if (!s.IsVisible) continue;

            BoundingBox? bb;
            try { bb = s.GetBounds(); }
            catch { continue; }
            if (bb == null) continue;

            double xMin = bb.Min.X, yMin = bb.Min.Y, xMax = bb.Max.X, yMax = bb.Max.Y;
            if (!IsFiniteBox(xMin, yMin, xMax, yMax)) continue;

            keep.Add(s);
            bMinX.Add(xMin); bMinY.Add(yMin); bMaxX.Add(xMax); bMaxY.Add(yMax);
        }

        _shapes = keep.ToArray();
        int n = _shapes.Length;
        _minX = bMinX.ToArray();
        _minY = bMinY.ToArray();
        _maxX = bMaxX.ToArray();
        _maxY = bMaxY.ToArray();

        if (n == 0)
        {
            _orderedIndices = Array.Empty<int>();
            _nodes = Array.Empty<Node>();
            _nodeCount = 0;
            _rootIndex = -1;
            return;
        }

        _orderedIndices = new int[n];
        for (int i = 0; i < n; i++) _orderedIndices[i] = i;

        // A balanced binary tree over N leaves has at most 2N - 1 nodes.
        _nodes = new Node[2 * n];
        _nodeCount = 0;
        _rootIndex = Build(0, n, depth: 0);
    }

    // ========================================================================
    // Public query API
    // ========================================================================

    /// <summary>
    /// Casts a ray from <paramref name="location"/> in <paramref name="direction"/>
    /// and returns the closest shape it hits, or <c>null</c> if nothing is hit.
    /// </summary>
    /// <param name="exclusionList">Optional. Shapes in this list are ignored
    /// during the query — useful for skipping the source shape when casting
    /// from inside or off a known shape, or for "find the next hit past
    /// these" queries. Reference equality; converted internally to a
    /// <see cref="HashSet{Shape}"/> for O(1) lookup.</param>
    public RayHit? FindIntersection(VXYZ location, VXYZ direction,
                                    List<Shape>? exclusionList = null)
        => ClosestHit(location, direction, double.PositiveInfinity, exclusionList);

    /// <summary>
    /// Same as <see cref="FindIntersection(VXYZ, VXYZ, List{Shape}?)"/> but
    /// ignores any shape farther than <paramref name="maxDistance"/> from
    /// the origin. The cap is also used to prune BVH sub-trees during
    /// traversal.
    /// </summary>
    public RayHit? FindIntersection(VXYZ location, VXYZ direction, double maxDistance,
                                    List<Shape>? exclusionList = null)
        => ClosestHit(location, direction, maxDistance, exclusionList);

    /// <summary>
    /// Returns <c>true</c> as soon as any shape is found within
    /// <paramref name="maxDistance"/> of the ray origin. Faster than
    /// <see cref="FindIntersection(VXYZ, VXYZ, double)"/> for shadow-ray /
    /// "is anything blocking?" style queries because the traversal exits on
    /// the first hit and does not need to order children front-to-back.
    /// </summary>
    public bool HasIntersection(VXYZ location, VXYZ direction,
                                double maxDistance = double.PositiveInfinity)
        => AnyHit(location, direction, maxDistance);

    /// <summary>
    /// Casts a batch of rays. The returned array is the same length as
    /// <paramref name="queries"/>; entry <c>i</c> is the hit for query
    /// <c>i</c> or <c>null</c> if that ray missed.
    /// </summary>
    /// <param name="queries">Rays to cast.</param>
    /// <param name="parallel">When <c>true</c> (default) the queries are
    /// distributed across worker threads via <see cref="Parallel.For"/>.
    /// Pass <c>false</c> for deterministic single-threaded execution.</param>
    public RayHit?[] FindIntersections(IReadOnlyList<RayQuery> queries, bool parallel = true)
    {
        if (queries == null) throw new ArgumentNullException(nameof(queries));
        int n = queries.Count;
        var results = new RayHit?[n];

        if (parallel && n > 1)
        {
            Parallel.For(0, n, i =>
            {
                results[i] = FindIntersection(queries[i].Origin, queries[i].Direction);
            });
        }
        else
        {
            for (int i = 0; i < n; i++)
                results[i] = FindIntersection(queries[i].Origin, queries[i].Direction);
        }
        return results;
    }

    /// <summary>
    /// Recomputes every node's AABB from the current bounds of its shapes
    /// without rebuilding the tree topology. O(N), preserves traversal speed
    /// for small movements. Rebuild (allocate a new <see cref="RayCaster"/>)
    /// when the scene undergoes large changes.
    /// </summary>
    public void Refit()
    {
        if (_rootIndex < 0) return;
        RefitNode(_rootIndex);
    }

    // ========================================================================
    // Internal query implementation
    // ========================================================================

    private struct HitState
    {
        public double BestDistSq;
        public Shape? BestShape;
        public double HitX;
        public double HitY;
    }

    private RayHit? ClosestHit(VXYZ location, VXYZ direction, double maxDistance,
                               List<Shape>? exclusionList)
    {
        if (_rootIndex < 0 || location == null || direction == null) return null;

        double ox = location.X, oy = location.Y;
        double dx = direction.X, dy = direction.Y;
        double len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 1e-12) return null;
        dx /= len; dy /= len;
        double invDx = dx != 0 ? 1.0 / dx : double.PositiveInfinity;
        double invDy = dy != 0 ? 1.0 / dy : double.PositiveInfinity;

        double maxSq = double.IsPositiveInfinity(maxDistance)
            ? double.PositiveInfinity
            : (maxDistance > 0 ? maxDistance * maxDistance : 0);

        // Build a hash set once per query for O(1) per-shape exclusion checks.
        HashSet<Shape>? excluded = (exclusionList != null && exclusionList.Count > 0)
            ? new HashSet<Shape>(exclusionList)
            : null;

        HitState hit = default;
        hit.BestDistSq = maxSq;

        Span<int> stack = stackalloc int[TraversalStackSize];
        int sp = 0;
        stack[sp++] = _rootIndex;

        while (sp > 0)
        {
            int idx = stack[--sp];
            ref readonly Node node = ref _nodes[idx];

            if (!RayHitsAabb(ox, oy, invDx, invDy,
                    node.MinX, node.MinY, node.MaxX, node.MaxY, out double tNode)
                || tNode * tNode > hit.BestDistSq)
                continue;

            if (node.IsLeaf)
            {
                int s = node.LeftOrStart;
                int end = s + node.RightOrCount;
                for (int i = s; i < end; i++)
                {
                    int shapeIdx = _orderedIndices[i];
                    var shape = _shapes[shapeIdx];
                    if (excluded != null && excluded.Contains(shape)) continue;
                    if (!RayHitsAabb(ox, oy, invDx, invDy,
                            _minX[shapeIdx], _minY[shapeIdx],
                            _maxX[shapeIdx], _maxY[shapeIdx], out double tShape)
                        || tShape * tShape > hit.BestDistSq)
                        continue;
                    IntersectShape(shape, shapeIdx, ox, oy, dx, dy, ref hit);
                }
                continue;
            }

            int leftIdx = node.LeftOrStart;
            int rightIdx = node.RightOrCount;
            ref readonly Node L = ref _nodes[leftIdx];
            ref readonly Node R = ref _nodes[rightIdx];
            RayHitsAabb(ox, oy, invDx, invDy, L.MinX, L.MinY, L.MaxX, L.MaxY, out double tL);
            RayHitsAabb(ox, oy, invDx, invDy, R.MinX, R.MinY, R.MaxX, R.MaxY, out double tR);

            // Push the farther child first so the nearer one pops next and
            // shrinks BestDistSq before we ever look at the farther sub-tree.
            if (tL <= tR) { stack[sp++] = rightIdx; stack[sp++] = leftIdx; }
            else          { stack[sp++] = leftIdx;  stack[sp++] = rightIdx; }
        }

        if (hit.BestShape == null) return null;
        double dist = Math.Sqrt(hit.BestDistSq);
        return new RayHit(hit.BestShape, new VXYZ(hit.HitX, hit.HitY, 0), dist);
    }

    private bool AnyHit(VXYZ location, VXYZ direction, double maxDistance)
    {
        if (_rootIndex < 0 || location == null || direction == null) return false;

        double ox = location.X, oy = location.Y;
        double dx = direction.X, dy = direction.Y;
        double len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 1e-12) return false;
        dx /= len; dy /= len;
        double invDx = dx != 0 ? 1.0 / dx : double.PositiveInfinity;
        double invDy = dy != 0 ? 1.0 / dy : double.PositiveInfinity;

        double maxSq = double.IsPositiveInfinity(maxDistance)
            ? double.PositiveInfinity
            : (maxDistance > 0 ? maxDistance * maxDistance : 0);

        // Distance cap doubles as the per-shape acceptance threshold, so we
        // reuse the same HitState machinery — we just bail on the first hit.
        HitState hit = default;
        hit.BestDistSq = maxSq;

        Span<int> stack = stackalloc int[TraversalStackSize];
        int sp = 0;
        stack[sp++] = _rootIndex;

        while (sp > 0)
        {
            int idx = stack[--sp];
            ref readonly Node node = ref _nodes[idx];

            if (!RayHitsAabb(ox, oy, invDx, invDy,
                    node.MinX, node.MinY, node.MaxX, node.MaxY, out double tNode)
                || tNode * tNode > maxSq)
                continue;

            if (node.IsLeaf)
            {
                int s = node.LeftOrStart;
                int end = s + node.RightOrCount;
                for (int i = s; i < end; i++)
                {
                    int shapeIdx = _orderedIndices[i];
                    if (!RayHitsAabb(ox, oy, invDx, invDy,
                            _minX[shapeIdx], _minY[shapeIdx],
                            _maxX[shapeIdx], _maxY[shapeIdx], out double tShape)
                        || tShape * tShape > maxSq)
                        continue;
                    IntersectShape(_shapes[shapeIdx], shapeIdx, ox, oy, dx, dy, ref hit);
                    if (hit.BestShape != null) return true;
                }
                continue;
            }

            // Any-hit doesn't benefit from front-to-back ordering — push both children.
            stack[sp++] = node.LeftOrStart;
            stack[sp++] = node.RightOrCount;
        }
        return false;
    }

    /// <summary>
    /// Standard 2D ray-vs-AABB slab test. <paramref name="tEntry"/> is the
    /// parametric distance to the entry point, clamped to 0 when the origin
    /// lies inside the box.
    ///
    /// Handles the degenerate cases where the ray direction is zero on an
    /// axis (the IEEE <c>0 * ∞</c> in the naive form yields NaN, which would
    /// poison the comparison chain and cause spurious hits / wrong winners).
    /// When <c>invD</c> is infinite, the slab is satisfied iff the ray's
    /// origin already lies inside it.
    /// </summary>
    private static bool RayHitsAabb(double ox, double oy, double invDx, double invDy,
                                    double minX, double minY, double maxX, double maxY,
                                    out double tEntry)
    {
        double tx1, tx2;
        if (double.IsInfinity(invDx))
        {
            if (ox < minX || ox > maxX) { tEntry = double.PositiveInfinity; return false; }
            tx1 = double.NegativeInfinity;
            tx2 = double.PositiveInfinity;
        }
        else
        {
            tx1 = (minX - ox) * invDx;
            tx2 = (maxX - ox) * invDx;
        }

        double ty1, ty2;
        if (double.IsInfinity(invDy))
        {
            if (oy < minY || oy > maxY) { tEntry = double.PositiveInfinity; return false; }
            ty1 = double.NegativeInfinity;
            ty2 = double.PositiveInfinity;
        }
        else
        {
            ty1 = (minY - oy) * invDy;
            ty2 = (maxY - oy) * invDy;
        }

        double tMin = Math.Max(Math.Min(tx1, tx2), Math.Min(ty1, ty2));
        double tMax = Math.Min(Math.Max(tx1, tx2), Math.Max(ty1, ty2));

        if (tMax < 0 || tMax < tMin)
        {
            tEntry = double.PositiveInfinity;
            return false;
        }
        tEntry = tMin > 0 ? tMin : 0;
        return true;
    }

    // ========================================================================
    // BVH construction (SAH binning) and refit
    // ========================================================================

    private int Build(int start, int end, int depth)
    {
        int nodeIdx = _nodeCount++;
        ComputeBounds(start, end, out double minX, out double minY, out double maxX, out double maxY);

        _nodes[nodeIdx].MinX = minX;
        _nodes[nodeIdx].MinY = minY;
        _nodes[nodeIdx].MaxX = maxX;
        _nodes[nodeIdx].MaxY = maxY;
        _nodes[nodeIdx].IsLeaf = true;
        _nodes[nodeIdx].LeftOrStart = start;
        _nodes[nodeIdx].RightOrCount = end - start;

        int count = end - start;
        if (count <= _leafSize || depth >= MaxDepth)
            return nodeIdx;

        if (!TrySahSplit(start, end, minX, minY, maxX, maxY, out int splitMid))
            return nodeIdx;

        int leftIdx = Build(start, splitMid, depth + 1);
        int rightIdx = Build(splitMid, end, depth + 1);
        _nodes[nodeIdx].IsLeaf = false;
        _nodes[nodeIdx].LeftOrStart = leftIdx;
        _nodes[nodeIdx].RightOrCount = rightIdx;
        return nodeIdx;
    }

    /// <summary>
    /// Surface Area Heuristic split: bin shapes by centroid along the longest
    /// axis, then pick the bin boundary that minimises
    /// <c>perim(left) * count(left) + perim(right) * count(right)</c>. Falls
    /// back to no-split (return false → leaf) when no candidate beats the
    /// "keep as leaf" cost.
    /// </summary>
    private bool TrySahSplit(int start, int end,
                             double nodeMinX, double nodeMinY, double nodeMaxX, double nodeMaxY,
                             out int splitMid)
    {
        splitMid = -1;
        int count = end - start;

        // Bin along the longest centroid extent (not the bounds extent — those
        // can differ a lot when shapes are large but tightly clustered).
        ComputeCentroidExtent(start, end, out double cMinX, out double cMinY,
                                            out double cMaxX, out double cMaxY);
        double cExtX = cMaxX - cMinX;
        double cExtY = cMaxY - cMinY;
        int axis = cExtX >= cExtY ? 0 : 1;
        double k0 = axis == 0 ? cMinX : cMinY;
        double k1 = axis == 0 ? cMaxX : cMaxY;
        double extent = k1 - k0;
        if (extent < 1e-12) return false;

        Span<int> binCount = stackalloc int[SahBins];
        Span<double> binMinX = stackalloc double[SahBins];
        Span<double> binMinY = stackalloc double[SahBins];
        Span<double> binMaxX = stackalloc double[SahBins];
        Span<double> binMaxY = stackalloc double[SahBins];
        for (int i = 0; i < SahBins; i++)
        {
            binMinX[i] = double.PositiveInfinity; binMinY[i] = double.PositiveInfinity;
            binMaxX[i] = double.NegativeInfinity; binMaxY[i] = double.NegativeInfinity;
        }

        double invExt = SahBins / extent;
        for (int i = start; i < end; i++)
        {
            int idx = _orderedIndices[i];
            double cent = Centroid(idx, axis);
            int bin = (int)((cent - k0) * invExt);
            if (bin < 0) bin = 0;
            if (bin >= SahBins) bin = SahBins - 1;
            binCount[bin]++;
            if (_minX[idx] < binMinX[bin]) binMinX[bin] = _minX[idx];
            if (_minY[idx] < binMinY[bin]) binMinY[bin] = _minY[idx];
            if (_maxX[idx] > binMaxX[bin]) binMaxX[bin] = _maxX[idx];
            if (_maxY[idx] > binMaxY[bin]) binMaxY[bin] = _maxY[idx];
        }

        // Prefix scans (left side) and suffix scans (right side) of (count, perimeter).
        Span<int> leftCount = stackalloc int[SahBins];
        Span<double> leftPerim = stackalloc double[SahBins];
        Span<int> rightCount = stackalloc int[SahBins];
        Span<double> rightPerim = stackalloc double[SahBins];

        {
            double lMinX = double.PositiveInfinity, lMinY = double.PositiveInfinity;
            double lMaxX = double.NegativeInfinity, lMaxY = double.NegativeInfinity;
            int lc = 0;
            for (int i = 0; i < SahBins; i++)
            {
                if (binCount[i] > 0)
                {
                    if (binMinX[i] < lMinX) lMinX = binMinX[i];
                    if (binMinY[i] < lMinY) lMinY = binMinY[i];
                    if (binMaxX[i] > lMaxX) lMaxX = binMaxX[i];
                    if (binMaxY[i] > lMaxY) lMaxY = binMaxY[i];
                    lc += binCount[i];
                }
                leftCount[i] = lc;
                leftPerim[i] = Perimeter(lMinX, lMinY, lMaxX, lMaxY);
            }
        }
        {
            double rMinX = double.PositiveInfinity, rMinY = double.PositiveInfinity;
            double rMaxX = double.NegativeInfinity, rMaxY = double.NegativeInfinity;
            int rc = 0;
            for (int i = SahBins - 1; i >= 0; i--)
            {
                if (binCount[i] > 0)
                {
                    if (binMinX[i] < rMinX) rMinX = binMinX[i];
                    if (binMinY[i] < rMinY) rMinY = binMinY[i];
                    if (binMaxX[i] > rMaxX) rMaxX = binMaxX[i];
                    if (binMaxY[i] > rMaxY) rMaxY = binMaxY[i];
                    rc += binCount[i];
                }
                rightCount[i] = rc;
                rightPerim[i] = Perimeter(rMinX, rMinY, rMaxX, rMaxY);
            }
        }

        double bestCost = double.PositiveInfinity;
        int bestSplitBin = -1;
        for (int i = 0; i < SahBins - 1; i++)
        {
            int lc = leftCount[i];
            int rc = rightCount[i + 1];
            if (lc == 0 || rc == 0) continue;
            double cost = leftPerim[i] * lc + rightPerim[i + 1] * rc;
            if (cost < bestCost) { bestCost = cost; bestSplitBin = i; }
        }
        if (bestSplitBin < 0) return false;

        double parentPerim = Perimeter(nodeMinX, nodeMinY, nodeMaxX, nodeMaxY);
        double leafCost = parentPerim * count;
        if (bestCost >= leafCost) return false;

        // Partition shapes around the chosen bin boundary.
        double splitPos = k0 + (bestSplitBin + 1) * (extent / SahBins);
        int lo = start, hi = end - 1;
        while (lo <= hi)
        {
            if (Centroid(_orderedIndices[lo], axis) < splitPos) { lo++; }
            else if (Centroid(_orderedIndices[hi], axis) >= splitPos) { hi--; }
            else { Swap(lo, hi); lo++; hi--; }
        }
        int mid = lo;
        if (mid <= start || mid >= end) return false;
        splitMid = mid;
        return true;
    }

    private void ComputeBounds(int start, int end,
        out double minX, out double minY, out double maxX, out double maxY)
    {
        minX = double.PositiveInfinity; minY = double.PositiveInfinity;
        maxX = double.NegativeInfinity; maxY = double.NegativeInfinity;
        for (int i = start; i < end; i++)
        {
            int idx = _orderedIndices[i];
            if (_minX[idx] < minX) minX = _minX[idx];
            if (_minY[idx] < minY) minY = _minY[idx];
            if (_maxX[idx] > maxX) maxX = _maxX[idx];
            if (_maxY[idx] > maxY) maxY = _maxY[idx];
        }
    }

    private void ComputeCentroidExtent(int start, int end,
        out double minX, out double minY, out double maxX, out double maxY)
    {
        minX = double.PositiveInfinity; minY = double.PositiveInfinity;
        maxX = double.NegativeInfinity; maxY = double.NegativeInfinity;
        for (int i = start; i < end; i++)
        {
            int idx = _orderedIndices[i];
            double cx = 0.5 * (_minX[idx] + _maxX[idx]);
            double cy = 0.5 * (_minY[idx] + _maxY[idx]);
            if (cx < minX) minX = cx;
            if (cy < minY) minY = cy;
            if (cx > maxX) maxX = cx;
            if (cy > maxY) maxY = cy;
        }
    }

    private double Centroid(int idx, int axis) =>
        axis == 0 ? 0.5 * (_minX[idx] + _maxX[idx])
                  : 0.5 * (_minY[idx] + _maxY[idx]);

    private void Swap(int i, int j)
    {
        (_orderedIndices[i], _orderedIndices[j]) = (_orderedIndices[j], _orderedIndices[i]);
    }

    private static double Perimeter(double minX, double minY, double maxX, double maxY)
    {
        if (maxX < minX || maxY < minY) return 0;
        return 2.0 * ((maxX - minX) + (maxY - minY));
    }

    private void RefitNode(int nodeIdx)
    {
        ref Node node = ref _nodes[nodeIdx];
        if (node.IsLeaf)
        {
            int s = node.LeftOrStart;
            int end = s + node.RightOrCount;
            double minX = double.PositiveInfinity, minY = double.PositiveInfinity;
            double maxX = double.NegativeInfinity, maxY = double.NegativeInfinity;
            for (int i = s; i < end; i++)
            {
                int idx = _orderedIndices[i];
                BoundingBox? bb;
                try { bb = _shapes[idx].GetBounds(); }
                catch { bb = null; }
                if (bb != null)
                {
                    double xMin = bb.Min.X, yMin = bb.Min.Y, xMax = bb.Max.X, yMax = bb.Max.Y;
                    // Keep the stale AABB if the new one would corrupt the tree.
                    if (IsFiniteBox(xMin, yMin, xMax, yMax))
                    {
                        _minX[idx] = xMin; _minY[idx] = yMin;
                        _maxX[idx] = xMax; _maxY[idx] = yMax;
                    }
                }
                if (_minX[idx] < minX) minX = _minX[idx];
                if (_minY[idx] < minY) minY = _minY[idx];
                if (_maxX[idx] > maxX) maxX = _maxX[idx];
                if (_maxY[idx] > maxY) maxY = _maxY[idx];
            }
            node.MinX = minX; node.MinY = minY; node.MaxX = maxX; node.MaxY = maxY;
            return;
        }

        int leftIdx = node.LeftOrStart;
        int rightIdx = node.RightOrCount;
        RefitNode(leftIdx);
        RefitNode(rightIdx);
        ref Node L = ref _nodes[leftIdx];
        ref Node R = ref _nodes[rightIdx];
        node.MinX = Math.Min(L.MinX, R.MinX);
        node.MinY = Math.Min(L.MinY, R.MinY);
        node.MaxX = Math.Max(L.MaxX, R.MaxX);
        node.MaxY = Math.Max(L.MaxY, R.MaxY);
    }

    // ========================================================================
    // Inline per-shape ray intersection (allocation-free)
    // ========================================================================

    private const double ParamEpsilon = 1e-9;
    private const double DegenerateEpsilon = 1e-12;

    private void IntersectShape(Shape shape, int shapeIndex,
                                double ox, double oy, double dx, double dy,
                                ref HitState hit)
    {
        switch (shape)
        {
            case VLine line:
                IntersectSegment(line.Start.X, line.Start.Y, line.End.X, line.End.Y,
                                 ox, oy, dx, dy, line, ref hit);
                break;

            case VCircle circle:
                IntersectCircle(circle.Center.X, circle.Center.Y, circle.Radius,
                                ox, oy, dx, dy, circle, ref hit);
                break;

            case VArc arc:
                IntersectArc(arc, ox, oy, dx, dy, ref hit);
                break;

            case VEllipse ellipse:
                IntersectEllipse(ellipse, ox, oy, dx, dy, ref hit);
                break;

            case VPolygon polygon:
                IntersectVertexLoop(polygon.Points, closed: true,
                                    ox, oy, dx, dy, polygon, ref hit);
                break;

            case VPolyline polyline:
                IntersectVertexLoop(polyline.Points, closed: false,
                                    ox, oy, dx, dy, polyline, ref hit);
                break;

            default:
                IntersectAabb(shape, shapeIndex, ox, oy, dx, dy, ref hit);
                break;
        }
    }

    private static void IntersectSegment(double sx, double sy, double ex, double ey,
                                         double ox, double oy, double dx, double dy,
                                         Shape shape, ref HitState hit)
    {
        double rx = ex - sx, ry = ey - sy;
        double cross = dx * ry - dy * rx;
        if (Math.Abs(cross) < DegenerateEpsilon) return;

        double diffX = sx - ox, diffY = sy - oy;
        double t = (diffX * ry - diffY * rx) / cross;
        if (t < -ParamEpsilon) return;

        double u = (diffX * dy - diffY * dx) / cross;
        if (u < -ParamEpsilon || u > 1 + ParamEpsilon) return;

        if (t < 0) t = 0;
        double distSq = t * t;
        if (distSq >= hit.BestDistSq) return;

        hit.BestDistSq = distSq;
        hit.BestShape = shape;
        hit.HitX = ox + dx * t;
        hit.HitY = oy + dy * t;
    }

    private static void IntersectCircle(double cx, double cy, double r,
                                        double ox, double oy, double dx, double dy,
                                        Shape shape, ref HitState hit)
    {
        double fx = ox - cx, fy = oy - cy;
        double b = fx * dx + fy * dy;
        double c = fx * fx + fy * fy - r * r;
        double disc = b * b - c;
        if (disc < 0) return;

        double sqrtDisc = Math.Sqrt(disc);
        double t = -b - sqrtDisc;
        if (t < 0) t = -b + sqrtDisc;
        if (t < 0) return;

        double distSq = t * t;
        if (distSq >= hit.BestDistSq) return;

        hit.BestDistSq = distSq;
        hit.BestShape = shape;
        hit.HitX = ox + dx * t;
        hit.HitY = oy + dy * t;
    }

    private static void IntersectArc(VArc arc, double ox, double oy, double dx, double dy,
                                     ref HitState hit)
    {
        double cx = arc.Center.X, cy = arc.Center.Y, r = arc.Radius;
        double fx = ox - cx, fy = oy - cy;
        double b = fx * dx + fy * dy;
        double c = fx * fx + fy * fy - r * r;
        double disc = b * b - c;
        if (disc < 0) return;

        double sqrtDisc = Math.Sqrt(disc);
        TryAcceptArcRoot(arc, -b - sqrtDisc, ox, oy, dx, dy, cx, cy, ref hit);
        TryAcceptArcRoot(arc, -b + sqrtDisc, ox, oy, dx, dy, cx, cy, ref hit);
    }

    private static void TryAcceptArcRoot(VArc arc, double t,
                                         double ox, double oy, double dx, double dy,
                                         double cx, double cy, ref HitState hit)
    {
        if (t < 0) return;
        double distSq = t * t;
        if (distSq >= hit.BestDistSq) return;

        double hx = ox + dx * t;
        double hy = oy + dy * t;
        double angleDeg = Math.Atan2(hy - cy, hx - cx) * (180.0 / Math.PI);
        if (!IsAngleWithin(arc.StartAngle, arc.EndAngle, angleDeg)) return;

        hit.BestDistSq = distSq;
        hit.BestShape = arc;
        hit.HitX = hx;
        hit.HitY = hy;
    }

    private static void IntersectEllipse(VEllipse ellipse,
                                         double ox, double oy, double dx, double dy,
                                         ref HitState hit)
    {
        double rx = ellipse.RadiusX, ry = ellipse.RadiusY;
        if (rx <= 0 || ry <= 0) return;

        double cx = ellipse.Center.X, cy = ellipse.Center.Y;
        double scale = rx / ry;
        double lox = ox - cx;
        double loy = (oy - cy) * scale;
        double ldx = dx;
        double ldy = dy * scale;
        double dlen = Math.Sqrt(ldx * ldx + ldy * ldy);
        if (dlen < DegenerateEpsilon) return;
        ldx /= dlen; ldy /= dlen;

        double b = lox * ldx + loy * ldy;
        double c = lox * lox + loy * loy - rx * rx;
        double disc = b * b - c;
        if (disc < 0) return;

        double sqrtDisc = Math.Sqrt(disc);
        double tLocal = -b - sqrtDisc;
        if (tLocal < 0) tLocal = -b + sqrtDisc;
        if (tLocal < 0) return;

        double lhx = lox + ldx * tLocal;
        double lhy = loy + ldy * tLocal;
        double hx = lhx + cx;
        double hy = lhy / scale + cy;

        if (ellipse.StartAngle != 0 || ellipse.EndAngle != 360)
        {
            double angleDeg = Math.Atan2(lhy, lhx) * (180.0 / Math.PI);
            if (!IsAngleWithin(ellipse.StartAngle, ellipse.EndAngle, angleDeg)) return;
        }

        double rxw = hx - ox, ryw = hy - oy;
        double tWorld = rxw * dx + ryw * dy;
        if (tWorld < 0) return;
        double distSq = rxw * rxw + ryw * ryw;
        if (distSq >= hit.BestDistSq) return;

        hit.BestDistSq = distSq;
        hit.BestShape = ellipse;
        hit.HitX = hx;
        hit.HitY = hy;
    }

    private static void IntersectVertexLoop(List<VPoint> pts, bool closed,
                                            double ox, double oy, double dx, double dy,
                                            Shape shape, ref HitState hit)
    {
        int n = pts.Count;
        if (n < 2) return;

        int edgeCount = closed ? n : n - 1;
        for (int i = 0; i < edgeCount; i++)
        {
            var s = pts[i];
            var e = pts[i + 1 == n ? 0 : i + 1];
            IntersectSegment(s.X, s.Y, e.X, e.Y, ox, oy, dx, dy, shape, ref hit);
        }
    }

    private void IntersectAabb(Shape shape, int shapeIndex,
                               double ox, double oy, double dx, double dy,
                               ref HitState hit)
    {
        double invDx = dx != 0 ? 1.0 / dx : double.PositiveInfinity;
        double invDy = dy != 0 ? 1.0 / dy : double.PositiveInfinity;

        if (!RayHitsAabb(ox, oy, invDx, invDy,
                _minX[shapeIndex], _minY[shapeIndex],
                _maxX[shapeIndex], _maxY[shapeIndex], out double t))
            return;

        double distSq = t * t;
        if (distSq >= hit.BestDistSq) return;

        hit.BestDistSq = distSq;
        hit.BestShape = shape;
        hit.HitX = ox + dx * t;
        hit.HitY = oy + dy * t;
    }

    private static bool IsAngleWithin(double startDeg, double endDeg, double angleDeg)
    {
        double s = Normalize360(startDeg);
        double e = Normalize360(endDeg);
        double a = Normalize360(angleDeg);
        if (s <= e) return a >= s && a <= e;
        return a >= s || a <= e;
    }

    private static double Normalize360(double angleDeg)
    {
        angleDeg %= 360.0;
        if (angleDeg < 0) angleDeg += 360.0;
        return angleDeg;
    }

    private static bool IsFiniteBox(double minX, double minY, double maxX, double maxY)
    {
        return !(double.IsNaN(minX) || double.IsNaN(minY) ||
                 double.IsNaN(maxX) || double.IsNaN(maxY) ||
                 double.IsInfinity(minX) || double.IsInfinity(minY) ||
                 double.IsInfinity(maxX) || double.IsInfinity(maxY));
    }
}
