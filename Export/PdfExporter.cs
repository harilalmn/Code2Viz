using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
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
    private const double DipToPoint = 72.0 / 96.0;

    // Compensates display-unit elements (line weights, point markers) for
    // the ScaleTransform so they stay at their intended physical size.
    private double _uiSizeScale = 1.0;

    /// <summary>
    /// Exports shapes to a PDF file with auto-sized page.
    /// </summary>
    public void Export(IReadOnlyList<IDrawable> shapes, string filePath)
    {
        if (shapes.Count == 0) return;

        // Snapshot to avoid "collection was modified" during enumeration
        shapes = shapes.ToList();

        _uiSizeScale = 1.0;

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
            if (drawable is Shape shape && shape.IsVisible)
            {
                DrawShape(gfx, shape);
            }
        }

        // Save
        document.Save(filePath);
    }

    /// <summary>
    /// Exports shapes to a PDF file with specified page size, scale, and margins.
    /// </summary>
    /// <param name="shapes">Shapes to export.</param>
    /// <param name="filePath">Output file path.</param>
    /// <param name="pageWidthMm">Page width in mm (0 = auto-size to content).</param>
    /// <param name="pageHeightMm">Page height in mm (0 = auto-size to content).</param>
    /// <param name="scaleMmPerUnit">Scale factor: 1 drawing unit = this many mm on paper.</param>
    /// <param name="marginMm">Page margin in mm.</param>
    public void Export(IReadOnlyList<IDrawable> shapes, string filePath,
        double pageWidthMm, double pageHeightMm, double scaleMmPerUnit, double marginMm)
    {
        if (shapes.Count == 0) return;

        // Snapshot to avoid "collection was modified" during enumeration
        shapes = shapes.ToList();

        const double mmToPoints = 72.0 / 25.4;

        // Calculate content bounds in drawing units
        var (minPt, maxPt) = GetBounds(shapes);
        double contentW = maxPt.X - minPt.X;
        double contentH = maxPt.Y - minPt.Y;

        // Content size in mm
        double contentWMm = contentW * scaleMmPerUnit;
        double contentHMm = contentH * scaleMmPerUnit;

        // Determine page size in mm
        double pageW, pageH;
        if (pageWidthMm <= 0 || pageHeightMm <= 0)
        {
            // Auto-size: content + margins
            pageW = contentWMm + 2 * marginMm;
            pageH = contentHMm + 2 * marginMm;
        }
        else
        {
            pageW = pageWidthMm;
            pageH = pageHeightMm;
        }

        // Convert to PDF points
        double pageWPt = pageW * mmToPoints;
        double pageHPt = pageH * mmToPoints;
        double marginPt = marginMm * mmToPoints;
        double scalePtPerUnit = scaleMmPerUnit * mmToPoints;

        // Keep text/point marker sizes visually consistent even when geometry is scaled.
        _uiSizeScale = 1.0 / scalePtPerUnit;

        // Create PDF document
        var document = new PdfDocument();
        document.Info.Title = "Code2Viz Export";

        var page = document.AddPage();
        page.Width = XUnit.FromPoint(Math.Max(pageWPt, 10));
        page.Height = XUnit.FromPoint(Math.Max(pageHPt, 10));

        using var gfx = XGraphics.FromPdfPage(page);

        // Printable area in points
        double printableWPt = pageWPt - 2 * marginPt;
        double printableHPt = pageHPt - 2 * marginPt;

        // Content size in points (at scale)
        double contentWPt = contentW * scalePtPerUnit;
        double contentHPt = contentH * scalePtPerUnit;

        // Center content in printable area
        double offsetXPt = marginPt + (printableWPt - contentWPt) / 2;
        double offsetYPt = marginPt + (printableHPt - contentHPt) / 2;

        // Transform: translate to position content, apply scale, flip Y
        gfx.TranslateTransform(offsetXPt - minPt.X * scalePtPerUnit,
            page.Height.Point - offsetYPt + minPt.Y * scalePtPerUnit);
        gfx.ScaleTransform(scalePtPerUnit, -scalePtPerUnit);

        // Draw shapes
        foreach (var drawable in shapes)
        {
            if (drawable is Shape shape && shape.IsVisible)
            {
                DrawShape(gfx, shape);
            }
        }

        // Save
        document.Save(filePath);
    }

    private BoundingBox GetBounds(IReadOnlyList<IDrawable> shapes)
    {
        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;

        foreach (var drawable in shapes)
        {
            if (drawable is Shape shape)
            {
                var bounds = shape.GetBounds();
                minX = Math.Min(minX, bounds.Min.X);
                minY = Math.Min(minY, bounds.Min.Y);
                maxX = Math.Max(maxX, bounds.Max.X);
                maxY = Math.Max(maxY, bounds.Max.Y);
            }
        }

        if (minX == double.MaxValue)
        {
            return new BoundingBox(VPoint.Internal(0, 0), VPoint.Internal(100, 100));
        }

        return new BoundingBox(VPoint.Internal(minX, minY), VPoint.Internal(maxX, maxY));
    }

    private void DrawShape(XGraphics gfx, Shape shape)
    {
        var pen = CreatePen(shape);
        var brush = CreateBrush(shape);

        switch (shape)
        {
            case VRadialDimension radDim:
                DrawRadialDimension(gfx, radDim);
                break;
            case VDimension dim:
                DrawDimension(gfx, dim);
                break;
            case VPoint point:
                if (ShouldExportPoint(point))
                {
                    DrawPoint(gfx, point, pen, brush);
                }
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
            case VHatch hatch:
                DrawHatch(gfx, hatch, pen);
                break;
        }
    }

    private void DrawHatch(XGraphics gfx, VHatch hatch, XPen pen)
    {
        if (hatch.Boundary.Count < 3) return;
        var lines = hatch.GenerateLines();
        foreach (var (start, end) in lines)
        {
            gfx.DrawLine(pen,
                new XPoint(start.X, -start.Y),
                new XPoint(end.X, -end.Y));
        }
    }

    private XPen CreatePen(Shape shape)
    {
        var color = ParseColor(shape.Color);
        // LineWeight is in WPF DIPs (display pixels); convert to points and
        // compensate for the geometry ScaleTransform so strokes stay a fixed
        // physical width on paper.
        return new XPen(color, Math.Max(shape.LineWeight * DipToPoint * _uiSizeScale, 0.001));
    }

    private XPen CreatePen(string colorName, double lineWeight)
    {
        var color = ParseColor(colorName);
        return new XPen(color, Math.Max(lineWeight * DipToPoint * _uiSizeScale, 0.001));
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

    /// <summary>
    /// Parses a color string using WPF's ColorConverter for exact color matching
    /// with the canvas rendering, then converts to PdfSharp XColor.
    /// </summary>
    private XColor ParseColor(string colorName)
    {
        if (string.IsNullOrEmpty(colorName))
            return XColors.Black;

        // Use WPF's ColorConverter (same parser the canvas uses),
        // so named colors and hex values resolve identically.
        try
        {
            var wpfColor = (Color)ColorConverter.ConvertFromString(colorName);
            return XColor.FromArgb(wpfColor.A, wpfColor.R, wpfColor.G, wpfColor.B);
        }
        catch
        {
            // Fallback: should rarely happen since WPF ColorConverter
            // handles all named colors and hex formats.
            return XColors.Black;
        }
    }

    private void DrawPoint(XGraphics gfx, VPoint point, XPen pen, XBrush? brush)
    {
        double r = 2 * _uiSizeScale;
        if (brush != null)
        {
            gfx.DrawEllipse(brush, point.X - r, point.Y - r, r * 2, r * 2);
        }
        gfx.DrawEllipse(pen, point.X - r, point.Y - r, r * 2, r * 2);
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
        var color = ParseColor(text.Color);
        var brush = new XSolidBrush(color);
        var fontFamily = text.Font switch
        {
            VFont.TimesNewRoman => "Times New Roman",
            VFont.CourierNew => "Courier New",
            VFont.Consolas => "Consolas",
            _ => "Arial"
        };
        var fontStyle = text.FontWeight == VFontWeight.Bold ? XFontStyleEx.Bold : XFontStyleEx.Regular;
        var font = new XFont(fontFamily, Math.Max(text.Height, 0.1), fontStyle);

        // Measure text for anchor offset
        var measuredWidth = gfx.MeasureString(text.Content ?? "", font).Width;
        var measuredHeight = text.Height;
        var (anchorOffsetX, anchorOffsetY) = text.GetAnchorOffset(measuredWidth, measuredHeight);

        // Text drawing with Y-flip correction. Angle rotates around Location (CCW in world Y-up).
        gfx.Save();
        gfx.TranslateTransform(text.Location.X, text.Location.Y);
        if (text.Angle != 0)
            gfx.RotateTransform(text.Angle); // Outer scale(1,-1) makes RotateTransform CCW in world coords.
        gfx.TranslateTransform(anchorOffsetX, anchorOffsetY);
        gfx.ScaleTransform(1, -1); // Un-flip for text
        gfx.DrawString(text.Content ?? "", font, brush, 0, 0);
        gfx.Restore();
    }

    private void DrawRadialDimension(XGraphics gfx, VRadialDimension dim)
    {
        var (leaderStart, leaderEnd, textPos) = dim.GetDimensionGeometry();

        string dimLineColor = dim.DimensionLineColor ?? dim.Color;
        string textColorName = dim.TextColor ?? dim.Color;

        var dimPen = CreatePen(dimLineColor, dim.LineWeight);
        var dimBrush = new XSolidBrush(ParseColor(dimLineColor));
        string displayText = dim.DisplayText;

        // Leader line with text gap
        var dimDx = leaderEnd.X - leaderStart.X;
        var dimDy = leaderEnd.Y - leaderStart.Y;
        var dimLength = Math.Sqrt(dimDx * dimDx + dimDy * dimDy);
        if (dimLength > 1e-10)
        {
            var gapFont = new XFont("Arial", Math.Max(dim.TextHeight, 0.1), XFontStyleEx.Regular);
            var textSizeForGap = gfx.MeasureString(displayText, gapFont);
            var textWorldWidth = textSizeForGap.Width;
            var padding = textWorldWidth * 0.15;
            var halfGap = textWorldWidth / 2 + padding;

            var dirX = dimDx / dimLength;
            var dirY = dimDy / dimLength;
            var midX = (leaderStart.X + leaderEnd.X) / 2;
            var midY = (leaderStart.Y + leaderEnd.Y) / 2;

            gfx.DrawLine(dimPen, leaderStart.X, leaderStart.Y,
                midX - dirX * halfGap, midY - dirY * halfGap);
            gfx.DrawLine(dimPen, midX + dirX * halfGap, midY + dirY * halfGap,
                leaderEnd.X, leaderEnd.Y);
        }
        else
        {
            gfx.DrawLine(dimPen, leaderStart.X, leaderStart.Y, leaderEnd.X, leaderEnd.Y);
        }

        // Arrowhead at circumference
        DrawDimensionArrowhead(gfx, dimBrush, leaderEnd, leaderStart, dim.ArrowSize);
        if (dim.ShowDiameter)
            DrawDimensionArrowhead(gfx, dimBrush, leaderStart, leaderEnd, dim.ArrowSize);

        // Text
        var textColor = ParseColor(textColorName);
        var textBrush = new XSolidBrush(textColor);
        var fontSize = dim.TextHeight;
        var font = new XFont("Arial", Math.Max(fontSize, 0.1), XFontStyleEx.Regular);
        var textSize = gfx.MeasureString(displayText, font);

        gfx.Save();
        gfx.TranslateTransform(textPos.X, textPos.Y);
        gfx.ScaleTransform(1, -1);

        if (dim.TextBackgroundOpaque)
        {
            gfx.DrawRectangle(XBrushes.White,
                -textSize.Width / 2, -textSize.Height / 2, textSize.Width, textSize.Height);
        }

        gfx.DrawString(displayText, font, textBrush, 0, -textSize.Height / 2,
            XStringFormats.TopCenter);
        gfx.Restore();
    }

    private void DrawDimension(XGraphics gfx, VDimension dim)
    {
        var geom = dim.GetDimensionGeometry();

        string extColor = dim.ExtensionLineColor ?? dim.Color;
        string dimLineColor = dim.DimensionLineColor ?? dim.Color;
        string textColorName = dim.TextColor ?? dim.Color;

        var extPen = CreatePen(extColor, dim.LineWeight);
        var dimPen = CreatePen(dimLineColor, dim.LineWeight);
        var dimBrush = new XSolidBrush(ParseColor(dimLineColor));
        string displayText = dim.DisplayText;

        // Extension lines
        if (!dim.SuppressExtLine1)
            gfx.DrawLine(extPen, geom.ext1Start.X, geom.ext1Start.Y, geom.ext1End.X, geom.ext1End.Y);
        if (!dim.SuppressExtLine2)
            gfx.DrawLine(extPen, geom.ext2Start.X, geom.ext2Start.Y, geom.ext2End.X, geom.ext2End.Y);

        // Dimension line and arrowheads
        if (!dim.SuppressDimensionLine)
        {
            // Mirror canvas behavior: split the dimension line around the text gap.
            var dimDx = geom.dimEnd.X - geom.dimStart.X;
            var dimDy = geom.dimEnd.Y - geom.dimStart.Y;
            var dimLength = Math.Sqrt(dimDx * dimDx + dimDy * dimDy);
            if (dimLength > 1e-10)
            {
                var gapFont = new XFont("Arial", Math.Max(dim.TextHeight, 0.1), XFontStyleEx.Regular);
                var textSizeForGap = gfx.MeasureString(displayText, gapFont);
                var textWorldWidth = textSizeForGap.Width;
                var padding = textWorldWidth * 0.15;
                var halfGap = textWorldWidth / 2 + padding;

                var dirX = dimDx / dimLength;
                var dirY = dimDy / dimLength;
                var midX = (geom.dimStart.X + geom.dimEnd.X) / 2;
                var midY = (geom.dimStart.Y + geom.dimEnd.Y) / 2;

                var gapStartX = midX - dirX * halfGap;
                var gapStartY = midY - dirY * halfGap;
                var gapEndX = midX + dirX * halfGap;
                var gapEndY = midY + dirY * halfGap;

                gfx.DrawLine(dimPen, geom.dimStart.X, geom.dimStart.Y, gapStartX, gapStartY);
                gfx.DrawLine(dimPen, gapEndX, gapEndY, geom.dimEnd.X, geom.dimEnd.Y);
            }
            else
            {
                gfx.DrawLine(dimPen, geom.dimStart.X, geom.dimStart.Y, geom.dimEnd.X, geom.dimEnd.Y);
            }

            // Filled arrowheads — in drawing units, scale with geometry
            // (matches canvas WorldToScreen behavior).
            DrawDimensionArrowhead(gfx, dimBrush, geom.dimStart, geom.dimEnd, dim.ArrowSize);
            DrawDimensionArrowhead(gfx, dimBrush, geom.dimEnd, geom.dimStart, dim.ArrowSize);
        }

        // Text – font size in drawing units; the global ScaleTransform scales it to paper size.
        var textColor = ParseColor(textColorName);
        var textBrush = new XSolidBrush(textColor);
        var fontSize = dim.TextHeight;
        var font = new XFont("Arial", Math.Max(fontSize, 0.1), XFontStyleEx.Regular);
        var textSize = gfx.MeasureString(displayText, font);

        gfx.Save();
        gfx.TranslateTransform(geom.textPos.X, geom.textPos.Y);
        gfx.ScaleTransform(1, -1); // Un-flip for text

        if (dim.TextBackgroundOpaque)
        {
            gfx.DrawRectangle(XBrushes.White,
                -textSize.Width / 2, -textSize.Height / 2, textSize.Width, textSize.Height);
        }

        gfx.DrawString(displayText, font, textBrush, 0, -textSize.Height / 2,
            XStringFormats.TopCenter);
        gfx.Restore();
    }

    private static void DrawDimensionArrowhead(XGraphics gfx, XBrush brush, VPoint tipPoint, VPoint tailPoint, double arrowSize)
    {
        var dx = tipPoint.X - tailPoint.X;
        var dy = tipPoint.Y - tailPoint.Y;
        var length = Math.Sqrt(dx * dx + dy * dy);
        if (length < 1e-10) return;

        var dirX = dx / length;
        var dirY = dy / length;
        var perpX = -dirY;
        var perpY = dirX;
        var halfWidth = arrowSize / 6.0;

        var tip = new XPoint(tipPoint.X, tipPoint.Y);
        var wing1 = new XPoint(
            tipPoint.X - dirX * arrowSize + perpX * halfWidth,
            tipPoint.Y - dirY * arrowSize + perpY * halfWidth);
        var wing2 = new XPoint(
            tipPoint.X - dirX * arrowSize - perpX * halfWidth,
            tipPoint.Y - dirY * arrowSize - perpY * halfWidth);

        // Use XGraphicsPath for reliable filled rendering under Y-flipped transforms.
        var path = new XGraphicsPath();
        path.AddPolygon([tip, wing1, wing2]);
        gfx.DrawPath(brush, path);
    }

    private static bool ShouldExportPoint(VPoint point)
    {
        // Most leaked helper points are auto-registered with default point styling.
        // Keep explicit points, and keep any styled points likely intended by the user.
        if (point.IsExplicitlyDrawn)
            return true;

        string defaultPointColor = ShapeDefaults.GlobalColor ?? "White";
        string defaultPointFill = ShapeDefaults.GlobalFillColor ?? "LimeGreen";
        double defaultPointWeight = ShapeDefaults.GlobalLineWeight ?? 2.0;

        bool hasDefaultAppearance =
            string.Equals(point.Color, defaultPointColor, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(point.FillColor, defaultPointFill, StringComparison.OrdinalIgnoreCase) &&
            Math.Abs(point.LineWeight - defaultPointWeight) < 1e-9;

        return !hasDefaultAppearance;
    }
}

