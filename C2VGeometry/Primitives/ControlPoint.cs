namespace C2VGeometry;

/// <summary>
/// Represents a control point for interactive shape editing.
/// </summary>
public class ControlPoint
{
    public ControlPointType Type { get; }
    public double X { get; set; }
    public double Y { get; set; }
    public string Label { get; }

    public ControlPoint(ControlPointType type, double x, double y, string label = "")
    {
        Type = type;
        X = x;
        Y = y;
        Label = label;
    }

    public VXYZ ToVXYZ() => new VXYZ(X, Y);
}
