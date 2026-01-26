using Code2Viz.Geometry;

namespace Code2Viz.Canvas;

/// <summary>
/// Drawing modes for the interactive drawing tool.
/// </summary>
public enum DrawingMode
{
    None,
    Point,
    Line,
    Circle,
    CircleDiameter,
    CircleTwoPoints,
    CircleThreePoints,
    Rectangle,
    Ellipse,
    Arc,
    Polygon,
    Polyline,
    Bezier,
    Spline,
    Arrow,
    Text
}

/// <summary>
/// Input modes for precise value entry during drawing (Tab to cycle).
/// </summary>
public enum DrawingInputMode
{
    None,
    Distance,
    Angle
}

/// <summary>
/// Manages the interactive drawing tool state and shape creation logic.
/// </summary>
public class DrawingTool
{
    /// <summary>
    /// Current drawing mode.
    /// </summary>
    public DrawingMode Mode { get; private set; } = DrawingMode.None;

    /// <summary>
    /// Collected click points for the current shape being drawn.
    /// </summary>
    public List<VPoint> Points { get; } = new();

    /// <summary>
    /// Current cursor position in world coordinates.
    /// </summary>
    public VPoint? CurrentPoint { get; private set; }

    /// <summary>
    /// Whether orthogonal constraint is active (Shift key held).
    /// When active, lines are constrained to horizontal or vertical.
    /// </summary>
    public bool IsOrthoMode { get; set; }

    /// <summary>
    /// Current snap result (if any).
    /// </summary>
    public SnapResult? CurrentSnap { get; private set; }

    /// <summary>
    /// The snap engine used for detecting snap points.
    /// </summary>
    public SnapEngine SnapEngine { get; }

    /// <summary>
    /// Current input mode for precise value entry (Tab to cycle).
    /// </summary>
    public DrawingInputMode InputMode { get; private set; } = DrawingInputMode.None;

    /// <summary>
    /// Current input buffer for typed values.
    /// </summary>
    public string InputBuffer { get; private set; } = "";

    /// <summary>
    /// Whether the buffer content is "selected" (first keystroke will replace all).
    /// </summary>
    public bool IsBufferSelected { get; private set; } = false;

    /// <summary>
    /// Override distance value (when user types a distance).
    /// </summary>
    public double? OverrideDistance { get; private set; }

    /// <summary>
    /// Override angle value in degrees (when user types an angle).
    /// </summary>
    public double? OverrideAngle { get; private set; }

    /// <summary>
    /// Event raised when input mode or buffer changes.
    /// </summary>
    public event EventHandler? InputChanged;

    /// <summary>
    /// Event raised when a shape is completed.
    /// </summary>
    public event EventHandler<Shape>? ShapeCompleted;

    /// <summary>
    /// Event raised when drawing mode changes.
    /// </summary>
    public event EventHandler<DrawingMode>? ModeChanged;

    /// <summary>
    /// Event raised when text placement is requested (click location collected, waiting for text input).
    /// The VPoint represents the location where the text should be placed.
    /// </summary>
    public event EventHandler<VPoint>? TextPlacementRequested;

    /// <summary>
    /// Gets the status message for the current drawing state.
    /// </summary>
    public string StatusMessage => GetStatusMessage();

    public DrawingTool()
    {
        SnapEngine = new SnapEngine();
        SnapEngine.SyncFromSettings();
    }

    /// <summary>
    /// Sets the drawing mode and resets state.
    /// </summary>
    public void SetMode(DrawingMode mode)
    {
        Mode = mode;
        Points.Clear();
        CurrentPoint = null;
        CurrentSnap = null;
        SnapEngine.ReferencePoint = null;
        ModeChanged?.Invoke(this, Mode);
    }

    /// <summary>
    /// Cancels the current drawing operation.
    /// </summary>
    public void Cancel()
    {
        var wasDrawing = Mode != DrawingMode.None;
        Mode = DrawingMode.None;
        Points.Clear();
        CurrentPoint = null;
        CurrentSnap = null;
        SnapEngine.ReferencePoint = null;
        ResetInputMode();
        if (wasDrawing)
        {
            ModeChanged?.Invoke(this, Mode);
        }
    }

    /// <summary>
    /// Cycles through input modes (None -> Distance -> Angle -> None).
    /// Only active when there's an extension snap or when drawing after first point.
    /// </summary>
    /// <returns>True if Tab was handled.</returns>
    public bool CycleInputMode()
    {
        // Only allow input mode when drawing and we have at least one point
        if (Mode == DrawingMode.None || Points.Count == 0)
            return false;

        InputMode = InputMode switch
        {
            DrawingInputMode.None => DrawingInputMode.Distance,
            DrawingInputMode.Distance => DrawingInputMode.Angle,
            DrawingInputMode.Angle => DrawingInputMode.None,
            _ => DrawingInputMode.None
        };

        // Clear buffer when switching modes but keep override values
        InputBuffer = "";
        IsBufferSelected = false;

        // Pre-populate buffer with current snap values if available
        if (CurrentSnap?.Type == SnapType.Extension && CurrentSnap.ExtensionSource != null)
        {
            if (InputMode == DrawingInputMode.Distance)
            {
                var dist = OverrideDistance ?? CurrentSnap.ExtensionSource.DistanceTo(CurrentSnap.Point);
                InputBuffer = dist.ToString("F2");
                IsBufferSelected = true;
            }
            else if (InputMode == DrawingInputMode.Angle)
            {
                var angle = OverrideAngle ?? CurrentSnap.ExtensionAngle;
                InputBuffer = angle.ToString("F0");
                IsBufferSelected = true;
            }
        }
        else if (Points.Count > 0 && CurrentPoint != null)
        {
            // Use current point distance/angle from last placed point
            var lastPoint = Points[^1];
            if (InputMode == DrawingInputMode.Distance)
            {
                var dist = OverrideDistance ?? lastPoint.DistanceTo(CurrentPoint);
                InputBuffer = dist.ToString("F2");
                IsBufferSelected = true;
            }
            else if (InputMode == DrawingInputMode.Angle)
            {
                var angle = OverrideAngle ?? Math.Atan2(CurrentPoint.Y - lastPoint.Y, CurrentPoint.X - lastPoint.X) * 180 / Math.PI;
                InputBuffer = angle.ToString("F0");
                IsBufferSelected = true;
            }
        }

        InputChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>
    /// Starts Distance input mode directly (called when user types a digit while drawing).
    /// Pre-populates the buffer with current distance so user can see and replace it.
    /// </summary>
    public void StartDistanceInput()
    {
        if (Mode == DrawingMode.None || Points.Count == 0)
            return;

        InputMode = DrawingInputMode.Distance;

        // Pre-populate with current distance from last point to cursor/snap position
        var lastPoint = Points[^1];
        var targetPoint = CurrentSnap?.Point ?? CurrentPoint;
        if (targetPoint != null)
        {
            var distance = lastPoint.DistanceTo(targetPoint);
            InputBuffer = distance.ToString("F2");
            IsBufferSelected = true; // First keystroke will replace the value
        }
        else
        {
            InputBuffer = "";
            IsBufferSelected = false;
        }

        InputChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Handles character input for distance/angle entry.
    /// </summary>
    /// <returns>True if the character was handled.</returns>
    public bool HandleCharInput(char c)
    {
        if (InputMode == DrawingInputMode.None)
            return false;

        // Allow digits, decimal point, minus sign
        if (char.IsDigit(c) || c == '.' || c == '-')
        {
            // If buffer is selected, first keystroke replaces the entire value
            if (IsBufferSelected)
            {
                InputBuffer = "";
                IsBufferSelected = false;
            }

            // Only allow one decimal point
            if (c == '.' && InputBuffer.Contains('.'))
                return true;

            // Only allow minus at the start
            if (c == '-' && InputBuffer.Length > 0)
                return true;

            InputBuffer += c;
            ApplyInputValue();
            InputChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Handles backspace for input editing.
    /// </summary>
    /// <returns>True if backspace was handled.</returns>
    public bool HandleBackspace()
    {
        if (InputMode == DrawingInputMode.None)
            return false;

        // If buffer is selected, clear entire buffer
        if (IsBufferSelected)
        {
            InputBuffer = "";
            IsBufferSelected = false;
            ApplyInputValue();
            InputChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }

        if (InputBuffer.Length == 0)
            return false;

        InputBuffer = InputBuffer[..^1];
        ApplyInputValue();
        InputChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>
    /// Handles Enter key to confirm input and place point.
    /// </summary>
    /// <returns>True if Enter was handled and a point should be placed.</returns>
    public bool HandleEnterInput()
    {
        if (InputMode == DrawingInputMode.None)
            return false;

        // Apply the current input value
        ApplyInputValue();

        // Reset input mode but keep override values for the click
        InputMode = DrawingInputMode.None;
        InputBuffer = "";
        InputChanged?.Invoke(this, EventArgs.Empty);

        return true;
    }

    /// <summary>
    /// Handles Escape key to cancel input mode.
    /// </summary>
    /// <returns>True if Escape was handled.</returns>
    public bool HandleEscapeInput()
    {
        if (InputMode == DrawingInputMode.None)
            return false;

        ResetInputMode();
        InputChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>
    /// Resets input mode and clears all override values.
    /// </summary>
    public void ResetInputMode()
    {
        InputMode = DrawingInputMode.None;
        InputBuffer = "";
        OverrideDistance = null;
        OverrideAngle = null;
    }

    /// <summary>
    /// Applies the current input buffer value to the appropriate override.
    /// </summary>
    private void ApplyInputValue()
    {
        if (string.IsNullOrEmpty(InputBuffer))
        {
            if (InputMode == DrawingInputMode.Distance)
                OverrideDistance = null;
            else if (InputMode == DrawingInputMode.Angle)
                OverrideAngle = null;
            return;
        }

        if (double.TryParse(InputBuffer, out var value))
        {
            if (InputMode == DrawingInputMode.Distance)
                OverrideDistance = Math.Abs(value); // Distance is always positive
            else if (InputMode == DrawingInputMode.Angle)
                OverrideAngle = value;
        }
    }

    /// <summary>
    /// Gets the effective end point considering override distance/angle.
    /// </summary>
    public VPoint? GetEffectiveEndPoint()
    {
        if (Points.Count == 0)
            return CurrentSnap?.Point ?? CurrentPoint;

        var basePoint = Points[^1];

        // If we have override values, calculate the point from those
        if (OverrideDistance.HasValue || OverrideAngle.HasValue)
        {
            double distance;
            double angleRad;

            if (CurrentSnap?.Type == SnapType.Extension && CurrentSnap.ExtensionSource != null)
            {
                // Use extension snap as base
                distance = OverrideDistance ?? CurrentSnap.ExtensionSource.DistanceTo(CurrentSnap.Point);
                angleRad = (OverrideAngle ?? CurrentSnap.ExtensionAngle) * Math.PI / 180;
                basePoint = CurrentSnap.ExtensionSource;
            }
            else
            {
                // Use cursor position to determine base direction
                var endPoint = CurrentSnap?.Point ?? CurrentPoint;
                if (endPoint == null)
                    return null;

                distance = OverrideDistance ?? basePoint.DistanceTo(endPoint);
                angleRad = OverrideAngle.HasValue
                    ? OverrideAngle.Value * Math.PI / 180
                    : Math.Atan2(endPoint.Y - basePoint.Y, endPoint.X - basePoint.X);
            }

            return new VPoint(
                basePoint.X + distance * Math.Cos(angleRad),
                basePoint.Y + distance * Math.Sin(angleRad)
            );
        }

        return CurrentSnap?.Point ?? CurrentPoint;
    }

    /// <summary>
    /// Gets the input display text for the current mode.
    /// </summary>
    public string GetInputDisplayText()
    {
        if (InputMode == DrawingInputMode.None)
            return "";

        var label = InputMode == DrawingInputMode.Distance ? "Distance" : "Angle";
        var unit = InputMode == DrawingInputMode.Angle ? "°" : "";
        var cursor = "_";

        return $"{label}: {InputBuffer}{cursor}{unit}";
    }

    /// <summary>
    /// Handles mouse movement during drawing.
    /// </summary>
    /// <param name="worldPos">Mouse position in world coordinates.</param>
    /// <param name="shapes">Current shapes on canvas.</param>
    /// <param name="scale">Current canvas scale.</param>
    /// <param name="spatialIndex">Optional spatial index for efficient snap detection.</param>
    public void OnMouseMove(VPoint worldPos, IReadOnlyList<IDrawable> shapes, double scale, QuadTree? spatialIndex = null)
    {
        if (Mode == DrawingMode.None)
            return;

        // Apply orthogonal constraint if Shift is held and we have a reference point
        var constrainedPos = worldPos;
        if (IsOrthoMode && Points.Count > 0)
        {
            constrainedPos = ApplyOrthoConstraint(worldPos, Points[^1]);
        }

        CurrentPoint = constrainedPos;

        // Use spatial index for efficient snap detection if available
        if (spatialIndex != null)
        {
            CurrentSnap = SnapEngine.FindSnapPoint(constrainedPos, spatialIndex, scale);
        }
        else
        {
            CurrentSnap = SnapEngine.FindSnapPoint(constrainedPos, shapes, scale);
        }
    }

    /// <summary>
    /// Applies orthogonal constraint to a point relative to a reference point.
    /// The point is constrained to be either horizontal or vertical from the reference.
    /// </summary>
    private VPoint ApplyOrthoConstraint(VPoint point, VPoint reference)
    {
        var dx = Math.Abs(point.X - reference.X);
        var dy = Math.Abs(point.Y - reference.Y);

        // Constrain to the axis with the larger delta
        if (dx >= dy)
        {
            // Horizontal constraint (keep X, lock Y to reference Y)
            return new VPoint(point.X, reference.Y);
        }
        else
        {
            // Vertical constraint (keep Y, lock X to reference X)
            return new VPoint(reference.X, point.Y);
        }
    }

    /// <summary>
    /// Handles left click during drawing.
    /// </summary>
    /// <returns>True if the click was handled and potentially completed a shape.</returns>
    public bool OnLeftClick(VPoint worldPos)
    {
        if (Mode == DrawingMode.None)
            return false;

        // Apply orthogonal constraint if active and we have a reference point
        var constrainedPos = worldPos;
        if (IsOrthoMode && Points.Count > 0)
        {
            constrainedPos = ApplyOrthoConstraint(worldPos, Points[^1]);
        }

        // Use effective end point if override values are set (from Tab input)
        VPoint clickPoint;
        if (Points.Count > 0 && (OverrideDistance.HasValue || OverrideAngle.HasValue))
        {
            clickPoint = GetEffectiveEndPoint() ?? CurrentSnap?.Point ?? constrainedPos;
        }
        else
        {
            clickPoint = CurrentSnap?.Point ?? constrainedPos;
        }

        // Reset input mode after placing a point
        ResetInputMode();

        // Special handling for Text mode - request text input via event
        if (Mode == DrawingMode.Text)
        {
            TextPlacementRequested?.Invoke(this, clickPoint);
            return true;
        }

        Points.Add(clickPoint);

        // Set reference point for perpendicular snapping
        if (Points.Count == 1)
        {
            SnapEngine.ReferencePoint = clickPoint;
        }

        // Check if shape is complete
        var shape = TryCompleteShape();
        if (shape != null)
        {
            ShapeCompleted?.Invoke(this, shape);
            Points.Clear();
            SnapEngine.ReferencePoint = null;
            // Stay in the same mode for drawing more shapes
            return true;
        }

        return true;
    }

    /// <summary>
    /// Handles double-click during drawing (for polygon/polyline/spline completion).
    /// </summary>
    /// <returns>True if the click was handled.</returns>
    public bool OnDoubleClick(VPoint worldPos)
    {
        if (Mode == DrawingMode.None)
            return false;

        // For multi-point shapes, complete on double-click
        if (Mode == DrawingMode.Polygon || Mode == DrawingMode.Polyline || Mode == DrawingMode.Spline)
        {
            var shape = TryForceCompleteShape();
            if (shape != null)
            {
                ShapeCompleted?.Invoke(this, shape);
                Points.Clear();
                SnapEngine.ReferencePoint = null;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Handles right-click to cancel current shape or exit drawing mode.
    /// </summary>
    /// <returns>True if the click was handled.</returns>
    public bool OnRightClick()
    {
        if (Mode == DrawingMode.None)
            return false;

        if (Points.Count > 0)
        {
            // Reset current shape but stay in mode
            Points.Clear();
            SnapEngine.ReferencePoint = null;
        }
        else
        {
            // Exit drawing mode
            Cancel();
        }

        return true;
    }

    /// <summary>
    /// Gets the preview shape for rendering (null if no points yet).
    /// </summary>
    public Shape? GetPreviewShape()
    {
        if (Mode == DrawingMode.None || Points.Count == 0)
            return null;

        // Use effective end point which considers override distance/angle
        var endPoint = GetEffectiveEndPoint();
        if (endPoint == null)
            return null;

        return Mode switch
        {
            DrawingMode.Point => CreatePreviewPoint(),
            DrawingMode.Line => CreatePreviewLine(endPoint),
            DrawingMode.Circle => CreatePreviewCircle(endPoint),
            DrawingMode.CircleDiameter => CreatePreviewCircleDiameter(endPoint),
            DrawingMode.CircleTwoPoints => CreatePreviewCircleTwoPoints(endPoint),
            DrawingMode.CircleThreePoints => CreatePreviewCircleThreePoints(endPoint),
            DrawingMode.Rectangle => CreatePreviewRectangle(endPoint),
            DrawingMode.Ellipse => CreatePreviewEllipse(endPoint),
            DrawingMode.Arc => CreatePreviewArc(endPoint),
            DrawingMode.Polygon => CreatePreviewPolygon(endPoint),
            DrawingMode.Polyline => CreatePreviewPolyline(endPoint),
            DrawingMode.Bezier => CreatePreviewBezier(endPoint),
            DrawingMode.Spline => CreatePreviewSpline(endPoint),
            DrawingMode.Arrow => CreatePreviewArrow(endPoint),
            DrawingMode.Text => CreatePreviewText(),
            _ => null
        };
    }

    /// <summary>
    /// Tries to complete the current shape based on collected points.
    /// </summary>
    private Shape? TryCompleteShape()
    {
        return Mode switch
        {
            DrawingMode.Point when Points.Count >= 1 => CreatePoint(),
            DrawingMode.Line when Points.Count >= 2 => CreateLine(),
            DrawingMode.Circle when Points.Count >= 2 => CreateCircle(),
            DrawingMode.CircleDiameter when Points.Count >= 2 => CreateCircleDiameter(),
            DrawingMode.CircleTwoPoints when Points.Count >= 2 => CreateCircleTwoPoints(),
            DrawingMode.CircleThreePoints when Points.Count >= 3 => CreateCircleThreePoints(),
            DrawingMode.Rectangle when Points.Count >= 2 => CreateRectangle(),
            DrawingMode.Ellipse when Points.Count >= 2 => CreateEllipse(),
            DrawingMode.Arc when Points.Count >= 3 => CreateArc(),
            DrawingMode.Bezier when Points.Count >= 4 => CreateBezier(),
            DrawingMode.Arrow when Points.Count >= 2 => CreateArrow(),
            // Polygon, Polyline, Spline require double-click to complete
            _ => null
        };
    }

    /// <summary>
    /// Forces completion of multi-point shapes.
    /// </summary>
    private Shape? TryForceCompleteShape()
    {
        return Mode switch
        {
            DrawingMode.Polygon when Points.Count >= 3 => CreatePolygon(),
            DrawingMode.Polyline when Points.Count >= 2 => CreatePolyline(),
            DrawingMode.Spline when Points.Count >= 2 => CreateSpline(),
            _ => null
        };
    }

    private string GetStatusMessage()
    {
        if (Mode == DrawingMode.None)
            return "Ready";

        var modeName = Mode.ToString();

        // Add ortho hint for modes where it's useful (when we have a reference point)
        var orthoHint = Points.Count > 0 ? " (Shift: ortho)" : "";

        return Mode switch
        {
            DrawingMode.Point => "Point: Click to place",
            DrawingMode.Line => Points.Count == 0 ? "Line: Click start point" : $"Line: Click end point{orthoHint}",
            DrawingMode.Circle => Points.Count == 0 ? "Circle: Click center" : "Circle: Click radius point",
            DrawingMode.CircleDiameter => Points.Count == 0 ? "Circle (Diameter): Click center" : "Circle (Diameter): Click diameter point",
            DrawingMode.CircleTwoPoints => Points.Count == 0 ? "Circle (2 Points): Click first diameter endpoint" : "Circle (2 Points): Click second diameter endpoint",
            DrawingMode.CircleThreePoints => Points.Count switch
            {
                0 => "Circle (3 Points): Click first point",
                1 => "Circle (3 Points): Click second point",
                _ => "Circle (3 Points): Click third point"
            },
            DrawingMode.Rectangle => Points.Count == 0 ? "Rectangle: Click first corner" : "Rectangle: Click opposite corner",
            DrawingMode.Ellipse => Points.Count == 0 ? "Ellipse: Click center" : "Ellipse: Drag to set radii",
            DrawingMode.Arc => Points.Count switch
            {
                0 => "Arc: Click start point",
                1 => "Arc: Click point on arc",
                _ => "Arc: Click end point"
            },
            DrawingMode.Polygon => Points.Count == 0 ? "Polygon: Click point 1" : Points.Count < 3 ? $"Polygon: Click point {Points.Count + 1}{orthoHint}" : $"Polygon: Click point {Points.Count + 1} (double-click to finish){orthoHint}",
            DrawingMode.Polyline => Points.Count == 0 ? "Polyline: Click point 1" : Points.Count < 2 ? $"Polyline: Click point {Points.Count + 1}{orthoHint}" : $"Polyline: Click point {Points.Count + 1} (double-click to finish){orthoHint}",
            DrawingMode.Bezier => Points.Count switch
            {
                0 => "Bezier: Click start point",
                1 => $"Bezier: Click control point 1{orthoHint}",
                2 => $"Bezier: Click control point 2{orthoHint}",
                _ => $"Bezier: Click end point{orthoHint}"
            },
            DrawingMode.Spline => Points.Count == 0 ? "Spline: Click point 1" : Points.Count < 2 ? $"Spline: Click point {Points.Count + 1}{orthoHint}" : $"Spline: Click point {Points.Count + 1} (double-click to finish){orthoHint}",
            DrawingMode.Arrow => Points.Count == 0 ? "Arrow: Click start point" : $"Arrow: Click end point{orthoHint}",
            DrawingMode.Text => "Text: Click to place",
            _ => $"{modeName}: Drawing..."
        };
    }

    #region Shape Creation

    private VPoint CreatePoint()
    {
        return new VPoint(Points[0].X, Points[0].Y);
    }

    private VLine CreateLine()
    {
        return new VLine(Points[0], Points[1]);
    }

    private VCircle CreateCircle()
    {
        var radius = Points[0].DistanceTo(Points[1]);
        return new VCircle(Points[0], radius);
    }

    private VCircle CreateCircleDiameter()
    {
        // Center + diameter point: use distance as diameter, not radius
        var diameter = Points[0].DistanceTo(Points[1]);
        return new VCircle(Points[0], diameter / 2.0);
    }

    private VCircle CreateCircleTwoPoints()
    {
        // Two points define the diameter endpoints
        return VCircle.FromTwoPoints(Points[0], Points[1]);
    }

    private VCircle CreateCircleThreePoints()
    {
        // Three points on circumference
        return new VCircle(Points[0], Points[1], Points[2]);
    }

    private VRectangle CreateRectangle()
    {
        var minX = Math.Min(Points[0].X, Points[1].X);
        var minY = Math.Min(Points[0].Y, Points[1].Y);
        var width = Math.Abs(Points[1].X - Points[0].X);
        var height = Math.Abs(Points[1].Y - Points[0].Y);
        return new VRectangle(minX, minY, width, height);
    }

    private VEllipse CreateEllipse()
    {
        var radiusX = Math.Abs(Points[1].X - Points[0].X);
        var radiusY = Math.Abs(Points[1].Y - Points[0].Y);
        return new VEllipse(Points[0], radiusX, radiusY);
    }

    private VArc CreateArc()
    {
        // Three-point arc: start point, point on arc, end point
        var p1 = Points[0]; // Start
        var p2 = Points[1]; // Point on arc (midpoint)
        var p3 = Points[2]; // End

        var (center, radius) = CalculateCircumcircle(p1, p2, p3);

        // Calculate angles from center to start and end points
        var startAngle = Math.Atan2(p1.Y - center.Y, p1.X - center.X) * 180 / Math.PI;
        var midAngle = Math.Atan2(p2.Y - center.Y, p2.X - center.X) * 180 / Math.PI;
        var endAngle = Math.Atan2(p3.Y - center.Y, p3.X - center.X) * 180 / Math.PI;

        // Determine the correct arc direction based on midpoint
        // The midpoint should be on the arc, so check if going from start to end
        // through the midpoint requires going clockwise or counter-clockwise
        var (adjustedStart, adjustedEnd) = DetermineArcDirection(startAngle, midAngle, endAngle);

        return new VArc(center, radius, adjustedStart, adjustedEnd);
    }

    /// <summary>
    /// Calculates the circumcircle (center and radius) from three points.
    /// </summary>
    private static (VPoint center, double radius) CalculateCircumcircle(VPoint p1, VPoint p2, VPoint p3)
    {
        var x1 = p1.X; var y1 = p1.Y;
        var x2 = p2.X; var y2 = p2.Y;
        var x3 = p3.X; var y3 = p3.Y;

        var d = 2 * (x1 * (y2 - y3) + x2 * (y3 - y1) + x3 * (y1 - y2));

        // Check for collinear points (d ≈ 0)
        if (Math.Abs(d) < 1e-10)
        {
            // Points are nearly collinear, return midpoint as center with large radius
            var midX = (x1 + x3) / 2;
            var midY = (y1 + y3) / 2;
            return (new VPoint(midX, midY), p1.DistanceTo(p3) / 2);
        }

        var sq1 = x1 * x1 + y1 * y1;
        var sq2 = x2 * x2 + y2 * y2;
        var sq3 = x3 * x3 + y3 * y3;

        var cx = (sq1 * (y2 - y3) + sq2 * (y3 - y1) + sq3 * (y1 - y2)) / d;
        var cy = (sq1 * (x3 - x2) + sq2 * (x1 - x3) + sq3 * (x2 - x1)) / d;

        var center = new VPoint(cx, cy);
        var radius = center.DistanceTo(p1);

        return (center, radius);
    }

    /// <summary>
    /// Determines the correct start and end angles so the arc passes through the midpoint.
    /// </summary>
    private static (double start, double end) DetermineArcDirection(double startAngle, double midAngle, double endAngle)
    {
        // Normalize all angles to (-180, 180] for easier comparison
        startAngle = NormalizeAngleSigned(startAngle);
        midAngle = NormalizeAngleSigned(midAngle);
        endAngle = NormalizeAngleSigned(endAngle);

        // Calculate angular distance from start to mid and start to end (going CCW, positive direction)
        double toMid = NormalizeAngleSigned(midAngle - startAngle);
        double toEnd = NormalizeAngleSigned(endAngle - startAngle);

        // Determine if mid is "between" start and end when going CCW (positive direction)
        // This happens if toMid has the same sign as toEnd and |toMid| < |toEnd|
        bool midBetweenCCW;
        if (toEnd >= 0)
        {
            midBetweenCCW = toMid >= 0 && toMid <= toEnd;
        }
        else
        {
            midBetweenCCW = toMid <= 0 && toMid >= toEnd;
        }

        if (midBetweenCCW)
        {
            // Arc from start to end (CCW direction if toEnd > 0) passes through mid
            return (startAngle, endAngle);
        }
        else
        {
            // Need to go the other way - swap to reverse direction
            return (endAngle, startAngle);
        }
    }

    private static double NormalizeAngleSigned(double angle)
    {
        while (angle <= -180) angle += 360;
        while (angle > 180) angle -= 360;
        return angle;
    }

    private VPolygon CreatePolygon()
    {
        return new VPolygon(Points.Select(p => new VPoint(p.X, p.Y)).ToArray());
    }

    private VPolyline CreatePolyline()
    {
        return new VPolyline(Points.Select(p => new VPoint(p.X, p.Y)).ToArray());
    }

    private VBezier CreateBezier()
    {
        return new VBezier(Points[0], Points[1], Points[2], Points[3]);
    }

    private VSpline CreateSpline()
    {
        return new VSpline(Points.Select(p => new VPoint(p.X, p.Y)).ToArray());
    }

    private VArrow CreateArrow()
    {
        return new VArrow(Points[0], Points[1]);
    }

    #endregion

    #region Preview Creation

    private VPoint CreatePreviewPoint()
    {
        var point = new VPoint(Points[0].X, Points[0].Y);
        point.Color = "Gray";
        point.FillColor = "DarkGray";
        return point;
    }

    private VLine? CreatePreviewLine(VPoint endPoint)
    {
        if (Points.Count < 1) return null;
        var line = new VLine(Points[0], endPoint);
        line.Color = "Gray";
        return line;
    }

    private VCircle? CreatePreviewCircle(VPoint endPoint)
    {
        if (Points.Count < 1) return null;
        var radius = Points[0].DistanceTo(endPoint);
        var circle = new VCircle(Points[0], radius);
        circle.Color = "Gray";
        circle.FillColor = "Transparent";
        return circle;
    }

    private VCircle? CreatePreviewCircleDiameter(VPoint endPoint)
    {
        if (Points.Count < 1) return null;
        // Distance is diameter, not radius
        var diameter = Points[0].DistanceTo(endPoint);
        var circle = new VCircle(Points[0], diameter / 2.0);
        circle.Color = "Gray";
        circle.FillColor = "Transparent";
        return circle;
    }

    private Shape? CreatePreviewCircleTwoPoints(VPoint endPoint)
    {
        if (Points.Count < 1) return null;
        // Two points define diameter endpoints, center is midpoint
        var center = new VPoint((Points[0].X + endPoint.X) / 2.0, (Points[0].Y + endPoint.Y) / 2.0);
        var radius = Points[0].DistanceTo(endPoint) / 2.0;
        var circle = new VCircle(center, radius);
        circle.Color = "Gray";
        circle.FillColor = "Transparent";
        return circle;
    }

    private Shape? CreatePreviewCircleThreePoints(VPoint endPoint)
    {
        if (Points.Count < 1) return null;

        if (Points.Count == 1)
        {
            // Only have first point, show line to cursor
            var line = new VLine(Points[0], endPoint);
            line.Color = "Gray";
            return line;
        }

        // Have two points, show preview circle through all three
        try
        {
            var circle = new VCircle(Points[0], Points[1], endPoint);
            circle.Color = "Gray";
            circle.FillColor = "Transparent";
            return circle;
        }
        catch
        {
            // Points are collinear, show polyline instead
            var polyline = new VPolyline(Points[0], Points[1], endPoint);
            polyline.Color = "Gray";
            return polyline;
        }
    }


    private VRectangle? CreatePreviewRectangle(VPoint endPoint)
    {
        if (Points.Count < 1) return null;
        var minX = Math.Min(Points[0].X, endPoint.X);
        var minY = Math.Min(Points[0].Y, endPoint.Y);
        var width = Math.Abs(endPoint.X - Points[0].X);
        var height = Math.Abs(endPoint.Y - Points[0].Y);
        var rect = new VRectangle(minX, minY, width, height);
        rect.Color = "Gray";
        rect.FillColor = "Transparent";
        return rect;
    }

    private VEllipse? CreatePreviewEllipse(VPoint endPoint)
    {
        if (Points.Count < 1) return null;
        var radiusX = Math.Abs(endPoint.X - Points[0].X);
        var radiusY = Math.Abs(endPoint.Y - Points[0].Y);
        var ellipse = new VEllipse(Points[0], radiusX, radiusY);
        ellipse.Color = "Gray";
        ellipse.FillColor = "Transparent";
        return ellipse;
    }

    private Shape? CreatePreviewArc(VPoint endPoint)
    {
        if (Points.Count < 1) return null;

        var p1 = Points[0]; // Start point

        if (Points.Count == 1)
        {
            // Only have start point, show line to current position as preview
            var line = new VLine(p1, endPoint);
            line.Color = "Gray";
            return line;
        }

        // Have start and mid point - show two lines instead of arc preview
        // This avoids confusion since arc preview may not match final result exactly
        var p2 = Points[1]; // Point on arc
        var p3 = endPoint;  // End point (preview)

        // Return a polyline showing: start -> mid -> end (cursor)
        var polyline = new VPolyline(p1, p2, p3);
        polyline.Color = "Gray";
        return polyline;
    }

    private VPolygon? CreatePreviewPolygon(VPoint endPoint)
    {
        if (Points.Count < 1) return null;
        var previewPoints = Points.Concat(new[] { endPoint }).Select(p => new VPoint(p.X, p.Y)).ToArray();
        var polygon = new VPolygon(previewPoints);
        polygon.Color = "Gray";
        polygon.FillColor = "Transparent";
        return polygon;
    }

    private VPolyline? CreatePreviewPolyline(VPoint endPoint)
    {
        if (Points.Count < 1) return null;
        var previewPoints = Points.Concat(new[] { endPoint }).Select(p => new VPoint(p.X, p.Y)).ToArray();
        var polyline = new VPolyline(previewPoints);
        polyline.Color = "Gray";
        return polyline;
    }

    private VBezier? CreatePreviewBezier(VPoint endPoint)
    {
        if (Points.Count < 1) return null;

        // Create preview based on number of points collected
        VPoint p0 = Points[0];
        VPoint p1 = Points.Count > 1 ? Points[1] : endPoint;
        VPoint p2 = Points.Count > 2 ? Points[2] : endPoint;
        VPoint p3 = Points.Count > 3 ? Points[3] : endPoint;

        var bezier = new VBezier(p0, p1, p2, p3);
        bezier.Color = "Gray";
        return bezier;
    }

    private VSpline? CreatePreviewSpline(VPoint endPoint)
    {
        if (Points.Count < 1) return null;
        var previewPoints = Points.Concat(new[] { endPoint }).Select(p => new VPoint(p.X, p.Y)).ToArray();
        var spline = new VSpline(previewPoints);
        spline.Color = "Gray";
        return spline;
    }

    private VArrow? CreatePreviewArrow(VPoint endPoint)
    {
        if (Points.Count < 1) return null;
        var arrow = new VArrow(Points[0], endPoint);
        arrow.Color = "Gray";
        return arrow;
    }

    private VText? CreatePreviewText()
    {
        if (Points.Count < 1) return null;
        var text = new VText(Points[0], "Text");
        text.Color = "Gray";
        return text;
    }

    #endregion

    /// <summary>
    /// Refreshes snap settings from application settings.
    /// </summary>
    public void RefreshSnapSettings()
    {
        SnapEngine.SyncFromSettings();
    }

    /// <summary>
    /// Completes a text shape with the given content.
    /// Called by the UI after getting text input from the user.
    /// </summary>
    public void CompleteText(VPoint location, string content)
    {
        if (Mode != DrawingMode.Text || string.IsNullOrEmpty(content))
            return;

        var text = new VText(location, content);
        ShapeCompleted?.Invoke(this, text);
    }
}
