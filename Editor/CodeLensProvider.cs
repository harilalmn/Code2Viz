using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Code2Viz.Editor
{
    /// <summary>
    /// Provides Code Lens functionality - shows reference counts above methods and types.
    /// Uses a visual line element generator to display reference counts inline.
    /// </summary>
    public class CodeLensGenerator : VisualLineElementGenerator
    {
        private readonly TextDocument _document;
        private List<CodeLensItem> _items = new();
        private bool _enabled = true;

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
            _items.Clear();

            if (!_enabled) return;

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

                // Find class/struct/interface declarations
                foreach (var typeDecl in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
                {
                    var typeName = typeDecl.Identifier.Text;
                    var refCount = usageMap.TryGetValue(typeName, out var count) ? count : 0;

                    var lineSpan = typeDecl.Identifier.GetLocation().GetLineSpan();
                    var lineNumber = lineSpan.StartLinePosition.Line + 1; // 1-based

                    // Get the start of the line (before any content)
                    var line = _document.GetLineByNumber(lineNumber);

                    _items.Add(new CodeLensItem
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

                    var line = _document.GetLineByNumber(lineNumber);

                    _items.Add(new CodeLensItem
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

                    var line = _document.GetLineByNumber(lineNumber);

                    _items.Add(new CodeLensItem
                    {
                        Offset = line.Offset,
                        Line = lineNumber,
                        Text = $"{refCount} reference{(refCount != 1 ? "s" : "")}",
                        Kind = CodeLensKind.Property,
                        SymbolName = propName
                    });
                }

                // Sort by offset
                _items.Sort((a, b) => a.Offset.CompareTo(b.Offset));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CodeLens error: {ex.Message}");
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
            if (!_enabled || _items.Count == 0) return -1;

            foreach (var item in _items)
            {
                if (item.Offset >= startOffset)
                    return item.Offset;
            }

            return -1;
        }

        public override VisualLineElement? ConstructElement(int offset)
        {
            if (!_enabled) return null;

            var item = _items.FirstOrDefault(i => i.Offset == offset);
            if (item == null) return null;

            return new CodeLensElement(item.Text);
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
    /// Visual element that renders code lens text inline (at the start of the line).
    /// </summary>
    public class CodeLensElement : VisualLineElement
    {
        private readonly string _text;

        public CodeLensElement(string text) : base(0, 0)
        {
            _text = text + " | ";
        }

        public override System.Windows.Media.TextFormatting.TextRun CreateTextRun(
            int startVisualColumn,
            ITextRunConstructionContext context)
        {
            var props = new CodeLensTextRunProperties(context.GlobalTextRunProperties);
            return new CodeLensTextRun(_text, props);
        }
    }

    public class CodeLensTextRun : System.Windows.Media.TextFormatting.TextRun
    {
        private readonly string _text;
        private readonly System.Windows.Media.TextFormatting.TextRunProperties _properties;

        public CodeLensTextRun(string text, System.Windows.Media.TextFormatting.TextRunProperties properties)
        {
            _text = text;
            _properties = properties;
        }

        public override System.Windows.Media.TextFormatting.CharacterBufferReference CharacterBufferReference =>
            new System.Windows.Media.TextFormatting.CharacterBufferReference(_text.ToCharArray(), 0);

        public override int Length => _text.Length;

        public override System.Windows.Media.TextFormatting.TextRunProperties Properties => _properties;
    }

    public class CodeLensTextRunProperties : System.Windows.Media.TextFormatting.TextRunProperties
    {
        private readonly System.Windows.Media.TextFormatting.TextRunProperties _baseProperties;

        public CodeLensTextRunProperties(System.Windows.Media.TextFormatting.TextRunProperties baseProperties)
        {
            _baseProperties = baseProperties;
        }

        public override Brush BackgroundBrush => Brushes.Transparent;

        public override System.Globalization.CultureInfo CultureInfo => _baseProperties.CultureInfo;

        public override double FontHintingEmSize => _baseProperties.FontHintingEmSize * 0.8;

        public override double FontRenderingEmSize => _baseProperties.FontRenderingEmSize * 0.8;

        public override Brush ForegroundBrush => new SolidColorBrush(Color.FromRgb(120, 120, 120));

        public override System.Windows.TextDecorationCollection? TextDecorations => null;

        public override System.Windows.Media.TextEffectCollection? TextEffects => null;

        public override Typeface Typeface => new Typeface(
            _baseProperties.Typeface.FontFamily,
            FontStyles.Italic,
            _baseProperties.Typeface.Weight,
            _baseProperties.Typeface.Stretch);
    }
}
