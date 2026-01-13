using System.IO;
using System.Runtime.CompilerServices;

namespace Code2Viz.Console;

/// <summary>
/// Console output for VizCode. Automatically captures the calling module and line number.
/// </summary>
public static class VizConsole
{
    /// <summary>
    /// Logs a message to the console with automatic module name and line number tracking.
    /// </summary>
    public static void Log(
        object? value,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        var moduleName = Path.GetFileNameWithoutExtension(filePath);
        ConsoleOutput.Instance.WriteLine(moduleName, lineNumber, value?.ToString() ?? "");
    }
}
