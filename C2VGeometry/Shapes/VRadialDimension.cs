namespace C2VGeometry;

/// <summary>
/// A radial dimension showing the radius or diameter of a circle or arc.
/// Draws a leader line from the center to a point on the circumference with an arrowhead and text.
/// </summary>
public class VRadialDimension : Shape
{
    /// <summary>Center of the circle/arc being dimensioned</summary>
    public VXYZ Center { get; set; }

    /// <summary>Radius of the circle/arc being dimensioned</summary>
    public double Radius { get; set; }

    /// <summary>Angle (in degrees) at which the leader line points to the circumference</summary>
    public double LeaderAngle { get; set; } = 45;

    /// <summary>If true, shows diameter instead of radius</summary>
    public bool ShowDiameter { get; set; }

    /// <summary>Size of the arrowhead</summary>
    public double ArrowSize { get; set; } = 8;

    /// <summary>Custom text (if null, shows the calculated value)</summary>
    public string? CustomText { get; set; }

    /// <summary>Number of decimal places for the displayed value</summary>
    public int DecimalPlaces { get; set; } = 2;

    /// <summary>Text height</summary>
    public double TextHeight { get; set; } = 12;

    /// <summary>Text prefix prepended to the dimension value</summary>
    public string Prefix { get; set; } = "";

    /// <summary>Text suffix appended to the dimension value</summary>
    public string Suffix { get; set; } = "";

    public VRadialDimension(VCircle circle)
    {
        Center = new VXYZ(circle.Center.X, circle.Center.Y);
        Radius = circle.Radius;
        Color = "Yellow";
    }

    public VRadialDimension(VArc arc)
    {
        Center = new VXYZ(arc.Center.X, arc.Center.Y);
        Radius = arc.Radius;
        LeaderAngle = (arc.StartAngle + arc.EndAngle) / 2;
        Color = "Yellow";
    }

    public VRadialDimension(VXYZ center, double radius)
    {
        Center = new VXYZ(center.X, center.Y);
        Radius = radius;
        Color = "Yellow";
    }

    /// <summary>
    /// Gets the measured value (radius or diameter).
    /// </summary>
    public double Value => ShowDiameter ? Radius * 2 : Radius;

    /// <summary>
    /// Gets the display text for the dimension.
    /// </summary>
    public string DisplayText
    {
        get
        {
            if (CustomText != null) return CustomText;
            var symbol = ShowDiameter ? "\u2300" : "R";
            return $"{Prefix}{symbol}{Value.ToString($"F{DecimalPlaces}")}{Suffix}";
        }
    }

    /// <summary>
    /// Gets the geometry for rendering: leader line from center to circumference point, and text position.
    /// </summary>
    public (VXYZ leaderStart, VXYZ leaderEnd, VXYZ textPos) GetDimensionGeometry()
    {
        double angleRad = LeaderAngle * Math.PI / 180.0;
        double dirX = Math.Cos(angleRad);
        double dirY = Math.Sin(angleRad);

        var circumferencePoint = new VXYZ(Center.X + dirX * Radius, Center.Y + dirY * Radius);

        VXYZ leaderStart;
        if (ShowDiameter)
        {
            // For diameter: line goes through center from one side to the other
            leaderStart = new VXYZ(Center.X - dirX * Radius, Center.Y - dirY * Radius);
        }
        else
        {
            // For radius: line goes from center to circumference
            leaderStart = new VXYZ(Center.X, Center.Y);
        }

        // Text at midpoint of leader line, offset slightly outward
        var textPos = new VXYZ(
            (leaderStart.X + circumferencePoint.X) / 2,
            (leaderStart.Y + circumferencePoint.Y) / 2);

        return (leaderStart, circumferencePoint, textPos);
    }

    public override List<ControlPoint> GetControlPoints()
    {
        var (_, leaderEnd, _) = GetDimensionGeometry();
        return new List<ControlPoint>
        {
            new ControlPoint(ControlPointType.Move, Center.X, Center.Y, "Center"),
            new ControlPoint(ControlPointType.Vertex, leaderEnd.X, leaderEnd.Y, "LeaderEnd")
        };
    }

    public override void MoveControlPoint(int index, VXYZ newPosition)
    {
        switch (index)
        {
            case 0:
                var delta = new VXYZ(newPosition.X - Center.X, newPosition.Y - Center.Y, 0);
                Move(delta);
                break;
            case 1:
                // Update leader angle based on new position
                LeaderAngle = Math.Atan2(newPosition.Y - Center.Y, newPosition.X - Center.X) * 180.0 / Math.PI;
                break;
        }
    }

    public override VRadialDimension Clone()
    {
        var clone = new VRadialDimension(Center.Clone(), Radius)
        {
            LeaderAngle = LeaderAngle,
            ShowDiameter = ShowDiameter,
            ArrowSize = ArrowSize,
            CustomText = CustomText,
            DecimalPlaces = DecimalPlaces,
            TextHeight = TextHeight,
            Prefix = Prefix,
            Suffix = Suffix
        };
        CopyStyleTo(clone);
        return clone;
    }

    public override void Move(VXYZ vector)
    {
        Center = Center + vector;
    }

    public override void Rotate(VXYZ pivot, double angleDegrees)
    {
        Center = GeometryHelper.RotatePoint(Center, pivot, angleDegrees);
        LeaderAngle += angleDegrees;
    }

    public override void Flip(VLine mirrorLine)
    {
        Center = GeometryHelper.FlipPoint(Center, mirrorLine);
        LeaderAngle = -LeaderAngle;
    }

    public override void Scale(VXYZ center, double factor)
    {
        Center = GeometryHelper.ScalePoint(Center, center, factor);
        Radius *= Math.Abs(factor);
        TextHeight *= Math.Abs(factor);
        ArrowSize *= Math.Abs(factor);
    }

    public override BoundingBox GetBounds()
    {
        var (start, end, textPos) = GetDimensionGeometry();
        double minX = Math.Min(Math.Min(start.X, end.X), textPos.X);
        double minY = Math.Min(Math.Min(start.Y, end.Y), textPos.Y);
        double maxX = Math.Max(Math.Max(start.X, end.X), textPos.X);
        double maxY = Math.Max(Math.Max(start.Y, end.Y), textPos.Y);
        return new BoundingBox(new VXYZ(minX, minY), new VXYZ(maxX, maxY));
    }

    public override string ToString() => $"VRadialDimension(Center: {Center}, R: {Radius}, {DisplayText})";
}
