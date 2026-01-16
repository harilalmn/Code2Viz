using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Code2Viz.Geometry;
using Point = System.Windows.Point;
// Direct usage of VPoint, VLine etc. No alias needed.

namespace Code2Viz.Canvas;

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

    // Brush cache for performance
    private static readonly Dictionary<string, Brush> _brushCache = new();
    private static readonly Dictionary<(string color, double thickness), Pen> _penCache = new();

    // Pre-frozen brushes for common colors
    // Removed static BackgroundBrush to allow dynamic changes
    private static readonly Brush GridBrush;
    private static readonly Brush AxisBrush;

    private Brush _backgroundBrush;

    static RenderCanvas()
    {
        GridBrush = new SolidColorBrush(Color.FromRgb(50, 50, 50));
        GridBrush.Freeze();

        AxisBrush = new SolidColorBrush(Color.FromRgb(100, 100, 100));
        AxisBrush.Freeze();
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
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.MiddleButton == MouseButtonState.Released && _isPanning)
        {
            _isPanning = false;
            ReleaseMouseCapture();
            Cursor = Cursors.Arrow;
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
            DrawGrid(dc);

        DrawAxes(dc);

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
        var axisPen = new Pen(AxisBrush, 1.5);
        axisPen.Freeze();

        var xAxisY = WorldToScreen(0, 0).Y;
        if (xAxisY >= 0 && xAxisY <= ActualHeight)
        {
            dc.DrawLine(axisPen, new Point(0, xAxisY), new Point(ActualWidth, xAxisY));
        }

        var yAxisX = WorldToScreen(0, 0).X;
        if (yAxisX >= 0 && yAxisX <= ActualWidth)
        {
            dc.DrawLine(axisPen, new Point(yAxisX, 0), new Point(yAxisX, ActualHeight));
        }
    }

    private void DrawPoint(DrawingContext dc, VPoint point)
    {
        if (point.Opacity <= 0) return;

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

        if (applyOpacity) dc.Pop();
    }

    private void DrawEllipse(DrawingContext dc, VEllipse ellipse)
    {
        if (ellipse.Opacity <= 0) return;

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
        if (string.IsNullOrEmpty(text.Content) || text.Opacity <= 0)
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
        if (arrow.Opacity <= 0) return;

        var applyOpacity = arrow.Opacity < 1.0;
        if (applyOpacity) dc.PushOpacity(arrow.Opacity);

        var pen = GetCachedPen(arrow.StrokeColor, arrow.StrokeThickness);
        var brush = GetCachedBrush(arrow.StrokeColor);  // Use stroke color for filled arrowhead
        var start = WorldToScreen(arrow.Start.X, arrow.Start.Y);
        var end = WorldToScreen(arrow.End.X, arrow.End.Y);

        // Draw main line
        dc.DrawLine(pen, start, end);

        // Draw filled end arrowhead
        var (wing1, wing2) = arrow.GetEndArrowhead();
        var w1 = WorldToScreen(wing1.X, wing1.Y);
        var w2 = WorldToScreen(wing2.X, wing2.Y);
        var arrowHead = new StreamGeometry();
        using (var ctx = arrowHead.Open())
        {
            ctx.BeginFigure(end, true, true);  // Start at tip, filled, closed
            ctx.LineTo(w1, true, false);
            ctx.LineTo(w2, true, false);
        }
        arrowHead.Freeze();
        dc.DrawGeometry(brush, pen, arrowHead);

        // Draw start arrowhead if double-ended
        if (arrow.DoubleEnded)
        {
            var (sw1, sw2) = arrow.GetStartArrowhead();
            var sw1Screen = WorldToScreen(sw1.X, sw1.Y);
            var sw2Screen = WorldToScreen(sw2.X, sw2.Y);
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

        if (applyOpacity) dc.Pop();
    }

    private void DrawDimension(DrawingContext dc, VDimension dim)
    {
        if (dim.Opacity <= 0) return;

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
}
