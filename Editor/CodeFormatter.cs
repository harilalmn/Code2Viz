using System.Text;
using System.Text.RegularExpressions;

namespace Code2Viz.Editor;

public static partial class CodeFormatter
{
    public static string Format(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return code;

        // Pre-process: Ensure Allman style (opening brace on new line)
        // 1. Handle ") {" -> ")\n{" (methods, if, while, for, etc.)
        code = Regex.Replace(code, @"\)\s*\{", ")\n{");
        
        // 2. Handle "else {" -> "else\n{"
        code = Regex.Replace(code, @"\belse\s*\{", "else\n{");
        
        // 3. Handle "try {" -> "try\n{"
        code = Regex.Replace(code, @"\btry\s*\{", "try\n{");
        
        // 4. Handle "finally {" -> "finally\n{"
        code = Regex.Replace(code, @"\bfinally\s*\{", "finally\n{");

        // 5. Handle "do {" -> "do\n{"
        code = Regex.Replace(code, @"\bdo\s*\{", "do\n{");
        
        // 6. Handle "struct/class X {" -> "class X\n{"
        // This is trickier as it involves identifiers, but let's try a safe approach for class/struct/interface/namespace
        // We match the end of the declaration.
        // Simplified: if a line ends with "Identifier {", split it.
        // code = Regex.Replace(code, @"([a-zA-Z0-9_>])\s*\{", "$1\n{"); // This might be too aggressive (e.g. object initializer)
        // Let's stick to the user's request for "formatting a method" which is covered by ") {" usually.

        var lines = code.Split('\n');
        var result = new StringBuilder();
        var indentLevel = 0;
        var indentString = "    "; // 4 spaces

        for (int i = 0; i < lines.Length; i++)
        {
            var rawLine = lines[i];
            var line = rawLine.Trim().TrimEnd('\r');

            if (string.IsNullOrEmpty(line))
            {
                result.AppendLine();
                continue;
            }

            // Count braces to determine indent changes
            var (openBraces, closeBraces) = CountBracesOutsideStrings(line);

            // Decrease indent BEFORE writing if line starts with closing brace
            // This ensures the closing brace aligns with its opening statement
            if (line.StartsWith('}') || line.StartsWith(')'))
            {
                indentLevel = Math.Max(0, indentLevel - 1);
                // Don't double-count this brace in post-processing
                closeBraces = Math.Max(0, closeBraces - 1);
            }

            // Add properly indented line
            result.Append(string.Concat(Enumerable.Repeat(indentString, indentLevel)));
            result.AppendLine(FormatLine(line));

            // Adjust indent based on remaining braces (e.g., "} else {" has both)
            indentLevel += openBraces - closeBraces;

            // Handle cases like "if (...)" without braces - indent next line
            // But ONLY if the next line is NOT a starting brace (Allman style handled by brace logic)
            if (IsControlStatementWithoutBrace(line))
            {
                var nextLineStartsWithBrace = false;
                // Peek ahead to check for {
                for (int j = i + 1; j < lines.Length; j++)
                {
                   var next = lines[j].Trim();
                   if (!string.IsNullOrEmpty(next))
                   {
                       if (next.StartsWith("{"))
                           nextLineStartsWithBrace = true;
                       break;
                   }
                }

                if (!nextLineStartsWithBrace)
                    indentLevel++;
            }

            indentLevel = Math.Max(0, indentLevel);
        }

        return result.ToString().TrimEnd();
    }

    private static string FormatLine(string line)
    {
        // 1. Hide strings and comments to prevent regex from messing them up
        var (maskedLine, literals) = HideLiterals(line);

        // 2. Perform formatting on code only
        
        // Add space after keywords
        maskedLine = KeywordSpaceRegex().Replace(maskedLine, "$1 $2");

        // Add space around operators
        // Add space around operators, but NOT around ++, --, or standalone = when part of =>
        maskedLine = OperatorSpaceRegex().Replace(maskedLine, match =>
        {
            var op = match.Groups[1].Value;
            if (op == "++" || op == "--")
                return op; // Return operator as-is (no extra spaces)
            if (op == "=>")
                return " => "; // Lambda arrow - ensure proper spacing
            return " " + op + " ";
        });

        // Fix any accidentally split lambda arrows (= >) back to => with proper spacing
        // Use regex to handle variable spacing: "= >" or "=  >" etc.
        maskedLine = SplitLambdaRegex().Replace(maskedLine, " => ");

        // Clean up multiple spaces
        maskedLine = MultiSpaceRegex().Replace(maskedLine, " ");

        // Add space after commas
        maskedLine = CommaSpaceRegex().Replace(maskedLine, ", ");

        // Add space after semicolons (except at end of line)
        maskedLine = SemicolonSpaceRegex().Replace(maskedLine, "; ");

        // Clean up spaces before punctuation
        maskedLine = SpaceBeforePunctuationRegex().Replace(maskedLine, "$1");

        // Clean up spaces inside parentheses
        maskedLine = SpaceAfterOpenParenRegex().Replace(maskedLine, "(");
        maskedLine = SpaceBeforeCloseParenRegex().Replace(maskedLine, ")");

        // 3. Restore literals
        return RestoreLiterals(maskedLine, literals).Trim();
    }

    private static (string masked, List<string> literals) HideLiterals(string line)
    {
        var literals = new List<string>();
        var sb = new StringBuilder();
        var currentLiteral = new StringBuilder();
        
        bool inString = false;
        bool inChar = false;
        bool inVerbatim = false;
        
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            char prev = i > 0 ? line[i - 1] : '\0';

            // Check for comment start (if not in string/char)
            if (!inString && !inChar && c == '/' && i + 1 < line.Length && line[i + 1] == '/')
            {
                // Capture everything from here to end as a literal
                var comment = line.Substring(i);
                literals.Add(comment);
                sb.Append($"__LIT_{literals.Count - 1}__");
                break; // Done with this line
            }

            // Handle escape sequences in strings/chars
            if ((inString || inChar) && !inVerbatim && prev == '\\')
            {
                currentLiteral.Append(c);
                continue;
            }

            // Check for verbatim string start @"
            if (!inString && !inChar && c == '"' && prev == '@')
            {
                // We need to backtrack to capture the @ in the literal
                // But wait, we already appended @ to sb in previous iteration?
                // Actually, logic is tricky with character-by-character.
                // Let's restart logic to be block-based or careful.
                
                // Simpler: Just track state. If state changes to "in literal", start capturing.
                // But we masked the previous char! 
                
                // Correct approach: Look ahead/current to start literal.
            }
            // THIS LOOP IS GETTING COMPLEX. LET'S SIMPLIFY.
        }
        
        // RESTARTING implementation with cleaner state machine
        literals.Clear();
        sb.Clear();
        currentLiteral.Clear();
        inString = false;
        inChar = false;
        inVerbatim = false;
        
        bool inBlockComment = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            // Check for end of block comment first
            if (inBlockComment)
            {
                currentLiteral.Append(c);
                if (c == '*' && i + 1 < line.Length && line[i + 1] == '/')
                {
                    currentLiteral.Append('/');
                    i++; // Skip the /
                    literals.Add(currentLiteral.ToString());
                    sb.Append($"__LIT_{literals.Count - 1}__");
                    currentLiteral.Clear();
                    inBlockComment = false;
                }
                continue;
            }

            // Check for start of literal
            if (!inString && !inChar)
            {
                // Block comment /*
                if (c == '/' && i + 1 < line.Length && line[i + 1] == '*')
                {
                    inBlockComment = true;
                    currentLiteral.Append("/*");
                    i++; // Skip the *
                    continue;
                }

                // Line comment //
                if (c == '/' && i + 1 < line.Length && line[i + 1] == '/')
                {
                    string comment = line.Substring(i);
                    literals.Add(comment);
                    sb.Append($"__LIT_{literals.Count - 1}__");
                    return (sb.ToString(), literals);
                }

                // Verbatim String @"
                if (c == '@' && i + 1 < line.Length && line[i + 1] == '"')
                {
                    inString = true;
                    inVerbatim = true;
                    currentLiteral.Append(c);
                    currentLiteral.Append(line[i + 1]);
                    i++; // Skip quote
                    continue;
                }
                
                // Interpolated String $"
                if (c == '$' && i + 1 < line.Length && line[i + 1] == '"')
                {
                    inString = true;
                    inVerbatim = false; // Regular interpolation for now (ignoring $@" combo for simplicity)
                    currentLiteral.Append(c);
                    currentLiteral.Append(line[i + 1]);
                    i++; // Skip quote
                    continue;
                }

                // Regular String "
                if (c == '"')
                {
                    inString = true;
                    inVerbatim = false;
                    currentLiteral.Append(c);
                    continue;
                }

                // Char '
                if (c == '\'')
                {
                    inChar = true;
                    currentLiteral.Append(c);
                    continue;
                }

                // Normal code char
                sb.Append(c);
            }
            else
            {
                // Inside literal
                currentLiteral.Append(c);
                
                // Check for end of literal
                bool endOfLiteral = false;
                
                if (inString)
                {
                    if (inVerbatim)
                    {
                        if (c == '"')
                        {
                            if (i + 1 < line.Length && line[i + 1] == '"')
                            {
                                // Escaped quote "" in verbatim
                                currentLiteral.Append('"');
                                i++; 
                            }
                            else
                            {
                                endOfLiteral = true;
                            }
                        }
                    }
                    else // Regular string
                    {
                        if (c == '"' && !IsEscaped(line, i))
                        {
                            endOfLiteral = true;
                        }
                    }
                }
                else if (inChar)
                {
                    if (c == '\'' && !IsEscaped(line, i))
                    {
                        endOfLiteral = true;
                    }
                }
                
                if (endOfLiteral)
                {
                    literals.Add(currentLiteral.ToString());
                    sb.Append($"__LIT_{literals.Count - 1}__");
                    currentLiteral.Clear();
                    inString = false;
                    inChar = false;
                    inVerbatim = false;
                }
            }
        }
        
        // If line ends inside a literal (e.g. unclosed string), just append it
        if (currentLiteral.Length > 0)
        {
             literals.Add(currentLiteral.ToString());
             sb.Append($"__LIT_{literals.Count - 1}__");
        }
        
        return (sb.ToString(), literals);
    }
    
    private static bool IsEscaped(string text, int index)
    {
        int backslashes = 0;
        for (int i = index - 1; i >= 0; i--)
        {
            if (text[i] == '\\') backslashes++;
            else break;
        }
        return backslashes % 2 != 0;
    }

    private static string RestoreLiterals(string maskedLine, List<string> literals)
    {
        for (int i = 0; i < literals.Count; i++)
        {
            maskedLine = maskedLine.Replace($"__LIT_{i}__", literals[i]);
            // Also handle case where formatter added spaces around the placeholder
            // e.g. " __LIT_0__ " -> we want to preserve original spacing if possible, 
            // but the formatter was specifically asked to fix spacing.
            // However, typical behavior is that literals should be restored EXACTLY.
            // The formatter might have done: var x=__LIT_0__; -> var x = __LIT_0__ ;
            // This is acceptable for the surrounding code.
        }
        return maskedLine;
    }

    private static bool IsControlStatementWithoutBrace(string line)
    {
        var controlKeywords = new[] { "if", "else if", "else", "for", "foreach", "while" };
        foreach (var keyword in controlKeywords)
        {
            if (line.StartsWith(keyword) && !line.EndsWith("{") && !line.EndsWith(";"))
            {
                return true;
            }
        }
        return false;
    }

    private static (int open, int close) CountBracesOutsideStrings(string line)
    {
        int open = 0, close = 0;
        bool inString = false;
        bool inChar = false;
        bool inVerbatim = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            char prev = i > 0 ? line[i - 1] : '\0';

            // Handle escape sequences
            if ((inString || inChar) && !inVerbatim && prev == '\\')
            {
                continue;
            }

            // Check for verbatim string start @"
            if (!inString && !inChar && c == '"' && prev == '@')
            {
                inString = true;
                inVerbatim = true;
                continue;
            }

            // Check for interpolated string start $"
            if (!inString && !inChar && c == '"' && prev == '$')
            {
                inString = true;
                continue;
            }

            // Toggle string state
            if (c == '"' && !inChar)
            {
                if (inVerbatim && i + 1 < line.Length && line[i + 1] == '"')
                {
                    i++; // Skip escaped quote in verbatim string
                    continue;
                }
                if (inString)
                {
                    inString = false;
                    inVerbatim = false;
                }
                else
                {
                    inString = true;
                }
                continue;
            }

            // Toggle char state
            if (c == '\'' && !inString)
            {
                inChar = !inChar;
                continue;
            }

            // Count braces only outside strings and chars
            if (!inString && !inChar)
            {
                if (c == '{') open++;
                else if (c == '}') close++;
            }
        }

        return (open, close);
    }

    [GeneratedRegex(@"\b(if|else|for|foreach|while|switch|catch|using)\b(\()")]
    private static partial Regex KeywordSpaceRegex();

    // Order matters: longer/compound operators first, then single char operators
    // Use negative lookahead to prevent = from matching when followed by >
    [GeneratedRegex(@"\s*(=>|\+\+|--|==|!=|<=|>=|&&|\|\||\+=|-=|\*=|/=|%=|&=|\|=|\^=|=(?!>)|[+\-*/%&|^])\s*")]
    private static partial Regex OperatorSpaceRegex();

    [GeneratedRegex(@"  +")]
    private static partial Regex MultiSpaceRegex();

    [GeneratedRegex(@",\s*")]
    private static partial Regex CommaSpaceRegex();

    [GeneratedRegex(@";\s*(?!$)")]
    private static partial Regex SemicolonSpaceRegex();

    [GeneratedRegex(@"\s+([;,\)])")]
    private static partial Regex SpaceBeforePunctuationRegex();

    [GeneratedRegex(@"\(\s+")]
    private static partial Regex SpaceAfterOpenParenRegex();

    [GeneratedRegex(@"\s+\)")]
    private static partial Regex SpaceBeforeCloseParenRegex();

    // Match split lambda arrows: "= >" or "=  >" etc. (= followed by spaces then >)
    [GeneratedRegex(@"=\s+>")]
    private static partial Regex SplitLambdaRegex();
}
