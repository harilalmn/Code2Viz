using System;
using System.Collections.Generic;

namespace C2VGeometry;

/// <summary>
/// A 2D KD-tree for fast nearest-neighbour queries.
/// Build: O(n log n), Query: O(log n) average.
/// </summary>
internal class KDTree<T>
{
    private readonly Func<T, double> _getX;
    private readonly Func<T, double> _getY;
    private KDNode? _root;

    private class KDNode
    {
        public T Item;
        public double SplitValue;
        public int Axis; // 0 = X, 1 = Y
        public KDNode? Left;
        public KDNode? Right;

        public KDNode(T item, double splitValue, int axis)
        {
            Item = item;
            SplitValue = splitValue;
            Axis = axis;
        }
    }

    public KDTree(Func<T, double> getX, Func<T, double> getY)
    {
        _getX = getX;
        _getY = getY;
    }

    /// <summary>
    /// Builds the tree from a collection of items. Call this once after all items are known.
    /// </summary>
    public void Build(IList<T> items)
    {
        var work = new T[items.Count];
        items.CopyTo(work, 0);
        _root = BuildRecursive(work, 0, work.Length - 1, 0);
    }

    private KDNode? BuildRecursive(T[] items, int left, int right, int depth)
    {
        if (left > right) return null;

        int axis = depth % 2;
        int mid = left + (right - left) / 2;

        // Partial sort to find median on the current axis
        NthElement(items, left, right, mid, axis);

        var node = new KDNode(items[mid], GetAxis(items[mid], axis), axis);
        node.Left = BuildRecursive(items, left, mid - 1, depth + 1);
        node.Right = BuildRecursive(items, mid + 1, right, depth + 1);
        return node;
    }

    /// <summary>
    /// Finds the nearest item to the given (x, y) point.
    /// </summary>
    public T Nearest(double x, double y)
    {
        if (_root == null)
            throw new InvalidOperationException("KDTree is empty. Call Build first.");

        T best = _root.Item;
        double bestDistSq = DistanceSquared(x, y, best);
        NearestRecursive(_root, x, y, ref best, ref bestDistSq);
        return best;
    }

    private void NearestRecursive(KDNode? node, double x, double y, ref T best, ref double bestDistSq)
    {
        if (node == null) return;

        double distSq = DistanceSquared(x, y, node.Item);
        if (distSq < bestDistSq)
        {
            bestDistSq = distSq;
            best = node.Item;
        }

        double queryVal = node.Axis == 0 ? x : y;
        double diff = queryVal - node.SplitValue;
        double diffSq = diff * diff;

        // Search the side the query point is on first
        var first = diff <= 0 ? node.Left : node.Right;
        var second = diff <= 0 ? node.Right : node.Left;

        NearestRecursive(first, x, y, ref best, ref bestDistSq);

        // Only search the other side if the splitting plane is closer than current best
        if (diffSq < bestDistSq)
        {
            NearestRecursive(second, x, y, ref best, ref bestDistSq);
        }
    }

    private double DistanceSquared(double x, double y, T item)
    {
        double dx = x - _getX(item);
        double dy = y - _getY(item);
        return dx * dx + dy * dy;
    }

    private double GetAxis(T item, int axis)
    {
        return axis == 0 ? _getX(item) : _getY(item);
    }

    /// <summary>
    /// Partial sort so that items[k] is the k-th smallest element on the given axis.
    /// Introselect-style using median-of-three quickselect.
    /// </summary>
    private void NthElement(T[] items, int left, int right, int k, int axis)
    {
        while (left < right)
        {
            // Median-of-three pivot selection
            int mid = left + (right - left) / 2;
            if (Compare(items[mid], items[left], axis) < 0) Swap(items, left, mid);
            if (Compare(items[right], items[left], axis) < 0) Swap(items, left, right);
            if (Compare(items[mid], items[right], axis) < 0) Swap(items, mid, right);
            double pivot = GetAxis(items[right], axis);

            int i = left;
            int j = right - 1;
            Swap(items, mid, right);

            while (true)
            {
                while (i <= j && GetAxis(items[i], axis) < pivot) i++;
                while (j >= i && GetAxis(items[j], axis) > pivot) j--;
                if (i >= j) break;
                Swap(items, i++, j--);
            }
            Swap(items, i, right);

            if (i == k) return;
            if (k < i) right = i - 1;
            else left = i + 1;
        }
    }

    private int Compare(T a, T b, int axis)
    {
        return GetAxis(a, axis).CompareTo(GetAxis(b, axis));
    }

    private static void Swap(T[] items, int i, int j)
    {
        (items[i], items[j]) = (items[j], items[i]);
    }
}
