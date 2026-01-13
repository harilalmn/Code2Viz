using Code2Viz.Canvas;

namespace Code2Viz.Geometry;

/// <summary>
/// A dimension line showing the distance between two points with text annotation.
/// </summary>
public class VDimension : Shape
{
    public VPoint Point1 { get; set; }
    public VPoint Point2 { get; set; }

    /// <summary>Offset distance of the dimension line from the points</summary>
    public double Offset { get; set; } = 20;

    /// <summary>Length of the extension lines</summary>
    public double ExtensionLength { get; set; } = 10;

    /// <summary>Size of the arrowheads</summary>
    public double ArrowSize { get; set; } = 8;

    /// <summary>Custom text (if null, shows the calculated distance)</summary>
    public string? CustomText { get; set; }

    /// <summary>Number of decimal places for distance display</summary>
    public int DecimalPlaces { get; set; } = 2;

    /// <summary>Text height</summary>
    public double TextHeight { get; set; } = 12;

    public VDimension(VPoint point1, VPoint point2)
    {
        Point1 = point1;
        Point2 = point2;
        StrokeColor = ShapeDefaults.GlobalStrokeColor ?? "Yellow";
    }

    public VDimension(double x1, double y1, double x2, double y2)
    {
        Point1 = new VPoint(x1, y1);
        Point2 = new VPoint(x2, y2);
        StrokeColor = ShapeDefaults.GlobalStrokeColor ?? "Yellow";
    }

    /// <summary>
    /// Gets the distance between the two points.
    /// </summary>
    public double Distance
    {
        get
        {
            double dx = Point2.X - Point1.X;
            double dy = Point2.Y - Point1.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }
    }

    /// <summary>
    /// Gets the display text for the dimension.
    /// </summary>
    public string DisplayText => CustomText ?? Distance.ToString($"F{DecimalPlaces}");

    /// <summary>
    /// Gets the geometry for rendering the dimension.
    /// </summary>
    public (VPoint dimStart, VPoint dimEnd, VPoint textPos, VPoint ext1Start, VPoint ext1End, VPoint ext2Start, VPoint ext2End) GetDimensionGeometry()
    {
        double dx = Point2.X - Point1.X;
        double dy = Point2.Y - Point1.Y;
        double length = Math.Sqrt(dx * dx + dy * dy);

        if (length < 1e-10)
        {
            return (Point1, Point1, Point1, Point1, Point1, Point1, Point1);
        }

        // Perpendicular direction (offset direction)
        double perpX = -dy / length;
        double perpY = dx / length;

        // Dimension line endpoints
        var dimStart = new VPoint(Point1.X + perpX * Offset, Point1.Y + perpY * Offset);
        var dimEnd = new VPoint(Point2.X + perpX * Offset, Point2.Y + perpY * Offset);

        // Text position (center of dimension line)
        var textPos = new VPoint((dimStart.X + dimEnd.X) / 2, (dimStart.Y + dimEnd.Y) / 2);

        // Extension lines
        var ext1Start = new VPoint(Point1.X + perpX * (Offset - ExtensionLength), Point1.Y + perpY * (Offset - ExtensionLength));
        var ext1End = new VPoint(Point1.X + perpX * (Offset + ExtensionLength), Point1.Y + perpY * (Offset + ExtensionLength));
        var ext2Start = new VPoint(Point2.X + perpX * (Offset - ExtensionLength), Point2.Y + perpY * (Offset - ExtensionLength));
        var ext2End = new VPoint(Point2.X + perpX * (Offset + ExtensionLength), Point2.Y + perpY * (Offset + ExtensionLength));

        return (dimStart, dimEnd, textPos, ext1Start, ext1End, ext2Start, ext2End);
    }

    public override void Draw()
    {
        CanvasRenderer.Instance.AddShape(this);
    }

    public override Shape Clone()
    {
        var clone = new VDimension((VPoint)Point1.Clone(), (VPoint)Point2.Clone())
        {
            Offset = Offset,
            ExtensionLength = ExtensionLength,
            ArrowSize = ArrowSize,
            CustomText = CustomText,
            DecimalPlaces = DecimalPlaces,
            TextHeight = TextHeight
        };
        CopyStyleTo(clone);
        return clone;
    }

    public override void Move(VXYZ vector)
    {
        Point1.Move(vector);
        Point2.Move(vector);
    }

    public override void Rotate(VPoint pivot, double angleDegrees)
    {
        Point1.Rotate(pivot, angleDegrees);
        Point2.Rotate(pivot, angleDegrees);
    }

    public override void Flip(VLine mirrorLine)
    {
        Point1.Flip(mirrorLine);
        Point2.Flip(mirrorLine);
    }

    public override void Scale(VPoint center, double factor)
    {
        Point1.Scale(center, factor);
        Point2.Scale(center, factor);
        Offset *= Math.Abs(factor);
        ExtensionLength *= Math.Abs(factor);
        TextHeight *= Math.Abs(factor);
    }

    public override (VPoint min, VPoint max) GetBounds()
    {
        return (
            new VPoint(Math.Min(Point1.X, Point2.X), Math.Min(Point1.Y, Point2.Y)),
            new VPoint(Math.Max(Point1.X, Point2.X), Math.Max(Point1.Y, Point2.Y))
        );
    }

    public override string ToString() => $"VDimension({Point1} -> {Point2}, {DisplayText})";
}
