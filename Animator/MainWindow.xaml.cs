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
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Folding;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using ICSharpCode.AvalonEdit.Rendering;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Win32;
using Animator.Canvas;
using Animator.Compiler;
using Animator.Console;
using Animator.Editor;
using Animator.Sketching;
using Code2Viz.Editor;
using AvalonSelection = ICSharpCode.AvalonEdit.Editing.Selection;
using TextDocument = ICSharpCode.AvalonEdit.Document.TextDocument;

namespace Animator;

public partial class MainWindow : Window
{
    private readonly SketchCompiler _compiler = new();
    private readonly CompletionEngine _completion = new();
    private CompletionWindow? _completionWindow;
    private string? _currentPath;
    private TimeSpan _lastRenderTime = TimeSpan.Zero;
    private bool _isDirty;
    private bool _suppressDirty;

    // ── Editor extensions (all linked from Code2Viz.Editor) ──
    private BracketHighlightRenderer? _bracketRenderer;
    private SelectionHighlightRenderer? _selectionHighlightRenderer;
    private MultiSelectionRenderer? _multiSelectionRenderer;
    private SemanticHighlighter? _semanticHighlighter;
    private InlayHintGenerator? _inlayHintGenerator;
    private SnippetSession? _snippetSession;
    private DocumentationSidecar? _docSidecar;
    private FoldingManager? _foldingManager;
    private BraceFoldingStrategy? _foldingStrategy;
    private DispatcherTimer? _foldingTimer;
    private DispatcherTimer? _semanticUpdateTimer;
    private OverloadInsightWindow? _insightWindow;

    // Re-entry guards to keep selection-change handlers from clobbering multi-cursor state.
    private bool _isAddingNextOccurrence;
    // Reserved for future multi-cursor edit guards (keystroke broadcast, paste, etc).
#pragma warning disable CS0649
    private bool _isMultiCursorEditing;
#pragma warning restore CS0649

    public MainWindow()
    {
        InitializeComponent();

        LoadSyntaxHighlighting();
        Editor.Text = Templates.DefaultSketch;
        UpdateFileLabel();

        InitializeEditor();

        ConsoleOutput.Instance.Changed += (s, e) => Dispatcher.Invoke(RefreshConsole);

        int lastLabelUpdate = -1;
        SketchRuntime.Instance.FrameProduced += shapes =>
        {
            if (CheckAccess())
            {
                Canvas.SetShapes(shapes);
                var fc = SketchRuntime.Instance.Active?.FrameCount ?? 0;
                if (fc - lastLabelUpdate >= 6 || fc == 0)
                {
                    FrameLabel.Text = $"frame {fc}";
                    lastLabelUpdate = fc;
                }
            }
            else
            {
                Dispatcher.BeginInvoke((Action)(() =>
                {
                    Canvas.SetShapes(shapes);
                    FrameLabel.Text = $"frame {SketchRuntime.Instance.Active?.FrameCount ?? 0}";
                }));
            }
        };

        CompositionTarget.Rendering += OnRendering;

        Editor.TextChanged += (s, e) =>
        {
            if (_suppressDirty) return;
            if (!_isDirty)
            {
                _isDirty = true;
                UpdateFileLabel();
            }
        };

        Closing += MainWindow_Closing;
        Closed += (s, e) =>
        {
            CompositionTarget.Rendering -= OnRendering;
            SketchRuntime.Instance.Stop();
        };

        // Keyboard shortcuts
        InputBindings.Add(new KeyBinding(new RelayCommand(_ => RunSketch()),         Key.F5,    ModifierKeys.None));
        InputBindings.Add(new KeyBinding(new RelayCommand(_ => StopSketch()),        Key.F5,    ModifierKeys.Shift));
        InputBindings.Add(new KeyBinding(new RelayCommand(_ => ToggleRun()),         Key.Enter, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(new RelayCommand(_ => Save()),              Key.S,     ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(new RelayCommand(_ => Open()),              Key.O,     ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(new RelayCommand(_ => New()),               Key.N,     ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(new RelayCommand(_ => FormatAll()),         Key.F,     ModifierKeys.Control | ModifierKeys.Shift));
        InputBindings.Add(new KeyBinding(new RelayCommand(_ => AddNextOccurrence()), Key.D,     ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(new RelayCommand(_ => SelectAllOccurrences()), Key.L,  ModifierKeys.Control | ModifierKeys.Shift));
    }

    // ── Editor initialization ────────────────────────────────────────────────

    private void InitializeEditor()
    {
        // Keep the completion engine's compilation in sync with the editor's text.
        _completion.Update(Editor.Text);
        Editor.TextChanged += (s, e) =>
        {
            _completion.Update(Editor.Text);
            TriggerSemanticHighlightingUpdate();
        };

        // Background renderers (layered visuals).
        _bracketRenderer = new BracketHighlightRenderer(Editor.TextArea.TextView);
        Editor.TextArea.TextView.BackgroundRenderers.Add(_bracketRenderer);

        _selectionHighlightRenderer = new SelectionHighlightRenderer(Editor.TextArea.TextView);
        Editor.TextArea.TextView.BackgroundRenderers.Add(_selectionHighlightRenderer);

        _multiSelectionRenderer = new MultiSelectionRenderer(Editor.TextArea.TextView);
        Editor.TextArea.TextView.BackgroundRenderers.Add(_multiSelectionRenderer);

        // Caret + selection events for the renderers above.
        Editor.TextArea.Caret.PositionChanged += Caret_PositionChanged;
        Editor.TextArea.SelectionChanged += OnEditorSelectionChanged;
        Editor.TextArea.SelectionChanged += OnSelectionChanged_ClearMultiSelect;

        // Semantic highlighting — shares the CompletionEngine's incremental workspace.
        _semanticHighlighter = new SemanticHighlighter(Editor.Document);
        Editor.TextArea.TextView.LineTransformers.Add(_semanticHighlighter);
        _semanticHighlighter.Enabled = true;

        _semanticUpdateTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _semanticUpdateTimer.Tick += async (s, e) =>
        {
            _semanticUpdateTimer!.Stop();
            await UpdateSemanticHighlightingAsync();
        };
        // Run once at startup so the default template is highlighted before the first edit.
        _ = UpdateSemanticHighlightingAsync();

        // Inlay hints — keep disabled by default to match Code2Viz; users can flip later.
        _inlayHintGenerator = new InlayHintGenerator(Editor.Document);
        Editor.TextArea.TextView.ElementGenerators.Add(_inlayHintGenerator);
        _inlayHintGenerator.Enabled = false;

        // Folding.
        _foldingManager = FoldingManager.Install(Editor.TextArea);
        _foldingStrategy = new BraceFoldingStrategy();
        _foldingTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _foldingTimer.Tick += (s, e) =>
        {
            try { _foldingStrategy.UpdateFoldings(_foldingManager, Editor.Document); }
            catch (Exception ex) { ConsoleOutput.Instance.WriteLine("Editor", $"Folding error: {ex.Message}"); }
        };
        _foldingTimer.Start();
        try { _foldingStrategy.UpdateFoldings(_foldingManager, Editor.Document); } catch { }

        // Snippet session (Tab-driven placeholder navigation).
        _snippetSession = new SnippetSession(Editor);
        SnippetCompletionData.ActiveSession = _snippetSession;

        // Doc sidecar — floats beside the completion window.
        _docSidecar = new DocumentationSidecar();

        // Auto-trigger completion on '.' and identifier chars; manual via Ctrl+Space.
        Editor.TextArea.TextEntered += OnTextEntered;
        Editor.TextArea.TextEntering += OnTextEntering;
        Editor.TextArea.PreviewKeyDown += OnEditorPreviewKeyDown;
        InputBindings.Add(new KeyBinding(new RelayCommand(_ => ShowCompletionWindow(autoTrigger: false)),
            Key.Space, ModifierKeys.Control));

        // Minimap.
        try { EditorMinimap.AttachToEditor(Editor); }
        catch (Exception ex) { ConsoleOutput.Instance.WriteLine("Editor", $"Minimap attach failed: {ex.Message}"); }
    }

    private void Caret_PositionChanged(object? sender, EventArgs e)
    {
        if (_bracketRenderer == null) return;
        _bracketRenderer.Result = BracketSearcher.SearchBracket(Editor.Document, Editor.CaretOffset);
        Editor.TextArea.TextView.InvalidateLayer(KnownLayer.Selection);
    }

    private void OnEditorSelectionChanged(object? sender, EventArgs e)
        => _selectionHighlightRenderer?.UpdateSelection(Editor.SelectedText);

    private void OnSelectionChanged_ClearMultiSelect(object? sender, EventArgs e)
    {
        if (_isAddingNextOccurrence || _isMultiCursorEditing) return;
        _multiSelectionRenderer?.ClearSelections();
    }

    private void TriggerSemanticHighlightingUpdate()
    {
        if (_semanticHighlighter == null || !_semanticHighlighter.Enabled) return;
        _semanticUpdateTimer?.Stop();
        _semanticUpdateTimer?.Start();
    }

    private async Task UpdateSemanticHighlightingAsync()
    {
        if (_semanticHighlighter == null) return;
        try
        {
            await _semanticHighlighter.UpdateTokensAsync(_completion.Workspace, CompletionEngine.FileId);
            Editor.TextArea.TextView.Redraw();

            if (_inlayHintGenerator != null && _inlayHintGenerator.Enabled)
            {
                _inlayHintGenerator.UpdateHints(Editor.Text);
                Editor.TextArea.TextView.Redraw();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Semantic highlight error: {ex}");
        }
    }

    // ── Text-entry handlers ──────────────────────────────────────────────────

    private void OnTextEntered(object sender, TextCompositionEventArgs e)
    {
        if (e.Text == null || e.Text.Length == 0) return;
        var ch = e.Text[0];

        // Auto-close matching bracket / quote.
        switch (ch)
        {
            case '(': AutoInsertClosingBracket(')'); break;
            case '[': AutoInsertClosingBracket(']'); break;
            case '{': AutoInsertClosingBracket('}'); break;
            case '"': AutoInsertClosingBracket('"'); break;
        }

        // Completion trigger.
        if (ch == '.')
        {
            ShowCompletionWindow(autoTrigger: true);
        }
        else if (_completionWindow == null && (char.IsLetter(ch) || ch == '_'))
        {
            var caret = Editor.CaretOffset;
            if (caret >= 2 && (char.IsLetterOrDigit(Editor.Document.GetCharAt(caret - 2)) || Editor.Document.GetCharAt(caret - 2) == '_'))
                ShowCompletionWindow(autoTrigger: true);
        }

        // Signature help on `(` or `,`.
        if (ch == '(' || ch == ',')
        {
            _ = ShowSignatureHelpAsync();
        }

        // Format current line when a `}` lands at the end of the line.
        if (ch == '}')
        {
            FormatCurrentLineOnType();
        }
    }

    private void OnTextEntering(object sender, TextCompositionEventArgs e)
    {
        // Let AvalonEdit's default behaviour handle commit/close when the user types
        // a non-identifier character while a completion window is open.
        _ = _completionWindow; _ = e;
    }

    private void OnEditorPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        // Tab cycles snippet placeholders when a session is active.
        if (e.Key == Key.Tab && _snippetSession is { IsActive: true })
        {
            if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0)
                _snippetSession.MoveToPreviousPlaceholder();
            else
                _snippetSession.MoveToNextPlaceholder();
            e.Handled = true;
            return;
        }

        // Escape ends snippet session.
        if (e.Key == Key.Escape && _snippetSession is { IsActive: true })
        {
            _snippetSession.EndSession();
            e.Handled = true;
            return;
        }

        // Enter → auto-indent based on previous line and surrounding braces.
        if (e.Key == Key.Return && (Keyboard.Modifiers & ~ModifierKeys.Shift) == 0)
        {
            if (HandleAutoIndentEnter())
                e.Handled = true;
        }
    }

    private bool HandleAutoIndentEnter()
    {
        var document = Editor.Document;
        var offset = Editor.CaretOffset;
        if (offset < 0 || offset > document.TextLength) return false;
        var line = document.GetLineByOffset(offset);
        var lineText = document.GetText(line.Offset, line.Length);

        var currentIndent = GetLineIndentation(lineText);
        var trimmedLine = lineText.Trim();
        var newIndent = currentIndent;

        if (trimmedLine.EndsWith("{"))
            newIndent += "    ";

        var afterCursor = document.GetText(offset, line.EndOffset - offset).Trim();
        if (trimmedLine.EndsWith("{") && afterCursor.StartsWith("}"))
        {
            document.Insert(offset, "\n" + newIndent + "\n" + currentIndent);
            Editor.CaretOffset = offset + 1 + newIndent.Length;
            return true;
        }

        document.Insert(offset, "\n" + newIndent);
        Editor.CaretOffset = offset + 1 + newIndent.Length;
        return true;
    }

    private static string GetLineIndentation(string lineText)
    {
        int i = 0;
        while (i < lineText.Length && (lineText[i] == ' ' || lineText[i] == '\t')) i++;
        return lineText.Substring(0, i);
    }

    private void AutoInsertClosingBracket(char closingBracket)
    {
        var offset = Editor.CaretOffset;
        if (offset < Editor.Document.TextLength)
        {
            var nextChar = Editor.Document.GetCharAt(offset);
            if (nextChar == closingBracket || char.IsLetterOrDigit(nextChar))
                return;
        }
        Editor.Document.Insert(offset, closingBracket.ToString());
        Editor.CaretOffset = offset;
    }

    // ── Completion window ───────────────────────────────────────────────────

    private void ShowCompletionWindow(bool autoTrigger)
    {
        if (_completionWindow != null)
        {
            _completionWindow.Close();
            _completionWindow = null;
        }

        var caret = Editor.CaretOffset;
        _completion.Update(Editor.Text);
        var items = new List<ICompletionData>();
        items.AddRange(_completion.GetCompletions(caret));

        // Add snippets unless we're in a member-access context (`.`).
        var atMemberAccess = caret > 0 && Editor.Document.GetCharAt(caret - 1) == '.';
        if (!atMemberAccess)
        {
            foreach (var (trigger, description) in CodeSnippets.GetAll())
            {
                var code = CodeSnippets.GetSnippet(trigger);
                if (code != null)
                    items.Add(new SnippetCompletionData(trigger, description, code));
            }
        }

        if (items.Count == 0) return;

        var window = new CompletionWindow(Editor.TextArea)
        {
            Background = (Brush)FindResource("BackgroundBrush"),
            Foreground = (Brush)FindResource("ForegroundBrush"),
            BorderBrush = (Brush)FindResource("BorderBrush"),
            CloseAutomatically = true,
            CloseWhenCaretAtBeginning = true,
        };
        var list = window.CompletionList.CompletionData;
        foreach (var item in items)
            list.Add(item);

        // Seed the filter with any partial identifier already typed.
        int prefixStart = caret;
        while (prefixStart > 0)
        {
            var c = Editor.Document.GetCharAt(prefixStart - 1);
            if (!char.IsLetterOrDigit(c) && c != '_') break;
            prefixStart--;
        }
        if (prefixStart < caret)
        {
            window.StartOffset = prefixStart;
            window.CompletionList.SelectItem(Editor.Document.GetText(prefixStart, caret - prefixStart));
        }

        // Hook the doc sidecar.
        if (_docSidecar != null)
        {
            _docSidecar.TrackCompletionWindow(window);
            window.CompletionList.SelectionChanged += (_, __) =>
            {
                if (window.CompletionList.SelectedItem is CompletionData cd)
                    _docSidecar.ShowForItem(cd);
                else
                    _docSidecar.Hide();
            };
        }

        window.Closed += (_, __) =>
        {
            _docSidecar?.Close();
            _completionWindow = null;
        };
        window.Show();
        _completionWindow = window;
    }

    // ── Signature help ──────────────────────────────────────────────────────

    private async Task ShowSignatureHelpAsync()
    {
        if (_insightWindow != null) return;

        var caret = Editor.CaretOffset;
        try
        {
            _completion.Update(Editor.Text);
            var (signatures, paramIndex) = await Task.Run(() => GetSignatureHelp(caret));
            if (signatures.Count == 0) return;

            _insightWindow = new OverloadInsightWindow(Editor.TextArea)
            {
                Background = (Brush)FindResource("BackgroundBrush"),
                Foreground = (Brush)FindResource("ForegroundBrush"),
            };
            _insightWindow.Provider = new SignatureHelpProvider(signatures, paramIndex);
            _insightWindow.StartOffset = caret;
            _insightWindow.EndOffset = Editor.Document.TextLength;
            _insightWindow.Closed += (_, __) => _insightWindow = null;
            _insightWindow.Show();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ShowSignatureHelp error: {ex}");
        }
    }

    /// <summary>
    /// Minimal signature-help extraction: finds the innermost InvocationExpression that contains
    /// the caret, asks Roslyn for candidate overloads, and formats each as a string. The current
    /// parameter index comes from counting commas at the current argument list depth.
    /// </summary>
    private (List<string> Signatures, int ParamIndex) GetSignatureHelp(int caretOffset)
    {
        var tree = _completion.Workspace.GetSyntaxTree(CompletionEngine.FileId);
        var model = _completion.Workspace.GetSemanticModel(CompletionEngine.FileId);
        if (tree == null || model == null) return (new(), 0);

        var root = tree.GetRoot();
        var token = root.FindToken(Math.Max(0, Math.Min(caretOffset, root.FullSpan.End - 1)));
        var invocation = token.Parent?.AncestorsAndSelf().OfType<InvocationExpressionSyntax>().FirstOrDefault();
        if (invocation == null) return (new(), 0);

        var args = invocation.ArgumentList;
        if (args == null) return (new(), 0);

        var paramIndex = 0;
        foreach (var sep in args.Arguments.GetSeparators())
        {
            if (sep.SpanStart < caretOffset) paramIndex++;
            else break;
        }

        var sigs = new List<string>();
        var info = model.GetSymbolInfo(invocation);
        var candidates = info.Symbol != null
            ? new[] { info.Symbol }
            : info.CandidateSymbols.ToArray();

        foreach (var sym in candidates.OfType<IMethodSymbol>().Distinct(SymbolEqualityComparer.Default).Cast<IMethodSymbol>())
        {
            sigs.Add(sym.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
        }
        return (sigs, paramIndex);
    }

    // ── Format ──────────────────────────────────────────────────────────────

    private void FormatAll()
    {
        try
        {
            var caret = Editor.CaretOffset;
            var formatted = CodeFormatter.Format(Editor.Text);
            if (formatted == Editor.Text) return;
            Editor.Text = formatted;
            if (caret <= Editor.Text.Length) Editor.CaretOffset = caret;
        }
        catch (Exception ex)
        {
            ConsoleOutput.Instance.WriteLine("Editor", $"Format error: {ex.Message}");
        }
    }

    private void FormatCurrentLineOnType()
    {
        try
        {
            var document = Editor.Document;
            var line = document.GetLineByOffset(Editor.CaretOffset);
            var lineText = document.GetText(line.Offset, line.Length);
            var trimmed = lineText.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("//")) return;

            var formatted = CodeFormatter.Format(lineText).TrimEnd('\r', '\n');
            if (formatted != lineText)
            {
                var caretInLine = Editor.CaretOffset - line.Offset;
                document.Replace(line.Offset, line.Length, formatted);
                var newOffset = line.Offset + Math.Min(caretInLine + (formatted.Length - lineText.Length), formatted.Length);
                Editor.CaretOffset = Math.Max(line.Offset, Math.Min(newOffset, line.Offset + formatted.Length));
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"FormatCurrentLineOnType error: {ex.Message}");
        }
    }

    // ── Multi-cursor commands ───────────────────────────────────────────────

    private void AddNextOccurrence()
    {
        if (_multiSelectionRenderer == null) return;
        if (!Editor.IsKeyboardFocusWithin) return;

        var textArea = Editor.TextArea;
        var selection = textArea.Selection;
        string searchText;
        int searchStart;

        if (selection.IsEmpty)
        {
            // Select the word under the caret.
            var (start, length) = GetWordAt(Editor.Document, Editor.CaretOffset);
            if (length == 0) return;
            searchText = Editor.Document.GetText(start, length);
            searchStart = start + length;
            _isAddingNextOccurrence = true;
            textArea.Selection = AvalonSelection.Create(textArea, start, start + length);
            textArea.Caret.Offset = start + length;
            _isAddingNextOccurrence = false;
            return;
        }

        searchText = textArea.Selection.GetText();
        if (string.IsNullOrEmpty(searchText)) return;
        searchStart = textArea.Selection.SurroundingSegment.EndOffset;

        var fullText = Editor.Text;
        var nextIndex = fullText.IndexOf(searchText, searchStart, StringComparison.Ordinal);
        if (nextIndex < 0) nextIndex = fullText.IndexOf(searchText, 0, StringComparison.Ordinal);
        if (nextIndex < 0 || nextIndex == textArea.Selection.SurroundingSegment.Offset) return;

        // Move existing main selection into the multi-selection set, then promote the new one.
        var prev = textArea.Selection.SurroundingSegment;
        _multiSelectionRenderer.AddSelection(prev.Offset, prev.Length);

        _isAddingNextOccurrence = true;
        textArea.Selection = AvalonSelection.Create(textArea, nextIndex, nextIndex + searchText.Length);
        textArea.Caret.Offset = nextIndex + searchText.Length;
        _isAddingNextOccurrence = false;
        textArea.Caret.BringCaretToView();
    }

    private void SelectAllOccurrences()
    {
        if (_multiSelectionRenderer == null) return;
        if (!Editor.IsKeyboardFocusWithin) return;

        var textArea = Editor.TextArea;
        string searchText;
        if (textArea.Selection.IsEmpty)
        {
            var (start, length) = GetWordAt(Editor.Document, Editor.CaretOffset);
            if (length == 0) return;
            searchText = Editor.Document.GetText(start, length);
        }
        else
        {
            searchText = textArea.Selection.GetText();
        }
        if (string.IsNullOrEmpty(searchText)) return;

        var fullText = Editor.Text;
        var occurrences = new List<(int Start, int End)>();
        int idx = 0;
        while ((idx = fullText.IndexOf(searchText, idx, StringComparison.Ordinal)) >= 0)
        {
            occurrences.Add((idx, idx + searchText.Length));
            idx += searchText.Length;
        }
        if (occurrences.Count == 0) return;

        _isAddingNextOccurrence = true;
        try
        {
            _multiSelectionRenderer.ClearSelections();
            for (int i = 0; i < occurrences.Count - 1; i++)
                _multiSelectionRenderer.AddSelection(occurrences[i].Start, occurrences[i].End - occurrences[i].Start);

            var last = occurrences[^1];
            textArea.Selection = AvalonSelection.Create(textArea, last.Start, last.End);
            textArea.Caret.Offset = last.End;
            textArea.Caret.BringCaretToView();
        }
        finally
        {
            _isAddingNextOccurrence = false;
        }
    }

    private static (int Start, int Length) GetWordAt(TextDocument doc, int offset)
    {
        if (doc.TextLength == 0) return (0, 0);
        int start = Math.Min(offset, doc.TextLength - 1);
        while (start > 0)
        {
            var c = doc.GetCharAt(start - 1);
            if (!char.IsLetterOrDigit(c) && c != '_') break;
            start--;
        }
        int end = offset;
        while (end < doc.TextLength)
        {
            var c = doc.GetCharAt(end);
            if (!char.IsLetterOrDigit(c) && c != '_') break;
            end++;
        }
        return (start, end - start);
    }

    // ── Syntax highlighting + console ──

    private void LoadSyntaxHighlighting()
    {
        try
        {
            var asm = typeof(MainWindow).Assembly;
            using var stream = asm.GetManifestResourceStream("Animator.CSharpHighlighting.xshd");
            if (stream == null)
            {
                Editor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("C#");
                ConsoleOutput.Instance.WriteLine("Editor",
                    "CSharpHighlighting.xshd resource not found; using default C# theme.");
                return;
            }
            using var reader = new XmlTextReader(stream);
            Editor.SyntaxHighlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
        }
        catch (Exception ex)
        {
            Editor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("C#");
            ConsoleOutput.Instance.WriteLine("Editor", $"Failed to load XSHD: {ex.Message}");
        }
    }

    private void RefreshConsole()
    {
        var snap = ConsoleOutput.Instance.Snapshot();
        ConsoleList.ItemsSource = snap;
        if (snap.Count > 0)
            ConsoleList.ScrollIntoView(snap[snap.Count - 1]);
    }

    // ── Sketch lifecycle ──

    private void OnRendering(object? sender, EventArgs e)
    {
        var args = (RenderingEventArgs)e;
        if (args.RenderingTime == _lastRenderTime) return;
        _lastRenderTime = args.RenderingTime;

        if (!SketchRuntime.Instance.IsRunning) return;

        var (mx, my) = Canvas.WorldMouse;
        SketchRuntime.Instance.UpdateInputState(mx, my,
            Mouse.LeftButton == MouseButtonState.Pressed,
            Keyboard.FocusedElement is UIElement,
            "");

        var bg = SketchRuntime.Instance.TryConsumeBackground();
        if (bg != null)
        {
            try
            {
                var c = (Color)ColorConverter.ConvertFromString(bg);
                Canvas.CanvasBackground = new SolidColorBrush(c);
            }
            catch
            {
                ConsoleOutput.Instance.WriteLine("Sketch",
                    $"Background: '{bg}' is not a recognised color name.");
            }
        }

        SketchRuntime.Instance.Tick();

        if (SketchRuntime.Instance.TryConsumeZoomRequest(out var w, out var h))
            Canvas.SetBoundary(w, h);
    }

    private void RunSketch()
    {
        StatusLabel.Text = "Compiling...";
        StopButton.IsEnabled = true;
        RunButton.IsEnabled = false;

        var source = Editor.Text;
        var name = _currentPath != null ? Path.GetFileName(_currentPath) : "Untitled.cs";

        _ = _compiler.CompileAndRunAsync(source, name).ContinueWith(t =>
        {
            Dispatcher.Invoke(() =>
            {
                RunButton.IsEnabled = true;
                if (t.Result.Success)
                {
                    StatusLabel.Text = "Running";
                }
                else
                {
                    StatusLabel.Text = "Compile error";
                    StopButton.IsEnabled = false;
                }
            });
        });
    }

    private void StopSketch()
    {
        SketchRuntime.Instance.Stop();
        Canvas.Clear();
        Canvas.ClearBoundary();
        Canvas.CanvasBackground = new SolidColorBrush(Color.FromRgb(30, 30, 30));
        StatusLabel.Text = "Stopped";
        StopButton.IsEnabled = false;
        FrameLabel.Text = "";
    }

    private void ToggleRun()
    {
        if (SketchRuntime.Instance.IsRunning) StopSketch();
        else RunSketch();
    }

    private void RunButton_Click(object sender, RoutedEventArgs e) => RunSketch();
    private void StopButton_Click(object sender, RoutedEventArgs e) => StopSketch();

    // ── File menu ──

    private void New()
    {
        if (!ConfirmDiscardChanges()) return;
        _suppressDirty = true;
        Editor.Text = Templates.DefaultSketch;
        _suppressDirty = false;
        _currentPath = null;
        _isDirty = false;
        UpdateFileLabel();
    }

    private void Open()
    {
        if (!ConfirmDiscardChanges()) return;
        var dlg = new OpenFileDialog
        {
            Filter = "C# files (*.cs)|*.cs|All files (*.*)|*.*",
            DefaultExt = ".cs"
        };
        if (dlg.ShowDialog(this) != true) return;
        _suppressDirty = true;
        Editor.Text = File.ReadAllText(dlg.FileName);
        _suppressDirty = false;
        _currentPath = dlg.FileName;
        _isDirty = false;
        UpdateFileLabel();
    }

    private bool Save()
    {
        if (_currentPath == null) return SaveAs();
        File.WriteAllText(_currentPath, Editor.Text);
        _isDirty = false;
        UpdateFileLabel();
        StatusLabel.Text = $"Saved {Path.GetFileName(_currentPath)}";
        return true;
    }

    private bool SaveAs()
    {
        var dlg = new SaveFileDialog
        {
            Filter = "C# files (*.cs)|*.cs|All files (*.*)|*.*",
            DefaultExt = ".cs",
            FileName = _currentPath != null ? Path.GetFileName(_currentPath) : "Sketch.cs"
        };
        if (dlg.ShowDialog(this) != true) return false;
        File.WriteAllText(dlg.FileName, Editor.Text);
        _currentPath = dlg.FileName;
        _isDirty = false;
        UpdateFileLabel();
        StatusLabel.Text = $"Saved {Path.GetFileName(_currentPath)}";
        return true;
    }

    private bool ConfirmDiscardChanges()
    {
        if (!_isDirty) return true;

        var name = _currentPath != null ? Path.GetFileName(_currentPath) : "Untitled.cs";
        var choice = MessageBox.Show(
            $"Save changes to {name} before closing?",
            "Animator",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning);

        return choice switch
        {
            MessageBoxResult.Yes    => Save(),
            MessageBoxResult.No     => true,
            _                       => false,
        };
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!ConfirmDiscardChanges())
            e.Cancel = true;
    }

    private void NewButton_Click(object sender, RoutedEventArgs e) => New();
    private void OpenButton_Click(object sender, RoutedEventArgs e) => Open();
    private void SaveButton_Click(object sender, RoutedEventArgs e) => Save();
    private void ClearConsoleButton_Click(object sender, RoutedEventArgs e) => ConsoleOutput.Instance.Clear();

    // ── Cross-app switch ──

    private void SwitchToCode2Viz_Click(object sender, RoutedEventArgs e)
    {
        if (!ConfirmDiscardChanges()) return;

        var path = AppSwitcher.FindSiblingApp("Code2Viz");
        if (path == null)
        {
            ConsoleOutput.Instance.WriteLine("Animator",
                "Could not locate Code2Viz.exe. Build the Code2Viz project first.");
            return;
        }
        try
        {
            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
            Closing -= MainWindow_Closing;
            Close();
        }
        catch (Exception ex)
        {
            ConsoleOutput.Instance.WriteLine("Animator", $"Failed to launch Code2Viz: {ex.Message}");
        }
    }

    private void UpdateFileLabel()
    {
        var name = _currentPath != null ? Path.GetFileName(_currentPath) : "Untitled.cs";
        FileLabel.Text = _isDirty ? name + " *" : name;
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

/// <summary>
/// Finds a sibling application's exe in the solution by walking upward from the current
/// assembly's location and probing standard bin output paths.
/// </summary>
internal static class AppSwitcher
{
    public static string? FindSiblingApp(string appName)
    {
        var thisDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;

        var candidates = new[]
        {
            Path.GetFullPath(Path.Combine(thisDir, "..", "..", "..", "..", "bin", "Debug", "net9.0-windows", $"{appName}.exe")),
            Path.GetFullPath(Path.Combine(thisDir, "..", "..", "..", "..", "bin", "Release", "net9.0-windows", $"{appName}.exe")),
            Path.GetFullPath(Path.Combine(thisDir, "..", "..", "..", "Animator", "bin", "Debug", "net9.0-windows", $"{appName}.exe")),
            Path.GetFullPath(Path.Combine(thisDir, "..", "..", "..", "Animator", "bin", "Release", "net9.0-windows", $"{appName}.exe")),
        };

        foreach (var c in candidates)
        {
            if (File.Exists(c)) return c;
        }
        return null;
    }
}
