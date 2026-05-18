using System;
using System.Collections.Generic;

namespace Animator.Console;

public enum ConsoleLevel { Info, Warning, Error }

public sealed record ConsoleLine(ConsoleLevel Level, string Source, string Message)
{
    public string Display => $"[{Source}] {Message}";
    public override string ToString() => Display;
}

/// <summary>
/// Process-wide console output collector. Sketches and the host use this to log messages
/// that surface in the bottom Console pane.
/// </summary>
public sealed class ConsoleOutput
{
    public static ConsoleOutput Instance { get; } = new();
    private readonly List<ConsoleLine> _lines = new();
    private readonly object _gate = new();

    public event EventHandler? Changed;

    public IReadOnlyList<ConsoleLine> Snapshot()
    {
        lock (_gate) return _lines.ToArray();
    }

    public void WriteLine(string source, string message)
        => Write(ConsoleLevel.Info, source, message);

    public void WriteWarning(string source, string message)
        => Write(ConsoleLevel.Warning, source, message);

    public void WriteError(string source, string message)
        => Write(ConsoleLevel.Error, source, message);

    public void Write(ConsoleLevel level, string source, string message)
    {
        lock (_gate) _lines.Add(new ConsoleLine(level, source, message));
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        lock (_gate) _lines.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>
/// Convenience entrypoint for user sketch code: <c>VizConsole.Log("...")</c>.
/// </summary>
public static class VizConsole
{
    public static void Log(object? message)
        => ConsoleOutput.Instance.WriteLine("Sketch", message?.ToString() ?? "");

    public static void Warn(object? message)
        => ConsoleOutput.Instance.WriteWarning("Sketch", message?.ToString() ?? "");

    public static void Error(object? message)
        => ConsoleOutput.Instance.WriteError("Sketch", message?.ToString() ?? "");
}
