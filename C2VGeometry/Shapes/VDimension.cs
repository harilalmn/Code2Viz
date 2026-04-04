namespace C2VGeometry;

/// <summary>
/// A dimension line showing the distance between two points with text annotation.
/// </summary>
public class VDimension : Shape
{
    public VXYZ Point1 { get; set; }
    public VXYZ Point2 { get; set; }

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

    /// <summary>How far extension lines extend beyond the dimension line</summary>
    public double ExtendBeyondDimLines { get; set; } = 1.25;

    /// <summary>Gap between the origin point and the start of the extension line</summary>
    public double OffsetFromOrigin { get; set; } = 0.625;

    /// <summary>If true, the first extension line is not drawn</summary>
    public bool SuppressExtLine1 { get; set; }

    /// <summary>If true, the second extension line is not drawn</summary>
    public bool SuppressExtLine2 { get; set; }

    /// <summary>Text prefix prepended to the dimension value</summary>
    public string Prefix { get; set; } = "";

    /// <summary>Text suffix appended to the dimension value</summary>
    public string Suffix { get; set; } = "";

    public VDimension(VXYZ point1, VXYZ point2)
    {
        Point1 = point1;
        Point2 = point2;
        Color = "Yellow";
    }

    public VDimension(double x1, double y1, double x2, double y2)
    {
        Point1 = new VXYZ(x1, y1);
        Point2 = new VXYZ(x2, y2);
        Color = "Yellow";
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
    public string DisplayText => CustomText ?? $"{Prefix}{Distance.ToString($"F{DecimalPlaces}")}{Suffix}";

    /// <summary>
    /// Gets the geometry for rendering the dimension.
    /// </summary>
    public (VXYZ dimStart, VXYZ dimEnd, VXYZ textPos, VXYZ ext1Start, VXYZ ext1End, VXYZ ext2Start, VXYZ ext2End) GetDimensionGeometry()
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
        var dimStart = new VXYZ(Point1.X + perpX * Offset, Point1.Y + perpY * Offset);
        var dimEnd = new VXYZ(Point2.X + perpX * Offset, Point2.Y + perpY * Offset);

        // Text position (center of dimension line)
        var textPos = new VXYZ((dimStart.X + dimEnd.X) / 2, (dimStart.Y + dimEnd.Y) / 2);

        // Extension lines: from OffsetFromOrigin gap to Offset + ExtendBeyondDimLines
        var ext1Start = new VXYZ(Point1.X + perpX * OffsetFromOrigin, Point1.Y + perpY * OffsetFromOrigin);
        var ext1End = new VXYZ(Point1.X + perpX * (Offset + ExtendBeyondDimLines), Point1.Y + perpY * (Offset + ExtendBeyondDimLines));
        var ext2Start = new VXYZ(Point2.X + perpX * OffsetFromOrigin, Point2.Y + perpY * OffsetFromOrigin);
        var ext2End = new VXYZ(Point2.X + perpX * (Offset + ExtendBeyondDimLines), Point2.Y + perpY * (Offset + ExtendBeyondDimLines));

        return (dimStart, dimEnd, textPos, ext1Start, ext1End, ext2Start, ext2End);
    }



    public override List<ControlPoint> GetControlPoints()
    {
        double midX = (Point1.X + Point2.X) / 2;
        double midY = (Point1.Y + Point2.Y) / 2;
        return new List<ControlPoint>
        {
            new ControlPoint(ControlPointType.Move, midX, midY, "Center"),
            new ControlPoint(ControlPointType.Vertex, Point1.X, Point1.Y, "Point1"),
            new ControlPoint(ControlPointType.Vertex, Point2.X, Point2.Y, "Point2")
        };
    }

    public override void MoveControlPoint(int index, VXYZ newPosition)
    {
        switch (index)
        {
            case 0:
                double midX = (Point1.X + Point2.X) / 2;
                double midY = (Point1.Y + Point2.Y) / 2;
                var delta = new VXYZ(newPosition.X - midX, newPosition.Y - midY, 0);
                Move(delta);
                break;
            case 1:
                Point1 = new VXYZ(newPosition.X, newPosition.Y);
                break;
            case 2:
                Point2 = new VXYZ(newPosition.X, newPosition.Y);
                break;
        }
    }

    public override VDimension Clone()
    {
        var clone = new VDimension(Point1.Clone(), Point2.Clone())
        {
            Offset = Offset,
            ExtensionLength = ExtensionLength,
            ArrowSize = ArrowSize,
            CustomText = CustomText,
            DecimalPlaces = DecimalPlaces,
            TextHeight = TextHeight,
            ExtendBeyondDimLines = ExtendBeyondDimLines,
            OffsetFromOrigin = OffsetFromOrigin,
            SuppressExtLine1 = SuppressExtLine1,
            SuppressExtLine2 = SuppressExtLine2,
            Prefix = Prefix,
            Suffix = Suffix
        };
        CopyStyleTo(clone);
        return clone;
    }

    public override void Move(VXYZ vector)
    {
        Point1 = Point1 + vector;
        Point2 = Point2 + vector;
    }

    public override void Rotate(VXYZ pivot, double angleDegrees)
    {
        Point1 = GeometryHelper.RotatePoint(Point1, pivot, angleDegrees);
        Point2 = GeometryHelper.RotatePoint(Point2, pivot, angleDegrees);
    }

    public override void Flip(VLine mirrorLine)
    {
        Point1 = GeometryHelper.FlipPoint(Point1, mirrorLine);
        Point2 = GeometryHelper.FlipPoint(Point2, mirrorLine);
    }

    public override void Scale(VXYZ center, double factor)
    {
        Point1 = GeometryHelper.ScalePoint(Point1, center, factor);
        Point2 = GeometryHelper.ScalePoint(Point2, center, factor);
        Offset *= Math.Abs(factor);
        ExtensionLength *= Math.Abs(factor);
        TextHeight *= Math.Abs(factor);
        ArrowSize *= Math.Abs(factor);
        ExtendBeyondDimLines *= Math.Abs(factor);
        OffsetFromOrigin *= Math.Abs(factor);
    }

    public override BoundingBox GetBounds()
    {
        return new BoundingBox(
            new VXYZ(Math.Min(Point1.X, Point2.X), Math.Min(Point1.Y, Point2.Y)),
            new VXYZ(Math.Max(Point1.X, Point2.X), Math.Max(Point1.Y, Point2.Y))
        );
    }

    public override string ToString() => $"VDimension({Point1} -> {Point2}, {DisplayText})";
}
