using System.IO;

namespace Code2Viz.Project;

public class VizCodeFile
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName => Path.GetFileName(FilePath);
    public string Content { get; set; } = string.Empty;
    public bool HasUnsavedChanges { get; set; }
    public bool IsNew { get; set; }
    public bool IsEntryPoint => FileName.Equals("StartViz.cs", StringComparison.OrdinalIgnoreCase)
                               || FileName.Equals("StartViz.fs", StringComparison.OrdinalIgnoreCase);
}
