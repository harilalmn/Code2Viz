using System;

namespace Code2Viz.Geometry;

/// <summary>
/// Holds global default settings for shapes.
/// These values are populated from the Project Settings.
/// </summary>
public static class ShapeDefaults
{
    /// <summary>
    /// Global default stroke color. If null, shapes use their specific defaults.
    /// </summary>
    public static string? GlobalStrokeColor { get; set; } = null;

    /// <summary>
    /// Global default fill color. If null, shapes use their specific defaults.
    /// </summary>
    public static string? GlobalFillColor { get; set; } = null;

    /// <summary>
    /// Global default stroke thickness. If null, shapes use default (usually 2.0).
    /// </summary>
    public static double? GlobalStrokeThickness { get; set; } = null;

    /// <summary>
    /// Resets defaults to initial state (nulls).
    /// </summary>
    public static void Reset()
    {
        GlobalStrokeColor = null;
        GlobalFillColor = null;
        GlobalStrokeThickness = null;
    }
}
