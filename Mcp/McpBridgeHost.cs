using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Threading;
using Code2Viz.Canvas;
using Code2Viz.Execution;
using Code2Viz.Geometry;
using Code2Viz.McpBridge;
using Code2Viz.Project;

namespace Code2Viz.Mcp;

/// <summary>
/// Hosts a named pipe server inside the WPF app to receive commands from the MCP server.
/// All canvas/editor operations are dispatched to the UI thread.
/// </summary>
public class McpBridgeHost : IDisposable
{
    private readonly IpcServer _server;
    private readonly Dispatcher _dispatcher;
    private readonly Func<string, Task<string>> _executeCode;
    private readonly Func<string, Task> _exportPng;
    private readonly Func<IReadOnlyList<IDrawable>> _getShapes;

    public McpBridgeHost(
        Dispatcher dispatcher,
        Func<string, Task<string>> executeCode,
        Func<string, Task> exportPng,
        Func<IReadOnlyList<IDrawable>> getShapes)
    {
        _dispatcher = dispatcher;
        _executeCode = executeCode;
        _exportPng = exportPng;
        _getShapes = getShapes;
        _server = new IpcServer(HandleRequest);
    }

    public void Start() => _server.Start();
    public void Stop() => _server.Stop();

    private async Task<IpcResponse> HandleRequest(IpcRequest request)
    {
        try
        {
            return request.Command switch
            {
                "execute_vizcode" => await HandleExecuteVizCode(request),
                "clear_canvas" => await HandleClearCanvas(request),
                "get_canvas_state" => await HandleGetCanvasState(request),
                "export_png" => await HandleExportPng(request),
                "get_console_output" => HandleGetConsoleOutput(request),
                _ => IpcResponse.Fail(request.Id, $"Unknown command: {request.Command}")
            };
        }
        catch (Exception ex)
        {
            return IpcResponse.Fail(request.Id, ex.Message);
        }
    }

    private async Task<IpcResponse> HandleExecuteVizCode(IpcRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Payload))
            return IpcResponse.Fail(request.Id, "No code provided");

        var code = AssignVariableNames(request.Payload!);

        var result = await _dispatcher.InvokeAsync(async () =>
        {
            return await _executeCode(code);
        }).Task.Unwrap();

        return IpcResponse.Ok(request.Id, result);
    }

    /// <summary>
    /// Preprocesses code to assign variable names to standalone "new V...()" statements.
    /// The AnimationNameRewriter uses variable names to mark shapes as named,
    /// and HideUnnamedShapes() hides shapes without names. This ensures MCP-sent
    /// code always produces visible shapes.
    /// </summary>
    private static string AssignVariableNames(string code)
    {
        int counter = 0;
        // Match lines like: new VCircle(...); or new VCircle(...) { ... };
        // that are NOT already preceded by "var x =" or "Type x ="
        return Regex.Replace(code, @"^(\s*)new\s+(V\w+)\s*\(", match =>
        {
            var indent = match.Groups[1].Value;
            var typeName = match.Groups[2].Value;
            var varName = $"_mcp_{typeName.ToLowerInvariant()}_{counter++}";
            return $"{indent}var {varName} = new {typeName}(";
        }, RegexOptions.Multiline);
    }

    private async Task<IpcResponse> HandleClearCanvas(IpcRequest request)
    {
        await _dispatcher.InvokeAsync(() =>
        {
            CanvasRenderer.Instance.Clear();
        });

        return IpcResponse.Ok(request.Id, "Canvas cleared");
    }

    private async Task<IpcResponse> HandleGetCanvasState(IpcRequest request)
    {
        var json = await _dispatcher.InvokeAsync(() =>
        {
            var shapes = _getShapes();
            var shapeList = new List<object>();

            foreach (var drawable in shapes)
            {
                if (drawable is Shape shape)
                {
                    var bounds = shape.GetBounds();
                    shapeList.Add(new
                    {
                        id = shape.Id,
                        type = shape.GetType().Name,
                        name = shape.Name,
                        color = shape.Color,
                        fillColor = shape.FillColor,
                        lineWeight = shape.LineWeight,
                        isVisible = shape.IsVisible,
                        bounds = new
                        {
                            minX = bounds.Min.X,
                            minY = bounds.Min.Y,
                            maxX = bounds.Max.X,
                            maxY = bounds.Max.Y
                        }
                    });
                }
            }

            return JsonSerializer.Serialize(new { shapeCount = shapeList.Count, shapes = shapeList });
        });

        return IpcResponse.Ok(request.Id, json);
    }

    private async Task<IpcResponse> HandleExportPng(IpcRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Payload))
            return IpcResponse.Fail(request.Id, "No filename provided");

        try
        {
            await _dispatcher.InvokeAsync(async () =>
            {
                await _exportPng(request.Payload!);
            }).Task.Unwrap();

            return IpcResponse.Ok(request.Id, $"Exported to {request.Payload}");
        }
        catch (Exception ex)
        {
            return IpcResponse.Fail(request.Id, $"Export failed: {ex.Message}");
        }
    }

    private IpcResponse HandleGetConsoleOutput(IpcRequest request)
    {
        var output = Console.ConsoleOutput.Instance.GetFormattedOutput();
        return IpcResponse.Ok(request.Id, output);
    }

    public void Dispose()
    {
        Stop();
    }
}
