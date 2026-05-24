using System;
using System.Collections.Generic;

namespace C2VGeometry;

/// <summary>
/// Available font families for text rendering.
/// </summary>
public enum VFont
{
    /// <summary>Arial - clean sans-serif font (default).</summary>
    Arial,
    /// <summary>Times New Roman - classic serif font.</summary>
    TimesNewRoman,
    /// <summary>Courier New - monospace font.</summary>
    CourierNew,
    /// <summary>Verdana - wide sans-serif font.</summary>
    Verdana,
    /// <summary>Georgia - elegant serif font.</summary>
    Georgia,
    /// <summary>Tahoma - compact sans-serif font.</summary>
    Tahoma,
    /// <summary>Trebuchet MS - humanist sans-serif font.</summary>
    TrebuchetMS,
    /// <summary>Consolas - modern monospace font.</summary>
    Consolas,
    /// <summary>Calibri - default Office font.</summary>
    Calibri,
    /// <summary>Cambria - serif font for body text.</summary>
    Cambria,
    /// <summary>Segoe UI - Windows system font.</summary>
    SegoeUI,
    /// <summary>Comic Sans MS - casual script font.</summary>
    ComicSansMS,
    /// <summary>Impact - bold display font.</summary>
    Impact,
    /// <summary>Lucida Console - monospace font.</summary>
    LucidaConsole
}

/// <summary>
/// Font weight for text rendering.
/// </summary>
public enum VFontWeight
{
    /// <summary>Normal weight (400).</summary>
    Normal,
    /// <summary>Bold weight (700).</summary>
    Bold
}

public class VText : Shape
{
    public VXYZ Location { get; set; }
    public string Content { get; set; }
    public double Height { get; set; } = 12;
    public double Width { get; set; } = 0; // 0 = auto (measured)
    public VFont Font { get; set; } = VFont.Arial;
    public VFontWeight FontWeight { get; set; } = VFontWeight.Normal;
    public VTextAnchor Anchor { get; set; } = VTextAnchor.BottomLeft;
    /// <summary>
    /// Rotation of the text block in degrees, counterclockwise around <see cref="Location"/>.
    /// Characters rotate with the block (Excel-style). 0 = horizontal, 90 = reads bottom-to-top.
    /// </summary>
    public double Angle { get; set; } = 0;

    public VText(VXYZ location, string content)
    {
        Location = new VXYZ(location.X, location.Y);
        Content = content;
        Color = "White";
        FillColor = "Transparent";
    }

    public VText(VXYZ location, string content, double height)
    {
        Location = new VXYZ(location.X, location.Y);
        Content = content;
        Height = height;
        Color = "White";
        FillColor = "Transparent";
    }

    public VText(double x, double y, string content)
    {
        Location = new VXYZ(x, y);
        Content = content;
        Color = "White";
        FillColor = "Transparent";
    }

    public VText(double x, double y, string content, double height)
    {
        Location = new VXYZ(x, y);
        Content = content;
        Height = height;
        Color = "White";
        FillColor = "Transparent";
    }



    public override List<ControlPoint> GetControlPoints()
    {
        return new List<ControlPoint>
        {
            new ControlPoint(ControlPointType.Move, Location.X, Location.Y, "Location")
        };
    }

    public override void MoveControlPoint(int index, VXYZ newPosition)
    {
        if (index == 0)
        {
            Location = new VXYZ(newPosition.X, newPosition.Y);
        }
    }

    public override VText Clone()
    {
        var clone = new VText(Location.Clone(), Content)
        {
            Height = Height,
            Width = Width,
            Font = Font,
            FontWeight = FontWeight,
            Anchor = Anchor,
            Angle = Angle
        };
        CopyStyleTo(clone);
        return clone;
    }

    public override void Move(VXYZ vector)
    {
        Location = Location + vector;
    }

    public override void Rotate(VXYZ pivot, double angleDegrees)
    {
        Location = GeometryHelper.RotatePoint(Location, pivot, angleDegrees);
    }

    public override void Flip(VLine mirrorLine)
    {
        Location = GeometryHelper.FlipPoint(Location, mirrorLine);
    }

    public override void Scale(VXYZ center, double factor)
    {
        Location = GeometryHelper.ScalePoint(Location, center, factor);
        Height *= Math.Abs(factor);
        if (Width > 0)
            Width *= Math.Abs(factor);
    }

    public override BoundingBox GetBounds()
    {
        var textWidth = Width > 0 ? Width : Height * Content.Length * 0.6;
        var (offsetX, offsetY) = GetAnchorOffset(textWidth, Height);

        if (Angle == 0)
        {
            var bottomLeft = new VXYZ(Location.X + offsetX, Location.Y + offsetY);
            return new BoundingBox(bottomLeft, new VXYZ(bottomLeft.X + textWidth, bottomLeft.Y + Height));
        }

        var rad = Angle * Math.PI / 180.0;
        var cos = Math.Cos(rad);
        var sin = Math.Sin(rad);
        double rx0 = offsetX, ry0 = offsetY;
        double rx1 = offsetX + textWidth, ry1 = offsetY;
        double rx2 = offsetX + textWidth, ry2 = offsetY + Height;
        double rx3 = offsetX, ry3 = offsetY + Height;

        VXYZ Rotate(double rx, double ry) =>
            new VXYZ(Location.X + rx * cos - ry * sin, Location.Y + rx * sin + ry * cos);

        var p0 = Rotate(rx0, ry0);
        var p1 = Rotate(rx1, ry1);
        var p2 = Rotate(rx2, ry2);
        var p3 = Rotate(rx3, ry3);

        var minX = Math.Min(Math.Min(p0.X, p1.X), Math.Min(p2.X, p3.X));
        var maxX = Math.Max(Math.Max(p0.X, p1.X), Math.Max(p2.X, p3.X));
        var minY = Math.Min(Math.Min(p0.Y, p1.Y), Math.Min(p2.Y, p3.Y));
        var maxY = Math.Max(Math.Max(p0.Y, p1.Y), Math.Max(p2.Y, p3.Y));
        return new BoundingBox(new VXYZ(minX, minY), new VXYZ(maxX, maxY));
    }

    internal (double offsetX, double offsetY) GetAnchorOffset(double textWidth, double textHeight)
    {
        double offsetX = Anchor switch
        {
            VTextAnchor.BottomLeft or VTextAnchor.MiddleLeft or VTextAnchor.TopLeft => 0,
            VTextAnchor.BottomCenter or VTextAnchor.MiddleCenter or VTextAnchor.TopCenter => -textWidth / 2,
            _ => -textWidth
        };
        double offsetY = Anchor switch
        {
            VTextAnchor.BottomLeft or VTextAnchor.BottomCenter or VTextAnchor.BottomRight => 0,
            VTextAnchor.MiddleLeft or VTextAnchor.MiddleCenter or VTextAnchor.MiddleRight => -textHeight / 2,
            _ => -textHeight
        };
        return (offsetX, offsetY);
    }

    /// <summary>
    /// Returns true if the text's (possibly rotated) bounding quad overlaps the other shape's
    /// bounding box. Symmetric for axis-aligned text; uses an OBB-vs-AABB SAT test when rotated.
    /// </summary>
    public override bool DoesIntersect(Shape other)
    {
        if (other == null) return false;

        GetCornerCoords(out var ax, out var ay);
        var b = other.GetBounds();
        var bx = new[] { b.Min.X, b.Max.X, b.Max.X, b.Min.X };
        var by = new[] { b.Min.Y, b.Min.Y, b.Max.Y, b.Max.Y };

        return ConvexQuadsOverlap(ax, ay, bx, by);
    }

    private void GetCornerCoords(out double[] xs, out double[] ys)
    {
        var textWidth = Width > 0 ? Width : Height * Content.Length * 0.6;
        var (offsetX, offsetY) = GetAnchorOffset(textWidth, Height);

        if (Angle == 0)
        {
            xs = new[] { Location.X + offsetX, Location.X + offsetX + textWidth, Location.X + offsetX + textWidth, Location.X + offsetX };
            ys = new[] { Location.Y + offsetY, Location.Y + offsetY, Location.Y + offsetY + Height, Location.Y + offsetY + Height };
            return;
        }

        var rad = Angle * Math.PI / 180.0;
        var cos = Math.Cos(rad);
        var sin = Math.Sin(rad);
        double rx0 = offsetX, ry0 = offsetY;
        double rx1 = offsetX + textWidth, ry1 = offsetY;
        double rx2 = offsetX + textWidth, ry2 = offsetY + Height;
        double rx3 = offsetX, ry3 = offsetY + Height;
        xs = new[]
        {
            Location.X + rx0 * cos - ry0 * sin,
            Location.X + rx1 * cos - ry1 * sin,
            Location.X + rx2 * cos - ry2 * sin,
            Location.X + rx3 * cos - ry3 * sin,
        };
        ys = new[]
        {
            Location.Y + rx0 * sin + ry0 * cos,
            Location.Y + rx1 * sin + ry1 * cos,
            Location.Y + rx2 * sin + ry2 * cos,
            Location.Y + rx3 * sin + ry3 * cos,
        };
    }

    private static bool ConvexQuadsOverlap(double[] ax, double[] ay, double[] bx, double[] by)
    {
        const double eps = 1e-9;
        for (int side = 0; side < 2; side++)
        {
            var qx = side == 0 ? ax : bx;
            var qy = side == 0 ? ay : by;
            for (int i = 0; i < 4; i++)
            {
                int j = (i + 1) & 3;
                double axisX = -(qy[j] - qy[i]);
                double axisY = qx[j] - qx[i];
                double len = Math.Sqrt(axisX * axisX + axisY * axisY);
                if (len < 1e-12) continue;
                axisX /= len; axisY /= len;

                Project(ax, ay, axisX, axisY, out var minA, out var maxA);
                Project(bx, by, axisX, axisY, out var minB, out var maxB);
                if (maxA < minB - eps || maxB < minA - eps) return false;
            }
        }
        return true;
    }

    private static void Project(double[] xs, double[] ys, double axisX, double axisY, out double min, out double max)
    {
        min = double.PositiveInfinity;
        max = double.NegativeInfinity;
        for (int i = 0; i < 4; i++)
        {
            double d = xs[i] * axisX + ys[i] * axisY;
            if (d < min) min = d;
            if (d > max) max = d;
        }
    }

    public override string ToString() => $"VText(\"{Content}\" at {Location})";
}
