using System.Globalization;
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

    /// <summary>
    /// Resets all shape counters (call when starting a new project or clearing the canvas).
    /// </summary>
    public static void ResetCounters()
    {
        _shapeCounters.Clear();
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

    private static string GeneratePointCode(VPoint p, ProjectLanguage language)
    {
        var varName = $"point{GetNextCounter("point")}";
        var indent = GetIndent(language);

        if (language == ProjectLanguage.FSharp)
            return $"let {varName} = VPoint({FormatDouble(p.X)}, {FormatDouble(p.Y)})\n{indent}{varName}.Draw()";

        return $"var {varName} = new VPoint({FormatDouble(p.X)}, {FormatDouble(p.Y)});\n{indent}{varName}.Draw();";
    }

    private static string GenerateLineCode(VLine l, ProjectLanguage language)
    {
        var varName = $"line{GetNextCounter("line")}";
        var indent = GetIndent(language);

        if (language == ProjectLanguage.FSharp)
            return $"let {varName} = VLine({FormatDouble(l.Start.X)}, {FormatDouble(l.Start.Y)}, {FormatDouble(l.End.X)}, {FormatDouble(l.End.Y)})\n{indent}{varName}.Draw()";

        return $"var {varName} = new VLine({FormatDouble(l.Start.X)}, {FormatDouble(l.Start.Y)}, {FormatDouble(l.End.X)}, {FormatDouble(l.End.Y)});\n{indent}{varName}.Draw();";
    }

    private static string GenerateCircleCode(VCircle c, ProjectLanguage language)
    {
        var varName = $"circle{GetNextCounter("circle")}";
        var indent = GetIndent(language);

        if (language == ProjectLanguage.FSharp)
            return $"let {varName} = VCircle({FormatDouble(c.Center.X)}, {FormatDouble(c.Center.Y)}, {FormatDouble(c.Radius)})\n{indent}{varName}.Draw()";

        return $"var {varName} = new VCircle({FormatDouble(c.Center.X)}, {FormatDouble(c.Center.Y)}, {FormatDouble(c.Radius)});\n{indent}{varName}.Draw();";
    }

    private static string GenerateRectangleCode(VRectangle r, ProjectLanguage language)
    {
        var varName = $"rect{GetNextCounter("rect")}";
        var indent = GetIndent(language);

        if (language == ProjectLanguage.FSharp)
            return $"let {varName} = VRectangle({FormatDouble(r.Corner.X)}, {FormatDouble(r.Corner.Y)}, {FormatDouble(r.Width)}, {FormatDouble(r.Height)})\n{indent}{varName}.Draw()";

        return $"var {varName} = new VRectangle({FormatDouble(r.Corner.X)}, {FormatDouble(r.Corner.Y)}, {FormatDouble(r.Width)}, {FormatDouble(r.Height)});\n{indent}{varName}.Draw();";
    }

    private static string GenerateEllipseCode(VEllipse e, ProjectLanguage language)
    {
        var varName = $"ellipse{GetNextCounter("ellipse")}";
        var indent = GetIndent(language);

        if (language == ProjectLanguage.FSharp)
            return $"let {varName} = VEllipse({FormatDouble(e.Center.X)}, {FormatDouble(e.Center.Y)}, {FormatDouble(e.RadiusX)}, {FormatDouble(e.RadiusY)})\n{indent}{varName}.Draw()";

        return $"var {varName} = new VEllipse({FormatDouble(e.Center.X)}, {FormatDouble(e.Center.Y)}, {FormatDouble(e.RadiusX)}, {FormatDouble(e.RadiusY)});\n{indent}{varName}.Draw();";
    }

    private static string GenerateArcCode(VArc a, ProjectLanguage language)
    {
        var varName = $"arc{GetNextCounter("arc")}";
        var indent = GetIndent(language);

        if (language == ProjectLanguage.FSharp)
            return $"let {varName} = VArc({FormatDouble(a.Center.X)}, {FormatDouble(a.Center.Y)}, {FormatDouble(a.Radius)}, {FormatDouble(a.StartAngle)}, {FormatDouble(a.EndAngle)})\n{indent}{varName}.Draw()";

        return $"var {varName} = new VArc({FormatDouble(a.Center.X)}, {FormatDouble(a.Center.Y)}, {FormatDouble(a.Radius)}, {FormatDouble(a.StartAngle)}, {FormatDouble(a.EndAngle)});\n{indent}{varName}.Draw();";
    }

    private static string GeneratePolygonCode(VPolygon pg, ProjectLanguage language)
    {
        var varName = $"polygon{GetNextCounter("polygon")}";
        var indent = GetIndent(language);

        if (language == ProjectLanguage.FSharp)
        {
            if (pg.Points.Count == 0)
                return $"let {varName} = VPolygon()\n{indent}{varName}.Draw()";

            var pointsCode = string.Join("; ", pg.Points.Select(p => $"VPoint({FormatDouble(p.X)}, {FormatDouble(p.Y)})"));
            return $"let {varName} = VPolygon([| {pointsCode} |])\n{indent}{varName}.Draw()";
        }

        if (pg.Points.Count == 0)
            return $"var {varName} = new VPolygon();\n{indent}{varName}.Draw();";

        var csharpPointsCode = string.Join(", ", pg.Points.Select(p => $"new VPoint({FormatDouble(p.X)}, {FormatDouble(p.Y)})"));
        return $"var {varName} = new VPolygon({csharpPointsCode});\n{indent}{varName}.Draw();";
    }

    private static string GeneratePolylineCode(VPolyline pl, ProjectLanguage language)
    {
        var varName = $"polyline{GetNextCounter("polyline")}";
        var indent = GetIndent(language);

        if (language == ProjectLanguage.FSharp)
        {
            if (pl.Points.Count == 0)
                return $"let {varName} = VPolyline()\n{indent}{varName}.Draw()";

            var pointsCode = string.Join("; ", pl.Points.Select(p => $"VPoint({FormatDouble(p.X)}, {FormatDouble(p.Y)})"));
            return $"let {varName} = VPolyline([| {pointsCode} |])\n{indent}{varName}.Draw()";
        }

        if (pl.Points.Count == 0)
            return $"var {varName} = new VPolyline();\n{indent}{varName}.Draw();";

        var csharpPointsCode = string.Join(", ", pl.Points.Select(p => $"new VPoint({FormatDouble(p.X)}, {FormatDouble(p.Y)})"));
        return $"var {varName} = new VPolyline({csharpPointsCode});\n{indent}{varName}.Draw();";
    }

    private static string GenerateBezierCode(VBezier b, ProjectLanguage language)
    {
        var varName = $"bezier{GetNextCounter("bezier")}";
        var indent = GetIndent(language);

        if (language == ProjectLanguage.FSharp)
            return $"let {varName} = VBezier({FormatDouble(b.P0.X)}, {FormatDouble(b.P0.Y)}, {FormatDouble(b.P1.X)}, {FormatDouble(b.P1.Y)}, {FormatDouble(b.P2.X)}, {FormatDouble(b.P2.Y)}, {FormatDouble(b.P3.X)}, {FormatDouble(b.P3.Y)})\n{indent}{varName}.Draw()";

        return $"var {varName} = new VBezier({FormatDouble(b.P0.X)}, {FormatDouble(b.P0.Y)}, {FormatDouble(b.P1.X)}, {FormatDouble(b.P1.Y)}, {FormatDouble(b.P2.X)}, {FormatDouble(b.P2.Y)}, {FormatDouble(b.P3.X)}, {FormatDouble(b.P3.Y)});\n{indent}{varName}.Draw();";
    }

    private static string GenerateSplineCode(VSpline s, ProjectLanguage language)
    {
        var varName = $"spline{GetNextCounter("spline")}";
        var indent = GetIndent(language);

        if (language == ProjectLanguage.FSharp)
        {
            if (s.ControlPoints.Count == 0)
                return $"let {varName} = VSpline()\n{indent}{varName}.Draw()";

            var pointsCode = string.Join("; ", s.ControlPoints.Select(p => $"VPoint({FormatDouble(p.X)}, {FormatDouble(p.Y)})"));
            return $"let {varName} = VSpline([| {pointsCode} |])\n{indent}{varName}.Draw()";
        }

        if (s.ControlPoints.Count == 0)
            return $"var {varName} = new VSpline();\n{indent}{varName}.Draw();";

        var csharpPointsCode = string.Join(", ", s.ControlPoints.Select(p => $"new VPoint({FormatDouble(p.X)}, {FormatDouble(p.Y)})"));
        return $"var {varName} = new VSpline({csharpPointsCode});\n{indent}{varName}.Draw();";
    }

    private static string GenerateArrowCode(VArrow ar, ProjectLanguage language)
    {
        var varName = $"arrow{GetNextCounter("arrow")}";
        var indent = GetIndent(language);

        if (language == ProjectLanguage.FSharp)
            return $"let {varName} = VArrow({FormatDouble(ar.Start.X)}, {FormatDouble(ar.Start.Y)}, {FormatDouble(ar.End.X)}, {FormatDouble(ar.End.Y)})\n{indent}{varName}.Draw()";

        return $"var {varName} = new VArrow({FormatDouble(ar.Start.X)}, {FormatDouble(ar.Start.Y)}, {FormatDouble(ar.End.X)}, {FormatDouble(ar.End.Y)});\n{indent}{varName}.Draw();";
    }

    private static string GenerateTextCode(VText t, ProjectLanguage language)
    {
        var varName = $"text{GetNextCounter("text")}";
        var indent = GetIndent(language);
        // Escape quotes in the text content
        var escapedContent = t.Content.Replace("\\", "\\\\").Replace("\"", "\\\"");

        if (language == ProjectLanguage.FSharp)
            return $"let {varName} = VText({FormatDouble(t.Location.X)}, {FormatDouble(t.Location.Y)}, \"{escapedContent}\")\n{indent}{varName}.Draw()";

        return $"var {varName} = new VText({FormatDouble(t.Location.X)}, {FormatDouble(t.Location.Y)}, \"{escapedContent}\");\n{indent}{varName}.Draw();";
    }
}
