using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.TextFormatting;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;
using Brushes = System.Windows.Media.Brushes;
using FontStyles = System.Windows.FontStyles;
using FontWeights = System.Windows.FontWeights;
using FontStyle = System.Windows.FontStyle;
using FontWeight = System.Windows.FontWeight;
using TextBlock = System.Windows.Controls.TextBlock;

namespace Code2Viz.Editor
{
    /// <summary>
    /// Provides Code Lens functionality - shows reference counts above methods and types.
    /// Uses a visual line element generator to display reference counts inline.
    /// </summary>
    public class CodeLensGenerator : VisualLineElementGenerator
    {
        private readonly TextDocument _document;
        private readonly object _lock = new();
        private List<CodeLensItem> _items = new();
        private bool _enabled = true;

        public int ItemCount
        {
            get { lock (_lock) return _items.Count; }
        }

        public bool Enabled
        {
            get => _enabled;
            set
            {
                _enabled = value;
                CurrentContext?.TextView?.Redraw();
            }
        }

        public CodeLensGenerator(TextDocument document)
        {
            _document = document;
        }

        /// <summary>
        /// Updates code lens information by analyzing the code.
        /// </summary>
        public void UpdateCodeLens(string code)
        {
            if (!_enabled)
            {
                lock (_lock)
                {
                    _items.Clear();
                }
                return;
            }

            var newItems = new List<CodeLensItem>();

            try
            {
                var syntaxTree = CSharpSyntaxTree.ParseText(code);
                var compilation = CSharpCompilation.Create(
                    "CodeLensAnalysis",
                    new[] { syntaxTree },
                    new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) },
                    new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                );

                var semanticModel = compilation.GetSemanticModel(syntaxTree);
                var root = syntaxTree.GetRoot();

                // Build a map of symbol usages
                var usageMap = BuildUsageMap(root);

                // Snapshot the document line count to avoid race conditions
                var lineCount = _document.LineCount;

                // Find class/struct/interface declarations
                foreach (var typeDecl in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
                {
                    var typeName = typeDecl.Identifier.Text;
                    var refCount = usageMap.TryGetValue(typeName, out var count) ? count : 0;

                    var lineSpan = typeDecl.Identifier.GetLocation().GetLineSpan();
                    var lineNumber = lineSpan.StartLinePosition.Line + 1; // 1-based

                    // Validate line number is within bounds
                    if (lineNumber < 1 || lineNumber > lineCount) continue;

                    // Get the start of the line (before any content)
                    var line = _document.GetLineByNumber(lineNumber);

                    newItems.Add(new CodeLensItem
                    {
                        Offset = line.Offset,
                        Line = lineNumber,
                        Text = $"{refCount} reference{(refCount != 1 ? "s" : "")}",
                        Kind = CodeLensKind.Type,
                        SymbolName = typeName
                    });
                }

                // Find method declarations
                foreach (var methodDecl in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
                {
                    var methodName = methodDecl.Identifier.Text;
                    var refCount = usageMap.TryGetValue(methodName, out var count) ? count : 0;

                    var lineSpan = methodDecl.Identifier.GetLocation().GetLineSpan();
                    var lineNumber = lineSpan.StartLinePosition.Line + 1;

                    // Validate line number is within bounds
                    if (lineNumber < 1 || lineNumber > lineCount) continue;

                    var line = _document.GetLineByNumber(lineNumber);

                    newItems.Add(new CodeLensItem
                    {
                        Offset = line.Offset,
                        Line = lineNumber,
                        Text = $"{refCount} reference{(refCount != 1 ? "s" : "")}",
                        Kind = CodeLensKind.Method,
                        SymbolName = methodName
                    });
                }

                // Find property declarations
                foreach (var propDecl in root.DescendantNodes().OfType<PropertyDeclarationSyntax>())
                {
                    var propName = propDecl.Identifier.Text;
                    var refCount = usageMap.TryGetValue(propName, out var count) ? count : 0;

                    var lineSpan = propDecl.Identifier.GetLocation().GetLineSpan();
                    var lineNumber = lineSpan.StartLinePosition.Line + 1;

                    // Validate line number is within bounds
                    if (lineNumber < 1 || lineNumber > lineCount) continue;

                    var line = _document.GetLineByNumber(lineNumber);

                    newItems.Add(new CodeLensItem
                    {
                        Offset = line.Offset,
                        Line = lineNumber,
                        Text = $"{refCount} reference{(refCount != 1 ? "s" : "")}",
                        Kind = CodeLensKind.Property,
                        SymbolName = propName
                    });
                }

                // Sort by offset
                newItems.Sort((a, b) => a.Offset.CompareTo(b.Offset));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CodeLens error: {ex.Message}");
            }

            // Atomically swap the items list
            lock (_lock)
            {
                _items = newItems;
            }
        }

        private Dictionary<string, int> BuildUsageMap(SyntaxNode root)
        {
            var map = new Dictionary<string, int>(StringComparer.Ordinal);

            // Count all identifier usages
            foreach (var identifier in root.DescendantNodes().OfType<IdentifierNameSyntax>())
            {
                var name = identifier.Identifier.Text;

                // Skip if it's part of a declaration
                if (identifier.Parent is VariableDeclaratorSyntax ||
                    identifier.Parent is MethodDeclarationSyntax ||
                    identifier.Parent is PropertyDeclarationSyntax ||
                    identifier.Parent is ClassDeclarationSyntax ||
                    identifier.Parent is StructDeclarationSyntax ||
                    identifier.Parent is InterfaceDeclarationSyntax)
                {
                    continue;
                }

                if (map.TryGetValue(name, out var count))
                {
                    map[name] = count + 1;
                }
                else
                {
                    map[name] = 1;
                }
            }

            // Also count type references in type syntax
            foreach (var typeSyntax in root.DescendantNodes().OfType<SimpleNameSyntax>())
            {
                if (typeSyntax is IdentifierNameSyntax) continue; // Already counted

                var name = typeSyntax.Identifier.Text;
                if (map.TryGetValue(name, out var count))
                {
                    map[name] = count + 1;
                }
                else
                {
                    map[name] = 1;
                }
            }

            return map;
        }

        public override int GetFirstInterestedOffset(int startOffset)
        {
            if (!_enabled) return -1;

            try
            {
                List<CodeLensItem> snapshot;
                lock (_lock)
                {
                    snapshot = _items;
                }

                if (snapshot.Count == 0) return -1;

                foreach (var item in snapshot)
                {
                    if (item.Offset >= startOffset)
                        return item.Offset;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CodeLens GetFirstInterestedOffset error: {ex.Message}");
            }

            return -1;
        }

        public override VisualLineElement? ConstructElement(int offset)
        {
            if (!_enabled) return null;

            try
            {
                List<CodeLensItem> snapshot;
                lock (_lock)
                {
                    snapshot = _items;
                }

                var item = snapshot.FirstOrDefault(i => i.Offset == offset);
                if (item == null) return null;

                var lineHeight = CurrentContext?.TextView?.DefaultLineHeight ?? 16.0;
                return new CodeLensElement(item.Text, lineHeight);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CodeLens ConstructElement error: {ex.Message}");
                return null;
            }
        }
    }

    public class CodeLensItem
    {
        public int Offset { get; set; }
        public int Line { get; set; }
        public string Text { get; set; } = "";
        public CodeLensKind Kind { get; set; }
        public string SymbolName { get; set; } = "";
    }

    public enum CodeLensKind
    {
        Type,
        Method,
        Property,
        Field
    }

    /// <summary>
    /// Visual element that renders code lens text on its own row above the line.
    /// Wraps a zero-width Canvas so it doesn't displace the line text horizontally;
    /// the canvas is taller than one line so the line itself is rendered at the bottom,
    /// leaving room above for the code lens label.
    /// </summary>
    public class CodeLensElement : InlineObjectElement
    {
        public CodeLensElement(string text, double lineHeight)
            : base(0, BuildElement(text, lineHeight))
        {
        }

        private static UIElement BuildElement(string text, double lineHeight)
        {
            var label = new TextBlock
            {
                Text = text,
                Foreground = new SolidColorBrush(Color.FromRgb(120, 120, 120)),
                FontStyle = FontStyles.Italic,
                FontSize = Math.Max(8, lineHeight * 0.65),
                IsHitTestVisible = false,
            };

            // Zero-width host so the line's normal characters keep their indentation.
            // Height = 2x line height: AvalonEdit baselines the line text at the host's
            // bottom, so the upper half becomes empty space where we render the label.
            // ClipToBounds=false lets the label paint past the canvas's reported width.
            var host = new System.Windows.Controls.Canvas
            {
                Width = 0,
                Height = lineHeight * 2,
                ClipToBounds = false,
                IsHitTestVisible = false,
            };
            System.Windows.Controls.Canvas.SetLeft(label, 0);
            System.Windows.Controls.Canvas.SetTop(label, 0);
            host.Children.Add(label);
            return host;
        }
    }
}
