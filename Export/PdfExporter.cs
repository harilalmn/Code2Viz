using System;
using System.Collections.Generic;
using Code2Viz.Canvas;
using Code2Viz.Geometry;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace Code2Viz.Export;

/// <summary>
/// Exports shapes to PDF format using PdfSharp.
/// </summary>
public class PdfExporter
{
    private double _margin = 20;

    /// <summary>
    /// Exports shapes to a PDF file.
    /// </summary>
    public void Export(IReadOnlyList<IDrawable> shapes, string filePath)
    {
        if (shapes.Count == 0) return;

        // Calculate bounds
        var (minPt, maxPt) = GetBounds(shapes);
        var width = maxPt.X - minPt.X + 2 * _margin;
        var height = maxPt.Y - minPt.Y + 2 * _margin;

        // Create PDF document
        var document = new PdfDocument();
        document.Info.Title = "Code2Viz Export";

        // Create a page with appropriate size
        var page = document.AddPage();
        page.Width = XUnit.FromPoint(Math.Max(width, 100));
        page.Height = XUnit.FromPoint(Math.Max(height, 100));

        using var gfx = XGraphics.FromPdfPage(page);

        // Transform: flip Y axis and translate
        gfx.TranslateTransform(_margin - minPt.X, page.Height.Point - _margin + minPt.Y);
        gfx.ScaleTransform(1, -1);

        // Draw shapes
        foreach (var drawable in shapes)
        {
            if (drawable is Shape shape)
            {
                DrawShape(gfx, shape);
            }
        }

        // Save
        document.Save(filePath);
    }

    private (VPoint min, VPoint max) GetBounds(IReadOnlyList<IDrawable> shapes)
    {
        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;

        foreach (var drawable in shapes)
        {
            if (drawable is Shape shape)
            {
                var (min, max) = shape.GetBounds();
                minX = Math.Min(minX, min.X);
                minY = Math.Min(minY, min.Y);
                maxX = Math.Max(maxX, max.X);
                maxY = Math.Max(maxY, max.Y);
            }
        }

        if (minX == double.MaxValue)
        {
            return (new VPoint(0, 0), new VPoint(100, 100));
        }

        return (new VPoint(minX, minY), new VPoint(maxX, maxY));
    }

    private void DrawShape(XGraphics gfx, Shape shape)
    {
        var pen = CreatePen(shape);
        var brush = CreateBrush(shape);

        switch (shape)
        {
            case VPoint point:
                DrawPoint(gfx, point, pen);
                break;
            case VLine line:
                DrawLine(gfx, line, pen);
                break;
            case VCircle circle:
                DrawCircle(gfx, circle, pen, brush);
                break;
            case VArc arc:
                DrawArc(gfx, arc, pen);
                break;
            case VEllipse ellipse:
                DrawEllipse(gfx, ellipse, pen, brush);
                break;
            case VRectangle rect:
                DrawRectangle(gfx, rect, pen, brush);
                break;
            case VPolygon polygon:
                DrawPolygon(gfx, polygon, pen, brush);
                break;
            case VPolyline polyline:
                DrawPolyline(gfx, polyline, pen);
                break;
            case VBezier bezier:
                DrawBezier(gfx, bezier, pen);
                break;
            case VSpline spline:
                DrawSpline(gfx, spline, pen);
                break;
            case VArrow arrow:
                DrawArrow(gfx, arrow, pen);
                break;
            case VText text:
                DrawText(gfx, text);
                break;
        }
    }

    private XPen CreatePen(Shape shape)
    {
        var color = ParseColor(shape.StrokeColor);
        return new XPen(color, shape.StrokeThickness);
    }

    private XBrush? CreateBrush(Shape shape)
    {
        if (string.IsNullOrEmpty(shape.FillColor) ||
            shape.FillColor.Equals("Transparent", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        var color = ParseColor(shape.FillColor);
        return new XSolidBrush(color);
    }

    private XColor ParseColor(string colorName)
    {
        if (string.IsNullOrEmpty(colorName))
            return XColors.Black;

        // Try named colors
        try
        {
            var prop = typeof(XColors).GetProperty(colorName,
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.IgnoreCase);
            if (prop != null)
            {
                return (XColor)prop.GetValue(null)!;
            }
        }
        catch { }

        // Try hex
        if (colorName.StartsWith("#"))
        {
            try
            {
                var hex = colorName.TrimStart('#');
                if (hex.Length == 6)
                {
                    var r = Convert.ToByte(hex.Substring(0, 2), 16);
                    var g = Convert.ToByte(hex.Substring(2, 2), 16);
                    var b = Convert.ToByte(hex.Substring(4, 2), 16);
                    return XColor.FromArgb(r, g, b);
                }
                else if (hex.Length == 8)
                {
                    var a = Convert.ToByte(hex.Substring(0, 2), 16);
                    var r = Convert.ToByte(hex.Substring(2, 2), 16);
                    var g = Convert.ToByte(hex.Substring(4, 2), 16);
                    var b = Convert.ToByte(hex.Substring(6, 2), 16);
                    return XColor.FromArgb(a, r, g, b);
                }
            }
            catch { }
        }

        return XColors.Black;
    }

    private void DrawPoint(XGraphics gfx, VPoint point, XPen pen)
    {
        gfx.DrawEllipse(pen, point.X - 2, point.Y - 2, 4, 4);
    }

    private void DrawLine(XGraphics gfx, VLine line, XPen pen)
    {
        gfx.DrawLine(pen, line.Start.X, line.Start.Y, line.End.X, line.End.Y);
    }

    private void DrawCircle(XGraphics gfx, VCircle circle, XPen pen, XBrush? brush)
    {
        var x = circle.Center.X - circle.Radius;
        var y = circle.Center.Y - circle.Radius;
        var size = circle.Radius * 2;

        if (brush != null)
        {
            gfx.DrawEllipse(brush, x, y, size, size);
        }
        gfx.DrawEllipse(pen, x, y, size, size);
    }

    private void DrawArc(XGraphics gfx, VArc arc, XPen pen)
    {
        var x = arc.Center.X - arc.Radius;
        var y = arc.Center.Y - arc.Radius;
        var size = arc.Radius * 2;

        // PdfSharp uses clockwise angles, so we may need to adjust
        double startAngle = -arc.StartAngle; // Negate for Y-flip
        double sweepAngle = -(arc.EndAngle - arc.StartAngle);

        gfx.DrawArc(pen, x, y, size, size, startAngle, sweepAngle);
    }

    private void DrawEllipse(XGraphics gfx, VEllipse ellipse, XPen pen, XBrush? brush)
    {
        var x = ellipse.Center.X - ellipse.RadiusX;
        var y = ellipse.Center.Y - ellipse.RadiusY;

        if (brush != null)
        {
            gfx.DrawEllipse(brush, x, y, ellipse.RadiusX * 2, ellipse.RadiusY * 2);
        }
        gfx.DrawEllipse(pen, x, y, ellipse.RadiusX * 2, ellipse.RadiusY * 2);
    }

    private void DrawRectangle(XGraphics gfx, VRectangle rect, XPen pen, XBrush? brush)
    {
        if (brush != null)
        {
            gfx.DrawRectangle(brush, rect.Corner.X, rect.Corner.Y, rect.Width, rect.Height);
        }
        gfx.DrawRectangle(pen, rect.Corner.X, rect.Corner.Y, rect.Width, rect.Height);
    }

    private void DrawPolygon(XGraphics gfx, VPolygon polygon, XPen pen, XBrush? brush)
    {
        if (polygon.Points.Count < 2) return;

        var points = new XPoint[polygon.Points.Count];
        for (int i = 0; i < polygon.Points.Count; i++)
        {
            points[i] = new XPoint(polygon.Points[i].X, polygon.Points[i].Y);
        }

        if (brush != null)
        {
            gfx.DrawPolygon(brush, points, XFillMode.Winding);
        }
        gfx.DrawPolygon(pen, points);
    }

    private void DrawPolyline(XGraphics gfx, VPolyline polyline, XPen pen)
    {
        if (polyline.Points.Count < 2) return;

        for (int i = 0; i < polyline.Points.Count - 1; i++)
        {
            gfx.DrawLine(pen,
                polyline.Points[i].X, polyline.Points[i].Y,
                polyline.Points[i + 1].X, polyline.Points[i + 1].Y);
        }
    }

    private void DrawBezier(XGraphics gfx, VBezier bezier, XPen pen)
    {
        gfx.DrawBezier(pen,
            bezier.P0.X, bezier.P0.Y,
            bezier.P1.X, bezier.P1.Y,
            bezier.P2.X, bezier.P2.Y,
            bezier.P3.X, bezier.P3.Y);
    }

    private void DrawSpline(XGraphics gfx, VSpline spline, XPen pen)
    {
        if (spline.ControlPoints.Count < 2) return;

        // Draw as polyline through control points (approximate)
        for (int i = 0; i < spline.ControlPoints.Count - 1; i++)
        {
            gfx.DrawLine(pen,
                spline.ControlPoints[i].X, spline.ControlPoints[i].Y,
                spline.ControlPoints[i + 1].X, spline.ControlPoints[i + 1].Y);
        }
    }

    private void DrawArrow(XGraphics gfx, VArrow arrow, XPen pen)
    {
        // Draw main line
        gfx.DrawLine(pen, arrow.Start.X, arrow.Start.Y, arrow.End.X, arrow.End.Y);

        // Draw arrowhead
        double dx = arrow.End.X - arrow.Start.X;
        double dy = arrow.End.Y - arrow.Start.Y;
        double length = Math.Sqrt(dx * dx + dy * dy);

        if (length > 0)
        {
            double headSize = Math.Min(length * 0.2, arrow.HeadLength);
            double angle = Math.Atan2(dy, dx);
            double headAngleRad = arrow.HeadAngle * Math.PI / 180;

            double x1 = arrow.End.X - headSize * Math.Cos(angle - headAngleRad);
            double y1 = arrow.End.Y - headSize * Math.Sin(angle - headAngleRad);
            double x2 = arrow.End.X - headSize * Math.Cos(angle + headAngleRad);
            double y2 = arrow.End.Y - headSize * Math.Sin(angle + headAngleRad);

            gfx.DrawLine(pen, arrow.End.X, arrow.End.Y, x1, y1);
            gfx.DrawLine(pen, arrow.End.X, arrow.End.Y, x2, y2);
        }
    }

    private void DrawText(XGraphics gfx, VText text)
    {
        var color = ParseColor(text.StrokeColor);
        var brush = new XSolidBrush(color);
        var font = new XFont("Arial", text.Height, XFontStyleEx.Regular);

        // Text drawing with Y-flip correction
        gfx.Save();
        gfx.TranslateTransform(text.Location.X, text.Location.Y);
        gfx.ScaleTransform(1, -1); // Un-flip for text
        gfx.DrawString(text.Content ?? "", font, brush, 0, 0);
        gfx.Restore();
    }
}
