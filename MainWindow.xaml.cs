using System.IO;
using System.Text.RegularExpressions;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Folding;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using ICSharpCode.AvalonEdit.Search;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Windows.Threading;
using Code2Viz.Animation;
using Code2Viz.Canvas;
using Code2Viz.Console;
using Code2Viz.Editor;
using Code2Viz.Execution;
using Code2Viz.Export;
using Code2Viz.Project;
using ICSharpCode.AvalonEdit.Rendering;

namespace Code2Viz;

public partial class MainWindow : Window
{
    private readonly ModuleCompiler _compiler;
    private VizCodeProject? _currentProject;
    private VizCodeFile? _activeFile;
    private CompletionWindow? _completionWindow;
    private OverloadInsightWindow? _insightWindow;
    
    // Folding
    private FoldingManager? _foldingManager;
    private BraceFoldingStrategy? _foldingStrategy;
    private DispatcherTimer? _foldingTimer;

    // File system watcher for external changes
    private FileSystemWatcher? _projectWatcher;
    private DispatcherTimer? _fileWatcherDebounceTimer;

    // Snippet session for Tab navigation
    private SnippetSession? _snippetSession;
    private VizTextMarkerService? _textMarkerService;
    private RefactoringProvider? _refactoringProvider;
    private BracketHighlightRenderer? _bracketRenderer;

    // Real-time error checking
    private DispatcherTimer? _syntaxCheckTimer;
    private bool _textChangedSinceLastCheck;

    // Animation
    private DispatcherTimer? _animationTimer;
    private System.Diagnostics.Stopwatch _animationStopwatch = new();

    public static RoutedCommand RenameCommand = new RoutedCommand();

    public MainWindow(VizCodeProject? project = null)
    {
        InitializeComponent();

        _compiler = new ModuleCompiler();
        _refactoringProvider = new RefactoringProvider(_compiler);

        // Initialize snippet session
        _snippetSession = new SnippetSession(CodeEditor);
        SnippetCompletionData.ActiveSession = _snippetSession;

        InitializeEditor();
        InitializeCommands();
        InitializeCanvas();
        InitializeConsole();
        InitializeContextMenu();

        if (project != null)
        {
            _currentProject = project;
            LoadProjectTree();
            RefreshFileTabs();

            var entry = _currentProject.EntryPointFile;
            if (entry != null) SelectFile(entry);

            // Start watching for external changes
            StartProjectWatcher(_currentProject.ProjectDirectory);

            LoadSettingsToUI();
        }

        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        RenderCanvas.CenterOrigin();
    }

    private void InitializeCanvas()
    {
        RenderCanvas.MouseWorldPositionChanged += (s, pos) =>
        {
            CoordinatesText.Text = $"X: {pos.X:F2}  Y: {pos.Y:F2}";
        };

        // Animation Loop
        _animationTimer = new DispatcherTimer();
        _animationTimer.Interval = TimeSpan.FromMilliseconds(16); // ~60 FPS
        bool _needsInitialZoom = true;
        _animationTimer.Tick += (s, e) =>
        {
            var timeline = CanvasRenderer.Instance.ActiveTimeline;
            if (timeline != null && timeline.IsPlaying)
            {
                // Update animation state (sets DrawFactor, positions, etc.)
                timeline.Update(_animationStopwatch.Elapsed.TotalSeconds);

                // Redraw canvas with updated shape properties
                RenderCanvas.Refresh();

                // Zoom to fit on first frame that has visible shapes
                if (_needsInitialZoom && timeline.Shapes.Count > 0)
                {
                    RenderCanvas.ZoomExtents(CanvasRenderer.Instance.GetShapes());
                    _needsInitialZoom = false;
                }
            }
            else
            {
                _needsInitialZoom = true; // Reset for next timeline
            }
        };
        _animationTimer.Start();
    }

    private void InitializeConsole()
    {
        Console.ConsoleOutput.Instance.OutputChanged += (s, e) =>
        {
            Dispatcher.Invoke(RefreshConsole);
        };
    }

    private void InitializeContextMenu()
    {
        var contextMenu = new ContextMenu();
        
        // Standard Edit Commands with proper binding
        contextMenu.Items.Add(new MenuItem { Header = "Cut", Command = ApplicationCommands.Cut });
        contextMenu.Items.Add(new MenuItem { Header = "Copy", Command = ApplicationCommands.Copy });
        contextMenu.Items.Add(new MenuItem { Header = "Paste", Command = ApplicationCommands.Paste });
        
        contextMenu.Items.Add(new Separator());
        
        // Refactoring Commands
        var moveItem = new MenuItem 
        { 
            Header = "Move type to new file...", 
            Name = "MoveTypeMenuItem",
            Tag = "" // Initialize Tag
        };
        moveItem.Click += MoveTypeMenuItem_Click;
        contextMenu.Items.Add(moveItem);

        CodeEditor.ContextMenu = contextMenu;
        CodeEditor.ContextMenuOpening += CodeEditor_ContextMenuOpening;
    }

    private void RefreshConsole()
    {
        ConsoleListBox.ItemsSource = Console.ConsoleOutput.Instance.GetEntries();
        if (ConsoleListBox.Items.Count > 0)
        {
            ConsoleListBox.ScrollIntoView(ConsoleListBox.Items[ConsoleListBox.Items.Count - 1]);
        }
    }

    private void ClearConsoleButton_Click(object sender, RoutedEventArgs e)
    {
        Console.ConsoleOutput.Instance.Clear();
    }

    private void ConsoleCopy_Click(object sender, RoutedEventArgs e)
    {
        CopySelectedConsoleItems();
    }

    private void ConsoleSelectAll_Click(object sender, RoutedEventArgs e)
    {
        ConsoleListBox.SelectAll();
    }

    private void ConsoleListBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.C && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            CopySelectedConsoleItems();
            e.Handled = true;
        }
        else if (e.Key == Key.A && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            ConsoleListBox.SelectAll();
            e.Handled = true;
        }
    }

    private void ConsoleListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ConsoleListBox.SelectedItem is Console.ConsoleEntry entry && entry.IsClickable)
        {
            NavigateToError(entry.FilePath!, entry.LineNumber, entry.Column);
            e.Handled = true;
        }
    }

    private void NavigateToError(string filePath, int line, int column)
    {
        if (_currentProject == null) return;

        // Find and open the file in the project
        var file = _currentProject.Files.FirstOrDefault(f =>
            string.Equals(f.FilePath, filePath, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Path.GetFileName(f.FilePath), Path.GetFileName(filePath), StringComparison.OrdinalIgnoreCase));

        if (file != null)
        {
            // Switch to the file's tab
            SelectFile(file);

            // Navigate to the line and column
            try
            {
                // Ensure line is within bounds
                if (line > 0 && line <= CodeEditor.Document.LineCount)
                {
                    var lineObj = CodeEditor.Document.GetLineByNumber(line);
                    var col = Math.Max(1, Math.Min(column, lineObj.Length + 1));
                    var offset = CodeEditor.Document.GetOffset(line, col);

                    CodeEditor.CaretOffset = offset;
                    CodeEditor.ScrollToLine(line);
                    CodeEditor.Focus();

                    // Highlight the line briefly
                    CodeEditor.Select(lineObj.Offset, lineObj.Length);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"NavigateToError: {ex.Message}");
            }
        }
    }

    private void CopySelectedConsoleItems()
    {
        if (ConsoleListBox.SelectedItems.Count == 0) return;
        
        var lines = ConsoleListBox.SelectedItems
            .Cast<Console.ConsoleEntry>()
            .Select(m => m.Message);
        var text = string.Join(Environment.NewLine, lines);
        System.Windows.Clipboard.SetText(text);
    }

    private void ConsoleSplitter_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        ResetCanvasConsoleLayout();
    }

    private void ResetLayout_Click(object sender, RoutedEventArgs e)
    {
        ResetCanvasConsoleLayout();
    }

    private void HelpNav_Click(object sender, RoutedEventArgs e)
    {
        string? targetName = null;

        if (sender is System.Windows.Documents.Hyperlink link)
            targetName = link.Tag as string;
        else if (sender is Button btn)
            targetName = btn.Tag as string;

        if (targetName != null)
        {
            var target = FindName(targetName) as FrameworkElement;
            target?.BringIntoView();
        }
    }

    private void ResetCanvasConsoleLayout()
    {
        // Reset the canvas/console splitter
        CanvasRow.Height = new GridLength(1, GridUnitType.Star);
        ConsoleRow.Height = new GridLength(200);
    }

    private void Caret_PositionChanged(object? sender, EventArgs e)
    {
        if (_bracketRenderer == null) return;
        
        var result = BracketSearcher.SearchBracket(CodeEditor.Document, CodeEditor.CaretOffset);
        _bracketRenderer.Result = result;
        CodeEditor.TextArea.TextView.InvalidateLayer(KnownLayer.Selection);
    }

    private void ExportConsoleButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "Text Files (*.txt)|*.txt",
            DefaultExt = ".txt",
            FileName = "console_output"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                File.WriteAllText(dialog.FileName, Console.ConsoleOutput.Instance.GetFormattedOutput());
                SetStatus($"Console exported: {Path.GetFileName(dialog.FileName)}", isError: false);
            }
            catch (Exception ex)
            {
                SetStatus($"Export error: {ex.Message}", isError: true);
            }
        }
    }

    private void InitializeEditor()
    {
        // Enable built-in Find/Replace (Ctrl+F / Ctrl+H)
        SearchPanel.Install(CodeEditor);
        
        // Load syntax highlighting
        try
        {
            var assembly = typeof(MainWindow).Assembly;
            var resourceName = "Code2Viz.Editor.CSharpHighlighting.xshd";
            using var stream = assembly.GetManifestResourceStream(resourceName);

            if (stream != null)
            {
                using var reader = new XmlTextReader(stream);
                CodeEditor.SyntaxHighlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
            }
            else
            {
                MessageBox.Show($"Could not find embedded resource '{resourceName}'.\nAvailable resources:\n{string.Join("\n", assembly.GetManifestResourceNames())}", "Resource Error", MessageBoxButton.OK, MessageBoxImage.Error);
                CodeEditor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("C#");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading highlighting: {ex.Message}\n{ex.StackTrace}", "Highlighting Error", MessageBoxButton.OK, MessageBoxImage.Error);
            CodeEditor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("C#");
        }

        // Track changes in active file
        CodeEditor.TextChanged += (s, e) =>
        {
            if (_activeFile != null)
            {
                _activeFile.HasUnsavedChanges = true;
                RefreshFileTabs();
            }
        };

        // Initialize TextMarkerService
        _textMarkerService = new VizTextMarkerService(CodeEditor.Document);
        CodeEditor.TextArea.TextView.BackgroundRenderers.Add(_textMarkerService);

        CodeEditor.TextArea.TextView.Services.AddService(typeof(VizTextMarkerService), _textMarkerService);
        
        // Marker events
        CodeEditor.MouseHover += TextEditor_MouseHover;
        CodeEditor.MouseHoverStopped += TextEditor_MouseHoverStopped;
        
        // Refactoring key binding (Ctrl+.)
        CodeEditor.InputBindings.Add(new KeyBinding(RenameCommand, new KeyGesture(Key.OemPeriod, ModifierKeys.Control)));
        CommandBindings.Add(new CommandBinding(RenameCommand, Rename_Executed));

        // Setup autocomplete
        CodeEditor.TextArea.TextEntered += TextArea_TextEntered;
        CodeEditor.TextArea.TextEntering += TextArea_TextEntering;

        // Subscribe to method completion callback for signature help
        CompletionData.OnMethodCompleted = ShowSignatureHelp;

        // Setup auto-indentation on Enter key
        CodeEditor.TextArea.PreviewKeyDown += TextArea_PreviewKeyDown;

        // Initialize Bracket Highlighting
        _bracketRenderer = new BracketHighlightRenderer(CodeEditor.TextArea.TextView);
        CodeEditor.TextArea.TextView.BackgroundRenderers.Add(_bracketRenderer);
        CodeEditor.TextArea.Caret.PositionChanged += Caret_PositionChanged;

        // Initialize Folding
        if (_foldingManager == null)
        {
            _foldingManager = FoldingManager.Install(CodeEditor.TextArea);
        }
        _foldingStrategy = new BraceFoldingStrategy();
        
        // Timer for folding updates
        _foldingTimer = new DispatcherTimer();
        _foldingTimer.Interval = TimeSpan.FromSeconds(2);
        _foldingTimer.Tick += (s, e) =>
        {
            try
            {
                UpdateFoldings();
            }
            catch (Exception ex)
            {
                SetStatus($"Folding Error: {ex.Message}", true);
            }
        };
        _foldingTimer.Start();

        // Timer for real-time syntax checking (continuous interval)
        _syntaxCheckTimer = new DispatcherTimer();
        _syntaxCheckTimer.Interval = TimeSpan.FromMilliseconds(800);
        _syntaxCheckTimer.Tick += async (s, e) =>
        {
            if (_textChangedSinceLastCheck && _currentProject != null)
            {
                _textChangedSinceLastCheck = false;
                await PerformSyntaxCheckAsync();
            }
        };
        _syntaxCheckTimer.Start();

        // Track text changes for syntax checking
        CodeEditor.TextChanged += (s, e) => _textChangedSinceLastCheck = true;

        // Ctrl+MouseWheel to change font size
        CodeEditor.PreviewMouseWheel += CodeEditor_PreviewMouseWheel;
    }

    private void CodeEditor_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            // Ctrl+Wheel: change font size
            var currentSize = CodeEditor.FontSize;
            if (e.Delta > 0)
            {
                // Scroll up: increase font size
                CodeEditor.FontSize = Math.Min(currentSize + 1, 48);
            }
            else
            {
                // Scroll down: decrease font size
                CodeEditor.FontSize = Math.Max(currentSize - 1, 8);
            }
            e.Handled = true;
        }
    }

    #region Autocomplete

    private void TextArea_TextEntering(object sender, TextCompositionEventArgs e)
    {
        // Handle wrap selection with brackets
        if (e.Text.Length == 1 && !CodeEditor.TextArea.Selection.IsEmpty)
        {
            var closingChar = e.Text[0] switch
            {
                '(' => ')',
                '{' => '}',
                '[' => ']',
                '<' => '>',
                '"' => '"',
                '\'' => '\'',
                _ => '\0'
            };

            if (closingChar != '\0')
            {
                WrapSelectionWith(e.Text[0], closingChar);
                e.Handled = true;
                return;
            }
        }

        if (e.Text.Length > 0 && _completionWindow != null)
        {
            var ch = e.Text[0];
            if (!char.IsLetterOrDigit(ch))
            {
                // Space should close completion without committing (allows typing variable names)
                if (ch == ' ')
                {
                    _completionWindow.Close();
                }
                else
                {
                    // Commit completion on punctuation (.;,() etc.)
                    _completionWindow.CompletionList.RequestInsertion(e);
                }
            }
        }
    }

    private void WrapSelectionWith(char open, char close)
    {
        var selection = CodeEditor.TextArea.Selection;
        var selectedText = selection.GetText();
        var document = CodeEditor.Document;

        var startOffset = selection.SurroundingSegment.Offset;
        var length = selection.SurroundingSegment.Length;

        var wrappedText = $"{open}{selectedText}{close}";
        document.Replace(startOffset, length, wrappedText);

        // Clear selection and position caret after the closing bracket
        CodeEditor.TextArea.ClearSelection();
        CodeEditor.CaretOffset = startOffset + wrappedText.Length;
    }

    private void TextArea_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Handle Tab for snippet placeholder navigation
        if (e.Key == Key.Tab && _snippetSession != null && _snippetSession.IsActive)
        {
            if (Keyboard.Modifiers == ModifierKeys.Shift)
            {
                // Shift+Tab: previous placeholder
                if (_snippetSession.MoveToPreviousPlaceholder())
                {
                    e.Handled = true;
                    return;
                }
            }
            else if (Keyboard.Modifiers == ModifierKeys.None)
            {
                // Tab: next placeholder
                if (_snippetSession.MoveToNextPlaceholder())
                {
                    e.Handled = true;
                    return;
                }
                // If MoveToNextPlaceholder returns false, session ended - let Tab work normally
            }
        }

        // Handle Escape to cancel snippet session
        if (e.Key == Key.Escape && _snippetSession != null && _snippetSession.IsActive)
        {
            _snippetSession.EndSession();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter && !AutoIndentMenuItem.IsChecked)
            return;

        if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None)
        {
            e.Handled = HandleAutoIndentEnter();
        }
    }

    /// <summary>
    /// Triggers completion based on context (Ctrl+Space).
    /// </summary>
    private void TriggerManualCompletion()
    {
        if (_completionWindow != null)
            return;

        var offset = CodeEditor.CaretOffset;
        if (offset <= 0)
        {
            ShowGeneralCompletion();
            return;
        }

        // Check if we're right after a dot - show member completion
        var charBefore = CodeEditor.Document.GetCharAt(offset - 1);
        if (charBefore == '.')
        {
            ShowMemberCompletion();
            return;
        }

        // Check if we're inside method arguments - show argument completion
        if (IsInsideMethodArguments(offset))
        {
            ShowArgumentCompletion();
            return;
        }

        // Check if we're completing an identifier after a dot
        var identifierStart = FindIdentifierStart(CodeEditor.Document, offset);
        if (identifierStart > 0 && CodeEditor.Document.GetCharAt(identifierStart - 1) == '.')
        {
            // We're completing a partial member name (e.g., "tl.AddAni|")
            ShowMemberCompletionWithPrefix(identifierStart);
            return;
        }

        // Default: show general completion
        ShowGeneralCompletion();
    }

    /// <summary>
    /// Shows member completion with a prefix filter (for partial member names).
    /// </summary>
    private void ShowMemberCompletionWithPrefix(int identifierStart)
    {
        try
        {
            var offset = CodeEditor.CaretOffset;
            var prefix = CodeEditor.Document.GetText(identifierStart, offset - identifierStart);
            var dotOffset = identifierStart - 1;

            if (dotOffset < 0)
                return;

            var allCode = GetAllProjectCode();
            string? typeName = null;

            // Find the identifier before the dot
            var beforeDotStart = FindDottedIdentifierStart(CodeEditor.Document, dotOffset);
            if (beforeDotStart >= dotOffset)
                return;

            var fullIdentifier = CodeEditor.Document.GetText(beforeDotStart, dotOffset - beforeDotStart);
            if (string.IsNullOrEmpty(fullIdentifier))
                return;

            // Resolve variable type
            if (!fullIdentifier.Contains('.'))
            {
                var textBefore = CodeEditor.Document.GetText(0, beforeDotStart);
                typeName = CompletionProvider.FindVariableType(textBefore, fullIdentifier, allCode) ?? fullIdentifier;
            }
            else
            {
                typeName = fullIdentifier;
            }

            if (string.IsNullOrEmpty(typeName))
                return;

            var completions = CompletionProvider.GetMemberCompletions(typeName, allCode)
                .Where(c => c.Text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (completions.Count == 0)
                return;

            _completionWindow = new CompletionWindow(CodeEditor.TextArea);
            _completionWindow.StartOffset = identifierStart;
            StyleCompletionWindow(_completionWindow);

            var data = _completionWindow.CompletionList.CompletionData;
            foreach (var item in completions)
            {
                data.Add(item);
            }

            ShowCompletionWindowWithSelection();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ShowMemberCompletionWithPrefix error: {ex}");
        }
    }

    /// <summary>
    /// Checks if the caret is inside method arguments (between parentheses of a method call).
    /// </summary>
    private bool IsInsideMethodArguments(int offset)
    {
        var text = CodeEditor.Document.Text;
        var parenDepth = 0;

        // Scan backwards to find if we're inside parentheses
        for (int i = offset - 1; i >= 0; i--)
        {
            var c = text[i];
            if (c == ')')
                parenDepth++;
            else if (c == '(')
            {
                if (parenDepth == 0)
                {
                    // Found unmatched opening paren - check if it's a method call
                    if (i > 0)
                    {
                        var beforeParen = i - 1;
                        while (beforeParen >= 0 && char.IsWhiteSpace(text[beforeParen]))
                            beforeParen--;

                        if (beforeParen >= 0 && (char.IsLetterOrDigit(text[beforeParen]) || text[beforeParen] == '_'))
                            return true;
                    }
                    return false;
                }
                parenDepth--;
            }
            else if (c == ';' || c == '{' || c == '}')
            {
                // Statement boundary - not in method arguments
                return false;
            }
        }
        return false;
    }

    /// <summary>
    /// Finds the start of an identifier (letters, digits, underscore).
    /// </summary>
    private static int FindIdentifierStart(ICSharpCode.AvalonEdit.Document.TextDocument document, int offset)
    {
        while (offset > 0)
        {
            var c = document.GetCharAt(offset - 1);
            if (char.IsLetterOrDigit(c) || c == '_')
                offset--;
            else
                break;
        }
        return offset;
    }

    private bool HandleAutoIndentEnter()
    {
        var document = CodeEditor.Document;
        var offset = CodeEditor.CaretOffset;
        var line = document.GetLineByOffset(offset);
        var lineText = document.GetText(line.Offset, line.Length);

        // Get current indentation
        var currentIndent = GetLineIndentation(lineText);
        var trimmedLine = lineText.Trim();

        // Calculate new indentation
        var newIndent = currentIndent;

        // Increase indent after opening brace
        if (trimmedLine.EndsWith("{"))
        {
            newIndent += "    ";
        }

        // Check if we're between { and } - need to add extra line
        var afterCursor = document.GetText(offset, line.EndOffset - offset).Trim();
        if (trimmedLine.EndsWith("{") && afterCursor.StartsWith("}"))
        {
            // Insert newline + indent + newline + decreased indent + position cursor
            var closingIndent = currentIndent;
            document.Insert(offset, "\n" + newIndent + "\n" + closingIndent);
            CodeEditor.CaretOffset = offset + 1 + newIndent.Length;
            return true;
        }

        // Insert newline with proper indentation
        document.Insert(offset, "\n" + newIndent);
        CodeEditor.CaretOffset = offset + 1 + newIndent.Length;
        return true;
    }

    private static string GetLineIndentation(string line)
    {
        var indent = new System.Text.StringBuilder();
        foreach (var c in line)
        {
            if (c == ' ' || c == '\t')
                indent.Append(c);
            else
                break;
        }
        return indent.ToString();
    }

    private void HandleClosingBraceIndent()
    {
        var document = CodeEditor.Document;
        var offset = CodeEditor.CaretOffset;
        var line = document.GetLineByOffset(offset);
        var lineText = document.GetText(line.Offset, line.Length);

        // Only auto-dedent if the line only contains whitespace before the }
        var textBeforeBrace = lineText.Substring(0, offset - line.Offset - 1);
        if (!string.IsNullOrWhiteSpace(textBeforeBrace))
            return;

        // Find matching opening brace to determine proper indentation
        var matchingIndent = FindMatchingBraceIndent(document.Text, offset - 1);
        if (matchingIndent == null)
            return;

        // Replace the current line's indentation
        var newLineText = matchingIndent + lineText.TrimStart();
        document.Replace(line.Offset, line.Length, newLineText);

        // Position caret after the }
        CodeEditor.CaretOffset = line.Offset + matchingIndent.Length + 1;
    }

    private static string? FindMatchingBraceIndent(string text, int closingBracePos)
    {
        var depth = 1;
        for (int i = closingBracePos - 1; i >= 0; i--)
        {
            var c = text[i];
            if (c == '}')
                depth++;
            else if (c == '{')
            {
                depth--;
                if (depth == 0)
                {
                    // Found matching brace, get its line's indentation
                    var lineStart = text.LastIndexOf('\n', i) + 1;
                    var lineText = text.Substring(lineStart, i - lineStart + 1);
                    return GetLineIndentation(lineText);
                }
            }
        }
        return null;
    }

    private void TextArea_TextEntered(object sender, TextCompositionEventArgs e)
    {
        if (e.Text == ".")
        {
            // Dot completion - show members
            ShowMemberCompletion();
        }
        else if (e.Text == "(")
        {
            // Auto-close parenthesis and show signature help
            AutoInsertClosingBracket(')');
            ShowSignatureHelp();
        }
        else if (e.Text == "{")
        {
            // Auto-close curly brace
            AutoInsertClosingBracket('}');
        }
        else if (e.Text == "[")
        {
            // Auto-close square bracket
            AutoInsertClosingBracket(']');
        }
        else if (e.Text == "<")
        {
            // Auto-close angle bracket only in generic context
            if (ShouldAutoCloseAngleBracket())
            {
                AutoInsertClosingBracket('>');
            }
        }
        else if (e.Text == "\"")
        {
            // Auto-close double quote (if not already closing one)
            AutoInsertClosingQuote('"');
        }
        else if (e.Text == "'")
        {
            // Auto-close single quote (if not already closing one)
            AutoInsertClosingQuote('\'');
        }
        else if (e.Text == ")" || e.Text == "}" || e.Text == "]" || e.Text == ">")
        {
            // Skip over closing bracket if it matches
            SkipOverClosingBracket(e.Text[0]);

            if (e.Text == ")")
            {
                // Close signature help
                _insightWindow?.Close();
            }
            else if (e.Text == "}" && AutoIndentMenuItem.IsChecked)
            {
                // Auto-dedent closing brace
                HandleClosingBraceIndent();
            }
        }
        else if (e.Text == ",")
        {
            // Move to next parameter in signature help
            if (_insightWindow != null)
            {
                // Reshow signature help to update parameter highlighting
                _insightWindow.Close();
                ShowSignatureHelp();
            }

            // Show argument completion if inside method arguments
            if (IsInsideMethodArguments(CodeEditor.CaretOffset))
            {
                ShowArgumentCompletion();
            }
        }
        else if (e.Text == " ")
        {
            // Check if we just typed "new " - show type completions
            var offset = CodeEditor.CaretOffset;
            if (offset >= 4)
            {
                var textBefore = CodeEditor.Document.GetText(offset - 4, 3);
                if (textBefore == "new")
                {
                    ShowNewKeywordCompletion();
                }
            }
        }
        else if (char.IsLetter(e.Text[0]))
        {
            // Show general completions after typing a letter
            ShowGeneralCompletion();
        }
    }

    /// <summary>
    /// Performs syntax check and updates error markers.
    /// </summary>
    private async Task PerformSyntaxCheckAsync()
    {
        if (_currentProject == null) return;

        try
        {
            // Sync current editor content
            if (_activeFile != null)
            {
                _activeFile.Content = CodeEditor.Text;
            }

            var result = await _compiler.CheckSyntaxAsync(_currentProject);

            // Clear previous markers
            _textMarkerService?.Clear();

            var totalErrorCount = 0;

            // Handle C# diagnostics
            if (result.Diagnostics != null)
            {
                foreach (var diagnostic in result.Diagnostics)
                {
                    if (diagnostic.Severity != Microsoft.CodeAnalysis.DiagnosticSeverity.Error &&
                        diagnostic.Severity != Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                        continue;

                    var lineSpan = diagnostic.Location.GetLineSpan();

                    // Check if diagnostic belongs to the currently active file
                    var activePath = _activeFile?.FilePath;
                    bool isMatch = false;

                    if (activePath != null)
                    {
                        if (string.IsNullOrEmpty(lineSpan.Path))
                            isMatch = true;
                        else if (string.Equals(lineSpan.Path, activePath, StringComparison.OrdinalIgnoreCase))
                            isMatch = true;
                        else if (string.Equals(Path.GetFileName(lineSpan.Path), Path.GetFileName(activePath), StringComparison.OrdinalIgnoreCase))
                            isMatch = true;
                    }

                    if (isMatch)
                    {
                        var startLine = lineSpan.StartLinePosition.Line + 1;
                        var startCol = lineSpan.StartLinePosition.Character + 1;
                        var endLine = lineSpan.EndLinePosition.Line + 1;
                        var endCol = lineSpan.EndLinePosition.Character + 1;

                        try
                        {
                            var offset = CodeEditor.Document.GetOffset(new TextLocation(startLine, startCol));
                            var endOffset = CodeEditor.Document.GetOffset(new TextLocation(endLine, endCol));
                            var length = endOffset - offset;

                            if (length > 0)
                            {
                                var color = diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error ? Colors.Red : Colors.Orange;
                                _textMarkerService?.Create(offset, length, diagnostic.GetMessage(), color);

                                if (diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
                                    totalErrorCount++;
                            }
                        }
                        catch { /* Ignore invalid ranges */ }
                    }
                }
            }

            // Handle F# diagnostics
            if (result.FSharpDiagnostics != null)
            {
                foreach (var diagnostic in result.FSharpDiagnostics)
                {
                    if (diagnostic.Severity != "Error" && diagnostic.Severity != "Warning")
                        continue;

                    // Check if diagnostic belongs to the currently active file
                    var activePath = _activeFile?.FilePath;
                    bool isMatch = false;

                    if (activePath != null)
                    {
                        if (string.IsNullOrEmpty(diagnostic.FilePath))
                            isMatch = true;
                        else if (string.Equals(diagnostic.FilePath, activePath, StringComparison.OrdinalIgnoreCase))
                            isMatch = true;
                        else if (string.Equals(Path.GetFileName(diagnostic.FilePath), Path.GetFileName(activePath), StringComparison.OrdinalIgnoreCase))
                            isMatch = true;
                    }

                    if (isMatch)
                    {
                        try
                        {
                            var offset = CodeEditor.Document.GetOffset(new TextLocation(diagnostic.StartLine, diagnostic.StartColumn + 1));
                            var endOffset = CodeEditor.Document.GetOffset(new TextLocation(diagnostic.EndLine, diagnostic.EndColumn + 1));
                            var length = endOffset - offset;

                            if (length > 0)
                            {
                                var color = diagnostic.Severity == "Error" ? Colors.Red : Colors.Orange;
                                _textMarkerService?.Create(offset, length, diagnostic.Message, color);

                                if (diagnostic.Severity == "Error")
                                    totalErrorCount++;
                            }
                        }
                        catch { /* Ignore invalid ranges */ }
                    }
                }
            }

            // Update status bar with error count or clear it
            if (totalErrorCount > 0)
            {
                SetStatus($"{totalErrorCount} error{(totalErrorCount != 1 ? "s" : "")}", isError: true);
            }
            else
            {
                SetStatus("Ready", isError: false);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Syntax check error: {ex.Message}");
        }
    }

    /// <summary>
    /// Inserts a closing bracket at the current cursor position without moving the cursor.
    /// </summary>
    private void AutoInsertClosingBracket(char closingBracket)
    {
        var offset = CodeEditor.CaretOffset;

        // Don't auto-close if the next character is already the closing bracket
        if (offset < CodeEditor.Document.TextLength)
        {
            var nextChar = CodeEditor.Document.GetCharAt(offset);
            if (nextChar == closingBracket)
                return;
        }

        // Don't auto-close if next char is a letter/digit (likely not wanting auto-close)
        if (offset < CodeEditor.Document.TextLength)
        {
            var nextChar = CodeEditor.Document.GetCharAt(offset);
            if (char.IsLetterOrDigit(nextChar))
                return;
        }

        CodeEditor.Document.Insert(offset, closingBracket.ToString());
        CodeEditor.CaretOffset = offset; // Keep cursor before the closing bracket
    }

    /// <summary>
    /// Inserts a closing quote, handling the case where we might be closing an existing quote.
    /// </summary>
    private void AutoInsertClosingQuote(char quote)
    {
        var offset = CodeEditor.CaretOffset;

        // Check if we just closed a quote (typed quote after existing content)
        // Count quotes before cursor to determine if we're in a string
        var textBefore = CodeEditor.Document.GetText(0, offset);
        var quoteCount = 0;
        var escaped = false;

        for (int i = 0; i < textBefore.Length - 1; i++) // -1 because we just typed the quote
        {
            if (textBefore[i] == '\\' && !escaped)
            {
                escaped = true;
                continue;
            }
            if (textBefore[i] == quote && !escaped)
            {
                quoteCount++;
            }
            escaped = false;
        }

        // If odd number of quotes, we just closed a string - don't auto-insert
        if (quoteCount % 2 == 1)
            return;

        // Don't auto-close if next character is already the same quote
        if (offset < CodeEditor.Document.TextLength)
        {
            var nextChar = CodeEditor.Document.GetCharAt(offset);
            if (nextChar == quote)
                return;
        }

        // Don't auto-close if next char is a letter/digit
        if (offset < CodeEditor.Document.TextLength)
        {
            var nextChar = CodeEditor.Document.GetCharAt(offset);
            if (char.IsLetterOrDigit(nextChar))
                return;
        }

        CodeEditor.Document.Insert(offset, quote.ToString());
        CodeEditor.CaretOffset = offset;
    }

    /// <summary>
    /// Checks if we should auto-close angle bracket (for generics, not comparisons).
    /// </summary>
    private bool ShouldAutoCloseAngleBracket()
    {
        var offset = CodeEditor.CaretOffset;
        if (offset < 2)
            return false;

        // Look at what's before the '<' to determine if it's likely a generic
        var charBefore = CodeEditor.Document.GetCharAt(offset - 2);

        // If preceded by a letter (likely a type name), it's probably a generic
        if (char.IsLetter(charBefore) || charBefore == '_')
        {
            // Additional check: find the identifier before '<'
            var start = offset - 2;
            while (start > 0 && (char.IsLetterOrDigit(CodeEditor.Document.GetCharAt(start - 1)) || CodeEditor.Document.GetCharAt(start - 1) == '_'))
            {
                start--;
            }

            var identifier = CodeEditor.Document.GetText(start, offset - 1 - start);

            // Common generic type names
            var genericTypes = new[] { "List", "Dictionary", "HashSet", "Queue", "Stack",
                "IEnumerable", "IList", "ICollection", "IDictionary", "ISet",
                "Action", "Func", "Task", "Nullable", "Lazy", "Tuple", "ValueTuple",
                "KeyValuePair", "Span", "Memory", "ReadOnlySpan", "ReadOnlyMemory" };

            if (genericTypes.Any(t => identifier.EndsWith(t)))
                return true;

            // If it looks like a type name (starts with uppercase), probably generic
            if (identifier.Length > 0 && char.IsUpper(identifier[0]))
                return true;
        }

        return false;
    }

    /// <summary>
    /// If the character after cursor matches the typed closing bracket, skip over it instead of duplicating.
    /// </summary>
    private void SkipOverClosingBracket(char closingBracket)
    {
        var offset = CodeEditor.CaretOffset;

        // Check if we just typed a closing bracket and there's another one right after
        if (offset < CodeEditor.Document.TextLength)
        {
            var nextChar = CodeEditor.Document.GetCharAt(offset);
            if (nextChar == closingBracket)
            {
                // Delete the duplicate we just typed and move past the existing one
                CodeEditor.Document.Remove(offset - 1, 1);
                CodeEditor.CaretOffset = offset;
            }
        }
    }

    private void ShowCompletionWindow()
    {
        // Triggered by Ctrl+Space
        if (_completionWindow != null)
            return;

        var offset = CodeEditor.CaretOffset;
        var wordStart = FindWordStart(CodeEditor.Document, offset);
        var prefix = CodeEditor.Document.GetText(wordStart, offset - wordStart);

        // Check if we're after a dot
        if (wordStart > 0 && CodeEditor.Document.GetCharAt(wordStart - 1) == '.')
        {
            ShowMemberCompletion();
        }
        else
        {
            ShowGeneralCompletion();
        }
    }

    private string GetAllProjectCode()
    {
        if (_currentProject == null)
            return CodeEditor.Text;

        // Make sure current editor content is synced
        SaveCurrentEditorContent();

        return string.Join("\n\n", _currentProject.Files.Select(f => f.Content));
    }

    private void ShowGeneralCompletion()
    {
        if (_completionWindow != null)
            return;

        try
        {
            var offset = CodeEditor.CaretOffset;
            var wordStart = FindWordStart(CodeEditor.Document, offset);
            var prefix = CodeEditor.Document.GetText(wordStart, offset - wordStart);

            var allCode = GetAllProjectCode();
            var textBeforeCursor = CodeEditor.Document.GetText(0, wordStart);
            var completions = CompletionProvider.GetCompletions(prefix, allCode, textBeforeCursor).ToList();
            if (completions.Count == 0)
                return;

            _completionWindow = new CompletionWindow(CodeEditor.TextArea);
            _completionWindow.StartOffset = wordStart;
            StyleCompletionWindow(_completionWindow);

            var data = _completionWindow.CompletionList.CompletionData;
            foreach (var item in completions)
            {
                data.Add(item);
            }

            ShowCompletionWindowWithSelection();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ShowGeneralCompletion error: {ex.Message}");
        }
    }

    /// <summary>
    /// Shows completion for method arguments - variables, types, and expressions.
    /// </summary>
    private void ShowArgumentCompletion()
    {
        if (_completionWindow != null)
            return;

        try
        {
            var offset = CodeEditor.CaretOffset;
            var wordStart = FindWordStart(CodeEditor.Document, offset);
            var prefix = CodeEditor.Document.GetText(wordStart, offset - wordStart);

            var allCode = GetAllProjectCode();
            var textBeforeCursor = CodeEditor.Document.GetText(0, wordStart);

            // Get argument-appropriate completions (variables, types, no keywords except true/false/null)
            var completions = CompletionProvider.GetArgumentCompletions(prefix, allCode, textBeforeCursor).ToList();
            if (completions.Count == 0)
                return;

            _completionWindow = new CompletionWindow(CodeEditor.TextArea);
            _completionWindow.StartOffset = wordStart;
            StyleCompletionWindow(_completionWindow);

            var data = _completionWindow.CompletionList.CompletionData;
            foreach (var item in completions)
            {
                data.Add(item);
            }

            ShowCompletionWindowWithSelection();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ShowArgumentCompletion error: {ex.Message}");
        }
    }

    private void ShowNewKeywordCompletion()
    {
        if (_completionWindow != null)
            return;

        try
        {
            var allCode = GetAllProjectCode();
            var textBeforeCursor = CodeEditor.Document.GetText(0, CodeEditor.CaretOffset);

            // Get only type completions (no keywords) since we're after 'new'
            var completions = CompletionProvider.GetTypeCompletions(allCode, textBeforeCursor).ToList();
            if (completions.Count == 0)
                return;

            _completionWindow = new CompletionWindow(CodeEditor.TextArea);
            _completionWindow.StartOffset = CodeEditor.CaretOffset;
            StyleCompletionWindow(_completionWindow);

            var data = _completionWindow.CompletionList.CompletionData;
            foreach (var item in completions)
            {
                data.Add(item);
            }

            ShowCompletionWindowWithSelection();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ShowNewKeywordCompletion error: {ex.Message}");
        }
    }

    private void ShowMemberCompletion()
    {
        if (_completionWindow != null)
            return;

        try
        {
            var offset = CodeEditor.CaretOffset;
            var dotOffset = offset - 1;

            if (dotOffset < 0)
                return;

            var allCode = GetAllProjectCode();
            string? typeName = null;

            // Check if the character before the dot is ')' - method call or parenthesized expression
            // dotOffset is the position of the dot, so dotOffset-1 is the character before it
            if (dotOffset > 0)
            {
                var charBeforeDot = CodeEditor.Document.GetCharAt(dotOffset - 1);
                if (charBeforeDot == ')')
                {
                    // Find matching opening parenthesis (starting from the ')' position)
                    var closeParenPos = dotOffset - 1;
                    var openParen = FindMatchingOpenParen(closeParenPos);
                    if (openParen >= 0)
                    {
                        // Check if this is a method call: identifier( or identifier.method(
                        // Look for method name before the opening paren
                        var methodCallType = InferMethodCallReturnType(openParen, allCode);
                        if (methodCallType != null)
                        {
                            typeName = methodCallType;
                        }
                        else
                        {
                            // Fallback: treat as parenthesized expression
                            var expr = CodeEditor.Document.GetText(openParen + 1, closeParenPos - openParen - 1);
                            var textBefore = CodeEditor.Document.GetText(0, openParen);
                            typeName = InferExpressionType(expr, textBefore, allCode);
                        }
                    }
                }
            }

            // If not a parenthesized expression or couldn't infer type, try normal identifier
            if (typeName == null)
            {
                // Find the full dotted identifier before the dot (e.g., "System.Numerics")
                var identifierStart = FindDottedIdentifierStart(CodeEditor.Document, dotOffset);

                // Safety check to avoid negative length
                if (identifierStart >= dotOffset)
                    return;

                var fullIdentifier = CodeEditor.Document.GetText(identifierStart, dotOffset - identifierStart);

                if (string.IsNullOrEmpty(fullIdentifier))
                    return;

                // For simple identifiers (no dots), try to find variable type
                if (!fullIdentifier.Contains('.'))
                {
                    var textBefore = CodeEditor.Document.GetText(0, identifierStart);
                    typeName = CompletionProvider.FindVariableType(textBefore, fullIdentifier, allCode) ?? fullIdentifier;
                }
                else
                {
                    // For dotted identifiers, use as-is (could be namespace or nested type)
                    typeName = fullIdentifier;
                }
            }

            if (string.IsNullOrEmpty(typeName))
                return;

            var completions = CompletionProvider.GetMemberCompletions(typeName, allCode).ToList();
            if (completions.Count == 0)
                return;

            _completionWindow = new CompletionWindow(CodeEditor.TextArea);
            _completionWindow.StartOffset = offset;
            StyleCompletionWindow(_completionWindow);

            var data = _completionWindow.CompletionList.CompletionData;
            foreach (var item in completions)
            {
                data.Add(item);
            }

            ShowCompletionWindowWithSelection();
        }
        catch (Exception ex)
        {
            // Log to debug output
            System.Diagnostics.Debug.WriteLine($"ShowMemberCompletion error: {ex}");
            SetStatus($"Autocomplete error: {ex.Message}", true);
        }
    }

    /// <summary>
    /// Finds the matching opening parenthesis for a closing paren at the given offset.
    /// </summary>
    private int FindMatchingOpenParen(int closeParenOffset)
    {
        var document = CodeEditor.Document;
        var depth = 1;

        for (int i = closeParenOffset - 1; i >= 0; i--)
        {
            var c = document.GetCharAt(i);
            if (c == ')')
                depth++;
            else if (c == '(')
            {
                depth--;
                if (depth == 0)
                    return i;
            }
        }
        return -1;
    }

    /// <summary>
    /// Infers the result type of an expression like "p1 - p2" or "a + b".
    /// </summary>
    private string? InferExpressionType(string expression, string textBefore, string allCode)
    {
        expression = expression.Trim();

        // Handle binary operations: look for +, -, *, / operators
        // Find the last operator at depth 0 (not inside nested parens)
        var operatorIndex = -1;
        var depth = 0;
        for (int i = expression.Length - 1; i >= 0; i--)
        {
            var c = expression[i];
            if (c == ')') depth++;
            else if (c == '(') depth--;
            else if (depth == 0 && (c == '+' || c == '-' || c == '*' || c == '/'))
            {
                // Make sure it's a binary operator, not unary
                if (i > 0 && !IsOperatorChar(expression[i - 1]))
                {
                    operatorIndex = i;
                    break;
                }
            }
        }

        if (operatorIndex > 0)
        {
            // Binary expression - get the left operand and infer its type
            var leftOperand = expression.Substring(0, operatorIndex).Trim();

            // Extract the identifier from the left operand
            var leftId = ExtractLastIdentifier(leftOperand);
            if (!string.IsNullOrEmpty(leftId))
            {
                var leftType = CompletionProvider.FindVariableType(textBefore, leftId, allCode);
                if (leftType != null)
                {
                    // For arithmetic operations on same type, result is usually same type
                    return leftType;
                }
            }
        }

        // Single identifier or method call
        var identifier = ExtractLastIdentifier(expression);
        if (!string.IsNullOrEmpty(identifier))
        {
            return CompletionProvider.FindVariableType(textBefore, identifier, allCode);
        }

        return null;
    }

    /// <summary>
    /// Infers the return type of a method call expression like "points.First()" or "list.Where(x => x > 0)".
    /// </summary>
    /// <param name="openParenOffset">The offset of the opening parenthesis of the method call.</param>
    /// <param name="allCode">All code in the project for context.</param>
    /// <returns>The inferred return type, or null if cannot be determined.</returns>
    private string? InferMethodCallReturnType(int openParenOffset, string allCode)
    {
        try
        {
            // Find the method name before the opening paren
            var methodNameEnd = openParenOffset;
            var methodNameStart = methodNameEnd;
            while (methodNameStart > 0 && char.IsLetterOrDigit(CodeEditor.Document.GetCharAt(methodNameStart - 1)))
                methodNameStart--;

            if (methodNameStart >= methodNameEnd)
                return null;

            var methodName = CodeEditor.Document.GetText(methodNameStart, methodNameEnd - methodNameStart);

            // Check if there's a dot before the method name (object.Method pattern)
            if (methodNameStart > 0 && CodeEditor.Document.GetCharAt(methodNameStart - 1) == '.')
            {
                var dotPos = methodNameStart - 1;

                // Find what's before the dot - could be another method call like "points.First().Count()"
                // or a simple identifier like "points.First()"
                string? objectType = null;

                // Check if there's a ')' before the dot (chained method call)
                if (dotPos > 0 && CodeEditor.Document.GetCharAt(dotPos - 1) == ')')
                {
                    // Recursive: find the return type of the preceding method call
                    var prevCloseParen = dotPos - 1;
                    var prevOpenParen = FindMatchingOpenParen(prevCloseParen);
                    if (prevOpenParen >= 0)
                    {
                        objectType = InferMethodCallReturnType(prevOpenParen, allCode);
                    }
                }
                else
                {
                    // Simple identifier before the dot
                    var identStart = FindDottedIdentifierStart(CodeEditor.Document, dotPos);
                    if (identStart < dotPos)
                    {
                        var objectName = CodeEditor.Document.GetText(identStart, dotPos - identStart);
                        var textBefore = CodeEditor.Document.GetText(0, identStart);
                        objectType = CompletionProvider.FindVariableType(textBefore, objectName, allCode);
                    }
                }

                if (!string.IsNullOrEmpty(objectType))
                {
                    return InferMethodReturnType(objectType, methodName);
                }
            }
        }
        catch
        {
            // Ignore parsing errors
        }

        return null;
    }

    /// <summary>
    /// Given an object type and method name, infers the return type.
    /// Handles common LINQ methods and collection methods.
    /// </summary>
    private static string? InferMethodReturnType(string objectType, string methodName)
    {
        // Extract element type from generic collection (e.g., List<VPoint> -> VPoint)
        string? elementType = null;
        var genericMatch = System.Text.RegularExpressions.Regex.Match(objectType, @"<(.+)>$");
        if (genericMatch.Success)
        {
            elementType = genericMatch.Groups[1].Value;
            // Handle nested generics - for Dictionary<K,V>, just take the first type for simplicity
            // For List<List<T>>, elementType would be List<T>
        }

        // Normalize method name for comparison
        var method = methodName.ToLowerInvariant();

        // LINQ methods that return element type (T)
        var elementReturningMethods = new HashSet<string>
        {
            "first", "firstordefault", "last", "lastordefault",
            "single", "singleordefault", "elementat", "elementatordefault",
            "min", "max", "aggregate"
        };

        if (elementReturningMethods.Contains(method) && !string.IsNullOrEmpty(elementType))
        {
            return elementType;
        }

        // LINQ methods that return the same collection type (or IEnumerable<T>)
        var collectionReturningMethods = new HashSet<string>
        {
            "where", "orderby", "orderbydescending", "thenby", "thenbydescending",
            "take", "takewhile", "skip", "skipwhile", "distinct", "reverse",
            "union", "intersect", "except", "concat", "append", "prepend",
            "defaultifempty", "asenumerable"
        };

        if (collectionReturningMethods.Contains(method) && !string.IsNullOrEmpty(elementType))
        {
            // Return as IEnumerable<T> for LINQ methods
            return $"IEnumerable<{elementType}>";
        }

        // LINQ methods that return bool
        var boolReturningMethods = new HashSet<string> { "any", "all", "contains", "sequenceequal" };
        if (boolReturningMethods.Contains(method))
        {
            return "bool";
        }

        // LINQ methods that return int
        if (method == "count")
        {
            return "int";
        }

        // LINQ methods that return long
        if (method == "longcount")
        {
            return "long";
        }

        // Conversion methods
        if (method == "tolist" && !string.IsNullOrEmpty(elementType))
        {
            return $"List<{elementType}>";
        }

        if (method == "toarray" && !string.IsNullOrEmpty(elementType))
        {
            return $"{elementType}[]";
        }

        if (method == "tohashset" && !string.IsNullOrEmpty(elementType))
        {
            return $"HashSet<{elementType}>";
        }

        if (method == "todictionary" && !string.IsNullOrEmpty(elementType))
        {
            // Simplified - actual key type depends on selector
            return $"Dictionary<object,{elementType}>";
        }

        // Select returns different type - for now, return generic IEnumerable
        if (method == "select" && !string.IsNullOrEmpty(elementType))
        {
            return "IEnumerable<object>";
        }

        if (method == "selectmany" && !string.IsNullOrEmpty(elementType))
        {
            return "IEnumerable<object>";
        }

        // GroupBy returns IEnumerable<IGrouping>
        if (method == "groupby")
        {
            return "IEnumerable<IGrouping>";
        }

        // Sum/Average return numeric types
        if (method == "sum" || method == "average")
        {
            return "double";
        }

        // String methods
        if (objectType == "string" || objectType == "String")
        {
            if (method == "substring" || method == "trim" || method == "tolower" || method == "toupper" ||
                method == "replace" || method == "remove" || method == "insert" || method == "padleft" || method == "padright")
                return "string";
            if (method == "split")
                return "string[]";
            if (method == "indexof" || method == "lastindexof" || method == "compareto")
                return "int";
            if (method == "startswith" || method == "endswith" || method == "contains" || method == "equals")
                return "bool";
            if (method == "tochararray")
                return "char[]";
        }

        // List-specific methods
        if (objectType.StartsWith("List<") || objectType.StartsWith("IList<"))
        {
            if (method == "find" || method == "findlast" && !string.IsNullOrEmpty(elementType))
                return elementType;
            if (method == "findall" && !string.IsNullOrEmpty(elementType))
                return $"List<{elementType}>";
            if (method == "findindex" || method == "findlastindex" || method == "indexof" || method == "lastindexof" ||
                method == "binarysearch" || method == "removeall")
                return "int";
            if (method == "getrange" && !string.IsNullOrEmpty(elementType))
                return $"List<{elementType}>";
            if (method == "exists" || method == "trueforall" || method == "remove")
                return "bool";
            if (method == "toarray" && !string.IsNullOrEmpty(elementType))
                return $"{elementType}[]";
        }

        return null;
    }

    private static bool IsOperatorChar(char c) => c == '+' || c == '-' || c == '*' || c == '/' || c == '=' || c == '<' || c == '>';

    private static string? ExtractLastIdentifier(string expr)
    {
        expr = expr.Trim();
        // Find the last word-like identifier
        var end = expr.Length;
        while (end > 0 && !char.IsLetterOrDigit(expr[end - 1]) && expr[end - 1] != '_')
            end--;

        var start = end;
        while (start > 0 && (char.IsLetterOrDigit(expr[start - 1]) || expr[start - 1] == '_'))
            start--;

        if (start < end)
            return expr.Substring(start, end - start);

        return null;
    }

    private static int FindWordStart(TextDocument document, int offset)
    {
        var start = offset;
        while (start > 0)
        {
            var c = document.GetCharAt(start - 1);
            if (!char.IsLetterOrDigit(c) && c != '_')
                break;
            start--;
        }
        return start;
    }

    private static int FindDottedIdentifierStart(TextDocument document, int offset)
    {
        var start = offset;
        while (start > 0)
        {
            var c = document.GetCharAt(start - 1);
            if (!char.IsLetterOrDigit(c) && c != '_' && c != '.')
                break;
            start--;
        }
        return start;
    }

    private void ShowSignatureHelp()
    {
        if (_insightWindow != null)
            return;

        var offset = CodeEditor.CaretOffset;

        // Find the opening parenthesis by searching backwards
        // This handles both cases: right after '(' and inside arguments (after comma)
        var parenOffset = FindOpeningParenthesis(offset);
        if (parenOffset < 0)
            return;

        // Find the method/constructor name before the parenthesis
        var nameEnd = parenOffset;
        var nameStart = FindDottedIdentifierStart(CodeEditor.Document, nameEnd);
        var fullName = CodeEditor.Document.GetText(nameStart, nameEnd - nameStart);

        if (string.IsNullOrEmpty(fullName))
            return;

        // Check if this is "new TypeName(" - constructor call
        var textBefore = nameStart >= 4 ? CodeEditor.Document.GetText(nameStart - 4, 4) : "";
        var isConstructor = textBefore.TrimEnd() == "new" || textBefore.EndsWith("new ");

        // Count commas to determine current parameter index
        var currentParamIndex = CountCommasBeforeCursor(parenOffset + 1, offset);

        // Get signatures
        List<string> signatures;
        if (isConstructor)
        {
            // First try built-in types via reflection
            signatures = TypeInspector.GetConstructorSignatures(fullName);

            // If not found, try custom classes from user code
            if (signatures.Count == 0)
            {
                var allCode = GetAllProjectCode();
                signatures = CompletionProvider.GetCustomConstructorSignatures(fullName, allCode);
            }
        }
        else
        {
            signatures = GetMethodSignatures(fullName);
        }

        if (signatures.Count == 0)
            return;

        _insightWindow = new OverloadInsightWindow(CodeEditor.TextArea);
        _insightWindow.Provider = new SignatureHelpProvider(signatures, currentParamIndex);
        
        // precise range to keep window open
        _insightWindow.StartOffset = parenOffset;
        var closingParen = FindClosingParenthesis(offset);
        if (closingParen > parenOffset)
        {
             _insightWindow.EndOffset = closingParen + 1;
        }
        else
        {
             // Fallback if no closing paren found (e.g. typing at end of file)
             _insightWindow.EndOffset = CodeEditor.Document.TextLength;
        }

        StyleInsightWindow(_insightWindow);
        _insightWindow.Show();
        _insightWindow.Closed += (s, e) => _insightWindow = null;
    }

    private int FindClosingParenthesis(int fromOffset)
    {
        var document = CodeEditor.Document;
        var depth = 1;

        for (int i = fromOffset; i < document.TextLength; i++)
        {
            var c = document.GetCharAt(i);
            if (c == '(')
            {
                depth++;
            }
            else if (c == ')')
            {
                depth--;
                if (depth == 0)
                    return i;
            }
        }
        return -1;
    }

    /// <summary>
    /// Finds the position of the opening parenthesis for the current method call.
    /// Handles nested parentheses correctly.
    /// </summary>
    private int FindOpeningParenthesis(int fromOffset)
    {
        var document = CodeEditor.Document;
        var depth = 0;

        for (int i = fromOffset - 1; i >= 0; i--)
        {
            var c = document.GetCharAt(i);
            if (c == ')')
            {
                depth++;
            }
            else if (c == '(')
            {
                if (depth == 0)
                    return i;
                depth--;
            }
            else if (c == ';' || c == '{' || c == '}')
            {
                // Stop searching at statement boundaries
                return -1;
            }
        }

        return -1;
    }

    /// <summary>
    /// Counts the number of commas between two positions to determine current parameter index.
    /// Handles nested parentheses and strings.
    /// </summary>
    private int CountCommasBeforeCursor(int startOffset, int endOffset)
    {
        var document = CodeEditor.Document;
        var count = 0;
        var parenDepth = 0;
        var inString = false;
        var inChar = false;

        for (int i = startOffset; i < endOffset && i < document.TextLength; i++)
        {
            var c = document.GetCharAt(i);
            var prev = i > 0 ? document.GetCharAt(i - 1) : '\0';

            // Handle escape sequences
            if ((inString || inChar) && prev == '\\')
                continue;

            // Toggle string state
            if (c == '"' && !inChar)
            {
                inString = !inString;
                continue;
            }

            // Toggle char state
            if (c == '\'' && !inString)
            {
                inChar = !inChar;
                continue;
            }

            if (inString || inChar)
                continue;

            // Track parenthesis depth
            if (c == '(') parenDepth++;
            else if (c == ')') parenDepth--;
            else if (c == ',' && parenDepth == 0)
                count++;
        }

        return count;
    }

    private List<string> GetMethodSignatures(string fullName)
    {
        // Split into type and method name
        var lastDot = fullName.LastIndexOf('.');
        if (lastDot < 0)
        {
            // Could be a local method or a type (for static methods)
            return TypeInspector.GetMethodSignatures(fullName, fullName);
        }

        var typePart = fullName.Substring(0, lastDot);
        var methodName = fullName.Substring(lastDot + 1);

        // Try to resolve the type
        var allCode = GetAllProjectCode();
        var textBefore = CodeEditor.Document.GetText(0, CodeEditor.CaretOffset);

        // Check if typePart is a variable
        var actualType = CompletionProvider.FindVariableType(textBefore, typePart, allCode);
        if (actualType != null)
        {
            return TypeInspector.GetMethodSignatures(actualType, methodName);
        }

        // typePart could be a type name or namespace.type
        return TypeInspector.GetMethodSignatures(typePart, methodName);
    }

    private void StyleCompletionWindow(CompletionWindow window)
    {
        // Dark theme colors
        var darkBackground = new SolidColorBrush(Color.FromRgb(30, 30, 30));
        var darkBorder = new SolidColorBrush(Color.FromRgb(60, 60, 60));

        window.Background = darkBackground;
        window.BorderBrush = darkBorder;
        window.BorderThickness = new Thickness(1);

        // Style the completion list
        window.CompletionList.Background = darkBackground;
        window.CompletionList.Foreground = Brushes.White;

        // Set minimum width for better readability
        window.MinWidth = 400;
        window.MaxWidth = 800;
    }

    /// <summary>
    /// Shows the completion window with the first item selected.
    /// </summary>
    private void ShowCompletionWindowWithSelection()
    {
        if (_completionWindow == null || _completionWindow.CompletionList.CompletionData.Count == 0)
            return;

        // Select the first item
        _completionWindow.CompletionList.SelectedItem = _completionWindow.CompletionList.CompletionData[0];

        _completionWindow.Show();
        _completionWindow.Closed += (s, e) => _completionWindow = null;
    }

    private void StyleInsightWindow(OverloadInsightWindow window)
    {
        // Dark theme colors
        var darkBackground = new SolidColorBrush(Color.FromRgb(30, 30, 30));
        var darkBorder = new SolidColorBrush(Color.FromRgb(60, 60, 60));

        window.Background = darkBackground;
        window.BorderBrush = darkBorder;
        window.BorderThickness = new Thickness(1);
        window.Foreground = Brushes.White;

        // Make window wider for better readability
        window.MinWidth = 500;
        window.MaxWidth = 900;
    }

    #endregion

    private void InitializeCommands()
    {
        PreviewKeyDown += MainWindow_PreviewKeyDown;
    }

    #region Project Management



    private void LoadProject(string projectFilePath)
    {
        try
        {
            // Stop existing watcher
            StopProjectWatcher();

            _currentProject = VizCodeProject.Load(projectFilePath);
            LoadProjectTree();
            RefreshFileTabs();

            var fileToSelect = _currentProject.EntryPointFile ?? _currentProject.Files.FirstOrDefault();
            if (fileToSelect != null)
            {
                SelectFile(fileToSelect);
            }

            // Start watching for external changes
            StartProjectWatcher(_currentProject.ProjectDirectory);

            // Add to recent projects
            Project.RecentProjectsManager.AddProject(projectFilePath, _currentProject.ProjectFile.Name);

            SetStatus($"Loaded project: {_currentProject.Files.Count} file(s)", isError: false);
            LoadSettingsToUI();
        }
        catch (Exception ex)
        {
            SetStatus($"Error loading project: {ex.Message}", isError: true);
        }
    }

    private void RefreshFileTabs()
    {
        var selectedFile = _activeFile;
        FileTabs.ItemsSource = null;
        FileTabs.ItemsSource = _currentProject?.Files;

        if (selectedFile != null && _currentProject?.Files.Contains(selectedFile) == true)
        {
            FileTabs.SelectedItem = selectedFile;
        }
    }

    private void SelectFile(VizCodeFile file)
    {
        // Save current editor content before switching
        SaveCurrentEditorContent();

        _activeFile = file;
        CodeEditor.Text = file.Content;
        
        UpdateSyntaxHighlighting(file.FileName);

        // Select the tab without triggering SelectionChanged recursively
        if (FileTabs.SelectedItem != file)
        {
            FileTabs.SelectedItem = file;
        }
    }

    private void UpdateSyntaxHighlighting(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLower();
        var isFSharp = ext == ".fs" || ext == ".fsi" || ext == ".fsx" || ext == ".fsscript";
        
        // If file has no extension (e.g. unsaved new file might need logic), fallback to project language
        if (string.IsNullOrEmpty(ext) && _currentProject != null)
        {
            isFSharp = _currentProject.ProjectFile.Language == ProjectLanguage.FSharp;
        }

        var resourceName = isFSharp ? "Code2Viz.Editor.FSharpHighlighting.xshd" : "Code2Viz.Editor.CSharpHighlighting.xshd";
        
        try
        {
            var assembly = typeof(MainWindow).Assembly;
            using var stream = assembly.GetManifestResourceStream(resourceName);

            if (stream != null)
            {
                using var reader = new XmlTextReader(stream);
                CodeEditor.SyntaxHighlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
            }
            else
            {
                // Fallback
                CodeEditor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition(isFSharp ? "F#" : "C#");
            }
        }
        catch
        {
            // Fallback
             CodeEditor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("C#");
        }
    }

    private void SaveCurrentEditorContent()
    {
        if (_activeFile != null && CodeEditor.Text != _activeFile.Content)
        {
            _activeFile.Content = CodeEditor.Text;
        }
    }

    private bool PromptSaveChanges()
    {
        if (_currentProject == null)
            return true;

        SaveCurrentEditorContent();

        var unsavedFiles = _currentProject.Files.Where(f => f.HasUnsavedChanges).ToList();
        if (unsavedFiles.Count == 0)
            return true;

        var result = MessageBox.Show(
            $"You have {unsavedFiles.Count} unsaved file(s). Save changes?",
            "Unsaved Changes",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Cancel)
            return false;

        if (result == MessageBoxResult.Yes)
        {
            // If project is in temp directory, prompt for save location
            if (_currentProject.ProjectDirectory.StartsWith(Path.GetTempPath()))
            {
                if (!SaveProjectToNewLocation())
                    return false;
            }
            else
            {
                _currentProject.SaveAllFiles();
            }
        }

        return true;
    }

    private bool SaveProjectToNewLocation()
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select folder to save VizCode project",
            UseDescriptionForTitle = true
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            _currentProject?.MoveToDirectory(dialog.SelectedPath);
            RefreshFileTabs();
            return true;
        }

        return false;
    }

    private void ManagePackagesMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (_currentProject == null)
        {
            SetStatus("No project open", isError: true);
            return;
        }
        var win = new NuGetPackageManagerWindow(_currentProject);
        win.Owner = this;
        win.ShowDialog();
        
        // Refresh project tree to show .packages folder if created
        LoadProjectTree();
        RefreshFileTabs(); // In case any files were modified/added externally (unlikely but good practice)
    }

    #region File System Watcher

    private void StartProjectWatcher(string projectDirectory)
    {
        if (string.IsNullOrEmpty(projectDirectory) || !Directory.Exists(projectDirectory))
            return;

        try
        {
            _projectWatcher = new FileSystemWatcher(projectDirectory)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite,
                IncludeSubdirectories = true,
                EnableRaisingEvents = true
            };

            // Watch for .cs and .fs files
            _projectWatcher.Created += OnProjectFileChanged;
            _projectWatcher.Deleted += OnProjectFileChanged;
            _projectWatcher.Renamed += OnProjectFileRenamed;
            _projectWatcher.Changed += OnProjectFileChanged;

            // Initialize debounce timer for batching rapid changes
            _fileWatcherDebounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _fileWatcherDebounceTimer.Tick += (s, e) =>
            {
                _fileWatcherDebounceTimer.Stop();
                RefreshProjectFromDisk();
            };
        }
        catch (Exception ex)
        {
            SetStatus($"Warning: Could not start file watcher: {ex.Message}", isError: false);
        }
    }

    private void StopProjectWatcher()
    {
        if (_projectWatcher != null)
        {
            _projectWatcher.EnableRaisingEvents = false;
            _projectWatcher.Created -= OnProjectFileChanged;
            _projectWatcher.Deleted -= OnProjectFileChanged;
            _projectWatcher.Renamed -= OnProjectFileRenamed;
            _projectWatcher.Changed -= OnProjectFileChanged;
            _projectWatcher.Dispose();
            _projectWatcher = null;
        }

        _fileWatcherDebounceTimer?.Stop();
    }

    private void OnProjectFileChanged(object sender, FileSystemEventArgs e)
    {
        // React to source code files or directories
        if (!ShouldRefreshForPath(e.FullPath)) return;

        // Debounce rapid changes
        Dispatcher.Invoke(() =>
        {
            _fileWatcherDebounceTimer?.Stop();
            _fileWatcherDebounceTimer?.Start();
        });
    }

    private void OnProjectFileRenamed(object sender, RenamedEventArgs e)
    {
        // React if either old or new path should trigger refresh
        if (!ShouldRefreshForPath(e.FullPath) && !ShouldRefreshForPath(e.OldFullPath)) return;

        Dispatcher.Invoke(() =>
        {
            _fileWatcherDebounceTimer?.Stop();
            _fileWatcherDebounceTimer?.Start();
        });
    }

    private bool ShouldRefreshForPath(string path)
    {
        // Always refresh for directories (folder created/deleted)
        if (Directory.Exists(path) || !Path.HasExtension(path))
            return true;

        // Refresh for source code files
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext == ".cs" || ext == ".fs";
    }

    private void RefreshProjectFromDisk()
    {
        if (_currentProject == null) return;

        try
        {
            // Remember current active file
            var currentActiveFilePath = _activeFile?.FilePath;

            // Refresh files from disk
            _currentProject.RefreshFilesFromDisk();

            // Refresh UI
            LoadProjectTree();
            RefreshFileTabs();

            // Restore active file if still exists
            if (!string.IsNullOrEmpty(currentActiveFilePath))
            {
                var restoredFile = _currentProject.Files.FirstOrDefault(f =>
                    f.FilePath.Equals(currentActiveFilePath, StringComparison.OrdinalIgnoreCase));
                if (restoredFile != null && restoredFile != _activeFile)
                {
                    SelectFile(restoredFile);
                }
                else if (_activeFile != null && !_currentProject.Files.Contains(_activeFile))
                {
                    // Active file was deleted, select entry point or first file
                    var fallback = _currentProject.EntryPointFile ?? _currentProject.Files.FirstOrDefault();
                    if (fallback != null) SelectFile(fallback);
                }
            }

            SetStatus("Project refreshed from disk", isError: false);
        }
        catch (Exception ex)
        {
            SetStatus($"Error refreshing project: {ex.Message}", isError: true);
        }
    }

    #endregion

    #endregion

    #region Tab Events

    private void FileTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FileTabs.SelectedItem is VizCodeFile selectedFile && selectedFile != _activeFile)
        {
            SelectFile(selectedFile);
        }
    }

    private void CloseTab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is VizCodeFile file)
        {
            // Don't allow closing the entry point file
            if (file.IsEntryPoint)
            {
                var entryFileName = _currentProject?.ProjectFile.Language == ProjectLanguage.FSharp
                    ? "StartViz.fs" : "StartViz.cs";
                MessageBox.Show(
                    $"Cannot close the entry point file ({entryFileName}).",
                    "Cannot Close",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            if (file.HasUnsavedChanges)
            {
                var result = MessageBox.Show(
                    $"Save changes to {file.FileName}?",
                    "Unsaved Changes",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Cancel)
                    return;

                if (result == MessageBoxResult.Yes)
                    _currentProject?.SaveFile(file);
            }

            _currentProject?.RemoveFile(file);
            RefreshFileTabs();

            if (_currentProject?.Files.Count > 0)
            {
                SelectFile(_currentProject.Files[0]);
            }
            else
            {
                _activeFile = null;
                CodeEditor.Text = "";
            }
        }
    }

    #endregion

    #region Button Handlers

    private void NewProjectButton_Click(object sender, RoutedEventArgs e)
    {
        if (!PromptSaveChanges())
            return;

        var dialog = new NewProjectDialog();
        dialog.Owner = this;
        if (dialog.ShowDialog() == true)
        {
            try
            {
                // Stop existing watcher
                StopProjectWatcher();

                if (dialog.OpenExistingProject)
                {
                    // Open existing project instead of creating new
                    _currentProject = VizCodeProject.Load(dialog.FullPath);
                    SetStatus($"Opened existing project: {dialog.ProjectName}", false);
                }
                else
                {
                    _currentProject = VizCodeProject.CreateNew(dialog.FullPath, dialog.ProjectName, dialog.SelectedLanguage);
                    SetStatus($"Project created: {dialog.ProjectName}", false);
                }

                LoadProjectTree();
                RefreshFileTabs();

                // Start watching for external changes
                StartProjectWatcher(_currentProject.ProjectDirectory);

                if (_currentProject.EntryPointFile != null)
                {
                    SelectFile(_currentProject.EntryPointFile);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}");
            }
        }
    }

    private void NewFileButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentProject == null)
        {
            SetStatus("No project open", isError: true);
            return;
        }

        // Generate unique name based on project language
        var ext = _currentProject.ProjectFile.Language == ProjectLanguage.FSharp ? ".fs" : ".cs";
        int i = 1;
        string fileName = $"Untitled-1{ext}";
        while (_currentProject.Files.Any(f => f.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase)))
        {
            i++;
            fileName = $"Untitled-{i}{ext}";
        }

        // Create new in-memory file with language-appropriate template
        var projectName = _currentProject.ProjectFile.Name;
        var className = Path.GetFileNameWithoutExtension(fileName);
        var isFSharp = _currentProject.ProjectFile.Language == ProjectLanguage.FSharp;
        var content = isFSharp
            ? FSharpTemplates.GetEmptyModuleTemplate(projectName, className)
            : string.Format(Templates.EmptyModuleTemplate, projectName, className);

        var newFile = new VizCodeFile
        {
            FilePath = string.Empty, // No path yet
            Content = content,
            HasUnsavedChanges = true,
            IsNew = true // Flag as new
        };

        // Hack: We need a temporary 'FilePath' for the tab binding to display name correctly
        // VizCodeFile.FileName is derived from FilePath. 
        // Let's set a fake path for now.
        newFile.FilePath = Path.Combine(_currentProject.ProjectDirectory, fileName); 

        _currentProject.Files.Add(newFile);
        RefreshFileTabs();
        SelectFile(newFile);

        SetStatus($"Created: {fileName}", isError: false);
    }

    private string? PromptForFileName()
    {
        // Using a simple approach with InputBox-style dialog
        var dialog = new Window
        {
            Title = "New File",
            Width = 350,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this,
            ResizeMode = ResizeMode.NoResize,
            Background = (SolidColorBrush)FindResource("SecondaryBackgroundBrush")
        };

        var grid = new Grid { Margin = new Thickness(16) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var label = new TextBlock
        {
            Text = "Enter file name (without extension):",
            Foreground = (SolidColorBrush)FindResource("ForegroundBrush"),
            Margin = new Thickness(0, 0, 0, 8)
        };
        Grid.SetRow(label, 0);

        var textBox = new TextBox
        {
            Text = "Module1",
            Margin = new Thickness(0, 0, 0, 16),
            Padding = new Thickness(8, 4, 8, 4)
        };
        textBox.SelectAll();
        Grid.SetRow(textBox, 1);

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        Grid.SetRow(buttonPanel, 2);

        var okButton = new Button
        {
            Content = "OK",
            Width = 80,
            Margin = new Thickness(0, 0, 8, 0),
            Style = (Style)FindResource("RunButtonStyle"), // Use Accent Color
            Foreground = Brushes.White // Force white text
        };
        okButton.Click += (s, e) => { dialog.DialogResult = true; dialog.Close(); };

        var cancelButton = new Button
        {
            Content = "Cancel",
            Width = 80,
            Style = (Style)FindResource("RibbonButtonStyle"),
            Foreground = (SolidColorBrush)FindResource("ForegroundBrush")
        };
        cancelButton.Click += (s, e) => { dialog.DialogResult = false; dialog.Close(); };

        buttonPanel.Children.Add(okButton);
        buttonPanel.Children.Add(cancelButton);

        grid.Children.Add(label);
        grid.Children.Add(textBox);
        grid.Children.Add(buttonPanel);

        dialog.Content = grid;
        textBox.Focus();

        if (dialog.ShowDialog() == true)
        {
            return textBox.Text;
        }

        return null;
    }

    private void OpenButton_Click(object sender, RoutedEventArgs e)
    {
        if (!PromptSaveChanges())
            return;

        var dialog = new OpenFileDialog
        {
            Filter = "Code2Viz Project (*.vizproj)|*.vizproj",
            DefaultExt = ".vizproj",
            Title = "Open Project"
        };

        if (dialog.ShowDialog() == true)
        {
            LoadProject(dialog.FileName);
        }
    }

    private void LoadSettingsToUI()
    {
        if (_currentProject == null) return;

        var settings = _currentProject.ProjectFile.Settings;
        if (settings == null)
        {
            // Should not happen as it's initialized in VizProjectFile, but just in case
            SettingsStrokeColorBox.Text = "";
            SettingsFillColorBox.Text = "";
            SettingsThicknessBox.Text = "";
            return;
        }

        SettingsStrokeColorBox.Text = settings.DefaultStrokeColor ?? "";
        SettingsFillColorBox.Text = settings.DefaultFillColor ?? "";
        SettingsCanvasColorBox.Text = settings.DefaultCanvasBackgroundColor ?? "";
        SettingsThicknessBox.Text = settings.DefaultStrokeThickness.HasValue 
            ? settings.DefaultStrokeThickness.Value.ToString() 
            : "";
            
        // Apply Canvas Background immediately on load (Fix for Issue 1)
        if (!string.IsNullOrEmpty(settings.DefaultCanvasBackgroundColor))
        {
            try {
                var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(settings.DefaultCanvasBackgroundColor));
                RenderCanvas.CanvasBackground = brush;
            } catch {}
        }

        // ---------------------------------------------------------
        // Load Application Settings
        // ---------------------------------------------------------
        var appSettings = ApplicationSettings.Instance;
        
        // Export Background
        string exportBg = appSettings.DefaultExportBackground ?? "Transparent";
        foreach (ComboBoxItem item in SettingsExportBackgroundCombo.Items)
        {
            var content = item.Content?.ToString();
            if (content == exportBg || 
               (exportBg == "Light" && content != null && content.Contains("Light")))
            {
                SettingsExportBackgroundCombo.SelectedItem = item;
                break;
            }
        }
        if (SettingsExportBackgroundCombo.SelectedItem == null) 
            SettingsExportBackgroundCombo.SelectedIndex = 0;
            
        // Include Grid
        SettingsIncludeGridCheck.IsChecked = appSettings.IncludeGridInExport;

        // Update Button colors
        UpdateColorButton(SettingsStrokeColorBtn, SettingsStrokeColorBox.Text);
        UpdateColorButton(SettingsFillColorBtn, SettingsFillColorBox.Text);
        UpdateColorButton(SettingsCanvasColorBtn, SettingsCanvasColorBox.Text);
    }
    
    private void ColorBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender == SettingsStrokeColorBox) 
            UpdateColorButton(SettingsStrokeColorBtn, SettingsStrokeColorBox.Text);
        else if (sender == SettingsFillColorBox) 
            UpdateColorButton(SettingsFillColorBtn, SettingsFillColorBox.Text);
        else if (sender == SettingsCanvasColorBox) 
            UpdateColorButton(SettingsCanvasColorBtn, SettingsCanvasColorBox.Text);
    }
    
    private void UpdateColorButton(Button btn, string colorText)
    {
        try 
        {
            if (string.IsNullOrWhiteSpace(colorText))
                btn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#444444"));
            else
            {
                 var color = (Color)ColorConverter.ConvertFromString(colorText);
                 btn.Background = new SolidColorBrush(color);
            }
        }
        catch 
        { 
            // Keep previous or set to default on error
            btn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#444444"));
        }
    }

    private void PickColorButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag)
        {
            TextBox targetBox;
            if (tag == "Stroke") targetBox = SettingsStrokeColorBox;
            else if (tag == "Fill") targetBox = SettingsFillColorBox;
            else targetBox = SettingsCanvasColorBox;
            
            var dialog = new ColorPickerDialog(targetBox.Text);
            dialog.Owner = this;
            if (dialog.ShowDialog() == true)
            {
                targetBox.Text = dialog.SelectedColor;
            }
        }
    }

    private void SaveSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        // 1. Save Project Settings
        if (_currentProject != null)
        {
            string? strokeColor = SettingsStrokeColorBox.Text.Trim();
            if (string.IsNullOrEmpty(strokeColor)) strokeColor = null;

            string? fillColor = SettingsFillColorBox.Text.Trim();
            if (string.IsNullOrEmpty(fillColor)) fillColor = null;
            
            string? canvasColor = SettingsCanvasColorBox.Text.Trim();
            if (string.IsNullOrEmpty(canvasColor)) canvasColor = null;

            double? thickness = null;
            if (double.TryParse(SettingsThicknessBox.Text.Trim(), out double t))
            {
                thickness = t;
            }

            var settings = _currentProject.ProjectFile.Settings;
            settings.DefaultStrokeColor = strokeColor;
            settings.DefaultFillColor = fillColor;
            settings.DefaultCanvasBackgroundColor = canvasColor;
            settings.DefaultStrokeThickness = thickness;
            // Remove DefaultExportBackground from Project Settings if desired, but nice to keep as fallback?
            // For now, we strictly use AppSettings for Export as requested.

            _currentProject.ApplySettings();
            
            if (!string.IsNullOrEmpty(canvasColor))
            {
                try {
                    var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(canvasColor));
                    RenderCanvas.CanvasBackground = brush;
                } catch { }
            }

            _currentProject.SaveProjectFile();
        }

        // 2. Save Application Settings
        string exportBg = "Transparent";
        if (SettingsExportBackgroundCombo.SelectedItem is ComboBoxItem item)
        {
            exportBg = item.Content?.ToString() ?? "Transparent";
        }
        
        ApplicationSettings.Instance.DefaultExportBackground = exportBg;
        ApplicationSettings.Instance.IncludeGridInExport = SettingsIncludeGridCheck.IsChecked == true;
        ApplicationSettings.Save();

        SetStatus("Settings saved (Project and Application).", isError: false);
    }

    private void CloseProjectMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (!PromptSaveChanges())
            return;

        var welcome = new WelcomeWindow();
        welcome.Show();
        Close();
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!PromptSaveChanges())
        {
            e.Cancel = true;
            return;
        }

        // Clean up file watcher
        StopProjectWatcher();
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentProject == null)
            return;

        SaveCurrentEditorContent();

        // Check for new files that need a save location
        foreach (var file in _currentProject.Files.Where(f => f.IsNew).ToList())
        {
            SelectFile(file); // Show the file being saved
            var isFSharp = _currentProject.ProjectFile.Language == ProjectLanguage.FSharp;
            var dialog = new SaveFileDialog
            {
                FileName = file.FileName,
                Filter = isFSharp ? "F# Files (*.fs)|*.fs" : "C# Files (*.cs)|*.cs",
                DefaultExt = isFSharp ? ".fs" : ".cs",
                InitialDirectory = _currentProject.ProjectDirectory
            };

            if (dialog.ShowDialog() == true)
            {
                file.FilePath = dialog.FileName;
                file.IsNew = false;
                // Update class name if file name changed? 
                // That's complex refactoring, skipping for now unless needed.
            }
            else
            {
                // User cancelled save for this file. 
                // We should probably stop saving others? Or just skip? 
                // Proceeding to save others.
            }
        }

        // If project is in temp directory, prompt for real location
        if (_currentProject.ProjectDirectory.StartsWith(Path.GetTempPath()))
        {
            if (!SaveProjectToNewLocation())
                return;
        }
        else
        {
            _currentProject.SaveAllFiles();
        }

        RefreshFileTabs();
        LoadProjectTree();
        SetStatus("All files saved", isError: false);
    }

    private async void RunButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentProject == null || _currentProject.Files.Count == 0)
        {
            SetStatus("No files to compile", isError: false);
            return;
        }

        // Save current editor content
        SaveCurrentEditorContent();

        // Verify entry point exists
        if (_currentProject.EntryPointFile == null)
        {
            var expectedFile = _currentProject.ProjectFile.Language == ProjectLanguage.FSharp
                ? "StartViz.fs" : "StartViz.cs";
            SetStatus($"Error: {expectedFile} not found", isError: true);
            return;
        }

        SetStatus("Compiling...", isError: false);
        RunButton.IsEnabled = false;

        try
        {
            _textMarkerService?.Clear();
            var result = await _compiler.CompileAndExecuteAsync(_currentProject);
            
            // Apply project settings (including background)
            _currentProject.ApplySettings();
            if (_currentProject.ProjectFile.Settings.DefaultCanvasBackgroundColor is string bgCode)
            {
                 try { RenderCanvas.CanvasBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(bgCode)); } catch {}
            }

            if (result.Success)
            {
                // Reset animation time
                _animationStopwatch.Restart();

                var shapes = CanvasRenderer.Instance.GetShapes();
                var count = shapes.Count;

                CanvasRenderer.Instance.RenderTo(RenderCanvas);
                SetStatus($"Success: {count} shape{(count != 1 ? "s" : "")} drawn", isError: false);
            }
            else
            {
                // Count errors for status (both C# and F# diagnostics)
                var errorCount = (result.Diagnostics?.Count(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error) ?? 0)
                               + (result.FSharpDiagnostics?.Count(d => d.Severity == "Error") ?? 0);

                if (errorCount > 0)
                {
                    SetStatus($"Compilation failed: {errorCount} error{(errorCount != 1 ? "s" : "")}", isError: true);
                }
                else if (!string.IsNullOrEmpty(result.Error))
                {
                    // Show the error message if no diagnostics but compilation failed
                    SetStatus("Compilation Error", isError: true);
                    // Also write full error to console
                    Console.ConsoleOutput.Instance.WriteLine("Compiler", 0, result.Error);
                }
                else
                {
                    SetStatus("Compilation failed", isError: true);
                }
            }

            // Show diagnostics (errors/warnings) in console and editor
            if (result.Diagnostics != null)
            {
                foreach (var diagnostic in result.Diagnostics)
                {
                    // Only show errors and warnings
                    if (diagnostic.Severity != Microsoft.CodeAnalysis.DiagnosticSeverity.Error &&
                        diagnostic.Severity != Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                        continue;

                    var lineSpan = diagnostic.Location.GetLineSpan();
                    var startLine = lineSpan.StartLinePosition.Line + 1;
                    var startCol = lineSpan.StartLinePosition.Character + 1;

                    // Determine file path - use lineSpan.Path or try to find matching project file
                    var filePath = lineSpan.Path;
                    if (string.IsNullOrEmpty(filePath) && _currentProject != null)
                    {
                        // Try to find the file by matching filename in the error
                        filePath = _activeFile?.FilePath ?? "";
                    }

                    // Add to console as clickable error entry
                    var errorCode = diagnostic.Id;
                    var message = $"{errorCode}: {diagnostic.GetMessage()}";
                    Console.ConsoleOutput.Instance.WriteCompilationError(filePath, startLine, startCol, message);

                    // Also highlight in editor if it matches the active file
                    var activePath = _activeFile?.FilePath;
                    bool isMatch = false;

                    if (activePath != null)
                    {
                        if (string.IsNullOrEmpty(lineSpan.Path))
                        {
                            isMatch = true;
                        }
                        else
                        {
                            if (string.Equals(lineSpan.Path, activePath, StringComparison.OrdinalIgnoreCase))
                                isMatch = true;
                            else if (string.Equals(Path.GetFileName(lineSpan.Path), Path.GetFileName(activePath), StringComparison.OrdinalIgnoreCase))
                                isMatch = true;
                        }
                    }

                    if (isMatch)
                    {
                        var endLine = lineSpan.EndLinePosition.Line + 1;
                        var endCol = lineSpan.EndLinePosition.Character + 1;

                        try
                        {
                            var offset = CodeEditor.Document.GetOffset(new TextLocation(startLine, startCol));
                            var endOffset = CodeEditor.Document.GetOffset(new TextLocation(endLine, endCol));
                            var length = endOffset - offset;

                            if (length > 0)
                            {
                                var color = diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error ? Colors.Red : Colors.Orange;
                                _textMarkerService?.Create(offset, length, diagnostic.GetMessage(), color);
                            }
                        }
                        catch (Exception) { /* Ignore invalid ranges */ }
                    }
                }
            }

            // Show F# diagnostics (errors/warnings) in console and editor
            if (result.FSharpDiagnostics != null)
            {
                foreach (var diagnostic in result.FSharpDiagnostics)
                {
                    if (diagnostic.Severity != "Error" && diagnostic.Severity != "Warning")
                        continue;

                    // Add to console as clickable error entry
                    var message = $"FS{diagnostic.ErrorNumber}: {diagnostic.Message}";
                    Console.ConsoleOutput.Instance.WriteCompilationError(diagnostic.FilePath, diagnostic.StartLine, diagnostic.StartColumn, message);

                    // Also highlight in editor if it matches the active file
                    var activePath = _activeFile?.FilePath;
                    bool isMatch = false;

                    if (activePath != null)
                    {
                        if (string.IsNullOrEmpty(diagnostic.FilePath))
                            isMatch = true;
                        else if (string.Equals(diagnostic.FilePath, activePath, StringComparison.OrdinalIgnoreCase))
                            isMatch = true;
                        else if (string.Equals(Path.GetFileName(diagnostic.FilePath), Path.GetFileName(activePath), StringComparison.OrdinalIgnoreCase))
                            isMatch = true;
                    }

                    if (isMatch)
                    {
                        try
                        {
                            var offset = CodeEditor.Document.GetOffset(new TextLocation(diagnostic.StartLine, diagnostic.StartColumn + 1));
                            var endOffset = CodeEditor.Document.GetOffset(new TextLocation(diagnostic.EndLine, diagnostic.EndColumn + 1));
                            var length = endOffset - offset;

                            if (length > 0)
                            {
                                var color = diagnostic.Severity == "Error" ? Colors.Red : Colors.Orange;
                                _textMarkerService?.Create(offset, length, diagnostic.Message, color);
                            }
                        }
                        catch (Exception) { /* Ignore invalid ranges */ }
                    }
                }
            }

        }
        catch (Exception ex)
        {
            SetStatus($"Error: {ex.Message}", isError: true);
        }
        finally
        {
            // Flush any pending console output
            Console.ConsoleOutput.Instance.Flush();
            RunButton.IsEnabled = true;
        }
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        CanvasRenderer.Instance.Clear();
        RenderCanvas.ClearShapes();
        SetStatus("Canvas cleared", isError: false);
    }

    private void FormatButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var caretOffset = CodeEditor.CaretOffset;
            CodeEditor.Text = CodeFormatter.Format(CodeEditor.Text);

            if (caretOffset <= CodeEditor.Text.Length)
                CodeEditor.CaretOffset = caretOffset;

            SetStatus("Code formatted", isError: false);
        }
        catch (Exception ex)
        {
            SetStatus($"Format error: {ex.Message}", isError: true);
        }
    }

    private void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        var optionsDialog = new ExportOptionsWindow();
        optionsDialog.Owner = this;
        
        // Set default from Application Settings
        if (ApplicationSettings.Instance.DefaultExportBackground != null)
        {
            optionsDialog.SetDefault(ApplicationSettings.Instance.DefaultExportBackground);
        }
        
        optionsDialog.SetGridDefault(ApplicationSettings.Instance.IncludeGridInExport);
        
        if (optionsDialog.ShowDialog() != true) return;

        var dialog = new SaveFileDialog
        {
            Filter = "PNG Image (*.png)|*.png",
            DefaultExt = ".png",
            FileName = "canvas_export"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                ExportCanvasToPng(dialog.FileName, optionsDialog.SelectedBackground, optionsDialog.IncludeGrid);
                SetStatus($"Exported: {Path.GetFileName(dialog.FileName)}", isError: false);
            }
            catch (Exception ex)
            {
                SetStatus($"Export error: {ex.Message}", isError: true);
            }
        }
    }

    private void ExportCanvasToPng(string filePath, Brush? overrideBackground = null, bool includeGrid = true)
    {
        // Save current state
        bool wasGridShown = RenderCanvas.ShowGrid;
        var originalBackground = RenderCanvas.CanvasBackground;

        try
        {
            // Apply export settings
            RenderCanvas.ShowGrid = includeGrid;

            // Set the export background (null means use current canvas background)
            if (overrideBackground != null)
            {
                RenderCanvas.CanvasBackground = overrideBackground;
            }

            // Allow visual to update
            RenderCanvas.UpdateLayout();

            var width = (int)RenderCanvas.ActualWidth;
            var height = (int)RenderCanvas.ActualHeight;

            if (width <= 0 || height <= 0)
                throw new InvalidOperationException($"Invalid Canvas Dimensions: {width}x{height}");

            var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(RenderCanvas);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rtb));

            using var fs = new FileStream(filePath, FileMode.Create);
            encoder.Save(fs);
        }
        finally
        {
            // Restore original state
            RenderCanvas.CanvasBackground = originalBackground;
            RenderCanvas.ShowGrid = wasGridShown;
            RenderCanvas.UpdateLayout();
        }
    }

    private void ExportGifButton_Click(object sender, RoutedEventArgs e)
    {
        var timeline = CanvasRenderer.Instance.ActiveTimeline;
        if (timeline == null)
        {
            MessageBox.Show(
                "No active animation timeline found.\n\nPlease run code that creates and plays a Timeline before exporting a GIF.",
                "No Animation",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var optionsDialog = new GifExportOptionsWindow();
        optionsDialog.Owner = this;
        optionsDialog.SetDuration(timeline.Duration);

        if (optionsDialog.ShowDialog() != true) return;

        var dialog = new SaveFileDialog
        {
            Filter = "GIF Animation (*.gif)|*.gif",
            DefaultExt = ".gif",
            FileName = "animation_export"
        };

        if (dialog.ShowDialog() == true)
        {
            // Show progress dialog
            var progressDialog = new ProgressDialog("Exporting GIF animation...");
            progressDialog.Owner = this;
            progressDialog.Show();

            // Set hourglass cursor on main window too
            var originalCursor = Cursor;
            Cursor = System.Windows.Input.Cursors.Wait;

            try
            {
                ExportCanvasToGif(dialog.FileName, timeline, optionsDialog.Duration, optionsDialog.Fps,
                    optionsDialog.SelectedBackground, optionsDialog.IncludeGrid, progressDialog);
                SetStatus($"Exported: {Path.GetFileName(dialog.FileName)}", isError: false);
            }
            catch (Exception ex)
            {
                SetStatus($"Export error: {ex.Message}", isError: true);
            }
            finally
            {
                progressDialog.Close();
                Cursor = originalCursor;
            }
        }
    }

    private void ExportCanvasToGif(string filePath, Timeline timeline, double duration, int fps,
        Brush? overrideBackground, bool includeGrid, ProgressDialog? progressDialog = null)
    {
        // Save current state
        bool wasGridShown = RenderCanvas.ShowGrid;
        var originalBackground = RenderCanvas.CanvasBackground;
        bool wasPlaying = timeline.IsPlaying;

        try
        {
            // Apply export settings
            RenderCanvas.ShowGrid = includeGrid;
            if (overrideBackground != null)
            {
                RenderCanvas.CanvasBackground = overrideBackground;
            }

            var width = (int)RenderCanvas.ActualWidth;
            var height = (int)RenderCanvas.ActualHeight;

            if (width <= 0 || height <= 0)
                throw new InvalidOperationException($"Invalid Canvas Dimensions: {width}x{height}");

            int totalFrames = (int)(duration * fps);
            int frameDelayMs = 1000 / fps;
            double timeStep = duration / totalFrames;

            using var fs = new FileStream(filePath, FileMode.Create);
            using var encoder = new GifEncoder(fs, width, height, frameDelayMs, repeat: true);

            for (int i = 0; i < totalFrames; i++)
            {
                // Update progress dialog
                progressDialog?.SetProgress(i + 1, totalFrames);

                // Update timeline to this frame's time
                double time = i * timeStep;
                timeline.Update(time);

                // Force canvas to redraw with updated animation state
                RenderCanvas.Refresh();

                // Force the dispatcher to process rendering and UI updates
                Dispatcher.Invoke(() => { }, DispatcherPriority.Render);

                // Capture frame
                var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
                rtb.Render(RenderCanvas);

                encoder.AddFrame(rtb);
            }
        }
        finally
        {
            // Restore original state
            RenderCanvas.CanvasBackground = originalBackground;
            RenderCanvas.ShowGrid = wasGridShown;

            // Restore timeline to end if it was playing
            if (wasPlaying)
            {
                timeline.Update(timeline.Duration);
            }

            RenderCanvas.Refresh();
        }
    }

    private void GridMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (RenderCanvas != null)
        {
            RenderCanvas.ShowGrid = GridMenuItem.IsChecked;
        }
    }

    private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (PromptSaveChanges())
        {
            Application.Current.Shutdown();
        }
    }

    private void DuplicateLineMenuItem_Click(object sender, RoutedEventArgs e)
    {
        DuplicateLine();
    }

    private void DeleteLineMenuItem_Click(object sender, RoutedEventArgs e)
    {
        DeleteLine();
    }

    private void MoveLineUpMenuItem_Click(object sender, RoutedEventArgs e)
    {
        MoveLineUp();
    }

    private void MoveLineDownMenuItem_Click(object sender, RoutedEventArgs e)
    {
        MoveLineDown();
    }

    private void ToggleCommentMenuItem_Click(object sender, RoutedEventArgs e)
    {
        ToggleComment();
    }

    private void HelpMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var helpWindow = new HelpWindow();
        helpWindow.Show();
    }

    private void AboutMenuItem_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            "Code2Viz - 2D Geometry Visualizer\n\n" +
            "A tool for visualizing 2D geometry using C# code.\n\n" +
            "Create points, lines, circles, rectangles, and more!\n\n" +
            "Version 1.0",
            "About Code2Viz",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    #endregion

    private void EditMenu_SubmenuOpened(object sender, RoutedEventArgs e)
    {
        // "Active" defined as having an open file.
        InsertColorMenuItem.IsEnabled = _activeFile != null;
    }

    private void InsertColorMenuItem_Click(object sender, RoutedEventArgs e)
    {
        PerformInsertColor();
    }

    private void PerformInsertColor()
    {
        if (_activeFile == null) return;
        
        // Ensure editor accepts input
        if (!CodeEditor.IsKeyboardFocusWithin)
        {
             // If called from menu, we might need to focus. 
             // But if called from shortcut, we want to ensure we don't insert when focus is in e.g. Console.
             // If called from Menu, CodeEditor usually isn't focused momentarilly?
             // Let's try to focus it.
             CodeEditor.Focus();
        }

        var dialog = new ColorPickerDialog();
        dialog.Owner = this;
        if (dialog.ShowDialog() == true)
        {
            CodeEditor.Document.Insert(CodeEditor.CaretOffset, dialog.SelectedColor);
        }
    }

    #region Keyboard Shortcuts

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            switch (e.Key)
            {
                case Key.N:
                    NewFileButton_Click(sender, e);
                    e.Handled = true;
                    break;
                case Key.O:
                    OpenButton_Click(sender, e);
                    e.Handled = true;
                    break;
                case Key.S:
                    SaveButton_Click(sender, e);
                    e.Handled = true;
                    break;
                case Key.OemQuestion:
                    ToggleComment();
                    e.Handled = true;
                    break;
                case Key.Space:
                    TriggerManualCompletion();
                    e.Handled = true;
                    break;
                case Key.Enter:
                    RunButton_Click(sender, e);
                    e.Handled = true;
                    break;
                case Key.R:
                    ResetCanvasConsoleLayout();
                    e.Handled = true;
                    break;
            }
        }
        else if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            switch (e.Key)
            {
                case Key.F:
                    FormatButton_Click(sender, e);
                    e.Handled = true;
                    break;
                case Key.D:
                    DeleteLine();
                    e.Handled = true;
                    break;
                case Key.N:
                    NewProjectButton_Click(sender, e);
                    e.Handled = true;
                    break;
                case Key.K:
                    PerformInsertColor();
                    e.Handled = true;
                    break;
            }
        }
        else if (Keyboard.Modifiers == ModifierKeys.Alt)
        {
            // When Alt is pressed, actual key is in e.SystemKey, not e.Key
            switch (e.SystemKey)
            {
                case Key.Up:
                    MoveLineUp();
                    e.Handled = true;
                    break;
                case Key.Down:
                    MoveLineDown();
                    e.Handled = true;
                    break;
            }
        }
        else if (Keyboard.Modifiers == (ModifierKeys.Shift | ModifierKeys.Alt))
        {
            // When Alt is pressed, actual key is in e.SystemKey, not e.Key
            switch (e.SystemKey)
            {
                case Key.Down:
                    DuplicateLine();
                    e.Handled = true;
                    break;
            }
        }
        else if (e.Key == Key.F5)
        {
            RunButton_Click(sender, e);
            e.Handled = true;
        }
        else if (e.Key == Key.F1)
        {
            HelpMenuItem_Click(sender, e);
            e.Handled = true;
        }
    }

    #endregion

    #region Editor Line Operations

    private void DuplicateLine()
    {
        if (!CodeEditor.IsKeyboardFocusWithin) return;

        var document = CodeEditor.Document;
        var caret = CodeEditor.TextArea.Caret;
        var line = document.GetLineByNumber(caret.Line);
        var lineText = document.GetText(line.Offset, line.Length);

        var insertOffset = line.EndOffset;
        document.Insert(insertOffset, Environment.NewLine + lineText);

        caret.Line = caret.Line + 1;
    }

    private void DeleteLine()
    {
        if (!CodeEditor.IsKeyboardFocusWithin) return;

        var document = CodeEditor.Document;
        var caret = CodeEditor.TextArea.Caret;
        var line = document.GetLineByNumber(caret.Line);

        var deleteLength = line.TotalLength;
        if (line.LineNumber == document.LineCount && line.LineNumber > 1)
        {
            var prevLine = document.GetLineByNumber(line.LineNumber - 1);
            document.Remove(prevLine.EndOffset, line.EndOffset - prevLine.EndOffset);
        }
        else
        {
            document.Remove(line.Offset, deleteLength);
        }
    }

    private void MoveLineUp()
    {
        if (!CodeEditor.IsKeyboardFocusWithin) return;

        var document = CodeEditor.Document;
        var caret = CodeEditor.TextArea.Caret;
        var currentLineNumber = caret.Line;

        if (currentLineNumber <= 1) return;

        var currentLine = document.GetLineByNumber(currentLineNumber);
        var previousLine = document.GetLineByNumber(currentLineNumber - 1);

        var currentText = document.GetText(currentLine.Offset, currentLine.Length);
        var previousText = document.GetText(previousLine.Offset, previousLine.Length);

        document.BeginUpdate();
        try
        {
            document.Replace(previousLine.Offset, previousLine.Length, currentText);
            var newCurrentLine = document.GetLineByNumber(currentLineNumber);
            document.Replace(newCurrentLine.Offset, newCurrentLine.Length, previousText);
        }
        finally
        {
            document.EndUpdate();
        }

        caret.Line = currentLineNumber - 1;
    }

    private void MoveLineDown()
    {
        if (!CodeEditor.IsKeyboardFocusWithin) return;

        var document = CodeEditor.Document;
        var caret = CodeEditor.TextArea.Caret;
        var currentLineNumber = caret.Line;

        if (currentLineNumber >= document.LineCount) return;

        var currentLine = document.GetLineByNumber(currentLineNumber);
        var nextLine = document.GetLineByNumber(currentLineNumber + 1);

        var currentText = document.GetText(currentLine.Offset, currentLine.Length);
        var nextText = document.GetText(nextLine.Offset, nextLine.Length);

        document.BeginUpdate();
        try
        {
            document.Replace(nextLine.Offset, nextLine.Length, currentText);
            var newCurrentLine = document.GetLineByNumber(currentLineNumber);
            document.Replace(newCurrentLine.Offset, newCurrentLine.Length, nextText);
        }
        finally
        {
            document.EndUpdate();
        }

        caret.Line = currentLineNumber + 1;
    }

    private void ToggleComment()
    {
        if (!CodeEditor.IsKeyboardFocusWithin) return;

        var document = CodeEditor.Document;
        var selection = CodeEditor.TextArea.Selection;

        if (selection.IsEmpty)
        {
            var caret = CodeEditor.TextArea.Caret;
            var line = document.GetLineByNumber(caret.Line);
            var lineText = document.GetText(line.Offset, line.Length);
            var trimmedText = lineText.TrimStart();

            if (trimmedText.StartsWith("//"))
            {
                var commentIndex = lineText.IndexOf("//", StringComparison.Ordinal);
                var removeLength = lineText.Length > commentIndex + 2 && lineText[commentIndex + 2] == ' ' ? 3 : 2;
                document.Remove(line.Offset + commentIndex, removeLength);
            }
            else
            {
                var insertIndex = lineText.Length - trimmedText.Length;
                document.Insert(line.Offset + insertIndex, "// ");
            }
        }
        else
        {
            var startLine = selection.StartPosition.Line;
            var endLine = selection.EndPosition.Line;

            var allCommented = true;
            for (var i = startLine; i <= endLine; i++)
            {
                var line = document.GetLineByNumber(i);
                var lineText = document.GetText(line.Offset, line.Length).TrimStart();
                if (!string.IsNullOrEmpty(lineText) && !lineText.StartsWith("//"))
                {
                    allCommented = false;
                    break;
                }
            }

            document.BeginUpdate();
            try
            {
                for (var i = endLine; i >= startLine; i--)
                {
                    var line = document.GetLineByNumber(i);
                    var lineText = document.GetText(line.Offset, line.Length);
                    var trimmedText = lineText.TrimStart();

                    if (allCommented)
                    {
                        if (trimmedText.StartsWith("//"))
                        {
                            var commentIndex = lineText.IndexOf("//", StringComparison.Ordinal);
                            var removeLength = lineText.Length > commentIndex + 2 && lineText[commentIndex + 2] == ' ' ? 3 : 2;
                            document.Remove(line.Offset + commentIndex, removeLength);
                        }
                    }
                    else
                    {
                        var insertIndex = lineText.Length - trimmedText.Length;
                        document.Insert(line.Offset + insertIndex, "// ");
                    }
                }
            }
            finally
            {
                document.EndUpdate();
            }
        }
    }

    #endregion

    private void SetStatus(string message, bool isError)
    {
        StatusText.Text = message;
        if (isError)
        {
            StatusText.Foreground = new SolidColorBrush(Colors.OrangeRed);
        }
        else
        {
            StatusText.Foreground = (SolidColorBrush)FindResource("ForegroundBrush");
        }
    }

    #region Folding

    private void UpdateFoldings()
    {
        if (_foldingStrategy != null && _foldingManager != null)
        {
            _foldingStrategy.UpdateFoldings(_foldingManager, CodeEditor.Document);
        }
    }

    #endregion

    #region Project Browser

    private void LoadProjectTree()
    {
        if (_currentProject == null) return;

        var root = new ProjectTreeItem
        {
            Name = Path.GetFileName(_currentProject.ProjectDirectory) ?? "Project",
            FullPath = _currentProject.ProjectDirectory,
            IsDirectory = true
        };

        // Add References virtual node
        var referencesNode = new ProjectTreeItem
        {
            Name = "References",
            FullPath = string.Empty,
            IsDirectory = false,
            IsReferencesNode = true
        };

        // Populate references from project file
        if (_currentProject.ProjectFile?.References != null)
        {
            foreach (var asmRef in _currentProject.ProjectFile.References)
            {
                referencesNode.Children.Add(new ProjectTreeItem
                {
                    Name = asmRef.ToString(),
                    FullPath = asmRef.Path,
                    IsDirectory = false,
                    IsReferenceItem = true
                });
            }
        }

        root.Children.Add(referencesNode);

        BuildProjectTree(root);
        
        ProjectTreeView.ItemsSource = new ObservableCollection<ProjectTreeItem> { root };
    }

    private void BuildProjectTree(ProjectTreeItem item)
    {
        if (!item.IsDirectory) return;

        try
        {
            // Directories
            foreach (var dir in Directory.GetDirectories(item.FullPath))
            {
                var dirItem = new ProjectTreeItem
                {
                    Name = Path.GetFileName(dir),
                    FullPath = dir,
                    IsDirectory = true
                };
                BuildProjectTree(dirItem);
                item.Children.Add(dirItem);
            }

            // Files
            foreach (var file in Directory.GetFiles(item.FullPath))
            {
                if (file.EndsWith(".vizproj", StringComparison.OrdinalIgnoreCase))
                    continue;

                var fileItem = new ProjectTreeItem
                {
                    Name = Path.GetFileName(file),
                    FullPath = file,
                    IsDirectory = false
                };
                item.Children.Add(fileItem);
            }
        }
        catch (Exception ex)
        {
            SetStatus($"Error building tree: {ex.Message}", true);
        }
    }

    private void ProjectTreeView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ProjectTreeView.SelectedItem is ProjectTreeItem item)
        {
            // Handle References node - open Add Reference dialog
            if (item.IsReferencesNode)
            {
                if (_currentProject != null)
                {
                    var dialog = new AddReferenceWindow(_currentProject);
                    dialog.Owner = this;
                    if (dialog.ShowDialog() == true)
                    {
                        LoadProjectTree(); // Refresh to show updated references
                        SetStatus("References updated", isError: false);
                    }
                }
                return;
            }

            // Ignore reference items (no action on double-click)
            if (item.IsReferenceItem)
            {
                return;
            }

            // Handle regular files
            if (!item.IsDirectory)
            {
                // Check if file is already loaded
                var existingFile = _currentProject?.Files.FirstOrDefault(f => f.FilePath.Equals(item.FullPath, StringComparison.OrdinalIgnoreCase));
                
                if (existingFile != null)
                {
                    SelectFile(existingFile);
                }
                else if (File.Exists(item.FullPath) && _currentProject != null)
                {
                    try
                    {
                        // Open generic file
                        var newFile = new VizCodeFile
                        {
                            FilePath = item.FullPath,
                            Content = File.ReadAllText(item.FullPath),
                            HasUnsavedChanges = false
                        };
                        
                        _currentProject.Files.Add(newFile);
                        RefreshFileTabs();
                        SelectFile(newFile);
                    }
                    catch (Exception ex)
                    {
                         SetStatus($"Error opening file: {ex.Message}", true);
                    }
                }
            }
        }
    }

    #region Project Tree Context Menu

    private void ContextMenu_NewFile_Click(object sender, RoutedEventArgs e)
    {
        if (_currentProject == null) return;

        var item = GetContextMenuTargetItem(sender);
        if (item == null) return;

        // Determine target directory
        var targetDir = item.IsDirectory ? item.FullPath : Path.GetDirectoryName(item.FullPath);
        if (string.IsNullOrEmpty(targetDir)) return;

        // Prompt for file name
        var fileName = PromptForInput("New File", "Enter file name:", GetDefaultNewFileName());
        if (string.IsNullOrEmpty(fileName)) return;

        // Ensure correct extension
        var ext = Path.GetExtension(fileName);
        if (string.IsNullOrEmpty(ext))
        {
            ext = _currentProject.ProjectFile.Language == ProjectLanguage.FSharp ? ".fs" : ".cs";
            fileName += ext;
        }

        var fullPath = Path.Combine(targetDir, fileName);

        if (File.Exists(fullPath))
        {
            MessageBox.Show($"File '{fileName}' already exists.", "File Exists", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            // Create file with template
            var projectName = _currentProject.ProjectFile.Name;
            var className = Path.GetFileNameWithoutExtension(fileName);
            var isFSharp = _currentProject.ProjectFile.Language == ProjectLanguage.FSharp;
            var content = isFSharp
                ? FSharpTemplates.GetEmptyModuleTemplate(projectName, className)
                : string.Format(Templates.EmptyModuleTemplate, projectName, className);

            File.WriteAllText(fullPath, content);

            // Add to project and open
            var newFile = new VizCodeFile
            {
                FilePath = fullPath,
                Content = content,
                HasUnsavedChanges = false
            };
            _currentProject.Files.Add(newFile);

            LoadProjectTree();
            RefreshFileTabs();
            SelectFile(newFile);
            SetStatus($"Created: {fileName}", isError: false);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error creating file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ContextMenu_NewFolder_Click(object sender, RoutedEventArgs e)
    {
        if (_currentProject == null) return;

        var item = GetContextMenuTargetItem(sender);
        if (item == null) return;

        // Determine target directory
        var targetDir = item.IsDirectory ? item.FullPath : Path.GetDirectoryName(item.FullPath);
        if (string.IsNullOrEmpty(targetDir)) return;

        // Prompt for folder name
        var folderName = PromptForInput("New Folder", "Enter folder name:", "NewFolder");
        if (string.IsNullOrEmpty(folderName)) return;

        var fullPath = Path.Combine(targetDir, folderName);

        if (Directory.Exists(fullPath))
        {
            MessageBox.Show($"Folder '{folderName}' already exists.", "Folder Exists", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            Directory.CreateDirectory(fullPath);
            LoadProjectTree();
            SetStatus($"Created folder: {folderName}", isError: false);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error creating folder: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ContextMenu_Rename_Click(object sender, RoutedEventArgs e)
    {
        if (_currentProject == null) return;

        var item = GetContextMenuTargetItem(sender);
        if (item == null || item.IsReferencesNode || item.IsReferenceItem) return;

        // Don't allow renaming entry point
        if (!item.IsDirectory && IsEntryPointFile(item.FullPath))
        {
            MessageBox.Show("Cannot rename the entry point file.", "Cannot Rename", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var currentName = item.Name;
        var newName = PromptForInput("Rename", $"Enter new name for '{currentName}':", currentName);
        if (string.IsNullOrEmpty(newName) || newName == currentName) return;

        var parentDir = Path.GetDirectoryName(item.FullPath);
        if (string.IsNullOrEmpty(parentDir)) return;

        var newPath = Path.Combine(parentDir, newName);

        try
        {
            if (item.IsDirectory)
            {
                if (Directory.Exists(newPath))
                {
                    MessageBox.Show($"Folder '{newName}' already exists.", "Folder Exists", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                Directory.Move(item.FullPath, newPath);

                // Update any open files that were in this directory
                foreach (var file in _currentProject.Files)
                {
                    if (file.FilePath.StartsWith(item.FullPath, StringComparison.OrdinalIgnoreCase))
                    {
                        file.FilePath = file.FilePath.Replace(item.FullPath, newPath);
                    }
                }
            }
            else
            {
                if (File.Exists(newPath))
                {
                    MessageBox.Show($"File '{newName}' already exists.", "File Exists", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                File.Move(item.FullPath, newPath);

                // Update open file reference
                var openFile = _currentProject.Files.FirstOrDefault(f => f.FilePath.Equals(item.FullPath, StringComparison.OrdinalIgnoreCase));
                if (openFile != null)
                {
                    openFile.FilePath = newPath;
                }
            }

            LoadProjectTree();
            RefreshFileTabs();
            SetStatus($"Renamed to: {newName}", isError: false);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error renaming: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ContextMenu_Delete_Click(object sender, RoutedEventArgs e)
    {
        if (_currentProject == null) return;

        var item = GetContextMenuTargetItem(sender);
        if (item == null || item.IsReferencesNode || item.IsReferenceItem) return;

        // Don't allow deleting entry point
        if (!item.IsDirectory && IsEntryPointFile(item.FullPath))
        {
            MessageBox.Show("Cannot delete the entry point file.", "Cannot Delete", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var itemType = item.IsDirectory ? "folder" : "file";
        var result = MessageBox.Show(
            $"Are you sure you want to delete the {itemType} '{item.Name}'?\n\nThis action cannot be undone.",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            if (item.IsDirectory)
            {
                // Close any open files from this directory
                var filesToClose = _currentProject.Files
                    .Where(f => f.FilePath.StartsWith(item.FullPath, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                foreach (var file in filesToClose)
                {
                    _currentProject.Files.Remove(file);
                }

                Directory.Delete(item.FullPath, true);
            }
            else
            {
                // Close file if open
                var openFile = _currentProject.Files.FirstOrDefault(f => f.FilePath.Equals(item.FullPath, StringComparison.OrdinalIgnoreCase));
                if (openFile != null)
                {
                    _currentProject.Files.Remove(openFile);
                }

                File.Delete(item.FullPath);
            }

            LoadProjectTree();
            RefreshFileTabs();

            // Select first available file if current was deleted
            if (_activeFile == null || !_currentProject.Files.Contains(_activeFile))
            {
                var firstFile = _currentProject.Files.FirstOrDefault();
                if (firstFile != null) SelectFile(firstFile);
            }

            SetStatus($"Deleted: {item.Name}", isError: false);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error deleting: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ProjectTreeView_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Select the TreeViewItem under the mouse
        var treeViewItem = FindAncestor<TreeViewItem>((DependencyObject)e.OriginalSource);
        if (treeViewItem != null)
        {
            treeViewItem.IsSelected = true;
            treeViewItem.Focus();

            // Show context menu
            var contextMenu = CreateProjectTreeContextMenu();
            contextMenu.PlacementTarget = treeViewItem;
            contextMenu.IsOpen = true;
            e.Handled = true;
        }
    }

    private ContextMenu CreateProjectTreeContextMenu()
    {
        var menu = new ContextMenu
        {
            Background = (SolidColorBrush)FindResource("SecondaryBackgroundBrush"),
            BorderBrush = (SolidColorBrush)FindResource("BorderBrush"),
            Foreground = (SolidColorBrush)FindResource("ForegroundBrush")
        };

        var newFileItem = new MenuItem { Header = "New File" };
        newFileItem.Icon = new Image { Source = new BitmapImage(new Uri("/img/file.png", UriKind.Relative)), Width = 16, Height = 16 };
        newFileItem.Click += ContextMenu_NewFile_Click;
        menu.Items.Add(newFileItem);

        var newFolderItem = new MenuItem { Header = "New Folder" };
        newFolderItem.Icon = new Image { Source = new BitmapImage(new Uri("/img/folder.png", UriKind.Relative)), Width = 16, Height = 16 };
        newFolderItem.Click += ContextMenu_NewFolder_Click;
        menu.Items.Add(newFolderItem);

        menu.Items.Add(new Separator { Background = (SolidColorBrush)FindResource("BorderBrush") });

        var renameItem = new MenuItem { Header = "Rename" };
        renameItem.Click += ContextMenu_Rename_Click;
        menu.Items.Add(renameItem);

        var deleteItem = new MenuItem { Header = "Delete", Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E74C3C")) };
        deleteItem.Click += ContextMenu_Delete_Click;
        menu.Items.Add(deleteItem);

        return menu;
    }

    private static T? FindAncestor<T>(DependencyObject current) where T : DependencyObject
    {
        while (current != null)
        {
            if (current is T found)
                return found;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }

    private ProjectTreeItem? GetContextMenuTargetItem(object sender)
    {
        // Use selected item (set by PreviewMouseRightButtonDown)
        if (ProjectTreeView.SelectedItem is ProjectTreeItem selectedItem)
            return selectedItem;
        return null;
    }

    private bool IsEntryPointFile(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        return fileName.Equals("StartViz.cs", StringComparison.OrdinalIgnoreCase)
            || fileName.Equals("StartViz.fs", StringComparison.OrdinalIgnoreCase);
    }

    private string GetDefaultNewFileName()
    {
        if (_currentProject == null) return "NewFile.cs";
        var ext = _currentProject.ProjectFile.Language == ProjectLanguage.FSharp ? ".fs" : ".cs";
        return $"NewFile{ext}";
    }

    private string? PromptForInput(string title, string prompt, string defaultValue)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 350,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this,
            ResizeMode = ResizeMode.NoResize,
            Background = (SolidColorBrush)FindResource("SecondaryBackgroundBrush")
        };

        var grid = new Grid { Margin = new Thickness(16) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var label = new TextBlock
        {
            Text = prompt,
            Foreground = (SolidColorBrush)FindResource("ForegroundBrush"),
            Margin = new Thickness(0, 0, 0, 8)
        };
        Grid.SetRow(label, 0);
        grid.Children.Add(label);

        var textBox = new TextBox
        {
            Text = defaultValue,
            Background = (SolidColorBrush)FindResource("BackgroundBrush"),
            Foreground = (SolidColorBrush)FindResource("ForegroundBrush"),
            BorderBrush = (SolidColorBrush)FindResource("BorderBrush"),
            Padding = new Thickness(8, 6, 8, 6),
            Margin = new Thickness(0, 0, 0, 16)
        };
        textBox.SelectAll();
        Grid.SetRow(textBox, 1);
        grid.Children.Add(textBox);

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        Grid.SetRow(buttonPanel, 2);

        string? result = null;

        var okButton = new Button
        {
            Content = "OK",
            Width = 80,
            Padding = new Thickness(0, 6, 0, 6),
            Margin = new Thickness(0, 0, 8, 0),
            Background = (SolidColorBrush)FindResource("AccentBrush"),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0)
        };
        okButton.Click += (s, e) =>
        {
            result = textBox.Text.Trim();
            dialog.DialogResult = true;
        };

        var cancelButton = new Button
        {
            Content = "Cancel",
            Width = 80,
            Padding = new Thickness(0, 6, 0, 6),
            Background = (SolidColorBrush)FindResource("SecondaryBackgroundBrush"),
            Foreground = (SolidColorBrush)FindResource("ForegroundBrush"),
            BorderBrush = (SolidColorBrush)FindResource("BorderBrush")
        };
        cancelButton.Click += (s, e) => dialog.DialogResult = false;

        buttonPanel.Children.Add(okButton);
        buttonPanel.Children.Add(cancelButton);
        grid.Children.Add(buttonPanel);

        dialog.Content = grid;
        dialog.Loaded += (s, e) => textBox.Focus();

        return dialog.ShowDialog() == true ? result : null;
    }

    #endregion

    #endregion

    private ToolTip? _currentToolTip;

    private void TextEditor_MouseHover(object sender, MouseEventArgs e)
    {
        try
        {
            var pos = CodeEditor.GetPositionFromPoint(e.GetPosition(CodeEditor));
            if (pos == null || CodeEditor.Document == null) return;

            var offset = CodeEditor.Document.GetOffset(pos.Value.Line, pos.Value.Column);

            // First check for error markers
            if (_textMarkerService != null)
            {
                var marker = _textMarkerService.GetMarkerAtOffset(offset);
                if (marker != null && marker.Message != null)
                {
                    ShowTooltip(marker.Message, isError: true);
                    e.Handled = true;
                    return;
                }
            }

            // No error marker - try to show type information for identifier under cursor
            var typeInfo = GetTypeInfoAtOffset(offset);
            if (typeInfo != null)
            {
                ShowStyledTypeTooltip(typeInfo.Value.category, typeInfo.Value.typeName, typeInfo.Value.identifier);
                e.Handled = true;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Hover error: {ex.Message}");
        }
    }

    private (string category, string typeName, string identifier)? GetTypeInfoAtOffset(int offset)
    {
        var document = CodeEditor.Document;
        if (document == null) return null;

        // Find the identifier at this position
        var wordStart = offset;
        var wordEnd = offset;

        // Expand backwards to find word start
        while (wordStart > 0)
        {
            var c = document.GetCharAt(wordStart - 1);
            if (!char.IsLetterOrDigit(c) && c != '_')
                break;
            wordStart--;
        }

        // Expand forwards to find word end
        while (wordEnd < document.TextLength)
        {
            var c = document.GetCharAt(wordEnd);
            if (!char.IsLetterOrDigit(c) && c != '_')
                break;
            wordEnd++;
        }

        if (wordStart >= wordEnd)
            return null;

        var identifier = document.GetText(wordStart, wordEnd - wordStart);
        if (string.IsNullOrEmpty(identifier))
            return null;

        var textBeforeCursor = document.GetText(0, wordStart);
        var allCode = GetAllProjectCode();

        // Check if it's a type name
        var resolvedType = Editor.TypeInspector.ResolveType(identifier);
        if (resolvedType != null)
        {
            var typeDesc = resolvedType.IsClass ? "class" : (resolvedType.IsValueType ? "struct" : "type");
            return (typeDesc, resolvedType.FullName ?? identifier, identifier);
        }

        // Check common types
        var commonType = Editor.TypeInspector.GetCommonTypes().FirstOrDefault(t => t.Name == identifier);
        if (commonType.Name != null)
        {
            return ("type", commonType.Description, identifier);
        }

        // Check if it's a method parameter
        var parameters = Editor.CompletionProvider.FindCurrentMethodParametersPublic(textBeforeCursor);
        var param = parameters.FirstOrDefault(p => p.Name == identifier);
        if (param.Name != null)
        {
            return ("parameter", param.Type, identifier);
        }

        // Check if it's a local variable
        var locals = Editor.CompletionProvider.FindLocalVariablesPublic(textBeforeCursor);
        var local = locals.FirstOrDefault(v => v.Name == identifier);
        if (local.Name != null)
        {
            return ("local", local.Type, identifier);
        }

        // Try to find variable type using existing infrastructure
        var varType = Editor.CompletionProvider.FindVariableType(textBeforeCursor, identifier, allCode);
        if (varType != null)
        {
            return ("variable", varType, identifier);
        }

        return null;
    }

    private void ShowTooltip(string message, bool isError = false)
    {
        if (_currentToolTip != null)
        {
            _currentToolTip.IsOpen = false;
        }

        _currentToolTip = new ToolTip();
        _currentToolTip.PlacementTarget = CodeEditor;
        _currentToolTip.Background = new SolidColorBrush(Color.FromRgb(30, 30, 30));
        _currentToolTip.BorderBrush = new SolidColorBrush(isError ? Color.FromRgb(200, 80, 80) : Color.FromRgb(60, 60, 60));
        _currentToolTip.Foreground = Brushes.White;

        var textBlock = new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 400
        };
        _currentToolTip.Content = textBlock;
        _currentToolTip.IsOpen = true;
    }

    private void ShowStyledTypeTooltip(string category, string typeName, string identifier)
    {
        if (_currentToolTip != null)
        {
            _currentToolTip.IsOpen = false;
        }

        _currentToolTip = new ToolTip();
        _currentToolTip.PlacementTarget = CodeEditor;
        _currentToolTip.Background = new SolidColorBrush(Color.FromRgb(30, 30, 30));
        _currentToolTip.BorderBrush = new SolidColorBrush(Color.FromRgb(60, 60, 60));

        var panel = new StackPanel { Orientation = Orientation.Horizontal };

        // Category in gray: (local), (parameter), (type), etc.
        panel.Children.Add(new TextBlock
        {
            Text = $"({category}) ",
            Foreground = new SolidColorBrush(Color.FromRgb(128, 128, 128)),
            FontSize = 12
        });

        // Type name in teal
        panel.Children.Add(new TextBlock
        {
            Text = typeName,
            Foreground = new SolidColorBrush(Color.FromRgb(78, 201, 176)),
            FontSize = 12
        });

        // Identifier name in light blue (only if different from type)
        if (identifier != typeName && category != "type" && category != "class" && category != "struct")
        {
            panel.Children.Add(new TextBlock
            {
                Text = $" {identifier}",
                Foreground = new SolidColorBrush(Color.FromRgb(156, 220, 254)),
                FontSize = 12
            });
        }

        _currentToolTip.Content = panel;
        _currentToolTip.IsOpen = true;
    }

    private void TextEditor_MouseHoverStopped(object sender, MouseEventArgs e)
    {
        if (_currentToolTip != null)
        {
            _currentToolTip.IsOpen = false;
            _currentToolTip = null;
        }
    }

    private async void Rename_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        if (_currentProject == null || _activeFile == null || _refactoringProvider == null) return;
        
        // Sync current content
        _activeFile.Content = CodeEditor.Text;
        var offset = CodeEditor.CaretOffset;
        
        // 1. Try to resolve symbol for renaming
        SetStatus("Analyzing...", false);
        var checkResult = await _refactoringProvider.GetRenameEditsAsync(_currentProject, _activeFile.FilePath, offset, "check");
        
        var contextMenu = new ContextMenu();
        bool hasItems = false;

        // 2. If valid symbol, add Rename option
        if (checkResult.Success)
        {
            var renameItem = new MenuItemHeader { Header = "Rename Symbol..." };
            renameItem.Click += (s, args) => PerformRename(checkResult.OriginalName ?? "");
            // Add icon if possible, or just text
            contextMenu.Items.Add(renameItem);
            hasItems = true;
        }

        // 3. Check for missing namespaces (always check, just in case)
        var word = GetWordAtOffset(CodeEditor.Document, offset);
        if (!string.IsNullOrEmpty(word))
        {
            var namespaces = TypeInspector.FindNamespacesForType(word);
            
            // Filter out namespaces that are already in the file
            var currentCode = CodeEditor.Text;
            var newNamespaces = namespaces.Where(ns => !currentCode.Contains($"using {ns};")).ToList();

            if (newNamespaces.Count > 0)
            {
                if (hasItems) contextMenu.Items.Add(new Separator());

                foreach (var ns in newNamespaces)
                {
                    var item = new MenuItemHeader { Header = $"using {ns};" };
                    item.Click += (s, args) => AddUsingStatement(ns);
                    contextMenu.Items.Add(item);
                }
                hasItems = true;
            }
        }

        // 4. Show menu or feedback
        if (hasItems)
        {
            // Position the menu near the caret/variable, not at mouse cursor
            var caretPos = CodeEditor.TextArea.Caret.CalculateCaretRectangle();
            var visualPos = CodeEditor.TextArea.TextView.GetVisualPosition(
                new TextViewPosition(CodeEditor.TextArea.Caret.Line, CodeEditor.TextArea.Caret.Column),
                ICSharpCode.AvalonEdit.Rendering.VisualYPosition.LineBottom);
            var screenPos = CodeEditor.TextArea.TextView.PointToScreen(visualPos);

            contextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.AbsolutePoint;
            contextMenu.HorizontalOffset = screenPos.X;
            contextMenu.VerticalOffset = screenPos.Y;

            CodeEditor.ContextMenu = contextMenu;
            contextMenu.IsOpen = true;
            SetStatus("Quick actions available", false);
        }
        else
        {
            SetStatus(checkResult.Error ?? "No actions available", true);
        }
    }

    private void PerformRename(string originalName)
    {
        var dialog = new RenameDialog(originalName);
        dialog.Owner = this;
        if (dialog.ShowDialog() == true && dialog.NewName != originalName)
        {
             ExecuteRename(dialog.NewName);
        }
    }

    private async void ExecuteRename(string newName)
    {
        if (_currentProject == null || _activeFile == null || _refactoringProvider == null) return;
        
        var offset = CodeEditor.CaretOffset;
        var result = await _refactoringProvider.GetRenameEditsAsync(_currentProject, _activeFile.FilePath, offset, newName);
        
        if (result.Success && result.Changes != null)
        {
            ApplyRefactoring(result.Changes);
            SetStatus("Rename applied", false);
        }
        else
        {
            SetStatus(result.Error ?? "Rename failed", true);
        }
    }

    private void AddUsingStatement(string namespaceName)
    {
        var document = CodeEditor.Document;
        var text = document.Text;
        
        // Simple insertion logic: find the last using or insert at top
        int insertOffset = 0;
        var lines = text.Split('\n');
        int lastUsingLine = -1;
        
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.StartsWith("using ") && line.EndsWith(";"))
            {
                lastUsingLine = i;
            }
            else if (!string.IsNullOrWhiteSpace(line) && !line.StartsWith("//") && lastUsingLine != -1)
            {
                // Found code after usings
                break;
            }
        }

        string textToInsert = $"using {namespaceName};\n";

        if (lastUsingLine >= 0)
        {
            // Insert after the last using
             var line = document.GetLineByNumber(lastUsingLine + 1); // 1-indexed
             insertOffset = line.EndOffset;
             textToInsert = Environment.NewLine + $"using {namespaceName};";
        }
        else
        {
            // Insert at top
            insertOffset = 0;
        }

        document.Insert(insertOffset, textToInsert);
        SetStatus($"Added using {namespaceName};", false);
    }

    private string GetWordAtOffset(TextDocument document, int offset)
    {
        if (offset < 0 || offset >= document.TextLength) return "";

        var start = offset;
        var end = offset;

        // Scan backwards
        while (start > 0)
        {
            char c = document.GetCharAt(start - 1);
            if (!char.IsLetterOrDigit(c) && c != '_') break;
            start--;
        }

        // Scan forwards
        while (end < document.TextLength)
        {
            char c = document.GetCharAt(end);
            if (!char.IsLetterOrDigit(c) && c != '_') break;
            end++;
        }

        if (end > start)
        {
            return document.GetText(start, end - start);
        }
        
        return "";
    }

    private void CodeEditor_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (CodeEditor.ContextMenu == null) return;
        
        var moveItem = CodeEditor.ContextMenu.Items.OfType<MenuItem>().FirstOrDefault(i => i.Name == "MoveTypeMenuItem");
        if (moveItem == null) return;

        moveItem.Visibility = Visibility.Collapsed;

        if (_activeFile == null) return;

        // Determine class under cursor
        var pos = CodeEditor.CaretOffset;
        // Safety check
        if (CodeEditor.Text == null || pos > CodeEditor.Text.Length) return;
        
        var fullCode = CodeEditor.Text;

        // Find the class declaration that contains the cursor position
        // This handles cases where cursor is in middle of class name
        string? className = null;
        var classMatches = Regex.Matches(fullCode, @"\bclass\s+(\w+)");
        foreach (Match match in classMatches)
        {
            var declStart = match.Index;
            var name = match.Groups[1].Value;

            // Skip the main Viz entry point class
            if (name == "Viz") continue;

            // Find the class body bounds (from declaration to closing brace)
            var openBrace = fullCode.IndexOf('{', declStart);
            if (openBrace == -1) continue;

            // Count braces to find class end
            int braceCount = 1;
            int classEnd = -1;
            for (int i = openBrace + 1; i < fullCode.Length; i++)
            {
                if (fullCode[i] == '{') braceCount++;
                else if (fullCode[i] == '}')
                {
                    braceCount--;
                    if (braceCount == 0)
                    {
                        classEnd = i + 1;
                        break;
                    }
                }
            }

            if (classEnd == -1) continue;

            // Check if cursor is within this class (from declaration start to class end)
            if (pos >= declStart && pos <= classEnd)
            {
                className = name;
                break;
            }
        }

        if (!string.IsNullOrEmpty(className))
        {
            var fileName = _activeFile.FileName ?? "";
            var ext = Path.GetExtension(fileName);
            if (string.IsNullOrEmpty(ext))
                ext = _currentProject?.ProjectFile.Language == ProjectLanguage.FSharp ? ".fs" : ".cs";

            moveItem.Header = $"Move type '{className}' to {className}{ext}";
            moveItem.Tag = className;
            moveItem.Visibility = Visibility.Visible;
        }
    }

    private void MoveTypeMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (_activeFile == null || _currentProject == null) return;

        if (sender is MenuItem item && item.Tag is string className)
        {
            MoveTypeToNewFile(className);
        }
    }

    private void MoveTypeToNewFile(string className)
    {
        if (_activeFile == null || _currentProject == null) return;

        var code = CodeEditor.Text;
        if (string.IsNullOrEmpty(code)) return;
        
        // Find class definition
        var match = Regex.Match(code, $@"\b(?:public\s+|private\s+|internal\s+)?class\s+{className}\b");
        if (!match.Success) return;

        var classStart = match.Index;
        var openBrace = code.IndexOf('{', classStart);
        if (openBrace == -1) return;

        // Find end of class by counting braces
        int braceCount = 1;
        int endPos = -1;
        for (int i = openBrace + 1; i < code.Length; i++)
        {
            if (code[i] == '{') braceCount++;
            else if (code[i] == '}')
            {
                braceCount--;
                if (braceCount == 0)
                {
                    endPos = i + 1;
                    break;
                }
            }
        }

        if (endPos == -1) return;

        // Extract class code
        var classCode = code.Substring(classStart, endPos - classStart);
        
        // Remove from current file (and potentially trailing newline)
        var newCode = code.Remove(classStart, endPos - classStart).TrimEnd();
        CodeEditor.Text = newCode;
        _activeFile.Content = newCode;

        // Create new file
        var fileName = _activeFile.FileName ?? "";
        var ext = Path.GetExtension(fileName);
        if (string.IsNullOrEmpty(ext))
            ext = _currentProject.ProjectFile.Language == ProjectLanguage.FSharp ? ".fs" : ".cs";
        var newFileName = $"{className}{ext}";
        
        // Basic template for new file (preserving usings if possible, or just the class)
        // Ideally we copy using statements from original file
        var usings = Regex.Matches(code, @"^using\s+[\w\.]+;", RegexOptions.Multiline)
                          .Select(m => m.Value)
                          .Distinct();
        var header = string.Join(Environment.NewLine, usings);
        
        // Check for namespace
        var nsMatch = Regex.Match(code, @"\bnamespace\s+([\w\.]+)");
        string newFileContent;
        
        if (nsMatch.Success)
        {
            var ns = nsMatch.Groups[1].Value;
            newFileContent = $"{header}\n\nnamespace {ns}\n{{\n    {classCode}\n}}";
        }
        else
        {
            newFileContent = $"{header}\n\n{classCode}";
        }

        // Create the file in project
        var newFile = new VizCodeFile
        {
            FilePath = Path.Combine(_currentProject.ProjectDirectory, newFileName),
            Content = newFileContent,
            HasUnsavedChanges = true,
            IsNew = true
        };
        
        _currentProject.Files.Add(newFile);
        RefreshFileTabs();
        
        SetStatus($"Moved '{className}' to {newFileName}", false);
    }


    // Helper class for simpler menu item creation (to avoid casting ambiguity if any)
    private class MenuItemHeader : MenuItem { }

    private void ApplyRefactoring(Dictionary<string, List<(int Offset, int Length, string NewText)>> changes)
    {
        int totalChanges = 0;
        
        foreach (var kvp in changes)
        {
            var filePath = kvp.Key;
            var fileChanges = kvp.Value;
            
            var file = _currentProject?.Files.FirstOrDefault(f => f.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase) || Path.GetFileName(f.FilePath).Equals(Path.GetFileName(filePath), StringComparison.OrdinalIgnoreCase));
            
            if (file != null)
            {
                // Check if this is the active file
                if (_activeFile == file)
                {
                    CodeEditor.Document.BeginUpdate();
                    foreach (var change in fileChanges) // assume sorted descending
                    {
                        CodeEditor.Document.Replace(change.Offset, change.Length, change.NewText);
                    }
                    CodeEditor.Document.EndUpdate();
                }
                else
                {
                    // Apply to string content
                    // Need to apply from end to start to avoid offset shifting
                    var content = file.Content;
                    foreach (var change in fileChanges)
                    {
                        if (change.Offset + change.Length <= content.Length)
                        {
                            content = content.Remove(change.Offset, change.Length).Insert(change.Offset, change.NewText);
                        }
                    }
                    file.Content = content;
                }
                
                file.HasUnsavedChanges = true;
                totalChanges += fileChanges.Count;
            }
        }
        
        RefreshFileTabs(); 
        SetStatus($"Renamed {totalChanges} occurrences.", false);
    }
}
