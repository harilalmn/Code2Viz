namespace C2VGeometry;

/// <summary>
/// An arrow (line with arrowhead at the end).
/// </summary>
public class VArrow : Shape
{
    public VXYZ Start { get; set; }
    public VXYZ End { get; set; }

    /// <summary>Size of the arrowhead (length from tip to base)</summary>
    public double HeadLength { get; set; } = 15;

    /// <summary>Angle of the arrowhead wings in degrees</summary>
    public double HeadAngle { get; set; } = 30;

    /// <summary>Whether to draw arrowhead at start as well</summary>
    public bool DoubleEnded { get; set; } = false;

    public VXYZ MidPoint => new VXYZ((Start.X + End.X) / 2, (Start.Y + End.Y) / 2);

    public VArrow(VXYZ start, VXYZ end)
    {
        Start = start;
        End = end;
        Color = ShapeDefaults.GlobalColor ?? "Orange";
    }

    public VArrow(double x1, double y1, double x2, double y2)
    {
        Start = new VXYZ(x1, y1);
        End = new VXYZ(x2, y2);
        Color = ShapeDefaults.GlobalColor ?? "Orange";
    }

    public VArrow(VXYZ startPoint, VXYZ direction, double length)
    {
        Start = startPoint;
        var normalizedDir = direction.Normalize();
        End = new VXYZ(startPoint.X + normalizedDir.X * length, startPoint.Y + normalizedDir.Y * length);
        Color = ShapeDefaults.GlobalColor ?? "Orange";
    }

    /// <summary>
    /// Gets the arrowhead points for the end of the arrow.
    /// </summary>
    public (VXYZ wing1, VXYZ wing2) GetEndArrowhead()
    {
        return GetArrowheadPoints(End, Start);
    }

    /// <summary>
    /// Gets the arrowhead points for the start of the arrow (if double-ended).
    /// </summary>
    public (VXYZ wing1, VXYZ wing2) GetStartArrowhead()
    {
        return GetArrowheadPoints(Start, End);
    }

    private (VXYZ wing1, VXYZ wing2) GetArrowheadPoints(VXYZ tip, VXYZ from)
    {
        double dx = tip.X - from.X;
        double dy = tip.Y - from.Y;
        double length = Math.Sqrt(dx * dx + dy * dy);

        if (length < 1e-10) return (tip, tip);

        // Normalize direction (pointing from -> tip)
        dx /= length;
        dy /= length;

        // Calculate base of arrowhead (HeadLength back from tip)
        double baseX = tip.X - dx * HeadLength;
        double baseY = tip.Y - dy * HeadLength;

        // Perpendicular direction for wing width (3:1 ratio means width = HeadLength/3)
        double halfWidth = HeadLength / 6.0;  // Half of (HeadLength / 3)
        double perpX = -dy;  // Perpendicular
        double perpY = dx;

        // Wing points at the base
        var wing1 = new VXYZ(baseX + perpX * halfWidth, baseY + perpY * halfWidth);
        var wing2 = new VXYZ(baseX - perpX * halfWidth, baseY - perpY * halfWidth);

        return (wing1, wing2);
    }



    public override List<ControlPoint> GetControlPoints()
    {
        var mid = MidPoint;
        return new List<ControlPoint>
        {
            new ControlPoint(ControlPointType.Move, mid.X, mid.Y, "Center"),
            new ControlPoint(ControlPointType.Vertex, Start.X, Start.Y, "Start"),
            new ControlPoint(ControlPointType.Vertex, End.X, End.Y, "End")
        };
    }

    public override void MoveControlPoint(int index, VXYZ newPosition)
    {
        switch (index)
        {
            case 0:
                var mid = MidPoint;
                var delta = new VXYZ(newPosition.X - mid.X, newPosition.Y - mid.Y, 0);
                Move(delta);
                break;
            case 1:
                Start = new VXYZ(newPosition.X, newPosition.Y);
                break;
            case 2:
                End = new VXYZ(newPosition.X, newPosition.Y);
                break;
        }
    }

    public override VArrow Clone()
    {
        var clone = new VArrow(Start.Clone(), End.Clone())
        {
            HeadLength = HeadLength,
            HeadAngle = HeadAngle,
            DoubleEnded = DoubleEnded
        };
        CopyStyleTo(clone);
        return clone;
    }

    public override void Move(VXYZ vector)
    {
        Start = Start + vector;
        End = End + vector;
    }

    public override void Rotate(VXYZ pivot, double angleDegrees)
    {
        Start = GeometryHelper.RotatePoint(Start, pivot, angleDegrees);
        End = GeometryHelper.RotatePoint(End, pivot, angleDegrees);
    }

    public override void Flip(VLine mirrorLine)
    {
        Start = GeometryHelper.FlipPoint(Start, mirrorLine);
        End = GeometryHelper.FlipPoint(End, mirrorLine);
    }

    public override void Scale(VXYZ center, double factor)
    {
        Start = GeometryHelper.ScalePoint(Start, center, factor);
        End = GeometryHelper.ScalePoint(End, center, factor);
        HeadLength *= Math.Abs(factor);
    }

    public override BoundingBox GetBounds()
    {
        return new BoundingBox(
            new VXYZ(Math.Min(Start.X, End.X), Math.Min(Start.Y, End.Y)),
            new VXYZ(Math.Max(Start.X, End.X), Math.Max(Start.Y, End.Y))
        );
    }

    public override string ToString() => $"VArrow({Start} -> {End})";
}
