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
using Code2Viz;
using Code2Viz.Editor;
using Code2Viz.Project;
using ICSharpCode.AvalonEdit.Search;
using AvalonSelection = ICSharpCode.AvalonEdit.Editing.Selection;
using TextDocument = ICSharpCode.AvalonEdit.Document.TextDocument;

namespace Animator;

public partial class MainWindow : Window
{
    private readonly SketchCompiler _compiler = new();
    private readonly CompletionEngine _completion = new();
    private string? _currentPath;
    private TimeSpan _lastRenderTime = TimeSpan.Zero;
    private bool _isDirty;
    private bool _suppressDirty;

    private SharedEditorController? _editorController;

    public MainWindow()
    {
        InitializeComponent();

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
    }

    // ── Editor initialization ────────────────────────────────────────────────

    private void InitializeEditor()
    {
        _editorController = new SharedEditorController(Editor, EditorMinimap);

        _editorController.GetActiveFilePath = () => _currentPath ?? "Sketch.cs";
        _editorController.GetWorkspace = () => _completion.Workspace;
        _editorController.SetStatusMessage = (msg, isError) => StatusLabel.Text = msg;
        _editorController.NavigateToLocation = NavigateToLocation;
        _editorController.ShowReferences = (symbolName, refs) =>
        {
            ConsoleOutput.Instance.WriteLine("Editor", $"References for '{symbolName}':");
            foreach (var r in refs)
            {
                ConsoleOutput.Instance.WriteLine("Editor", $"  {Path.GetFileName(r.FilePath)}: line {r.Line}, col {r.Column}");
            }
        };
        _editorController.GetActiveProject = () => null;

        _editorController.Initialize();

        // Keep completion engine synced
        _completion.Update(Editor.Text);
        Editor.TextChanged += (s, e) =>
        {
            _completion.Update(Editor.Text);
        };
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
        StopButton.IsEnabled = false;
        RunButton.IsEnabled = true;
        StatusLabel.Text = "Stopped";
    }

    private void ToggleRun()
    {
        if (SketchRuntime.Instance.IsRunning)
            StopSketch();
        else
            RunSketch();
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
        _editorController?.SetActiveFile("Sketch.cs");
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
        _editorController?.SetActiveFile(dlg.FileName);
    }

    private bool Save()
    {
        if (_currentPath == null) return SaveAs();
        File.WriteAllText(_currentPath, Editor.Text);
        _isDirty = false;
        UpdateFileLabel();
        StatusLabel.Text = $"Saved {Path.GetFileName(_currentPath)}";
        _editorController?.SetActiveFile(_currentPath);
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
        _editorController?.SetActiveFile(_currentPath);
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

    private void NavigateToLocation(string filePath, int line, int column)
    {
        if (line <= 0) return;
        var doc = Editor.Document;
        if (line > doc.LineCount) return;
        var lineSegment = doc.GetLineByNumber(line);
        int offset = lineSegment.Offset + Math.Max(0, Math.Min(column - 1, lineSegment.Length));
        Editor.CaretOffset = offset;
        Editor.ScrollTo(line, column);
        Editor.Focus();
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

internal static class AppSwitcher
{
    public static string? FindSiblingApp(string appName)
    {
        // Installed layout: {app}\Code2Viz.exe + {app}\Animator\Animator.exe (so we walk up one level for Code2Viz)
        // Dev layouts:      Animator at .../Animator/bin/{Config}/{TFM}, Code2Viz at .../bin/{Config}/{TFM}
        var thisDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;

        var candidates = new[]
        {
            Path.GetFullPath(Path.Combine(thisDir, "..", $"{appName}.exe")),
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
