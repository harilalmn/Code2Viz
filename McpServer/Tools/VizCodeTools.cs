using System.ComponentModel;
using Code2Viz.McpBridge;
using ModelContextProtocol.Server;

namespace Code2Viz.McpServer.Tools;

[McpServerToolType]
public static class VizCodeTools
{
    [McpServerTool, Description(
        "Execute C# vizcode on the Code2Viz canvas. " +
        "The code is inserted as the body of Main(). " +
        "Available imports: System, System.Linq, System.Numerics, System.Collections.Generic, Code2Viz.Geometry, Code2Viz.Console, Code2Viz.Animation. " +
        "IMPORTANT: Always assign shapes to variables so they remain visible. Shapes without variable names are hidden by the animation system. " +
        "CORRECT: var c = new VCircle(0, 0, 50); WRONG: new VCircle(0, 0, 50); " +
        "Available shapes: VPoint(x,y), VLine(x1,y1,x2,y2), VCircle(cx,cy,r), VArc(cx,cy,r,startDeg,endDeg), VRectangle(x,y,w,h), VEllipse(cx,cy,rx,ry), " +
        "VPolygon(params VPoint[]), VPolyline(params VPoint[]), VBezier(x1,y1,cx1,cy1,cx2,cy2,x2,y2), VSpline(params VPoint[]), " +
        "VText(x,y,text) or VText(x,y,text,height), VArrow(x1,y1,x2,y2), VGroup(params Shape[]), VGrid(origin,xCount,yCount,xSpacing,ySpacing,centered), VDimension(x1,y1,x2,y2), VXLine.Horizontal(y)/Vertical(x), VRay.AtAngle(x,y,deg). " +
        "Shape properties: Color, FillColor, LineWeight, LineType(Continuous/Dashed/Dotted/DashDot/DashDotDot/Center/Phantom/Hidden), LineTypeScale, Name, IsVisible, Opacity. " +
        "Shape methods: Move(new VXYZ(dx,dy,0)), Rotate(pivot,angleDeg), Scale(center,factor), Flip(mirrorLine), Clone(), GetBounds() returns BoundingBox(Min,Max,Width,Height,Center,Area), Show(), Hide(), Remove(), Contains(point), DistanceTo(point). " +
        "ICurve methods (VLine,VCircle,VArc,VEllipse,VPolyline,VPolygon,VBezier,VSpline): GetLength(), Divide(n), Measure(segLen), PointAtSegmentLength(len), Project(point), Offset(dist), Intersect(otherCurve), StartPoint, EndPoint, Vertices, SplitAtPoint(pt), NormalAtPoint(pt). " +
        "VText props: Content, Height, Font(VFont enum: Arial,TimesNewRoman,CourierNew,Consolas,etc), FontWeight(Normal/Bold). " +
        "VArrow props: HeadLength(15), HeadAngle(30), DoubleEnded(false). VDimension props: Offset(20), CustomText, TextHeight(12), DecimalPlaces(2). " +
        "VGroup: Add(shape), AddRange(shapes), Remove(shape), ForEach(action), Where(predicate), ApplyColor(), ApplyFillColor(), ApplyLineWeight(), SetOpacity(val), GetCenter(), Flatten(), GetShapesOfType<T>(). " +
        "VPoint: DistanceTo(point), AsVXYZ(), operators +,-,*,/. VXYZ(x,y,z): GetLength(), Normalize(), DotProduct(), CrossProduct(), AngleTo(), static Zero/BasisX/BasisY. " +
        "BoundingBox: returned by GetBounds(), properties Min/Max(VPoint),Width,Height,Center,Area; methods Contains(pt),Intersects(other),Union(other),Expand(dist); supports tuple deconstruction var(min,max)=bounds. " +
        "ArrayOps: shape.LinearArrayX(count,spacing), shape.LinearArrayY(count,spacing), shape.CircularArray(center,count,totalAngle,rotateItems), shape.RectangularArray(rows,cols,rowSpacing,colSpacing), shape.PathArray(curve,count,alignToPath), shape.Mirror(mirrorLine), shape.SpiralArray(center,count,startR,endR,revolutions,rotateItems). " +
        "BooleanOps (VPolygon only): polygon.Union(other), polygon.Intersect(other), polygon.Difference(other), polygon.Xor(other), polygon.OffsetPolygon(dist), polygon.Contains(point), polygon.GetArea(). " +
        "VColor: VColor.Red, .Blue, .Green, etc (static properties). VColor.FromRgb(r,g,b), VColor.GetRandomColor(), VColor.GetRandomVibrantColor(). " +
        "ShapeDefaults: GlobalColor, GlobalFillColor, GlobalLineWeight, GlobalLineType, GlobalLineTypeScale, Reset(). " +
        "Animation: var animator = new Animator(); animator.Repeat=true; animator.Speed=1.5; " +
        "animator.AddToAnimations(new DrawAnimation(shape,duration)); Sequential. " +
        "animator.AddToAnimations(new List<Animation>{...}); Parallel. " +
        "Types: DrawAnimation(target,dur), MoveAnimation(target,new VXYZ(dx,dy,0),dur), RotateAnimation(target,pivot,angleDeg,dur), FadeInAnimation(target,dur), FadeOutAnimation(target,dur), FlipAnimation(target,mirrorAxis,dur). " +
        "Easing: anim.EasingFunction = EasingFunctions.EaseInOutCubic; (Linear,EaseInQuad,EaseOutQuad,EaseInOutQuad,EaseInCubic,EaseOutCubic,EaseInOutCubic). " +
        "animator.Animate(); to start. " +
        "Console output: VizConsole.Log(value) — only method available, auto-tracks file and line number.")]
    public static async Task<string> ExecuteVizcode(
        [Description("C# code to execute as the body of Main(). Example: var circle = new VCircle(0, 0, 50);")] string code)
    {
        using var client = new IpcClient();
        var request = new IpcRequest { Command = "execute_vizcode", Payload = code };
        var response = await client.SendAsync(request);
        return response.Success ? response.Result ?? "Success" : $"Error: {response.Error}";
    }

    [McpServerTool, Description("Clear all shapes from the Code2Viz canvas.")]
    public static async Task<string> ClearCanvas()
    {
        using var client = new IpcClient();
        var request = new IpcRequest { Command = "clear_canvas" };
        var response = await client.SendAsync(request);
        return response.Success ? response.Result ?? "Cleared" : $"Error: {response.Error}";
    }

    [McpServerTool, Description(
        "Get the current state of the Code2Viz canvas as JSON. " +
        "Returns shape count and a list of shapes with their type, name, color, fill color, line weight, visibility, and bounding box.")]
    public static async Task<string> GetCanvasState()
    {
        using var client = new IpcClient();
        var request = new IpcRequest { Command = "get_canvas_state" };
        var response = await client.SendAsync(request);
        return response.Success ? response.Result ?? "{}" : $"Error: {response.Error}";
    }

    [McpServerTool, Description("Export the Code2Viz canvas to a PNG image file.")]
    public static async Task<string> ExportPng(
        [Description("Full file path for the PNG export. Example: C:\\Output\\shapes.png")] string filename)
    {
        using var client = new IpcClient();
        var request = new IpcRequest { Command = "export_png", Payload = filename };
        var response = await client.SendAsync(request);
        return response.Success ? response.Result ?? "Exported" : $"Error: {response.Error}";
    }

    [McpServerTool, Description("Get the console output from the last Code2Viz code execution.")]
    public static async Task<string> GetConsoleOutput()
    {
        using var client = new IpcClient();
        var request = new IpcRequest { Command = "get_console_output" };
        var response = await client.SendAsync(request);
        return response.Success ? response.Result ?? "" : $"Error: {response.Error}";
    }
}
