using System.Numerics;
using System.Reflection;
using System.Linq;

namespace Code2Viz.Editor;

/// <summary>
/// Uses reflection to get members of any type for autocomplete.
/// </summary>
public static class TypeInspector
{
    // Cache to avoid repeated reflection
    private static readonly Dictionary<string, List<(string Name, string Description, CompletionKind Kind)>> _typeCache = new();

    // Known type mappings for common types
    private static readonly Dictionary<string, Type> _knownTypes = new()
    {
        // System types
        ["Math"] = typeof(Math),
        ["Console"] = typeof(System.Console),
        ["Convert"] = typeof(Convert),
        ["String"] = typeof(string),
        ["string"] = typeof(string),
        ["Int32"] = typeof(int),
        ["int"] = typeof(int),
        ["Double"] = typeof(double),
        ["double"] = typeof(double),
        ["Boolean"] = typeof(bool),
        ["bool"] = typeof(bool),
        ["DateTime"] = typeof(DateTime),
        ["TimeSpan"] = typeof(TimeSpan),
        ["Random"] = typeof(Random),
        ["Environment"] = typeof(Environment),
        ["Path"] = typeof(System.IO.Path),
        ["File"] = typeof(System.IO.File),
        ["Directory"] = typeof(System.IO.Directory),
        ["Guid"] = typeof(Guid),
        ["Object"] = typeof(object),
        ["object"] = typeof(object),
        ["Enumerable"] = typeof(Enumerable),



        // Viz2d types
        ["VPoint"] = typeof(Geometry.VPoint),
        ["VXYZ"] = typeof(Geometry.VXYZ),
        ["VLine3D"] = typeof(Geometry.VLine3D),
        ["VPlane"] = typeof(Geometry.VPlane),
        ["VTransform"] = typeof(Geometry.VTransform),
        ["VBox"] = typeof(Geometry.VBox),
        ["VCircle"] = typeof(Geometry.VCircle),
        ["VArc"] = typeof(Geometry.VArc),
        ["VRectangle"] = typeof(Geometry.VRectangle),
        ["VEllipse"] = typeof(Geometry.VEllipse),
        ["VPolygon"] = typeof(Geometry.VPolygon),
        ["VPolyline"] = typeof(Geometry.VPolyline),
        ["VText"] = typeof(Geometry.VText),
        ["VBezier"] = typeof(Geometry.VBezier),
        ["VSpline"] = typeof(Geometry.VSpline),
        ["VArrow"] = typeof(Geometry.VArrow),
        ["VDimension"] = typeof(Geometry.VDimension),
        ["VGroup"] = typeof(Geometry.VGroup),
        ["VGroup"] = typeof(Geometry.VGroup),
        ["VizConsole"] = typeof(Console.VizConsole),

        // Generic Collections
        ["List"] = typeof(System.Collections.Generic.List<>),
        ["Dictionary"] = typeof(System.Collections.Generic.Dictionary<,>),
        ["HashSet"] = typeof(System.Collections.Generic.HashSet<>),
        ["Queue"] = typeof(System.Collections.Generic.Queue<>),
        ["Stack"] = typeof(System.Collections.Generic.Stack<>),
        ["LinkedList"] = typeof(System.Collections.Generic.LinkedList<>),
    };

    // Types to add for general completion
    private static readonly (string Name, string Description)[] CommonTypes =
    {
        // Viz2d types
        ("VPoint", "2D point with X, Y coordinates"),
        ("VXYZ", "3D vector with X, Y, Z coordinates"),
        ("VLine3D", "Line between two points in 3D"),
        ("VPlane", "Infinite plane in 3D"),
        ("VTransform", "3D Transformation matrix"),
        ("VBox", "Abstract base class for 3D boxes"),
        ("VCircle", "Circle with center and radius"),
        ("VArc", "Arc with center, radius, start and end angles"),
        ("VRectangle", "Rectangle with position, width and height"),
        ("VEllipse", "Ellipse with center, radiusX and radiusY"),
        ("VPolygon", "Closed polygon from list of points"),
        ("VPolyline", "Open polyline from list of points"),
        ("VText", "Text with location, content, color and height"),
        ("VBezier", "Cubic Bezier curve with 4 control points"),
        ("VSpline", "Catmull-Rom spline through control points"),
        ("VArrow", "Arrow with configurable arrowhead"),
        ("VDimension", "Dimension line showing distance between points"),
        ("VGroup", "Group of shapes for batch transformations"),
        ("VizConsole", "Console output with line tracking"),

        // Common System types
        ("Math", "System.Math - Mathematical functions"),
        ("Console", "System.Console - Console I/O"),
        ("Convert", "System.Convert - Type conversion"),
        ("String", "System.String - String manipulation"),
        ("List", "System.Collections.Generic.List<T>"),
        ("Dictionary", "System.Collections.Generic.Dictionary<K,V>"),
        ("Random", "System.Random - Random number generator"),
        ("DateTime", "System.DateTime - Date and time"),
        ("TimeSpan", "System.TimeSpan - Time interval"),
        ("Environment", "System.Environment - Environment info"),
        ("Path", "System.IO.Path - Path manipulation"),
        ("File", "System.IO.File - File operations"),
        ("Directory", "System.IO.Directory - Directory operations"),
        ("Guid", "System.Guid - Unique identifiers"),
        ("Enumerable", "System.Linq.Enumerable - LINQ methods"),
    };

    /// <summary>
    /// Get all common types for general completion.
    /// </summary>
    public static IEnumerable<(string Name, string Description)> GetCommonTypes() => CommonTypes;

    /// <summary>
    /// Try to resolve a type name to an actual Type.
    /// </summary>
    public static Type? ResolveType(string typeName)
    {
        if (string.IsNullOrEmpty(typeName))
            return null;

        // Handle generic syntax: List<T> -> List
        // We strip the generic parameters to resolve the open generic type definition
        // which gives us the members (Add, Clear, etc)
        string baseTypeName = typeName;
        if (typeName.Contains('<'))
        {
            var tickIndex = typeName.IndexOf('<');
            baseTypeName = typeName.Substring(0, tickIndex);
        }

        // Check known types first (using base name)
        if (_knownTypes.TryGetValue(baseTypeName, out var knownType))
            return knownType;

        // Try common System namespaces with Type.GetType
        var systemTypes = new[]
        {
            $"System.{typeName}",
            $"System.Collections.Generic.{typeName}",
            $"System.IO.{typeName}",
            $"System.Text.{typeName}",
            $"System.Numerics.{typeName}",
            $"System.Numerics.{typeName}",
            typeName,
            // Try base name if different
            baseTypeName != typeName ? $"System.Collections.Generic.{baseTypeName}" : null,
            baseTypeName != typeName ? $"System.{baseTypeName}" : null
        };

        foreach (var fullName in systemTypes)
        {
            if (fullName == null)
                continue;
            var type = Type.GetType(fullName);
            if (type != null)
                return type;
        }

        // Search all loaded assemblies for the type
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                // Try exact type name match
                var type = assembly.GetTypes()
                    .FirstOrDefault(t => t.Name == typeName && t.IsPublic);
                if (type != null)
                    return type;
            }
            catch
            {
                // Skip assemblies that can't be reflected
            }
        }

        return null;
    }

    /// <summary>
    /// Get members of a type using reflection.
    /// </summary>
    public static List<(string Name, string Description, CompletionKind Kind)> GetTypeMembers(string typeName)
    {
        if (string.IsNullOrEmpty(typeName))
            return new List<(string, string, CompletionKind)>();

        // Check cache first
        if (_typeCache.TryGetValue(typeName, out var cached))
            return cached;

        var type = ResolveType(typeName);
        if (type == null)
            return new List<(string, string, CompletionKind)>();

        var members = GetTypeMembersFromReflection(type);
        _typeCache[typeName] = members;
        return members;
    }

    /// <summary>
    /// Get members from a Type using reflection.
    /// </summary>
    public static List<(string Name, string Description, CompletionKind Kind)> GetTypeMembersFromReflection(Type type)
    {
        var members = new List<(string Name, string Description, CompletionKind Kind)>();
        var seenNames = new HashSet<string>();

        // Get properties
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
        foreach (var prop in properties)
        {
            if (seenNames.Add(prop.Name))
            {
                var isStatic = prop.GetMethod?.IsStatic ?? false;
                var staticStr = isStatic ? " (static)" : "";
                members.Add((prop.Name, $"{GetTypeName(prop.PropertyType)}{staticStr}", CompletionKind.Property));
            }
        }

        // Get fields (for constants like Math.PI)
        var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
        foreach (var field in fields)
        {
            if (seenNames.Add(field.Name))
            {
                var isConst = field.IsLiteral;
                var isStatic = field.IsStatic;
                var modifier = isConst ? " (const)" : isStatic ? " (static)" : "";
                members.Add((field.Name, $"{GetTypeName(field.FieldType)}{modifier}", CompletionKind.Property));
            }
        }

        // Get methods (excluding property accessors and object methods)
        var objectMethods = new HashSet<string> { "GetType", "ToString", "Equals", "GetHashCode", "MemberwiseClone", "Finalize" };
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .Where(m => !m.IsSpecialName) // Exclude property accessors
            .Where(m => !objectMethods.Contains(m.Name) || type == typeof(object))
            .GroupBy(m => m.Name)
            .Select(g => g.First()); // Take first overload

        foreach (var method in methods)
        {
            if (seenNames.Add(method.Name))
            {
                var isStatic = method.IsStatic;
                var staticStr = isStatic ? " (static)" : "";
                var returnType = GetTypeName(method.ReturnType);
                var parameters = string.Join(", ", method.GetParameters().Select(p => $"{GetTypeName(p.ParameterType)} {p.Name}"));
                var description = $"{returnType} {method.Name}({parameters}){staticStr}";
                members.Add((method.Name, description, CompletionKind.Method));
            }
        }

        // Sort: Properties first, then methods, alphabetically within each group
        return members
            .OrderBy(m => m.Kind == CompletionKind.Method ? 1 : 0)
            .ThenBy(m => m.Name)
            .ToList();
    }

    /// <summary>
    /// Get a friendly type name.
    /// </summary>
    private static string GetTypeName(Type type)
    {
        if (type == typeof(void)) return "void";
        if (type == typeof(int)) return "int";
        if (type == typeof(double)) return "double";
        if (type == typeof(float)) return "float";
        if (type == typeof(bool)) return "bool";
        if (type == typeof(string)) return "string";
        if (type == typeof(object)) return "object";
        if (type == typeof(byte)) return "byte";
        if (type == typeof(char)) return "char";
        if (type == typeof(long)) return "long";
        if (type == typeof(decimal)) return "decimal";

        if (type.IsGenericType)
        {
            var name = type.Name;
            var tickIndex = name.IndexOf('`');
            if (tickIndex > 0) name = name.Substring(0, tickIndex);
            var args = string.Join(", ", type.GetGenericArguments().Select(GetTypeName));
            return $"{name}<{args}>";
        }

        return type.Name;
    }

    /// <summary>
    /// Check if a type name is one of our explicitly known types.
    /// This is used to prevent user code from shadowing important types.
    /// </summary>
    public static bool IsKnownType(string typeName)
    {
        return !string.IsNullOrEmpty(typeName) && _knownTypes.ContainsKey(typeName);
    }

    /// <summary>
    /// Check if a type name is a known static class.
    /// </summary>
    public static bool IsKnownStaticClass(string typeName)
    {
        var type = ResolveType(typeName);
        if (type == null) return false;

        return type.IsAbstract && type.IsSealed; // Static classes are abstract and sealed
    }

    /// <summary>
    /// Check if a string represents a namespace.
    /// </summary>
    public static bool IsNamespace(string name)
    {
        if (string.IsNullOrEmpty(name))
            return false;

        try
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (assembly.GetTypes().Any(t => t.Namespace == name ||
                        (t.Namespace != null && t.Namespace.StartsWith(name + "."))))
                        return true;
                }
                catch
                {
                    // Skip assemblies that can't be reflected
                }
            }
        }
        catch
        {
            // Ignore any assembly enumeration errors
        }
        return false;
    }

    /// <summary>
    /// Get types and sub-namespaces within a namespace.
    /// </summary>
    public static List<(string Name, string Description, CompletionKind Kind)> GetNamespaceMembers(string namespaceName)
    {
        var members = new List<(string Name, string Description, CompletionKind Kind)>();
        
        if (string.IsNullOrEmpty(namespaceName))
            return members;

        var seenNames = new HashSet<string>();
        var subNamespaces = new HashSet<string>();

        try
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        if (type.Namespace == null || !type.IsPublic)
                            continue;

                        // Check for types directly in this namespace
                        if (type.Namespace == namespaceName)
                        {
                            var typeName = type.Name;
                            // Skip generic type names with backtick
                            if (typeName.Contains('`'))
                                typeName = typeName.Substring(0, typeName.IndexOf('`'));

                            if (seenNames.Add(typeName))
                            {
                                var kind = type.IsEnum ? CompletionKind.Property :
                                           type.IsInterface ? CompletionKind.Type :
                                           CompletionKind.Type;
                                var typeKind = type.IsEnum ? "enum" :
                                              type.IsInterface ? "interface" :
                                              type.IsValueType ? "struct" :
                                              type.IsAbstract && type.IsSealed ? "static class" : "class";
                                members.Add((typeName, $"{type.Namespace}.{typeName} ({typeKind})", kind));
                            }
                        }
                        // Check for sub-namespaces
                        else if (type.Namespace.StartsWith(namespaceName + "."))
                        {
                            var remaining = type.Namespace.Substring(namespaceName.Length + 1);
                            var nextPart = remaining.Split('.')[0];
                            if (subNamespaces.Add(nextPart))
                            {
                                members.Add((nextPart, $"{namespaceName}.{nextPart} (namespace)", CompletionKind.Keyword));
                            }
                        }
                    }
                }
                catch
                {
                    // Skip assemblies that can't be reflected
                }
            }
        }
        catch
        {
            // Ignore assembly enumeration errors
        }

        return members.OrderBy(m => m.Kind == CompletionKind.Keyword ? 0 : 1)
                      .ThenBy(m => m.Name)
                      .ToList();
    }

    /// <summary>
    /// Clear the cache (useful if types change).
    /// </summary>
    public static void ClearCache()
    {
        _typeCache.Clear();
    }

    /// <summary>
    /// Get constructor signatures for a type.
    /// </summary>
    public static List<string> GetConstructorSignatures(string typeName)
    {
        var signatures = new List<string>();
        var type = ResolveType(typeName);
        if (type == null)
            return signatures;

        var constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
        foreach (var ctor in constructors)
        {
            var parameters = ctor.GetParameters();
            var paramStr = string.Join(", ", parameters.Select(p => $"{GetTypeName(p.ParameterType)} {p.Name}"));
            signatures.Add($"{type.Name}({paramStr})");
        }

        // If no public constructors found, add default
        if (signatures.Count == 0)
        {
            signatures.Add($"{type.Name}()");
        }

        return signatures;
    }

    /// <summary>
    /// Get method signatures for a specific method on a type.
    /// </summary>
    public static List<string> GetMethodSignatures(string typeName, string methodName)
    {
        var signatures = new List<string>();
        var type = ResolveType(typeName);
        if (type == null)
            return signatures;

        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .Where(m => m.Name.Equals(methodName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var method in methods)
        {
            var parameters = method.GetParameters();
            var paramStr = string.Join(", ", parameters.Select(p => $"{GetTypeName(p.ParameterType)} {p.Name}"));
            var staticStr = method.IsStatic ? "static " : "";
            signatures.Add($"{staticStr}{GetTypeName(method.ReturnType)} {method.Name}({paramStr})");
        }

        return signatures;
    }

    /// <summary>
    /// Find all namespaces that contain a public type with the given name.
    /// </summary>
    public static List<string> FindNamespacesForType(string typeName)
    {
        var namespaces = new HashSet<string>();

        if (string.IsNullOrWhiteSpace(typeName))
            return namespaces.ToList();

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (type.IsPublic && type.Name == typeName && !string.IsNullOrEmpty(type.Namespace))
                    {
                        namespaces.Add(type.Namespace);
                    }
                }
            }
            catch
            {
                // Ignored
            }
        }

        return namespaces.OrderBy(n => n).ToList();
    }
}
