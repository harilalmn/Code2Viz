using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Rendering;

namespace Code2Viz.Editor;

/// <summary>
/// Renders multiple selection highlights and manages multi-cursor editing for Ctrl+D feature.
/// </summary>
public class MultiSelectionRenderer : IBackgroundRenderer
{
    private readonly TextView _textView;
    private readonly TextArea _textArea;
    private readonly List<TextSegment> _selections = new();

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
    /// Adds a new selection segment.
    /// </summary>
    public void AddSelection(int offset, int length)
    {
        _selections.Add(new TextSegment { StartOffset = offset, Length = length });
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
            _textView.InvalidateLayer(Layer);
        }
    }

    /// <summary>
    /// Checks if there are any additional selections.
    /// </summary>
    public bool HasSelections => _selections.Count > 0;

    /// <summary>
    /// Inserts text at all cursor positions (main + additional selections).
    /// </summary>
    public void InsertTextAtAllCursors(string text)
    {
        if (_selections.Count == 0) return;

        var document = _textView.Document;
        var mainSelection = _textArea.Selection;
        var mainSegment = mainSelection.SurroundingSegment;

        // Collect all selections including main, sorted by offset descending
        // (process from end to start to avoid offset shifts affecting earlier positions)
        var allSelections = new List<(int Offset, int Length)>();

        foreach (var sel in _selections)
        {
            allSelections.Add((sel.StartOffset, sel.Length));
        }

        // Add main selection/caret position (SurroundingSegment can be null if no selection)
        if (mainSegment != null)
        {
            allSelections.Add((mainSegment.Offset, mainSegment.Length));
        }
        else
        {
            // No selection, use caret position
            allSelections.Add((_textArea.Caret.Offset, 0));
        }

        // Sort by offset descending
        allSelections = allSelections.OrderByDescending(s => s.Offset).ToList();

        // Begin update for undo grouping
        document.BeginUpdate();
        try
        {
            foreach (var (offset, length) in allSelections)
            {
                // Replace selection with new text
                document.Replace(offset, length, text);
            }
        }
        finally
        {
            document.EndUpdate();
        }

        // Update selections to be zero-length at new cursor positions
        // Calculate new positions (all selections now have the inserted text)
        var newSelections = new List<TextSegment>();
        int adjustment = 0;

        // Process in ascending order for position calculation
        foreach (var (offset, length) in allSelections.OrderBy(s => s.Offset))
        {
            var newOffset = offset + adjustment + text.Length;
            adjustment += text.Length - length;
            newSelections.Add(new TextSegment { StartOffset = newOffset, Length = 0 });
        }

        // Remove the last one (that's the main caret) and update others
        _selections.Clear();
        for (int i = 0; i < newSelections.Count - 1; i++)
        {
            _selections.Add(newSelections[i]);
        }

        // Update main caret position
        var lastPos = newSelections[newSelections.Count - 1].StartOffset;
        _textArea.Caret.Offset = lastPos;
        _textArea.Selection = Selection.Create(_textArea, lastPos, lastPos);

        _textView.InvalidateLayer(Layer);
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

        // Collect all selections including main
        var allSelections = new List<(int Offset, int Length)>();

        foreach (var sel in _selections)
        {
            allSelections.Add((sel.StartOffset, sel.Length));
        }

        // Add main selection/caret position
        if (mainSegment != null)
        {
            allSelections.Add((mainSegment.Offset, mainSegment.Length));
        }
        else
        {
            allSelections.Add((_textArea.Caret.Offset, 0));
        }

        // Sort by offset descending
        allSelections = allSelections.OrderByDescending(s => s.Offset).ToList();

        document.BeginUpdate();
        try
        {
            foreach (var (offset, length) in allSelections)
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

        // Update cursor positions
        UpdateCursorPositionsAfterBackspace(allSelections);
    }

    private void UpdateCursorPositionsAfterBackspace(List<(int Offset, int Length)> originalSelections)
    {
        var newSelections = new List<TextSegment>();
        int adjustment = 0;

        foreach (var (offset, length) in originalSelections.OrderBy(s => s.Offset))
        {
            int deleteAmount = length > 0 ? length : (offset > 0 ? 1 : 0);
            var newOffset = Math.Max(0, offset + adjustment - (length > 0 ? 0 : 1));
            adjustment -= deleteAmount;
            newSelections.Add(new TextSegment { StartOffset = newOffset, Length = 0 });
        }

        _selections.Clear();
        for (int i = 0; i < newSelections.Count - 1; i++)
        {
            _selections.Add(newSelections[i]);
        }

        var lastPos = newSelections[newSelections.Count - 1].StartOffset;
        _textArea.Caret.Offset = lastPos;
        _textArea.Selection = Selection.Create(_textArea, lastPos, lastPos);

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

        var allSelections = new List<(int Offset, int Length)>();

        foreach (var sel in _selections)
        {
            allSelections.Add((sel.StartOffset, sel.Length));
        }

        // Add main selection/caret position
        if (mainSegment != null)
        {
            allSelections.Add((mainSegment.Offset, mainSegment.Length));
        }
        else
        {
            allSelections.Add((_textArea.Caret.Offset, 0));
        }

        allSelections = allSelections.OrderByDescending(s => s.Offset).ToList();

        document.BeginUpdate();
        try
        {
            foreach (var (offset, length) in allSelections)
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

        // Update cursor positions (cursors stay in place, just selections become zero-length)
        UpdateCursorPositionsAfterDelete(allSelections);
    }

    private void UpdateCursorPositionsAfterDelete(List<(int Offset, int Length)> originalSelections)
    {
        var newSelections = new List<TextSegment>();
        int adjustment = 0;
        var document = _textView.Document;

        foreach (var (offset, length) in originalSelections.OrderBy(s => s.Offset))
        {
            int deleteAmount = length > 0 ? length : (offset < document.TextLength + adjustment ? 1 : 0);
            var newOffset = offset + adjustment;
            adjustment -= deleteAmount;
            newSelections.Add(new TextSegment { StartOffset = Math.Max(0, newOffset), Length = 0 });
        }

        _selections.Clear();
        for (int i = 0; i < newSelections.Count - 1; i++)
        {
            _selections.Add(newSelections[i]);
        }

        var lastPos = newSelections[newSelections.Count - 1].StartOffset;
        _textArea.Caret.Offset = lastPos;
        _textArea.Selection = Selection.Create(_textArea, lastPos, lastPos);

        _textView.InvalidateLayer(Layer);
    }

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (_selections.Count == 0) return;

        foreach (var segment in _selections)
        {
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

            // Draw caret line at the end of selection
            var caretSegment = new TextSegment { StartOffset = segment.EndOffset, Length = 0 };
            foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, caretSegment))
            {
                // Draw a thin vertical line as caret
                var caretRect = new Rect(rect.X, rect.Y, 2, rect.Height);
                drawingContext.DrawRectangle(CaretBrush, null, caretRect);
            }
        }
    }
}
