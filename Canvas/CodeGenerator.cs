using System.Globalization;
using System.Text.RegularExpressions;
using C2VGeometry;

namespace Code2Viz.Canvas;

/// <summary>
/// Generates C# code strings for shapes created by the drawing tool.
/// </summary>
public static class CodeGenerator
{
    // Counters for sequential variable names
    private static readonly Dictionary<string, int> _shapeCounters = new();

    // Pattern to match variable declarations like "var line1", "VLine line2"
    private static readonly Regex _variablePattern = new(
        @"\b(?:var|VPoint|VLine|VCircle|VRectangle|VEllipse|VArc|VPolygon|VPolyline|VBezier|VSpline|VArrow|VText|VGrid|VGroup)\s+(point|line|circle|rect|ellipse|arc|polygon|polyline|bezier|spline|arrow|text|grid|group)(\d+)\b",
        RegexOptions.Compiled);

    /// <summary>
    /// Resets all shape counters (call when starting a new project or clearing the canvas).
    /// </summary>
    public static void ResetCounters()
    {
        _shapeCounters.Clear();
    }

    /// <summary>
    /// Scans existing code to find the highest counter values for each shape type.
    /// Call this before generating new code to avoid duplicate variable names.
    /// </summary>
    /// <param name="existingCode">The existing source code to scan.</param>
    public static void SyncCountersFromCode(string existingCode)
    {
        if (string.IsNullOrEmpty(existingCode))
            return;

        var matches = _variablePattern.Matches(existingCode);
        foreach (Match match in matches)
        {
            var shapeName = match.Groups[1].Value; // e.g., "line", "circle"
            var numberStr = match.Groups[2].Value; // e.g., "1", "5"

            if (int.TryParse(numberStr, out var number))
            {
                if (!_shapeCounters.TryGetValue(shapeName, out var current) || current < number)
                {
                    _shapeCounters[shapeName] = number;
                }
            }
        }
    }

    private static int GetNextCounter(string shapeName)
    {
        if (!_shapeCounters.TryGetValue(shapeName, out var count))
        {
            count = 0;
        }
        count++;
        _shapeCounters[shapeName] = count;
        return count;
    }

    /// <summary>
    /// Generates the constructor call for a shape (e.g., "new VCircle(0, 0, 10)").
    /// Used for code sync when updating existing shapes.
    /// </summary>
    public static string GenerateConstructorCall(Shape shape)
    {
        return shape switch
        {
            VPoint p => $"new VPoint({FormatDouble(p.X)}, {FormatDouble(p.Y)})",
            VLine l => $"new VLine({FormatDouble(l.Start.X)}, {FormatDouble(l.Start.Y)}, {FormatDouble(l.End.X)}, {FormatDouble(l.End.Y)})",
            VCircle c => $"new VCircle({FormatDouble(c.Center.X)}, {FormatDouble(c.Center.Y)}, {FormatDouble(c.Radius)})",
            VRectangle r => $"new VRectangle({FormatDouble(r.Corner.X)}, {FormatDouble(r.Corner.Y)}, {FormatDouble(r.Width)}, {FormatDouble(r.Height)})",
            VEllipse e => $"new VEllipse({FormatDouble(e.Center.X)}, {FormatDouble(e.Center.Y)}, {FormatDouble(e.RadiusX)}, {FormatDouble(e.RadiusY)})",
            VArc a => $"new VArc({FormatDouble(a.Center.X)}, {FormatDouble(a.Center.Y)}, {FormatDouble(a.Radius)}, {FormatDouble(a.StartAngle)}, {FormatDouble(a.EndAngle)})",
            VArrow ar => $"new VArrow({FormatDouble(ar.Start.X)}, {FormatDouble(ar.Start.Y)}, {FormatDouble(ar.End.X)}, {FormatDouble(ar.End.Y)})",
            VBezier b => $"new VBezier({FormatDouble(b.P0.X)}, {FormatDouble(b.P0.Y)}, {FormatDouble(b.P1.X)}, {FormatDouble(b.P1.Y)}, {FormatDouble(b.P2.X)}, {FormatDouble(b.P2.Y)}, {FormatDouble(b.P3.X)}, {FormatDouble(b.P3.Y)})",
            VText t => $"new VText({FormatDouble(t.Location.X)}, {FormatDouble(t.Location.Y)}, \"{t.Content.Replace("\\", "\\\\").Replace("\"", "\\\"")}\")",
            VPolygon pg => GeneratePolygonConstructor(pg),
            VPolyline pl => GeneratePolylineConstructor(pl),
            VSpline s => GenerateSplineConstructor(s),
            _ => $"/* Unknown shape: {shape.GetType().Name} */"
        };
    }

    private static string GeneratePolygonConstructor(VPolygon pg)
    {
        if (pg.Points.Count == 0)
            return "new VPolygon()";
        var points = string.Join(", ", pg.Points.Select(p => $"new VXYZ({FormatDouble(p.X)}, {FormatDouble(p.Y)})"));
        return $"new VPolygon({points})";
    }

    private static string GeneratePolylineConstructor(VPolyline pl)
    {
        if (pl.Points.Count == 0)
            return "new VPolyline()";
        var points = string.Join(", ", pl.Points.Select(p => $"new VXYZ({FormatDouble(p.X)}, {FormatDouble(p.Y)})"));
        return $"new VPolyline({points})";
    }

    private static string GenerateSplineConstructor(VSpline s)
    {
        if (s.ControlPoints.Count == 0)
            return "new VSpline()";
        var points = string.Join(", ", s.ControlPoints.Select(p => $"new VXYZ({FormatDouble(p.X)}, {FormatDouble(p.Y)})"));
        return $"new VSpline({points})";
    }

    /// <summary>
    /// Generates code to create the given shape with a sequential variable name.
    /// </summary>
    public static string GenerateCode(Shape shape)
    {
        return shape switch
        {
            VPoint p => GeneratePointCode(p),
            VLine l => GenerateLineCode(l),
            VCircle c => GenerateCircleCode(c),
            VRectangle r => GenerateRectangleCode(r),
            VEllipse e => GenerateEllipseCode(e),
            VArc a => GenerateArcCode(a),
            VPolygon pg => GeneratePolygonCode(pg),
            VPolyline pl => GeneratePolylineCode(pl),
            VBezier b => GenerateBezierCode(b),
            VSpline s => GenerateSplineCode(s),
            VArrow ar => GenerateArrowCode(ar),
            VText t => GenerateTextCode(t),
            _ => $"// Unknown shape: {shape.GetType().Name}"
        };
    }

    private static string FormatDouble(double value)
    {
        return value.ToString("F2", CultureInfo.InvariantCulture);
    }

    private const string Indent = "            ";

    /// <summary>
    /// Generates style property assignments for a shape (Color, etc.).
    /// </summary>
    private static string GenerateStyleCode(Shape shape, string varName)
    {
        var lines = new List<string>();

        if (!string.IsNullOrEmpty(shape.Color))
            lines.Add($"{varName}.Color = \"{shape.Color}\";");

        if (!string.IsNullOrEmpty(shape.FillColor) &&
            !shape.FillColor.Equals("Transparent", StringComparison.OrdinalIgnoreCase))
            lines.Add($"{varName}.FillColor = \"{shape.FillColor}\";");

        if (Math.Abs(shape.LineWeight - 2.0) > 0.01)
            lines.Add($"{varName}.LineWeight = {FormatDouble(shape.LineWeight)};");

        if (lines.Count == 0)
            return "";

        return Indent + string.Join("\n" + Indent, lines) + "\n";
    }

    private static string GeneratePointCode(VPoint p)
    {
        var varName = $"point{GetNextCounter("point")}";
        p.Name = varName;
        var style = GenerateStyleCode(p, varName);
        return $"var {varName} = new VPoint({FormatDouble(p.X)}, {FormatDouble(p.Y)});\n{style}{Indent}{varName}.Draw();";
    }

    private static string GenerateLineCode(VLine l)
    {
        var varName = $"line{GetNextCounter("line")}";
        l.Name = varName;
        var style = GenerateStyleCode(l, varName);
        return $"var {varName} = new VLine({FormatDouble(l.Start.X)}, {FormatDouble(l.Start.Y)}, {FormatDouble(l.End.X)}, {FormatDouble(l.End.Y)});\n{style}{Indent}{varName}.Draw();";
    }

    private static string GenerateCircleCode(VCircle c)
    {
        var varName = $"circle{GetNextCounter("circle")}";
        c.Name = varName;
        var style = GenerateStyleCode(c, varName);
        return $"var {varName} = new VCircle({FormatDouble(c.Center.X)}, {FormatDouble(c.Center.Y)}, {FormatDouble(c.Radius)});\n{style}{Indent}{varName}.Draw();";
    }

    private static string GenerateRectangleCode(VRectangle r)
    {
        var varName = $"rect{GetNextCounter("rect")}";
        r.Name = varName;
        var style = GenerateStyleCode(r, varName);
        return $"var {varName} = new VRectangle({FormatDouble(r.Corner.X)}, {FormatDouble(r.Corner.Y)}, {FormatDouble(r.Width)}, {FormatDouble(r.Height)});\n{style}{Indent}{varName}.Draw();";
    }

    private static string GenerateEllipseCode(VEllipse e)
    {
        var varName = $"ellipse{GetNextCounter("ellipse")}";
        e.Name = varName;
        var style = GenerateStyleCode(e, varName);
        return $"var {varName} = new VEllipse({FormatDouble(e.Center.X)}, {FormatDouble(e.Center.Y)}, {FormatDouble(e.RadiusX)}, {FormatDouble(e.RadiusY)});\n{style}{Indent}{varName}.Draw();";
    }

    private static string GenerateArcCode(VArc a)
    {
        var varName = $"arc{GetNextCounter("arc")}";
        a.Name = varName;
        var style = GenerateStyleCode(a, varName);
        return $"var {varName} = new VArc({FormatDouble(a.Center.X)}, {FormatDouble(a.Center.Y)}, {FormatDouble(a.Radius)}, {FormatDouble(a.StartAngle)}, {FormatDouble(a.EndAngle)});\n{style}{Indent}{varName}.Draw();";
    }

    private static string GeneratePolygonCode(VPolygon pg)
    {
        var varName = $"polygon{GetNextCounter("polygon")}";
        pg.Name = varName;
        var style = GenerateStyleCode(pg, varName);
        if (pg.Points.Count == 0)
            return $"var {varName} = new VPolygon();\n{style}{Indent}{varName}.Draw();";
        var points = string.Join(", ", pg.Points.Select(p => $"new VPoint({FormatDouble(p.X)}, {FormatDouble(p.Y)})"));
        return $"var {varName} = new VPolygon({points});\n{style}{Indent}{varName}.Draw();";
    }

    private static string GeneratePolylineCode(VPolyline pl)
    {
        var varName = $"polyline{GetNextCounter("polyline")}";
        pl.Name = varName;
        var style = GenerateStyleCode(pl, varName);
        if (pl.Points.Count == 0)
            return $"var {varName} = new VPolyline();\n{style}{Indent}{varName}.Draw();";
        var points = string.Join(", ", pl.Points.Select(p => $"new VPoint({FormatDouble(p.X)}, {FormatDouble(p.Y)})"));
        return $"var {varName} = new VPolyline({points});\n{style}{Indent}{varName}.Draw();";
    }

    private static string GenerateBezierCode(VBezier b)
    {
        var varName = $"bezier{GetNextCounter("bezier")}";
        b.Name = varName;
        var style = GenerateStyleCode(b, varName);
        return $"var {varName} = new VBezier({FormatDouble(b.P0.X)}, {FormatDouble(b.P0.Y)}, {FormatDouble(b.P1.X)}, {FormatDouble(b.P1.Y)}, {FormatDouble(b.P2.X)}, {FormatDouble(b.P2.Y)}, {FormatDouble(b.P3.X)}, {FormatDouble(b.P3.Y)});\n{style}{Indent}{varName}.Draw();";
    }

    private static string GenerateSplineCode(VSpline s)
    {
        var varName = $"spline{GetNextCounter("spline")}";
        s.Name = varName;
        var style = GenerateStyleCode(s, varName);
        if (s.ControlPoints.Count == 0)
            return $"var {varName} = new VSpline();\n{style}{Indent}{varName}.Draw();";
        var points = string.Join(", ", s.ControlPoints.Select(p => $"new VPoint({FormatDouble(p.X)}, {FormatDouble(p.Y)})"));
        return $"var {varName} = new VSpline({points});\n{style}{Indent}{varName}.Draw();";
    }

    private static string GenerateArrowCode(VArrow ar)
    {
        var varName = $"arrow{GetNextCounter("arrow")}";
        ar.Name = varName;
        var style = GenerateStyleCode(ar, varName);
        return $"var {varName} = new VArrow({FormatDouble(ar.Start.X)}, {FormatDouble(ar.Start.Y)}, {FormatDouble(ar.End.X)}, {FormatDouble(ar.End.Y)});\n{style}{Indent}{varName}.Draw();";
    }

    private static string GenerateTextCode(VText t)
    {
        var varName = $"text{GetNextCounter("text")}";
        t.Name = varName;
        var style = GenerateStyleCode(t, varName);
        var escapedContent = t.Content.Replace("\\", "\\\\").Replace("\"", "\\\"");
        return $"var {varName} = new VText({FormatDouble(t.Location.X)}, {FormatDouble(t.Location.Y)}, \"{escapedContent}\");\n{style}{Indent}{varName}.Draw();";
    }
}
