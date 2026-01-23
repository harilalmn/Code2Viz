using System.IO;
using System.Text.Json;

namespace Code2Viz;

public class AppSettingsData
{
    public bool IncludeGridInExport { get; set; } = true;
    public string DefaultExportBackground { get; set; } = "Transparent";
    public bool ShowGrid { get; set; } = true;
    public bool ZoomToFitOnRun { get; set; } = false;

    // Window Visibility Settings
    public bool ShowProjectBrowser { get; set; } = false;
    public bool ShowOutliner { get; set; } = false;
    public bool ShowTimeline { get; set; } = false;
    public bool ShowToolbar { get; set; } = false;
    public bool ShowConsole { get; set; } = true;

    // Snap Settings
    public bool SnapEndpointEnabled { get; set; } = true;
    public bool SnapMidpointEnabled { get; set; } = true;
    public bool SnapCenterEnabled { get; set; } = true;
    public bool SnapIntersectionEnabled { get; set; } = true;
    public bool SnapNearestEnabled { get; set; } = true;
    public bool SnapPerpendicularEnabled { get; set; } = true;
    public bool SnapExtensionEnabled { get; set; } = true;
    public bool SnapTangentEnabled { get; set; } = true;

    // Highlight Settings (for Outliner hover)
    public string HighlightColor { get; set; } = "Yellow";
    public int HighlightOpacity { get; set; } = 40; // 0-100 percentage
}

public static class ApplicationSettings
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
        "Viz2d", 
        "appsettings.json");

    public static AppSettingsData Instance { get; private set; } = new();

    static ApplicationSettings()
    {
        Load();
    }

    public static void Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                Instance = JsonSerializer.Deserialize<AppSettingsData>(json) ?? new AppSettingsData();
            }
        }
        catch { /* Ignore errors, use defaults */ }
    }

    public static void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(SettingsPath);
            if (!Directory.Exists(dir) && dir != null) Directory.CreateDirectory(dir);

            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(Instance, options);
            File.WriteAllText(SettingsPath, json);
        }
        catch { /* Ignore errors */ }
    }
}
