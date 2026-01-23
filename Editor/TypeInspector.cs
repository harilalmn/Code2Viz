using System.Numerics;
using System.Reflection;
using System.Linq;
using System.Threading.Tasks;

namespace Code2Viz.Editor;

/// <summary>
/// Uses reflection to get members of any type for autocomplete.
/// </summary>
public static class TypeInspector
{
    // Cache to avoid repeated reflection
    private static readonly Dictionary<string, List<(string Name, string Description, CompletionKind Kind)>> _typeCache = new();

    // Known type mappings - built dynamically via reflection
    private static Dictionary<string, Type>? _knownTypes;
    private static List<(string Name, string Description)>? _commonTypes;
    private static HashSet<string>? _collectionTypeNames;

    /// <summary>
    /// Get or build the known types dictionary using reflection.
    /// </summary>
    private static Dictionary<string, Type> GetKnownTypes()
    {
        if (_knownTypes != null)
            return _knownTypes;

        _knownTypes = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

        // Add primitive type aliases
        _knownTypes["string"] = typeof(string);
        _knownTypes["int"] = typeof(int);
        _knownTypes["double"] = typeof(double);
        _knownTypes["float"] = typeof(float);
        _knownTypes["bool"] = typeof(bool);
        _knownTypes["long"] = typeof(long);
        _knownTypes["short"] = typeof(short);
        _knownTypes["byte"] = typeof(byte);
        _knownTypes["char"] = typeof(char);
        _knownTypes["decimal"] = typeof(decimal);
        _knownTypes["object"] = typeof(object);

        // Scan Code2Viz.Geometry assembly for all public types
        var geometryAssembly = typeof(Geometry.VPoint).Assembly;
        foreach (var type in geometryAssembly.GetExportedTypes())
        {
            if (type.IsPublic && !type.IsNested)
            {
                _knownTypes[type.Name] = type;
            }
        }

        // Add VizConsole
        _knownTypes["VizConsole"] = typeof(Console.VizConsole);

        // Scan Code2Viz.Animation namespace for Timeline and Animation types
        var animationTypes = geometryAssembly.GetExportedTypes()
            .Where(t => t.Namespace == "Code2Viz.Animation" && t.IsPublic && !t.IsNested);
        foreach (var type in animationTypes)
        {
            _knownTypes[type.Name] = type;
        }

        // Scan common System namespaces
        var systemTypes = new[]
        {
            typeof(Math), typeof(System.Console), typeof(Convert), typeof(string),
            typeof(DateTime), typeof(TimeSpan), typeof(Random), typeof(Environment),
            typeof(Guid), typeof(Enumerable), typeof(object),
            typeof(System.IO.Path), typeof(System.IO.File), typeof(System.IO.Directory),
            typeof(System.Text.StringBuilder), typeof(System.Text.RegularExpressions.Regex),
        };
        foreach (var type in systemTypes)
        {
            _knownTypes[type.Name] = type;
        }

        // Scan System.Collections.Generic for collection types
        var collectionsAssembly = typeof(List<>).Assembly;
        var collectionNamespace = "System.Collections.Generic";
        foreach (var type in collectionsAssembly.GetExportedTypes())
        {
            if (type.Namespace == collectionNamespace && type.IsPublic && !type.IsNested)
            {
                // Use the name without generic arity suffix for easier lookup
                var name = type.Name;
                var tickIndex = name.IndexOf('`');
                if (tickIndex > 0)
                    name = name.Substring(0, tickIndex);

                if (!_knownTypes.ContainsKey(name))
                    _knownTypes[name] = type;
            }
        }

        // Add non-generic collections
        _knownTypes["ArrayList"] = typeof(System.Collections.ArrayList);
        _knownTypes["Hashtable"] = typeof(System.Collections.Hashtable);

        return _knownTypes;
    }

    /// <summary>
    /// Get all common types for general completion - built dynamically via reflection.
    /// </summary>
    public static IEnumerable<(string Name, string Description)> GetCommonTypes()
    {
        if (_commonTypes != null)
            return _commonTypes;

        _commonTypes = new List<(string Name, string Description)>();
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var geometryAssembly = typeof(Geometry.VPoint).Assembly;

        // Add Code2Viz.Geometry types (VPoint, VLine, VCircle, etc.)
        var geometryTypes = geometryAssembly.GetExportedTypes()
            .Where(t => t.Namespace == "Code2Viz.Geometry" && t.IsPublic && !t.IsNested);
        foreach (var type in geometryTypes)
        {
            var suffix = type.IsAbstract ? " (abstract)" : "";
            var description = $"{type.Namespace}.{type.Name}{suffix}";
            if (seenNames.Add(type.Name))
            {
                _commonTypes.Add((type.Name, description));
            }
        }

        // Add VizConsole
        if (seenNames.Add("VizConsole"))
        {
            _commonTypes.Add(("VizConsole", "Code2Viz.Console.VizConsole - Console output with line tracking"));
        }

        // Add Animation types (Timeline, DrawAnimation, MoveAnimation, etc.)
        var animationTypes = geometryAssembly.GetExportedTypes()
            .Where(t => t.Namespace == "Code2Viz.Animation" && t.IsPublic && !t.IsNested);
        foreach (var type in animationTypes)
        {
            var suffix = type.IsAbstract ? " (abstract)" : "";
            var description = $"{type.Namespace}.{type.Name}{suffix}";
            if (seenNames.Add(type.Name))
            {
                _commonTypes.Add((type.Name, description));
            }
        }

        // Add primitive types with their C# aliases
        var primitiveTypes = new (string Alias, Type Type, string Desc)[]
        {
            ("int", typeof(int), "32-bit signed integer"),
            ("double", typeof(double), "64-bit floating point"),
            ("float", typeof(float), "32-bit floating point"),
            ("bool", typeof(bool), "Boolean true/false"),
            ("string", typeof(string), "Text string"),
            ("long", typeof(long), "64-bit signed integer"),
            ("short", typeof(short), "16-bit signed integer"),
            ("byte", typeof(byte), "8-bit unsigned integer"),
            ("char", typeof(char), "Unicode character"),
            ("decimal", typeof(decimal), "High-precision decimal"),
            ("object", typeof(object), "Base object type"),
        };
        foreach (var (alias, type, desc) in primitiveTypes)
        {
            if (seenNames.Add(alias))
            {
                _commonTypes.Add((alias, $"{type.FullName} - {desc}"));
            }
        }

        // Add common System types
        var systemTypes = new (Type Type, string Desc)[]
        {
            (typeof(Math), "Mathematical functions"),
            (typeof(System.Console), "Console I/O"),
            (typeof(Convert), "Type conversion"),
            (typeof(String), "String manipulation"),
            (typeof(Random), "Random number generator"),
            (typeof(DateTime), "Date and time"),
            (typeof(TimeSpan), "Time interval"),
            (typeof(Environment), "Environment info"),
            (typeof(Guid), "Unique identifiers"),
            (typeof(Enumerable), "LINQ methods"),
            (typeof(System.IO.Path), "Path manipulation"),
            (typeof(System.IO.File), "File operations"),
            (typeof(System.IO.Directory), "Directory operations"),
            (typeof(System.Text.StringBuilder), "Mutable string builder"),
            (typeof(System.Text.RegularExpressions.Regex), "Regular expressions"),
            (typeof(Tuple), "Immutable tuple"),
            (typeof(Action), "Delegate without return"),
            (typeof(Func<>), "Delegate with return"),
            (typeof(Exception), "Base exception"),
            (typeof(Array), "Base array type"),
            (typeof(Nullable<>), "Nullable wrapper"),
            (typeof(Task), "Async task"),
            (typeof(System.Numerics.Vector2), "2D vector"),
            (typeof(System.Numerics.Vector3), "3D vector"),
        };
        foreach (var (type, desc) in systemTypes)
        {
            var name = type.Name;
            var tickIndex = name.IndexOf('`');
            if (tickIndex > 0) name = name.Substring(0, tickIndex);
            if (seenNames.Add(name))
            {
                _commonTypes.Add((name, $"{type.FullName} - {desc}"));
            }
        }

        // Add generic collections
        var genericCollections = new (Type Type, string Desc)[]
        {
            (typeof(List<>), "Generic list"),
            (typeof(Dictionary<,>), "Key-value dictionary"),
            (typeof(HashSet<>), "Unique element set"),
            (typeof(Queue<>), "FIFO queue"),
            (typeof(Stack<>), "LIFO stack"),
            (typeof(LinkedList<>), "Doubly-linked list"),
            (typeof(SortedSet<>), "Sorted unique set"),
            (typeof(SortedList<,>), "Sorted key-value list"),
            (typeof(SortedDictionary<,>), "Sorted dictionary"),
            (typeof(IEnumerable<>), "Enumerable interface"),
            (typeof(IList<>), "List interface"),
            (typeof(ICollection<>), "Collection interface"),
            (typeof(IDictionary<,>), "Dictionary interface"),
            (typeof(KeyValuePair<,>), "Key-value pair"),
        };
        foreach (var (type, desc) in genericCollections)
        {
            var name = type.Name;
            var tickIndex = name.IndexOf('`');
            if (tickIndex > 0) name = name.Substring(0, tickIndex);
            if (seenNames.Add(name))
            {
                _commonTypes.Add((name, $"System.Collections.Generic.{name} - {desc}"));
            }
        }

        return _commonTypes;
    }

    /// <summary>
    /// Get set of collection type names - built dynamically via reflection.
    /// </summary>
    private static HashSet<string> GetCollectionTypeNames()
    {
        if (_collectionTypeNames != null)
            return _collectionTypeNames;

        _collectionTypeNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Scan System.Collections.Generic for types that implement IEnumerable
        var collectionsAssembly = typeof(List<>).Assembly;
        var enumerableType = typeof(System.Collections.IEnumerable);

        foreach (var type in collectionsAssembly.GetExportedTypes())
        {
            if (type.Namespace?.StartsWith("System.Collections") == true &&
                type.IsPublic && !type.IsNested &&
                enumerableType.IsAssignableFrom(type))
            {
                var name = type.Name;
                var tickIndex = name.IndexOf('`');
                if (tickIndex > 0)
                    name = name.Substring(0, tickIndex);
                _collectionTypeNames.Add(name);
            }
        }

        // Add interface names
        _collectionTypeNames.Add("IList");
        _collectionTypeNames.Add("ICollection");
        _collectionTypeNames.Add("IEnumerable");
        _collectionTypeNames.Add("IDictionary");
        _collectionTypeNames.Add("ISet");

        return _collectionTypeNames;
    }

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
        if (GetKnownTypes().TryGetValue(baseTypeName, out var knownType))
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
        var members = type != null
            ? GetTypeMembersFromReflection(type)
            : new List<(string Name, string Description, CompletionKind Kind)>();

        // Add LINQ extension methods for collection types
        // Check both the resolved type AND the type name string pattern
        bool isCollection = (type != null && IsEnumerableType(type)) || IsCollectionTypeName(typeName);
        if (isCollection)
        {
            var linqMethods = GetLinqExtensionMethods();
            var seenNames = new HashSet<string>(members.Select(m => m.Name));
            foreach (var method in linqMethods)
            {
                if (seenNames.Add(method.Name))
                {
                    members.Add(method);
                }
            }
        }

        if (members.Count > 0)
        {
            _typeCache[typeName] = members;
        }
        return members;
    }

    /// <summary>
    /// Check if a type name string looks like a collection type.
    /// Used as fallback when type resolution fails.
    /// </summary>
    private static bool IsCollectionTypeName(string typeName)
    {
        if (string.IsNullOrEmpty(typeName))
            return false;

        // Extract base type name (before generic parameter)
        var baseName = typeName;
        var genericIndex = typeName.IndexOf('<');
        if (genericIndex > 0)
            baseName = typeName.Substring(0, genericIndex);

        // Check against dynamically-discovered collection type names
        return GetCollectionTypeNames().Contains(baseName);
    }

    /// <summary>
    /// Check if a type implements IEnumerable (is a collection type).
    /// </summary>
    private static bool IsEnumerableType(Type type)
    {
        if (type == null) return false;

        // Check for generic IEnumerable<T>
        try
        {
            if (type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>)))
                return true;
        }
        catch { }

        // Check for non-generic IEnumerable
        try
        {
            if (typeof(System.Collections.IEnumerable).IsAssignableFrom(type) && type != typeof(string))
                return true;
        }
        catch { }

        // Check for open generic types like List<>
        if (type.IsGenericTypeDefinition)
        {
            try
            {
                // Try to check if it would implement IEnumerable when closed
                var interfaces = type.GetInterfaces();
                if (interfaces.Any(i => i.Name.StartsWith("IEnumerable")))
                    return true;
            }
            catch { }
        }

        // Fallback: Check by type name for common collection types
        var typeName = type.Name;
        if (typeName.StartsWith("List") || typeName.StartsWith("IList") ||
            typeName.StartsWith("ICollection") || typeName.StartsWith("IEnumerable") ||
            typeName.StartsWith("HashSet") || typeName.StartsWith("ISet") ||
            typeName.StartsWith("Dictionary") || typeName.StartsWith("IDictionary") ||
            typeName.StartsWith("Queue") || typeName.StartsWith("Stack") ||
            typeName.StartsWith("LinkedList") || typeName.StartsWith("SortedSet") ||
            typeName.StartsWith("SortedList") || typeName.StartsWith("SortedDictionary") ||
            typeName == "ArrayList" || typeName == "Hashtable")
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Get LINQ extension methods using reflection from System.Linq.Enumerable.
    /// </summary>
    private static List<(string Name, string Description, CompletionKind Kind)>? _cachedLinqMethods;

    private static List<(string Name, string Description, CompletionKind Kind)> GetLinqExtensionMethods()
    {
        if (_cachedLinqMethods != null)
            return _cachedLinqMethods;

        var methods = new List<(string Name, string Description, CompletionKind Kind)>();
        var seenNames = new HashSet<string>();

        // Get all extension methods from System.Linq.Enumerable
        var enumerableType = typeof(Enumerable);
        var linqMethods = enumerableType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.IsDefined(typeof(System.Runtime.CompilerServices.ExtensionAttribute), false))
            .GroupBy(m => m.Name)
            .Select(g => g.First()); // Take first overload of each method

        foreach (var method in linqMethods)
        {
            if (seenNames.Add(method.Name))
            {
                var returnType = GetTypeName(method.ReturnType);
                var parameters = method.GetParameters().Skip(1); // Skip 'this' parameter
                var paramStr = string.Join(", ", parameters.Select(p => $"{GetTypeName(p.ParameterType)} {p.Name}"));
                var description = $"{returnType} {method.Name}({paramStr})";
                methods.Add((method.Name, description, CompletionKind.Method));
            }
        }

        // Also get extension methods from System.Linq.Queryable for completeness
        try
        {
            var queryableType = typeof(Queryable);
            var queryableMethods = queryableType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.IsDefined(typeof(System.Runtime.CompilerServices.ExtensionAttribute), false))
                .GroupBy(m => m.Name)
                .Select(g => g.First());

            foreach (var method in queryableMethods)
            {
                if (seenNames.Add(method.Name))
                {
                    var returnType = GetTypeName(method.ReturnType);
                    var parameters = method.GetParameters().Skip(1);
                    var paramStr = string.Join(", ", parameters.Select(p => $"{GetTypeName(p.ParameterType)} {p.Name}"));
                    var description = $"{returnType} {method.Name}({paramStr})";
                    methods.Add((method.Name, description, CompletionKind.Method));
                }
            }
        }
        catch { }

        _cachedLinqMethods = methods;
        return methods;
    }

    /// <summary>
    /// Get members from a Type using reflection.
    /// </summary>
    public static List<(string Name, string Description, CompletionKind Kind)> GetTypeMembersFromReflection(Type type)
    {
        var members = new List<(string Name, string Description, CompletionKind Kind)>();
        var seenNames = new HashSet<string>();

        // Check if this is EasingFunctions - its methods should be marked as Delegate
        // since they're meant to be used as method references for delegate properties
        bool isEasingFunctions = type.Name == "EasingFunctions";

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

                // Mark EasingFunctions methods as Delegate (no parentheses on completion)
                var kind = isEasingFunctions ? CompletionKind.Delegate : CompletionKind.Method;
                members.Add((method.Name, description, kind));
            }
        }

        // Sort: Properties first, then methods/delegates, alphabetically within each group
        return members
            .OrderBy(m => m.Kind == CompletionKind.Method || m.Kind == CompletionKind.Delegate ? 1 : 0)
            .ThenBy(m => m.Name)
            .ToList();
    }

    /// <summary>
    /// Get a friendly type name (e.g., "int" instead of "Int32", "List&lt;T&gt;" instead of "List`1").
    /// </summary>
    public static string GetTypeName(Type type)
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
        return !string.IsNullOrEmpty(typeName) && GetKnownTypes().ContainsKey(typeName);
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
    /// Get the return type of a member (property or field) on a type.
    /// Returns the type name as a string, or null if not found.
    /// </summary>
    public static string? GetMemberReturnType(Type type, string memberName)
    {
        if (type == null || string.IsNullOrEmpty(memberName))
            return null;

        // Check properties (instance and static, including inherited)
        var property = type.GetProperty(memberName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
        if (property != null)
        {
            return FormatTypeName(property.PropertyType);
        }

        // Check fields (instance and static, including inherited)
        var field = type.GetField(memberName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
        if (field != null)
        {
            return FormatTypeName(field.FieldType);
        }

        return null;
    }

    /// <summary>
    /// Get the return type of a method on a type.
    /// Returns the type name as a string, or null if not found.
    /// </summary>
    public static string? GetMethodReturnType(string typeName, string methodName)
    {
        var type = ResolveType(typeName);
        if (type == null || string.IsNullOrEmpty(methodName))
            return null;

        // Check methods (instance and static)
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .Where(m => m.Name.Equals(methodName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (methods.Count > 0)
        {
            // Return the first method's return type (all overloads should have same return type typically)
            return FormatTypeName(methods[0].ReturnType);
        }

        return null;
    }

    /// <summary>
    /// Formats a Type to a readable type name including generic arguments.
    /// E.g., List&lt;ICurve&gt; instead of List`1
    /// </summary>
    private static string FormatTypeName(Type type)
    {
        if (!type.IsGenericType)
            return type.Name;

        // Get the base name without the `N suffix
        var baseName = type.Name;
        var tickIndex = baseName.IndexOf('`');
        if (tickIndex > 0)
            baseName = baseName.Substring(0, tickIndex);

        // Get the generic arguments
        var genericArgs = type.GetGenericArguments();
        var argNames = genericArgs.Select(FormatTypeName);

        return $"{baseName}<{string.Join(", ", argNames)}>";
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

        // Strip generic parameters if present (e.g., "List<VPolygon>" -> "List")
        var baseTypeName = typeName;
        var genericIndex = typeName.IndexOf('<');
        if (genericIndex > 0)
            baseTypeName = typeName.Substring(0, genericIndex);

        // First check our known types (Code2Viz types are always available)
        if (GetKnownTypes().TryGetValue(baseTypeName, out var knownType))
        {
            if (!string.IsNullOrEmpty(knownType.Namespace))
            {
                namespaces.Add(knownType.Namespace);
            }
        }

        // Then search all loaded assemblies
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                // Use GetExportedTypes for better performance (only public types)
                foreach (var type in assembly.GetExportedTypes())
                {
                    if (string.IsNullOrEmpty(type.Namespace))
                        continue;

                    // Get type name, stripping generic arity suffix (e.g., "List`1" -> "List")
                    var name = type.Name;
                    var tickIndex = name.IndexOf('`');
                    if (tickIndex > 0)
                        name = name.Substring(0, tickIndex);

                    if (name.Equals(baseTypeName, StringComparison.Ordinal))
                    {
                        namespaces.Add(type.Namespace);
                    }
                }
            }
            catch (NotSupportedException)
            {
                // Dynamic assemblies don't support GetExportedTypes
                // Try GetTypes() as fallback
                try
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        if (!type.IsPublic || string.IsNullOrEmpty(type.Namespace))
                            continue;

                        var name = type.Name;
                        var tickIndex = name.IndexOf('`');
                        if (tickIndex > 0)
                            name = name.Substring(0, tickIndex);

                        if (name.Equals(baseTypeName, StringComparison.Ordinal))
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
            catch
            {
                // Ignored
            }
        }

        // Prioritize Code2Viz namespaces at the top
        return namespaces
            .OrderByDescending(n => n.StartsWith("Code2Viz"))
            .ThenBy(n => n)
            .ToList();
    }

    /// <summary>
    /// Find namespaces that contain extension methods with the given name.
    /// This is useful for suggesting "using System.Linq;" when Select, Where, etc. are used.
    /// </summary>
    public static List<string> FindNamespacesForExtensionMethod(string methodName)
    {
        var namespaces = new HashSet<string>();

        if (string.IsNullOrWhiteSpace(methodName))
            return namespaces.ToList();

        // Common extension method sources to check
        var extensionMethodSources = new[]
        {
            typeof(Enumerable),           // System.Linq
            typeof(Queryable),            // System.Linq
        };

        foreach (var sourceType in extensionMethodSources)
        {
            try
            {
                var methods = sourceType.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                    .Where(m => m.IsDefined(typeof(System.Runtime.CompilerServices.ExtensionAttribute), false));

                if (methods.Any(m => m.Name.Equals(methodName, StringComparison.Ordinal)))
                {
                    if (!string.IsNullOrEmpty(sourceType.Namespace))
                    {
                        namespaces.Add(sourceType.Namespace);
                    }
                }
            }
            catch
            {
                // Ignored
            }
        }

        // Also search for extension methods in Code2Viz namespaces
        var code2vizAssembly = typeof(Geometry.VPoint).Assembly;
        try
        {
            foreach (var type in code2vizAssembly.GetExportedTypes())
            {
                if (!type.IsAbstract || !type.IsSealed) // Static classes are abstract and sealed
                    continue;

                try
                {
                    var methods = type.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                        .Where(m => m.IsDefined(typeof(System.Runtime.CompilerServices.ExtensionAttribute), false));

                    if (methods.Any(m => m.Name.Equals(methodName, StringComparison.Ordinal)))
                    {
                        if (!string.IsNullOrEmpty(type.Namespace))
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
        }
        catch
        {
            // Ignored
        }

        return namespaces
            .OrderByDescending(n => n.StartsWith("Code2Viz"))
            .ThenBy(n => n)
            .ToList();
    }
}
