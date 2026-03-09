using System.IO;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Code2Viz.Project;
using FSharp.Compiler.CodeAnalysis;
using FSharp.Compiler.Text;
using Code2Viz.Console;

namespace Code2Viz.Execution;

public class FSharpModuleCompiler
{
    private static readonly FSharpChecker Checker = FSharpChecker.Create(
        projectCacheSize: null,
        keepAssemblyContents: null,
        keepAllBackgroundResolutions: null,
        legacyReferenceResolver: null,
        tryGetMetadataSnapshot: null,
        suggestNamesForErrors: null,
        keepAllBackgroundSymbolUses: null,
        enableBackgroundItemKeyStoreAndSemanticClassification: null,
        enablePartialTypeChecking: null,
        parallelReferenceResolution: null,
        captureIdentifiersWhenParsing: null,
        documentSource: null,
        useTransparentCompiler: null,
        useSyntaxTreeCache: null
    );

    public async Task<CompilationResult> CompileAndExecuteAsync(VizCodeProject project)
    {
        try
        {
            // Clear previous shapes and console
            Code2Viz.Canvas.CanvasRenderer.Instance.Clear();
            ConsoleOutput.Instance.Clear();

            var tempDir = Path.Combine(Path.GetTempPath(), "Code2Viz_FSharp_Build", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                // Get ALL source files from project directory (not just open ones)
                var allSourceFiles = project.GetAllSourceFiles().ToList();

                // Write files to temp directory, preserving folder structure
                // F# requires files in dependency order:
                // 1. VizDsl.fs (built-in DSL) - FIRST
                // 2. User modules (non-StartViz files)
                // 3. StartViz.fs - LAST (entry point)
                var sourceFiles = new List<string>();
                string? startVizPath = null;

                // Write VizDsl.fs first (built-in functional DSL module)
                var vizDslPath = Path.Combine(tempDir, "VizDsl.fs");
                var vizDslContent = GetEmbeddedVizDsl();
                File.WriteAllText(vizDslPath, vizDslContent);
                sourceFiles.Add(vizDslPath);

                foreach (var file in allSourceFiles)
                {
                    // Preserve relative path structure to avoid name collisions
                    var relativePath = Path.GetRelativePath(project.ProjectDirectory, file.FilePath);
                    var tempPath = Path.Combine(tempDir, relativePath);

                    // Ensure directory exists
                    var tempFileDir = Path.GetDirectoryName(tempPath);
                    if (!string.IsNullOrEmpty(tempFileDir) && !Directory.Exists(tempFileDir))
                    {
                        Directory.CreateDirectory(tempFileDir);
                    }

                    // Convert tabs to 4 spaces (F# doesn't allow tabs)
                    var content = file.Content.Replace("\t", "    ");
                    File.WriteAllText(tempPath, content);

                    // StartViz.fs is the entry point, must be compiled last
                    var fileName = Path.GetFileName(file.FilePath);
                    if (fileName.Equals("StartViz.fs", StringComparison.OrdinalIgnoreCase))
                    {
                        startVizPath = tempPath;
                    }
                    else
                    {
                        sourceFiles.Add(tempPath);
                    }
                }

                // Add StartViz.fs at the end
                if (startVizPath != null)
                {
                    sourceFiles.Add(startVizPath);
                }

                // Prepare arguments
                var dllName = $"{project.ProjectFile.Name ?? "Output"}.dll";
                var dllPath = Path.Combine(tempDir, dllName);

                var args = new List<string>
                {
                    "fsc.exe",
                    "-o", dllPath,
                    "-a", // Library
                    "--target:library",
                    "--debug+",
                    "--optimize-",
                    "--targetprofile:netcore"
                };

                // Add references
                var refs = await GetReferencesAsync(project);
                foreach (var r in refs)
                {
                    args.Add($"-r:{r}");
                }

                // Add source files
                args.AddRange(sourceFiles);

                // Compile
                var compileAsync = Checker.Compile(args.ToArray(), userOpName: null);
                var results = Microsoft.FSharp.Control.FSharpAsync.RunSynchronously(compileAsync, null, null);

                if (results.Item2 != 0) // Exit code
                {
                    var errorMsg = string.Join("\n", results.Item1.Select(e => $"{e.FileName}({e.StartLine},{e.StartColumn}): {e.Severity} {e.ErrorNumber}: {e.Message}"));
                    
                    return new CompilationResult
                    {
                        Success = false,
                        Error = "Compilation Error:\n" + errorMsg
                    };
                }

                // Read assembly into memory
                using var fs = File.OpenRead(dllPath);
                using var ms = new MemoryStream();
                await fs.CopyToAsync(ms);
                ms.Seek(0, SeekOrigin.Begin);

                // Set working directory to project folder so relative paths resolve correctly
                var previousDirectory = Environment.CurrentDirectory;
                if (!string.IsNullOrEmpty(project.ProjectDirectory))
                    Environment.CurrentDirectory = project.ProjectDirectory;

                // Execute
                try
                {
                    return await ModuleCompiler.ExecuteAssemblyAsync(ms, null, refs, project.ProjectFile.Name ?? "MyProject");
                }
                finally
                {
                    Environment.CurrentDirectory = previousDirectory;
                }
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
        catch (Exception ex)
        {
             return new CompilationResult
            {
                Success = false,
                Error = $"Error: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Check for syntax errors without compiling/executing.
    /// </summary>
    public async Task<CompilationResult> CheckSyntaxAsync(VizCodeProject project)
    {
        try
        {
            var allSourceFiles = project.GetAllSourceFiles().ToList();
            var diagnostics = new List<FSharpDiagnosticInfo>();

            foreach (var file in allSourceFiles)
            {
                // Convert tabs to spaces for F#
                var content = file.Content.Replace("\t", "    ");
                var sourceText = SourceText.ofString(content);

                // Parse and check for errors
                var parseResults = Checker.ParseFile(
                    file.FilePath,
                    sourceText,
                    FSharpParsingOptions.Default,
                    userOpName: null,
                    cache: null
                );

                var parsed = Microsoft.FSharp.Control.FSharpAsync.RunSynchronously(parseResults, null, null);

                foreach (var error in parsed.Diagnostics)
                {
                    diagnostics.Add(new FSharpDiagnosticInfo
                    {
                        FilePath = file.FilePath,
                        StartLine = error.StartLine,
                        StartColumn = error.StartColumn,
                        EndLine = error.EndLine,
                        EndColumn = error.EndColumn,
                        Message = error.Message,
                        Severity = error.Severity.IsError ? "Error" : "Warning",
                        ErrorNumber = error.ErrorNumber.ToString()
                    });
                }
            }

            var errors = diagnostics.Where(d => d.Severity == "Error").ToList();

            return new CompilationResult
            {
                Success = errors.Count == 0,
                Error = errors.Count > 0 ? $"{errors.Count} error(s)" : null,
                FSharpDiagnostics = diagnostics
            };
        }
        catch (Exception ex)
        {
            return new CompilationResult
            {
                Success = false,
                Error = $"F# syntax check error: {ex.Message}"
            };
        }
    }

    private async Task<HashSet<string>> GetReferencesAsync(VizCodeProject project)
    {
        var references = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Standard references
        var trustedAssemblies = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? "")
            .Split(Path.PathSeparator);

        var neededAssemblies = new[]
        {
            "System.Runtime",
            "System.Private.CoreLib",
            "netstandard",
            "System.Collections",
            "System.Console",
            "System.Linq",
            "System.Numerics.Vectors",
            "System.Runtime.Numerics",
            "System.ObjectModel",
            "System.ComponentModel",
            "System.IO",
            "System.Text.RegularExpressions"
        };

        foreach (var assembly in trustedAssemblies)
        {
            var name = Path.GetFileNameWithoutExtension(assembly);
            if (neededAssemblies.Any(n => name.Equals(n, StringComparison.OrdinalIgnoreCase)))
            {
                references.Add(assembly);
            }
        }

        // Add FSharp.Core
        var fsharpCore = trustedAssemblies.FirstOrDefault(a => Path.GetFileNameWithoutExtension(a).Equals("FSharp.Core", StringComparison.OrdinalIgnoreCase));
        if (fsharpCore != null) references.Add(fsharpCore);
        else 
        {
             var appDir = Path.GetDirectoryName(typeof(FSharpModuleCompiler).Assembly.Location);
             var localFsCore = Path.Combine(appDir!, "FSharp.Core.dll");
             if (File.Exists(localFsCore)) references.Add(localFsCore);
        }

        // Add Code2Viz.Geometry/Viz2d
        references.Add(typeof(Code2Viz.Geometry.VPoint).Assembly.Location);

        // Project references (NuGet)
        if (project.ProjectFile != null && project.ProjectFile.Packages.Any())
        {
            var packagesDir = Path.Combine(project.ProjectDirectory, ".packages");
            using var nuget = new NuGetHelper(packagesDir);

            foreach (var pkg in project.ProjectFile.Packages)
            {
                try
                {
                    var dlls = await nuget.RestorePackageAsync(pkg.Id, pkg.Version);
                    foreach (var dll in dlls) references.Add(dll);
                }
                catch (Exception ex)
                {
                    ConsoleOutput.Instance.WriteLine("Compiler", 0, $"Warning: Failed to restore {pkg.Id}: {ex.Message}");
                }
            }
        }

        return references;
    }

    /// <summary>
    /// Reads the embedded VizDsl.fs resource.
    /// </summary>
    private static string GetEmbeddedVizDsl()
    {
        var assembly = typeof(FSharpModuleCompiler).Assembly;
        var resourceName = "Code2Viz.FSharp.VizDsl.fs";

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            throw new InvalidOperationException($"Could not find embedded resource: {resourceName}");
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
