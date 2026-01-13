using System.IO;
using System.Text.Json;

namespace Code2Viz.Project;

public class PackageReference
{
    public string Id { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;

    public override string ToString() => $"{Id} ({Version})";
}

public class AssemblyReference
{
    /// <summary>
    /// Path to the DLL (for local references) or assembly name (for framework references).
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// True for .NET framework assemblies that should be resolved from trusted assemblies.
    /// False for local DLL files.
    /// </summary>
    public bool IsFramework { get; set; }

    public override string ToString() => IsFramework ? Path : System.IO.Path.GetFileName(Path);
}


public enum ProjectLanguage
{
    CSharp,
    FSharp
}

public class VizProjectFile
{
    public string Name { get; set; } = "New Project";
    public ProjectLanguage Language { get; set; } = ProjectLanguage.CSharp;
    public List<PackageReference> Packages { get; set; } = new();
    public List<AssemblyReference> References { get; set; } = new();
    public ProjectSettings Settings { get; set; } = new();

    public static VizProjectFile Load(string path)
    {
        if (!File.Exists(path)) return new VizProjectFile();
        var json = File.ReadAllText(path);
        var project = JsonSerializer.Deserialize<VizProjectFile>(json) ?? new VizProjectFile();
        return project;
    }

    public void Save(string path)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(this, options);
        File.WriteAllText(path, json);
    }
}

public class ProjectSettings
{
    public string? DefaultStrokeColor { get; set; }
    public string? DefaultFillColor { get; set; }
    public string? DefaultCanvasBackgroundColor { get; set; }
    public string? DefaultExportBackground { get; set; } // "Transparent", "Canvas", "White", "Black", "Light"
    public double? DefaultStrokeThickness { get; set; }
}
