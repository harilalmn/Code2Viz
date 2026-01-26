using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace Code2Viz.Editor
{
    /// <summary>
    /// Provides XML documentation for types and members from XML doc files and source comments.
    /// </summary>
    public static class XmlDocumentationProvider
    {
        private static readonly Dictionary<string, XDocument?> _loadedDocs = new();
        private static readonly Dictionary<string, string> _memberDocCache = new();

        /// <summary>
        /// Gets the summary documentation for a type.
        /// </summary>
        public static string? GetTypeSummary(Type type)
        {
            var docId = $"T:{type.FullName}";
            return GetDocumentation(type.Assembly, docId);
        }

        /// <summary>
        /// Gets the summary documentation for a method.
        /// </summary>
        public static string? GetMethodSummary(MethodInfo method)
        {
            var docId = GetMethodDocId(method);
            return GetDocumentation(method.DeclaringType?.Assembly, docId);
        }

        /// <summary>
        /// Gets the summary documentation for a property.
        /// </summary>
        public static string? GetPropertySummary(PropertyInfo property)
        {
            var docId = $"P:{property.DeclaringType?.FullName}.{property.Name}";
            return GetDocumentation(property.DeclaringType?.Assembly, docId);
        }

        /// <summary>
        /// Gets the summary documentation for a field.
        /// </summary>
        public static string? GetFieldSummary(FieldInfo field)
        {
            var docId = $"F:{field.DeclaringType?.FullName}.{field.Name}";
            return GetDocumentation(field.DeclaringType?.Assembly, docId);
        }

        /// <summary>
        /// Gets parameter documentation for a method.
        /// </summary>
        public static string? GetParameterDoc(MethodInfo method, string paramName)
        {
            var docId = GetMethodDocId(method);
            var doc = GetXmlMember(method.DeclaringType?.Assembly, docId);
            if (doc == null) return null;

            var paramElement = doc.Elements("param").FirstOrDefault(e => e.Attribute("name")?.Value == paramName);
            return CleanXmlText(paramElement?.Value);
        }

        /// <summary>
        /// Gets return value documentation for a method.
        /// </summary>
        public static string? GetReturnDoc(MethodInfo method)
        {
            var docId = GetMethodDocId(method);
            var doc = GetXmlMember(method.DeclaringType?.Assembly, docId);
            if (doc == null) return null;

            var returnsElement = doc.Element("returns");
            return CleanXmlText(returnsElement?.Value);
        }

        /// <summary>
        /// Gets all documentation for a member including summary, params, returns.
        /// </summary>
        public static MemberDocumentation? GetMemberDocumentation(MemberInfo member)
        {
            string docId;
            Assembly? assembly;

            switch (member)
            {
                case MethodInfo method:
                    docId = GetMethodDocId(method);
                    assembly = method.DeclaringType?.Assembly;
                    break;
                case PropertyInfo prop:
                    docId = $"P:{prop.DeclaringType?.FullName}.{prop.Name}";
                    assembly = prop.DeclaringType?.Assembly;
                    break;
                case FieldInfo field:
                    docId = $"F:{field.DeclaringType?.FullName}.{field.Name}";
                    assembly = field.DeclaringType?.Assembly;
                    break;
                case Type type:
                    docId = $"T:{type.FullName}";
                    assembly = type.Assembly;
                    break;
                case ConstructorInfo ctor:
                    docId = GetConstructorDocId(ctor);
                    assembly = ctor.DeclaringType?.Assembly;
                    break;
                default:
                    return null;
            }

            var xmlMember = GetXmlMember(assembly, docId);
            if (xmlMember == null) return null;

            var result = new MemberDocumentation
            {
                Summary = CleanXmlText(xmlMember.Element("summary")?.Value),
                Returns = CleanXmlText(xmlMember.Element("returns")?.Value),
                Remarks = CleanXmlText(xmlMember.Element("remarks")?.Value)
            };

            foreach (var param in xmlMember.Elements("param"))
            {
                var name = param.Attribute("name")?.Value;
                if (!string.IsNullOrEmpty(name))
                {
                    result.Parameters[name] = CleanXmlText(param.Value) ?? "";
                }
            }

            return result;
        }

        private static string GetMethodDocId(MethodInfo method)
        {
            var sb = new StringBuilder();
            sb.Append("M:");
            sb.Append(method.DeclaringType?.FullName);
            sb.Append('.');
            sb.Append(method.Name);

            var parameters = method.GetParameters();
            if (parameters.Length > 0)
            {
                sb.Append('(');
                sb.Append(string.Join(",", parameters.Select(p => GetTypeDocId(p.ParameterType))));
                sb.Append(')');
            }

            return sb.ToString();
        }

        private static string GetConstructorDocId(ConstructorInfo ctor)
        {
            var sb = new StringBuilder();
            sb.Append("M:");
            sb.Append(ctor.DeclaringType?.FullName);
            sb.Append(".#ctor");

            var parameters = ctor.GetParameters();
            if (parameters.Length > 0)
            {
                sb.Append('(');
                sb.Append(string.Join(",", parameters.Select(p => GetTypeDocId(p.ParameterType))));
                sb.Append(')');
            }

            return sb.ToString();
        }

        private static string GetTypeDocId(Type type)
        {
            if (type.IsGenericType)
            {
                var genericDef = type.GetGenericTypeDefinition();
                var name = genericDef.FullName ?? genericDef.Name;
                var tickIndex = name.IndexOf('`');
                if (tickIndex > 0)
                    name = name.Substring(0, tickIndex);

                var args = type.GetGenericArguments();
                return $"{name}{{{string.Join(",", args.Select(GetTypeDocId))}}}";
            }

            if (type.IsArray)
            {
                return GetTypeDocId(type.GetElementType()!) + "[]";
            }

            return type.FullName ?? type.Name;
        }

        private static string? GetDocumentation(Assembly? assembly, string docId)
        {
            if (assembly == null) return null;

            // Check cache first
            if (_memberDocCache.TryGetValue(docId, out var cached))
                return cached;

            var member = GetXmlMember(assembly, docId);
            var summary = CleanXmlText(member?.Element("summary")?.Value);

            if (summary != null)
                _memberDocCache[docId] = summary;

            return summary;
        }

        private static XElement? GetXmlMember(Assembly? assembly, string docId)
        {
            if (assembly == null) return null;

            var doc = LoadXmlDocumentation(assembly);
            if (doc == null) return null;

            var members = doc.Descendants("member");
            return members.FirstOrDefault(m => m.Attribute("name")?.Value == docId);
        }

        private static XDocument? LoadXmlDocumentation(Assembly assembly)
        {
            var assemblyPath = assembly.Location;
            if (string.IsNullOrEmpty(assemblyPath))
                return null;

            // Check cache
            if (_loadedDocs.TryGetValue(assemblyPath, out var cached))
                return cached;

            // Try to find XML doc file
            var xmlPath = Path.ChangeExtension(assemblyPath, ".xml");

            // For framework assemblies, also check ref pack locations
            if (!File.Exists(xmlPath))
            {
                // Try to find in shared framework docs
                var frameworkDir = Path.GetDirectoryName(typeof(object).Assembly.Location);
                if (frameworkDir != null)
                {
                    var frameworkXml = Path.Combine(frameworkDir, Path.GetFileNameWithoutExtension(assemblyPath) + ".xml");
                    if (File.Exists(frameworkXml))
                        xmlPath = frameworkXml;
                }
            }

            XDocument? doc = null;
            if (File.Exists(xmlPath))
            {
                try
                {
                    doc = XDocument.Load(xmlPath);
                }
                catch
                {
                    // Failed to load XML doc
                }
            }

            _loadedDocs[assemblyPath] = doc;
            return doc;
        }

        private static string? CleanXmlText(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            // Remove excessive whitespace
            text = Regex.Replace(text, @"\s+", " ").Trim();

            // Handle <see cref="..."/> and <paramref name="..."/> tags
            text = Regex.Replace(text, @"<see\s+cref=""[^""]*\.([^""\.]+)""\s*/?>", "$1");
            text = Regex.Replace(text, @"<paramref\s+name=""([^""]+)""\s*/?>", "$1");
            text = Regex.Replace(text, @"<[^>]+>", ""); // Remove remaining XML tags

            return text;
        }

        /// <summary>
        /// Predefined documentation for common Code2Viz types.
        /// </summary>
        public static string? GetBuiltInDocumentation(string typeName, string? memberName = null)
        {
            // Provide documentation for Code2Viz geometry types
            if (memberName == null)
            {
                return typeName switch
                {
                    "VPoint" => "Represents a 2D point with X and Y coordinates.",
                    "VLine" => "Represents a line segment defined by start and end points.",
                    "VCircle" => "Represents a circle defined by center point and radius.",
                    "VArc" => "Represents an arc segment of a circle.",
                    "VRectangle" => "Represents a rectangle defined by position and dimensions.",
                    "VEllipse" => "Represents an ellipse defined by center and radii.",
                    "VPolygon" => "Represents a closed polygon defined by a list of vertices.",
                    "VPolyline" => "Represents an open polyline defined by a list of points.",
                    "VBezier" => "Represents a cubic Bezier curve with four control points.",
                    "VSpline" => "Represents a smooth curve through control points.",
                    "VText" => "Represents text at a specified position.",
                    "VArrow" => "Represents a line with an arrowhead.",
                    "VGroup" => "Groups multiple shapes together as a single entity.",
                    "VizConsole" => "Provides methods for outputting text to the console panel.",
                    "Timeline" => "Manages animation sequences and keyframes.",
                    _ => null
                };
            }

            // Member documentation
            var key = $"{typeName}.{memberName}";
            return key switch
            {
                "VPoint.X" => "The X coordinate of the point.",
                "VPoint.Y" => "The Y coordinate of the point.",
                "VLine.Start" => "The starting point of the line.",
                "VLine.End" => "The ending point of the line.",
                "VCircle.Center" => "The center point of the circle.",
                "VCircle.Radius" => "The radius of the circle.",
                "VRectangle.Width" => "The width of the rectangle.",
                "VRectangle.Height" => "The height of the rectangle.",
                "Shape.Draw" => "Renders the shape on the canvas.",
                "Shape.Color" => "The color of the shape's outline.",
                "Shape.FillColor" => "The fill color of the shape.",
                "Shape.LineWeight" => "The thickness of the shape's outline.",
                "VizConsole.Write" => "Writes text to the console without a newline.",
                "VizConsole.WriteLine" => "Writes text to the console followed by a newline.",
                _ => null
            };
        }
    }

    public class MemberDocumentation
    {
        public string? Summary { get; set; }
        public string? Returns { get; set; }
        public string? Remarks { get; set; }
        public Dictionary<string, string> Parameters { get; set; } = new();
    }
}
