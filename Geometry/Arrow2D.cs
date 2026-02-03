using Code2Viz.Canvas;

namespace Code2Viz.Geometry;

/// <summary>
/// An arrow (line with arrowhead at the end).
/// </summary>
public class VArrow : Shape
{
    public VPoint Start { get; set; }
    public VPoint End { get; set; }

    /// <summary>Size of the arrowhead (length from tip to base)</summary>
    public double HeadLength { get; set; } = 15;

    /// <summary>Angle of the arrowhead wings in degrees</summary>
    public double HeadAngle { get; set; } = 30;

    /// <summary>Whether to draw arrowhead at start as well</summary>
    public bool DoubleEnded { get; set; } = false;

    public VPoint StartPoint => Start;
    public VPoint EndPoint => End;
    public VPoint MidPoint => VPoint.Internal((Start.X + End.X) / 2, (Start.Y + End.Y) / 2);

    public VArrow(VPoint start, VPoint end)
    {
        Start = start;
        End = end;
        Color = ShapeDefaults.GlobalColor ?? "Orange";
    }

    public VArrow(double x1, double y1, double x2, double y2)
    {
        Start = VPoint.Internal(x1, y1);
        End = VPoint.Internal(x2, y2);
        Color = ShapeDefaults.GlobalColor ?? "Orange";
    }

    public VArrow(VPoint startPoint, VXYZ direction, double length)
    {
        Start = startPoint;
        var normalizedDir = direction.Normalize();
        End = VPoint.Internal(startPoint.X + normalizedDir.X * length, startPoint.Y + normalizedDir.Y * length);
        Color = ShapeDefaults.GlobalColor ?? "Orange";
    }

    /// <summary>
    /// Gets the arrowhead points for the end of the arrow.
    /// </summary>
    public (VPoint wing1, VPoint wing2) GetEndArrowhead()
    {
        return GetArrowheadPoints(End, Start);
    }

    /// <summary>
    /// Gets the arrowhead points for the start of the arrow (if double-ended).
    /// </summary>
    public (VPoint wing1, VPoint wing2) GetStartArrowhead()
    {
        return GetArrowheadPoints(Start, End);
    }

    private (VPoint wing1, VPoint wing2) GetArrowheadPoints(VPoint tip, VPoint from)
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
        var wing1 = VPoint.Internal(baseX + perpX * halfWidth, baseY + perpY * halfWidth);
        var wing2 = VPoint.Internal(baseX - perpX * halfWidth, baseY - perpY * halfWidth);

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

    public override void MoveControlPoint(int index, VPoint newPosition)
    {
        switch (index)
        {
            case 0:
                var mid = MidPoint;
                var delta = new VXYZ(newPosition.X - mid.X, newPosition.Y - mid.Y, 0);
                Move(delta);
                break;
            case 1:
                Start.X = newPosition.X;
                Start.Y = newPosition.Y;
                break;
            case 2:
                End.X = newPosition.X;
                End.Y = newPosition.Y;
                break;
        }
    }

    public override Shape Clone()
    {
        var clone = new VArrow((VPoint)Start.Clone(), (VPoint)End.Clone())
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
        Start.Move(vector);
        End.Move(vector);
    }

    public override void Rotate(VPoint pivot, double angleDegrees)
    {
        Start.Rotate(pivot, angleDegrees);
        End.Rotate(pivot, angleDegrees);
    }

    public override void Flip(VLine mirrorLine)
    {
        Start.Flip(mirrorLine);
        End.Flip(mirrorLine);
    }

    public override void Scale(VPoint center, double factor)
    {
        Start.Scale(center, factor);
        End.Scale(center, factor);
        HeadLength *= Math.Abs(factor);
    }

    public override (VPoint min, VPoint max) GetBounds()
    {
        return (
            VPoint.Internal(Math.Min(Start.X, End.X), Math.Min(Start.Y, End.Y)),
            VPoint.Internal(Math.Max(Start.X, End.X), Math.Max(Start.Y, End.Y))
        );
    }

    public override string ToString() => $"VArrow({Start} -> {End})";
}
