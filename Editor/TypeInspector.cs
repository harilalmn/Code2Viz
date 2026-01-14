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

        // Add Code2Viz.Geometry types with XML doc summaries where available
        var geometryAssembly = typeof(Geometry.VPoint).Assembly;
        foreach (var type in geometryAssembly.GetExportedTypes())
        {
            if (type.IsPublic && !type.IsNested && !type.IsAbstract)
            {
                var description = $"{type.Namespace}.{type.Name}";
                _commonTypes.Add((type.Name, description));
            }
        }

        // Add VizConsole
        _commonTypes.Add(("VizConsole", "Console output with line tracking"));

        // Add common System types
        var systemTypes = new (Type Type, string Desc)[]
        {
            (typeof(Math), "Mathematical functions"),
            (typeof(System.Console), "Console I/O"),
            (typeof(Convert), "Type conversion"),
            (typeof(string), "String manipulation"),
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
        };
        foreach (var (type, desc) in systemTypes)
        {
            _commonTypes.Add((type.Name, $"{type.FullName} - {desc}"));
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
        };
        foreach (var (type, desc) in genericCollections)
        {
            var name = type.Name;
            var tickIndex = name.IndexOf('`');
            if (tickIndex > 0)
                name = name.Substring(0, tickIndex);
            _commonTypes.Add((name, $"System.Collections.Generic.{name} - {desc}"));
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
            return property.PropertyType.Name;
        }

        // Check fields (instance and static, including inherited)
        var field = type.GetField(memberName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
        if (field != null)
        {
            return field.FieldType.Name;
        }

        return null;
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
