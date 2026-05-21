using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;
using Pen = System.Windows.Media.Pen;
using Rect = System.Windows.Rect;

namespace Code2Viz.Editor;

public class SelectionHighlightRenderer : IBackgroundRenderer
{
    private readonly TextView _textView;
    private string _currentSelection = string.Empty;
    
    // Subtle background highlight for occurrences
    private static readonly Brush HighlightBrush = new SolidColorBrush(Color.FromArgb(40, 150, 150, 150));
    private static readonly Pen? HighlightPen = null; // No border

    static SelectionHighlightRenderer()
    {
        HighlightBrush.Freeze();
    }

    public SelectionHighlightRenderer(TextView textView)
    {
        _textView = textView;
    }

    public KnownLayer Layer => KnownLayer.Background;

    public void UpdateSelection(string text)
    {
        _currentSelection = text;
        _textView.InvalidateLayer(Layer);
    }

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (string.IsNullOrWhiteSpace(_currentSelection) || _currentSelection.Length < 2)
            return;

        var document = textView.Document;
        if (document == null) return;

        var text = document.Text;
        var selectionLength = _currentSelection.Length;
        int offset = 0;

        // Limit maximum matches to avoid performance issues on large files
        int matchCount = 0;
        const int MaxMatches = 1000;

        while ((offset = text.IndexOf(_currentSelection, offset, StringComparison.Ordinal)) != -1)
        {
            if (matchCount++ > MaxMatches) break;

            // Optional: Check if it's a whole word? 
            // VS Code default behavior for selection highlight is *contains*, not necessarily whole word,
            // unless configured otherwise. Sticking to simple substring match for now as per "occurrences" request.

            var segment = new TextSegment { StartOffset = offset, Length = selectionLength };
            
            // Only draw if visible
            // This check happens inside GetRectsForSegment but we can optimize if needed
            
            foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, segment))
            {
                drawingContext.DrawRectangle(HighlightBrush, HighlightPen,
                    VisualLineRectHelpers.ClampToTextRow(rect, textView));
            }

            offset += selectionLength;
        }
    }
}
