using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Code2Viz.Canvas;
using Code2Viz.Console;
using Code2Viz.Project;

namespace Code2Viz.Execution;

public class FSharpDiagnosticInfo
{
    public string FilePath { get; set; } = string.Empty;
    public int StartLine { get; set; }
    public int StartColumn { get; set; }
    public int EndLine { get; set; }
    public int EndColumn { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string ErrorNumber { get; set; } = string.Empty;
}

public class CompilationResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public IEnumerable<Diagnostic>? Diagnostics { get; set; }
    public List<FSharpDiagnosticInfo>? FSharpDiagnostics { get; set; }
}

public class ModuleCompiler
{
    private static readonly List<MetadataReference> DefaultReferences;

    static ModuleCompiler()
    {
        DefaultReferences = new List<MetadataReference>();

        // Add core runtime references
        var trustedAssemblies = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? "")
            .Split(Path.PathSeparator);

        var neededAssemblies = new[]
        {
            // Core runtime
            "System.Runtime",
            "System.Private.CoreLib",
            "netstandard",
            
            // Collections
            "System.Collections",
            "System.Collections.Concurrent",
            "System.Collections.Immutable",
            
            // Linq
            "System.Linq",
            "System.Linq.Expressions",
            
            // Numerics
            "System.Numerics",
            "System.Numerics.Vectors",
            
            // I/O
            "System.Console",
            "System.IO",
            "System.IO.FileSystem",
            
            // Text
            "System.Text.RegularExpressions",
            "System.Text.Json",
            "System.Text.Encoding.Extensions",
            
            // Threading
            "System.Threading",
            "System.Threading.Tasks",
            
            // Other common
            "System.Memory",
            "System.ObjectModel",
            "System.ComponentModel",
            "System.ComponentModel.Primitives",
            "Microsoft.CSharp",
            
            // UI
            "System.Windows.Forms",
            
            // WPF
            "WindowsBase",
            "PresentationCore",
            "PresentationFramework",
            "System.Xaml",
        };

        foreach (var assembly in trustedAssemblies)
        {
            var name = Path.GetFileNameWithoutExtension(assembly);
            if (neededAssemblies.Any(n => name.Equals(n, StringComparison.OrdinalIgnoreCase)))
            {
                DefaultReferences.Add(MetadataReference.CreateFromFile(assembly));
            }
        }

        // Add Code2Viz.Geometry assembly
        DefaultReferences.Add(MetadataReference.CreateFromFile(typeof(Geometry.VPoint).Assembly.Location));
    }

    /// <summary>
    /// Gets the default metadata references used for compilation.
    /// Useful for semantic analysis features like semantic highlighting.
    /// </summary>
    public IEnumerable<MetadataReference> GetReferences()
    {
        return DefaultReferences;
    }

    public async Task<CompilationResult> CompileAndExecuteAsync(VizCodeProject project)
    {
        // Dispatch based on language
        if (project.ProjectFile.Language == ProjectLanguage.FSharp)
        {
            var fsCompiler = new FSharpModuleCompiler();
            return await fsCompiler.CompileAndExecuteAsync(project);
        }

        try
        {
            // Clear previous shapes and console
            CanvasRenderer.Instance.Clear();
            ConsoleOutput.Instance.Clear();

            // Create compilation
            var (compilation, allDlls) = await CreateCompilationAsync(project);

            // Emit to memory stream with PDB for line numbers in stack traces
            using var ms = new MemoryStream();
            using var pdbStream = new MemoryStream();
            var emitResult = compilation.Emit(ms, pdbStream);

            if (!emitResult.Success)
            {
                var errors = emitResult.Diagnostics
                    .Where(d => d.Severity == DiagnosticSeverity.Error)
                    .Select(FormatDiagnostic);

                return new CompilationResult
                {
                    Success = false,
                    Error = "Compilation Error:\n" + string.Join(Environment.NewLine, errors),
                    Diagnostics = emitResult.Diagnostics
                };
            }

            // Set working directory to project folder so relative paths resolve correctly
            var previousDirectory = Environment.CurrentDirectory;
            if (!string.IsNullOrEmpty(project.ProjectDirectory))
                Environment.CurrentDirectory = project.ProjectDirectory;

            // Execute
            ms.Seek(0, SeekOrigin.Begin);
            pdbStream.Seek(0, SeekOrigin.Begin);
            try
            {
                return await ExecuteAssemblyAsync(ms, pdbStream, allDlls, project.ProjectFile.Name ?? "MyProject");
            }
            finally
            {
                Environment.CurrentDirectory = previousDirectory;
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
    /// Check for syntax and compilation errors without executing.
    /// Used for real-time error checking while typing.
    /// </summary>
    public async Task<CompilationResult> CheckSyntaxAsync(VizCodeProject project)
    {
        if (project.ProjectFile.Language == ProjectLanguage.FSharp)
        {
            var fsCompiler = new FSharpModuleCompiler();
            return await fsCompiler.CheckSyntaxAsync(project);
        }

        try
        {
            var (compilation, _) = await CreateCompilationAsync(project);

            // Get diagnostics without emitting
            var diagnostics = compilation.GetDiagnostics();
            var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();

            return new CompilationResult
            {
                Success = errors.Count == 0,
                Error = errors.Count > 0 ? $"{errors.Count} error(s)" : null,
                Diagnostics = diagnostics
            };
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

    public static async Task<CompilationResult> ExecuteAssemblyAsync(Stream assemblyStream, Stream? pdbStream, HashSet<string> dependencies, string projectName)
    {
        try
        {
            // Use our custom AssemblyLoadContext that knows about restored packages
            var loadContext = new VizAssemblyLoadContext(dependencies);
            try
            {
                var assembly = pdbStream != null
                    ? loadContext.LoadFromStream(assemblyStream, pdbStream)
                    : loadContext.LoadFromStream(assemblyStream);

                // Get the entry point namespace from project name
                var projectNamespace = Templates.SanitizeIdentifier(projectName);
                var entryTypeName = $"{projectNamespace}.Viz";
                
                // Try finding entry type (CS) or module (FS)
                var entryType = assembly.GetType(entryTypeName);
                
                // F# modules may compile with different naming conventions
                // Try alternates: "Namespace.Viz" or "Namespace.VizModule" 
                if (entryType == null)
                {
                    entryType = assembly.GetType($"{projectNamespace}.VizModule");
                }
                
                // List all types in the assembly for debugging
                if (entryType == null)
                {
                    var allTypes = assembly.GetTypes().Select(t => t.FullName).ToList();
                    return new CompilationResult
                    {
                        Success = false,
                        Error = $"Entry point not found: class '{entryTypeName}' is missing.\n\nAvailable types:\n" + 
                                string.Join("\n", allTypes.Take(10)) +
                                $"\n\nEnsure StartViz contains:\nnamespace {projectNamespace}\n...\n    class Viz / module Viz"
                    };
                }

                var mainMethod = entryType.GetMethod("Main", BindingFlags.Public | BindingFlags.Static);
                if (mainMethod == null)
                {
                    // Maybe it's a script without a class? 
                    return new CompilationResult
                    {
                        Success = false,
                        Error = $"Entry point not found: static method 'Main()' is missing in {entryTypeName} class."
                    };
                }

                // Execute Main() - catch TargetInvocationException inside Task.Run
                // to prevent VS debugger from breaking on "user-unhandled" exception
                var invokeException = await Task.Run<Exception?>(() =>
                {
                    try
                    {
                        if (mainMethod.GetParameters().Length > 0)
                            mainMethod.Invoke(null, new object[] { Array.Empty<string>() });
                        else
                            mainMethod.Invoke(null, null);
                        return null;
                    }
                    catch (TargetInvocationException ex)
                    {
                        return ex.InnerException ?? ex;
                    }
                });

                if (invokeException != null)
                {
                    return new CompilationResult
                    {
                        Success = false,
                        Error = $"Runtime Error: {FormatRuntimeError(invokeException)}"
                    };
                }

                // After successful execution, hide shapes without variable names
                // (shapes with names have Name set by AnimationNameRewriter)
                HideUnnamedShapes();

                return new CompilationResult { Success = true };
            }
            finally
            {
                loadContext.Unload();
            }
        }
        catch (TargetInvocationException ex)
        {
             return new CompilationResult
            {
                Success = false,
                Error = $"Runtime Error: {FormatRuntimeError(ex.InnerException ?? ex)}"
            };
        }
        catch (Exception ex)
        {
            return new CompilationResult
            {
                Success = false,
                Error = $"Runtime Error: {FormatRuntimeError(ex)}"
            };
        }
    }

    /// <summary>
    /// Formats a runtime exception with file/line info extracted from the stack trace.
    /// </summary>
    private static string FormatRuntimeError(Exception ex)
    {
        var message = ex.Message;

        // Parse the stack trace to find frames in user .vizcode files
        if (ex.StackTrace != null)
        {
            var lines = ex.StackTrace.Split('\n');
            foreach (var line in lines)
            {
                // Look for stack frames with .vizcode file references
                // Format: "at Namespace.Class.Method() in C:\path\file.vizcode:line 42"
                var match = System.Text.RegularExpressions.Regex.Match(
                    line, @"in\s+(.+\.vizcode):line\s+(\d+)");
                if (match.Success)
                {
                    var filePath = match.Groups[1].Value;
                    var lineNumber = match.Groups[2].Value;
                    var fileName = Path.GetFileName(filePath);

                    // Also extract the method name for context
                    var methodMatch = System.Text.RegularExpressions.Regex.Match(
                        line, @"at\s+(.+?)\(");
                    var methodInfo = methodMatch.Success ? methodMatch.Groups[1].Value : null;

                    // Simplify method name - just show "Class.Method"
                    if (methodInfo != null)
                    {
                        var parts = methodInfo.Split('.');
                        if (parts.Length >= 2)
                            methodInfo = string.Join(".", parts.Skip(parts.Length - 2));
                    }

                    message += $"\n  at {(methodInfo != null ? methodInfo + " " : "")}({fileName}, line {lineNumber})";
                }
            }
        }

        return message;
    }

    /// <summary>
    /// Hides shapes that don't have a Name set (anonymous/inline shapes).
    /// Only shapes with explicit variable names are shown.
    /// </summary>
    private static void HideUnnamedShapes()
    {
        var shapes = CanvasRenderer.Instance.GetShapes();
        foreach (var drawable in shapes)
        {
            if (drawable is Geometry.Shape shape && string.IsNullOrEmpty(shape.Name) && !shape.IsExplicitlyDrawn)
            {
                shape.IsVisible = false;
            }
        }
    }

    public async Task<(List<MetadataReference> References, HashSet<string> AllDlls)> GetProjectReferencesAndDllsAsync(VizCodeProject project)
    {
        var references = new List<MetadataReference>(DefaultReferences);
        var allDlls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (project.ProjectFile != null && project.ProjectFile.Packages.Any())
        {
            var packagesDir = Path.Combine(project.ProjectDirectory, ".packages");
            using var nuget = new NuGetHelper(packagesDir);

            foreach (var pkg in project.ProjectFile.Packages)
            {
                try
                {
                    var dlls = await nuget.RestorePackageAsync(pkg.Id, pkg.Version);
                    foreach (var dll in dlls) allDlls.Add(dll);
                }
                catch (Exception ex)
                {
                    ConsoleOutput.Instance.WriteLine("Compiler", 0, $"Warning: Failed to restore {pkg.Id}: {ex.Message}");
                }
            }

            foreach (var dll in allDlls)
            {
                using var fs = File.OpenRead(dll);
                references.Add(MetadataReference.CreateFromStream(fs, filePath: dll));
            }
        }

        // Load project assembly references
        if (project.ProjectFile?.References?.Any() == true)
        {
            var trustedAssemblies = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? "")
                .Split(Path.PathSeparator);

            foreach (var asmRef in project.ProjectFile.References)
            {
                try
                {
                    if (asmRef.IsFramework)
                    {
                        // Find framework assembly by name
                        var match = trustedAssemblies.FirstOrDefault(a =>
                            Path.GetFileNameWithoutExtension(a).Equals(asmRef.Path, StringComparison.OrdinalIgnoreCase));
                        if (match != null)
                        {
                            references.Add(MetadataReference.CreateFromFile(match));
                        }
                        else
                        {
                            ConsoleOutput.Instance.WriteLine("Compiler", 0, $"Warning: Framework assembly '{asmRef.Path}' not found.");
                        }
                    }
                    else
                    {
                        // Load local DLL
                        var dllPath = asmRef.Path;
                        if (!Path.IsPathRooted(dllPath))
                        {
                            dllPath = Path.Combine(project.ProjectDirectory, dllPath);
                        }

                        if (File.Exists(dllPath))
                        {
                            using var fs = File.OpenRead(dllPath);
                            references.Add(MetadataReference.CreateFromStream(fs, filePath: dllPath));
                            allDlls.Add(dllPath); // Add for runtime loading context
                        }
                        else
                        {
                            ConsoleOutput.Instance.WriteLine("Compiler", 0, $"Warning: Assembly '{asmRef.Path}' not found.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    ConsoleOutput.Instance.WriteLine("Compiler", 0, $"Warning: Failed to load reference '{asmRef.Path}': {ex.Message}");
                }
            }
        }

        return (references, allDlls);
    }

    public async Task<(CSharpCompilation Compilation, HashSet<string> AllDlls)> CreateCompilationAsync(VizCodeProject project)
    {
        // Get ALL source files from project directory (not just open ones)
        var allSourceFiles = project.GetAllSourceFiles().ToList();

        // Parse all source files into syntax trees
        var rewriter = new AnimationNameRewriter();
        var syntaxTrees = allSourceFiles.Select(file =>
        {
            var sourceText = Microsoft.CodeAnalysis.Text.SourceText.From(
                file.Content, System.Text.Encoding.UTF8);
            var tree = CSharpSyntaxTree.ParseText(
                sourceText,
                path: file.FilePath,
                options: new CSharpParseOptions(LanguageVersion.Latest));

            // Transform animation variable declarations to include Name property
            var newRoot = rewriter.Visit(tree.GetRoot());
            
            // IMPORTANT: Preserve the original file path when creating the new tree
            // Using newRoot.SyntaxTree loses the file path!
            return tree.WithRootAndOptions(newRoot, tree.Options);
        }).ToList();

        // Resolve NuGet packages and references
        var (references, allDlls) = await GetProjectReferencesAndDllsAsync(project);

        // Create compilation
        var compilation = CSharpCompilation.Create(
            assemblyName: $"VizCodeAssembly_{Guid.NewGuid():N}",
            syntaxTrees: syntaxTrees,
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithOptimizationLevel(OptimizationLevel.Debug)
                .WithPlatform(Platform.X64)
        );

        return (compilation, allDlls);
    }

    private static string FormatDiagnostic(Diagnostic d)
    {
        var location = d.Location.GetLineSpan();
        var fileName = Path.GetFileName(location.Path);
        var line = location.StartLinePosition.Line + 1;
        var col = location.StartLinePosition.Character + 1;

        // Include filename if available
        if (!string.IsNullOrEmpty(fileName))
            return $"{fileName}({line},{col}): error {d.Id}: {d.GetMessage()}";

        return $"({line},{col}): error {d.Id}: {d.GetMessage()}";
    }
}

public class VizAssemblyLoadContext : AssemblyLoadContext
{
    private readonly HashSet<string> _dependencyPaths;
    private static readonly Assembly _hostAssembly = typeof(VizAssemblyLoadContext).Assembly;
    private static readonly string _hostAssemblyName = typeof(VizAssemblyLoadContext).Assembly.GetName().Name!;

    public VizAssemblyLoadContext(HashSet<string> dependencyPaths) : base(isCollectible: true)
    {
        _dependencyPaths = dependencyPaths;
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // IMPORTANT: Return the host Viz2d assembly to share singletons like CanvasRenderer.Instance
        if (string.Equals(assemblyName.Name, _hostAssemblyName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(assemblyName.Name, "Code2Viz", StringComparison.OrdinalIgnoreCase))
        {
            return _hostAssembly;
        }
        
        // Check if we have a path for this assembly
        // We look for any path that matches the assembly name (case insensitive)
        foreach (var path in _dependencyPaths)
        {
            var fileName = Path.GetFileNameWithoutExtension(path);
            if (string.Equals(fileName, assemblyName.Name, StringComparison.OrdinalIgnoreCase))
            {
                // Found it, load from file
                return LoadFromAssemblyPath(path);
            }
        }

        // Return null to allow default loading (from runtime, etc.)
        return null;
    }
}

/// <summary>
/// Rewrites animation and shape variable declarations to automatically set the Name property
/// to the variable name. Transforms:
///   Animation circleAnim = new MoveAnimation(...);
///   VCircle myCircle = new VCircle(0, 0, 10);
/// To:
///   Animation circleAnim = new MoveAnimation(...) { Name = "circleAnim" };
///   VCircle myCircle = new VCircle(0, 0, 10) { Name = "myCircle" };
/// </summary>
internal class AnimationNameRewriter : CSharpSyntaxRewriter
{
    private static readonly HashSet<string> AnimationTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Animation", "DrawAnimation", "MoveAnimation", "RotateAnimation",
        "FlipAnimation", "FadeInAnimation", "FadeOutAnimation", "ValueAnimation",
        "ObjectPropertyAnimation"
    };

    private static readonly HashSet<string> ShapeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "VPoint", "VLine", "VCircle", "VArc", "VRectangle", "VEllipse",
        "VPolygon", "VPolyline", "VBezier", "VSpline", "VArrow", "VText",
        "VGrid", "VGroup", "VDimension", "Region", "VXLine", "VRay"
    };

    public override SyntaxNode? VisitLocalDeclarationStatement(LocalDeclarationStatementSyntax node)
    {
        var declaration = node.Declaration;
        var variables = declaration.Variables;

        // Process each variable in the declaration
        var newVariables = new List<VariableDeclaratorSyntax>();
        bool anyChanged = false;

        foreach (var variable in variables)
        {
            var newVariable = TryRewriteNamedVariable(declaration.Type, variable);
            if (newVariable != variable)
                anyChanged = true;
            newVariables.Add(newVariable);
        }

        if (!anyChanged)
            return base.VisitLocalDeclarationStatement(node);

        var newDeclaration = declaration.WithVariables(
            SyntaxFactory.SeparatedList(newVariables));
        return node.WithDeclaration(newDeclaration);
    }

    public override SyntaxNode? VisitFieldDeclaration(FieldDeclarationSyntax node)
    {
        var declaration = node.Declaration;
        var variables = declaration.Variables;

        // Process each variable in the declaration
        var newVariables = new List<VariableDeclaratorSyntax>();
        bool anyChanged = false;

        foreach (var variable in variables)
        {
            var newVariable = TryRewriteNamedVariable(declaration.Type, variable);
            if (newVariable != variable)
                anyChanged = true;
            newVariables.Add(newVariable);
        }

        if (!anyChanged)
            return base.VisitFieldDeclaration(node);

        var newDeclaration = declaration.WithVariables(
            SyntaxFactory.SeparatedList(newVariables));
        return node.WithDeclaration(newDeclaration);
    }

    private VariableDeclaratorSyntax TryRewriteNamedVariable(TypeSyntax type, VariableDeclaratorSyntax variable)
    {
        // Check if this is an animation or shape type (explicit or var)
        // For generic types like ValueAnimation<VCircle>, extract the base name
        var typeName = type.ToString();
        var baseTypeName = type is GenericNameSyntax genericType
            ? genericType.Identifier.Text
            : typeName;
        bool isExplicitNamedType = AnimationTypes.Contains(typeName) || AnimationTypes.Contains(baseTypeName) || ShapeTypes.Contains(typeName);

        // Check if initializer is an object creation expression
        if (variable.Initializer?.Value is ObjectCreationExpressionSyntax objectCreation)
        {
            // Check if the created type is an animation or shape type
            // For generic types like ValueAnimation<VCircle>, extract the base name
            var createdTypeName = objectCreation.Type.ToString();
            var baseCreatedTypeName = objectCreation.Type is GenericNameSyntax genericName
                ? genericName.Identifier.Text
                : createdTypeName;
            bool isCreatedTypeNamed = AnimationTypes.Contains(createdTypeName) || AnimationTypes.Contains(baseCreatedTypeName) || ShapeTypes.Contains(createdTypeName);

            // Only add Name initializer if the created type is actually an animation/shape type
            // This prevents adding Name to List<VPoint> and other non-shape types when 'var' is used
            if (!isExplicitNamedType && !isCreatedTypeNamed)
                return variable;

            return TryAddNameInitializer(variable, objectCreation, objectCreation.Initializer);
        }

        // Handle target-typed new: VLine line = new(...)
        if (variable.Initializer?.Value is ImplicitObjectCreationExpressionSyntax implicitCreation)
        {
            // For implicit object creation, we need an explicit named type
            if (!isExplicitNamedType)
                return variable;

            return TryAddNameInitializerImplicit(variable, implicitCreation, implicitCreation.Initializer);
        }

        return variable;
    }

    private VariableDeclaratorSyntax TryAddNameInitializer(
        VariableDeclaratorSyntax variable,
        ObjectCreationExpressionSyntax objectCreation,
        InitializerExpressionSyntax? existingInitializer)
    {
        // Skip if has a collection initializer (cannot mix with object initializer)
        if (existingInitializer != null && existingInitializer.Kind() == SyntaxKind.CollectionInitializerExpression)
            return variable;

        // Skip if already has an initializer with Name set
        if (existingInitializer != null)
        {
            var hasNameProperty = existingInitializer.Expressions
                .OfType<AssignmentExpressionSyntax>()
                .Any(a => a.Left.ToString() == "Name");
            if (hasNameProperty)
                return variable;
        }

        // Create the Name = "variableName" assignment
        var variableName = variable.Identifier.Text;
        var nameAssignment = SyntaxFactory.AssignmentExpression(
            SyntaxKind.SimpleAssignmentExpression,
            SyntaxFactory.IdentifierName("Name"),
            SyntaxFactory.LiteralExpression(
                SyntaxKind.StringLiteralExpression,
                SyntaxFactory.Literal(variableName)));

        // Create or extend the object initializer
        InitializerExpressionSyntax newInitializer;
        if (existingInitializer != null)
        {
            // Add to existing initializer
            var newExpressions = existingInitializer.Expressions.Add(nameAssignment);
            newInitializer = existingInitializer.WithExpressions(newExpressions);
        }
        else
        {
            // Create new initializer
            newInitializer = SyntaxFactory.InitializerExpression(
                SyntaxKind.ObjectInitializerExpression,
                SyntaxFactory.SingletonSeparatedList<ExpressionSyntax>(nameAssignment));
        }

        var newObjectCreation = objectCreation.WithInitializer(newInitializer);
        var newInitializerClause = variable.Initializer!.WithValue(newObjectCreation);
        return variable.WithInitializer(newInitializerClause);
    }

    private VariableDeclaratorSyntax TryAddNameInitializerImplicit(
        VariableDeclaratorSyntax variable,
        ImplicitObjectCreationExpressionSyntax implicitCreation,
        InitializerExpressionSyntax? existingInitializer)
    {
        // Skip if has a collection initializer (cannot mix with object initializer)
        if (existingInitializer != null && existingInitializer.Kind() == SyntaxKind.CollectionInitializerExpression)
            return variable;

        // Skip if already has an initializer with Name set
        if (existingInitializer != null)
        {
            var hasNameProperty = existingInitializer.Expressions
                .OfType<AssignmentExpressionSyntax>()
                .Any(a => a.Left.ToString() == "Name");
            if (hasNameProperty)
                return variable;
        }

        // Create the Name = "variableName" assignment
        var variableName = variable.Identifier.Text;
        var nameAssignment = SyntaxFactory.AssignmentExpression(
            SyntaxKind.SimpleAssignmentExpression,
            SyntaxFactory.IdentifierName("Name"),
            SyntaxFactory.LiteralExpression(
                SyntaxKind.StringLiteralExpression,
                SyntaxFactory.Literal(variableName)));

        // Create or extend the object initializer
        InitializerExpressionSyntax newInitializer;
        if (existingInitializer != null)
        {
            // Add to existing initializer
            var newExpressions = existingInitializer.Expressions.Add(nameAssignment);
            newInitializer = existingInitializer.WithExpressions(newExpressions);
        }
        else
        {
            // Create new initializer
            newInitializer = SyntaxFactory.InitializerExpression(
                SyntaxKind.ObjectInitializerExpression,
                SyntaxFactory.SingletonSeparatedList<ExpressionSyntax>(nameAssignment));
        }

        var newImplicitCreation = implicitCreation.WithInitializer(newInitializer);
        var newInitializerClause = variable.Initializer!.WithValue(newImplicitCreation);
        return variable.WithInitializer(newInitializerClause);
    }
}
