using Code2Viz.Geometry;
using Code2Viz.Animation;

namespace Code2Viz.Canvas;

public class CanvasRenderer
{
    private static CanvasRenderer? _instance;
    private static readonly object _lock = new();

    private readonly List<IDrawable> _shapes = new();

    /// <summary>
    /// The currently active timeline for animation playback.
    /// </summary>
    public Timeline? ActiveTimeline { get; set; }

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
        // Prevent duplicate adds - check if shape is already placed
        if (shape is Shape s)
        {
            if (s.IsPlaced) return;
            s.IsPlaced = true;
        }
        _shapes.Add(shape);
    }

    public IReadOnlyList<IDrawable> GetShapes() => _shapes.AsReadOnly();

    public void Clear()
    {
        // Reset IsPlaced for all shapes so they can be re-added in next run
        foreach (var shape in _shapes)
        {
            if (shape is Shape s)
            {
                s.IsPlaced = false;
            }
        }
        _shapes.Clear();
        ActiveTimeline?.Stop();
        ActiveTimeline = null;
    }

    public void RenderTo(RenderCanvas canvas)
    {
        canvas.Render(_shapes);
        canvas.ZoomExtents(_shapes);
    }
}
