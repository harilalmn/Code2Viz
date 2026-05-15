using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Code2Viz.Canvas;
using Code2Viz.Geometry;

namespace Code2Viz.Export;

/// <summary>
/// Exports shapes to AutoCAD DXF format (R12 ASCII).
/// </summary>
public class DxfExporter
{
    private readonly StringBuilder _sb = new();
    private int _handleCounter = 1;

    /// <summary>
    /// Exports shapes to a DXF file.
    /// </summary>
    public void Export(IReadOnlyList<IDrawable> shapes, string filePath)
    {
        _sb.Clear();
        _handleCounter = 1;

        WriteHeader();
        WriteTables();
        WriteBlocks();
        WriteEntities(shapes);
        WriteObjects();
        WriteEof();

        File.WriteAllText(filePath, _sb.ToString(), Encoding.ASCII);
    }

    /// <summary>
    /// Exports shapes to a DXF string.
    /// </summary>
    public string ExportToString(IReadOnlyList<IDrawable> shapes)
    {
        _sb.Clear();
        _handleCounter = 1;

        WriteHeader();
        WriteTables();
        WriteBlocks();
        WriteEntities(shapes);
        WriteObjects();
        WriteEof();

        return _sb.ToString();
    }

    private void WriteHeader()
    {
        WriteLine(0, "SECTION");
        WriteLine(2, "HEADER");
        WriteLine(9, "$ACADVER");
        WriteLine(1, "AC1009"); // R12 format
        WriteLine(9, "$INSUNITS");
        WriteLine(70, "0"); // Unitless
        WriteLine(0, "ENDSEC");
    }

    private void WriteTables()
    {
        WriteLine(0, "SECTION");
        WriteLine(2, "TABLES");

        // Layer table
        WriteLine(0, "TABLE");
        WriteLine(2, "LAYER");
        WriteLine(70, "1");
        WriteLine(0, "LAYER");
        WriteLine(2, "0");
        WriteLine(70, "0");
        WriteLine(62, "7"); // White color
        WriteLine(6, "CONTINUOUS");
        WriteLine(0, "ENDTAB");

        WriteLine(0, "ENDSEC");
    }

    private void WriteBlocks()
    {
        WriteLine(0, "SECTION");
        WriteLine(2, "BLOCKS");
        WriteLine(0, "ENDSEC");
    }

    private void WriteEntities(IReadOnlyList<IDrawable> shapes)
    {
        WriteLine(0, "SECTION");
        WriteLine(2, "ENTITIES");

        foreach (var drawable in shapes)
        {
            if (drawable is Shape shape)
            {
                WriteShape(shape);
            }
        }

        WriteLine(0, "ENDSEC");
    }

    private void WriteObjects()
    {
        WriteLine(0, "SECTION");
        WriteLine(2, "OBJECTS");
        WriteLine(0, "ENDSEC");
    }

    private void WriteEof()
    {
        WriteLine(0, "EOF");
    }

    private void WriteShape(Shape shape)
    {
        switch (shape)
        {
            case VPoint point:
                WritePoint(point);
                break;
            case VLine line:
                WriteLine(line);
                break;
            case VCircle circle:
                WriteCircle(circle);
                break;
            case VArc arc:
                WriteArc(arc);
                break;
            case VEllipse ellipse:
                WriteEllipse(ellipse);
                break;
            case VRectangle rect:
                WriteRectangle(rect);
                break;
            case VPolygon polygon:
                WritePolygon(polygon);
                break;
            case VPolyline polyline:
                WritePolyline(polyline);
                break;
            case VBezier bezier:
                WriteBezier(bezier);
                break;
            case VSpline spline:
                WriteSpline(spline);
                break;
            case VArrow arrow:
                WriteArrow(arrow);
                break;
            case VText text:
                WriteText(text);
                break;
        }
    }

    private void WritePoint(VPoint point)
    {
        WriteLine(0, "POINT");
        WriteHandle();
        WriteLayer();
        WriteCoord(10, 20, 30, point.X, point.Y, 0);
    }

    private void WriteLine(VLine line)
    {
        _sb.AppendLine("0");
        _sb.AppendLine("LINE");
        WriteHandle();
        WriteLayer();
        WriteCoord(10, 20, 30, line.Start.X, line.Start.Y, 0);
        WriteCoord(11, 21, 31, line.End.X, line.End.Y, 0);
    }

    private void WriteCircle(VCircle circle)
    {
        WriteLine(0, "CIRCLE");
        WriteHandle();
        WriteLayer();
        WriteCoord(10, 20, 30, circle.Center.X, circle.Center.Y, 0);
        WriteDouble(40, circle.Radius);
    }

    private void WriteArc(VArc arc)
    {
        WriteLine(0, "ARC");
        WriteHandle();
        WriteLayer();
        WriteCoord(10, 20, 30, arc.Center.X, arc.Center.Y, 0);
        WriteDouble(40, arc.Radius);
        WriteDouble(50, arc.StartAngle);
        WriteDouble(51, arc.EndAngle);
    }

    private void WriteEllipse(VEllipse ellipse)
    {
        // DXF R12 doesn't have native ELLIPSE support
        // Approximate with polyline
        var points = new List<(double x, double y)>();
        int segments = 72;
        for (int i = 0; i <= segments; i++)
        {
            double angle = 2 * Math.PI * i / segments;
            double x = ellipse.Center.X + ellipse.RadiusX * Math.Cos(angle);
            double y = ellipse.Center.Y + ellipse.RadiusY * Math.Sin(angle);
            points.Add((x, y));
        }
        WriteLwPolyline(points, closed: true);
    }

    private void WriteRectangle(VRectangle rect)
    {
        var points = new List<(double x, double y)>
        {
            (rect.Corner.X, rect.Corner.Y),
            (rect.Corner.X + rect.Width, rect.Corner.Y),
            (rect.Corner.X + rect.Width, rect.Corner.Y + rect.Height),
            (rect.Corner.X, rect.Corner.Y + rect.Height)
        };
        WriteLwPolyline(points, closed: true);
    }

    private void WritePolygon(VPolygon polygon)
    {
        var points = new List<(double x, double y)>();
        foreach (var vertex in polygon.Points)
        {
            points.Add((vertex.X, vertex.Y));
        }
        if (points.Count > 0)
        {
            WriteLwPolyline(points, closed: true);
        }
    }

    private void WritePolyline(VPolyline polyline)
    {
        var points = new List<(double x, double y)>();
        foreach (var vertex in polyline.Points)
        {
            points.Add((vertex.X, vertex.Y));
        }
        if (points.Count > 0)
        {
            WriteLwPolyline(points, closed: false);
        }
    }

    private void WriteBezier(VBezier bezier)
    {
        // Approximate bezier with polyline
        var points = new List<(double x, double y)>();
        int segments = 32;
        for (int i = 0; i <= segments; i++)
        {
            double t = (double)i / segments;
            var pt = EvaluateBezier(bezier, t);
            points.Add((pt.x, pt.y));
        }
        WriteLwPolyline(points, closed: false);
    }

    private (double x, double y) EvaluateBezier(VBezier bezier, double t)
    {
        double u = 1 - t;
        double tt = t * t;
        double uu = u * u;
        double uuu = uu * u;
        double ttt = tt * t;

        double x = uuu * bezier.P0.X + 3 * uu * t * bezier.P1.X +
                   3 * u * tt * bezier.P2.X + ttt * bezier.P3.X;
        double y = uuu * bezier.P0.Y + 3 * uu * t * bezier.P1.Y +
                   3 * u * tt * bezier.P2.Y + ttt * bezier.P3.Y;

        return (x, y);
    }

    private void WriteSpline(VSpline spline)
    {
        // Export control points as polyline
        var points = new List<(double x, double y)>();
        foreach (var pt in spline.ControlPoints)
        {
            points.Add((pt.X, pt.Y));
        }
        if (points.Count > 0)
        {
            WriteLwPolyline(points, closed: false);
        }
    }

    private void WriteArrow(VArrow arrow)
    {
        // Write as line with arrowhead geometry
        _sb.AppendLine("0");
        _sb.AppendLine("LINE");
        WriteHandle();
        WriteLayer();
        WriteCoord(10, 20, 30, arrow.Start.X, arrow.Start.Y, 0);
        WriteCoord(11, 21, 31, arrow.End.X, arrow.End.Y, 0);

        // Calculate arrowhead
        double dx = arrow.End.X - arrow.Start.X;
        double dy = arrow.End.Y - arrow.Start.Y;
        double length = Math.Sqrt(dx * dx + dy * dy);
        if (length > 0)
        {
            double headSize = Math.Min(length * 0.2, 10);
            double angle = Math.Atan2(dy, dx);

            double x1 = arrow.End.X - headSize * Math.Cos(angle - Math.PI / 6);
            double y1 = arrow.End.Y - headSize * Math.Sin(angle - Math.PI / 6);
            double x2 = arrow.End.X - headSize * Math.Cos(angle + Math.PI / 6);
            double y2 = arrow.End.Y - headSize * Math.Sin(angle + Math.PI / 6);

            // First arrowhead line
            _sb.AppendLine("0");
            _sb.AppendLine("LINE");
            WriteHandle();
            WriteLayer();
            WriteCoord(10, 20, 30, arrow.End.X, arrow.End.Y, 0);
            WriteCoord(11, 21, 31, x1, y1, 0);

            // Second arrowhead line
            _sb.AppendLine("0");
            _sb.AppendLine("LINE");
            WriteHandle();
            WriteLayer();
            WriteCoord(10, 20, 30, arrow.End.X, arrow.End.Y, 0);
            WriteCoord(11, 21, 31, x2, y2, 0);
        }
    }

    private void WriteText(VText text)
    {
        WriteLine(0, "TEXT");
        WriteHandle();
        WriteLayer();
        WriteCoord(10, 20, 30, text.Location.X, text.Location.Y, 0);
        WriteDouble(40, text.Height);
        WriteLine(1, text.Content ?? "");
        if (text.Angle != 0)
            WriteDouble(50, text.Angle); // DXF text rotation is CCW degrees, same convention as world.
        WriteLine(7, "STANDARD");
    }

    private void WriteLwPolyline(List<(double x, double y)> points, bool closed)
    {
        // Use R12-compatible POLYLINE format (not LWPOLYLINE which requires R14+)
        WriteLine(0, "POLYLINE");
        WriteHandle();
        WriteLayer();
        WriteLine(66, "1"); // Vertices follow flag
        WriteLine(70, closed ? "1" : "0"); // Closed flag
        WriteCoord(10, 20, 30, 0, 0, 0); // Base point

        foreach (var (x, y) in points)
        {
            WriteLine(0, "VERTEX");
            WriteHandle();
            WriteLayer();
            WriteCoord(10, 20, 30, x, y, 0);
        }

        WriteLine(0, "SEQEND");
        WriteHandle();
        WriteLayer();
    }

    private void WriteHandle()
    {
        WriteLine(5, _handleCounter.ToString("X"));
        _handleCounter++;
    }

    private void WriteLayer()
    {
        WriteLine(8, "0");
    }

    private void WriteLine(int code, string value)
    {
        _sb.AppendLine(code.ToString());
        _sb.AppendLine(value);
    }

    private void WriteDouble(int code, double value)
    {
        _sb.AppendLine(code.ToString());
        _sb.AppendLine(value.ToString("F6", CultureInfo.InvariantCulture));
    }

    private void WriteCoord(int xCode, int yCode, int zCode, double x, double y, double z)
    {
        WriteDouble(xCode, x);
        WriteDouble(yCode, y);
        WriteDouble(zCode, z);
    }
}
