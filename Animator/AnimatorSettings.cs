using System.IO;
using System.Text.Json;

namespace Animator;

public class AnimatorSettingsData
{
    public bool InlayHintsEnabled { get; set; } = false;
    public bool SemanticHighlightingEnabled { get; set; } = true;
    public bool CodeLensEnabled { get; set; } = true;
    public bool MinimapVisible { get; set; } = true;
    public bool ConsoleVisible { get; set; } = true;
    public double ConsoleHeight { get; set; } = 180;
}

/// <summary>
/// Persists Animator's View-menu toggles across runs. Mirrors Code2Viz's
/// <c>ApplicationSettings</c> but uses its own JSON file under
/// <c>%AppData%\Code2Viz\animator_settings.json</c> so the two apps' UI states
/// stay independent.
/// </summary>
public static class AnimatorSettings
{
    private static readonly string SettingsPath = Path.Combine(
        System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
        "Code2Viz",
        "animator_settings.json");

    public static AnimatorSettingsData Instance { get; private set; } = new();

    static AnimatorSettings()
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
                Instance = JsonSerializer.Deserialize<AnimatorSettingsData>(json) ?? new AnimatorSettingsData();
            }
        }
        catch { /* fall through to defaults */ }
    }

    public static void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(SettingsPath);
            if (!Directory.Exists(dir) && dir != null) Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(Instance, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
        }
        catch { /* swallow — settings persistence is best-effort */ }
    }
}
