using System;
using System.Collections.Generic;
using System.Linq;
using Code2Viz.Geometry;
using Code2Viz.Animation;
using Code2Viz.Console;

namespace CSharpSample
{
    /// <summary>
    /// Code2Viz Sample Project - Comprehensive Examples
    ///
    /// This file demonstrates all geometry types, their properties, methods,
    /// and animation capabilities available in Code2Viz.
    ///
    /// Uncomment individual sections to see them in action.
    /// </summary>
    public class Viz
    {
        public static void Main()
        {
            // ═══════════════════════════════════════════════════════════════
            // CHOOSE WHICH EXAMPLE TO RUN (uncomment one at a time)
            // ═══════════════════════════════════════════════════════════════

             PointExamples();
            // LineExamples();
            // CircleExamples();
            // ArcExamples();
            // EllipseExamples();
            // RectangleExamples();
            // PolygonExamples();
            // PolylineExamples();
            // BezierExamples();
            // SplineExamples();
            // TextExamples();
            // ArrowExamples();
            // DimensionExamples();
            // GroupExamples();
            // GridExamples();
            // BooleanOperationsExamples();
            // AnimationExamples();
            // EasingFunctionsExamples();
        }

        // ═══════════════════════════════════════════════════════════════════
        // VPOINT - Point in 2D Space
        // ═══════════════════════════════════════════════════════════════════
        static void PointExamples()
        {
            VizConsole.Log("=== VPoint Examples ===");

            // --- Creating Points ---

            // Basic constructor: VPoint(x, y)
            var p1 = new VPoint(0, 0);           // Origin
            var p2 = new VPoint(50, 30);         // Point at (50, 30)
            var p3 = new VPoint(-40, 60);        // Negative coordinates work too

            // --- Point Properties ---
            VizConsole.Log($"P2 coordinates: X={p2.X}, Y={p2.Y}");

            // --- Point Methods ---

            // DistanceTo: Calculate distance between points
            double dist = p1.DistanceTo(p2);
            VizConsole.Log($"Distance from p1 to p2: {dist:F2}");

            // Clone: Create an independent copy
            var p4 = (VPoint)p2.Clone();

            // --- Styling Points ---
            p1.Color = "Red";
            p2.Color = "Blue";
            p3.Color = "Green";

            // --- Point with Label ---
            var labeledPoint = new VPoint(0, -50);
            labeledPoint.Color = "Purple";

            // Use VText to add a label near a point
            var label = new VText(10, -55, "Origin");
            label.Color = "Purple";
            label.Height = 12;
        }

        // ═══════════════════════════════════════════════════════════════════
        // VLINE - Line Segment
        // ═══════════════════════════════════════════════════════════════════
        static void LineExamples()
        {
            VizConsole.Log("=== VLine Examples ===");

            // --- Creating Lines ---

            // From two points: VLine(startPoint, endPoint)
            var line1 = new VLine(new VPoint(-50, 0), new VPoint(50, 0));

            // From coordinates: VLine(x1, y1, x2, y2)
            var line2 = new VLine(0, -50, 0, 50);

            // --- Line Properties ---
            VizConsole.Log($"Line1 Start: ({line1.Start.X}, {line1.Start.Y})");
            VizConsole.Log($"Line1 End: ({line1.End.X}, {line1.End.Y})");
            VizConsole.Log($"Line1 Length: {line1.GetLength():F2}");
            VizConsole.Log($"Line1 Midpoint: ({line1.MidPoint.X}, {line1.MidPoint.Y})");

            // Direction vector (normalized VXYZ)
            var direction = line1.Direction;
            VizConsole.Log($"Line1 Direction: ({direction.X:F2}, {direction.Y:F2})");

            // --- Line Methods ---

            // Evaluate: Get point at parameter t (0=start, 1=end)
            var midPoint = line1.Evaluate(0.5);
            var quarterPoint = line1.Evaluate(0.25);

            // ParameterAtPoint: Get parameter for a point on line
            double t = line1.ParameterAtPoint(midPoint);
            VizConsole.Log($"Parameter at midpoint: {t}");

            // --- Styling Lines ---
            line1.Color = "Red";
            line1.LineWeight = 3;

            line2.Color = "Blue";
            line2.LineWeight = 2;
            line2.LineType = LineType.Dashed;

            // Diagonal line
            var line3 = new VLine(-40, -40, 40, 40);
            line3.Color = "Green";
            line3.LineWeight = 2;
        }

        // ═══════════════════════════════════════════════════════════════════
        // VCIRCLE - Circle
        // ═══════════════════════════════════════════════════════════════════
        static void CircleExamples()
        {
            VizConsole.Log("=== VCircle Examples ===");

            // --- Creating Circles ---

            // Center and radius: VCircle(centerX, centerY, radius)
            var circle1 = new VCircle(0, 0, 50);

            // From center point: VCircle(centerPoint, radius)
            var circle2 = new VCircle(new VPoint(80, 0), 30);

            // --- Circle Properties ---
            VizConsole.Log($"Circle1 Center: ({circle1.Center.X}, {circle1.Center.Y})");
            VizConsole.Log($"Circle1 Radius: {circle1.Radius}");
            VizConsole.Log($"Circle1 Diameter: {circle1.Radius * 2}");
            VizConsole.Log($"Circle1 Circumference: {circle1.Circumference:F2}");
            VizConsole.Log($"Circle1 Area: {circle1.Area:F2}");

            // --- Circle Methods ---

            // PointAtParameter: Get point at angle (0=right, 0.25=top, 0.5=left, 0.75=bottom)
            var topPoint = circle1.PointAtParameter(0.25);      // Top of circle
            var rightPoint = circle1.PointAtParameter(0);       // Right side
            new VPoint(topPoint.X, topPoint.Y).Color = "Red";
            new VPoint(rightPoint.X, rightPoint.Y).Color = "Blue";

            // ParameterAtPoint: Get parameter for a point on circle
            double param = circle1.ParameterAtPoint(topPoint);
            VizConsole.Log($"Parameter at top: {param:F2}");

            // --- Styling Circles ---
            circle1.Color = "DarkBlue";
            circle1.LineWeight = 2;

            // Filled circle
            circle2.Color = "Green";
            circle2.FillColor = "LightGreen";
            circle2.Opacity = 0.5;

            // Circle with dashed outline
            var circle3 = new VCircle(-80, 0, 25);
            circle3.Color = "Orange";
            circle3.LineType = LineType.Dashed;
        }

        // ═══════════════════════════════════════════════════════════════════
        // VARC - Circular Arc
        // ═══════════════════════════════════════════════════════════════════
        static void ArcExamples()
        {
            VizConsole.Log("=== VArc Examples ===");

            // --- Creating Arcs ---

            // Center, radius, start angle, end angle (angles in degrees)
            // VArc(centerX, centerY, radius, startAngleDegrees, endAngleDegrees)
            var arc1 = new VArc(0, 0, 50, 0, 90);        // Quarter circle (0 to 90)

            // Half circle
            var arc2 = new VArc(0, 0, 40, 45, 225);      // 180 arc

            // Three-quarter circle
            var arc3 = new VArc(0, 0, 30, 0, 270);

            // --- Arc Properties ---
            VizConsole.Log($"Arc1 Center: ({arc1.Center.X}, {arc1.Center.Y})");
            VizConsole.Log($"Arc1 Radius: {arc1.Radius}");
            VizConsole.Log($"Arc1 Start Angle: {arc1.StartAngle} deg");
            VizConsole.Log($"Arc1 End Angle: {arc1.EndAngle} deg");
            VizConsole.Log($"Arc1 Length: {arc1.GetLength():F2}");

            // Start and end points
            VizConsole.Log($"Arc1 Start Point: ({arc1.StartPoint.X:F1}, {arc1.StartPoint.Y:F1})");
            VizConsole.Log($"Arc1 End Point: ({arc1.EndPoint.X:F1}, {arc1.EndPoint.Y:F1})");

            // --- Arc Methods ---

            // Evaluate: Get point at parameter t (0=start, 1=end)
            var midArc = arc1.Evaluate(0.5);
            new VPoint(midArc.X, midArc.Y).Color = "Red";

            // --- Styling Arcs ---
            arc1.Color = "Blue";
            arc1.LineWeight = 3;

            arc2.Color = "Green";
            arc2.LineWeight = 2;
            arc2.LineType = LineType.Dotted;

            arc3.Color = "Orange";
            arc3.LineWeight = 2;
        }

        // ═══════════════════════════════════════════════════════════════════
        // VELLIPSE - Ellipse
        // ═══════════════════════════════════════════════════════════════════
        static void EllipseExamples()
        {
            VizConsole.Log("=== VEllipse Examples ===");

            // --- Creating Ellipses ---

            // Center and radii: VEllipse(centerX, centerY, radiusX, radiusY)
            var ellipse1 = new VEllipse(0, 0, 60, 30);    // Wider than tall

            // Taller than wide
            var ellipse2 = new VEllipse(0, 0, 25, 50);

            // --- Ellipse Properties ---
            VizConsole.Log($"Ellipse1 Center: ({ellipse1.Center.X}, {ellipse1.Center.Y})");
            VizConsole.Log($"Ellipse1 RadiusX: {ellipse1.RadiusX}");
            VizConsole.Log($"Ellipse1 RadiusY: {ellipse1.RadiusY}");
            VizConsole.Log($"Ellipse1 Area: {ellipse1.Area:F2}");
            VizConsole.Log($"Ellipse1 Circumference: {ellipse1.Circumference:F2}");

            // --- Ellipse Methods ---

            // Evaluate: Get point at angle parameter
            var rightPoint = ellipse1.Evaluate(0);
            var topPoint = ellipse1.Evaluate(0.25);
            new VPoint(rightPoint.X, rightPoint.Y).Color = "Red";
            new VPoint(topPoint.X, topPoint.Y).Color = "Blue";

            // --- Styling Ellipses ---
            ellipse1.Color = "Purple";
            ellipse1.LineWeight = 2;
            ellipse1.FillColor = "Lavender";
            ellipse1.Opacity = 0.3;

            ellipse2.Color = "Teal";
            ellipse2.LineType = LineType.Dashed;
        }

        // ═══════════════════════════════════════════════════════════════════
        // VRECTANGLE - Rectangle
        // ═══════════════════════════════════════════════════════════════════
        static void RectangleExamples()
        {
            VizConsole.Log("=== VRectangle Examples ===");

            // --- Creating Rectangles ---

            // From corner, width, height: VRectangle(cornerX, cornerY, width, height)
            var rect1 = new VRectangle(-40, -20, 80, 40);

            // From corner point
            var rect2 = new VRectangle(new VPoint(-40, 40), 80, 30);

            // --- Rectangle Properties ---
            VizConsole.Log($"Rect1 Corner: ({rect1.Corner.X}, {rect1.Corner.Y})");
            VizConsole.Log($"Rect1 Width: {rect1.Width}");
            VizConsole.Log($"Rect1 Height: {rect1.Height}");
            VizConsole.Log($"Rect1 Perimeter: {rect1.GetLength():F2}");

            // Calculate center from corner
            double centerX = rect1.Corner.X + rect1.Width / 2;
            double centerY = rect1.Corner.Y + rect1.Height / 2;
            VizConsole.Log($"Rect1 Center: ({centerX}, {centerY})");

            // --- Styling Rectangles ---
            rect1.Color = "DarkBlue";
            rect1.LineWeight = 2;

            // Filled rectangle
            rect2.Color = "DarkGreen";
            rect2.FillColor = "LightGreen";
            rect2.Opacity = 0.5;

            // Rectangle with dashed border
            var rect3 = new VRectangle(-30, -70, 60, 25);
            rect3.Color = "Orange";
            rect3.LineType = LineType.DashDot;
        }

        // ═══════════════════════════════════════════════════════════════════
        // VPOLYGON - Closed Polygon
        // ═══════════════════════════════════════════════════════════════════
        static void PolygonExamples()
        {
            VizConsole.Log("=== VPolygon Examples ===");

            // --- Creating Polygons ---

            // From list of points (automatically closed)
            var trianglePoints = new List<VPoint>
            {
                new VPoint(0, 40),
                new VPoint(-35, -20),
                new VPoint(35, -20)
            };
            var triangle = new VPolygon(trianglePoints);

            // Pentagon using calculated points
            var pentagonPoints = new List<VPoint>();
            for (int i = 0; i < 5; i++)
            {
                double angle = Math.PI / 2 + i * 2 * Math.PI / 5;  // Start from top
                pentagonPoints.Add(new VPoint(
                    80 + 30 * Math.Cos(angle),
                    0 + 30 * Math.Sin(angle)
                ));
            }
            var pentagon = new VPolygon(pentagonPoints);

            // Hexagon
            var hexPoints = new List<VPoint>();
            for (int i = 0; i < 6; i++)
            {
                double angle = i * Math.PI / 3;
                hexPoints.Add(new VPoint(
                    -80 + 25 * Math.Cos(angle),
                    0 + 25 * Math.Sin(angle)
                ));
            }
            var hexagon = new VPolygon(hexPoints);

            // --- Polygon Properties ---
            VizConsole.Log($"Triangle Points: {triangle.Points.Count}");
            VizConsole.Log($"Triangle Area: {triangle.Area:F2}");
            VizConsole.Log($"Triangle Perimeter: {triangle.GetLength():F2}");
            VizConsole.Log($"Triangle SignedArea: {triangle.SignedArea:F2}");

            // --- Polygon Methods ---

            // Clone
            var triangleCopy = (VPolygon)triangle.Clone();

            // --- Styling Polygons ---
            triangle.Color = "Red";
            triangle.LineWeight = 2;
            triangle.FillColor = "LightCoral";
            triangle.Opacity = 0.4;

            pentagon.Color = "Blue";
            pentagon.FillColor = "LightBlue";
            pentagon.Opacity = 0.4;

            hexagon.Color = "Green";
            hexagon.LineType = LineType.Dotted;
        }

        // ═══════════════════════════════════════════════════════════════════
        // VPOLYLINE - Open Path of Line Segments
        // ═══════════════════════════════════════════════════════════════════
        static void PolylineExamples()
        {
            VizConsole.Log("=== VPolyline Examples ===");

            // --- Creating Polylines ---

            // From list of points (open path - not closed)
            var zigzagPoints = new List<VPoint>
            {
                new VPoint(-60, -20),
                new VPoint(-30, 20),
                new VPoint(0, -20),
                new VPoint(30, 20),
                new VPoint(60, -20)
            };
            var zigzag = new VPolyline(zigzagPoints);

            // Staircase pattern
            var stairPoints = new List<VPoint>();
            for (int i = 0; i < 6; i++)
            {
                stairPoints.Add(new VPoint(-50 + i * 20, 40 + i * 10));
                stairPoints.Add(new VPoint(-50 + (i + 1) * 20, 40 + i * 10));
            }
            var staircase = new VPolyline(stairPoints);

            // --- Polyline Properties ---
            VizConsole.Log($"Zigzag Points: {zigzag.Points.Count}");
            VizConsole.Log($"Zigzag Length: {zigzag.GetLength():F2}");

            // --- Polyline Methods ---

            // PointAtParameter: Get point along the path (0 to 1)
            var midPoint = zigzag.PointAtParameter(0.5);
            new VPoint(midPoint.X, midPoint.Y).Color = "Red";

            // --- Styling Polylines ---
            zigzag.Color = "Blue";
            zigzag.LineWeight = 3;

            staircase.Color = "Orange";
            staircase.LineWeight = 2;
            staircase.LineType = LineType.Dashed;
        }

        // ═══════════════════════════════════════════════════════════════════
        // VBEZIER - Cubic Bezier Curve
        // ═══════════════════════════════════════════════════════════════════
        static void BezierExamples()
        {
            VizConsole.Log("=== VBezier Examples ===");

            // --- Creating Bezier Curves ---

            // Cubic Bezier: P0 (start), P1 (control1), P2 (control2), P3 (end)
            var bezier1 = new VBezier(
                new VPoint(-60, 0),    // P0: Start point
                new VPoint(-30, 50),   // P1: Control point 1
                new VPoint(30, -50),   // P2: Control point 2
                new VPoint(60, 0)      // P3: End point
            );

            // S-curve
            var bezier2 = new VBezier(
                new VPoint(-50, -40),
                new VPoint(50, -40),
                new VPoint(-50, 40),
                new VPoint(50, 40)
            );

            // --- Bezier Properties ---
            VizConsole.Log($"Bezier1 Start: ({bezier1.StartPoint.X}, {bezier1.StartPoint.Y})");
            VizConsole.Log($"Bezier1 End: ({bezier1.EndPoint.X}, {bezier1.EndPoint.Y})");
            VizConsole.Log($"Bezier1 Length: {bezier1.GetLength():F2}");

            // Control points are P0, P1, P2, P3
            VizConsole.Log($"P1: ({bezier1.P1.X}, {bezier1.P1.Y})");
            VizConsole.Log($"P2: ({bezier1.P2.X}, {bezier1.P2.Y})");

            // --- Bezier Methods ---

            // Evaluate: Get point at parameter t (0 to 1)
            var midBezier = bezier1.Evaluate(0.5);
            new VPoint(midBezier.X, midBezier.Y).Color = "Red";

            // --- Visualize Control Points ---
            new VPoint(bezier1.P1.X, bezier1.P1.Y).Color = "Gray";
            new VPoint(bezier1.P2.X, bezier1.P2.Y).Color = "Gray";

            // Control handles (lines from endpoints to control points)
            var handle1 = new VLine(bezier1.P0, bezier1.P1);
            handle1.Color = "LightGray";
            handle1.LineType = LineType.Dotted;

            var handle2 = new VLine(bezier1.P3, bezier1.P2);
            handle2.Color = "LightGray";
            handle2.LineType = LineType.Dotted;

            // --- Styling Beziers ---
            bezier1.Color = "Blue";
            bezier1.LineWeight = 3;

            bezier2.Color = "Purple";
            bezier2.LineWeight = 2;
        }

        // ═══════════════════════════════════════════════════════════════════
        // VSPLINE - Smooth Curve Through Points
        // ═══════════════════════════════════════════════════════════════════
        static void SplineExamples()
        {
            VizConsole.Log("=== VSpline Examples ===");

            // --- Creating Splines ---

            // Spline passes through all control points smoothly
            var splinePoints = new List<VPoint>
            {
                new VPoint(-60, 0),
                new VPoint(-30, 40),
                new VPoint(0, -20),
                new VPoint(30, 30),
                new VPoint(60, 0)
            };
            var spline = new VSpline(splinePoints);

            // Wave pattern
            var wavePoints = new List<VPoint>();
            for (int i = 0; i <= 8; i++)
            {
                double x = -80 + i * 20;
                double y = 30 * Math.Sin(i * Math.PI / 2);
                wavePoints.Add(new VPoint(x, y - 60));
            }
            var wave = new VSpline(wavePoints);

            // --- Spline Properties ---
            VizConsole.Log($"Spline ControlPoints: {spline.ControlPoints.Count}");
            VizConsole.Log($"Spline Length: {spline.GetLength():F2}");

            // --- Spline Methods ---

            // Evaluate: Get point along spline
            var quarterPoint = spline.Evaluate(0.25);
            new VPoint(quarterPoint.X, quarterPoint.Y).Color = "Red";

            // --- Visualize Control Points ---
            foreach (var pt in splinePoints)
            {
                var marker = new VPoint(pt.X, pt.Y);
                marker.Color = "Gray";
            }

            // --- Styling Splines ---
            spline.Color = "DarkGreen";
            spline.LineWeight = 3;

            wave.Color = "Teal";
            wave.LineWeight = 2;
        }

        // ═══════════════════════════════════════════════════════════════════
        // VTEXT - Text Label
        // ═══════════════════════════════════════════════════════════════════
        static void TextExamples()
        {
            VizConsole.Log("=== VText Examples ===");

            // --- Creating Text ---

            // Basic text: VText(x, y, content)
            var text1 = new VText(0, 50, "Hello, Code2Viz!");

            // Multiline text
            var text2 = new VText(0, 0, "Line 1\nLine 2\nLine 3");

            // --- Text Properties ---
            VizConsole.Log($"Text1 Content: {text1.Content}");
            VizConsole.Log($"Text1 Location: ({text1.Location.X}, {text1.Location.Y})");

            // --- Styling Text ---

            // Font size (Height property)
            text1.Height = 24;
            text1.Color = "DarkBlue";

            // Different font sizes
            text2.Height = 14;
            text2.Color = "Gray";

            // Large title
            var title = new VText(0, 80, "GEOMETRY DEMO");
            title.Height = 32;
            title.Color = "Purple";

            // Small annotation
            var annotation = new VText(20, -50, "<- This is a point");
            annotation.Height = 10;
            annotation.Color = "Green";

            // Reference point for annotation
            var refPoint = new VPoint(10, -50);
            refPoint.Color = "Green";
        }

        // ═══════════════════════════════════════════════════════════════════
        // VARROW - Arrow (Line with Arrowhead)
        // ═══════════════════════════════════════════════════════════════════
        static void ArrowExamples()
        {
            VizConsole.Log("=== VArrow Examples ===");

            // --- Creating Arrows ---

            // From points
            var arrow1 = new VArrow(new VPoint(-50, 0), new VPoint(50, 0));   // Horizontal
            var arrow2 = new VArrow(new VPoint(0, -40), new VPoint(0, 40));   // Vertical

            // From coordinates
            var arrow3 = new VArrow(-40, -40, 40, 40);   // Diagonal
            var arrow4 = new VArrow(40, -40, -40, 40);

            // --- Arrow Properties ---
            VizConsole.Log($"Arrow1 Start: ({arrow1.Start.X}, {arrow1.Start.Y})");
            VizConsole.Log($"Arrow1 End: ({arrow1.End.X}, {arrow1.End.Y})");
            VizConsole.Log($"Arrow1 Length: {arrow1.Start.DistanceTo(arrow1.End):F2}");

            // --- Styling Arrows ---
            arrow1.Color = "Red";
            arrow1.LineWeight = 2;

            arrow2.Color = "Blue";
            arrow2.LineWeight = 2;

            arrow3.Color = "Green";
            arrow3.LineWeight = 2;
            arrow3.LineType = LineType.Dashed;

            arrow4.Color = "Orange";
            arrow4.LineWeight = 2;
        }

        // ═══════════════════════════════════════════════════════════════════
        // VDIMENSION - Dimension Line with Measurement
        // ═══════════════════════════════════════════════════════════════════
        static void DimensionExamples()
        {
            VizConsole.Log("=== VDimension Examples ===");

            // Create a rectangle to dimension
            var rect = new VRectangle(-40, -25, 80, 50);
            rect.Color = "Gray";

            // --- Creating Dimensions ---

            // Dimension from two points
            var widthDim = new VDimension(
                new VPoint(-40, -25),   // Start point
                new VPoint(40, -25)     // End point
            );
            widthDim.Offset = 15;       // Distance from line

            // Vertical dimension (height)
            var heightDim = new VDimension(
                new VPoint(40, -25),
                new VPoint(40, 25)
            );
            heightDim.Offset = 15;

            // --- Dimension Properties ---
            VizConsole.Log($"Width Dimension: {widthDim.Distance:F2}");
            VizConsole.Log($"Height Dimension: {heightDim.Distance:F2}");

            // --- Styling Dimensions ---
            widthDim.Color = "Blue";
            heightDim.Color = "Blue";
        }

        // ═══════════════════════════════════════════════════════════════════
        // VGROUP - Group of Shapes
        // ═══════════════════════════════════════════════════════════════════
        static void GroupExamples()
        {
            VizConsole.Log("=== VGroup Examples ===");

            // --- Creating Groups ---

            // Create shapes to group
            var face = new VCircle(0, 0, 40);
            var leftEye = new VCircle(-15, 10, 5);
            var rightEye = new VCircle(15, 10, 5);
            var smile = new VArc(0, -5, 20, 200, 340);

            // Group them together
            var smiley = new VGroup(new List<Shape> { face, leftEye, rightEye, smile });

            // --- Group Properties ---
            VizConsole.Log($"Group contains {smiley.Shapes.Count} shapes");

            // --- Group Methods ---

            // Add more shapes to group
            var nose = new VLine(0, 5, 0, -5);
            nose.Color = "Black";
            smiley.Add(nose);

            // --- Styling Groups ---
            // Style is applied to individual children
            face.Color = "Gold";
            face.FillColor = "Yellow";
            face.Opacity = 0.8;

            leftEye.Color = "Black";
            leftEye.FillColor = "Black";

            rightEye.Color = "Black";
            rightEye.FillColor = "Black";

            smile.Color = "Black";
            smile.LineWeight = 3;

            // Create another group at different position
            var circle = new VCircle(80, 0, 20);
            circle.Color = "Red";
            var square = new VRectangle(65, -15, 30, 30);
            square.Color = "Blue";
            var group2 = new VGroup(new List<Shape> { circle, square });
        }

        // ═══════════════════════════════════════════════════════════════════
        // VGRID - Grid of Points
        // ═══════════════════════════════════════════════════════════════════
        static void GridExamples()
        {
            VizConsole.Log("=== VGrid Examples ===");

            // --- Creating Grids ---

            // Grid at origin with specified count and spacing
            // VGrid(location, xcount, ycount, xSpacing, ySpacing, centered)
            var grid1 = new VGrid(new VPoint(0, 0), 8, 6, 25.0, 25.0, true);

            // --- Grid Properties ---
            VizConsole.Log($"Grid XCount: {grid1.XCount}");
            VizConsole.Log($"Grid YCount: {grid1.YCount}");
            VizConsole.Log($"Grid XSpacing: {grid1.XSpacing}");
            VizConsole.Log($"Grid YSpacing: {grid1.YSpacing}");
            VizConsole.Log($"Grid Total Points: {grid1.Count}");

            // --- Styling Grids ---
            grid1.Color = "LightGray";
            grid1.LineWeight = 0.5;
            grid1.ApplyStyle();

            // Access individual points
            var centerPoint = grid1[3, 2];  // column 3, row 2
            centerPoint.Color = "Red";

            // Highlight a row
            var row = grid1.GetRow(0);
            foreach (var pt in row)
            {
                pt.Color = "Blue";
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // BOOLEAN OPERATIONS - Union, Intersection, Difference
        // ═══════════════════════════════════════════════════════════════════
        static void BooleanOperationsExamples()
        {
            VizConsole.Log("=== Boolean Operations Examples ===");

            // Create two overlapping polygons
            var square1 = new VPolygon(new List<VPoint>
            {
                new VPoint(-30, -30),
                new VPoint(30, -30),
                new VPoint(30, 30),
                new VPoint(-30, 30)
            });

            var square2 = new VPolygon(new List<VPoint>
            {
                new VPoint(0, 0),
                new VPoint(60, 0),
                new VPoint(60, 60),
                new VPoint(0, 60)
            });

            // Show original shapes (faded)
            square1.Color = "Blue";
            square1.FillColor = "LightBlue";
            square1.Opacity = 0.3;

            square2.Color = "Red";
            square2.FillColor = "LightCoral";
            square2.Opacity = 0.3;

            // --- Union ---
            var unionResult = BooleanOps.Union(square1, square2);
            if (unionResult != null)
            {
                VizConsole.Log($"Union created polygon with {unionResult.Points.Count} points");
            }

            // --- Intersection ---
            var intersectResults = BooleanOps.Intersect(square1, square2);
            VizConsole.Log($"Intersection created {intersectResults.Count} polygon(s)");
            foreach (var poly in intersectResults)
            {
                poly.Color = "Green";
                poly.FillColor = "LightGreen";
                poly.Opacity = 0.5;
            }

            // --- Difference ---
            var diffResults = BooleanOps.Difference(square1, square2);
            VizConsole.Log($"Difference created {diffResults.Count} polygon(s)");

            // --- Offset Polygon ---
            var triangle = new VPolygon(new List<VPoint>
            {
                new VPoint(-100, -40),
                new VPoint(-60, -40),
                new VPoint(-80, 0)
            });
            triangle.Color = "Purple";

            var offsetResults = BooleanOps.OffsetPolygon(triangle, 5);  // Outward offset
            foreach (var poly in offsetResults)
            {
                poly.Color = "Violet";
                poly.LineType = LineType.Dashed;
            }

            // --- Point in Polygon Test ---
            var testPoint = new VPoint(15, 15);
            bool inside = BooleanOps.PointInPolygon(square1, testPoint);
            VizConsole.Log($"Point (15,15) inside square1: {inside}");
            testPoint.Color = inside ? "Green" : "Red";
        }

        // ═══════════════════════════════════════════════════════════════════
        // ANIMATIONS - Bringing Shapes to Life
        // ═══════════════════════════════════════════════════════════════════
        static void AnimationExamples()
        {
            VizConsole.Log("=== Animation Examples ===");

            // Create an animator to manage animations
            var animator = new Animator();
            animator.Repeat = true;    // Loop animations
            animator.Speed = 1.0;      // Normal speed (0.5 = half, 2.0 = double)

            // --- Create shapes to animate ---
            var circle = new VCircle(-60, 0, 20);
            circle.Color = "Blue";
            circle.FillColor = "LightBlue";

            var square = new VPolygon(new List<VPoint>
            {
                new VPoint(-10, -10),
                new VPoint(10, -10),
                new VPoint(10, 10),
                new VPoint(-10, 10)
            });
            square.Color = "Red";
            square.FillColor = "LightCoral";

            var text = new VText(40, 0, "Animated!");
            text.Color = "Green";
            text.Height = 16;

            // ═══════════════════════════════════════════════════════════════
            // DRAW ANIMATION - Progressive drawing of shapes
            // ═══════════════════════════════════════════════════════════════

            // DrawAnimation reveals a shape over time (like drawing with a pen)
            var drawCircle = new DrawAnimation(circle, 1.5);  // 1.5 seconds
            animator.AddToAnimations(drawCircle);

            // ═══════════════════════════════════════════════════════════════
            // MOVE ANIMATION - Translate shapes
            // ═══════════════════════════════════════════════════════════════

            // MoveAnimation moves a shape by a displacement vector
            var moveSquare = new MoveAnimation(
                square,
                new VXYZ(50, 30, 0),   // Move right 50, up 30
                1.0                     // Duration in seconds
            );
            animator.AddToAnimations(moveSquare);

            // ═══════════════════════════════════════════════════════════════
            // ROTATE ANIMATION - Spin shapes
            // ═══════════════════════════════════════════════════════════════

            // RotateAnimation rotates around a pivot point
            var rotateSquare = new RotateAnimation(
                square,
                new VPoint(0, 0),      // Pivot point (center of rotation)
                360,                    // Angle in degrees
                2.0                     // Duration
            );
            animator.AddToAnimations(rotateSquare);

            // ═══════════════════════════════════════════════════════════════
            // FADE IN/OUT ANIMATIONS - Opacity transitions
            // ═══════════════════════════════════════════════════════════════

            // FadeInAnimation fades from transparent to opaque
            var fadeInText = new FadeInAnimation(text, 1.0);
            animator.AddToAnimations(fadeInText);

            // Pause between animations
            animator.Pause(0.5);  // Wait 0.5 seconds

            // FadeOutAnimation fades from opaque to transparent
            var fadeOutText = new FadeOutAnimation(text, 1.0);
            animator.AddToAnimations(fadeOutText);

            // ═══════════════════════════════════════════════════════════════
            // PARALLEL ANIMATIONS - Multiple animations at once
            // ═══════════════════════════════════════════════════════════════

            // Add multiple animations to play simultaneously
            var parallelAnims = new List<Animation>
            {
                new MoveAnimation(circle, new VXYZ(30, 0, 0), 1.0),
                new RotateAnimation(square, new VPoint(0, 0), 180, 1.0)
            };
            animator.AddToAnimations(parallelAnims);  // These play together

            // ═══════════════════════════════════════════════════════════════
            // VALUE ANIMATION - Animate any numeric property
            // ═══════════════════════════════════════════════════════════════

            // ValueAnimation can animate any double property
            var growCircle = new ValueAnimation<VCircle>(
                circle,
                c => c.Radius,         // Property to animate
                20,                     // Start value
                40,                     // End value
                1.0                     // Duration
            );
            animator.AddToAnimations(growCircle);

            // Shrink it back
            var shrinkCircle = new ValueAnimation<VCircle>(
                circle,
                c => c.Radius,
                40,
                20,
                1.0
            );
            animator.AddToAnimations(shrinkCircle);

            // ═══════════════════════════════════════════════════════════════
            // START THE ANIMATION
            // ═══════════════════════════════════════════════════════════════

            VizConsole.Log($"Total animation duration: {animator.Duration:F2} seconds");
            animator.Animate();  // Start playback!
        }

        // ═══════════════════════════════════════════════════════════════════
        // EASING FUNCTIONS - Control Animation Timing
        // ═══════════════════════════════════════════════════════════════════
        static void EasingFunctionsExamples()
        {
            VizConsole.Log("=== Easing Functions Demo ===");

            var animator = new Animator();
            animator.Repeat = true;

            // Create circles to demonstrate different easings
            double startX = -80;
            double endX = 80;
            double y = 60;
            double spacing = 30;

            // Linear (default) - constant speed
            var linear = new VCircle(startX, y, 10);
            linear.Color = "Red";
            linear.FillColor = "Red";
            var linearMove = new MoveAnimation(linear, new VXYZ(endX - startX, 0, 0), 2.0);
            linearMove.EasingFunction = EasingFunctions.Linear;

            // EaseInQuad - starts slow, accelerates
            y -= spacing;
            var easeIn = new VCircle(startX, y, 10);
            easeIn.Color = "Orange";
            easeIn.FillColor = "Orange";
            var easeInMove = new MoveAnimation(easeIn, new VXYZ(endX - startX, 0, 0), 2.0);
            easeInMove.EasingFunction = EasingFunctions.EaseInQuad;

            // EaseOutQuad - starts fast, decelerates
            y -= spacing;
            var easeOut = new VCircle(startX, y, 10);
            easeOut.Color = "Yellow";
            easeOut.FillColor = "Yellow";
            var easeOutMove = new MoveAnimation(easeOut, new VXYZ(endX - startX, 0, 0), 2.0);
            easeOutMove.EasingFunction = EasingFunctions.EaseOutQuad;

            // EaseInOutQuad - slow start and end, fast middle
            y -= spacing;
            var easeInOut = new VCircle(startX, y, 10);
            easeInOut.Color = "Green";
            easeInOut.FillColor = "Green";
            var easeInOutMove = new MoveAnimation(easeInOut, new VXYZ(endX - startX, 0, 0), 2.0);
            easeInOutMove.EasingFunction = EasingFunctions.EaseInOutQuad;

            // EaseInOutCubic - more pronounced ease
            y -= spacing;
            var cubic = new VCircle(startX, y, 10);
            cubic.Color = "Blue";
            cubic.FillColor = "Blue";
            var cubicMove = new MoveAnimation(cubic, new VXYZ(endX - startX, 0, 0), 2.0);
            cubicMove.EasingFunction = EasingFunctions.EaseInOutCubic;

            // Add labels
            var l1 = new VText(-110, 60, "Linear"); l1.Color = "Gray"; l1.Height = 10;
            var l2 = new VText(-110, 30, "EaseIn"); l2.Color = "Gray"; l2.Height = 10;
            var l3 = new VText(-110, 0, "EaseOut"); l3.Color = "Gray"; l3.Height = 10;
            var l4 = new VText(-110, -30, "EaseInOut"); l4.Color = "Gray"; l4.Height = 10;
            var l5 = new VText(-110, -60, "Cubic"); l5.Color = "Gray"; l5.Height = 10;

            // Play all animations together
            animator.AddToAnimations(new List<Animation>
            {
                linearMove,
                easeInMove,
                easeOutMove,
                easeInOutMove,
                cubicMove
            });

            animator.Animate();
        }
    }
}
