using Code2Viz.Console;

namespace Code2Viz.Services
{
    /// <summary>
    /// Interface for console output services.
    /// Provides thread-safe output logging with throttled UI updates.
    /// </summary>
    public interface IConsoleOutput
    {
        /// <summary>
        /// Event raised when output changes (throttled to prevent UI flooding).
        /// </summary>
        event EventHandler? OutputChanged;

        /// <summary>
        /// Writes a standard message to the console.
        /// </summary>
        /// <param name="moduleName">Source module/file name</param>
        /// <param name="lineNumber">Source line number</param>
        /// <param name="message">Message text</param>
        void WriteLine(string moduleName, int lineNumber, string message);

        /// <summary>
        /// Writes an error message to the console.
        /// </summary>
        /// <param name="moduleName">Source module/file name</param>
        /// <param name="lineNumber">Source line number</param>
        /// <param name="message">Error message text</param>
        void WriteError(string moduleName, int lineNumber, string message);

        /// <summary>
        /// Writes a compilation error with full location info for click-to-navigate.
        /// </summary>
        /// <param name="filePath">Full path to the source file</param>
        /// <param name="lineNumber">Line number (1-based)</param>
        /// <param name="column">Column number (1-based)</param>
        /// <param name="message">Error message text</param>
        void WriteCompilationError(string filePath, int lineNumber, int column, string message);

        /// <summary>
        /// Adds a custom entry to the console output.
        /// </summary>
        /// <param name="message">Message text</param>
        /// <param name="filePath">Optional file path for navigation</param>
        /// <param name="lineNumber">Optional line number for navigation</param>
        /// <param name="column">Optional column number for navigation</param>
        /// <param name="isError">Whether this is an error entry</param>
        void AddEntry(string message, string? filePath = null, int lineNumber = 0, int column = 0, bool isError = false);

        /// <summary>
        /// Clears all console entries.
        /// </summary>
        void Clear();

        /// <summary>
        /// Flushes any pending updates immediately.
        /// Call after code execution completes to ensure all output is visible.
        /// </summary>
        void Flush();

        /// <summary>
        /// Gets all entries formatted as a single string.
        /// </summary>
        string GetFormattedOutput();

        /// <summary>
        /// Gets a read-only snapshot of all console entries.
        /// </summary>
        IReadOnlyList<ConsoleEntry> GetEntries();
    }
}
