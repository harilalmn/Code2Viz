using System.IO;
using System.Text.Json;

namespace Code2Viz;

public class AppSettingsData
{
    public bool IncludeGridInExport { get; set; } = true;
    public string DefaultExportBackground { get; set; } = "Transparent";
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
