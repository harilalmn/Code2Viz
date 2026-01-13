namespace Code2Viz.Project;

public static class FSharpTemplates
{
    public static string GetStartVizTemplate(string projectName)
    {
        var safeName = Templates.SanitizeIdentifier(projectName);
        return $@"namespace {safeName}

open System
open Code2Viz.Geometry
open Code2Viz.Canvas

module Viz =
    // Entry point for the application
    let Main() =
        Console.WriteLine(""Hello from F#!"")

        // Draw a circle
        let circle = VCircle(VPoint(400.0, 300.0), 100.0)
        circle.FillColor <- ""#FF5733""
        circle.StrokeColor <- ""White""
        circle.StrokeThickness <- 2.0
        circle.Draw()

        // Draw some text
        let text = VText(VPoint(400.0, 300.0), ""Hello F#"")
        text.Height <- 24.0
        text.Color <- ""White""
        text.Draw()
";
    }

    public static string GetEmptyModuleTemplate(string projectName, string moduleName)
    {
        var safeName = Templates.SanitizeIdentifier(projectName);
        var safeModuleName = Templates.SanitizeIdentifier(moduleName);
        return $@"namespace {safeName}

open System
open Code2Viz.Geometry
open Code2Viz.Canvas

module {safeModuleName} =
    // Add your code here
    let example() =
        ()
";
    }
}
