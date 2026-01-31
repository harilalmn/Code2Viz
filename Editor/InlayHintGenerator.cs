using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.TextFormatting;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace Code2Viz.Editor
{
    /// <summary>
    /// Generates inlay hints for parameter names and inferred types.
    /// </summary>
    public class InlayHintGenerator : VisualLineElementGenerator
    {
        private readonly TextDocument _document;
        private List<InlayHint> _hints = new();
        private bool _enabled = true;
        private bool _showParameterNames = true;
        private bool _showInferredTypes = true;

        public bool Enabled
        {
            get => _enabled;
            set
            {
                _enabled = value;
                CurrentContext?.TextView?.Redraw();
            }
        }

        public bool ShowParameterNames
        {
            get => _showParameterNames;
            set
            {
                _showParameterNames = value;
                CurrentContext?.TextView?.Redraw();
            }
        }

        public bool ShowInferredTypes
        {
            get => _showInferredTypes;
            set
            {
                _showInferredTypes = value;
                CurrentContext?.TextView?.Redraw();
            }
        }

        public InlayHintGenerator(TextDocument document)
        {
            _document = document;
        }

        public void UpdateHints(string code)
        {
            _hints.Clear();

            if (!_enabled) return;

            try
            {
                // Find parameter hints for method calls
                if (_showParameterNames)
                {
                    FindParameterHints(code);
                }

                // Find type hints for var declarations
                if (_showInferredTypes)
                {
                    FindTypeHints(code);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"InlayHintGenerator error: {ex.Message}");
            }
        }

        private void FindParameterHints(string code)
        {
            // Find method calls with literal arguments or simple variable arguments
            // Pattern: methodName(arg1, arg2, ...)
            var methodCallPattern = @"\b([A-Z][a-zA-Z0-9]*)\s*\(([^)]+)\)";
            var matches = Regex.Matches(code, methodCallPattern);

            foreach (Match match in matches)
            {
                var methodName = match.Groups[1].Value;
                var argsText = match.Groups[2].Value;
                var argsStart = match.Groups[2].Index;

                // Skip if it's a type cast or control statement
                if (IsControlKeyword(methodName)) continue;

                // Get parameter names for known types
                // Parse arguments
                var args = ParseArguments(argsText);

                var paramNames = GetParameterNames(methodName, args.Count);
                if (paramNames == null || paramNames.Count == 0) continue;

                for (int i = 0; i < args.Count && i < paramNames.Count; i++)
                {
                    var arg = args[i];

                    // Only show hints for literals and simple variables (not complex expressions)
                    if (ShouldShowParameterHint(arg.Text))
                    {
                        var hintOffset = argsStart + arg.StartOffset;
                        _hints.Add(new InlayHint
                        {
                            Offset = hintOffset,
                            Text = $"{paramNames[i]}:",
                            Kind = InlayHintKind.Parameter
                        });
                    }
                }
            }
        }

        private void FindTypeHints(string code)
        {
            // Find var declarations with initializers
            // Pattern: var name = expression;
            var varPattern = @"\bvar\s+([a-zA-Z_][a-zA-Z0-9_]*)\s*=\s*([^;]+);";
            var matches = Regex.Matches(code, varPattern);

            foreach (Match match in matches)
            {
                var varName = match.Groups[1].Value;
                var initializer = match.Groups[2].Value.Trim();
                var varNameEnd = match.Groups[1].Index + match.Groups[1].Length;

                // Try to infer type from initializer
                var inferredType = InferType(initializer);
                if (!string.IsNullOrEmpty(inferredType))
                {
                    _hints.Add(new InlayHint
                    {
                        Offset = varNameEnd,
                        Text = $": {inferredType}",
                        Kind = InlayHintKind.Type
                    });
                }
            }
        }

        private List<string>? GetParameterNames(string methodName, int argCount)
        {
            // Known Code2Viz constructors and methods
            return methodName switch
            {
                "VPoint" => new List<string> { "x", "y" },
                "VLine" => argCount switch
                {
                    3 => new List<string> { "startPoint", "angleInDegrees", "length" },
                    2 => new List<string> { "start", "end" },
                    _ => new List<string> { "x1", "y1", "x2", "y2" },
                },
                "VCircle" => new List<string> { "centerX", "centerY", "radius" },
                "VArc" => new List<string> { "centerX", "centerY", "radius", "startAngle", "endAngle" },
                "VRectangle" => new List<string> { "x", "y", "width", "height" },
                "VEllipse" => new List<string> { "centerX", "centerY", "radiusX", "radiusY" },
                "VText" => new List<string> { "text", "x", "y" },
                "VArrow" => new List<string> { "x1", "y1", "x2", "y2" },
                "Substring" => new List<string> { "startIndex", "length" },
                "IndexOf" => new List<string> { "value" },
                "Replace" => new List<string> { "oldValue", "newValue" },
                "Split" => new List<string> { "separator" },
                "Pow" => new List<string> { "x", "y" },
                "Sqrt" => new List<string> { "d" },
                "Sin" => new List<string> { "a" },
                "Cos" => new List<string> { "a" },
                "Tan" => new List<string> { "a" },
                "Abs" => new List<string> { "value" },
                "Min" => new List<string> { "val1", "val2" },
                "Max" => new List<string> { "val1", "val2" },
                "Round" => new List<string> { "value", "digits" },
                "Floor" => new List<string> { "d" },
                "Ceiling" => new List<string> { "d" },
                "WriteLine" => new List<string> { "value" },
                "Write" => new List<string> { "value" },
                _ => null
            };
        }

        private string? InferType(string initializer)
        {
            // Simple type inference based on initializer patterns
            if (initializer.StartsWith("\"") || initializer.StartsWith("$\"") || initializer.StartsWith("@\""))
                return "string";
            if (initializer == "true" || initializer == "false")
                return "bool";
            if (Regex.IsMatch(initializer, @"^\d+$"))
                return "int";
            if (Regex.IsMatch(initializer, @"^\d+\.\d+[fd]?$"))
                return "double";
            if (Regex.IsMatch(initializer, @"^\d+\.\d+m$"))
                return "decimal";
            if (initializer.StartsWith("new List<"))
            {
                var typeMatch = Regex.Match(initializer, @"new List<([^>]+)>");
                if (typeMatch.Success)
                    return $"List<{typeMatch.Groups[1].Value}>";
            }
            if (initializer.StartsWith("new Dictionary<"))
            {
                var typeMatch = Regex.Match(initializer, @"new Dictionary<([^>]+)>");
                if (typeMatch.Success)
                    return $"Dictionary<{typeMatch.Groups[1].Value}>";
            }
            if (initializer.StartsWith("new VPoint")) return "VPoint";
            if (initializer.StartsWith("new VLine")) return "VLine";
            if (initializer.StartsWith("new VCircle")) return "VCircle";
            if (initializer.StartsWith("new VRectangle")) return "VRectangle";
            if (initializer.StartsWith("new VEllipse")) return "VEllipse";
            if (initializer.StartsWith("new VPolygon")) return "VPolygon";
            if (initializer.StartsWith("new VPolyline")) return "VPolyline";
            if (initializer.StartsWith("new VText")) return "VText";
            if (initializer.StartsWith("new VArrow")) return "VArrow";
            if (initializer.StartsWith("new VBezier")) return "VBezier";
            if (initializer.StartsWith("new VSpline")) return "VSpline";
            if (initializer.StartsWith("new VArc")) return "VArc";
            if (initializer.StartsWith("new VGroup")) return "VGroup";

            return null;
        }

        private bool ShouldShowParameterHint(string arg)
        {
            arg = arg.Trim();

            // Show hints for:
            // - Numeric literals
            // - String literals
            // - Boolean literals
            // - Simple variable names (no dots or complex expressions)

            if (Regex.IsMatch(arg, @"^-?\d+(\.\d+)?[fdm]?$")) return true;
            if (arg.StartsWith("\"") || arg.StartsWith("$\"") || arg.StartsWith("@\"")) return true;
            if (arg == "true" || arg == "false" || arg == "null") return true;
            if (Regex.IsMatch(arg, @"^[a-zA-Z_][a-zA-Z0-9_]*$")) return true;

            return false;
        }

        private bool IsControlKeyword(string word)
        {
            return word is "if" or "while" or "for" or "foreach" or "switch" or "catch" or "using" or "lock";
        }

        private List<(string Text, int StartOffset)> ParseArguments(string argsText)
        {
            var result = new List<(string, int)>();
            var current = new System.Text.StringBuilder();
            var depth = 0;
            var inString = false;
            var startOffset = 0;

            for (int i = 0; i < argsText.Length; i++)
            {
                var c = argsText[i];

                if (c == '"' && (i == 0 || argsText[i - 1] != '\\'))
                {
                    inString = !inString;
                }

                if (!inString)
                {
                    if (c == '(' || c == '[' || c == '{') depth++;
                    else if (c == ')' || c == ']' || c == '}') depth--;
                    else if (c == ',' && depth == 0)
                    {
                        var arg = current.ToString().Trim();
                        if (!string.IsNullOrEmpty(arg))
                        {
                            result.Add((arg, startOffset));
                        }
                        current.Clear();
                        startOffset = i + 1;
                        continue;
                    }
                }

                current.Append(c);
            }

            var lastArg = current.ToString().Trim();
            if (!string.IsNullOrEmpty(lastArg))
            {
                result.Add((lastArg, startOffset));
            }

            return result;
        }

        public override int GetFirstInterestedOffset(int startOffset)
        {
            if (!_enabled || _hints.Count == 0) return -1;

            foreach (var hint in _hints)
            {
                if (hint.Offset >= startOffset)
                    return hint.Offset;
            }

            return -1;
        }

        public override VisualLineElement? ConstructElement(int offset)
        {
            if (!_enabled) return null;

            var hint = _hints.FirstOrDefault(h => h.Offset == offset);
            if (hint == null) return null;

            return new InlayHintElement(hint.Text, hint.Kind);
        }
    }

    public class InlayHint
    {
        public int Offset { get; set; }
        public string Text { get; set; } = "";
        public InlayHintKind Kind { get; set; }
    }

    public enum InlayHintKind
    {
        Parameter,
        Type
    }

    /// <summary>
    /// Visual element that renders an inlay hint inline.
    /// </summary>
    public class InlayHintElement : VisualLineElement
    {
        private readonly string _text;
        private readonly InlayHintKind _kind;

        public InlayHintElement(string text, InlayHintKind kind) : base(0, 0)
        {
            _text = text;
            _kind = kind;
        }

        public override TextRun CreateTextRun(int startVisualColumn, ITextRunConstructionContext context)
        {
            var textProps = new InlayHintTextRunProperties(context.GlobalTextRunProperties, _kind);
            return new InlayHintTextRun(_text, textProps);
        }
    }

    /// <summary>
    /// Custom text run for inlay hints.
    /// </summary>
    public class InlayHintTextRun : TextRun
    {
        private readonly string _text;
        private readonly TextRunProperties _properties;

        public InlayHintTextRun(string text, TextRunProperties properties)
        {
            _text = text;
            _properties = properties;
        }

        public override CharacterBufferReference CharacterBufferReference =>
            new CharacterBufferReference(_text.ToCharArray(), 0);

        public override int Length => _text.Length;

        public override TextRunProperties Properties => _properties;
    }

    /// <summary>
    /// Text properties for inlay hints (styling).
    /// </summary>
    public class InlayHintTextRunProperties : TextRunProperties
    {
        private readonly TextRunProperties _baseProperties;
        private readonly InlayHintKind _kind;

        public InlayHintTextRunProperties(TextRunProperties baseProperties, InlayHintKind kind)
        {
            _baseProperties = baseProperties;
            _kind = kind;
        }

        public override Brush BackgroundBrush => new SolidColorBrush(Color.FromArgb(40, 128, 128, 128));

        public override CultureInfo CultureInfo => _baseProperties.CultureInfo;

        public override double FontHintingEmSize => _baseProperties.FontHintingEmSize * 0.85;

        public override double FontRenderingEmSize => _baseProperties.FontRenderingEmSize * 0.85;

        public override Brush ForegroundBrush => _kind == InlayHintKind.Parameter
            ? new SolidColorBrush(Color.FromRgb(150, 150, 150))
            : new SolidColorBrush(Color.FromRgb(78, 201, 176));

        public override TextDecorationCollection? TextDecorations => null;

        public override TextEffectCollection? TextEffects => null;

        public override Typeface Typeface => _baseProperties.Typeface;
    }
}
