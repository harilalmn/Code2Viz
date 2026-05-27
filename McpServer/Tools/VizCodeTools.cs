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
        "Available imports: System, System.Linq, System.Numerics, System.Collections.Generic, C2VGeometry, Code2Viz.Console, Code2Viz.Animation. " +
        "IMPORTANT: Always assign shapes to variables so they remain visible. Shapes without variable names are hidden by the animation system. " +
        "CORRECT: var c = new VCircle(0, 0, 50); WRONG: new VCircle(0, 0, 50); " +
        "Available shapes: VPoint(x,y), VLine(x1,y1,x2,y2) or VLine(start,end) or VLine(start,angleDeg,length), " +
        "VCircle(cx,cy,r) or VCircle(p1,p2,p3) circumcircle, VCircle.FromCenterDiameter(center,d), VCircle.FromTwoPoints(p1,p2), " +
        "VArc(cx,cy,r,startDeg,endDeg) or VArc(start,mid,end), factory: VArc.FromStartCenterEnd/FromCenterStartEnd/FromStartCenterAngle/FromCenterStartAngle/FromStartCenterLength/FromCenterStartLength/FromStartEndRadius/FromStartEndAngle/Continue, " +
        "VRectangle(x,y,w,h), VEllipse(cx,cy,rx,ry) or VEllipse(center,rx,ry,startAngle,endAngle) partial, " +
        "VPolygon(params VXYZ[]) with Slice(p1,p2)/Slice(xline)/Slice(ray), VPolyline(params VXYZ[]), VBezier(x1,y1,cx1,cy1,cx2,cy2,x2,y2), VSpline(params VXYZ[]) with Tension(0.5), " +
        "VText(x,y,text) or VText(x,y,text,height), VArrow(x1,y1,x2,y2), VGroup(params Shape[]), VGrid(origin,xCount,yCount,xSpacing,ySpacing,centered), VDimension(x1,y1,x2,y2), " +
        "VXLine(basePoint,direction)/VXLine(pt1,pt2)/VXLine.Horizontal(y)/Vertical(x), " +
        "VRay(origin,direction)/VRay(origin,throughPoint)/VRay.AtAngle(origin,angleDeg)/VRay.HorizontalRight(origin)/HorizontalLeft/VerticalUp/VerticalDown. " +
        "Shape properties: Color, FillColor, LineWeight, LineType(Continuous/Dashed/Dotted/DashDot/DashDotDot/Center/Phantom/Hidden), LineTypeScale, Name, IsVisible, Opacity. " +
        "Shape methods: Move(new VXYZ(dx,dy,0)), Rotate(pivot,angleDeg), Scale(center,factor), Flip(mirrorLine), Clone(), GetBounds() returns BoundingBox(Min,Max,Width,Height,Center,Area), Show(), Hide(), Remove(), Contains(point), DistanceTo(point). " +
        "ICurve methods (VLine,VCircle,VArc,VEllipse,VPolyline,VPolygon,VBezier,VSpline): GetLength(), Divide(n), Measure(segLen), PointAtSegmentLength(len), Project(point), Offset(dist), Intersect(otherCurve), StartPoint, EndPoint, Vertices, SplitAtPoint(pt), NormalAtPoint(pt). " +
        "VText props: Content, Height, Font(VFont enum: Arial,TimesNewRoman,CourierNew,Consolas,etc), FontWeight(Normal/Bold), Anchor(VTextAnchor enum, default BottomLeft), Angle(degrees CCW around Location, default 0 — Excel-style block rotation). " +
        "VArrow props: HeadLength(15), HeadAngle(30), DoubleEnded(false). VDimension props: Offset(20), CustomText, TextHeight(12), DecimalPlaces(2). " +
        "VGroup: Add(shape), AddRange(shapes), Remove(shape), ForEach(action), Where(predicate), ApplyColor(), ApplyFillColor(), ApplyLineWeight(), SetOpacity(val), GetCenter(), Flatten(), GetShapesOfType<T>(). " +
        "VPoint: DistanceTo(point), AsVXYZ(), operators +,-,*,/. VXYZ(x,y,z): GetLength(), Normalize(), DotProduct(), CrossProduct(), AngleTo(), static Zero/BasisX/BasisY. " +
        "BoundingBox: returned by GetBounds(), properties Min/Max(VXYZ),Width,Height,Center,Area; methods Contains(pt),Intersects(other),Union(other),Expand(dist); supports tuple deconstruction var(min,max)=bounds. " +
        "ArrayOps: shape.LinearArrayX(count,spacing), shape.LinearArrayY(count,spacing), shape.CircularArray(center,count,totalAngle,rotateItems), shape.RectangularArray(rows,cols,rowSpacing,colSpacing), shape.PathArray(curve,count,alignToPath), shape.Mirror(mirrorLine), shape.SpiralArray(center,count,startR,endR,revolutions,rotateItems). " +
        "BooleanOps (VPolygon only): polygon.Union(other), polygon.Intersect(other), polygon.Difference(other), polygon.Xor(other), polygon.OffsetPolygon(dist), polygon.OffsetPolygonSafe(dist), polygon.MaxSafeInwardOffset(), polygon.HasSelfIntersections(), polygon.MakeSimple(), polygon.Contains(point), polygon.GetArea(). " +
        "BooleanOps static: BooleanOps.OffsetPolygon(poly,dist,JoinType,EndType), BooleanOps.Simplify(poly,tolerance), BooleanOps.DifferenceWithHoles/IntersectWithHoles/UnionWithHoles. " +
        "PolygonWithHoles: new PolygonWithHoles(outer), AddHole(hole), props Outer/Holes/Area, Contains(pt), Clone(). " +
        "JoinType enum: Miter(default),Round,Square. EndType enum: Polygon(default),OpenRound,OpenSquare,OpenButt. " +
        "VColor: VColor.Red, .Blue, .Green, etc (static properties). VColor.FromRgb(r,g,b), VColor.FromArgb(a,r,g,b), VColor.WithOpacity(r,g,b,opacity), VColor.GetRandomColor(), VColor.GetRandomVibrantColor(), VColor.GetRandomPastelColor(), VColor.GetVibrantColors(), VColor.GetPastelColors(). " +
        "ShapeDefaults: GlobalColor, GlobalFillColor, GlobalLineWeight, GlobalLineType, GlobalLineTypeScale, Reset(). " +
        "Animation: var animator = new Animator(); animator.Repeat=true; animator.Speed=1.5; " +
        "animator.AddToAnimations(new DrawAnimation(shape,duration)); Sequential. " +
        "animator.AddToAnimations(new List<Animation>{...}); Parallel. " +
        "animator.Pause(seconds); inserts time gap. " +
        "Types: DrawAnimation(target,dur), MoveAnimation(target,new VXYZ(dx,dy,0),dur), RotateAnimation(target,pivot,angleDeg,dur), FadeInAnimation(target,dur), FadeOutAnimation(target,dur), FlipAnimation(target,mirrorAxis,dur). " +
        "Easing: anim.EasingFunction = EasingFunctions.EaseInOutCubic; (Linear,EaseInQuad,EaseOutQuad,EaseInOutQuad,EaseInCubic,EaseOutCubic,EaseInOutCubic). " +
        "animator.Animate(); to start. animator.Stop(); to stop. " +
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

    [McpServerTool, Description(
        "Get the full project context — all C# source files in the current Code2Viz project. " +
        "Returns JSON with fileCount and a files array, each containing fileName, isEntryPoint, and content. " +
        "Use this to understand custom classes, helper methods, and other code defined across the project " +
        "before writing code that references them. The entry point file (StartViz.cs) contains Main(). " +
        "Other files may define custom classes, utility functions, or data that Main() can reference. " +
        "Call this FIRST when working with an existing project to understand available types and resources.")]
    public static async Task<string> GetProjectContext()
    {
        using var client = new IpcClient();
        var request = new IpcRequest { Command = "get_project_context" };
        var response = await client.SendAsync(request);
        return response.Success ? response.Result ?? "{}" : $"Error: {response.Error}";
    }

    [McpServerTool, Description(
        "Create or update a source file in the current Code2Viz project. " +
        "If the file exists, its content is replaced. If it doesn't exist, a new file is created. " +
        "The file is saved to disk and the editor is refreshed automatically. " +
        "Use this to add helper classes, utility code, or data files that Main() in StartViz.cs can reference. " +
        "Do NOT use this to update StartViz.cs — use execute_vizcode instead, which sets the Main() body.")]
    public static async Task<string> UpdateFile(
        [Description("File name (e.g. 'Room.cs', 'Helpers.cs'). Must end in .cs.")] string fileName,
        [Description("The full source code content for the file.")] string content)
    {
        using var client = new IpcClient();
        var payload = System.Text.Json.JsonSerializer.Serialize(new { fileName, content });
        var request = new IpcRequest { Command = "update_file", Payload = payload };
        var response = await client.SendAsync(request);
        return response.Success ? response.Result ?? "Updated" : $"Error: {response.Error}";
    }
}
