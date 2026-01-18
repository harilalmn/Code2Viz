using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Code2Viz.Geometry;
using Point = System.Windows.Point;
// Direct usage of VPoint, VLine etc. No alias needed.

namespace Code2Viz.Canvas;

// Snap indicator marker brushes
internal static class SnapMarkerBrushes
{
    public static readonly Brush EndpointBrush;
    public static readonly Brush MidpointBrush;
    public static readonly Brush CenterBrush;
    public static readonly Brush IntersectionBrush;
    public static readonly Brush NearestBrush;
    public static readonly Brush PerpendicularBrush;
    public static readonly Pen MeasuringLinePen;

    static SnapMarkerBrushes()
    {
        EndpointBrush = new SolidColorBrush(Colors.Yellow);
        EndpointBrush.Freeze();

        MidpointBrush = new SolidColorBrush(Colors.Cyan);
        MidpointBrush.Freeze();

        CenterBrush = new SolidColorBrush(Colors.Magenta);
        CenterBrush.Freeze();

        IntersectionBrush = new SolidColorBrush(Colors.Red);
        IntersectionBrush.Freeze();

        NearestBrush = new SolidColorBrush(Colors.LimeGreen);
        NearestBrush.Freeze();

        PerpendicularBrush = new SolidColorBrush(Colors.Orange);
        PerpendicularBrush.Freeze();

        var measuringBrush = new SolidColorBrush(Colors.LimeGreen);
        measuringBrush.Freeze();
        MeasuringLinePen = new Pen(measuringBrush, 2) { DashStyle = DashStyles.Dash };
        MeasuringLinePen.Freeze();
    }
}

/// <summary>
/// High-performance canvas using DrawingVisual for rendering tens of thousands of shapes.
/// </summary>
public class RenderCanvas : FrameworkElement
{
    private const double PointRadius = 5;
    private const double MinZoom = 0.01;
    private const double MaxZoom = 100;
    private const double ZoomFactor = 1.1;

    private double _scale = 1.0;
    private double _panX = 0;
    private double _panY = 0;
    private Point _lastMousePosition;
    private bool _isPanning = false;
    private bool _showGrid = true;
    private double _gridSpacing = 50;

    private List<IDrawable> _currentShapes = new();
    private readonly DrawingVisual _visual;

    // Measuring Tool
    private MeasuringTool? _measuringTool;
    public MeasuringTool MeasuringTool => _measuringTool ??= new MeasuringTool();

    // Drawing Tool
    private DrawingTool? _drawingTool;
    public DrawingTool DrawingTool => _drawingTool ??= new DrawingTool();

    // Selection Tool
    private SelectionTool? _selectionTool;
    public SelectionTool SelectionTool => _selectionTool ??= new SelectionTool();

    /// <summary>
    /// Whether selection mode is active (vs drawing mode).
    /// </summary>
    public bool IsSelectionMode { get; set; } = true;

    // Shape highlighting (for Outliner hover)
    private long? _highlightedShapeId;
    public long? HighlightedShapeId
    {
        get => _highlightedShapeId;
        set
        {
            if (_highlightedShapeId != value)
            {
                _highlightedShapeId = value;
                RedrawAll();
            }
        }
    }

    // Brush cache for performance
    private static readonly Dictionary<string, Brush> _brushCache = new();
    private static readonly Dictionary<(string color, double thickness), Pen> _penCache = new();

    // Pre-frozen brushes for common colors
    // Removed static BackgroundBrush to allow dynamic changes
    private static readonly Brush GridBrush;
    private static readonly Brush XAxisBrush;  // Red for X-axis
    private static readonly Brush YAxisBrush;  // Green for Y-axis

    private Brush _backgroundBrush;

    static RenderCanvas()
    {
        GridBrush = new SolidColorBrush(Color.FromRgb(50, 50, 50));
        GridBrush.Freeze();

        XAxisBrush = new SolidColorBrush(Color.FromRgb(180, 60, 60));  // Red
        XAxisBrush.Freeze();

        YAxisBrush = new SolidColorBrush(Color.FromRgb(60, 180, 60));  // Green
        YAxisBrush.Freeze();
    }

    public event EventHandler<Point>? MouseWorldPositionChanged;

    public Brush CanvasBackground
    {
        get => _backgroundBrush;
        set
        {
            _backgroundBrush = value;
            if (_backgroundBrush.CanFreeze) _backgroundBrush.Freeze();
            RedrawAll();
        }
    }

    public bool ShowGrid
    {
        get => _showGrid;
        set { _showGrid = value; RedrawAll(); }
    }

    public double GridSpacing
    {
        get => _gridSpacing;
        set { _gridSpacing = value; RedrawAll(); }
    }

    public RenderCanvas()
    {
        _backgroundBrush = new SolidColorBrush(Color.FromRgb(30, 30, 30));
        _backgroundBrush.Freeze();

        _visual = new DrawingVisual();
        AddVisualChild(_visual);
        AddLogicalChild(_visual);

        ClipToBounds = true;

        MouseWheel += OnMouseWheel;
        MouseDown += OnMouseDown;
        MouseUp += OnMouseUp;
        MouseMove += OnMouseMove;
        SizeChanged += OnSizeChanged;
    }

    // Required overrides for hosting DrawingVisual
    protected override int VisualChildrenCount => 1;

    protected override Visual GetVisualChild(int index)
    {
        if (index != 0) throw new ArgumentOutOfRangeException(nameof(index));
        return _visual;
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        RedrawAll();
    }

    public void CenterOrigin()
    {
        _scale = 1.0;
        _panX = 0;
        _panY = 0;
        RedrawAll();
    }

    /// <summary>
    /// Forces an immediate redraw of the canvas.
    /// </summary>
    public void Refresh()
    {
        RedrawAll();
    }

    // Convert world coordinates to screen coordinates
    private Point WorldToScreen(double worldX, double worldY)
    {
        var screenX = ActualWidth / 2 + (worldX * _scale) + _panX;
        var screenY = ActualHeight / 2 - (worldY * _scale) + _panY;
        return new Point(screenX, screenY);
    }

    // Convert screen coordinates to world coordinates
    private Point ScreenToWorld(double screenX, double screenY)
    {
        var worldX = (screenX - ActualWidth / 2 - _panX) / _scale;
        var worldY = -(screenY - ActualHeight / 2 - _panY) / _scale;
        return new Point(worldX, worldY);
    }

    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var mouseScreenPos = e.GetPosition(this);
        var worldPos = ScreenToWorld(mouseScreenPos.X, mouseScreenPos.Y);

        if (e.Delta > 0)
            _scale *= ZoomFactor;
        else
            _scale /= ZoomFactor;

        _scale = Math.Clamp(_scale, MinZoom, MaxZoom);

        var newScreenPos = WorldToScreen(worldPos.X, worldPos.Y);
        _panX += mouseScreenPos.X - newScreenPos.X;
        _panY += mouseScreenPos.Y - newScreenPos.Y;

        RedrawAll();
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.MiddleButton == MouseButtonState.Pressed)
        {
            _isPanning = true;
            _lastMousePosition = e.GetPosition(this);
            CaptureMouse();
            Cursor = Cursors.Hand;
        }
        else if (e.LeftButton == MouseButtonState.Pressed)
        {
            var screenPos = e.GetPosition(this);
            var worldPos = ScreenToWorld(screenPos.X, screenPos.Y);
            var vPoint = new VPoint(worldPos.X, worldPos.Y);

            // Handle drawing tool clicks first (if active)
            if (_drawingTool != null && _drawingTool.Mode != DrawingMode.None)
            {
                if (e.ClickCount == 2)
                {
                    _drawingTool.OnDoubleClick(vPoint);
                }
                else
                {
                    _drawingTool.OnLeftClick(vPoint);
                }
                RedrawAll();
                e.Handled = true;
                return;
            }

            // Handle measuring tool clicks
            if (_measuringTool?.Mode == ToolMode.Measuring)
            {
                _measuringTool.OnLeftClick(vPoint);
                RedrawAll();
                e.Handled = true;
                return;
            }

            // Handle selection mode
            if (IsSelectionMode && _selectionTool != null)
            {
                var shift = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);
                var ctrl = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);

                _selectionTool.OnMouseDown(vPoint, shift, ctrl, _currentShapes, _scale);

                if (_selectionTool.IsBoxSelecting || _selectionTool.IsDraggingHandle)
                {
                    CaptureMouse();
                }

                RedrawAll();
                e.Handled = true;
            }
        }
        else if (e.RightButton == MouseButtonState.Pressed)
        {
            // Handle drawing tool right-click (cancel)
            if (_drawingTool != null && _drawingTool.Mode != DrawingMode.None)
            {
                _drawingTool.OnRightClick();
                RedrawAll();
                e.Handled = true;
            }
        }
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.MiddleButton == MouseButtonState.Released && _isPanning)
        {
            _isPanning = false;
            ReleaseMouseCapture();
            Cursor = Cursors.Arrow;
        }
        else if (e.LeftButton == MouseButtonState.Released)
        {
            // Handle selection box completion or handle dragging end
            if (_selectionTool?.IsBoxSelecting == true || _selectionTool?.IsDraggingHandle == true)
            {
                var screenPos = e.GetPosition(this);
                var worldPos = ScreenToWorld(screenPos.X, screenPos.Y);
                var vPoint = new VPoint(worldPos.X, worldPos.Y);

                var shift = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);
                var ctrl = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);

                _selectionTool.OnMouseUp(vPoint, _currentShapes, shift, ctrl);
                ReleaseMouseCapture();
                RedrawAll();
            }
        }
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        var screenPos = e.GetPosition(this);
        var worldPos = ScreenToWorld(screenPos.X, screenPos.Y);
        MouseWorldPositionChanged?.Invoke(this, worldPos);

        if (_isPanning)
        {
            _panX += screenPos.X - _lastMousePosition.X;
            _panY += screenPos.Y - _lastMousePosition.Y;
            _lastMousePosition = screenPos;
            RedrawAll();
        }
        else if (_drawingTool != null && _drawingTool.Mode != DrawingMode.None)
        {
            // Update drawing tool with cursor position
            _drawingTool.OnMouseMove(new VPoint(worldPos.X, worldPos.Y), _currentShapes, _scale);
            RedrawAll();
        }
        else if (_measuringTool?.Mode == ToolMode.Measuring)
        {
            // Update measuring tool with cursor position
            _measuringTool.OnMouseMove(new VPoint(worldPos.X, worldPos.Y), _currentShapes, _scale);
            RedrawAll();
        }
        else if (_selectionTool?.IsBoxSelecting == true || _selectionTool?.IsDraggingHandle == true)
        {
            // Update selection box or handle drag
            _selectionTool.OnMouseMove(new VPoint(worldPos.X, worldPos.Y));
            RedrawAll();
        }
    }

    public void ClearShapes()
    {
        _currentShapes.Clear();
        _scale = 1.0;
        _panX = 0;
        _panY = 0;
        RedrawAll();
    }

    public void Render(IEnumerable<IDrawable> shapes)
    {
        _currentShapes = shapes.ToList();
        RedrawAll();
    }

    /// <summary>
    /// Adds a shape to the current canvas display without requiring code execution.
    /// </summary>
    public void AddShape(IDrawable shape)
    {
        _currentShapes.Add(shape);
        RedrawAll();
    }

    /// <summary>
    /// Gets a read-only list of current shapes.
    /// </summary>
    public IReadOnlyList<IDrawable> GetCurrentShapes()
    {
        return _currentShapes.AsReadOnly();
    }

    private static Brush GetCachedBrush(string colorName)
    {
        if (_brushCache.TryGetValue(colorName, out var cached))
            return cached;

        try
        {
            var color = (Color)ColorConverter.ConvertFromString(colorName);
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            _brushCache[colorName] = brush;
            return brush;
        }
        catch
        {
            return Brushes.White;
        }
    }

    private static Pen GetCachedPen(string colorName, double thickness)
    {
        var key = (colorName, thickness);
        if (_penCache.TryGetValue(key, out var cached))
            return cached;

        var brush = GetCachedBrush(colorName);
        var pen = new Pen(brush, thickness);
        pen.Freeze();
        _penCache[key] = pen;
        return pen;
    }

    private void RedrawAll()
    {
        using var dc = _visual.RenderOpen();

        if (ActualWidth <= 0 || ActualHeight <= 0)
            return;

        // Draw background
        dc.DrawRectangle(_backgroundBrush, null, new Rect(0, 0, ActualWidth, ActualHeight));

        if (_showGrid)
        {
            DrawGrid(dc);
            DrawAxes(dc);
        }

        // Calculate Viewport in World Coordinates for Culling
        var p1 = ScreenToWorld(0, 0);
        var p2 = ScreenToWorld(ActualWidth, ActualHeight);
        
        // Normalize coordinates (min/max)
        var minX = Math.Min(p1.X, p2.X);
        var maxX = Math.Max(p1.X, p2.X);
        var minY = Math.Min(p1.Y, p2.Y);
        var maxY = Math.Max(p1.Y, p2.Y);

        // Add padding to account for stroke thickness (approx 20px in world units)
        var padding = 20.0 / Math.Max(_scale, MinZoom);
        minX -= padding;
        maxX += padding;
        minY -= padding;
        maxY += padding;

        // Draw all shapes using DrawingContext with Viewport Culling
        foreach (var shape in _currentShapes)
        {
            switch (shape)
            {
                case VPoint point:
                    if (point.X >= minX && point.X <= maxX && point.Y >= minY && point.Y <= maxY)
                        DrawPoint(dc, point);
                    break;
                    
                case VLine line:
                    // AABB check
                    if (Math.Max(line.Start.X, line.End.X) >= minX && Math.Min(line.Start.X, line.End.X) <= maxX &&
                        Math.Max(line.Start.Y, line.End.Y) >= minY && Math.Min(line.Start.Y, line.End.Y) <= maxY)
                        DrawLine(dc, line);
                    break;
                    
                case VArc arc:
                    // Bounding Box check using Center +/- Radius
                    if ((arc.Center.X + arc.Radius) >= minX && (arc.Center.X - arc.Radius) <= maxX &&
                        (arc.Center.Y + arc.Radius) >= minY && (arc.Center.Y - arc.Radius) <= maxY)
                        DrawArc(dc, arc);
                    break;
                    
                case VCircle circle:
                    if ((circle.Center.X + circle.Radius) >= minX && (circle.Center.X - circle.Radius) <= maxX &&
                        (circle.Center.Y + circle.Radius) >= minY && (circle.Center.Y - circle.Radius) <= maxY)
                        DrawCircle(dc, circle);
                    break;
                    
                case VRectangle rect:
                    if ((rect.Corner.X + rect.Width) >= minX && rect.Corner.X <= maxX &&
                        (rect.Corner.Y + rect.Height) >= minY && rect.Corner.Y <= maxY)
                        DrawRectangle(dc, rect);
                    break;
                    
                case VEllipse ellipse:
                    if ((ellipse.Center.X + ellipse.RadiusX) >= minX && (ellipse.Center.X - ellipse.RadiusX) <= maxX &&
                        (ellipse.Center.Y + ellipse.RadiusY) >= minY && (ellipse.Center.Y - ellipse.RadiusY) <= maxY)
                        DrawEllipse(dc, ellipse);
                    break;
                    
                case VPolygon polygon:
                    DrawPolygon(dc, polygon);
                    break;
                    
                case VPolyline polyline:
                    DrawPolyline(dc, polyline);
                    break;
                    
                case VText text:
                    if (text.Location.X >= minX && text.Location.X <= maxX && 
                        text.Location.Y >= minY && text.Location.Y <= maxY)
                        DrawText(dc, text);
                    break;
                    
                case VBezier bezier:
                    DrawBezier(dc, bezier);
                    break;
                    
                case VSpline spline:
                    DrawSpline(dc, spline);
                    break;
                    
                case VArrow arrow:
                    DrawArrow(dc, arrow);
                    break;
                    
                case VDimension dim:
                    DrawDimension(dc, dim);
                    break;
                    
                case VGroup group:
                    DrawGroup(dc, group);
                    break;
            }
        }

        // Draw shape highlight (for Outliner hover)
        if (_highlightedShapeId.HasValue)
        {
            DrawShapeHighlight(dc, _highlightedShapeId.Value);
        }

        // Draw measuring tool overlay
        if (_measuringTool?.Mode == ToolMode.Measuring)
        {
            DrawMeasuringOverlay(dc);
        }

        // Draw drawing tool overlay
        if (_drawingTool?.Mode != DrawingMode.None)
        {
            DrawDrawingToolOverlay(dc);
        }

        // Draw selection overlay
        if (IsSelectionMode && _selectionTool != null)
        {
            DrawSelectionOverlay(dc);
        }
    }

    private void DrawDrawingToolOverlay(DrawingContext dc)
    {
        if (_drawingTool == null) return;

        // Draw snap indicator
        if (_drawingTool.CurrentSnap != null)
        {
            DrawSnapIndicator(dc, _drawingTool.CurrentSnap);
        }

        // Draw collected points as markers
        foreach (var point in _drawingTool.Points)
        {
            var screenPos = WorldToScreen(point.X, point.Y);
            dc.DrawEllipse(SnapMarkerBrushes.EndpointBrush, null, screenPos, 5, 5);
        }

        // Draw preview shape
        var previewShape = _drawingTool.GetPreviewShape();
        if (previewShape != null)
        {
            DrawPreviewShape(dc, previewShape);
        }
    }

    private void DrawPreviewShape(DrawingContext dc, Geometry.Shape shape)
    {
        // Use dashed gray pen for preview
        var previewBrush = new SolidColorBrush(Colors.Gray);
        previewBrush.Freeze();
        var previewPen = new Pen(previewBrush, 1.5) { DashStyle = DashStyles.Dash };
        previewPen.Freeze();

        switch (shape)
        {
            case VPoint point:
                var screenPoint = WorldToScreen(point.X, point.Y);
                dc.DrawEllipse(previewBrush, previewPen, screenPoint, PointRadius, PointRadius);
                break;

            case VLine line:
                var lineStart = WorldToScreen(line.Start.X, line.Start.Y);
                var lineEnd = WorldToScreen(line.End.X, line.End.Y);
                dc.DrawLine(previewPen, lineStart, lineEnd);
                break;

            case VCircle circle:
                var circleCenter = WorldToScreen(circle.Center.X, circle.Center.Y);
                var circleRadius = circle.Radius * _scale;
                dc.DrawEllipse(null, previewPen, circleCenter, circleRadius, circleRadius);
                break;

            case VRectangle rect:
                var rectTopLeft = WorldToScreen(rect.Corner.X, rect.Corner.Y + rect.Height);
                var rectWidth = rect.Width * _scale;
                var rectHeight = rect.Height * _scale;
                dc.DrawRectangle(null, previewPen, new Rect(rectTopLeft.X, rectTopLeft.Y, rectWidth, rectHeight));
                break;

            case VEllipse ellipse:
                var ellipseCenter = WorldToScreen(ellipse.Center.X, ellipse.Center.Y);
                var radiusX = ellipse.RadiusX * _scale;
                var radiusY = ellipse.RadiusY * _scale;
                dc.DrawEllipse(null, previewPen, ellipseCenter, radiusX, radiusY);
                break;

            case VArc arc:
                DrawArcPreview(dc, arc, previewPen);
                break;

            case VPolygon polygon:
                if (polygon.Points.Count > 1)
                {
                    var polyGeom = new StreamGeometry();
                    using (var ctx = polyGeom.Open())
                    {
                        var firstPt = WorldToScreen(polygon.Points[0].X, polygon.Points[0].Y);
                        ctx.BeginFigure(firstPt, false, true);
                        for (int i = 1; i < polygon.Points.Count; i++)
                        {
                            var pt = WorldToScreen(polygon.Points[i].X, polygon.Points[i].Y);
                            ctx.LineTo(pt, true, false);
                        }
                    }
                    polyGeom.Freeze();
                    dc.DrawGeometry(null, previewPen, polyGeom);
                }
                break;

            case VPolyline polyline:
                if (polyline.Points.Count > 1)
                {
                    var plGeom = new StreamGeometry();
                    using (var ctx = plGeom.Open())
                    {
                        var firstPt = WorldToScreen(polyline.Points[0].X, polyline.Points[0].Y);
                        ctx.BeginFigure(firstPt, false, false);
                        for (int i = 1; i < polyline.Points.Count; i++)
                        {
                            var pt = WorldToScreen(polyline.Points[i].X, polyline.Points[i].Y);
                            ctx.LineTo(pt, true, false);
                        }
                    }
                    plGeom.Freeze();
                    dc.DrawGeometry(null, previewPen, plGeom);
                }
                break;

            case VBezier bezier:
                var bezierPts = bezier.GetRenderPoints();
                if (bezierPts.Count > 1)
                {
                    var bezGeom = new StreamGeometry();
                    using (var ctx = bezGeom.Open())
                    {
                        var firstPt = WorldToScreen(bezierPts[0].X, bezierPts[0].Y);
                        ctx.BeginFigure(firstPt, false, false);
                        for (int i = 1; i < bezierPts.Count; i++)
                        {
                            var pt = WorldToScreen(bezierPts[i].X, bezierPts[i].Y);
                            ctx.LineTo(pt, true, false);
                        }
                    }
                    bezGeom.Freeze();
                    dc.DrawGeometry(null, previewPen, bezGeom);
                }
                // Also draw control point indicators
                var cp1 = WorldToScreen(bezier.P1.X, bezier.P1.Y);
                var cp2 = WorldToScreen(bezier.P2.X, bezier.P2.Y);
                dc.DrawEllipse(previewBrush, null, cp1, 4, 4);
                dc.DrawEllipse(previewBrush, null, cp2, 4, 4);
                break;

            case VSpline spline:
                var splinePts = spline.GetRenderPoints();
                if (splinePts.Count > 1)
                {
                    var spGeom = new StreamGeometry();
                    using (var ctx = spGeom.Open())
                    {
                        var firstPt = WorldToScreen(splinePts[0].X, splinePts[0].Y);
                        ctx.BeginFigure(firstPt, false, false);
                        for (int i = 1; i < splinePts.Count; i++)
                        {
                            var pt = WorldToScreen(splinePts[i].X, splinePts[i].Y);
                            ctx.LineTo(pt, true, false);
                        }
                    }
                    spGeom.Freeze();
                    dc.DrawGeometry(null, previewPen, spGeom);
                }
                break;

            case VArrow arrow:
                var arrowStart = WorldToScreen(arrow.Start.X, arrow.Start.Y);
                var arrowEnd = WorldToScreen(arrow.End.X, arrow.End.Y);
                dc.DrawLine(previewPen, arrowStart, arrowEnd);
                // Draw arrowhead
                var (wing1, wing2) = arrow.GetEndArrowhead();
                var screenWing1 = WorldToScreen(wing1.X, wing1.Y);
                var screenWing2 = WorldToScreen(wing2.X, wing2.Y);
                dc.DrawLine(previewPen, arrowEnd, screenWing1);
                dc.DrawLine(previewPen, arrowEnd, screenWing2);
                break;

            case VText text:
                var textPos = WorldToScreen(text.Location.X, text.Location.Y);
                var formattedText = new FormattedText(
                    text.Content,
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Consolas"),
                    text.Height * _scale,
                    previewBrush,
                    1.0);
                dc.DrawText(formattedText, new Point(textPos.X, textPos.Y - text.Height * _scale));
                break;
        }
    }

    private void DrawArcPreview(DrawingContext dc, VArc arc, Pen pen)
    {
        var center = WorldToScreen(arc.Center.X, arc.Center.Y);
        var radius = arc.Radius * _scale;

        var startAngle = arc.StartAngle * Math.PI / 180;
        var endAngle = arc.EndAngle * Math.PI / 180;

        var startPoint = new Point(
            center.X + radius * Math.Cos(-startAngle),
            center.Y + radius * Math.Sin(-startAngle));
        var endPoint = new Point(
            center.X + radius * Math.Cos(-endAngle),
            center.Y + radius * Math.Sin(-endAngle));

        var sweepAngle = endAngle - startAngle;
        while (sweepAngle < 0) sweepAngle += 2 * Math.PI;
        var isLargeArc = sweepAngle > Math.PI;

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(startPoint, false, false);
            ctx.ArcTo(endPoint, new Size(radius, radius), 0, isLargeArc, SweepDirection.Clockwise, true, false);
        }
        geometry.Freeze();
        dc.DrawGeometry(null, pen, geometry);
    }

    private void DrawMeasuringOverlay(DrawingContext dc)
    {
        if (_measuringTool == null) return;

        // Draw snap indicator
        if (_measuringTool.CurrentSnap != null)
        {
            DrawSnapIndicator(dc, _measuringTool.CurrentSnap);
        }

        // Draw measuring line if first point is set
        if (_measuringTool.FirstPoint != null)
        {
            var startScreen = WorldToScreen(_measuringTool.FirstPoint.X, _measuringTool.FirstPoint.Y);

            // Draw first point marker
            dc.DrawEllipse(SnapMarkerBrushes.EndpointBrush, null, startScreen, 6, 6);

            // Draw line to current position
            var endPoint = _measuringTool.GetEffectiveEndPoint();
            if (endPoint != null)
            {
                var endScreen = WorldToScreen(endPoint.X, endPoint.Y);
                dc.DrawLine(SnapMarkerBrushes.MeasuringLinePen, startScreen, endScreen);

                // Draw distance label at midpoint
                var distance = _measuringTool.GetCurrentDistance();
                if (distance.HasValue)
                {
                    var midScreen = new Point(
                        (startScreen.X + endScreen.X) / 2,
                        (startScreen.Y + endScreen.Y) / 2);

                    DrawDistanceLabel(dc, midScreen, distance.Value);
                }
            }
        }
    }

    private void DrawSelectionOverlay(DrawingContext dc)
    {
        if (_selectionTool == null) return;

        // Create selection brushes and pens
        var selectionBrush = new SolidColorBrush(Color.FromArgb(40, 0, 150, 255));
        selectionBrush.Freeze();
        var selectionPen = new Pen(new SolidColorBrush(Color.FromRgb(0, 150, 255)), 1.5);
        selectionPen.Freeze();
        var handleBrush = new SolidColorBrush(Colors.White);
        handleBrush.Freeze();
        var handlePen = new Pen(new SolidColorBrush(Color.FromRgb(0, 120, 215)), 1.5);
        handlePen.Freeze();

        // Draw selection box if dragging
        if (_selectionTool.IsBoxSelecting && _selectionTool.BoxStart != null && _selectionTool.BoxEnd != null)
        {
            var start = WorldToScreen(_selectionTool.BoxStart.X, _selectionTool.BoxStart.Y);
            var end = WorldToScreen(_selectionTool.BoxEnd.X, _selectionTool.BoxEnd.Y);

            var rect = new Rect(
                Math.Min(start.X, end.X),
                Math.Min(start.Y, end.Y),
                Math.Abs(end.X - start.X),
                Math.Abs(end.Y - start.Y));

            dc.DrawRectangle(selectionBrush, selectionPen, rect);
        }

        // Draw selection handles for selected shapes
        foreach (var shape in _selectionTool.SelectedShapes)
        {
            DrawSelectionHandles(dc, shape, handleBrush, handlePen, selectionPen);
        }
    }

    private void DrawSelectionHandles(DrawingContext dc, Shape shape, Brush handleBrush, Pen handlePen, Pen boundsPen)
    {
        const double handleSize = 8;
        const double smallHandleSize = 6;

        // Get bounding box
        var bounds = shape.GetBounds();
        var minScreen = WorldToScreen(bounds.min.X, bounds.max.Y);
        var maxScreen = WorldToScreen(bounds.max.X, bounds.min.Y);

        // Draw bounding box
        var boundsRect = new Rect(minScreen, maxScreen);
        dc.DrawRectangle(null, boundsPen, boundsRect);

        // Draw control points
        var controlPoints = shape.GetControlPoints();
        var moveBrush = new SolidColorBrush(Color.FromRgb(50, 205, 50)); // Green for move
        moveBrush.Freeze();
        var vertexBrush = new SolidColorBrush(Color.FromRgb(255, 165, 0)); // Orange for vertex
        vertexBrush.Freeze();
        var radiusBrush = new SolidColorBrush(Color.FromRgb(138, 43, 226)); // Purple for radius
        radiusBrush.Freeze();
        var curveBrush = new SolidColorBrush(Color.FromRgb(255, 105, 180)); // Pink for curve control
        curveBrush.Freeze();

        foreach (var cp in controlPoints)
        {
            var screenPos = WorldToScreen(cp.X, cp.Y);
            var size = cp.Type == ControlPointType.Move ? handleSize : smallHandleSize;

            Brush fillBrush = cp.Type switch
            {
                ControlPointType.Move => moveBrush,
                ControlPointType.Vertex => vertexBrush,
                ControlPointType.Radius => radiusBrush,
                ControlPointType.CurveControl => curveBrush,
                _ => handleBrush
            };

            if (cp.Type == ControlPointType.Move)
            {
                // Draw circle for move handle
                dc.DrawEllipse(fillBrush, handlePen, screenPos, size / 2, size / 2);
            }
            else if (cp.Type == ControlPointType.CurveControl)
            {
                // Draw diamond for curve control
                var diamond = new StreamGeometry();
                using (var ctx = diamond.Open())
                {
                    ctx.BeginFigure(new Point(screenPos.X, screenPos.Y - size / 2), true, true);
                    ctx.LineTo(new Point(screenPos.X + size / 2, screenPos.Y), true, false);
                    ctx.LineTo(new Point(screenPos.X, screenPos.Y + size / 2), true, false);
                    ctx.LineTo(new Point(screenPos.X - size / 2, screenPos.Y), true, false);
                }
                diamond.Freeze();
                dc.DrawGeometry(fillBrush, handlePen, diamond);
            }
            else
            {
                // Draw square for other handles
                dc.DrawRectangle(fillBrush, handlePen, new Rect(
                    screenPos.X - size / 2,
                    screenPos.Y - size / 2,
                    size,
                    size));
            }
        }
    }

    private void DrawSnapIndicator(DrawingContext dc, SnapResult snap)
    {
        var screenPos = WorldToScreen(snap.Point.X, snap.Point.Y);
        const double markerSize = 8;

        Brush markerBrush = snap.Type switch
        {
            SnapType.Endpoint => SnapMarkerBrushes.EndpointBrush,
            SnapType.Midpoint => SnapMarkerBrushes.MidpointBrush,
            SnapType.Center => SnapMarkerBrushes.CenterBrush,
            SnapType.Intersection => SnapMarkerBrushes.IntersectionBrush,
            SnapType.Nearest => SnapMarkerBrushes.NearestBrush,
            SnapType.Perpendicular => SnapMarkerBrushes.PerpendicularBrush,
            _ => Brushes.White
        };

        var markerPen = new Pen(markerBrush, 2);
        markerPen.Freeze();

        switch (snap.Type)
        {
            case SnapType.Endpoint:
                // Square marker
                dc.DrawRectangle(null, markerPen, new Rect(
                    screenPos.X - markerSize / 2, screenPos.Y - markerSize / 2,
                    markerSize, markerSize));
                break;

            case SnapType.Midpoint:
                // Triangle marker
                var triangle = new StreamGeometry();
                using (var ctx = triangle.Open())
                {
                    ctx.BeginFigure(new Point(screenPos.X, screenPos.Y - markerSize), false, true);
                    ctx.LineTo(new Point(screenPos.X - markerSize * 0.866, screenPos.Y + markerSize / 2), true, false);
                    ctx.LineTo(new Point(screenPos.X + markerSize * 0.866, screenPos.Y + markerSize / 2), true, false);
                }
                triangle.Freeze();
                dc.DrawGeometry(null, markerPen, triangle);
                break;

            case SnapType.Center:
                // Circle marker
                dc.DrawEllipse(null, markerPen, screenPos, markerSize / 2, markerSize / 2);
                break;

            case SnapType.Intersection:
                // X marker
                dc.DrawLine(markerPen,
                    new Point(screenPos.X - markerSize / 2, screenPos.Y - markerSize / 2),
                    new Point(screenPos.X + markerSize / 2, screenPos.Y + markerSize / 2));
                dc.DrawLine(markerPen,
                    new Point(screenPos.X + markerSize / 2, screenPos.Y - markerSize / 2),
                    new Point(screenPos.X - markerSize / 2, screenPos.Y + markerSize / 2));
                break;

            case SnapType.Nearest:
                // Diamond marker
                var diamond = new StreamGeometry();
                using (var ctx = diamond.Open())
                {
                    ctx.BeginFigure(new Point(screenPos.X, screenPos.Y - markerSize), false, true);
                    ctx.LineTo(new Point(screenPos.X + markerSize, screenPos.Y), true, false);
                    ctx.LineTo(new Point(screenPos.X, screenPos.Y + markerSize), true, false);
                    ctx.LineTo(new Point(screenPos.X - markerSize, screenPos.Y), true, false);
                }
                diamond.Freeze();
                dc.DrawGeometry(null, markerPen, diamond);
                break;

            case SnapType.Perpendicular:
                // Right angle marker
                var rightAngle = new StreamGeometry();
                using (var ctx = rightAngle.Open())
                {
                    ctx.BeginFigure(new Point(screenPos.X - markerSize, screenPos.Y), false, false);
                    ctx.LineTo(new Point(screenPos.X, screenPos.Y), true, false);
                    ctx.LineTo(new Point(screenPos.X, screenPos.Y - markerSize), true, false);
                }
                rightAngle.Freeze();
                dc.DrawGeometry(null, markerPen, rightAngle);
                break;
        }
    }

    private void DrawDistanceLabel(DrawingContext dc, Point screenPos, double distance)
    {
        var text = distance.ToString("F2");
        var typeface = new Typeface("Segoe UI");
        var formattedText = new FormattedText(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeface,
            14,
            Brushes.LimeGreen,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);

        // Draw background
        var padding = 4.0;
        var bgRect = new Rect(
            screenPos.X - formattedText.Width / 2 - padding,
            screenPos.Y - formattedText.Height / 2 - padding,
            formattedText.Width + padding * 2,
            formattedText.Height + padding * 2);

        var bgBrush = new SolidColorBrush(Color.FromArgb(200, 30, 30, 30));
        bgBrush.Freeze();
        dc.DrawRectangle(bgBrush, null, bgRect);

        // Draw text
        dc.DrawText(formattedText, new Point(
            screenPos.X - formattedText.Width / 2,
            screenPos.Y - formattedText.Height / 2));
    }

    private void DrawShapeHighlight(DrawingContext dc, long shapeId)
    {
        // Find the shape by ID
        var shape = _currentShapes.OfType<Shape>().FirstOrDefault(s => s.Id == shapeId);
        if (shape == null) return;

        // Get bounding box
        var bounds = shape.GetBounds();
        var minScreen = WorldToScreen(bounds.min.X, bounds.max.Y); // Y is inverted
        var maxScreen = WorldToScreen(bounds.max.X, bounds.min.Y);

        // Add padding in screen coordinates
        const double padding = 8;
        var highlightRect = new Rect(
            minScreen.X - padding,
            minScreen.Y - padding,
            (maxScreen.X - minScreen.X) + padding * 2,
            (maxScreen.Y - minScreen.Y) + padding * 2);

        // Create highlight brush from settings
        var highlightBrush = CreateHighlightBrush();

        // Draw highlight fill only (no stroke)
        dc.DrawRectangle(highlightBrush, null, highlightRect);
    }

    private Brush CreateHighlightBrush()
    {
        var settings = ApplicationSettings.Instance;
        try
        {
            var baseColor = (Color)ColorConverter.ConvertFromString(settings.HighlightColor);
            var alpha = (byte)(settings.HighlightOpacity * 255 / 100);
            var brush = new SolidColorBrush(Color.FromArgb(alpha, baseColor.R, baseColor.G, baseColor.B));
            brush.Freeze();
            return brush;
        }
        catch
        {
            // Fallback to yellow with 40% opacity
            var brush = new SolidColorBrush(Color.FromArgb(102, 255, 255, 0));
            brush.Freeze();
            return brush;
        }
    }

    private void DrawGrid(DrawingContext dc)
    {
        var gridPen = new Pen(GridBrush, 0.5);
        gridPen.Freeze();

        // Calculate adaptive spacing
        var spacing = CalculateAdaptiveSpacing();

        var topLeft = ScreenToWorld(0, 0);
        var bottomRight = ScreenToWorld(ActualWidth, ActualHeight);

        var startX = Math.Floor(topLeft.X / spacing) * spacing;
        var endX = Math.Ceiling(bottomRight.X / spacing) * spacing;
        var startY = Math.Floor(bottomRight.Y / spacing) * spacing;
        var endY = Math.Ceiling(topLeft.Y / spacing) * spacing;

        // Vertical lines
        for (var x = startX; x <= endX; x += spacing)
        {
            // Avoid drawing over the Y-axis (x=0) if possible, or let it draw over. 
            // The axis is drawn later so it will be on top anyway.
            if (Math.Abs(x) < 0.001) continue; 
            
            var screenX = WorldToScreen(x, 0).X;
            dc.DrawLine(gridPen, new Point(screenX, 0), new Point(screenX, ActualHeight));
        }

        // Horizontal lines
        for (var y = startY; y <= endY; y += spacing)
        {
            if (Math.Abs(y) < 0.001) continue;
            
            var screenY = WorldToScreen(0, y).Y;
            dc.DrawLine(gridPen, new Point(0, screenY), new Point(ActualWidth, screenY));
        }
    }

    private double CalculateAdaptiveSpacing()
    {
        // Target visual spacing in pixels (approx 50px)
        const double targetPixelSpacing = 50.0;
        
        // Calculate the theoretical world spacing to achieve target pixel spacing
        // world = pixels / scale
        double rawSpacing = targetPixelSpacing / _scale;

        // Find the nearest "nice" interval: 1, 2, 5, 10, 20, 50, etc.
        double powerOf10 = Math.Pow(10, Math.Floor(Math.Log10(rawSpacing)));
        double normalized = rawSpacing / powerOf10;

        double niceSpacing;
        if (normalized >= 5.0)       niceSpacing = 5.0;
        else if (normalized >= 2.0)  niceSpacing = 2.0;
        else                         niceSpacing = 1.0;

        return niceSpacing * powerOf10;
    }

    private void DrawAxes(DrawingContext dc)
    {
        var xAxisPen = new Pen(XAxisBrush, 1.5);
        xAxisPen.Freeze();

        var yAxisPen = new Pen(YAxisBrush, 1.5);
        yAxisPen.Freeze();

        // X-axis (horizontal, red)
        var xAxisY = WorldToScreen(0, 0).Y;
        if (xAxisY >= 0 && xAxisY <= ActualHeight)
        {
            dc.DrawLine(xAxisPen, new Point(0, xAxisY), new Point(ActualWidth, xAxisY));
        }

        // Y-axis (vertical, green)
        var yAxisX = WorldToScreen(0, 0).X;
        if (yAxisX >= 0 && yAxisX <= ActualWidth)
        {
            dc.DrawLine(yAxisPen, new Point(yAxisX, 0), new Point(yAxisX, ActualHeight));
        }
    }

    private void DrawPoint(DrawingContext dc, VPoint point)
    {
        if (point.DrawFactor <= 0 || point.Opacity <= 0) return;

        var applyOpacity = point.Opacity < 1.0;
        if (applyOpacity) dc.PushOpacity(point.Opacity);

        var screenPos = WorldToScreen(point.X, point.Y);
        var fill = GetCachedBrush(point.FillColor);
        var pen = GetCachedPen(point.StrokeColor, point.StrokeThickness);

        dc.DrawEllipse(fill, pen, screenPos, PointRadius, PointRadius);

        if (applyOpacity) dc.Pop();
    }

    private void DrawLine(DrawingContext dc, VLine line)
    {
        if (line.DrawFactor <= 0 || line.Opacity <= 0) return;

        var applyOpacity = line.Opacity < 1.0;
        if (applyOpacity) dc.PushOpacity(line.Opacity);

        // Apply rotation transform if needed
        var applyRotation = line.RotationAngle != 0 && line.RotationPivot != null;
        if (applyRotation)
        {
            var pivot = WorldToScreen(line.RotationPivot!.X + line.OffsetX, line.RotationPivot!.Y + line.OffsetY);
            // Negate angle because screen Y is inverted
            dc.PushTransform(new RotateTransform(-line.RotationAngle, pivot.X, pivot.Y));
        }

        // Apply offset for move animation
        var offsetX = line.OffsetX;
        var offsetY = line.OffsetY;

        var start = WorldToScreen(line.Start.X + offsetX, line.Start.Y + offsetY);
        var end = WorldToScreen(line.End.X + offsetX, line.End.Y + offsetY);
        var pen = GetCachedPen(line.StrokeColor, line.StrokeThickness);

        // Apply DrawFactor for animation (partial line drawing)
        if (line.DrawFactor < 1.0)
        {
            var dx = end.X - start.X;
            var dy = end.Y - start.Y;
            end = new Point(start.X + dx * line.DrawFactor, start.Y + dy * line.DrawFactor);
        }

        dc.DrawLine(pen, start, end);

        if (applyRotation) dc.Pop();
        if (applyOpacity) dc.Pop();
    }

    private void DrawArc(DrawingContext dc, VArc arc)
    {
        if (arc.DrawFactor <= 0 || arc.Opacity <= 0) return;

        var applyOpacity = arc.Opacity < 1.0;
        if (applyOpacity) dc.PushOpacity(arc.Opacity);

        // Apply offset for move animation
        var offsetX = arc.OffsetX;
        var offsetY = arc.OffsetY;

        var startAngleRad = arc.StartAngle * Math.PI / 180;

        // Apply DrawFactor - draw partial arc
        var effectiveEndAngle = arc.StartAngle + (arc.EndAngle - arc.StartAngle) * arc.DrawFactor;
        var endAngleRad = effectiveEndAngle * Math.PI / 180;

        var startWorldX = arc.Center.X + offsetX + arc.Radius * Math.Cos(startAngleRad);
        var startWorldY = arc.Center.Y + offsetY + arc.Radius * Math.Sin(startAngleRad);
        var endWorldX = arc.Center.X + offsetX + arc.Radius * Math.Cos(endAngleRad);
        var endWorldY = arc.Center.Y + offsetY + arc.Radius * Math.Sin(endAngleRad);

        var startScreen = WorldToScreen(startWorldX, startWorldY);
        var endScreen = WorldToScreen(endWorldX, endWorldY);

        var angleDiff = effectiveEndAngle - arc.StartAngle;
        if (angleDiff < 0) angleDiff += 360;
        var isLargeArc = angleDiff > 180;

        var screenRadius = arc.Radius * _scale;
        var pen = GetCachedPen(arc.StrokeColor, arc.StrokeThickness);

        // Use StreamGeometry for better performance
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(startScreen, false, false);
            ctx.ArcTo(endScreen, new Size(screenRadius, screenRadius), 0, isLargeArc, SweepDirection.Counterclockwise, true, false);
        }
        geometry.Freeze();

        dc.DrawGeometry(null, pen, geometry);

        if (applyOpacity) dc.Pop();
    }

    private void DrawCircle(DrawingContext dc, VCircle circle)
    {
        if (circle.DrawFactor <= 0 || circle.Opacity <= 0) return;

        var applyOpacity = circle.Opacity < 1.0;
        if (applyOpacity) dc.PushOpacity(circle.Opacity);

        // Apply rotation transform if needed
        var applyRotation = circle.RotationAngle != 0 && circle.RotationPivot != null;
        if (applyRotation)
        {
            var pivot = WorldToScreen(circle.RotationPivot!.X + circle.OffsetX, circle.RotationPivot!.Y + circle.OffsetY);
            dc.PushTransform(new RotateTransform(-circle.RotationAngle, pivot.X, pivot.Y));
        }

        // Apply offset for move animation
        var offsetX = circle.OffsetX;
        var offsetY = circle.OffsetY;

        var centerScreen = WorldToScreen(circle.Center.X + offsetX, circle.Center.Y + offsetY);
        var screenRadius = circle.Radius * _scale;
        var fill = GetCachedBrush(circle.FillColor);
        var pen = GetCachedPen(circle.StrokeColor, circle.StrokeThickness);

        // Apply DrawFactor - draw as arc from 0 to DrawFactor*360 degrees
        if (circle.DrawFactor < 1.0)
        {
            var endAngle = 360.0 * circle.DrawFactor;
            var endAngleRad = endAngle * Math.PI / 180;

            var startWorldX = circle.Center.X + offsetX + circle.Radius;
            var startWorldY = circle.Center.Y + offsetY;
            var endWorldX = circle.Center.X + offsetX + circle.Radius * Math.Cos(endAngleRad);
            var endWorldY = circle.Center.Y + offsetY + circle.Radius * Math.Sin(endAngleRad);

            var startScreen = WorldToScreen(startWorldX, startWorldY);
            var endScreen = WorldToScreen(endWorldX, endWorldY);

            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                ctx.BeginFigure(startScreen, false, false);
                ctx.ArcTo(endScreen, new Size(screenRadius, screenRadius), 0, endAngle > 180, SweepDirection.Counterclockwise, true, false);
            }
            geometry.Freeze();
            dc.DrawGeometry(null, pen, geometry);
        }
        else
        {
            dc.DrawEllipse(fill, pen, centerScreen, screenRadius, screenRadius);
        }

        if (applyRotation) dc.Pop();
        if (applyOpacity) dc.Pop();
    }

    private void DrawRectangle(DrawingContext dc, VRectangle rect)
    {
        if (rect.DrawFactor <= 0 || rect.Opacity <= 0) return;

        var applyOpacity = rect.Opacity < 1.0;
        if (applyOpacity) dc.PushOpacity(rect.Opacity);

        // Apply rotation transform if needed
        var applyRotation = rect.RotationAngle != 0 && rect.RotationPivot != null;
        if (applyRotation)
        {
            var pivot = WorldToScreen(rect.RotationPivot!.X + rect.OffsetX, rect.RotationPivot!.Y + rect.OffsetY);
            dc.PushTransform(new RotateTransform(-rect.RotationAngle, pivot.X, pivot.Y));
        }

        // Apply offset for move animation
        var offsetX = rect.OffsetX;
        var offsetY = rect.OffsetY;

        var corner = WorldToScreen(rect.Corner.X + offsetX, rect.Corner.Y + rect.Height + offsetY);
        var screenWidth = rect.Width * _scale;
        var screenHeight = rect.Height * _scale;
        var fill = GetCachedBrush(rect.FillColor);
        var pen = GetCachedPen(rect.StrokeColor, rect.StrokeThickness);

        // Apply DrawFactor - draw partial rectangle outline
        if (rect.DrawFactor < 1.0)
        {
            var perimeter = 2 * (rect.Width + rect.Height);
            var drawLength = perimeter * rect.DrawFactor;

            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                ctx.BeginFigure(corner, false, false);
                var remaining = drawLength;

                // Right edge
                if (remaining > 0)
                {
                    var len = Math.Min(remaining, screenWidth);
                    ctx.LineTo(new Point(corner.X + len, corner.Y), true, false);
                    remaining -= rect.Width;
                }
                // Bottom edge
                if (remaining > 0)
                {
                    var len = Math.Min(remaining, screenHeight);
                    ctx.LineTo(new Point(corner.X + screenWidth, corner.Y + len), true, false);
                    remaining -= rect.Height;
                }
                // Left edge
                if (remaining > 0)
                {
                    var len = Math.Min(remaining, screenWidth);
                    ctx.LineTo(new Point(corner.X + screenWidth - len, corner.Y + screenHeight), true, false);
                    remaining -= rect.Width;
                }
                // Top edge
                if (remaining > 0)
                {
                    var len = Math.Min(remaining, screenHeight);
                    ctx.LineTo(new Point(corner.X, corner.Y + screenHeight - len), true, false);
                }
            }
            geometry.Freeze();
            dc.DrawGeometry(null, pen, geometry);
        }
        else
        {
            dc.DrawRectangle(fill, pen, new Rect(corner.X, corner.Y, screenWidth, screenHeight));
        }

        if (applyRotation) dc.Pop();
        if (applyOpacity) dc.Pop();
    }

    private void DrawEllipse(DrawingContext dc, VEllipse ellipse)
    {
        if (ellipse.DrawFactor <= 0 || ellipse.Opacity <= 0) return;

        var applyOpacity = ellipse.Opacity < 1.0;
        if (applyOpacity) dc.PushOpacity(ellipse.Opacity);

        var centerScreen = WorldToScreen(ellipse.Center.X, ellipse.Center.Y);
        var screenRadiusX = ellipse.RadiusX * _scale;
        var screenRadiusY = ellipse.RadiusY * _scale;
        var fill = GetCachedBrush(ellipse.FillColor);
        var pen = GetCachedPen(ellipse.StrokeColor, ellipse.StrokeThickness);

        dc.DrawEllipse(fill, pen, centerScreen, screenRadiusX, screenRadiusY);

        if (applyOpacity) dc.Pop();
    }

    private void DrawPolygon(DrawingContext dc, VPolygon polygon)
    {
        if (polygon.Points.Count < 3 || polygon.DrawFactor <= 0 || polygon.Opacity <= 0) return;

        var applyOpacity = polygon.Opacity < 1.0;
        if (applyOpacity) dc.PushOpacity(polygon.Opacity);

        // Apply offset for move animation
        var offsetX = polygon.OffsetX;
        var offsetY = polygon.OffsetY;

        var fill = GetCachedBrush(polygon.FillColor);
        var pen = GetCachedPen(polygon.StrokeColor, polygon.StrokeThickness);

        // Apply DrawFactor - draw partial polygon outline
        var totalSegments = polygon.Points.Count; // includes closing segment
        var segmentsToDraw = polygon.DrawFactor * totalSegments;

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            var firstPoint = WorldToScreen(polygon.Points[0].X + offsetX, polygon.Points[0].Y + offsetY);
            ctx.BeginFigure(firstPoint, polygon.DrawFactor >= 1.0, polygon.DrawFactor >= 1.0);

            int fullSegments = (int)segmentsToDraw;
            double partialFraction = segmentsToDraw - fullSegments;

            // Draw full segments
            for (int i = 1; i <= fullSegments && i <= polygon.Points.Count; i++)
            {
                var idx = i % polygon.Points.Count;
                var pt = WorldToScreen(polygon.Points[idx].X + offsetX, polygon.Points[idx].Y + offsetY);
                ctx.LineTo(pt, true, false);
            }

            // Draw partial segment if needed
            if (partialFraction > 0 && fullSegments < polygon.Points.Count)
            {
                var prevIdx = fullSegments % polygon.Points.Count;
                var nextIdx = (fullSegments + 1) % polygon.Points.Count;
                var prevPt = WorldToScreen(polygon.Points[prevIdx].X + offsetX, polygon.Points[prevIdx].Y + offsetY);
                var nextPt = WorldToScreen(polygon.Points[nextIdx].X + offsetX, polygon.Points[nextIdx].Y + offsetY);
                var partialPt = new Point(
                    prevPt.X + (nextPt.X - prevPt.X) * partialFraction,
                    prevPt.Y + (nextPt.Y - prevPt.Y) * partialFraction);
                ctx.LineTo(partialPt, true, false);
            }
        }
        geometry.Freeze();

        dc.DrawGeometry(polygon.DrawFactor >= 1.0 ? fill : null, pen, geometry);

        if (applyOpacity) dc.Pop();
    }

    private void DrawPolyline(DrawingContext dc, VPolyline polyline)
    {
        if (polyline.Points.Count < 2 || polyline.DrawFactor <= 0 || polyline.Opacity <= 0) return;

        var applyOpacity = polyline.Opacity < 1.0;
        if (applyOpacity) dc.PushOpacity(polyline.Opacity);

        // Apply offset for move animation
        var offsetX = polyline.OffsetX;
        var offsetY = polyline.OffsetY;

        var pen = GetCachedPen(polyline.StrokeColor, polyline.StrokeThickness);

        // Apply DrawFactor - draw partial polyline
        var totalSegments = polyline.Points.Count - 1;
        var segmentsToDraw = polyline.DrawFactor * totalSegments;

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            var firstPoint = WorldToScreen(polyline.Points[0].X + offsetX, polyline.Points[0].Y + offsetY);
            ctx.BeginFigure(firstPoint, false, false);

            for (int i = 1; i < polyline.Points.Count && i <= segmentsToDraw + 1; i++)
            {
                var pt = WorldToScreen(polyline.Points[i].X + offsetX, polyline.Points[i].Y + offsetY);

                // Partial last segment
                if (i > segmentsToDraw && i <= segmentsToDraw + 1)
                {
                    var prevPt = WorldToScreen(polyline.Points[i - 1].X + offsetX, polyline.Points[i - 1].Y + offsetY);
                    var fraction = segmentsToDraw - (i - 1);
                    var partialPt = new Point(
                        prevPt.X + (pt.X - prevPt.X) * fraction,
                        prevPt.Y + (pt.Y - prevPt.Y) * fraction);
                    ctx.LineTo(partialPt, true, false);
                }
                else
                {
                    ctx.LineTo(pt, true, false);
                }
            }
        }
        geometry.Freeze();

        dc.DrawGeometry(null, pen, geometry);

        if (applyOpacity) dc.Pop();
    }

    private void DrawText(DrawingContext dc, VText text)
    {
        if (string.IsNullOrEmpty(text.Content) || text.DrawFactor <= 0 || text.Opacity <= 0)
            return;

        var applyOpacity = text.Opacity < 1.0;
        if (applyOpacity) dc.PushOpacity(text.Opacity);

        var screenPos = WorldToScreen(text.Location.X, text.Location.Y);
        var brush = GetCachedBrush(text.StrokeColor);

        // Scale font size with zoom, but keep it readable
        var fontSize = text.Height * _scale;
        fontSize = Math.Max(fontSize, 6); // Minimum readable size

        var typeface = new Typeface("Segoe UI");
        var formattedText = new FormattedText(
            text.Content,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeface,
            fontSize,
            brush,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);

        // Draw text with origin at bottom-left (mathematical coordinate style)
        dc.DrawText(formattedText, new Point(screenPos.X, screenPos.Y - formattedText.Height));

        if (applyOpacity) dc.Pop();
    }

    private void DrawBezier(DrawingContext dc, VBezier bezier)
    {
        if (bezier.DrawFactor <= 0 || bezier.Opacity <= 0) return;

        var applyOpacity = bezier.Opacity < 1.0;
        if (applyOpacity) dc.PushOpacity(bezier.Opacity);

        // Apply offset for move animation
        var offsetX = bezier.OffsetX;
        var offsetY = bezier.OffsetY;

        var pen = GetCachedPen(bezier.StrokeColor, bezier.StrokeThickness);
        var points = bezier.GetRenderPoints();
        if (points.Count < 2)
        {
            if (applyOpacity) dc.Pop();
            return;
        }

        // Apply DrawFactor - draw partial bezier
        var pointsToDraw = (int)Math.Ceiling(points.Count * bezier.DrawFactor);
        pointsToDraw = Math.Max(2, Math.Min(pointsToDraw, points.Count));

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            var first = WorldToScreen(points[0].X + offsetX, points[0].Y + offsetY);
            ctx.BeginFigure(first, false, false);
            for (int i = 1; i < pointsToDraw; i++)
            {
                var pt = WorldToScreen(points[i].X + offsetX, points[i].Y + offsetY);
                ctx.LineTo(pt, true, false);
            }
        }
        geometry.Freeze();
        dc.DrawGeometry(null, pen, geometry);

        if (applyOpacity) dc.Pop();
    }

    private void DrawSpline(DrawingContext dc, VSpline spline)
    {
        if (spline.Opacity <= 0) return;

        var applyOpacity = spline.Opacity < 1.0;
        if (applyOpacity) dc.PushOpacity(spline.Opacity);

        var pen = GetCachedPen(spline.StrokeColor, spline.StrokeThickness);
        var points = spline.GetRenderPoints();
        if (points.Count < 2)
        {
            if (applyOpacity) dc.Pop();
            return;
        }

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            var first = WorldToScreen(points[0].X, points[0].Y);
            ctx.BeginFigure(first, false, false);
            for (int i = 1; i < points.Count; i++)
            {
                var pt = WorldToScreen(points[i].X, points[i].Y);
                ctx.LineTo(pt, true, false);
            }
        }
        geometry.Freeze();
        dc.DrawGeometry(null, pen, geometry);

        if (applyOpacity) dc.Pop();
    }

    private void DrawArrow(DrawingContext dc, VArrow arrow)
    {
        if (arrow.DrawFactor <= 0 || arrow.Opacity <= 0) return;

        var applyOpacity = arrow.Opacity < 1.0;
        if (applyOpacity) dc.PushOpacity(arrow.Opacity);

        // Apply rotation transform if needed
        var applyRotation = arrow.RotationAngle != 0 && arrow.RotationPivot != null;
        if (applyRotation)
        {
            var pivot = WorldToScreen(arrow.RotationPivot!.X + arrow.OffsetX, arrow.RotationPivot!.Y + arrow.OffsetY);
            dc.PushTransform(new RotateTransform(-arrow.RotationAngle, pivot.X, pivot.Y));
        }

        var pen = GetCachedPen(arrow.StrokeColor, arrow.StrokeThickness);
        var brush = GetCachedBrush(arrow.StrokeColor);  // Use stroke color for filled arrowhead
        var start = WorldToScreen(arrow.Start.X + arrow.OffsetX, arrow.Start.Y + arrow.OffsetY);
        var fullEnd = WorldToScreen(arrow.End.X + arrow.OffsetX, arrow.End.Y + arrow.OffsetY);

        // Apply DrawFactor for animation (partial arrow drawing)
        Point end = fullEnd;
        if (arrow.DrawFactor < 1.0)
        {
            // Calculate partial end point
            var dx = fullEnd.X - start.X;
            var dy = fullEnd.Y - start.Y;
            end = new Point(start.X + dx * arrow.DrawFactor, start.Y + dy * arrow.DrawFactor);
        }

        // Draw main line
        dc.DrawLine(pen, start, end);

        // Draw filled end arrowhead (at the current end position)
        // Calculate arrowhead based on current draw progress
        var arrowDirX = arrow.End.X - arrow.Start.X;
        var arrowDirY = arrow.End.Y - arrow.Start.Y;
        var currentEndX = arrow.Start.X + arrow.OffsetX + arrowDirX * arrow.DrawFactor;
        var currentEndY = arrow.Start.Y + arrow.OffsetY + arrowDirY * arrow.DrawFactor;

        // Get arrowhead wings relative to current end position
        var length = Math.Sqrt(arrowDirX * arrowDirX + arrowDirY * arrowDirY);
        if (length > 0)
        {
            var dirX = arrowDirX / length;
            var dirY = arrowDirY / length;
            var perpX = -dirY;
            var perpY = dirX;
            var headLen = arrow.HeadLength;
            var halfWidth = headLen / 6.0;  // Match VArrow's calculation

            var w1 = WorldToScreen(currentEndX - dirX * headLen + perpX * halfWidth,
                                   currentEndY - dirY * headLen + perpY * halfWidth);
            var w2 = WorldToScreen(currentEndX - dirX * headLen - perpX * halfWidth,
                                   currentEndY - dirY * headLen - perpY * halfWidth);

            var arrowHead = new StreamGeometry();
            using (var ctx = arrowHead.Open())
            {
                ctx.BeginFigure(end, true, true);  // Start at tip, filled, closed
                ctx.LineTo(w1, true, false);
                ctx.LineTo(w2, true, false);
            }
            arrowHead.Freeze();
            dc.DrawGeometry(brush, pen, arrowHead);
        }

        // Draw start arrowhead if double-ended (only when fully drawn)
        if (arrow.DoubleEnded && arrow.DrawFactor >= 1.0)
        {
            var (sw1, sw2) = arrow.GetStartArrowhead();
            var sw1Screen = WorldToScreen(sw1.X + arrow.OffsetX, sw1.Y + arrow.OffsetY);
            var sw2Screen = WorldToScreen(sw2.X + arrow.OffsetX, sw2.Y + arrow.OffsetY);
            var startHead = new StreamGeometry();
            using (var ctx = startHead.Open())
            {
                ctx.BeginFigure(start, true, true);  // Start at tip, filled, closed
                ctx.LineTo(sw1Screen, true, false);
                ctx.LineTo(sw2Screen, true, false);
            }
            startHead.Freeze();
            dc.DrawGeometry(brush, pen, startHead);
        }

        if (applyRotation) dc.Pop();
        if (applyOpacity) dc.Pop();
    }

    private void DrawDimension(DrawingContext dc, VDimension dim)
    {
        if (dim.DrawFactor <= 0 || dim.Opacity <= 0) return;

        var applyOpacity = dim.Opacity < 1.0;
        if (applyOpacity) dc.PushOpacity(dim.Opacity);

        var pen = GetCachedPen(dim.StrokeColor, dim.StrokeThickness);
        var (dimStart, dimEnd, textPos, ext1Start, ext1End, ext2Start, ext2End) = dim.GetDimensionGeometry();

        // Draw dimension line
        var ds = WorldToScreen(dimStart.X, dimStart.Y);
        var de = WorldToScreen(dimEnd.X, dimEnd.Y);
        dc.DrawLine(pen, ds, de);

        // Draw extension lines
        dc.DrawLine(pen, WorldToScreen(ext1Start.X, ext1Start.Y), WorldToScreen(ext1End.X, ext1End.Y));
        dc.DrawLine(pen, WorldToScreen(ext2Start.X, ext2Start.Y), WorldToScreen(ext2End.X, ext2End.Y));

        // Draw text
        var brush = GetCachedBrush(dim.StrokeColor);
        var fontSize = dim.TextHeight * _scale;
        fontSize = Math.Max(fontSize, 8);
        var typeface = new Typeface("Segoe UI");
        var formattedText = new FormattedText(
            dim.DisplayText,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeface,
            fontSize,
            brush,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);

        var tp = WorldToScreen(textPos.X, textPos.Y);
        dc.DrawText(formattedText, new Point(tp.X - formattedText.Width / 2, tp.Y - formattedText.Height / 2));

        if (applyOpacity) dc.Pop();
    }

    private void DrawGroup(DrawingContext dc, VGroup group)
    {
        // Groups don't draw themselves - shapes inside are added individually via Draw()
        // This is here for completeness if groups are added directly to renderer
    }

    public void ZoomExtents(IEnumerable<IDrawable> shapes)
    {
        var shapeList = shapes.ToList();
        if (!shapeList.Any() || ActualWidth <= 0 || ActualHeight <= 0)
        {
            _scale = 1.0;
            _panX = 0;
            _panY = 0;
            RedrawAll();
            return;
        }

        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;

        foreach (var shape in shapeList)
        {
            switch (shape)
            {
                case VPoint point:
                    UpdateBounds(ref minX, ref maxX, ref minY, ref maxY, point.X, point.Y);
                    break;
                case VLine line:
                    UpdateBounds(ref minX, ref maxX, ref minY, ref maxY, line.Start.X, line.Start.Y);
                    UpdateBounds(ref minX, ref maxX, ref minY, ref maxY, line.End.X, line.End.Y);
                    break;
                case VArc arc:
                    UpdateBounds(ref minX, ref maxX, ref minY, ref maxY, arc.Center.X - arc.Radius, arc.Center.Y - arc.Radius);
                    UpdateBounds(ref minX, ref maxX, ref minY, ref maxY, arc.Center.X + arc.Radius, arc.Center.Y + arc.Radius);
                    break;
                case VCircle circle:
                    UpdateBounds(ref minX, ref maxX, ref minY, ref maxY, circle.Center.X - circle.Radius, circle.Center.Y - circle.Radius);
                    UpdateBounds(ref minX, ref maxX, ref minY, ref maxY, circle.Center.X + circle.Radius, circle.Center.Y + circle.Radius);
                    break;
                case VRectangle rect:
                    UpdateBounds(ref minX, ref maxX, ref minY, ref maxY, rect.Corner.X, rect.Corner.Y);
                    UpdateBounds(ref minX, ref maxX, ref minY, ref maxY, rect.Corner.X + rect.Width, rect.Corner.Y + rect.Height);
                    break;
                case VEllipse ellipse:
                    UpdateBounds(ref minX, ref maxX, ref minY, ref maxY, ellipse.Center.X - ellipse.RadiusX, ellipse.Center.Y - ellipse.RadiusY);
                    UpdateBounds(ref minX, ref maxX, ref minY, ref maxY, ellipse.Center.X + ellipse.RadiusX, ellipse.Center.Y + ellipse.RadiusY);
                    break;
                case VPolygon polygon:
                    foreach (var p in polygon.Points)
                        UpdateBounds(ref minX, ref maxX, ref minY, ref maxY, p.X, p.Y);
                    break;
                case VPolyline polyline:
                    foreach (var p in polyline.Points)
                        UpdateBounds(ref minX, ref maxX, ref minY, ref maxY, p.X, p.Y);
                    break;
                case VText text:
                    UpdateBounds(ref minX, ref maxX, ref minY, ref maxY, text.Location.X, text.Location.Y);
                    break;
                case VBezier bezier:
                    UpdateBounds(ref minX, ref maxX, ref minY, ref maxY, bezier.P0.X, bezier.P0.Y);
                    UpdateBounds(ref minX, ref maxX, ref minY, ref maxY, bezier.P1.X, bezier.P1.Y);
                    UpdateBounds(ref minX, ref maxX, ref minY, ref maxY, bezier.P2.X, bezier.P2.Y);
                    UpdateBounds(ref minX, ref maxX, ref minY, ref maxY, bezier.P3.X, bezier.P3.Y);
                    break;
                case VSpline spline:
                    foreach (var p in spline.ControlPoints)
                        UpdateBounds(ref minX, ref maxX, ref minY, ref maxY, p.X, p.Y);
                    break;
                case VArrow arrow:
                    UpdateBounds(ref minX, ref maxX, ref minY, ref maxY, arrow.Start.X, arrow.Start.Y);
                    UpdateBounds(ref minX, ref maxX, ref minY, ref maxY, arrow.End.X, arrow.End.Y);
                    break;
                case VDimension dim:
                    UpdateBounds(ref minX, ref maxX, ref minY, ref maxY, dim.Point1.X, dim.Point1.Y);
                    UpdateBounds(ref minX, ref maxX, ref minY, ref maxY, dim.Point2.X, dim.Point2.Y);
                    break;
                case VGroup group:
                    var bounds = group.GetBounds();
                    UpdateBounds(ref minX, ref maxX, ref minY, ref maxY, bounds.min.X, bounds.min.Y);
                    UpdateBounds(ref minX, ref maxX, ref minY, ref maxY, bounds.max.X, bounds.max.Y);
                    break;
            }
        }

        var padding = 50.0;
        var worldWidth = maxX - minX;
        var worldHeight = maxY - minY;

        if (worldWidth < 1) worldWidth = 100;
        if (worldHeight < 1) worldHeight = 100;

        var worldCenterX = (minX + maxX) / 2;
        var worldCenterY = (minY + maxY) / 2;

        var availableWidth = ActualWidth - padding * 2;
        var availableHeight = ActualHeight - padding * 2;

        var scaleX = availableWidth / worldWidth;
        var scaleY = availableHeight / worldHeight;
        _scale = Math.Min(scaleX, scaleY);
        _scale = Math.Clamp(_scale, MinZoom, MaxZoom);

        _panX = -worldCenterX * _scale;
        _panY = worldCenterY * _scale;

        RedrawAll();
    }

    private static void UpdateBounds(ref double minX, ref double maxX, ref double minY, ref double maxY, double x, double y)
    {
        minX = Math.Min(minX, x);
        maxX = Math.Max(maxX, x);
        minY = Math.Min(minY, y);
        maxY = Math.Max(maxY, y);
    }

    /// <summary>
    /// Finds a shape by its unique ID and zooms the canvas to fit it.
    /// </summary>
    /// <param name="id">The unique ID of the shape to zoom to.</param>
    /// <returns>True if the shape was found and zoomed to, false otherwise.</returns>
    public bool ZoomToShape(long id)
    {
        var shape = _currentShapes.OfType<Shape>().FirstOrDefault(s => s.Id == id);
        if (shape == null)
            return false;

        ZoomExtents(new[] { shape }, minWorldSize: 10);
        return true;
    }

    /// <summary>
    /// Zooms the canvas to fit the given shapes with a specified minimum world size.
    /// </summary>
    public void ZoomExtents(IEnumerable<IDrawable> shapes, double minWorldSize)
    {
        var shapeList = shapes.ToList();
        if (!shapeList.Any() || ActualWidth <= 0 || ActualHeight <= 0)
        {
            _scale = 1.0;
            _panX = 0;
            _panY = 0;
            RedrawAll();
            return;
        }

        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;

        foreach (var shape in shapeList)
        {
            switch (shape)
            {
                case VPoint point:
                    UpdateBounds(ref minX, ref maxX, ref minY, ref maxY, point.X, point.Y);
                    break;
                case VLine line:
                    UpdateBounds(ref minX, ref maxX, ref minY, ref maxY, line.Start.X, line.Start.Y);
                    UpdateBounds(ref minX, ref maxX, ref minY, ref maxY, line.End.X, line.End.Y);
                    break;
                case VArc arc:
                    UpdateBounds(ref minX, ref maxX, ref minY, ref maxY, arc.Center.X - arc.Radius, arc.Center.Y - arc.Radius);
                    UpdateBounds(ref minX, ref maxX, ref minY, ref maxY, arc.Center.X + arc.Radius, arc.Center.Y + arc.Radius);
                    break;
                case VCircle circle:
                    UpdateBounds(ref minX, ref maxX, ref minY, ref maxY, circle.Center.X - circle.Radius, circle.Center.Y - circle.Radius);
                    UpdateBounds(ref minX, ref maxX, ref minY, ref maxY, circle.Center.X + circle.Radius, circle.Center.Y + circle.Radius);
                    break;
                case VRectangle rect:
                    UpdateBounds(ref minX, ref maxX, ref minY, ref maxY, rect.Corner.X, rect.Corner.Y);
                    UpdateBounds(ref minX, ref maxX, ref minY, ref maxY, rect.Corner.X + rect.Width, rect.Corner.Y + rect.Height);
                    break;
                case VEllipse ellipse:
                    UpdateBounds(ref minX, ref maxX, ref minY, ref maxY, ellipse.Center.X - ellipse.RadiusX, ellipse.Center.Y - ellipse.RadiusY);
                    UpdateBounds(ref minX, ref maxX, ref minY, ref maxY, ellipse.Center.X + ellipse.RadiusX, ellipse.Center.Y + ellipse.RadiusY);
                    break;
                case VPolygon polygon:
                    foreach (var p in polygon.Points)
                        UpdateBounds(ref minX, ref maxX, ref minY, ref maxY, p.X, p.Y);
                    break;
                case VPolyline polyline:
                    foreach (var p in polyline.Points)
                        UpdateBounds(ref minX, ref maxX, ref minY, ref maxY, p.X, p.Y);
                    break;
                case VText text:
                    UpdateBounds(ref minX, ref maxX, ref minY, ref maxY, text.Location.X, text.Location.Y);
                    break;
                case VBezier bezier:
                    UpdateBounds(ref minX, ref maxX, ref minY, ref maxY, bezier.P0.X, bezier.P0.Y);
                    UpdateBounds(ref minX, ref maxX, ref minY, ref maxY, bezier.P1.X, bezier.P1.Y);
                    UpdateBounds(ref minX, ref maxX, ref minY, ref maxY, bezier.P2.X, bezier.P2.Y);
                    UpdateBounds(ref minX, ref maxX, ref minY, ref maxY, bezier.P3.X, bezier.P3.Y);
                    break;
                case VSpline spline:
                    foreach (var p in spline.ControlPoints)
                        UpdateBounds(ref minX, ref maxX, ref minY, ref maxY, p.X, p.Y);
                    break;
                case VArrow arrow:
                    UpdateBounds(ref minX, ref maxX, ref minY, ref maxY, arrow.Start.X, arrow.Start.Y);
                    UpdateBounds(ref minX, ref maxX, ref minY, ref maxY, arrow.End.X, arrow.End.Y);
                    break;
                case VDimension dim:
                    UpdateBounds(ref minX, ref maxX, ref minY, ref maxY, dim.Point1.X, dim.Point1.Y);
                    UpdateBounds(ref minX, ref maxX, ref minY, ref maxY, dim.Point2.X, dim.Point2.Y);
                    break;
                case VGroup group:
                    var bounds = group.GetBounds();
                    UpdateBounds(ref minX, ref maxX, ref minY, ref maxY, bounds.min.X, bounds.min.Y);
                    UpdateBounds(ref minX, ref maxX, ref minY, ref maxY, bounds.max.X, bounds.max.Y);
                    break;
            }
        }

        var padding = 50.0;
        var worldWidth = maxX - minX;
        var worldHeight = maxY - minY;

        // Ensure minimum world size for better visibility
        if (worldWidth < minWorldSize) worldWidth = minWorldSize;
        if (worldHeight < minWorldSize) worldHeight = minWorldSize;

        var worldCenterX = (minX + maxX) / 2;
        var worldCenterY = (minY + maxY) / 2;

        var availableWidth = ActualWidth - padding * 2;
        var availableHeight = ActualHeight - padding * 2;

        var scaleX = availableWidth / worldWidth;
        var scaleY = availableHeight / worldHeight;
        _scale = Math.Min(scaleX, scaleY);
        _scale = Math.Clamp(_scale, MinZoom, MaxZoom);

        _panX = -worldCenterX * _scale;
        _panY = worldCenterY * _scale;

        RedrawAll();
    }
}
