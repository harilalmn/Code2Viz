using System.Collections.Generic;
using Code2Viz.Geometry;

namespace Code2Viz.Sketching;

/// <summary>
/// Per-frame collector for <see cref="C2VGeometry.Shape"/> instances created inside a sketch's Draw().
/// <para>
/// Bound to <see cref="C2VGeometry.Shape.DefaultRegistry"/> while a sketch is running so that
/// every shape constructed by the user is captured here. At the end of each frame the runtime
/// calls <see cref="DrainConverted"/> to convert and ship them to the WPF canvas, then clears
/// the collection for the next frame.
/// </para>
/// </summary>
public sealed class C2VGeometryRegistry : C2VGeometry.IShapeRegistry
{
    private readonly List<C2VGeometry.Shape> _frameShapes = new();

    public IReadOnlyList<C2VGeometry.Shape> FrameShapes => _frameShapes;

    public void Register(C2VGeometry.Shape shape) => _frameShapes.Add(shape);

    public void Unregister(C2VGeometry.Shape shape) => _frameShapes.Remove(shape);

    public void MoveAbove(C2VGeometry.Shape shape, C2VGeometry.Shape referenceShape)
    {
        int refIndex = _frameShapes.IndexOf(referenceShape);
        if (refIndex < 0) return;
        if (!_frameShapes.Remove(shape)) return;
        refIndex = _frameShapes.IndexOf(referenceShape);
        _frameShapes.Insert(refIndex + 1, shape);
    }

    public void MoveBehind(C2VGeometry.Shape shape, C2VGeometry.Shape referenceShape)
    {
        int refIndex = _frameShapes.IndexOf(referenceShape);
        if (refIndex < 0) return;
        if (!_frameShapes.Remove(shape)) return;
        refIndex = _frameShapes.IndexOf(referenceShape);
        _frameShapes.Insert(refIndex, shape);
    }

    public void Clear() => _frameShapes.Clear();

    /// <summary>
    /// Yields one <see cref="IDrawable"/> per registered shape (skipping any whose adapter is
    /// unsupported) and clears the frame collection.
    /// </summary>
    public IEnumerable<IDrawable> DrainConverted()
    {
        var snapshot = _frameShapes.ToArray();
        _frameShapes.Clear();
        foreach (var src in snapshot)
        {
            var dst = C2VGeometryAdapter.Convert(src);
            if (dst != null) yield return dst;
        }
    }
}
