using System;

namespace Code2Viz.Geometry;

public interface IDrawable
{
    void Draw();

    // Styling properties with defaults
    string StrokeColor { get; set; }
    string FillColor { get; set; }
    double StrokeThickness { get; set; }
}

public abstract class Shape : IDrawable
{
    private static int _idCounter = 0;
    
    public string Id { get; set; } = $"shape_{++_idCounter}";
    public string Name { get; set; } = "";
    
    public string StrokeColor { get; set; } = ShapeDefaults.GlobalStrokeColor ?? "Cyan";
    public string FillColor { get; set; } = ShapeDefaults.GlobalFillColor ?? "Transparent";
    public double StrokeThickness { get; set; } = ShapeDefaults.GlobalStrokeThickness ?? 2;

    // Animation properties
    /// <summary>
    /// Draw factor for progressive drawing animation (0 = invisible, 1 = fully drawn).
    /// </summary>
    public double DrawFactor { get; set; } = 1.0;

    /// <summary>
    /// X offset for translation animation.
    /// </summary>
    public double OffsetX { get; set; } = 0;

    /// <summary>
    /// Y offset for translation animation.
    /// </summary>
    public double OffsetY { get; set; } = 0;

    /// <summary>
    /// Rotation angle in degrees for rotation animation.
    /// </summary>
    public double RotationAngle { get; set; } = 0;

    /// <summary>
    /// Pivot point for rotation animation.
    /// </summary>
    public VPoint? RotationPivot { get; set; }

    /// <summary>
    /// Progress for flip animation (0 = original, 1 = fully flipped).
    /// </summary>
    public double FlipProgress { get; set; } = 0;

    /// <summary>
    /// Axis line for flip animation.
    /// </summary>
    public VLine? FlipAxis { get; set; }

    /// <summary>
    /// Opacity for fade animation (0 = fully transparent, 1 = fully opaque).
    /// </summary>
    public double Opacity { get; set; } = 1.0;

    public abstract void Draw();

    /// <summary>
    /// Creates a deep copy of this shape.
    /// </summary>
    public abstract Shape Clone();

    /// <summary>
    /// Moves this shape by the given vector.
    /// </summary>
    public abstract void Move(VXYZ vector);

    /// <summary>
    /// Rotates this shape around a pivot point by the given angle.
    /// </summary>
    public abstract void Rotate(VPoint pivot, double angleDegrees);

    /// <summary>
    /// Flips (mirrors) this shape across the given line.
    /// </summary>
    public abstract void Flip(VLine mirrorLine);

    /// <summary>
    /// Scales this shape around a center point.
    /// </summary>
    /// <param name="center">The center point to scale around.</param>
    /// <param name="factor">Scale factor (1.0 = no change, 2.0 = double size).</param>
    public abstract void Scale(VPoint center, double factor);

    /// <summary>
    /// Gets the bounding box of this shape.
    /// </summary>
    /// <returns>Tuple of (min point, max point) defining the axis-aligned bounding box.</returns>
    public abstract (VPoint min, VPoint max) GetBounds();

    /// <summary>
    /// Calculates the intersection of this shape with another shape.
    /// Returns the resulting Shape (VPoint, VLine, VRectangle, etc.) or null if no intersection.
    /// </summary>
    public virtual Shape? Intersect(Shape other)
    {
        return null; // Default implementation returns null (no intersection supported by default)
    }

    /// <summary>
    /// Checks if this shape intersects with another shape.
    /// </summary>
    public virtual bool DoesIntersect(Shape other)
    {
        return Intersect(other) != null;
    }


    /// <summary>
    /// Calculates the minimum distance from this shape to a point.
    /// </summary>
    public virtual double DistanceTo(VPoint point)
    {
        // Default implementation uses bounding box center
        var (min, max) = GetBounds();
        var centerX = (min.X + max.X) / 2;
        var centerY = (min.Y + max.Y) / 2;
        var dx = point.X - centerX;
        var dy = point.Y - centerY;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>
    /// Checks if a point is inside this shape (for filled shapes).
    /// </summary>
    public virtual bool Contains(VPoint point)
    {
        // Default implementation checks bounding box
        var (min, max) = GetBounds();
        return point.X >= min.X && point.X <= max.X &&
               point.Y >= min.Y && point.Y <= max.Y;
    }

    /// <summary>
    /// Copies styling properties to another shape.
    /// </summary>
    protected void CopyStyleTo(Shape target)
    {
        target.StrokeColor = StrokeColor;
        target.FillColor = FillColor;
        target.StrokeThickness = StrokeThickness;
    }
}
