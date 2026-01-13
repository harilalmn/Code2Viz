using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Code2Viz.Canvas;

namespace Code2Viz.Execution;

public class ScriptResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
}

public class ScriptRunner
{
    private static readonly ScriptOptions DefaultOptions;

    static ScriptRunner()
    {
        DefaultOptions = ScriptOptions.Default
            .AddReferences(typeof(Geometry.VPoint).Assembly)
            .AddImports(
                "System",
                "System.Math",
                "System.Collections.Generic",
                "Code2Viz.Geometry"
            );
    }

    public async Task<ScriptResult> ExecuteAsync(string code)
    {
        try
        {
            // Clear previous shapes
            CanvasRenderer.Instance.Clear();

            // Execute the script
            await CSharpScript.EvaluateAsync(code, DefaultOptions);

            return new ScriptResult { Success = true };
        }
        catch (CompilationErrorException ex)
        {
            var errors = string.Join(Environment.NewLine,
                ex.Diagnostics.Select(d => d.ToString()));
            return new ScriptResult
            {
                Success = false,
                Error = $"Compilation Error:\n{errors}"
            };
        }
        catch (Exception ex)
        {
            return new ScriptResult
            {
                Success = false,
                Error = $"Runtime Error: {ex.Message}"
            };
        }
    }
}
