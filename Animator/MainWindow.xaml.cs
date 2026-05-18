using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using Microsoft.Win32;
using Animator.Canvas;
using Animator.Compiler;
using Animator.Console;
using Animator.Editor;
using Animator.Sketching;

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

    public MainWindow()
    {
        InitializeComponent();

        LoadSyntaxHighlighting();
        Editor.Text = Templates.DefaultSketch;
        UpdateFileLabel();

        WireIntelliSense();

        ConsoleOutput.Instance.Changed += (s, e) => Dispatcher.Invoke(RefreshConsole);

        int lastLabelUpdate = -1;
        SketchRuntime.Instance.FrameProduced += shapes =>
        {
            if (CheckAccess())
            {
                // Steady-state ticks already run on the WPF UI thread — no marshalling.
                Canvas.SetShapes(shapes);
                var fc = SketchRuntime.Instance.Active?.FrameCount ?? 0;
                // Throttle FrameLabel to ~10 Hz; updating text every 16 ms triggers
                // a measure/arrange pass that visibly stutters the canvas.
                if (fc - lastLabelUpdate >= 6 || fc == 0)
                {
                    FrameLabel.Text = $"frame {fc}";
                    lastLabelUpdate = fc;
                }
            }
            else
            {
                // Setup() runs from CompileAndRunAsync's Task.Run — needs marshalling.
                Dispatcher.BeginInvoke((Action)(() =>
                {
                    Canvas.SetShapes(shapes);
                    FrameLabel.Text = $"frame {SketchRuntime.Instance.Active?.FrameCount ?? 0}";
                }));
            }
        };

        CompositionTarget.Rendering += OnRendering;

        // Track unsaved edits in the editor; programmatic loads (New/Open) bracket themselves
        // with _suppressDirty = true so they don't mark the document dirty.
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
        InputBindings.Add(new KeyBinding(new RelayCommand(_ => RunSketch()),  Key.F5,    ModifierKeys.None));
        InputBindings.Add(new KeyBinding(new RelayCommand(_ => StopSketch()), Key.F5,    ModifierKeys.Shift));
        InputBindings.Add(new KeyBinding(new RelayCommand(_ => ToggleRun()),  Key.Enter, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(new RelayCommand(_ => Save()),       Key.S,     ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(new RelayCommand(_ => Open()),       Key.O,     ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(new RelayCommand(_ => New()),        Key.N,     ModifierKeys.Control));
    }

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

    // ── IntelliSense ──

    private void WireIntelliSense()
    {
        // Keep the completion engine's compilation in sync with the editor's text.
        _completion.Update(Editor.Text);
        Editor.TextChanged += (s, e) => _completion.Update(Editor.Text);

        // Auto-trigger on '.' or identifier characters; manual trigger via Ctrl+Space.
        Editor.TextArea.TextEntered += OnTextEntered;
        Editor.TextArea.TextEntering += OnTextEntering;
        InputBindings.Add(new KeyBinding(new RelayCommand(_ => ShowCompletionWindow(autoTrigger: false)),
            Key.Space, ModifierKeys.Control));
    }

    private void OnTextEntered(object sender, System.Windows.Input.TextCompositionEventArgs e)
    {
        if (e.Text == null || e.Text.Length == 0) return;

        // Auto-trigger after '.' or when typing an identifier character with no popup open.
        var ch = e.Text[0];
        if (ch == '.')
        {
            ShowCompletionWindow(autoTrigger: true);
        }
        else if (_completionWindow == null && (char.IsLetter(ch) || ch == '_'))
        {
            // Only auto-open once we have at least one identifier char already typed
            // (avoids popping after every keystroke at the start of a new word).
            var caret = Editor.CaretOffset;
            if (caret >= 2 && (char.IsLetterOrDigit(Editor.Document.GetCharAt(caret - 2)) || Editor.Document.GetCharAt(caret - 2) == '_'))
                ShowCompletionWindow(autoTrigger: true);
        }
    }

    private void OnTextEntering(object sender, System.Windows.Input.TextCompositionEventArgs e)
    {
        // If user types a non-identifier character while a completion window is open,
        // let AvalonEdit's default behaviour handle commit/close.
        if (_completionWindow != null && e.Text != null && e.Text.Length > 0)
        {
            var ch = e.Text[0];
            if (!char.IsLetterOrDigit(ch) && ch != '_')
            {
                // Let AvalonEdit's CompletionList handle the request itself
                // (commits highlighted item if any).
            }
        }
    }

    private void ShowCompletionWindow(bool autoTrigger)
    {
        if (_completionWindow != null)
        {
            _completionWindow.Close();
            _completionWindow = null;
        }

        var caret = Editor.CaretOffset;
        _completion.Update(Editor.Text);
        var items = _completion.GetCompletions(caret);
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
            list.Add(new AvalonCompletionData(item));

        // If the user is typing inside an identifier, seed the filter with the partial token.
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

        window.Closed += (_, __) => _completionWindow = null;
        window.Show();
        _completionWindow = window;
    }

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

    // ── Run / Stop ──

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

    /// <summary>Saves the current document. Returns true on success, false if the user cancelled.</summary>
    private bool Save()
    {
        if (_currentPath == null) return SaveAs();
        File.WriteAllText(_currentPath, Editor.Text);
        _isDirty = false;
        UpdateFileLabel();
        StatusLabel.Text = $"Saved {Path.GetFileName(_currentPath)}";
        return true;
    }

    /// <summary>Save-as. Returns true on success, false if the user cancelled.</summary>
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

    /// <summary>
    /// If the editor has unsaved changes, prompts the user. Returns true if it's safe to
    /// proceed with whatever destructive action triggered the check (Yes saved, No discarded);
    /// returns false if the user cancelled (caller should abort).
    /// </summary>
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
            MessageBoxResult.Yes    => Save(),    // proceed only if save actually succeeded
            MessageBoxResult.No     => true,      // discard
            _                       => false,    // cancel
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
        // Prompt about unsaved changes BEFORE launching the other app, so we don't end
        // up with both apps running if the user cancels.
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
            // ConfirmDiscardChanges already cleared dirty (saved or user said discard);
            // detach the closing handler so we don't re-prompt on the way out.
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

internal sealed class RelayCommand : System.Windows.Input.ICommand
{
    private readonly Action<object?> _exec;
    public RelayCommand(Action<object?> exec) { _exec = exec; }
    public event EventHandler? CanExecuteChanged;
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

        // Standard pattern: each project builds to "{ProjectDir}/bin/{Config}/{TFM}/{exe}".
        // Try direct sibling project (peer to current project dir), or solution-root sibling.
        var candidates = new[]
        {
            // Animator (in Animator) ⇒ Code2Viz at solution root: ../../../../bin/{Config}/{TFM}/Code2Viz.exe
            Path.GetFullPath(Path.Combine(thisDir, "..", "..", "..", "..", "bin", "Debug", "net9.0-windows", $"{appName}.exe")),
            Path.GetFullPath(Path.Combine(thisDir, "..", "..", "..", "..", "bin", "Release", "net9.0-windows", $"{appName}.exe")),
            // Code2Viz (in root) ⇒ Animator at Animator subfolder: ../../../Animator/bin/{Config}/{TFM}/Animator.exe
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
