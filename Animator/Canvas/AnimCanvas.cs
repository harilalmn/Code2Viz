using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Animator.Canvas;

/// <summary>
/// Lightweight 2D canvas for Animator. Renders <see cref="C2VGeometry.Shape"/> instances directly
/// using a single <see cref="DrawingVisual"/>. Coordinate system is mathematical (Y-up),
/// origin at the centre of the visual. Supports mouse-wheel zoom and middle-button pan.
/// </summary>
public class AnimCanvas : FrameworkElement
{
    private readonly DrawingVisual _visual = new();
    private List<C2VGeometry.Shape> _shapes = new();
    private Brush _backgroundBrush;
    private double _scale = 1.0;
    private double _panX, _panY;
    private Point _lastMouse;
    private bool _panning;
    private readonly Pen _gridPen;
    private readonly Pen _axisPen;
    private readonly Pen _boundaryPen;
    private readonly Dictionary<string, Brush> _brushCache = new(StringComparer.OrdinalIgnoreCase);
    private double _boundaryWidth, _boundaryHeight;
    private bool _hasBoundary;

    public AnimCanvas()
    {
        _backgroundBrush = new SolidColorBrush(Color.FromRgb(30, 30, 30));
        _backgroundBrush.Freeze();
        _gridPen = new Pen(new SolidColorBrush(Color.FromRgb(50, 50, 50)), 1) { DashStyle = DashStyles.Solid };
        _gridPen.Freeze();
        _axisPen = new Pen(new SolidColorBrush(Color.FromRgb(80, 80, 80)), 1);
        _axisPen.Freeze();
        _boundaryPen = new Pen(new SolidColorBrush(Color.FromRgb(136, 136, 136)), 1.5);
        _boundaryPen.Freeze();

        AddVisualChild(_visual);
        AddLogicalChild(_visual);
        ClipToBounds = true;
        Focusable = true;
        FocusVisualStyle = null;

        MouseDown += OnMouseDown;
        MouseMove += OnMouseMove;
        MouseUp += OnMouseUp;
        SizeChanged += (s, e) =>
        {
            if (_hasBoundary) ZoomToBounds(-_boundaryWidth / 2, -_boundaryHeight / 2, _boundaryWidth / 2, _boundaryHeight / 2);
            else Refresh();
        };
    }

    /// <summary>
    /// Declares the sketch's logical drawing area. The canvas zooms to fit a centered
    /// width×height rectangle and renders a faint outline around it.
    /// </summary>
    public void SetBoundary(double width, double height)
    {
        _boundaryWidth = width;
        _boundaryHeight = height;
        _hasBoundary = true;
        ZoomToBounds(-width / 2, -height / 2, width / 2, height / 2);
    }

    public void ClearBoundary()
    {
        _hasBoundary = false;
        Refresh();
    }

    protected override int VisualChildrenCount => 1;
    protected override Visual GetVisualChild(int index) => _visual;

    public Brush CanvasBackground
    {
        get => _backgroundBrush;
        set
        {
            _backgroundBrush = value;
            if (_backgroundBrush.CanFreeze) _backgroundBrush.Freeze();
            Refresh();
        }
    }

    /// <summary>World coordinate at the current mouse position.</summary>
    public (double X, double Y) WorldMouse
    {
        get
        {
            var p = Mouse.GetPosition(this);
            return ScreenToWorld(p.X, p.Y);
        }
    }

    /// <summary>Replaces the shape set and triggers a redraw.</summary>
    public void SetShapes(IReadOnlyList<C2VGeometry.Shape> shapes)
    {
        // Avoid per-frame List allocation — the caller's snapshot is immutable
        // for our purposes; we cast it to a List once or just iterate over it.
        _shapes = shapes as List<C2VGeometry.Shape> ?? new List<C2VGeometry.Shape>(shapes);
        Refresh();
    }

    public void Clear() => SetShapes(Array.Empty<C2VGeometry.Shape>());

    /// <summary>Zoom and centre so the given world box fits with a small margin.</summary>
    public void ZoomToBounds(double minX, double minY, double maxX, double maxY)
    {
        if (ActualWidth <= 0 || ActualHeight <= 0) return;
        var w = Math.Max(maxX - minX, 1e-6);
        var h = Math.Max(maxY - minY, 1e-6);
        var sx = ActualWidth / w;
        var sy = ActualHeight / h;
        _scale = Math.Min(sx, sy) * 0.9;
        _panX = -(minX + maxX) * 0.5 * _scale;
        _panY = (minY + maxY) * 0.5 * _scale;
        Refresh();
    }

    public void Refresh()
    {
        if (ActualWidth <= 0 || ActualHeight <= 0) return;
        using var dc = _visual.RenderOpen();

        dc.DrawRectangle(_backgroundBrush, null, new Rect(0, 0, ActualWidth, ActualHeight));
        // Grid intentionally disabled — see request from user.
        DrawAxes(dc);

        if (_hasBoundary)
        {
            var tl = WorldPt(-_boundaryWidth / 2,  _boundaryHeight / 2);
            var br = WorldPt( _boundaryWidth / 2, -_boundaryHeight / 2);
            dc.DrawRectangle(null, _boundaryPen,
                new Rect(tl, br));
        }

        foreach (var shape in _shapes)
        {
            if (!shape.IsVisible) continue;
            DrawShape(dc, shape);
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // World ↔ Screen
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>Transforms a world point to screen pixels (Y is inverted: world up = screen up).</summary>
    public (double X, double Y) WorldToScreen(double wx, double wy)
    {
        var sx = wx * _scale + _panX + ActualWidth * 0.5;
        var sy = -wy * _scale + _panY + ActualHeight * 0.5;
        return (sx, sy);
    }

    public (double X, double Y) ScreenToWorld(double sx, double sy)
    {
        var wx = (sx - _panX - ActualWidth * 0.5) / _scale;
        var wy = -(sy - _panY - ActualHeight * 0.5) / _scale;
        return (wx, wy);
    }

    private Point WorldPt(double x, double y)
    {
        var (sx, sy) = WorldToScreen(x, y);
        return new Point(sx, sy);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Background grid and axes
    // ────────────────────────────────────────────────────────────────────────

    private void DrawGrid(DrawingContext dc)
    {
        // Choose a grid spacing that gives ~50-150 screen px per cell.
        var targetPx = 80.0;
        var worldStep = targetPx / _scale;
        var pow10 = Math.Pow(10, Math.Floor(Math.Log10(worldStep)));
        var nice = pow10;
        if (worldStep / pow10 > 5) nice = pow10 * 5;
        else if (worldStep / pow10 > 2) nice = pow10 * 2;

        var (wMinX, wMaxY) = ScreenToWorld(0, 0);
        var (wMaxX, wMinY) = ScreenToWorld(ActualWidth, ActualHeight);

        var x0 = Math.Floor(wMinX / nice) * nice;
        for (var x = x0; x <= wMaxX; x += nice)
        {
            var (sx, _) = WorldToScreen(x, 0);
            dc.DrawLine(_gridPen, new Point(sx, 0), new Point(sx, ActualHeight));
        }
        var y0 = Math.Floor(wMinY / nice) * nice;
        for (var y = y0; y <= wMaxY; y += nice)
        {
            var (_, sy) = WorldToScreen(0, y);
            dc.DrawLine(_gridPen, new Point(0, sy), new Point(ActualWidth, sy));
        }
    }

    private void DrawAxes(DrawingContext dc)
    {
        var (oxScreen, oyScreen) = WorldToScreen(0, 0);
        dc.DrawLine(_axisPen, new Point(0, oyScreen), new Point(ActualWidth, oyScreen));
        dc.DrawLine(_axisPen, new Point(oxScreen, 0), new Point(oxScreen, ActualHeight));
    }

    // ────────────────────────────────────────────────────────────────────────
    // Shape rendering
    // ────────────────────────────────────────────────────────────────────────

    private Brush GetBrush(string color)
    {
        if (string.IsNullOrEmpty(color) || color.Equals("Transparent", StringComparison.OrdinalIgnoreCase))
            return Brushes.Transparent;
        if (_brushCache.TryGetValue(color, out var cached)) return cached;
        try
        {
            var c = (Color)ColorConverter.ConvertFromString(color);
            var b = new SolidColorBrush(c);
            b.Freeze();
            _brushCache[color] = b;
            return b;
        }
        catch
        {
            return Brushes.Magenta;
        }
    }

    private Pen StrokePen(C2VGeometry.Shape s)
    {
        var brush = GetBrush(s.Color);
        var pen = new Pen(brush, s.LineWeight);
        if (s.LineType != C2VGeometry.LineType.Continuous)
            pen.DashStyle = s.LineType switch
            {
                C2VGeometry.LineType.Dashed   => DashStyles.Dash,
                C2VGeometry.LineType.Dotted   => DashStyles.Dot,
                C2VGeometry.LineType.DashDot  => DashStyles.DashDot,
                C2VGeometry.LineType.DashDotDot => DashStyles.DashDotDot,
                _ => DashStyles.Solid
            };
        pen.Freeze();
        return pen;
    }

    private void DrawShape(DrawingContext dc, C2VGeometry.Shape s)
    {
        var opacity = s.Opacity;
        bool pushedOpacity = opacity < 0.999;
        if (pushedOpacity) dc.PushOpacity(opacity);

        // Offset translation (from animation OffsetX/Y)
        bool pushedOffset = s.OffsetX != 0 || s.OffsetY != 0;
        if (pushedOffset)
            dc.PushTransform(new TranslateTransform(s.OffsetX * _scale, -s.OffsetY * _scale));

        switch (s)
        {
            case C2VGeometry.VCircle c:    DrawCircle(dc, c); break;
            case C2VGeometry.VEllipse e:   DrawEllipse(dc, e); break;
            case C2VGeometry.VArc a:       DrawArc(dc, a); break;
            case C2VGeometry.VRectangle r: DrawRectangle(dc, r); break;
            case C2VGeometry.VPolygon pg:  DrawPolygon(dc, pg); break;
            case C2VGeometry.VPolyline pl: DrawPolyline(dc, pl); break;
            case C2VGeometry.VLine l:      DrawLine(dc, l); break;
            case C2VGeometry.VPoint p:     DrawPoint(dc, p); break;
            default: /* unsupported shape type — skip */ break;
        }

        if (pushedOffset) dc.Pop();
        if (pushedOpacity) dc.Pop();
    }

    private void DrawCircle(DrawingContext dc, C2VGeometry.VCircle c)
    {
        var centre = WorldPt(c.Center.X, c.Center.Y);
        var r = c.Radius * _scale;
        dc.DrawEllipse(GetBrush(c.FillColor), StrokePen(c), centre, r, r);
    }

    private void DrawEllipse(DrawingContext dc, C2VGeometry.VEllipse e)
    {
        var centre = WorldPt(e.Center.X, e.Center.Y);
        dc.DrawEllipse(GetBrush(e.FillColor), StrokePen(e), centre, e.RadiusX * _scale, e.RadiusY * _scale);
    }

    private void DrawArc(DrawingContext dc, C2VGeometry.VArc a)
    {
        var pen = StrokePen(a);
        var startRad = a.StartAngle * Math.PI / 180.0;
        var endRad = a.EndAngle * Math.PI / 180.0;
        var p0 = WorldPt(a.Center.X + a.Radius * Math.Cos(startRad), a.Center.Y + a.Radius * Math.Sin(startRad));
        var p1 = WorldPt(a.Center.X + a.Radius * Math.Cos(endRad),   a.Center.Y + a.Radius * Math.Sin(endRad));
        var sweep = Math.Abs(a.EndAngle - a.StartAngle);
        var isLargeArc = sweep > 180;
        // Mathematical Y-up: positive sweep is CCW in world but, after Y flip, CW on screen.
        var sweepDir = (a.EndAngle > a.StartAngle) ? SweepDirection.Counterclockwise : SweepDirection.Clockwise;

        var fig = new PathFigure { StartPoint = p0 };
        fig.Segments.Add(new ArcSegment(p1, new Size(a.Radius * _scale, a.Radius * _scale), 0, isLargeArc, sweepDir, true));
        fig.IsClosed = false;
        var geom = new PathGeometry();
        geom.Figures.Add(fig);
        dc.DrawGeometry(null, pen, geom);
    }

    private void DrawRectangle(DrawingContext dc, C2VGeometry.VRectangle r)
    {
        // Render via its underlying polygon points so rotation is respected.
        DrawPolygon(dc, r);
    }

    private void DrawPolygon(DrawingContext dc, C2VGeometry.VPolygon poly)
    {
        if (poly.Points.Count < 2) return;
        var fig = new PathFigure { StartPoint = WorldPt(poly.Points[0].X, poly.Points[0].Y) };
        for (int i = 1; i < poly.Points.Count; i++)
            fig.Segments.Add(new LineSegment(WorldPt(poly.Points[i].X, poly.Points[i].Y), true));
        fig.IsClosed = true;
        var geom = new PathGeometry();
        geom.Figures.Add(fig);
        dc.DrawGeometry(GetBrush(poly.FillColor), StrokePen(poly), geom);
    }

    private void DrawPolyline(DrawingContext dc, C2VGeometry.VPolyline poly)
    {
        if (poly.Points.Count < 2) return;
        var pen = StrokePen(poly);
        for (int i = 0; i < poly.Points.Count - 1; i++)
            dc.DrawLine(pen, WorldPt(poly.Points[i].X, poly.Points[i].Y),
                             WorldPt(poly.Points[i + 1].X, poly.Points[i + 1].Y));
    }

    private void DrawLine(DrawingContext dc, C2VGeometry.VLine l)
        => dc.DrawLine(StrokePen(l), WorldPt(l.Start.X, l.Start.Y), WorldPt(l.End.X, l.End.Y));

    private void DrawPoint(DrawingContext dc, C2VGeometry.VPoint p)
    {
        var pt = WorldPt(p.X, p.Y);
        dc.DrawEllipse(GetBrush(p.FillColor), StrokePen(p), pt, 3, 3);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Mouse interaction (pan only — zoom is intentionally disabled)
    // ────────────────────────────────────────────────────────────────────────

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.MiddleButton == MouseButtonState.Pressed)
        {
            _panning = true;
            _lastMouse = e.GetPosition(this);
            CaptureMouse();
            Cursor = Cursors.SizeAll;
        }
        Focus();
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_panning)
        {
            var p = e.GetPosition(this);
            _panX += p.X - _lastMouse.X;
            _panY += p.Y - _lastMouse.Y;
            _lastMouse = p;
            Refresh();
        }
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_panning && e.MiddleButton == MouseButtonState.Released)
        {
            _panning = false;
            ReleaseMouseCapture();
            Cursor = Cursors.Arrow;
        }
    }
}
