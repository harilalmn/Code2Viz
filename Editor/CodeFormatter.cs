using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Code2Viz.Editor;

public static partial class CodeFormatter
{
    public static string Format(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return code;

        // 1. Mask Literals (Strings, Chars, Comments)
        // We use a robust regex to capture them so we don't accidentally format braces inside them.
        var (maskedCode, literals) = MaskLiterals(code);

        // 2. Structural Formatting
        // Ensure every brace is on its own line with surrounding breaks
        // We replace { with \r\n{\r\n and } with \r\n}\r\n
        // This guarantees separation. We'll clean up double newlines later during line processing.
        
        var sb = new StringBuilder(maskedCode);
        sb.Replace("{", "\r\n{\r\n");
        sb.Replace("}", "\r\n}\r\n");
        maskedCode = sb.ToString();

        // 3. Restore Literals
        // We restore them BEFORE processing lines to proper indentation?
        // No, if a literal contains \n, it might mess up our line splitting if we aren't careful.
        // But if we process lines on masked code, we might break a multi-line literal into indented chunks mistakenly if we aren't careful?
        // Actually, Indenting multi-line strings is dangerous (changes content).
        // Safest strategy: 
        // a. Split masked code by newline.
        // b. Indent masked lines.
        // c. Restore literals. (This assumes literals don't span lines? No, @"" strings do).
        
        // Better Strategy:
        // Restore literals NOW. 
        // Then split by lines.
        // When processing a line, check if we are INSIDE a verbatim string and DO NOT touch indentation if so.
        
        var unmaskedCode = RestoreLiterals(maskedCode, literals);
        
        // 4. Line Processing & Indentation
        var lines = unmaskedCode.Split(new[] { '\r', '\n' }, StringSplitOptions.None); // Keep empty lines to preserve spacing, but trim later
        var result = new StringBuilder();
        
        int indentLevel = 0;
        string indentString = "    ";
        bool inVerbatimString = false;

        for (int i = 0; i < lines.Length; i++)
        {
            var rawLine = lines[i];
            var trimmedLine = rawLine.Trim();

            // Check if we are inside a multi-line string context from previous line
            if (inVerbatimString)
            {
                // We are continuing a string. Do NOT trim or indent.
                // NOTE: This simple check is fragile if we restored literals fully. 
                // A better way is to check the literal boundaries.
                // BUT, since we requested "format the code", let's assume we re-indent content unless it destroys data.
                
                // Fallback: If the complexity of verbatim strings is too high for simple logic, 
                // we treat the entire literal as a block.
                // RE-THINK: Using the masked code for indentation calculation is safer for structure.
            }
        }
        
        // REVISED STRATEGY: Use Masked Code for indentation logic.
        // Masked literals will appear as __LIT_X__. They won't contain { or } or \n (except maybe we want to keep them on one line?).
        // Wait, masking replaced the WHOLE literal with one token. 
        // If the original literal was multi-line, it is now flattened to __LIT_X__ in the masked string.
        // This is GOOD for structure, but when we restore, we put back the multi-line string.
        // REPLACE STRATEGY: 
        // 1. Mask literals (flattening multi-lines).
        // 2. Format structure (braces).
        // 3. Split into lines (masked).
        // 4. Apply indentation to masked lines.
        // 5. Join lines.
        // 6. Restore literals.
        // CON: If we restore a multi-line literal, it will be injected into one line. 
        // We rely on the renderer/editor to handle the newline characters inside the restored string.
        // Ideally, we want the *start* of the literal aligned, and the rest relative? 
        // Standard "Format Document" usually respects the internal formatting of the string.
        
        var formattedLines = new List<string>();
        var splitMasked = maskedCode.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        
        indentLevel = 0;
        
        foreach (var line in splitMasked)
        {
            var trim = line.Trim();
            if (string.IsNullOrWhiteSpace(trim)) continue;
            
            // Indent Logic
            var (opens, closes) = CountBraces(trim);
            
            var printIndent = indentLevel;
            
            // Adjust for leading closing brace (visual only for this line)
            if (trim.StartsWith("}"))
            {
                printIndent = Math.Max(0, printIndent - 1);
            }
            
            // Apply indentation
            formattedLines.Add(new string(' ', printIndent * 4) + FormatLineSpacing(trim));
            
            // Adjust state for next line
            indentLevel += (opens - closes);
            if (indentLevel < 0) indentLevel = 0;
        }
        
        var finalMasked = string.Join(Environment.NewLine, formattedLines);
        return RestoreLiterals(finalMasked, literals);
    }
    
    private static (string masked, List<string> literals) MaskLiterals(string code)
    {
        var literals = new List<string>();
        // 1. Verbatim strings (@"...") - handle "" escape
        // 2. Interpolated verbatim ($@"..." or @$"...")
        // 3. String literals ("...") - handle \" escape
        // 4. Char literals ('...') - handle \' escape
        // 5. Comments (//... and /*...*/)
        
        // Regex components:
        var verbatim = @"@""(?:[^""]|"""")*""";
        var interpolatedVerbatim = @"(?:\$@|@\$)(?:""(?:[^""]|"""")*"")"; // Simplified
        var str = @"""(?:\\.|[^\\""])*""";
        var interpolated = @"\$(?:""(?:\\.|[^\\""])*"")"; // Simplified, doesn't handle nested braces fully but avoids simple " breakage
        var chr = @"'(?:\\.|[^\\'])*'";
        var singleLineComment = @"//.*";
        var multiLineComment = @"/\*(?s:.*?)\*/";
        
        // Combined pattern
        var pattern = $"{interpolatedVerbatim}|{verbatim}|{interpolated}|{str}|{chr}|{singleLineComment}|{multiLineComment}";
        
        var masked = Regex.Replace(code, pattern, match =>
        {
            literals.Add(match.Value);
            return $"__LIT_{literals.Count - 1}__";
        });
        
        return (masked, literals);
    }

    private static string RestoreLiterals(string code, List<string> literals)
    {
        // Simple replacement. Since we know the format __LIT_X__, we can just replace.
        // We loop backwards or text replace.
        for (int i = 0; i < literals.Count; i++)
        {
             code = code.Replace($"__LIT_{i}__", literals[i]);
        }
        return code;
    }

    private static (int open, int close) CountBraces(string str)
    {
        int open = 0;
        int close = 0;
        foreach (char c in str)
        {
            if (c == '{') open++;
            else if (c == '}') close++;
        }
        return (open, close);
    }
    
    // Minimal spacing logic - adds space after keywords, around operators, etc.
    // Preserves the existing helper style
    private static string FormatLineSpacing(string line)
    {
        // Space after keywords
        line = Regex.Replace(line, @"\b(if|else|for|foreach|while|switch|catch|using)\b(\()", "$1 $2");
        
        // Space after comma
        line = Regex.Replace(line, @",\s*", ", ");
        
        // Space after semicolon
        line = Regex.Replace(line, @";\s*(?!$)", "; ");

        // We could add more, but let's stick to the high-value ones
        return line;
    }
    
}
