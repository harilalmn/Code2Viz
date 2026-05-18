using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using Animator.Canvas;
using Animator.Console;

namespace Animator.Sketching;

/// <summary>
/// Drives the per-frame execution of a user <see cref="Sketch"/>.
/// Owns the <see cref="AssemblyLoadContext"/> while the sketch is running so the user
/// assembly stays resident across frames. Binds <see cref="C2VGeometry.Shape.DefaultRegistry"/>
/// to a per-frame collector and forwards each frame's shapes to the active canvas.
/// </summary>
public sealed class SketchRuntime
{
    public static SketchRuntime Instance { get; } = new();

    private Sketch? _active;
    private AssemblyLoadContext? _ctx;
    private readonly ShapeRegistry _registry = new();
    private readonly Stopwatch _clock = new();
    private double _lastFrameSec;
    private int _frame;
    private bool _looping = true;
    private bool _zoomRequested;
    private double _zoomWidth, _zoomHeight;
    private string? _background;
    private bool _backgroundChanged;

    public bool IsRunning => _active != null;
    public Sketch? Active => _active;
    public ShapeRegistry Registry => _registry;

    /// <summary>Optional callback fired with the snapshotted frame shapes after each tick.</summary>
    public event Action<IReadOnlyList<C2VGeometry.Shape>>? FrameProduced;

    private SketchRuntime() { }

    public bool TryConsumeZoomRequest(out double width, out double height)
    {
        width = _zoomWidth; height = _zoomHeight;
        if (!_zoomRequested) return false;
        _zoomRequested = false;
        return true;
    }

    public string? TryConsumeBackground()
    {
        if (!_backgroundChanged) return null;
        _backgroundChanged = false;
        return _background;
    }

    public void Start(Type sketchType, AssemblyLoadContext ctx)
    {
        Stop();

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
            try { ctx.Unload(); } catch { /* Default ALC etc. */ }
            return;
        }

        _ctx = ctx;
        _frame = 0;
        _lastFrameSec = 0;
        _looping = true;
        _clock.Restart();

        ConsoleOutput.Instance.WriteLine("Sketch",
            $"Started: {sketchType.FullName}. Press Stop to end.");

        try
        {
            PopulateTimeState(0);
            _registry.Clear();
            _active.Setup();
            EmitFrame();
        }
        catch (Exception ex)
        {
            ReportError(ex, "Setup");
            Stop();
        }
    }

    public void Tick()
    {
        if (_active == null || !_looping) return;

        var now = _clock.Elapsed.TotalSeconds;
        var dt = now - _lastFrameSec;
        _lastFrameSec = now;
        _frame++;

        _registry.Clear();
        PopulateTimeState(dt);

        try
        {
            _active.Draw();
            EmitFrame();
        }
        catch (Exception ex)
        {
            ReportError(ex, $"Draw (frame {_frame})");
            Stop();
        }
    }

    public void Stop()
    {
        if (_active == null && _ctx == null) return;

        _active = null;
        C2VGeometry.Shape.DefaultRegistry = null;
        _registry.Clear();
        _clock.Reset();
        _frame = 0;
        _looping = true;
        _zoomRequested = false;
        _backgroundChanged = false;
        _background = null;

        if (_ctx != null)
        {
            try { _ctx.Unload(); } catch { /* Default ALC etc. */ }
            _ctx = null;
        }

        // Tell the canvas to clear by emitting an empty frame.
        FrameProduced?.Invoke(Array.Empty<C2VGeometry.Shape>());
    }

    public void UpdateInputState(double mouseX, double mouseY, bool mousePressed, bool keyPressed, string lastKey)
    {
        if (_active == null) return;
        _active.MouseX = mouseX;
        _active.MouseY = mouseY;
        _active.MousePressed = mousePressed;
        _active.KeyPressed = keyPressed;
        _active.LastKey = lastKey ?? "";
    }

    internal void SetBackground(string c)
    {
        _background = c;
        _backgroundChanged = true;
    }

    internal void RequestZoomToBounds(double w, double h)
    {
        _zoomRequested = true;
        _zoomWidth = w;
        _zoomHeight = h;
    }

    internal void SetLooping(bool v) => _looping = v;

    private void PopulateTimeState(double dt)
    {
        if (_active == null) return;
        _active.FrameCount = _frame;
        _active.ElapsedSeconds = _clock.Elapsed.TotalSeconds;
        _active.DeltaSeconds = dt;
    }

    private void EmitFrame()
    {
        var snap = _registry.Snapshot();
        FrameProduced?.Invoke(snap);
    }

    private static void ReportError(Exception ex, string phase)
    {
        ConsoleOutput.Instance.WriteError("Sketch", $"{phase} error: {ex.Message}");
        if (ex.StackTrace != null)
        {
            foreach (var line in ex.StackTrace.Split('\n'))
            {
                var match = System.Text.RegularExpressions.Regex.Match(
                    line, @"in\s+(.+\.cs):line\s+(\d+)");
                if (match.Success)
                    ConsoleOutput.Instance.WriteError("Sketch",
                        $"  at {System.IO.Path.GetFileName(match.Groups[1].Value)}:{match.Groups[2].Value}");
            }
        }
    }
}
