using Code2Viz.Canvas;

namespace Code2Viz.Geometry;

public class VText : Shape
{
    public VPoint Location { get; set; }
    public string Content { get; set; }
    public double Height { get; set; } = 12;
    public double Width { get; set; } = 0; // 0 = auto (measured)

    /// <summary>
    /// Text color (alias for StrokeColor)
    /// </summary>
    public string Color
    {
        get => StrokeColor;
        set => StrokeColor = value;
    }

    public VText(VPoint location, string content)
    {
        Location = location;
        Content = content;
        StrokeColor = ShapeDefaults.GlobalStrokeColor ?? "White";
        FillColor = ShapeDefaults.GlobalFillColor ?? "Transparent";
    }

    public VText(double x, double y, string content)
    {
        Location = new VPoint(x, y);
        Content = content;
        StrokeColor = ShapeDefaults.GlobalStrokeColor ?? "White";
        FillColor = ShapeDefaults.GlobalFillColor ?? "Transparent";
    }

    public override void Draw()
    {
        CanvasRenderer.Instance.AddShape(this);
    }

    public override Shape Clone()
    {
        var clone = new VText((VPoint)Location.Clone(), Content) { Height = Height, Width = Width };
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

    public override (VPoint min, VPoint max) GetBounds()
    {
        var textWidth = Width > 0 ? Width : Height * Content.Length * 0.6;
        return (Location, new VPoint(Location.X + textWidth, Location.Y + Height));
    }

    public override string ToString() => $"VText(\"{Content}\" at {Location})";
}

