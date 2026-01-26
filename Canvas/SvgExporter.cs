using System.Globalization;
using System.Text;
using Code2Viz.Geometry;

namespace Code2Viz.Canvas;

/// <summary>
/// Exports shapes to SVG format.
/// </summary>
public static class SvgExporter
{
    /// <summary>
    /// Exports shapes to an SVG string.
    /// </summary>
    public static string Export(IEnumerable<IDrawable> shapes, double width = 800, double height = 600, double padding = 20)
    {
        var shapeList = shapes.ToList();
        
        // Calculate bounds
        double minX = 0, minY = 0, maxX = width, maxY = height;
        if (shapeList.Any())
        {
            minX = double.MaxValue; minY = double.MaxValue;
            maxX = double.MinValue; maxY = double.MinValue;
            
            foreach (var shape in shapeList.OfType<Shape>())
            {
                var (min, max) = shape.GetBounds();
                minX = Math.Min(minX, min.X);
                minY = Math.Min(minY, min.Y);
                maxX = Math.Max(maxX, max.X);
                maxY = Math.Max(maxY, max.Y);
            }
        }
        
        // Add padding
        minX -= padding;
        minY -= padding;
        maxX += padding;
        maxY += padding;
        
        var viewWidth = maxX - minX;
        var viewHeight = maxY - minY;
        
        var sb = new StringBuilder();
        sb.AppendLine($"<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{width}\" height=\"{height}\" viewBox=\"{F(minX)} {F(-maxY)} {F(viewWidth)} {F(viewHeight)}\">");
        sb.AppendLine("  <g transform=\"scale(1, -1)\">");  // Flip Y for math coordinates
        
        foreach (var shape in shapeList)
        {
            var svgElement = ShapeToSvg(shape);
            if (!string.IsNullOrEmpty(svgElement))
                sb.AppendLine("    " + svgElement);
        }
        
        sb.AppendLine("  </g>");
        sb.AppendLine("</svg>");
        
        return sb.ToString();
    }

    private static string ShapeToSvg(IDrawable drawable)
    {
        return drawable switch
        {
            VPoint p => $"<circle cx=\"{F(p.X)}\" cy=\"{F(p.Y)}\" r=\"5\" fill=\"{p.FillColor}\" stroke=\"{p.Color}\" stroke-width=\"{F(p.LineWeight)}\" />",
            
            VLine l => $"<line x1=\"{F(l.Start.X)}\" y1=\"{F(l.Start.Y)}\" x2=\"{F(l.End.X)}\" y2=\"{F(l.End.Y)}\" stroke=\"{l.Color}\" stroke-width=\"{F(l.LineWeight)}\" />",
            
            VCircle c => $"<circle cx=\"{F(c.Center.X)}\" cy=\"{F(c.Center.Y)}\" r=\"{F(c.Radius)}\" fill=\"{c.FillColor}\" stroke=\"{c.Color}\" stroke-width=\"{F(c.LineWeight)}\" />",
            
            VEllipse e => $"<ellipse cx=\"{F(e.Center.X)}\" cy=\"{F(e.Center.Y)}\" rx=\"{F(e.RadiusX)}\" ry=\"{F(e.RadiusY)}\" fill=\"{e.FillColor}\" stroke=\"{e.Color}\" stroke-width=\"{F(e.LineWeight)}\" />",
            
            VRectangle r => $"<rect x=\"{F(r.Corner.X)}\" y=\"{F(r.Corner.Y)}\" width=\"{F(r.Width)}\" height=\"{F(r.Height)}\" fill=\"{r.FillColor}\" stroke=\"{r.Color}\" stroke-width=\"{F(r.LineWeight)}\" />",
            
            VArc a => ArcToSvg(a),
            VPolygon pg => PolygonToSvg(pg),
            VPolyline pl => PolylineToSvg(pl),
            VBezier b => BezierToSvg(b),
            VSpline s => SplineToSvg(s),
            VArrow ar => ArrowToSvg(ar),
            VDimension d => DimensionToSvg(d),
            VText t => $"<text x=\"{F(t.Location.X)}\" y=\"{F(t.Location.Y)}\" fill=\"{t.Color}\" font-size=\"{F(t.Height)}\" transform=\"scale(1,-1)\">{EscapeXml(t.Content)}</text>",
            VGroup g => GroupToSvg(g),
            _ => ""
        };
    }

    private static string ArcToSvg(VArc arc)
    {
        var startRad = arc.StartAngle * Math.PI / 180;
        var endRad = arc.EndAngle * Math.PI / 180;
        var startX = arc.Center.X + arc.Radius * Math.Cos(startRad);
        var startY = arc.Center.Y + arc.Radius * Math.Sin(startRad);
        var endX = arc.Center.X + arc.Radius * Math.Cos(endRad);
        var endY = arc.Center.Y + arc.Radius * Math.Sin(endRad);
        
        var angleDiff = arc.EndAngle - arc.StartAngle;
        if (angleDiff < 0) angleDiff += 360;
        var largeArc = angleDiff > 180 ? 1 : 0;
        
        return $"<path d=\"M {F(startX)} {F(startY)} A {F(arc.Radius)} {F(arc.Radius)} 0 {largeArc} 0 {F(endX)} {F(endY)}\" fill=\"none\" stroke=\"{arc.Color}\" stroke-width=\"{F(arc.LineWeight)}\" />";
    }

    private static string PolygonToSvg(VPolygon polygon)
    {
        if (polygon.Points.Count < 3) return "";
        var points = string.Join(" ", polygon.Points.Select(p => $"{F(p.X)},{F(p.Y)}"));
        return $"<polygon points=\"{points}\" fill=\"{polygon.FillColor}\" stroke=\"{polygon.Color}\" stroke-width=\"{F(polygon.LineWeight)}\" />";
    }

    private static string PolylineToSvg(VPolyline polyline)
    {
        if (polyline.Points.Count < 2) return "";
        var points = string.Join(" ", polyline.Points.Select(p => $"{F(p.X)},{F(p.Y)}"));
        return $"<polyline points=\"{points}\" fill=\"none\" stroke=\"{polyline.Color}\" stroke-width=\"{F(polyline.LineWeight)}\" />";
    }

    private static string BezierToSvg(VBezier bezier)
    {
        return $"<path d=\"M {F(bezier.P0.X)} {F(bezier.P0.Y)} C {F(bezier.P1.X)} {F(bezier.P1.Y)}, {F(bezier.P2.X)} {F(bezier.P2.Y)}, {F(bezier.P3.X)} {F(bezier.P3.Y)}\" fill=\"none\" stroke=\"{bezier.Color}\" stroke-width=\"{F(bezier.LineWeight)}\" />";
    }

    private static string SplineToSvg(VSpline spline)
    {
        var points = spline.GetRenderPoints();
        if (points.Count < 2) return "";
        
        var pathData = $"M {F(points[0].X)} {F(points[0].Y)}";
        for (int i = 1; i < points.Count; i++)
            pathData += $" L {F(points[i].X)} {F(points[i].Y)}";
        
        return $"<path d=\"{pathData}\" fill=\"none\" stroke=\"{spline.Color}\" stroke-width=\"{F(spline.LineWeight)}\" />";
    }

    private static string ArrowToSvg(VArrow arrow)
    {
        var (w1, w2) = arrow.GetEndArrowhead();
        var sb = new StringBuilder();
        // Main line
        sb.Append($"<line x1=\"{F(arrow.Start.X)}\" y1=\"{F(arrow.Start.Y)}\" x2=\"{F(arrow.End.X)}\" y2=\"{F(arrow.End.Y)}\" stroke=\"{arrow.Color}\" stroke-width=\"{F(arrow.LineWeight)}\" />");
        // Filled arrowhead polygon
        sb.Append($"<polygon points=\"{F(arrow.End.X)},{F(arrow.End.Y)} {F(w1.X)},{F(w1.Y)} {F(w2.X)},{F(w2.Y)}\" fill=\"{arrow.Color}\" stroke=\"{arrow.Color}\" stroke-width=\"{F(arrow.LineWeight)}\" />");
        return $"<g>{sb}</g>";
    }

    private static string DimensionToSvg(VDimension dim)
    {
        var (ds, de, tp, e1s, e1e, e2s, e2e) = dim.GetDimensionGeometry();
        var sb = new StringBuilder();
        sb.Append($"<line x1=\"{F(ds.X)}\" y1=\"{F(ds.Y)}\" x2=\"{F(de.X)}\" y2=\"{F(de.Y)}\" stroke=\"{dim.Color}\" stroke-width=\"{F(dim.LineWeight)}\" />");
        sb.Append($"<line x1=\"{F(e1s.X)}\" y1=\"{F(e1s.Y)}\" x2=\"{F(e1e.X)}\" y2=\"{F(e1e.Y)}\" stroke=\"{dim.Color}\" stroke-width=\"{F(dim.LineWeight)}\" />");
        sb.Append($"<line x1=\"{F(e2s.X)}\" y1=\"{F(e2s.Y)}\" x2=\"{F(e2e.X)}\" y2=\"{F(e2e.Y)}\" stroke=\"{dim.Color}\" stroke-width=\"{F(dim.LineWeight)}\" />");
        sb.Append($"<text x=\"{F(tp.X)}\" y=\"{F(tp.Y)}\" fill=\"{dim.Color}\" font-size=\"{F(dim.TextHeight)}\" text-anchor=\"middle\" transform=\"scale(1,-1)\">{dim.DisplayText}</text>");
        return $"<g>{sb}</g>";
    }

    private static string GroupToSvg(VGroup group)
    {
        var sb = new StringBuilder("<g>");
        foreach (var shape in group.Shapes)
        {
            var svgElement = ShapeToSvg(shape);
            if (!string.IsNullOrEmpty(svgElement))
                sb.Append(svgElement);
        }
        sb.Append("</g>");
        return sb.ToString();
    }

    private static string F(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
    
    private static string EscapeXml(string text) => 
        text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

    /// <summary>
    /// Saves shapes to an SVG file.
    /// </summary>
    public static void SaveToFile(string filePath, IEnumerable<IDrawable> shapes, double width = 800, double height = 600)
    {
        var svg = Export(shapes, width, height);
        System.IO.File.WriteAllText(filePath, svg);
    }
}
