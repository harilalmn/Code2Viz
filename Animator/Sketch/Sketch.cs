namespace Animator.Sketching;

/// <summary>
/// Base class for p5.js-style animation sketches in Animator.
/// Override <see cref="Setup"/> (runs once) and <see cref="Draw"/> (runs every frame).
/// Geometry is created with the C2VGeometry namespace.
/// </summary>
public abstract class Sketch
{
    /// <summary>Frame number — 0 during Setup, increments before each Draw call.</summary>
    public int FrameCount { get; internal set; }

    /// <summary>Seconds elapsed since Setup() returned.</summary>
    public double ElapsedSeconds { get; internal set; }

    /// <summary>Seconds since the previous Draw call.</summary>
    public double DeltaSeconds { get; internal set; }

    /// <summary>Sketch logical width (set via <see cref="Size"/>).</summary>
    public double Width { get; internal set; } = 800;

    /// <summary>Sketch logical height (set via <see cref="Size"/>).</summary>
    public double Height { get; internal set; } = 600;

    /// <summary>Last known mouse position in world coordinates (Y-up).</summary>
    public double MouseX { get; internal set; }
    public double MouseY { get; internal set; }
    public bool MousePressed { get; internal set; }
    public bool KeyPressed { get; internal set; }
    public string LastKey { get; internal set; } = "";

    public virtual void Setup() { }
    public virtual void Draw() { }

    /// <summary>
    /// Defines the sketch's logical drawing area. The canvas zooms to fit a
    /// width×height region centered on origin on the first frame.
    /// </summary>
    protected void Size(double width, double height)
    {
        Width = width;
        Height = height;
        SketchRuntime.Instance.RequestZoomToBounds(width, height);
    }

    /// <summary>Sets the canvas background color (WPF named color or "#RRGGBB").</summary>
    protected void Background(string color)
        => SketchRuntime.Instance.SetBackground(color);

    /// <summary>Pauses the frame loop until <see cref="Loop"/>.</summary>
    protected void NoLoop() => SketchRuntime.Instance.SetLooping(false);

    /// <summary>Resumes the frame loop.</summary>
    protected void Loop() => SketchRuntime.Instance.SetLooping(true);
}
