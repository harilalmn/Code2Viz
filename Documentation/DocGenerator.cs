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

        public DocGenerator()
        {
            _assembly = Assembly.GetExecutingAssembly();
            InitializeSummaries();
            InitializeFSharpSamples();
            InitializeCSharpSamples();
        }

        private void InitializeSummaries()
        {
            _summaries = new Dictionary<string, string>
            {
                { "Code2Viz", "Root namespace for the Code2Viz application." },
                { "Code2Viz.Geometry", "Contains classes and interfaces for 2D geometric shapes and operations." },
                { "Code2Viz.Editor", "Contains classes related to the code editor, including formatting, completion, and snippets." },

                // Base classes
                { "Shape", "Abstract base class for all drawable shapes. Provides common properties like StrokeColor, FillColor, StrokeThickness, and animation properties (DrawFactor, OffsetX, OffsetY, RotationAngle). Also defines common methods: Draw(), Clone(), Move(), Rotate(), Flip(), Scale(), GetBounds(), Contains(), DistanceTo()." },
                { "IDrawable", "Interface for any object that can be drawn on the canvas. Defines Draw() method and styling properties." },
                { "ICurve", "Interface for geometric shapes that can be treated as curves (measurable, divisible)." },

                // Shapes
                { "VArc", "Represents a 2D arc defined by a center point, radius, start angle, and end angle (in degrees). The arc is drawn counter-clockwise from start to end angle." },
                { "VCircle", "Represents a 2D circle defined by a center point and a radius. Can be filled and/or stroked." },
                { "VRectangle", "Represents a 2D axis-aligned rectangle defined by a corner point (bottom-left), width, and height." },
                { "VPolygon", "Represents a closed 2D polygon defined by a list of vertices. Automatically closes the shape by connecting last point to first." },
                { "VPolyline", "Represents an open sequence of connected line segments. Unlike polygon, does not close automatically." },
                { "VLine", "Represents a straight line segment between two points. The most basic geometric primitive." },
                { "VEllipse", "Represents a 2D ellipse defined by a center point, X radius (horizontal), and Y radius (vertical)." },
                { "VPoint", "Represents a point in 2D Cartesian space (X, Y). Can be drawn as a small dot on the canvas." },
                { "VBezier", "Represents a 2D cubic Bezier curve defined by four control points: start, control1, control2, and end." },
                { "VSpline", "Represents a smooth Catmull-Rom spline curve passing through a series of points." },
                { "VText", "Represents text drawn at a specific position. Supports font size (Height property) and styling." },
                { "VGroup", "Represents a collection of shapes treated as a single unit. Supports group transformations (Move, Rotate, Scale)." },
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

                // Support classes
                { "VXYZ", "Represents a 3D vector or point with X, Y, Z coordinates. Provides vector operations like Add, Subtract, CrossProduct, DotProduct, Normalize, GetLength. Also has static properties BasisX, BasisY, BasisZ, Zero." },
                { "VPlane", "Represents a plane in 3D space defined by origin and basis vectors. Used for coordinate transformations." },
                { "VTransform", "Represents a 3D transformation matrix for rotation and reflection operations." },
                { "VCoordinateSystem", "Represents a 3D coordinate system with origin and orthonormal basis vectors (X, Y, Z axes)." },
                { "GeometryHelper", "Static helper class providing common geometric algorithms like intersection, projection, distance calculations, and angle measurements." },
                { "ShapeDefaults", "Static class holding global default settings for shapes (GlobalStrokeColor, GlobalFillColor, GlobalStrokeThickness). These are populated from Project Settings." },

                // Animation
                { "Code2Viz.Animation", "Contains classes for animating shapes over time using a timeline-based system." },
                { "Timeline", "Manages a collection of shapes and animations, controlling playback timing and state. Supports looping (Repeat), speed control, and multiple concurrent animations." },
                { "Animation", "Abstract base class for all animations. Defines Target shape, StartTime, Duration, and EasingFunction. Subclasses implement the Apply() method." },
                { "DrawAnimation", "Animates the DrawFactor property to progressively draw a shape from 0% to 100%. Creates a 'drawing' effect where shapes appear to be drawn over time." },
                { "MoveAnimation", "Animates moving a shape by a specified displacement vector over time. The shape smoothly translates from its original position." },
                { "RotateAnimation", "Animates rotating a shape around a specified pivot point by a given angle in degrees. Useful for spinning or orbiting effects." },
                { "FlipAnimation", "Animates flipping (mirroring) a shape across a specified axis line. Creates a reflection transformation over time." },
                { "EasingFunctions", "Static class providing common easing functions for smooth animations: Linear, EaseInQuad, EaseOutQuad, EaseInOutQuad, EaseInCubic, EaseOutCubic, EaseInOutCubic." },
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
                doc.Blocks.Add(GenerateMemberTable(dtors));
            }

            // Properties
            var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            if (props.Length > 0)
            {
                AddSectionHeader(doc, "Properties");
                doc.Blocks.Add(GenerateMemberTable(props));
            }

            // Methods
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName && m.DeclaringType != typeof(object)) // Exclude getter/setter internal methods and Object methods
                .ToArray();

            if (methods.Length > 0)
            {
                AddSectionHeader(doc, "Methods");
                doc.Blocks.Add(GenerateMemberTable(methods));
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

        private Table GenerateMemberTable(MemberInfo[] members)
        {
            var table = new Table();
            table.CellSpacing = 0;
            table.BorderBrush = Brushes.LightGray;
            table.BorderThickness = new Thickness(1);
            
            // Use fixed widths to ensure stability and readability
            table.Columns.Add(new TableColumn { Width = new GridLength(220) }); // Name
            table.Columns.Add(new TableColumn { Width = new GridLength(500) }); // Description

            var rowGroup = new TableRowGroup();
            
            // Header
            var headerRow = new TableRow();
            headerRow.Background = Brushes.AliceBlue;
            headerRow.Cells.Add(CreateHeaderCell("Name"));
            headerRow.Cells.Add(CreateHeaderCell("Signature / Description"));
            rowGroup.Rows.Add(headerRow);

            bool isAlt = false;
            foreach (var member in members)
            {
                var row = new TableRow();
                if (isAlt) row.Background = Brushes.WhiteSmoke;
                isAlt = !isAlt;
                
                // Name
                var nameText = new Run(member.Name) { FontWeight = FontWeights.Bold, Foreground = Brushes.DarkBlue };
                var nameCell = new TableCell(new Paragraph(nameText)) { Padding = new Thickness(5), BorderBrush = Brushes.LightGray, BorderThickness = new Thickness(0,0,0,1) };
                row.Cells.Add(nameCell);

                // Description/Signature
                string sig = "";
                if (member is MethodInfo mi)
                {
                    var paramStr = string.Join(", ", mi.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                    sig = $"{mi.ReturnType.Name} ({paramStr})";
                }
                else if (member is PropertyInfo pi)
                {
                    sig = pi.PropertyType.Name;
                }
                else if (member is ConstructorInfo ci)
                {
                    var paramStr = string.Join(", ", ci.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                    if (string.IsNullOrEmpty(paramStr)) paramStr = "()";
                    sig = paramStr;
                }

                var sigPara = new Paragraph(new Run(sig));
                sigPara.FontFamily = new FontFamily("Consolas");
                sigPara.FontSize = 12; // Slightly larger
                var sigCell = new TableCell(sigPara) { Padding = new Thickness(5), BorderBrush = Brushes.LightGray, BorderThickness = new Thickness(0,0,0,1) };
                
                row.Cells.Add(sigCell);

                rowGroup.Rows.Add(row);
            }

            table.RowGroups.Add(rowGroup);
            return table;
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
                { "VLine", "let start = VPoint(0.0, 0.0)\nlet endP = VPoint(200.0, 200.0)\nlet line = VLine(start, endP)\nline.StrokeColor <- \"#00FF00\"\nline.StrokeThickness <- 2.0\nline.Draw()" },
                { "VCircle", "let center = VPoint(300.0, 200.0)\nlet radius = 50.0\nlet circle = VCircle(center, radius)\ncircle.FillColor <- \"Blue\"\ncircle.Draw()" },
                { "VRectangle", "let rect = VRectangle(VPoint(50.0, 50.0), 150.0, 100.0)\nrect.FillColor <- \"#800000FF\"\nrect.Draw()" },
                { "VEllipse", "let ellipse = VEllipse(VPoint(400.0, 300.0), 80.0, 40.0)\nellipse.StrokeThickness <- 3.0\nellipse.Draw()" },
                { "VArc", "let arc = VArc(VPoint(200.0, 200.0), 100.0, 0.0, 180.0)\narc.Draw()" },
                { "VPolygon", "let pts = [| VPoint(100.0,100.0); VPoint(200.0,100.0); VPoint(150.0,200.0) |]\nlet poly = VPolygon(pts)\npoly.FillColor <- \"Yellow\"\npoly.Draw()" },
                { "VPolyline", "let pts = [| VPoint(100.0,300.0); VPoint(150.0,350.0); VPoint(200.0,300.0) |]\nlet line = VPolyline(pts)\nline.Draw()" },
                { "VBezier", "let b = VBezier(VPoint(0.0,0.0), VPoint(0.0,100.0), VPoint(100.0,100.0), VPoint(100.0,0.0))\nb.Draw()" },
                { "VSpline", "let pts = [| VPoint(0.0,0.0); VPoint(50.0,50.0); VPoint(100.0,0.0) |]\nlet s = VSpline(pts)\ns.Draw()" },
                { "VText", "let t = VText(VPoint(50.0, 50.0), \"Hi\")\nt.Height <- 40.0\nt.Draw()" },
                { "VArrow", "// From two points\nlet a = VArrow(VPoint(10.0, 10.0), VPoint(100.0, 10.0))\na.Draw()\n\n// From start point, direction, and length\nlet a2 = VArrow(VPoint(0.0, 0.0), VXYZ.BasisX, 50.0)\na2.Draw()" },
                { "VGroup", "let g = VGroup()\ng.Add(VCircle(VPoint(0.0,0.0), 10.0))\ng.Move(100.0, 100.0)\ng.Draw()" },
                
                // Support classes
                { "VXYZ", "let v = VXYZ(10.0, 20.0, 30.0)\nlet len = v.GetLength()" },
                { "VPlane", "let origin = VXYZ.Zero\nlet normal = VXYZ.BasisZ\nlet plane = VPlane.CreateByNormalAndOrigin(normal, origin)" },
                { "VTransform", "let t = VTransform.CreateRotation(VXYZ.BasisZ, 90.0)" },

                { "CanvasRenderer", "CanvasRenderer.Instance.Clear()\nCanvasRenderer.Instance.AddShape(someShape)" },
                { "VizConsole", "VizConsole.Log(\"Debug info\")\nVizConsole.Log($\"Value: {someVariable}\")" },

                // Animation
                { "Timeline", @"// Create shapes
let line = VLine(0.0, 0.0, 100.0, 50.0)
let circle = VCircle(50.0, 50.0, 30.0)

// Create timeline with shapes (use array for F#)
let shapes = [| line :> Shape; circle :> Shape |]
let timeline = Timeline(shapes)
timeline.Duration <- 5.0
timeline.Repeat <- true

// Add animations
timeline.AddAnimation(DrawAnimation(line, 0.0, 2.0))
timeline.AddAnimation(DrawAnimation(circle, 0.5, 2.0))
timeline.AddAnimation(MoveAnimation(line, VXYZ(0.0, 50.0, 0.0), 2.0, 2.0))

// Start playback
timeline.Play()" },

                { "DrawAnimation", @"// Animates shape drawing from 0% to 100%
let line = VLine(0.0, 0.0, 100.0, 0.0)
let timeline = Timeline([| line :> Shape |])

// Draw the line over 2 seconds starting at t=0
timeline.AddAnimation(DrawAnimation(line, 0.0, 2.0))
timeline.Play()" },

                { "MoveAnimation", @"// Animates moving a shape by a vector
let circle = VCircle(0.0, 0.0, 30.0)
let timeline = Timeline([| circle :> Shape |])

// Move circle by (100, 50) over 3 seconds, starting at t=1
timeline.AddAnimation(MoveAnimation(circle, VXYZ(100.0, 50.0, 0.0), 1.0, 3.0))
timeline.Play()" },

                { "RotateAnimation", @"// Animates rotating a shape around a pivot
let rect = VRectangle(0.0, 0.0, 50.0, 30.0)
let pivot = VPoint(25.0, 15.0)
let timeline = Timeline([| rect :> Shape |])

// Rotate 360 degrees over 4 seconds
timeline.AddAnimation(RotateAnimation(rect, pivot, 360.0, 0.0, 4.0))
timeline.Play()" },

                { "FlipAnimation", @"// Animates flipping a shape across a mirror axis
let triangle = VPolygon(VPoint(0.0,0.0), VPoint(50.0,0.0), VPoint(25.0,50.0))
let mirrorAxis = VLine(25.0, -10.0, 25.0, 60.0)
let timeline = Timeline([| triangle :> Shape |])

// Flip across the axis over 2 seconds
timeline.AddAnimation(FlipAnimation(triangle, mirrorAxis, 0.0, 2.0))
timeline.Play()" },

                { "EasingFunctions", @"// Apply easing to any animation for smooth motion
let circle = VCircle(0.0, 0.0, 30.0)
let timeline = Timeline()

let moveAnim = MoveAnimation(circle, VXYZ(200.0, 0.0, 0.0), 0.0, 3.0)

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

timeline.AddAnimation(moveAnim)
timeline.Play()" }
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
p.StrokeColor = ""Red"";
p.Draw();" },

                { "VLine", @"// Create a line from two points
var line = new VLine(new VPoint(0, 0), new VPoint(100, 50));
line.StrokeColor = ""Cyan"";
line.StrokeThickness = 2;
line.Draw();

// Or using coordinates directly
var line2 = new VLine(0, 100, 150, 100);
line2.Draw();" },

                { "VCircle", @"// Create a circle with center and radius
var circle = new VCircle(new VPoint(50, 50), 30);
circle.StrokeColor = ""Yellow"";
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
rect.StrokeColor = ""LimeGreen"";
rect.FillColor = ""#2000FF00"";
rect.Draw();

// Or using coordinates
var rect2 = new VRectangle(100, 0, 60, 40);
rect2.Draw();" },

                { "VEllipse", @"// Create an ellipse with center and radii
var ellipse = new VEllipse(new VPoint(100, 100), 60, 30);
ellipse.StrokeColor = ""Magenta"";
ellipse.StrokeThickness = 2;
ellipse.Draw();" },

                { "VArc", @"// Create an arc (center, radius, startAngle, endAngle)
var arc = new VArc(new VPoint(50, 50), 40, 0, 270);
arc.StrokeColor = ""Orange"";
arc.StrokeThickness = 3;
arc.Draw();

// Angles are in degrees, counter-clockwise from positive X-axis" },

                { "VPolygon", @"// Create a triangle
var triangle = new VPolygon(
    new VPoint(0, 0),
    new VPoint(100, 0),
    new VPoint(50, 80)
);
triangle.StrokeColor = ""LimeGreen"";
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
polyline.StrokeColor = ""Cyan"";
polyline.Draw();" },

                { "VBezier", @"// Create a cubic Bezier curve (4 control points)
var bezier = new VBezier(
    new VPoint(0, 0),      // Start point
    new VPoint(30, 80),    // Control point 1
    new VPoint(70, 80),    // Control point 2
    new VPoint(100, 0)     // End point
);
bezier.StrokeColor = ""Magenta"";
bezier.StrokeThickness = 2;
bezier.Draw();" },

                { "VSpline", @"// Create a smooth spline through points
var spline = new VSpline(
    new VPoint(0, 0),
    new VPoint(30, 40),
    new VPoint(60, 20),
    new VPoint(100, 50)
);
spline.StrokeColor = ""Cyan"";
spline.Draw();" },

                { "VText", @"// Create text at a position
var text = new VText(new VPoint(50, 50), ""Hello World"");
text.Height = 24;
text.StrokeColor = ""White"";
text.Draw();" },

                { "VArrow", @"// Create an arrow from two points
var arrow = new VArrow(new VPoint(0, 0), new VPoint(100, 0));
arrow.StrokeColor = ""Orange"";
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

                { "VGroup", @"// Create a group of shapes
var group = new VGroup();
group.Add(new VCircle(0, 0, 20));
group.Add(new VLine(-30, 0, 30, 0));
group.Add(new VLine(0, -30, 0, 30));

// Transform the whole group
group.Move(new VXYZ(100, 100, 0));
group.Rotate(new VPoint(100, 100), 45);
group.Draw();" },

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
ShapeDefaults.GlobalStrokeColor = ""Cyan"";
ShapeDefaults.GlobalFillColor = ""#20FFFFFF"";
ShapeDefaults.GlobalStrokeThickness = 2.0;

// Now all new shapes use these defaults
var circle = new VCircle(0, 0, 50);  // Uses Cyan stroke
circle.Draw();

// Reset to original defaults
ShapeDefaults.Reset();" },

                { "Shape", @"// Shape is the base class for all drawable shapes
// Common properties available on all shapes:

shape.StrokeColor = ""Cyan"";           // Outline color
shape.FillColor = ""Transparent"";      // Fill color
shape.StrokeThickness = 2.0;           // Line thickness

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

                // Animation
                { "Timeline", @"// Create shapes
var line = new VLine(0, 0, 100, 50);
var circle = new VCircle(50, 50, 30);

// Create timeline with shapes
var shapes = new List<Shape> { line, circle };
var timeline = new Timeline(shapes);
timeline.Duration = 5.0;
timeline.Repeat = true;

// Add animations
timeline.AddAnimation(new DrawAnimation(line, 0.0, 2.0));
timeline.AddAnimation(new DrawAnimation(circle, 0.5, 2.0));
timeline.AddAnimation(new MoveAnimation(line, new VXYZ(0, 50, 0), 2.0, 2.0));

// Start playback
timeline.Play();" },

                { "DrawAnimation", @"// Animates shape drawing from 0% to 100%
var line = new VLine(0, 0, 100, 0);
var timeline = new Timeline(new[] { line });

// Draw the line over 2 seconds starting at t=0
timeline.AddAnimation(new DrawAnimation(line, startTime: 0.0, duration: 2.0));
timeline.Play();" },

                { "MoveAnimation", @"// Animates moving a shape by a vector
var circle = new VCircle(0, 0, 30);
var timeline = new Timeline(new[] { circle });

// Move circle by (100, 50) over 3 seconds, starting at t=1
timeline.AddAnimation(new MoveAnimation(circle, new VXYZ(100, 50, 0), startTime: 1.0, duration: 3.0));
timeline.Play();" },

                { "RotateAnimation", @"// Animates rotating a shape around a pivot
var rect = new VRectangle(0, 0, 50, 30);
var pivot = new VPoint(25, 15); // center of rectangle
var timeline = new Timeline(new[] { rect });

// Rotate 360 degrees over 4 seconds
timeline.AddAnimation(new RotateAnimation(rect, pivot, angleDegrees: 360.0, startTime: 0.0, duration: 4.0));
timeline.Play();" },

                { "FlipAnimation", @"// Animates flipping a shape across a mirror axis
var triangle = new VPolygon(new VPoint(0,0), new VPoint(50,0), new VPoint(25,50));
var mirrorAxis = new VLine(25, -10, 25, 60); // vertical line
var timeline = new Timeline(new[] { triangle });

// Flip across the axis over 2 seconds
timeline.AddAnimation(new FlipAnimation(triangle, mirrorAxis, startTime: 0.0, duration: 2.0));
timeline.Play();" },

                { "EasingFunctions", @"// Apply easing to any animation for smooth motion
var circle = new VCircle(0, 0, 30);
var timeline = new Timeline();

var moveAnim = new MoveAnimation(circle, new VXYZ(200, 0, 0), 0, 3);

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

timeline.AddAnimation(moveAnim);
timeline.Play();" }
            };
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
            AddListItem(featuresList, "Animation System", "Create timeline-based animations with draw, move, rotate, and flip effects");
            AddListItem(featuresList, "Interactive Canvas", "Zoom with mouse wheel, pan with middle-click, toggle grid display");
            AddListItem(featuresList, "Export Options", "Save your visualizations as PNG images or animated GIFs");
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
            circle.StrokeColor = ""Cyan"";
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
            // Canvas & Tools
            AddShortcutRow(rowGroup, "Mouse Wheel", "Zoom canvas", false);
            AddShortcutRow(rowGroup, "Middle Click", "Pan canvas", true);
            AddShortcutRow(rowGroup, "Ctrl+G", "Zoom to shape by ID", false);
            AddShortcutRow(rowGroup, "Ctrl+M", "Toggle Measuring Tape tool", true);
            AddShortcutRow(rowGroup, "Esc", "Cancel current tool/operation", false);
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
            AddListItem(tipsList, "ShapeDefaults", "Set ShapeDefaults.GlobalStrokeColor to apply colors to all new shapes");
            AddListItem(tipsList, "Animation", "Create a Timeline, add animations, and call .Play() to animate shapes");
            AddListItem(tipsList, "Help Browser", "Select any class from the tree on the left to see its documentation");
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
    }
}
