using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Code2Viz.Geometry;
using Point = System.Windows.Point;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Colors = System.Windows.Media.Colors;
using Pen = System.Windows.Media.Pen;
using Size = System.Windows.Size;
using Rect = System.Windows.Rect;
using DashStyle = System.Windows.Media.DashStyle;
using DashStyles = System.Windows.Media.DashStyles;
using PenLineCap = System.Windows.Media.PenLineCap;
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
    public static readonly Brush ExtensionBrush;
    public static readonly Brush TangentBrush;
    public static readonly Pen ExtensionLinePen;
    public static readonly Pen PerpendicularLinePen;
    public static readonly Pen TangentLinePen;
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

        ExtensionBrush = new SolidColorBrush(Colors.DeepSkyBlue);
        ExtensionBrush.Freeze();

        TangentBrush = new SolidColorBrush(Colors.Violet);
        TangentBrush.Freeze();

        ExtensionLinePen = new Pen(ExtensionBrush, 1) { DashStyle = DashStyles.Dot };
        ExtensionLinePen.Freeze();

        PerpendicularLinePen = new Pen(PerpendicularBrush, 1) { DashStyle = DashStyles.Dot };
        PerpendicularLinePen.Freeze();

        TangentLinePen = new Pen(TangentBrush, 1) { DashStyle = DashStyles.Dot };
        TangentLinePen.Freeze();

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

    // Viewport transformation (encapsulates scale/pan/coordinate conversion)
    private readonly ViewportTransform _viewport = new();

    private Point _lastMousePosition;
    private bool _isPanning = false;
    private bool _showGrid = true;
    private double _gridSpacing = 50;

    /// <summary>
    /// Current zoom scale factor. Read-only; use zoom methods to modify.
    /// </summary>
    public double Scale => _viewport.Scale;

    private List<IDrawable> _currentShapes = new();
    private readonly DrawingVisual _visual;
    private QuadTree? _spatialIndex;

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

    /// <summary>
    /// When enabled, the effective cursor position snaps to the nearest grid intersection.
    /// </summary>
    public bool SnapToGrid { get; set; } = false;

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
    private static readonly Dictionary<(string color, double thickness, LineType style), Pen> _penCache = new();
    private static readonly Dictionary<(string color, double thickness, LineType style, double scale), Pen> _scaledPenCache = new();

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
        Focusable = true; // Allow canvas to receive keyboard focus

        MouseWheel += OnMouseWheel;
        MouseDown += OnMouseDown;
        MouseUp += OnMouseUp;
        MouseMove += OnMouseMove;
        SizeChanged += OnSizeChanged;
        PreviewKeyDown += OnPreviewKeyDown;
    }

    /// <summary>
    /// Handles keyboard input for drawing tool when canvas has focus.
    /// </summary>
    private void OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        // Only handle keys when drawing and waiting for next point
        if (DrawingTool.Mode == DrawingMode.None || DrawingTool.Points.Count == 0)
            return;

        var key = e.Key;

        // Escape cancels input mode
        if (key == System.Windows.Input.Key.Escape)
        {
            if (DrawingTool.InputMode != DrawingInputMode.None)
            {
                DrawingTool.HandleEscapeInput();
                Refresh();
                e.Handled = true;
            }
            return;
        }

        // Tab cycles through input modes (None -> Distance -> Angle -> None)
        if (key == System.Windows.Input.Key.Tab)
        {
            e.Handled = true;
            if (DrawingTool.CycleInputMode())
            {
                Refresh();
            }
            return;
        }

        // Enter confirms input
        if (key == System.Windows.Input.Key.Enter)
        {
            if (DrawingTool.InputMode != DrawingInputMode.None)
            {
                // Let MainWindow handle the Enter to place the point
                return;
            }
        }

        // Backspace removes last character
        if (key == System.Windows.Input.Key.Back)
        {
            if (DrawingTool.HandleBackspace())
            {
                Refresh();
                e.Handled = true;
            }
            return;
        }

        // Number keys (0-9) - start distance input if not already in input mode
        char? inputChar = null;
        if (key >= System.Windows.Input.Key.D0 && key <= System.Windows.Input.Key.D9)
        {
            inputChar = (char)('0' + (key - System.Windows.Input.Key.D0));
        }
        else if (key >= System.Windows.Input.Key.NumPad0 && key <= System.Windows.Input.Key.NumPad9)
        {
            inputChar = (char)('0' + (key - System.Windows.Input.Key.NumPad0));
        }
        else if (key == System.Windows.Input.Key.OemPeriod || key == System.Windows.Input.Key.Decimal)
        {
            inputChar = '.';
        }
        else if (key == System.Windows.Input.Key.OemMinus || key == System.Windows.Input.Key.Subtract)
        {
            inputChar = '-';
        }

        if (inputChar.HasValue)
        {
            // Start Distance mode if not already in input mode
            if (DrawingTool.InputMode == DrawingInputMode.None)
            {
                DrawingTool.StartDistanceInput();
            }

            if (DrawingTool.HandleCharInput(inputChar.Value))
            {
                Refresh();
                e.Handled = true;
            }
        }
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
        _viewport.SetViewportSize(ActualWidth, ActualHeight);
        RedrawAll();
    }

    public void CenterOrigin()
    {
        _viewport.Reset();
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
        => _viewport.WorldToScreen(worldX, worldY);

    // Convert screen coordinates to world coordinates
    private Point ScreenToWorld(double screenX, double screenY)
        => _viewport.ScreenToWorld(screenX, screenY);

    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var mouseScreenPos = e.GetPosition(this);
        _viewport.ZoomAtPoint(mouseScreenPos.X, mouseScreenPos.Y, e.Delta > 0);
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

                // Check for double-click on empty space to zoom extents
                if (e.ClickCount == 2)
                {
                    var hitShape = _selectionTool.HitTest(vPoint, _currentShapes, _viewport.Scale);
                    if (hitShape == null)
                    {
                        ZoomExtents(_currentShapes);
                        e.Handled = true;
                        return;
                    }
                }

                _selectionTool.OnMouseDown(vPoint, shift, ctrl, _currentShapes, _viewport.Scale);

                if (_selectionTool.IsBoxSelecting || _selectionTool.IsDraggingHandle)
                {
                    CaptureMouse();
                }

                RedrawAll();
                e.Handled = true;
                return;
            }

            // Double-click on empty space: Zoom to Fit
            if (e.ClickCount == 2)
            {
                ZoomExtents(_currentShapes);
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

        if (SnapToGrid && !_isPanning)
        {
            var snapped = SnapPointToGrid(worldPos.X, worldPos.Y);
            worldPos = new Point(snapped.X, snapped.Y);
        }

        MouseWorldPositionChanged?.Invoke(this, worldPos);

        if (_isPanning)
        {
            _viewport.Pan(screenPos.X - _lastMousePosition.X, screenPos.Y - _lastMousePosition.Y);
            _lastMousePosition = screenPos;
            RedrawAll();
        }
        else if (_drawingTool != null && _drawingTool.Mode != DrawingMode.None)
        {
            // Update drawing tool with cursor position (use spatial index for O(log n) snap detection)
            // Check for Shift key to enable orthogonal constraint
            _drawingTool.IsOrthoMode = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);
            _drawingTool.OnMouseMove(new VPoint(worldPos.X, worldPos.Y), _currentShapes, _viewport.Scale, _spatialIndex);
            RedrawAll();

            // Focus canvas when drawing to enable keyboard input for distance/angle
            if (_drawingTool.Points.Count > 0 && !IsFocused)
            {
                Focus();
            }
        }
        else if (_measuringTool?.Mode == ToolMode.Measuring)
        {
            // Update measuring tool with cursor position (use spatial index for O(log n) snap detection)
            _measuringTool.OnMouseMove(new VPoint(worldPos.X, worldPos.Y), _currentShapes, _viewport.Scale, _spatialIndex);
            RedrawAll();
        }
        else if (_selectionTool?.IsBoxSelecting == true || _selectionTool?.IsDraggingHandle == true)
        {
            // Update selection box or handle drag (with snapping support, use spatial index for O(log n) performance)
            _selectionTool.OnMouseMove(new VPoint(worldPos.X, worldPos.Y), _currentShapes, _viewport.Scale, _spatialIndex);
            RedrawAll();
        }
    }

    public void ClearShapes()
    {
        _currentShapes.Clear();
        _spatialIndex = null;
        _viewport.Reset();
        RedrawAll();
    }

    public void Render(IEnumerable<IDrawable> shapes)
    {
        _currentShapes = shapes.ToList();
        RebuildSpatialIndex();
        RedrawAll();
    }

    /// <summary>
    /// Rebuilds the spatial index from the current shapes.
    /// Called for bulk operations; individual add/remove use incremental updates.
    /// </summary>
    private void RebuildSpatialIndex()
    {
        _spatialIndex = QuadTree.FromShapes(_currentShapes);
    }

    /// <summary>
    /// Ensures the spatial index exists and expands bounds if necessary.
    /// Returns the bounds for the given shape.
    /// </summary>
    private AABB EnsureSpatialIndexForShape(IDrawable shape)
    {
        AABB shapeBounds = default;
        if (shape is Shape s)
            shapeBounds = AABB.FromShape(s);

        if (_spatialIndex == null)
        {
            // Create a new spatial index with generous initial bounds
            var padding = Math.Max(100, Math.Max(shapeBounds.Width, shapeBounds.Height) * 2);
            var initialBounds = new AABB(
                shapeBounds.MinX - padding,
                shapeBounds.MinY - padding,
                shapeBounds.MaxX + padding,
                shapeBounds.MaxY + padding
            );
            _spatialIndex = new QuadTree(initialBounds);
        }
        else if (!_spatialIndex.Bounds.Contains(shapeBounds))
        {
            // Shape is outside current bounds - rebuild with expanded bounds
            RebuildSpatialIndex();
        }

        return shapeBounds;
    }

    /// <summary>
    /// Adds a shape to the current canvas display without requiring code execution.
    /// Uses incremental spatial index update instead of full rebuild.
    /// </summary>
    public void AddShape(IDrawable shape)
    {
        _currentShapes.Add(shape);

        // Incremental insert into spatial index
        var bounds = EnsureSpatialIndexForShape(shape);
        _spatialIndex?.Insert(shape, bounds);

        RedrawAll();
    }

    /// <summary>
    /// Removes a shape from the canvas.
    /// Uses incremental spatial index update.
    /// </summary>
    public void RemoveShape(IDrawable shape)
    {
        _currentShapes.Remove(shape);
        _spatialIndex?.Remove(shape);
        RedrawAll();
    }

    /// <summary>
    /// Updates a shape's position in the spatial index.
    /// Call this after moving or resizing a shape.
    /// </summary>
    public void UpdateShapePosition(IDrawable shape)
    {
        if (shape is Shape s && _spatialIndex != null)
        {
            var newBounds = AABB.FromShape(s);
            if (!_spatialIndex.Bounds.Contains(newBounds))
            {
                // Shape moved outside bounds - rebuild
                RebuildSpatialIndex();
            }
            else
            {
                _spatialIndex.Update(shape, newBounds);
            }
        }
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

    private static Pen GetCachedPen(string colorName, double thickness, LineType style = LineType.Continuous, double scale = 1.0)
    {
        // Round scale to avoid too many cache entries
        var roundedScale = Math.Round(scale, 2);
        var key = (colorName, thickness, style, roundedScale);
        if (_scaledPenCache.TryGetValue(key, out var cached))
            return cached;

        var brush = GetCachedBrush(colorName);
        var pen = new Pen(brush, thickness);

        // Apply dash pattern based on stroke style
        if (style != LineType.Continuous)
        {
            pen.DashStyle = GetDashStyle(style, roundedScale);
            pen.DashCap = PenLineCap.Round;
        }

        pen.Freeze();
        _scaledPenCache[key] = pen;
        return pen;
    }

    private static DashStyle GetDashStyle(LineType style, double scale = 1.0)
    {
        double[] pattern = style switch
        {
            LineType.Dashed => new double[] { 4, 2 },
            LineType.Dotted => new double[] { 1, 2 },
            LineType.DashDot => new double[] { 4, 2, 1, 2 },
            LineType.DashDotDot => new double[] { 4, 2, 1, 2, 1, 2 },
            LineType.Center => new double[] { 6, 2, 2, 2 },
            LineType.Phantom => new double[] { 6, 2, 2, 2, 2, 2 },
            LineType.Hidden => new double[] { 2, 2 },
            _ => Array.Empty<double>()
        };

        if (pattern.Length == 0)
            return DashStyles.Solid;

        // Apply scale to pattern
        if (Math.Abs(scale - 1.0) > 0.001)
        {
            for (int i = 0; i < pattern.Length; i++)
                pattern[i] *= scale;
        }

        return new DashStyle(pattern, 0);
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
        var visibleBounds = _viewport.GetVisibleWorldBounds();

        // Add padding to account for stroke thickness (approx 20px in world units)
        var padding = 20.0 / Math.Max(_viewport.Scale, ViewportTransform.MinZoom);
        var minX = visibleBounds.Left - padding;
        var maxX = visibleBounds.Right + padding;
        var minY = visibleBounds.Top - padding;
        var maxY = visibleBounds.Bottom + padding;

        // Query spatial index for visible shapes (O(log n + k) instead of O(n))
        var viewport = new AABB(minX, minY, maxX, maxY);
        HashSet<IDrawable>? visibleSet = null;

        if (_spatialIndex != null)
        {
            visibleSet = new HashSet<IDrawable>();
            _spatialIndex.Query(viewport, visibleSet);
        }

        // Draw all shapes using DrawingContext with Viewport Culling via QuadTree
        foreach (var shape in _currentShapes)
        {
            // Skip if spatial index exists and shape not in visible set
            if (visibleSet != null && !visibleSet.Contains(shape))
                continue;

            // Skip hidden shapes
            if (shape is Shape s && !s.IsVisible)
                continue;

            switch (shape)
            {
                case VPoint point:
                    DrawPoint(dc, point);
                    break;

                case VLine line:
                    DrawLine(dc, line);
                    break;

                case VXLine xline:
                    DrawXLine(dc, xline);
                    break;

                case VRay ray:
                    DrawRay(dc, ray);
                    break;

                case VArc arc:
                    DrawArc(dc, arc);
                    break;

                case VCircle circle:
                    DrawCircle(dc, circle);
                    break;

                case VRectangle rect:
                    DrawRectangle(dc, rect);
                    break;

                case VEllipse ellipse:
                    DrawEllipse(dc, ellipse);
                    break;

                case VPolygon polygon:
                    DrawPolygon(dc, polygon);
                    break;

                case VPolyline polyline:
                    DrawPolyline(dc, polyline);
                    break;

                case VText text:
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
                var circleRadius = circle.Radius * _viewport.Scale;
                dc.DrawEllipse(null, previewPen, circleCenter, circleRadius, circleRadius);
                break;

            case VRectangle rect:
                var rectTopLeft = WorldToScreen(rect.Corner.X, rect.Corner.Y + rect.Height);
                var rectWidth = rect.Width * _viewport.Scale;
                var rectHeight = rect.Height * _viewport.Scale;
                dc.DrawRectangle(null, previewPen, new Rect(rectTopLeft.X, rectTopLeft.Y, rectWidth, rectHeight));
                break;

            case VEllipse ellipse:
                var ellipseCenter = WorldToScreen(ellipse.Center.X, ellipse.Center.Y);
                var radiusX = ellipse.RadiusX * _viewport.Scale;
                var radiusY = ellipse.RadiusY * _viewport.Scale;
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
                    text.Height * _viewport.Scale,
                    previewBrush,
                    1.0);
                dc.DrawText(formattedText, new Point(textPos.X, textPos.Y - text.Height * _viewport.Scale));
                break;
        }
    }

    private void DrawArcPreview(DrawingContext dc, VArc arc, Pen pen)
    {
        var center = WorldToScreen(arc.Center.X, arc.Center.Y);
        var radius = arc.Radius * _viewport.Scale;

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

        // Draw snap indicator when dragging control points
        if (_selectionTool.IsDraggingHandle && _selectionTool.CurrentSnap != null)
        {
            DrawSnapIndicator(dc, _selectionTool.CurrentSnap);
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
            SnapType.Extension => SnapMarkerBrushes.ExtensionBrush,
            SnapType.Tangent => SnapMarkerBrushes.TangentBrush,
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
                // Draw dotted line from reference point to perpendicular point
                if (snap.ReferenceSource != null)
                {
                    var refScreen = WorldToScreen(snap.ReferenceSource.X, snap.ReferenceSource.Y);
                    dc.DrawLine(SnapMarkerBrushes.PerpendicularLinePen, refScreen, screenPos);

                    // Draw small circle at reference point
                    dc.DrawEllipse(null, markerPen, refScreen, markerSize / 3, markerSize / 3);
                }

                // Right angle marker at snap point
                var rightAngle = new StreamGeometry();
                using (var ctx = rightAngle.Open())
                {
                    ctx.BeginFigure(new Point(screenPos.X - markerSize, screenPos.Y), false, false);
                    ctx.LineTo(new Point(screenPos.X, screenPos.Y), true, false);
                    ctx.LineTo(new Point(screenPos.X, screenPos.Y - markerSize), true, false);
                }
                rightAngle.Freeze();
                dc.DrawGeometry(null, markerPen, rightAngle);

                // Draw perpendicular label
                if (snap.ReferenceSource != null)
                {
                    var distance = snap.ReferenceSource.DistanceTo(snap.Point);
                    DrawSnapLabel(dc, screenPos, $"Perp: {distance:F2}", SnapMarkerBrushes.PerpendicularBrush);
                }
                break;

            case SnapType.Tangent:
                // Draw dotted line from reference point to tangent point
                if (snap.ReferenceSource != null)
                {
                    var refScreen = WorldToScreen(snap.ReferenceSource.X, snap.ReferenceSource.Y);
                    dc.DrawLine(SnapMarkerBrushes.TangentLinePen, refScreen, screenPos);

                    // Draw small circle at reference point
                    var tangentPen = new Pen(SnapMarkerBrushes.TangentBrush, 2);
                    tangentPen.Freeze();
                    dc.DrawEllipse(null, tangentPen, refScreen, markerSize / 3, markerSize / 3);
                }

                // Circle marker at tangent point
                dc.DrawEllipse(null, markerPen, screenPos, markerSize / 2, markerSize / 2);

                // Draw tangent label
                if (snap.ReferenceSource != null)
                {
                    var distance = snap.ReferenceSource.DistanceTo(snap.Point);
                    DrawSnapLabel(dc, screenPos, $"Tan: {distance:F2}", SnapMarkerBrushes.TangentBrush);
                }
                break;

            case SnapType.Extension:
                // Draw dotted extension line from source to snap point
                if (snap.ExtensionSource != null)
                {
                    var sourceScreen = WorldToScreen(snap.ExtensionSource.X, snap.ExtensionSource.Y);
                    dc.DrawLine(SnapMarkerBrushes.ExtensionLinePen, sourceScreen, screenPos);

                    // Draw small square at source endpoint
                    dc.DrawRectangle(null, markerPen, new Rect(
                        sourceScreen.X - markerSize / 3, sourceScreen.Y - markerSize / 3,
                        markerSize * 2 / 3, markerSize * 2 / 3));
                }

                // Draw X marker at snap point
                dc.DrawLine(markerPen,
                    new Point(screenPos.X - markerSize / 2, screenPos.Y - markerSize / 2),
                    new Point(screenPos.X + markerSize / 2, screenPos.Y + markerSize / 2));
                dc.DrawLine(markerPen,
                    new Point(screenPos.X + markerSize / 2, screenPos.Y - markerSize / 2),
                    new Point(screenPos.X - markerSize / 2, screenPos.Y + markerSize / 2));

                // Draw extension label with distance and angle
                if (snap.ExtensionSource != null)
                {
                    var effectivePoint = _drawingTool.GetEffectiveEndPoint() ?? snap.Point;
                    var basePoint = _drawingTool.OverrideDistance.HasValue || _drawingTool.OverrideAngle.HasValue
                        ? snap.ExtensionSource
                        : snap.ExtensionSource;

                    var distance = _drawingTool.OverrideDistance ?? snap.ExtensionSource.DistanceTo(effectivePoint);
                    var angle = _drawingTool.OverrideAngle ?? snap.ExtensionAngle;

                    // Format label with highlighting for active input mode
                    string labelText;
                    if (_drawingTool.InputMode == DrawingInputMode.Distance)
                    {
                        labelText = $"Extension: [{_drawingTool.InputBuffer}_] < {angle:F0}°";
                    }
                    else if (_drawingTool.InputMode == DrawingInputMode.Angle)
                    {
                        labelText = $"Extension: {distance:F2} < [{_drawingTool.InputBuffer}_]°";
                    }
                    else
                    {
                        labelText = $"Extension: {distance:F2} < {angle:F0}°";
                    }
                    DrawExtensionLabel(dc, screenPos, labelText);
                }
                break;
        }
    }

    private void DrawExtensionLabel(DrawingContext dc, Point screenPos, string text)
    {
        var typeface = new Typeface("Segoe UI");
        var formattedText = new FormattedText(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeface,
            12,
            SnapMarkerBrushes.ExtensionBrush,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);

        // Position label below and to the right of the snap point
        var labelPos = new Point(screenPos.X + 10, screenPos.Y + 5);

        // Draw background
        var padding = 3.0;
        var bgRect = new Rect(
            labelPos.X - padding,
            labelPos.Y - padding,
            formattedText.Width + padding * 2,
            formattedText.Height + padding * 2);

        var bgBrush = new SolidColorBrush(Color.FromArgb(220, 30, 30, 30));
        bgBrush.Freeze();
        dc.DrawRectangle(bgBrush, null, bgRect);

        // Draw text
        dc.DrawText(formattedText, labelPos);
    }

    private void DrawSnapLabel(DrawingContext dc, Point screenPos, string text, Brush brush)
    {
        var typeface = new Typeface("Segoe UI");
        var formattedText = new FormattedText(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeface,
            12,
            brush,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);

        // Position label below and to the right of the snap point
        var labelPos = new Point(screenPos.X + 10, screenPos.Y + 5);

        // Draw background
        var padding = 3.0;
        var bgRect = new Rect(
            labelPos.X - padding,
            labelPos.Y - padding,
            formattedText.Width + padding * 2,
            formattedText.Height + padding * 2);

        var bgBrush = new SolidColorBrush(Color.FromArgb(220, 30, 30, 30));
        bgBrush.Freeze();
        dc.DrawRectangle(bgBrush, null, bgRect);

        // Draw text
        dc.DrawText(formattedText, labelPos);
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

    private VPoint SnapPointToGrid(double worldX, double worldY)
    {
        var spacing = CalculateAdaptiveSpacing();
        var snappedX = Math.Round(worldX / spacing) * spacing;
        var snappedY = Math.Round(worldY / spacing) * spacing;
        return new VPoint(snappedX, snappedY);
    }

    private double CalculateAdaptiveSpacing()
    {
        // Target visual spacing in pixels (approx 50px)
        const double targetPixelSpacing = 50.0;

        // Calculate the theoretical world spacing to achieve target pixel spacing
        // world = pixels / scale
        double rawSpacing = targetPixelSpacing / _viewport.Scale;

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

        // Apply offset for move animation
        var screenPos = WorldToScreen(point.X + point.OffsetX, point.Y + point.OffsetY);
        var fill = GetCachedBrush(point.FillColor);
        var pen = GetCachedPen(point.Color, point.LineWeight, point.LineType, point.LineTypeScale);

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
        var pen = GetCachedPen(line.Color, line.LineWeight, line.LineType, line.LineTypeScale);

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

    private void DrawXLine(DrawingContext dc, VXLine xline)
    {
        if (xline.DrawFactor <= 0 || xline.Opacity <= 0) return;

        var applyOpacity = xline.Opacity < 1.0;
        if (applyOpacity) dc.PushOpacity(xline.Opacity);

        // Apply offset for move animation
        var offsetX = xline.OffsetX;
        var offsetY = xline.OffsetY;

        // Get the visible canvas bounds in world coordinates
        var (minWorld, maxWorld) = GetVisibleWorldBounds();

        // Calculate intersection of the infinite line with a large bounding box
        // Use the larger of the render extent or canvas bounds
        double extent = Math.Max(xline.RenderExtent, Math.Max(maxWorld.X - minWorld.X, maxWorld.Y - minWorld.Y) * 2);

        var p1 = xline.GetPointAtParameter(-extent);
        var p2 = xline.GetPointAtParameter(extent);

        var start = WorldToScreen(p1.X + offsetX, p1.Y + offsetY);
        var end = WorldToScreen(p2.X + offsetX, p2.Y + offsetY);
        var pen = GetCachedPen(xline.Color, xline.LineWeight, xline.LineType, xline.LineTypeScale);

        // Apply DrawFactor for animation
        if (xline.DrawFactor < 1.0)
        {
            var dx = end.X - start.X;
            var dy = end.Y - start.Y;
            var midX = (start.X + end.X) / 2;
            var midY = (start.Y + end.Y) / 2;
            start = new Point(midX - dx * xline.DrawFactor / 2, midY - dy * xline.DrawFactor / 2);
            end = new Point(midX + dx * xline.DrawFactor / 2, midY + dy * xline.DrawFactor / 2);
        }

        dc.DrawLine(pen, start, end);

        if (applyOpacity) dc.Pop();
    }

    private void DrawRay(DrawingContext dc, VRay ray)
    {
        if (ray.DrawFactor <= 0 || ray.Opacity <= 0) return;

        var applyOpacity = ray.Opacity < 1.0;
        if (applyOpacity) dc.PushOpacity(ray.Opacity);

        // Apply offset for move animation
        var offsetX = ray.OffsetX;
        var offsetY = ray.OffsetY;

        // Get the visible canvas bounds in world coordinates
        var (minWorld, maxWorld) = GetVisibleWorldBounds();

        // Calculate extent based on render extent or canvas size
        double extent = Math.Max(ray.RenderExtent, Math.Max(maxWorld.X - minWorld.X, maxWorld.Y - minWorld.Y) * 2);

        var p1 = ray.Origin;
        var p2 = ray.GetPointAtDistance(extent);

        var start = WorldToScreen(p1.X + offsetX, p1.Y + offsetY);
        var end = WorldToScreen(p2.X + offsetX, p2.Y + offsetY);
        var pen = GetCachedPen(ray.Color, ray.LineWeight, ray.LineType, ray.LineTypeScale);

        // Apply DrawFactor for animation (draws from origin outward)
        if (ray.DrawFactor < 1.0)
        {
            var dx = end.X - start.X;
            var dy = end.Y - start.Y;
            end = new Point(start.X + dx * ray.DrawFactor, start.Y + dy * ray.DrawFactor);
        }

        dc.DrawLine(pen, start, end);

        if (applyOpacity) dc.Pop();
    }

    private (VPoint min, VPoint max) GetVisibleWorldBounds()
    {
        var minScreen = new Point(0, 0);
        var maxScreen = new Point(ActualWidth, ActualHeight);
        var minWorld = ScreenToWorld(minScreen.X, minScreen.Y);
        var maxWorld = ScreenToWorld(maxScreen.X, maxScreen.Y);
        // Swap Y values since screen Y is inverted
        return (new VPoint(Math.Min(minWorld.X, maxWorld.X), Math.Min(minWorld.Y, maxWorld.Y)),
                new VPoint(Math.Max(minWorld.X, maxWorld.X), Math.Max(minWorld.Y, maxWorld.Y)));
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

        var screenRadius = arc.Radius * _viewport.Scale;
        var pen = GetCachedPen(arc.Color, arc.LineWeight, arc.LineType, arc.LineTypeScale);

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
        var screenRadius = circle.Radius * _viewport.Scale;
        var fill = GetCachedBrush(circle.FillColor);
        var pen = GetCachedPen(circle.Color, circle.LineWeight, circle.LineType, circle.LineTypeScale);

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

        // Apply offset for move animation
        var offsetX = rect.OffsetX;
        var offsetY = rect.OffsetY;

        var fill = GetCachedBrush(rect.FillColor);
        var pen = GetCachedPen(rect.Color, rect.LineWeight, rect.LineType, rect.LineTypeScale);

        // If rectangle has internal rotation, draw as polygon
        if (Math.Abs(rect.RotationAngle) > 1e-9)
        {
            var vertices = rect.Vertices;
            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                var first = WorldToScreen(vertices[0].X + offsetX, vertices[0].Y + offsetY);
                ctx.BeginFigure(first, fill != null, true);
                for (int i = 1; i < vertices.Count; i++)
                {
                    var pt = WorldToScreen(vertices[i].X + offsetX, vertices[i].Y + offsetY);
                    ctx.LineTo(pt, true, false);
                }
            }
            geometry.Freeze();
            dc.DrawGeometry(fill, pen, geometry);

            if (applyOpacity) dc.Pop();
            return;
        }

        // Apply external rotation transform if needed (for animations)
        var applyRotation = rect.RotationPivot != null;
        if (applyRotation)
        {
            var pivot = WorldToScreen(rect.RotationPivot!.X + offsetX, rect.RotationPivot!.Y + offsetY);
            dc.PushTransform(new RotateTransform(-rect.RotationAngle, pivot.X, pivot.Y));
        }

        var actualWidth = rect.Width;
        var actualHeight = rect.Height;
        var cornerX = rect.Corner.X + offsetX + (actualWidth < 0 ? actualWidth : 0);
        var cornerY = rect.Corner.Y + offsetY + (actualHeight > 0 ? actualHeight : 0);
        var corner = WorldToScreen(cornerX, cornerY);
        var screenWidth = Math.Abs(actualWidth) * _viewport.Scale;
        var screenHeight = Math.Abs(actualHeight) * _viewport.Scale;

        // Apply DrawFactor - draw partial rectangle outline
        if (rect.DrawFactor < 1.0)
        {
            var absWidth = Math.Abs(rect.Width);
            var absHeight = Math.Abs(rect.Height);
            var perimeter = 2 * (absWidth + absHeight);
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
                    remaining -= absWidth;
                }
                // Bottom edge
                if (remaining > 0)
                {
                    var len = Math.Min(remaining, screenHeight);
                    ctx.LineTo(new Point(corner.X + screenWidth, corner.Y + len), true, false);
                    remaining -= absHeight;
                }
                // Left edge
                if (remaining > 0)
                {
                    var len = Math.Min(remaining, screenWidth);
                    ctx.LineTo(new Point(corner.X + screenWidth - len, corner.Y + screenHeight), true, false);
                    remaining -= absWidth;
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
        var screenRadiusX = ellipse.RadiusX * _viewport.Scale;
        var screenRadiusY = ellipse.RadiusY * _viewport.Scale;
        var fill = GetCachedBrush(ellipse.FillColor);
        var pen = GetCachedPen(ellipse.Color, ellipse.LineWeight, ellipse.LineType, ellipse.LineTypeScale);

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
        var pen = GetCachedPen(polygon.Color, polygon.LineWeight, polygon.LineType, polygon.LineTypeScale);

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

        var pen = GetCachedPen(polyline.Color, polyline.LineWeight, polyline.LineType, polyline.LineTypeScale);

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
        var brush = GetCachedBrush(text.Color);

        // Scale font size with zoom, but keep it readable
        var fontSize = text.Height * _viewport.Scale;
        fontSize = Math.Max(fontSize, 6); // Minimum readable size

        var fontFamily = GetFontFamilyName(text.Font);
        var fontWeight = text.FontWeight == VFontWeight.Bold ? FontWeights.Bold : FontWeights.Normal;
        var typeface = new Typeface(new FontFamily(fontFamily), FontStyles.Normal, fontWeight, FontStretches.Normal);
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

    private static string GetFontFamilyName(VFont font) => font switch
    {
        VFont.Arial => "Arial",
        VFont.TimesNewRoman => "Times New Roman",
        VFont.CourierNew => "Courier New",
        VFont.Verdana => "Verdana",
        VFont.Georgia => "Georgia",
        VFont.Tahoma => "Tahoma",
        VFont.TrebuchetMS => "Trebuchet MS",
        VFont.Consolas => "Consolas",
        VFont.Calibri => "Calibri",
        VFont.Cambria => "Cambria",
        VFont.SegoeUI => "Segoe UI",
        VFont.ComicSansMS => "Comic Sans MS",
        VFont.Impact => "Impact",
        VFont.LucidaConsole => "Lucida Console",
        _ => "Arial"
    };

    private void DrawBezier(DrawingContext dc, VBezier bezier)
    {
        if (bezier.DrawFactor <= 0 || bezier.Opacity <= 0) return;

        var applyOpacity = bezier.Opacity < 1.0;
        if (applyOpacity) dc.PushOpacity(bezier.Opacity);

        // Apply offset for move animation
        var offsetX = bezier.OffsetX;
        var offsetY = bezier.OffsetY;

        var pen = GetCachedPen(bezier.Color, bezier.LineWeight, bezier.LineType, bezier.LineTypeScale);
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

        var pen = GetCachedPen(spline.Color, spline.LineWeight, spline.LineType, spline.LineTypeScale);
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

        var pen = GetCachedPen(arrow.Color, arrow.LineWeight, arrow.LineType, arrow.LineTypeScale);
        var brush = GetCachedBrush(arrow.Color);  // Use stroke color for filled arrowhead
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

        var pen = GetCachedPen(dim.Color, dim.LineWeight, dim.LineType, dim.LineTypeScale);
        var (dimStart, dimEnd, textPos, ext1Start, ext1End, ext2Start, ext2End) = dim.GetDimensionGeometry();

        // Draw dimension line
        var ds = WorldToScreen(dimStart.X, dimStart.Y);
        var de = WorldToScreen(dimEnd.X, dimEnd.Y);
        dc.DrawLine(pen, ds, de);

        // Draw extension lines
        dc.DrawLine(pen, WorldToScreen(ext1Start.X, ext1Start.Y), WorldToScreen(ext1End.X, ext1End.Y));
        dc.DrawLine(pen, WorldToScreen(ext2Start.X, ext2Start.Y), WorldToScreen(ext2End.X, ext2End.Y));

        // Draw text
        var brush = GetCachedBrush(dim.Color);
        var fontSize = dim.TextHeight * _viewport.Scale;
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
        if (group.DrawFactor <= 0 || group.Opacity <= 0) return;

        var applyOpacity = group.Opacity < 1.0;
        if (applyOpacity) dc.PushOpacity(group.Opacity);

        // Draw each shape in the group
        foreach (var shape in group.Shapes)
        {
            DrawShape(dc, shape);
        }

        if (applyOpacity) dc.Pop();
    }

    /// <summary>
    /// Draws a single shape using the appropriate method based on its type.
    /// Used for rendering child shapes within groups.
    /// </summary>
    private void DrawShape(DrawingContext dc, Shape shape)
    {
        switch (shape)
        {
            case VPoint point:
                DrawPoint(dc, point);
                break;
            case VLine line:
                DrawLine(dc, line);
                break;
            case VArc arc:
                DrawArc(dc, arc);
                break;
            case VCircle circle:
                DrawCircle(dc, circle);
                break;
            case VRectangle rect:
                DrawRectangle(dc, rect);
                break;
            case VEllipse ellipse:
                DrawEllipse(dc, ellipse);
                break;
            case VPolygon polygon:
                DrawPolygon(dc, polygon);
                break;
            case VPolyline polyline:
                DrawPolyline(dc, polyline);
                break;
            case VText text:
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
            case VGroup nestedGroup:
                DrawGroup(dc, nestedGroup);
                break;
        }
    }

    public void ZoomExtents(IEnumerable<IDrawable> shapes)
    {
        var shapeList = shapes.ToList();
        if (!shapeList.Any() || ActualWidth <= 0 || ActualHeight <= 0)
        {
            _viewport.Scale = 1.0;
            _viewport.PanX = 0;
            _viewport.PanY = 0;
            RedrawAll();
            return;
        }

        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;

        foreach (var shape in shapeList)
        {
            // Skip hidden shapes
            if (shape is Shape shp && !shp.IsVisible)
                continue;

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
        _viewport.Scale = Math.Min(scaleX, scaleY);
        _viewport.Scale = Math.Clamp(_viewport.Scale, ViewportTransform.MinZoom, ViewportTransform.MaxZoom);

        _viewport.PanX = -worldCenterX * _viewport.Scale;
        _viewport.PanY = worldCenterY * _viewport.Scale;

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
            _viewport.Scale = 1.0;
            _viewport.PanX = 0;
            _viewport.PanY = 0;
            RedrawAll();
            return;
        }

        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;

        foreach (var shape in shapeList)
        {
            // Skip hidden shapes
            if (shape is Shape shp && !shp.IsVisible)
                continue;

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
        _viewport.Scale = Math.Min(scaleX, scaleY);
        _viewport.Scale = Math.Clamp(_viewport.Scale, ViewportTransform.MinZoom, ViewportTransform.MaxZoom);

        _viewport.PanX = -worldCenterX * _viewport.Scale;
        _viewport.PanY = worldCenterY * _viewport.Scale;

        RedrawAll();
    }
}
