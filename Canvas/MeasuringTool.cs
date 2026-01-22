using Code2Viz.Geometry;

namespace Code2Viz.Canvas;

/// <summary>
/// Current mode of the canvas tool.
/// </summary>
public enum ToolMode
{
    None,
    Measuring
}

/// <summary>
/// Manages the measuring tape tool state and measurement logic.
/// </summary>
public class MeasuringTool
{
    /// <summary>
    /// Current tool mode.
    /// </summary>
    public ToolMode Mode { get; private set; } = ToolMode.None;

    /// <summary>
    /// First point of the measurement (null if not yet set).
    /// </summary>
    public VPoint? FirstPoint { get; private set; }

    /// <summary>
    /// Current cursor position in world coordinates.
    /// </summary>
    public VPoint? CurrentPoint { get; private set; }

    /// <summary>
    /// Current snap result (if any).
    /// </summary>
    public SnapResult? CurrentSnap { get; private set; }

    /// <summary>
    /// The snap engine used for detecting snap points.
    /// </summary>
    public SnapEngine SnapEngine { get; }

    /// <summary>
    /// Last measured distance (null if no complete measurement).
    /// </summary>
    public double? LastMeasuredDistance { get; private set; }

    /// <summary>
    /// Event raised when measurement is completed.
    /// </summary>
    public event EventHandler<double>? MeasurementCompleted;

    /// <summary>
    /// Event raised when tool mode changes.
    /// </summary>
    public event EventHandler<ToolMode>? ModeChanged;

    public MeasuringTool()
    {
        SnapEngine = new SnapEngine();
        SnapEngine.SyncFromSettings();
    }

    /// <summary>
    /// Starts the measuring tool.
    /// </summary>
    public void StartMeasuring()
    {
        Mode = ToolMode.Measuring;
        FirstPoint = null;
        CurrentPoint = null;
        CurrentSnap = null;
        LastMeasuredDistance = null;
        ModeChanged?.Invoke(this, Mode);
    }

    /// <summary>
    /// Cancels the measuring operation and returns to normal mode.
    /// </summary>
    public void CancelMeasuring()
    {
        Mode = ToolMode.None;
        FirstPoint = null;
        CurrentPoint = null;
        CurrentSnap = null;
        SnapEngine.ReferencePoint = null;
        ModeChanged?.Invoke(this, Mode);
    }

    /// <summary>
    /// Handles mouse movement during measuring.
    /// </summary>
    /// <param name="worldPos">Mouse position in world coordinates.</param>
    /// <param name="shapes">Current shapes on canvas.</param>
    /// <param name="scale">Current canvas scale.</param>
    /// <param name="spatialIndex">Optional spatial index for efficient snap detection.</param>
    public void OnMouseMove(VPoint worldPos, IReadOnlyList<IDrawable> shapes, double scale, QuadTree? spatialIndex = null)
    {
        if (Mode != ToolMode.Measuring)
            return;

        CurrentPoint = worldPos;

        // Use spatial index for efficient snap detection if available
        if (spatialIndex != null)
        {
            CurrentSnap = SnapEngine.FindSnapPoint(worldPos, spatialIndex, scale);
        }
        else
        {
            // Fall back to checking all shapes (slower for large shape counts)
            CurrentSnap = SnapEngine.FindSnapPoint(worldPos, shapes, scale);
        }
    }

    /// <summary>
    /// Handles left click during measuring.
    /// </summary>
    /// <param name="worldPos">Mouse position in world coordinates.</param>
    /// <returns>True if the click was handled by the measuring tool.</returns>
    public bool OnLeftClick(VPoint worldPos)
    {
        if (Mode != ToolMode.Measuring)
            return false;

        // Use snap point if available, otherwise use cursor position
        var clickPoint = CurrentSnap?.Point ?? worldPos;

        if (FirstPoint == null)
        {
            // Set first point
            FirstPoint = clickPoint;
            // Set reference point for perpendicular snapping
            SnapEngine.ReferencePoint = clickPoint;
        }
        else
        {
            // Complete measurement
            LastMeasuredDistance = FirstPoint.DistanceTo(clickPoint);
            MeasurementCompleted?.Invoke(this, LastMeasuredDistance.Value);

            // Reset for next measurement (stay in measuring mode)
            FirstPoint = null;
            CurrentSnap = null;
            SnapEngine.ReferencePoint = null;
        }

        return true;
    }

    /// <summary>
    /// Gets the current distance being measured (if first point is set).
    /// </summary>
    /// <returns>Current distance or null.</returns>
    public double? GetCurrentDistance()
    {
        if (FirstPoint == null || CurrentPoint == null)
            return null;

        var endPoint = CurrentSnap?.Point ?? CurrentPoint;
        return FirstPoint.DistanceTo(endPoint);
    }

    /// <summary>
    /// Gets the effective end point (snapped or cursor position).
    /// </summary>
    public VPoint? GetEffectiveEndPoint()
    {
        if (CurrentSnap != null)
            return CurrentSnap.Point;
        return CurrentPoint;
    }

    /// <summary>
    /// Toggles the measuring tool on/off.
    /// </summary>
    public void Toggle()
    {
        if (Mode == ToolMode.Measuring)
        {
            CancelMeasuring();
        }
        else
        {
            StartMeasuring();
        }
    }

    /// <summary>
    /// Refreshes snap settings from application settings.
    /// </summary>
    public void RefreshSnapSettings()
    {
        SnapEngine.SyncFromSettings();
    }
}
