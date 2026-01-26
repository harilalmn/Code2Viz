using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace Code2Viz.Documentation
{
    public class DocGenerator
    {
        private Assembly _assembly;
        private Dictionary<string, string> _summaries;
        private Dictionary<string, string> _fsharpSamples;
        private Dictionary<string, string> _csharpSamples;
        private Dictionary<string, string> _memberDescriptions;

        public DocGenerator()
        {
            _assembly = Assembly.GetExecutingAssembly();
            InitializeSummaries();
            InitializeFSharpSamples();
            InitializeCSharpSamples();
            InitializeMemberDescriptions();
        }

        private void InitializeSummaries()
        {
            _summaries = new Dictionary<string, string>
            {
                { "Code2Viz", "Root namespace for the Code2Viz application." },
                { "Code2Viz.Geometry", "Contains classes and interfaces for 2D geometric shapes and operations." },
                { "Code2Viz.Editor", "Contains classes related to the code editor, including formatting, completion, and snippets." },

                // Base classes
                { "Shape", "Abstract base class for all drawable shapes. Provides common properties like Color, FillColor, LineWeight, and animation properties (DrawFactor, OffsetX, OffsetY, RotationAngle). Also defines common methods: Draw(), Clone(), Move(), Rotate(), Flip(), Scale(), GetBounds(), Contains(), DistanceTo()." },
                { "IDrawable", "Interface for any object that can be drawn on the canvas. Defines Draw() method and styling properties." },
                { "ICurve", "Interface for geometric shapes that can be treated as curves. Extends IDrawable, so all curves have Draw(), Color, FillColor, and LineWeight. Provides curve operations: StartPoint, EndPoint, SelfIntersecting, Divide(), Measure(), GetLength(), Project(), PointAtSegmentLength(), Offset(), PointsAtChordLengthFromPoint(), SplitAtPoint(), NormalAtPoint(), Intersect(). The SelfIntersecting property indicates if the curve crosses itself. The Intersect() method computes intersection points with another curve." },
                { "IntersectionResult", "Represents the result of an intersection operation between curves. Contains Points (list of intersection points) and Curves (list of overlapping segments). Properties: HasIntersection (true if any intersection), IsSinglePoint (exactly one point), HasOverlap (curves share a segment), Count (total elements). Use Intersect() method on any ICurve to compute intersections." },
                { "CurveIntersection", "Static utility class providing curve intersection algorithms. Supports Line-Line, Line-Circle, Line-Arc, Line-Ellipse, Circle-Circle, Circle-Arc, Arc-Arc intersections with specialized algorithms. Complex curves use segment-based approximation. Also provides IsSelfIntersecting() for detecting self-intersections." },

                // Shapes
                { "VArc", "Represents a 2D arc defined by a center point, radius, start angle, and end angle (in degrees). The arc is drawn counter-clockwise from start to end angle." },
                { "VCircle", "Represents a 2D circle defined by a center point and a radius. Can be filled and/or stroked." },
                { "VRectangle", "Represents a 2D rectangle defined by a corner point (bottom-left), width, and height. Inherits from VPolygon, so all polygon methods (Area, Slice, etc.) are available. Supports rotation via RotationAngle property. Can also be created from two corner points (bottom-left and top-right)." },
                { "VPolygon", "Represents a closed 2D polygon defined by a list of vertices. Automatically closes the shape by connecting last point to first." },
                { "VPolyline", "Represents an open sequence of connected line segments. Unlike polygon, does not close automatically." },
                { "VLine", "Represents a straight line segment between two points. The most basic geometric primitive." },
                { "VXLine", "Represents an infinite construction line (like AutoCAD's XLine). Extends infinitely in both directions through a base point along a direction. Useful for construction geometry and slicing polygons. Static helpers: Horizontal(y), Vertical(x)." },
                { "VRay", "Represents a semi-infinite ray (like AutoCAD's Ray). Starts at an origin point and extends infinitely in one direction. Static helpers: HorizontalRight, HorizontalLeft, VerticalUp, VerticalDown, AtAngle." },
                { "VEllipse", "Represents a 2D ellipse defined by a center point, X radius (horizontal), and Y radius (vertical)." },
                { "VPoint", "Represents a point in 2D Cartesian space (X, Y). Can be drawn as a small dot on the canvas." },
                { "VBezier", "Represents a 2D cubic Bezier curve defined by four control points: start, control1, control2, and end." },
                { "VSpline", "Represents a smooth Catmull-Rom spline curve passing through a series of points." },
                { "VText", "Represents text drawn at a specific position. Supports font size via Height property or constructor parameter. Constructors: VText(point, text), VText(point, text, height), VText(x, y, text), VText(x, y, text, height). Supports Font and FontWeight properties for styling." },
                { "VGroup", "Represents a collection of shapes treated as a single unit. Supports multiple constructors (empty, params, IEnumerable, List), group transformations (Move, Rotate, Scale, Flip), style application (ApplyStyle, ApplyColor, ApplyFillColor), and utility methods (Flatten, ForEach, Where, GetShapesOfType). When drawn, the group is rendered and selected as a single entity on the canvas." },
                { "VGrid", "Represents a rectangular grid of VPoints. Constructor: VGrid(location, xcount, ycount, xSpacing, ySpacing, centered). If centered=true, grid is centered at location; if false, location is bottom-left corner. Access points via Points property, indexers [index] or [col, row], or GetRow()/GetColumn() methods. Supports all Shape transformations (Move, Rotate, Scale, Flip) and ApplyStyle() to set colors on all points." },
                { "VArrow", "Represents an arrow (line with arrowhead). Supports single or double-ended arrows with configurable head size and angle." },
                { "VDimension", "Represents a dimension line showing the distance between two points with text annotation. Useful for technical drawings." },

                // Legacy aliases (for backward compatibility)
                { "Arc2D", "Represents a 2D arc defined by a center, radius, start angle, and end angle." },
                { "Circle2D", "Represents a 2D circle defined by a center point and a radius." },
                { "Rectangle2D", "Represents a 2D axis-aligned rectangle defined by a corner, width, and height." },
                { "Polygon2D", "Represents a closed 2D polygon defined by a list of vertices." },
                { "Polyline2D", "Represents an open sequence of connected line segments." },
                { "Line2D", "Represents a straight line segment between two points." },
                { "Ellipse2D", "Represents a 2D ellipse defined by a center, X radius, and Y radius." },
                { "Point2D", "Represents a point in 2D Cartesian space (X, Y)." },
                { "Bezier2D", "Represents a 2D cubic Bezier curve." },
                { "Spline2D", "Represents a smooth spline curve passing through a series of points." },
                { "Text2D", "Represents text drawn at a specific position." },
                { "Group2D", "Represents a collection of shapes treated as a single unit." },
                { "Grid2D", "Represents a rectangular grid of points." },

                // Support classes
                { "VXYZ", "Represents a 3D vector or point with X, Y, Z coordinates. Provides vector operations like Add, Subtract, CrossProduct, DotProduct, Normalize, GetLength. Also has static properties BasisX, BasisY, BasisZ, Zero." },
                { "VPlane", "Represents a plane in 3D space defined by origin and basis vectors. Used for coordinate transformations." },
                { "VTransform", "Represents a 3D transformation matrix for rotation and reflection operations." },
                { "VCoordinateSystem", "Represents a 3D coordinate system with origin and orthonormal basis vectors (X, Y, Z axes)." },
                { "GeometryHelper", "Static helper class providing common geometric algorithms like intersection, projection, distance calculations, and angle measurements." },
                { "ShapeDefaults", "Static class holding global default settings for shapes (GlobalColor, GlobalFillColor, GlobalLineWeight, GlobalLineType). These are populated from Project Settings." },
                { "LineType", "Enum defining the stroke style (line pattern) for shape outlines. Options: Continuous (solid, default), Dashed, Dotted, DashDot, DashDotDot, Center, Phantom, Hidden." },
                { "VColor", "Static utility class for easy color access and random color generation. Provides named color properties (Red, Blue, Green, etc.), GetRandomColor(pastel) for random colors, FromRgb/FromArgb for custom colors. Use with Color and FillColor properties." },
                { "ColorName", "Enum containing common color names (Red, Green, Blue, Yellow, Orange, etc.). Use VColor.FromEnum(ColorName.Red) to convert to string." },

                // Animation
                { "Code2Viz.Animation", "Contains classes for animating shapes over time. Use the Animator class to manage animation sequencing automatically." },
                { "Animator", "Main class for creating animations. Manages sequencing automatically - animations added with AddToAnimations() play sequentially; pass a List<Animation> for parallel playback. Call Animate() to start." },
                { "Animation", "Abstract base class for all animations. Defines Target shape, Duration, and EasingFunction. Subclasses implement the Apply() method. StartTime is set automatically by Animator." },
                { "DrawAnimation", "Animates the DrawFactor property to progressively draw a shape from 0% to 100%. Constructor: new DrawAnimation(shape, duration)." },
                { "MoveAnimation", "Animates moving a shape by a displacement vector. Constructor: new MoveAnimation(shape, displacement, duration)." },
                { "RotateAnimation", "Animates rotating a shape around a pivot point. Constructor: new RotateAnimation(shape, pivot, angleDegrees, duration)." },
                { "FlipAnimation", "Animates flipping (mirroring) a shape across an axis line. Constructor: new FlipAnimation(shape, mirrorAxis, duration)." },
                { "FadeInAnimation", "Animates fading in a shape from transparent to opaque. Constructor: new FadeInAnimation(shape, duration)." },
                { "FadeOutAnimation", "Animates fading out a shape from opaque to transparent. Constructor: new FadeOutAnimation(shape, duration, targetOpacity)." },
                { "EasingFunctions", "Static class providing common easing functions for smooth animations: Linear, EaseInQuad, EaseOutQuad, EaseInOutQuad, EaseInCubic, EaseOutCubic, EaseInOutCubic." },

                // Boolean Operations
                { "BooleanOps", "Static class providing polygon boolean operations using Clipper2 library. Supports Union (combine polygons), Intersect (overlapping area), Difference (subtract), Xor (symmetric difference), OffsetPolygon (grow/shrink), Simplify (remove redundant points), Area calculation, and PointInPolygon test." },

                // Array Operations
                { "ArrayOps", "Static class providing array and pattern generation for shapes. Includes LinearArray (copies along direction), RectangularArray (grid pattern), CircularArray (polar pattern around center), PathArray (copies along curve), SpiralArray (spiral pattern), and Mirror (create mirrored copy)." },

                // Export
                { "Code2Viz.Export", "Contains classes for exporting shapes and animations to various file formats." },
                { "DxfExporter", "Exports shapes to AutoCAD DXF format (R12 ASCII). Supports all shape types including lines, circles, arcs, ellipses, polygons, polylines, text, and arrows." },
                { "PdfExporter", "Exports shapes to vector PDF format using PdfSharp library. Preserves colors, stroke styles, and produces high-quality vector output suitable for printing." },
                { "SvgExporter", "Exports shapes to SVG (Scalable Vector Graphics) format. Web-compatible vector format that opens in browsers and vector editors. Supports all shape types with full color and styling." },
                { "VideoExporter", "Exports animations to MP4 video using Windows Media Foundation H.264 encoder. Renders vector graphics at target resolution using high DPI for sharp output. Supports resolution presets (Canvas Size, 720p, 1080p, 4K, Custom), configurable frame rate (15-60 FPS), and bitrate (1-20 Mbps). No external dependencies required." },
                { "GifEncoder", "Exports animations to animated GIF format. Supports configurable frame rate and duration. Good for short animations and web sharing." },

                // Canvas and Snap System
                { "Code2Viz.Canvas", "Contains classes for the interactive canvas, drawing tools, and snap detection system." },
                { "SnapType", "Enumeration of snap point types: Endpoint (line/arc ends), Midpoint (center of segments), Center (circle/ellipse/arc centers), Intersection (where curves cross), Nearest (closest point on curve), Perpendicular (90° from reference point), Extension (line extended beyond endpoint), Tangent (tangent point on circles/arcs)." },
                { "SnapResult", "Represents a detected snap point with its type, position, and distance from cursor. For Extension snaps, includes ExtensionSource (the endpoint the extension originates from) and ExtensionAngle (direction in degrees). For Perpendicular/Tangent snaps, includes ReferenceSource (your first click) and ConstraintPoint (the perpendicular/tangent point on the shape)." },
                { "SnapEngine", "Engine for detecting snap points on shapes. Supports 8 snap types (Endpoint, Midpoint, Center, Intersection, Nearest, Perpendicular, Extension, Tangent). Each snap type can be individually enabled/disabled via Settings. Uses spatial indexing for efficient detection even with many shapes." },
                { "DrawingInputMode", "Enumeration for precise input modes while drawing: None (mouse-controlled), Distance (typing distance value), Angle (typing angle value). Press Tab to cycle between modes when drawing. Type numbers to enter precise values, Enter to confirm." },
                { "DrawingTool", "Manages interactive drawing state and shape creation. Supports all shape types with visual preview. Features: snap detection with 8 snap types, orthogonal constraint (Shift key), precise distance/angle input (Tab to cycle, type value, Enter to confirm). The InputMode property indicates current input state; InputBuffer holds the typed value." },
            };
        }

        public string GetSummary(string name)
        {
            if (_summaries.TryGetValue(name, out var summary))
                return summary;
            return "No description available.";
        }

        public List<Type> GetDocumentableTypes()
        {
            return _assembly.GetTypes()
                .Where(t => t.IsPublic && (t.IsClass || t.IsAbstract) && t.Namespace != null &&
                    (t.Namespace.StartsWith("Code2Viz.Geometry") ||
                     t.Namespace.StartsWith("Code2Viz.Animation") ||
                     t.Namespace.StartsWith("Code2Viz.Export") ||
                     t.Name.Contains("Helper")))
                .OrderBy(t => t.Namespace)
                .ThenBy(t => t.Name)
                .ToList();
        }

        public FlowDocument GenerateDocForType(Type type)
        {
            var doc = new FlowDocument();
            doc.FontFamily = new FontFamily("Segoe UI");
            doc.PagePadding = new Thickness(20);
            doc.ColumnWidth = double.NaN; // Force single column mode

            // Title
            var title = new Paragraph(new Run(type.Name + " Class"))
            {
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.DarkSlateGray,
                Margin = new Thickness(0, 0, 0, 10)
            };
            doc.Blocks.Add(title);

            // Summary
            var summaryText = GetSummary(type.Name);
            doc.Blocks.Add(new Paragraph(new Run(summaryText)) { FontSize = 14, Margin = new Thickness(0, 0, 0, 20) });

            // Inheritance
            AddSectionHeader(doc, "Inheritance Hierarchy");
            doc.Blocks.Add(GenerateInheritance(type));

             // C# Samples
            AddSectionHeader(doc, "C# Sample Code");
            if (_csharpSamples == null) InitializeCSharpSamples();
            if (_csharpSamples.TryGetValue(type.Name, out var sample))
            {
                 var p = new Paragraph(new Run(sample));
                 p.FontFamily = new FontFamily("Consolas");
                 p.Background = Brushes.WhiteSmoke;
                 p.Padding = new Thickness(10);
                 doc.Blocks.Add(p);
            }
            else
            {
                 doc.Blocks.Add(new Paragraph(new Run("// No specific usage example available.")) { FontFamily = new FontFamily("Consolas"), Padding = new Thickness(5) });
            }

            // Syntax

            // Syntax
            AddSectionHeader(doc, "Syntax");
            doc.Blocks.Add(GenerateSyntax(type));

            // Constructors
            var dtors = type.GetConstructors();
            if (dtors.Length > 0)
            {
                AddSectionHeader(doc, "Constructors");
                doc.Blocks.Add(GenerateMemberTable(dtors, type.Name));
            }

            // Properties
            var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            if (props.Length > 0)
            {
                AddSectionHeader(doc, "Properties");
                doc.Blocks.Add(GenerateMemberTable(props, type.Name));
            }

            // Methods
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName && m.DeclaringType != typeof(object)) // Exclude getter/setter internal methods and Object methods
                .ToArray();

            if (methods.Length > 0)
            {
                AddSectionHeader(doc, "Methods");
                doc.Blocks.Add(GenerateMemberTable(methods, type.Name));
            }

            return doc;
        }

        private void AddSectionHeader(FlowDocument doc, string text)
        {
            doc.Blocks.Add(new Paragraph(new Run(text))
            {
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.Teal,
                Margin = new Thickness(0, 10, 0, 5),
                BorderBrush = Brushes.LightGray,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(0, 0, 0, 5)
            });
        }

        private Paragraph GenerateInheritance(Type type)
        {
            var p = new Paragraph();
            var hierarchy = new List<Type>();
            var current = type;
            while (current != null)
            {
                hierarchy.Insert(0, current);
                current = current.BaseType;
            }

            for (int i = 0; i < hierarchy.Count; i++)
            {
                var run = new Run(hierarchy[i].Name);
                if (i == hierarchy.Count - 1) run.FontWeight = FontWeights.Bold;
                p.Inlines.Add(run);
                if (i < hierarchy.Count - 1) p.Inlines.Add(" → ");
            }
            return p;
        }

        private Paragraph GenerateSyntax(Type type)
        {
            var syntax = $"public class {type.Name}";
            if (type.BaseType != null && type.BaseType != typeof(object))
                syntax += $" : {type.BaseType.Name}";

            var interfaces = type.GetInterfaces();
            if (interfaces.Length > 0)
            {
                syntax += (type.BaseType != null && type.BaseType != typeof(object) ? ", " : " : ");
                syntax += string.Join(", ", interfaces.Select(i => i.Name));
            }

            var p = new Paragraph(new Run(syntax))
            {
                FontFamily = new FontFamily("Consolas"),
                Background = Brushes.WhiteSmoke,
                Padding = new Thickness(10)
            };
            return p;
        }

        private Table GenerateMemberTable(MemberInfo[] members, string className = "")
        {
            var table = new Table();
            table.CellSpacing = 0;
            table.BorderBrush = Brushes.LightGray;
            table.BorderThickness = new Thickness(1);

            // Use fixed widths: Name, Type/Signature, Description
            table.Columns.Add(new TableColumn { Width = new GridLength(150) }); // Name
            table.Columns.Add(new TableColumn { Width = new GridLength(220) }); // Type/Signature
            table.Columns.Add(new TableColumn { Width = new GridLength(400) }); // Description

            var rowGroup = new TableRowGroup();

            // Header
            var headerRow = new TableRow();
            headerRow.Background = Brushes.AliceBlue;
            headerRow.Cells.Add(CreateHeaderCell("Name"));
            headerRow.Cells.Add(CreateHeaderCell("Type / Signature"));
            headerRow.Cells.Add(CreateHeaderCell("Description"));
            rowGroup.Rows.Add(headerRow);

            bool isAlt = false;
            foreach (var member in members)
            {
                var row = new TableRow();
                if (isAlt) row.Background = Brushes.WhiteSmoke;
                isAlt = !isAlt;

                // Name column
                var nameText = new Run(member.Name) { FontWeight = FontWeights.Bold, Foreground = Brushes.DarkBlue };
                var nameCell = new TableCell(new Paragraph(nameText)) { Padding = new Thickness(5), BorderBrush = Brushes.LightGray, BorderThickness = new Thickness(0,0,0,1) };
                row.Cells.Add(nameCell);

                // Type/Signature column
                string sig = "";
                string returnType = "";
                if (member is MethodInfo mi)
                {
                    var paramStr = string.Join(", ", mi.GetParameters().Select(p => $"{GetFriendlyTypeName(p.ParameterType)} {p.Name}"));
                    returnType = GetFriendlyTypeName(mi.ReturnType);
                    sig = $"{returnType} ({paramStr})";
                }
                else if (member is PropertyInfo pi)
                {
                    sig = GetFriendlyTypeName(pi.PropertyType);
                    var accessors = new List<string>();
                    if (pi.CanRead) accessors.Add("get");
                    if (pi.CanWrite) accessors.Add("set");
                    if (accessors.Count > 0)
                        sig += $" {{ {string.Join("; ", accessors)} }}";
                }
                else if (member is ConstructorInfo ci)
                {
                    var paramStr = string.Join(", ", ci.GetParameters().Select(p => $"{GetFriendlyTypeName(p.ParameterType)} {p.Name}"));
                    sig = string.IsNullOrEmpty(paramStr) ? "()" : $"({paramStr})";
                }

                var sigPara = new Paragraph(new Run(sig));
                sigPara.FontFamily = new FontFamily("Consolas");
                sigPara.FontSize = 11;
                sigPara.Foreground = Brushes.DarkSlateGray;
                sigPara.TextAlignment = TextAlignment.Left;
                var sigCell = new TableCell(sigPara) { Padding = new Thickness(5), BorderBrush = Brushes.LightGray, BorderThickness = new Thickness(0,0,0,1), TextAlignment = TextAlignment.Left };
                row.Cells.Add(sigCell);

                // Description column
                var description = GetMemberDescription(className, member.Name);
                if (string.IsNullOrEmpty(description))
                {
                    // Try base class descriptions for inherited members
                    description = GetMemberDescription("Shape", member.Name);
                }
                if (string.IsNullOrEmpty(description))
                {
                    description = GetMemberDescription("ICurve", member.Name);
                }
                var descPara = new Paragraph(new Run(description));
                descPara.FontSize = 11;
                descPara.Foreground = string.IsNullOrEmpty(description) ? Brushes.Gray : Brushes.Black;
                if (string.IsNullOrEmpty(description))
                    descPara.Inlines.Clear();
                var descCell = new TableCell(descPara) { Padding = new Thickness(5), BorderBrush = Brushes.LightGray, BorderThickness = new Thickness(0,0,0,1) };
                row.Cells.Add(descCell);

                rowGroup.Rows.Add(row);
            }

            table.RowGroups.Add(rowGroup);
            return table;
        }

        private string GetFriendlyTypeName(Type type)
        {
            if (type == typeof(void)) return "void";
            if (type == typeof(int)) return "int";
            if (type == typeof(double)) return "double";
            if (type == typeof(float)) return "float";
            if (type == typeof(bool)) return "bool";
            if (type == typeof(string)) return "string";
            if (type == typeof(object)) return "object";

            if (type.IsGenericType)
            {
                var baseName = type.Name;
                var tickIndex = baseName.IndexOf('`');
                if (tickIndex > 0)
                    baseName = baseName.Substring(0, tickIndex);
                var args = string.Join(", ", type.GetGenericArguments().Select(GetFriendlyTypeName));
                return $"{baseName}<{args}>";
            }

            return type.Name;
        }

        private TableCell CreateHeaderCell(string text)
        {
            return new TableCell(new Paragraph(new Run(text)) { FontWeight = FontWeights.Bold })
            {
                BorderBrush = Brushes.LightGray,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(5)
            };
        }

        private void InitializeFSharpSamples()
        {
            _fsharpSamples = new Dictionary<string, string>
            {
                { "VPoint", "let p = VPoint(100.0, 200.0)" },
                { "VLine", "let start = VPoint(0.0, 0.0)\nlet endP = VPoint(200.0, 200.0)\nlet line = VLine(start, endP)\nline.Color <- \"#00FF00\"\nline.LineWeight <- 2.0\nline.Draw()" },
                { "VXLine", "// Infinite construction line\nlet xline = VXLine(VPoint(0.0, 0.0), VXYZ(1.0, 1.0, 0.0))\nxline.Color <- \"Gray\"\nxline.Draw()\n\n// Horizontal and vertical helpers\nlet hLine = VXLine.Horizontal(100.0)\nlet vLine = VXLine.Vertical(50.0)\nhLine.Draw()\nvLine.Draw()" },
                { "VRay", "// Semi-infinite ray\nlet ray = VRay(VPoint(0.0, 0.0), VXYZ(1.0, 0.5, 0.0))\nray.Color <- \"Orange\"\nray.Draw()\n\n// Static helpers\nlet rightRay = VRay.HorizontalRight(VPoint(0.0, 0.0))\nlet angledRay = VRay.AtAngle(VPoint(0.0, 0.0), 45.0)\nrightRay.Draw()\nangledRay.Draw()" },
                { "VCircle", "let center = VPoint(300.0, 200.0)\nlet radius = 50.0\nlet circle = VCircle(center, radius)\ncircle.FillColor <- \"Blue\"\ncircle.Draw()" },
                { "VRectangle", "let rect = VRectangle(VPoint(50.0, 50.0), 150.0, 100.0)\nrect.FillColor <- \"#800000FF\"\nrect.Draw()\n\n// Create from two corner points (bottom-left and top-right)\nlet rect2 = VRectangle(VPoint(0.0, 0.0), VPoint(100.0, 75.0))\nrect2.Draw()" },
                { "VEllipse", "let ellipse = VEllipse(VPoint(400.0, 300.0), 80.0, 40.0)\nellipse.LineWeight <- 3.0\nellipse.Draw()" },
                { "VArc", "let arc = VArc(VPoint(200.0, 200.0), 100.0, 0.0, 180.0)\narc.Draw()" },
                { "VPolygon", "let pts = [| VPoint(100.0,100.0); VPoint(200.0,100.0); VPoint(150.0,200.0) |]\nlet poly = VPolygon(pts)\npoly.FillColor <- \"Yellow\"\npoly.Draw()" },
                { "VPolyline", "let pts = [| VPoint(100.0,300.0); VPoint(150.0,350.0); VPoint(200.0,300.0) |]\nlet line = VPolyline(pts)\nline.Draw()" },
                { "VBezier", "let b = VBezier(VPoint(0.0,0.0), VPoint(0.0,100.0), VPoint(100.0,100.0), VPoint(100.0,0.0))\nb.Draw()" },
                { "VSpline", "let pts = [| VPoint(0.0,0.0); VPoint(50.0,50.0); VPoint(100.0,0.0) |]\nlet s = VSpline(pts)\ns.Draw()" },
                { "VText", "let t = VText(VPoint(50.0, 50.0), \"Hi\")\nt.Height <- 40.0\nt.Draw()" },
                { "VArrow", "// From two points\nlet a = VArrow(VPoint(10.0, 10.0), VPoint(100.0, 10.0))\na.Draw()\n\n// From start point, direction, and length\nlet a2 = VArrow(VPoint(0.0, 0.0), VXYZ.BasisX, 50.0)\na2.Draw()" },
                { "VGroup", @"// Create a group from shapes
let circle = VCircle(VPoint(0.0, 0.0), 20.0)
let line1 = VLine(VPoint(-30.0, 0.0), VPoint(30.0, 0.0))
let line2 = VLine(VPoint(0.0, -30.0), VPoint(0.0, 30.0))
let group = VGroup([| circle :> Shape; line1 :> Shape; line2 :> Shape |])

// Transform the entire group
group.Move(VXYZ(100.0, 100.0, 0.0))
group.Rotate(VPoint(100.0, 100.0), 45.0)

// Apply styling to all shapes
group.Color <- ""Cyan""
group.ApplyStyle() |> ignore

// Draw as a single selectable entity
group.Draw()" },

                { "VGrid", @"// Create a centered grid at origin: 5 columns x 3 rows, spacing 10
let grid = VGrid(VPoint(0.0, 0.0), 5, 3, 10.0, true)
grid.FillColor <- ""Cyan""
grid.ApplyStyle()
grid.Draw()

// Access points
let firstPoint = grid.[0]
let cell = grid.[2, 1]  // Column 2, Row 1

// Get rows/columns
let bottomRow = grid.GetRow(0)
let thirdCol = grid.GetColumn(2)

// Transform
grid.Move(VXYZ(50.0, 25.0, 0.0))
grid.Rotate(VPoint(0.0, 0.0), 45.0)" },

                // Support classes
                { "VXYZ", "let v = VXYZ(10.0, 20.0, 30.0)\nlet len = v.GetLength()" },
                { "VPlane", "let origin = VXYZ.Zero\nlet normal = VXYZ.BasisZ\nlet plane = VPlane.CreateByNormalAndOrigin(normal, origin)" },
                { "VTransform", "let t = VTransform.CreateRotation(VXYZ.BasisZ, 90.0)" },

                { "CanvasRenderer", "CanvasRenderer.Instance.Clear()\nCanvasRenderer.Instance.AddShape(someShape)" },
                { "VizConsole", "VizConsole.Log(\"Debug info\")\nVizConsole.Log($\"Value: {someVariable}\")" },

                // Animation
                { "Animator", @"// Create shapes
let line = VLine(0.0, 0.0, 100.0, 50.0)
let circle = VCircle(50.0, 50.0, 30.0)

// Create animator
let anim = Animator()
anim.Repeat <- true  // Loop animation

// Add animations sequentially
anim.AddToAnimations(DrawAnimation(line, 2.0))      // 0-2s
anim.AddToAnimations(DrawAnimation(circle, 2.0))   // 2-4s
anim.AddToAnimations(MoveAnimation(circle, VXYZ(50.0, 0.0, 0.0), 2.0)) // 4-6s

// Start playback
anim.Animate()

// For parallel animations, pass a List:
anim.AddToAnimations(ResizeArray([
    FadeInAnimation(line, 1.0) :> Animation
    FadeInAnimation(circle, 1.0) :> Animation
]))" },

                { "DrawAnimation", @"// Animates shape drawing from 0% to 100%
let line = VLine(0.0, 0.0, 100.0, 0.0)
let anim = Animator()

// Draw the line over 2 seconds
anim.AddToAnimations(DrawAnimation(line, 2.0))
anim.Animate()" },

                { "MoveAnimation", @"// Animates moving a shape by a vector
let circle = VCircle(0.0, 0.0, 30.0)
let anim = Animator()

// Move circle by (100, 50) over 3 seconds
anim.AddToAnimations(MoveAnimation(circle, VXYZ(100.0, 50.0, 0.0), 3.0))
anim.Animate()" },

                { "RotateAnimation", @"// Animates rotating a shape around a pivot
let rect = VRectangle(0.0, 0.0, 50.0, 30.0)
let pivot = VPoint(25.0, 15.0)
let anim = Animator()

// Rotate 360 degrees over 4 seconds
anim.AddToAnimations(RotateAnimation(rect, pivot, 360.0, 4.0))
anim.Animate()" },

                { "FlipAnimation", @"// Animates flipping a shape across a mirror axis
let triangle = VPolygon(VPoint(0.0,0.0), VPoint(50.0,0.0), VPoint(25.0,50.0))
let mirrorAxis = VLine(25.0, -10.0, 25.0, 60.0)
let anim = Animator()

// Flip across the axis over 2 seconds
anim.AddToAnimations(FlipAnimation(triangle, mirrorAxis, 2.0))
anim.Animate()" },

                { "FadeInAnimation", @"// Animates fading in a shape
let circle = VCircle(0.0, 0.0, 50.0)
let anim = Animator()

// Fade in over 2 seconds
anim.AddToAnimations(FadeInAnimation(circle, 2.0))
anim.Animate()" },

                { "FadeOutAnimation", @"// Animates fading out a shape
let circle = VCircle(0.0, 0.0, 50.0)
let anim = Animator()

// Fade out over 2 seconds
anim.AddToAnimations(FadeOutAnimation(circle, 2.0))
anim.Animate()" },

                { "EasingFunctions", @"// Apply easing to any animation for smooth motion
let circle = VCircle(0.0, 0.0, 30.0)
let anim = Animator()

let moveAnim = MoveAnimation(circle, VXYZ(200.0, 0.0, 0.0), 3.0)

// Available Easing Functions:
// ┌─────────────────┬───────────┬──────────────────────────┐
// │ Function        │ Formula   │ Effect                   │
// ├─────────────────┼───────────┼──────────────────────────┤
// │ Linear          │ t         │ Constant speed           │
// │ EaseInQuad      │ t²        │ Slow start, accelerates  │
// │ EaseOutQuad     │ t(2-t)    │ Fast start, decelerates  │
// │ EaseInOutQuad   │ Piecewise │ Slow start & end         │
// │ EaseInCubic     │ t³        │ Slower start             │
// │ EaseOutCubic    │ (t-1)³+1  │ Slower end               │
// │ EaseInOutCubic  │ Piecewise │ Smooth start & end       │
// └─────────────────┴───────────┴──────────────────────────┘

// Set the easing function
moveAnim.EasingFunction <- EasingFunctions.EaseInOutCubic

anim.AddToAnimations(moveAnim)
anim.Animate()" }
            };
        }

        public FlowDocument GenerateFSharpDocForType(Type type)
        {
            var doc = new FlowDocument();
            doc.FontFamily = new FontFamily("Segoe UI");
            doc.PagePadding = new Thickness(20);
            doc.ColumnWidth = double.NaN;

            var title = new Paragraph(new Run(type.Name + " (F#)"))
            {
                FontSize = 24, FontWeight = FontWeights.Bold, Foreground = Brushes.DarkSlateGray, Margin = new Thickness(0, 0, 0, 10)
            };
            doc.Blocks.Add(title);

            var summaryText = GetSummary(type.Name);
            if (summaryText == "No description available." && type.Name.StartsWith("V"))
            {
                 var altName = type.Name.Substring(1) + "2D";
                 summaryText = GetSummary(altName);
                 if (summaryText == "No description available.") summaryText = GetSummary(type.Name.Substring(1));
            }
            doc.Blocks.Add(new Paragraph(new Run(summaryText)) { FontSize = 14, Margin = new Thickness(0, 0, 0, 20) });

            AddSectionHeader(doc, "F# Sample Code");
            if (_fsharpSamples == null) InitializeFSharpSamples();
            if (_fsharpSamples.TryGetValue(type.Name, out var sample))
            {
                 var p = new Paragraph(new Run(sample));
                 p.FontFamily = new FontFamily("Consolas");
                 p.Background = Brushes.WhiteSmoke;
                 p.Padding = new Thickness(10);
                 doc.Blocks.Add(p);
            }
            else
            {
                 doc.Blocks.Add(new Paragraph(new Run("// No specific usage example available.")) { FontFamily = new FontFamily("Consolas"), Padding = new Thickness(5) });
            }

            var dtors = type.GetConstructors();
            if (dtors.Length > 0)
            {
                AddSectionHeader(doc, "Constructors");
                doc.Blocks.Add(GenerateFSharpMemberTable(dtors));
            }

            var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            if (props.Length > 0)
            {
                AddSectionHeader(doc, "Properties");
                doc.Blocks.Add(GenerateFSharpMemberTable(props));
            }

            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName && m.DeclaringType != typeof(object))
                .ToArray();

            if (methods.Length > 0)
            {
                AddSectionHeader(doc, "Methods");
                doc.Blocks.Add(GenerateFSharpMemberTable(methods));
            }

            return doc;
        }

        private Table GenerateFSharpMemberTable(MemberInfo[] members)
        {
            var table = new Table();
            table.CellSpacing = 0;
            table.BorderBrush = Brushes.LightGray;
            table.BorderThickness = new Thickness(1);
            table.Columns.Add(new TableColumn { Width = new GridLength(200) });
            table.Columns.Add(new TableColumn { Width = new GridLength(520) });

            var rowGroup = new TableRowGroup();
            var headerRow = new TableRow();
            headerRow.Background = Brushes.AliceBlue;
            headerRow.Cells.Add(CreateHeaderCell("Name"));
            headerRow.Cells.Add(CreateHeaderCell("Usage / Signature"));
            rowGroup.Rows.Add(headerRow);

            bool isAlt = false;
            foreach (var member in members)
            {
                var row = new TableRow();
                if (isAlt) row.Background = Brushes.WhiteSmoke;
                isAlt = !isAlt;

                var nameText = new Run(member.Name) { FontWeight = FontWeights.Bold, Foreground = Brushes.Teal };
                var nameCell = new TableCell(new Paragraph(nameText)) { Padding = new Thickness(5), BorderBrush = Brushes.LightGray, BorderThickness = new Thickness(0,0,0,1) };
                row.Cells.Add(nameCell);

                string sig = "";
                if (member is MethodInfo mi)
                {
                    var paramStr = string.Join(", ", mi.GetParameters().Select(p => $"{p.Name}: {p.ParameterType.Name}"));
                    if (string.IsNullOrEmpty(paramStr)) paramStr = "()";
                    else paramStr = $"({paramStr})";
                    sig = $"obj.{mi.Name}{paramStr} -> {mi.ReturnType.Name}";
                }
                else if (member is PropertyInfo pi)
                {
                    if (pi.CanWrite) sig = $"obj.{pi.Name} <- value ({pi.PropertyType.Name})";
                    else sig = $"obj.{pi.Name} : {pi.PropertyType.Name}";
                }
                else if (member is ConstructorInfo ci)
                {
                     var paramStr = string.Join(", ", ci.GetParameters().Select(p => $"{p.Name}"));
                     sig = $"{member.DeclaringType.Name}({paramStr})";
                }

                var sigPara = new Paragraph(new Run(sig));
                sigPara.FontFamily = new FontFamily("Consolas");
                sigPara.FontSize = 12;
                var sigCell = new TableCell(sigPara) { Padding = new Thickness(5), BorderBrush = Brushes.LightGray, BorderThickness = new Thickness(0,0,0,1) };
                row.Cells.Add(sigCell);
                rowGroup.Rows.Add(row);
            }
            table.RowGroups.Add(rowGroup);
            return table;
        }
        private void InitializeCSharpSamples()
        {
            _csharpSamples = new Dictionary<string, string>
            {
                // Basic shapes
                { "VPoint", @"// Create a point
var p = new VPoint(100, 200);
p.Color = ""Red"";
p.Draw();" },

                { "VLine", @"// Create a line from two points
var line = new VLine(new VPoint(0, 0), new VPoint(100, 50));
line.Color = ""Cyan"";
line.LineWeight = 2;
line.Draw();

// Or using coordinates directly
var line2 = new VLine(0, 100, 150, 100);
line2.Draw();" },

                { "VXLine", @"// Create an infinite construction line through a point with direction
var xline = new VXLine(new VPoint(0, 0), new VXYZ(1, 1, 0));
xline.Color = ""Gray"";
xline.Draw();

// Create from two points (line passes through both, extends infinitely)
var xline2 = new VXLine(new VPoint(0, 50), new VPoint(100, 50));
xline2.Draw();

// Static helpers for horizontal and vertical lines
var hLine = VXLine.Horizontal(100);  // Horizontal at Y=100
var vLine = VXLine.Vertical(50);     // Vertical at X=50
hLine.Draw();
vLine.Draw();

// Use for slicing polygons
var polygon = new VPolygon(new VPoint(0,0), new VPoint(100,0), new VPoint(100,100), new VPoint(0,100));
var sliced = polygon.Slice(xline);
foreach (var p in sliced) p.Draw();" },

                { "VRay", @"// Create a ray from origin in a direction
var ray = new VRay(new VPoint(0, 0), new VXYZ(1, 0.5, 0));
ray.Color = ""Orange"";
ray.Draw();

// Create from origin through a point
var ray2 = new VRay(new VPoint(50, 50), new VPoint(100, 75));
ray2.Draw();

// Static helpers for common rays
var rightRay = VRay.HorizontalRight(new VPoint(0, 0));
var upRay = VRay.VerticalUp(new VPoint(100, 0));
var angledRay = VRay.AtAngle(new VPoint(0, 0), 45);  // 45 degrees from origin
rightRay.Draw();
upRay.Draw();
angledRay.Draw();

// Use for slicing polygons
var polygon = new VPolygon(new VPoint(0,0), new VPoint(100,0), new VPoint(100,100), new VPoint(0,100));
var sliced = polygon.Slice(ray);
foreach (var p in sliced) p.Draw();" },

                { "VCircle", @"// Create a circle with center and radius
var circle = new VCircle(new VPoint(50, 50), 30);
circle.Color = ""Yellow"";
circle.FillColor = ""#4000FFFF""; // Semi-transparent cyan
circle.Draw();

// Or using coordinates
var circle2 = new VCircle(100, 100, 25);
circle2.Draw();

// Create a circumcircle through 3 points
var p1 = new VPoint(0, 0);
var p2 = new VPoint(100, 0);
var p3 = new VPoint(50, 80);
var circumcircle = new VCircle(p1, p2, p3);
circumcircle.Draw();" },

                { "VRectangle", @"// Create a rectangle (corner, width, height)
var rect = new VRectangle(new VPoint(10, 10), 80, 50);
rect.Color = ""LimeGreen"";
rect.FillColor = ""#2000FF00"";
rect.Draw();

// Or using coordinates
var rect2 = new VRectangle(100, 0, 60, 40);
rect2.Draw();

// Create from two corner points (bottom-left and top-right)
var bottomLeft = new VPoint(0, 0);
var topRight = new VPoint(100, 75);
var rect3 = new VRectangle(bottomLeft, topRight);
rect3.Draw();

// VRectangle inherits from VPolygon, so all polygon methods work
double area = rect.Area;  // Signed area from VPolygon" },

                { "VEllipse", @"// Create an ellipse with center and radii
var ellipse = new VEllipse(new VPoint(100, 100), 60, 30);
ellipse.Color = ""Magenta"";
ellipse.LineWeight = 2;
ellipse.Draw();" },

                { "VArc", @"// Create an arc (center, radius, startAngle, endAngle)
var arc = new VArc(new VPoint(50, 50), 40, 0, 270);
arc.Color = ""Orange"";
arc.LineWeight = 3;
arc.Draw();

// Angles are in degrees, counter-clockwise from positive X-axis" },

                { "VPolygon", @"// Create a triangle
var triangle = new VPolygon(
    new VPoint(0, 0),
    new VPoint(100, 0),
    new VPoint(50, 80)
);
triangle.Color = ""LimeGreen"";
triangle.FillColor = ""#4000FF00"";
triangle.Draw();

// Create from array
var points = new[] { new VPoint(0,0), new VPoint(50,0), new VPoint(50,50), new VPoint(0,50) };
var square = new VPolygon(points);
square.Draw();" },

                { "VPolyline", @"// Create an open polyline (not closed)
var polyline = new VPolyline(
    new VPoint(0, 0),
    new VPoint(30, 50),
    new VPoint(60, 20),
    new VPoint(100, 60)
);
polyline.Color = ""Cyan"";
polyline.Draw();" },

                { "VBezier", @"// Create a cubic Bezier curve (4 control points)
var bezier = new VBezier(
    new VPoint(0, 0),      // Start point
    new VPoint(30, 80),    // Control point 1
    new VPoint(70, 80),    // Control point 2
    new VPoint(100, 0)     // End point
);
bezier.Color = ""Magenta"";
bezier.LineWeight = 2;
bezier.Draw();" },

                { "VSpline", @"// Create a smooth spline through points
var spline = new VSpline(
    new VPoint(0, 0),
    new VPoint(30, 40),
    new VPoint(60, 20),
    new VPoint(100, 50)
);
spline.Color = ""Cyan"";
spline.Draw();" },

                { "VText", @"// Create text at a position
var text = new VText(new VPoint(50, 50), ""Hello World"");
text.Height = 24;
text.Color = ""White"";
text.Draw();

// Create text with height in constructor
var text2 = new VText(0, -50, ""Compact syntax"", 18);
text2.Color = ""Cyan"";
text2.Draw();" },

                { "VArrow", @"// Create an arrow from two points
var arrow = new VArrow(new VPoint(0, 0), new VPoint(100, 0));
arrow.Color = ""Orange"";
arrow.HeadLength = 15;
arrow.HeadAngle = 30;
arrow.Draw();

// Create from point, direction, and length
var arrow2 = new VArrow(new VPoint(0, 50), VXYZ.BasisX, 80);
arrow2.DoubleEnded = true; // Arrow on both ends
arrow2.Draw();" },

                { "VDimension", @"// Create a dimension line between two points
var dim = new VDimension(new VPoint(0, 0), new VPoint(100, 0));
dim.Offset = 20;          // Distance above the line
dim.DecimalPlaces = 1;    // Show 1 decimal place
dim.TextHeight = 14;
dim.Draw();

// Custom text
var dim2 = new VDimension(0, 50, 80, 50);
dim2.CustomText = ""80 mm"";
dim2.Draw();" },

                { "VGroup", @"// Create a group from shapes
var group = new VGroup(
    new VCircle(0, 0, 20),
    new VLine(-30, 0, 30, 0),
    new VLine(0, -30, 0, 30)
);

// Or create empty and add shapes
var group2 = new VGroup();
group2.Add(new VCircle(50, 50, 15));
group2.AddRange(new[] { new VLine(40, 50, 60, 50) });

// Transform the entire group
group.Move(new VXYZ(100, 100, 0));
group.Rotate(new VPoint(100, 100), 45);
group.Scale(group.GetCenter(), 1.5);

// Apply styling to all shapes
group.Color = ""Cyan"";
group.ApplyStyle();

// Utility methods
var circles = group.GetShapesOfType<VCircle>();
var allShapes = group.Flatten();  // Includes nested groups
group.ForEach(s => s.LineWeight = 2);

// Draw as a single selectable entity
group.Draw();" },

                { "VGrid", @"// Create a centered grid at origin: 5 columns x 3 rows, spacing 10
var grid = new VGrid(new VPoint(0, 0), 5, 3, 10, true);
grid.FillColor = ""Cyan"";
grid.ApplyStyle();
grid.Draw();

// Create grid with bottom-left at (-100, -50), different X/Y spacing
var grid2 = new VGrid(new VPoint(-100, -50), 4, 4, 20, 15, false);
grid2.Draw();

// Access individual points
VPoint firstPoint = grid[0];           // By index
VPoint cell = grid[2, 1];              // By column, row

// Get rows and columns
var bottomRow = grid.GetRow(0);
var thirdColumn = grid.GetColumn(2);

// Transform entire grid
grid.Move(new VXYZ(50, 25, 0));
grid.Rotate(new VPoint(0, 0), 45);
grid.Scale(grid.GetCenter(), 2.0);" },

                // Support classes
                { "VXYZ", @"// Create a 3D vector
var v = new VXYZ(10, 20, 30);
double len = v.GetLength();
var normalized = v.Normalize();

// Vector operations
var v1 = new VXYZ(1, 0, 0);
var v2 = new VXYZ(0, 1, 0);
var cross = v1.CrossProduct(v2);  // (0, 0, 1)
var dot = v1.DotProduct(v2);      // 0

// Static basis vectors
var x = VXYZ.BasisX;  // (1, 0, 0)
var y = VXYZ.BasisY;  // (0, 1, 0)
var z = VXYZ.BasisZ;  // (0, 0, 1)" },

                { "VPlane", @"// Create a plane from normal and origin
var origin = VXYZ.Zero;
var normal = VXYZ.BasisZ;
var plane = VPlane.CreateByNormalAndOrigin(normal, origin);" },

                { "VTransform", @"// Create a rotation transform
var rotation = VTransform.CreateRotation(VXYZ.BasisZ, 90);

// Create a reflection transform
var reflection = VTransform.CreateReflection(plane);" },

                { "ShapeDefaults", @"// Set global defaults for all new shapes
ShapeDefaults.GlobalColor = ""Cyan"";
ShapeDefaults.GlobalFillColor = ""#20FFFFFF"";
ShapeDefaults.GlobalLineWeight = 2.0;
ShapeDefaults.GlobalLineType = LineType.Continuous;

// Now all new shapes use these defaults
var circle = new VCircle(0, 0, 50);  // Uses Cyan stroke
circle.Draw();

// Reset to original defaults
ShapeDefaults.Reset();" },

                { "LineType", @"// LineType controls the line pattern for shape outlines

// Solid line (default)
var line1 = new VLine(0, 0, 100, 0);
line1.LineType = LineType.Continuous;
line1.Draw();

// Dashed line
var line2 = new VLine(0, 20, 100, 20);
line2.LineType = LineType.Dashed;
line2.Draw();

// Dotted line
var line3 = new VLine(0, 40, 100, 40);
line3.LineType = LineType.Dotted;
line3.Draw();

// Dash-dot pattern (commonly used for centerlines)
var line4 = new VLine(0, 60, 100, 60);
line4.LineType = LineType.DashDot;
line4.Draw();

// Hidden line (short dashes for hidden edges)
var rect = new VRectangle(0, 100, 80, 50);
rect.LineType = LineType.Hidden;
rect.Draw();" },

                { "Shape", @"// Shape is the base class for all drawable shapes
// Common properties available on all shapes:

shape.Color = ""Cyan"";           // Outline color
shape.FillColor = ""Transparent"";      // Fill color
shape.LineWeight = 2.0;           // Line thickness
shape.LineType = LineType.Continuous;  // Line pattern (Continuous, Dashed, Dotted, etc.)

// Animation properties
shape.DrawFactor = 1.0;    // 0-1, for progressive drawing
shape.OffsetX = 0;         // Translation offset
shape.OffsetY = 0;
shape.RotationAngle = 0;   // Degrees
shape.RotationPivot = null; // Pivot point for rotation

// Common methods
shape.Draw();              // Render to canvas
var copy = shape.Clone();  // Create a copy
shape.Move(new VXYZ(10, 20, 0));
shape.Rotate(pivot, 45);
shape.Scale(center, 2.0);
var bounds = shape.GetBounds();
bool inside = shape.Contains(point);
double dist = shape.DistanceTo(point);" },

                { "GeometryHelper", @"// Static methods for geometric calculations
double dist = GeometryHelper.DistancePointToLine(point, line);
VPoint? intersection = GeometryHelper.LineLineIntersection(line1, line2);
double angle = GeometryHelper.AngleBetweenVectors(v1, v2);" },

                { "ICurve", @"// ICurve interface - all curve shapes implement this
ICurve curve = new VLine(0, 0, 100, 100);
curve.Draw();  // ICurve extends IDrawable

// Check for self-intersection
bool selfIntersects = curve.SelfIntersecting;
VizConsole.Log($""Self-intersecting: {selfIntersects}"");

// Intersect with another curve
var line2 = new VLine(0, 100, 100, 0);
IntersectionResult result = curve.Intersect(line2);
if (result.HasIntersection)
{
    foreach (var pt in result.Points)
        new VPoint(pt.X, pt.Y).Draw();
}" },

                { "IntersectionResult", @"// IntersectionResult holds intersection data
var line1 = new VLine(0, 0, 100, 100);
var line2 = new VLine(0, 100, 100, 0);
var circle = new VCircle(50, 50, 30);

// Line-Line intersection
var result = line1.Intersect(line2);
if (result.IsSinglePoint)
    VizConsole.Log($""Cross at: {result.Points[0]}"");

// Line-Circle may have multiple points
var circleResult = line1.Intersect(circle);
VizConsole.Log($""Found {circleResult.Points.Count} intersections"");

// Check for overlapping segments (collinear lines)
if (result.HasOverlap)
    foreach (var c in result.Curves) c.Draw();" },

                { "CurveIntersection", @"// Static utility for curve intersections
var line = new VLine(0, 0, 100, 100);
var circle = new VCircle(50, 50, 40);

// Use directly via static methods
var result = CurveIntersection.Intersect(line, circle);
foreach (var pt in result.Points)
    new VPoint(pt.X, pt.Y).Draw();

// Check self-intersection
var polyline = new VPolyline(
    new VPoint(0, 0), new VPoint(100, 0),
    new VPoint(50, 50), new VPoint(50, -50));
bool selfX = CurveIntersection.IsSelfIntersecting(polyline);" },

                // Animation
                { "Animator", @"// Create shapes
var line = new VLine(0, 0, 100, 50);
var circle = new VCircle(50, 50, 30);

// Create animator
var anim = new Animator();
anim.Repeat = true;  // Loop animation

// Add animations sequentially - they auto-sequence
anim.AddToAnimations(new DrawAnimation(line, 2.0));      // 0-2s
anim.AddToAnimations(new DrawAnimation(circle, 2.0));   // 2-4s
anim.AddToAnimations(new MoveAnimation(circle, new VXYZ(50, 0, 0), 2.0)); // 4-6s

// Start playback
anim.Animate();

// For parallel animations, pass a List:
anim.AddToAnimations(new List<Animation> {
    new FadeInAnimation(line, 1.0),
    new FadeInAnimation(circle, 1.0)
});  // Both run simultaneously" },

                { "DrawAnimation", @"// Animates shape drawing from 0% to 100%
var line = new VLine(0, 0, 100, 0);
var anim = new Animator();

// Draw the line over 2 seconds
anim.AddToAnimations(new DrawAnimation(line, 2.0));
anim.Animate();" },

                { "MoveAnimation", @"// Animates moving a shape by a vector
var circle = new VCircle(0, 0, 30);
var anim = new Animator();

// Move circle by (100, 50) over 3 seconds
anim.AddToAnimations(new MoveAnimation(circle, new VXYZ(100, 50, 0), 3.0));
anim.Animate();" },

                { "RotateAnimation", @"// Animates rotating a shape around a pivot
var rect = new VRectangle(0, 0, 50, 30);
var pivot = new VPoint(25, 15); // center of rectangle
var anim = new Animator();

// Rotate 360 degrees over 4 seconds
anim.AddToAnimations(new RotateAnimation(rect, pivot, 360.0, 4.0));
anim.Animate();" },

                { "FlipAnimation", @"// Animates flipping a shape across a mirror axis
var triangle = new VPolygon(new VPoint(0,0), new VPoint(50,0), new VPoint(25,50));
var mirrorAxis = new VLine(25, -10, 25, 60); // vertical line
var anim = new Animator();

// Flip across the axis over 2 seconds
anim.AddToAnimations(new FlipAnimation(triangle, mirrorAxis, 2.0));
anim.Animate();" },

                { "FadeInAnimation", @"// Animates fading in a shape from transparent to opaque
var circle = new VCircle(0, 0, 50);
var anim = new Animator();

// Fade in over 2 seconds
anim.AddToAnimations(new FadeInAnimation(circle, 2.0));
anim.Animate();" },

                { "FadeOutAnimation", @"// Animates fading out a shape from opaque to transparent
var circle = new VCircle(0, 0, 50);
var anim = new Animator();

// Fade out over 2 seconds (to fully transparent)
anim.AddToAnimations(new FadeOutAnimation(circle, 2.0));

// Or fade to partial transparency
anim.AddToAnimations(new FadeOutAnimation(circle, 2.0, 0.3));  // Fade to 30% opacity
anim.Animate();" },

                { "EasingFunctions", @"// Apply easing to any animation for smooth motion
var circle = new VCircle(0, 0, 30);
var anim = new Animator();

var moveAnim = new MoveAnimation(circle, new VXYZ(200, 0, 0), 3.0);

// Available Easing Functions:
// ┌─────────────────┬───────────┬──────────────────────────┐
// │ Function        │ Formula   │ Effect                   │
// ├─────────────────┼───────────┼──────────────────────────┤
// │ Linear          │ t         │ Constant speed           │
// │ EaseInQuad      │ t²        │ Slow start, accelerates  │
// │ EaseOutQuad     │ t(2-t)    │ Fast start, decelerates  │
// │ EaseInOutQuad   │ Piecewise │ Slow start & end         │
// │ EaseInCubic     │ t³        │ Slower start             │
// │ EaseOutCubic    │ (t-1)³+1  │ Slower end               │
// │ EaseInOutCubic  │ Piecewise │ Smooth start & end       │
// └─────────────────┴───────────┴──────────────────────────┘

// Set the easing function
moveAnim.EasingFunction = EasingFunctions.EaseInOutCubic;

anim.AddToAnimations(moveAnim);
anim.Animate();" },

                // Boolean Operations
                { "BooleanOps", @"// Boolean operations on polygons using Clipper2

var poly1 = new VPolygon(
    new VPoint(0, 0), new VPoint(100, 0),
    new VPoint(100, 100), new VPoint(0, 100));
var poly2 = new VPolygon(
    new VPoint(50, 50), new VPoint(150, 50),
    new VPoint(150, 150), new VPoint(50, 150));

// Union - combine polygons
var union = poly1.Union(poly2);
foreach (var p in union) { p.Color = ""Cyan""; p.Draw(); }

// Intersection - overlapping area
var intersection = poly1.Intersect(poly2);

// Difference - subtract poly2 from poly1
var difference = poly1.Difference(poly2);

// XOR - symmetric difference (non-overlapping areas)
var xor = poly1.Xor(poly2);

// Utility methods
bool inside = poly1.Contains(new VPoint(50, 50));
double area = poly1.GetArea();

// Offset polygon (positive = outward, negative = inward)
var offsetPolygons = BooleanOps.OffsetPolygon(poly1, 10);

// Simplify polygon (remove redundant points)
var simplified = BooleanOps.Simplify(poly1, tolerance: 0.1);" },

                // Array Operations
                { "ArrayOps", @"// Create arrays and patterns of shapes

var circle = new VCircle(0, 0, 20);

// Linear array along X axis: 5 copies, 50 units apart
circle.LinearArrayX(5, 50).DrawAll();

// Linear array along Y axis: 4 copies, 40 units apart
circle.LinearArrayY(4, 40).DrawAll();

// Linear array along custom direction
circle.LinearArray(new VXYZ(1, 1, 0), 6, 30).DrawAll();

// Rectangular grid: 3 rows, 4 columns
var rect = new VRectangle(0, 0, 30, 20);
rect.RectangularArray(rows: 3, cols: 4, rowSpacing: 40, colSpacing: 50).DrawAll();

// Circular array around a center point
var shape = new VCircle(50, 0, 10);
var center = new VPoint(0, 0);
shape.CircularArray(center, count: 8).DrawAll();  // Full circle
shape.CircularArray(center, count: 6, totalAngleDegrees: 180).DrawAll();  // Half circle

// Path array - distribute along a curve
var marker = new VCircle(0, 0, 5);
var path = new VSpline(new VPoint(0,0), new VPoint(50,100), new VPoint(100,0));
marker.PathArray(path, count: 10, alignToPath: true).DrawAll();

// Spiral array
var dot = new VCircle(0, 0, 3);
dot.SpiralArray(center, count: 30, startRadius: 20, endRadius: 100, totalRevolutions: 2).DrawAll();

// Mirror across an axis
var triangle = new VPolygon(new VPoint(0,0), new VPoint(50,0), new VPoint(25,40));
var mirrorAxis = new VLine(0, -50, 0, 50);
triangle.Mirror(mirrorAxis).DrawAll();" }
            };
        }

        private void InitializeMemberDescriptions()
        {
            _memberDescriptions = new Dictionary<string, string>
            {
                // VGrid Properties
                { "VGrid.Points", "Gets the collection of all VPoint objects in the grid. Points are stored in row-major order (left to right, bottom to top)." },
                { "VGrid.Location", "Gets the reference location point. If Centered is true, this is the center of the grid. If false, this is the bottom-left corner." },
                { "VGrid.XCount", "Gets the number of points along the X (horizontal) axis." },
                { "VGrid.YCount", "Gets the number of points along the Y (vertical) axis." },
                { "VGrid.XSpacing", "Gets the spacing distance between adjacent points along the X axis." },
                { "VGrid.YSpacing", "Gets the spacing distance between adjacent points along the Y axis." },
                { "VGrid.Centered", "Gets whether the grid is centered at the Location point. If true, grid is centered; if false, Location is the bottom-left corner." },
                { "VGrid.Count", "Gets the total number of points in the grid (XCount × YCount)." },
                { "VGrid.Item", "Gets a point by index (single parameter) or by column and row indices (two parameters). Indexer: grid[index] or grid[col, row]." },

                // VGrid Methods
                { "VGrid.Draw", "Draws all points in the grid to the canvas. Each point is rendered using its individual style properties." },
                { "VGrid.Clone", "Creates a deep copy of this grid with all points cloned. Returns a new VGrid instance with the same properties and point positions." },
                { "VGrid.Move", "Translates all points in the grid by the specified displacement vector. Also updates the Location property." },
                { "VGrid.Rotate", "Rotates all points in the grid around a specified pivot point by the given angle in degrees (counter-clockwise)." },
                { "VGrid.Flip", "Mirrors all points in the grid across the specified line (mirror axis). Creates a reflection of the grid." },
                { "VGrid.Scale", "Scales all points in the grid relative to a center point by the specified factor. Factor > 1 enlarges, < 1 shrinks." },
                { "VGrid.GetBounds", "Returns the axis-aligned bounding box of all points as a tuple (minPoint, maxPoint)." },
                { "VGrid.DistanceTo", "Returns the minimum distance from any point in the grid to the specified point." },
                { "VGrid.ApplyStyle", "Applies the grid's Color, FillColor, and LineWeight to all contained points." },
                { "VGrid.GetRow", "Returns a list of all points in the specified row (0-based index, row 0 is the bottom row)." },
                { "VGrid.GetColumn", "Returns a list of all points in the specified column (0-based index, column 0 is the leftmost)." },
                { "VGrid.GetCenter", "Calculates and returns the geometric center point of the grid based on its bounding box." },
                { "VGrid.ToString", "Returns a string representation of the grid: \"VGrid(XCount×YCount, Location=..., Centered=...)\"" },

                // VGroup Properties
                { "VGroup.Shapes", "Gets the list of Shape objects contained in this group. Shapes can be added, removed, or modified directly." },
                { "VGroup.Count", "Gets the number of shapes currently in the group." },
                { "VGroup.Item", "Gets a shape at the specified index. Indexer: group[index]." },

                // VGroup Methods
                { "VGroup.Add", "Adds a shape to the group and returns the group for method chaining." },
                { "VGroup.AddRange", "Adds multiple shapes to the group and returns the group for method chaining." },
                { "VGroup.Remove", "Removes the specified shape from the group. Returns true if successful." },
                { "VGroup.RemoveAt", "Removes the shape at the specified index from the group." },
                { "VGroup.Clear", "Removes all shapes from the group." },
                { "VGroup.ContainsShape", "Returns true if the specified shape is in the group." },
                { "VGroup.Flatten", "Returns a flat list of all shapes, expanding any nested groups recursively." },
                { "VGroup.ForEach", "Executes the specified action on each shape in the group." },
                { "VGroup.Where", "Returns a new VGroup containing only shapes that match the predicate." },
                { "VGroup.GetShapesOfType", "Returns all shapes of the specified type T from the group." },
                { "VGroup.ApplyStyle", "Applies the group's Color, FillColor, and LineWeight to all contained shapes." },
                { "VGroup.ApplyColor", "Applies only the group's Color to all contained shapes." },
                { "VGroup.ApplyFillColor", "Applies only the group's FillColor to all contained shapes." },
                { "VGroup.ApplyLineWeight", "Applies only the group's LineWeight to all contained shapes." },
                { "VGroup.SetOpacity", "Sets the opacity (0.0 to 1.0) for all shapes in the group by adjusting their fill color alpha." },
                { "VGroup.GetCenter", "Calculates and returns the geometric center point of all shapes in the group." },

                // VPoint Properties
                { "VPoint.X", "Gets or sets the X coordinate of the point in world units." },
                { "VPoint.Y", "Gets or sets the Y coordinate of the point in world units." },

                // VPoint Methods
                { "VPoint.AsVXYZ", "Converts this VPoint to a VXYZ vector with Z=0." },
                { "VPoint.Add", "Returns a new VPoint that is the sum of this point and the other point or vector." },
                { "VPoint.Draw", "Renders the point to the canvas as a small dot." },
                { "VPoint.Clone", "Creates a deep copy of this point with all properties duplicated." },
                { "VPoint.Move", "Translates the point by the specified displacement vector." },
                { "VPoint.Rotate", "Rotates the point around the specified pivot by the given angle in degrees." },
                { "VPoint.Flip", "Mirrors the point across the specified line (axis of reflection)." },
                { "VPoint.Scale", "Scales the point position relative to a center point by the specified factor." },
                { "VPoint.GetBounds", "Returns the bounding box (point itself for both min and max)." },
                { "VPoint.DistanceTo", "Returns the Euclidean distance from this point to another point." },
                { "VPoint.Intersect", "Returns a copy of this point if it lies inside the other shape, otherwise null." },
                { "VPoint.ToString", "Returns a string representation: \"VPoint(X, Y)\"." },

                // VLine Properties
                { "VLine.StartPoint", "Gets or sets the starting point of the line segment." },
                { "VLine.EndPoint", "Gets or sets the ending point of the line segment." },
                { "VLine.Length", "Gets the length of the line segment." },
                { "VLine.MidPoint", "Gets the midpoint of the line segment." },
                { "VLine.SelfIntersecting", "Always returns false (lines cannot self-intersect)." },

                // VLine Methods
                { "VLine.Draw", "Renders the line segment to the canvas." },
                { "VLine.Clone", "Creates a deep copy of this line with all properties duplicated." },
                { "VLine.Move", "Translates the line by the specified displacement vector." },
                { "VLine.Rotate", "Rotates the line around the specified pivot by the given angle in degrees." },
                { "VLine.Flip", "Mirrors the line across the specified axis line." },
                { "VLine.Scale", "Scales the line relative to a center point by the specified factor." },
                { "VLine.GetBounds", "Returns the axis-aligned bounding box of the line segment." },
                { "VLine.Contains", "Returns true if the specified point lies on the line segment." },
                { "VLine.DistanceTo", "Returns the minimum distance from the line to the specified point." },
                { "VLine.GetLength", "Returns the length of the line segment." },
                { "VLine.Divide", "Divides the line into equal segments, returning the division points." },
                { "VLine.Measure", "Returns points along the line at fixed distance intervals." },
                { "VLine.Project", "Projects a point onto the line, returning the closest point on the line." },
                { "VLine.PointAtSegmentLength", "Returns the point at the specified distance from the start." },
                { "VLine.PointAtParameter", "Returns a point on the line at the given normalized parameter (0 to 1)." },
                { "VLine.Offset", "Creates a parallel line offset by the specified distance." },
                { "VLine.SplitAtPoint", "Splits the line at the specified point, returning two line segments." },
                { "VLine.NormalAtPoint", "Returns the normal vector (perpendicular) to the line." },
                { "VLine.Intersect", "Computes intersection with another curve." },
                { "VLine.PointsAtChordLengthFromPoint", "Returns points on the line at a chord distance from a given point." },
                { "VLine.ToString", "Returns a string representation of the line." },

                // VCircle Properties
                { "VCircle.Center", "Gets or sets the center point of the circle." },
                { "VCircle.Radius", "Gets or sets the radius of the circle." },
                { "VCircle.Diameter", "Gets the diameter of the circle (2 × Radius)." },
                { "VCircle.Circumference", "Gets the circumference of the circle (2π × Radius)." },
                { "VCircle.Area", "Gets the area of the circle (π × Radius²)." },
                { "VCircle.SelfIntersecting", "Always returns false (circles cannot self-intersect)." },
                { "VCircle.StartPoint", "Gets a point on the circle (at 0 degrees)." },
                { "VCircle.EndPoint", "Gets a point on the circle (same as StartPoint for closed curves)." },

                // VCircle Methods
                { "VCircle.Draw", "Renders the circle to the canvas." },
                { "VCircle.Clone", "Creates a deep copy of this circle with all properties duplicated." },
                { "VCircle.Move", "Translates the circle by the specified displacement vector." },
                { "VCircle.Rotate", "Rotates the circle around the specified pivot by the given angle in degrees." },
                { "VCircle.Flip", "Mirrors the circle across the specified axis line." },
                { "VCircle.Scale", "Scales the circle relative to a center point by the specified factor." },
                { "VCircle.GetBounds", "Returns the axis-aligned bounding box of the circle." },
                { "VCircle.Contains", "Returns true if the specified point is inside or on the circle." },
                { "VCircle.DistanceTo", "Returns the minimum distance from the circle to the specified point." },
                { "VCircle.GetLength", "Returns the circumference of the circle." },
                { "VCircle.Divide", "Divides the circle into equal arc segments, returning the division points." },
                { "VCircle.Measure", "Returns points along the circle at fixed arc length intervals." },
                { "VCircle.Project", "Projects a point onto the circle, returning the closest point on the circle." },
                { "VCircle.PointAtParameter", "Returns a point on the circle at the given normalized parameter (0 to 1), where 0 and 1 are at angle 0 (3 o'clock)." },
                { "VCircle.Offset", "Creates a concentric circle offset by the specified distance (+ = outward)." },
                { "VCircle.NormalAtPoint", "Returns the normal vector at the specified point on the circle (points outward)." },
                { "VCircle.Intersect", "Computes intersection with another curve." },
                { "VCircle.ToString", "Returns a string representation of the circle." },

                // VXLine Properties (infinite construction line)
                { "VXLine.BasePoint", "Gets or sets the base point that the infinite line passes through." },
                { "VXLine.Direction", "Gets or sets the direction vector of the line (normalized)." },
                { "VXLine.RenderExtent", "Gets or sets the extent used for rendering (default: 10000). Points at ±RenderExtent define the visual segment." },
                { "VXLine.StartPoint", "Gets a point far in the negative direction (for rendering)." },
                { "VXLine.EndPoint", "Gets a point far in the positive direction (for rendering)." },
                { "VXLine.SelfIntersecting", "Always returns false (infinite lines cannot self-intersect)." },
                { "VXLine.Vertices", "Gets the base point as the only vertex." },

                // VXLine Constructors
                { "VXLine(VPoint, VXYZ)", "Creates an infinite line through a base point in the specified direction." },
                { "VXLine(VPoint, VPoint)", "Creates an infinite line passing through two points." },
                { "VXLine(double, double, double, double)", "Creates an infinite line through two points specified by coordinates." },

                // VXLine Static Methods
                { "VXLine.Horizontal", "Creates a horizontal infinite line at the specified Y coordinate." },
                { "VXLine.Vertical", "Creates a vertical infinite line at the specified X coordinate." },

                // VXLine Methods
                { "VXLine.Draw", "Renders the infinite line to the canvas (clipped to render extent)." },
                { "VXLine.Clone", "Creates a deep copy of this infinite line." },
                { "VXLine.Move", "Translates the line by moving the base point." },
                { "VXLine.Rotate", "Rotates the line around the specified pivot by the given angle in degrees." },
                { "VXLine.Flip", "Mirrors the line across the specified axis line." },
                { "VXLine.Scale", "Scales the line by moving the base point relative to a center." },
                { "VXLine.GetBounds", "Returns bounds based on render extent." },
                { "VXLine.GetLength", "Returns positive infinity (infinite line)." },
                { "VXLine.Project", "Projects a point onto the infinite line." },
                { "VXLine.GetPointAtParameter", "Gets a point on the line at the specified parameter (0 = BasePoint)." },
                { "VXLine.PointAtParameter", "Returns a point at normalized parameter (0 to 1 maps to -RenderExtent to +RenderExtent)." },
                { "VXLine.GetTwoPoints", "Gets two distinct points on the line for algorithms requiring two points." },
                { "VXLine.ToFiniteLine", "Converts to a finite VLine segment for intersection calculations." },
                { "VXLine.SplitAtPoint", "Splits the line at a point, returning two rays going in opposite directions." },
                { "VXLine.Intersect", "Computes intersection with another curve." },
                { "VXLine.ToString", "Returns a string representation of the infinite line." },

                // VRay Properties (semi-infinite ray)
                { "VRay.Origin", "Gets or sets the origin point where the ray starts." },
                { "VRay.Direction", "Gets or sets the direction vector of the ray (normalized)." },
                { "VRay.RenderExtent", "Gets or sets the extent used for rendering (default: 10000)." },
                { "VRay.StartPoint", "Gets the origin (same as Origin property)." },
                { "VRay.EndPoint", "Gets a point at RenderExtent distance from origin (for rendering)." },
                { "VRay.SelfIntersecting", "Always returns false (rays cannot self-intersect)." },
                { "VRay.Vertices", "Gets the origin as the only vertex." },

                // VRay Constructors
                { "VRay(VPoint, VXYZ)", "Creates a ray starting at origin in the specified direction." },
                { "VRay(VPoint, VPoint)", "Creates a ray starting at origin passing through a second point." },
                { "VRay(double, double, double, double)", "Creates a ray from coordinates (originX, originY, throughX, throughY)." },

                // VRay Static Methods
                { "VRay.HorizontalRight", "Creates a horizontal ray pointing right from the specified point." },
                { "VRay.HorizontalLeft", "Creates a horizontal ray pointing left from the specified point." },
                { "VRay.VerticalUp", "Creates a vertical ray pointing up from the specified point." },
                { "VRay.VerticalDown", "Creates a vertical ray pointing down from the specified point." },
                { "VRay.AtAngle", "Creates a ray at a specified angle from the origin (degrees, counter-clockwise from +X)." },

                // VRay Methods
                { "VRay.Draw", "Renders the ray to the canvas (from origin to render extent)." },
                { "VRay.Clone", "Creates a deep copy of this ray." },
                { "VRay.Move", "Translates the ray by moving the origin." },
                { "VRay.Rotate", "Rotates the ray around the specified pivot by the given angle in degrees." },
                { "VRay.Flip", "Mirrors the ray across the specified axis line." },
                { "VRay.Scale", "Scales the ray by moving the origin relative to a center." },
                { "VRay.GetBounds", "Returns bounds from origin to render extent." },
                { "VRay.GetLength", "Returns positive infinity (semi-infinite ray)." },
                { "VRay.Project", "Projects a point onto the ray. Returns origin if projection is behind the ray." },
                { "VRay.GetPointAtDistance", "Gets a point on the ray at the specified distance from origin." },
                { "VRay.PointAtParameter", "Returns a point at normalized parameter (0 = origin, 1 = RenderExtent)." },
                { "VRay.ContainsPoint", "Checks if a point is on the ray (within tolerance)." },
                { "VRay.ToFiniteLine", "Converts to a finite VLine segment for intersection calculations." },
                { "VRay.ToXLine", "Converts to an infinite VXLine." },
                { "VRay.SplitAtPoint", "Splits the ray at a point, returning a line segment and a continuing ray." },
                { "VRay.Intersect", "Computes intersection with another curve." },
                { "VRay.ToString", "Returns a string representation of the ray." },

                // VRectangle Properties (inherits from VPolygon)
                { "VRectangle.Corner", "Gets or sets the bottom-left corner point of the rectangle. Setting this updates the underlying polygon points." },
                { "VRectangle.Width", "Gets or sets the width of the rectangle (along X axis). Setting this updates the underlying polygon points." },
                { "VRectangle.Height", "Gets or sets the height of the rectangle (along Y axis). Setting this updates the underlying polygon points." },
                { "VRectangle.RotationAngle", "Gets or sets the rotation angle in degrees (counter-clockwise) for the rectangle's intrinsic orientation." },
                { "VRectangle.Area", "Inherited from VPolygon. Gets the signed area using the shoelace formula. Positive for counter-clockwise vertices." },
                { "VRectangle.Points", "Inherited from VPolygon. Gets the four corner vertices as a list of VPoints." },

                // VRectangle Constructors
                { "VRectangle(VPoint, double, double)", "Creates a rectangle from a corner point, width, and height." },
                { "VRectangle(double, double, double, double)", "Creates a rectangle from x, y coordinates, width, and height." },
                { "VRectangle(VPoint, VPoint)", "Creates a rectangle from two corner points (bottom-left and top-right)." },

                // VRectangle Methods
                { "VRectangle.Draw", "Renders the rectangle to the canvas." },
                { "VRectangle.Clone", "Creates a deep copy of this rectangle with all properties duplicated." },
                { "VRectangle.Move", "Translates the rectangle by the specified displacement vector." },
                { "VRectangle.Rotate", "Rotates the rectangle around the specified pivot by the given angle in degrees. Also accumulates the RotationAngle." },
                { "VRectangle.Flip", "Mirrors the rectangle across the specified axis line." },
                { "VRectangle.Scale", "Scales the rectangle relative to a center point by the specified factor." },
                { "VRectangle.GetBounds", "Returns the axis-aligned bounding box of the rectangle." },
                { "VRectangle.Contains", "Returns true if the specified point is inside or on the rectangle. Uses simple bounds check for axis-aligned, polygon containment for rotated." },
                { "VRectangle.DistanceTo", "Returns the minimum distance from the rectangle to the specified point." },
                { "VRectangle.PointAtParameter", "Returns a point on the rectangle perimeter at the given normalized parameter (0 to 1)." },
                { "VRectangle.Slice", "Inherited from VPolygon. Slices the rectangle along an infinite line defined by two points." },
                { "VRectangle.ToString", "Returns a string representation of the rectangle." },

                // VArc Properties
                { "VArc.Center", "Gets or sets the center point of the arc." },
                { "VArc.Radius", "Gets or sets the radius of the arc." },
                { "VArc.StartAngle", "Gets or sets the start angle in degrees (0 = positive X axis)." },
                { "VArc.EndAngle", "Gets or sets the end angle in degrees (counter-clockwise from start)." },
                { "VArc.StartPoint", "Gets the starting point of the arc." },
                { "VArc.EndPoint", "Gets the ending point of the arc." },
                { "VArc.SelfIntersecting", "Always returns false (arcs cannot self-intersect)." },

                // VArc Methods
                { "VArc.Draw", "Renders the arc to the canvas." },
                { "VArc.Clone", "Creates a deep copy of this arc with all properties duplicated." },
                { "VArc.Move", "Translates the arc by the specified displacement vector." },
                { "VArc.Rotate", "Rotates the arc around the specified pivot by the given angle in degrees." },
                { "VArc.Flip", "Mirrors the arc across the specified axis line." },
                { "VArc.Scale", "Scales the arc relative to a center point by the specified factor." },
                { "VArc.GetBounds", "Returns the axis-aligned bounding box of the arc." },
                { "VArc.Contains", "Returns true if the specified point is on the arc." },
                { "VArc.DistanceTo", "Returns the minimum distance from the arc to the specified point." },
                { "VArc.GetLength", "Returns the arc length." },
                { "VArc.Divide", "Divides the arc into equal segments, returning the division points." },
                { "VArc.Measure", "Returns points along the arc at fixed distance intervals." },
                { "VArc.Project", "Projects a point onto the arc, returning the closest point on the arc." },
                { "VArc.PointAtParameter", "Returns a point on the arc at the given normalized parameter (0 to 1)." },
                { "VArc.Offset", "Creates a concentric arc offset by the specified distance." },
                { "VArc.NormalAtPoint", "Returns the normal vector at the specified point on the arc." },
                { "VArc.Intersect", "Computes intersection with another curve." },
                { "VArc.ToString", "Returns a string representation of the arc." },

                // VEllipse Properties
                { "VEllipse.Center", "Gets or sets the center point of the ellipse." },
                { "VEllipse.RadiusX", "Gets or sets the horizontal radius (semi-major or semi-minor axis)." },
                { "VEllipse.RadiusY", "Gets or sets the vertical radius (semi-major or semi-minor axis)." },
                { "VEllipse.SelfIntersecting", "Always returns false (ellipses cannot self-intersect)." },

                // VEllipse Methods
                { "VEllipse.Draw", "Renders the ellipse to the canvas." },
                { "VEllipse.Clone", "Creates a deep copy of this ellipse with all properties duplicated." },
                { "VEllipse.Move", "Translates the ellipse by the specified displacement vector." },
                { "VEllipse.Rotate", "Rotates the ellipse around the specified pivot by the given angle in degrees." },
                { "VEllipse.Flip", "Mirrors the ellipse across the specified axis line." },
                { "VEllipse.Scale", "Scales the ellipse relative to a center point by the specified factor." },
                { "VEllipse.GetBounds", "Returns the axis-aligned bounding box of the ellipse." },
                { "VEllipse.Contains", "Returns true if the specified point is inside or on the ellipse." },
                { "VEllipse.DistanceTo", "Returns the minimum distance from the ellipse to the specified point." },
                { "VEllipse.GetLength", "Returns the approximate perimeter of the ellipse." },
                { "VEllipse.PointAtParameter", "Returns a point on the ellipse at the given normalized parameter (0 to 1)." },
                { "VEllipse.Intersect", "Computes intersection with another curve." },
                { "VEllipse.ToString", "Returns a string representation of the ellipse." },

                // VPolygon Properties
                { "VPolygon.Points", "Gets or sets the list of vertex points defining the polygon." },
                { "VPolygon.Curves", "Gets the list of curves used to construct the polygon (if created from curves)." },
                { "VPolygon.StartPoint", "Gets the first vertex of the polygon." },
                { "VPolygon.EndPoint", "Gets the last vertex (same as StartPoint for closed polygon)." },
                { "VPolygon.SelfIntersecting", "Returns true if any edges of the polygon cross each other." },
                { "VPolygon.Area", "Gets the signed area of the polygon (positive for CCW, negative for CW vertices)." },

                // VPolygon Methods
                { "VPolygon.Draw", "Renders the polygon to the canvas (closed shape)." },
                { "VPolygon.Clone", "Creates a deep copy of this polygon with all properties duplicated." },
                { "VPolygon.Move", "Translates the polygon by the specified displacement vector." },
                { "VPolygon.Rotate", "Rotates the polygon around the specified pivot by the given angle in degrees." },
                { "VPolygon.Flip", "Mirrors the polygon across the specified axis line." },
                { "VPolygon.Scale", "Scales the polygon relative to a center point by the specified factor." },
                { "VPolygon.GetBounds", "Returns the axis-aligned bounding box of the polygon." },
                { "VPolygon.Contains", "Returns true if the specified point is inside or on the polygon." },
                { "VPolygon.DistanceTo", "Returns the minimum distance from the polygon to the specified point." },
                { "VPolygon.GetLength", "Returns the total perimeter of the polygon." },
                { "VPolygon.Divide", "Divides the polygon perimeter into equal segments, returning the division points." },
                { "VPolygon.Measure", "Returns points along the polygon perimeter at fixed distance intervals." },
                { "VPolygon.AddPoint", "Adds a vertex point to the polygon." },
                { "VPolygon.Project", "Projects a point onto the polygon boundary, returning the closest point." },
                { "VPolygon.PointAtSegmentLength", "Returns the point at the specified distance along the polygon perimeter." },
                { "VPolygon.PointAtParameter", "Returns a point on the polygon perimeter at the given normalized parameter (0 to 1)." },
                { "VPolygon.Offset", "Creates an offset polygon at the specified distance (+ = outward, - = inward)." },
                { "VPolygon.PointsAtChordLengthFromPoint", "Returns points on the polygon at a chord distance from a given point." },
                { "VPolygon.SplitAtPoint", "Splits the polygon at the specified point into two polylines." },
                { "VPolygon.NormalAtPoint", "Returns the outward normal vector at the specified point on the polygon." },
                { "VPolygon.Intersect", "Computes intersection with another curve." },
                { "VPolygon.Slice", "Slices the polygon along an infinite line defined by two points, returning a list of resulting polygons." },
                { "VPolygon.ToString", "Returns a string representation of the polygon." },

                // VPolyline Properties
                { "VPolyline.Points", "Gets the list of points defining the polyline." },
                { "VPolyline.PointCount", "Gets the number of points in the polyline." },
                { "VPolyline.StartPoint", "Gets the first point of the polyline." },
                { "VPolyline.EndPoint", "Gets the last point of the polyline." },
                { "VPolyline.SelfIntersecting", "Returns true if any segments of the polyline cross each other." },

                // VPolyline Methods
                { "VPolyline.Draw", "Renders the polyline to the canvas (open shape)." },
                { "VPolyline.Clone", "Creates a deep copy of this polyline with all properties duplicated." },
                { "VPolyline.Move", "Translates the polyline by the specified displacement vector." },
                { "VPolyline.Rotate", "Rotates the polyline around the specified pivot by the given angle in degrees." },
                { "VPolyline.Flip", "Mirrors the polyline across the specified axis line." },
                { "VPolyline.Scale", "Scales the polyline relative to a center point by the specified factor." },
                { "VPolyline.GetBounds", "Returns the axis-aligned bounding box of the polyline." },
                { "VPolyline.Contains", "Returns true if the specified point is on the polyline." },
                { "VPolyline.DistanceTo", "Returns the minimum distance from the polyline to the specified point." },
                { "VPolyline.GetLength", "Returns the total length of all segments." },
                { "VPolyline.Divide", "Divides the polyline into equal segments, returning the division points." },
                { "VPolyline.Measure", "Returns points along the polyline at fixed distance intervals." },
                { "VPolyline.Project", "Projects a point onto the polyline, returning the closest point." },
                { "VPolyline.PointAtParameter", "Returns a point on the polyline at the given normalized parameter (0 to 1)." },
                { "VPolyline.Offset", "Creates a parallel polyline offset by the specified distance." },
                { "VPolyline.Intersect", "Computes intersection with another curve." },
                { "VPolyline.ToString", "Returns a string representation of the polyline." },

                // VText Properties
                { "VText.Position", "Gets or sets the position point for the text." },
                { "VText.Text", "Gets or sets the text content to display." },
                { "VText.Height", "Gets or sets the font height in world units." },
                { "VText.FontFamily", "Gets or sets the font family name." },

                // VText Methods
                { "VText.Draw", "Renders the text to the canvas." },
                { "VText.Clone", "Creates a deep copy of this text with all properties duplicated." },
                { "VText.Move", "Translates the text by the specified displacement vector." },
                { "VText.Rotate", "Rotates the text around the specified pivot by the given angle in degrees." },
                { "VText.Flip", "Mirrors the text across the specified axis line." },
                { "VText.Scale", "Scales the text relative to a center point by the specified factor." },
                { "VText.GetBounds", "Returns the axis-aligned bounding box of the text." },
                { "VText.ToString", "Returns a string representation of the text object." },

                // VBezier Properties
                { "VBezier.StartPoint", "Gets or sets the starting point of the Bezier curve." },
                { "VBezier.Control1", "Gets or sets the first control point." },
                { "VBezier.Control2", "Gets or sets the second control point." },
                { "VBezier.EndPoint", "Gets or sets the ending point of the Bezier curve." },
                { "VBezier.SelfIntersecting", "Returns true if the Bezier curve crosses itself." },

                // VBezier Methods
                { "VBezier.Draw", "Renders the Bezier curve to the canvas." },
                { "VBezier.Clone", "Creates a deep copy of this Bezier with all properties duplicated." },
                { "VBezier.Move", "Translates the Bezier by the specified displacement vector." },
                { "VBezier.Rotate", "Rotates the Bezier around the specified pivot by the given angle in degrees." },
                { "VBezier.Flip", "Mirrors the Bezier across the specified axis line." },
                { "VBezier.Scale", "Scales the Bezier relative to a center point by the specified factor." },
                { "VBezier.GetBounds", "Returns the axis-aligned bounding box of the Bezier curve." },
                { "VBezier.GetLength", "Returns the approximate arc length of the Bezier curve." },
                { "VBezier.Divide", "Divides the Bezier into equal arc-length segments." },
                { "VBezier.PointAtParameter", "Returns a point on the Bezier curve at the given normalized parameter (0 to 1)." },
                { "VBezier.Intersect", "Computes intersection with another curve." },
                { "VBezier.ToString", "Returns a string representation of the Bezier curve." },

                // VSpline Properties
                { "VSpline.ControlPoints", "Gets the list of control points defining the spline." },
                { "VSpline.StartPoint", "Gets the starting point of the spline." },
                { "VSpline.EndPoint", "Gets the ending point of the spline." },
                { "VSpline.SelfIntersecting", "Returns true if the spline crosses itself." },

                // VSpline Methods
                { "VSpline.Draw", "Renders the spline curve to the canvas." },
                { "VSpline.Clone", "Creates a deep copy of this spline with all properties duplicated." },
                { "VSpline.Move", "Translates the spline by the specified displacement vector." },
                { "VSpline.Rotate", "Rotates the spline around the specified pivot by the given angle in degrees." },
                { "VSpline.Flip", "Mirrors the spline across the specified axis line." },
                { "VSpline.Scale", "Scales the spline relative to a center point by the specified factor." },
                { "VSpline.GetBounds", "Returns the axis-aligned bounding box of the spline." },
                { "VSpline.GetLength", "Returns the approximate arc length of the spline." },
                { "VSpline.Divide", "Divides the spline into equal arc-length segments." },
                { "VSpline.PointAtParameter", "Returns a point on the spline at the given normalized parameter (0 to 1)." },
                { "VSpline.Intersect", "Computes intersection with another curve." },
                { "VSpline.ToString", "Returns a string representation of the spline." },

                // VArrow Properties
                { "VArrow.Start", "Gets or sets the starting point of the arrow." },
                { "VArrow.End", "Gets or sets the ending point (tip) of the arrow." },
                { "VArrow.HeadSize", "Gets or sets the size of the arrowhead." },
                { "VArrow.HeadAngle", "Gets or sets the angle of the arrowhead in degrees." },
                { "VArrow.DoubleHeaded", "Gets or sets whether the arrow has heads on both ends." },

                // VArrow Methods
                { "VArrow.Draw", "Renders the arrow to the canvas." },
                { "VArrow.Clone", "Creates a deep copy of this arrow with all properties duplicated." },
                { "VArrow.Move", "Translates the arrow by the specified displacement vector." },
                { "VArrow.Rotate", "Rotates the arrow around the specified pivot by the given angle in degrees." },
                { "VArrow.Flip", "Mirrors the arrow across the specified axis line." },
                { "VArrow.Scale", "Scales the arrow relative to a center point by the specified factor." },
                { "VArrow.GetBounds", "Returns the axis-aligned bounding box of the arrow." },
                { "VArrow.ToString", "Returns a string representation of the arrow." },

                // VDimension Properties
                { "VDimension.Point1", "Gets or sets the first measurement point." },
                { "VDimension.Point2", "Gets or sets the second measurement point." },
                { "VDimension.Offset", "Gets or sets the offset distance for the dimension line." },
                { "VDimension.TextHeight", "Gets or sets the height of the dimension text." },

                // VDimension Methods
                { "VDimension.Draw", "Renders the dimension annotation to the canvas." },
                { "VDimension.Clone", "Creates a deep copy of this dimension with all properties duplicated." },
                { "VDimension.Move", "Translates the dimension by the specified displacement vector." },
                { "VDimension.Rotate", "Rotates the dimension around the specified pivot by the given angle in degrees." },
                { "VDimension.Flip", "Mirrors the dimension across the specified axis line." },
                { "VDimension.Scale", "Scales the dimension relative to a center point by the specified factor." },
                { "VDimension.GetBounds", "Returns the axis-aligned bounding box of the dimension." },
                { "VDimension.ToString", "Returns a string representation of the dimension." },

                // Shape base class properties
                { "Shape.Id", "Gets the unique identifier for this shape, automatically assigned on creation." },
                { "Shape.Color", "Gets or sets the outline/stroke color as a string (named color or hex code like '#FF0000' or '#80FF0000')." },
                { "Shape.FillColor", "Gets or sets the fill color as a string. Use 'Transparent' for no fill." },
                { "Shape.LineWeight", "Gets or sets the thickness of the outline stroke in pixels." },
                { "Shape.LineType", "Gets or sets the stroke style (line pattern). Options: Continuous (solid), Dashed, Dotted, DashDot, DashDotDot, Center, Phantom, Hidden." },
                { "Shape.LineTypeScale", "Gets or sets the scale factor for stroke patterns (default 1.0). Values > 1.0 create longer dashes/gaps, < 1.0 create shorter ones." },
                { "Shape.DrawFactor", "Gets or sets the draw factor (0.0 to 1.0) for progressive drawing animations." },
                { "Shape.OffsetX", "Gets or sets the X offset for translation animations." },
                { "Shape.OffsetY", "Gets or sets the Y offset for translation animations." },
                { "Shape.RotationAngle", "Gets or sets the rotation angle in degrees for rotation animations." },
                { "Shape.RotationPivot", "Gets or sets the pivot point for rotation animations. Null uses shape center." },
                { "Shape.IsVisible", "Gets or sets whether this shape is visible on the canvas. Hidden shapes are not rendered but remain in the shape collection." },

                // Shape base class methods
                { "Shape.Draw", "Renders the shape to the canvas. Must be called for the shape to be visible." },
                { "Shape.Clone", "Creates a deep copy of the shape with all properties duplicated." },
                { "Shape.Move", "Translates the shape by the specified displacement vector." },
                { "Shape.Rotate", "Rotates the shape around the specified pivot point by the given angle in degrees." },
                { "Shape.Flip", "Mirrors the shape across the specified line (axis of reflection)." },
                { "Shape.Scale", "Scales the shape relative to a center point by the specified factor." },
                { "Shape.GetBounds", "Returns the axis-aligned bounding box as a tuple (minPoint, maxPoint)." },
                { "Shape.Contains", "Returns true if the specified point is inside or on the shape boundary." },
                { "Shape.DistanceTo", "Returns the minimum distance from the shape to the specified point." },
                { "Shape.Intersect", "Computes geometric intersection with another shape." },
                { "Shape.CopyStyleTo", "Copies this shape's style properties to another shape." },
                { "Shape.ToString", "Returns a string representation of the shape." },
                { "Shape.Show", "Shows this shape on the canvas by setting IsVisible to true." },
                { "Shape.Hide", "Hides this shape from the canvas by setting IsVisible to false. The shape remains in the collection but is not rendered." },

                // VXYZ Properties
                { "VXYZ.X", "Gets or sets the X component of the vector." },
                { "VXYZ.Y", "Gets or sets the Y component of the vector." },
                { "VXYZ.Z", "Gets or sets the Z component of the vector." },
                { "VXYZ.Item", "Gets the vector component at the specified index (0=X, 1=Y, 2=Z)." },

                // VXYZ Methods
                { "VXYZ.Add", "Returns a new vector that is the sum of this vector and another." },
                { "VXYZ.Subtract", "Returns a new vector that is the difference of this vector and another." },
                { "VXYZ.Multiply", "Returns a new vector with each component multiplied by the scalar value." },
                { "VXYZ.Divide", "Returns a new vector with each component divided by the scalar value." },
                { "VXYZ.Negate", "Returns a new vector with all components negated (reversed direction)." },
                { "VXYZ.AsVPoint", "Converts this VXYZ to a VPoint (ignores Z component)." },
                { "VXYZ.GetLength", "Returns the magnitude (length) of the vector." },
                { "VXYZ.Normalize", "Returns a unit vector in the same direction (length = 1)." },
                { "VXYZ.DistanceTo", "Returns the Euclidean distance from this point/vector to another." },
                { "VXYZ.DotProduct", "Returns the dot product (scalar product) of this vector with another vector." },
                { "VXYZ.CrossProduct", "Returns the cross product of this vector with another vector (3D only)." },
                { "VXYZ.TripleProduct", "Returns the scalar triple product of three vectors: this · (a × b)." },
                { "VXYZ.AngleTo", "Returns the angle in radians between this vector and another vector." },
                { "VXYZ.IsZeroLength", "Returns true if the vector has zero length (all components are zero)." },
                { "VXYZ.IsUnitLength", "Returns true if the vector has unit length (magnitude ≈ 1)." },
                { "VXYZ.IsAlmostEqualTo", "Returns true if this vector is approximately equal to another within the specified tolerance." },
                { "VXYZ.Equals", "Returns true if this vector equals another object." },
                { "VXYZ.GetHashCode", "Returns a hash code for this vector." },
                { "VXYZ.ToString", "Returns a string representation: \"(X, Y, Z)\"." },

                // ICurve interface
                { "ICurve.StartPoint", "Gets the starting point of the curve." },
                { "ICurve.EndPoint", "Gets the ending point of the curve." },
                { "ICurve.Vertices", "Gets the key vertices/control points of the curve. For lines: start and end. For circles/ellipses: center. For arcs: center, start, end. For polygons/polylines: all vertices. For beziers/splines: all control points." },
                { "ICurve.SelfIntersecting", "Gets whether this curve intersects itself. Simple curves (lines, circles) always return false." },
                { "ICurve.GetLength", "Returns the total arc length of the curve." },
                { "ICurve.Divide", "Divides the curve into the specified number of equal segments, returning the division points." },
                { "ICurve.Measure", "Returns points along the curve at fixed distance intervals." },
                { "ICurve.Project", "Projects a point onto the curve, returning the closest point on the curve." },
                { "ICurve.PointAtSegmentLength", "Returns the point at the specified distance along the curve from the start." },
                { "ICurve.PointAtParameter", "Returns a point on the curve at the given normalized parameter (0 to 1), where 0 is the start and 1 is the end." },
                { "ICurve.Offset", "Creates a new curve offset by the specified distance (positive = left, negative = right)." },
                { "ICurve.SplitAtPoint", "Splits the curve at the specified point, returning two curve segments." },
                { "ICurve.NormalAtPoint", "Returns the normal vector (perpendicular) to the curve at the specified point." },
                { "ICurve.Intersect", "Computes intersection with another curve, returning an IntersectionResult with points and overlapping segments." },

                // Animator
                { "Animator.Duration", "Gets the total duration of all animations in seconds." },
                { "Animator.Repeat", "Gets or sets whether the animation loops continuously." },
                { "Animator.Speed", "Gets or sets the playback speed multiplier (1.0 = normal speed)." },
                { "Animator.AddToAnimations", "Adds animation(s) to play. Single animation plays sequentially; List<Animation> plays in parallel." },
                { "Animator.Animate", "Starts playback of all animations." },
                { "Animator.Stop", "Stops playback of all animations." },

                // IntersectionResult
                { "IntersectionResult.Points", "Gets the list of intersection points." },
                { "IntersectionResult.Curves", "Gets the list of overlapping curve segments (for collinear/coincident curves)." },
                { "IntersectionResult.HasIntersection", "Returns true if there is at least one intersection point or overlapping segment." },
                { "IntersectionResult.IsSinglePoint", "Returns true if there is exactly one intersection point." },
                { "IntersectionResult.HasOverlap", "Returns true if the curves share an overlapping segment." },
                { "IntersectionResult.Count", "Gets the total number of intersection elements (points + curves)." },

                // Animation base class
                { "Animation.Target", "Gets the shape that this animation affects." },
                { "Animation.StartTime", "Gets the time in seconds when the animation begins (set automatically by Animator)." },
                { "Animation.Duration", "Gets the animation duration in seconds (set in constructor)." },
                { "Animation.EasingFunction", "Gets or sets the easing function for smooth motion (e.g., EaseInOut)." },
                { "Animation.Apply", "Applies the animation at the specified normalized time (0 to 1)." },

                // DrawAnimation
                { "DrawAnimation.Target", "Gets the shape to animate drawing." },
                { "DrawAnimation.Duration", "Gets how long the drawing takes (in seconds)." },
                { "DrawAnimation.EasingFunction", "Gets or sets the easing function for the draw effect." },

                // MoveAnimation
                { "MoveAnimation.Target", "Gets the shape to move." },
                { "MoveAnimation.Duration", "Gets how long the movement takes (in seconds)." },
                { "MoveAnimation.EasingFunction", "Gets or sets the easing function for smooth movement." },

                // RotateAnimation
                { "RotateAnimation.Target", "Gets the shape to rotate." },
                { "RotateAnimation.Duration", "Gets how long the rotation takes (in seconds)." },
                { "RotateAnimation.EasingFunction", "Gets or sets the easing function for smooth rotation." },

                // FlipAnimation
                { "FlipAnimation.Target", "Gets the shape to flip." },
                { "FlipAnimation.Duration", "Gets how long the flip takes (in seconds)." },
                { "FlipAnimation.EasingFunction", "Gets or sets the easing function for the flip effect." },
                { "FlipAnimation.Apply", "Applies the flip animation, progressively mirroring the shape." },

                // FadeInAnimation
                { "FadeInAnimation.Target", "Gets the shape to fade in." },
                { "FadeInAnimation.Duration", "Gets how long the fade-in takes (in seconds)." },
                { "FadeInAnimation.EasingFunction", "Gets or sets the easing function for smooth fade-in." },

                // FadeOutAnimation
                { "FadeOutAnimation.Target", "Gets the shape to fade out." },
                { "FadeOutAnimation.Duration", "Gets how long the fade-out takes (in seconds)." },
                { "FadeOutAnimation.EasingFunction", "Gets or sets the easing function for smooth fade-out." },
                { "FadeOutAnimation.Apply", "Applies the fade-out animation, decreasing opacity from 1 to 0." },

                // EasingFunctions
                { "EasingFunctions.Linear", "Returns linear easing (constant speed, no acceleration)." },
                { "EasingFunctions.EaseInQuad", "Returns quadratic ease-in (slow start, accelerating)." },
                { "EasingFunctions.EaseOutQuad", "Returns quadratic ease-out (fast start, decelerating)." },
                { "EasingFunctions.EaseInOutQuad", "Returns quadratic ease-in-out (slow start and end)." },
                { "EasingFunctions.EaseInCubic", "Returns cubic ease-in (slower start than quadratic)." },
                { "EasingFunctions.EaseOutCubic", "Returns cubic ease-out (slower end than quadratic)." },
                { "EasingFunctions.EaseInOutCubic", "Returns cubic ease-in-out (smoother start and end)." },

                // ArrayOps
                { "ArrayOps.LinearArray", "Creates copies of a shape along a direction vector." },
                { "ArrayOps.LinearArrayX", "Creates copies of a shape along the X axis." },
                { "ArrayOps.LinearArrayY", "Creates copies of a shape along the Y axis." },
                { "ArrayOps.RectangularArray", "Creates a grid pattern of shape copies (rows × columns)." },
                { "ArrayOps.CircularArray", "Creates copies arranged in a circle around a center point." },
                { "ArrayOps.PathArray", "Creates copies distributed along a curve path." },
                { "ArrayOps.SpiralArray", "Creates copies arranged in a spiral pattern." },
                { "ArrayOps.Mirror", "Creates a mirrored copy of a shape across an axis line." },

                // BooleanOps
                { "BooleanOps.Union", "Combines two or more polygons into one. Returns a single VPolygon if successful, or null if polygons don't overlap/touch (logs reason to console)." },
                { "BooleanOps.Intersect", "Returns the overlapping area of two polygons (logical AND)." },
                { "BooleanOps.Difference", "Subtracts one polygon from another." },
                { "BooleanOps.Xor", "Returns the symmetric difference of two polygons (non-overlapping areas)." },
                { "BooleanOps.OffsetPolygon", "Grows or shrinks a polygon by the specified distance." },
                { "BooleanOps.Simplify", "Removes redundant points from a polygon within tolerance." },
                { "BooleanOps.Area", "Calculates the area of a polygon." },
                { "BooleanOps.PointInPolygon", "Tests if a point is inside a polygon." },

                // ControlPoint
                { "ControlPoint.Position", "Gets or sets the position of the control point." },
                { "ControlPoint.Weight", "Gets or sets the weight for NURBS curves (default 1.0)." },

                // CurveIntersection
                { "CurveIntersection.Intersect", "Computes intersection points between two curves." },
                { "CurveIntersection.IsSelfIntersecting", "Checks if a curve crosses itself." },
                { "CurveIntersection.LineLineIntersection", "Finds the intersection of two line segments." },
                { "CurveIntersection.LineCircleIntersection", "Finds intersections of a line and circle." },
                { "CurveIntersection.CircleCircleIntersection", "Finds intersections of two circles." },
                { "CurveIntersection.LineArcIntersection", "Finds intersections of a line and arc." },
                { "CurveIntersection.ArcArcIntersection", "Finds intersections of two arcs." },

                // GeometryHelper
                { "GeometryHelper.DistancePointToLine", "Calculates perpendicular distance from a point to a line." },
                { "GeometryHelper.DistancePointToPoint", "Calculates Euclidean distance between two points." },
                { "GeometryHelper.LineLineIntersection", "Finds the intersection point of two infinite lines." },
                { "GeometryHelper.AngleBetweenVectors", "Calculates the angle between two vectors in radians." },
                { "GeometryHelper.RotatePoint", "Rotates a point around a pivot by an angle." },
                { "GeometryHelper.FlipPoint", "Mirrors a point across a line." },
                { "GeometryHelper.ProjectPointOnLine", "Projects a point onto the nearest point on a line." },
                { "GeometryHelper.IsPointOnLine", "Checks if a point lies on a line segment." },
                { "GeometryHelper.IsPointInPolygon", "Checks if a point is inside a polygon." },
                { "GeometryHelper.GetPolygonArea", "Calculates the signed area of a polygon." },
                { "GeometryHelper.GetPolygonCentroid", "Calculates the centroid (center of mass) of a polygon." },

                // IDrawable
                { "IDrawable.Draw", "Renders the drawable object to the canvas." },
                { "IDrawable.Color", "Gets or sets the stroke/outline color." },
                { "IDrawable.FillColor", "Gets or sets the fill color." },
                { "IDrawable.LineWeight", "Gets or sets the stroke thickness." },
                { "IDrawable.LineTypeScale", "Gets or sets the scale factor for stroke patterns. Default is 1.0." },

                // ShapeDefaults
                { "ShapeDefaults.GlobalColor", "Gets or sets the default stroke color for new shapes." },
                { "ShapeDefaults.GlobalFillColor", "Gets or sets the default fill color for new shapes." },
                { "ShapeDefaults.GlobalLineWeight", "Gets or sets the default stroke thickness for new shapes." },
                { "ShapeDefaults.GlobalLineType", "Gets or sets the default stroke style for new shapes. Options: Continuous, Dashed, Dotted, DashDot, DashDotDot, Center, Phantom, Hidden." },
                { "ShapeDefaults.GlobalLineTypeScale", "Gets or sets the default stroke style scale for new shapes. Controls the scale of dash patterns (default 1.0)." },
                { "ShapeDefaults.Reset", "Resets all global defaults to their original values." },

                // LineType enum values
                { "LineType.Continuous", "Solid continuous line (default). Standard line with no gaps." },
                { "LineType.Dashed", "Dashed line pattern with long dashes and short gaps." },
                { "LineType.Dotted", "Dotted line pattern with short dots and gaps." },
                { "LineType.DashDot", "Alternating dash and dot pattern (dash-dot-dash-dot)." },
                { "LineType.DashDotDot", "Alternating dash and two dots pattern (dash-dot-dot-dash)." },
                { "LineType.Center", "Center line pattern (long-short-long), commonly used for centerlines in technical drawings." },
                { "LineType.Phantom", "Phantom line pattern (long-short-short), used for alternate positions or hidden features." },
                { "LineType.Hidden", "Hidden line pattern with short dashes, used for hidden edges in technical drawings." },

                // VCoordinateSystem
                { "VCoordinateSystem.Origin", "Gets or sets the origin point of the coordinate system." },
                { "VCoordinateSystem.BasisX", "Gets or sets the X-axis direction vector." },
                { "VCoordinateSystem.BasisY", "Gets or sets the Y-axis direction vector." },
                { "VCoordinateSystem.BasisZ", "Gets or sets the Z-axis direction vector." },
                { "VCoordinateSystem.IsRightHanded", "Returns true if the coordinate system is right-handed." },
                { "VCoordinateSystem.Transform", "Transforms a point from world to local coordinates." },
                { "VCoordinateSystem.InverseTransform", "Transforms a point from local to world coordinates." },

                // VPlane
                { "VPlane.Origin", "Gets or sets the origin point of the plane." },
                { "VPlane.Normal", "Gets the normal vector perpendicular to the plane." },
                { "VPlane.XAxis", "Gets the X-axis direction vector on the plane." },
                { "VPlane.YAxis", "Gets the Y-axis direction vector on the plane." },
                { "VPlane.CreateByNormalAndOrigin", "Creates a plane from a normal vector and origin point." },
                { "VPlane.CreateByThreePoints", "Creates a plane passing through three points." },
                { "VPlane.ProjectPoint", "Projects a 3D point onto the plane." },
                { "VPlane.DistanceTo", "Returns the signed distance from a point to the plane." },

                // VTransform
                { "VTransform.Matrix", "Gets the 4x4 transformation matrix." },
                { "VTransform.IsIdentity", "Returns true if this is an identity transform (no change)." },
                { "VTransform.CreateRotation", "Creates a rotation transform around an axis by an angle." },
                { "VTransform.CreateTranslation", "Creates a translation transform by a displacement vector." },
                { "VTransform.CreateScale", "Creates a uniform or non-uniform scale transform." },
                { "VTransform.CreateReflection", "Creates a reflection transform across a plane." },
                { "VTransform.Multiply", "Multiplies (combines) two transforms." },
                { "VTransform.Inverse", "Returns the inverse of this transform." },
                { "VTransform.TransformPoint", "Applies the transform to a point." },
                { "VTransform.TransformVector", "Applies the transform to a vector (ignores translation)." },

                // DxfExporter
                { "DxfExporter.Export", "Exports shapes to a DXF file (AutoCAD format)." },
                { "DxfExporter.ExportToString", "Exports shapes to a DXF string." },

                // PdfExporter
                { "PdfExporter.Export", "Exports shapes to a PDF file." },
                { "PdfExporter.PageSize", "Gets or sets the page size (A4, Letter, etc.)." },
                { "PdfExporter.Margin", "Gets or sets the page margins." },

                // SvgExporter
                { "SvgExporter.Export", "Exports shapes to an SVG file." },
                { "SvgExporter.ExportToString", "Exports shapes to an SVG string." },
                { "SvgExporter.Width", "Gets or sets the SVG canvas width." },
                { "SvgExporter.Height", "Gets or sets the SVG canvas height." },

                // GifEncoder
                { "GifEncoder.AddFrame", "Adds a frame to the GIF animation." },
                { "GifEncoder.Save", "Saves the GIF to a file." },
                { "GifEncoder.FrameDelay", "Gets or sets the delay between frames in milliseconds." },
                { "GifEncoder.Repeat", "Gets or sets whether the GIF loops infinitely." },

                // VideoExporter
                { "VideoExporter.AddFrame", "Adds a frame (RenderTargetBitmap) to the video. Frames are encoded in sequence at the configured frame rate." },
                { "VideoExporter.Dispose", "Finalizes the video encoding and releases resources. Must be called to produce a valid MP4 file." },

                // ShapeArrayExtensions (extension methods)
                { "ShapeArrayExtensions.DrawAll", "Draws all shapes in the collection." },
                { "ShapeArrayExtensions.LinearArrayX", "Extension: creates copies along the X axis." },
                { "ShapeArrayExtensions.LinearArrayY", "Extension: creates copies along the Y axis." },
                { "ShapeArrayExtensions.LinearArray", "Extension: creates copies along a direction." },
                { "ShapeArrayExtensions.RectangularArray", "Extension: creates a grid pattern of copies." },
                { "ShapeArrayExtensions.CircularArray", "Extension: creates copies in a circle." },
                { "ShapeArrayExtensions.PathArray", "Extension: creates copies along a path." },
                { "ShapeArrayExtensions.SpiralArray", "Extension: creates copies in a spiral." },
                { "ShapeArrayExtensions.Mirror", "Extension: creates a mirrored copy." },

                // VPolygonBooleanExtensions (extension methods)
                { "VPolygonBooleanExtensions.Union", "Extension: combines polygons into one. Returns VPolygon or null if they don't overlap." },
                { "VPolygonBooleanExtensions.Intersect", "Extension: returns overlapping area (boolean AND)." },
                { "VPolygonBooleanExtensions.Difference", "Extension: subtracts one polygon from another." },
                { "VPolygonBooleanExtensions.Xor", "Extension: returns symmetric difference." },
                { "VPolygonBooleanExtensions.Contains", "Extension: tests if a point is inside the polygon." },
                { "VPolygonBooleanExtensions.GetArea", "Extension: calculates polygon area." },

                // VColor Static Properties (common colors)
                { "VColor.Red", "Returns \"Red\" color string." },
                { "VColor.Green", "Returns \"Green\" color string." },
                { "VColor.Blue", "Returns \"Blue\" color string." },
                { "VColor.Yellow", "Returns \"Yellow\" color string." },
                { "VColor.Orange", "Returns \"Orange\" color string." },
                { "VColor.Purple", "Returns \"Purple\" color string." },
                { "VColor.Pink", "Returns \"Pink\" color string." },
                { "VColor.Cyan", "Returns \"Cyan\" color string." },
                { "VColor.Magenta", "Returns \"Magenta\" color string." },
                { "VColor.White", "Returns \"White\" color string." },
                { "VColor.Black", "Returns \"Black\" color string." },
                { "VColor.Gray", "Returns \"Gray\" color string." },
                { "VColor.LimeGreen", "Returns \"LimeGreen\" color string." },
                { "VColor.Gold", "Returns \"Gold\" color string." },
                { "VColor.Coral", "Returns \"Coral\" color string." },

                // VColor Static Methods
                { "VColor.GetRandomColor", "Returns a random color string. If returnPastelColor is true (default), returns soft pastel colors; if false, returns vibrant colors." },
                { "VColor.GetRandomVibrantColor", "Returns a random vibrant color (good for strokes on dark backgrounds)." },
                { "VColor.GetRandomPastelColor", "Returns a random pastel color (good for fills)." },
                { "VColor.FromEnum", "Converts a ColorName enum value to its string representation." },
                { "VColor.FromRgb", "Creates a hex color string from RGB values (0-255). Example: FromRgb(255, 128, 0) returns \"#FF8000\"." },
                { "VColor.FromArgb", "Creates a hex color string from ARGB values (0-255). Example: FromArgb(128, 255, 0, 0) returns \"#80FF0000\"." },
                { "VColor.WithOpacity", "Creates a semi-transparent color from RGB values and opacity (0.0-1.0)." },
                { "VColor.GetVibrantColors", "Returns an array of all vibrant color names." },
                { "VColor.GetPastelColors", "Returns an array of all pastel color names." },
            };
        }

        private string GetMemberDescription(string className, string memberName)
        {
            var key = $"{className}.{memberName}";
            if (_memberDescriptions != null && _memberDescriptions.TryGetValue(key, out var desc))
                return desc;
            return "";
        }

        public FlowDocument GenerateWelcomePage()
        {
            var doc = new FlowDocument();
            doc.FontFamily = new FontFamily("Segoe UI");
            doc.PagePadding = new Thickness(20);
            doc.ColumnWidth = double.NaN;

            // Title
            var title = new Paragraph(new Run("Welcome to Code2Viz"))
            {
                FontSize = 28,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.DarkSlateGray,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 20)
            };
            doc.Blocks.Add(title);

            // Tagline
            var tagline = new Paragraph(new Run("A Visual Programming Environment for 2D Geometry"))
            {
                FontSize = 16,
                FontStyle = FontStyles.Italic,
                Foreground = Brushes.Teal,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 30)
            };
            doc.Blocks.Add(tagline);

            // Introduction
            AddWelcomeSectionHeader(doc, "What is Code2Viz?");
            doc.Blocks.Add(new Paragraph(new Run(
                "Code2Viz is an interactive application that lets you write C# or F# code to create and visualize 2D geometric shapes. " +
                "Simply write code in the built-in editor, press F5 (or click Run), and see your shapes appear on the canvas instantly. " +
                "It's perfect for learning geometry, creating diagrams, prototyping visualizations, and exploring mathematical concepts."))
            { FontSize = 14, Margin = new Thickness(0, 0, 0, 15) });

            // Key Features
            AddWelcomeSectionHeader(doc, "Key Features");
            var featuresList = new List
            {
                MarkerStyle = TextMarkerStyle.Disc,
                Margin = new Thickness(20, 0, 0, 20)
            };
            AddListItem(featuresList, "Multi-language Support", "Write code in C# or F# with full IntelliSense-like syntax highlighting");
            AddListItem(featuresList, "Rich Shape Library", "Points, lines, circles, rectangles, ellipses, arcs, polygons, polylines, Bezier curves, splines, text, arrows, and dimensions");
            AddListItem(featuresList, "Drawing Tools", "Draw shapes directly on the canvas with automatic code generation");
            AddListItem(featuresList, "Animation System", "Create timeline-based animations with draw, move, rotate, and flip effects");
            AddListItem(featuresList, "Interactive Canvas", "Zoom with mouse wheel, pan with middle-click, toggle grid display");
            AddListItem(featuresList, "Export Options", "Save visualizations as PNG images, animated GIFs, or MP4 videos");
            AddListItem(featuresList, "Project Management", "Organize multiple code files into projects with tabbed editing");
            AddListItem(featuresList, "NuGet Package Manager", "Search, install, update, and remove NuGet packages via Tools menu");
            doc.Blocks.Add(featuresList);

            // Getting Started
            AddWelcomeSectionHeader(doc, "Getting Started");
            var stepsList = new List
            {
                MarkerStyle = TextMarkerStyle.Decimal,
                Margin = new Thickness(20, 0, 0, 20)
            };
            AddListItem(stepsList, "Create or Open a Project", "Use File > New Project or File > Open to start");
            AddListItem(stepsList, "Write Your Code", "The entry point is StartViz.Viz.Main() in StartViz.vizcode");
            AddListItem(stepsList, "Create Shapes", "Instantiate shape objects (e.g., new VCircle(0, 0, 50))");
            AddListItem(stepsList, "Draw to Canvas", "Call .Draw() on shapes to render them");
            AddListItem(stepsList, "Run Your Code", "Press F5 or click the Run button to see results");
            doc.Blocks.Add(stepsList);

            // Quick Example
            AddWelcomeSectionHeader(doc, "Quick Example");
            var exampleCode = @"using Code2Viz.Geometry;

namespace StartViz
{
    public class Viz
    {
        public static void Main()
        {
            // Create a circle at origin with radius 50
            var circle = new VCircle(0, 0, 50);
            circle.Color = ""Cyan"";
            circle.FillColor = ""#4000FFFF"";
            circle.Draw();

            // Add crosshairs
            new VLine(-60, 0, 60, 0).Draw();
            new VLine(0, -60, 0, 60).Draw();
        }
    }
}";
            var codeP = new Paragraph(new Run(exampleCode))
            {
                FontFamily = new FontFamily("Consolas"),
                Background = Brushes.WhiteSmoke,
                Padding = new Thickness(15),
                Margin = new Thickness(0, 0, 0, 20)
            };
            doc.Blocks.Add(codeP);

            // Drawing Tools
            AddWelcomeSectionHeader(doc, "Drawing Tools");
            doc.Blocks.Add(new Paragraph(new Run(
                "Draw shapes directly on the canvas using the toolbar below the menu bar. " +
                "Click to place points, and the corresponding C#/F# code is automatically generated and inserted into your Main() method."))
            { FontSize = 14, Margin = new Thickness(0, 0, 0, 10) });

            // Drawing tools table
            var drawingTable = new Table();
            drawingTable.CellSpacing = 0;
            drawingTable.BorderBrush = Brushes.LightGray;
            drawingTable.BorderThickness = new Thickness(1);
            drawingTable.Columns.Add(new TableColumn { Width = new GridLength(100) });
            drawingTable.Columns.Add(new TableColumn { Width = new GridLength(250) });
            drawingTable.Columns.Add(new TableColumn { Width = new GridLength(100) });

            var drawingRowGroup = new TableRowGroup();
            // Header
            var drawingHeaderRow = new TableRow();
            drawingHeaderRow.Background = Brushes.AliceBlue;
            drawingHeaderRow.Cells.Add(CreateHelpHeaderCell("Shape"));
            drawingHeaderRow.Cells.Add(CreateHelpHeaderCell("Method"));
            drawingHeaderRow.Cells.Add(CreateHelpHeaderCell("Clicks"));
            drawingRowGroup.Rows.Add(drawingHeaderRow);

            AddDrawingToolRow(drawingRowGroup, "Point", "Single click", "1", false);
            AddDrawingToolRow(drawingRowGroup, "Line", "Click start, click end", "2", true);
            AddDrawingToolRow(drawingRowGroup, "Circle", "Click center, click radius", "2", false);
            AddDrawingToolRow(drawingRowGroup, "Rectangle", "Click corner, click opposite", "2", true);
            AddDrawingToolRow(drawingRowGroup, "Arc", "Click center, start, end", "3", false);
            AddDrawingToolRow(drawingRowGroup, "Polygon", "Click vertices, double-click", "N", true);
            AddDrawingToolRow(drawingRowGroup, "Polyline", "Click points, double-click", "N", false);
            AddDrawingToolRow(drawingRowGroup, "Bezier", "Click start, ctrl1, ctrl2, end", "4", true);

            drawingTable.RowGroups.Add(drawingRowGroup);
            doc.Blocks.Add(drawingTable);

            // Snap support
            doc.Blocks.Add(new Paragraph(new Run("\nSnap Support: ") { FontWeight = FontWeights.SemiBold })
            { FontSize = 14, Margin = new Thickness(0, 10, 0, 5) });
            var snapList = new List
            {
                MarkerStyle = TextMarkerStyle.Disc,
                Margin = new Thickness(20, 0, 0, 15)
            };
            AddListItem(snapList, "Endpoints", "Start/end points of lines, arcs, polylines");
            AddListItem(snapList, "Midpoints", "Middle point of lines and curves");
            AddListItem(snapList, "Centers", "Center of circles, arcs, ellipses");
            AddListItem(snapList, "Intersections", "Where two shapes cross");
            AddListItem(snapList, "Nearest", "Closest point on any curve");
            doc.Blocks.Add(snapList);

            // Keyboard Shortcuts
            AddWelcomeSectionHeader(doc, "Keyboard Shortcuts");
            var shortcutsTable = new Table();
            shortcutsTable.CellSpacing = 0;
            shortcutsTable.BorderBrush = Brushes.LightGray;
            shortcutsTable.BorderThickness = new Thickness(1);
            shortcutsTable.Columns.Add(new TableColumn { Width = new GridLength(150) });
            shortcutsTable.Columns.Add(new TableColumn { Width = new GridLength(300) });

            var rowGroup = new TableRowGroup();
            // File operations
            AddShortcutRow(rowGroup, "F5 / Ctrl+Enter", "Run code", true);
            AddShortcutRow(rowGroup, "Ctrl+S", "Save all files", false);
            AddShortcutRow(rowGroup, "Ctrl+N", "New file", true);
            AddShortcutRow(rowGroup, "Ctrl+Shift+N", "New project", false);
            AddShortcutRow(rowGroup, "Ctrl+O", "Open project", true);
            // Editor operations
            AddShortcutRow(rowGroup, "Ctrl+Shift+F", "Format code", false);
            AddShortcutRow(rowGroup, "Ctrl+/", "Toggle comment", true);
            // Find and Replace
            AddShortcutRow(rowGroup, "Ctrl+F", "Find", false);
            AddShortcutRow(rowGroup, "Ctrl+H", "Find and Replace", true);
            AddShortcutRow(rowGroup, "F3", "Find Next", false);
            AddShortcutRow(rowGroup, "Shift+F3", "Find Previous", true);
            // Line operations
            AddShortcutRow(rowGroup, "Alt+Up/Down", "Move line up/down", false);
            AddShortcutRow(rowGroup, "Shift+Alt+Up", "Copy line up", true);
            AddShortcutRow(rowGroup, "Shift+Alt+Down", "Copy line down", false);
            AddShortcutRow(rowGroup, "Ctrl+Shift+D", "Delete line", true);
            // Selection operations
            AddShortcutRow(rowGroup, "Shift+Alt+Right", "Expand selection", false);
            AddShortcutRow(rowGroup, "Shift+Alt+Left", "Shrink selection", true);
            AddShortcutRow(rowGroup, "Ctrl+D", "Add next occurrence", false);
            AddShortcutRow(rowGroup, "Ctrl+Shift+L", "Select all occurrences", true);
            AddShortcutRow(rowGroup, "Ctrl+Alt+Up", "Add cursor above", false);
            AddShortcutRow(rowGroup, "Ctrl+Alt+Down", "Add cursor below", true);
            // Canvas & Tools
            AddShortcutRow(rowGroup, "Mouse Wheel", "Zoom canvas", false);
            AddShortcutRow(rowGroup, "Middle Click", "Pan canvas", true);
            AddShortcutRow(rowGroup, "Ctrl+G", "Zoom to shape by ID", false);
            AddShortcutRow(rowGroup, "Ctrl+M", "Toggle Measuring Tape tool", true);
            // Drawing Tools
            AddShortcutRow(rowGroup, "P", "Point drawing tool", false);
            AddShortcutRow(rowGroup, "L", "Line drawing tool", true);
            AddShortcutRow(rowGroup, "C", "Circle drawing tool", false);
            AddShortcutRow(rowGroup, "R", "Rectangle drawing tool", true);
            AddShortcutRow(rowGroup, "Esc", "Cancel drawing / Return to select", false);
            // Code Navigation & Intellisense
            AddShortcutRow(rowGroup, "F12", "Go to Definition", true);
            AddShortcutRow(rowGroup, "Shift+F12", "Find All References", false);
            AddShortcutRow(rowGroup, "Alt+F12", "Peek Definition", true);
            AddShortcutRow(rowGroup, "Ctrl+.", "Quick Fix (add using)", false);
            AddShortcutRow(rowGroup, "Ctrl+Shift+O", "Document Symbols", true);
            AddShortcutRow(rowGroup, "Ctrl+T", "Workspace Symbols", false);
            AddShortcutRow(rowGroup, "Ctrl+Shift+H", "Call Hierarchy", true);
            AddShortcutRow(rowGroup, "Ctrl+Shift+T", "Type Hierarchy", false);
            AddShortcutRow(rowGroup, "F2", "Rename Symbol", true);
            shortcutsTable.RowGroups.Add(rowGroup);
            doc.Blocks.Add(shortcutsTable);

            // Coordinate System
            AddWelcomeSectionHeader(doc, "Coordinate System");
            doc.Blocks.Add(new Paragraph(new Run(
                "Code2Viz uses a standard mathematical coordinate system with the origin (0, 0) at the center of the canvas. " +
                "The X-axis points right and the Y-axis points up (not down like typical screen coordinates). " +
                "Positive angles are measured counter-clockwise from the positive X-axis."))
            { FontSize = 14, Margin = new Thickness(0, 0, 0, 20) });

            // Tips
            AddWelcomeSectionHeader(doc, "Tips");
            var tipsList = new List
            {
                MarkerStyle = TextMarkerStyle.Circle,
                Margin = new Thickness(20, 0, 0, 20)
            };
            AddListItem(tipsList, "Colors", "Use color names (\"Red\", \"Cyan\") or hex codes (\"#FF0000\", \"#80FFFFFF\" for semi-transparent)");
            AddListItem(tipsList, "VizConsole", "Use VizConsole.Log() to output debug messages to the console panel");
            AddListItem(tipsList, "Auto-update Canvas", "Canvas updates automatically as you type (500ms delay). Disable in Settings > Application Settings if you prefer manual Run");
            AddListItem(tipsList, "No Draw() Needed", "Shapes appear automatically when created - Draw() is optional and kept for backwards compatibility");
            AddListItem(tipsList, "Show/Hide Shapes", "Use shape.Hide() and shape.Show() to control visibility without removing from canvas");
            AddListItem(tipsList, "ShapeDefaults", "Set ShapeDefaults.GlobalColor to apply colors to all new shapes");
            AddListItem(tipsList, "Animation", "Create a Timeline, add animations, and call .Play() to animate shapes");
            AddListItem(tipsList, "Drawing Tools", "Use the toolbar or press P/L/C/R to draw shapes directly on canvas with auto-generated code");
            AddListItem(tipsList, "Help Browser", "Select any class from the tree on the left to see its documentation");
            AddListItem(tipsList, "Find and Replace", "Press Ctrl+F to find, Ctrl+H to find and replace. Supports regex and project-wide search");
            AddListItem(tipsList, "NuGet Packages", "Use Tools > NuGet Package Manager to add external libraries like Newtonsoft.Json");
            AddListItem(tipsList, "Shape IDs", "Every shape has a unique Id property. Use Ctrl+G to zoom to a shape by its ID");
            AddListItem(tipsList, "Outliner", "The Outliner panel shows all shapes grouped by type. Click an ID to zoom to that shape");
            AddListItem(tipsList, "Outliner Hover", "Hover over shapes in the Outliner to highlight them on the canvas");
            AddListItem(tipsList, "Measuring Tool", "Press Ctrl+M to activate the Measuring Tape with AutoCAD-style snap points");
            AddListItem(tipsList, "Snap Settings", "Configure snap types (Endpoint, Midpoint, Center, etc.) in Settings > Application Settings");
            AddListItem(tipsList, "Highlight Settings", "Customize Outliner hover highlight color and opacity in Settings > Application Settings");
            AddListItem(tipsList, "Circumcircle", "Create a circle through 3 points: new VCircle(p1, p2, p3)");
            doc.Blocks.Add(tipsList);

            // Footer
            var footer = new Paragraph(new Run("Select a class from the tree on the left to view its documentation."))
            {
                FontSize = 12,
                FontStyle = FontStyles.Italic,
                Foreground = Brushes.Gray,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 30, 0, 0)
            };
            doc.Blocks.Add(footer);

            return doc;
        }

        private void AddWelcomeSectionHeader(FlowDocument doc, string text)
        {
            doc.Blocks.Add(new Paragraph(new Run(text))
            {
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.Teal,
                Margin = new Thickness(0, 15, 0, 8),
                BorderBrush = Brushes.LightGray,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(0, 0, 0, 5)
            });
        }

        private void AddListItem(List list, string title, string description)
        {
            var para = new Paragraph();
            para.Inlines.Add(new Run(title + ": ") { FontWeight = FontWeights.SemiBold });
            para.Inlines.Add(new Run(description));
            para.Margin = new Thickness(0, 2, 0, 2);
            list.ListItems.Add(new ListItem(para));
        }

        private void AddShortcutRow(TableRowGroup group, string shortcut, string description, bool isAlt)
        {
            var row = new TableRow();
            if (isAlt) row.Background = Brushes.WhiteSmoke;

            var keyCell = new TableCell(new Paragraph(new Run(shortcut) { FontFamily = new FontFamily("Consolas"), FontWeight = FontWeights.SemiBold }))
            {
                Padding = new Thickness(8, 4, 8, 4),
                BorderBrush = Brushes.LightGray,
                BorderThickness = new Thickness(0, 0, 0, 1)
            };

            var descCell = new TableCell(new Paragraph(new Run(description)))
            {
                Padding = new Thickness(8, 4, 8, 4),
                BorderBrush = Brushes.LightGray,
                BorderThickness = new Thickness(0, 0, 0, 1)
            };

            row.Cells.Add(keyCell);
            row.Cells.Add(descCell);
            group.Rows.Add(row);
        }

        private TableCell CreateHelpHeaderCell(string text)
        {
            return new TableCell(new Paragraph(new Run(text)) { FontWeight = FontWeights.Bold })
            {
                BorderBrush = Brushes.LightGray,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(8, 4, 8, 4)
            };
        }

        private void AddDrawingToolRow(TableRowGroup group, string shape, string method, string clicks, bool isAlt)
        {
            var row = new TableRow();
            if (isAlt) row.Background = Brushes.WhiteSmoke;

            var shapeCell = new TableCell(new Paragraph(new Run(shape) { FontWeight = FontWeights.SemiBold }))
            {
                Padding = new Thickness(8, 4, 8, 4),
                BorderBrush = Brushes.LightGray,
                BorderThickness = new Thickness(0, 0, 0, 1)
            };

            var methodCell = new TableCell(new Paragraph(new Run(method)))
            {
                Padding = new Thickness(8, 4, 8, 4),
                BorderBrush = Brushes.LightGray,
                BorderThickness = new Thickness(0, 0, 0, 1)
            };

            var clicksCell = new TableCell(new Paragraph(new Run(clicks)) { TextAlignment = TextAlignment.Center })
            {
                Padding = new Thickness(8, 4, 8, 4),
                BorderBrush = Brushes.LightGray,
                BorderThickness = new Thickness(0, 0, 0, 1)
            };

            row.Cells.Add(shapeCell);
            row.Cells.Add(methodCell);
            row.Cells.Add(clicksCell);
            group.Rows.Add(row);
        }
    }
}
