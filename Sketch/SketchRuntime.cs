using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using Code2Viz.Canvas;
using Code2Viz.Console;
using Geom = Code2Viz.Geometry;

namespace Code2Viz.Sketching;

/// <summary>
/// Drives the per-frame execution of a user <see cref="Sketch"/>.
/// <para>
/// Owns the <see cref="AssemblyLoadContext"/> for as long as the sketch is running so the
/// compiled user code survives across frames. Binds <see cref="C2VGeometry.Shape.DefaultRegistry"/>
/// to a per-frame collector and ships frame output to the WPF canvas via an adapter.
/// </para>
/// </summary>
public sealed class SketchRuntime
{
    public static SketchRuntime Instance { get; } = new();

    private Sketch? _active;
    private AssemblyLoadContext? _ctx;
    private readonly C2VGeometryRegistry _registry = new();
    private readonly Stopwatch _clock = new();
    private double _lastFrameSec;
    private int _frame;
    private bool _looping = true;
    private bool _zoomRequested;
    private double _zoomWidth, _zoomHeight;
    private string? _background;
    private bool _backgroundChanged;

    /// <summary>True while a sketch is active and its frame loop should tick.</summary>
    public bool IsRunning => _active != null;

    /// <summary>The currently active sketch, or null when stopped.</summary>
    public Sketch? Active => _active;

    private SketchRuntime() { }

    /// <summary>
    /// Returns true once after the sketch called <see cref="Sketch.Size"/>; the caller should
    /// zoom the canvas to fit. Subsequent calls return false until Size() is invoked again.
    /// </summary>
    public bool TryConsumeZoomRequest()
    {
        if (!_zoomRequested) return false;
        _zoomRequested = false;
        return true;
    }

    /// <summary>
    /// Returns the most recent <see cref="Sketch.Background"/> request, or null if none is pending.
    /// Clears the pending value.
    /// </summary>
    public string? TryConsumeBackground()
    {
        if (!_backgroundChanged) return null;
        _backgroundChanged = false;
        return _background;
    }

    /// <summary>
    /// Activates a sketch: instantiates the type, calls Setup(), and arms the frame loop.
    /// Takes ownership of the load context — it will be unloaded on <see cref="Stop"/>.
    /// </summary>
    public void Start(Type sketchType, AssemblyLoadContext ctx)
    {
        Stop();

        // Bind C2VGeometry auto-registration to our per-frame collector
        C2VGeometry.Shape.DefaultRegistry = _registry;
        C2VGeometry.Shape.AutoRegister = true;
        C2VGeometry.Shape.ResetIdCounter();

        try
        {
            _active = (Sketch)Activator.CreateInstance(sketchType)!;
        }
        catch (Exception ex)
        {
            ReportError(ex, "Sketch construction");
            C2VGeometry.Shape.DefaultRegistry = null;
            try { ctx.Unload(); } catch { /* Default ALC etc */ }
            return;
        }

        _ctx = ctx;
        _frame = 0;
        _lastFrameSec = 0;
        _looping = true;
        _clock.Restart();

        ConsoleOutput.Instance.WriteLine("Sketch", 0,
            $"Sketch started: {sketchType.FullName}. Press Stop to end.");

        try
        {
            PopulateState(0);
            _active.Setup();
            FlushFrame();
        }
        catch (Exception ex)
        {
            ReportError(ex, "Setup");
            Stop();
        }
    }

    /// <summary>
    /// Advances the sketch by one frame. Called from the WPF rendering loop while
    /// <see cref="IsRunning"/> is true. No-op when paused via <see cref="Sketch.NoLoop"/>.
    /// </summary>
    public void Tick()
    {
        if (_active == null || !_looping) return;

        var now = _clock.Elapsed.TotalSeconds;
        var dt = now - _lastFrameSec;
        _lastFrameSec = now;
        _frame++;

        PopulateState(dt);

        try
        {
            _active.Draw();
            FlushFrame();
        }
        catch (Exception ex)
        {
            ReportError(ex, $"Draw (frame {_frame})");
            Stop();
        }
    }

    /// <summary>
    /// Stops the active sketch, clears the canvas, and unloads the user assembly's load context.
    /// Safe to call when no sketch is running.
    /// </summary>
    public void Stop()
    {
        if (_active == null && _ctx == null) return;

        _active = null;
        C2VGeometry.Shape.DefaultRegistry = null;
        _registry.Clear();
        CanvasRenderer.Instance.Clear();
        _clock.Reset();
        _frame = 0;
        _looping = true;
        _zoomRequested = false;
        _backgroundChanged = false;
        _background = null;

        if (_ctx != null)
        {
            try { _ctx.Unload(); } catch { /* defensive — already unloaded */ }
            _ctx = null;
        }
    }

    /// <summary>Updates the polled input state available to the sketch (mouse pos, buttons, keys).</summary>
    public void UpdateInputState(double mouseX, double mouseY, bool mousePressed, bool keyPressed, string lastKey)
    {
        if (_active == null) return;
        _active.MouseX = mouseX;
        _active.MouseY = mouseY;
        _active.MousePressed = mousePressed;
        _active.KeyPressed = keyPressed;
        _active.LastKey = lastKey ?? "";
    }

    internal void SetBackground(string color)
    {
        _background = color;
        _backgroundChanged = true;
    }

    internal void RequestZoomToBounds(double w, double h)
    {
        _zoomRequested = true;
        _zoomWidth = w;
        _zoomHeight = h;
    }

    internal void SetLooping(bool v) => _looping = v;

    private void PopulateState(double dt)
    {
        if (_active == null) return;
        _active.FrameCount = _frame;
        _active.ElapsedSeconds = _clock.Elapsed.TotalSeconds;
        _active.DeltaSeconds = dt;
    }

    private void FlushFrame()
    {
        if (_active == null) return;

        CanvasRenderer.Instance.Clear();

        // DrainConverted constructs Geom shapes that auto-register with CanvasRenderer.Instance
        foreach (var _ in _registry.DrainConverted()) { /* iteration is the work */ }

        // Boundary rectangle showing the sketch area. Color picked to be visible against
        // both light and dark canvas backgrounds.
        var halfW = _active.Width / 2;
        var halfH = _active.Height / 2;
        // Construction auto-registers with the canvas
        _ = new Geom.VRectangle(new Geom.VPoint(-halfW, -halfH), _active.Width, _active.Height)
        {
            Color = "#888888",
            FillColor = "Transparent",
            LineWeight = 1,
            Name = "_sketchBounds"
        };

        // Note: TryConsumeZoomRequest / TryConsumeBackground are pulled by the frame-loop
        // host (MainWindow). They are not cleared here so a single Setup()-time call survives
        // until the host has a chance to apply it.
    }

    private static void ReportError(Exception ex, string phase)
    {
        ConsoleOutput.Instance.WriteLine("Sketch", 0, $"{phase} error: {ex.Message}");
        if (ex.StackTrace != null)
        {
            foreach (var line in ex.StackTrace.Split('\n'))
            {
                var match = System.Text.RegularExpressions.Regex.Match(
                    line, @"in\s+(.+\.cs):line\s+(\d+)");
                if (match.Success)
                {
                    ConsoleOutput.Instance.WriteLine("Sketch", 0,
                        $"  at {System.IO.Path.GetFileName(match.Groups[1].Value)}:{match.Groups[2].Value}");
                }
            }
        }
    }
}
