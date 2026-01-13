using Code2Viz.Geometry;

namespace Code2Viz.Canvas;

public class CanvasRenderer
{
    private static CanvasRenderer? _instance;
    private static readonly object _lock = new();

    private readonly List<IDrawable> _shapes = new();

    public static CanvasRenderer Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new CanvasRenderer();
                }
            }
            return _instance;
        }
    }

    private CanvasRenderer() { }

    public void AddShape(IDrawable shape)
    {
        _shapes.Add(shape);
    }

    public IReadOnlyList<IDrawable> GetShapes() => _shapes.AsReadOnly();

    public void Clear()
    {
        _shapes.Clear();
    }

    public void RenderTo(RenderCanvas canvas)
    {
        canvas.Render(_shapes);
        canvas.ZoomExtents(_shapes);
    }
}
