using System.IO;

namespace Code2Viz.Project;

public class VizCodeProject
{
    public string ProjectFilePath { get; private set; } = string.Empty;
    public string ProjectDirectory => Path.GetDirectoryName(ProjectFilePath) ?? string.Empty;
    public VizProjectFile ProjectFile { get; private set; } = new VizProjectFile();

    public List<VizCodeFile> Files { get; } = new();

    public VizCodeFile? EntryPointFile => Files.FirstOrDefault(f => f.IsEntryPoint);
    public bool HasUnsavedChanges => Files.Any(f => f.HasUnsavedChanges);

    // Expose config for compatibility (or refactor consumers)
    // Consumers accessed project.Config.Packages... now project.ProjectFile.Packages
    // I can alias it if needed, but better to update consumers.

    private VizCodeProject() { }

    public static VizCodeProject Load(string vizProjPath)
    {
        if (!File.Exists(vizProjPath))
            throw new FileNotFoundException("Project file not found", vizProjPath);

        var project = new VizCodeProject
        {
            ProjectFilePath = vizProjPath,
            ProjectFile = VizProjectFile.Load(vizProjPath)
        };

        var directory = project.ProjectDirectory;
        var vizCodeFiles = DiscoverVizCodeFiles(directory);
        
        foreach (var filePath in vizCodeFiles)
        {
            var file = new VizCodeFile
            {
                FilePath = filePath,
                Content = File.ReadAllText(filePath),
                HasUnsavedChanges = false
            };
            project.Files.Add(file);
        }

        SortFiles(project);

        // Only open the entry point file by default
        var entryPoint = project.EntryPointFile;
        if (entryPoint != null)
        {
            entryPoint.IsOpen = true;
        }

        project.ApplySettings();
        return project;
    }

    public void ApplySettings()
    {
        Geometry.ShapeDefaults.GlobalColor = ProjectFile.Settings.DefaultColor;
        Geometry.ShapeDefaults.GlobalFillColor = ProjectFile.Settings.DefaultFillColor;
        Geometry.ShapeDefaults.GlobalLineWeight = ProjectFile.Settings.DefaultLineWeight;
        Geometry.ShapeDefaults.GlobalLineTypeScale = ProjectFile.Settings.DefaultLineTypeScale;

        // Dimension style defaults
        Geometry.ShapeDefaults.DimOffset = ProjectFile.Settings.DimOffset;
        Geometry.ShapeDefaults.DimArrowSize = ProjectFile.Settings.DimArrowSize;
        Geometry.ShapeDefaults.DimTextHeight = ProjectFile.Settings.DimTextHeight;
        Geometry.ShapeDefaults.DimDecimalPlaces = ProjectFile.Settings.DimDecimalPlaces;
        Geometry.ShapeDefaults.DimExtendBeyondDimLines = ProjectFile.Settings.DimExtendBeyondDimLines;
        Geometry.ShapeDefaults.DimOffsetFromOrigin = ProjectFile.Settings.DimOffsetFromOrigin;
        Geometry.ShapeDefaults.DimPrefix = ProjectFile.Settings.DimPrefix;
        Geometry.ShapeDefaults.DimSuffix = ProjectFile.Settings.DimSuffix;
        Geometry.ShapeDefaults.DimTextBgOpaque = ProjectFile.Settings.DimTextBgOpaque;
        Geometry.ShapeDefaults.DimExtensionLineColor = ProjectFile.Settings.DimExtensionLineColor;
        Geometry.ShapeDefaults.DimDimensionLineColor = ProjectFile.Settings.DimDimensionLineColor;
        Geometry.ShapeDefaults.DimTextColor = ProjectFile.Settings.DimTextColor;
        Geometry.ShapeDefaults.DimSuppressDimensionLine = ProjectFile.Settings.DimSuppressDimensionLine;
    }

    public static VizCodeProject CreateNew(string directory, string projectName, ProjectLanguage language)
    {
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var projFileName = $"{projectName}.vizproj";
        var projPath = Path.Combine(directory, projFileName);

        var project = new VizCodeProject
        {
            ProjectFilePath = projPath,
            ProjectFile = new VizProjectFile { Name = projectName, Language = language }
        };
        
        project.SaveProjectFile();

        // Create entry point file with namespace matching project name
        var extension = language == ProjectLanguage.FSharp ? ".fs" : ".cs";
        var fileName = language == ProjectLanguage.FSharp ? "StartViz.fs" : "StartViz.cs";
        var entryPointPath = Path.Combine(directory, fileName);
        
        var content = language == ProjectLanguage.FSharp 
            ? FSharpTemplates.GetStartVizTemplate(projectName)
            : Templates.GetStartVizTemplate(projectName);

        var entryPointFile = new VizCodeFile
        {
            FilePath = entryPointPath,
            Content = content,
            HasUnsavedChanges = true,
            IsOpen = true
        };
        
        // Write it immediately so it exists on disk? 
        // Or keep purely in memory until save? 
        // CreateNew usually implies creating on disk.
        File.WriteAllText(entryPointPath, entryPointFile.Content);
        entryPointFile.HasUnsavedChanges = false;
        
        project.Files.Add(entryPointFile);

        return project;
    }

    public void SaveFile(VizCodeFile file)
    {
        if (string.IsNullOrEmpty(file.FilePath)) return;
        File.WriteAllText(file.FilePath, file.Content);
        file.HasUnsavedChanges = false;
    }

    public void SaveAllFiles()
    {
        foreach (var file in Files)
        {
            if (file.HasUnsavedChanges)
            {
                SaveFile(file);
            }
        }
    }

    public void AddFile(VizCodeFile file)
    {
        if (!Files.Contains(file)) Files.Add(file);
    }

    public void RemoveFile(VizCodeFile file)
    {
        Files.Remove(file);
        // Note: Only removes from open tabs, does NOT delete from disk
    }

    public void AddPackage(string id, string version)
    {
        if (!ProjectFile.Packages.Any(p => p.Id.Equals(id, StringComparison.OrdinalIgnoreCase)))
        {
            ProjectFile.Packages.Add(new PackageReference { Id = id, Version = version });
            SaveProjectFile();
        }
    }

    public void RemovePackage(string id)
    {
        var package = ProjectFile.Packages.FirstOrDefault(p => p.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (package != null)
        {
            ProjectFile.Packages.Remove(package);
            SaveProjectFile();
        }
    }

    public void MoveToDirectory(string newDirectory)
    {
        if (!Directory.Exists(newDirectory))
        {
            Directory.CreateDirectory(newDirectory);
        }

        var fileName = Path.GetFileName(ProjectFilePath);
        ProjectFilePath = Path.Combine(newDirectory, fileName);

        foreach (var file in Files)
        {
            // Assuming flat structure for now or preserving relative?
            // Old impl assumed flat (Path.Combine(newDirectory, file.FileName))
            var name = Path.GetFileName(file.FilePath);
            if (string.IsNullOrEmpty(name)) name = $"{Guid.NewGuid()}.cs"; // Should not happen for existing files
            file.FilePath = Path.Combine(newDirectory, name);
        }

        SaveAllFiles();
        SaveProjectFile();
    }

    public void SaveProjectFile()
    {
        ProjectFile.Save(ProjectFilePath);
    }

    private static IEnumerable<string> DiscoverVizCodeFiles(string directory)
    {
        var files = new List<string>();
        files.AddRange(Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories));
        files.AddRange(Directory.GetFiles(directory, "*.fs", SearchOption.AllDirectories));
        return files;
    }

    /// <summary>
    /// Gets all source files from the project directory for compilation.
    /// Uses in-memory content for open files, reads from disk for others.
    /// In-memory-only files (newly created via the New File dialog, not yet saved) are
    /// included too — without this, an unsaved sketch file would not be compiled.
    /// </summary>
    public IEnumerable<VizCodeFile> GetAllSourceFiles()
    {
        var allFiles = new List<VizCodeFile>();
        var addedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1. Include all in-memory files first. This covers unsaved IsNew files whose
        //    FilePath points to a location not yet on disk.
        foreach (var file in Files)
        {
            if (string.IsNullOrEmpty(file.FilePath)) continue;
            if (addedPaths.Add(file.FilePath))
                allFiles.Add(file);
        }

        // 2. Then walk the project directory and pull in any disk files we haven't
        //    already added from memory.
        foreach (var filePath in DiscoverVizCodeFiles(ProjectDirectory))
        {
            if (!addedPaths.Add(filePath)) continue;
            try
            {
                allFiles.Add(new VizCodeFile
                {
                    FilePath = filePath,
                    Content = File.ReadAllText(filePath),
                    HasUnsavedChanges = false
                });
            }
            catch
            {
                // Skip files that can't be read
            }
        }

        return allFiles;
    }

    /// <summary>
    /// Refreshes the Files list to match what's on disk.
    /// Adds new files, removes deleted files, preserves unsaved changes.
    /// </summary>
    public void RefreshFilesFromDisk()
    {
        var discoveredPaths = DiscoverVizCodeFiles(ProjectDirectory).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Remove files that no longer exist on disk (unless they have unsaved changes)
        var filesToRemove = Files
            .Where(f => !discoveredPaths.Contains(f.FilePath) && !f.HasUnsavedChanges && !f.IsNew)
            .ToList();
        foreach (var file in filesToRemove)
        {
            Files.Remove(file);
        }

        // Add new files that aren't already loaded
        foreach (var filePath in discoveredPaths)
        {
            if (!Files.Any(f => f.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase)))
            {
                try
                {
                    Files.Add(new VizCodeFile
                    {
                        FilePath = filePath,
                        Content = File.ReadAllText(filePath),
                        HasUnsavedChanges = false
                    });
                }
                catch
                {
                    // Skip files that can't be read
                }
            }
        }

        SortFiles(this);
    }

    private static void SortFiles(VizCodeProject project)
    {
         project.Files.Sort((a, b) =>
        {
            if (a.IsEntryPoint) return -1;
            if (b.IsEntryPoint) return 1;
            return string.Compare(a.FileName, b.FileName, StringComparison.OrdinalIgnoreCase);
        });
    }
}
