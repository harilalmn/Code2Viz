namespace Code2Viz.Project;

public static class Templates
{
    /// <summary>
    /// Generates the entry point template with the project name as the namespace.
    /// </summary>
    public static string GetStartVizTemplate(string projectName)
    {
        // Sanitize project name to be a valid C# identifier
        var safeName = SanitizeIdentifier(projectName);

        return $$"""
            using System;
            using System.Linq;
            using System.Numerics;
            using System.Collections.Generic;
            using Code2Viz.Geometry;
            using Code2Viz.Console;

            namespace {{safeName}}
            {
                public class Viz
                {
                    public static void Main()
                    {
                        VizConsole.Log("Hello World");
                        // Shapes appear on canvas automatically when created
                        var p = new VPoint(0, 0);
                    }
                }
            }
            """;
    }

    /// <summary>
    /// Sanitizes a string to be a valid C# identifier.
    /// Used for namespace and class names.
    /// </summary>
    public static string SanitizeIdentifier(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "MyProject";

        // Replace invalid characters with underscores
        var result = new System.Text.StringBuilder();
        foreach (var c in name)
        {
            if (char.IsLetterOrDigit(c) || c == '_')
                result.Append(c);
            else if (c == ' ' || c == '-' || c == '.')
                result.Append('_');
        }

        var identifier = result.ToString();

        // Ensure it doesn't start with a digit
        if (identifier.Length > 0 && char.IsDigit(identifier[0]))
            identifier = "_" + identifier;

        return string.IsNullOrEmpty(identifier) ? "MyProject" : identifier;
    }

    public const string EmptyModuleTemplate = """
        using System;
        using System.Linq;
        using System.Numerics;
        using System.Collections.Generic;
        using Code2Viz.Geometry;
        using Code2Viz.Console;

        namespace {0}
        {{
            public class {1}
            {{
                // Add your code here
            }}
        }}
        """;

    /// <summary>
    /// Generates a p5.js-style sketch template with Setup() + Draw() blocks.
    /// Geometry uses the C2VGeometry namespace; shapes are constructed fresh each frame.
    /// </summary>
    public static string GetStartSketchTemplate(string projectName)
    {
        var safeName = SanitizeIdentifier(projectName);

        return $$"""
            using System;
            using C2VGeometry;
            using Code2Viz.Sketching;
            using Code2Viz.Console;

            namespace {{safeName}}
            {
                public class MySketch : Sketch
                {
                    public override void Setup()
                    {
                        Size(800, 600);
                        Background("Black");
                    }

                    public override void Draw()
                    {
                        // Fresh shapes each frame — orbit around origin so the circle stays in view.
                        var r = 200.0;
                        var x = r * Math.Sin(ElapsedSeconds);
                        var y = r * Math.Cos(ElapsedSeconds);
                        new VCircle(new VXYZ(x, y), 12) { FillColor = "Cyan" };
                    }
                }
            }
            """;
    }
}
