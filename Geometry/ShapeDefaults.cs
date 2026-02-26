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
    public static string? GlobalColor { get; set; } = null;

    /// <summary>
    /// Global default fill color. If null, shapes use their specific defaults.
    /// </summary>
    public static string? GlobalFillColor { get; set; } = null;

    /// <summary>
    /// Global default stroke thickness. If null, shapes use default (usually 2.0).
    /// </summary>
    public static double? GlobalLineWeight { get; set; } = null;

    /// <summary>
    /// Global default stroke style. If null, shapes use default (Continuous).
    /// </summary>
    public static LineType? GlobalLineType { get; set; } = null;

    /// <summary>
    /// Global default stroke style scale. If null, shapes use default (1.0).
    /// Controls the scale of dash patterns (dash length, gap size).
    /// </summary>
    public static double? GlobalLineTypeScale { get; set; } = null;

    // Dimension style defaults
    public static double? DimOffset { get; set; } = null;
    public static double? DimArrowSize { get; set; } = null;
    public static double? DimTextHeight { get; set; } = null;
    public static int? DimDecimalPlaces { get; set; } = null;
    public static double? DimExtendBeyondDimLines { get; set; } = null;
    public static double? DimOffsetFromOrigin { get; set; } = null;
    public static string? DimPrefix { get; set; } = null;
    public static string? DimSuffix { get; set; } = null;
    public static bool? DimTextBgOpaque { get; set; } = null;

    /// <summary>
    /// Resets defaults to initial state (nulls).
    /// </summary>
    public static void Reset()
    {
        GlobalColor = null;
        GlobalFillColor = null;
        GlobalLineWeight = null;
        GlobalLineType = null;
        GlobalLineTypeScale = null;
        DimOffset = null;
        DimArrowSize = null;
        DimTextHeight = null;
        DimDecimalPlaces = null;
        DimExtendBeyondDimLines = null;
        DimOffsetFromOrigin = null;
        DimPrefix = null;
        DimSuffix = null;
        DimTextBgOpaque = null;
    }
}
