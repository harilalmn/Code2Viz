using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RoslynAccessibility = Microsoft.CodeAnalysis.Accessibility;

namespace Animator.Editor;

/// <summary>
/// Minimal Roslyn-backed completion engine for the Animator editor.
/// Maintains an incremental compilation; on request returns symbols at a cursor position
/// using <see cref="SemanticModel.LookupSymbols"/> so dot-completion and lexical scope work.
/// </summary>
public sealed class CompletionEngine
{
    private readonly CachedCompilationWorkspace _workspace;
    private const string FileId = "Sketch.cs";

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
    }

    /// <summary>Replaces the cached source text. Cheap incremental tree replace.</summary>
    public void Update(string source) => _workspace.UpdateFile(FileId, source);

    /// <summary>
    /// Returns completion candidates at the given offset. Filtering by typed prefix is
    /// done downstream by AvalonEdit's CompletionList.
    /// </summary>
    public List<CompletionItem> GetCompletions(int offset)
    {
        var tree = _workspace.GetSyntaxTree(FileId);
        var model = _workspace.GetSemanticModel(FileId);
        if (tree == null || model == null) return new();

        var root = tree.GetRoot();
        if (offset > root.FullSpan.End) offset = root.FullSpan.End;
        if (offset < 0) offset = 0;

        try
        {
            // Detect member-access context (`expr.` or `expr.partial`)
            var memberAccess = FindMemberAccessContainer(root, model, offset);
            ImmutableArrayCompatible symbols;
            if (memberAccess != null)
            {
                symbols = new ImmutableArrayCompatible(
                    model.LookupSymbols(offset, container: memberAccess, includeReducedExtensionMethods: true));
            }
            else
            {
                symbols = new ImmutableArrayCompatible(
                    model.LookupSymbols(offset, includeReducedExtensionMethods: true));
            }

            return symbols.Items
                .Where(s => s != null && IsUseful(s))
                .Select(ToItem)
                .GroupBy(i => i.DisplayText)
                .Select(g => g.OrderByDescending(x => (int)x.Kind).First())
                .OrderBy(i => i.DisplayText, StringComparer.OrdinalIgnoreCase)
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

        // Find the enclosing MemberAccessExpression (`x.y`) or QualifiedName (`X.Y`).
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

    private static CompletionItem ToItem(ISymbol s)
    {
        var kind = s switch
        {
            INamedTypeSymbol nt when nt.TypeKind == TypeKind.Class     => CompletionKind.Class,
            INamedTypeSymbol nt when nt.TypeKind == TypeKind.Interface => CompletionKind.Interface,
            INamedTypeSymbol nt when nt.TypeKind == TypeKind.Struct    => CompletionKind.Struct,
            INamedTypeSymbol nt when nt.TypeKind == TypeKind.Enum      => CompletionKind.Enum,
            IMethodSymbol      => CompletionKind.Method,
            IPropertySymbol    => CompletionKind.Property,
            IFieldSymbol       => CompletionKind.Field,
            IEventSymbol       => CompletionKind.Event,
            ILocalSymbol       => CompletionKind.Local,
            IParameterSymbol   => CompletionKind.Parameter,
            INamespaceSymbol   => CompletionKind.Namespace,
            _ => CompletionKind.Other
        };
        var description = s.ToDisplayString();
        return new CompletionItem(s.Name, description, kind);
    }

    /// <summary>
    /// Tiny adapter so we don't have to take a hard dependency on
    /// System.Collections.Immutable in our public surface.
    /// </summary>
    private readonly struct ImmutableArrayCompatible
    {
        public readonly IEnumerable<ISymbol> Items;
        public ImmutableArrayCompatible(System.Collections.Immutable.ImmutableArray<ISymbol> arr)
            => Items = arr;
    }
}

public enum CompletionKind { Class, Interface, Struct, Enum, Method, Property, Field, Event, Local, Parameter, Namespace, Keyword, Other }

public sealed record CompletionItem(string DisplayText, string Description, CompletionKind Kind);
