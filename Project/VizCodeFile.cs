using System.IO;

namespace Code2Viz.Project;

public enum VizFileKind
{
    Module,
    MainEntry,
    Sketch
}

public class VizCodeFile
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName => Path.GetFileName(FilePath);
    public string Content { get; set; } = string.Empty;
    public bool HasUnsavedChanges { get; set; }
    public bool IsNew { get; set; }
    public bool IsOpen { get; set; } = false;

    private VizFileKind? _kind;
    public VizFileKind Kind
    {
        get => _kind ?? InferKindFromFileName(FileName);
        set => _kind = value;
    }

    public bool IsEntryPoint => Kind == VizFileKind.MainEntry || Kind == VizFileKind.Sketch;
    public bool IsSketch => Kind == VizFileKind.Sketch;

    private static VizFileKind InferKindFromFileName(string fileName)
    {
        if (fileName.StartsWith("StartSketch", StringComparison.OrdinalIgnoreCase)
            && (fileName.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                || fileName.EndsWith(".fs", StringComparison.OrdinalIgnoreCase)))
            return VizFileKind.Sketch;
        if (fileName.Equals("StartViz.cs", StringComparison.OrdinalIgnoreCase)
            || fileName.Equals("StartViz.fs", StringComparison.OrdinalIgnoreCase))
            return VizFileKind.MainEntry;
        return VizFileKind.Module;
    }
}
