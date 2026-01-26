using System.Globalization;
using System.Text.RegularExpressions;
using Code2Viz.Geometry;
using Code2Viz.Project;

namespace Code2Viz.Canvas;

/// <summary>
/// Generates C# and F# code strings for shapes created by the drawing tool.
/// </summary>
public static class CodeGenerator
{
    // Counters for sequential variable names
    private static readonly Dictionary<string, int> _shapeCounters = new();

    // Pattern to match variable declarations like "var line1", "let circle5", "VLine line2"
    private static readonly Regex _variablePattern = new(
        @"\b(?:var|let|VPoint|VLine|VCircle|VRectangle|VEllipse|VArc|VPolygon|VPolyline|VBezier|VSpline|VArrow|VText|VGrid|VGroup)\s+(point|line|circle|rect|ellipse|arc|polygon|polyline|bezier|spline|arrow|text|grid|group)(\d+)\b",
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
                // Update counter to be at least this number
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
    public static string GenerateConstructorCall(Shape shape, ProjectLanguage language = ProjectLanguage.CSharp)
    {
        var prefix = language == ProjectLanguage.FSharp ? "" : "new ";
        return shape switch
        {
            VPoint p => $"{prefix}VPoint({FormatDouble(p.X)}, {FormatDouble(p.Y)})",
            VLine l => $"{prefix}VLine({FormatDouble(l.Start.X)}, {FormatDouble(l.Start.Y)}, {FormatDouble(l.End.X)}, {FormatDouble(l.End.Y)})",
            VCircle c => $"{prefix}VCircle({FormatDouble(c.Center.X)}, {FormatDouble(c.Center.Y)}, {FormatDouble(c.Radius)})",
            VRectangle r => $"{prefix}VRectangle({FormatDouble(r.Corner.X)}, {FormatDouble(r.Corner.Y)}, {FormatDouble(r.Width)}, {FormatDouble(r.Height)})",
            VEllipse e => $"{prefix}VEllipse({FormatDouble(e.Center.X)}, {FormatDouble(e.Center.Y)}, {FormatDouble(e.RadiusX)}, {FormatDouble(e.RadiusY)})",
            VArc a => $"{prefix}VArc({FormatDouble(a.Center.X)}, {FormatDouble(a.Center.Y)}, {FormatDouble(a.Radius)}, {FormatDouble(a.StartAngle)}, {FormatDouble(a.EndAngle)})",
            VArrow ar => $"{prefix}VArrow({FormatDouble(ar.Start.X)}, {FormatDouble(ar.Start.Y)}, {FormatDouble(ar.End.X)}, {FormatDouble(ar.End.Y)})",
            VBezier b => $"{prefix}VBezier({FormatDouble(b.P0.X)}, {FormatDouble(b.P0.Y)}, {FormatDouble(b.P1.X)}, {FormatDouble(b.P1.Y)}, {FormatDouble(b.P2.X)}, {FormatDouble(b.P2.Y)}, {FormatDouble(b.P3.X)}, {FormatDouble(b.P3.Y)})",
            VText t => $"{prefix}VText({FormatDouble(t.Location.X)}, {FormatDouble(t.Location.Y)}, \"{t.Content.Replace("\\", "\\\\").Replace("\"", "\\\"")}\")",
            VPolygon pg => GeneratePolygonConstructor(pg, language),
            VPolyline pl => GeneratePolylineConstructor(pl, language),
            VSpline s => GenerateSplineConstructor(s, language),
            _ => $"/* Unknown shape: {shape.GetType().Name} */"
        };
    }

    private static string GeneratePolygonConstructor(VPolygon pg, ProjectLanguage language)
    {
        var prefix = language == ProjectLanguage.FSharp ? "" : "new ";
        if (pg.Points.Count == 0)
            return $"{prefix}VPolygon()";

        if (language == ProjectLanguage.FSharp)
        {
            var pointsCode = string.Join("; ", pg.Points.Select(p => $"VPoint({FormatDouble(p.X)}, {FormatDouble(p.Y)})"));
            return $"VPolygon([| {pointsCode} |])";
        }

        var csharpPointsCode = string.Join(", ", pg.Points.Select(p => $"new VPoint({FormatDouble(p.X)}, {FormatDouble(p.Y)})"));
        return $"new VPolygon({csharpPointsCode})";
    }

    private static string GeneratePolylineConstructor(VPolyline pl, ProjectLanguage language)
    {
        var prefix = language == ProjectLanguage.FSharp ? "" : "new ";
        if (pl.Points.Count == 0)
            return $"{prefix}VPolyline()";

        if (language == ProjectLanguage.FSharp)
        {
            var pointsCode = string.Join("; ", pl.Points.Select(p => $"VPoint({FormatDouble(p.X)}, {FormatDouble(p.Y)})"));
            return $"VPolyline([| {pointsCode} |])";
        }

        var csharpPointsCode = string.Join(", ", pl.Points.Select(p => $"new VPoint({FormatDouble(p.X)}, {FormatDouble(p.Y)})"));
        return $"new VPolyline({csharpPointsCode})";
    }

    private static string GenerateSplineConstructor(VSpline s, ProjectLanguage language)
    {
        var prefix = language == ProjectLanguage.FSharp ? "" : "new ";
        if (s.ControlPoints.Count == 0)
            return $"{prefix}VSpline()";

        if (language == ProjectLanguage.FSharp)
        {
            var pointsCode = string.Join("; ", s.ControlPoints.Select(p => $"VPoint({FormatDouble(p.X)}, {FormatDouble(p.Y)})"));
            return $"VSpline([| {pointsCode} |])";
        }

        var csharpPointsCode = string.Join(", ", s.ControlPoints.Select(p => $"new VPoint({FormatDouble(p.X)}, {FormatDouble(p.Y)})"));
        return $"new VSpline({csharpPointsCode})";
    }

    /// <summary>
    /// Generates code to create the given shape with a sequential variable name.
    /// </summary>
    /// <param name="shape">The shape to generate code for.</param>
    /// <param name="language">The target language (C# or F#).</param>
    public static string GenerateCode(Shape shape, ProjectLanguage language = ProjectLanguage.CSharp)
    {
        return shape switch
        {
            VPoint p => GeneratePointCode(p, language),
            VLine l => GenerateLineCode(l, language),
            VCircle c => GenerateCircleCode(c, language),
            VRectangle r => GenerateRectangleCode(r, language),
            VEllipse e => GenerateEllipseCode(e, language),
            VArc a => GenerateArcCode(a, language),
            VPolygon pg => GeneratePolygonCode(pg, language),
            VPolyline pl => GeneratePolylineCode(pl, language),
            VBezier b => GenerateBezierCode(b, language),
            VSpline s => GenerateSplineCode(s, language),
            VArrow ar => GenerateArrowCode(ar, language),
            VText t => GenerateTextCode(t, language),
            _ => language == ProjectLanguage.FSharp
                ? $"// Unknown shape: {shape.GetType().Name}"
                : $"// Unknown shape: {shape.GetType().Name}"
        };
    }

    private static string FormatDouble(double value)
    {
        // Format with 2 decimal places, using invariant culture
        return value.ToString("F2", CultureInfo.InvariantCulture);
    }

    // Indentation for the second line
    private static string GetIndent(ProjectLanguage language) =>
        language == ProjectLanguage.FSharp ? "        " : "            ";

    /// <summary>
    /// Generates style property assignments for a shape (Color, etc.).
    /// </summary>
    private static string GenerateStyleCode(Shape shape, string varName, string indent, ProjectLanguage language)
    {
        var lines = new List<string>();

        // Always include Color so the shape looks the same when code is re-run
        if (!string.IsNullOrEmpty(shape.Color))
        {
            if (language == ProjectLanguage.FSharp)
                lines.Add($"{varName}.Color <- \"{shape.Color}\"");
            else
                lines.Add($"{varName}.Color = \"{shape.Color}\";");
        }

        // Include FillColor if it's not Transparent (the default)
        if (!string.IsNullOrEmpty(shape.FillColor) &&
            !shape.FillColor.Equals("Transparent", StringComparison.OrdinalIgnoreCase))
        {
            if (language == ProjectLanguage.FSharp)
                lines.Add($"{varName}.FillColor <- \"{shape.FillColor}\"");
            else
                lines.Add($"{varName}.FillColor = \"{shape.FillColor}\";");
        }

        // Include LineWeight if it's not the default (2.0)
        if (Math.Abs(shape.LineWeight - 2.0) > 0.01)
        {
            if (language == ProjectLanguage.FSharp)
                lines.Add($"{varName}.LineWeight <- {FormatDouble(shape.LineWeight)}");
            else
                lines.Add($"{varName}.LineWeight = {FormatDouble(shape.LineWeight)};");
        }

        if (lines.Count == 0)
            return "";

        return indent + string.Join("\n" + indent, lines) + "\n";
    }

    private static string GeneratePointCode(VPoint p, ProjectLanguage language)
    {
        var varName = $"point{GetNextCounter("point")}";
        p.Name = varName; // Set Name for status bar display
        var indent = GetIndent(language);
        var style = GenerateStyleCode(p, varName, indent, language);

        if (language == ProjectLanguage.FSharp)
            return $"let {varName} = VPoint({FormatDouble(p.X)}, {FormatDouble(p.Y)})\n{style}{indent}{varName}.Draw()";

        return $"var {varName} = new VPoint({FormatDouble(p.X)}, {FormatDouble(p.Y)});\n{style}{indent}{varName}.Draw();";
    }

    private static string GenerateLineCode(VLine l, ProjectLanguage language)
    {
        var varName = $"line{GetNextCounter("line")}";
        l.Name = varName; // Set Name for status bar display
        var indent = GetIndent(language);
        var style = GenerateStyleCode(l, varName, indent, language);

        if (language == ProjectLanguage.FSharp)
            return $"let {varName} = VLine({FormatDouble(l.Start.X)}, {FormatDouble(l.Start.Y)}, {FormatDouble(l.End.X)}, {FormatDouble(l.End.Y)})\n{style}{indent}{varName}.Draw()";

        return $"var {varName} = new VLine({FormatDouble(l.Start.X)}, {FormatDouble(l.Start.Y)}, {FormatDouble(l.End.X)}, {FormatDouble(l.End.Y)});\n{style}{indent}{varName}.Draw();";
    }

    private static string GenerateCircleCode(VCircle c, ProjectLanguage language)
    {
        var varName = $"circle{GetNextCounter("circle")}";
        c.Name = varName; // Set Name for status bar display
        var indent = GetIndent(language);
        var style = GenerateStyleCode(c, varName, indent, language);

        if (language == ProjectLanguage.FSharp)
            return $"let {varName} = VCircle({FormatDouble(c.Center.X)}, {FormatDouble(c.Center.Y)}, {FormatDouble(c.Radius)})\n{style}{indent}{varName}.Draw()";

        return $"var {varName} = new VCircle({FormatDouble(c.Center.X)}, {FormatDouble(c.Center.Y)}, {FormatDouble(c.Radius)});\n{style}{indent}{varName}.Draw();";
    }

    private static string GenerateRectangleCode(VRectangle r, ProjectLanguage language)
    {
        var varName = $"rect{GetNextCounter("rect")}";
        r.Name = varName; // Set Name for status bar display
        var indent = GetIndent(language);
        var style = GenerateStyleCode(r, varName, indent, language);

        if (language == ProjectLanguage.FSharp)
            return $"let {varName} = VRectangle({FormatDouble(r.Corner.X)}, {FormatDouble(r.Corner.Y)}, {FormatDouble(r.Width)}, {FormatDouble(r.Height)})\n{style}{indent}{varName}.Draw()";

        return $"var {varName} = new VRectangle({FormatDouble(r.Corner.X)}, {FormatDouble(r.Corner.Y)}, {FormatDouble(r.Width)}, {FormatDouble(r.Height)});\n{style}{indent}{varName}.Draw();";
    }

    private static string GenerateEllipseCode(VEllipse e, ProjectLanguage language)
    {
        var varName = $"ellipse{GetNextCounter("ellipse")}";
        e.Name = varName; // Set Name for status bar display
        var indent = GetIndent(language);
        var style = GenerateStyleCode(e, varName, indent, language);

        if (language == ProjectLanguage.FSharp)
            return $"let {varName} = VEllipse({FormatDouble(e.Center.X)}, {FormatDouble(e.Center.Y)}, {FormatDouble(e.RadiusX)}, {FormatDouble(e.RadiusY)})\n{style}{indent}{varName}.Draw()";

        return $"var {varName} = new VEllipse({FormatDouble(e.Center.X)}, {FormatDouble(e.Center.Y)}, {FormatDouble(e.RadiusX)}, {FormatDouble(e.RadiusY)});\n{style}{indent}{varName}.Draw();";
    }

    private static string GenerateArcCode(VArc a, ProjectLanguage language)
    {
        var varName = $"arc{GetNextCounter("arc")}";
        a.Name = varName; // Set Name for status bar display
        var indent = GetIndent(language);
        var style = GenerateStyleCode(a, varName, indent, language);

        if (language == ProjectLanguage.FSharp)
            return $"let {varName} = VArc({FormatDouble(a.Center.X)}, {FormatDouble(a.Center.Y)}, {FormatDouble(a.Radius)}, {FormatDouble(a.StartAngle)}, {FormatDouble(a.EndAngle)})\n{style}{indent}{varName}.Draw()";

        return $"var {varName} = new VArc({FormatDouble(a.Center.X)}, {FormatDouble(a.Center.Y)}, {FormatDouble(a.Radius)}, {FormatDouble(a.StartAngle)}, {FormatDouble(a.EndAngle)});\n{style}{indent}{varName}.Draw();";
    }

    private static string GeneratePolygonCode(VPolygon pg, ProjectLanguage language)
    {
        var varName = $"polygon{GetNextCounter("polygon")}";
        pg.Name = varName; // Set Name for status bar display
        var indent = GetIndent(language);
        var style = GenerateStyleCode(pg, varName, indent, language);

        if (language == ProjectLanguage.FSharp)
        {
            if (pg.Points.Count == 0)
                return $"let {varName} = VPolygon()\n{style}{indent}{varName}.Draw()";

            var pointsCode = string.Join("; ", pg.Points.Select(p => $"VPoint({FormatDouble(p.X)}, {FormatDouble(p.Y)})"));
            return $"let {varName} = VPolygon([| {pointsCode} |])\n{style}{indent}{varName}.Draw()";
        }

        if (pg.Points.Count == 0)
            return $"var {varName} = new VPolygon();\n{style}{indent}{varName}.Draw();";

        var csharpPointsCode = string.Join(", ", pg.Points.Select(p => $"new VPoint({FormatDouble(p.X)}, {FormatDouble(p.Y)})"));
        return $"var {varName} = new VPolygon({csharpPointsCode});\n{style}{indent}{varName}.Draw();";
    }

    private static string GeneratePolylineCode(VPolyline pl, ProjectLanguage language)
    {
        var varName = $"polyline{GetNextCounter("polyline")}";
        pl.Name = varName; // Set Name for status bar display
        var indent = GetIndent(language);
        var style = GenerateStyleCode(pl, varName, indent, language);

        if (language == ProjectLanguage.FSharp)
        {
            if (pl.Points.Count == 0)
                return $"let {varName} = VPolyline()\n{style}{indent}{varName}.Draw()";

            var pointsCode = string.Join("; ", pl.Points.Select(p => $"VPoint({FormatDouble(p.X)}, {FormatDouble(p.Y)})"));
            return $"let {varName} = VPolyline([| {pointsCode} |])\n{style}{indent}{varName}.Draw()";
        }

        if (pl.Points.Count == 0)
            return $"var {varName} = new VPolyline();\n{style}{indent}{varName}.Draw();";

        var csharpPointsCode = string.Join(", ", pl.Points.Select(p => $"new VPoint({FormatDouble(p.X)}, {FormatDouble(p.Y)})"));
        return $"var {varName} = new VPolyline({csharpPointsCode});\n{style}{indent}{varName}.Draw();";
    }

    private static string GenerateBezierCode(VBezier b, ProjectLanguage language)
    {
        var varName = $"bezier{GetNextCounter("bezier")}";
        b.Name = varName; // Set Name for status bar display
        var indent = GetIndent(language);
        var style = GenerateStyleCode(b, varName, indent, language);

        if (language == ProjectLanguage.FSharp)
            return $"let {varName} = VBezier({FormatDouble(b.P0.X)}, {FormatDouble(b.P0.Y)}, {FormatDouble(b.P1.X)}, {FormatDouble(b.P1.Y)}, {FormatDouble(b.P2.X)}, {FormatDouble(b.P2.Y)}, {FormatDouble(b.P3.X)}, {FormatDouble(b.P3.Y)})\n{style}{indent}{varName}.Draw()";

        return $"var {varName} = new VBezier({FormatDouble(b.P0.X)}, {FormatDouble(b.P0.Y)}, {FormatDouble(b.P1.X)}, {FormatDouble(b.P1.Y)}, {FormatDouble(b.P2.X)}, {FormatDouble(b.P2.Y)}, {FormatDouble(b.P3.X)}, {FormatDouble(b.P3.Y)});\n{style}{indent}{varName}.Draw();";
    }

    private static string GenerateSplineCode(VSpline s, ProjectLanguage language)
    {
        var varName = $"spline{GetNextCounter("spline")}";
        s.Name = varName; // Set Name for status bar display
        var indent = GetIndent(language);
        var style = GenerateStyleCode(s, varName, indent, language);

        if (language == ProjectLanguage.FSharp)
        {
            if (s.ControlPoints.Count == 0)
                return $"let {varName} = VSpline()\n{style}{indent}{varName}.Draw()";

            var pointsCode = string.Join("; ", s.ControlPoints.Select(p => $"VPoint({FormatDouble(p.X)}, {FormatDouble(p.Y)})"));
            return $"let {varName} = VSpline([| {pointsCode} |])\n{style}{indent}{varName}.Draw()";
        }

        if (s.ControlPoints.Count == 0)
            return $"var {varName} = new VSpline();\n{style}{indent}{varName}.Draw();";

        var csharpPointsCode = string.Join(", ", s.ControlPoints.Select(p => $"new VPoint({FormatDouble(p.X)}, {FormatDouble(p.Y)})"));
        return $"var {varName} = new VSpline({csharpPointsCode});\n{style}{indent}{varName}.Draw();";
    }

    private static string GenerateArrowCode(VArrow ar, ProjectLanguage language)
    {
        var varName = $"arrow{GetNextCounter("arrow")}";
        ar.Name = varName; // Set Name for status bar display
        var indent = GetIndent(language);
        var style = GenerateStyleCode(ar, varName, indent, language);

        if (language == ProjectLanguage.FSharp)
            return $"let {varName} = VArrow({FormatDouble(ar.Start.X)}, {FormatDouble(ar.Start.Y)}, {FormatDouble(ar.End.X)}, {FormatDouble(ar.End.Y)})\n{style}{indent}{varName}.Draw()";

        return $"var {varName} = new VArrow({FormatDouble(ar.Start.X)}, {FormatDouble(ar.Start.Y)}, {FormatDouble(ar.End.X)}, {FormatDouble(ar.End.Y)});\n{style}{indent}{varName}.Draw();";
    }

    private static string GenerateTextCode(VText t, ProjectLanguage language)
    {
        var varName = $"text{GetNextCounter("text")}";
        t.Name = varName; // Set Name for status bar display
        var indent = GetIndent(language);
        var style = GenerateStyleCode(t, varName, indent, language);
        // Escape quotes in the text content
        var escapedContent = t.Content.Replace("\\", "\\\\").Replace("\"", "\\\"");

        if (language == ProjectLanguage.FSharp)
            return $"let {varName} = VText({FormatDouble(t.Location.X)}, {FormatDouble(t.Location.Y)}, \"{escapedContent}\")\n{style}{indent}{varName}.Draw()";

        return $"var {varName} = new VText({FormatDouble(t.Location.X)}, {FormatDouble(t.Location.Y)}, \"{escapedContent}\");\n{style}{indent}{varName}.Draw();";
    }
}
