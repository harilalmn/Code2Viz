using System.IO;
using System.Text.Json;

namespace Code2Viz.Project;

public class RecentAnimation
{
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public DateTime LastOpened { get; set; } = DateTime.Now;
}

public static class RecentAnimationsManager
{
    private const int MaxRecentAnimations = 10;
    private static readonly string SettingsPath;
    private static List<RecentAnimation> _recentAnimations = new();

    static RecentAnimationsManager()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var folder = System.IO.Path.Combine(appData, "Code2Viz");
        Directory.CreateDirectory(folder);
        SettingsPath = System.IO.Path.Combine(folder, "recent_animations.json");
        Load();
    }

    public static IReadOnlyList<RecentAnimation> GetRecentAnimations() => _recentAnimations.AsReadOnly();

    public static void AddAnimation(string filePath, string? name = null)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            return;

        var displayName = name ?? System.IO.Path.GetFileNameWithoutExtension(filePath);

        _recentAnimations.RemoveAll(p => p.Path.Equals(filePath, StringComparison.OrdinalIgnoreCase));

        _recentAnimations.Insert(0, new RecentAnimation
        {
            Name = displayName,
            Path = filePath,
            LastOpened = DateTime.Now
        });

        if (_recentAnimations.Count > MaxRecentAnimations)
            _recentAnimations = _recentAnimations.Take(MaxRecentAnimations).ToList();

        Save();
    }

    public static void RemoveAnimation(string filePath)
    {
        _recentAnimations.RemoveAll(p => p.Path.Equals(filePath, StringComparison.OrdinalIgnoreCase));
        Save();
    }

    public static void Clear()
    {
        _recentAnimations.Clear();
        Save();
    }

    private static void Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                _recentAnimations = JsonSerializer.Deserialize<List<RecentAnimation>>(json) ?? new();
                _recentAnimations.RemoveAll(p => !File.Exists(p.Path));
            }
        }
        catch
        {
            _recentAnimations = new();
        }
    }

    private static void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(_recentAnimations, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
        }
        catch
        {
            // Ignore save errors
        }
    }
}
