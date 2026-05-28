namespace C2VGeometry;

/// <summary>
/// Configuration for charts produced by <see cref="Chart"/>. All properties are optional;
/// sensible defaults are applied where unset. Axis ranges default to auto-fit from the
/// data when <see cref="XMin"/>/<see cref="XMax"/>/<see cref="YMin"/>/<see cref="YMax"/>
/// are left null.
/// </summary>
public class ChartOptions
{
    /// <summary>Bottom-left corner of the plot area in world coordinates. Default: (0, 0).</summary>
    public VXYZ Origin { get; set; } = new VXYZ(0, 0);

    /// <summary>Width of the plot area in world units. Default: 400.</summary>
    public double Width { get; set; } = 400;

    /// <summary>Height of the plot area in world units. Default: 250.</summary>
    public double Height { get; set; } = 250;

    /// <summary>Chart title rendered above the plot area. Null/empty hides the title.</summary>
    public string? Title { get; set; }

    /// <summary>X-axis label rendered below the plot. Null/empty hides it.</summary>
    public string? XAxisTitle { get; set; }

    /// <summary>Y-axis label rendered to the left of the plot (rotated 90°). Null/empty hides it.</summary>
    public string? YAxisTitle { get; set; }

    /// <summary>Minimum X value. Null = auto-fit from data.</summary>
    public double? XMin { get; set; }

    /// <summary>Maximum X value. Null = auto-fit from data.</summary>
    public double? XMax { get; set; }

    /// <summary>Minimum Y value. Null = auto-fit from data (with small padding).</summary>
    public double? YMin { get; set; }

    /// <summary>Maximum Y value. Null = auto-fit from data (with small padding).</summary>
    public double? YMax { get; set; }

    /// <summary>Approximate number of ticks on the X axis. Default: 6.</summary>
    public int XTickCount { get; set; } = 6;

    /// <summary>Approximate number of ticks on the Y axis. Default: 6.</summary>
    public int YTickCount { get; set; } = 6;

    /// <summary>Show light gridlines behind the chart. Default: true.</summary>
    public bool ShowGrid { get; set; } = true;

    /// <summary>Show a legend (only honored when chart provides one). Default: false.</summary>
    public bool ShowLegend { get; set; } = false;

    /// <summary>Rotation of X tick labels in degrees (useful for long category names). Default: 0.</summary>
    public double XLabelRotation { get; set; } = 0;

    /// <summary>Font size for axis tick labels and legend entries. Default: 10.</summary>
    public double LabelFontSize { get; set; } = 10;

    /// <summary>Font size for the chart title. Default: 14.</summary>
    public double TitleFontSize { get; set; } = 14;

    /// <summary>Stroke color for axes. Default: "White".</summary>
    public string AxisColor { get; set; } = "White";

    /// <summary>Stroke color for gridlines. Default: "DimGray".</summary>
    public string GridColor { get; set; } = "DimGray";

    /// <summary>Color for all text (labels, title). Default: "White".</summary>
    public string TextColor { get; set; } = "White";

    /// <summary>
    /// Color palette used cyclically for series / bars / pie slices.
    /// Default: a 10-color qualitative palette.
    /// </summary>
    public string[] Palette { get; set; } = new[]
    {
        "DodgerBlue", "Tomato", "MediumSeaGreen", "Gold", "MediumOrchid",
        "Turquoise", "Coral", "SteelBlue", "Khaki", "HotPink"
    };

    /// <summary>Decimal places shown in numeric tick labels. Default: auto (use ToString).</summary>
    public int? TickDecimalPlaces { get; set; }
}
