using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading.Tasks;
using Animator.Console;
using Animator.Sketching;
using Code2Viz.Execution; // StackGuardRewriter (shared source, linked into Animator)
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Animator.Compiler;

public class CompileResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Compiles a single .cs file with Roslyn, finds a concrete <see cref="Sketch"/> subclass,
/// and hands it off to <see cref="SketchRuntime"/>. There is no Main() fallback — Animator is
/// sketch-only.
/// </summary>
public class SketchCompiler
{
    private static readonly MetadataReference[] DefaultReferences;

    // Global usings injected into every sketch compilation. The boilerplate template
    // therefore doesn't need any `using` directives — these namespaces are always in scope.
    // The CompletionEngine workspace mirrors this list so the editor IntelliSense sees the
    // same view of the world.
    public const string GlobalUsingsSource = """
        global using System;
        global using System.Linq;
        global using System.Collections.Generic;
        global using C2VGeometry;
        global using Animator.Sketching;
        global using Animator.Console;
        """;

    // Names of the trusted-platform assemblies that get loaded as MetadataReferences for both
    // sketch compilation and editor IntelliSense. Kept in lockstep with Code2Viz's
    // ModuleCompiler so a user who types e.g. `System.Text.Json.` gets completions AND the
    // same code compiles at runtime.
    public static readonly string[] NeededAssemblies = new[]
    {
        "System.Runtime", "System.Private.CoreLib", "netstandard",
        "System.Collections", "System.Collections.Concurrent", "System.Collections.Immutable",
        "System.Linq", "System.Linq.Expressions",
        "System.Numerics", "System.Numerics.Vectors",
        "System.Console", "System.IO", "System.IO.FileSystem",
        "System.Text.RegularExpressions", "System.Text.Json", "System.Text.Encoding.Extensions",
        "System.Threading", "System.Threading.Tasks",
        "System.Memory", "System.ObjectModel", "System.ComponentModel", "System.ComponentModel.Primitives",
        "Microsoft.CSharp",
        "System.Windows.Forms",
        "WindowsBase", "PresentationCore", "PresentationFramework", "System.Xaml"
    };

    /// <summary>
    /// Builds the full reference set used for sketch compilation. Reused by the editor's
    /// CompletionEngine so IntelliSense and runtime see the same symbols.
    /// </summary>
    public static System.Collections.Generic.List<MetadataReference> BuildDefaultReferences()
    {
        var trusted = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? "")
            .Split(Path.PathSeparator);
        var refs = new System.Collections.Generic.List<MetadataReference>();
        foreach (var a in trusted)
        {
            var name = Path.GetFileNameWithoutExtension(a);
            if (NeededAssemblies.Contains(name, StringComparer.OrdinalIgnoreCase))
                refs.Add(MetadataReference.CreateFromFile(a));
        }
        // The host (Animator.exe) brings in Sketch and the canvas types.
        refs.Add(MetadataReference.CreateFromFile(typeof(SketchCompiler).Assembly.Location));
        // C2VGeometry brings in the shape types.
        refs.Add(MetadataReference.CreateFromFile(typeof(C2VGeometry.Shape).Assembly.Location));
        return refs;
    }

    static SketchCompiler()
    {
        DefaultReferences = BuildDefaultReferences().ToArray();
    }

    public async Task<CompileResult> CompileAndRunAsync(string sourceCode, string sourceName)
    {
        try
        {
            // Tear down any running sketch first.
            SketchRuntime.Instance.Stop();
            ConsoleOutput.Instance.Clear();
            ConsoleOutput.Instance.WriteLine("Compiler",
                $"Compiling '{sourceName}'...");

            var parseOptions = new CSharpParseOptions(LanguageVersion.Latest);
            var tree = CSharpSyntaxTree.ParseText(
                Microsoft.CodeAnalysis.Text.SourceText.From(sourceCode, System.Text.Encoding.UTF8),
                path: sourceName,
                options: parseOptions);

            // Inject stack-depth guards so runaway recursion in the sketch surfaces as a catchable
            // InsufficientExecutionStackException (handled by SketchRuntime) instead of an
            // uncatchable StackOverflowException that would kill Animator.exe. Recreate the tree
            // with the original path + encoding so PDB emit and diagnostic line mapping still point
            // at the user's source.
            var guardedRoot = (CSharpSyntaxNode)StackGuardRewriter.Inject(tree.GetRoot());
            tree = CSharpSyntaxTree.Create(
                guardedRoot, parseOptions, path: sourceName, encoding: System.Text.Encoding.UTF8);

            // SourceText.From(...) with an explicit encoding is required when emitting
            // PDBs — passing a bare string here produces "CS8055: Cannot emit debug
            // information for a source text without encoding" during compilation.Emit.
            var globalUsingsTree = CSharpSyntaxTree.ParseText(
                Microsoft.CodeAnalysis.Text.SourceText.From(GlobalUsingsSource, System.Text.Encoding.UTF8),
                path: "_GlobalUsings.g.cs",
                options: parseOptions);

            var compilation = CSharpCompilation.Create(
                assemblyName: $"AnimatorSketch_{Guid.NewGuid():N}",
                syntaxTrees: new[] { globalUsingsTree, tree },
                references: DefaultReferences,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                    .WithOptimizationLevel(OptimizationLevel.Debug)
                    .WithPlatform(Platform.X64));

            using var ms = new MemoryStream();
            using var pdb = new MemoryStream();
            var emit = compilation.Emit(ms, pdb);
            if (!emit.Success)
            {
                var errs = emit.Diagnostics
                    .Where(d => d.Severity == DiagnosticSeverity.Error)
                    .ToList();
                foreach (var d in errs)
                {
                    // Only diagnostics located in the user's sketch tree map to an editor line —
                    // errors in the injected global-usings tree have no clickable source line.
                    int line = 0, col = 0;
                    if (d.Location.IsInSource && ReferenceEquals(d.Location.SourceTree, tree))
                    {
                        var pos = d.Location.GetLineSpan().StartLinePosition;
                        line = pos.Line + 1;
                        col = pos.Character + 1;
                    }
                    ConsoleOutput.Instance.WriteError("Compiler", FormatDiagnostic(d), line, col);
                }
                return new CompileResult { Success = false, Error = string.Join(Environment.NewLine, errs.Select(FormatDiagnostic)) };
            }

            ms.Seek(0, SeekOrigin.Begin);
            pdb.Seek(0, SeekOrigin.Begin);

            return await Task.Run(() => LoadAndStart(ms, pdb));
        }
        catch (Exception ex)
        {
            ConsoleOutput.Instance.WriteError("Compiler", $"Error: {ex.Message}");
            return new CompileResult { Success = false, Error = ex.Message };
        }
    }

    private static CompileResult LoadAndStart(MemoryStream asmStream, MemoryStream pdbStream)
    {
        var ctx = new SketchAssemblyLoadContext();
        bool transferred = false;
        try
        {
            var asm = ctx.LoadFromStream(asmStream, pdbStream);
            var sketchBase = typeof(Sketch);
            var sketchType = asm.GetTypes()
                .FirstOrDefault(t => !t.IsAbstract && sketchBase.IsAssignableFrom(t));

            if (sketchType == null)
            {
                ConsoleOutput.Instance.WriteError("Compiler",
                    "No Sketch subclass found. Define `class Foo : Sketch` and override Setup()/Draw().");
                return new CompileResult { Success = false, Error = "No Sketch subclass found." };
            }

            ConsoleOutput.Instance.WriteLine("Compiler",
                $"Sketch detected: {sketchType.FullName}. Entering frame loop.");
            SketchRuntime.Instance.Start(sketchType, ctx);
            transferred = true;
            return new CompileResult { Success = true };
        }
        finally
        {
            if (!transferred) ctx.Unload();
        }
    }

    private static string FormatDiagnostic(Diagnostic d)
    {
        var span = d.Location.GetLineSpan();
        var line = span.StartLinePosition.Line + 1;
        var col = span.StartLinePosition.Character + 1;
        return $"({line},{col}): {d.Id}: {d.GetMessage()}";
    }

    private sealed class SketchAssemblyLoadContext : AssemblyLoadContext
    {
        private static readonly Assembly _hostAssembly = typeof(SketchCompiler).Assembly;
        private static readonly string _hostName = _hostAssembly.GetName().Name!;

        public SketchAssemblyLoadContext() : base(isCollectible: true) { }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            // Share the host so SketchRuntime/Sketch/AnimCanvas singletons are the same instances.
            if (string.Equals(assemblyName.Name, _hostName, StringComparison.OrdinalIgnoreCase))
                return _hostAssembly;
            return null; // fall back to default resolution
        }
    }
}
