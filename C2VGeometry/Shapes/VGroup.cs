namespace C2VGeometry;

/// <summary>
/// A group of shapes that can be transformed together.
/// Supports all Shape operations and propagates them to child shapes.
/// </summary>
public class VGroup : Shape
{
    /// <summary>
    /// The shapes contained in this group.
    /// </summary>
    public List<Shape> Shapes { get; } = new();

    /// <summary>
    /// Gets the number of shapes in the group.
    /// </summary>
    public int Count => Shapes.Count;

    /// <summary>
    /// Gets a shape by index.
    /// </summary>
    public Shape this[int index] => Shapes[index];

    /// <summary>
    /// Creates an empty group.
    /// </summary>
    public VGroup()
    {
        Color = "White";
        FillColor = "Transparent";
    }

    /// <summary>
    /// Creates a group from one or more shapes.
    /// </summary>
    public VGroup(params Shape[] shapes)
    {
        Shapes.AddRange(shapes);
        Color = "White";
        FillColor = "Transparent";
    }

    /// <summary>
    /// Creates a group from a collection of shapes.
    /// </summary>
    public VGroup(IEnumerable<Shape> shapes)
    {
        Shapes.AddRange(shapes);
        Color = "White";
        FillColor = "Transparent";
    }

    /// <summary>
    /// Creates a group from a List of shapes.
    /// </summary>
    public VGroup(List<Shape> shapes)
    {
        Shapes.AddRange(shapes);
        Color = "White";
        FillColor = "Transparent";
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
    /// Removes a shape at the specified index.
    /// </summary>
    public void RemoveAt(int index) => Shapes.RemoveAt(index);

    /// <summary>
    /// Clears all shapes from the group.
    /// </summary>
    public void Clear() => Shapes.Clear();

    /// <summary>
    /// Checks if the group contains a specific shape.
    /// </summary>
    public bool ContainsShape(Shape shape) => Shapes.Contains(shape);

    /// <summary>
    /// Gets the bounding box of all shapes in the group.
    /// </summary>
    public override BoundingBox GetBounds()
    {
        if (Shapes.Count == 0)
            return new BoundingBox(new VXYZ(0, 0), new VXYZ(0, 0));

        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;

        foreach (var shape in Shapes)
        {
            var bounds = shape.GetBounds();
            minX = Math.Min(minX, bounds.Min.X);
            minY = Math.Min(minY, bounds.Min.Y);
            maxX = Math.Max(maxX, bounds.Max.X);
            maxY = Math.Max(maxY, bounds.Max.Y);
        }

        return new BoundingBox(new VXYZ(minX, minY), new VXYZ(maxX, maxY));
    }

    /// <summary>
    /// Gets the center of the group's bounding box.
    /// </summary>
    public VXYZ GetCenter()
    {
        var bounds = GetBounds();
        return new VXYZ((bounds.Min.X + bounds.Max.X) / 2, (bounds.Min.Y + bounds.Max.Y) / 2);
    }

    /// <summary>
    /// Draws the group as a single entity.
    /// The group is added to the renderer and rendered as one selectable unit.
    /// </summary>


    /// <summary>
    /// Creates a deep copy of this group and all its shapes.
    /// </summary>
    public override VGroup Clone()
    {
        var clone = new VGroup(Shapes.Select(s => s.Clone())) { Name = Name };
        CopyStyleTo(clone);
        return clone;
    }

    /// <summary>
    /// Moves all shapes in the group by the given vector.
    /// </summary>
    public override void Move(VXYZ vector)
    {
        foreach (var shape in Shapes)
            shape.Move(vector);
    }

    /// <summary>
    /// Rotates all shapes in the group around a pivot point.
    /// </summary>
    public override void Rotate(VXYZ pivot, double angleDegrees)
    {
        foreach (var shape in Shapes)
            shape.Rotate(pivot, angleDegrees);
    }

    /// <summary>
    /// Flips (mirrors) all shapes in the group across the given line.
    /// </summary>
    public override void Flip(VLine mirrorLine)
    {
        foreach (var shape in Shapes)
            shape.Flip(mirrorLine);
    }

    /// <summary>
    /// Scales all shapes in the group around a center point.
    /// </summary>
    public override void Scale(VXYZ center, double factor)
    {
        foreach (var shape in Shapes)
            shape.Scale(center, factor);
    }

    /// <summary>
    /// Gets control points for the group (center point for moving the entire group).
    /// </summary>
    public override List<ControlPoint> GetControlPoints()
    {
        var bounds = GetBounds();
        var center = GetCenter();

        return new List<ControlPoint>
        {
            new ControlPoint(ControlPointType.Move, center.X, center.Y, "Center"),
            new ControlPoint(ControlPointType.Vertex, bounds.Min.X, bounds.Min.Y, "Min"),
            new ControlPoint(ControlPointType.Vertex, bounds.Max.X, bounds.Max.Y, "Max"),
            new ControlPoint(ControlPointType.Vertex, bounds.Min.X, bounds.Max.Y, "TL"),
            new ControlPoint(ControlPointType.Vertex, bounds.Max.X, bounds.Min.Y, "BR")
        };
    }

    /// <summary>
    /// Moves a control point. Index 0 moves the entire group.
    /// </summary>
    public override void MoveControlPoint(int index, VXYZ newPosition)
    {
        if (index == 0)
        {
            // Move entire group
            var center = GetCenter();
            var delta = new VXYZ(newPosition.X - center.X, newPosition.Y - center.Y, 0);
            Move(delta);
        }
        // Corner control points could be used for scaling in the future
    }

    /// <summary>
    /// Calculates the minimum distance from any shape in the group to a point.
    /// </summary>
    public override double DistanceTo(VXYZ point)
    {
        if (Shapes.Count == 0)
            return double.MaxValue;

        return Shapes.Min(s => s.DistanceTo(point));
    }

    /// <summary>
    /// Checks if any shape in the group contains the point.
    /// </summary>
    public override bool Contains(VXYZ point)
    {
        return Shapes.Any(s => s.Contains(point));
    }

    /// <summary>
    /// Checks if any shape in the group intersects with another shape.
    /// </summary>
    public override bool DoesIntersect(Shape other)
    {
        if (other is VGroup otherGroup)
        {
            // Check all pairs of shapes
            return Shapes.Any(s => otherGroup.Shapes.Any(os => s.DoesIntersect(os)));
        }
        return Shapes.Any(s => s.DoesIntersect(other));
    }

    /// <summary>
    /// Finds the first intersection between any shape in the group and another shape.
    /// </summary>
    public override Shape? Intersect(Shape other)
    {
        foreach (var shape in Shapes)
        {
            var intersection = shape.Intersect(other);
            if (intersection != null)
                return intersection;
        }
        return null;
    }

    /// <summary>
    /// Applies the group's stroke color to all shapes.
    /// </summary>
    public VGroup ApplyColor()
    {
        foreach (var shape in Shapes)
            shape.Color = Color;
        return this;
    }

    /// <summary>
    /// Applies the group's fill color to all shapes.
    /// </summary>
    public VGroup ApplyFillColor()
    {
        foreach (var shape in Shapes)
            shape.FillColor = FillColor;
        return this;
    }

    /// <summary>
    /// Applies the group's stroke thickness to all shapes.
    /// </summary>
    public VGroup ApplyLineWeight()
    {
        foreach (var shape in Shapes)
            shape.LineWeight = LineWeight;
        return this;
    }

    /// <summary>
    /// Applies all group styling (color, fill, thickness) to all shapes.
    /// </summary>
    public VGroup ApplyStyle()
    {
        foreach (var shape in Shapes)
        {
            shape.Color = Color;
            shape.FillColor = FillColor;
            shape.LineWeight = LineWeight;
        }
        return this;
    }

    /// <summary>
    /// Sets the opacity for all shapes in the group.
    /// </summary>
    public VGroup SetOpacity(double opacity)
    {
        Opacity = opacity;
        foreach (var shape in Shapes)
            shape.Opacity = opacity;
        return this;
    }

    /// <summary>
    /// Gets all shapes of a specific type from the group.
    /// </summary>
    public IEnumerable<T> GetShapesOfType<T>() where T : Shape
    {
        return Shapes.OfType<T>();
    }

    /// <summary>
    /// Gets a flattened list of all shapes, including shapes within nested groups.
    /// </summary>
    public List<Shape> Flatten()
    {
        var result = new List<Shape>();
        foreach (var shape in Shapes)
        {
            if (shape is VGroup nestedGroup)
                result.AddRange(nestedGroup.Flatten());
            else
                result.Add(shape);
        }
        return result;
    }

    /// <summary>
    /// Applies an action to each shape in the group.
    /// </summary>
    public VGroup ForEach(Action<Shape> action)
    {
        foreach (var shape in Shapes)
            action(shape);
        return this;
    }

    /// <summary>
    /// Filters shapes in the group using a predicate.
    /// Returns a new group containing only matching shapes.
    /// </summary>
    public VGroup Where(Func<Shape, bool> predicate)
    {
        return new VGroup(Shapes.Where(predicate));
    }

    public override string ToString() => $"VGroup({Shapes.Count} shapes{(string.IsNullOrEmpty(Name) ? "" : $", \"{Name}\"")})";
}
