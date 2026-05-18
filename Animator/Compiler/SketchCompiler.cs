using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading.Tasks;
using Animator.Console;
using Animator.Sketching;
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

    static SketchCompiler()
    {
        var trusted = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? "")
            .Split(Path.PathSeparator);
        var needed = new[]
        {
            "System.Runtime", "System.Private.CoreLib", "netstandard",
            "System.Collections", "System.Linq", "System.Numerics",
            "System.Console", "System.Text.RegularExpressions",
            "System.Threading", "Microsoft.CSharp",
            "WindowsBase", "PresentationCore", "PresentationFramework"
        };
        var refs = new System.Collections.Generic.List<MetadataReference>();
        foreach (var a in trusted)
        {
            var name = Path.GetFileNameWithoutExtension(a);
            if (needed.Contains(name, StringComparer.OrdinalIgnoreCase))
                refs.Add(MetadataReference.CreateFromFile(a));
        }
        // The host (Animator.exe) brings in Sketch and the canvas types.
        refs.Add(MetadataReference.CreateFromFile(typeof(SketchCompiler).Assembly.Location));
        // C2VGeometry brings in the shape types.
        refs.Add(MetadataReference.CreateFromFile(typeof(C2VGeometry.Shape).Assembly.Location));
        DefaultReferences = refs.ToArray();
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

            var tree = CSharpSyntaxTree.ParseText(
                Microsoft.CodeAnalysis.Text.SourceText.From(sourceCode, System.Text.Encoding.UTF8),
                path: sourceName,
                options: new CSharpParseOptions(LanguageVersion.Latest));

            var compilation = CSharpCompilation.Create(
                assemblyName: $"AnimatorSketch_{Guid.NewGuid():N}",
                syntaxTrees: new[] { tree },
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
                    .Select(FormatDiagnostic)
                    .ToList();
                foreach (var line in errs)
                    ConsoleOutput.Instance.WriteError("Compiler", line);
                return new CompileResult { Success = false, Error = string.Join(Environment.NewLine, errs) };
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
