using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.CodeCompletion;

namespace Code2Viz.Editor;

public class SignatureHelpProvider : IOverloadProvider, INotifyPropertyChanged
{
    private readonly List<string> _signatures;
    private int _selectedIndex;
    private int _currentParameterIndex;

    // VS Code-like colors
    private static readonly Brush ReturnTypeColor = new SolidColorBrush(Color.FromRgb(78, 201, 176));    // Teal
    private static readonly Brush MethodNameColor = new SolidColorBrush(Color.FromRgb(220, 220, 170));   // Light yellow
    private static readonly Brush ParamTypeColor = new SolidColorBrush(Color.FromRgb(86, 156, 214));     // Blue
    private static readonly Brush ParamNameColor = new SolidColorBrush(Color.FromRgb(156, 220, 254));    // Light blue
    private static readonly Brush PunctuationColor = new SolidColorBrush(Color.FromRgb(200, 200, 200));  // Light gray
    private static readonly Brush StaticColor = new SolidColorBrush(Color.FromRgb(128, 128, 128));       // Gray
    private static readonly Brush HighlightParamTypeColor = new SolidColorBrush(Color.FromRgb(255, 255, 255));  // White (highlighted)
    private static readonly Brush HighlightParamNameColor = new SolidColorBrush(Color.FromRgb(255, 200, 100));  // Orange-ish (highlighted)

    public SignatureHelpProvider(List<string> signatures, int currentParameterIndex = 0)
    {
        _signatures = signatures;
        _selectedIndex = 0;
        _currentParameterIndex = currentParameterIndex;
    }

    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            if (_selectedIndex != value)
            {
                _selectedIndex = value;
                OnPropertyChanged(nameof(SelectedIndex));
                OnPropertyChanged(nameof(CurrentHeader));
                OnPropertyChanged(nameof(CurrentContent));
                OnPropertyChanged(nameof(CurrentIndexText));
            }
        }
    }

    public int Count => _signatures.Count;

    public string CurrentIndexText => Count > 1 ? $"{_selectedIndex + 1}/{_signatures.Count}" : "";

    public object CurrentHeader
    {
        get
        {
            if (_signatures.Count == 0)
                return "";

            var signature = _signatures[_selectedIndex];
            return CreateStyledSignature(signature);
        }
    }

    public object CurrentContent => ""; // Could add parameter descriptions here

    private object CreateStyledSignature(string signature)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal };

        // Parse signature: "returnType methodName(params) (static)"
        // Example: "static Vector2 Create(float value)"
        var isStatic = signature.EndsWith(" (static)");
        if (isStatic)
            signature = signature.Substring(0, signature.Length - 9);

        // Find method name and parameters
        var parenStart = signature.IndexOf('(');
        var parenEnd = signature.LastIndexOf(')');

        if (parenStart > 0 && parenEnd > parenStart)
        {
            var beforeParen = signature.Substring(0, parenStart).Trim();
            var paramsStr = signature.Substring(parenStart + 1, parenEnd - parenStart - 1);

            // Split "returnType methodName"
            var lastSpace = beforeParen.LastIndexOf(' ');
            if (lastSpace > 0)
            {
                var returnType = beforeParen.Substring(0, lastSpace);
                var methodName = beforeParen.Substring(lastSpace + 1);

                // Add return type
                panel.Children.Add(CreateTextBlock(returnType, ReturnTypeColor));
                panel.Children.Add(CreateTextBlock(" ", PunctuationColor));

                // Add method name
                panel.Children.Add(CreateTextBlock(methodName, MethodNameColor));
            }
            else
            {
                // Constructor - no return type
                panel.Children.Add(CreateTextBlock(beforeParen, MethodNameColor));
            }

            // Add opening paren
            panel.Children.Add(CreateTextBlock("(", PunctuationColor));

            // Add parameters
            if (!string.IsNullOrWhiteSpace(paramsStr))
            {
                var parameters = SplitParameters(paramsStr);
                for (int i = 0; i < parameters.Length; i++)
                {
                    if (i > 0)
                        panel.Children.Add(CreateTextBlock(", ", PunctuationColor));

                    var isCurrentParam = (i == _currentParameterIndex);
                    var param = parameters[i].Trim();
                    var paramSpace = param.LastIndexOf(' ');
                    if (paramSpace > 0)
                    {
                        var paramType = param.Substring(0, paramSpace);
                        var paramName = param.Substring(paramSpace + 1);
                        panel.Children.Add(CreateTextBlock(paramType, isCurrentParam ? HighlightParamTypeColor : ParamTypeColor, null, isCurrentParam));
                        panel.Children.Add(CreateTextBlock(" ", PunctuationColor));
                        panel.Children.Add(CreateTextBlock(paramName, isCurrentParam ? HighlightParamNameColor : ParamNameColor, null, isCurrentParam));
                    }
                    else
                    {
                        panel.Children.Add(CreateTextBlock(param, isCurrentParam ? HighlightParamTypeColor : ParamTypeColor, null, isCurrentParam));
                    }
                }
            }

            // Add closing paren
            panel.Children.Add(CreateTextBlock(")", PunctuationColor));

            // Add static indicator
            if (isStatic)
            {
                panel.Children.Add(CreateTextBlock("  static", StaticColor, FontStyles.Italic));
            }
        }
        else
        {
            // Fallback - just show the signature as-is
            panel.Children.Add(CreateTextBlock(signature, Brushes.White));
        }

        return panel;
    }

    private static TextBlock CreateTextBlock(string text, Brush foreground, FontStyle? fontStyle = null, bool bold = false)
    {
        return new TextBlock
        {
            Text = text,
            Foreground = foreground,
            FontStyle = fontStyle ?? FontStyles.Normal,
            FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
            FontSize = 13
        };
    }

    private static string[] SplitParameters(string paramsStr)
    {
        // Handle generic types with commas inside angle brackets
        var result = new List<string>();
        var depth = 0;
        var start = 0;

        for (int i = 0; i < paramsStr.Length; i++)
        {
            var c = paramsStr[i];
            if (c == '<') depth++;
            else if (c == '>') depth--;
            else if (c == ',' && depth == 0)
            {
                result.Add(paramsStr.Substring(start, i - start));
                start = i + 1;
            }
        }

        result.Add(paramsStr.Substring(start));
        return result.ToArray();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
