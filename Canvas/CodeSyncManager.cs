using System.Text.RegularExpressions;
using Code2Viz.Geometry;
using Code2Viz.Project;

namespace Code2Viz.Canvas;

/// <summary>
/// Manages synchronization between shapes on the canvas and their source code.
/// </summary>
public class CodeSyncManager
{
    private static CodeSyncManager? _instance;
    public static CodeSyncManager Instance => _instance ??= new CodeSyncManager();

    // Maps shape ID to source information
    private readonly Dictionary<long, ShapeSourceInfo> _shapeSourceMap = new();

    /// <summary>
    /// Source code information for a shape.
    /// </summary>
    public class ShapeSourceInfo
    {
        public string FilePath { get; set; } = "";
        public int StartLine { get; set; }
        public int EndLine { get; set; }
        public string CodeSnippet { get; set; } = "";
    }

    /// <summary>
    /// Registers a shape with its source code location.
    /// </summary>
    public void RegisterShape(Shape shape, string filePath, int startLine, int endLine, string code)
    {
        _shapeSourceMap[shape.Id] = new ShapeSourceInfo
        {
            FilePath = filePath,
            StartLine = startLine,
            EndLine = endLine,
            CodeSnippet = code
        };
    }

    /// <summary>
    /// Gets the source info for a shape.
    /// </summary>
    public ShapeSourceInfo? GetSourceInfo(Shape shape)
    {
        return _shapeSourceMap.TryGetValue(shape.Id, out var info) ? info : null;
    }

    /// <summary>
    /// Removes source tracking for a shape.
    /// </summary>
    public void UnregisterShape(Shape shape)
    {
        _shapeSourceMap.Remove(shape.Id);
    }

    /// <summary>
    /// Clears all shape-source mappings.
    /// </summary>
    public void Clear()
    {
        _shapeSourceMap.Clear();
    }

    /// <summary>
    /// Updates shape constructor in code with current property values.
    /// </summary>
    /// <param name="content">The file content.</param>
    /// <param name="shape">The shape with updated properties.</param>
    /// <param name="language">The target language.</param>
    /// <returns>Tuple of (new content, was found and updated).</returns>
    public static (string newContent, bool found) UpdateShapeCode(string content, Shape shape, ProjectLanguage language = ProjectLanguage.CSharp)
    {
        var shapeType = shape.GetType().Name;

        // Pattern to match the shape constructor: new VCircle(...) or VCircle(...)
        // This captures the constructor call including parameters
        var pattern = language == ProjectLanguage.FSharp
            ? $@"({shapeType}\s*\()[^)]*(\))"
            : $@"(new\s+{shapeType}\s*\()[^)]*(\))";

        var regex = new Regex(pattern, RegexOptions.Singleline);
        var match = regex.Match(content);

        if (match.Success)
        {
            // Generate the new constructor call
            var newConstructor = CodeGenerator.GenerateConstructorCall(shape, language);

            // Replace the old constructor with the new one
            var newContent = content.Substring(0, match.Index) + newConstructor + content.Substring(match.Index + match.Length);
            return (newContent, true);
        }

        return (content, false);
    }

    /// <summary>
    /// Finds and removes shape code from file content.
    /// </summary>
    /// <param name="content">The file content.</param>
    /// <param name="shape">The shape to find and remove.</param>
    /// <returns>Tuple of (new content, was found).</returns>
    public static (string newContent, bool found) RemoveShapeCode(string content, Shape shape)
    {
        // Generate patterns to match the shape creation code
        var patterns = GetShapePatterns(shape);

        foreach (var pattern in patterns)
        {
            var regex = new Regex(pattern, RegexOptions.Multiline);
            var match = regex.Match(content);
            if (match.Success)
            {
                // Remove the entire line containing the shape
                var startIndex = content.LastIndexOf('\n', match.Index);
                if (startIndex < 0) startIndex = 0;
                else startIndex++; // Move past the newline

                var endIndex = content.IndexOf('\n', match.Index + match.Length);
                if (endIndex < 0) endIndex = content.Length;
                else endIndex++; // Include the newline

                var newContent = content.Remove(startIndex, endIndex - startIndex);
                return (newContent, true);
            }
        }

        return (content, false);
    }

    private static List<string> GetShapePatterns(Shape shape)
    {
        var patterns = new List<string>();
        var shapeType = shape.GetType().Name;

        // Pattern for: new VCircle(...).Draw();
        patterns.Add($@"new\s+{shapeType}\s*\([^)]*\)\s*\.Draw\s*\(\s*\)\s*;");

        // Pattern for: var name = new VCircle(...);
        patterns.Add($@"var\s+\w+\s*=\s*new\s+{shapeType}\s*\([^)]*\)\s*;");

        // Pattern for explicit type: VCircle name = new VCircle(...);
        patterns.Add($@"{shapeType}\s+\w+\s*=\s*new\s+{shapeType}\s*\([^)]*\)\s*;");

        return patterns;
    }

    /// <summary>
    /// Removes multiple shapes from code.
    /// </summary>
    public static string RemoveShapesCode(string content, IEnumerable<Shape> shapes)
    {
        var result = content;
        foreach (var shape in shapes)
        {
            var (newContent, found) = RemoveShapeCode(result, shape);
            if (found)
            {
                result = newContent;
            }
        }
        return result;
    }
}
