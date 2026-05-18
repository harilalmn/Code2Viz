namespace Code2Viz.Sketching;

/// <summary>
/// Base class for p5.js-style animation sketches.
/// Subclass and override <see cref="Setup"/> (called once) and <see cref="Draw"/> (called every frame).
/// Geometry inside Draw() is constructed using the C2VGeometry namespace and is recreated each frame.
/// </summary>
/// <remarks>
/// Persistent state lives in fields on the subclass. The runtime clears the registered shapes
/// between frames, so anything you want visible must be re-created (or kept as a field) in Draw().
/// </remarks>
public abstract class Sketch
{
    /// <summary>Frame number, starting at 0 for the first Draw() call.</summary>
    public int FrameCount { get; internal set; }

    /// <summary>Seconds elapsed since Setup() returned.</summary>
    public double ElapsedSeconds { get; internal set; }

    /// <summary>Seconds elapsed since the previous frame.</summary>
    public double DeltaSeconds { get; internal set; }

    /// <summary>Sketch logical width (set via <see cref="Size"/>); defaults to 800.</summary>
    public double Width { get; internal set; } = 800;

    /// <summary>Sketch logical height (set via <see cref="Size"/>); defaults to 600.</summary>
    public double Height { get; internal set; } = 600;

    /// <summary>Last known mouse X position in canvas world coordinates.</summary>
    public double MouseX { get; internal set; }

    /// <summary>Last known mouse Y position in canvas world coordinates.</summary>
    public double MouseY { get; internal set; }

    /// <summary>True while any mouse button is held over the canvas.</summary>
    public bool MousePressed { get; internal set; }

    /// <summary>True while any key is held with the canvas focused.</summary>
    public bool KeyPressed { get; internal set; }

    /// <summary>Name of the last key pressed.</summary>
    public string LastKey { get; internal set; } = "";

    /// <summary>Override to run one-time initialization before the frame loop starts.</summary>
    public virtual void Setup() { }

    /// <summary>Override to run code every frame.</summary>
    public virtual void Draw() { }

    /// <summary>
    /// Declares the sketch's logical drawing area. Centers a width×height region on origin
    /// (Y-up) and zooms the canvas to fit it on the first frame.
    /// </summary>
    protected void Size(double width, double height)
    {
        Width = width;
        Height = height;
        SketchRuntime.Instance.RequestZoomToBounds(width, height);
    }

    /// <summary>Sets the canvas background color for the running sketch.</summary>
    protected void Background(string color)
        => SketchRuntime.Instance.SetBackground(color);

    /// <summary>Pauses the frame loop until <see cref="Loop"/> is called.</summary>
    protected void NoLoop()
        => SketchRuntime.Instance.SetLooping(false);

    /// <summary>Resumes the frame loop after <see cref="NoLoop"/>.</summary>
    protected void Loop()
        => SketchRuntime.Instance.SetLooping(true);
}
