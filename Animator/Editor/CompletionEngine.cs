using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Code2Viz.Editor;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RoslynAccessibility = Microsoft.CodeAnalysis.Accessibility;

namespace Animator.Editor;

/// <summary>
/// Minimal Roslyn-backed completion engine for the Animator editor.
/// Maintains an incremental compilation via <see cref="CachedCompilationWorkspace"/>
/// (the same one used by Code2Viz) and produces <see cref="CompletionData"/> items
/// rich enough for the doc sidecar to render XML docs.
/// </summary>
public sealed class CompletionEngine
{
    private readonly CachedCompilationWorkspace _workspace;
    public const string FileId = "Sketch.cs";
    private const string GlobalUsingsFileId = "_GlobalUsings.g.cs";

    /// <summary>The underlying incremental Roslyn workspace, reused by other editor features
    /// (semantic highlighting, inlay hints) so they all share a single compilation.</summary>
    public CachedCompilationWorkspace Workspace => _workspace;

    public CompletionEngine()
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
        var refs = new List<MetadataReference>();
        foreach (var a in trusted)
        {
            var name = Path.GetFileNameWithoutExtension(a);
            if (needed.Contains(name, StringComparer.OrdinalIgnoreCase))
                refs.Add(MetadataReference.CreateFromFile(a));
        }
        refs.Add(MetadataReference.CreateFromFile(typeof(Animator.Sketching.Sketch).Assembly.Location));
        refs.Add(MetadataReference.CreateFromFile(typeof(C2VGeometry.Shape).Assembly.Location));

        _workspace = new CachedCompilationWorkspace(refs);

        // Mirror SketchCompiler.GlobalUsingsSource — every sketch compilation has these
        // global usings pre-applied, so the editor's compilation must too or it will
        // flag the unqualified Sketch / VizConsole / V* types as missing.
        _workspace.UpdateFile(GlobalUsingsFileId, Animator.Compiler.SketchCompiler.GlobalUsingsSource);
    }

    /// <summary>Replaces the cached source text. Cheap incremental tree replace.</summary>
    public void Update(string source) => _workspace.UpdateFile(FileId, source);

    /// <summary>
    /// Returns completion candidates at the given offset. Filtering by typed prefix is
    /// done downstream by AvalonEdit's CompletionList.
    /// </summary>
    public List<CompletionData> GetCompletions(int offset)
    {
        var tree = _workspace.GetSyntaxTree(FileId);
        var model = _workspace.GetSemanticModel(FileId);
        if (tree == null || model == null) return new();

        var root = tree.GetRoot();
        if (offset > root.FullSpan.End) offset = root.FullSpan.End;
        if (offset < 0) offset = 0;

        try
        {
            var memberAccess = FindMemberAccessContainer(root, model, offset);
            System.Collections.Immutable.ImmutableArray<ISymbol> symbols;
            if (memberAccess != null)
                symbols = model.LookupSymbols(offset, container: memberAccess, includeReducedExtensionMethods: true);
            else
                symbols = model.LookupSymbols(offset, includeReducedExtensionMethods: true);

            return symbols
                .Where(s => s != null && IsUseful(s))
                .Select(ToItem)
                .GroupBy(i => i.Text)
                .Select(g => g.OrderByDescending(x => (int)x.Kind).First())
                .OrderBy(i => i.Text, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            return new();
        }
    }

    /// <summary>Walks back from the cursor to find the type/namespace symbol on the LHS of a `.`.</summary>
    private static INamespaceOrTypeSymbol? FindMemberAccessContainer(SyntaxNode root, SemanticModel model, int offset)
    {
        if (offset == 0) return null;
        var token = root.FindToken(Math.Max(0, offset - 1));

        SyntaxNode? lhs = null;
        if (token.IsKind(SyntaxKind.DotToken))
        {
            if (token.Parent is MemberAccessExpressionSyntax ma) lhs = ma.Expression;
            else if (token.Parent is QualifiedNameSyntax qn) lhs = qn.Left;
        }
        else if (token.Parent?.Parent is MemberAccessExpressionSyntax accMa)
        {
            lhs = accMa.Expression;
        }
        else if (token.Parent?.Parent is QualifiedNameSyntax accQn)
        {
            lhs = accQn.Left;
        }
        if (lhs == null) return null;

        var symbolInfo = model.GetSymbolInfo(lhs);
        if (symbolInfo.Symbol is INamespaceOrTypeSymbol nts) return nts;

        var typeInfo = model.GetTypeInfo(lhs);
        return typeInfo.Type;
    }

    private static bool IsUseful(ISymbol s)
    {
        if (s.IsImplicitlyDeclared) return false;
        if (s.Name.StartsWith("<") || s.Name.StartsWith(".") || s.Name.StartsWith("_")) return false;
        if (s.DeclaredAccessibility == RoslynAccessibility.Private && !IsLocalScope(s)) return false;
        if (s is IMethodSymbol ms)
        {
            if (ms.MethodKind == MethodKind.PropertyGet) return false;
            if (ms.MethodKind == MethodKind.PropertySet) return false;
            if (ms.MethodKind == MethodKind.EventAdd) return false;
            if (ms.MethodKind == MethodKind.EventRemove) return false;
            if (ms.MethodKind == MethodKind.Constructor) return false;
            if (ms.MethodKind == MethodKind.Destructor) return false;
            if (ms.MethodKind == MethodKind.StaticConstructor) return false;
        }
        return true;
    }

    private static bool IsLocalScope(ISymbol s)
        => s is ILocalSymbol or IParameterSymbol or ITypeParameterSymbol;

    private static CompletionData ToItem(ISymbol s)
    {
        // Map ISymbol → Code2Viz.Editor.CompletionKind. That enum is coarser
        // (Keyword/Type/Property/Method/Delegate/Snippet) so collapse type-like
        // symbols to Type and field/local/parameter to Property for icon purposes.
        var kind = s switch
        {
            INamedTypeSymbol or ITypeParameterSymbol => CompletionKind.Type,
            IMethodSymbol => CompletionKind.Method,
            IPropertySymbol => CompletionKind.Property,
            IFieldSymbol or ILocalSymbol or IParameterSymbol => CompletionKind.Property,
            INamespaceSymbol => CompletionKind.Type,
            _ => CompletionKind.Property
        };

        var description = s.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        var scope = s switch
        {
            ILocalSymbol or IParameterSymbol => SymbolScope.Local,
            _ => SymbolScope.Global
        };
        return new CompletionData(s.Name, description, kind) { Symbol = s, Scope = scope };
    }
}
