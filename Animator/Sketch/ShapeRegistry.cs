using System.Collections.Generic;

namespace Animator.Sketching;

/// <summary>
/// Per-frame collector for <see cref="C2VGeometry.Shape"/> instances constructed inside Draw().
/// Bound to <see cref="C2VGeometry.Shape.DefaultRegistry"/> while a sketch is running so every
/// shape the user creates is captured here; the runtime drains and renders it each frame.
/// </summary>
public sealed class ShapeRegistry : C2VGeometry.IShapeRegistry
{
    private readonly List<C2VGeometry.Shape> _shapes = new();
    private readonly object _gate = new();

    public IReadOnlyList<C2VGeometry.Shape> Snapshot()
    {
        lock (_gate) return _shapes.ToArray();
    }

    public int Count
    {
        get { lock (_gate) return _shapes.Count; }
    }

    public void Register(C2VGeometry.Shape shape)
    {
        lock (_gate) _shapes.Add(shape);
    }

    public void Unregister(C2VGeometry.Shape shape)
    {
        lock (_gate) _shapes.Remove(shape);
    }

    public void MoveAbove(C2VGeometry.Shape shape, C2VGeometry.Shape referenceShape)
    {
        lock (_gate)
        {
            int refIdx = _shapes.IndexOf(referenceShape);
            if (refIdx < 0) return;
            if (!_shapes.Remove(shape)) return;
            refIdx = _shapes.IndexOf(referenceShape);
            _shapes.Insert(refIdx + 1, shape);
        }
    }

    public void MoveBehind(C2VGeometry.Shape shape, C2VGeometry.Shape referenceShape)
    {
        lock (_gate)
        {
            int refIdx = _shapes.IndexOf(referenceShape);
            if (refIdx < 0) return;
            if (!_shapes.Remove(shape)) return;
            refIdx = _shapes.IndexOf(referenceShape);
            _shapes.Insert(refIdx, shape);
        }
    }

    public void Clear()
    {
        lock (_gate) _shapes.Clear();
    }
}
