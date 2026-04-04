using System;

namespace C2VGeometry;

/// <summary>
/// Represents an axis-aligned bounding box defined by minimum and maximum corner points.
/// Similar to Revit API's BoundingBoxXYZ.
/// </summary>
public class BoundingBox
{
    /// <summary>
    /// The minimum corner point (lower-left) of the bounding box.
    /// </summary>
    public VXYZ Min { get; }

    /// <summary>
    /// The maximum corner point (upper-right) of the bounding box.
    /// </summary>
    public VXYZ Max { get; }

    /// <summary>
    /// Creates a new bounding box from minimum and maximum corner points.
    /// </summary>
    /// <param name="min">The minimum corner point (lower-left).</param>
    /// <param name="max">The maximum corner point (upper-right).</param>
    public BoundingBox(VXYZ min, VXYZ max)
    {
        Min = min;
        Max = max;
    }

    /// <summary>
    /// Gets the width of the bounding box (X extent).
    /// </summary>
    public double Width => Max.X - Min.X;

    /// <summary>
    /// Gets the height of the bounding box (Y extent).
    /// </summary>
    public double Height => Max.Y - Min.Y;

    /// <summary>
    /// Gets the center point of the bounding box.
    /// </summary>
    public VXYZ Center => new VXYZ((Min.X + Max.X) / 2, (Min.Y + Max.Y) / 2);

    /// <summary>
    /// Gets the area of the bounding box.
    /// </summary>
    public double Area => Width * Height;

    /// <summary>
    /// Checks if this bounding box contains a point.
    /// </summary>
    /// <param name="point">The point to check.</param>
    /// <returns>True if the point is inside or on the boundary of the bounding box.</returns>
    public bool Contains(VXYZ point)
    {
        return point.X >= Min.X && point.X <= Max.X &&
               point.Y >= Min.Y && point.Y <= Max.Y;
    }

    /// <summary>
    /// Checks if this bounding box intersects with another bounding box.
    /// </summary>
    /// <param name="other">The other bounding box to check.</param>
    /// <returns>True if the bounding boxes overlap.</returns>
    public bool Intersects(BoundingBox other)
    {
        return !(other.Max.X < Min.X || other.Min.X > Max.X ||
                 other.Max.Y < Min.Y || other.Min.Y > Max.Y);
    }

    /// <summary>
    /// Creates a new bounding box that contains both this and another bounding box.
    /// </summary>
    /// <param name="other">The other bounding box to union with.</param>
    /// <returns>A new bounding box that encompasses both.</returns>
    public BoundingBox Union(BoundingBox other)
    {
        return new BoundingBox(
            new VXYZ(Math.Min(Min.X, other.Min.X), Math.Min(Min.Y, other.Min.Y)),
            new VXYZ(Math.Max(Max.X, other.Max.X), Math.Max(Max.Y, other.Max.Y))
        );
    }

    /// <summary>
    /// Creates a new bounding box expanded by the specified distance in all directions.
    /// </summary>
    /// <param name="distance">The distance to expand (negative values contract).</param>
    /// <returns>A new expanded bounding box.</returns>
    public BoundingBox Expand(double distance)
    {
        return new BoundingBox(
            new VXYZ(Min.X - distance, Min.Y - distance),
            new VXYZ(Max.X + distance, Max.Y + distance)
        );
    }

    /// <summary>
    /// Deconstructs the bounding box into min and max points for tuple-style access.
    /// </summary>
    public void Deconstruct(out VXYZ min, out VXYZ max)
    {
        min = Min;
        max = Max;
    }

    public override string ToString() => $"BoundingBox(Min: {Min}, Max: {Max})";
}
