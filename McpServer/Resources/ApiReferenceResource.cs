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
        using C2VGeometry;
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
        | Name | string | "" | Shape identifier. Shapes with an empty Name are auto-hidden after a script run (see Shape Visibility Rules below) |
        | IsVisible | bool | true | Show/hide shape |
        | Opacity | double | 1.0 | Transparency (0-1) |
        | Id | long | auto | Unique identifier (read-only) |

        ## Shape Methods (all shapes)
        - `Move(new VXYZ(dx, dy, 0))` — Translate by vector
        - `Rotate(pivot, angleDeg)` — Rotate around VXYZ pivot
        - `Scale(center, factor)` — Scale around VXYZ center
        - `Flip(mirrorLine)` — Mirror across VLine
        - `Clone()` — Deep copy
        - `GetBounds()` — Returns BoundingBox with Min, Max, Width, Height, Center, Area
        - `Show()` / `Hide()` — Toggle visibility
        - `Remove()` — Remove from canvas
        - `BringAbove(otherShape)` — Move above another shape in draw order (renders on top)
        - `SendBehind(otherShape)` — Move behind another shape in draw order (renders underneath)
        - `Contains(point)` — Point containment test
        - `DistanceTo(point)` — Distance to VXYZ
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
        new VLine(VXYZ start, VXYZ end);
        new VLine(startPoint, angleInDegrees, length);
        // Properties: Start, End, MidPoint, Direction (VXYZ)
        ```

        ### VCircle
        ```csharp
        new VCircle(centerX, centerY, radius);
        new VCircle(center, radius);                       // VXYZ center
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
        new VPolygon(params VXYZ[] vertices);
        new VPolygon(List<VXYZ> vertices);
        new VPolygon(List<ICurve> curves);  // from ordered curves forming closed loop
        // Auto-closes
        // Properties: Points, Area, SignedArea
        // Methods: AddPoint(point), Slice(p1, p2) / Slice(xline) / Slice(ray) — returns List<VPolygon>
        ```

        ### VPolyline
        ```csharp
        new VPolyline(params VXYZ[] points);
        new VPolyline(List<VXYZ> points);
        // Open path
        ```

        ### VBezier
        ```csharp
        new VBezier(x1,y1, cx1,cy1, cx2,cy2, x2,y2);
        ```

        ### VSpline
        ```csharp
        new VSpline(params VXYZ[] points);
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
        // DoesIntersect(other): text-aware via OBB-vs-AABB SAT on the rotated, anchor-aware
        //   bounding quad. Shape.DoesIntersect falls back so the check is symmetric:
        //   other.DoesIntersect(text) returns the same result.
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
        new VRadialDimension(center, radius);  // from VXYZ + radius
        // Properties: LeaderAngle (45), ShowDiameter (false), ArrowSize (8), TextHeight (12), DecimalPlaces (2)
        // Prefix (""), Suffix (""), CustomText (null), TextBackgroundOpaque (false)
        // Per-element colors (null = use base Color): DimensionLineColor, TextColor
        // Read-only: Value (radius or diameter), DisplayText (with R/dia symbol)
        ```

        ### VXLine (infinite construction line)
        ```csharp
        new VXLine(basePoint, direction);       // VXYZ + VXYZ
        new VXLine(point1, point2);            // through two VXYZ points
        new VXLine(x1, y1, x2, y2);
        VXLine.Horizontal(y);
        VXLine.Vertical(x);
        ```

        ### VRay (semi-infinite ray)
        ```csharp
        new VRay(origin, direction);            // VXYZ + VXYZ
        new VRay(origin, throughPoint);         // two VXYZ points
        new VRay(ox, oy, tx, ty);
        VRay.AtAngle(origin, angleDeg);        // VXYZ origin + angle
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

        ### Chart (Chart.js-style charts)
        Each method returns a `VGroup` containing axes, gridlines, ticks, labels and data.
        Child shapes don't register individually — only the outer VGroup does, so the chart
        can be Moved/Rotated/Scaled as one unit. Axis ranges auto-fit using "nice" round-
        number tick spacing. Methods: `Bar`, `Line`, `Scatter`, `Pie`, `Area`.

        **Bar — categorical values with a numeric Y axis**
        ```csharp
        var labels = new[] { "Q1", "Q2", "Q3", "Q4" };
        var values = new[] { 120.0, 150, 95, 180 };

        var revenue = Chart.Bar(labels, values, new ChartOptions
        {
            Origin = new VXYZ(-250, -150),
            Width = 500, Height = 300,
            Title = "Quarterly Revenue (M$)",
            YAxisTitle = "Revenue",
            YMin = 0,
            TickDecimalPlaces = 0
        });
        ```

        **Line — computed time series, auto-fit ranges**
        ```csharp
        var xs = Enumerable.Range(0, 60).Select(i => i * 0.1).ToArray();
        var ys = xs.Select(x => Math.Exp(-0.3 * x) * Math.Sin(2 * x)).ToArray();

        var trace = Chart.Line(xs, ys, new ChartOptions
        {
            Origin = new VXYZ(-300, -150),
            Width = 600, Height = 300,
            Title = "Damped Oscillator",
            XAxisTitle = "Time (s)",
            YAxisTitle = "Amplitude"
        });
        ```

        **Scatter — correlated random sample**
        ```csharp
        var rng = new Random(42);
        var sample = Enumerable.Range(0, 80).Select(_ =>
        {
            double age = rng.NextDouble() * 40 + 20;
            double height = age * 0.4 + 150 + rng.NextDouble() * 20;
            return new VXYZ(age, height);
        }).ToArray();

        var scatter = Chart.Scatter(sample, new ChartOptions
        {
            Origin = new VXYZ(-250, -150),
            Width = 500, Height = 300,
            Title = "Height vs Age",
            XAxisTitle = "Age",
            YAxisTitle = "Height (cm)"
        });
        ```

        **Pie — named slices, custom palette**
        ```csharp
        var share    = new[] { 64.7, 19.5, 9.3, 3.5, 3.0 };
        var browsers = new[] { "Chrome", "Safari", "Edge", "Firefox", "Other" };

        var pie = Chart.Pie(share, browsers, new ChartOptions
        {
            Origin = new VXYZ(-150, -150),
            Width = 300, Height = 300,
            Title = "Browser Market Share",
            Palette = new[] { "DodgerBlue", "Tomato", "MediumSeaGreen", "Gold", "Gray" }
        });
        ```

        **Area — filled trend with axis titles**
        ```csharp
        var months = Enumerable.Range(0, 12).Select(i => (double)(i + 1)).ToArray();
        var mau    = new[] { 4.2, 5.1, 6.0, 7.3, 8.1, 8.8, 9.4, 9.7, 10.2, 10.5, 11.0, 11.6 };

        var growth = Chart.Area(months, mau, new ChartOptions
        {
            Origin = new VXYZ(-300, -150),
            Width = 600, Height = 300,
            Title = "Monthly Active Users",
            XAxisTitle = "Month",
            YAxisTitle = "MAU (millions)",
            YMin = 0
        });

        // A chart is a VGroup — move/rotate/scale/style as a unit
        growth.Move(new VXYZ(0, 50));
        ```

        **ChartOptions — every property optional**
        ```csharp
        var opts = new ChartOptions
        {
            Origin = new VXYZ(0, 0),       // bottom-left of plot area
            Width = 500, Height = 300,
            Title = "Monthly active users",
            XAxisTitle = "Month", YAxisTitle = "MAU",
            XMin = null, XMax = null,      // null = auto-fit (also YMin/YMax)
            XTickCount = 12, YTickCount = 5,
            ShowGrid = true,
            XLabelRotation = 45,           // degrees, for long category names
            LabelFontSize = 9, TitleFontSize = 14,
            AxisColor = "White", GridColor = "DimGray", TextColor = "White",
            Palette = new[] { "DodgerBlue", "Tomato", "MediumSeaGreen" },
            TickDecimalPlaces = 0          // null = auto format
        };
        ```

        ## Shape Visibility Rules (important!)
        After your script's `Main()` returns, Code2Viz hides any Shape where `Name` is empty and `IsExplicitlyDrawn` is false. The intent is to suppress intermediate construction shapes. The auto-naming pass only fills `Name` from these two C# patterns:
        - Local declarations: `var x = new VShape(...)`
        - Field declarations: `private VShape myShape = new VShape(...);`

        These patterns slip past the rewriter and need an explicit `Name`:
        ```csharp
        list.Add(new VLine(0, 0, 100, 100) { Color = "Cyan", Name = "edge" });  // List.Add
        hulls[i] = new VPolygon(pts) { Color = "Lime", Name = $"hull{i}" };      // array slot
        VLine Make(VXYZ a, VXYZ b) =>                                            // helper return
            new VLine(a, b) { Color = "Gold", Name = "edge" };
        ```
        If shapes get hidden, the console logs `Warning: N unnamed shape(s) hidden (...)`. Calling `.Draw()` on a shape also keeps it visible (sets `IsExplicitlyDrawn = true`).

        ## ICurve Interface (VLine, VCircle, VArc, VEllipse, VPolyline, VPolygon, VBezier, VSpline)
        Properties: StartPoint, EndPoint, Vertices, SelfIntersecting
        Methods:
        - `GetLength()` — Total arc length
        - `Divide(n)` — Split into n equal segments, returns List<VXYZ>
        - `Measure(segmentLength)` — Points at fixed distance intervals
        - `PointAtSegmentLength(length)` — Point at distance along curve
        - `PointAtParameter(t)` — Point at parameter (0.0 to 1.0)
        - `ParameterAtPoint(point)` — Parameter (0–1) of closest point on curve
        - `Project(point)` — Closest point on curve
        - `Offset(distance)` — Parallel curve (returns ICurve)
        - `NormalAtPoint(point)` — Normal vector at point
        - `Intersect(otherCurve)` — Returns IntersectionResult (Points, Curves, HasIntersection, Count)
        - `SplitAtPoint(point)` — Returns (ICurve, ICurve) tuple
        - `SetBounds(startParam, endParam)` — Trim curve in place; the parameter sub-range [startParam, endParam] becomes the new [0,1]. Parameters are clamped to [0,1] and swapped if reversed. Supported for VLine/VArc/VEllipse/VPolyline/VBezier/VSpline. Throws `NotSupportedException` on VCircle/VPolygon/VRay/VXLine (their trimmed form would be a different shape type — use `SplitAtPoint` for those).

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
        // Properties: Min, Max (VXYZ corners), Width, Height, Center, Area
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

        ## Ray Casting (RayCaster)
        ```csharp
        // Build a BVH once over every visible shape on the canvas (snapshot
        // at construction); queries then run in O(log N).
        var caster = new RayCaster();                              // leafSize = 8
        var caster2 = new RayCaster(leafSize: 16);

        // Closest hit (XY plane; Z is ignored)
        RayHit? hit = caster.FindIntersection(new VXYZ(0,0,0), new VXYZ(1,0,0));
        if (hit is { } h) { Shape s = h.Shape; VXYZ pt = h.Point; double d = h.Distance; }

        // With distance cap (prunes BVH sub-trees)
        var near = caster.FindIntersection(origin, dir, maxDistance: 50);

        // Exclude specific shapes (e.g. the source shape) from the candidate set
        var past = caster.FindIntersection(origin, dir,
            exclusionList: new List<Shape> { sourceShape });
        var pastCapped = caster.FindIntersection(origin, dir, maxDistance: 100,
            exclusionList: new List<Shape> { sourceShape });

        // Any-hit early-out (faster than closest-hit for shadow rays)
        bool blocked = caster.HasIntersection(origin, dir);
        bool nearby  = caster.HasIntersection(origin, dir, maxDistance: 100);

        // Parallel batch (BVH is read-only — thread-safe by construction)
        var qs = new[] {
            new RayQuery(new VXYZ(0,0,0), new VXYZ(1,0,0)),
            new RayQuery(new VXYZ(0,0,0), new VXYZ(0,1,0))
        };
        RayHit?[] results = caster.FindIntersections(qs);            // parallel
        RayHit?[] seq     = caster.FindIntersections(qs, parallel: false);

        // Refit after shape movement (in-place, O(N), preserves topology)
        circle.Center = new VXYZ(50, 0);
        caster.Refit();
        ```
        - Indexes every Shape in CanvasRenderer.Instance.GetShapes() with IsVisible == true.
        - VPoint markers are always excluded (zero-area; not a useful ray target).
        - Canvas state is snapshotted at construction; later adds/removes are not reflected — build a new RayCaster to pick them up.
        - Returns `RayHit(Shape, VXYZ Point, double Distance)`; `RayQuery(VXYZ Origin, VXYZ Direction)` is the batch input record.
        - Direction need not be normalised; Z component is ignored.
        - Inline ray-vs-shape math covers VLine, VCircle, VArc, VEllipse, VPolygon (incl. VRectangle), VPolyline; other shape types fall back to AABB hit.
        - Shapes with infinite bounds (VRay, VXLine) are excluded from the index.

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
            return new VXYZ(Math.Cos(a) * 20, Math.Sin(a) * 20);
        }).ToArray()) { Color = "Cyan" };
        var ring = hex.CircularArray(new VXYZ(0, 0), 12);
        ```

        ### Star polygon
        ```csharp
        var pts = new List<VXYZ>();
        for (int i = 0; i < 5; i++) {
            double angle = Math.PI / 2 + i * 4 * Math.PI / 5;
            pts.Add(new VXYZ(Math.Cos(angle) * 100, Math.Sin(angle) * 100));
        }
        var star = new VPolygon(pts) { Color = "Gold", FillColor = "DarkGoldenrod" };
        ```

        ## Sibling app: Animator (p5.js-style sketches)

        Code2Viz ships with a separate WPF app `Animator.exe` (folder `Animator/`) for frame-driven animation sketches. Animator depends only on `C2VGeometry.dll` (no Code2Viz dependency). The MCP tools above do not target Animator — Animator is launched manually or via Code2Viz's **Switch to Animator** button.

        A sketch subclasses `Animator.Sketching.Sketch` and overrides `Setup()` (runs once) and `Draw()` (runs every frame). Geometry uses **C2VGeometry** types; shapes auto-register each frame and the canvas re-renders. Persistent state lives in fields on the sketch class; locals reset each call.

        ```csharp
        using System;
        using C2VGeometry;
        using Animator.Sketching;

        public class MySketch : Sketch
        {
            public override void Setup() { Size(800, 600); Background("Black"); }

            public override void Draw()
            {
                var r = 200.0;
                var x = r * Math.Sin(ElapsedSeconds);
                var y = r * Math.Cos(ElapsedSeconds);
                new VCircle(new VXYZ(x, y), 12) { FillColor = "Cyan" };
            }
        }
        ```

        Sketch base members: `Setup()`, `Draw()`, `Size(w,h)`, `Background(color)`, `Loop()`/`NoLoop()`, `FrameCount`, `ElapsedSeconds`, `DeltaSeconds`, `Width`/`Height`, `MouseX`/`MouseY`/`MousePressed`, `KeyPressed`/`LastKey`. Console helpers: `VizConsole.Log/Warn/Error(message)`.
        """;
}
