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
                
                // Shapes
                { "Arc2D", "Represents a 2D arc defined by a center, radius, start angle, and end angle." },
                { "Circle2D", "Represents a 2D circle defined by a center point and a radius." },
                { "Rectangle2D", "Represents a 2D axis-aligned rectangle defined by a top-left corner, width, and height." },
                { "Polygon2D", "Represents a closed 2D polygon defined by a list of vertices." },
                { "Polyline2D", "Represents an open sequence of connected line segments." },
                { "Line2D", "Represents a straight line segment between two points." },
                { "Ellipse2D", "Represents a 2D ellipse defined by a center, X radius, and Y radius." },
                { "Point2D", "Represents a point in 2D Cartesian space (X, Y)." },
                { "Bezier2D", "Represents a 2D cubic Bezier curve." },
                { "Spline2D", "Represents a smooth spline curve passing through a series of points." },
                { "Text2D", "Represents text drawn at a specific position." },
                { "Group2D", "Represents a collection of shapes treated as a single unit." },
                
                // Interfaces/Helpers
                { "ICurve", "Interface for geometric shapes that can be treated as curves (measurable, divisible)." },
                { "IDrawable", "Interface for any object that can be drawn on the canvas." },
                { "GeometryHelper", "Static helper class providing common geometric algorithms like intersection and projection." },
                { "VPoint", "A value type representing a 2D point coordinates." },
                { "VXYZ", "Object representing coordinates in 3-dimensional space." },
                { "VLine3D", "Represents a line segment in 3D space." },
                { "VPlane", "Represents a plane in 3D space defined by origin and basis vectors." },
                { "VTransform", "Represents a 3D transformation (rotation, reflection)." },
                { "VBox", "Represents an oriented box in 3D space." },
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
                .Where(t => t.IsPublic && t.IsClass && t.Namespace != null && (t.Namespace.StartsWith("Code2Viz.Geometry") || t.Name.Contains("Helper")))
                .OrderBy(t => t.Name)
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
                
                // 3D V-Objects
                { "VXYZ", "let v = VXYZ(10.0, 20.0, 30.0)\nlet len = v.GetLength()" },
                { "VLine3D", "let start = VXYZ(0.0, 0.0, 0.0)\nlet end = VXYZ(100.0, 100.0, 100.0)\nlet line = VLine3D.CreateBound(start, end)" },
                { "VPlane", "let origin = VXYZ.Zero\nlet normal = VXYZ.BasisZ\nlet plane = VPlane.CreateByNormalAndOrigin(normal, origin)" },
                { "VTransform", "let t = VTransform.CreateRotation(VXYZ.BasisZ, 90.0)" },
                // VBox is abstract, but if there were a concrete implementation:
                { "VBox", "// VBox is abstract and cannot be instantiated directly.\n// Inherit from VBox to create concrete box types." },

                { "CanvasRenderer", "CanvasRenderer.Instance.Clear()\nCanvasRenderer.Instance.AddShape(someShape)" },
                { "VizConsole", "VizConsole.Log(\"Debug info\")\nVizConsole.Clear()" }
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
                { "VPoint", "VPoint p = new VPoint(100.0, 200.0);" },
                { "VXYZ", "VXYZ v = new VXYZ(10.0, 20.0, 30.0);\ndouble len = v.GetLength();" },
                { "VLine", "VPoint start = new VPoint(0, 0);\nVPoint end = new VPoint(100, 100);\nVLine line = new VLine(start, end);" },
                { "VLine3D", "VXYZ start = new VXYZ(0.0, 0.0, 0.0);\nVXYZ end = new VXYZ(100.0, 100.0, 100.0);\nVLine3D line = VLine3D.CreateBound(start, end);" },
                { "VPlane", "VXYZ origin = VXYZ.Zero;\nVXYZ normal = VXYZ.BasisZ;\nVPlane plane = VPlane.CreateByNormalAndOrigin(normal, origin);" },
                { "VTransform", "VTransform t = VTransform.CreateRotation(VXYZ.BasisZ, 90.0);" },
                { "VBox", "// VBox is abstract.\n// Usage depends on concrete implementation." },
                { "VArrow", "// From two points\nVArrow a = new VArrow(new VPoint(10, 10), new VPoint(100, 10));\na.Draw();\n\n// From start point, direction, and length\nVArrow a2 = new VArrow(new VPoint(0, 0), VXYZ.BasisX, 50);\na2.Draw();" },

                { "CanvasRenderer", "CanvasRenderer.Instance.Clear();\nCanvasRenderer.Instance.AddShape(someShape);" },
                { "VizConsole", "VizConsole.Log(\"Debug info\");\nVizConsole.Clear();" }
            };
        }
    }
}
