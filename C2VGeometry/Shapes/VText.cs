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
            Anchor = Anchor
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
        var bottomLeft = new VXYZ(Location.X + offsetX, Location.Y + offsetY);
        return new BoundingBox(bottomLeft, new VXYZ(bottomLeft.X + textWidth, bottomLeft.Y + Height));
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

    public override string ToString() => $"VText(\"{Content}\" at {Location})";
}
