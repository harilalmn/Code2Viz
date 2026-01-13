using System.IO;
using System.Text.Json;

namespace Code2Viz.Project;

public class RecentProject
{
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public DateTime LastOpened { get; set; } = DateTime.Now;
}

public static class RecentProjectsManager
{
    private const int MaxRecentProjects = 10;
    private static readonly string SettingsPath;
    private static List<RecentProject> _recentProjects = new();

    static RecentProjectsManager()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var viz2dFolder = System.IO.Path.Combine(appData, "Code2Viz");
        Directory.CreateDirectory(viz2dFolder);
        SettingsPath = System.IO.Path.Combine(viz2dFolder, "recent_projects.json");
        Load();
    }

    public static IReadOnlyList<RecentProject> GetRecentProjects() => _recentProjects.AsReadOnly();

    public static void AddProject(string projectPath, string? projectName = null)
    {
        if (string.IsNullOrEmpty(projectPath) || !File.Exists(projectPath))
            return;

        var name = projectName ?? System.IO.Path.GetFileNameWithoutExtension(projectPath);

        // Remove existing entry with same path
        _recentProjects.RemoveAll(p => p.Path.Equals(projectPath, StringComparison.OrdinalIgnoreCase));

        // Add to top
        _recentProjects.Insert(0, new RecentProject
        {
            Name = name,
            Path = projectPath,
            LastOpened = DateTime.Now
        });

        // Keep only max entries
        if (_recentProjects.Count > MaxRecentProjects)
            _recentProjects = _recentProjects.Take(MaxRecentProjects).ToList();

        Save();
    }

    public static void RemoveProject(string projectPath)
    {
        _recentProjects.RemoveAll(p => p.Path.Equals(projectPath, StringComparison.OrdinalIgnoreCase));
        Save();
    }

    public static void Clear()
    {
        _recentProjects.Clear();
        Save();
    }

    private static void Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                _recentProjects = JsonSerializer.Deserialize<List<RecentProject>>(json) ?? new();

                // Remove projects that no longer exist
                _recentProjects.RemoveAll(p => !File.Exists(p.Path));
            }
        }
        catch
        {
            _recentProjects = new();
        }
    }

    private static void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(_recentProjects, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
        }
        catch
        {
            // Ignore save errors
        }
    }
}
