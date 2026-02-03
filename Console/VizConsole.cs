using System.Collections;
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
    /// When <paramref name="itemize"/> is true (default) and <paramref name="value"/> is a collection,
    /// each item is printed on a separate line. When false, the collection's ToString() is printed.
    /// </summary>
    public static void Log(
        object? value,
        bool itemize = true,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        var moduleName = Path.GetFileNameWithoutExtension(filePath);

        if (itemize && value is IEnumerable enumerable and not string)
        {
            bool any = false;
            foreach (var item in enumerable)
            {
                any = true;
                ConsoleOutput.Instance.WriteLine(moduleName, lineNumber, item?.ToString() ?? "");
            }
            if (!any)
            {
                ConsoleOutput.Instance.WriteLine(moduleName, lineNumber, "(empty)");
            }
        }
        else
        {
            ConsoleOutput.Instance.WriteLine(moduleName, lineNumber, value?.ToString() ?? "");
        }
    }
}
