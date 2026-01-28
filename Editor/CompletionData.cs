using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;

namespace Code2Viz.Editor;

public class CompletionData : ICompletionData
{
    private readonly string _text;
    private readonly string _description;

    /// <summary>
    /// Callback invoked when a method completion is performed, to trigger signature help.
    /// </summary>
    public static Action? OnMethodCompleted { get; set; }

    public CompletionData(string text, string description, CompletionKind kind)
    {
        _text = text;
        _description = description;
        Kind = kind;
    }

    public string Text => _text;
    public object Description => _description;
    public CompletionKind Kind { get; }

    public ImageSource? Image => null;

    // VS Code-like colors for different completion kinds
    private static readonly Brush KeywordColor = new SolidColorBrush(Color.FromRgb(86, 156, 214));   // Blue
    private static readonly Brush TypeColor = new SolidColorBrush(Color.FromRgb(78, 201, 176));      // Teal
    private static readonly Brush MethodColor = new SolidColorBrush(Color.FromRgb(220, 220, 170));   // Light yellow
    private static readonly Brush PropertyColor = new SolidColorBrush(Color.FromRgb(156, 220, 254)); // Light blue
    private static readonly Brush DescriptionColor = new SolidColorBrush(Color.FromRgb(128, 128, 128)); // Gray
    private static readonly Brush SnippetIconColor = new SolidColorBrush(Color.FromRgb(255, 152, 0));  // Orange

    public object Content
    {
        get
        {
            var nameColor = Kind switch
            {
                CompletionKind.Keyword => KeywordColor,
                CompletionKind.Type => TypeColor,
                CompletionKind.Method => MethodColor,
                CompletionKind.Delegate => MethodColor,  // Same color as methods
                CompletionKind.Property => PropertyColor,
                _ => Brushes.White
            };

            // VS-style icons for each kind
            var (icon, iconColor) = Kind switch
            {
                CompletionKind.Keyword => ("⏺", KeywordColor),      // Blue circle for keywords
                CompletionKind.Type => ("◆", TypeColor),            // Diamond for types/classes
                CompletionKind.Method => ("▶", MethodColor),        // Play symbol for methods
                CompletionKind.Delegate => ("▷", MethodColor),      // Hollow play for delegates
                CompletionKind.Property => ("◇", PropertyColor),    // Hollow diamond for properties
                CompletionKind.Snippet => ("⬡", SnippetIconColor),  // Hexagon for snippets
                _ => ("•", Brushes.White)
            };

            var panel = new StackPanel { Orientation = Orientation.Horizontal };
            
            // Icon (fixed width for alignment)
            var iconBlock = new TextBlock
            {
                Text = icon + " ",
                Foreground = iconColor,
                Width = 20,
                FontSize = 12
            };
            panel.Children.Add(iconBlock);
            
            // TextBlock for Name
            var nameBlock = new TextBlock
            {
                Text = _text,
                FontWeight = FontWeights.SemiBold
            };
            nameBlock.Style = CreateSelectionAwareStyle(nameColor);
            panel.Children.Add(nameBlock);

            // TextBlock for Description (show type signature)
            if (!string.IsNullOrWhiteSpace(_description))
            {
                var descBlock = new TextBlock
                {
                    Text = "  " + _description,
                    FontSize = 11
                };
                descBlock.Style = CreateSelectionAwareStyle(DescriptionColor);
                panel.Children.Add(descBlock);
            }

            return panel;
        }
    }

    private Style CreateSelectionAwareStyle(Brush defaultBrush)
    {
        var style = new Style(typeof(TextBlock));
        style.Setters.Add(new Setter(TextBlock.ForegroundProperty, defaultBrush));

        var trigger = new DataTrigger
        {
            Binding = new System.Windows.Data.Binding("IsSelected")
            {
                RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(ListBoxItem), 1)
            },
            Value = true
        };
        trigger.Setters.Add(new Setter(TextBlock.ForegroundProperty, Brushes.White));
        
        style.Triggers.Add(trigger);
        return style;
    }

    private double? _priority;
    public double Priority
    {
        get => _priority ?? Kind switch
        {
            CompletionKind.Keyword => 1.0,
            CompletionKind.Type => 2.0,
            CompletionKind.Property => 3.0,
            CompletionKind.Method => 3.0,
            CompletionKind.Delegate => 3.0,
            CompletionKind.Snippet => 0.5,
            _ => 1.0
        };
        set => _priority = value;
    }

    public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
    {
        var textToInsert = Text;

        // Add parentheses for methods
        if (Kind == CompletionKind.Method && !Text.EndsWith("()"))
        {
            textToInsert = Text + "()";
        }

        textArea.Document.Replace(completionSegment, textToInsert);

        // Position cursor inside parentheses for methods and trigger signature help
        if (Kind == CompletionKind.Method)
        {
            textArea.Caret.Offset = completionSegment.Offset + textToInsert.Length - 1;

            // Dispatch signature help trigger (deferred to allow completion window to close first)
            textArea.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, OnMethodCompleted);
        }
    }
}

public enum CompletionKind
{
    Keyword,
    Type,
    Property,
    Method,
    Delegate,  // Method reference used as delegate (no parentheses)
    Snippet
}

/// <summary>
/// Completion data for code snippets that insert multi-line templates.
/// Supports $0, $1, $2, etc. placeholders for Tab navigation.
/// </summary>
public class SnippetCompletionData : ICompletionData
{
    private readonly string _trigger;
    private readonly string _description;
    private readonly string _snippetCode;

    // Static reference to active snippet session (set by MainWindow)
    public static SnippetSession? ActiveSession { get; set; }

    private static readonly Brush SnippetColor = new SolidColorBrush(Color.FromRgb(255, 152, 0));  // Orange
    private static readonly Brush DescriptionColor = new SolidColorBrush(Color.FromRgb(128, 128, 128)); // Gray

    public SnippetCompletionData(string trigger, string description, string snippetCode)
    {
        _trigger = trigger;
        _description = description;
        _snippetCode = snippetCode;
    }

    public string Text => _trigger;
    public object Description => $"[Snippet] {_description}\n\n{GetDisplayCode()}";
    public ImageSource? Image => null;
    public double Priority => 0.5;  // Show snippets at top

    private string GetDisplayCode()
    {
        // Remove placeholder markers for display
        return System.Text.RegularExpressions.Regex.Replace(_snippetCode, @"\$\d+", "");
    }

    public object Content
    {
        get
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal };

            // Snippet icon/prefix
            var iconBlock = new TextBlock
            {
                Text = "⬡ ",
                Foreground = SnippetColor,
                FontWeight = FontWeights.Bold
            };
            panel.Children.Add(iconBlock);

            // Trigger text
            var nameBlock = new TextBlock
            {
                Text = _trigger,
                Foreground = SnippetColor,
                FontWeight = FontWeights.SemiBold
            };
            panel.Children.Add(nameBlock);

            // Description
            var descBlock = new TextBlock
            {
                Text = "  " + _description,
                Foreground = DescriptionColor
            };
            panel.Children.Add(descBlock);

            return panel;
        }
    }

    public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
    {
        // Use the snippet session if available
        if (ActiveSession != null)
        {
            ActiveSession.InsertSnippet(completionSegment.Offset, completionSegment.Length, _snippetCode);
        }
        else
        {
            // Fallback: simple insertion without placeholder support
            var cleanCode = System.Text.RegularExpressions.Regex.Replace(_snippetCode, @"\$\d+", "");
            textArea.Document.Replace(completionSegment, cleanCode);
        }
    }
}
