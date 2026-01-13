using System.Text.RegularExpressions;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;

namespace Code2Viz.Editor;

/// <summary>
/// Manages an active snippet session with placeholder navigation.
/// Supports $1, $2, etc. placeholders and Tab navigation between them.
/// $0 marks the final cursor position.
/// </summary>
public class SnippetSession
{
    private readonly TextEditor _editor;
    private readonly List<PlaceholderInfo> _placeholders = new();
    private int _currentPlaceholderIndex = -1;
    private int _insertOffset;

    public bool IsActive => _placeholders.Count > 0 && _currentPlaceholderIndex >= 0;

    public SnippetSession(TextEditor editor)
    {
        _editor = editor;
    }

    /// <summary>
    /// Inserts a snippet and activates placeholder navigation.
    /// Automatically replaces "ClassName" with the actual enclosing class name for ctor snippets.
    /// </summary>
    public void InsertSnippet(int offset, int replacementLength, string snippetCode)
    {
        _placeholders.Clear();
        _currentPlaceholderIndex = -1;
        _insertOffset = offset;

        // Replace "ClassName" placeholder with actual class name for ctor snippet
        if (snippetCode.Contains("ClassName"))
        {
            var textBeforeCursor = _editor.Text.Substring(0, offset);
            var currentClass = FindCurrentClass(textBeforeCursor);
            if (!string.IsNullOrEmpty(currentClass))
            {
                snippetCode = snippetCode.Replace("ClassName", currentClass);
            }
        }

        // Parse placeholders ($1, $2, ..., $0)
        var placeholderPattern = new Regex(@"\$(\d+)");
        var matches = placeholderPattern.Matches(snippetCode);

        // Collect all placeholders with their positions
        var tempPlaceholders = new List<(int index, int position, int length)>();
        foreach (Match match in matches)
        {
            var placeholderIndex = int.Parse(match.Groups[1].Value);
            tempPlaceholders.Add((placeholderIndex, match.Index, match.Length));
        }

        // Sort by placeholder index ($1 first, then $2, etc., $0 last)
        tempPlaceholders = tempPlaceholders
            .OrderBy(p => p.index == 0 ? int.MaxValue : p.index)
            .ToList();

        // Remove placeholders from code and calculate actual positions
        var cleanCode = snippetCode;
        // Process in reverse order to maintain positions
        var sortedByPosition = tempPlaceholders.OrderByDescending(p => p.position).ToList();
        foreach (var (index, position, length) in sortedByPosition)
        {
            cleanCode = cleanCode.Remove(position, length);
        }

        // Recalculate positions in the clean code
        var processedIndices = new HashSet<int>();


        foreach (var (index, originalPosition, length) in tempPlaceholders.OrderBy(p => p.position))
        {
            // Calculate how many placeholder markers came before this one
            var markersBefore = tempPlaceholders.Count(p => p.position < originalPosition);
            var adjustedPosition = originalPosition - (markersBefore * 2); // Each $N is 2 chars

            if (!processedIndices.Contains(index))
            {
                _placeholders.Add(new PlaceholderInfo
                {
                    Index = index,
                    Offset = adjustedPosition,
                    Length = 0 // Placeholders are zero-length by default
                });
                processedIndices.Add(index);
            }
        }

        // Sort placeholders: $1, $2, ... then $0 at end
        _placeholders.Sort((a, b) =>
        {
            if (a.Index == 0) return 1;
            if (b.Index == 0) return -1;
            return a.Index.CompareTo(b.Index);
        });

        // Insert the clean code
        _editor.Document.Replace(offset, replacementLength, cleanCode);

        // Adjust placeholder offsets relative to insert position
        foreach (var p in _placeholders)
        {
            p.Offset += offset;
        }

        // Move to first placeholder (or $0 if only that exists)
        if (_placeholders.Count > 0)
        {
            _currentPlaceholderIndex = 0;
            SelectCurrentPlaceholder();
        }
    }

    /// <summary>
    /// Moves to the next placeholder. Returns true if moved, false if session ended.
    /// </summary>
    public bool MoveToNextPlaceholder()
    {
        if (_placeholders.Count == 0)
            return false;

        _currentPlaceholderIndex++;

        if (_currentPlaceholderIndex >= _placeholders.Count)
        {
            // Session complete
            EndSession();
            return false;
        }

        SelectCurrentPlaceholder();
        return true;
    }

    /// <summary>
    /// Moves to the previous placeholder.
    /// </summary>
    public bool MoveToPreviousPlaceholder()
    {
        if (_placeholders.Count == 0 || _currentPlaceholderIndex <= 0)
            return false;

        _currentPlaceholderIndex--;
        SelectCurrentPlaceholder();
        return true;
    }

    private void SelectCurrentPlaceholder()
    {
        if (_currentPlaceholderIndex < 0 || _currentPlaceholderIndex >= _placeholders.Count)
            return;

        var placeholder = _placeholders[_currentPlaceholderIndex];
        _editor.CaretOffset = placeholder.Offset;

        // For zero-length placeholders, just position cursor
        // For placeholders with length, select the text
        if (placeholder.Length > 0)
        {
            _editor.Select(placeholder.Offset, placeholder.Length);
        }
    }

    public void EndSession()
    {
        _placeholders.Clear();
        _currentPlaceholderIndex = -1;
    }

    private class PlaceholderInfo
    {
        public int Index { get; set; }
        public int Offset { get; set; }
        public int Length { get; set; }
    }

    /// <summary>
    /// Finds the class name that contains the cursor position based on brace counting.
    /// </summary>
    private static string? FindCurrentClass(string textBeforeCursor)
    {
        // Find all class declarations with their positions
        var classPattern = @"\b(?:public\s+)?class\s+(\w+)";
        var matches = Regex.Matches(textBeforeCursor, classPattern);

        var classStack = new Stack<(string Name, int BraceDepth)>();
        var braceDepth = 0;
        var lastClassEnd = 0;

        foreach (Match match in matches)
        {
            // Count braces from last class end to this class declaration
            var textBetween = textBeforeCursor.Substring(lastClassEnd, match.Index - lastClassEnd);
            foreach (var c in textBetween)
            {
                if (c == '{') braceDepth++;
                else if (c == '}')
                {
                    braceDepth--;
                    // Pop classes that have ended
                    while (classStack.Count > 0 && classStack.Peek().BraceDepth >= braceDepth)
                    {
                        classStack.Pop();
                    }
                }
            }

            var className = match.Groups[1].Value;
            if (className != "Viz") // Skip entry point class
            {
                classStack.Push((className, braceDepth));
            }
            lastClassEnd = match.Index + match.Length;
        }

        // Count remaining braces after last class declaration
        var remainingText = textBeforeCursor.Substring(lastClassEnd);
        foreach (var c in remainingText)
        {
            if (c == '{') braceDepth++;
            else if (c == '}')
            {
                braceDepth--;
                while (classStack.Count > 0 && classStack.Peek().BraceDepth >= braceDepth)
                {
                    classStack.Pop();
                }
            }
        }

        // The current class is the top of the stack (if any)
        return classStack.Count > 0 ? classStack.Peek().Name : null;
    }
}
