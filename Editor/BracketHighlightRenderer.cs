using System;
using System.Windows;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;

namespace Code2Viz.Editor;

public class BracketHighlightRenderer : IBackgroundRenderer
{
    private readonly TextView _textView;
    public BracketSearchResult? Result { get; set; }
    
    // Highlight styling - using a subtle box around the bracket
    private static readonly Brush HighlightBrush = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)); 
    private static readonly Pen HighlightPen = new Pen(new SolidColorBrush(Colors.LightGray), 1);

    static BracketHighlightRenderer()
    {
        HighlightBrush.Freeze();
        HighlightPen.Freeze();
    }

    public BracketHighlightRenderer(TextView textView)
    {
        _textView = textView;
    }

    public KnownLayer Layer => KnownLayer.Selection;

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (Result == null) return;

        // Draw highlights
        DrawHighlight(textView, drawingContext, Result.OpeningOffset);
        DrawHighlight(textView, drawingContext, Result.ClosingOffset);
    }

    private void DrawHighlight(TextView textView, DrawingContext drawingContext, int offset)
    {
        if (offset < 0 || offset >= textView.Document.TextLength) return;
        
        var textSegment = new TextSegment { StartOffset = offset, Length = 1 };
        foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, textSegment))
        {
             // Adjust rect slightly to look good
             var drawRect = new Rect(rect.Location, new Size(rect.Width, rect.Height));
             drawingContext.DrawRectangle(HighlightBrush, HighlightPen, drawRect);
        }
    }
}

public class BracketSearchResult
{
    public int OpeningOffset { get; set; }
    public int ClosingOffset { get; set; }
}

public static class BracketSearcher 
{
    public static BracketSearchResult? SearchBracket(TextDocument document, int caretOffset)
    {
        if (document == null) return null;

        // Check character to the right of caret (e.g. caret |() -> highlighting '(', ')' )
        if (caretOffset < document.TextLength)
        {
             var c = document.GetCharAt(caretOffset);
             if (IsOpenBracket(c) || IsCloseBracket(c))
             {
                 return FindMatching(document, caretOffset, c);
             }
        }
        
        // Check character to the left of caret (e.g. caret )| -> highlighting '(', ')' )
        if (caretOffset > 0)
        {
             var c = document.GetCharAt(caretOffset - 1);
              if (IsOpenBracket(c) || IsCloseBracket(c))
             {
                 return FindMatching(document, caretOffset - 1, c);
             }
        }
        
        return null;
    }

    private static BracketSearchResult? FindMatching(TextDocument document, int offset, char c) 
    {
        bool isOpen = IsOpenBracket(c);
        char match = GetMatching(c);
        int matchOffset = -1;
        
        if (isOpen) 
        {
            matchOffset = FindMatchingClose(document, offset + 1, c, match);
        }
        else 
        {
            matchOffset = FindMatchingOpen(document, offset - 1, match, c);
        }

        if (matchOffset >= 0)
        {
            return new BracketSearchResult 
            { 
               OpeningOffset = isOpen ? offset : matchOffset,
               ClosingOffset = isOpen ? matchOffset : offset
            };
        }
        return null;
    }
    
    private static int FindMatchingClose(TextDocument document, int startOffset, char open, char close)
    {
        int depth = 1;
        for (int i = startOffset; i < document.TextLength; i++)
        {
            char c = document.GetCharAt(i);
            if (c == open) depth++;
            else if (c == close)
            {
                depth--;
                if (depth == 0) return i;
            }
        }
        return -1;
    }

    private static int FindMatchingOpen(TextDocument document, int startOffset, char open, char close)
    {
        int depth = 1;
        for (int i = startOffset; i >= 0; i--)
        {
            char c = document.GetCharAt(i);
            if (c == close) depth++; // Nested closing bracket means we need one more open
            else if (c == open)
            {
                depth--;
                if (depth == 0) return i;
            }
        }
        return -1;
    }

    private static bool IsOpenBracket(char c) => c == '(' || c == '[' || c == '{' || c == '<';
    private static bool IsCloseBracket(char c) => c == ')' || c == ']' || c == '}' || c == '>';
    private static char GetMatching(char c) => c switch {
       '(' => ')', '[' => ']', '{' => '}', '<' => '>',
       ')' => '(', ']' => '[', '}' => '{', '>' => '<',
       _ => '\0'
    };
}
