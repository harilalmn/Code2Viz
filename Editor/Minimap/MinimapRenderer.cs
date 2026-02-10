using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;

namespace Code2Viz.Editor.Minimap
{
    /// <summary>
    /// Renders code to a minimap bitmap using simplified regex-based tokenization.
    /// Uses a 5-color palette for fast rendering without full Roslyn parsing.
    /// </summary>
    public class MinimapRenderer
    {
        // Simplified color palette (VSCode-like dark theme)
        private static readonly Color KeywordColor = Color.FromRgb(86, 156, 214);      // Blue
        private static readonly Color StringColor = Color.FromRgb(206, 145, 120);      // Orange
        private static readonly Color CommentColor = Color.FromRgb(106, 153, 85);      // Green
        private static readonly Color TypeColor = Color.FromRgb(78, 201, 176);         // Teal
        private static readonly Color NumberColor = Color.FromRgb(181, 206, 168);      // Light green
        private static readonly Color DefaultColor = Color.FromRgb(212, 212, 212);     // Light gray

        // Cached brushes for performance
        private static readonly SolidColorBrush KeywordBrush = new(KeywordColor);
        private static readonly SolidColorBrush StringBrush = new(StringColor);
        private static readonly SolidColorBrush CommentBrush = new(CommentColor);
        private static readonly SolidColorBrush TypeBrush = new(TypeColor);
        private static readonly SolidColorBrush NumberBrush = new(NumberColor);
        private static readonly SolidColorBrush DefaultBrush = new(DefaultColor);

        static MinimapRenderer()
        {
            KeywordBrush.Freeze();
            StringBrush.Freeze();
            CommentBrush.Freeze();
            TypeBrush.Freeze();
            NumberBrush.Freeze();
            DefaultBrush.Freeze();
        }

        // Regex patterns for tokenization
        private static readonly Regex TokenPattern = new(
            @"(?<comment>//.*$|/\*[\s\S]*?\*/)|" +                     // Comments
            @"(?<string>""(?:[^""\\]|\\.)*""|@""[^""]*""|'(?:[^'\\]|\\.)*')|" +  // Strings
            @"(?<number>\b\d+\.?\d*[fFdDmM]?\b)|" +                    // Numbers
            @"(?<keyword>\b(?:abstract|as|base|bool|break|byte|case|catch|char|checked|class|const|continue|decimal|default|delegate|do|double|else|enum|event|explicit|extern|false|finally|fixed|float|for|foreach|goto|if|implicit|in|int|interface|internal|is|lock|long|namespace|new|null|object|operator|out|override|params|private|protected|public|readonly|record|ref|return|sbyte|sealed|short|sizeof|stackalloc|static|string|struct|switch|this|throw|true|try|typeof|uint|ulong|unchecked|unsafe|ushort|using|var|virtual|void|volatile|while|async|await|when|where|yield|and|or|not|with|init|required|file|scoped|global|dynamic|nint|nuint)\b)|" +
            @"(?<type>\b[A-Z][a-zA-Z0-9_]*\b)",                        // Types (PascalCase)
            RegexOptions.Multiline | RegexOptions.Compiled);

        /// <summary>
        /// Renders the code to a DrawingVisual for the minimap.
        /// </summary>
        /// <param name="code">Source code to render</param>
        /// <param name="lineHeight">Height per line in pixels (typically 2)</param>
        /// <param name="charWidth">Width per character in pixels (typically 1)</param>
        /// <param name="maxWidth">Maximum width of the minimap</param>
        /// <returns>DrawingVisual containing the rendered minimap</returns>
        public DrawingVisual Render(string code, double lineHeight, double charWidth, double maxWidth)
        {
            var visual = new DrawingVisual();
            using var dc = visual.RenderOpen();

            if (string.IsNullOrEmpty(code)) return visual;

            var lines = code.Split('\n');
            double y = 0;

            foreach (var line in lines)
            {
                RenderLine(dc, line, y, lineHeight, charWidth, maxWidth);
                y += lineHeight;
            }

            return visual;
        }

        /// <summary>
        /// Renders a single line of code.
        /// </summary>
        private void RenderLine(DrawingContext dc, string line, double y, double lineHeight, double charWidth, double maxWidth)
        {
            if (string.IsNullOrWhiteSpace(line)) return;

            // Tokenize the line
            var tokens = TokenizeLine(line);

            foreach (var token in tokens)
            {
                double x = token.Start * charWidth;
                double width = Math.Min(token.Length * charWidth, maxWidth - x);

                if (x >= maxWidth || width <= 0) continue;

                var brush = GetBrush(token.Kind);
                dc.DrawRectangle(brush, null, new Rect(x, y, width, lineHeight));
            }
        }

        /// <summary>
        /// Tokenizes a line of code using regex.
        /// </summary>
        private List<MinimapToken> TokenizeLine(string line)
        {
            var tokens = new List<MinimapToken>();
            var matches = TokenPattern.Matches(line);

            foreach (Match match in matches)
            {
                MinimapTokenKind kind;

                if (match.Groups["comment"].Success)
                    kind = MinimapTokenKind.Comment;
                else if (match.Groups["string"].Success)
                    kind = MinimapTokenKind.String;
                else if (match.Groups["number"].Success)
                    kind = MinimapTokenKind.Number;
                else if (match.Groups["keyword"].Success)
                    kind = MinimapTokenKind.Keyword;
                else if (match.Groups["type"].Success)
                    kind = MinimapTokenKind.Type;
                else
                    continue;

                tokens.Add(new MinimapToken
                {
                    Start = match.Index,
                    Length = match.Length,
                    Kind = kind
                });
            }

            // Fill gaps with default tokens for non-whitespace
            var defaultTokens = new List<MinimapToken>();
            int lastEnd = 0;

            foreach (var token in tokens)
            {
                // Add default tokens for gaps (but skip whitespace)
                if (token.Start > lastEnd)
                {
                    var gap = line.Substring(lastEnd, token.Start - lastEnd);
                    int gapStart = lastEnd;

                    for (int i = 0; i < gap.Length; i++)
                    {
                        if (!char.IsWhiteSpace(gap[i]))
                        {
                            // Find consecutive non-whitespace
                            int start = i;
                            while (i < gap.Length && !char.IsWhiteSpace(gap[i])) i++;

                            defaultTokens.Add(new MinimapToken
                            {
                                Start = gapStart + start,
                                Length = i - start,
                                Kind = MinimapTokenKind.Default
                            });
                        }
                    }
                }
                lastEnd = token.Start + token.Length;
            }

            // Handle remaining text after last token
            if (lastEnd < line.Length)
            {
                var remaining = line.Substring(lastEnd);
                for (int i = 0; i < remaining.Length; i++)
                {
                    if (!char.IsWhiteSpace(remaining[i]))
                    {
                        int start = i;
                        while (i < remaining.Length && !char.IsWhiteSpace(remaining[i])) i++;

                        defaultTokens.Add(new MinimapToken
                        {
                            Start = lastEnd + start,
                            Length = i - start,
                            Kind = MinimapTokenKind.Default
                        });
                    }
                }
            }

            tokens.AddRange(defaultTokens);
            tokens.Sort((a, b) => a.Start.CompareTo(b.Start));

            return tokens;
        }

        private static SolidColorBrush GetBrush(MinimapTokenKind kind)
        {
            return kind switch
            {
                MinimapTokenKind.Keyword => KeywordBrush,
                MinimapTokenKind.String => StringBrush,
                MinimapTokenKind.Comment => CommentBrush,
                MinimapTokenKind.Type => TypeBrush,
                MinimapTokenKind.Number => NumberBrush,
                _ => DefaultBrush
            };
        }
    }

    internal struct MinimapToken
    {
        public int Start;
        public int Length;
        public MinimapTokenKind Kind;
    }

    internal enum MinimapTokenKind
    {
        Default,
        Keyword,
        String,
        Comment,
        Type,
        Number
    }
}
