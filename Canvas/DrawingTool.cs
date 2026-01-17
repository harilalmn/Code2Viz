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
    /// Current snap result (if any).
    /// </summary>
    public SnapResult? CurrentSnap { get; private set; }

    /// <summary>
    /// The snap engine used for detecting snap points.
    /// </summary>
    public SnapEngine SnapEngine { get; }

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
        if (wasDrawing)
        {
            ModeChanged?.Invoke(this, Mode);
        }
    }

    /// <summary>
    /// Handles mouse movement during drawing.
    /// </summary>
    public void OnMouseMove(VPoint worldPos, IReadOnlyList<IDrawable> shapes, double scale)
    {
        if (Mode == DrawingMode.None)
            return;

        CurrentPoint = worldPos;
        CurrentSnap = SnapEngine.FindSnapPoint(worldPos, shapes, scale);
    }

    /// <summary>
    /// Handles left click during drawing.
    /// </summary>
    /// <returns>True if the click was handled and potentially completed a shape.</returns>
    public bool OnLeftClick(VPoint worldPos)
    {
        if (Mode == DrawingMode.None)
            return false;

        var clickPoint = CurrentSnap?.Point ?? worldPos;

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

        var endPoint = CurrentSnap?.Point ?? CurrentPoint;
        if (endPoint == null)
            return null;

        return Mode switch
        {
            DrawingMode.Point => CreatePreviewPoint(),
            DrawingMode.Line => CreatePreviewLine(endPoint),
            DrawingMode.Circle => CreatePreviewCircle(endPoint),
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

        return Mode switch
        {
            DrawingMode.Point => "Point: Click to place",
            DrawingMode.Line => Points.Count == 0 ? "Line: Click start point" : "Line: Click end point",
            DrawingMode.Circle => Points.Count == 0 ? "Circle: Click center" : "Circle: Click radius point",
            DrawingMode.Rectangle => Points.Count == 0 ? "Rectangle: Click first corner" : "Rectangle: Click opposite corner",
            DrawingMode.Ellipse => Points.Count == 0 ? "Ellipse: Click center" : "Ellipse: Drag to set radii",
            DrawingMode.Arc => Points.Count switch
            {
                0 => "Arc: Click center",
                1 => "Arc: Click start point",
                _ => "Arc: Click end point"
            },
            DrawingMode.Polygon => Points.Count < 3 ? $"Polygon: Click point {Points.Count + 1}" : $"Polygon: Click point {Points.Count + 1} (double-click to finish)",
            DrawingMode.Polyline => Points.Count < 2 ? $"Polyline: Click point {Points.Count + 1}" : $"Polyline: Click point {Points.Count + 1} (double-click to finish)",
            DrawingMode.Bezier => Points.Count switch
            {
                0 => "Bezier: Click start point",
                1 => "Bezier: Click control point 1",
                2 => "Bezier: Click control point 2",
                _ => "Bezier: Click end point"
            },
            DrawingMode.Spline => Points.Count < 2 ? $"Spline: Click point {Points.Count + 1}" : $"Spline: Click point {Points.Count + 1} (double-click to finish)",
            DrawingMode.Arrow => Points.Count == 0 ? "Arrow: Click start point" : "Arrow: Click end point",
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
        // Arc: center, start point, end point
        var center = Points[0];
        var radius = center.DistanceTo(Points[1]);
        var startAngle = Math.Atan2(Points[1].Y - center.Y, Points[1].X - center.X) * 180 / Math.PI;
        var endAngle = Math.Atan2(Points[2].Y - center.Y, Points[2].X - center.X) * 180 / Math.PI;
        return new VArc(center, radius, startAngle, endAngle);
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
        point.StrokeColor = "Gray";
        point.FillColor = "DarkGray";
        return point;
    }

    private VLine? CreatePreviewLine(VPoint endPoint)
    {
        if (Points.Count < 1) return null;
        var line = new VLine(Points[0], endPoint);
        line.StrokeColor = "Gray";
        return line;
    }

    private VCircle? CreatePreviewCircle(VPoint endPoint)
    {
        if (Points.Count < 1) return null;
        var radius = Points[0].DistanceTo(endPoint);
        var circle = new VCircle(Points[0], radius);
        circle.StrokeColor = "Gray";
        circle.FillColor = "Transparent";
        return circle;
    }

    private VRectangle? CreatePreviewRectangle(VPoint endPoint)
    {
        if (Points.Count < 1) return null;
        var minX = Math.Min(Points[0].X, endPoint.X);
        var minY = Math.Min(Points[0].Y, endPoint.Y);
        var width = Math.Abs(endPoint.X - Points[0].X);
        var height = Math.Abs(endPoint.Y - Points[0].Y);
        var rect = new VRectangle(minX, minY, width, height);
        rect.StrokeColor = "Gray";
        rect.FillColor = "Transparent";
        return rect;
    }

    private VEllipse? CreatePreviewEllipse(VPoint endPoint)
    {
        if (Points.Count < 1) return null;
        var radiusX = Math.Abs(endPoint.X - Points[0].X);
        var radiusY = Math.Abs(endPoint.Y - Points[0].Y);
        var ellipse = new VEllipse(Points[0], radiusX, radiusY);
        ellipse.StrokeColor = "Gray";
        ellipse.FillColor = "Transparent";
        return ellipse;
    }

    private VArc? CreatePreviewArc(VPoint endPoint)
    {
        if (Points.Count < 1) return null;

        var center = Points[0];

        if (Points.Count == 1)
        {
            // Show radius preview as a line
            var radius = center.DistanceTo(endPoint);
            var circle = new VCircle(center, radius);
            circle.StrokeColor = "Gray";
            circle.FillColor = "Transparent";
            return null; // For now, return null as we need a different shape type
        }

        var radius2 = center.DistanceTo(Points[1]);
        var startAngle = Math.Atan2(Points[1].Y - center.Y, Points[1].X - center.X) * 180 / Math.PI;
        var endAngle = Math.Atan2(endPoint.Y - center.Y, endPoint.X - center.X) * 180 / Math.PI;

        var arc = new VArc(center, radius2, startAngle, endAngle);
        arc.StrokeColor = "Gray";
        return arc;
    }

    private VPolygon? CreatePreviewPolygon(VPoint endPoint)
    {
        if (Points.Count < 1) return null;
        var previewPoints = Points.Concat(new[] { endPoint }).Select(p => new VPoint(p.X, p.Y)).ToArray();
        var polygon = new VPolygon(previewPoints);
        polygon.StrokeColor = "Gray";
        polygon.FillColor = "Transparent";
        return polygon;
    }

    private VPolyline? CreatePreviewPolyline(VPoint endPoint)
    {
        if (Points.Count < 1) return null;
        var previewPoints = Points.Concat(new[] { endPoint }).Select(p => new VPoint(p.X, p.Y)).ToArray();
        var polyline = new VPolyline(previewPoints);
        polyline.StrokeColor = "Gray";
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
        bezier.StrokeColor = "Gray";
        return bezier;
    }

    private VSpline? CreatePreviewSpline(VPoint endPoint)
    {
        if (Points.Count < 1) return null;
        var previewPoints = Points.Concat(new[] { endPoint }).Select(p => new VPoint(p.X, p.Y)).ToArray();
        var spline = new VSpline(previewPoints);
        spline.StrokeColor = "Gray";
        return spline;
    }

    private VArrow? CreatePreviewArrow(VPoint endPoint)
    {
        if (Points.Count < 1) return null;
        var arrow = new VArrow(Points[0], endPoint);
        arrow.StrokeColor = "Gray";
        return arrow;
    }

    private VText? CreatePreviewText()
    {
        if (Points.Count < 1) return null;
        var text = new VText(Points[0], "Text");
        text.StrokeColor = "Gray";
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
