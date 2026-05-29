using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Folding;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using ICSharpCode.AvalonEdit.Search;
using Code2Viz.Project;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using ICSharpCode.AvalonEdit.Rendering;
using Code2Viz.Editor.Minimap;
using AvalonSelection = ICSharpCode.AvalonEdit.Editing.Selection;

#if ANIMATOR
using Animator.Editor;
#endif

namespace Code2Viz.Editor
{
    public class DiagnosticMarkerInfo
    {
        public string FilePath { get; set; } = "";
        public int StartLine { get; set; }
        public int StartColumn { get; set; }
        public int EndLine { get; set; }
        public int EndColumn { get; set; }
        public string Message { get; set; } = "";
        public bool IsError { get; set; } // true = error, false = warning
    }

    public class SharedEditorController
    {
        private readonly TextEditor _editor;

        // Command Definitions (Self-contained in the controller)
        public static readonly RoutedCommand RenameCommand = new RoutedCommand("Rename", typeof(SharedEditorController));
        public static readonly RoutedCommand GoToDefinitionCommand = new RoutedCommand("GoToDefinition", typeof(SharedEditorController));
        public static readonly RoutedCommand PeekDefinitionCommand = new RoutedCommand("PeekDefinition", typeof(SharedEditorController));
        public static readonly RoutedCommand FindAllReferencesCommand = new RoutedCommand("FindAllReferences", typeof(SharedEditorController));
        public static readonly RoutedCommand DocumentSymbolsCommand = new RoutedCommand("DocumentSymbols", typeof(SharedEditorController));
        public static readonly RoutedCommand WorkspaceSymbolsCommand = new RoutedCommand("WorkspaceSymbols", typeof(SharedEditorController));
        public static readonly RoutedCommand CallHierarchyCommand = new RoutedCommand("CallHierarchy", typeof(SharedEditorController));
        public static readonly RoutedCommand TypeHierarchyCommand = new RoutedCommand("TypeHierarchy", typeof(SharedEditorController));
        public static readonly RoutedCommand DirectRenameCommand = new RoutedCommand("DirectRename", typeof(SharedEditorController));

        // Callbacks / Delegates to the Host
        public Func<string?> GetActiveFilePath { get; set; } = () => null;
        public Func<VizCodeProject?> GetActiveProject { get; set; } = () => null;
        public Action<string, bool> SetStatusMessage { get; set; } = (m, e) => { };
        public Action<string, int, int> NavigateToLocation { get; set; } = (f, l, c) => { };
        public Action<string, List<RefactoringProvider.ReferenceLocation>> ShowReferences { get; set; } = (s, r) => { };
        
        // Auto-run canvas callback (mainly Code2Viz debounced auto-update)
        public Func<Task> AutoRunCodeAsync { get; set; } = () => Task.CompletedTask;
        public Func<bool> IsAutoUpdateEnabled { get; set; } = () => false;
        public Func<int> GetAutoUpdateDelayMs { get; set; } = () => 1000;

        // Workspace Compilation Provider
        public Func<CachedCompilationWorkspace?> GetWorkspace { get; set; } = () => null;

        // Renderers & Services
        private VizTextMarkerService? _textMarkerService;
        private BracketHighlightRenderer? _bracketRenderer;
        private SelectionHighlightRenderer? _selectionHighlightRenderer;
        private MultiSelectionRenderer? _multiSelectionRenderer;
        private SemanticHighlighter? _semanticHighlighter;
        private InlayHintGenerator? _inlayHintGenerator;
        private CodeLensGenerator? _codeLensGenerator;
        private FoldingManager? _foldingManager;
        private BraceFoldingStrategy? _foldingStrategy;

        // Refactoring and Symbol Navigation
        private RefactoringProvider? _refactoringProvider;
        private HierarchyProvider? _hierarchyProvider;

        // Timers
        private DispatcherTimer? _semanticUpdateTimer;
        private DispatcherTimer? _foldingTimer;
        private DispatcherTimer? _syntaxCheckTimer;
        private DispatcherTimer? _autoUpdateTimer;

        // Overlays
        private CompletionWindow? _completionWindow;
        private OverloadInsightWindow? _insightWindow;
        private DocumentationSidecar? _docSidecar;
        private SnippetSession? _snippetSession;
        private ToolTip? _activeHoverTip;

        // UI Popup state
        private System.Windows.Controls.Primitives.Popup? _peekPopup;
        private System.Windows.Controls.Primitives.Popup? _symbolsPopup;

        // State Flags
        private bool _textChangedSinceLastCheck;
        private bool _isAddingNextOccurrence;
        private bool _isMultiCursorEditing;
        private bool _suppressAutoUpdate;
        
        public bool EnableInternalSyntaxCheck { get; set; } = true;

#if ANIMATOR
        private const string DefaultFileId = Animator.Editor.CompletionEngine.FileId;
#else
        private const string DefaultFileId = "Sketch.cs";
#endif

        private readonly MinimapControl? _minimap;

        public Func<RefactoringProvider.QuickActionItem, Task<bool>>? CustomQuickActionHandler { get; set; }

        public SharedEditorController(TextEditor editor, MinimapControl? minimap = null)
        {
            _editor = editor;
            _minimap = minimap;
            ApplyRefactoring = DefaultApplyRefactoring;
        }

        public Action<Dictionary<string, List<(int Offset, int Length, string NewText)>>> ApplyRefactoring { get; set; }

        private void DefaultApplyRefactoring(Dictionary<string, List<(int Offset, int Length, string NewText)>> changes)
        {
            _editor.Document.BeginUpdate();
            try
            {
                foreach (var kvp in changes)
                {
                    var fileChanges = kvp.Value;
                    foreach (var change in fileChanges.OrderByDescending(c => c.Offset))
                    {
                        _editor.Document.Replace(change.Offset, change.Length, change.NewText);
                    }
                }
            }
            finally
            {
                _editor.Document.EndUpdate();
            }
        }

        public void Initialize()
        {
            // 1. Clean up potential old attachments
            _editor.TextArea.TextView.BackgroundRenderers.Clear();
            _editor.TextArea.TextView.ElementGenerators.Clear();
            _editor.TextArea.TextView.LineTransformers.Clear();
            
            // 2. Editor Options
            _editor.Options.ConvertTabsToSpaces = true;
            _editor.Options.IndentationSize = 4;
            
            // 3. Built-in Find/Replace Search Panel
            SearchPanel.Install(_editor);

            // 4. Load Highlighting dynamically
            LoadLanguageHighlighting(GetActiveFilePath() ?? ".cs");

            // 5. Setup VizTextMarkerService
            _textMarkerService = new VizTextMarkerService(_editor.Document);
            _editor.TextArea.TextView.BackgroundRenderers.Add(_textMarkerService);
            _editor.TextArea.TextView.Services.AddService(typeof(VizTextMarkerService), _textMarkerService);
            _textMarkerService.MarkersChanged += (s, e) => UpdateMinimapMarkers();

            // 6. Setup Renderers
            _bracketRenderer = new BracketHighlightRenderer(_editor.TextArea.TextView);
            _editor.TextArea.TextView.BackgroundRenderers.Add(_bracketRenderer);
            _editor.TextArea.Caret.PositionChanged += Caret_PositionChanged;

            _selectionHighlightRenderer = new SelectionHighlightRenderer(_editor.TextArea.TextView);
            _editor.TextArea.TextView.BackgroundRenderers.Add(_selectionHighlightRenderer);
            _editor.TextArea.SelectionChanged += OnEditorSelectionChanged;

            _multiSelectionRenderer = new MultiSelectionRenderer(_editor.TextArea.TextView);
            _editor.TextArea.TextView.BackgroundRenderers.Add(_multiSelectionRenderer);
            _editor.TextArea.SelectionChanged += OnSelectionChanged_ClearMultiSelect;
            _editor.TextArea.TextEntering += TextArea_TextEntering_MultiCursor;
            _editor.TextArea.PreviewMouseDown += TextArea_PreviewMouseDown_ClearMultiSelect;

            // 7. Setup Semantic Highlighter
            _semanticHighlighter = new SemanticHighlighter(_editor.Document);
            _editor.TextArea.TextView.LineTransformers.Add(_semanticHighlighter);
            _semanticHighlighter.Enabled = true;

            // 8. Setup Inlay Hints
            _inlayHintGenerator = new InlayHintGenerator(_editor.Document);
            _editor.TextArea.TextView.ElementGenerators.Add(_inlayHintGenerator);
            _inlayHintGenerator.Enabled = false;

            // 9. Setup Code Lens
            _codeLensGenerator = new CodeLensGenerator(_editor.Document);
            _editor.TextArea.TextView.ElementGenerators.Add(_codeLensGenerator);
            _codeLensGenerator.Enabled = true;

            // 10. Setup Snippets & Sidecar
            _snippetSession = new SnippetSession(_editor);
            SnippetCompletionData.ActiveSession = _snippetSession;
            _docSidecar = new DocumentationSidecar();

            // 11. Autocomplete & Indentation Event Handlers
            _editor.TextArea.TextEntered += OnTextEntered;
            _editor.TextArea.TextEntering += OnTextEntering;
            _editor.TextArea.PreviewKeyDown += OnEditorPreviewKeyDown;

            // Intercept Paste to support multi-cursor paste
            _editor.TextArea.CommandBindings.Insert(0, new CommandBinding(
                ApplicationCommands.Paste,
                (s, e) =>
                {
                    if (_multiSelectionRenderer != null && _multiSelectionRenderer.HasSelections)
                    {
                        _isMultiCursorEditing = true;
                        _isAddingNextOccurrence = true;
                        try
                        {
                            _multiSelectionRenderer.PasteAtAllCursors();
                            e.Handled = true;
                        }
                        finally
                        {
                            _isAddingNextOccurrence = false;
                            _isMultiCursorEditing = false;
                        }
                    }
                },
                (s, e) =>
                {
                    if (_multiSelectionRenderer != null && _multiSelectionRenderer.HasSelections)
                        e.CanExecute = true;
                }));

            // 12. Setup Refactoring & Hierarchy Providers
            _refactoringProvider = new RefactoringProvider(async () =>
            {
                var ws = GetWorkspace();
                return ws != null ? ws.GetCompilation() : null!;
            });
            _hierarchyProvider = new HierarchyProvider();

            // 13. Command Bindings on Editor
            _editor.CommandBindings.Add(new CommandBinding(RenameCommand, Rename_Executed));
            _editor.CommandBindings.Add(new CommandBinding(GoToDefinitionCommand, GoToDefinition_Executed));
            _editor.CommandBindings.Add(new CommandBinding(PeekDefinitionCommand, PeekDefinition_Executed));
            _editor.CommandBindings.Add(new CommandBinding(FindAllReferencesCommand, FindAllReferences_Executed));
            _editor.CommandBindings.Add(new CommandBinding(DocumentSymbolsCommand, DocumentSymbols_Executed));
            _editor.CommandBindings.Add(new CommandBinding(WorkspaceSymbolsCommand, WorkspaceSymbols_Executed));
            _editor.CommandBindings.Add(new CommandBinding(CallHierarchyCommand, CallHierarchy_Executed));
            _editor.CommandBindings.Add(new CommandBinding(TypeHierarchyCommand, TypeHierarchy_Executed));
            _editor.CommandBindings.Add(new CommandBinding(DirectRenameCommand, DirectRename_Executed));

            // 14. Key Gestures
            _editor.InputBindings.Add(new KeyBinding(GoToDefinitionCommand, Key.F12, ModifierKeys.None));
            _editor.InputBindings.Add(new KeyBinding(PeekDefinitionCommand, Key.F12, ModifierKeys.Alt));
            _editor.InputBindings.Add(new KeyBinding(FindAllReferencesCommand, Key.F12, ModifierKeys.Shift));
            _editor.InputBindings.Add(new KeyBinding(DocumentSymbolsCommand, Key.O, ModifierKeys.Control | ModifierKeys.Shift));
            _editor.InputBindings.Add(new KeyBinding(WorkspaceSymbolsCommand, Key.T, ModifierKeys.Control));
            _editor.InputBindings.Add(new KeyBinding(CallHierarchyCommand, Key.H, ModifierKeys.Control | ModifierKeys.Shift));
            _editor.InputBindings.Add(new KeyBinding(TypeHierarchyCommand, Key.T, ModifierKeys.Control | ModifierKeys.Shift));
            _editor.InputBindings.Add(new KeyBinding(DirectRenameCommand, Key.F2, ModifierKeys.None));
            _editor.InputBindings.Add(new KeyBinding(RenameCommand, Key.OemPeriod, ModifierKeys.Control));
            _editor.InputBindings.Add(new KeyBinding(new RelayCommand(_ => ShowCompletionWindow(autoTrigger: false)), Key.Space, ModifierKeys.Control));

            // 15. Attach Minimap
            try { _minimap?.AttachToEditor(_editor); }
            catch (Exception ex) { Debug.WriteLine($"Minimap attach failed: {ex.Message}"); }

            // 16. Hover quick info / error tooltip
            _editor.MouseHover += Editor_MouseHover;
            _editor.MouseHoverStopped += Editor_MouseHoverStopped;
            _editor.MouseLeave += (s, e) => CloseHoverTip();
            _editor.LostFocus += (s, e) => CloseHoverTip();
            _editor.PreviewMouseDown += (s, e) => CloseHoverTip();
            _editor.PreviewKeyDown += (s, e) => CloseHoverTip();
            // The host Window may not exist yet during Initialize; hook it lazily.
            _editor.Loaded += (s, e) =>
            {
                var host = Window.GetWindow(_editor);
                if (host == null) return;
                host.Deactivated += (s2, e2) => CloseHoverTip();
                host.LocationChanged += (s2, e2) => CloseHoverTip();
                host.Closing += (s2, e2) => CloseHoverTip();
            };

            // 17. Context Menu
            InitializeContextMenu();

            // 18. Initialize Timers
            _semanticUpdateTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _semanticUpdateTimer.Tick += async (s, e) =>
            {
                _semanticUpdateTimer.Stop();
                await UpdateSemanticHighlightingAsync();
            };
            
            // Initial run of semantic highlighting
            _ = UpdateSemanticHighlightingAsync();

            _foldingManager = FoldingManager.Install(_editor.TextArea);
            _foldingStrategy = new BraceFoldingStrategy();
            _foldingTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _foldingTimer.Tick += (s, e) =>
            {
                try { _foldingStrategy.UpdateFoldings(_foldingManager, _editor.Document); }
                catch (Exception ex) { Debug.WriteLine($"Folding error: {ex.Message}"); }
            };
            _foldingTimer.Start();
            try { _foldingStrategy.UpdateFoldings(_foldingManager, _editor.Document); } catch { }

            _syntaxCheckTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(800) };
            _syntaxCheckTimer.Tick += async (s, e) =>
            {
                if (_textChangedSinceLastCheck && EnableInternalSyntaxCheck)
                {
                    _textChangedSinceLastCheck = false;
                    await PerformWorkspaceSyntaxCheckAsync();
                }
            };
            _syntaxCheckTimer.Start();

            _autoUpdateTimer = new DispatcherTimer();
            _autoUpdateTimer.Tick += async (s, e) =>
            {
                _autoUpdateTimer.Stop();
                if (IsAutoUpdateEnabled() && !_suppressAutoUpdate)
                {
                    await AutoRunCodeAsync();
                }
            };

            _editor.TextChanged += (s, e) =>
            {
                _textChangedSinceLastCheck = true;
                _suppressAutoUpdate = false;

                // Keep the C# compilation workspace in sync
                var ws = GetWorkspace();
                var activeFile = GetActiveFilePath() ?? DefaultFileId;
                if (ws != null)
                {
                    ws.UpdateFile(activeFile, _editor.Text);
                }

                TriggerSemanticHighlightingUpdate();

                if (IsAutoUpdateEnabled())
                {
                    _autoUpdateTimer.Interval = TimeSpan.FromMilliseconds(GetAutoUpdateDelayMs());
                    _autoUpdateTimer.Stop();
                    _autoUpdateTimer.Start();
                }
            };

            // Font resizing via Ctrl+MouseWheel
            _editor.PreviewMouseWheel += Editor_PreviewMouseWheel;
        }

        public void SetActiveFile(string fileId)
        {
            _editor.Dispatcher.VerifyAccess();
            ClosePeekPopup();
            CloseSymbolsPopup();
            
            LoadLanguageHighlighting(fileId);

            var ws = GetWorkspace();
            if (ws != null)
            {
                ws.UpdateFile(fileId, _editor.Text);
            }

            _textMarkerService?.Clear();
            UpdateMinimapMarkers();
            
            _ = UpdateSemanticHighlightingAsync();
            try { _foldingStrategy?.UpdateFoldings(_foldingManager, _editor.Document); } catch { }
        }

        public bool InlayHintsEnabled
        {
            get => _inlayHintGenerator != null && _inlayHintGenerator.Enabled;
            set
            {
                if (_inlayHintGenerator != null)
                {
                    _inlayHintGenerator.Enabled = value;
                    _editor.TextArea.TextView.Redraw();
                }
            }
        }

        public bool CodeLensEnabled
        {
            get => _codeLensGenerator != null && _codeLensGenerator.Enabled;
            set
            {
                if (_codeLensGenerator != null)
                {
                    _codeLensGenerator.Enabled = value;
                    _editor.TextArea.TextView.Redraw();
                }
            }
        }

        public bool SemanticHighlightingEnabled
        {
            get => _semanticHighlighter != null && _semanticHighlighter.Enabled;
            set
            {
                if (_semanticHighlighter != null)
                {
                    _semanticHighlighter.Enabled = value;
                    if (value)
                        _ = UpdateSemanticHighlightingAsync();
                    else
                        _editor.TextArea.TextView.Redraw();
                }
            }
        }

        public void LoadLanguageHighlighting(string filePath)
        {
            try
            {
                var assembly = Assembly.GetEntryAssembly() ?? typeof(SharedEditorController).Assembly;
                var resourceNames = assembly.GetManifestResourceNames();
                var resourceName = resourceNames.FirstOrDefault(r => r.EndsWith("CSharpHighlighting.xshd"));

                if (resourceName != null)
                {
                    using var stream = assembly.GetManifestResourceStream(resourceName);
                    if (stream != null)
                    {
                        using var reader = new XmlTextReader(stream);
                        _editor.SyntaxHighlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
                        return;
                    }
                }

                _editor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("C#");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading highlighting: {ex.Message}");
                _editor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("C#");
            }
        }

        public void UpdateErrorMarkers(IEnumerable<DiagnosticMarkerInfo> markers)
        {
            _editor.Dispatcher.VerifyAccess();
            if (_textMarkerService == null) return;

            _textMarkerService.Clear();
            int errorCount = 0;
            string? activeFile = GetActiveFilePath();

            foreach (var marker in markers)
            {
                if (!string.IsNullOrEmpty(activeFile) && !string.IsNullOrEmpty(marker.FilePath))
                {
                    if (!string.Equals(Path.GetFileName(marker.FilePath), Path.GetFileName(activeFile), StringComparison.OrdinalIgnoreCase))
                        continue;
                }

                try
                {
                    var document = _editor.Document;
                    var startOffset = document.GetOffset(new TextLocation(marker.StartLine, marker.StartColumn));
                    var endOffset = document.GetOffset(new TextLocation(marker.EndLine, marker.EndColumn));
                    var length = endOffset - startOffset;

                    if (length > 0)
                    {
                        var color = marker.IsError ? Colors.Red : Colors.Orange;
                        _textMarkerService.Create(startOffset, length, marker.Message, color);
                        if (marker.IsError)
                            errorCount++;
                    }
                }
                catch { /* Ignore invalid ranges */ }
            }

            UpdateMinimapMarkers();

            if (errorCount > 0)
            {
                SetStatusMessage($"{errorCount} error{(errorCount != 1 ? "s" : "")}", true);
            }
            else
            {
                SetStatusMessage("Ready", false);
            }
        }

        private async Task PerformWorkspaceSyntaxCheckAsync()
        {
            var workspace = GetWorkspace();
            if (workspace == null) return;

            var activeFile = GetActiveFilePath() ?? DefaultFileId;
            var compilation = workspace.GetCompilation();
            var diagnostics = compilation.GetDiagnostics();

            var markers = new List<DiagnosticMarkerInfo>();
            foreach (var diag in diagnostics)
            {
                if (diag.Severity != DiagnosticSeverity.Error && diag.Severity != DiagnosticSeverity.Warning)
                    continue;

                var lineSpan = diag.Location.GetLineSpan();
                bool isMatch = false;

                if (!string.IsNullOrEmpty(lineSpan.Path))
                {
                    isMatch = string.Equals(Path.GetFileName(lineSpan.Path), Path.GetFileName(activeFile), StringComparison.OrdinalIgnoreCase);
                }
                else
                {
                    isMatch = true;
                }

                if (isMatch)
                {
                    markers.Add(new DiagnosticMarkerInfo
                    {
                        FilePath = activeFile,
                        StartLine = lineSpan.StartLinePosition.Line + 1,
                        StartColumn = lineSpan.StartLinePosition.Character + 1,
                        EndLine = lineSpan.EndLinePosition.Line + 1,
                        EndColumn = lineSpan.EndLinePosition.Character + 1,
                        Message = diag.GetMessage(),
                        IsError = diag.Severity == DiagnosticSeverity.Error
                    });
                }
            }

            UpdateErrorMarkers(markers);
        }

        private void UpdateMinimapMarkers()
        {
            if (_textMarkerService == null || _editor.Document == null) return;

            try
            {
                var markers = _textMarkerService.GetMarkers()
                    .Select(m =>
                    {
                        var line = _editor.Document.GetLineByOffset(m.StartOffset);
                        return new MinimapMarker
                        {
                            Line = line.LineNumber,
                            Color = m.MarkerColor ?? Colors.Red,
                            Message = m.Message
                        };
                    })
                    .GroupBy(m => m.Line)
                    .Select(g => g.First())
                    .ToList();

                _minimap?.UpdateMarkers(markers);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UpdateMinimapMarkers error: {ex.Message}");
            }
        }

        public async Task UpdateSemanticHighlightingAsync()
        {
            if (_semanticHighlighter == null) return;
            try
            {
                var workspace = GetWorkspace();
                var activeFile = GetActiveFilePath() ?? DefaultFileId;

                if (workspace != null && !string.IsNullOrEmpty(activeFile))
                {
                    await _semanticHighlighter.UpdateTokensAsync(workspace, activeFile);
                    _editor.TextArea.TextView.Redraw();
                }

                if (_inlayHintGenerator != null && _inlayHintGenerator.Enabled)
                {
                    _inlayHintGenerator.UpdateHints(_editor.Text);
                    _editor.TextArea.TextView.Redraw();
                }

                if (_codeLensGenerator != null && _codeLensGenerator.Enabled)
                {
                    _codeLensGenerator.UpdateCodeLens(_editor.Text);
                    _editor.TextArea.TextView.Redraw();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Semantic highlight error: {ex.Message}");
            }
        }

        private void TriggerSemanticHighlightingUpdate()
        {
            if (_semanticHighlighter == null || !_semanticHighlighter.Enabled) return;
            _semanticUpdateTimer?.Stop();
            _semanticUpdateTimer?.Start();
        }

        // Caret & selection handlers
        private void Caret_PositionChanged(object? sender, EventArgs e)
        {
            if (_bracketRenderer == null) return;
            _bracketRenderer.Result = BracketSearcher.SearchBracket(_editor.Document, _editor.CaretOffset);
            _editor.TextArea.TextView.InvalidateLayer(KnownLayer.Selection);
        }

        private void OnEditorSelectionChanged(object? sender, EventArgs e)
            => _selectionHighlightRenderer?.UpdateSelection(_editor.SelectedText);

        private void OnSelectionChanged_ClearMultiSelect(object? sender, EventArgs e)
        {
            if (_isAddingNextOccurrence || _isMultiCursorEditing) return;
            _multiSelectionRenderer?.ClearSelections();
        }

        // Context Menu Setup
        private void InitializeContextMenu()
        {
            var menu = new ContextMenu();
            menu.Items.Add(new MenuItem { Header = "Cut", Command = ApplicationCommands.Cut });
            menu.Items.Add(new MenuItem { Header = "Copy", Command = ApplicationCommands.Copy });
            menu.Items.Add(new MenuItem { Header = "Paste", Command = ApplicationCommands.Paste });
            menu.Items.Add(new Separator());
            menu.Items.Add(new MenuItem { Header = "Go to Definition", Command = GoToDefinitionCommand, InputGestureText = "F12" });
            menu.Items.Add(new MenuItem { Header = "Peek Definition", Command = PeekDefinitionCommand, InputGestureText = "Alt+F12" });
            menu.Items.Add(new MenuItem { Header = "Find All References", Command = FindAllReferencesCommand, InputGestureText = "Shift+F12" });
            menu.Items.Add(new MenuItem { Header = "Rename", Command = DirectRenameCommand, InputGestureText = "F2" });
            menu.Items.Add(new MenuItem { Header = "Toggle Comment", Command = new RelayCommand(_ => ToggleComment()), InputGestureText = "Ctrl+/" });
            menu.Items.Add(new MenuItem { Header = "Choose Color...", Command = new RelayCommand(_ => InsertColor()), InputGestureText = "Ctrl+Shift+K" });
            
            _editor.ContextMenu = menu;
        }

        // Command executions
        private async void GoToDefinition_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var project = GetActiveProject();
            var filePath = GetActiveFilePath() ?? DefaultFileId;
            if (_refactoringProvider == null) return;

            SetStatusMessage("Finding definition...", false);
            var offset = _editor.CaretOffset;
            var result = await _refactoringProvider.GetDefinitionAsync(project, filePath, offset);

            if (result.Success && result.FilePath != null)
            {
                NavigateToLocation(result.FilePath, result.Line, result.Column);
                SetStatusMessage($"Definition: {result.SymbolKind} {result.SymbolName}", false);
            }
            else
            {
                SetStatusMessage(result.Error ?? "Definition not found", true);
            }
        }

        private async void PeekDefinition_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var project = GetActiveProject();
            var filePath = GetActiveFilePath() ?? DefaultFileId;
            if (_refactoringProvider == null) return;

            ClosePeekPopup();
            SetStatusMessage("Finding definition...", false);
            var offset = _editor.CaretOffset;
            var result = await _refactoringProvider.GetDefinitionAsync(project, filePath, offset);

            if (result.Success && result.FilePath != null)
            {
                ShowPeekDefinition(result);
            }
            else
            {
                SetStatusMessage(result.Error ?? "Definition not found", true);
            }
        }

        private async void FindAllReferences_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var project = GetActiveProject();
            var filePath = GetActiveFilePath() ?? DefaultFileId;
            if (_refactoringProvider == null) return;

            SetStatusMessage("Finding references...", false);
            var offset = _editor.CaretOffset;
            var result = await _refactoringProvider.FindAllReferencesAsync(project, filePath, offset);

            if (result.Success)
            {
                if (result.References.Count == 0)
                {
                    SetStatusMessage("No references found", true);
                    return;
                }

                if (result.References.Count == 1)
                {
                    var singleRef = result.References[0];
                    NavigateToLocation(singleRef.FilePath, singleRef.Line, singleRef.Column);
                    SetStatusMessage($"Found 1 reference to '{result.SymbolName}'", false);
                    return;
                }

                ShowReferences(result.SymbolName ?? "Symbol", result.References);
                SetStatusMessage($"Found {result.References.Count} references to '{result.SymbolName}'", false);
            }
            else
            {
                SetStatusMessage(result.Error ?? "Find references failed", true);
            }
        }

        private async void DocumentSymbols_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var project = GetActiveProject();
            var filePath = GetActiveFilePath() ?? DefaultFileId;
            if (_refactoringProvider == null) return;

            SetStatusMessage("Loading document symbols...", false);
            var result = await _refactoringProvider.GetDocumentSymbolsAsync(project, filePath);

            if (result.Success)
            {
                ShowSymbolPickerPopup(result.Symbols, "Go to Symbol in Editor", false);
            }
            else
            {
                SetStatusMessage(result.Error ?? "Failed to load symbols", true);
            }
        }

        private async void WorkspaceSymbols_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var project = GetActiveProject();
            if (_refactoringProvider == null) return;

            SetStatusMessage("Loading workspace symbols...", false);
            var result = await _refactoringProvider.GetWorkspaceSymbolsAsync(project);

            if (result.Success)
            {
                ShowSymbolPickerPopup(result.Symbols, "Go to Symbol in Workspace", true);
            }
            else
            {
                SetStatusMessage(result.Error ?? "Failed to load symbols", true);
            }
        }

        private void CallHierarchy_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            // Placeholder: Not implemented or host-specific
        }

        private void TypeHierarchy_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            // Placeholder: Not implemented or host-specific
        }

        private void DirectRename_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var offset = _editor.CaretOffset;
            var text = _editor.Text;

            if (offset < 0 || offset > text.Length) return;

            int start = offset;
            int end = offset;

            while (start > 0 && IsIdentifierChar(text[start - 1]))
                start--;

            while (end < text.Length && IsIdentifierChar(text[end]))
                end++;

            if (start == end)
            {
                SetStatusMessage("Place cursor on an identifier to rename", true);
                return;
            }

            var wordToRename = text.Substring(start, end - start);
            PerformRename(wordToRename, offset);
        }

        private static bool IsIdentifierChar(char c) => char.IsLetterOrDigit(c) || c == '_';

        private void PerformRename(string originalName, int offset = -1)
        {
            if (offset < 0)
                offset = _editor.CaretOffset;

            var dialog = new RenameDialog(originalName);
            var window = Window.GetWindow(_editor);
            if (window != null)
                dialog.Owner = window;

            if (dialog.ShowDialog() == true && dialog.NewName != originalName)
            {
                ExecuteRename(dialog.NewName, offset);
            }
        }

        private async void ExecuteRename(string newName, int offset)
        {
            var project = GetActiveProject();
            var filePath = GetActiveFilePath() ?? DefaultFileId;
            if (_refactoringProvider == null) return;

            SetStatusMessage("Renaming...", false);
            string currentContent = _editor.Text;
            var result = await _refactoringProvider.GetRenameEditsAsync(project, filePath, offset, newName, currentContent);

            if (result.Success && result.Changes != null)
            {
                ApplyRefactoring(result.Changes);
                SetStatusMessage("Rename applied", false);
            }
            else
            {
                SetStatusMessage(result.Error ?? "Rename failed", true);
            }
        }

        private async void Rename_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (_refactoringProvider == null) return;

            var currentContent = _editor.Text;
            var offset = _editor.CaretOffset;
            var selectionLength = _editor.SelectionLength;

            SetStatusMessage("Analyzing...", false);
            var project = GetActiveProject();
            var filePath = GetActiveFilePath() ?? DefaultFileId;
            var quickActions = await _refactoringProvider.GetQuickActionsAsync(project, filePath, currentContent, offset, selectionLength);
            SetStatusMessage("Ready", false);

            var contextMenu = new ContextMenu();
            bool hasItems = false;

            foreach (var action in quickActions)
            {
                var item = new MenuItem { Header = action.Title };
                if (action.ActionId == "Rename") item.InputGestureText = "F2";

                item.Click += (s, args) => PerformQuickAction(action);
                contextMenu.Items.Add(item);
                hasItems = true;
            }

            var word = GetWordAtOffset(_editor.Document, offset);
            if (!string.IsNullOrEmpty(word))
            {
                var currentCode = _editor.Text;
                var namespaces = TypeInspector.FindNamespacesForType(word);
                var extensionNamespaces = TypeInspector.FindNamespacesForExtensionMethod(word);
                foreach (var ns in extensionNamespaces)
                {
                    namespaces.Add(ns);
                }

                var newNamespaces = namespaces.Distinct()
                    .Where(ns => !currentCode.Contains($"using {ns};"))
                    .OrderByDescending(n => n.StartsWith("Animator") || n.StartsWith("Code2Viz"))
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

            if (hasItems)
            {
                var textView = _editor.TextArea.TextView;
                var pos = textView.GetVisualPosition(
                    new TextViewPosition(_editor.TextArea.Caret.Line, _editor.TextArea.Caret.Column),
                    ICSharpCode.AvalonEdit.Rendering.VisualYPosition.LineBottom);

                pos = new System.Windows.Point(pos.X - textView.ScrollOffset.X, pos.Y - textView.ScrollOffset.Y);

                contextMenu.PlacementTarget = textView;
                contextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.RelativePoint;
                contextMenu.HorizontalOffset = pos.X;
                contextMenu.VerticalOffset = pos.Y;
                contextMenu.IsOpen = true;
                SetStatusMessage("Quick actions available", false);
            }
            else
            {
                SetStatusMessage("No quick actions available", false);
            }
        }

        private async void PerformQuickAction(RefactoringProvider.QuickActionItem action)
        {
            var project = GetActiveProject();
            var filePath = GetActiveFilePath() ?? DefaultFileId;
            if (_refactoringProvider == null) return;

            if (action.ActionId == "Rename")
            {
                if (action.Data.TryGetValue("Name", out var name))
                {
                    PerformRename(name);
                }
                else
                {
                    var nameFromTitle = action.Title.Substring("Rename to '".Length);
                    nameFromTitle = nameFromTitle.Substring(0, nameFromTitle.Length - 1);
                    PerformRename(nameFromTitle);
                }
            }
            else if (action.ActionId == "FixFormatting")
            {
                try 
                {
                    var newText = Code2Viz.Editor.CodeFormatter.Format(_editor.Text);
                    _editor.Document.Replace(0, _editor.Document.TextLength, newText);
                }
                catch (Exception ex)
                {
                    SetStatusMessage($"Formatting failed: {ex.Message}", true);
                }
            }
            else if (action.ActionId == "GenerateMethod")
            {
                if (action.Data.TryGetValue("MethodName", out var methodName))
                {
                    action.Data.TryGetValue("IsStatic", out var isStaticStr);
                    bool isStatic = isStaticStr == "True";
                    action.Data.TryGetValue("Parameters", out var parameters);
                    parameters ??= "";
                    action.Data.TryGetValue("ReturnType", out var returnType);
                    returnType = string.IsNullOrEmpty(returnType) ? "void" : returnType;
                    
                    var staticModifier = isStatic ? "static " : "";
                    var stub = $"\r\n\r\n        private {staticModifier}{returnType} {methodName}({parameters})\r\n        {{\r\n            throw new NotImplementedException();\r\n        }}";
                    
                    var text = _editor.Text;
                    var braceCount = 0;
                    var insertPosition = -1;
                    
                    for (int i = text.Length - 1; i >= 0; i--)
                    {
                        if (text[i] == '}')
                        {
                            braceCount++;
                            if (braceCount == 2)
                            {
                                insertPosition = i;
                                break;
                            }
                        }
                    }
                    
                    if (insertPosition > 0)
                    {
                        _editor.Document.Insert(insertPosition, stub);
                    }
                    else if (text.LastIndexOf('}') > 0)
                    {
                        _editor.Document.Insert(text.LastIndexOf('}'), stub);
                    }
                }
            }
            else if (CustomQuickActionHandler != null)
            {
                await CustomQuickActionHandler(action);
            }
        }

        private void AddUsingStatement(string ns)
        {
            var document = _editor.Document;
            var text = document.Text;
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
                    break;
                }
            }

            string textToInsert = $"using {ns};\r\n";

            if (lastUsingLine >= 0)
            {
                var lineObj = document.GetLineByNumber(lastUsingLine + 1);
                insertOffset = lineObj.EndOffset;
                textToInsert = "\r\n" + $"using {ns};";
            }

            document.Insert(insertOffset, textToInsert);
            SetStatusMessage($"Added using {ns};", false);
        }

        // Completion & Tooltips Handlers
        private void OnTextEntered(object sender, TextCompositionEventArgs e)
        {
            if (e.Text == null || e.Text.Length == 0) return;
            var ch = e.Text[0];

            switch (ch)
            {
                case '(': AutoInsertClosingBracket(')'); break;
                case '[': AutoInsertClosingBracket(']'); break;
                case '{': AutoInsertClosingBracket('}'); break;
                case '"': AutoInsertClosingQuote('"'); break;
                case '\'': AutoInsertClosingQuote('\''); break;
            }

            if (ch == '.')
            {
                ShowCompletionWindow(autoTrigger: true);
            }
            else if (ch == ' ' && _completionWindow == null && IsAfterCompletionKeyword())
            {
                // "new " / "is " / "as " — list candidate types up front instead of waiting
                // for the user to type a character into the empty-prefix slot.
                ShowCompletionWindow(autoTrigger: true);
            }
            else if (_completionWindow == null && (char.IsLetter(ch) || ch == '_'))
            {
                var caret = _editor.CaretOffset;
                // Default: trigger from the 2nd character of an identifier (caret-2 is a word
                // char). Also trigger on the 1st character when the preceding token is a
                // completion-priming keyword like `new`, so `new V` immediately lists types
                // instead of waiting for the 2nd letter.
                bool trigger = caret >= 2 && (char.IsLetterOrDigit(_editor.Document.GetCharAt(caret - 2)) || _editor.Document.GetCharAt(caret - 2) == '_');
                if (!trigger && IsAfterCompletionKeyword(skipBack: 1))
                    trigger = true;
                if (trigger)
                    ShowCompletionWindow(autoTrigger: true);
            }

            if (ch == '(' || ch == ',')
            {
                _ = ShowSignatureHelpAsync();
            }

            if (ch == '}')
            {
                FormatCurrentLineOnType();
            }
        }

        /// <summary>
        /// Returns true when the caret sits immediately after a completion-priming keyword
        /// like `new`, `is`, or `as` (with intervening whitespace). Used to fire completion
        /// on the space following the keyword, and on the first letter of the type name.
        /// </summary>
        /// <param name="skipBack">Characters to ignore at the caret tail (1 when probing
        /// from `OnTextEntered` for the letter the user just typed).</param>
        private bool IsAfterCompletionKeyword(int skipBack = 0)
        {
            var doc = _editor.Document;
            int i = _editor.CaretOffset - skipBack;
            // Walk back over the trailing space the trigger itself emitted, or the gap
            // before the just-typed letter.
            while (i > 0 && doc.GetCharAt(i - 1) == ' ') i--;
            // Pull the preceding word.
            int end = i;
            while (i > 0)
            {
                char c = doc.GetCharAt(i - 1);
                if (char.IsLetterOrDigit(c) || c == '_') i--;
                else break;
            }
            if (end == i) return false;
            var word = doc.GetText(i, end - i);
            return word == "new" || word == "is" || word == "as";
        }

        private void OnTextEntering(object sender, TextCompositionEventArgs e)
        {
            // Wrap selection with brackets
            if (e.Text.Length == 1 && !_editor.TextArea.Selection.IsEmpty)
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
        }

        private void OnEditorPreviewKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Tab && _snippetSession is { IsActive: true })
            {
                if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0)
                    _snippetSession.MoveToPreviousPlaceholder();
                else
                    _snippetSession.MoveToNextPlaceholder();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Escape && _snippetSession is { IsActive: true })
            {
                _snippetSession.EndSession();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Return && (Keyboard.Modifiers & ~ModifierKeys.Shift) == 0)
            {
                if (HandleAutoIndentEnter())
                    e.Handled = true;
                return;
            }

            var mods = Keyboard.Modifiers;
            if (mods == ModifierKeys.Alt)
            {
                switch (e.SystemKey)
                {
                    case Key.Up:   MoveLineUp();   e.Handled = true; return;
                    case Key.Down: MoveLineDown(); e.Handled = true; return;
                }
            }
            else if (mods == (ModifierKeys.Alt | ModifierKeys.Shift))
            {
                switch (e.SystemKey)
                {
                    case Key.Up:   CopyLineUp();   e.Handled = true; return;
                    case Key.Down: CopyLineDown(); e.Handled = true; return;
                }
            }
            else if (mods == ModifierKeys.Control)
            {
                switch (e.Key)
                {
                    case Key.OemQuestion: // Ctrl+/
                        ToggleComment();
                        e.Handled = true;
                        return;
                    case Key.D: // Ctrl+D
                        AddNextOccurrence();
                        e.Handled = true;
                        return;
                }
            }
            else if (mods == (ModifierKeys.Control | ModifierKeys.Shift))
            {
                switch (e.Key)
                {
                    case Key.K: // Ctrl+Shift+K
                        InsertColor();
                        e.Handled = true;
                        return;
                    case Key.D: // Ctrl+Shift+D
                        DeleteLine();
                        e.Handled = true;
                        return;
                    case Key.L: // Ctrl+Shift+L
                        SelectAllOccurrences();
                        e.Handled = true;
                        return;
                    case Key.F: // Ctrl+Shift+F
                        FormatAll();
                        e.Handled = true;
                        return;
                }
            }
            else if (mods == (ModifierKeys.Shift | ModifierKeys.Alt))
            {
                var actualKey = e.Key == Key.System ? e.SystemKey : e.Key;
                switch (actualKey)
                {
                    case Key.Right:
                        ExpandSelection();
                        e.Handled = true;
                        return;
                    case Key.Left:
                        ShrinkSelection();
                        e.Handled = true;
                        return;
                }
            }
            else if (mods == (ModifierKeys.Control | ModifierKeys.Alt))
            {
                var actualKey = e.Key == Key.System ? e.SystemKey : e.Key;
                switch (actualKey)
                {
                    case Key.Up:
                        AddCursorAbove();
                        e.Handled = true;
                        return;
                    case Key.Down:
                        AddCursorBelow();
                        e.Handled = true;
                        return;
                }
            }
        }

        // Completion implementation
        private async void ShowCompletionWindow(bool autoTrigger)
        {
            if (_completionWindow != null) return;

            var offset = _editor.CaretOffset;
            var code = _editor.Text;
            var activeFile = GetActiveFilePath() ?? DefaultFileId;
            var workspace = GetWorkspace();

            try
            {
                List<ICompletionData> completions = new List<ICompletionData>();
                bool isAfterNew = false;
                string prefix = "";
                string? expectedType = null;

                // Unified semantic completion for both apps (Code2Viz multi-file project and the
                // Animator single-file sketch). The workspace carries the right references and, in
                // Animator, the injected global-usings tree; `activeFile` is the workspace's file-id
                // key in both hosts (Animator: CompletionEngine.FileId), so the correct syntax tree
                // and semantic model are used. This replaces Animator's old, much simpler
                // CompletionEngine path — it now gets fuzzy matching, scope/expected-type sorting,
                // the doc sidecar, and noise filtering, same as Code2Viz.
                if (workspace != null)
                {
                    var service = new RoslynCompletionService(workspace);
                    (completions, isAfterNew, prefix, expectedType) = await service.GetCompletionsAsync(code, offset, workspace, activeFile);
                }

                if (completions.Count > 0)
                {
                    _completionWindow = new CompletionWindow(_editor.TextArea);
                    _completionWindow.StartOffset = offset - prefix.Length;
                    var data = _completionWindow.CompletionList.CompletionData;

                    // Fuzzy-filter + rank (scope priority, expected-type/after-new boosting, score).
                    // Now shared by both apps since Animator uses RoslynCompletionService too.
                    var sorted = SortCompletions(completions, prefix, isAfterNew, expectedType);

                    foreach (var item in sorted)
                    {
                        data.Add(item);
                    }

                    // Add snippets
                    var isMemberAccess = offset > prefix.Length && code.Length > offset - prefix.Length - 1 && code[offset - prefix.Length - 1] == '.';
                    if (!isAfterNew && !isMemberAccess)
                    {
                        foreach (var (trigger, description) in CodeSnippets.GetAll())
                        {
                            data.Add(new SnippetCompletionData(trigger, description, CodeSnippets.GetSnippet(trigger)!));
                        }
                    }

                    StyleCompletionWindow(_completionWindow);
                    _docSidecar?.TrackCompletionWindow(_completionWindow);

                    // Show the selected item's documentation beside the list as the user navigates.
                    _completionWindow.CompletionList.ListBox.SelectionChanged += (s, e) =>
                    {
                        if (_completionWindow?.CompletionList.SelectedItem is CompletionData sel && sel.Symbol != null)
                            _docSidecar?.ShowForItem(sel);
                        else
                            _docSidecar?.Hide();
                    };
                    _completionWindow.Loaded += (s, e) =>
                    {
                        _docSidecar?.UpdatePosition();
                        if (_completionWindow?.CompletionList.SelectedItem is CompletionData init && init.Symbol != null)
                            _docSidecar?.ShowForItem(init);
                    };

                    _completionWindow.Show();

                    _completionWindow.Closed += (s, e) =>
                    {
                        _completionWindow = null;
                        _docSidecar?.Close();
                    };
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ShowCompletionWindow error: {ex.Message}");
            }
        }

        private List<ICompletionData> SortCompletions(List<ICompletionData> completions, string prefix, bool isAfterNew, string? expectedType)
        {
            var matchQuality = new Dictionary<ICompletionData, int?>();
            foreach (var item in completions)
            {
                matchQuality[item] = string.IsNullOrEmpty(prefix) 
                    ? 0 
                    : FuzzyMatcher.Score(prefix, item.Text);
            }

            return completions
                .Where(item => string.IsNullOrEmpty(prefix) || matchQuality[item] != null)
                .OrderByDescending(item => item.Text.Equals(expectedType, StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(item => isAfterNew && item is CompletionData cd && cd.Kind == CompletionKind.Type)
                .ThenByDescending(item => item is CompletionData cd2 ? (int)cd2.Scope == 0 : false)
                .ThenByDescending(item => matchQuality[item] ?? 0)
                .ThenBy(item => item.Text, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private async Task ShowSignatureHelpAsync()
        {
            if (_insightWindow != null) return;

            var offset = _editor.CaretOffset;
            var code = _editor.Text;
            var workspace = GetWorkspace();

            try
            {
#if !ANIMATOR
                if (workspace != null)
                {
                    var activeFile = GetActiveFilePath() ?? "";
                    var service = new RoslynCompletionService(workspace);
                    var (signatures, currentParamIndex) = await service.GetSignatureHelpAsync(code, offset);

                    if (signatures.Count > 0)
                    {
                        _insightWindow = new OverloadInsightWindow(_editor.TextArea);
                        _insightWindow.Provider = new SignatureHelpProvider(signatures, currentParamIndex);
                        _insightWindow.StartOffset = offset;
                        _insightWindow.EndOffset = _editor.Document.TextLength;
                        
                        StyleInsightWindow(_insightWindow);
                        _insightWindow.Show();
                        _insightWindow.Closed += (s, e) => _insightWindow = null;
                    }
                }
#else
                // In Animator, signature help can be built directly using compilation SemanticModel
                if (workspace != null)
                {
                    var tree = workspace.GetSyntaxTree(DefaultFileId);
                    var model = workspace.GetSemanticModel(DefaultFileId);
                    if (tree != null && model != null)
                    {
                        var service = new RoslynCompletionService(workspace.GetCompilation().References);
                        var (signatures, currentParamIndex) = await service.GetSignatureHelpAsync(code, offset);
                        
                        if (signatures.Count > 0)
                        {
                            _insightWindow = new OverloadInsightWindow(_editor.TextArea);
                            _insightWindow.Provider = new SignatureHelpProvider(signatures, currentParamIndex);
                            _insightWindow.StartOffset = offset;
                            _insightWindow.EndOffset = _editor.Document.TextLength;
                            
                            StyleInsightWindow(_insightWindow);
                            _insightWindow.Show();
                            _insightWindow.Closed += (s, e) => _insightWindow = null;
                        }
                    }
                }
#endif
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ShowSignatureHelp error: {ex}");
            }
        }

        // Mouse hover tooltips
        private void Editor_MouseHover(object sender, MouseEventArgs e)
        {
            // Drop any previous hover tooltip before we consider showing a new one — every
            // ToolTip we open here is force-shown via IsOpen=true and would otherwise linger.
            CloseHoverTip();

            var pos = _editor.GetPositionFromPoint(e.GetPosition(_editor));
            if (pos == null) return;

            var offset = _editor.Document.GetOffset(pos.Value.Line, pos.Value.Column);

            // 1. Diagnostics Tooltips (Highest priority)
            if (_textMarkerService != null)
            {
                var marker = _textMarkerService.GetMarkerAtOffset(offset);
                if (marker != null && !string.IsNullOrEmpty(marker.Message))
                {
                    ShowErrorTooltip(e, marker.Message);
                    return;
                }
            }

            // 2. Roslyn Symbol Quick Info Tooltip
            _ = ShowQuickInfoTooltipAsync(e, offset);
        }

        private void Editor_MouseHoverStopped(object sender, MouseEventArgs e) => CloseHoverTip();

        private void CloseHoverTip()
        {
            if (_activeHoverTip != null)
            {
                _activeHoverTip.IsOpen = false;
                _activeHoverTip = null;
            }
            ToolTipService.SetToolTip(_editor, null);
        }

        private void ShowHoverTip(ToolTip tip, MouseEventArgs e)
        {
            CloseHoverTip();
            _activeHoverTip = tip;
            tip.Closed += (s, _) =>
            {
                if (ReferenceEquals(_activeHoverTip, tip))
                    _activeHoverTip = null;
            };
            ToolTipService.SetToolTip(_editor, tip);
            tip.IsOpen = true;
            e.Handled = true;
        }

        private void ShowErrorTooltip(MouseEventArgs e, string message)
        {
            var tooltip = new ToolTip
            {
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(224, 79, 95)), // Red border
                BorderThickness = new Thickness(1),
                Padding = new Thickness(6, 4, 6, 4),
                Content = new TextBlock
                {
                    Text = message,
                    Foreground = Brushes.White,
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 400
                }
            };
            ShowHoverTip(tooltip, e);
        }

        private async Task ShowQuickInfoTooltipAsync(MouseEventArgs e, int offset)
        {
            var workspace = GetWorkspace();
            if (workspace == null) return;

            var activeFile = GetActiveFilePath() ?? DefaultFileId;
            var syntaxTree = workspace.GetSyntaxTree(activeFile);
            var model = workspace.GetSemanticModel(activeFile);
            if (syntaxTree == null || model == null) return;

            try
            {
                var root = await syntaxTree.GetRootAsync();
                var node = root.FindToken(offset).Parent;
                if (node == null) return;

                ISymbol? symbol = model.GetSymbolInfo(node).Symbol ?? model.GetDeclaredSymbol(node);
                if (symbol == null) return;

                var display = symbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
                var xmlDoc = symbol.GetDocumentationCommentXml();
                var desc = "";
                if (!string.IsNullOrEmpty(xmlDoc))
                {
                    try
                    {
                        var doc = new XmlDocument();
                        doc.LoadXml(xmlDoc);
                        desc = doc.SelectSingleNode("//summary")?.InnerText.Trim() ?? "";
                    }
                    catch { }
                }

                // HSL styling based on symbol type
                var borderBrush = GetSymbolColor(symbol);

                var stack = new StackPanel();
                stack.Children.Add(new TextBlock
                {
                    Text = display,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White,
                    FontFamily = _editor.FontFamily
                });

                if (!string.IsNullOrEmpty(desc))
                {
                    stack.Children.Add(new Separator { Margin = new Thickness(0, 4, 0, 4), Background = new SolidColorBrush(Color.FromRgb(60, 60, 60)) });
                    stack.Children.Add(new TextBlock
                    {
                        Text = desc,
                        Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                        FontStyle = FontStyles.Italic,
                        TextWrapping = TextWrapping.Wrap,
                        MaxWidth = 350,
                        FontFamily = _editor.FontFamily
                    });
                }

                var tooltip = new ToolTip
                {
                    Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                    BorderBrush = borderBrush,
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(8, 6, 8, 6),
                    Content = stack
                };

                // The async Roslyn lookup that produced this tooltip may have completed after
                // the user already moved the mouse away — bail out instead of opening a stale tip.
                if (!_editor.IsMouseOver) return;

                ShowHoverTip(tooltip, e);
            }
            catch { }
        }

        private Brush GetSymbolColor(ISymbol symbol)
        {
            return symbol switch
            {
                INamedTypeSymbol t when t.TypeKind == TypeKind.Interface => new SolidColorBrush(Color.FromRgb(184, 215, 163)), // Interface (Greenish)
                INamedTypeSymbol => new SolidColorBrush(Color.FromRgb(78, 201, 176)), // Class/Struct (Cyan)
                IMethodSymbol => new SolidColorBrush(Color.FromRgb(220, 220, 170)), // Method (Yellow)
                IPropertySymbol or IFieldSymbol => new SolidColorBrush(Color.FromRgb(156, 220, 254)), // Property/Field (Light Blue)
                _ => new SolidColorBrush(Color.FromRgb(0, 122, 204)) // Accent blue fallback
            };
        }

        // Navigation and Peek Definition Popup
        private void ShowPeekDefinition(RefactoringProvider.DefinitionResult result)
        {
            var project = GetActiveProject();
            if (result.FilePath == null) return;

            string content = "";
#if !ANIMATOR
            if (project != null)
            {
                var file = project.Files.FirstOrDefault(f => f.FilePath.Equals(result.FilePath, StringComparison.OrdinalIgnoreCase) || Path.GetFileName(f.FilePath).Equals(Path.GetFileName(result.FilePath), StringComparison.OrdinalIgnoreCase));
                if (file != null) content = file.Content;
            }
            else
#endif
            {
                // Animator fallback: read file from disk
                if (File.Exists(result.FilePath))
                    content = File.ReadAllText(result.FilePath);
                else if (string.Equals(Path.GetFileName(result.FilePath), DefaultFileId, StringComparison.OrdinalIgnoreCase))
                    content = _editor.Text;
            }

            if (string.IsNullOrEmpty(content))
            {
                SetStatusMessage($"File not found: {Path.GetFileName(result.FilePath)}", true);
                return;
            }

            var lines = content.Split('\n');
            var startLine = Math.Max(0, result.Line - 6);
            var endLine = Math.Min(lines.Length, result.Line + 15);
            var contextText = string.Join("\n", lines.Skip(startLine).Take(endLine - startLine));

            _peekPopup = new System.Windows.Controls.Primitives.Popup
            {
                PlacementTarget = _editor,
                Placement = System.Windows.Controls.Primitives.PlacementMode.Relative,
                StaysOpen = false,
                AllowsTransparency = true
            };

            var visualPos = _editor.TextArea.TextView.GetVisualPosition(
                new TextViewPosition(_editor.TextArea.Caret.Line, _editor.TextArea.Caret.Column),
                ICSharpCode.AvalonEdit.Rendering.VisualYPosition.LineBottom);

            _peekPopup.HorizontalOffset = 50;
            _peekPopup.VerticalOffset = visualPos.Y + 5;

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

            // Preview Editor
            var previewEditor = new TextEditor
            {
                Text = contextText,
                IsReadOnly = true,
                ShowLineNumbers = true,
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                Foreground = Brushes.White,
                FontFamily = _editor.FontFamily,
                FontSize = _editor.FontSize - 1,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(5)
            };
            previewEditor.SyntaxHighlighting = _editor.SyntaxHighlighting;
            previewEditor.TextArea.TextView.LineTransformers.Clear();

            var defLineInContext = result.Line - startLine - 1;
            if (defLineInContext > 0 && defLineInContext <= previewEditor.Document.LineCount)
            {
                previewEditor.ScrollToLine(defLineInContext);
                var defLineObj = previewEditor.Document.GetLineByNumber(Math.Min(defLineInContext + 1, previewEditor.Document.LineCount));
                previewEditor.Select(defLineObj.Offset, defLineObj.Length);
            }

            Grid.SetRow(previewEditor, 1);
            grid.Children.Add(previewEditor);

            // Footer
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
            footerPanel.Children.Add(new TextBlock
            {
                Text = "Press Escape to close",
                Foreground = new SolidColorBrush(Color.FromRgb(128, 128, 128)),
                VerticalAlignment = VerticalAlignment.Center
            });
            footer.Child = footerPanel;
            Grid.SetRow(footer, 2);
            grid.Children.Add(footer);

            border.Child = grid;
            _peekPopup.Child = border;

            _peekPopup.KeyDown += (s, args) =>
            {
                if (args.Key == Key.Escape)
                {
                    ClosePeekPopup();
                    _editor.Focus();
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

            SetStatusMessage($"Peek: {result.SymbolKind} {result.SymbolName} in {Path.GetFileName(result.FilePath)}:{result.Line}", false);
        }

        private void ClosePeekPopup()
        {
            if (_peekPopup != null)
            {
                _peekPopup.IsOpen = false;
                _peekPopup = null;
            }
        }

        // Symbol Picker Popup
        private void ShowSymbolPickerPopup(List<RefactoringProvider.DocumentSymbol> symbols, string title, bool showFilePath)
        {
            CloseSymbolsPopup();

            _symbolsPopup = new System.Windows.Controls.Primitives.Popup
            {
                PlacementTarget = _editor,
                Placement = System.Windows.Controls.Primitives.PlacementMode.Center,
                StaysOpen = false,
                AllowsTransparency = true
            };

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

            // Filter Textbox
            var filterBox = new TextBox
            {
                Background = new SolidColorBrush(Color.FromRgb(45, 45, 45)),
                Foreground = Brushes.White,
                CaretBrush = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(10, 5, 10, 10),
                Padding = new Thickness(5, 3, 5, 3),
                FontFamily = _editor.FontFamily
            };
            Grid.SetRow(filterBox, 1);
            grid.Children.Add(filterBox);

            // Symbols ListBox
            var listBox = new ListBox
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Margin = new Thickness(0, 0, 0, 10)
            };
            ScrollViewer.SetHorizontalScrollBarVisibility(listBox, ScrollBarVisibility.Disabled);
            ScrollViewer.SetVerticalScrollBarVisibility(listBox, ScrollBarVisibility.Auto);

            var itemStyle = new Style(typeof(ListBoxItem));
            itemStyle.Setters.Add(new Setter(ListBoxItem.BackgroundProperty, Brushes.Transparent));
            itemStyle.Setters.Add(new Setter(ListBoxItem.BorderThicknessProperty, new Thickness(0)));
            itemStyle.Setters.Add(new Setter(ListBoxItem.PaddingProperty, new Thickness(10, 5, 10, 5)));
            
            var selectedTrigger = new Trigger { Property = ListBoxItem.IsSelectedProperty, Value = true };
            selectedTrigger.Setters.Add(new Setter(ListBoxItem.BackgroundProperty, new SolidColorBrush(Color.FromRgb(0, 122, 204))));
            itemStyle.Triggers.Add(selectedTrigger);

            var hoverTrigger = new MultiTrigger();
            hoverTrigger.Conditions.Add(new Condition(ListBoxItem.IsMouseOverProperty, true));
            hoverTrigger.Conditions.Add(new Condition(ListBoxItem.IsSelectedProperty, false));
            hoverTrigger.Setters.Add(new Setter(ListBoxItem.BackgroundProperty, new SolidColorBrush(Color.FromRgb(45, 45, 48))));
            itemStyle.Triggers.Add(hoverTrigger);

            listBox.ItemContainerStyle = itemStyle;

            // Populate initial list
            void UpdateFilteredList(string filter)
            {
                listBox.Items.Clear();
                var lowerFilter = filter.ToLowerInvariant();
                foreach (var symbol in symbols)
                {
                    if (string.IsNullOrEmpty(filter) || symbol.Name.ToLowerInvariant().Contains(lowerFilter) || symbol.Kind.ToLowerInvariant().Contains(lowerFilter))
                    {
                        var itemPanel = new Grid();
                        itemPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                        itemPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                        var iconBlock = new TextBlock
                        {
                            Text = GetSymbolKindIcon(symbol.Kind),
                            Foreground = GetSymbolKindColor(symbol.Kind),
                            FontFamily = new FontFamily("Segoe UI Emoji"),
                            Margin = new Thickness(0, 0, 8, 0)
                        };
                        Grid.SetColumn(iconBlock, 0);
                        itemPanel.Children.Add(iconBlock);

                        var nameStack = new StackPanel { Orientation = Orientation.Horizontal };
                        nameStack.Children.Add(new TextBlock { Text = symbol.Name, Foreground = Brushes.White, FontWeight = FontWeights.Medium });
                        
                        var typeInfo = $"  ({symbol.Kind})";
                        if (showFilePath && !string.IsNullOrEmpty(symbol.FilePath))
                        {
                            typeInfo += $" in {Path.GetFileName(symbol.FilePath)}";
                        }
                        nameStack.Children.Add(new TextBlock { Text = typeInfo, Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 150)) });
                        
                        Grid.SetColumn(nameStack, 1);
                        itemPanel.Children.Add(nameStack);

                        var wrapper = new ListBoxItem { Content = itemPanel, Tag = symbol };
                        listBox.Items.Add(wrapper);
                    }
                }

                if (listBox.Items.Count > 0)
                {
                    listBox.SelectedIndex = 0;
                }
            }

            UpdateFilteredList("");

            filterBox.TextChanged += (s, args) => UpdateFilteredList(filterBox.Text);

            Grid.SetRow(listBox, 2);
            grid.Children.Add(listBox);

            border.Child = grid;
            _symbolsPopup.Child = border;

            // Handlers
            void CommitSelection()
            {
                if (listBox.SelectedItem is ListBoxItem item && item.Tag is RefactoringProvider.DocumentSymbol sym)
                {
                    CloseSymbolsPopup();
                    NavigateToLocation(sym.FilePath ?? GetActiveFilePath() ?? DefaultFileId, sym.Line, sym.Column);
                    _editor.Focus();
                }
            }

            filterBox.PreviewKeyDown += (s, args) =>
            {
                if (args.Key == Key.Down)
                {
                    if (listBox.SelectedIndex < listBox.Items.Count - 1)
                    {
                        listBox.SelectedIndex++;
                        listBox.ScrollIntoView(listBox.SelectedItem);
                    }
                    args.Handled = true;
                }
                else if (args.Key == Key.Up)
                {
                    if (listBox.SelectedIndex > 0)
                    {
                        listBox.SelectedIndex--;
                        listBox.ScrollIntoView(listBox.SelectedItem);
                    }
                    args.Handled = true;
                }
                else if (args.Key == Key.Enter)
                {
                    CommitSelection();
                    args.Handled = true;
                }
                else if (args.Key == Key.Escape)
                {
                    CloseSymbolsPopup();
                    _editor.Focus();
                    args.Handled = true;
                }
            };

            listBox.MouseDoubleClick += (s, args) => CommitSelection();

            _symbolsPopup.Closed += (s, args) => _symbolsPopup = null;
            _symbolsPopup.IsOpen = true;
            filterBox.Focus();
        }

        private string GetSymbolKindIcon(string kind)
        {
            return kind.ToLowerInvariant() switch
            {
                "class" => "🗂️",
                "interface" => "🧱",
                "struct" => "📦",
                "enum" => "🔢",
                "method" => "⚡",
                "property" => "🔧",
                "field" => "🏷️",
                _ => "📝"
            };
        }

        private Brush GetSymbolKindColor(string kind)
        {
            return kind.ToLowerInvariant() switch
            {
                "class" or "struct" => new SolidColorBrush(Color.FromRgb(78, 201, 176)),
                "interface" => new SolidColorBrush(Color.FromRgb(184, 215, 163)),
                "method" => new SolidColorBrush(Color.FromRgb(220, 220, 170)),
                "property" or "field" => new SolidColorBrush(Color.FromRgb(156, 220, 254)),
                _ => Brushes.White
            };
        }

        private void CloseSymbolsPopup()
        {
            if (_symbolsPopup != null)
            {
                _symbolsPopup.IsOpen = false;
                _symbolsPopup = null;
            }
        }

        // Visual helper styling
        private void StyleCompletionWindow(CompletionWindow window)
        {
            try
            {
                var darkBg = new SolidColorBrush(Color.FromRgb(30, 30, 30));
                var borderColor = new SolidColorBrush(Color.FromRgb(60, 60, 60));

                if (window.FindResource("SecondaryBackgroundBrush") is Brush bg)
                {
                    window.Background = bg;
                    window.CompletionList.Background = bg;
                }
                else
                {
                    window.Background = darkBg;
                    window.CompletionList.Background = darkBg;
                }

                if (window.FindResource("BorderBrush") is Brush border)
                {
                    window.BorderBrush = border;
                }
                else
                {
                    window.BorderBrush = borderColor;
                }

                if (window.FindResource("ForegroundBrush") is Brush fg)
                {
                    window.Foreground = fg;
                    window.CompletionList.Foreground = fg;
                }
                else
                {
                    window.CompletionList.Foreground = Brushes.White;
                }

                var listBox = window.CompletionList.ListBox;
                if (listBox != null)
                {
                    var hoverBrush = new SolidColorBrush(Color.FromRgb(45, 45, 48));
                    listBox.Background = window.CompletionList.Background;
                    listBox.BorderThickness = new Thickness(0);

                    var itemStyle = new Style(typeof(ListBoxItem));
                    itemStyle.Setters.Add(new Setter(ListBoxItem.PaddingProperty, new Thickness(4, 2, 4, 2)));
                    itemStyle.Setters.Add(new Setter(ListBoxItem.MarginProperty, new Thickness(0)));
                    itemStyle.Setters.Add(new Setter(ListBoxItem.BorderThicknessProperty, new Thickness(0)));

                    var selectedTrigger = new Trigger { Property = ListBoxItem.IsSelectedProperty, Value = true };
                    selectedTrigger.Setters.Add(new Setter(ListBoxItem.BackgroundProperty, new SolidColorBrush(Color.FromRgb(0, 122, 204))));
                    selectedTrigger.Setters.Add(new Setter(ListBoxItem.ForegroundProperty, Brushes.White));
                    itemStyle.Triggers.Add(selectedTrigger);

                    var hoverTrigger = new MultiTrigger();
                    hoverTrigger.Conditions.Add(new Condition(ListBoxItem.IsMouseOverProperty, true));
                    hoverTrigger.Conditions.Add(new Condition(ListBoxItem.IsSelectedProperty, false));
                    hoverTrigger.Setters.Add(new Setter(ListBoxItem.BackgroundProperty, hoverBrush));
                    itemStyle.Triggers.Add(hoverTrigger);

                    listBox.ItemContainerStyle = itemStyle;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"StyleCompletionWindow error: {ex.Message}");
            }
        }

        private void StyleInsightWindow(OverloadInsightWindow window)
        {
            try
            {
                var darkBg = new SolidColorBrush(Color.FromRgb(30, 30, 30));
                var borderColor = new SolidColorBrush(Color.FromRgb(0, 122, 204)); // Blue border

                if (window.FindResource("SecondaryBackgroundBrush") is Brush bg)
                {
                    window.Background = bg;
                }
                else
                {
                    window.Background = darkBg;
                }

                if (window.FindResource("BorderBrush") is Brush border)
                {
                    window.BorderBrush = border;
                }
                else
                {
                    window.BorderBrush = borderColor;
                }

                window.BorderThickness = new Thickness(1);
                window.Foreground = Brushes.White;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"StyleInsightWindow error: {ex.Message}");
            }
        }

        // Bracket insertions
        private void AutoInsertClosingBracket(char ch)
        {
            var offset = _editor.CaretOffset;
            _editor.Document.Insert(offset, ch.ToString());
            _editor.CaretOffset = offset;
        }

        private void AutoInsertClosingQuote(char ch)
        {
            var offset = _editor.CaretOffset;
            var text = _editor.Text;
            if (offset < text.Length && text[offset] == ch)
            {
                _editor.CaretOffset = offset + 1;
            }
            else
            {
                _editor.Document.Insert(offset, ch.ToString());
                _editor.CaretOffset = offset;
            }
        }

        private void SkipOverClosingBracket(char ch)
        {
            var offset = _editor.CaretOffset;
            var text = _editor.Text;
            if (offset < text.Length && text[offset] == ch)
            {
                _editor.CaretOffset = offset + 1;
            }
        }

        private void FormatCurrentLineOnType()
        {
            var lineNum = _editor.TextArea.Caret.Line;
            var lineObj = _editor.Document.GetLineByNumber(lineNum);
            var lineText = _editor.Document.GetText(lineObj.Offset, lineObj.Length);
            var trimmed = lineText.Trim();

            if (trimmed == "}")
            {
                HandleClosingBraceIndent();
            }
        }

        private void HandleClosingBraceIndent()
        {
            var lineNum = _editor.TextArea.Caret.Line;
            if (lineNum <= 1) return;

            var document = _editor.Document;
            var lineObj = document.GetLineByNumber(lineNum);
            var lineText = document.GetText(lineObj.Offset, lineObj.Length);
            var trimmed = lineText.Trim();

            if (trimmed != "}") return;

            // Find matching open brace line
            int openBraceLine = -1;
            int depth = 0;

            for (int i = lineNum - 1; i >= 1; i--)
            {
                var prevLine = document.GetLineByNumber(i);
                var prevText = document.GetText(prevLine.Offset, prevLine.Length);

                if (prevText.Contains("}")) depth++;
                if (prevText.Contains("{"))
                {
                    if (depth == 0)
                    {
                        openBraceLine = i;
                        break;
                    }
                    else
                    {
                        depth--;
                    }
                }
            }

            if (openBraceLine >= 1)
            {
                var openLineObj = document.GetLineByNumber(openBraceLine);
                var openText = document.GetText(openLineObj.Offset, openLineObj.Length);
                var indent = GetLineIndentation(openText);

                document.Replace(lineObj.Offset, lineText.Length - trimmed.Length + 1, indent + "}");
            }
        }

        private string GetLineIndentation(string lineText)
        {
            int i = 0;
            while (i < lineText.Length && (lineText[i] == ' ' || lineText[i] == '\t'))
            {
                i++;
            }
            return lineText.Substring(0, i);
        }

        private bool HandleAutoIndentEnter()
        {
            var document = _editor.Document;
            var offset = _editor.CaretOffset;
            if (offset < 0 || offset > document.TextLength) return false;
            var line = document.GetLineByOffset(offset);
            var lineText = document.GetText(line.Offset, line.Length);

            var currentIndent = GetLineIndentation(lineText);
            var trimmedLine = lineText.Trim();
            var newIndent = currentIndent;

            if (trimmedLine.EndsWith("{"))
                newIndent += "    ";

            document.Insert(offset, Environment.NewLine + newIndent);
            return true;
        }

        // Line manipulations (port from MainWindow)
        private (int Start, int End) GetSelectedLineRange()
        {
            var document = _editor.Document;
            var textArea = _editor.TextArea;
            var selection = textArea.Selection;

            if (selection.IsEmpty)
            {
                var ln = textArea.Caret.Line;
                return (ln, ln);
            }

            var startLine = document.GetLineByOffset(selection.SurroundingSegment.Offset).LineNumber;
            var endLine = document.GetLineByOffset(selection.SurroundingSegment.EndOffset).LineNumber;
            var endLineObj = document.GetLineByNumber(endLine);
            if (selection.SurroundingSegment.EndOffset == endLineObj.Offset && endLine > startLine)
                endLine--;
            return (startLine, endLine);
        }

        private void MoveLineUp()
        {
            if (!_editor.IsKeyboardFocusWithin) return;
            var document = _editor.Document;
            var textArea = _editor.TextArea;
            var (startLine, endLine) = GetSelectedLineRange();
            if (startLine <= 1) return;

            var firstLine = document.GetLineByNumber(startLine);
            var lastLine = document.GetLineByNumber(endLine);
            var lineAbove = document.GetLineByNumber(startLine - 1);

            var selectedText = document.GetText(firstLine.Offset, lastLine.EndOffset - firstLine.Offset);
            var aboveText = document.GetText(lineAbove.Offset, lineAbove.Length);

            document.BeginUpdate();
            try
            {
                int blockStart = lineAbove.Offset;
                int blockLength = lastLine.EndOffset - lineAbove.Offset;
                document.Replace(blockStart, blockLength, selectedText + Environment.NewLine + aboveText);
            }
            finally { document.EndUpdate(); }

            var newFirstLine = document.GetLineByNumber(startLine - 1);
            var newLastLine = document.GetLineByNumber(endLine - 1);
            textArea.Caret.Position = new TextViewPosition(startLine - 1, 1);
            textArea.Selection = AvalonSelection.Create(textArea, newFirstLine.Offset, newLastLine.EndOffset);
        }

        private void MoveLineDown()
        {
            if (!_editor.IsKeyboardFocusWithin) return;
            var document = _editor.Document;
            var textArea = _editor.TextArea;
            var (startLine, endLine) = GetSelectedLineRange();
            if (endLine >= document.LineCount) return;

            var firstLine = document.GetLineByNumber(startLine);
            var lastLine = document.GetLineByNumber(endLine);
            var lineBelow = document.GetLineByNumber(endLine + 1);

            var selectedText = document.GetText(firstLine.Offset, lastLine.EndOffset - firstLine.Offset);
            var belowText = document.GetText(lineBelow.Offset, lineBelow.Length);

            document.BeginUpdate();
            try
            {
                int blockStart = firstLine.Offset;
                int blockLength = lineBelow.EndOffset - firstLine.Offset;
                document.Replace(blockStart, blockLength, belowText + Environment.NewLine + selectedText);
            }
            finally { document.EndUpdate(); }

            var newFirstLine = document.GetLineByNumber(startLine + 1);
            var newLastLine = document.GetLineByNumber(endLine + 1);
            textArea.Caret.Position = new TextViewPosition(startLine + 1, 1);
            textArea.Selection = AvalonSelection.Create(textArea, newFirstLine.Offset, newLastLine.EndOffset);
        }

        private void CopyLineDown()
        {
            if (!_editor.IsKeyboardFocusWithin) return;
            var document = _editor.Document;
            var textArea = _editor.TextArea;
            var (startLine, endLine) = GetSelectedLineRange();

            var firstLine = document.GetLineByNumber(startLine);
            var lastLine = document.GetLineByNumber(endLine);
            var textToDuplicate = document.GetText(firstLine.Offset, lastLine.EndOffset - firstLine.Offset);

            document.Insert(lastLine.EndOffset, Environment.NewLine + textToDuplicate);
            var lineCount = endLine - startLine + 1;
            textArea.Caret.Line = textArea.Caret.Line + lineCount;
        }

        private void CopyLineUp()
        {
            if (!_editor.IsKeyboardFocusWithin) return;
            var document = _editor.Document;
            var (startLine, endLine) = GetSelectedLineRange();

            var firstLine = document.GetLineByNumber(startLine);
            var lastLine = document.GetLineByNumber(endLine);
            var textToDuplicate = document.GetText(firstLine.Offset, lastLine.EndOffset - firstLine.Offset);

            document.Insert(firstLine.Offset, textToDuplicate + Environment.NewLine);
        }

        private void ToggleComment()
        {
            var document = _editor.Document;
            var (startLine, endLine) = GetSelectedLineRange();

            document.BeginUpdate();
            try
            {
                bool allCommented = true;
                for (int i = startLine; i <= endLine; i++)
                {
                    var line = document.GetLineByNumber(i);
                    var lineText = document.GetText(line.Offset, line.Length).TrimStart();
                    if (!lineText.StartsWith("//"))
                    {
                        allCommented = false;
                        break;
                    }
                }

                for (int i = startLine; i <= endLine; i++)
                {
                    var line = document.GetLineByNumber(i);
                    var lineText = document.GetText(line.Offset, line.Length);

                    if (allCommented)
                    {
                        int commentIndex = lineText.IndexOf("//");
                        if (commentIndex >= 0)
                        {
                            document.Remove(line.Offset + commentIndex, 2);
                        }
                    }
                    else
                    {
                        document.Insert(line.Offset, "//");
                    }
                }
            }
            finally { document.EndUpdate(); }
        }

        public void FormatAll()
        {
            try
            {
                var caret = _editor.CaretOffset;
                var text = CodeFormatter.Format(_editor.Text);
                _editor.Text = text;
                if (caret <= _editor.Text.Length)
                    _editor.CaretOffset = caret;
                SetStatusMessage("Formatted document", false);
            }
            catch (Exception ex)
            {
                SetStatusMessage($"Format error: {ex.Message}", true);
            }
        }

        private void DeleteLine()
        {
            var document = _editor.Document;
            var (startLine, endLine) = GetSelectedLineRange();

            document.BeginUpdate();
            try
            {
                var firstLine = document.GetLineByNumber(startLine);
                var lastLine = document.GetLineByNumber(endLine);
                int startOffset = firstLine.Offset;
                int endOffset = lastLine.EndOffset;

                if (endLine < document.LineCount)
                {
                    endOffset = document.GetLineByNumber(endLine + 1).Offset;
                }
                else if (startLine > 1)
                {
                    startOffset = document.GetLineByNumber(startLine - 1).EndOffset;
                }

                document.Remove(startOffset, endOffset - startOffset);
            }
            finally { document.EndUpdate(); }
        }

        private void InsertColor()
        {
            var dialog = new ColorPickerDialog();
            var window = Window.GetWindow(_editor);
            if (window != null)
                dialog.Owner = window;

            if (dialog.ShowDialog() == true)
            {
                var hexColor = dialog.SelectedColor;
                var formatString = $"Color.FromHex(\"{hexColor}\")";
                
                var caretOffset = _editor.CaretOffset;
                _editor.Document.Insert(caretOffset, formatString);
                _editor.CaretOffset = caretOffset + formatString.Length;
            }
        }

        private void WrapSelectionWith(char open, char close)
        {
            var selection = _editor.TextArea.Selection;
            var selectedText = selection.GetText();
            var document = _editor.Document;

            var startOffset = selection.SurroundingSegment.Offset;
            var length = selection.SurroundingSegment.Length;

            var wrappedText = $"{open}{selectedText}{close}";
            document.Replace(startOffset, length, wrappedText);

            // Clear selection and position caret after the closing bracket
            _editor.TextArea.ClearSelection();
            _editor.CaretOffset = startOffset + wrappedText.Length;
        }

        // Multi-cursor functionality
        private void AddCursorAbove()
        {
            if (_multiSelectionRenderer == null) return;
            var caret = _editor.TextArea.Caret;
            if (caret.Line > 1)
            {
                int newOffset = _editor.Document.GetOffset(caret.Line - 1, caret.Column);
                _multiSelectionRenderer.AddSelection(newOffset, newOffset);
                _editor.TextArea.TextView.InvalidateLayer(KnownLayer.Selection);
            }
        }

        private void AddCursorBelow()
        {
            if (_multiSelectionRenderer == null) return;
            var caret = _editor.TextArea.Caret;
            if (caret.Line < _editor.Document.LineCount)
            {
                int newOffset = _editor.Document.GetOffset(caret.Line + 1, caret.Column);
                _multiSelectionRenderer.AddSelection(newOffset, newOffset);
                _editor.TextArea.TextView.InvalidateLayer(KnownLayer.Selection);
            }
        }

        private void AddNextOccurrence()
        {
            if (_multiSelectionRenderer == null) return;
            
            var selectedText = _editor.SelectedText;
            if (string.IsNullOrEmpty(selectedText))
            {
                // Select word under caret
                var offset = _editor.CaretOffset;
                var word = GetWordAtOffset(_editor.Document, offset);
                if (!string.IsNullOrEmpty(word))
                {
                    int wordStart = _editor.Text.IndexOf(word, Math.Max(0, offset - word.Length));
                    if (wordStart >= 0)
                    {
                        _editor.Select(wordStart, word.Length);
                        selectedText = word;
                    }
                }
            }

            if (string.IsNullOrEmpty(selectedText)) return;

            int searchFrom = _editor.SelectionStart + _editor.SelectionLength;
            if (searchFrom >= _editor.Text.Length) searchFrom = 0;

            int nextOffset = _editor.Text.IndexOf(selectedText, searchFrom, StringComparison.Ordinal);
            if (nextOffset < 0 && searchFrom > 0)
            {
                nextOffset = _editor.Text.IndexOf(selectedText, 0, searchFrom, StringComparison.Ordinal);
            }

            if (nextOffset >= 0)
            {
                _isAddingNextOccurrence = true;
                try
                {
                    if (!_multiSelectionRenderer.HasSelections)
                    {
                        _multiSelectionRenderer.AddSelection(_editor.SelectionStart, _editor.SelectionStart + _editor.SelectionLength);
                    }
                    _multiSelectionRenderer.AddSelection(nextOffset, nextOffset + selectedText.Length);
                    _editor.TextArea.TextView.InvalidateLayer(KnownLayer.Selection);
                }
                finally
                {
                    _isAddingNextOccurrence = false;
                }
            }
        }

        private void SelectAllOccurrences()
        {
            if (_multiSelectionRenderer == null) return;

            var selectedText = _editor.SelectedText;
            if (string.IsNullOrEmpty(selectedText)) return;

            _isAddingNextOccurrence = true;
            try
            {
                _multiSelectionRenderer.ClearSelections();
                int idx = 0;
                while ((idx = _editor.Text.IndexOf(selectedText, idx, StringComparison.Ordinal)) >= 0)
                {
                    _multiSelectionRenderer.AddSelection(idx, idx + selectedText.Length);
                    idx += selectedText.Length;
                }
                _editor.TextArea.TextView.InvalidateLayer(KnownLayer.Selection);
            }
            finally
            {
                _isAddingNextOccurrence = false;
            }
        }

        private void ExpandSelection()
        {
            // Placeholder: Could be implemented via syntax trees if needed
        }

        private void ShrinkSelection()
        {
            // Placeholder: Could be implemented if needed
        }

        private void TextArea_TextEntering_MultiCursor(object? sender, TextCompositionEventArgs e)
        {
            if (_multiSelectionRenderer == null || !_multiSelectionRenderer.HasSelections) return;
            if (e.Text.Length > 0 && !char.IsControl(e.Text[0]))
            {
                _isMultiCursorEditing = true;
                try
                {
                    _multiSelectionRenderer.InsertTextAtAllCursors(e.Text);
                    e.Handled = true;
                }
                finally
                {
                    _isMultiCursorEditing = false;
                }
            }
        }

        private void TextArea_PreviewMouseDown_ClearMultiSelect(object sender, MouseButtonEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) == 0 && (Keyboard.Modifiers & ModifierKeys.Alt) == 0)
            {
                _multiSelectionRenderer?.ClearSelections();
            }
        }

        private string GetWordAtOffset(ICSharpCode.AvalonEdit.Document.TextDocument document, int offset)
        {
            if (offset < 0 || offset > document.TextLength) return "";
            
            int start = offset;
            while (start > 0 && IsIdentifierChar(document.GetCharAt(start - 1)))
                start--;

            int end = offset;
            while (end < document.TextLength && IsIdentifierChar(document.GetCharAt(end)))
                end++;

            if (start == end) return "";
            return document.GetText(start, end - start);
        }

        private void Editor_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                double fontSize = _editor.FontSize + (e.Delta > 0 ? 1 : -1);
                if (fontSize >= 6 && fontSize <= 48)
                {
                    _editor.FontSize = fontSize;
                }
                e.Handled = true;
            }
        }
    }

    internal sealed class RelayCommand : ICommand
    {
        private readonly Action<object?> _exec;
        public RelayCommand(Action<object?> exec) { _exec = exec; }
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _exec(parameter);
    }
}
