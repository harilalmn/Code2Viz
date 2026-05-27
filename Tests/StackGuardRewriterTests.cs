using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using Code2Viz.Execution;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Code2Viz.Tests;

/// <summary>
/// Verifies the Animator stack-guard rewriter turns runaway recursion into a catchable
/// <see cref="InsufficientExecutionStackException"/> instead of a process-killing
/// <see cref="StackOverflowException"/>. These tests are safe to run in-process precisely
/// because the guard makes the recursion catchable — if the guard regressed, the test host
/// would crash, which is itself a loud signal.
/// </summary>
public class StackGuardRewriterTests
{
    // ── Syntactic: the guard call is injected into method-like bodies ──────────────────────────

    [Fact]
    public void Inject_AddsGuard_ToBlockBodiedMethod()
    {
        var rewritten = Rewrite("""
            class C { void M() { System.Console.WriteLine(1); } }
            """);
        Assert.Contains("EnsureSufficientExecutionStack", rewritten);
    }

    [Fact]
    public void Inject_AddsGuard_ToExpressionBodiedMethod()
    {
        // `int M(int n) => n;` has no block — the rewriter must promote it to a guarded block
        // with a return. (Exact spacing isn't asserted: Roslyn compiles from the tree, not the
        // rendered text, and the arrow's trailing trivia can swallow the space before the value.
        // Semantic correctness of the promotion is proven by the behavioral tests below.)
        var rewritten = Rewrite("class C { int M(int n) => n; }");
        Assert.Contains("EnsureSufficientExecutionStack", rewritten);
        Assert.Contains("return", rewritten);
        Assert.DoesNotContain("=>", rewritten);
    }

    [Fact]
    public void Inject_GuardsEveryMethod_InAClass()
    {
        var rewritten = Rewrite("""
            class C {
                void A() { }
                void B() { }
                int C2() => 1;
            }
            """);
        Assert.Equal(3, CountOccurrences(rewritten, "EnsureSufficientExecutionStack"));
    }

    [Fact]
    public void Inject_DoesNotTouch_AbstractMethodWithoutBody()
    {
        var rewritten = Rewrite("abstract class C { abstract void M(); }");
        Assert.DoesNotContain("EnsureSufficientExecutionStack", rewritten);
    }

    // ── Behavioral: guarded recursion throws a CATCHABLE exception ──────────────────────────────

    [Fact]
    public void MutualRecursion_ThrowsCatchableException_NotStackOverflow()
    {
        // Mirrors the real circlesFill.cs bug: A <-> B with no exit condition.
        var asm = CompileGuarded("""
            public static class Boom {
                public static void A() { B(); }
                public static void B() { A(); }
            }
            """);
        var method = asm.GetType("Boom")!.GetMethod("A")!;

        var ex = Assert.Throws<TargetInvocationException>(() => method.Invoke(null, null));
        Assert.IsType<InsufficientExecutionStackException>(ex.InnerException);
    }

    [Fact]
    public void SelfRecursion_ThroughExpressionBody_ThrowsCatchableException()
    {
        var asm = CompileGuarded("""
            public static class Boom {
                public static int Deep(int n) => Deep(n + 1);
            }
            """);
        var method = asm.GetType("Boom")!.GetMethod("Deep")!;

        var ex = Assert.Throws<TargetInvocationException>(
            () => method.Invoke(null, new object[] { 0 }));
        Assert.IsType<InsufficientExecutionStackException>(ex.InnerException);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────────

    private static string Rewrite(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        return StackGuardRewriter.Inject(tree.GetRoot()).ToFullString();
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        int count = 0, i = 0;
        while ((i = haystack.IndexOf(needle, i, StringComparison.Ordinal)) >= 0) { count++; i += needle.Length; }
        return count;
    }

    private static Assembly CompileGuarded(string source)
    {
        var parse = new CSharpParseOptions(LanguageVersion.Latest);
        var tree = CSharpSyntaxTree.ParseText(source, parse);
        var guarded = (CSharpSyntaxNode)StackGuardRewriter.Inject(tree.GetRoot());
        tree = CSharpSyntaxTree.Create(guarded, parse, encoding: Encoding.UTF8);

        var compilation = CSharpCompilation.Create(
            "StackGuardTest_" + Guid.NewGuid().ToString("N"),
            new[] { tree },
            CoreReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var ms = new MemoryStream();
        var emit = compilation.Emit(ms);
        Assert.True(emit.Success,
            "compile failed: " + string.Join("; ", emit.Diagnostics.Select(d => d.ToString())));
        ms.Seek(0, SeekOrigin.Begin);
        return AssemblyLoadContext.Default.LoadFromStream(ms);
    }

    private static MetadataReference[] CoreReferences()
    {
        var trusted = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? "")
            .Split(Path.PathSeparator);
        var needed = new[] { "System.Runtime", "System.Private.CoreLib", "netstandard", "System.Console" };
        return trusted
            .Where(p => needed.Contains(Path.GetFileNameWithoutExtension(p), StringComparer.OrdinalIgnoreCase))
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .ToArray();
    }
}
