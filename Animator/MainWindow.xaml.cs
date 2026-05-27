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
using Animator.Ipc;
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

    // Phase-2 process isolation (SKETCH_ISOLATION_PLAN.md). When ANIMATOR_ISOLATE=1, sketches run
    // in a separate SketchHost.exe so an infinite loop / OOM / native crash can't take down the UI.
    // Default OFF — the in-process path stays authoritative until parity is confirmed.
    private readonly bool _isolate = Environment.GetEnvironmentVariable("ANIMATOR_ISOLATE") == "1";
    private SketchHostClient? _hostClient;

    /// <summary>Unified "is a sketch running" across the in-process and out-of-process paths.</summary>
    private bool SketchIsRunning =>
        _isolate ? (_hostClient?.IsSketchRunning ?? false) : SketchRuntime.Instance.IsRunning;

    private SharedEditorController? _editorController;

    public MainWindow() : this(null) { }

    public MainWindow(string? initialFile)
    {
        InitializeComponent();

        VersionText.Text = "v" + GetAppVersion();

        if (!string.IsNullOrWhiteSpace(initialFile) && File.Exists(initialFile))
        {
            try
            {
                Editor.Text = File.ReadAllText(initialFile);
                _currentPath = initialFile;
                RecentAnimationsManager.AddAnimation(initialFile);
            }
            catch
            {
                Editor.Text = Templates.DefaultSketch;
            }
        }
        else
        {
            Editor.Text = Templates.DefaultSketch;
        }
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

        Editor.PreviewKeyDown += Editor_PreviewKeyDown_StopSketch;

        Closing += MainWindow_Closing;
        Closed += (s, e) =>
        {
            CompositionTarget.Rendering -= OnRendering;
            SketchRuntime.Instance.Stop();
            _hostClient?.Dispose();
        };

        // Keyboard shortcuts
        InputBindings.Add(new KeyBinding(new RelayCommand(_ => RunSketch()),         Key.F5,    ModifierKeys.None));
        InputBindings.Add(new KeyBinding(new RelayCommand(_ => StopSketch()),        Key.F5,    ModifierKeys.Shift));
        InputBindings.Add(new KeyBinding(new RelayCommand(_ => ToggleRun()),         Key.Enter, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(new RelayCommand(_ => Save()),              Key.S,     ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(new RelayCommand(_ => Open()),              Key.O,     ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(new RelayCommand(_ => New()),               Key.N,     ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(new RelayCommand(_ => HelpMenuItem_Click(this, new RoutedEventArgs())), Key.F1, ModifierKeys.None));
    }

    // ── Editor initialization ────────────────────────────────────────────────

    private void InitializeEditor()
    {
        _editorController = new SharedEditorController(Editor, EditorMinimap);

        // Animator is single-file. The workspace key is always CompletionEngine.FileId
        // ("Sketch.cs") regardless of which file the user opened on disk — otherwise the
        // SharedEditorController's TextChanged handler would add a second tree at the
        // disk path while CompletionEngine.Update kept feeding "Sketch.cs", and every
        // top-level declaration would compile twice.
        _editorController.GetActiveFilePath = () => CompletionEngine.FileId;
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

        // Seed the workspace with the initial text. After this, SharedEditorController's
        // TextChanged handler keeps the workspace in sync — no separate listener needed.
        _completion.Update(Editor.Text);
    }

    private void RefreshConsole()
    {
        var snap = ConsoleOutput.Instance.Snapshot();
        ConsoleList.ItemsSource = snap;
        if (snap.Count > 0)
            ConsoleList.ScrollIntoView(snap[snap.Count - 1]);
    }

    // ── Sketch lifecycle ──

    /// <summary>
    /// Ensures a live <see cref="SketchHostClient"/> with an alive child process, (re)spawning and
    /// wiring events as needed (the child may have exited or been killed by the watchdog). Returns
    /// false if SketchHost.exe can't be located.
    /// </summary>
    private bool EnsureHostClient()
    {
        if (_hostClient is { IsChildAlive: true }) return true;

        _hostClient?.Dispose();
        _hostClient = null;

        var exe = AppSwitcher.FindSketchHostExe();
        if (exe == null)
        {
            ConsoleOutput.Instance.WriteError("Animator",
                "Could not locate SketchHost.exe — build the SketchHost project. Falling back is not available in isolation mode.");
            return false;
        }

        var client = new SketchHostClient();

        client.FrameReceived += (shapes, frameCount) => Dispatcher.BeginInvoke(() =>
        {
            Canvas.SetShapes(shapes);
            FrameLabel.Text = $"frame {frameCount}";
        });
        client.BackgroundChanged += color => Dispatcher.BeginInvoke(() => ApplyBackground(color));
        client.ZoomRequested += (w, h) => Dispatcher.BeginInvoke(() => Canvas.SetBoundary(w, h));
        // ConsoleOutput is thread-safe and its Changed handler already marshals to the UI.
        client.ConsoleLine += (level, source, message) =>
            ConsoleOutput.Instance.Write((ConsoleLevel)level, source, message);
        client.CompileCompleted += (ok, error) => Dispatcher.BeginInvoke(() =>
        {
            RunButton.IsEnabled = true;
            StatusLabel.Text = ok ? "Running" : "Compile error";
            if (!ok) StopButton.IsEnabled = false;
        });
        client.SketchStopped += () => Dispatcher.BeginInvoke(() =>
        {
            StatusLabel.Text = "Stopped";
            StopButton.IsEnabled = false;
            RunButton.IsEnabled = true;
        });
        client.Hung += message =>
        {
            ConsoleOutput.Instance.WriteError("Sketch", message);
            Dispatcher.BeginInvoke(() =>
            {
                Canvas.Clear();
                StatusLabel.Text = "Stopped (hung)";
                StopButton.IsEnabled = false;
                RunButton.IsEnabled = true;
            });
        };
        client.Exited += code => Dispatcher.BeginInvoke(() =>
        {
            // Unexpected exit while we thought a sketch was live (e.g. a crash the guard can't catch).
            if (StatusLabel.Text == "Running")
            {
                ConsoleOutput.Instance.WriteError("Sketch",
                    $"Sketch host exited unexpectedly (code {code}). The app is unaffected; run again to restart it.");
                StatusLabel.Text = "Stopped (host exited)";
                StopButton.IsEnabled = false;
                RunButton.IsEnabled = true;
            }
        });

        try { client.Start(exe); }
        catch (Exception ex)
        {
            ConsoleOutput.Instance.WriteError("Animator", $"Failed to start SketchHost: {ex.Message}");
            client.Dispose();
            return false;
        }

        _hostClient = client;
        return true;
    }

    /// <summary>Applies a sketch-requested background color to the canvas (shared by both paths).</summary>
    private void ApplyBackground(string color)
    {
        try { Canvas.CanvasBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)); }
        catch { ConsoleOutput.Instance.WriteLine("Sketch", $"Background: '{color}' is not a recognised color name."); }
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        var args = (RenderingEventArgs)e;
        if (args.RenderingTime == _lastRenderTime) return;
        _lastRenderTime = args.RenderingTime;

        if (_isolate)
        {
            // Out-of-process: the child drives its own Draw loop and pushes frames/background/zoom
            // back via events. The parent only forwards input each frame.
            if (!(_hostClient?.IsSketchRunning ?? false)) return;
            var (imx, imy) = Canvas.WorldMouse;
            _hostClient!.SendInput(imx, imy,
                Mouse.LeftButton == MouseButtonState.Pressed,
                Keyboard.FocusedElement is UIElement,
                "");
            return;
        }

        if (!SketchRuntime.Instance.IsRunning) return;

        var (mx, my) = Canvas.WorldMouse;
        SketchRuntime.Instance.UpdateInputState(mx, my,
            Mouse.LeftButton == MouseButtonState.Pressed,
            Keyboard.FocusedElement is UIElement,
            "");

        var bg = SketchRuntime.Instance.TryConsumeBackground();
        if (bg != null) ApplyBackground(bg);

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

        if (_isolate)
        {
            // Out-of-process: hand the source to the child; CompileCompleted/SketchStopped events
            // drive the status. (Re)spawn the child if it isn't alive.
            if (!EnsureHostClient())
            {
                StatusLabel.Text = "Host unavailable";
                StopButton.IsEnabled = false;
                RunButton.IsEnabled = true;
                return;
            }
            Canvas.Clear();
            _hostClient!.Run(name, source);
            return;
        }

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
        if (_isolate)
            _hostClient?.StopSketch();
        else
            SketchRuntime.Instance.Stop();

        Canvas.Clear();
        Canvas.ClearBoundary();
        StopButton.IsEnabled = false;
        RunButton.IsEnabled = true;
        StatusLabel.Text = "Stopped";
    }

    private void ToggleRun()
    {
        if (SketchIsRunning)
            StopSketch();
        else
            RunSketch();
    }

    // Stop the running sketch the moment the user starts interacting with the editor.
    // Skip pure modifier keys (they fire on their own as part of any shortcut) and
    // Ctrl/Alt/Win combos so genuine shortcuts (Ctrl+S, Shift+F5, etc.) still work.
    private void Editor_PreviewKeyDown_StopSketch(object sender, KeyEventArgs e)
    {
        if (!SketchIsRunning) return;

        if (e.Key is Key.LeftCtrl or Key.RightCtrl
                  or Key.LeftShift or Key.RightShift
                  or Key.LeftAlt or Key.RightAlt
                  or Key.System
                  or Key.LWin or Key.RWin
                  or Key.CapsLock or Key.NumLock or Key.Scroll)
            return;

        if ((Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Windows)) != 0)
            return;

        StopSketch();
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
        _editorController?.SetActiveFile(CompletionEngine.FileId);
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
        _editorController?.SetActiveFile(CompletionEngine.FileId);
        RecentAnimationsManager.AddAnimation(dlg.FileName);
    }

    private bool Save()
    {
        if (_currentPath == null) return SaveAs();
        File.WriteAllText(_currentPath, Editor.Text);
        _isDirty = false;
        UpdateFileLabel();
        StatusLabel.Text = $"Saved {Path.GetFileName(_currentPath)}";
        _editorController?.SetActiveFile(CompletionEngine.FileId);
        RecentAnimationsManager.AddAnimation(_currentPath);
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
        _editorController?.SetActiveFile(CompletionEngine.FileId);
        RecentAnimationsManager.AddAnimation(_currentPath);
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
    private void SaveAsMenuItem_Click(object sender, RoutedEventArgs e) => SaveAs();
    private void ClearConsoleButton_Click(object sender, RoutedEventArgs e) => ConsoleOutput.Instance.Clear();
    private void ExitMenuItem_Click(object sender, RoutedEventArgs e) => Close();

    // ── View menu toggles ─────────────────────────────────────────────────────

    private void InlayHintsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (_editorController == null) return;
        _editorController.InlayHintsEnabled = InlayHintsMenuItem.IsChecked;
    }

    private void SemanticHighlightingMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (_editorController == null) return;
        _editorController.SemanticHighlightingEnabled = SemanticHighlightingMenuItem.IsChecked;
    }

    private void CodeLensMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (_editorController == null) return;
        _editorController.CodeLensEnabled = CodeLensMenuItem.IsChecked;
    }

    private void MinimapMenuItem_Click(object sender, RoutedEventArgs e)
    {
        EditorMinimap.Visibility = MinimapMenuItem.IsChecked ? Visibility.Visible : Visibility.Collapsed;
    }

    private GridLength _savedConsoleHeight = new GridLength(180);
    private void ConsoleMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (ConsoleMenuItem.IsChecked)
        {
            ConsoleSplitterRow.Height = GridLength.Auto;
            ConsoleRow.Height = _savedConsoleHeight;
        }
        else
        {
            // Preserve whatever the user dragged the splitter to before hiding.
            if (ConsoleRow.Height.IsAbsolute && ConsoleRow.Height.Value > 0)
                _savedConsoleHeight = ConsoleRow.Height;
            ConsoleSplitterRow.Height = new GridLength(0);
            ConsoleRow.Height = new GridLength(0);
        }
    }

    // ── Export ────────────────────────────────────────────────────────────────

    private void ExportGifMenuItem_Click(object sender, RoutedEventArgs e)
        => RunExport(Animator.Export.SketchExporter.Format.Gif, "Export GIF Animation",
            "GIF Animation (*.gif)|*.gif", ".gif");

    private void ExportMp4MenuItem_Click(object sender, RoutedEventArgs e)
        => RunExport(Animator.Export.SketchExporter.Format.Mp4, "Export Video (MP4)",
            "MP4 Video (*.mp4)|*.mp4", ".mp4");

    private async void RunExport(Animator.Export.SketchExporter.Format format, string title,
        string filter, string extension)
    {
        var options = new Animator.Export.AnimatorExportOptionsWindow(title) { Owner = this };
        if (options.ShowDialog() != true) return;

        var save = new SaveFileDialog
        {
            Filter = filter,
            DefaultExt = extension,
            FileName = (_currentPath != null ? Path.GetFileNameWithoutExtension(_currentPath) : "Sketch") + extension
        };
        if (save.ShowDialog(this) != true) return;

        // Stop the live sketch so the export driver has a clean slate. Restore it after.
        // (Export always renders in-process via SketchExporter; in isolation mode this just stops
        // the child so it isn't competing while we export.)
        bool wasRunning = SketchIsRunning;
        if (wasRunning)
            StopSketch();

        var progressDialog = new Code2Viz.ProgressDialog($"Exporting {title}...") { Owner = this };
        progressDialog.Show();
        var originalCursor = Cursor;
        Cursor = System.Windows.Input.Cursors.Wait;

        bool success = false;
        try
        {
            success = await Animator.Export.SketchExporter.ExportAsync(
                Canvas, Editor.Text,
                _currentPath != null ? Path.GetFileName(_currentPath) : "Sketch.cs",
                save.FileName, format, options.Duration, options.Fps,
                (i, total) => progressDialog.SetProgress(i, total));
        }
        catch (Exception ex)
        {
            ConsoleOutput.Instance.WriteError("Export", $"Error: {ex.Message}");
        }
        finally
        {
            progressDialog.Close();
            Cursor = originalCursor;
        }

        if (success)
        {
            StatusLabel.Text = $"Exported {Path.GetFileName(save.FileName)}";
            ConsoleOutput.Instance.WriteLine("Export", $"Wrote {save.FileName}");
        }
        else
        {
            StatusLabel.Text = "Export failed";
        }

        // Restart the sketch if it was running before the export.
        if (wasRunning) RunSketch();
    }

    // ── Help menu ─────────────────────────────────────────────────────────────

    private void HelpMenuItem_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var generator = new Code2Viz.Documentation.DocGenerator(
                typeof(C2VGeometry.Shape).Assembly,
                "C2VGeometry");
            var helpWindow = new Code2Viz.HelpWindow(generator) { Owner = this };
            helpWindow.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Failed to open Help window:\n\n{ex}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void AboutMenuItem_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(this,
            "Animator — p5.js-style sketch host for Code2Viz\n\n" +
            "Write Setup()/Draw() in C# and render to the canvas every frame.\n\n" +
            "Part of the Code2Viz suite.",
            "About Animator",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    /// <summary>App version for the branding strip — the calendar version stamped into the
    /// assembly's informational version (Directory.Build.props), minus any build metadata.</summary>
    private static string GetAppVersion()
    {
        var asm = Assembly.GetExecutingAssembly();
        var info = asm.GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrEmpty(info))
        {
            var plus = info.IndexOf('+');
            return plus >= 0 ? info.Substring(0, plus) : info;
        }
        return asm.GetName().Version?.ToString(3) ?? "0.0.0";
    }

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

    /// <summary>
    /// Locates the out-of-process sketch host. Installed layout puts it at {app}\SketchHost\
    /// alongside {app}\Animator\; dev layout has it under the solution root's SketchHost\bin\.
    /// </summary>
    public static string? FindSketchHostExe()
    {
        var thisDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        var candidates = new[]
        {
            // Installed: {app}\Animator\Animator.exe -> {app}\SketchHost\SketchHost.exe
            Path.GetFullPath(Path.Combine(thisDir, "..", "SketchHost", "SketchHost.exe")),
            // Dev: .../Animator/bin/{Config}/net9.0-windows -> solution root /SketchHost/bin/{Config}/...
            Path.GetFullPath(Path.Combine(thisDir, "..", "..", "..", "..", "SketchHost", "bin", "Debug", "net9.0-windows", "SketchHost.exe")),
            Path.GetFullPath(Path.Combine(thisDir, "..", "..", "..", "..", "SketchHost", "bin", "Release", "net9.0-windows", "SketchHost.exe")),
        };
        foreach (var c in candidates)
            if (File.Exists(c)) return c;
        return null;
    }
}
