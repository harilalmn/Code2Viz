using System;
using System.Collections.Generic;

namespace C2VGeometry;

/// <summary>
/// Abstract base class for all 2D geometry shapes.
/// Shapes can optionally auto-register with a canvas/rendering system via IShapeRegistry.
/// </summary>
public abstract class Shape : IDrawable
{
    private static long _idCounter = 0;

    /// <summary>
    /// Unique identifier for this shape instance.
    /// </summary>
    public long Id { get; } = System.Threading.Interlocked.Increment(ref _idCounter);

    /// <summary>
    /// Resets the shape ID counter back to 0. Called before each code execution.
    /// </summary>
    public static void ResetIdCounter() => System.Threading.Interlocked.Exchange(ref _idCounter, 0);

    /// <summary>
    /// Optional name/label for this shape.
    /// </summary>
    public string Name { get; set; } = "";

    #region Static Configuration

    /// <summary>
    /// Optional registry for shape auto-registration.
    /// Set this to receive callbacks when shapes are created.
    /// If null, shapes are created without registration (standalone mode).
    /// </summary>
    public static IShapeRegistry? DefaultRegistry { get; set; }

    /// <summary>
    /// When false, shapes will not auto-register with DefaultRegistry on construction.
    /// Use this for algorithms that create many temporary shapes.
    /// Default is true for normal usage.
    /// </summary>
    public static bool AutoRegister { get; set; } = true;

    /// <summary>
    /// Default stroke color for new shapes.
    /// </summary>
    public static string DefaultColor { get; set; } = "Cyan";

    /// <summary>
    /// Default fill color for new shapes.
    /// </summary>
    public static string DefaultFillColor { get; set; } = "Transparent";

    /// <summary>
    /// Default stroke weight for new shapes.
    /// </summary>
    public static double DefaultLineWeight { get; set; } = 2.0;

    /// <summary>
    /// Default line type for new shapes.
    /// </summary>
    public static LineType DefaultLineType { get; set; } = LineType.Continuous;

    /// <summary>
    /// Default line type scale for new shapes.
    /// </summary>
    public static double DefaultLineTypeScale { get; set; } = 1.0;

    /// <summary>
    /// Resets all static defaults to their initial values.
    /// </summary>
    public static void ResetDefaults()
    {
        DefaultColor = "Cyan";
        DefaultFillColor = "Transparent";
        DefaultLineWeight = 2.0;
        DefaultLineType = LineType.Continuous;
        DefaultLineTypeScale = 1.0;
    }

    #endregion

    #region Constructors

    /// <summary>
    /// Base constructor that auto-registers the shape with the registry (if AutoRegister is true and DefaultRegistry is set).
    /// Shapes are automatically displayed when created - no need to call Draw().
    /// </summary>
    protected Shape()
    {
        Color = DefaultColor;
        FillColor = DefaultFillColor;
        LineWeight = DefaultLineWeight;
        LineType = DefaultLineType;
        LineTypeScale = DefaultLineTypeScale;

        // Auto-register with registry if configured
        if (AutoRegister && DefaultRegistry != null)
        {
            DefaultRegistry.Register(this);
        }
    }

    /// <summary>
    /// Protected constructor that allows skipping auto-registration.
    /// Used internally by geometry classes for intermediate calculations.
    /// </summary>
    /// <param name="register">If false, the shape will not be auto-registered with the registry.</param>
    protected Shape(bool register)
    {
        Color = DefaultColor;
        FillColor = DefaultFillColor;
        LineWeight = DefaultLineWeight;
        LineType = DefaultLineType;
        LineTypeScale = DefaultLineTypeScale;

        if (register && AutoRegister && DefaultRegistry != null)
        {
            DefaultRegistry.Register(this);
        }
    }

    #endregion

    #region Styling Properties

    /// <summary>
    /// The stroke color name (e.g., "Cyan", "Red", "#FF0000").
    /// </summary>
    public string Color { get; set; }

    /// <summary>
    /// The fill color name (e.g., "Transparent", "Blue").
    /// </summary>
    public string FillColor { get; set; }

    /// <summary>
    /// The stroke thickness in pixels.
    /// </summary>
    public double LineWeight { get; set; }

    /// <summary>
    /// The line pattern style (solid, dashed, dotted, etc.).
    /// </summary>
    public LineType LineType { get; set; }

    /// <summary>
    /// Scale factor for stroke pattern (dash/gap lengths). Default is 1.0.
    /// </summary>
    public double LineTypeScale { get; set; }

    #endregion

    #region Animation Properties

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
    public VXYZ? RotationPivot { get; set; }

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

    #endregion

    #region State Properties

    /// <summary>
    /// Indicates whether this shape has been added to a registry/canvas via Draw().
    /// </summary>
    public bool IsPlaced { get; set; } = false;

    /// <summary>
    /// Indicates whether this shape is visible on the canvas.
    /// Hidden shapes are not rendered but remain in the shape collection.
    /// </summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>
    /// Indicates whether this shape was explicitly drawn by the user calling .Draw()
    /// </summary>
    public bool IsExplicitlyDrawn { get; set; } = false;

    /// <summary>
    /// Indicates whether this shape is currently selected.
    /// </summary>
    public bool IsSelected { get; set; } = false;

    #endregion

    #region Core Methods

    /// <summary>
    /// Registers this shape with the default registry (if set).
    /// </summary>
    public virtual void Draw()
    {
        IsExplicitlyDrawn = true;
        DefaultRegistry?.Register(this);
    }

    /// <summary>
    /// Removes this shape from the registry.
    /// </summary>
    public void Remove()
    {
        DefaultRegistry?.Unregister(this);
    }

    /// <summary>
    /// Moves this shape above the specified shape in the draw order (renders on top).
    /// </summary>
    public void BringAbove(Shape otherShape)
    {
        DefaultRegistry?.MoveAbove(this, otherShape);
    }

    /// <summary>
    /// Moves this shape behind the specified shape in the draw order (renders underneath).
    /// </summary>
    public void SendBehind(Shape otherShape)
    {
        DefaultRegistry?.MoveBehind(this, otherShape);
    }

    /// <summary>
    /// Shows this shape on the canvas (sets IsVisible to true).
    /// </summary>
    public void Show()
    {
        IsVisible = true;
    }

    /// <summary>
    /// Hides this shape from the canvas (sets IsVisible to false).
    /// The shape remains in the shape collection but is not rendered.
    /// </summary>
    public void Hide()
    {
        IsVisible = false;
    }

    #endregion

    #region Abstract Methods

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
    public abstract void Rotate(VXYZ pivot, double angleDegrees);

    /// <summary>
    /// Flips (mirrors) this shape across the given line.
    /// </summary>
    public abstract void Flip(VLine mirrorLine);

    /// <summary>
    /// Scales this shape around a center point.
    /// </summary>
    /// <param name="center">The center point to scale around.</param>
    /// <param name="factor">Scale factor (1.0 = no change, 2.0 = double size).</param>
    public abstract void Scale(VXYZ center, double factor);

    /// <summary>
    /// Gets the bounding box of this shape.
    /// </summary>
    /// <returns>A BoundingBox with Min and Max points defining the axis-aligned bounding box.</returns>
    public abstract BoundingBox GetBounds();

    #endregion

    #region Virtual Methods

    /// <summary>
    /// Gets the control points for interactive editing.
    /// </summary>
    /// <returns>List of control points with their types and positions.</returns>
    public virtual List<ControlPoint> GetControlPoints()
    {
        // Default implementation returns bounding box center
        var bounds = GetBounds();
        return new List<ControlPoint>
        {
            new ControlPoint(ControlPointType.Move, (bounds.Min.X + bounds.Max.X) / 2, (bounds.Min.Y + bounds.Max.Y) / 2, "Center")
        };
    }

    /// <summary>
    /// Moves a control point to a new position.
    /// </summary>
    /// <param name="index">Index of the control point.</param>
    /// <param name="newPosition">New position for the control point.</param>
    public virtual void MoveControlPoint(int index, VXYZ newPosition)
    {
        // Default implementation moves the entire shape
        if (index == 0)
        {
            var bounds = GetBounds();
            var centerX = (bounds.Min.X + bounds.Max.X) / 2;
            var centerY = (bounds.Min.Y + bounds.Max.Y) / 2;
            var delta = new VXYZ(newPosition.X - centerX, newPosition.Y - centerY, 0);
            Move(delta);
        }
    }

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
    public virtual double DistanceTo(VXYZ point)
    {
        // Default implementation uses bounding box center
        var bounds = GetBounds();
        var centerX = (bounds.Min.X + bounds.Max.X) / 2;
        var centerY = (bounds.Min.Y + bounds.Max.Y) / 2;
        var dx = point.X - centerX;
        var dy = point.Y - centerY;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>
    /// Checks if a point is inside this shape (for filled shapes).
    /// </summary>
    public virtual bool Contains(VXYZ point)
    {
        // Default implementation checks bounding box
        var bounds = GetBounds();
        return point.X >= bounds.Min.X && point.X <= bounds.Max.X &&
               point.Y >= bounds.Min.Y && point.Y <= bounds.Max.Y;
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Copies styling properties to another shape.
    /// </summary>
    protected void CopyStyleTo(Shape target)
    {
        target.Color = Color;
        target.FillColor = FillColor;
        target.LineWeight = LineWeight;
        target.LineType = LineType;
        target.LineTypeScale = LineTypeScale;
    }

    #endregion
}
