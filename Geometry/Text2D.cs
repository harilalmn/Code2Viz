using Code2Viz.Canvas;

namespace Code2Viz.Geometry;

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
    public VPoint Location { get; set; }
    public string Content { get; set; }
    public double Height { get; set; } = 12;
    public double Width { get; set; } = 0; // 0 = auto (measured)
    public VFont Font { get; set; } = VFont.Arial;
    public VFontWeight FontWeight { get; set; } = VFontWeight.Normal;

    public VText(VPoint location, string content)
    {
        Location = VPoint.Internal(location.X, location.Y);
        Content = content;
        Color = ShapeDefaults.GlobalColor ?? "White";
        FillColor = ShapeDefaults.GlobalFillColor ?? "Transparent";
    }

    public VText(VPoint location, string content, double height)
    {
        Location = VPoint.Internal(location.X, location.Y);
        Content = content;
        Height = height;
        Color = ShapeDefaults.GlobalColor ?? "White";
        FillColor = ShapeDefaults.GlobalFillColor ?? "Transparent";
    }

    public VText(double x, double y, string content)
    {
        Location = VPoint.Internal(x, y);
        Content = content;
        Color = ShapeDefaults.GlobalColor ?? "White";
        FillColor = ShapeDefaults.GlobalFillColor ?? "Transparent";
    }

    public VText(double x, double y, string content, double height)
    {
        Location = VPoint.Internal(x, y);
        Content = content;
        Height = height;
        Color = ShapeDefaults.GlobalColor ?? "White";
        FillColor = ShapeDefaults.GlobalFillColor ?? "Transparent";
    }



    public override List<ControlPoint> GetControlPoints()
    {
        return new List<ControlPoint>
        {
            new ControlPoint(ControlPointType.Move, Location.X, Location.Y, "Location")
        };
    }

    public override void MoveControlPoint(int index, VPoint newPosition)
    {
        if (index == 0)
        {
            Location.X = newPosition.X;
            Location.Y = newPosition.Y;
        }
    }

    public override Shape Clone()
    {
        var clone = new VText((VPoint)Location.Clone(), Content)
        {
            Height = Height,
            Width = Width,
            Font = Font,
            FontWeight = FontWeight
        };
        CopyStyleTo(clone);
        return clone;
    }

    public override void Move(VXYZ vector)
    {
        Location.Move(vector);
    }

    public override void Rotate(VPoint pivot, double angleDegrees)
    {
        Location.Rotate(pivot, angleDegrees);
    }

    public override void Flip(VLine mirrorLine)
    {
        Location.Flip(mirrorLine);
    }

    public override void Scale(VPoint center, double factor)
    {
        Location.Scale(center, factor);
        Height *= Math.Abs(factor);
        if (Width > 0)
            Width *= Math.Abs(factor);
    }

    public override BoundingBox GetBounds()
    {
        var textWidth = Width > 0 ? Width : Height * Content.Length * 0.6;
        return new BoundingBox(Location, VPoint.Internal(Location.X + textWidth, Location.Y + Height));
    }

    public override string ToString() => $"VText(\"{Content}\" at {Location})";
}

