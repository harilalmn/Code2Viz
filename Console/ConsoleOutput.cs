using System.Text;
using System.Timers;

namespace Code2Viz.Console;

public class ConsoleEntry
{
    public string ModuleName { get; set; } = string.Empty;
    public int LineNumber { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool IsNewLine { get; set; }
    public bool IsError { get; set; }

    // For clickable error navigation
    public string? FilePath { get; set; }
    public int Column { get; set; }
    public bool IsClickable => !string.IsNullOrEmpty(FilePath) && LineNumber > 0;
}

public class ConsoleOutput
{
    private static readonly Lazy<ConsoleOutput> _instance = new(() => new ConsoleOutput());
    public static ConsoleOutput Instance => _instance.Value;

    private readonly List<ConsoleEntry> _entries = new();
    private readonly object _lock = new();

    // Throttling for UI updates
    private readonly System.Timers.Timer _throttleTimer;
    private bool _pendingUpdate = false;
    private const int ThrottleIntervalMs = 50;

    public event EventHandler? OutputChanged;

    private ConsoleOutput()
    {
        _throttleTimer = new System.Timers.Timer(ThrottleIntervalMs);
        _throttleTimer.Elapsed += OnThrottleTimerElapsed;
        _throttleTimer.AutoReset = false;
    }

    private void OnThrottleTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        if (_pendingUpdate)
        {
            _pendingUpdate = false;
            OutputChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void NotifyOutputChanged()
    {
        if (!_throttleTimer.Enabled)
        {
            // First update - fire immediately and start throttle timer
            OutputChanged?.Invoke(this, EventArgs.Empty);
            _throttleTimer.Start();
        }
        else
        {
            // Subsequent updates within throttle window - mark as pending
            _pendingUpdate = true;
        }
    }

    public void WriteLine(string moduleName, int lineNumber, string message)
    {
        lock (_lock)
        {
            _entries.Add(new ConsoleEntry
            {
                ModuleName = moduleName,
                LineNumber = lineNumber,
                Message = message,
                IsNewLine = true,
                IsError = false
            });
        }
        NotifyOutputChanged();
    }

    public void WriteError(string moduleName, int lineNumber, string message)
    {
        lock (_lock)
        {
            _entries.Add(new ConsoleEntry
            {
                ModuleName = moduleName,
                LineNumber = lineNumber,
                Message = message,
                IsNewLine = true,
                IsError = true
            });
        }
        NotifyOutputChanged();
    }

    /// <summary>
    /// Writes a compilation error with full location info for click-to-navigate.
    /// </summary>
    public void WriteCompilationError(string filePath, int lineNumber, int column, string message)
    {
        var fileName = System.IO.Path.GetFileName(filePath);
        lock (_lock)
        {
            _entries.Add(new ConsoleEntry
            {
                ModuleName = fileName,
                LineNumber = lineNumber,
                Column = column,
                FilePath = filePath,
                Message = message,
                IsNewLine = true,
                IsError = true
            });
        }
        NotifyOutputChanged();
    }

    public void Clear()
    {
        lock (_lock)
        {
            _entries.Clear();
        }
        _pendingUpdate = false;
        OutputChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Flushes any pending updates immediately. Call after code execution completes.
    /// </summary>
    public void Flush()
    {
        _throttleTimer.Stop();
        if (_pendingUpdate)
        {
            _pendingUpdate = false;
            OutputChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public string GetFormattedOutput()
    {
        lock (_lock)
        {
            var sb = new StringBuilder();
            foreach (var entry in _entries)
            {
                var prefix = $"[{entry.ModuleName}:{entry.LineNumber}] ";
                sb.Append(prefix);
                sb.Append(entry.Message);
                if (entry.IsNewLine)
                {
                    sb.AppendLine();
                }
            }
            return sb.ToString();
        }
    }

    public IReadOnlyList<ConsoleEntry> GetEntries()
    {
        lock (_lock)
        {
            return _entries.ToList();
        }
    }
}
