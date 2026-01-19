using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Code2Viz.Editor
{
    /// <summary>
    /// Provides semantic syntax highlighting based on Roslyn analysis.
    /// Colors identifiers based on their semantic meaning (parameters, locals, fields, etc.)
    /// </summary>
    public class SemanticHighlighter : DocumentColorizingTransformer
    {
        private readonly TextDocument _document;
        private List<SemanticToken> _tokens = new();
        private bool _enabled = true;
        private CancellationTokenSource? _cts;

        // Semantic colors (VSCode-like dark theme)
        private static readonly Dictionary<SemanticTokenKind, Color> TokenColors = new()
        {
            { SemanticTokenKind.LocalVariable, Color.FromRgb(156, 220, 254) },     // Light blue
            { SemanticTokenKind.Parameter, Color.FromRgb(156, 220, 254) },         // Light blue
            { SemanticTokenKind.Field, Color.FromRgb(156, 220, 254) },             // Light blue
            { SemanticTokenKind.Property, Color.FromRgb(156, 220, 254) },          // Light blue
            { SemanticTokenKind.Method, Color.FromRgb(220, 220, 170) },            // Light yellow
            { SemanticTokenKind.ExtensionMethod, Color.FromRgb(220, 220, 170) },   // Light yellow
            { SemanticTokenKind.StaticMethod, Color.FromRgb(220, 220, 170) },      // Light yellow
            { SemanticTokenKind.Class, Color.FromRgb(78, 201, 176) },              // Teal
            { SemanticTokenKind.Struct, Color.FromRgb(78, 201, 176) },             // Teal
            { SemanticTokenKind.Interface, Color.FromRgb(184, 215, 163) },         // Light green
            { SemanticTokenKind.Enum, Color.FromRgb(184, 215, 163) },              // Light green
            { SemanticTokenKind.EnumMember, Color.FromRgb(79, 193, 255) },         // Cyan
            { SemanticTokenKind.Delegate, Color.FromRgb(78, 201, 176) },           // Teal
            { SemanticTokenKind.TypeParameter, Color.FromRgb(184, 215, 163) },     // Light green
            { SemanticTokenKind.Namespace, Color.FromRgb(156, 220, 254) },         // Light blue
            { SemanticTokenKind.Constant, Color.FromRgb(79, 193, 255) },           // Cyan
            { SemanticTokenKind.Event, Color.FromRgb(156, 220, 254) },             // Light blue
            { SemanticTokenKind.Label, Color.FromRgb(156, 220, 254) },             // Light blue
            { SemanticTokenKind.Static, Color.FromRgb(79, 193, 255) }              // Cyan for static fields/properties
        };

        public bool Enabled
        {
            get => _enabled;
            set
            {
                _enabled = value;
                _tokens.Clear();
            }
        }

        public SemanticHighlighter(TextDocument document)
        {
            _document = document;
        }

        /// <summary>
        /// Updates semantic tokens by analyzing the code.
        /// Call this when code changes (debounced).
        /// </summary>
        public async Task UpdateTokensAsync(string code, IEnumerable<MetadataReference>? references = null)
        {
            if (!_enabled) return;

            // Cancel previous analysis
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;

            try
            {
                var newTokens = await Task.Run(() => AnalyzeCode(code, references, ct), ct);

                if (!ct.IsCancellationRequested)
                {
                    _tokens = newTokens;
                }
            }
            catch (OperationCanceledException)
            {
                // Analysis was cancelled, that's OK
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SemanticHighlighter error: {ex.Message}");
            }
        }

        private List<SemanticToken> AnalyzeCode(string code, IEnumerable<MetadataReference>? references, CancellationToken ct)
        {
            var tokens = new List<SemanticToken>();

            try
            {
                var syntaxTree = CSharpSyntaxTree.ParseText(code, cancellationToken: ct);

                // Create compilation with references
                var refs = references?.ToList() ?? new List<MetadataReference>
                {
                    MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(System.Console).Assembly.Location)
                };

                // Add System.Runtime reference
                var runtimePath = System.IO.Path.GetDirectoryName(typeof(object).Assembly.Location);
                if (runtimePath != null)
                {
                    var runtimeDll = System.IO.Path.Combine(runtimePath, "System.Runtime.dll");
                    if (System.IO.File.Exists(runtimeDll))
                    {
                        refs.Add(MetadataReference.CreateFromFile(runtimeDll));
                    }
                }

                var compilation = CSharpCompilation.Create(
                    "SemanticAnalysis",
                    new[] { syntaxTree },
                    refs,
                    new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                );

                var semanticModel = compilation.GetSemanticModel(syntaxTree);
                var root = syntaxTree.GetRoot(ct);

                // Find all identifiers
                var identifiers = root.DescendantNodes()
                    .OfType<IdentifierNameSyntax>()
                    .ToList();

                foreach (var identifier in identifiers)
                {
                    if (ct.IsCancellationRequested) break;

                    var symbolInfo = semanticModel.GetSymbolInfo(identifier, ct);
                    var symbol = symbolInfo.Symbol;

                    if (symbol != null)
                    {
                        var kind = GetTokenKind(symbol);
                        if (kind != SemanticTokenKind.None)
                        {
                            tokens.Add(new SemanticToken
                            {
                                Start = identifier.Span.Start,
                                Length = identifier.Span.Length,
                                Kind = kind
                            });
                        }
                    }
                }

                // Find type declarations (class, struct, interface, record)
                foreach (var typeDecl in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
                {
                    if (ct.IsCancellationRequested) break;

                    var kind = typeDecl switch
                    {
                        ClassDeclarationSyntax => SemanticTokenKind.Class,
                        StructDeclarationSyntax => SemanticTokenKind.Struct,
                        InterfaceDeclarationSyntax => SemanticTokenKind.Interface,
                        _ => SemanticTokenKind.None
                    };

                    if (kind != SemanticTokenKind.None)
                    {
                        tokens.Add(new SemanticToken
                        {
                            Start = typeDecl.Identifier.Span.Start,
                            Length = typeDecl.Identifier.Span.Length,
                            Kind = kind
                        });
                    }
                }

                // Find enum declarations (separate from TypeDeclarationSyntax)
                foreach (var enumDecl in root.DescendantNodes().OfType<EnumDeclarationSyntax>())
                {
                    if (ct.IsCancellationRequested) break;

                    tokens.Add(new SemanticToken
                    {
                        Start = enumDecl.Identifier.Span.Start,
                        Length = enumDecl.Identifier.Span.Length,
                        Kind = SemanticTokenKind.Enum
                    });
                }

                // Find method declarations
                foreach (var methodDecl in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
                {
                    if (ct.IsCancellationRequested) break;

                    var kind = methodDecl.Modifiers.Any(SyntaxKind.StaticKeyword)
                        ? SemanticTokenKind.StaticMethod
                        : SemanticTokenKind.Method;

                    tokens.Add(new SemanticToken
                    {
                        Start = methodDecl.Identifier.Span.Start,
                        Length = methodDecl.Identifier.Span.Length,
                        Kind = kind
                    });
                }

                // Find property declarations
                foreach (var propDecl in root.DescendantNodes().OfType<PropertyDeclarationSyntax>())
                {
                    if (ct.IsCancellationRequested) break;

                    var kind = propDecl.Modifiers.Any(SyntaxKind.StaticKeyword)
                        ? SemanticTokenKind.Static
                        : SemanticTokenKind.Property;

                    tokens.Add(new SemanticToken
                    {
                        Start = propDecl.Identifier.Span.Start,
                        Length = propDecl.Identifier.Span.Length,
                        Kind = kind
                    });
                }

                // Find field declarations
                foreach (var fieldDecl in root.DescendantNodes().OfType<FieldDeclarationSyntax>())
                {
                    if (ct.IsCancellationRequested) break;

                    foreach (var variable in fieldDecl.Declaration.Variables)
                    {
                        var isStatic = fieldDecl.Modifiers.Any(SyntaxKind.StaticKeyword);
                        var isConst = fieldDecl.Modifiers.Any(SyntaxKind.ConstKeyword);

                        var kind = isConst ? SemanticTokenKind.Constant
                            : isStatic ? SemanticTokenKind.Static
                            : SemanticTokenKind.Field;

                        tokens.Add(new SemanticToken
                        {
                            Start = variable.Identifier.Span.Start,
                            Length = variable.Identifier.Span.Length,
                            Kind = kind
                        });
                    }
                }

                // Find parameter declarations
                foreach (var param in root.DescendantNodes().OfType<ParameterSyntax>())
                {
                    if (ct.IsCancellationRequested) break;

                    tokens.Add(new SemanticToken
                    {
                        Start = param.Identifier.Span.Start,
                        Length = param.Identifier.Span.Length,
                        Kind = SemanticTokenKind.Parameter
                    });
                }

                // Find local variable declarations
                foreach (var localDecl in root.DescendantNodes().OfType<VariableDeclarationSyntax>())
                {
                    if (ct.IsCancellationRequested) break;

                    // Skip if it's a field declaration (already handled)
                    if (localDecl.Parent is FieldDeclarationSyntax) continue;

                    foreach (var variable in localDecl.Variables)
                    {
                        tokens.Add(new SemanticToken
                        {
                            Start = variable.Identifier.Span.Start,
                            Length = variable.Identifier.Span.Length,
                            Kind = SemanticTokenKind.LocalVariable
                        });
                    }
                }

                // Find foreach variables
                foreach (var foreachStmt in root.DescendantNodes().OfType<ForEachStatementSyntax>())
                {
                    if (ct.IsCancellationRequested) break;

                    tokens.Add(new SemanticToken
                    {
                        Start = foreachStmt.Identifier.Span.Start,
                        Length = foreachStmt.Identifier.Span.Length,
                        Kind = SemanticTokenKind.LocalVariable
                    });
                }

                // Find type parameters
                foreach (var typeParam in root.DescendantNodes().OfType<TypeParameterSyntax>())
                {
                    if (ct.IsCancellationRequested) break;

                    tokens.Add(new SemanticToken
                    {
                        Start = typeParam.Identifier.Span.Start,
                        Length = typeParam.Identifier.Span.Length,
                        Kind = SemanticTokenKind.TypeParameter
                    });
                }

                // Sort by position
                tokens.Sort((a, b) => a.Start.CompareTo(b.Start));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Code analysis error: {ex.Message}");
            }

            return tokens;
        }

        private SemanticTokenKind GetTokenKind(ISymbol symbol)
        {
            return symbol switch
            {
                ILocalSymbol local => local.IsConst ? SemanticTokenKind.Constant : SemanticTokenKind.LocalVariable,
                IParameterSymbol => SemanticTokenKind.Parameter,
                IFieldSymbol field => field.IsConst ? SemanticTokenKind.Constant
                    : field.IsStatic ? SemanticTokenKind.Static
                    : SemanticTokenKind.Field,
                IPropertySymbol prop => prop.IsStatic ? SemanticTokenKind.Static : SemanticTokenKind.Property,
                IMethodSymbol method => method.IsExtensionMethod ? SemanticTokenKind.ExtensionMethod
                    : method.IsStatic ? SemanticTokenKind.StaticMethod
                    : SemanticTokenKind.Method,
                INamedTypeSymbol type => type.TypeKind switch
                {
                    TypeKind.Class => SemanticTokenKind.Class,
                    TypeKind.Struct => SemanticTokenKind.Struct,
                    TypeKind.Interface => SemanticTokenKind.Interface,
                    TypeKind.Enum => SemanticTokenKind.Enum,
                    TypeKind.Delegate => SemanticTokenKind.Delegate,
                    _ => SemanticTokenKind.Class
                },
                ITypeParameterSymbol => SemanticTokenKind.TypeParameter,
                INamespaceSymbol => SemanticTokenKind.Namespace,
                IEventSymbol => SemanticTokenKind.Event,
                ILabelSymbol => SemanticTokenKind.Label,
                _ => SemanticTokenKind.None
            };
        }

        protected override void ColorizeLine(DocumentLine line)
        {
            if (!_enabled || _tokens.Count == 0) return;

            var lineStart = line.Offset;
            var lineEnd = line.EndOffset;

            // Find tokens that overlap with this line
            foreach (var token in _tokens)
            {
                if (token.Start >= lineEnd) break; // Tokens are sorted
                if (token.Start + token.Length < lineStart) continue;

                // Token overlaps with line
                var start = Math.Max(token.Start, lineStart);
                var end = Math.Min(token.Start + token.Length, lineEnd);

                if (start < end && TokenColors.TryGetValue(token.Kind, out var color))
                {
                    try
                    {
                        ChangeLinePart(start, end, element =>
                        {
                            element.TextRunProperties.SetForegroundBrush(new SolidColorBrush(color));
                        });
                    }
                    catch
                    {
                        // Ignore errors during colorization
                    }
                }
            }
        }

        public void Clear()
        {
            _tokens.Clear();
            _cts?.Cancel();
        }
    }

    public struct SemanticToken
    {
        public int Start;
        public int Length;
        public SemanticTokenKind Kind;
    }

    public enum SemanticTokenKind
    {
        None,
        LocalVariable,
        Parameter,
        Field,
        Property,
        Method,
        ExtensionMethod,
        StaticMethod,
        Class,
        Struct,
        Interface,
        Enum,
        EnumMember,
        Delegate,
        TypeParameter,
        Namespace,
        Constant,
        Event,
        Label,
        Static
    }
}
