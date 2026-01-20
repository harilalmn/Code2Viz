namespace Code2Viz.Geometry;

/// <summary>
/// Common color names for use with shape StrokeColor and FillColor properties.
/// </summary>
public enum ColorName
{
    // Basic colors
    Red, Green, Blue, Yellow, Orange, Purple, Pink, Cyan, Magenta, White, Black, Gray,

    // Extended colors
    Brown, Coral, Crimson, DarkBlue, DarkGreen, DarkRed, DarkOrange, DarkViolet,
    DeepPink, DeepSkyBlue, DodgerBlue, ForestGreen, Fuchsia, Gold, GreenYellow,
    HotPink, IndianRed, Indigo, Khaki, Lavender, LawnGreen, LightBlue, LightCoral,
    LightGreen, LightPink, LightSalmon, LightSeaGreen, LightSkyBlue, LightYellow,
    Lime, LimeGreen, Maroon, MediumBlue, MediumOrchid, MediumPurple, MediumSeaGreen,
    MediumSlateBlue, MediumSpringGreen, MediumTurquoise, MediumVioletRed, MidnightBlue,
    Navy, Olive, OliveDrab, OrangeRed, Orchid, PaleGreen, PaleTurquoise, PaleVioletRed,
    Peru, Plum, RoyalBlue, Salmon, SandyBrown, SeaGreen, Sienna, Silver, SkyBlue,
    SlateBlue, SlateGray, SpringGreen, SteelBlue, Tan, Teal, Thistle, Tomato,
    Turquoise, Violet, Wheat, YellowGreen
}

/// <summary>
/// Static color utility class for easy color access and random color generation.
/// </summary>
public static class VColor
{
    private static readonly Random _random = new();

    // Basic colors
    public static string Red => "Red";
    public static string Green => "Green";
    public static string Blue => "Blue";
    public static string Yellow => "Yellow";
    public static string Orange => "Orange";
    public static string Purple => "Purple";
    public static string Pink => "Pink";
    public static string Cyan => "Cyan";
    public static string Magenta => "Magenta";
    public static string White => "White";
    public static string Black => "Black";
    public static string Gray => "Gray";

    // Extended colors
    public static string Brown => "Brown";
    public static string Coral => "Coral";
    public static string Crimson => "Crimson";
    public static string DarkBlue => "DarkBlue";
    public static string DarkGreen => "DarkGreen";
    public static string DarkRed => "DarkRed";
    public static string DarkOrange => "DarkOrange";
    public static string DarkViolet => "DarkViolet";
    public static string DeepPink => "DeepPink";
    public static string DeepSkyBlue => "DeepSkyBlue";
    public static string DodgerBlue => "DodgerBlue";
    public static string ForestGreen => "ForestGreen";
    public static string Fuchsia => "Fuchsia";
    public static string Gold => "Gold";
    public static string GreenYellow => "GreenYellow";
    public static string HotPink => "HotPink";
    public static string IndianRed => "IndianRed";
    public static string Indigo => "Indigo";
    public static string Khaki => "Khaki";
    public static string Lavender => "Lavender";
    public static string LawnGreen => "LawnGreen";
    public static string LightBlue => "LightBlue";
    public static string LightCoral => "LightCoral";
    public static string LightGreen => "LightGreen";
    public static string LightPink => "LightPink";
    public static string LightSalmon => "LightSalmon";
    public static string LightSeaGreen => "LightSeaGreen";
    public static string LightSkyBlue => "LightSkyBlue";
    public static string LightYellow => "LightYellow";
    public static string Lime => "Lime";
    public static string LimeGreen => "LimeGreen";
    public static string Maroon => "Maroon";
    public static string MediumBlue => "MediumBlue";
    public static string MediumOrchid => "MediumOrchid";
    public static string MediumPurple => "MediumPurple";
    public static string MediumSeaGreen => "MediumSeaGreen";
    public static string MediumSlateBlue => "MediumSlateBlue";
    public static string MediumSpringGreen => "MediumSpringGreen";
    public static string MediumTurquoise => "MediumTurquoise";
    public static string MediumVioletRed => "MediumVioletRed";
    public static string MidnightBlue => "MidnightBlue";
    public static string Navy => "Navy";
    public static string Olive => "Olive";
    public static string OliveDrab => "OliveDrab";
    public static string OrangeRed => "OrangeRed";
    public static string Orchid => "Orchid";
    public static string PaleGreen => "PaleGreen";
    public static string PaleTurquoise => "PaleTurquoise";
    public static string PaleVioletRed => "PaleVioletRed";
    public static string Peru => "Peru";
    public static string Plum => "Plum";
    public static string RoyalBlue => "RoyalBlue";
    public static string Salmon => "Salmon";
    public static string SandyBrown => "SandyBrown";
    public static string SeaGreen => "SeaGreen";
    public static string Sienna => "Sienna";
    public static string Silver => "Silver";
    public static string SkyBlue => "SkyBlue";
    public static string SlateBlue => "SlateBlue";
    public static string SlateGray => "SlateGray";
    public static string SpringGreen => "SpringGreen";
    public static string SteelBlue => "SteelBlue";
    public static string Tan => "Tan";
    public static string Teal => "Teal";
    public static string Thistle => "Thistle";
    public static string Tomato => "Tomato";
    public static string Turquoise => "Turquoise";
    public static string Violet => "Violet";
    public static string Wheat => "Wheat";
    public static string YellowGreen => "YellowGreen";

    // Vibrant colors (good for dark backgrounds)
    private static readonly string[] VibrantColors =
    {
        "Red", "Green", "Blue", "Yellow", "Orange", "Purple", "Cyan", "Magenta",
        "Lime", "LimeGreen", "DeepSkyBlue", "HotPink", "Gold", "Coral", "Crimson",
        "DodgerBlue", "SpringGreen", "Tomato", "Turquoise", "Violet", "OrangeRed",
        "GreenYellow", "Fuchsia", "Aqua", "Chartreuse"
    };

    // Pastel colors (softer, good for fills)
    private static readonly string[] PastelColors =
    {
        "LightBlue", "LightGreen", "LightPink", "LightCoral", "LightSalmon",
        "LightSkyBlue", "LightYellow", "Lavender", "PaleGreen", "PaleTurquoise",
        "PaleVioletRed", "Thistle", "Wheat", "Khaki", "MistyRose", "PeachPuff",
        "LemonChiffon", "Honeydew", "AliceBlue", "LavenderBlush", "Cornsilk",
        "Beige", "AntiqueWhite", "PapayaWhip", "BlanchedAlmond"
    };

    /// <summary>
    /// Gets a random color string.
    /// </summary>
    /// <param name="returnPastelColor">If true, returns softer pastel colors; if false, returns vibrant colors.</param>
    /// <returns>A color name string that can be used for StrokeColor or FillColor.</returns>
    public static string GetRandomColor(bool returnPastelColor = true)
    {
        var colors = returnPastelColor ? PastelColors : VibrantColors;
        return colors[_random.Next(colors.Length)];
    }

    /// <summary>
    /// Gets a random vibrant color (good for strokes on dark backgrounds).
    /// </summary>
    public static string GetRandomVibrantColor() => GetRandomColor(false);

    /// <summary>
    /// Gets a random pastel color (good for fills).
    /// </summary>
    public static string GetRandomPastelColor() => GetRandomColor(true);

    /// <summary>
    /// Converts a ColorName enum value to its string representation.
    /// </summary>
    public static string FromEnum(ColorName color) => color.ToString();

    /// <summary>
    /// Creates a color string from RGB values (0-255).
    /// </summary>
    public static string FromRgb(int r, int g, int b) => $"#{r:X2}{g:X2}{b:X2}";

    /// <summary>
    /// Creates a color string from ARGB values (0-255).
    /// </summary>
    public static string FromArgb(int a, int r, int g, int b) => $"#{a:X2}{r:X2}{g:X2}{b:X2}";

    /// <summary>
    /// Creates a semi-transparent color from RGB values.
    /// </summary>
    /// <param name="r">Red (0-255)</param>
    /// <param name="g">Green (0-255)</param>
    /// <param name="b">Blue (0-255)</param>
    /// <param name="opacity">Opacity from 0.0 (transparent) to 1.0 (opaque)</param>
    public static string WithOpacity(int r, int g, int b, double opacity)
    {
        var alpha = (int)(Math.Clamp(opacity, 0, 1) * 255);
        return $"#{alpha:X2}{r:X2}{g:X2}{b:X2}";
    }

    /// <summary>
    /// Gets a list of all vibrant color names.
    /// </summary>
    public static string[] GetVibrantColors() => (string[])VibrantColors.Clone();

    /// <summary>
    /// Gets a list of all pastel color names.
    /// </summary>
    public static string[] GetPastelColors() => (string[])PastelColors.Clone();
}
