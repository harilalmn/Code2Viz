using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Rendering;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;
using Brushes = System.Windows.Media.Brushes;
using Colors = System.Windows.Media.Colors;
using Rect = System.Windows.Rect;

namespace Code2Viz.Editor;

/// <summary>
/// Renders multiple selection highlights and manages multi-cursor editing for Ctrl+D feature.
/// </summary>
public class MultiSelectionRenderer : IBackgroundRenderer
{
    private readonly TextView _textView;
    private readonly TextArea _textArea;
    private readonly List<TextSegment> _selections = new();

    // Track anchor and caret points for each selection (anchor stays fixed, caret moves)
    // Parallel to _selections - anchor[i] and caret[i] correspond to _selections[i]
    private readonly List<int> _anchors = new();
    private readonly List<int> _carets = new();

    // Selection highlight styling - matches the editor's selection color
    private static readonly Brush SelectionBrush;
    private static readonly Brush CaretBrush;

    static MultiSelectionRenderer()
    {
        // Use a color similar to VS Code's multi-selection highlight
        SelectionBrush = new SolidColorBrush(Color.FromArgb(100, 38, 79, 120));
        SelectionBrush.Freeze();
        CaretBrush = new SolidColorBrush(Colors.White);
        CaretBrush.Freeze();
    }

    public MultiSelectionRenderer(TextView textView)
    {
        _textView = textView;
        _textArea = textView.GetService(typeof(TextArea)) as TextArea
                    ?? throw new InvalidOperationException("TextArea not found");
    }

    public KnownLayer Layer => KnownLayer.Caret;

    /// <summary>
    /// Gets the list of additional selection segments (besides the main caret selection).
    /// </summary>
    public List<TextSegment> Selections => _selections;

    /// <summary>
    /// Adds a new selection segment. Anchor is at start, caret at end.
    /// </summary>
    public void AddSelection(int offset, int length)
    {
        _selections.Add(new TextSegment { StartOffset = offset, Length = length });
        _anchors.Add(offset); // Anchor at start
        _carets.Add(offset + length); // Caret at end
        _textView.InvalidateLayer(Layer);
    }

    /// <summary>
    /// Clears all additional selections.
    /// </summary>
    public void ClearSelections()
    {
        if (_selections.Count > 0)
        {
            _selections.Clear();
            _anchors.Clear();
            _carets.Clear();
            _textView.InvalidateLayer(Layer);
        }
    }

    /// <summary>
    /// Checks if there are any additional selections.
    /// </summary>
    public bool HasSelections => _selections.Count > 0;

    /// <summary>
    /// Adds a cursor above the current cursor position(s).
    /// </summary>
    public bool AddCursorAbove()
    {
        var document = _textView.Document;
        var caret = _textArea.Caret;

        // Get all current cursor positions (main + additional)
        var cursorPositions = GetAllCursorPositions();

        // Find the topmost cursor
        var topmost = cursorPositions.OrderBy(p => p.Line).First();

        // Can't go above line 1
        if (topmost.Line <= 1)
            return false;

        // Get the target line
        int targetLine = topmost.Line - 1;
        var targetLineObj = document.GetLineByNumber(targetLine);

        // Calculate target column (clamped to line length)
        int targetColumn = Math.Min(topmost.Column, targetLineObj.Length + 1);
        int targetOffset = targetLineObj.Offset + targetColumn - 1;

        // Add the new cursor
        AddSelection(targetOffset, 0);
        return true;
    }

    /// <summary>
    /// Adds a cursor below the current cursor position(s).
    /// </summary>
    public bool AddCursorBelow()
    {
        var document = _textView.Document;
        var caret = _textArea.Caret;

        // Get all current cursor positions (main + additional)
        var cursorPositions = GetAllCursorPositions();

        // Find the bottommost cursor
        var bottommost = cursorPositions.OrderByDescending(p => p.Line).First();

        // Can't go below last line
        if (bottommost.Line >= document.LineCount)
            return false;

        // Get the target line
        int targetLine = bottommost.Line + 1;
        var targetLineObj = document.GetLineByNumber(targetLine);

        // Calculate target column (clamped to line length)
        int targetColumn = Math.Min(bottommost.Column, targetLineObj.Length + 1);
        int targetOffset = targetLineObj.Offset + targetColumn - 1;

        // Add the new cursor
        AddSelection(targetOffset, 0);
        return true;
    }

    /// <summary>
    /// Gets all cursor positions including main caret and additional selections.
    /// </summary>
    private List<(int Line, int Column, int Offset)> GetAllCursorPositions()
    {
        var document = _textView.Document;
        var positions = new List<(int Line, int Column, int Offset)>();

        // Add main caret
        var caretLine = _textArea.Caret.Line;
        var caretColumn = _textArea.Caret.Column;
        var caretOffset = _textArea.Caret.Offset;
        positions.Add((caretLine, caretColumn, caretOffset));

        // Add additional cursors
        foreach (var sel in _selections)
        {
            var loc = document.GetLocation(sel.EndOffset);
            positions.Add((loc.Line, loc.Column, sel.EndOffset));
        }

        return positions;
    }

    /// <summary>
    /// Inserts text at all cursor positions (main + additional selections).
    /// </summary>
    public void InsertTextAtAllCursors(string text)
    {
        if (_selections.Count == 0) return;

        var document = _textView.Document;
        var mainSelection = _textArea.Selection;
        var mainSegment = mainSelection.SurroundingSegment;

        // Collect all selections including main with their indices, sorted by offset descending
        // (process from end to start to avoid offset shifts affecting earlier positions)
        var allSelections = new List<(int Offset, int Length, int Index, bool IsMain)>();

        for (int i = 0; i < _selections.Count; i++)
        {
            allSelections.Add((_selections[i].StartOffset, _selections[i].Length, i, false));
        }

        // Add main selection/caret position (SurroundingSegment can be null if no selection)
        if (mainSegment != null)
        {
            allSelections.Add((mainSegment.Offset, mainSegment.Length, -1, true));
        }
        else
        {
            // No selection, use caret position
            allSelections.Add((_textArea.Caret.Offset, 0, -1, true));
        }

        // Sort by offset descending for deletion
        var sortedDesc = allSelections.OrderByDescending(s => s.Offset).ToList();

        // Begin update for undo grouping
        document.BeginUpdate();
        try
        {
            foreach (var (offset, length, _, _) in sortedDesc)
            {
                // Replace selection with new text
                document.Replace(offset, length, text);
            }
        }
        finally
        {
            document.EndUpdate();
        }

        // Calculate new positions (process in ascending order)
        var sortedAsc = allSelections.OrderBy(s => s.Offset).ToList();
        int adjustment = 0;

        // Clear and rebuild selections with updated positions
        _selections.Clear();
        _anchors.Clear();
        _carets.Clear();

        int mainNewOffset = 0;

        foreach (var (offset, length, index, isMain) in sortedAsc)
        {
            var newOffset = offset + adjustment + text.Length;
            adjustment += text.Length - length;

            if (isMain)
            {
                mainNewOffset = newOffset;
            }
            else
            {
                _selections.Add(new TextSegment { StartOffset = newOffset, Length = 0 });
                _anchors.Add(newOffset);
                _carets.Add(newOffset);
            }
        }

        // Update main caret position
        _textArea.Caret.Offset = mainNewOffset;
        _textArea.Selection = Selection.Create(_textArea, mainNewOffset, mainNewOffset);

        _textView.InvalidateLayer(Layer);
    }

    /// <summary>
    /// Inserts a newline with auto-indentation at all cursor positions.
    /// </summary>
    public void EnterAtAllCursors(bool autoIndent)
    {
        if (_selections.Count == 0) return;

        var document = _textView.Document;
        var mainSelection = _textArea.Selection;
        var mainSegment = mainSelection.SurroundingSegment;

        // Collect all cursor positions including main
        var allSelections = new List<(int Offset, int Length, bool IsMain)>();

        for (int i = 0; i < _selections.Count; i++)
        {
            allSelections.Add((_selections[i].StartOffset, _selections[i].Length, false));
        }

        if (mainSegment != null)
        {
            allSelections.Add((mainSegment.Offset, mainSegment.Length, true));
        }
        else
        {
            allSelections.Add((_textArea.Caret.Offset, 0, true));
        }

        // For each cursor, compute the text to insert (newline + indentation)
        var insertions = new List<(int Offset, int Length, string Text, bool IsMain)>();
        foreach (var (offset, length, isMain) in allSelections)
        {
            var insertText = "\n";
            if (autoIndent)
            {
                var line = document.GetLineByOffset(offset);
                var lineText = document.GetText(line.Offset, line.Length);
                var currentIndent = GetLineIndentation(lineText);
                var trimmedLine = lineText.TrimEnd();

                var newIndent = currentIndent;
                if (trimmedLine.EndsWith("{"))
                {
                    newIndent += "    ";
                }

                var afterCursor = document.GetText(offset, line.EndOffset - offset).Trim();
                if (trimmedLine.EndsWith("{") && afterCursor.StartsWith("}"))
                {
                    insertText = "\n" + newIndent + "\n" + currentIndent;
                }
                else
                {
                    insertText = "\n" + newIndent;
                }
            }
            insertions.Add((offset, length, insertText, isMain));
        }

        // Sort descending by offset for safe replacement
        var sortedDesc = insertions.OrderByDescending(s => s.Offset).ToList();

        document.BeginUpdate();
        try
        {
            foreach (var (offset, length, text, _) in sortedDesc)
            {
                document.Replace(offset, length, text);
            }
        }
        finally
        {
            document.EndUpdate();
        }

        // Calculate new positions (ascending order)
        var sortedAsc = insertions.OrderBy(s => s.Offset).ToList();
        int adjustment = 0;

        _selections.Clear();
        _anchors.Clear();
        _carets.Clear();

        int mainNewOffset = 0;

        foreach (var (offset, length, text, isMain) in sortedAsc)
        {
            // Position cursor after first newline + indent (not after the closing brace line)
            int cursorAdvance;
            var firstNewline = text.IndexOf('\n', 1);
            if (firstNewline > 0)
            {
                // Between braces case: place cursor at end of middle line
                cursorAdvance = firstNewline;
            }
            else
            {
                cursorAdvance = text.Length;
            }

            var newOffset = offset + adjustment + cursorAdvance;
            adjustment += text.Length - length;

            if (isMain)
            {
                mainNewOffset = newOffset;
            }
            else
            {
                _selections.Add(new TextSegment { StartOffset = newOffset, Length = 0 });
                _anchors.Add(newOffset);
                _carets.Add(newOffset);
            }
        }

        _textArea.Caret.Offset = mainNewOffset;
        _textArea.Selection = Selection.Create(_textArea, mainNewOffset, mainNewOffset);

        _textView.InvalidateLayer(Layer);
    }

    private static string GetLineIndentation(string line)
    {
        var indent = new System.Text.StringBuilder();
        foreach (var c in line)
        {
            if (c == ' ' || c == '\t')
                indent.Append(c);
            else
                break;
        }
        return indent.ToString();
    }

    /// <summary>
    /// Handles backspace at all cursor positions.
    /// </summary>
    public void BackspaceAtAllCursors()
    {
        if (_selections.Count == 0) return;

        var document = _textView.Document;
        var mainSelection = _textArea.Selection;
        var mainSegment = mainSelection.SurroundingSegment;

        // Collect all selections including main with their indices
        var allSelections = new List<(int Offset, int Length, bool IsMain)>();

        for (int i = 0; i < _selections.Count; i++)
        {
            allSelections.Add((_selections[i].StartOffset, _selections[i].Length, false));
        }

        // Add main selection/caret position
        if (mainSegment != null)
        {
            allSelections.Add((mainSegment.Offset, mainSegment.Length, true));
        }
        else
        {
            allSelections.Add((_textArea.Caret.Offset, 0, true));
        }

        // Sort by offset descending for deletion
        var sortedDesc = allSelections.OrderByDescending(s => s.Offset).ToList();

        document.BeginUpdate();
        try
        {
            foreach (var (offset, length, _) in sortedDesc)
            {
                if (length > 0)
                {
                    // Delete selection
                    document.Remove(offset, length);
                }
                else if (offset > 0)
                {
                    // Delete character before cursor
                    document.Remove(offset - 1, 1);
                }
            }
        }
        finally
        {
            document.EndUpdate();
        }

        // Calculate new positions (process in ascending order)
        var sortedAsc = allSelections.OrderBy(s => s.Offset).ToList();
        int adjustment = 0;

        // Clear and rebuild selections with updated positions
        _selections.Clear();
        _anchors.Clear();
        _carets.Clear();

        int mainNewOffset = 0;

        foreach (var (offset, length, isMain) in sortedAsc)
        {
            int deleteAmount = length > 0 ? length : (offset > 0 ? 1 : 0);
            var newOffset = Math.Max(0, offset + adjustment - (length > 0 ? 0 : 1));
            adjustment -= deleteAmount;

            if (isMain)
            {
                mainNewOffset = newOffset;
            }
            else
            {
                _selections.Add(new TextSegment { StartOffset = newOffset, Length = 0 });
                _anchors.Add(newOffset);
                _carets.Add(newOffset);
            }
        }

        // Update main caret position
        _textArea.Caret.Offset = mainNewOffset;
        _textArea.Selection = Selection.Create(_textArea, mainNewOffset, mainNewOffset);

        _textView.InvalidateLayer(Layer);
    }

    /// <summary>
    /// Handles delete key at all cursor positions.
    /// </summary>
    public void DeleteAtAllCursors()
    {
        if (_selections.Count == 0) return;

        var document = _textView.Document;
        var mainSelection = _textArea.Selection;
        var mainSegment = mainSelection.SurroundingSegment;

        // Collect all selections including main with their indices
        var allSelections = new List<(int Offset, int Length, bool IsMain)>();

        for (int i = 0; i < _selections.Count; i++)
        {
            allSelections.Add((_selections[i].StartOffset, _selections[i].Length, false));
        }

        // Add main selection/caret position
        if (mainSegment != null)
        {
            allSelections.Add((mainSegment.Offset, mainSegment.Length, true));
        }
        else
        {
            allSelections.Add((_textArea.Caret.Offset, 0, true));
        }

        // Sort by offset descending for deletion
        var sortedDesc = allSelections.OrderByDescending(s => s.Offset).ToList();
        int originalTextLength = document.TextLength;

        document.BeginUpdate();
        try
        {
            foreach (var (offset, length, _) in sortedDesc)
            {
                if (length > 0)
                {
                    document.Remove(offset, length);
                }
                else if (offset < document.TextLength)
                {
                    document.Remove(offset, 1);
                }
            }
        }
        finally
        {
            document.EndUpdate();
        }

        // Calculate new positions (process in ascending order)
        var sortedAsc = allSelections.OrderBy(s => s.Offset).ToList();
        int adjustment = 0;
        int runningTextLength = originalTextLength;

        // Clear and rebuild selections with updated positions
        _selections.Clear();
        _anchors.Clear();
        _carets.Clear();

        int mainNewOffset = 0;

        foreach (var (offset, length, isMain) in sortedAsc)
        {
            int deleteAmount = length > 0 ? length : (offset < runningTextLength ? 1 : 0);
            var newOffset = Math.Max(0, offset + adjustment);
            adjustment -= deleteAmount;
            runningTextLength -= deleteAmount;

            if (isMain)
            {
                mainNewOffset = newOffset;
            }
            else
            {
                _selections.Add(new TextSegment { StartOffset = newOffset, Length = 0 });
                _anchors.Add(newOffset);
                _carets.Add(newOffset);
            }
        }

        // Update main caret position
        _textArea.Caret.Offset = mainNewOffset;
        _textArea.Selection = Selection.Create(_textArea, mainNewOffset, mainNewOffset);

        _textView.InvalidateLayer(Layer);
    }

    /// <summary>
    /// Moves all cursors left by one character, collapsing any selections.
    /// </summary>
    public void MoveAllCursorsLeft()
    {
        var document = _textView.Document;

        // Move additional selections
        for (int i = 0; i < _selections.Count; i++)
        {
            var sel = _selections[i];
            // If there's a selection, collapse to start; otherwise move left
            int newOffset = sel.Length > 0 ? sel.StartOffset : Math.Max(0, sel.StartOffset - 1);
            _selections[i] = new TextSegment { StartOffset = newOffset, Length = 0 };
            _anchors[i] = newOffset; // Reset anchor to new position
            _carets[i] = newOffset;  // Reset caret to new position
        }

        // Move main caret
        var mainSel = _textArea.Selection;
        var mainSegment = mainSel.SurroundingSegment;
        int mainNewOffset;
        if (mainSegment != null && mainSegment.Length > 0)
        {
            mainNewOffset = mainSegment.Offset;
        }
        else
        {
            mainNewOffset = Math.Max(0, _textArea.Caret.Offset - 1);
        }
        _textArea.Caret.Offset = mainNewOffset;
        _textArea.Selection = Selection.Create(_textArea, mainNewOffset, mainNewOffset);

        _textView.InvalidateLayer(Layer);
    }

    /// <summary>
    /// Moves all cursors right by one character, collapsing any selections.
    /// </summary>
    public void MoveAllCursorsRight()
    {
        var document = _textView.Document;

        // Move additional selections
        for (int i = 0; i < _selections.Count; i++)
        {
            var sel = _selections[i];
            // If there's a selection, collapse to end; otherwise move right
            int newOffset = sel.Length > 0 ? sel.EndOffset : Math.Min(document.TextLength, sel.StartOffset + 1);
            _selections[i] = new TextSegment { StartOffset = newOffset, Length = 0 };
            _anchors[i] = newOffset; // Reset anchor to new position
            _carets[i] = newOffset;  // Reset caret to new position
        }

        // Move main caret
        var mainSel = _textArea.Selection;
        var mainSegment = mainSel.SurroundingSegment;
        int mainNewOffset;
        if (mainSegment != null && mainSegment.Length > 0)
        {
            mainNewOffset = mainSegment.EndOffset;
        }
        else
        {
            mainNewOffset = Math.Min(document.TextLength, _textArea.Caret.Offset + 1);
        }
        _textArea.Caret.Offset = mainNewOffset;
        _textArea.Selection = Selection.Create(_textArea, mainNewOffset, mainNewOffset);

        _textView.InvalidateLayer(Layer);
    }

    /// <summary>
    /// Extends all selections left by one character (Shift+Left behavior).
    /// </summary>
    public void ExtendAllSelectionsLeft()
    {
        var document = _textView.Document;

        // Extend additional selections
        for (int i = 0; i < _selections.Count; i++)
        {
            int anchor = _anchors[i];
            int caret = _carets[i];

            // Move caret left
            int newCaret = Math.Max(0, caret - 1);
            _carets[i] = newCaret;

            // Create selection from anchor to new caret
            int start = Math.Min(anchor, newCaret);
            int end = Math.Max(anchor, newCaret);
            _selections[i] = new TextSegment { StartOffset = start, Length = end - start };
        }

        // Extend main selection
        var mainSel = _textArea.Selection;
        int mainAnchor, mainCaret;
        if (mainSel.IsEmpty)
        {
            mainAnchor = mainCaret = _textArea.Caret.Offset;
        }
        else
        {
            // Determine anchor vs caret based on caret position
            var seg = mainSel.SurroundingSegment;
            if (_textArea.Caret.Offset == seg.EndOffset)
            {
                mainAnchor = seg.Offset;
                mainCaret = seg.EndOffset;
            }
            else
            {
                mainAnchor = seg.EndOffset;
                mainCaret = seg.Offset;
            }
        }

        int mainNewCaret = Math.Max(0, mainCaret - 1);
        int mainStart = Math.Min(mainAnchor, mainNewCaret);
        int mainEnd = Math.Max(mainAnchor, mainNewCaret);
        _textArea.Selection = Selection.Create(_textArea, mainStart, mainEnd);
        _textArea.Caret.Offset = mainNewCaret;

        _textView.InvalidateLayer(Layer);
    }

    /// <summary>
    /// Extends all selections right by one character (Shift+Right behavior).
    /// </summary>
    public void ExtendAllSelectionsRight()
    {
        var document = _textView.Document;

        // Extend additional selections
        for (int i = 0; i < _selections.Count; i++)
        {
            int anchor = _anchors[i];
            int caret = _carets[i];

            // Move caret right
            int newCaret = Math.Min(document.TextLength, caret + 1);
            _carets[i] = newCaret;

            // Create selection from anchor to new caret
            int start = Math.Min(anchor, newCaret);
            int end = Math.Max(anchor, newCaret);
            _selections[i] = new TextSegment { StartOffset = start, Length = end - start };
        }

        // Extend main selection
        var mainSel = _textArea.Selection;
        int mainAnchor, mainCaret;
        if (mainSel.IsEmpty)
        {
            mainAnchor = mainCaret = _textArea.Caret.Offset;
        }
        else
        {
            // Determine anchor vs caret based on caret position
            var seg = mainSel.SurroundingSegment;
            if (_textArea.Caret.Offset == seg.EndOffset)
            {
                mainAnchor = seg.Offset;
                mainCaret = seg.EndOffset;
            }
            else
            {
                mainAnchor = seg.EndOffset;
                mainCaret = seg.Offset;
            }
        }

        int mainNewCaret = Math.Min(document.TextLength, mainCaret + 1);
        int mainStart = Math.Min(mainAnchor, mainNewCaret);
        int mainEnd = Math.Max(mainAnchor, mainNewCaret);
        _textArea.Selection = Selection.Create(_textArea, mainStart, mainEnd);
        _textArea.Caret.Offset = mainNewCaret;

        _textView.InvalidateLayer(Layer);
    }

    /// <summary>
    /// Moves all cursors up by one line, collapsing any selections.
    /// </summary>
    public void MoveAllCursorsUp()
    {
        var document = _textView.Document;

        // Move additional selections
        for (int i = 0; i < _selections.Count; i++)
        {
            var sel = _selections[i];
            int offset = sel.Length > 0 ? sel.StartOffset : sel.EndOffset;
            var loc = document.GetLocation(offset);

            int newOffset;
            if (loc.Line > 1)
            {
                var targetLine = document.GetLineByNumber(loc.Line - 1);
                int targetColumn = Math.Min(loc.Column, targetLine.Length + 1);
                newOffset = targetLine.Offset + targetColumn - 1;
            }
            else
            {
                newOffset = sel.StartOffset;
            }
            _selections[i] = new TextSegment { StartOffset = newOffset, Length = 0 };
            _anchors[i] = newOffset; // Reset anchor
            _carets[i] = newOffset;  // Reset caret
        }

        // Move main caret
        var mainSel = _textArea.Selection;
        var mainSegment = mainSel.SurroundingSegment;
        int mainOffset = mainSegment != null && mainSegment.Length > 0 ? mainSegment.Offset : _textArea.Caret.Offset;
        var mainLoc = document.GetLocation(mainOffset);

        if (mainLoc.Line > 1)
        {
            var targetLine = document.GetLineByNumber(mainLoc.Line - 1);
            int targetColumn = Math.Min(mainLoc.Column, targetLine.Length + 1);
            int mainNewOffset = targetLine.Offset + targetColumn - 1;
            _textArea.Caret.Offset = mainNewOffset;
            _textArea.Selection = Selection.Create(_textArea, mainNewOffset, mainNewOffset);
        }
        else
        {
            // Just collapse selection
            _textArea.Selection = Selection.Create(_textArea, mainOffset, mainOffset);
        }

        _textView.InvalidateLayer(Layer);
    }

    /// <summary>
    /// Moves all cursors down by one line, collapsing any selections.
    /// </summary>
    public void MoveAllCursorsDown()
    {
        var document = _textView.Document;

        // Move additional selections
        for (int i = 0; i < _selections.Count; i++)
        {
            var sel = _selections[i];
            int offset = sel.Length > 0 ? sel.EndOffset : sel.EndOffset;
            var loc = document.GetLocation(offset);

            int newOffset;
            if (loc.Line < document.LineCount)
            {
                var targetLine = document.GetLineByNumber(loc.Line + 1);
                int targetColumn = Math.Min(loc.Column, targetLine.Length + 1);
                newOffset = targetLine.Offset + targetColumn - 1;
            }
            else
            {
                newOffset = sel.EndOffset;
            }
            _selections[i] = new TextSegment { StartOffset = newOffset, Length = 0 };
            _anchors[i] = newOffset; // Reset anchor
            _carets[i] = newOffset;  // Reset caret
        }

        // Move main caret
        var mainSel = _textArea.Selection;
        var mainSegment = mainSel.SurroundingSegment;
        int mainOffset = mainSegment != null && mainSegment.Length > 0 ? mainSegment.EndOffset : _textArea.Caret.Offset;
        var mainLoc = document.GetLocation(mainOffset);

        if (mainLoc.Line < document.LineCount)
        {
            var targetLine = document.GetLineByNumber(mainLoc.Line + 1);
            int targetColumn = Math.Min(mainLoc.Column, targetLine.Length + 1);
            int mainNewOffset = targetLine.Offset + targetColumn - 1;
            _textArea.Caret.Offset = mainNewOffset;
            _textArea.Selection = Selection.Create(_textArea, mainNewOffset, mainNewOffset);
        }
        else
        {
            // Just collapse selection
            _textArea.Selection = Selection.Create(_textArea, mainOffset, mainOffset);
        }

        _textView.InvalidateLayer(Layer);
    }

    /// <summary>
    /// Moves all cursors to the beginning of their respective lines, collapsing any selections.
    /// </summary>
    public void MoveAllCursorsHome()
    {
        var document = _textView.Document;

        // Move additional selections
        for (int i = 0; i < _selections.Count; i++)
        {
            var sel = _selections[i];
            int offset = sel.Length > 0 ? sel.StartOffset : sel.StartOffset;
            var line = document.GetLineByOffset(offset);
            int newOffset = line.Offset;
            _selections[i] = new TextSegment { StartOffset = newOffset, Length = 0 };
            _anchors[i] = newOffset;
            _carets[i] = newOffset;
        }

        // Move main caret
        var mainSel = _textArea.Selection;
        var mainSegment = mainSel.SurroundingSegment;
        int mainOffset = mainSegment != null && mainSegment.Length > 0 ? mainSegment.Offset : _textArea.Caret.Offset;
        var mainLine = document.GetLineByOffset(mainOffset);
        int mainNewOffset = mainLine.Offset;
        _textArea.Caret.Offset = mainNewOffset;
        _textArea.Selection = Selection.Create(_textArea, mainNewOffset, mainNewOffset);

        _textView.InvalidateLayer(Layer);
    }

    /// <summary>
    /// Moves all cursors to the end of their respective lines, collapsing any selections.
    /// </summary>
    public void MoveAllCursorsEnd()
    {
        var document = _textView.Document;

        // Move additional selections
        for (int i = 0; i < _selections.Count; i++)
        {
            var sel = _selections[i];
            int offset = sel.Length > 0 ? sel.EndOffset : sel.EndOffset;
            var line = document.GetLineByOffset(offset);
            int newOffset = line.EndOffset;
            _selections[i] = new TextSegment { StartOffset = newOffset, Length = 0 };
            _anchors[i] = newOffset;
            _carets[i] = newOffset;
        }

        // Move main caret
        var mainSel = _textArea.Selection;
        var mainSegment = mainSel.SurroundingSegment;
        int mainOffset = mainSegment != null && mainSegment.Length > 0 ? mainSegment.EndOffset : _textArea.Caret.Offset;
        var mainLine = document.GetLineByOffset(mainOffset);
        int mainNewOffset = mainLine.EndOffset;
        _textArea.Caret.Offset = mainNewOffset;
        _textArea.Selection = Selection.Create(_textArea, mainNewOffset, mainNewOffset);

        _textView.InvalidateLayer(Layer);
    }

    /// <summary>
    /// Extends all selections to the beginning of their respective lines (Shift+Home behavior).
    /// </summary>
    public void ExtendAllSelectionsHome()
    {
        var document = _textView.Document;

        // Extend additional selections
        for (int i = 0; i < _selections.Count; i++)
        {
            int anchor = _anchors[i];
            int caret = _carets[i];

            // Move caret to line start
            var line = document.GetLineByOffset(caret);
            int newCaret = line.Offset;
            _carets[i] = newCaret;

            // Create selection from anchor to new caret
            int start = Math.Min(anchor, newCaret);
            int end = Math.Max(anchor, newCaret);
            _selections[i] = new TextSegment { StartOffset = start, Length = end - start };
        }

        // Extend main selection
        var mainSel = _textArea.Selection;
        int mainAnchor, mainCaret;
        if (mainSel.IsEmpty)
        {
            mainAnchor = mainCaret = _textArea.Caret.Offset;
        }
        else
        {
            var seg = mainSel.SurroundingSegment;
            if (_textArea.Caret.Offset == seg.EndOffset)
            {
                mainAnchor = seg.Offset;
                mainCaret = seg.EndOffset;
            }
            else
            {
                mainAnchor = seg.EndOffset;
                mainCaret = seg.Offset;
            }
        }

        var mainLine = document.GetLineByOffset(mainCaret);
        int mainNewCaret = mainLine.Offset;
        int mainStart = Math.Min(mainAnchor, mainNewCaret);
        int mainEnd = Math.Max(mainAnchor, mainNewCaret);
        _textArea.Selection = Selection.Create(_textArea, mainStart, mainEnd);
        _textArea.Caret.Offset = mainNewCaret;

        _textView.InvalidateLayer(Layer);
    }

    /// <summary>
    /// Extends all selections to the end of their respective lines (Shift+End behavior).
    /// </summary>
    public void ExtendAllSelectionsEnd()
    {
        var document = _textView.Document;

        // Extend additional selections
        for (int i = 0; i < _selections.Count; i++)
        {
            int anchor = _anchors[i];
            int caret = _carets[i];

            // Move caret to line end
            var line = document.GetLineByOffset(caret);
            int newCaret = line.EndOffset;
            _carets[i] = newCaret;

            // Create selection from anchor to new caret
            int start = Math.Min(anchor, newCaret);
            int end = Math.Max(anchor, newCaret);
            _selections[i] = new TextSegment { StartOffset = start, Length = end - start };
        }

        // Extend main selection
        var mainSel = _textArea.Selection;
        int mainAnchor, mainCaret;
        if (mainSel.IsEmpty)
        {
            mainAnchor = mainCaret = _textArea.Caret.Offset;
        }
        else
        {
            var seg = mainSel.SurroundingSegment;
            if (_textArea.Caret.Offset == seg.EndOffset)
            {
                mainAnchor = seg.Offset;
                mainCaret = seg.EndOffset;
            }
            else
            {
                mainAnchor = seg.EndOffset;
                mainCaret = seg.Offset;
            }
        }

        var mainLine = document.GetLineByOffset(mainCaret);
        int mainNewCaret = mainLine.EndOffset;
        int mainStart = Math.Min(mainAnchor, mainNewCaret);
        int mainEnd = Math.Max(mainAnchor, mainNewCaret);
        _textArea.Selection = Selection.Create(_textArea, mainStart, mainEnd);
        _textArea.Caret.Offset = mainNewCaret;

        _textView.InvalidateLayer(Layer);
    }

    /// <summary>
    /// Pastes clipboard text at all cursor positions.
    /// </summary>
    public void PasteAtAllCursors()
    {
        if (_selections.Count == 0) return;

        string clipboardText;
        try
        {
            if (!System.Windows.Clipboard.ContainsText())
                return;
            clipboardText = System.Windows.Clipboard.GetText();
            if (string.IsNullOrEmpty(clipboardText))
                return;
        }
        catch
        {
            return;
        }

        // Use InsertTextAtAllCursors which handles all the offset adjustments
        InsertTextAtAllCursors(clipboardText);
    }

    /// <summary>
    /// Gets the word boundary to the left of the given offset.
    /// </summary>
    private int GetWordStartLeft(int offset)
    {
        var document = _textView.Document;
        if (offset <= 0) return 0;

        var text = document.Text;
        int pos = offset - 1;

        // Skip whitespace
        while (pos > 0 && char.IsWhiteSpace(text[pos]))
            pos--;

        if (pos <= 0) return 0;

        // Determine the character class at current position
        char c = text[pos];
        bool isWordChar = char.IsLetterOrDigit(c) || c == '_';

        // Move back through same character class
        while (pos > 0)
        {
            char prev = text[pos - 1];
            bool prevIsWordChar = char.IsLetterOrDigit(prev) || prev == '_';

            if (isWordChar != prevIsWordChar)
                break;

            pos--;
        }

        return pos;
    }

    /// <summary>
    /// Gets the word boundary to the right of the given offset.
    /// </summary>
    private int GetWordEndRight(int offset)
    {
        var document = _textView.Document;
        var text = document.Text;
        int length = text.Length;

        if (offset >= length) return length;

        int pos = offset;

        // Skip whitespace
        while (pos < length && char.IsWhiteSpace(text[pos]))
            pos++;

        if (pos >= length) return length;

        // Determine the character class at current position
        char c = text[pos];
        bool isWordChar = char.IsLetterOrDigit(c) || c == '_';

        // Move forward through same character class
        while (pos < length)
        {
            char curr = text[pos];
            bool currIsWordChar = char.IsLetterOrDigit(curr) || curr == '_';

            if (isWordChar != currIsWordChar)
                break;

            pos++;
        }

        return pos;
    }

    /// <summary>
    /// Moves all cursors left by one word (Ctrl+Left behavior).
    /// </summary>
    public void MoveAllCursorsWordLeft()
    {
        var document = _textView.Document;

        // Move additional selections
        for (int i = 0; i < _selections.Count; i++)
        {
            var sel = _selections[i];
            int offset = sel.Length > 0 ? sel.StartOffset : sel.StartOffset;
            int newOffset = GetWordStartLeft(offset);
            _selections[i] = new TextSegment { StartOffset = newOffset, Length = 0 };
            _anchors[i] = newOffset;
            _carets[i] = newOffset;
        }

        // Move main caret
        var mainSel = _textArea.Selection;
        var mainSegment = mainSel.SurroundingSegment;
        int mainOffset = mainSegment != null && mainSegment.Length > 0 ? mainSegment.Offset : _textArea.Caret.Offset;
        int mainNewOffset = GetWordStartLeft(mainOffset);
        _textArea.Caret.Offset = mainNewOffset;
        _textArea.Selection = Selection.Create(_textArea, mainNewOffset, mainNewOffset);

        _textView.InvalidateLayer(Layer);
    }

    /// <summary>
    /// Moves all cursors right by one word (Ctrl+Right behavior).
    /// </summary>
    public void MoveAllCursorsWordRight()
    {
        var document = _textView.Document;

        // Move additional selections
        for (int i = 0; i < _selections.Count; i++)
        {
            var sel = _selections[i];
            int offset = sel.Length > 0 ? sel.EndOffset : sel.EndOffset;
            int newOffset = GetWordEndRight(offset);
            _selections[i] = new TextSegment { StartOffset = newOffset, Length = 0 };
            _anchors[i] = newOffset;
            _carets[i] = newOffset;
        }

        // Move main caret
        var mainSel = _textArea.Selection;
        var mainSegment = mainSel.SurroundingSegment;
        int mainOffset = mainSegment != null && mainSegment.Length > 0 ? mainSegment.EndOffset : _textArea.Caret.Offset;
        int mainNewOffset = GetWordEndRight(mainOffset);
        _textArea.Caret.Offset = mainNewOffset;
        _textArea.Selection = Selection.Create(_textArea, mainNewOffset, mainNewOffset);

        _textView.InvalidateLayer(Layer);
    }

    /// <summary>
    /// Extends all selections left by one word (Ctrl+Shift+Left behavior).
    /// </summary>
    public void ExtendAllSelectionsWordLeft()
    {
        var document = _textView.Document;

        // Extend additional selections
        for (int i = 0; i < _selections.Count; i++)
        {
            int anchor = _anchors[i];
            int caret = _carets[i];

            // Move caret to word start
            int newCaret = GetWordStartLeft(caret);
            _carets[i] = newCaret;

            // Create selection from anchor to new caret
            int start = Math.Min(anchor, newCaret);
            int end = Math.Max(anchor, newCaret);
            _selections[i] = new TextSegment { StartOffset = start, Length = end - start };
        }

        // Extend main selection
        var mainSel = _textArea.Selection;
        int mainAnchor, mainCaret;
        if (mainSel.IsEmpty)
        {
            mainAnchor = mainCaret = _textArea.Caret.Offset;
        }
        else
        {
            var seg = mainSel.SurroundingSegment;
            if (_textArea.Caret.Offset == seg.EndOffset)
            {
                mainAnchor = seg.Offset;
                mainCaret = seg.EndOffset;
            }
            else
            {
                mainAnchor = seg.EndOffset;
                mainCaret = seg.Offset;
            }
        }

        int mainNewCaret = GetWordStartLeft(mainCaret);
        int mainStart = Math.Min(mainAnchor, mainNewCaret);
        int mainEnd = Math.Max(mainAnchor, mainNewCaret);
        _textArea.Selection = Selection.Create(_textArea, mainStart, mainEnd);
        _textArea.Caret.Offset = mainNewCaret;

        _textView.InvalidateLayer(Layer);
    }

    /// <summary>
    /// Extends all selections right by one word (Ctrl+Shift+Right behavior).
    /// </summary>
    public void ExtendAllSelectionsWordRight()
    {
        var document = _textView.Document;

        // Extend additional selections
        for (int i = 0; i < _selections.Count; i++)
        {
            int anchor = _anchors[i];
            int caret = _carets[i];

            // Move caret to word end
            int newCaret = GetWordEndRight(caret);
            _carets[i] = newCaret;

            // Create selection from anchor to new caret
            int start = Math.Min(anchor, newCaret);
            int end = Math.Max(anchor, newCaret);
            _selections[i] = new TextSegment { StartOffset = start, Length = end - start };
        }

        // Extend main selection
        var mainSel = _textArea.Selection;
        int mainAnchor, mainCaret;
        if (mainSel.IsEmpty)
        {
            mainAnchor = mainCaret = _textArea.Caret.Offset;
        }
        else
        {
            var seg = mainSel.SurroundingSegment;
            if (_textArea.Caret.Offset == seg.EndOffset)
            {
                mainAnchor = seg.Offset;
                mainCaret = seg.EndOffset;
            }
            else
            {
                mainAnchor = seg.EndOffset;
                mainCaret = seg.Offset;
            }
        }

        int mainNewCaret = GetWordEndRight(mainCaret);
        int mainStart = Math.Min(mainAnchor, mainNewCaret);
        int mainEnd = Math.Max(mainAnchor, mainNewCaret);
        _textArea.Selection = Selection.Create(_textArea, mainStart, mainEnd);
        _textArea.Caret.Offset = mainNewCaret;

        _textView.InvalidateLayer(Layer);
    }

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (_selections.Count == 0) return;

        for (int i = 0; i < _selections.Count; i++)
        {
            var segment = _selections[i];

            // Skip invalid segments
            if (segment.StartOffset < 0 || segment.EndOffset > textView.Document.TextLength)
                continue;

            if (segment.Length > 0)
            {
                // Draw selection highlight
                foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, segment))
                {
                    drawingContext.DrawRectangle(SelectionBrush, null, rect);
                }
            }

            // Draw caret line at the tracked caret position
            // Use GetVisualPosition to properly locate the caret (GetRectsForSegment returns empty for zero-length)
            int caretOffset = _carets[i];
            if (caretOffset < 0 || caretOffset > textView.Document.TextLength)
                continue;

            var visualLine = textView.GetVisualLine(textView.Document.GetLineByOffset(caretOffset).LineNumber);
            if (visualLine != null)
            {
                int relativeOffset = caretOffset - visualLine.FirstDocumentLine.Offset;
                var caretPos = visualLine.GetVisualPosition(relativeOffset, VisualYPosition.LineTop);
                var caretBottom = visualLine.GetVisualPosition(relativeOffset, VisualYPosition.LineBottom);

                // Adjust for scroll position
                caretPos = caretPos - textView.ScrollOffset;
                caretBottom = caretBottom - textView.ScrollOffset;

                // Draw a thin vertical line as caret
                var caretRect = new Rect(caretPos.X, caretPos.Y, 2, caretBottom.Y - caretPos.Y);
                drawingContext.DrawRectangle(CaretBrush, null, caretRect);
            }
        }
    }
}
