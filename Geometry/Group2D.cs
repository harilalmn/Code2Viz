using Code2Viz.Canvas;

namespace Code2Viz.Geometry;

/// <summary>
/// A group of shapes that can be transformed together.
/// </summary>
public class VGroup : Shape
{
    public List<Shape> Shapes { get; } = new();



    public VGroup(params Shape[] shapes)
    {
        Shapes.AddRange(shapes);
        StrokeColor = ShapeDefaults.GlobalStrokeColor ?? "White";
    }

    public VGroup(IEnumerable<Shape> shapes)
    {
        Shapes.AddRange(shapes);
        StrokeColor = ShapeDefaults.GlobalStrokeColor ?? "White";
    }

    /// <summary>
    /// Adds a shape to the group.
    /// </summary>
    public VGroup Add(Shape shape)
    {
        Shapes.Add(shape);
        return this;
    }

    /// <summary>
    /// Adds multiple shapes to the group.
    /// </summary>
    public VGroup AddRange(IEnumerable<Shape> shapes)
    {
        Shapes.AddRange(shapes);
        return this;
    }

    /// <summary>
    /// Removes a shape from the group.
    /// </summary>
    public bool Remove(Shape shape) => Shapes.Remove(shape);

    /// <summary>
    /// Clears all shapes from the group.
    /// </summary>
    public void Clear() => Shapes.Clear();

    /// <summary>
    /// Gets the bounding box of all shapes in the group.
    /// </summary>
    public override (VPoint min, VPoint max) GetBounds()
    {
        if (Shapes.Count == 0)
            return (new VPoint(0, 0), new VPoint(0, 0));

        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;

        foreach (var shape in Shapes)
        {
            var (min, max) = shape.GetBounds();
            minX = Math.Min(minX, min.X);
            minY = Math.Min(minY, min.Y);
            maxX = Math.Max(maxX, max.X);
            maxY = Math.Max(maxY, max.Y);
        }

        return (new VPoint(minX, minY), new VPoint(maxX, maxY));
    }

    /// <summary>
    /// Gets the center of the group's bounding box.
    /// </summary>
    public VPoint GetCenter()
    {
        var (min, max) = GetBounds();
        return new VPoint((min.X + max.X) / 2, (min.Y + max.Y) / 2);
    }

    public override void Draw()
    {
        foreach (var shape in Shapes)
        {
            shape.Draw();
        }
    }

    public override Shape Clone()
    {
        var clone = new VGroup(Shapes.Select(s => s.Clone())) { Name = Name };
        CopyStyleTo(clone);
        return clone;
    }

    public override void Move(VXYZ vector)
    {
        foreach (var shape in Shapes)
            shape.Move(vector);
    }

    public override void Rotate(VPoint pivot, double angleDegrees)
    {
        foreach (var shape in Shapes)
            shape.Rotate(pivot, angleDegrees);
    }

    public override void Flip(VLine mirrorLine)
    {
        foreach (var shape in Shapes)
            shape.Flip(mirrorLine);
    }

    /// <summary>
    /// Scales all shapes in the group around a center point.
    /// </summary>
    public override void Scale(VPoint center, double factor)
    {
        foreach (var shape in Shapes)
            shape.Scale(center, factor);
    }

    public override string ToString() => $"VGroup({Shapes.Count} shapes{(string.IsNullOrEmpty(Name) ? "" : $", \"{Name}\"")})";
}
