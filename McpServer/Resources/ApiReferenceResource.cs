using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Code2Viz.McpServer.Resources;

[McpServerResourceType]
public class ApiReferenceResource
{
    [McpServerResource(UriTemplate = "viz2d://api-reference", Name = "Code2Viz Shape API Reference", MimeType = "text/markdown")]
    [Description("Complete API reference for all Code2Viz geometry shapes, properties, and methods")]
    public static string GetApiReference() => ApiReferenceContent;

    private const string ApiReferenceContent = """
        # Code2Viz Shape API Reference

        ## Coordinate System
        - Origin (0,0) at canvas center
        - Y-axis points UP (mathematical coordinates)
        - Shapes auto-register on construction — no need to call Draw()

        ## Available Imports
        ```csharp
        using System;
        using System.Linq;
        using System.Numerics;
        using System.Collections.Generic;
        using Code2Viz.Geometry;
        using Code2Viz.Console;
        using Code2Viz.Animation;
        ```

        ## Console Output
        ```csharp
        VizConsole.Log("message");   // Only method — auto-tracks file and line number
        VizConsole.Log(42);          // Accepts any object
        VizConsole.Log(myList);      // Itemizes collections (prints each item, default)
        VizConsole.Log(myList, false); // Prints collection's ToString() instead
        // Output: [ModuleName:LineNumber] message
        ```

        ## Shape Base Properties (all shapes)
        | Property | Type | Default | Description |
        |----------|------|---------|-------------|
        | Color | string | "Cyan" | Stroke color (any WPF named color or hex) |
        | FillColor | string | "Transparent" | Fill color |
        | LineWeight | double | 2 | Stroke width |
        | LineType | LineType | Continuous | Continuous, Dashed, Dotted, DashDot, DashDotDot, Center, Phantom, Hidden |
        | LineTypeScale | double | 1.0 | Scale for dash pattern |
        | Name | string | "" | Shape identifier |
        | IsVisible | bool | true | Show/hide shape |
        | Opacity | double | 1.0 | Transparency (0-1) |
        | Id | long | auto | Unique identifier (read-only) |

        ## Shape Methods (all shapes)
        - `Move(new VXYZ(dx, dy, 0))` — Translate by vector
        - `Rotate(pivot, angleDeg)` — Rotate around VPoint pivot
        - `Scale(center, factor)` — Scale around VPoint center
        - `Flip(mirrorLine)` — Mirror across VLine
        - `Clone()` — Deep copy
        - `GetBounds()` — Returns (VPoint min, VPoint max) bounding box
        - `Show()` / `Hide()` — Toggle visibility
        - `Remove()` — Remove from canvas
        - `Contains(point)` — Point containment test
        - `DistanceTo(point)` — Distance to VPoint
        - `DoesIntersect(other)` — Check intersection with another shape
        - `Intersect(other)` — Get intersection shape (or null)

        ## Shapes

        ### VPoint
        ```csharp
        new VPoint(x, y);
        // Properties: X, Y
        // Methods: DistanceTo(other), AsVXYZ()
        // Operators: +, -, *, / (with VPoint, VXYZ, or double)
        ```

        ### VLine
        ```csharp
        new VLine(x1, y1, x2, y2);
        new VLine(VPoint start, VPoint end);
        ```

        ### VCircle
        ```csharp
        new VCircle(centerX, centerY, radius);
        // Properties: Center, Radius, Area, Circumference
        ```

        ### VArc
        ```csharp
        new VArc(centerX, centerY, radius, startAngleDeg, endAngleDeg);
        // Counter-clockwise from start to end
        ```

        ### VRectangle
        ```csharp
        new VRectangle(x, y, width, height);              // bottom-left corner
        new VRectangle(x1, y1, x2, y2, fromCorners: true); // two corners
        ```

        ### VEllipse
        ```csharp
        new VEllipse(centerX, centerY, radiusX, radiusY);
        // Properties: Center, RadiusX, RadiusY, Area, Circumference
        ```

        ### VPolygon
        ```csharp
        new VPolygon(params VPoint[] vertices);
        new VPolygon(List<VPoint> vertices);
        // Auto-closes
        ```

        ### VPolyline
        ```csharp
        new VPolyline(params VPoint[] points);
        new VPolyline(List<VPoint> points);
        // Open path
        ```

        ### VBezier
        ```csharp
        new VBezier(x1,y1, cx1,cy1, cx2,cy2, x2,y2);
        ```

        ### VSpline
        ```csharp
        new VSpline(params VPoint[] points);
        // Catmull-Rom through all points
        ```

        ### VText
        ```csharp
        new VText(x, y, "text");
        new VText(x, y, "text", height);
        // Properties: Content, Height, Width (0=auto), Font (VFont enum), FontWeight (Normal/Bold)
        // VFont: Arial, TimesNewRoman, CourierNew, Verdana, Georgia, Tahoma, Consolas, etc.
        ```

        ### VArrow
        ```csharp
        new VArrow(x1, y1, x2, y2);
        new VArrow(startPoint, direction, length);
        // Properties: HeadLength (15), HeadAngle (30), DoubleEnded (false), MidPoint
        ```

        ### VGroup
        ```csharp
        new VGroup(params Shape[] shapes);
        new VGroup(IEnumerable<Shape> shapes);
        // Fluent API: Add(shape), AddRange(shapes), ForEach(action), Where(predicate)
        // ApplyColor(), ApplyFillColor(), ApplyLineWeight(), ApplyStyle(), SetOpacity(val)
        // GetCenter(), Flatten(), GetShapesOfType<T>(), Count, Shapes, [index]
        ```

        ### VGrid
        ```csharp
        new VGrid(origin, xCount, yCount, xSpacing, ySpacing, centered);
        ```

        ### VDimension
        ```csharp
        new VDimension(x1, y1, x2, y2);
        // Properties: Offset (20), ExtensionLength (10), ArrowSize (8), CustomText, DecimalPlaces (2), TextHeight (12)
        // Read-only: Distance, DisplayText
        ```

        ### VXLine (infinite construction line)
        ```csharp
        VXLine.Horizontal(y);
        VXLine.Vertical(x);
        ```

        ### VRay (semi-infinite ray)
        ```csharp
        VRay.AtAngle(x, y, angleDegrees);
        ```

        ## ICurve Interface (VLine, VCircle, VArc, VEllipse, VPolyline, VPolygon, VBezier, VSpline)
        Properties: StartPoint, EndPoint, Vertices, SelfIntersecting
        Methods:
        - `GetLength()` — Total arc length
        - `Divide(n)` — Split into n equal segments, returns List<VPoint>
        - `Measure(segmentLength)` — Points at fixed distance intervals
        - `PointAtSegmentLength(length)` — Point at distance along curve
        - `PointAtParameter(t)` — Point at parameter (0.0 to 1.0)
        - `Project(point)` — Closest point on curve
        - `Offset(distance)` — Parallel curve (returns ICurve)
        - `NormalAtPoint(point)` — Normal vector at point
        - `Intersect(otherCurve)` — Returns IntersectionResult (Points, Curves, HasIntersection, Count)
        - `SplitAtPoint(point)` — Returns (ICurve, ICurve) tuple

        ## VXYZ (3D Vector)
        ```csharp
        new VXYZ(x, y, 0);  // Z unused in 2D
        VXYZ.Zero; VXYZ.BasisX; VXYZ.BasisY;
        // Methods: GetLength(), Normalize(), DotProduct(other), CrossProduct(other), AngleTo(other), AsVPoint(), Rotate(angleDeg)
        // Operators: +, -, *, /, unary -, ==, !=
        ```

        ## Array Operations (extension methods, return List<Shape>)
        ```csharp
        shape.LinearArrayX(count, spacing);
        shape.LinearArrayY(count, spacing);
        shape.LinearArray(direction, count, spacing);
        shape.RectangularArray(rows, cols, rowSpacing, colSpacing);
        shape.CircularArray(center, count, totalAngle=360, rotateItems=true);
        shape.PathArray(curve, count, alignToPath=true);
        shape.Mirror(mirrorLine);
        shape.SpiralArray(center, count, startRadius, endRadius, revolutions=1, rotateItems=true);
        ```

        ## Boolean Operations (VPolygon only, extension methods)
        ```csharp
        polygon.Union(other);          // VPolygon?
        polygon.Intersect(other);      // List<VPolygon>
        polygon.Difference(other);     // List<VPolygon>
        polygon.Xor(other);            // List<VPolygon>
        polygon.OffsetPolygon(dist);   // List<VPolygon>
        polygon.Contains(point);       // bool
        polygon.GetArea();             // double
        ```

        ## VColor Utility
        ```csharp
        VColor.Red; VColor.Blue; VColor.Green; // 60+ named color properties
        VColor.FromRgb(r, g, b);               // from RGB (0-255)
        VColor.FromArgb(a, r, g, b);           // from ARGB
        VColor.GetRandomColor();               // random pastel
        VColor.GetRandomVibrantColor();        // random vibrant
        ```

        ## Style Defaults
        ```csharp
        ShapeDefaults.GlobalColor = "Red";
        ShapeDefaults.GlobalFillColor = "Yellow";
        ShapeDefaults.GlobalLineWeight = 3;
        ShapeDefaults.GlobalLineType = LineType.Dashed;
        ShapeDefaults.GlobalLineTypeScale = 2.0;
        ShapeDefaults.Reset();  // reset all to defaults
        ```

        ## Animation
        ```csharp
        var animator = new Animator();
        animator.Repeat = true;
        animator.Speed = 1.5;
        animator.Fps = 30;  // Target frame rate (1-120, default 60)
        // Sequential: animator.AddToAnimations(new DrawAnimation(shape, duration));
        // Parallel: animator.AddToAnimations(new List<Animation> { anim1, anim2 });
        // Types: DrawAnimation, MoveAnimation(target, VXYZ, dur), RotateAnimation(target, pivot, deg, dur),
        //        FadeInAnimation, FadeOutAnimation, FlipAnimation(target, mirrorAxis, dur),
        //        ValueAnimation<T>(shape, s => s.Prop, start, end, dur) — animate numeric property on Shape,
        //        ObjectPropertyAnimation<T>(obj, o => o.Prop, start, end, dur) — animate numeric property on any object
        // Easing: anim.EasingFunction = EasingFunctions.EaseInOutCubic;
        animator.Animate();
        ```

        ## Examples

        ### Basic shapes
        ```csharp
        var circle = new VCircle(0, 0, 100) { Color = "Red", FillColor = "Yellow" };
        var line = new VLine(-50, -50, 50, 50) { Color = "Green", LineWeight = 3 };
        var rect = new VRectangle(-30, -20, 60, 40) { FillColor = "Blue" };
        ```

        ### Circular array
        ```csharp
        var hex = new VPolygon(Enumerable.Range(0, 6).Select(i => {
            double a = Math.PI / 3 * i;
            return new VPoint(Math.Cos(a) * 20, Math.Sin(a) * 20);
        }).ToArray()) { Color = "Cyan" };
        var ring = hex.CircularArray(new VPoint(0, 0), 12);
        ```

        ### Star polygon
        ```csharp
        var pts = new List<VPoint>();
        for (int i = 0; i < 5; i++) {
            double angle = Math.PI / 2 + i * 4 * Math.PI / 5;
            pts.Add(new VPoint(Math.Cos(angle) * 100, Math.Sin(angle) * 100));
        }
        var star = new VPolygon(pts) { Color = "Gold", FillColor = "DarkGoldenrod" };
        ```
        """;
}
