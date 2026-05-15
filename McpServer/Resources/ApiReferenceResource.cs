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
        - `GetBounds()` — Returns BoundingBox with Min, Max, Width, Height, Center, Area
        - `Show()` / `Hide()` — Toggle visibility
        - `Remove()` — Remove from canvas
        - `BringAbove(otherShape)` — Move above another shape in draw order (renders on top)
        - `SendBehind(otherShape)` — Move behind another shape in draw order (renders underneath)
        - `Contains(point)` — Point containment test
        - `DistanceTo(point)` — Distance to VPoint
        - `DoesIntersect(other)` — Check intersection with another shape
        - `Intersect(other)` — Get intersection shape (or null)

        ## Shapes

        ### VPoint
        ```csharp
        new VPoint(x, y);
        // Properties: X, Y
        // Methods: DistanceTo(other), AsVXYZ(), PolarPoint(angleDeg, distance)
        // Operators: +, -, *, / (with VPoint, VXYZ, or double)
        // PolarPoint: create a point at angle (degrees) and distance from this point
        var p = new VPoint(0, 0).PolarPoint(45, 100); // 45 degrees, distance 100
        ```

        ### VLine
        ```csharp
        new VLine(x1, y1, x2, y2);
        new VLine(VPoint start, VPoint end);
        new VLine(startPoint, angleInDegrees, length);
        // Properties: Start, End, MidPoint, Direction (VXYZ)
        ```

        ### VCircle
        ```csharp
        new VCircle(centerX, centerY, radius);
        new VCircle(center, radius);                       // VPoint center
        new VCircle(p1, p2, p3);                           // circumcircle through 3 points
        VCircle.FromCenterDiameter(center, diameter);
        VCircle.FromCenterDiameter(cx, cy, diameter);
        VCircle.FromTwoPoints(p1, p2);                     // diameter endpoints
        // Properties: Center, Radius, Area, Circumference
        ```

        ### VArc
        ```csharp
        new VArc(centerX, centerY, radius, startAngleDeg, endAngleDeg);
        new VArc(center, radius, startAngleDeg, endAngleDeg);
        new VArc(start, mid, end);                         // arc through 3 points
        // Factory: FromStartCenterEnd, FromCenterStartEnd, FromStartCenterAngle,
        //   FromCenterStartAngle, FromStartCenterLength, FromCenterStartLength,
        //   FromStartEndRadius(start, end, radius, largeArc), FromStartEndAngle,
        //   Continue(previousCurve, arcLength)
        ```

        ### VRectangle
        ```csharp
        new VRectangle(x, y, width, height);              // bottom-left corner
        new VRectangle(x1, y1, x2, y2, fromCorners: true); // two corners
        ```

        ### VEllipse
        ```csharp
        new VEllipse(centerX, centerY, radiusX, radiusY);
        new VEllipse(center, radiusX, radiusY);
        new VEllipse(center, radiusX, radiusY, startAngle, endAngle);  // partial ellipse
        // Properties: Center, RadiusX, RadiusY, StartAngle (0), EndAngle (360), Area, Circumference
        ```

        ### VPolygon
        ```csharp
        new VPolygon(params VPoint[] vertices);
        new VPolygon(List<VPoint> vertices);
        new VPolygon(List<ICurve> curves);  // from ordered curves forming closed loop
        // Auto-closes
        // Properties: Points, Area, SignedArea
        // Methods: AddPoint(point), Slice(p1, p2) / Slice(xline) / Slice(ray) — returns List<VPolygon>
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
        // Properties: ControlPoints, SegmentsPerSpan (16), Tension (0.5, range 0=sharp to 1=loose)
        ```

        ### VText
        ```csharp
        new VText(x, y, "text");
        new VText(x, y, "text", height);
        // Properties: Content, Height, Width (0=auto), Font (VFont enum), FontWeight (Normal/Bold),
        //   Anchor (VTextAnchor enum, default BottomLeft),
        //   Angle (degrees, CCW around Location, default 0; rotates the whole text block Excel-style)
        // VFont: Arial, TimesNewRoman, CourierNew, Verdana, Georgia, Tahoma, Consolas, etc.
        // VTextAnchor: BottomLeft (default), BottomCenter, BottomRight, MiddleLeft, MiddleCenter,
        //   MiddleRight, TopLeft, TopCenter, TopRight
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

        ### VCell
        ```csharp
        // Created by VSpatialGrid. Extends VPolygon (square boundary).
        // Properties: UniqueId (int), Neighbours (List<VCell>), Center (VXYZ), CellSize (double)
        // Column (int), Row (int), Blocked (bool, default false)
        ```

        ### VSpatialGrid
        ```csharp
        new VSpatialGrid(location, xCount, yCount, cellSize);
        // location = center of bottom-left cell (VXYZ)
        // Properties: Cells (List<VCell>), Location (VXYZ), XCount, YCount, CellSize, Count
        // Indexers: [index], [col, row]
        // Methods: FindPath(start, end) -> List<VCell> (A* pathfinding, respects Blocked)
        //          GetClosestCell(point) -> VCell (O(log n) KD-tree lookup)
        //          GetCellAt(point) -> VCell? (cell containing point)
        //          GetRow(row), GetColumn(col), GetCenter(), ApplyStyle()
        // 4-connectivity neighbours (left, right, below, above)
        ```

        ### VDimension
        ```csharp
        new VDimension(x1, y1, x2, y2);
        // Properties: Offset (20), ExtensionLength (10), ArrowSize (8), CustomText, DecimalPlaces (2), TextHeight (12)
        // AutoCAD-style: ExtendBeyondDimLines (1.25), OffsetFromOrigin (0.625), SuppressExtLine1/2 (false), Prefix (""), Suffix ("")
        // TextBackgroundOpaque (false) - opaque background behind text; dimension line always splits around text
        // SuppressDimensionLine (false) - hide dimension line and arrowheads
        // Per-element colors (null = use base Color): ExtensionLineColor, DimensionLineColor, TextColor
        // Read-only: Distance, DisplayText (includes Prefix/Suffix)
        ```

        ### VRadialDimension
        ```csharp
        new VRadialDimension(circle);          // from VCircle
        new VRadialDimension(arc);             // from VArc
        new VRadialDimension(center, radius);  // from VPoint + radius
        // Properties: LeaderAngle (45), ShowDiameter (false), ArrowSize (8), TextHeight (12), DecimalPlaces (2)
        // Prefix (""), Suffix (""), CustomText (null), TextBackgroundOpaque (false)
        // Per-element colors (null = use base Color): DimensionLineColor, TextColor
        // Read-only: Value (radius or diameter), DisplayText (with R/dia symbol)
        ```

        ### VXLine (infinite construction line)
        ```csharp
        new VXLine(basePoint, direction);       // VPoint + VXYZ
        new VXLine(point1, point2);            // through two VPoints
        new VXLine(x1, y1, x2, y2);
        VXLine.Horizontal(y);
        VXLine.Vertical(x);
        ```

        ### VRay (semi-infinite ray)
        ```csharp
        new VRay(origin, direction);            // VPoint + VXYZ
        new VRay(origin, throughPoint);         // two VPoints
        new VRay(ox, oy, tx, ty);
        VRay.AtAngle(origin, angleDeg);        // VPoint origin + angle
        VRay.HorizontalRight(origin); VRay.HorizontalLeft(origin);
        VRay.VerticalUp(origin); VRay.VerticalDown(origin);
        // Properties: Origin, Direction, RenderExtent (10000)
        // Methods: GetPointAtDistance(d), ContainsPoint(pt), ToFiniteLine(), ToXLine()
        ```

        ### Region (curve-bounded area)
        ```csharp
        new Region(new List<ICurve> { line1, arc1, line2, arc2 }); // curves auto-ordered into loop
        new Region(outerCurves, new List<List<ICurve>> { holeCurves }); // with holes
        Region.FromPolygon(polygon);
        Region.FromPolygonWithHoles(pwh);
        // Properties: OuterLoop, Holes, Area, SignedArea, Perimeter
        // Methods: AddHole(curves), Contains(point), ToPolygon(), ToPolygonHighRes(segments),
        //          ToPolygonWithHoles(segments), Clone(), Move(), Rotate(), Flip(), Scale(), GetBounds()
        // Boolean ops (static): RegionBooleanOps.Union(a, b), .Intersect(a, b), .Difference(a, b), .Xor(a, b)
        // Multi-union: RegionBooleanOps.Union(r1, r2, r3, ...)
        ```

        ### VHatch (pattern fill)
        ```csharp
        new VHatch(polygon, BuiltInHatch.ANSI31, scale: 10);           // built-in enum
        new VHatch(polygon, "BRICK", scale: 5, angle: 45);             // built-in by name
        new VHatch(boundaryPoints, pattern, scale, angle);              // from HatchType
        VHatch.FromDefinition(polygon, patString, scale, angle);        // from .pat string
        // Properties: Boundary, Pattern, PatternScale, PatternAngle, Color, LineWeight
        // Methods: GenerateLines() — returns clipped (Start, End) pairs
        // Built-in patterns (73): ANSI31-38, BRICK, STEEL, HEX, HONEY, NET, DOTS, CROSS, etc.
        // Custom: HatchType.Parse("*NAME, Desc\n45, 0,0, 0,10"), or new HatchType(name, desc, lines)
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

        ## BoundingBox
        Returned by `shape.GetBounds()` method on all shapes.
        ```csharp
        BoundingBox bounds = shape.GetBounds();
        // Properties: Min, Max (VPoint corners), Width, Height, Center, Area
        // Methods: Contains(point), Intersects(other), Union(other), Expand(distance)
        var (min, max) = bounds;  // tuple deconstruction
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

        ## Boolean Operations (VPolygon only)
        ```csharp
        // Extension methods
        polygon.Union(other);              // VPolygon?
        polygon.Intersect(other);          // List<VPolygon>
        polygon.Difference(other);         // List<VPolygon>
        polygon.Xor(other);               // List<VPolygon>
        polygon.OffsetPolygon(dist);       // List<VPolygon>
        polygon.OffsetPolygonSafe(dist);   // List<VPolygon> — safe inward offset
        polygon.MaxSafeInwardOffset();     // double — max safe inward distance
        polygon.HasSelfIntersections();    // bool
        polygon.MakeSimple();              // List<VPolygon> — resolve self-intersections
        polygon.Contains(point);           // bool
        polygon.GetArea();                 // double

        // Static methods
        BooleanOps.Union(params VPolygon[]);
        BooleanOps.OffsetPolygon(polygon, dist, JoinType.Round, EndType.Polygon);
        BooleanOps.Simplify(polygon, tolerance);
        BooleanOps.DifferenceWithHoles(a, b);  // List<PolygonWithHoles>
        BooleanOps.IntersectWithHoles(a, b);
        BooleanOps.UnionWithHoles(a, b);

        // PolygonWithHoles — outer boundary with holes
        var pwh = new PolygonWithHoles(outer); pwh.AddHole(hole);
        // Props: Outer, Holes, Area. Methods: Contains(pt), Clone()

        // JoinType: Miter (default), Round, Square
        // EndType: Polygon (default), OpenRound, OpenSquare, OpenButt
        ```

        ## Angle Conversion Extensions
        Extension methods on `double` — drop the `* Math.PI / 180.0` boilerplate.
        ```csharp
        double rad = 45.0.ToRadians();      // degrees → radians
        double deg = Math.PI.ToDegrees();   // radians → degrees
        double y = Math.Sin(30.0.ToRadians());      // 0.5
        double a = Math.Atan2(dy, dx).ToDegrees();   // angle in degrees
        ```

        ## VColor Utility
        ```csharp
        VColor.Red; VColor.Blue; VColor.Green; // 60+ named color properties
        VColor.FromRgb(r, g, b);               // from RGB (0-255)
        VColor.FromArgb(a, r, g, b);           // from ARGB (0-255)
        VColor.WithOpacity(r, g, b, opacity);  // semi-transparent (opacity 0-1)
        VColor.GetRandomColor();               // random pastel (default)
        VColor.GetRandomVibrantColor();        // random vibrant
        VColor.GetRandomPastelColor();         // random pastel
        VColor.GetVibrantColors();             // string[] of all vibrant colors
        VColor.GetPastelColors();              // string[] of all pastel colors
        ```

        ## Style Defaults
        ```csharp
        ShapeDefaults.GlobalColor = "Red";
        ShapeDefaults.GlobalFillColor = "Yellow";
        ShapeDefaults.GlobalLineWeight = 3;
        ShapeDefaults.GlobalLineType = LineType.Dashed;
        ShapeDefaults.GlobalLineTypeScale = 2.0;

        // Dimension style defaults (apply to new VDimension shapes)
        ShapeDefaults.DimOffset = 15.0;
        ShapeDefaults.DimArrowSize = 6.0;
        ShapeDefaults.DimTextHeight = 10.0;
        ShapeDefaults.DimDecimalPlaces = 1;
        ShapeDefaults.DimExtendBeyondDimLines = 2.0;
        ShapeDefaults.DimOffsetFromOrigin = 1.0;
        ShapeDefaults.DimPrefix = "L=";
        ShapeDefaults.DimSuffix = "mm";
        ShapeDefaults.DimTextBgOpaque = true;
        ShapeDefaults.DimExtensionLineColor = "Green";   // extension line color
        ShapeDefaults.DimDimensionLineColor = "Red";     // dimension line & arrowhead color
        ShapeDefaults.DimTextColor = "Blue";             // text color
        ShapeDefaults.DimSuppressDimensionLine = true;   // hide dimension line & arrowheads

        ShapeDefaults.Reset();  // reset all to defaults
        ```

        ## Animation
        ```csharp
        var animator = new Animator();
        animator.Repeat = true;  // Each animation loops independently at its own duration
        animator.Speed = 1.5;
        animator.Fps = 30;  // Target frame rate (1-120, default 60)
        // Sequential: animator.AddToAnimations(new DrawAnimation(shape, duration));
        // Parallel: animator.AddToAnimations(new List<Animation> { anim1, anim2 });
        animator.Pause(1.5);  // insert time gap before next animation
        // Types: DrawAnimation, MoveAnimation(target, VXYZ, dur), PathAnimation(target, ICurve path, dur),
        //        RotateAnimation(target, pivot, deg, dur),
        //        FadeInAnimation, FadeOutAnimation, FlipAnimation(target, mirrorAxis, dur),
        //        ValueAnimation<T>(shape, s => s.Prop, start, end, dur) — animate numeric property on Shape,
        //        ValueAnimation<T>(shape, s => s.Prop, new List<double> { v1, v2, ... }, dur) — animate through multiple values,
        //        ObjectPropertyAnimation<T>(obj, o => o.Prop, start, end, dur) — animate numeric property on any object
        // Easing: anim.EasingFunction = EasingFunctions.EaseInOutCubic;
        animator.Animate();  // start playback
        animator.Stop();     // stop playback
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
