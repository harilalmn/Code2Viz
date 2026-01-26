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
using Code2Viz.Commands;
using Code2Viz.Console;
using Code2Viz.Editor;
using Code2Viz.Execution;
using Code2Viz.Export;
using Code2Viz.Project;
using Code2Viz.Search;
using ICSharpCode.AvalonEdit.Rendering;
using Microsoft.CodeAnalysis;

// Resolve ambiguities between WPF and WinForms/Drawing
using Color = System.Windows.Media.Color;
using Point = System.Windows.Point;
using Size = System.Windows.Size;
using Pen = System.Windows.Media.Pen;
using Brush = System.Windows.Media.Brush;
using FontFamily = System.Windows.Media.FontFamily;
using FontStyle = System.Windows.FontStyle;
using FontWeight = System.Windows.FontWeight;
using ToolTip = System.Windows.Controls.ToolTip;
using ContextMenu = System.Windows.Controls.ContextMenu;
using MenuItem = System.Windows.Controls.MenuItem;
using Control = System.Windows.Controls.Control;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;
using Cursors = System.Windows.Input.Cursors;
using Cursor = System.Windows.Input.Cursor;
using Button = System.Windows.Controls.Button;
using TextBox = System.Windows.Controls.TextBox;
using ComboBox = System.Windows.Controls.ComboBox;
using CheckBox = System.Windows.Controls.CheckBox;
using Label = System.Windows.Controls.Label;
using Image = System.Windows.Controls.Image;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;

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
    private MultiSelectionRenderer? _multiSelectionRenderer;
    private SelectionHighlightRenderer? _selectionHighlightRenderer;

    // Real-time error checking
    private DispatcherTimer? _syntaxCheckTimer;
    private bool _textChangedSinceLastCheck;

    // Animation
    private DispatcherTimer? _animationTimer;
    private System.Diagnostics.Stopwatch _animationStopwatch = new();

    // Peek Definition popup
    private System.Windows.Controls.Primitives.Popup? _peekPopup;

    // Inlay Hints
    private Editor.InlayHintGenerator? _inlayHintGenerator;

    // Semantic Highlighting
    private Editor.SemanticHighlighter? _semanticHighlighter;
    private DispatcherTimer? _semanticUpdateTimer;

    // Auto-update Canvas (debounced auto-run)
    private DispatcherTimer? _autoUpdateTimer;

    // Code Lens
    private Editor.CodeLensGenerator? _codeLensGenerator;

    // Hierarchy Provider
    private Editor.HierarchyProvider? _hierarchyProvider;

    // Find and Replace
    private FindReplaceService _findReplaceService = new();
    private FindReplaceDialog? _findReplaceDialog;

    public static RoutedCommand RenameCommand = new RoutedCommand();
    public static RoutedCommand GoToDefinitionCommand = new RoutedCommand();
    public static RoutedCommand FindAllReferencesCommand = new RoutedCommand();
    public static RoutedCommand PeekDefinitionCommand = new RoutedCommand();
    public static RoutedCommand DocumentSymbolsCommand = new RoutedCommand();
    public static RoutedCommand WorkspaceSymbolsCommand = new RoutedCommand();
    public static RoutedCommand CallHierarchyCommand = new RoutedCommand();
    public static RoutedCommand TypeHierarchyCommand = new RoutedCommand();
    public static RoutedCommand DirectRenameCommand = new RoutedCommand();

    public MainWindow(VizCodeProject? project = null)
    {
        InitializeComponent();

        _compiler = new ModuleCompiler();
        _refactoringProvider = new RefactoringProvider(_compiler);
        _hierarchyProvider = new Editor.HierarchyProvider();

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

        // Apply application settings
        var settings = ApplicationSettings.Instance;
        RenderCanvas.ShowGrid = settings.ShowGrid;
        GridMenuItem.IsChecked = settings.ShowGrid;

        // Apply window visibility settings
        ApplyWindowVisibilitySettings();
    }

    private void InitializeCanvas()
    {
        RenderCanvas.MouseWorldPositionChanged += (s, pos) =>
        {
            CoordinatesText.Text = $"X: {pos.X:F2}  Y: {pos.Y:F2}";
        };

        // Selection changes
        RenderCanvas.SelectionTool.SelectionChanged += OnSelectionChanged;

        // Control point drag ended - update code when shape is moved
        RenderCanvas.SelectionTool.ControlPointDragEnded += OnControlPointDragEnded;

        // Timeline panel events
        TimelinePanel.TimeChanged += (s, time) =>
        {
            var timeline = CanvasRenderer.Instance.ActiveTimeline;
            if (timeline != null)
            {
                // Pause if scrubbing
                if (timeline.IsPlaying)
                {
                    _isPaused = true;
                    timeline.IsPlaying = false;
                    _animationStopwatch.Stop();
                    PlayPauseBtn.Content = "\u25B6";
                }
                RenderCanvas.Refresh();
            }
        };

        // Animation Loop
        _animationTimer = new DispatcherTimer();
        _animationTimer.Interval = TimeSpan.FromMilliseconds(16); // ~60 FPS
        bool _needsInitialZoom = true;
        _animationTimer.Tick += (s, e) =>
        {
            var timeline = CanvasRenderer.Instance.ActiveTimeline;

            // Update animation controls visibility and time display
            UpdateAnimationControlsVisibility();

            if (timeline != null && timeline.IsPlaying)
            {
                // Update animation state (sets DrawFactor, positions, etc.)
                // Apply speed multiplier
                var scaledTime = _animationStopwatch.Elapsed.TotalSeconds * timeline.Speed;
                timeline.Update(scaledTime);

                // Redraw canvas with updated shape properties
                RenderCanvas.Refresh();

                // Zoom to fit on first frame that has visible shapes (if setting enabled)
                if (_needsInitialZoom && timeline.Shapes.Count > 0)
                {
                    if (ApplicationSettings.Instance.ZoomToFitOnRun)
                    {
                        RenderCanvas.ZoomExtents(CanvasRenderer.Instance.GetShapes());
                    }
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

        // Initialize Find Results Panel
        FindResultsPanel.ResultActivated += (s, result) =>
        {
            if (result != null)
            {
                NavigateToSearchResult(result);
            }
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

    private bool _isResizingConsole;
    private Point _consoleResizeStartPoint;
    private double _consoleResizeStartHeight;

    private void ConsoleSplitter_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Handle double-click to reset layout
        if (e.ClickCount == 2)
        {
            ResetCanvasConsoleLayout();
            e.Handled = true;
            return;
        }

        // Start resize drag
        _isResizingConsole = true;
        _consoleResizeStartPoint = e.GetPosition(CanvasConsoleGrid);
        _consoleResizeStartHeight = ConsoleRow.ActualHeight;
        ((Border)sender).CaptureMouse();
        e.Handled = true;
    }

    private void ConsoleSplitter_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isResizingConsole)
        {
            _isResizingConsole = false;
            ((Border)sender).ReleaseMouseCapture();
            e.Handled = true;
        }
    }

    private void ConsoleSplitter_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isResizingConsole)
        {
            var currentPoint = e.GetPosition(CanvasConsoleGrid);
            var delta = _consoleResizeStartPoint.Y - currentPoint.Y;
            var newHeight = _consoleResizeStartHeight + delta;

            // Apply min/max constraints
            var minHeight = 80.0;
            var maxHeight = CanvasConsoleGrid.ActualHeight - 200; // Keep canvas at least 200px

            newHeight = Math.Max(minHeight, Math.Min(maxHeight, newHeight));

            // Update the console row height
            ConsoleRow.Height = new GridLength(newHeight);
            // Keep canvas as star-sized to fill remaining space
            CanvasRow.Height = new GridLength(1, GridUnitType.Star);

            e.Handled = true;
        }
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
        // Reset the canvas/console splitter to default 3:1 ratio
        CanvasRow.Height = new GridLength(3, GridUnitType.Star);
        ConsoleRow.Height = new GridLength(1, GridUnitType.Star);
    }

    private void Caret_PositionChanged(object? sender, EventArgs e)
    {
        if (_bracketRenderer == null) return;

        var result = BracketSearcher.SearchBracket(CodeEditor.Document, CodeEditor.CaretOffset);
        _bracketRenderer.Result = result;
        CodeEditor.TextArea.TextView.InvalidateLayer(KnownLayer.Selection);

        // Update breadcrumb navigation
        UpdateBreadcrumb();
    }

    private void UpdateBreadcrumb()
    {
        try
        {
            var text = CodeEditor.Text;
            var offset = CodeEditor.CaretOffset;
            var line = CodeEditor.TextArea.Caret.Line;

            // Find current namespace, class, and method
            var breadcrumbParts = new List<(string Text, string Kind)>();

            // Parse backwards to find enclosing constructs
            var currentNamespace = FindEnclosingConstruct(text, offset, "namespace");
            var currentClass = FindEnclosingConstruct(text, offset, "class");
            var currentMethod = FindEnclosingMethod(text, offset);

            if (!string.IsNullOrEmpty(currentNamespace))
                breadcrumbParts.Add((currentNamespace, "namespace"));

            if (!string.IsNullOrEmpty(currentClass))
                breadcrumbParts.Add((currentClass, "class"));

            if (!string.IsNullOrEmpty(currentMethod))
                breadcrumbParts.Add((currentMethod, "method"));

            // Update UI
            BreadcrumbPanel.Children.Clear();

            if (breadcrumbParts.Count == 0)
            {
                BreadcrumbText.Text = _activeFile?.FileName ?? "Ready";
                BreadcrumbPanel.Children.Add(BreadcrumbText);
            }
            else
            {
                for (int i = 0; i < breadcrumbParts.Count; i++)
                {
                    var (partText, kind) = breadcrumbParts[i];

                    // Add separator
                    if (i > 0)
                    {
                        BreadcrumbPanel.Children.Add(new TextBlock
                        {
                            Text = " > ",
                            Foreground = new SolidColorBrush(Color.FromRgb(128, 128, 128)),
                            VerticalAlignment = VerticalAlignment.Center
                        });
                    }

                    // Color based on kind
                    var color = kind switch
                    {
                        "namespace" => Color.FromRgb(86, 156, 214),   // Blue
                        "class" => Color.FromRgb(78, 201, 176),       // Teal
                        "method" => Color.FromRgb(220, 220, 170),     // Yellow
                        _ => Color.FromRgb(156, 220, 254)             // Light blue
                    };

                    var textBlock = new TextBlock
                    {
                        Text = partText,
                        Foreground = new SolidColorBrush(color),
                        VerticalAlignment = VerticalAlignment.Center,
                        Cursor = System.Windows.Input.Cursors.Hand
                    };

                    BreadcrumbPanel.Children.Add(textBlock);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"UpdateBreadcrumb error: {ex.Message}");
        }
    }

    private string? FindEnclosingConstruct(string text, int offset, string keyword)
    {
        // Simple regex-based search for enclosing namespace or class
        var pattern = keyword == "namespace"
            ? @"namespace\s+([a-zA-Z_][a-zA-Z0-9_\.]*)"
            : @"(?:class|struct|interface|record)\s+([a-zA-Z_][a-zA-Z0-9_<>,\s]*)";

        var matches = System.Text.RegularExpressions.Regex.Matches(text.Substring(0, Math.Min(offset, text.Length)), pattern);

        // Find the last match that starts before the offset
        string? result = null;
        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            // Check if we're still inside this construct by looking for matching braces
            var constructStart = match.Index;
            var braceDepth = 0;
            var inConstruct = false;

            for (int i = constructStart; i < Math.Min(offset, text.Length); i++)
            {
                if (text[i] == '{')
                {
                    braceDepth++;
                    inConstruct = true;
                }
                else if (text[i] == '}')
                {
                    braceDepth--;
                    if (braceDepth <= 0 && inConstruct)
                    {
                        inConstruct = false;
                        break;
                    }
                }
            }

            if (braceDepth > 0 || !inConstruct)
            {
                var name = match.Groups[1].Value.Trim();
                // Clean up generics
                var angleIndex = name.IndexOf('<');
                if (angleIndex > 0 && keyword != "namespace")
                {
                    name = name.Substring(0, angleIndex);
                }
                result = name;
            }
        }

        return result;
    }

    private string? FindEnclosingMethod(string text, int offset)
    {
        // Find method declarations before the offset
        var pattern = @"(?:public|private|protected|internal|static|async|override|virtual|abstract|\s)+\s+\S+\s+([a-zA-Z_][a-zA-Z0-9_]*)\s*\([^)]*\)\s*(?:where[^{]*)?{";
        var matches = System.Text.RegularExpressions.Regex.Matches(text.Substring(0, Math.Min(offset, text.Length)), pattern);

        string? result = null;
        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            var methodStart = match.Index;
            var braceStart = match.Index + match.Length - 1;
            var braceDepth = 1;

            // Check if we're inside this method's body
            for (int i = braceStart + 1; i < Math.Min(offset, text.Length); i++)
            {
                if (text[i] == '{') braceDepth++;
                else if (text[i] == '}')
                {
                    braceDepth--;
                    if (braceDepth <= 0) break;
                }
            }

            if (braceDepth > 0)
            {
                result = match.Groups[1].Value;
            }
        }

        return result;
    }

    // Flag to prevent clearing multi-selections when AddNextOccurrence changes the selection
    private bool _isAddingNextOccurrence;

    // Flag to suppress marking file as unsaved during programmatic text changes
    private bool _suppressUnsavedMarking;

    private void TextArea_SelectionChanged_ClearMultiSelect(object? sender, EventArgs e)
    {
        // Don't clear if we're in the middle of AddNextOccurrence or multi-cursor editing
        if (_isAddingNextOccurrence || _isMultiCursorEditing) return;

        // Clear multi-selections when user manually changes selection
        _multiSelectionRenderer?.ClearSelections();
    }

    private void OnCodeEditorSelectionChanged(object? sender, EventArgs e)
    {
        if (_selectionHighlightRenderer != null)
        {
            _selectionHighlightRenderer.UpdateSelection(CodeEditor.SelectedText);
        }
    }

    private void TextArea_PreviewMouseDown_ClearMultiSelect(object? sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        // Ctrl+Alt+Click adds a new cursor at the click position
        if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed &&
            Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Alt))
        {
            // Get the position from mouse click
            var position = CodeEditor.TextArea.TextView.GetPositionFloor(e.GetPosition(CodeEditor.TextArea.TextView));
            if (position.HasValue)
            {
                var offset = CodeEditor.Document.GetOffset(position.Value.Location);

                // Initialize multi-selection renderer if needed, starting from current caret
                if (_multiSelectionRenderer != null)
                {
                    if (!_multiSelectionRenderer.HasSelections)
                    {
                        // Add the current caret position as first cursor
                        _multiSelectionRenderer.AddSelection(CodeEditor.CaretOffset, 0);
                    }
                    // Add new cursor at click position
                    _multiSelectionRenderer.AddSelection(offset, 0);
                }

                e.Handled = true;
                return;
            }
        }

        // Clear multi-selections when user clicks in the text area (without Ctrl+Alt)
        // This handles the case where SelectionChanged doesn't fire (e.g., clicking to place caret)
        if (_multiSelectionRenderer != null && _multiSelectionRenderer.HasSelections)
        {
            _multiSelectionRenderer.ClearSelections();
        }
    }

    // Flag to prevent clearing multi-selections during multi-cursor editing
    private bool _isMultiCursorEditing;

    private void TextArea_TextEntering_MultiCursor(object? sender, System.Windows.Input.TextCompositionEventArgs e)
    {
        // If we have multi-selections and user types, apply to all cursors
        if (_multiSelectionRenderer != null && _multiSelectionRenderer.HasSelections && !string.IsNullOrEmpty(e.Text))
        {
            _isMultiCursorEditing = true;
            _isAddingNextOccurrence = true;
            try
            {
                _multiSelectionRenderer.InsertTextAtAllCursors(e.Text);
                e.Handled = true; // Prevent default handling
            }
            finally
            {
                _isAddingNextOccurrence = false;
                _isMultiCursorEditing = false;
            }
        }
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
            if (_activeFile != null && !_suppressUnsavedMarking)
            {
                _activeFile.HasUnsavedChanges = true;
                RefreshFileTabs();
            }
        };

        // Initialize TextMarkerService
        _textMarkerService = new VizTextMarkerService(CodeEditor.Document);
        CodeEditor.TextArea.TextView.BackgroundRenderers.Add(_textMarkerService);

        CodeEditor.TextArea.TextView.Services.AddService(typeof(VizTextMarkerService), _textMarkerService);
        
        // Initial options
        CodeEditor.Options.ConvertTabsToSpaces = true;
        CodeEditor.Options.IndentationSize = 4;
        
        // Handle KeyDown for shortcuts
        CodeEditor.TextArea.KeyDown += CodeEditor_KeyDown;
        
        // Marker events
        CodeEditor.MouseHover += TextEditor_MouseHover;
        CodeEditor.MouseHoverStopped += TextEditor_MouseHoverStopped;
        
        // Refactoring key binding (Ctrl+.)
        CodeEditor.InputBindings.Add(new KeyBinding(RenameCommand, new KeyGesture(Key.OemPeriod, ModifierKeys.Control)));
        CommandBindings.Add(new CommandBinding(RenameCommand, Rename_Executed));

        // Go to Definition (F12)
        CodeEditor.InputBindings.Add(new KeyBinding(GoToDefinitionCommand, new KeyGesture(Key.F12)));
        CommandBindings.Add(new CommandBinding(GoToDefinitionCommand, GoToDefinition_Executed));

        // Find All References (Shift+F12)
        CodeEditor.InputBindings.Add(new KeyBinding(FindAllReferencesCommand, new KeyGesture(Key.F12, ModifierKeys.Shift)));
        CommandBindings.Add(new CommandBinding(FindAllReferencesCommand, FindAllReferences_Executed));

        // Peek Definition (Alt+F12)
        CodeEditor.InputBindings.Add(new KeyBinding(PeekDefinitionCommand, new KeyGesture(Key.F12, ModifierKeys.Alt)));
        CommandBindings.Add(new CommandBinding(PeekDefinitionCommand, PeekDefinition_Executed));

        // Document Symbols (Ctrl+Shift+O)
        CodeEditor.InputBindings.Add(new KeyBinding(DocumentSymbolsCommand, new KeyGesture(Key.O, ModifierKeys.Control | ModifierKeys.Shift)));
        CommandBindings.Add(new CommandBinding(DocumentSymbolsCommand, DocumentSymbols_Executed));

        // Workspace Symbols (Ctrl+T)
        CodeEditor.InputBindings.Add(new KeyBinding(WorkspaceSymbolsCommand, new KeyGesture(Key.T, ModifierKeys.Control)));
        CommandBindings.Add(new CommandBinding(WorkspaceSymbolsCommand, WorkspaceSymbols_Executed));

        // Call Hierarchy (Ctrl+Shift+H)
        CodeEditor.InputBindings.Add(new KeyBinding(CallHierarchyCommand, new KeyGesture(Key.H, ModifierKeys.Control | ModifierKeys.Shift)));
        CommandBindings.Add(new CommandBinding(CallHierarchyCommand, CallHierarchy_Executed));

        // Type Hierarchy (Ctrl+Shift+T)
        CodeEditor.InputBindings.Add(new KeyBinding(TypeHierarchyCommand, new KeyGesture(Key.T, ModifierKeys.Control | ModifierKeys.Shift)));
        CommandBindings.Add(new CommandBinding(TypeHierarchyCommand, TypeHierarchy_Executed));

        // Direct Rename (F2)
        CodeEditor.InputBindings.Add(new KeyBinding(DirectRenameCommand, new KeyGesture(Key.F2)));
        CommandBindings.Add(new CommandBinding(DirectRenameCommand, DirectRename_Executed));

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

        // Initialize Multi-Selection Highlighting (for Ctrl+D)
        _multiSelectionRenderer = new MultiSelectionRenderer(CodeEditor.TextArea.TextView);
        CodeEditor.TextArea.TextView.BackgroundRenderers.Add(_multiSelectionRenderer);
        CodeEditor.TextArea.SelectionChanged += TextArea_SelectionChanged_ClearMultiSelect;

        // Initialize Selection Highlight Renderer (Draws occurrences of selected text)
        _selectionHighlightRenderer = new SelectionHighlightRenderer(CodeEditor.TextArea.TextView);
        CodeEditor.TextArea.TextView.BackgroundRenderers.Add(_selectionHighlightRenderer);
        CodeEditor.TextArea.SelectionChanged += OnCodeEditorSelectionChanged;
        CodeEditor.TextArea.TextEntering += TextArea_TextEntering_MultiCursor;
        CodeEditor.TextArea.PreviewMouseDown += TextArea_PreviewMouseDown_ClearMultiSelect;

        // Initialize Inlay Hints
        _inlayHintGenerator = new Editor.InlayHintGenerator(CodeEditor.Document);
        CodeEditor.TextArea.TextView.ElementGenerators.Add(_inlayHintGenerator);
        _inlayHintGenerator.Enabled = false; // Disabled by default, can be enabled via menu

        // Initialize Semantic Highlighting
        _semanticHighlighter = new Editor.SemanticHighlighter(CodeEditor.Document);
        CodeEditor.TextArea.TextView.LineTransformers.Add(_semanticHighlighter);
        _semanticHighlighter.Enabled = true; // Enabled by default

        // Timer for debounced semantic highlighting updates
        _semanticUpdateTimer = new DispatcherTimer();
        _semanticUpdateTimer.Interval = TimeSpan.FromMilliseconds(500);
        _semanticUpdateTimer.Tick += async (s, e) =>
        {
            _semanticUpdateTimer.Stop();
            await UpdateSemanticHighlightingAsync();
        };

        // Initialize Code Lens
        _codeLensGenerator = new Editor.CodeLensGenerator(CodeEditor.Document);
        CodeEditor.TextArea.TextView.ElementGenerators.Add(_codeLensGenerator);
        _codeLensGenerator.Enabled = false; // Disabled by default (can be slow)

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

        // Auto-update canvas timer (debounced auto-run)
        _autoUpdateTimer = new DispatcherTimer();
        _autoUpdateTimer.Interval = TimeSpan.FromMilliseconds(ApplicationSettings.Instance.AutoUpdateDelayMs);
        _autoUpdateTimer.Tick += async (s, e) =>
        {
            _autoUpdateTimer.Stop();
            if (ApplicationSettings.Instance.AutoUpdateCanvas && _currentProject != null)
            {
                await AutoRunCodeAsync();
            }
        };

        // Trigger auto-update on text changes
        CodeEditor.TextChanged += (s, e) =>
        {
            if (ApplicationSettings.Instance.AutoUpdateCanvas)
            {
                _autoUpdateTimer.Stop();
                _autoUpdateTimer.Start();
            }
        };

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
            // Filter existing window
        }
        else if (e.Text == ".")
        {
             // Trigger completion on dot
             TriggerManualCompletion();
        }
        else if (e.Text.Length > 0 && char.IsLetter(e.Text[0]) && _completionWindow == null)
        {
            // Optional: Trigger completion on typing letters (Intellisense style)
            // For now, let's stick to Ctrl+Space or Dot, unless specifically requested to auto-popup
            // But VS does popup on typing.
            // TriggerManualCompletion(); 
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
        // HIGHEST PRIORITY: Tab key for drawing input mode cycling
        // Must intercept here as well since TextArea may handle Tab before MainWindow
        if (e.Key == Key.Tab && RenderCanvas.DrawingTool.Mode != Canvas.DrawingMode.None && RenderCanvas.DrawingTool.Points.Count > 0)
        {
            e.Handled = true;
            if (RenderCanvas.DrawingTool.CycleInputMode())
            {
                RenderCanvas.Refresh();
                UpdateDrawingInputStatus();
            }
            return;
        }

        // Handle Backspace/Delete for multi-cursor editing
        if (_multiSelectionRenderer != null && _multiSelectionRenderer.HasSelections)
        {
            if (e.Key == Key.Back)
            {
                _isMultiCursorEditing = true;
                _isAddingNextOccurrence = true;
                try
                {
                    _multiSelectionRenderer.BackspaceAtAllCursors();
                    e.Handled = true;
                }
                finally
                {
                    _isAddingNextOccurrence = false;
                    _isMultiCursorEditing = false;
                }
                return;
            }
            else if (e.Key == Key.Delete)
            {
                _isMultiCursorEditing = true;
                _isAddingNextOccurrence = true;
                try
                {
                    _multiSelectionRenderer.DeleteAtAllCursors();
                    e.Handled = true;
                }
                finally
                {
                    _isAddingNextOccurrence = false;
                    _isMultiCursorEditing = false;
                }
                return;
            }
            else if (e.Key == Key.Escape)
            {
                // Escape clears multi-cursor mode
                _multiSelectionRenderer.ClearSelections();
                e.Handled = true;
                return;
            }
            else if (e.Key == Key.Left)
            {
                _isMultiCursorEditing = true;
                _isAddingNextOccurrence = true;
                try
                {
                    if (Keyboard.Modifiers == ModifierKeys.Shift)
                        _multiSelectionRenderer.ExtendAllSelectionsLeft();
                    else
                        _multiSelectionRenderer.MoveAllCursorsLeft();
                    e.Handled = true;
                }
                finally
                {
                    _isAddingNextOccurrence = false;
                    _isMultiCursorEditing = false;
                }
                return;
            }
            else if (e.Key == Key.Right)
            {
                _isMultiCursorEditing = true;
                _isAddingNextOccurrence = true;
                try
                {
                    if (Keyboard.Modifiers == ModifierKeys.Shift)
                        _multiSelectionRenderer.ExtendAllSelectionsRight();
                    else
                        _multiSelectionRenderer.MoveAllCursorsRight();
                    e.Handled = true;
                }
                finally
                {
                    _isAddingNextOccurrence = false;
                    _isMultiCursorEditing = false;
                }
                return;
            }
            else if (e.Key == Key.Up)
            {
                _isMultiCursorEditing = true;
                _isAddingNextOccurrence = true;
                try
                {
                    _multiSelectionRenderer.MoveAllCursorsUp();
                    e.Handled = true;
                }
                finally
                {
                    _isAddingNextOccurrence = false;
                    _isMultiCursorEditing = false;
                }
                return;
            }
            else if (e.Key == Key.Down)
            {
                _isMultiCursorEditing = true;
                _isAddingNextOccurrence = true;
                try
                {
                    _multiSelectionRenderer.MoveAllCursorsDown();
                    e.Handled = true;
                }
                finally
                {
                    _isAddingNextOccurrence = false;
                    _isMultiCursorEditing = false;
                }
                return;
            }
            else if (e.Key == Key.Home)
            {
                _isMultiCursorEditing = true;
                _isAddingNextOccurrence = true;
                try
                {
                    if (Keyboard.Modifiers == ModifierKeys.Shift)
                        _multiSelectionRenderer.ExtendAllSelectionsHome();
                    else
                        _multiSelectionRenderer.MoveAllCursorsHome();
                    e.Handled = true;
                }
                finally
                {
                    _isAddingNextOccurrence = false;
                    _isMultiCursorEditing = false;
                }
                return;
            }
            else if (e.Key == Key.End)
            {
                _isMultiCursorEditing = true;
                _isAddingNextOccurrence = true;
                try
                {
                    if (Keyboard.Modifiers == ModifierKeys.Shift)
                        _multiSelectionRenderer.ExtendAllSelectionsEnd();
                    else
                        _multiSelectionRenderer.MoveAllCursorsEnd();
                    e.Handled = true;
                }
                finally
                {
                    _isAddingNextOccurrence = false;
                    _isMultiCursorEditing = false;
                }
                return;
            }
        }

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

        // Handle Ctrl+Space for manual completion
        if (e.Key == Key.Space && Keyboard.Modifiers == ModifierKeys.Control)
        {
            TriggerManualCompletion();
            e.Handled = true;
            return;
        }

        // Handle Ctrl+Alt+Up/Down for adding cursors (catch before AvalonEdit)
        if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Alt))
        {
            // Check both e.Key and e.SystemKey - behavior varies
            var actualKey = e.Key == Key.System ? e.SystemKey : e.Key;
            if (actualKey == Key.Up)
            {
                _multiSelectionRenderer?.AddCursorAbove();
                e.Handled = true;
                return;
            }
            else if (actualKey == Key.Down)
            {
                _multiSelectionRenderer?.AddCursorBelow();
                e.Handled = true;
                return;
            }
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
    /// <summary>
    /// Triggers completion based on context (Ctrl+Space or typing).
    /// </summary>
    private async void TriggerManualCompletion()
    {
        if (_completionWindow != null)
            return;

        var offset = CodeEditor.CaretOffset;
        var code = CodeEditor.Text;

        try
        {
             // Use Roslyn Completion Service
             var service = new Editor.RoslynCompletionService(_compiler.GetReferences());
             var (completions, isAfterNew, prefix, expectedType) = await service.GetCompletionsAsync(code, offset);

             if (completions.Count > 0)
             {
                 _completionWindow = new CompletionWindow(CodeEditor.TextArea);

                 // Explicitly set StartOffset based on the prefix length to fix off-by-one replacement bugs
                 _completionWindow.StartOffset = offset - prefix.Length;

                 var data = _completionWindow.CompletionList.CompletionData;

                 // Sort completions: expected type first, then types when after 'new', then by match quality
                 var sortedCompletions = SortCompletions(completions, prefix, isAfterNew, expectedType);

                 foreach (var item in sortedCompletions)
                 {
                     data.Add(item);
                 }

                 // Add snippets (not when after 'new')
                 if (!isAfterNew)
                 {
                     foreach (var (trigger, description) in Editor.CodeSnippets.GetAll())
                     {
                         data.Add(new Editor.SnippetCompletionData(trigger, description, Editor.CodeSnippets.GetSnippet(trigger)!));
                     }
                 }

                 ShowCompletionWindowWithSelection();
                 _completionWindow.Closed += (s, args) => _completionWindow = null;
             }
        }
        catch (Exception ex)
        {
             System.Diagnostics.Debug.WriteLine($"Completion Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Sorts completions by match quality and context.
    /// Expected type from left-hand side appears first.
    /// Types appear first when after 'new' keyword.
    /// Best prefix matches appear at the top.
    /// </summary>
    private List<ICompletionData> SortCompletions(List<ICompletionData> completions, string prefix, bool isAfterNew, string? expectedType)
    {
        var prefixLower = prefix.ToLowerInvariant();
        var expectedTypeLower = expectedType?.ToLowerInvariant();

        return completions
            .OrderBy(c =>
            {
                var textLower = c.Text.ToLowerInvariant();
                var item = c as Editor.CompletionData;
                var isType = item?.Kind == Editor.CompletionKind.Type;

                // Priority -1: Expected type from left-hand side (e.g., VPoint p1 = new |)
                if (expectedTypeLower != null && textLower == expectedTypeLower)
                    return -1;

                // Priority 0: Exact match with typed prefix (case-insensitive)
                if (!string.IsNullOrEmpty(prefixLower) && textLower == prefixLower)
                    return 0;

                // Priority 1: Starts with prefix (case-insensitive)
                if (!string.IsNullOrEmpty(prefixLower) && textLower.StartsWith(prefixLower))
                {
                    // If after 'new', prioritize types even more
                    if (isAfterNew && isType)
                        return 1;
                    return isType ? 2 : 3;
                }

                // Priority 2: Contains prefix
                if (!string.IsNullOrEmpty(prefixLower) && textLower.Contains(prefixLower))
                {
                    if (isAfterNew && isType)
                        return 4;
                    return isType ? 5 : 6;
                }

                // Priority 3: No prefix typed - sort types first when after 'new'
                if (isAfterNew && isType)
                    return 7;
                return isType ? 8 : 9;
            })
            .ThenBy(c => c.Text.Length) // Shorter names first for same priority
            .ThenBy(c => c.Text) // Alphabetical for same length
            .ToList();
    }

    // Legacy methods removed


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
            TriggerManualCompletion();
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
            // Close existing signature help window if open
            if (_insightWindow != null)
            {
                _insightWindow.Close();
            }

            // Always show signature help when comma is typed inside parentheses
            // Use Dispatcher to ensure the window is fully closed before reopening
            Dispatcher.BeginInvoke(new Action(() =>
            {
                ShowSignatureHelp();
                TriggerManualCompletion();
            }), System.Windows.Threading.DispatcherPriority.Input);
        }
        else if (e.Text == " ")
        {
            // Close completion window when space is typed after keywords
            var offset = CodeEditor.CaretOffset;
            if (offset >= 4)
            {
                var textBefore = CodeEditor.Document.GetText(offset - 4, 3);
                if (textBefore == "new")
                {
                    TriggerManualCompletion();
                }
                else if (textBefore == "var")
                {
                    // Close completion window after 'var '
                    _completionWindow?.Close();
                }
            }
        }
        else if (e.Text == ";")
        {
            // Format on type - format the current line when semicolon is typed
            FormatCurrentLineOnType();
        }
        else if (char.IsLetter(e.Text[0]))
        {
            var offset = CodeEditor.CaretOffset;
            var wordStart = offset - 1;
            while (wordStart > 0 && char.IsLetterOrDigit(CodeEditor.Document.GetCharAt(wordStart - 1)))
            {
                wordStart--;
            }
            var currentWord = CodeEditor.Document.GetText(wordStart, offset - wordStart);
            
            // Type keywords that shouldn't trigger completion for themselves OR for the variable name after them
            var typeKeywords = new[] { "var", "int", "string", "bool", "double", "float", "char", "byte", 
                "short", "long", "decimal", "object", "void" };
            
            // Control flow keywords - no completion for themselves
            var controlKeywords = new[] { "using", "namespace", "class", "struct", 
                "interface", "enum", "return", "if", "else", "while", "for", "foreach", "switch", "case",
                "break", "continue", "try", "catch", "finally", "throw", "public", "private", "protected",
                "internal", "static", "const", "readonly", "virtual", "override", "abstract", "sealed" };
            
            if (typeKeywords.Contains(currentWord) || controlKeywords.Contains(currentWord))
            {
                // Close any existing completion window when a keyword is fully typed
                _completionWindow?.Close();
            }
            else
            {
                // Check if the PREVIOUS word (before current word) is a type keyword
                // This detects "var arc|" or "int count|" patterns
                var prevWordEnd = wordStart;
                // Skip whitespace before current word
                while (prevWordEnd > 0 && char.IsWhiteSpace(CodeEditor.Document.GetCharAt(prevWordEnd - 1)))
                {
                    prevWordEnd--;
                }
                // Find start of previous word
                var prevWordStart = prevWordEnd;
                while (prevWordStart > 0 && char.IsLetterOrDigit(CodeEditor.Document.GetCharAt(prevWordStart - 1)))
                {
                    prevWordStart--;
                }
                
                if (prevWordStart < prevWordEnd)
                {
                    var prevWord = CodeEditor.Document.GetText(prevWordStart, prevWordEnd - prevWordStart);
                    if (typeKeywords.Contains(prevWord))
                    {
                        // User is typing a variable name after a type - don't show completion
                        _completionWindow?.Close();
                        return;
                    }
                }
                
                // Show general completions after typing a letter
                TriggerManualCompletion();
            }
        }
    }

    private void FormatCurrentLineOnType()
    {
        try
        {
            var document = CodeEditor.Document;
            var line = document.GetLineByOffset(CodeEditor.CaretOffset);
            var lineText = document.GetText(line.Offset, line.Length);

            // Only format if the line has actual code (not just whitespace or comments)
            var trimmed = lineText.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("//"))
                return;

            // Format the line
            var formatted = FormatLineForOnType(lineText);

            // Only replace if different
            if (formatted != lineText)
            {
                var caretInLine = CodeEditor.CaretOffset - line.Offset;
                document.Replace(line.Offset, line.Length, formatted);

                // Try to maintain caret position relative to end of line
                var newOffset = line.Offset + Math.Min(caretInLine + (formatted.Length - lineText.Length), formatted.Length);
                CodeEditor.CaretOffset = Math.Max(line.Offset, Math.Min(newOffset, line.Offset + formatted.Length));
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"FormatCurrentLineOnType error: {ex.Message}");
        }
    }

    private string FormatLineForOnType(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return line;

        // Preserve leading whitespace (indentation)
        var leadingWhitespace = "";
        var i = 0;
        while (i < line.Length && char.IsWhiteSpace(line[i]))
        {
            leadingWhitespace += line[i];
            i++;
        }

        var content = line.Substring(i).TrimEnd();
        if (string.IsNullOrEmpty(content))
            return line;

        // Basic formatting rules (minimal to avoid breaking code)
        // Add space after keywords
        content = System.Text.RegularExpressions.Regex.Replace(content, @"\b(if|else|for|foreach|while|switch|using|return|throw|new|var|catch|finally)\(", "$1 (");

        // Add space around = but not ==, !=, <=, >=, +=, -=, =>, etc.
        content = System.Text.RegularExpressions.Regex.Replace(content, @"([^=!<>+\-*/%&|^])=(?!>)([^=])", "$1 = $2");

        // Add space after comma (but not inside strings)
        content = System.Text.RegularExpressions.Regex.Replace(content, @",([^\s])", ", $1");

        // Remove space before semicolon
        content = System.Text.RegularExpressions.Regex.Replace(content, @"\s+;", ";");

        // Remove multiple spaces
        content = System.Text.RegularExpressions.Regex.Replace(content, @"  +", " ");

        return leadingWhitespace + content;
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

            // Update inlay hints
            if (_inlayHintGenerator != null && _inlayHintGenerator.Enabled)
            {
                _inlayHintGenerator.UpdateHints(CodeEditor.Text);
                CodeEditor.TextArea.TextView.Redraw();
            }

            // Trigger semantic highlighting update (debounced)
            TriggerSemanticHighlightingUpdate();

            // Update Code Lens (debounced - done via semantic timer)
            UpdateCodeLens();

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
        TriggerManualCompletion();
    }

    private string GetAllProjectCode()
    {
        if (_currentProject == null)
            return CodeEditor.Text;

        // Make sure current editor content is synced
        SaveCurrentEditorContent();

        return string.Join("\n\n", _currentProject.Files.Select(f => f.Content));
    }

    // Legacy methods removed


    // Legacy inference methods removed


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

    private async void ShowSignatureHelp()
    {
        if (_insightWindow != null)
            return;

        var offset = CodeEditor.CaretOffset;
        var code = CodeEditor.Text;

        try
        {
             var service = new Editor.RoslynCompletionService(_compiler.GetReferences());
             var (signatures, currentParamIndex) = await service.GetSignatureHelpAsync(code, offset);

             if (signatures.Count == 0)
                 return;

             _insightWindow = new OverloadInsightWindow(CodeEditor.TextArea);
             _insightWindow.Provider = new SignatureHelpProvider(signatures, currentParamIndex);
             
             // Try to find reasonable start/end offsets for the window logic (optional)
             // Simple approach: Current cursor
             _insightWindow.StartOffset = offset;
             _insightWindow.EndOffset = CodeEditor.Document.TextLength;

             StyleInsightWindow(_insightWindow);
             _insightWindow.Show();
             _insightWindow.Closed += (s, e) => _insightWindow = null;
        }
        catch (Exception ex)
        {
             System.Diagnostics.Debug.WriteLine($"ShowSignatureHelp error: {ex}");
        }
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
        var actualType = null as string; // Legacy logic disabled
        if (actualType != null)
        {
            return TypeInspector.GetMethodSignatures(actualType, methodName);
        }

        // typePart could be a type name or namespace.type
        return TypeInspector.GetMethodSignatures(typePart, methodName);
    }

    private void StyleCompletionWindow(CompletionWindow window)
    {
        try
        {
            // Use application theme resources
            if (FindResource("SecondaryBackgroundBrush") is Brush bg)
            {
                window.Background = bg;
                window.CompletionList.Background = bg;
            }
            
            if (FindResource("BorderBrush") is Brush border)
            {
                window.BorderBrush = border;
            }
            
            if (FindResource("ForegroundBrush") is Brush fg)
            {
                window.Foreground = fg;
                window.CompletionList.Foreground = fg;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"StyleCompletionWindow error: {ex.Message}");
            // Fallback
            var darkBg = new SolidColorBrush(Color.FromRgb(37, 37, 38));
            window.Background = darkBg;
            window.CompletionList.Background = darkBg;
            window.CompletionList.Foreground = Brushes.White;
        }

        window.BorderThickness = new Thickness(1);

        // Auto width with constraints
        window.Width = double.NaN;
        window.MinWidth = 300;
        window.MaxWidth = 1000;
        window.SizeToContent = SizeToContent.Width;
    }

    /// <summary>
    /// Shows the completion window with the first item selected.
    /// </summary>
    private void ShowCompletionWindowWithSelection()
    {
        if (_completionWindow == null || _completionWindow.CompletionList.CompletionData.Count == 0)
            return;

        StyleCompletionWindow(_completionWindow);

        // Select the first item
        _completionWindow.CompletionList.SelectedItem = _completionWindow.CompletionList.CompletionData[0];

        // If signature help is visible, offset the completion window below it
        if (_insightWindow != null)
        {
            _completionWindow.Loaded += (s, e) =>
            {
                // Get the insight window's actual height and add offset
                var insightHeight = _insightWindow?.ActualHeight ?? 30;
                _completionWindow.Top += insightHeight + 2;
            };
        }

        // Close window if it becomes empty after filtering
        _completionWindow.CompletionList.ListBox.Items.CurrentChanged += (s, e) =>
        {
            if (_completionWindow != null && 
                _completionWindow.CompletionList.ListBox.Items.Count == 0)
            {
                _completionWindow.Close();
            }
        };

        _completionWindow.Show();
        _completionWindow.Closed += (s, e) => _completionWindow = null;
    }

    private void StyleInsightWindow(OverloadInsightWindow window)
    {
        try
        {
            if (FindResource("SecondaryBackgroundBrush") is Brush bg)
                window.Background = bg;
            
            if (FindResource("BorderBrush") is Brush border)
                window.BorderBrush = border;
            
            if (FindResource("ForegroundBrush") is Brush fg)
                window.Foreground = fg;
        }
        catch
        {
             // Fallback
             window.Background = new SolidColorBrush(Color.FromRgb(37, 37, 38));
             window.BorderBrush = new SolidColorBrush(Color.FromRgb(63, 63, 70));
             window.Foreground = Brushes.White;
        }

        window.BorderThickness = new Thickness(1);
        
        // Ensure good sizing for signature help
        window.Width = double.NaN;
        window.MinWidth = 500;
        window.MaxWidth = 800; // Allow sufficient width for long signatures
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
        FileTabs.ItemsSource = _currentProject?.Files.Where(f => f.IsOpen).ToList();

        if (selectedFile != null && selectedFile.IsOpen)
        {
            FileTabs.SelectedItem = selectedFile;
        }
    }

    private void SelectFile(VizCodeFile file)
    {
        // Save current editor content before switching
        SaveCurrentEditorContent();

        _activeFile = file;

        // Suppress unsaved marking when loading file content
        _suppressUnsavedMarking = true;
        CodeEditor.Text = file.Content;
        _suppressUnsavedMarking = false;

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

            // Close the tab (don't remove from project, just mark as not open)
            file.IsOpen = false;
            RefreshFileTabs();

            // Select another open file
            var openFiles = _currentProject?.Files.Where(f => f.IsOpen).ToList();
            if (openFiles?.Count > 0)
            {
                SelectFile(openFiles[0]);
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
            SettingsColorBox.Text = "";
            SettingsFillColorBox.Text = "";
            SettingsThicknessBox.Text = "";
            return;
        }

        SettingsColorBox.Text = settings.DefaultColor ?? "";
        SettingsFillColorBox.Text = settings.DefaultFillColor ?? "";
        SettingsCanvasColorBox.Text = settings.DefaultCanvasBackgroundColor ?? "";
        SettingsThicknessBox.Text = settings.DefaultLineWeight.HasValue
            ? settings.DefaultLineWeight.Value.ToString()
            : "";
        SettingsLineTypeScaleBox.Text = settings.DefaultLineTypeScale.HasValue
            ? settings.DefaultLineTypeScale.Value.ToString()
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

        // Application-level Default Shape Settings
        AppSettingsColorBox.Text = appSettings.AppDefaultStrokeColor ?? "";
        AppSettingsFillColorBox.Text = appSettings.AppDefaultFillColor ?? "";
        AppSettingsCanvasColorBox.Text = appSettings.AppDefaultCanvasBackground ?? "";
        AppSettingsThicknessBox.Text = appSettings.AppDefaultLineWeight.HasValue
            ? appSettings.AppDefaultLineWeight.Value.ToString()
            : "";
        AppSettingsLineTypeScaleBox.Text = appSettings.AppDefaultLineTypeScale.HasValue
            ? appSettings.AppDefaultLineTypeScale.Value.ToString()
            : "";

        // Snap Settings
        SnapEndpointCheck.IsChecked = appSettings.SnapEndpointEnabled;
        SnapMidpointCheck.IsChecked = appSettings.SnapMidpointEnabled;
        SnapCenterCheck.IsChecked = appSettings.SnapCenterEnabled;
        SnapIntersectionCheck.IsChecked = appSettings.SnapIntersectionEnabled;
        SnapNearestCheck.IsChecked = appSettings.SnapNearestEnabled;
        SnapPerpendicularCheck.IsChecked = appSettings.SnapPerpendicularEnabled;
        SnapExtensionCheck.IsChecked = appSettings.SnapExtensionEnabled;
        SnapTangentCheck.IsChecked = appSettings.SnapTangentEnabled;

        // Highlight Settings
        HighlightColorBox.Text = appSettings.HighlightColor ?? "Yellow";
        HighlightOpacitySlider.Value = appSettings.HighlightOpacity;
        HighlightOpacityText.Text = $"{appSettings.HighlightOpacity}%";
        UpdateColorButton(HighlightColorBtn, HighlightColorBox.Text);

        // Canvas Settings
        SettingsZoomToFitCheck.IsChecked = appSettings.ZoomToFitOnRun;
        SettingsAutoUpdateCanvasCheck.IsChecked = appSettings.AutoUpdateCanvas;

        // Update Button colors for Project Settings
        UpdateColorButton(SettingsColorBtn, SettingsColorBox.Text);
        UpdateColorButton(SettingsFillColorBtn, SettingsFillColorBox.Text);
        UpdateColorButton(SettingsCanvasColorBtn, SettingsCanvasColorBox.Text);

        // Update Button colors for Application Settings
        UpdateColorButton(AppSettingsColorBtn, AppSettingsColorBox.Text);
        UpdateColorButton(AppSettingsFillColorBtn, AppSettingsFillColorBox.Text);
        UpdateColorButton(AppSettingsCanvasColorBtn, AppSettingsCanvasColorBox.Text);
    }

    private void ColorBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        // Project Settings color boxes
        if (sender == SettingsColorBox)
            UpdateColorButton(SettingsColorBtn, SettingsColorBox.Text);
        else if (sender == SettingsFillColorBox)
            UpdateColorButton(SettingsFillColorBtn, SettingsFillColorBox.Text);
        else if (sender == SettingsCanvasColorBox)
            UpdateColorButton(SettingsCanvasColorBtn, SettingsCanvasColorBox.Text);
        // Application Settings color boxes
        else if (sender == AppSettingsColorBox)
            UpdateColorButton(AppSettingsColorBtn, AppSettingsColorBox.Text);
        else if (sender == AppSettingsFillColorBox)
            UpdateColorButton(AppSettingsFillColorBtn, AppSettingsFillColorBox.Text);
        else if (sender == AppSettingsCanvasColorBox)
            UpdateColorButton(AppSettingsCanvasColorBtn, AppSettingsCanvasColorBox.Text);
    }
    
    private void UpdateColorButton(Button btn, string colorText)
    {
        if (btn == null) return;

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
            TextBox? targetBox = tag switch
            {
                "Stroke" => SettingsColorBox,
                "Fill" => SettingsFillColorBox,
                "Canvas" => SettingsCanvasColorBox,
                "AppStroke" => AppSettingsColorBox,
                "AppFill" => AppSettingsFillColorBox,
                "AppCanvas" => AppSettingsCanvasColorBox,
                _ => null
            };

            if (targetBox == null) return;

            var dialog = new ColorPickerDialog(targetBox.Text);
            dialog.Owner = this;
            if (dialog.ShowDialog() == true)
            {
                targetBox.Text = dialog.SelectedColor;
            }
        }
    }

    private void HighlightColorBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (HighlightColorBtn != null && HighlightColorBox != null)
            UpdateColorButton(HighlightColorBtn, HighlightColorBox.Text);
    }

    private void PickHighlightColorButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ColorPickerDialog(HighlightColorBox.Text);
        dialog.Owner = this;
        if (dialog.ShowDialog() == true)
        {
            HighlightColorBox.Text = dialog.SelectedColor;
        }
    }

    private void HighlightOpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (HighlightOpacityText != null)
        {
            HighlightOpacityText.Text = $"{(int)e.NewValue}%";
        }
    }

    private void SettingsZoomToFitCheck_Changed(object sender, RoutedEventArgs e)
    {
        ApplicationSettings.Instance.ZoomToFitOnRun = SettingsZoomToFitCheck.IsChecked == true;
        ApplicationSettings.Save();
    }

    private void SettingsAutoUpdateCanvasCheck_Changed(object sender, RoutedEventArgs e)
    {
        ApplicationSettings.Instance.AutoUpdateCanvas = SettingsAutoUpdateCanvasCheck.IsChecked == true;
        ApplicationSettings.Save();
    }

    private void SaveSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        // 1. Save Project Settings
        if (_currentProject != null)
        {
            string? Color = SettingsColorBox.Text.Trim();
            if (string.IsNullOrEmpty(Color)) Color = null;

            string? fillColor = SettingsFillColorBox.Text.Trim();
            if (string.IsNullOrEmpty(fillColor)) fillColor = null;
            
            string? canvasColor = SettingsCanvasColorBox.Text.Trim();
            if (string.IsNullOrEmpty(canvasColor)) canvasColor = null;

            double? thickness = null;
            if (double.TryParse(SettingsThicknessBox.Text.Trim(), out double t))
            {
                thickness = t;
            }

            double? lineTypeScale = null;
            if (double.TryParse(SettingsLineTypeScaleBox.Text.Trim(), out double lts))
            {
                lineTypeScale = lts;
            }

            var settings = _currentProject.ProjectFile.Settings;
            settings.DefaultColor = Color;
            settings.DefaultFillColor = fillColor;
            settings.DefaultCanvasBackgroundColor = canvasColor;
            settings.DefaultLineWeight = thickness;
            settings.DefaultLineTypeScale = lineTypeScale;

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

        // Save Application-level default shape settings
        string? appStrokeColor = AppSettingsColorBox.Text.Trim();
        ApplicationSettings.Instance.AppDefaultStrokeColor = string.IsNullOrEmpty(appStrokeColor) ? null : appStrokeColor;

        string? appFillColor = AppSettingsFillColorBox.Text.Trim();
        ApplicationSettings.Instance.AppDefaultFillColor = string.IsNullOrEmpty(appFillColor) ? null : appFillColor;

        string? appCanvasColor = AppSettingsCanvasColorBox.Text.Trim();
        ApplicationSettings.Instance.AppDefaultCanvasBackground = string.IsNullOrEmpty(appCanvasColor) ? null : appCanvasColor;

        if (double.TryParse(AppSettingsThicknessBox.Text.Trim(), out double appThickness))
            ApplicationSettings.Instance.AppDefaultLineWeight = appThickness;
        else
            ApplicationSettings.Instance.AppDefaultLineWeight = null;

        if (double.TryParse(AppSettingsLineTypeScaleBox.Text.Trim(), out double appLineTypeScale))
            ApplicationSettings.Instance.AppDefaultLineTypeScale = appLineTypeScale;
        else
            ApplicationSettings.Instance.AppDefaultLineTypeScale = null;

        // Save Snap Settings
        ApplicationSettings.Instance.SnapEndpointEnabled = SnapEndpointCheck.IsChecked == true;
        ApplicationSettings.Instance.SnapMidpointEnabled = SnapMidpointCheck.IsChecked == true;
        ApplicationSettings.Instance.SnapCenterEnabled = SnapCenterCheck.IsChecked == true;
        ApplicationSettings.Instance.SnapIntersectionEnabled = SnapIntersectionCheck.IsChecked == true;
        ApplicationSettings.Instance.SnapNearestEnabled = SnapNearestCheck.IsChecked == true;
        ApplicationSettings.Instance.SnapPerpendicularEnabled = SnapPerpendicularCheck.IsChecked == true;
        ApplicationSettings.Instance.SnapExtensionEnabled = SnapExtensionCheck.IsChecked == true;
        ApplicationSettings.Instance.SnapTangentEnabled = SnapTangentCheck.IsChecked == true;

        // Save Highlight Settings
        ApplicationSettings.Instance.HighlightColor = HighlightColorBox.Text.Trim();
        ApplicationSettings.Instance.HighlightOpacity = (int)HighlightOpacitySlider.Value;

        ApplicationSettings.Save();

        // Refresh snap settings for all tools
        RenderCanvas.MeasuringTool.RefreshSnapSettings();
        RenderCanvas.SelectionTool.RefreshSnapSettings();
        RenderCanvas.DrawingTool.RefreshSnapSettings();

        SetStatus("Settings saved (Project and Application).", isError: false);
    }

    private void CloseProjectMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (!PromptSaveChanges())
            return;

        // Stop any running animations before closing
        StopAllAnimations();

        var welcome = new WelcomeWindow();
        welcome.Show();
        Close();
    }

    private void StopAllAnimations()
    {
        var timeline = CanvasRenderer.Instance.ActiveTimeline;
        if (timeline != null)
        {
            timeline.IsPlaying = false;
            timeline.Stop();
            _animationStopwatch.Reset();
        }

        // Clear active timeline reference
        CanvasRenderer.Instance.ActiveTimeline = null;

        // Hide animation controls
        AnimationControlsPanel.Visibility = Visibility.Collapsed;
        _isPaused = false;
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!PromptSaveChanges())
        {
            e.Cancel = true;
            return;
        }

        // Stop any running animations
        StopAllAnimations();

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

        // Show console tab when running code
        ShowConsoleTab();

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

                // Clear undo stack since all shapes are regenerated from code
                TransactionManager.Instance.Clear();

                var shapes = CanvasRenderer.Instance.GetShapes();
                var count = shapes.Count;

                CanvasRenderer.Instance.RenderTo(RenderCanvas);
                SetStatus($"Success: {count} shape{(count != 1 ? "s" : "")} drawn", isError: false);
                PopulateOutliner(shapes);
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

    /// <summary>
    /// Silently compiles and runs code for auto-update (no error dialogs, minimal status updates).
    /// </summary>
    private async Task AutoRunCodeAsync()
    {
        if (_currentProject == null || _currentProject.Files.Count == 0)
            return;

        // Save current editor content
        SaveCurrentEditorContent();

        // Verify entry point exists
        if (_currentProject.EntryPointFile == null)
            return;

        try
        {
            _textMarkerService?.Clear();
            var result = await _compiler.CompileAndExecuteAsync(_currentProject);

            // Apply project settings
            _currentProject.ApplySettings();
            if (_currentProject.ProjectFile.Settings.DefaultCanvasBackgroundColor is string bgCode)
            {
                try { RenderCanvas.CanvasBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(bgCode)); } catch { }
            }

            if (result.Success)
            {
                _animationStopwatch.Restart();
                TransactionManager.Instance.Clear();

                var shapes = CanvasRenderer.Instance.GetShapes();
                CanvasRenderer.Instance.RenderTo(RenderCanvas);

                // Zoom to fit if enabled in settings
                if (ApplicationSettings.Instance.ZoomToFitOnRun && shapes.Count > 0)
                {
                    RenderCanvas.ZoomExtents(shapes);
                }

                SetStatus($"Auto-update: {shapes.Count} shape{(shapes.Count != 1 ? "s" : "")}", isError: false);
                PopulateOutliner(shapes);
            }
            else
            {
                // Show error count in status bar only (no dialogs)
                var errorCount = (result.Diagnostics?.Count(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error) ?? 0)
                               + (result.FSharpDiagnostics?.Count(d => d.Severity == "Error") ?? 0);
                if (errorCount > 0)
                {
                    SetStatus($"Auto-update: {errorCount} error{(errorCount != 1 ? "s" : "")}", isError: true);
                }

                // Add error markers to editor silently
                if (result.Diagnostics != null)
                {
                    foreach (var diagnostic in result.Diagnostics.Where(d =>
                        d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error ||
                        d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Warning))
                    {
                        var lineSpan = diagnostic.Location.GetLineSpan();
                        var activePath = _activeFile?.FilePath;
                        if (activePath == null) continue;

                        bool isMatch = string.IsNullOrEmpty(lineSpan.Path) ||
                            string.Equals(lineSpan.Path, activePath, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(Path.GetFileName(lineSpan.Path), Path.GetFileName(activePath), StringComparison.OrdinalIgnoreCase);

                        if (isMatch)
                        {
                            try
                            {
                                var startLine = lineSpan.StartLinePosition.Line + 1;
                                var startCol = lineSpan.StartLinePosition.Character + 1;
                                var endLine = lineSpan.EndLinePosition.Line + 1;
                                var endCol = lineSpan.EndLinePosition.Character + 1;
                                var offset = CodeEditor.Document.GetOffset(new TextLocation(startLine, startCol));
                                var endOffset = CodeEditor.Document.GetOffset(new TextLocation(endLine, endCol));
                                var length = endOffset - offset;

                                if (length > 0)
                                {
                                    var color = diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error ? Colors.Red : Colors.Orange;
                                    _textMarkerService?.Create(offset, length, diagnostic.GetMessage(), color);
                                }
                            }
                            catch { /* Ignore invalid ranges */ }
                        }
                    }
                }
            }
        }
        catch
        {
            // Silently ignore errors during auto-update
        }
        finally
        {
            Console.ConsoleOutput.Instance.Flush();
        }
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        CanvasRenderer.Instance.Clear();
        RenderCanvas.ClearShapes();
        TransactionManager.Instance.Clear(); // Clear undo stack
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

    private void ExportDxfButton_Click(object sender, RoutedEventArgs e)
    {
        var shapes = CanvasRenderer.Instance.GetShapes();
        if (shapes.Count == 0)
        {
            MessageBox.Show(
                "No shapes to export.\n\nPlease run code that creates shapes before exporting to DXF.",
                "No Shapes",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "AutoCAD DXF (*.dxf)|*.dxf",
            DefaultExt = ".dxf",
            FileName = "shapes_export"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var exporter = new DxfExporter();
                exporter.Export(shapes, dialog.FileName);
                SetStatus($"Exported: {Path.GetFileName(dialog.FileName)}", isError: false);
            }
            catch (Exception ex)
            {
                SetStatus($"DXF export error: {ex.Message}", isError: true);
            }
        }
    }

    private void ExportPdfButton_Click(object sender, RoutedEventArgs e)
    {
        var shapes = CanvasRenderer.Instance.GetShapes();
        if (shapes.Count == 0)
        {
            MessageBox.Show(
                "No shapes to export.\n\nPlease run code that creates shapes before exporting to PDF.",
                "No Shapes",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "PDF Document (*.pdf)|*.pdf",
            DefaultExt = ".pdf",
            FileName = "shapes_export"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var exporter = new PdfExporter();
                exporter.Export(shapes, dialog.FileName);
                SetStatus($"Exported: {Path.GetFileName(dialog.FileName)}", isError: false);
            }
            catch (Exception ex)
            {
                SetStatus($"PDF export error: {ex.Message}", isError: true);
            }
        }
    }

    private void ExportSvgButton_Click(object sender, RoutedEventArgs e)
    {
        var shapes = CanvasRenderer.Instance.GetShapes();
        if (shapes.Count == 0)
        {
            MessageBox.Show(
                "No shapes to export.\n\nPlease run code that creates shapes before exporting to SVG.",
                "No Shapes",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "SVG Image (*.svg)|*.svg",
            DefaultExt = ".svg",
            FileName = "shapes_export"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                Canvas.SvgExporter.SaveToFile(dialog.FileName, shapes);
                SetStatus($"Exported: {Path.GetFileName(dialog.FileName)}", isError: false);
            }
            catch (Exception ex)
            {
                SetStatus($"SVG export error: {ex.Message}", isError: true);
            }
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

    private void ExportVideoButton_Click(object sender, RoutedEventArgs e)
    {
        var timeline = CanvasRenderer.Instance.ActiveTimeline;
        if (timeline == null)
        {
            MessageBox.Show(
                "No active animation timeline found.\n\nPlease run code that creates and plays a Timeline before exporting a video.",
                "No Animation",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var optionsDialog = new VideoExportOptionsWindow();
        optionsDialog.Owner = this;
        optionsDialog.SetDuration(timeline.Duration);
        optionsDialog.SetCanvasSize((int)RenderCanvas.ActualWidth, (int)RenderCanvas.ActualHeight);

        if (optionsDialog.ShowDialog() != true) return;

        var dialog = new SaveFileDialog
        {
            Filter = "MP4 Video (*.mp4)|*.mp4",
            DefaultExt = ".mp4",
            FileName = "animation_export"
        };

        if (dialog.ShowDialog() == true)
        {
            var progressDialog = new ProgressDialog("Exporting MP4 video...");
            progressDialog.Owner = this;
            progressDialog.Show();

            var originalCursor = Cursor;
            Cursor = System.Windows.Input.Cursors.Wait;

            try
            {
                ExportCanvasToVideo(dialog.FileName, timeline, optionsDialog.Duration, optionsDialog.Fps,
                    optionsDialog.Bitrate, optionsDialog.OutputWidth, optionsDialog.OutputHeight,
                    optionsDialog.SelectedBackground, optionsDialog.IncludeGrid, progressDialog);
                SetStatus($"Exported: {Path.GetFileName(dialog.FileName)}", isError: false);
            }
            catch (Exception ex)
            {
                SetStatus($"Export error: {ex.Message}", isError: true);
                MessageBox.Show($"Failed to export video:\n\n{ex.Message}", "Export Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                progressDialog.Close();
                Cursor = originalCursor;
            }
        }
    }

    private void ExportCanvasToVideo(string filePath, Timeline timeline, double duration, int fps,
        uint bitrateMbps, int outputWidth, int outputHeight, Brush? overrideBackground, bool includeGrid,
        ProgressDialog? progressDialog = null)
    {
        bool wasGridShown = RenderCanvas.ShowGrid;
        var originalBackground = RenderCanvas.CanvasBackground;
        bool wasPlaying = timeline.IsPlaying;

        try
        {
            RenderCanvas.ShowGrid = includeGrid;
            if (overrideBackground != null)
            {
                RenderCanvas.CanvasBackground = overrideBackground;
            }

            var canvasWidth = (int)RenderCanvas.ActualWidth;
            var canvasHeight = (int)RenderCanvas.ActualHeight;

            if (canvasWidth <= 0 || canvasHeight <= 0)
                throw new InvalidOperationException($"Invalid Canvas Dimensions: {canvasWidth}x{canvasHeight}");

            // Ensure output dimensions are even (required for H.264)
            int width = outputWidth - (outputWidth % 2);
            int height = outputHeight - (outputHeight % 2);

            int totalFrames = (int)(duration * fps);
            double timeStep = duration / totalFrames;

            // Check if we need to scale
            bool needsScaling = (width != canvasWidth || height != canvasHeight);

            using var encoder = new Export.VideoExporter(filePath, width, height, fps, bitrateMbps);

            for (int i = 0; i < totalFrames; i++)
            {
                progressDialog?.SetProgress(i + 1, totalFrames);

                double time = i * timeStep;
                timeline.Update(time);

                RenderCanvas.Refresh();
                Dispatcher.Invoke(() => { }, DispatcherPriority.Render);

                RenderTargetBitmap rtb;

                if (needsScaling)
                {
                    // Render canvas at its native size first
                    var canvasRtb = new RenderTargetBitmap(canvasWidth, canvasHeight, 96, 96, PixelFormats.Pbgra32);
                    canvasRtb.Render(RenderCanvas);

                    // Scale to output resolution using DrawingVisual
                    var drawingVisual = new DrawingVisual();
                    using (var dc = drawingVisual.RenderOpen())
                    {
                        dc.DrawImage(canvasRtb, new Rect(0, 0, width, height));
                    }

                    rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
                    rtb.Render(drawingVisual);
                }
                else
                {
                    rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
                    rtb.Render(RenderCanvas);
                }

                encoder.AddFrame(rtb);
            }
        }
        finally
        {
            RenderCanvas.CanvasBackground = originalBackground;
            RenderCanvas.ShowGrid = wasGridShown;

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

            // Save to application settings
            ApplicationSettings.Instance.ShowGrid = GridMenuItem.IsChecked;
            ApplicationSettings.Save();
        }
    }

    private void InlayHintsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (_inlayHintGenerator != null)
        {
            _inlayHintGenerator.Enabled = InlayHintsMenuItem.IsChecked;

            // Update hints immediately if enabling
            if (_inlayHintGenerator.Enabled)
            {
                _inlayHintGenerator.UpdateHints(CodeEditor.Text);
            }

            CodeEditor.TextArea.TextView.Redraw();
        }
    }

    private void SemanticHighlightingMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (_semanticHighlighter != null)
        {
            _semanticHighlighter.Enabled = SemanticHighlightingMenuItem.IsChecked;

            // Update highlighting immediately if enabling
            if (_semanticHighlighter.Enabled)
            {
                _ = UpdateSemanticHighlightingAsync();
            }
            else
            {
                _semanticHighlighter.Clear();
            }

            CodeEditor.TextArea.TextView.Redraw();
        }
    }

    private async Task UpdateSemanticHighlightingAsync()
    {
        if (_semanticHighlighter == null || !_semanticHighlighter.Enabled) return;

        try
        {
            var code = CodeEditor.Text;
            var references = _compiler.GetReferences();

            await _semanticHighlighter.UpdateTokensAsync(code, references);

            // Redraw on UI thread
            await Dispatcher.InvokeAsync(() =>
            {
                CodeEditor.TextArea.TextView.Redraw();
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Semantic highlighting error: {ex.Message}");
        }
    }

    private void TriggerSemanticHighlightingUpdate()
    {
        if (_semanticHighlighter == null || !_semanticHighlighter.Enabled) return;

        // Restart the debounce timer
        _semanticUpdateTimer?.Stop();
        _semanticUpdateTimer?.Start();
    }

    private void UpdateCodeLens()
    {
        if (_codeLensGenerator == null || !_codeLensGenerator.Enabled) return;

        try
        {
            _codeLensGenerator.UpdateCodeLens(CodeEditor.Text);
            CodeEditor.TextArea.TextView.Redraw();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Code lens error: {ex.Message}");
        }
    }

    private void CodeLensMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (_codeLensGenerator != null)
        {
            _codeLensGenerator.Enabled = CodeLensMenuItem.IsChecked;

            // Update code lens immediately if enabling
            if (_codeLensGenerator.Enabled)
            {
                UpdateCodeLens();
            }

            CodeEditor.TextArea.TextView.Redraw();
        }
    }

    #region Windows Menu - Visibility Controls

    private void ShowProjectBrowserMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var isVisible = ShowProjectBrowserMenuItem.IsChecked;
        SetProjectBrowserVisibility(isVisible);

        ApplicationSettings.Instance.ShowProjectBrowser = isVisible;
        ApplicationSettings.Save();
    }

    private void SetProjectBrowserVisibility(bool isVisible)
    {
        // Update Project Browser row visibility within the right panel
        if (isVisible)
        {
            // Show Project Browser rows
            // The grid rows for explorer header and tree will be visible by default
        }

        // Update right panel visibility based on whether Project Browser OR Outliner is visible
        UpdateRightPanelVisibility();
    }

    private void UpdateRightPanelVisibility()
    {
        // Right panel is visible if either Project Browser or Outliner is checked
        var showRightPanel = ShowProjectBrowserMenuItem.IsChecked || ShowOutlinerMenuItem.IsChecked;

        ProjectBrowserPanel.Visibility = showRightPanel ? Visibility.Visible : Visibility.Collapsed;
        RightPanelSplitter.Visibility = showRightPanel ? Visibility.Visible : Visibility.Collapsed;

        if (showRightPanel)
        {
            RightPanelColumn.Width = new GridLength(250);
            RightPanelColumn.MinWidth = 150;
        }
        else
        {
            RightPanelColumn.Width = new GridLength(0);
            RightPanelColumn.MinWidth = 0;
        }

        // Update internal row heights based on which panels are visible
        var showProjectBrowser = ShowProjectBrowserMenuItem.IsChecked;
        var showOutliner = ShowOutlinerMenuItem.IsChecked;

        if (showProjectBrowser && showOutliner)
        {
            // Both visible - split space
            OutlinerSplitterRow.Height = GridLength.Auto;
            OutlinerSplitter.Visibility = Visibility.Visible;
            OutlinerHeaderRow.Height = GridLength.Auto;
            OutlinerHeader.Visibility = Visibility.Visible;
            OutlinerTreeRow.Height = new GridLength(1, GridUnitType.Star);
            OutlinerTreeView.Visibility = Visibility.Visible;
        }
        else if (showProjectBrowser)
        {
            // Only Project Browser - hide outliner rows
            OutlinerSplitterRow.Height = new GridLength(0);
            OutlinerSplitter.Visibility = Visibility.Collapsed;
            OutlinerHeaderRow.Height = new GridLength(0);
            OutlinerHeader.Visibility = Visibility.Collapsed;
            OutlinerTreeRow.Height = new GridLength(0);
            OutlinerTreeView.Visibility = Visibility.Collapsed;
        }
        else if (showOutliner)
        {
            // Only Outliner - hide project browser, show outliner in full space
            OutlinerSplitterRow.Height = new GridLength(0);
            OutlinerSplitter.Visibility = Visibility.Collapsed;
            OutlinerHeaderRow.Height = GridLength.Auto;
            OutlinerHeader.Visibility = Visibility.Visible;
            OutlinerTreeRow.Height = new GridLength(1, GridUnitType.Star);
            OutlinerTreeView.Visibility = Visibility.Visible;
        }
    }

    private void ShowOutlinerMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var isVisible = ShowOutlinerMenuItem.IsChecked;
        SetOutlinerVisibility(isVisible);

        ApplicationSettings.Instance.ShowOutliner = isVisible;
        ApplicationSettings.Save();
    }

    private void SetOutlinerVisibility(bool isVisible)
    {
        // Update right panel visibility based on whether Project Browser OR Outliner is visible
        UpdateRightPanelVisibility();
    }

    private void ShowTimelineMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var isVisible = ShowTimelineMenuItem.IsChecked;
        SetTimelineVisibility(isVisible);

        ApplicationSettings.Instance.ShowTimeline = isVisible;
        ApplicationSettings.Save();
    }

    private void SetTimelineVisibility(bool isVisible)
    {
        TimelinePanel.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
        TimelineSplitter.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
        TimelineRow.Height = isVisible ? new GridLength(150) : new GridLength(0);
    }

    private void ShowToolbarMenuItem_Click(object sender, RoutedEventArgs e)
    {
        // Toolbar menu item has been removed
    }

    private void ShowConsoleMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var isVisible = ShowConsoleMenuItem.IsChecked;
        if (isVisible)
        {
            ShowConsoleTab();
        }
        else
        {
            ConsoleTab.Visibility = Visibility.Collapsed;
            if (FindResultsTab.Visibility != Visibility.Visible)
            {
                SetConsoleVisibility(false);
            }
        }
    }

    private void SetConsoleVisibility(bool isVisible)
    {
        ConsolePanel.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
        ConsoleSplitter.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
        ConsoleRow.Height = isVisible ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        ConsoleRow.MinHeight = isVisible ? 80 : 0;
        ShowConsoleMenuItem.IsChecked = isVisible || ConsoleTab.Visibility == Visibility.Visible;
    }

    private void ShowConsoleTab()
    {
        ConsoleTab.Visibility = Visibility.Visible;
        BottomTabControl.SelectedItem = ConsoleTab;
        SetConsoleVisibility(true);
        ShowConsoleMenuItem.IsChecked = true;
    }

    private void ShowFindResultsTab()
    {
        FindResultsTab.Visibility = Visibility.Visible;
        BottomTabControl.SelectedItem = FindResultsTab;
        SetConsoleVisibility(true);
    }

    private void CloseConsoleTab_Click(object sender, RoutedEventArgs e)
    {
        ConsoleTab.Visibility = Visibility.Collapsed;
        ShowConsoleMenuItem.IsChecked = false;

        // If Find Results tab is visible, switch to it
        if (FindResultsTab.Visibility == Visibility.Visible)
        {
            BottomTabControl.SelectedItem = FindResultsTab;
        }
        else
        {
            // Both tabs hidden, hide the entire panel
            SetConsoleVisibility(false);
        }
    }

    private void CloseFindResultsTab_Click(object sender, RoutedEventArgs e)
    {
        FindResultsTab.Visibility = Visibility.Collapsed;

        // If Console tab is visible, switch to it
        if (ConsoleTab.Visibility == Visibility.Visible)
        {
            BottomTabControl.SelectedItem = ConsoleTab;
        }
        else
        {
            // Both tabs hidden, hide the entire panel
            SetConsoleVisibility(false);
        }
    }

    private void ShowCanvasMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var isVisible = ShowCanvasMenuItem.IsChecked;
        SetCanvasVisibility(isVisible);

        ApplicationSettings.Instance.ShowCanvas = isVisible;
        ApplicationSettings.Save();
    }

    private void SetCanvasVisibility(bool isVisible)
    {
        RenderCanvas.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
        ConsoleSplitter.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;

        if (isVisible)
        {
            CanvasRow.Height = new GridLength(3, GridUnitType.Star);
            CanvasRow.MinHeight = 200;
            ConsoleRow.Height = new GridLength(1, GridUnitType.Star);
        }
        else
        {
            CanvasRow.Height = new GridLength(0);
            CanvasRow.MinHeight = 0;
            // Let Console take up the space
            ConsoleRow.Height = new GridLength(1, GridUnitType.Star);
        }

        // Disable Run and Draw when canvas is not visible
        RunButton.IsEnabled = isVisible;
        RunMenuItem.IsEnabled = isVisible;
        DrawMenu.IsEnabled = isVisible;
    }

    private void ApplyWindowVisibilitySettings()
    {
        // Apply saved settings on startup
        var settings = ApplicationSettings.Instance;

        ShowProjectBrowserMenuItem.IsChecked = settings.ShowProjectBrowser;
        SetProjectBrowserVisibility(settings.ShowProjectBrowser);

        ShowOutlinerMenuItem.IsChecked = settings.ShowOutliner;
        SetOutlinerVisibility(settings.ShowOutliner);

        ShowTimelineMenuItem.IsChecked = settings.ShowTimeline;
        SetTimelineVisibility(settings.ShowTimeline);

        // Toolbar has been removed

        // Console visibility
        ShowConsoleMenuItem.IsChecked = settings.ShowConsole;
        if (settings.ShowConsole)
        {
            ShowConsoleTab();
        }
        else
        {
            SetConsoleVisibility(false);
        }

        ShowCanvasMenuItem.IsChecked = settings.ShowCanvas;
        SetCanvasVisibility(settings.ShowCanvas);
    }

    #endregion

    private void ZoomToShapeMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ZoomToShapeDialog { Owner = this };
        if (dialog.ShowDialog() == true && dialog.ShapeId.HasValue)
        {
            if (RenderCanvas.ZoomToShape(dialog.ShapeId.Value))
            {
                SetStatus($"Zoomed to shape ID: {dialog.ShapeId.Value}", isError: false);
            }
            else
            {
                SetStatus($"Shape with ID {dialog.ShapeId.Value} not found", isError: true);
            }
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

    private void AddCursorAboveMenuItem_Click(object sender, RoutedEventArgs e)
    {
        CodeEditor.Focus();
        AddCursorAbove();
    }

    private void AddCursorBelowMenuItem_Click(object sender, RoutedEventArgs e)
    {
        CodeEditor.Focus();
        AddCursorBelow();
    }

    private void ToggleCommentMenuItem_Click(object sender, RoutedEventArgs e)
    {
        ToggleComment();
    }

    private void RefactorMenuItem_Click(object sender, RoutedEventArgs e)
    {
        // Trigger the same logic as the keyboard shortcut
        Rename_Executed(sender, null);
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

    private void CodeEditor_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space && Keyboard.Modifiers == ModifierKeys.Control)
        {
            TriggerManualCompletion();
            e.Handled = true;
        }
    }

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // HIGHEST PRIORITY: Drawing input when mouse is over canvas and waiting for next point
        // This intercepts digit keys to start distance input mode for precise drawing
        if (RenderCanvas.IsMouseOver &&
            RenderCanvas.DrawingTool.Mode != Canvas.DrawingMode.None &&
            RenderCanvas.DrawingTool.Points.Count > 0)
        {
            var isInInputMode = RenderCanvas.DrawingTool.InputMode != Canvas.DrawingInputMode.None;

            // Tab cycles input modes (None -> Distance -> Angle -> None)
            if (e.Key == Key.Tab)
            {
                e.Handled = true;
                if (RenderCanvas.DrawingTool.CycleInputMode())
                {
                    RenderCanvas.Refresh();
                    UpdateDrawingInputStatus();
                }
                return;
            }

            // Escape cancels input mode
            if (e.Key == Key.Escape && isInInputMode)
            {
                RenderCanvas.DrawingTool.HandleEscapeInput();
                RenderCanvas.Refresh();
                UpdateDrawingInputStatus();
                e.Handled = true;
                return;
            }

            // Backspace removes last character
            if (e.Key == Key.Back && isInInputMode)
            {
                if (RenderCanvas.DrawingTool.HandleBackspace())
                {
                    RenderCanvas.Refresh();
                    UpdateDrawingInputStatus();
                    e.Handled = true;
                }
                return;
            }

            // Number keys - start distance input when drawing
            char? inputChar = null;
            if (e.Key >= Key.D0 && e.Key <= Key.D9)
                inputChar = (char)('0' + (e.Key - Key.D0));
            else if (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9)
                inputChar = (char)('0' + (e.Key - Key.NumPad0));
            else if (e.Key == Key.OemPeriod || e.Key == Key.Decimal)
                inputChar = '.';
            else if (e.Key == Key.OemMinus || e.Key == Key.Subtract)
                inputChar = '-';

            if (inputChar.HasValue)
            {
                // Start Distance mode if not already in input mode
                if (!isInInputMode)
                {
                    RenderCanvas.DrawingTool.StartDistanceInput();
                }

                if (RenderCanvas.DrawingTool.HandleCharInput(inputChar.Value))
                {
                    RenderCanvas.Refresh();
                    UpdateDrawingInputStatus();
                    e.Handled = true;
                }
                return;
            }

            // Enter confirms input and places point
            if (e.Key == Key.Enter && isInInputMode)
            {
                if (RenderCanvas.DrawingTool.HandleEnterInput())
                {
                    var effectivePoint = RenderCanvas.DrawingTool.GetEffectiveEndPoint();
                    if (effectivePoint != null)
                    {
                        // Simulate a click at the effective position
                        RenderCanvas.DrawingTool.OnLeftClick(effectivePoint);
                        RenderCanvas.Refresh();
                        UpdateDrawingInputStatus();
                    }
                }
                e.Handled = true;
                return;
            }
        }

        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            switch (e.Key)
            {
                case Key.Z:
                    // Undo - only handle if canvas has focus or shapes are selected
                    if (RenderCanvas.IsFocused || RenderCanvas.IsMouseOver || RenderCanvas.SelectionTool.SelectedShapes.Count > 0)
                    {
                        PerformUndo();
                        e.Handled = true;
                    }
                    break;
                case Key.Y:
                    // Redo - only handle if canvas has focus or shapes are selected
                    if (RenderCanvas.IsFocused || RenderCanvas.IsMouseOver || RenderCanvas.SelectionTool.SelectedShapes.Count > 0)
                    {
                        PerformRedo();
                        e.Handled = true;
                    }
                    break;
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
                case Key.D:
                    AddNextOccurrence();
                    e.Handled = true;
                    break;
                case Key.G:
                    ZoomToShapeMenuItem_Click(sender, e);
                    e.Handled = true;
                    break;
                case Key.M:
                    ToggleMeasuringTool();
                    e.Handled = true;
                    break;
                case Key.F:
                    FindMenuItem_Click(sender, e);
                    e.Handled = true;
                    break;
                case Key.H:
                    FindReplaceMenuItem_Click(sender, e);
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
                case Key.L:
                    SelectAllOccurrences();
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
                    CopyLineDown();
                    e.Handled = true;
                    break;
                case Key.Up:
                    CopyLineUp();
                    e.Handled = true;
                    break;
                case Key.Right:
                    ExpandSelection();
                    e.Handled = true;
                    break;
                case Key.Left:
                    ShrinkSelection();
                    e.Handled = true;
                    break;
            }
        }
        else if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Alt))
        {
            // Ctrl+Alt+Up/Down: Add cursor above/below (like VSCode)
            var actualKey = e.Key == Key.System ? e.SystemKey : e.Key;
            switch (actualKey)
            {
                case Key.Up:
                    AddCursorAbove();
                    e.Handled = true;
                    break;
                case Key.Down:
                    AddCursorBelow();
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
        // Handle numeric input for drawing tool distance/angle
        else if (!CodeEditor.IsKeyboardFocusWithin && RenderCanvas.DrawingTool.InputMode != Canvas.DrawingInputMode.None)
        {
            // Number keys
            if (e.Key >= Key.D0 && e.Key <= Key.D9)
            {
                var digit = (char)('0' + (e.Key - Key.D0));
                if (RenderCanvas.DrawingTool.HandleCharInput(digit))
                {
                    RenderCanvas.Refresh();
                    UpdateDrawingInputStatus();
                    e.Handled = true;
                }
            }
            else if (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9)
            {
                var digit = (char)('0' + (e.Key - Key.NumPad0));
                if (RenderCanvas.DrawingTool.HandleCharInput(digit))
                {
                    RenderCanvas.Refresh();
                    UpdateDrawingInputStatus();
                    e.Handled = true;
                }
            }
            // Decimal point
            else if (e.Key == Key.OemPeriod || e.Key == Key.Decimal)
            {
                if (RenderCanvas.DrawingTool.HandleCharInput('.'))
                {
                    RenderCanvas.Refresh();
                    UpdateDrawingInputStatus();
                    e.Handled = true;
                }
            }
            // Minus sign
            else if (e.Key == Key.OemMinus || e.Key == Key.Subtract)
            {
                if (RenderCanvas.DrawingTool.HandleCharInput('-'))
                {
                    RenderCanvas.Refresh();
                    UpdateDrawingInputStatus();
                    e.Handled = true;
                }
            }
            // Backspace
            else if (e.Key == Key.Back)
            {
                if (RenderCanvas.DrawingTool.HandleBackspace())
                {
                    RenderCanvas.Refresh();
                    UpdateDrawingInputStatus();
                    e.Handled = true;
                }
            }
            // Enter to confirm input and place point
            else if (e.Key == Key.Enter)
            {
                if (RenderCanvas.DrawingTool.HandleEnterInput())
                {
                    // Simulate a click at the effective position
                    var effectivePoint = RenderCanvas.DrawingTool.GetEffectiveEndPoint();
                    if (effectivePoint != null)
                    {
                        RenderCanvas.DrawingTool.OnLeftClick(effectivePoint);
                        RenderCanvas.Refresh();
                        UpdateDrawingStatus();
                    }
                    e.Handled = true;
                }
            }
        }
        else if (e.Key == Key.Escape)
        {
            // First check if we need to cancel input mode
            if (RenderCanvas.DrawingTool.InputMode != Canvas.DrawingInputMode.None)
            {
                RenderCanvas.DrawingTool.HandleEscapeInput();
                RenderCanvas.Refresh();
                UpdateDrawingStatus();
                e.Handled = true;
            }
            // Cancel drawing tool if active
            else if (RenderCanvas.DrawingTool.Mode != Canvas.DrawingMode.None)
            {
                CancelDrawingTool();
                EnableSelectionMode();
                e.Handled = true;
            }
            // Cancel measuring tool if active
            else if (RenderCanvas.MeasuringTool.Mode == Canvas.ToolMode.Measuring)
            {
                RenderCanvas.MeasuringTool.CancelMeasuring();
                RenderCanvas.Refresh();
                SetStatus("Measuring cancelled", isError: false);
                e.Handled = true;
            }
            // Clear selection if in selection mode
            else if (RenderCanvas.IsSelectionMode && RenderCanvas.SelectionTool.SelectedShapes.Count > 0)
            {
                RenderCanvas.SelectionTool.ClearSelection();
                RenderCanvas.Refresh();
                SetStatus("Selection cleared", isError: false);
                e.Handled = true;
            }
        }
        // Delete key - delete selected shapes (only when editor is not focused)
        else if (e.Key == Key.Delete && !CodeEditor.IsKeyboardFocusWithin)
        {
            if (RenderCanvas.IsSelectionMode && RenderCanvas.SelectionTool.SelectedShapes.Count > 0)
            {
                DeleteSelectedShapes();
                e.Handled = true;
            }
        }
        // Drawing tool shortcuts (only when editor is not focused)
        else if (!CodeEditor.IsKeyboardFocusWithin && Keyboard.Modifiers == ModifierKeys.None)
        {
            switch (e.Key)
            {
                case Key.P:
                    SetDrawingMode(Canvas.DrawingMode.Point);
                    e.Handled = true;
                    break;
                case Key.L:
                    SetDrawingMode(Canvas.DrawingMode.Line);
                    e.Handled = true;
                    break;
                case Key.C:
                    SetDrawingMode(Canvas.DrawingMode.Circle);
                    e.Handled = true;
                    break;
                case Key.R:
                    SetDrawingMode(Canvas.DrawingMode.Rectangle);
                    e.Handled = true;
                    break;
                case Key.A:
                    // Select all shapes (when not in editor)
                    if (RenderCanvas.IsSelectionMode)
                    {
                        RenderCanvas.SelectionTool.SelectAll(RenderCanvas.GetCurrentShapes());
                        RenderCanvas.Refresh();
                        var count = RenderCanvas.SelectionTool.SelectedShapes.Count;
                        SetStatus($"Selected {count} shape{(count != 1 ? "s" : "")}", isError: false);
                        e.Handled = true;
                    }
                    break;
            }
        }
    }

    private void DeleteSelectedShapes()
    {
        var selectedShapes = RenderCanvas.SelectionTool.SelectedShapes.ToList();
        if (selectedShapes.Count == 0) return;

        var entryFile = _currentProject?.EntryPointFile;
        if (entryFile == null) return;

        // Get current content
        var content = entryFile.Content;

        // Remove shapes from code
        var newContent = Canvas.CodeSyncManager.RemoveShapesCode(content, selectedShapes);

        // Check if any changes were made
        if (newContent != content)
        {
            // Update file content
            entryFile.Content = newContent;
            entryFile.HasUnsavedChanges = true;

            // Update editor if this file is active
            if (_activeFile == entryFile)
            {
                CodeEditor.Text = newContent;
            }

            RefreshFileTabs();
        }

        // Clear selection first
        var count = selectedShapes.Count;
        RenderCanvas.SelectionTool.ClearSelection();

        // Remove shapes from canvas directly (no need to re-run code)
        foreach (var shape in selectedShapes)
        {
            RenderCanvas.RemoveShape(shape);
        }

        SetStatus($"Deleted {count} shape{(count != 1 ? "s" : "")}", isError: false);
    }

    #endregion

    #region Measuring Tool

    private void ToggleMeasuringTool()
    {
        var tool = RenderCanvas.MeasuringTool;
        tool.Toggle();

        if (tool.Mode == Canvas.ToolMode.Measuring)
        {
            SetStatus("Measuring: Click first point", isError: false);
            tool.MeasurementCompleted += OnMeasurementCompleted;
            tool.ModeChanged += OnMeasuringModeChanged;
            tool.RefreshSnapSettings();
        }
        else
        {
            tool.MeasurementCompleted -= OnMeasurementCompleted;
            tool.ModeChanged -= OnMeasuringModeChanged;
            SetStatus("Ready", isError: false);
        }

        RenderCanvas.Refresh();
    }

    private void OnMeasurementCompleted(object? sender, double distance)
    {
        SetStatus($"Distance: {distance:F2}", isError: false);
    }

    private void OnMeasuringModeChanged(object? sender, Canvas.ToolMode mode)
    {
        if (mode == Canvas.ToolMode.Measuring)
        {
            if (RenderCanvas.MeasuringTool.FirstPoint == null)
            {
                SetStatus("Measuring: Click first point", isError: false);
            }
        }
        else
        {
            SetStatus("Ready", isError: false);
        }
    }

    #endregion

    #region Drawing Tools

    private void SelectTool_Click(object sender, RoutedEventArgs e)
    {
        CancelDrawingTool();
        EnableSelectionMode();
    }

    private void EnableSelectionMode()
    {
        RenderCanvas.IsSelectionMode = true;
        RenderCanvas.Cursor = Cursors.Arrow;
        RenderCanvas.SelectionTool.RefreshSnapSettings();
        SetStatus("Selection mode: Click to select, Shift+Click to add, Ctrl+Click to toggle", isError: false);
        RenderCanvas.Refresh();
    }

    private void OnSelectionChanged(object? sender, EventArgs e)
    {
        var selectedShapes = RenderCanvas.SelectionTool.SelectedShapes;

        // Update status bar
        var count = selectedShapes.Count;
        if (count == 0)
        {
            SetStatus("Selection mode: Click to select, Shift+Click to add, Ctrl+Click to toggle", isError: false);
        }
        else if (count == 1)
        {
            var shape = selectedShapes[0];
            var nameInfo = !string.IsNullOrEmpty(shape.Name) ? $" \"{shape.Name}\"" : "";
            SetStatus($"Selected: {shape.GetType().Name}{nameInfo} (ID: {shape.Id})", isError: false);
        }
        else
        {
            SetStatus($"Selected {count} shapes", isError: false);
        }
    }

    private void OnControlPointDragEnded(object? sender, Canvas.ControlPointDragEndedEventArgs e)
    {
        // Update the code for the dragged shape
        var shape = e.Shape;
        if (shape == null || _currentProject == null) return;

        var entryFile = _currentProject.EntryPointFile;
        if (entryFile == null) return;

        var content = entryFile.Content;
        var language = _currentProject.ProjectFile.Language;

        // Try to update the shape's constructor in code
        var (newContent, found) = Canvas.CodeSyncManager.UpdateShapeCode(content, shape, language);

        if (found && newContent != content)
        {
            // Update the file content
            entryFile.Content = newContent;

            // Update the editor if this file is currently displayed
            if (_activeFile == entryFile)
            {
                // Save cursor position
                var caretOffset = CodeEditor.CaretOffset;

                CodeEditor.Text = newContent;

                // Restore cursor position (clamped to valid range)
                CodeEditor.CaretOffset = Math.Min(caretOffset, newContent.Length);
            }

            // Mark as modified
            RefreshFileTabs();
            SetStatus($"Updated {shape.GetType().Name} in code", isError: false);
        }
    }

    private void DrawPoint_Click(object sender, RoutedEventArgs e)
    {
        SetDrawingMode(Canvas.DrawingMode.Point);
    }

    private void DrawLine_Click(object sender, RoutedEventArgs e)
    {
        SetDrawingMode(Canvas.DrawingMode.Line);
    }

    private void DrawCircle_CenterRadius_Click(object sender, RoutedEventArgs e)
    {
        SetDrawingMode(Canvas.DrawingMode.Circle);
    }

    private void DrawCircle_CenterDiameter_Click(object sender, RoutedEventArgs e)
    {
        SetDrawingMode(Canvas.DrawingMode.CircleDiameter);
    }

    private void DrawCircle_TwoPoints_Click(object sender, RoutedEventArgs e)
    {
        SetDrawingMode(Canvas.DrawingMode.CircleTwoPoints);
    }

    private void DrawCircle_ThreePoints_Click(object sender, RoutedEventArgs e)
    {
        SetDrawingMode(Canvas.DrawingMode.CircleThreePoints);
    }

    private void DrawCircle_TanTanRadius_Click(object sender, RoutedEventArgs e)
    {
        SetStatus("Circle (Tan, Tan, Radius) - Not yet implemented", true);
    }

    private void DrawCircle_TanTanTan_Click(object sender, RoutedEventArgs e)
    {
        SetStatus("Circle (Tan, Tan, Tan) - Not yet implemented", true);
    }

    private void DrawRect_Click(object sender, RoutedEventArgs e)
    {
        SetDrawingMode(Canvas.DrawingMode.Rectangle);
    }

    private void DrawEllipse_Click(object sender, RoutedEventArgs e)
    {
        SetDrawingMode(Canvas.DrawingMode.Ellipse);
    }

    private void DrawArc_ThreePoints_Click(object sender, RoutedEventArgs e)
    {
        SetDrawingMode(Canvas.DrawingMode.Arc);
    }

    private void DrawArc_StartCenterEnd_Click(object sender, RoutedEventArgs e)
    {
        SetDrawingMode(Canvas.DrawingMode.Arc);
        SetStatus("Arc (Start, Center, End) - Click start, center, then end point", false);
    }

    private void DrawArc_StartCenterAngle_Click(object sender, RoutedEventArgs e)
    {
        SetDrawingMode(Canvas.DrawingMode.Arc);
        SetStatus("Arc (Start, Center, Angle) - Click start, center, then sweep angle", false);
    }

    private void DrawArc_StartCenterLength_Click(object sender, RoutedEventArgs e)
    {
        SetDrawingMode(Canvas.DrawingMode.Arc);
        SetStatus("Arc (Start, Center, Length) - Click start, center, then arc length", false);
    }

    private void DrawArc_StartEndAngle_Click(object sender, RoutedEventArgs e)
    {
        SetDrawingMode(Canvas.DrawingMode.Arc);
        SetStatus("Arc (Start, End, Angle) - Click start, end, then sweep angle", false);
    }

    private void DrawArc_StartEndRadius_Click(object sender, RoutedEventArgs e)
    {
        SetDrawingMode(Canvas.DrawingMode.Arc);
        SetStatus("Arc (Start, End, Radius) - Click start, end, then radius point", false);
    }

    private void DrawArc_CenterStartEnd_Click(object sender, RoutedEventArgs e)
    {
        SetDrawingMode(Canvas.DrawingMode.Arc);
        SetStatus("Arc (Center, Start, End) - Click center, start, then end point", false);
    }

    private void DrawArc_CenterStartAngle_Click(object sender, RoutedEventArgs e)
    {
        SetDrawingMode(Canvas.DrawingMode.Arc);
        SetStatus("Arc (Center, Start, Angle) - Click center, start, then sweep angle", false);
    }

    private void DrawArc_CenterStartLength_Click(object sender, RoutedEventArgs e)
    {
        SetDrawingMode(Canvas.DrawingMode.Arc);
        SetStatus("Arc (Center, Start, Length) - Click center, start, then arc length", false);
    }

    private void DrawArc_Continue_Click(object sender, RoutedEventArgs e)
    {
        SetStatus("Arc (Continue) - Not yet implemented", true);
    }

    private void DrawPolygon_Click(object sender, RoutedEventArgs e)
    {
        SetDrawingMode(Canvas.DrawingMode.Polygon);
    }

    private void DrawPolyline_Click(object sender, RoutedEventArgs e)
    {
        SetDrawingMode(Canvas.DrawingMode.Polyline);
    }

    private void DrawBezier_Click(object sender, RoutedEventArgs e)
    {
        SetDrawingMode(Canvas.DrawingMode.Bezier);
    }

    private void DrawSpline_Click(object sender, RoutedEventArgs e)
    {
        SetDrawingMode(Canvas.DrawingMode.Spline);
    }

    private void DrawArrow_Click(object sender, RoutedEventArgs e)
    {
        SetDrawingMode(Canvas.DrawingMode.Arrow);
    }

    private void DrawText_Click(object sender, RoutedEventArgs e)
    {
        SetDrawingMode(Canvas.DrawingMode.Text);
    }

    private void SetDrawingMode(Canvas.DrawingMode mode)
    {
        // Cancel measuring tool if active
        if (RenderCanvas.MeasuringTool.Mode == Canvas.ToolMode.Measuring)
        {
            RenderCanvas.MeasuringTool.CancelMeasuring();
        }

        // Disable selection mode when drawing
        RenderCanvas.IsSelectionMode = false;
        RenderCanvas.SelectionTool.ClearSelection();

        var tool = RenderCanvas.DrawingTool;
        tool.SetMode(mode);

        // Update UI
        UpdateDrawingToolbarButtons();
        SetStatus(tool.StatusMessage, isError: false);

        // Set crosshair cursor on canvas
        RenderCanvas.Cursor = Cursors.Cross;

        // Subscribe to events
        tool.ShapeCompleted -= OnShapeCompleted;
        tool.ModeChanged -= OnDrawingModeChanged;
        tool.TextPlacementRequested -= OnTextPlacementRequested;
        tool.ShapeCompleted += OnShapeCompleted;
        tool.ModeChanged += OnDrawingModeChanged;
        tool.TextPlacementRequested += OnTextPlacementRequested;
        tool.RefreshSnapSettings();

        RenderCanvas.Refresh();
    }

    private void CancelDrawingTool()
    {
        var tool = RenderCanvas.DrawingTool;
        tool.Cancel();
        UpdateDrawingToolbarButtons();

        // Reset cursor to normal
        RenderCanvas.Cursor = Cursors.Arrow;

        RenderCanvas.Refresh();
    }

    private void UpdateDrawingStatus()
    {
        var tool = RenderCanvas.DrawingTool;
        SetStatus(tool.StatusMessage, isError: false);
    }

    private void UpdateDrawingInputStatus()
    {
        var tool = RenderCanvas.DrawingTool;
        if (tool.InputMode != Canvas.DrawingInputMode.None)
        {
            var inputText = tool.GetInputDisplayText();
            var hint = "(Tab: cycle, Enter: confirm, Esc: cancel)";
            SetStatus($"{inputText}  {hint}", isError: false);
        }
        else
        {
            SetStatus(tool.StatusMessage, isError: false);
        }
    }

    private void OnShapeCompleted(object? sender, Geometry.Shape shape)
    {
        // Generate code based on project language
        var language = _currentProject?.ProjectFile?.Language ?? Project.ProjectLanguage.CSharp;

        // Sync counters from existing code to avoid duplicate variable names
        var existingCode = _currentProject?.EntryPointFile?.Content ?? "";
        Canvas.CodeGenerator.SyncCountersFromCode(existingCode);

        var code = Canvas.CodeGenerator.GenerateCode(shape, language);
        InsertShapeCode(code);

        // Add the shape directly to the canvas (no need to run code)
        RenderCanvas.AddShape(shape);

        // Update status
        var tool = RenderCanvas.DrawingTool;
        SetStatus(tool.StatusMessage, isError: false);
    }

    private void OnTextPlacementRequested(object? sender, Geometry.VPoint location)
    {
        // Show dialog to get text content
        var dialog = new System.Windows.Window
        {
            Title = "Enter Text",
            Width = 350,
            Height = 180,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this,
            ResizeMode = ResizeMode.NoResize,
            Background = (Brush)FindResource("BackgroundBrush"),
            WindowStyle = WindowStyle.ToolWindow
        };

        var panel = new StackPanel { Margin = new Thickness(15) };
        var label = new TextBlock
        {
            Text = "Enter text content:",
            Foreground = (Brush)FindResource("ForegroundBrush"),
            Margin = new Thickness(0, 0, 0, 8)
        };
        var textBox = new System.Windows.Controls.TextBox
        {
            Background = (Brush)FindResource("SecondaryBackgroundBrush"),
            Foreground = (Brush)FindResource("ForegroundBrush"),
            BorderBrush = (Brush)FindResource("BorderBrush"),
            Padding = new Thickness(8, 6, 8, 6),
            FontSize = 14
        };
        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0)
        };
        var okButton = new Button
        {
            Content = "OK",
            Width = 80,
            Margin = new Thickness(0, 0, 8, 0),
            Style = (Style)FindResource("RibbonButtonStyle"),
            IsDefault = true
        };
        var cancelButton = new Button
        {
            Content = "Cancel",
            Width = 80,
            Style = (Style)FindResource("RibbonButtonStyle"),
            IsCancel = true
        };

        okButton.Click += (s, e) => { dialog.DialogResult = true; dialog.Close(); };
        cancelButton.Click += (s, e) => { dialog.DialogResult = false; dialog.Close(); };

        buttonPanel.Children.Add(okButton);
        buttonPanel.Children.Add(cancelButton);
        panel.Children.Add(label);
        panel.Children.Add(textBox);
        panel.Children.Add(buttonPanel);
        dialog.Content = panel;

        dialog.Loaded += (s, e) => textBox.Focus();

        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(textBox.Text))
        {
            // Complete the text shape
            var tool = RenderCanvas.DrawingTool;
            tool.CompleteText(location, textBox.Text);
        }
    }

    private void OnDrawingModeChanged(object? sender, Canvas.DrawingMode mode)
    {
        UpdateDrawingToolbarButtons();
        if (mode == Canvas.DrawingMode.None)
        {
            // Return to selection mode
            EnableSelectionMode();
        }
        else
        {
            var tool = RenderCanvas.DrawingTool;
            SetStatus(tool.StatusMessage, isError: false);
        }
    }

    private void UpdateDrawingToolbarButtons()
    {
        // Toolbar has been removed - this method is now a no-op
    }

    private void InsertShapeCode(string code)
    {
        var entryFile = _currentProject?.EntryPointFile;
        if (entryFile == null) return;

        var content = entryFile.Content;
        var insertPos = FindMainMethodInsertPosition(content);
        if (insertPos < 0) return;

        // Insert the code with proper indentation (8 spaces for F#, 12 for C#)
        var isFSharp = _currentProject?.ProjectFile?.Language == Project.ProjectLanguage.FSharp;
        var indent = isFSharp ? "        " : "            ";
        var indentedCode = indent + code + Environment.NewLine;
        var newContent = content.Insert(insertPos, indentedCode);

        // Update the file content
        entryFile.Content = newContent;
        entryFile.HasUnsavedChanges = true;

        // Update the editor if this is the active file
        if (_activeFile == entryFile)
        {
            var caretPos = CodeEditor.CaretOffset;
            CodeEditor.Text = newContent;
            // Try to restore caret position
            if (caretPos <= insertPos)
            {
                CodeEditor.CaretOffset = caretPos;
            }
            else
            {
                CodeEditor.CaretOffset = caretPos + indentedCode.Length;
            }
        }
    }

    private int FindMainMethodInsertPosition(string content)
    {
        var isFSharp = _currentProject?.ProjectFile?.Language == Project.ProjectLanguage.FSharp;

        if (isFSharp)
        {
            // F# syntax: "let Main() ="
            var mainIndex = content.IndexOf("let Main()");
            if (mainIndex < 0) return -1;

            // Find the '=' after Main()
            var eqIndex = content.IndexOf('=', mainIndex);
            if (eqIndex < 0) return -1;

            // Find the end of the file or next top-level declaration
            // In F#, we insert at the end of the Main function (before any following module/type)
            // For simplicity, find the last non-empty line before end of module or file
            var insertPos = content.Length;

            // Look for next module, type, or let at same indentation level
            var lines = content.Substring(eqIndex + 1).Split('\n');
            var currentPos = eqIndex + 1;
            var lastContentLine = currentPos;

            foreach (var line in lines)
            {
                var trimmed = line.TrimStart();
                // Check if this is a new top-level declaration (not indented content)
                if (trimmed.Length > 0 && !char.IsWhiteSpace(line[0]) &&
                    (trimmed.StartsWith("module ") || trimmed.StartsWith("type ") ||
                     trimmed.StartsWith("let ") || trimmed.StartsWith("open ") ||
                     trimmed.StartsWith("//")))
                {
                    break;
                }
                if (trimmed.Length > 0 && !trimmed.StartsWith("//"))
                {
                    lastContentLine = currentPos + line.Length;
                }
                currentPos += line.Length + 1; // +1 for \n
            }

            // Insert after the last content line in Main
            return lastContentLine + 1;
        }
        else
        {
            // C# syntax: "public static void Main()"
            var mainIndex = content.IndexOf("public static void Main");
            if (mainIndex < 0) return -1;

            // Find the opening brace of Main()
            var braceStart = content.IndexOf('{', mainIndex);
            if (braceStart < 0) return -1;

            // Find matching closing brace
            int braceCount = 1;
            int pos = braceStart + 1;
            int lastNewline = pos;

            while (pos < content.Length && braceCount > 0)
            {
                if (content[pos] == '{') braceCount++;
                else if (content[pos] == '}') braceCount--;
                if (content[pos] == '\n') lastNewline = pos + 1;
                pos++;
            }

            // Insert before the closing brace line
            return lastNewline;
        }
    }

    #endregion

    #region Editor Line Operations

    private void DuplicateLine() => CopyLineDown();

    private void CopyLineDown()
    {
        if (!CodeEditor.IsKeyboardFocusWithin) return;

        var document = CodeEditor.Document;
        var textArea = CodeEditor.TextArea;
        var selection = textArea.Selection;

        // Determine the range of lines to duplicate
        int startLine, endLine;
        if (selection.IsEmpty)
        {
            // No selection - duplicate current line
            startLine = endLine = textArea.Caret.Line;
        }
        else
        {
            // Has selection - get all lines in selection
            var selStart = selection.SurroundingSegment.Offset;
            var selEnd = selection.SurroundingSegment.EndOffset;
            startLine = document.GetLineByOffset(selStart).LineNumber;
            endLine = document.GetLineByOffset(selEnd).LineNumber;

            // If selection ends at the very start of a line, don't include that line
            var endLineObj = document.GetLineByNumber(endLine);
            if (selEnd == endLineObj.Offset && endLine > startLine)
            {
                endLine--;
            }
        }

        // Get the text of all lines to duplicate
        var firstLine = document.GetLineByNumber(startLine);
        var lastLine = document.GetLineByNumber(endLine);
        var textToDuplicate = document.GetText(firstLine.Offset, lastLine.EndOffset - firstLine.Offset);

        // Insert the duplicated text after the last line
        var insertOffset = lastLine.EndOffset;
        document.Insert(insertOffset, Environment.NewLine + textToDuplicate);

        // Move caret down by the number of lines duplicated
        var lineCount = endLine - startLine + 1;
        textArea.Caret.Line = textArea.Caret.Line + lineCount;
    }

    private void CopyLineUp()
    {
        if (!CodeEditor.IsKeyboardFocusWithin) return;

        var document = CodeEditor.Document;
        var textArea = CodeEditor.TextArea;
        var selection = textArea.Selection;

        // Determine the range of lines to duplicate
        int startLine, endLine;
        if (selection.IsEmpty)
        {
            startLine = endLine = textArea.Caret.Line;
        }
        else
        {
            var selStart = selection.SurroundingSegment.Offset;
            var selEnd = selection.SurroundingSegment.EndOffset;
            startLine = document.GetLineByOffset(selStart).LineNumber;
            endLine = document.GetLineByOffset(selEnd).LineNumber;

            var endLineObj = document.GetLineByNumber(endLine);
            if (selEnd == endLineObj.Offset && endLine > startLine)
            {
                endLine--;
            }
        }

        // Get the text of all lines to duplicate
        var firstLine = document.GetLineByNumber(startLine);
        var lastLine = document.GetLineByNumber(endLine);
        var textToDuplicate = document.GetText(firstLine.Offset, lastLine.EndOffset - firstLine.Offset);

        // Insert the duplicated text before the first line
        var insertOffset = firstLine.Offset;
        document.Insert(insertOffset, textToDuplicate + Environment.NewLine);

        // Caret stays at same position (which is now in the duplicated text)
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
        var textArea = CodeEditor.TextArea;
        var selection = textArea.Selection;

        // Get the range of lines to move
        int startLine, endLine;
        bool hadSelection = !selection.IsEmpty;
        if (selection.IsEmpty)
        {
            startLine = endLine = textArea.Caret.Line;
        }
        else
        {
            startLine = document.GetLineByOffset(selection.SurroundingSegment.Offset).LineNumber;
            endLine = document.GetLineByOffset(selection.SurroundingSegment.EndOffset).LineNumber;
            // If selection ends at start of a line, don't include that line
            var endLineObj = document.GetLineByNumber(endLine);
            if (selection.SurroundingSegment.EndOffset == endLineObj.Offset && endLine > startLine)
                endLine--;
        }

        if (startLine <= 1) return;

        var firstLine = document.GetLineByNumber(startLine);
        var lastLine = document.GetLineByNumber(endLine);
        var lineAbove = document.GetLineByNumber(startLine - 1);

        var selectedText = document.GetText(firstLine.Offset, lastLine.EndOffset - firstLine.Offset);
        var aboveText = document.GetText(lineAbove.Offset, lineAbove.Length);

        document.BeginUpdate();
        try
        {
            // Remove the selected lines and the line above, then insert in swapped order
            int blockStart = lineAbove.Offset;
            int blockLength = lastLine.EndOffset - lineAbove.Offset;

            // Build the new text: selected lines + newline + line that was above
            string newText = selectedText + Environment.NewLine + aboveText;

            document.Replace(blockStart, blockLength, newText);
        }
        finally
        {
            document.EndUpdate();
        }

        // Always restore selection at new position to allow continuous moving
        var newFirstLine = document.GetLineByNumber(startLine - 1);
        var newLastLine = document.GetLineByNumber(endLine - 1);

        // Select from start of first line to end of last line
        textArea.Caret.Position = new ICSharpCode.AvalonEdit.TextViewPosition(startLine - 1, 1);
        textArea.Selection = ICSharpCode.AvalonEdit.Editing.Selection.Create(textArea, newFirstLine.Offset, newLastLine.EndOffset);
    }

    private void MoveLineDown()
    {
        if (!CodeEditor.IsKeyboardFocusWithin) return;

        var document = CodeEditor.Document;
        var textArea = CodeEditor.TextArea;
        var selection = textArea.Selection;

        // Get the range of lines to move
        int startLine, endLine;
        bool hadSelection = !selection.IsEmpty;
        if (selection.IsEmpty)
        {
            startLine = endLine = textArea.Caret.Line;
        }
        else
        {
            startLine = document.GetLineByOffset(selection.SurroundingSegment.Offset).LineNumber;
            endLine = document.GetLineByOffset(selection.SurroundingSegment.EndOffset).LineNumber;
            // If selection ends at start of a line, don't include that line
            var endLineObj = document.GetLineByNumber(endLine);
            if (selection.SurroundingSegment.EndOffset == endLineObj.Offset && endLine > startLine)
                endLine--;
        }

        if (endLine >= document.LineCount) return;

        var firstLine = document.GetLineByNumber(startLine);
        var lastLine = document.GetLineByNumber(endLine);
        var lineBelow = document.GetLineByNumber(endLine + 1);

        var selectedText = document.GetText(firstLine.Offset, lastLine.EndOffset - firstLine.Offset);
        var belowText = document.GetText(lineBelow.Offset, lineBelow.Length);

        document.BeginUpdate();
        try
        {
            // Remove the selected lines and the line below, then insert in swapped order
            int blockStart = firstLine.Offset;
            int blockLength = lineBelow.EndOffset - firstLine.Offset;

            // Build the new text: line that was below + newline + selected lines
            string newText = belowText + Environment.NewLine + selectedText;

            document.Replace(blockStart, blockLength, newText);
        }
        finally
        {
            document.EndUpdate();
        }

        // Always restore selection at new position to allow continuous moving
        var newFirstLine = document.GetLineByNumber(startLine + 1);
        var newLastLine = document.GetLineByNumber(endLine + 1);

        // Select from start of first line to end of last line
        textArea.Caret.Position = new ICSharpCode.AvalonEdit.TextViewPosition(startLine + 1, 1);
        textArea.Selection = ICSharpCode.AvalonEdit.Editing.Selection.Create(textArea, newFirstLine.Offset, newLastLine.EndOffset);
    }

    private void AddCursorAbove()
    {
        if (!CodeEditor.IsKeyboardFocusWithin) return;
        _multiSelectionRenderer?.AddCursorAbove();
    }

    private void AddCursorBelow()
    {
        if (!CodeEditor.IsKeyboardFocusWithin) return;
        _multiSelectionRenderer?.AddCursorBelow();
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

    #region Selection Operations

    // Stack to track selection expansion history for shrinking
    private readonly Stack<(int Start, int Length)> _selectionHistory = new();

    private void ExpandSelection()
    {
        if (!CodeEditor.IsKeyboardFocusWithin) return;

        var document = CodeEditor.Document;
        var textArea = CodeEditor.TextArea;
        var selection = textArea.Selection;

        int currentStart, currentLength;
        if (selection.IsEmpty)
        {
            currentStart = textArea.Caret.Offset;
            currentLength = 0;
        }
        else
        {
            var segment = selection.SurroundingSegment;
            currentStart = segment.Offset;
            currentLength = segment.Length;
        }

        // Save current selection for shrinking
        _selectionHistory.Push((currentStart, currentLength));

        // Determine what to expand to
        var text = document.Text;
        int newStart = currentStart;
        int newEnd = currentStart + currentLength;

        if (currentLength == 0)
        {
            // No selection - select current word
            (newStart, newEnd) = GetWordBounds(text, currentStart);
        }
        else
        {
            // Try expanding to larger constructs
            var currentText = text.Substring(currentStart, currentLength);

            // If word selected, try to expand to quoted string or parentheses
            var (wordStart, wordEnd) = GetWordBounds(text, currentStart);
            if (newStart == wordStart && newEnd == wordEnd)
            {
                // Try to expand to enclosing brackets/quotes
                var (bracketStart, bracketEnd) = GetEnclosingBrackets(text, currentStart, currentLength);
                if (bracketStart < newStart || bracketEnd > newEnd)
                {
                    newStart = bracketStart;
                    newEnd = bracketEnd;
                }
                else
                {
                    // Expand to line
                    var line = document.GetLineByOffset(currentStart);
                    newStart = line.Offset;
                    newEnd = line.EndOffset;
                }
            }
            else if (IsEntireLine(document, currentStart, currentLength))
            {
                // Expand to include more lines or block
                var (blockStart, blockEnd) = GetEnclosingBlock(text, currentStart, currentLength);
                newStart = blockStart;
                newEnd = blockEnd;
            }
            else
            {
                // Expand to line
                var startLine = document.GetLineByOffset(currentStart);
                var endLine = document.GetLineByOffset(currentStart + currentLength);
                newStart = startLine.Offset;
                newEnd = endLine.EndOffset;
            }
        }

        // Apply new selection
        if (newStart != currentStart || newEnd != currentStart + currentLength)
        {
            textArea.Selection = ICSharpCode.AvalonEdit.Editing.Selection.Create(textArea, newStart, newEnd);
            textArea.Caret.Offset = newEnd;
        }
    }

    private void ShrinkSelection()
    {
        if (!CodeEditor.IsKeyboardFocusWithin) return;
        if (_selectionHistory.Count == 0) return;

        var textArea = CodeEditor.TextArea;
        var (start, length) = _selectionHistory.Pop();

        if (length == 0)
        {
            textArea.ClearSelection();
            textArea.Caret.Offset = start;
        }
        else
        {
            textArea.Selection = ICSharpCode.AvalonEdit.Editing.Selection.Create(textArea, start, start + length);
            textArea.Caret.Offset = start + length;
        }
    }

    private (int Start, int End) GetWordBounds(string text, int offset)
    {
        if (offset >= text.Length) return (offset, offset);

        int start = offset;
        int end = offset;

        // Expand backwards
        while (start > 0 && (char.IsLetterOrDigit(text[start - 1]) || text[start - 1] == '_'))
            start--;

        // Expand forwards
        while (end < text.Length && (char.IsLetterOrDigit(text[end]) || text[end] == '_'))
            end++;

        return (start, end);
    }

    private (int Start, int End) GetEnclosingBrackets(string text, int start, int length)
    {
        var brackets = new Dictionary<char, char>
        {
            { ')', '(' }, { ']', '[' }, { '}', '{' }, { '>', '<' }, { '"', '"' }, { '\'', '\'' }
        };

        int searchStart = start;
        int searchEnd = start + length;

        // Search outward for enclosing brackets
        for (int i = start - 1; i >= 0; i--)
        {
            char c = text[i];
            if (c == '(' || c == '[' || c == '{')
            {
                // Find matching closing bracket
                int depth = 1;
                for (int j = searchEnd; j < text.Length; j++)
                {
                    if (text[j] == c) depth++;
                    else if (text[j] == brackets.FirstOrDefault(x => x.Value == c).Key)
                    {
                        depth--;
                        if (depth == 0)
                        {
                            return (i, j + 1);
                        }
                    }
                }
            }
            else if (c == '"' || c == '\'')
            {
                // Find matching quote
                for (int j = searchEnd; j < text.Length; j++)
                {
                    if (text[j] == c && (j == 0 || text[j - 1] != '\\'))
                    {
                        return (i, j + 1);
                    }
                }
            }
        }

        return (start, start + length);
    }

    private (int Start, int End) GetEnclosingBlock(string text, int start, int length)
    {
        // Find enclosing braces
        int braceDepth = 0;
        int blockStart = start;

        for (int i = start - 1; i >= 0; i--)
        {
            if (text[i] == '}') braceDepth++;
            else if (text[i] == '{')
            {
                if (braceDepth == 0)
                {
                    blockStart = i;
                    break;
                }
                braceDepth--;
            }
        }

        braceDepth = 0;
        int blockEnd = start + length;

        for (int i = start + length; i < text.Length; i++)
        {
            if (text[i] == '{') braceDepth++;
            else if (text[i] == '}')
            {
                if (braceDepth == 0)
                {
                    blockEnd = i + 1;
                    break;
                }
                braceDepth--;
            }
        }

        return (blockStart, blockEnd);
    }

    private bool IsEntireLine(ICSharpCode.AvalonEdit.Document.TextDocument document, int start, int length)
    {
        var startLine = document.GetLineByOffset(start);
        var endLine = document.GetLineByOffset(start + length);
        return start == startLine.Offset && start + length == endLine.EndOffset;
    }

    private string? _lastSearchText;

    private void AddNextOccurrence()
    {
        if (!CodeEditor.IsKeyboardFocusWithin) return;
        if (_multiSelectionRenderer == null) return;

        var document = CodeEditor.Document;
        var textArea = CodeEditor.TextArea;
        var selection = textArea.Selection;

        string searchText;
        int searchFrom;

        if (selection.IsEmpty && !_multiSelectionRenderer.HasSelections)
        {
            // No selection - select current word first
            var (wordStart, wordEnd) = GetWordBounds(document.Text, textArea.Caret.Offset);
            if (wordStart == wordEnd) return;

            searchText = document.GetText(wordStart, wordEnd - wordStart);
            _isAddingNextOccurrence = true;
            textArea.Selection = ICSharpCode.AvalonEdit.Editing.Selection.Create(textArea, wordStart, wordEnd);
            textArea.Caret.Offset = wordEnd;
            _isAddingNextOccurrence = false;
            _lastSearchText = searchText;
            return;
        }

        // Get selected text (from current selection or last search)
        var segment = selection.SurroundingSegment;
        searchText = document.GetText(segment.Offset, segment.Length);
        _lastSearchText = searchText;

        // Search for next occurrence after current selection
        searchFrom = segment.EndOffset;
        var text = document.Text;
        var nextIndex = text.IndexOf(searchText, searchFrom, StringComparison.Ordinal);

        // Wrap around if not found
        if (nextIndex < 0)
        {
            nextIndex = text.IndexOf(searchText, 0, StringComparison.Ordinal);
        }

        // Check if this occurrence is already selected (in main selection or multi-selections)
        if (nextIndex >= 0)
        {
            // Check if already in multi-selections
            bool alreadySelected = false;
            foreach (var sel in _multiSelectionRenderer.Selections)
            {
                if (sel.StartOffset == nextIndex && sel.Length == searchText.Length)
                {
                    alreadySelected = true;
                    break;
                }
            }
            // Also check if it's the current main selection
            if (segment.Offset == nextIndex && segment.Length == searchText.Length)
            {
                alreadySelected = true;
            }

            if (alreadySelected)
            {
                // All occurrences already selected
                return;
            }

            // Add current selection to the multi-selection renderer before moving
            _multiSelectionRenderer.AddSelection(segment.Offset, segment.Length);

            // Move caret selection to the new occurrence
            _isAddingNextOccurrence = true;
            textArea.Selection = ICSharpCode.AvalonEdit.Editing.Selection.Create(textArea, nextIndex, nextIndex + searchText.Length);
            textArea.Caret.Offset = nextIndex + searchText.Length;
            _isAddingNextOccurrence = false;

            // Scroll to make visible
            textArea.Caret.BringCaretToView();
        }
    }

    private void SelectAllOccurrences()
    {
        if (!CodeEditor.IsKeyboardFocusWithin) return;
        if (_multiSelectionRenderer == null) return;

        var document = CodeEditor.Document;
        var textArea = CodeEditor.TextArea;
        var selection = textArea.Selection;

        string searchText;

        if (selection.IsEmpty)
        {
            // No selection - use word at caret
            var (wordStart, wordEnd) = GetWordBounds(document.Text, textArea.Caret.Offset);
            if (wordStart == wordEnd) return;
            searchText = document.GetText(wordStart, wordEnd - wordStart);
        }
        else
        {
            var segment = selection.SurroundingSegment;
            searchText = document.GetText(segment.Offset, segment.Length);
        }

        // Find all occurrences
        var text = document.Text;
        var occurrences = new List<(int Start, int End)>();
        int index = 0;

        while ((index = text.IndexOf(searchText, index, StringComparison.Ordinal)) >= 0)
        {
            occurrences.Add((index, index + searchText.Length));
            index += searchText.Length;
        }

        if (occurrences.Count <= 1) return;

        _isAddingNextOccurrence = true;
        try
        {
            // Clear existing multi-selections
            _multiSelectionRenderer.ClearSelections();

            // Add all occurrences except the last one to multi-selection renderer
            for (int i = 0; i < occurrences.Count - 1; i++)
            {
                var occ = occurrences[i];
                _multiSelectionRenderer.AddSelection(occ.Start, occ.End - occ.Start);
            }

            // Set main selection to the last occurrence
            var last = occurrences[^1];
            textArea.Selection = ICSharpCode.AvalonEdit.Editing.Selection.Create(textArea, last.Start, last.End);
            textArea.Caret.Offset = last.End;
            textArea.Caret.BringCaretToView();
        }
        finally
        {
            _isAddingNextOccurrence = false;
        }

        SetStatus($"Selected {occurrences.Count} occurrences of \"{searchText}\"", false);
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
            // Handle References node - keep double-click for this dialog
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
        }
    }

    private void ProjectTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is ProjectTreeItem item)
        {
            OpenFileFromProjectTree(item);
        }
    }

    private void OpenFileFromProjectTree(ProjectTreeItem item)
    {
        // Ignore reference items
        if (item.IsReferencesNode || item.IsReferenceItem)
        {
            return;
        }

        // Handle regular files (ignore directories for opening)
        if (!item.IsDirectory)
        {
            // Check if file is already loaded
            var existingFile = _currentProject?.Files.FirstOrDefault(f => f.FilePath.Equals(item.FullPath, StringComparison.OrdinalIgnoreCase));

            if (existingFile != null)
            {
                // Reopen the tab if it was closed
                if (!existingFile.IsOpen)
                {
                    existingFile.IsOpen = true;
                    RefreshFileTabs();
                }
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

    private async void TextEditor_MouseHover(object sender, MouseEventArgs e)
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

            // Check if hovering over a method call - show signature
            var methodInfo = GetMethodSignatureAtOffset(offset);
            if (methodInfo != null)
            {
                ShowMethodSignatureTooltip(methodInfo.Value.typeName, methodInfo.Value.methodName, methodInfo.Value.signatures);
                e.Handled = true;
                return;
            }

            // Roslyn-based Quick Info
            // We use a fire-and-forget approach here because MouseHover is synchronous
            // and we don't want to block the UI thread.
            var code = CodeEditor.Text;
            var service = new Editor.RoslynCompletionService(_compiler.GetReferences());
            var quickInfo = await service.GetQuickInfoAsync(code, offset);

            if (quickInfo != null)
            {
                 ShowStyledTypeTooltip(quickInfo.Value.Kind, quickInfo.Value.TypeName, quickInfo.Value.Name, quickInfo.Value.Documentation);
                 e.Handled = true;
                 return;
            }

            // Fallback: No method call - try to show type information for identifier under cursor using partial reflection if Roslyn fails (or during typing)
            // Ideally Roslyn should handle everything.
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

        // Hover logic temporarily disabled during Roslyn refactor
        // Check if it's a method parameter
        // var parameters = Editor.CompletionProvider.FindCurrentMethodParametersPublic(textBeforeCursor);
        // var param = parameters.FirstOrDefault(p => p.Name == identifier);
        // if (param.Name != null)
        // {
        //     return ("parameter", param.Type, identifier);
        // }

        // Check if it's a local variable
        // var locals = Editor.CompletionProvider.FindLocalVariablesPublic(textBeforeCursor);
        // var local = locals.FirstOrDefault(v => v.Name == identifier);
        // if (local.Name != null)
        // {
        //     return ("local", local.Type, identifier);
        // }

        // Try to find variable type using existing infrastructure
        // var varType = Editor.CompletionProvider.FindVariableType(textBeforeCursor, identifier, allCode);
        // if (varType != null)
        // {
        //     return ("variable", varType, identifier);
        // }

        return null;
    }

    /// <summary>
    /// Gets method signature information at the given offset if the identifier is a method call.
    /// </summary>
    private (string typeName, string methodName, List<string> signatures)? GetMethodSignatureAtOffset(int offset)
    {
        return null; // Legacy logic disabled during Roslyn refactor
    }

    private List<string> GetExtensionMethodSignatures(string typeName, string methodName)
    {
        var signatures = new List<string>();

        // Check LINQ extension methods
        var linqMethods = typeof(System.Linq.Enumerable).GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(m => m.Name.Equals(methodName, StringComparison.OrdinalIgnoreCase) &&
                        m.IsDefined(typeof(System.Runtime.CompilerServices.ExtensionAttribute), false))
            .ToList();

        foreach (var method in linqMethods.Take(3)) // Limit to 3 overloads
        {
            var parameters = method.GetParameters().Skip(1); // Skip 'this' parameter
            var paramStr = string.Join(", ", parameters.Select(p => $"{Editor.TypeInspector.GetTypeName(p.ParameterType)} {p.Name}"));
            signatures.Add($"{Editor.TypeInspector.GetTypeName(method.ReturnType)} {method.Name}({paramStr})");
        }

        return signatures;
    }

    private void ShowMethodSignatureTooltip(string typeName, string methodName, List<string> signatures)
    {
        if (_currentToolTip != null)
        {
            _currentToolTip.IsOpen = false;
        }

        _currentToolTip = new ToolTip();
        _currentToolTip.PlacementTarget = CodeEditor;
        _currentToolTip.Background = new SolidColorBrush(Color.FromRgb(30, 30, 30));
        _currentToolTip.BorderBrush = new SolidColorBrush(Color.FromRgb(60, 60, 60));

        var mainPanel = new StackPanel();

        // Show overload count if multiple signatures
        if (signatures.Count > 1)
        {
            mainPanel.Children.Add(new TextBlock
            {
                Text = $"({signatures.Count} overloads)",
                Foreground = new SolidColorBrush(Color.FromRgb(128, 128, 128)),
                FontSize = 11,
                Margin = new Thickness(0, 0, 0, 4)
            });
        }

        // Display each signature
        foreach (var signature in signatures.Take(5)) // Limit display to 5 overloads
        {
            var sigPanel = new WrapPanel();

            // Parse signature: "returnType methodName(params)"
            var parenIndex = signature.IndexOf('(');
            if (parenIndex > 0)
            {
                var beforeParen = signature.Substring(0, parenIndex).Trim();
                var paramsAndClose = signature.Substring(parenIndex);

                // Split return type and method name
                var lastSpace = beforeParen.LastIndexOf(' ');
                if (lastSpace > 0)
                {
                    var returnType = beforeParen.Substring(0, lastSpace).Trim();
                    var mName = beforeParen.Substring(lastSpace + 1).Trim();

                    // Return type in teal
                    sigPanel.Children.Add(new TextBlock
                    {
                        Text = returnType + " ",
                        Foreground = new SolidColorBrush(Color.FromRgb(78, 201, 176)),
                        FontSize = 12
                    });

                    // Method name in yellow
                    sigPanel.Children.Add(new TextBlock
                    {
                        Text = mName,
                        Foreground = new SolidColorBrush(Color.FromRgb(220, 220, 170)),
                        FontSize = 12
                    });
                }
                else
                {
                    // No return type (constructor)
                    sigPanel.Children.Add(new TextBlock
                    {
                        Text = beforeParen,
                        Foreground = new SolidColorBrush(Color.FromRgb(78, 201, 176)),
                        FontSize = 12
                    });
                }

                // Parameters with syntax coloring
                var paramText = paramsAndClose.Trim('(', ')');
                sigPanel.Children.Add(new TextBlock
                {
                    Text = "(",
                    Foreground = Brushes.White,
                    FontSize = 12
                });

                if (!string.IsNullOrWhiteSpace(paramText))
                {
                    var paramParts = SplitParameters(paramText);
                    for (int i = 0; i < paramParts.Count; i++)
                    {
                        var param = paramParts[i].Trim();
                        var paramLastSpace = param.LastIndexOf(' ');
                        if (paramLastSpace > 0)
                        {
                            var paramType = param.Substring(0, paramLastSpace);
                            var paramName = param.Substring(paramLastSpace + 1);

                            // Parameter type in teal
                            sigPanel.Children.Add(new TextBlock
                            {
                                Text = paramType + " ",
                                Foreground = new SolidColorBrush(Color.FromRgb(78, 201, 176)),
                                FontSize = 12
                            });

                            // Parameter name in light blue
                            sigPanel.Children.Add(new TextBlock
                            {
                                Text = paramName,
                                Foreground = new SolidColorBrush(Color.FromRgb(156, 220, 254)),
                                FontSize = 12
                            });
                        }
                        else
                        {
                            sigPanel.Children.Add(new TextBlock
                            {
                                Text = param,
                                Foreground = Brushes.White,
                                FontSize = 12
                            });
                        }

                        if (i < paramParts.Count - 1)
                        {
                            sigPanel.Children.Add(new TextBlock
                            {
                                Text = ", ",
                                Foreground = Brushes.White,
                                FontSize = 12
                            });
                        }
                    }
                }

                sigPanel.Children.Add(new TextBlock
                {
                    Text = ")",
                    Foreground = Brushes.White,
                    FontSize = 12
                });
            }
            else
            {
                // Fallback: display as plain text
                sigPanel.Children.Add(new TextBlock
                {
                    Text = signature,
                    Foreground = Brushes.White,
                    FontSize = 12
                });
            }

            mainPanel.Children.Add(sigPanel);
        }

        if (signatures.Count > 5)
        {
            mainPanel.Children.Add(new TextBlock
            {
                Text = $"... and {signatures.Count - 5} more",
                Foreground = new SolidColorBrush(Color.FromRgb(128, 128, 128)),
                FontSize = 11,
                Margin = new Thickness(0, 4, 0, 0)
            });
        }

        _currentToolTip.Content = mainPanel;
        _currentToolTip.IsOpen = true;
    }

    private List<string> SplitParameters(string paramText)
    {
        var result = new List<string>();
        int depth = 0;
        int start = 0;

        for (int i = 0; i < paramText.Length; i++)
        {
            char c = paramText[i];
            if (c == '<' || c == '(' || c == '[') depth++;
            else if (c == '>' || c == ')' || c == ']') depth--;
            else if (c == ',' && depth == 0)
            {
                result.Add(paramText.Substring(start, i - start));
                start = i + 1;
            }
        }

        if (start < paramText.Length)
            result.Add(paramText.Substring(start));

        return result;
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

    private void ShowStyledTypeTooltip(string category, string typeName, string identifier, string? documentation = null)
    {
        if (_currentToolTip != null)
        {
            _currentToolTip.IsOpen = false;
        }

        _currentToolTip = new ToolTip();
        _currentToolTip.PlacementTarget = CodeEditor;
        _currentToolTip.Background = new SolidColorBrush(Color.FromRgb(30, 30, 30));
        _currentToolTip.BorderBrush = new SolidColorBrush(Color.FromRgb(60, 60, 60));

        var mainPanel = new StackPanel();

        var headerPanel = new StackPanel { Orientation = Orientation.Horizontal };

        // Category in gray: (local), (parameter), (type), etc.
        headerPanel.Children.Add(new TextBlock
        {
            Text = $"({category}) ",
            Foreground = new SolidColorBrush(Color.FromRgb(128, 128, 128)),
            FontSize = 12
        });

        // Type name in teal
        headerPanel.Children.Add(new TextBlock
        {
            Text = typeName,
            Foreground = new SolidColorBrush(Color.FromRgb(78, 201, 176)),
            FontSize = 12
        });

        // Identifier name in light blue (only if different from type)
        if (identifier != typeName && category != "type" && category != "class" && category != "struct")
        {
            headerPanel.Children.Add(new TextBlock
            {
                Text = $" {identifier}",
                Foreground = new SolidColorBrush(Color.FromRgb(156, 220, 254)),
                FontSize = 12
            });
        }

        mainPanel.Children.Add(headerPanel);

        // Try to get documentation
        // string? documentation is now passed in as argument
        
        if (documentation == null)
        {
            // First try built-in documentation
            if (category == "type" || category == "class" || category == "struct")
            {
                documentation = Editor.XmlDocumentationProvider.GetBuiltInDocumentation(identifier);
            }
            else
            {
                // Try to get documentation from the type's member
                documentation = Editor.XmlDocumentationProvider.GetBuiltInDocumentation(typeName, identifier);
            }

            // If no built-in doc, try reflection-based XML docs
            if (documentation == null)
            {
                var resolvedType = Editor.TypeInspector.ResolveType(typeName);
                if (resolvedType != null)
                {
                    if (category == "type" || category == "class" || category == "struct")
                    {
                        documentation = Editor.XmlDocumentationProvider.GetTypeSummary(resolvedType);
                    }
                }
            }
        }

        if (!string.IsNullOrEmpty(documentation))
        {
            mainPanel.Children.Add(new Separator
            {
                Margin = new Thickness(0, 4, 0, 4),
                Background = new SolidColorBrush(Color.FromRgb(60, 60, 60))
            });

            mainPanel.Children.Add(new TextBlock
            {
                Text = documentation,
                Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 350
            });
        }

        _currentToolTip.Content = mainPanel;
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
        // Debug checks
        if (_currentProject == null)
        {
            MessageBox.Show("No project loaded.", "Quick Actions Debug");
            return;
        }
        if (_activeFile == null)
        {
            MessageBox.Show("No active file.", "Quick Actions Debug");
            return;
        }
        if (_refactoringProvider == null)
        {
            MessageBox.Show("Provider not initialized.", "Quick Actions Debug");
            return;
        }
        
        // Sync current content
        _activeFile.Content = CodeEditor.Text;
        var currentContent = CodeEditor.Text;
        var offset = CodeEditor.CaretOffset;
        var selectionLength = CodeEditor.SelectionLength;
        
        // 1. Get Quick Actions from RefactoringProvider (pass current content directly)
        SetStatus("Analyzing...", false);
        var quickActions = await _refactoringProvider.GetQuickActionsAsync(_currentProject, _activeFile.FilePath, currentContent, offset, selectionLength);
        SetStatus("Ready", false);
        
        var contextMenu = new ContextMenu();
        bool hasItems = false;

        // Add Refactoring Items
        foreach (var action in quickActions)
        {
            var item = new MenuItem { Header = action.Title };
            
            // Add shortcut hint if applicable
            if (action.ActionId == "Rename") item.InputGestureText = "Ctrl+R, R";
            
            item.Click += (s, args) => PerformQuickAction(action);
            contextMenu.Items.Add(item);
            hasItems = true;
        }

        // 2. Check for missing namespaces (types and extension methods)
        // Keep existing logic for now as it's robust
        var word = GetWordAtOffset(CodeEditor.Document, offset);
        if (!string.IsNullOrEmpty(word))
        {
            var currentCode = CodeEditor.Text;

            // First check for types
            var namespaces = TypeInspector.FindNamespacesForType(word);

            // Also check for extension methods (like LINQ's Select, Where, etc.)
            var extensionNamespaces = TypeInspector.FindNamespacesForExtensionMethod(word);
            foreach (var ns in extensionNamespaces)
            {
                namespaces.Add(ns);
            }

            // Filter out namespaces that are already in the file
            var newNamespaces = namespaces.Distinct()
                .Where(ns => !currentCode.Contains($"using {ns};"))
                .OrderByDescending(n => n.StartsWith("Code2Viz"))
                .ThenBy(n => n)
                .ToList();

            if (newNamespaces.Count > 0)
            {
                if (hasItems) contextMenu.Items.Add(new Separator());

                foreach (var ns in newNamespaces)
                {
                    var item = new MenuItem { Header = $"using {ns};" };
                    item.Click += (s, args) => AddUsingStatement(ns);
                    contextMenu.Items.Add(item);
                }
                hasItems = true;
            }
        }

        // 4. Show menu or feedback
        if (hasItems)
        {
            // Get visual position below the caret
            var textView = CodeEditor.TextArea.TextView;
            var pos = textView.GetVisualPosition(
                new TextViewPosition(CodeEditor.TextArea.Caret.Line, CodeEditor.TextArea.Caret.Column),
                ICSharpCode.AvalonEdit.Rendering.VisualYPosition.LineBottom);

            // Adjust for scrolling
            pos = new System.Windows.Point(pos.X - textView.ScrollOffset.X, pos.Y - textView.ScrollOffset.Y);

            // Position relative to TextView at caret position
            contextMenu.PlacementTarget = textView;
            contextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.RelativePoint;
            contextMenu.HorizontalOffset = pos.X;
            contextMenu.VerticalOffset = pos.Y;
            contextMenu.IsOpen = true;
            SetStatus("Quick actions available", false);
        }
        else
        {
            SetStatus($"No quick actions. File: {System.IO.Path.GetFileName(_activeFile.FilePath)}, Offset: {offset}", true);
        }
    }

    private async void PerformQuickAction(Code2Viz.Editor.RefactoringProvider.QuickActionItem action)
    {
        if (action.ActionId == "Rename")
        {
             if (action.Data.TryGetValue("Name", out var name))
             {
                 PerformRename(name);
             }
        }
        else if (action.ActionId == "MoveTypeToFile")
        {
            if (action.Data.TryGetValue("TypeName", out var typeName))
            {
                MoveTypeToNewFile(typeName);
            }
        }
        else if (action.ActionId == "ExtractInterface")
        {
             MessageBox.Show("Extract Interface: Coming soon!", "Refactoring", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else if (action.ActionId == "ImplementInterface")
        {
            if (action.Data.TryGetValue("InterfaceName", out var interfaceName) &&
                action.Data.TryGetValue("ClassName", out var className))
            {
                await ImplementInterfaceAsync(className, interfaceName);
            }
        }
        else if (action.ActionId == "FixFormatting")
        {
             try 
             {
                 var newText = Code2Viz.Editor.CodeFormatter.Format(CodeEditor.Text);
                 CodeEditor.Document.Replace(0, CodeEditor.Document.TextLength, newText);
             }
             catch (Exception ex)
             {
                 SetStatus($"Formatting failed: {ex.Message}", true);
             }
        }
        else if (action.ActionId == "GenerateMethod")
        {
            if (action.Data.TryGetValue("MethodName", out var methodName))
            {
                // Check if method should be static
                action.Data.TryGetValue("IsStatic", out var isStaticStr);
                bool isStatic = isStaticStr == "True";
                
                // Get inferred parameters
                action.Data.TryGetValue("Parameters", out var parameters);
                parameters ??= "";
                
                // Build the method signature
                var staticModifier = isStatic ? "static " : "";
                var stub = $"\r\n\r\n        private {staticModifier}void {methodName}({parameters})\r\n        {{\r\n            throw new NotImplementedException();\r\n        }}";
                
                // Find the class closing brace (second-to-last '}' in the file)
                // The last '}' is typically the namespace closing brace
                var text = CodeEditor.Text;
                var braceCount = 0;
                var insertPosition = -1;
                
                for (int i = text.Length - 1; i >= 0; i--)
                {
                    if (text[i] == '}')
                    {
                        braceCount++;
                        if (braceCount == 2) // Found the class closing brace
                        {
                            insertPosition = i;
                            break;
                        }
                    }
                }
                
                if (insertPosition > 0)
                {
                    CodeEditor.Document.Insert(insertPosition, stub);
                }
                else if (text.LastIndexOf('}') > 0)
                {
                    // Fallback: insert before last brace if only one found
                    CodeEditor.Document.Insert(text.LastIndexOf('}'), stub);
                }
            }
        }
        else if (action.ActionId == "GenerateConstructor")
        {
            if (action.Data.TryGetValue("TypeName", out var typeName))
            {
                // Generate a constructor stub
                var stub = $"\r\n\r\n        public {typeName}()\r\n        {{\r\n            // TODO: Initialize fields\r\n        }}";
                
                // Find the class opening brace and insert after the first line inside
                var text = CodeEditor.Text;
                var classPattern = $"class\\s+{System.Text.RegularExpressions.Regex.Escape(typeName)}";
                var match = System.Text.RegularExpressions.Regex.Match(text, classPattern);
                
                if (match.Success)
                {
                    // Find the opening brace after the class declaration
                    var bracePos = text.IndexOf('{', match.Index);
                    if (bracePos > 0)
                    {
                        // Insert after the opening brace
                        CodeEditor.Document.Insert(bracePos + 1, stub);
                    }
                }
            }
        }
        else if (action.ActionId == "AddParameter")
        {
            if (action.Data.TryGetValue("MethodName", out var methodName))
            {
                // Prompt for parameter details
                var paramType = PromptForInput("Add Parameter", "Enter parameter type:", "string");
                if (string.IsNullOrEmpty(paramType)) return;
                
                var paramName = PromptForInput("Add Parameter", "Enter parameter name:", "value");
                if (string.IsNullOrEmpty(paramName)) return;
                
                var newParam = $"{paramType} {paramName}";
                
                // Find the method DECLARATION (not call site)
                // Method declarations have a return type before the method name
                var text = CodeEditor.Text;
                var escapedName = System.Text.RegularExpressions.Regex.Escape(methodName);
                // Pattern: return_type methodName( - the return type includes modifiers
                var methodDeclPattern = $@"(?:void|int|string|bool|double|float|object|var|\w+)\s+{escapedName}\s*\(";
                var match = System.Text.RegularExpressions.Regex.Match(text, methodDeclPattern);
                
                if (match.Success)
                {
                    var openParen = match.Index + match.Length - 1;
                    var closeParen = text.IndexOf(')', openParen);
                    
                    if (closeParen > openParen)
                    {
                        var existingParams = text.Substring(openParen + 1, closeParen - openParen - 1).Trim();
                        
                        if (string.IsNullOrEmpty(existingParams))
                        {
                            // No existing params, just insert
                            CodeEditor.Document.Insert(openParen + 1, newParam);
                        }
                        else
                        {
                            // Add comma and new param
                            CodeEditor.Document.Insert(closeParen, $", {newParam}");
                        }
                    }
                }
            }
        }
        else if (action.ActionId == "RemoveUnusedUsings")
        {
            try
            {
                // Use Roslyn to properly detect unused usings
                var text = CodeEditor.Text;
                
                // Parse the code and get compilation with diagnostics
                var tree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(text);
                var root = tree.GetRoot();
                
                // Get all using directives
                var usingDirectives = root.DescendantNodes()
                    .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.UsingDirectiveSyntax>()
                    .ToList();
                
                if (usingDirectives.Count == 0)
                {
                    SetStatus("No using statements found", false);
                    return;
                }
                
                // Get compilation with the current project to check for unused usings
                var (compilation, _) = await _compiler.CreateCompilationAsync(_currentProject!);
                
                // Replace the tree in compilation for accurate analysis
                var oldTree = compilation.SyntaxTrees.FirstOrDefault(t => 
                    string.Equals(System.IO.Path.GetFileName(t.FilePath), 
                                  System.IO.Path.GetFileName(_activeFile!.FilePath), 
                                  StringComparison.OrdinalIgnoreCase));
                
                var newTree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(
                    text, 
                    options: new Microsoft.CodeAnalysis.CSharp.CSharpParseOptions(Microsoft.CodeAnalysis.CSharp.LanguageVersion.Latest),
                    path: _activeFile.FilePath ?? "");
                
                if (oldTree != null)
                {
                    compilation = compilation.ReplaceSyntaxTree(oldTree, newTree);
                }
                else
                {
                    compilation = compilation.AddSyntaxTrees(newTree);
                }
                
                // Get diagnostics - CS8019 is "Unnecessary using directive"
                var diagnostics = compilation.GetDiagnostics()
                    .Where(d => d.Id == "CS8019" || d.Id == "IDE0005")
                    .ToList();
                
                if (diagnostics.Count == 0)
                {
                    // Fallback: Check for CS0246 "type or namespace not found" after removing each using
                    // If removing a using causes CS0246, it's needed
                    var usedUsings = new HashSet<int>();
                    var model = compilation.GetSemanticModel(newTree);
                    var newRoot = await newTree.GetRootAsync();
                    var newUsingDirectives = newRoot.DescendantNodes()
                        .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.UsingDirectiveSyntax>()
                        .ToList();
                    
                    // Get all type references in the code
                    var typeRefs = newRoot.DescendantNodes()
                        .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.IdentifierNameSyntax>()
                        .ToList();
                    
                    foreach (var typeRef in typeRefs)
                    {
                        var symbolInfo = model.GetSymbolInfo(typeRef);
                        if (symbolInfo.Symbol != null)
                        {
                            var containingNs = symbolInfo.Symbol.ContainingNamespace?.ToDisplayString();
                            if (containingNs != null)
                            {
                                for (int i = 0; i < newUsingDirectives.Count; i++)
                                {
                                    var usingNs = newUsingDirectives[i].Name?.ToString();
                                    if (usingNs != null && containingNs.StartsWith(usingNs))
                                    {
                                        usedUsings.Add(i);
                                    }
                                }
                            }
                        }
                    }
                    
                    // Remove unused usings (those not in usedUsings set)
                    var lines = text.Split('\n').ToList();
                    var removedCount = 0;
                    
                    for (int i = newUsingDirectives.Count - 1; i >= 0; i--)
                    {
                        if (!usedUsings.Contains(i))
                        {
                            var usingLine = newUsingDirectives[i].GetLocation().GetLineSpan().StartLinePosition.Line;
                            if (usingLine < lines.Count)
                            {
                                lines.RemoveAt(usingLine);
                                removedCount++;
                            }
                        }
                    }
                    
                    if (removedCount > 0)
                    {
                        CodeEditor.Document.Replace(0, CodeEditor.Document.TextLength, string.Join("\n", lines));
                        SetStatus($"Removed {removedCount} unused using(s)", false);
                    }
                    else
                    {
                        SetStatus("No unused usings found", false);
                    }
                }
                else
                {
                    // Use the diagnostics to find unused usings
                    var lines = text.Split('\n').ToList();
                    var linesToRemove = diagnostics
                        .Select(d => d.Location.GetLineSpan().StartLinePosition.Line)
                        .Distinct()
                        .OrderByDescending(x => x)
                        .ToList();
                    
                    foreach (var lineNum in linesToRemove)
                    {
                        if (lineNum < lines.Count)
                        {
                            lines.RemoveAt(lineNum);
                        }
                    }
                    
                    CodeEditor.Document.Replace(0, CodeEditor.Document.TextLength, string.Join("\n", lines));
                    SetStatus($"Removed {linesToRemove.Count} unused using(s)", false);
                }
            }
            catch (Exception ex)
            {
                SetStatus($"Failed to remove unused usings: {ex.Message}", true);
            }
        }
        else if (action.ActionId == "ChangeSignature")
        {
            if (action.Data.TryGetValue("MethodName", out var methodName))
            {
                // Find the method declaration
                // Method declarations have a return type before the method name
                var text = CodeEditor.Text;
                var escapedName = System.Text.RegularExpressions.Regex.Escape(methodName);
                
                // Pattern: return_type methodName(parameters)
                // Use a non-greedy match for return type: (?:...)\s+
                var methodDeclPattern = $@"(?:void|int|string|bool|double|float|object|var|\w+)\s+{escapedName}\s*\((.*?)\)";
                var match = System.Text.RegularExpressions.Regex.Match(text, methodDeclPattern, System.Text.RegularExpressions.RegexOptions.Singleline);
                
                if (match.Success)
                {
                    var currentParams = match.Groups[1].Value.Trim();
                    
                    // Prompt user for new parameters
                    var newParams = PromptForInput("Change Signature", $"Edit parameters for '{methodName}':", currentParams);
                    
                    if (newParams != null && newParams != currentParams)
                    {
                        var methodIndex = match.Index;
                        var paramStartIndex = match.Groups[1].Index;
                        var paramLength = match.Groups[1].Length;
                        
                        CodeEditor.Document.Replace(paramStartIndex, paramLength, newParams);
                        SetStatus($"Signature changed for '{methodName}'", false);
                    }
                }
                else
                {
                    MessageBox.Show($"Could not find method declaration for '{methodName}'.", "Change Signature", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }
        else
        {
             MessageBox.Show($"Action '{action.Title}' ({action.ActionId}) initiated.\nContext: {string.Join(", ", action.Data.Keys)}", "Quick Action", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void PerformRename(string originalName, int offset = -1)
    {
        // Capture offset before dialog if not provided
        if (offset < 0)
            offset = CodeEditor.CaretOffset;

        var dialog = new RenameDialog(originalName);
        dialog.Owner = this;
        if (dialog.ShowDialog() == true && dialog.NewName != originalName)
        {
             ExecuteRename(dialog.NewName, offset);
        }
    }

    private async void ExecuteRename(string newName, int offset)
    {
        if (_currentProject == null || _activeFile == null || _refactoringProvider == null) return;

        string currentContent = CodeEditor.Text; // Should be main thread, safe to access
        var result = await _refactoringProvider.GetRenameEditsAsync(_currentProject, _activeFile.FilePath, offset, newName, currentContent);

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

    private void DirectRename_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        if (_currentProject == null || _activeFile == null) return;

        // Get the word at the current caret position
        var offset = CodeEditor.CaretOffset;
        var document = CodeEditor.Document;
        var text = document.Text;

        if (offset < 0 || offset > text.Length) return;

        // Find word boundaries
        int start = offset;
        int end = offset;

        // Move start backward to find word start
        while (start > 0 && IsIdentifierChar(text[start - 1]))
            start--;

        // Move end forward to find word end
        while (end < text.Length && IsIdentifierChar(text[end]))
            end++;

        if (start == end)
        {
            SetStatus("Place cursor on an identifier to rename", true);
            return;
        }

        var wordToRename = text.Substring(start, end - start);
        PerformRename(wordToRename, offset);
    }

    private static bool IsIdentifierChar(char c)
    {
        return char.IsLetterOrDigit(c) || c == '_';
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

    private async void GoToDefinition_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        if (_currentProject == null || _activeFile == null || _refactoringProvider == null) return;

        // Sync current content
        _activeFile.Content = CodeEditor.Text;
        var offset = CodeEditor.CaretOffset;

        SetStatus("Finding definition...", false);

        var result = await _refactoringProvider.GetDefinitionAsync(_currentProject, _activeFile.FilePath, offset);

        if (result.Success && result.FilePath != null)
        {
            // Navigate to definition
            NavigateToLocation(result.FilePath, result.Line, result.Column);
            SetStatus($"Definition: {result.SymbolKind} {result.SymbolName}", false);
        }
        else
        {
            SetStatus(result.Error ?? "Definition not found", true);
        }
    }

    private async void FindAllReferences_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        if (_currentProject == null || _activeFile == null || _refactoringProvider == null) return;

        // Sync current content
        _activeFile.Content = CodeEditor.Text;
        var offset = CodeEditor.CaretOffset;

        SetStatus("Finding references...", false);

        var result = await _refactoringProvider.FindAllReferencesAsync(_currentProject, _activeFile.FilePath, offset);

        if (result.Success)
        {
            if (result.References.Count == 0)
            {
                SetStatus("No references found", true);
                return;
            }

            // If only one reference (the definition itself), just navigate to it
            if (result.References.Count == 1)
            {
                var singleRef = result.References[0];
                NavigateToLocation(singleRef.FilePath, singleRef.Line, singleRef.Column);
                SetStatus($"Found 1 reference to '{result.SymbolName}'", false);
                return;
            }

            // Show references in console panel
            ShowReferencesInConsole(result.SymbolName ?? "Symbol", result.References);
            SetStatus($"Found {result.References.Count} references to '{result.SymbolName}'", false);
        }
        else
        {
            SetStatus(result.Error ?? "Find references failed", true);
        }
    }

    private void ShowReferencesInConsole(string symbolName, List<Editor.RefactoringProvider.ReferenceLocation> references)
    {
        // Clear console and show references
        Console.ConsoleOutput.Instance.Clear();
        Console.ConsoleOutput.Instance.AddEntry($"References to '{symbolName}' ({references.Count} found):");
        Console.ConsoleOutput.Instance.AddEntry(new string('-', 50));

        foreach (var reference in references)
        {
            var prefix = reference.IsDefinition ? "[Definition] " : "";
            var message = $"{prefix}{reference.LineText}";

            Console.ConsoleOutput.Instance.AddEntry(
                message,
                reference.FilePath,
                reference.Line,
                reference.Column
            );
        }

        Console.ConsoleOutput.Instance.AddEntry(new string('-', 50));
        Console.ConsoleOutput.Instance.AddEntry("Double-click to navigate to reference");

        // Ensure console panel is visible
        if (ConsoleRow.Height.Value < 0.5)
        {
            ConsoleRow.Height = new GridLength(1, GridUnitType.Star);
        }
    }

    private void NavigateToLocation(string filePath, int line, int column)
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
                if (line > 0 && line <= CodeEditor.Document.LineCount)
                {
                    var lineObj = CodeEditor.Document.GetLineByNumber(line);
                    var col = Math.Max(1, Math.Min(column, lineObj.Length + 1));
                    var offset = CodeEditor.Document.GetOffset(line, col);

                    CodeEditor.CaretOffset = offset;
                    CodeEditor.ScrollToLine(line);
                    CodeEditor.Focus();

                    // Select the identifier at this location
                    var wordEnd = offset;
                    while (wordEnd < CodeEditor.Document.TextLength)
                    {
                        var c = CodeEditor.Document.GetCharAt(wordEnd);
                        if (!char.IsLetterOrDigit(c) && c != '_') break;
                        wordEnd++;
                    }
                    if (wordEnd > offset)
                    {
                        CodeEditor.Select(offset, wordEnd - offset);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"NavigateToLocation: {ex.Message}");
            }
        }
        else
        {
            SetStatus($"File not found in project: {Path.GetFileName(filePath)}", true);
        }
    }

    private async void PeekDefinition_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        if (_currentProject == null || _activeFile == null || _refactoringProvider == null) return;

        // Close any existing peek popup
        ClosePeekPopup();

        // Sync current content
        _activeFile.Content = CodeEditor.Text;
        var offset = CodeEditor.CaretOffset;

        SetStatus("Finding definition...", false);

        var result = await _refactoringProvider.GetDefinitionAsync(_currentProject, _activeFile.FilePath, offset);

        if (result.Success && result.FilePath != null)
        {
            ShowPeekDefinition(result);
        }
        else
        {
            SetStatus(result.Error ?? "Definition not found", true);
        }
    }

    private void ShowPeekDefinition(Editor.RefactoringProvider.DefinitionResult result)
    {
        if (_currentProject == null || result.FilePath == null) return;

        // Find the file content
        var file = _currentProject.Files.FirstOrDefault(f =>
            string.Equals(f.FilePath, result.FilePath, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Path.GetFileName(f.FilePath), Path.GetFileName(result.FilePath), StringComparison.OrdinalIgnoreCase));

        if (file == null)
        {
            SetStatus($"File not found: {Path.GetFileName(result.FilePath)}", true);
            return;
        }

        // Get context around the definition (5 lines before and 15 lines after)
        var lines = file.Content.Split('\n');
        var startLine = Math.Max(0, result.Line - 6);
        var endLine = Math.Min(lines.Length, result.Line + 15);
        var contextLines = lines.Skip(startLine).Take(endLine - startLine).ToList();
        var contextText = string.Join("\n", contextLines);

        // Create peek popup
        _peekPopup = new System.Windows.Controls.Primitives.Popup
        {
            PlacementTarget = CodeEditor,
            Placement = System.Windows.Controls.Primitives.PlacementMode.Relative,
            StaysOpen = false,
            AllowsTransparency = true
        };

        // Calculate position based on caret
        var caretPos = CodeEditor.TextArea.Caret.CalculateCaretRectangle();
        var visualPos = CodeEditor.TextArea.TextView.GetVisualPosition(
            new ICSharpCode.AvalonEdit.TextViewPosition(CodeEditor.TextArea.Caret.Line, CodeEditor.TextArea.Caret.Column),
            ICSharpCode.AvalonEdit.Rendering.VisualYPosition.LineBottom);

        _peekPopup.HorizontalOffset = 50;
        _peekPopup.VerticalOffset = visualPos.Y + 5;

        // Create content
        var border = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0, 122, 204)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            Width = 600,
            MaxHeight = 350
        };

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Header
        var header = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(45, 45, 45)),
            Padding = new Thickness(10, 5, 10, 5)
        };
        var headerPanel = new StackPanel { Orientation = Orientation.Horizontal };
        headerPanel.Children.Add(new TextBlock
        {
            Text = Path.GetFileName(result.FilePath),
            Foreground = new SolidColorBrush(Color.FromRgb(78, 201, 176)),
            FontWeight = FontWeights.SemiBold
        });
        headerPanel.Children.Add(new TextBlock
        {
            Text = $" : {result.Line}",
            Foreground = new SolidColorBrush(Color.FromRgb(156, 220, 254))
        });
        headerPanel.Children.Add(new TextBlock
        {
            Text = $"  ({result.SymbolKind} {result.SymbolName})",
            Foreground = new SolidColorBrush(Color.FromRgb(128, 128, 128)),
            Margin = new Thickness(10, 0, 0, 0)
        });
        header.Child = headerPanel;
        Grid.SetRow(header, 0);
        grid.Children.Add(header);

        // Code preview with AvalonEdit
        var previewEditor = new TextEditor
        {
            Text = contextText,
            IsReadOnly = true,
            ShowLineNumbers = true,
            Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
            Foreground = Brushes.White,
            FontFamily = CodeEditor.FontFamily,
            FontSize = CodeEditor.FontSize - 1,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Padding = new Thickness(5)
        };

        // Apply syntax highlighting
        previewEditor.SyntaxHighlighting = CodeEditor.SyntaxHighlighting;

        // Set line number starting offset
        previewEditor.TextArea.TextView.LineTransformers.Clear();

        // Scroll to show the definition line in context
        var defLineInContext = result.Line - startLine - 1;
        if (defLineInContext > 0 && defLineInContext <= previewEditor.Document.LineCount)
        {
            previewEditor.ScrollToLine(defLineInContext);
            // Highlight the definition line
            var defLineObj = previewEditor.Document.GetLineByNumber(Math.Min(defLineInContext + 1, previewEditor.Document.LineCount));
            previewEditor.Select(defLineObj.Offset, defLineObj.Length);
        }

        Grid.SetRow(previewEditor, 1);
        grid.Children.Add(previewEditor);

        // Footer with actions
        var footer = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(45, 45, 45)),
            Padding = new Thickness(10, 5, 10, 5)
        };
        var footerPanel = new StackPanel { Orientation = Orientation.Horizontal };

        var goToButton = new Button
        {
            Content = "Go to Definition (Enter)",
            Padding = new Thickness(10, 3, 10, 3),
            Margin = new Thickness(0, 0, 10, 0),
            Background = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(80, 80, 80))
        };
        goToButton.Click += (s, args) =>
        {
            ClosePeekPopup();
            NavigateToLocation(result.FilePath, result.Line, result.Column);
        };
        footerPanel.Children.Add(goToButton);

        var closeText = new TextBlock
        {
            Text = "Press Escape to close",
            Foreground = new SolidColorBrush(Color.FromRgb(128, 128, 128)),
            VerticalAlignment = VerticalAlignment.Center
        };
        footerPanel.Children.Add(closeText);

        footer.Child = footerPanel;
        Grid.SetRow(footer, 2);
        grid.Children.Add(footer);

        border.Child = grid;
        _peekPopup.Child = border;

        // Handle keyboard events
        _peekPopup.KeyDown += (s, args) =>
        {
            if (args.Key == Key.Escape)
            {
                ClosePeekPopup();
                CodeEditor.Focus();
                args.Handled = true;
            }
            else if (args.Key == Key.Enter)
            {
                ClosePeekPopup();
                NavigateToLocation(result.FilePath, result.Line, result.Column);
                args.Handled = true;
            }
        };

        _peekPopup.Closed += (s, args) => _peekPopup = null;

        _peekPopup.IsOpen = true;
        previewEditor.Focus();

        SetStatus($"Peek: {result.SymbolKind} {result.SymbolName} in {Path.GetFileName(result.FilePath)}:{result.Line}", false);
    }

    private void ClosePeekPopup()
    {
        if (_peekPopup != null)
        {
            _peekPopup.IsOpen = false;
            _peekPopup = null;
        }
    }

    // Symbol picker popup
    private System.Windows.Controls.Primitives.Popup? _symbolsPopup;

    private async void DocumentSymbols_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        if (_currentProject == null || _activeFile == null || _refactoringProvider == null) return;

        // Sync current content
        _activeFile.Content = CodeEditor.Text;

        SetStatus("Loading document symbols...", false);

        var result = await _refactoringProvider.GetDocumentSymbolsAsync(_currentProject, _activeFile.FilePath);

        if (result.Success)
        {
            ShowSymbolPicker(result.Symbols, "Go to Symbol in Editor", false);
        }
        else
        {
            SetStatus(result.Error ?? "Failed to load symbols", true);
        }
    }

    private async void WorkspaceSymbols_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        if (_currentProject == null || _refactoringProvider == null) return;

        // Sync current content if we have an active file
        if (_activeFile != null)
        {
            _activeFile.Content = CodeEditor.Text;
        }

        SetStatus("Loading workspace symbols...", false);

        var result = await _refactoringProvider.GetWorkspaceSymbolsAsync(_currentProject);

        if (result.Success)
        {
            ShowSymbolPicker(result.Symbols, "Go to Symbol in Workspace", true);
        }
        else
        {
            SetStatus(result.Error ?? "Failed to load symbols", true);
        }
    }

    private void ShowSymbolPicker(List<Editor.RefactoringProvider.DocumentSymbol> symbols, string title, bool showFilePath)
    {
        // Close existing popup
        CloseSymbolsPopup();

        _symbolsPopup = new System.Windows.Controls.Primitives.Popup
        {
            PlacementTarget = CodeEditor,
            Placement = System.Windows.Controls.Primitives.PlacementMode.Center,
            StaysOpen = false,
            AllowsTransparency = true
        };

        // Create content
        var border = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0, 122, 204)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(5),
            Width = 500,
            MaxHeight = 400
        };

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // Title
        var titleBlock = new TextBlock
        {
            Text = title,
            Foreground = Brushes.White,
            FontWeight = FontWeights.SemiBold,
            Padding = new Thickness(10, 8, 10, 5)
        };
        Grid.SetRow(titleBlock, 0);
        grid.Children.Add(titleBlock);

        // Search box
        var searchBox = new TextBox
        {
            Margin = new Thickness(10, 5, 10, 5),
            Padding = new Thickness(5),
            Background = new SolidColorBrush(Color.FromRgb(45, 45, 45)),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
            CaretBrush = Brushes.White
        };
        Grid.SetRow(searchBox, 1);
        grid.Children.Add(searchBox);

        // Symbols list
        var listBox = new ListBox
        {
            Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            MaxHeight = 300
        };

        // Flatten symbols including children
        var flatSymbols = new List<Editor.RefactoringProvider.DocumentSymbol>();
        void AddSymbolsRecursive(List<Editor.RefactoringProvider.DocumentSymbol> list, int indent = 0)
        {
            foreach (var symbol in list)
            {
                symbol.Detail = (indent > 0 ? new string(' ', indent * 2) : "") + symbol.Detail;
                flatSymbols.Add(symbol);
                if (symbol.Children.Count > 0)
                {
                    AddSymbolsRecursive(symbol.Children, indent + 1);
                }
            }
        }
        AddSymbolsRecursive(symbols);

        void PopulateList(string filter)
        {
            listBox.Items.Clear();
            var filtered = string.IsNullOrEmpty(filter)
                ? flatSymbols
                : flatSymbols.Where(s => s.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();

            foreach (var symbol in filtered.Take(100)) // Limit to 100 items
            {
                var itemPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(5, 3, 5, 3) };

                // Symbol kind icon/color
                var kindBrush = symbol.Kind switch
                {
                    "Class" => new SolidColorBrush(Color.FromRgb(78, 201, 176)),
                    "Interface" => new SolidColorBrush(Color.FromRgb(184, 215, 163)),
                    "Method" => new SolidColorBrush(Color.FromRgb(220, 220, 170)),
                    "Property" => new SolidColorBrush(Color.FromRgb(156, 220, 254)),
                    "Field" => new SolidColorBrush(Color.FromRgb(86, 156, 214)),
                    "Constructor" => new SolidColorBrush(Color.FromRgb(220, 220, 170)),
                    "Enum" => new SolidColorBrush(Color.FromRgb(184, 215, 163)),
                    "Event" => new SolidColorBrush(Color.FromRgb(255, 198, 109)),
                    _ => Brushes.White
                };

                var kindIcon = symbol.Kind switch
                {
                    "Class" => "C",
                    "Interface" => "I",
                    "Method" => "M",
                    "Property" => "P",
                    "Field" => "F",
                    "Constructor" => "C",
                    "Enum" => "E",
                    "Event" => "V",
                    "Struct" => "S",
                    "Record" => "R",
                    _ => "?"
                };

                itemPanel.Children.Add(new Border
                {
                    Width = 18,
                    Height = 18,
                    Background = kindBrush,
                    CornerRadius = new CornerRadius(2),
                    Margin = new Thickness(0, 0, 8, 0),
                    Child = new TextBlock
                    {
                        Text = kindIcon,
                        Foreground = Brushes.Black,
                        FontWeight = FontWeights.Bold,
                        FontSize = 11,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                });

                itemPanel.Children.Add(new TextBlock
                {
                    Text = symbol.Name,
                    Foreground = Brushes.White,
                    VerticalAlignment = VerticalAlignment.Center
                });

                if (!string.IsNullOrEmpty(symbol.Detail))
                {
                    itemPanel.Children.Add(new TextBlock
                    {
                        Text = "  " + symbol.Detail,
                        Foreground = new SolidColorBrush(Color.FromRgb(128, 128, 128)),
                        VerticalAlignment = VerticalAlignment.Center
                    });
                }

                if (showFilePath)
                {
                    itemPanel.Children.Add(new TextBlock
                    {
                        Text = $"  : {symbol.Line}",
                        Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
                        VerticalAlignment = VerticalAlignment.Center
                    });
                }

                var item = new ListBoxItem
                {
                    Content = itemPanel,
                    Tag = symbol,
                    Background = Brushes.Transparent
                };
                item.MouseDoubleClick += (s, args) => NavigateToSymbol(symbol);
                listBox.Items.Add(item);
            }

            if (listBox.Items.Count > 0)
            {
                listBox.SelectedIndex = 0;
            }
        }

        PopulateList("");

        searchBox.TextChanged += (s, args) => PopulateList(searchBox.Text);
        searchBox.PreviewKeyDown += (s, args) =>
        {
            if (args.Key == Key.Down && listBox.Items.Count > 0)
            {
                listBox.SelectedIndex = Math.Min(listBox.SelectedIndex + 1, listBox.Items.Count - 1);
                listBox.ScrollIntoView(listBox.SelectedItem);
                args.Handled = true;
            }
            else if (args.Key == Key.Up && listBox.Items.Count > 0)
            {
                listBox.SelectedIndex = Math.Max(listBox.SelectedIndex - 1, 0);
                listBox.ScrollIntoView(listBox.SelectedItem);
                args.Handled = true;
            }
            else if (args.Key == Key.Enter && listBox.SelectedItem is ListBoxItem selectedItem && selectedItem.Tag is Editor.RefactoringProvider.DocumentSymbol symbol)
            {
                NavigateToSymbol(symbol);
                args.Handled = true;
            }
            else if (args.Key == Key.Escape)
            {
                CloseSymbolsPopup();
                CodeEditor.Focus();
                args.Handled = true;
            }
        };

        Grid.SetRow(listBox, 2);
        grid.Children.Add(listBox);

        border.Child = grid;
        _symbolsPopup.Child = border;

        _symbolsPopup.Closed += (s, args) => _symbolsPopup = null;
        _symbolsPopup.IsOpen = true;
        searchBox.Focus();

        SetStatus($"Found {flatSymbols.Count} symbols", false);
    }

    private void NavigateToSymbol(Editor.RefactoringProvider.DocumentSymbol symbol)
    {
        CloseSymbolsPopup();
        NavigateToLocation(symbol.FilePath, symbol.Line, symbol.Column);
    }

    private void CloseSymbolsPopup()
    {
        if (_symbolsPopup != null)
        {
            _symbolsPopup.IsOpen = false;
            _symbolsPopup = null;
        }
    }

    private void CallHierarchy_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        if (_hierarchyProvider == null) return;

        var offset = CodeEditor.CaretOffset;
        var result = _hierarchyProvider.GetCallHierarchy(CodeEditor.Text, offset);

        if (result == null)
        {
            SetStatus("No method found at cursor position", true);
            return;
        }

        // Display results in console
        ConsoleOutput.Instance.Clear();
        ConsoleOutput.Instance.AddEntry($"=== Call Hierarchy for '{result.MethodName}' ===");

        if (result.IncomingCalls.Count > 0)
        {
            ConsoleOutput.Instance.AddEntry("");
            ConsoleOutput.Instance.AddEntry($"Incoming Calls ({result.IncomingCalls.Count}):");
            foreach (var call in result.IncomingCalls)
            {
                ConsoleOutput.Instance.AddEntry(
                    $"  {call.MethodName}() calls {result.MethodName}()",
                    _activeFile?.FilePath,
                    call.Line,
                    0,
                    false);
            }
        }
        else
        {
            ConsoleOutput.Instance.AddEntry("No incoming calls found.");
        }

        if (result.OutgoingCalls.Count > 0)
        {
            ConsoleOutput.Instance.AddEntry("");
            ConsoleOutput.Instance.AddEntry($"Outgoing Calls ({result.OutgoingCalls.Count}):");
            foreach (var call in result.OutgoingCalls)
            {
                ConsoleOutput.Instance.AddEntry(
                    $"  {result.MethodName}() calls {call.MethodName}()",
                    _activeFile?.FilePath,
                    call.Line,
                    0,
                    false);
            }
        }
        else
        {
            ConsoleOutput.Instance.AddEntry("No outgoing calls found.");
        }

        SetStatus($"Call hierarchy for {result.MethodName}: {result.IncomingCalls.Count} callers, {result.OutgoingCalls.Count} callees", false);
    }

    private void TypeHierarchy_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        if (_hierarchyProvider == null) return;

        var offset = CodeEditor.CaretOffset;
        var result = _hierarchyProvider.GetTypeHierarchy(CodeEditor.Text, offset);

        if (result == null)
        {
            SetStatus("No type found at cursor position", true);
            return;
        }

        // Display results in console
        ConsoleOutput.Instance.Clear();
        ConsoleOutput.Instance.AddEntry($"=== Type Hierarchy for '{result.TypeName}' ({result.TypeKind}) ===");

        if (result.BaseTypes.Count > 0)
        {
            ConsoleOutput.Instance.AddEntry("");
            ConsoleOutput.Instance.AddEntry("Base Types:");
            foreach (var baseType in result.BaseTypes)
            {
                ConsoleOutput.Instance.AddEntry($"  : {baseType.TypeName}");
            }
        }
        else
        {
            ConsoleOutput.Instance.AddEntry("No base types (other than object).");
        }

        if (result.DerivedTypes.Count > 0)
        {
            ConsoleOutput.Instance.AddEntry("");
            ConsoleOutput.Instance.AddEntry($"Derived Types ({result.DerivedTypes.Count}):");
            foreach (var derived in result.DerivedTypes)
            {
                ConsoleOutput.Instance.AddEntry(
                    $"  {derived.TypeName} : {result.TypeName}",
                    _activeFile?.FilePath,
                    derived.Line,
                    0,
                    false);
            }
        }
        else
        {
            ConsoleOutput.Instance.AddEntry("No derived types found.");
        }

        SetStatus($"Type hierarchy for {result.TypeName}: {result.BaseTypes.Count} base, {result.DerivedTypes.Count} derived", false);
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

    private void MoveTypeToNewFile(string typeName)
    {
        if (_activeFile == null || _currentProject == null) return;

        var code = CodeEditor.Text;
        if (string.IsNullOrEmpty(code)) return;

        // Find type definition (class, interface, enum, struct, record)
        var match = Regex.Match(code, $@"\b(?:public\s+|private\s+|internal\s+|protected\s+)?(?:partial\s+)?(?:static\s+)?(?:abstract\s+|sealed\s+)?(?:class|interface|enum|struct|record)\s+{Regex.Escape(typeName)}\b");
        if (!match.Success) return;

        var typeStart = match.Index;
        var openBrace = code.IndexOf('{', typeStart);
        if (openBrace == -1) return;

        // Find end of type by counting braces
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

        // Extract type code
        var typeCode = code.Substring(typeStart, endPos - typeStart);

        // Remove from current file (and potentially trailing newline)
        var newCode = code.Remove(typeStart, endPos - typeStart).TrimEnd();
        CodeEditor.Text = newCode;
        _activeFile.Content = newCode;

        // Create new file
        var fileName = _activeFile.FileName ?? "";
        var ext = Path.GetExtension(fileName);
        if (string.IsNullOrEmpty(ext))
            ext = _currentProject.ProjectFile.Language == ProjectLanguage.FSharp ? ".fs" : ".cs";
        var newFileName = $"{typeName}{ext}";

        // Basic template for new file (preserving usings if possible, or just the type)
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
            newFileContent = $"{header}\n\nnamespace {ns}\n{{\n    {typeCode}\n}}";
        }
        else
        {
            newFileContent = $"{header}\n\n{typeCode}";
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
        LoadProjectTree();

        SetStatus($"Moved '{typeName}' to {newFileName}", false);
    }

    private async Task ImplementInterfaceAsync(string className, string interfaceName)
    {
        if (_activeFile == null || _currentProject == null || _refactoringProvider == null) return;

        try
        {
            SetStatus($"Implementing {interfaceName}...", false);

            var implementation = await _refactoringProvider.GenerateInterfaceImplementationAsync(
                _currentProject, _activeFile.FilePath, className, interfaceName);

            if (string.IsNullOrEmpty(implementation))
            {
                SetStatus("No members to implement.", false);
                return;
            }

            // Find the class and insert before its closing brace
            var code = CodeEditor.Text;
            var classPattern = $@"class\s+{Regex.Escape(className)}\b";
            var classMatch = Regex.Match(code, classPattern);

            if (!classMatch.Success)
            {
                SetStatus($"Could not find class '{className}'", true);
                return;
            }

            // Find the opening brace of the class
            var openBrace = code.IndexOf('{', classMatch.Index);
            if (openBrace == -1) return;

            // Find the closing brace by counting braces
            int braceCount = 1;
            int closeBrace = -1;
            for (int i = openBrace + 1; i < code.Length; i++)
            {
                if (code[i] == '{') braceCount++;
                else if (code[i] == '}')
                {
                    braceCount--;
                    if (braceCount == 0)
                    {
                        closeBrace = i;
                        break;
                    }
                }
            }

            if (closeBrace == -1) return;

            // Insert implementation before the closing brace
            CodeEditor.Document.Insert(closeBrace, implementation);
            _activeFile.Content = CodeEditor.Text;
            _activeFile.HasUnsavedChanges = true;

            SetStatus($"Implemented interface '{interfaceName}'", false);
        }
        catch (Exception ex)
        {
            SetStatus($"Failed to implement interface: {ex.Message}", true);
        }
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

    #region Outliner

    private const int OutlinerMaxShapes = 1000;

    private void PopulateOutliner(IReadOnlyList<Geometry.IDrawable> shapes)
    {
        var items = new System.Collections.ObjectModel.ObservableCollection<Project.OutlinerItem>();

        // Group shapes by type
        var groupedShapes = shapes
            .OfType<Geometry.Shape>()
            .GroupBy(s => s.GetType().Name)
            .OrderBy(g => g.Key);

        var totalShapeCount = shapes.Count;

        foreach (var group in groupedShapes)
        {
            var groupItem = new Project.OutlinerItem(group.Key + $" ({group.Count()})");

            // Skip individual shape items if too many shapes (performance optimization)
            if (totalShapeCount <= OutlinerMaxShapes)
            {
                foreach (var shape in group.OrderBy(s => s.Id))
                {
                    var shapeName = !string.IsNullOrEmpty(shape.Name) ? shape.Name : group.Key;
                    var shapeItem = new Project.OutlinerItem(shapeName, isShape: true, id: shape.Id);
                    groupItem.Children.Add(shapeItem);
                }
            }

            items.Add(groupItem);
        }

        OutlinerTreeView.ItemsSource = items;
    }

    private void OutlinerExpandAll_Click(object sender, RoutedEventArgs e)
    {
        SetOutlinerItemsExpanded(OutlinerTreeView, true);
    }

    private void OutlinerCollapseAll_Click(object sender, RoutedEventArgs e)
    {
        SetOutlinerItemsExpanded(OutlinerTreeView, false);
    }

    private void SetOutlinerItemsExpanded(ItemsControl itemsControl, bool isExpanded)
    {
        foreach (var item in itemsControl.Items)
        {
            var container = itemsControl.ItemContainerGenerator.ContainerFromItem(item) as TreeViewItem;
            if (container != null)
            {
                container.IsExpanded = isExpanded;
                SetOutlinerItemsExpanded(container, isExpanded);
            }
        }
    }

    private void OutlinerIdLink_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is System.Windows.Controls.TextBlock textBlock && textBlock.DataContext is Project.OutlinerItem item && item.IsShape)
        {
            if (RenderCanvas.ZoomToShape(item.Id))
            {
                SetStatus($"Zoomed to shape ID: {item.Id}", isError: false);
            }
            else
            {
                SetStatus($"Shape with ID {item.Id} not found", isError: true);
            }
        }
    }

    private void OutlinerItem_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is Project.OutlinerItem item && item.IsShape)
        {
            RenderCanvas.HighlightedShapeId = item.Id;
        }
    }

    private void OutlinerItem_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        RenderCanvas.HighlightedShapeId = null;
    }

    #endregion

    #region Undo/Redo

    private void PerformUndo()
    {
        if (TransactionManager.Instance.CanUndo)
        {
            var description = TransactionManager.Instance.UndoDescription;
            TransactionManager.Instance.Undo();
            SetStatus($"Undo: {description}", isError: false);
        }
        else
        {
            SetStatus("Nothing to undo", isError: false);
        }
    }

    private void PerformRedo()
    {
        if (TransactionManager.Instance.CanRedo)
        {
            var description = TransactionManager.Instance.RedoDescription;
            TransactionManager.Instance.Redo();
            SetStatus($"Redo: {description}", isError: false);
        }
        else
        {
            SetStatus("Nothing to redo", isError: false);
        }
    }

    private void UndoMenuItem_Click(object sender, RoutedEventArgs e)
    {
        PerformUndo();
    }

    private void RedoMenuItem_Click(object sender, RoutedEventArgs e)
    {
        PerformRedo();
    }

    #endregion

    #region Animation Controls

    private bool _isPaused = false;

    private void PlayPause_Click(object sender, RoutedEventArgs e)
    {
        var timeline = CanvasRenderer.Instance.ActiveTimeline;
        if (timeline == null) return;

        if (_isPaused)
        {
            // Resume from paused state
            _isPaused = false;
            timeline.IsPlaying = true;
            _animationStopwatch.Start();
            PlayPauseBtn.Content = "\u23F8"; // Pause symbol
        }
        else if (timeline.IsPlaying)
        {
            // Pause
            _isPaused = true;
            timeline.IsPlaying = false;
            _animationStopwatch.Stop();
            PlayPauseBtn.Content = "\u25B6"; // Play symbol
        }
        else
        {
            // Start playing
            timeline.IsPlaying = true;
            _animationStopwatch.Restart();
            PlayPauseBtn.Content = "\u23F8"; // Pause symbol
        }
    }

    private void Stop_Click(object sender, RoutedEventArgs e)
    {
        var timeline = CanvasRenderer.Instance.ActiveTimeline;
        if (timeline == null) return;

        // Stop and reset
        timeline.IsPlaying = false;
        _isPaused = false;
        _animationStopwatch.Reset();
        timeline.Update(0);
        RenderCanvas.Refresh();

        PlayPauseBtn.Content = "\u25B6"; // Play symbol
        TimeDisplay.Text = $"0.00s / {timeline.Duration:F2}s";
    }

    private void SpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        var timeline = CanvasRenderer.Instance.ActiveTimeline;
        if (timeline != null)
        {
            timeline.Speed = SpeedSlider.Value;
        }

        // Update speed display
        if (SpeedText != null)
        {
            SpeedText.Text = $"{SpeedSlider.Value:F2}x";
        }
    }

    private Timeline? _lastTimeline = null;

    private void UpdateAnimationControlsVisibility()
    {
        var timeline = CanvasRenderer.Instance.ActiveTimeline;
        if (timeline != null)
        {
            // Always show animation controls (play/pause buttons)
            AnimationControlsPanel.Visibility = Visibility.Visible;

            // Only show timeline panel if user hasn't disabled it in Window menu
            bool userWantsTimeline = ShowTimelineMenuItem.IsChecked;
            if (userWantsTimeline)
            {
                TimelinePanel.Visibility = Visibility.Visible;
                TimelineSplitter.Visibility = Visibility.Visible;
                TimelineRow.Height = new GridLength(120);
            }

            // Update time display
            var currentTime = timeline.CurrentTime;
            var duration = timeline.Duration;
            TimeDisplay.Text = $"{currentTime:F2}s / {duration:F2}s";

            // Update play/pause button
            if (timeline.IsPlaying && !_isPaused)
            {
                PlayPauseBtn.Content = "\u23F8"; // Pause symbol
            }
            else
            {
                PlayPauseBtn.Content = "\u25B6"; // Play symbol
            }

            // Update timeline panel if timeline changed
            if (timeline != _lastTimeline)
            {
                _lastTimeline = timeline;
                TimelinePanel.SetTimeline(timeline);
            }
            else
            {
                // Just update playhead
                TimelinePanel.UpdatePlayhead();
            }
        }
        else
        {
            AnimationControlsPanel.Visibility = Visibility.Collapsed;
            TimelinePanel.Visibility = Visibility.Collapsed;
            TimelineSplitter.Visibility = Visibility.Collapsed;
            TimelineRow.Height = new GridLength(0);
            _isPaused = false;

            if (_lastTimeline != null)
            {
                _lastTimeline = null;
                TimelinePanel.SetTimeline(null);
            }
        }
    }

    #endregion

    #region Find and Replace

    private void FindMenuItem_Click(object sender, RoutedEventArgs e)
    {
        ShowFindReplaceDialog(showReplace: false);
    }

    private void FindReplaceMenuItem_Click(object sender, RoutedEventArgs e)
    {
        ShowFindReplaceDialog(showReplace: true);
    }

    private void FindInFilesMenuItem_Click(object sender, RoutedEventArgs e)
    {
        ShowFindReplaceDialog(showReplace: false, projectScope: true);
    }

    private void ShowFindReplaceDialog(bool showReplace, bool projectScope = false)
    {
        if (_findReplaceDialog == null)
        {
            _findReplaceDialog = new FindReplaceDialog { Owner = this };

            _findReplaceDialog.FindNextRequested += (s, options) => PerformFindNext(options);
            _findReplaceDialog.FindAllRequested += (s, options) => PerformFindAll(options);
            _findReplaceDialog.ReplaceRequested += (s, options) => PerformReplace(options);
            _findReplaceDialog.ReplaceAllRequested += (s, options) => PerformReplaceAll(options);
        }

        _findReplaceDialog.ShowReplace = showReplace;

        // Set initial search text from selection
        if (CodeEditor.SelectionLength > 0 && CodeEditor.SelectionLength < 100)
        {
            var selectedText = CodeEditor.SelectedText;
            if (!selectedText.Contains('\n') && !selectedText.Contains('\r'))
            {
                _findReplaceDialog.SearchText = selectedText;
            }
        }

        if (projectScope)
        {
            _findReplaceDialog.SetProjectScope();
        }

        _findReplaceDialog.Show();
        _findReplaceDialog.Activate();
    }

    private void PerformFindNext(SearchOptions options)
    {
        if (_activeFile == null) return;

        var content = CodeEditor.Text;
        var startIndex = CodeEditor.CaretOffset;

        var result = _findReplaceService.FindNext(content, options, startIndex);

        if (result.HasValue)
        {
            CodeEditor.Select(result.Value.Start, result.Value.Length);
            CodeEditor.ScrollTo(CodeEditor.Document.GetLineByOffset(result.Value.Start).LineNumber, 0);
            _findReplaceDialog?.SetStatus($"Match found at offset {result.Value.Start}");
        }
        else
        {
            _findReplaceDialog?.SetStatus("No matches found");
        }
    }

    private void PerformFindAll(SearchOptions options)
    {
        var results = new List<SearchResult>();

        if (options.Scope == SearchScope.EntireProject && _currentProject != null)
        {
            // Search all files in project
            var files = new Dictionary<string, string>();
            foreach (var file in _currentProject.Files)
            {
                files[file.FilePath] = file.Content;
            }
            results = _findReplaceService.FindInProject(files, options);
        }
        else if (_activeFile != null)
        {
            // Search current file only
            results = _findReplaceService.FindAll(CodeEditor.Text, _activeFile.FilePath, options);
        }

        ShowFindResults(results, options.SearchText);
    }

    private void ShowFindResults(List<SearchResult> results, string searchTerm)
    {
        FindResultsPanel.Results = results;
        FindResultsPanel.SetSearchTerm(searchTerm);
        ShowFindResultsTab();

        if (results.Count > 0)
        {
            _findReplaceDialog?.SetStatus($"Found {results.Count} match{(results.Count == 1 ? "" : "es")}");
        }
        else
        {
            _findReplaceDialog?.SetStatus("No matches found");
        }
    }

    private void PerformReplace(SearchOptions options)
    {
        if (_activeFile == null) return;

        var content = CodeEditor.Text;
        var startIndex = CodeEditor.CaretOffset;

        var result = _findReplaceService.ReplaceNext(content, options, startIndex);

        if (result.HasValue)
        {
            CodeEditor.Document.Text = result.Value.NewContent;
            CodeEditor.Select(result.Value.MatchStart, result.Value.MatchLength);
            CodeEditor.ScrollTo(CodeEditor.Document.GetLineByOffset(result.Value.MatchStart).LineNumber, 0);
            _findReplaceDialog?.SetStatus("Replaced 1 occurrence");
        }
        else
        {
            _findReplaceDialog?.SetStatus("No matches found");
        }
    }

    private void PerformReplaceAll(SearchOptions options)
    {
        if (options.Scope == SearchScope.EntireProject && _currentProject != null)
        {
            // Replace in all files
            int totalReplacements = 0;
            int filesModified = 0;

            foreach (var file in _currentProject.Files)
            {
                var (newContent, count) = _findReplaceService.ReplaceAll(file.Content, options);
                if (count > 0)
                {
                    file.Content = newContent;
                    totalReplacements += count;
                    filesModified++;

                    // Update editor if this is the active file
                    if (file == _activeFile)
                    {
                        CodeEditor.Document.Text = newContent;
                    }
                }
            }

            _findReplaceDialog?.SetStatus($"Replaced {totalReplacements} occurrence{(totalReplacements == 1 ? "" : "s")} in {filesModified} file{(filesModified == 1 ? "" : "s")}");
        }
        else if (_activeFile != null)
        {
            // Replace in current file only
            var (newContent, count) = _findReplaceService.ReplaceAll(CodeEditor.Text, options);

            if (count > 0)
            {
                CodeEditor.Document.Text = newContent;
                _findReplaceDialog?.SetStatus($"Replaced {count} occurrence{(count == 1 ? "" : "s")}");
            }
            else
            {
                _findReplaceDialog?.SetStatus("No matches found");
            }
        }
    }

    private void NavigateToSearchResult(SearchResult result)
    {
        // Find and open the file if it's different from current
        if (_currentProject != null)
        {
            var file = _currentProject.Files.FirstOrDefault(f => f.FilePath == result.FilePath);
            if (file != null && file != _activeFile)
            {
                SelectFile(file);
            }
        }

        // Navigate to the location
        if (_activeFile != null && _activeFile.FilePath == result.FilePath)
        {
            var line = Math.Min(result.LineNumber, CodeEditor.Document.LineCount);
            var lineObj = CodeEditor.Document.GetLineByNumber(line);
            var offset = lineObj.Offset + Math.Max(0, result.Column - 1);

            CodeEditor.CaretOffset = offset;
            CodeEditor.Select(offset, result.MatchLength);
            CodeEditor.ScrollTo(line, result.Column);
            CodeEditor.Focus();
        }
    }

    #endregion
}
