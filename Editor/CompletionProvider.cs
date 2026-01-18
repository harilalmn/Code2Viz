using System.Text.RegularExpressions;
using ICSharpCode.AvalonEdit.CodeCompletion;

namespace Code2Viz.Editor;

public static class CompletionProvider
{
    // C# Keywords
    private static readonly string[] Keywords =
    {
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch",
        "char", "checked", "class", "const", "continue", "decimal", "default",
        "delegate", "do", "double", "else", "enum", "event", "explicit",
        "extern", "false", "finally", "fixed", "float", "for", "foreach",
        "goto", "if", "implicit", "in", "int", "interface", "internal", "is",
        "lock", "long", "namespace", "new", "null", "object", "operator",
        "out", "override", "params", "private", "protected", "public",
        "readonly", "ref", "return", "sbyte", "sealed", "short", "sizeof",
        "stackalloc", "static", "string", "struct", "switch", "this", "throw",
        "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe",
        "ushort", "using", "var", "virtual", "void", "volatile", "while"
    };

    // Cache for custom classes extracted from code
    private static Dictionary<string, List<(string Name, string Description, CompletionKind Kind)>> _customClasses = new();
    private static string _lastCodeHash = "";

    public static IEnumerable<ICompletionData> GetCompletions(string prefix, string? allCode = null, string? textBeforeCursor = null)
    {
        var completions = new List<ICompletionData>();
        string? expectedType = null;

        // Try to infer expected type from context: Type variable = new ...
        if (!string.IsNullOrEmpty(textBeforeCursor))
        {
            expectedType = ExtractExpectedTypeFromNewExpression(textBeforeCursor);
        }

        // Add matching type first if found (prioritized completion)
        if (expectedType != null)
        {
            // Check common types for description
            var common = TypeInspector.GetCommonTypes().FirstOrDefault(t => t.Name == expectedType);
            if (common.Name != null)
            {
                completions.Add(new CompletionData(common.Name, common.Description, CompletionKind.Type) { Priority = 1000 });
            }
            // Check custom classes
            else if (_customClasses.ContainsKey(expectedType))
            {
                completions.Add(new CompletionData(expectedType, $"Custom class: {expectedType}", CompletionKind.Type) { Priority = 1000 });
            }
            // For generic types or other types, add anyway with inferred description
            else
            {
                // Extract base type name for description (e.g., "Tuple" from "Tuple<int, int>")
                var baseTypeName = expectedType.Contains('<') ? expectedType.Substring(0, expectedType.IndexOf('<')) : expectedType;
                completions.Add(new CompletionData(expectedType, $"Expected type: {baseTypeName}", CompletionKind.Type) { Priority = 1000 });
            }
        }

        // Extract and update custom classes first (needed for class member lookup)
        if (!string.IsNullOrEmpty(allCode))
        {
            UpdateCustomClasses(allCode);
        }

        // Add members of the current class context (properties, methods, fields)
        if (!string.IsNullOrEmpty(textBeforeCursor))
        {
            var currentClass = FindCurrentClass(textBeforeCursor);
            if (!string.IsNullOrEmpty(currentClass))
            {
                // Add members from the custom class itself
                if (_customClasses.TryGetValue(currentClass, out var classMembers))
                {
                    foreach (var member in classMembers)
                    {
                        if (string.IsNullOrEmpty(prefix) || member.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        {
                            completions.Add(new CompletionData(member.Name, member.Description, member.Kind));
                        }
                    }
                }

                // Add inherited members from base class
                var baseClassName = FindBaseClass(allCode ?? textBeforeCursor, currentClass);
                if (!string.IsNullOrEmpty(baseClassName))
                {
                    var baseType = TypeInspector.ResolveType(baseClassName);
                    if (baseType != null)
                    {
                        var inheritedMembers = TypeInspector.GetTypeMembersFromReflection(baseType);
                        foreach (var member in inheritedMembers)
                        {
                            if (string.IsNullOrEmpty(prefix) || member.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                            {
                                completions.Add(new CompletionData(member.Name, $"{member.Description} (inherited)", member.Kind));
                            }
                        }
                    }
                }
            }

            // Add method/constructor parameters if inside a method body
            var parameters = FindCurrentMethodParameters(textBeforeCursor);
            foreach (var (paramName, paramType) in parameters)
            {
                if (string.IsNullOrEmpty(prefix) || paramName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    completions.Add(new CompletionData(paramName, $"{paramType} (parameter)", CompletionKind.Property));
                }
            }

            // Add local variables
            var locals = FindLocalVariables(textBeforeCursor);
            foreach (var (varName, varType) in locals)
            {
                if (string.IsNullOrEmpty(prefix) || varName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    completions.Add(new CompletionData(varName, $"{varType} (local)", CompletionKind.Property));
                }
            }
        }

        // Add keywords
        foreach (var keyword in Keywords)
        {
            if (string.IsNullOrEmpty(prefix) || keyword.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                completions.Add(new CompletionData(keyword, $"C# keyword: {keyword}", CompletionKind.Keyword));
            }
        }

        // Add common types from TypeInspector
        foreach (var (name, desc) in TypeInspector.GetCommonTypes())
        {
            if (name == expectedType) continue; // Skip if already added
            if (string.IsNullOrEmpty(prefix) || name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                completions.Add(new CompletionData(name, desc, CompletionKind.Type));
            }
        }

        // Add custom class names
        foreach (var className in _customClasses.Keys)
        {
            if (className == expectedType) continue; // Skip if already added
            if (string.IsNullOrEmpty(prefix) || className.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                completions.Add(new CompletionData(className, $"Custom class: {className}", CompletionKind.Type));
            }
        }

        // Add code snippets (prefixed with snip: or matching trigger)
        foreach (var (trigger, desc) in CodeSnippets.GetAll())
        {
            if (string.IsNullOrEmpty(prefix) ||
                trigger.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
                ("snip:" + trigger).StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                completions.Add(new SnippetCompletionData(trigger, desc, CodeSnippets.GetSnippet(trigger) ?? ""));
            }
        }

        return completions;
    }

    /// <summary>
    /// Finds the class name that contains the cursor position based on brace counting.
    /// </summary>
    public static string? FindCurrentClass(string textBeforeCursor)
    {
        // Find all class declarations with their positions
        var classPattern = @"\b(?:public\s+)?class\s+(\w+)";
        var matches = Regex.Matches(textBeforeCursor, classPattern);

        var classStack = new Stack<(string Name, int BraceDepth)>();
        var braceDepth = 0;
        var lastClassEnd = 0;

        foreach (Match match in matches)
        {
            // Count braces from last class end to this class declaration
            var textBetween = textBeforeCursor.Substring(lastClassEnd, match.Index - lastClassEnd);
            foreach (var c in textBetween)
            {
                if (c == '{') braceDepth++;
                else if (c == '}')
                {
                    braceDepth--;
                    // Pop classes that have ended
                    while (classStack.Count > 0 && classStack.Peek().BraceDepth >= braceDepth)
                    {
                        classStack.Pop();
                    }
                }
            }

            var className = match.Groups[1].Value;
            if (className != "Viz") // Skip entry point class
            {
                classStack.Push((className, braceDepth));
            }
            lastClassEnd = match.Index + match.Length;
        }

        // Count remaining braces after last class declaration
        var remainingText = textBeforeCursor.Substring(lastClassEnd);
        foreach (var c in remainingText)
        {
            if (c == '{') braceDepth++;
            else if (c == '}')
            {
                braceDepth--;
                while (classStack.Count > 0 && classStack.Peek().BraceDepth >= braceDepth)
                {
                    classStack.Pop();
                }
            }
        }

        // The current class is the top of the stack (if any)
        return classStack.Count > 0 ? classStack.Peek().Name : null;
    }

    /// <summary>
    /// Finds parameters of the current method/constructor the cursor is inside.
    /// </summary>
    private static List<(string Name, string Type)> FindCurrentMethodParameters(string textBeforeCursor)
    {
        var parameters = new List<(string Name, string Type)>();

        // Find all method/constructor signatures with their positions
        // Match: access? static? returnType? MethodName(params)
        var methodPattern = @"(?:public|private|protected|internal)?\s*(?:static\s+)?(?:[\w<>\[\]?]+\s+)?(\w+)\s*\(([^)]*)\)\s*\{";
        var matches = Regex.Matches(textBeforeCursor, methodPattern);

        if (matches.Count == 0)
            return parameters;

        // Find which method body we're inside by tracking braces
        string? currentMethodParams = null;

        foreach (Match match in matches)
        {
            // Count braces up to this method
            var textBefore = textBeforeCursor.Substring(0, match.Index);
            var openBefore = textBefore.Count(c => c == '{');
            var closeBefore = textBefore.Count(c => c == '}');
            var depthAtMethod = openBefore - closeBefore;

            // Count braces from method start to cursor
            var textAfterMethod = textBeforeCursor.Substring(match.Index + match.Length);
            var openAfter = textAfterMethod.Count(c => c == '{');
            var closeAfter = textAfterMethod.Count(c => c == '}');

            // If we're still inside this method's braces (depth > depthAtMethod)
            var currentDepth = openBefore + 1 + openAfter - closeBefore - closeAfter;
            if (currentDepth > depthAtMethod)
            {
                currentMethodParams = match.Groups[2].Value;
            }
        }

        if (string.IsNullOrEmpty(currentMethodParams))
            return parameters;

        // Parse parameters: "Type name, Type2 name2"
        var paramParts = currentMethodParams.Split(',');
        foreach (var part in paramParts)
        {
            var trimmed = part.Trim();
            if (string.IsNullOrEmpty(trimmed))
                continue;

            // Handle "Type? name" or "Type name" or "ref Type name" etc.
            var paramMatch = Regex.Match(trimmed, @"(?:ref\s+|out\s+|in\s+|params\s+)?([\w<>\[\]?]+)\s+(\w+)$");
            if (paramMatch.Success)
            {
                var paramType = paramMatch.Groups[1].Value;
                var paramName = paramMatch.Groups[2].Value;
                parameters.Add((paramName, paramType));
            }
        }

        return parameters;
    }

    /// <summary>
    /// Finds local variables declared before the cursor position in all enclosing scopes.
    /// </summary>
    private static List<(string Name, string Type)> FindLocalVariables(string textBeforeCursor)
    {
        var variables = new List<(string Name, string Type)>();
        var seenNames = new HashSet<string>();

        // Find the method body start by looking for method signature pattern followed by {
        var methodPattern = @"(?:public|private|protected|internal)?\s*(?:static\s+)?(?:[\w<>\[\]?]+\s+)?\w+\s*\([^)]*\)\s*\{";
        var methodMatches = Regex.Matches(textBeforeCursor, methodPattern);

        if (methodMatches.Count == 0)
            return variables;

        // Find which method body we're actually inside
        int methodBodyStart = 0;
        foreach (Match match in methodMatches)
        {
            var matchEnd = match.Index + match.Length;
            // Count braces to see if we're still inside this method
            var textAfter = textBeforeCursor.Substring(matchEnd);
            var opens = textAfter.Count(c => c == '{') + 1; // +1 for the method's opening brace
            var closes = textAfter.Count(c => c == '}');
            if (opens > closes)
            {
                // We're inside this method - start searching from after its opening brace
                methodBodyStart = matchEnd;
            }
        }

        // Search from method body start to cursor for all variable declarations
        var scopeText = textBeforeCursor.Substring(methodBodyStart);

        // Match variable declarations: "Type name = " or "Type name;" or "var name = "
        var varPattern = @"\b(var|[\w<>\[\]?]+)\s+(\w+)\s*[=;]";
        var matches = Regex.Matches(scopeText, varPattern);

        foreach (Match match in matches)
        {
            var varType = match.Groups[1].Value;
            var varName = match.Groups[2].Value;

            // Skip if it looks like a method call or keyword, or if already seen (shadowing)
            // Note: "var" is a keyword but is valid as a type for variable declarations
            if ((varType == "var" || !Keywords.Contains(varType)) && !Keywords.Contains(varName) && !seenNames.Contains(varName))
            {
                variables.Add((varName, varType));
                seenNames.Add(varName);
            }
        }

        return variables;
    }

    /// <summary>
    /// Public wrapper for FindLocalVariables for use by hover tooltip.
    /// </summary>
    public static List<(string Name, string Type)> FindLocalVariablesPublic(string textBeforeCursor)
    {
        return FindLocalVariables(textBeforeCursor);
    }

    /// <summary>
    /// Public wrapper for FindCurrentMethodParameters for use by hover tooltip.
    /// </summary>
    public static List<(string Name, string Type)> FindCurrentMethodParametersPublic(string textBeforeCursor)
    {
        return FindCurrentMethodParameters(textBeforeCursor);
    }

    /// <summary>
    /// Gets only type completions (no keywords) for use after 'new' keyword.
    /// </summary>
    public static IEnumerable<ICompletionData> GetTypeCompletions(string? allCode = null, string? textBeforeCursor = null)
    {
        var completions = new List<ICompletionData>();
        string? expectedType = null;

        // Try to infer expected type from context: Type variable = new ...
        if (!string.IsNullOrEmpty(textBeforeCursor))
        {
            expectedType = ExtractExpectedTypeFromNewExpression(textBeforeCursor);
        }

        // Add matching type first if found (prioritized completion)
        if (expectedType != null)
        {
            // Check common types for description
            var common = TypeInspector.GetCommonTypes().FirstOrDefault(t => t.Name == expectedType);
            if (common.Name != null)
            {
                completions.Add(new CompletionData(common.Name, common.Description, CompletionKind.Type) { Priority = 1000 });
            }
            // Check custom classes
            else if (_customClasses.ContainsKey(expectedType))
            {
                completions.Add(new CompletionData(expectedType, $"Custom class: {expectedType}", CompletionKind.Type) { Priority = 1000 });
            }
            // For generic types or other types, add anyway with inferred description
            else
            {
                // Extract base type name for description (e.g., "Tuple" from "Tuple<int, int>")
                var baseTypeName = expectedType.Contains('<') ? expectedType.Substring(0, expectedType.IndexOf('<')) : expectedType;
                completions.Add(new CompletionData(expectedType, $"Expected type: {baseTypeName}", CompletionKind.Type) { Priority = 1000 });
            }
        }

        // Add common types from TypeInspector
        foreach (var (name, desc) in TypeInspector.GetCommonTypes())
        {
            // Skip if already added as prioritized
            if (name == expectedType) continue;
            completions.Add(new CompletionData(name, desc, CompletionKind.Type));
        }

        // Extract and add custom classes from code
        if (!string.IsNullOrEmpty(allCode))
        {
            UpdateCustomClasses(allCode);

            foreach (var className in _customClasses.Keys)
            {
                // Skip if already added as prioritized
                if (className == expectedType) continue;
                completions.Add(new CompletionData(className, $"Custom class: {className}", CompletionKind.Type));
            }
        }

        return completions;
    }

    /// <summary>
    /// Gets completions appropriate for method arguments - local variables, parameters, types, and relevant keywords.
    /// </summary>
    public static IEnumerable<ICompletionData> GetArgumentCompletions(string prefix, string? allCode = null, string? textBeforeCursor = null)
    {
        var completions = new List<ICompletionData>();
        var seenNames = new HashSet<string>();

        // Extract custom classes first (needed for type completions)
        if (!string.IsNullOrEmpty(allCode))
        {
            UpdateCustomClasses(allCode);
        }

        if (!string.IsNullOrEmpty(textBeforeCursor))
        {
            // Add local variables (highest priority for arguments)
            var locals = FindLocalVariables(textBeforeCursor);
            foreach (var (varName, varType) in locals)
            {
                if (seenNames.Add(varName) && (string.IsNullOrEmpty(prefix) || varName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                {
                    completions.Add(new CompletionData(varName, $"{varType} (local)", CompletionKind.Property) { Priority = 100 });
                }
            }

            // Add method parameters
            var parameters = FindCurrentMethodParameters(textBeforeCursor);
            foreach (var (paramName, paramType) in parameters)
            {
                if (seenNames.Add(paramName) && (string.IsNullOrEmpty(prefix) || paramName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                {
                    completions.Add(new CompletionData(paramName, $"{paramType} (parameter)", CompletionKind.Property) { Priority = 90 });
                }
            }

            // Add class members if inside a class
            var currentClass = FindCurrentClass(textBeforeCursor);
            if (!string.IsNullOrEmpty(currentClass))
            {
                if (_customClasses.TryGetValue(currentClass, out var classMembers))
                {
                    foreach (var member in classMembers)
                    {
                        if (seenNames.Add(member.Name) && (string.IsNullOrEmpty(prefix) || member.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                        {
                            completions.Add(new CompletionData(member.Name, member.Description, member.Kind) { Priority = 80 });
                        }
                    }
                }
            }
        }

        // Add argument-relevant keywords
        var argKeywords = new[] { "true", "false", "null", "new", "this", "typeof", "nameof", "default" };
        foreach (var keyword in argKeywords)
        {
            if (seenNames.Add(keyword) && (string.IsNullOrEmpty(prefix) || keyword.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            {
                completions.Add(new CompletionData(keyword, $"C# keyword: {keyword}", CompletionKind.Keyword) { Priority = 50 });
            }
        }

        // Add types (for new expressions or type arguments)
        foreach (var (name, desc) in TypeInspector.GetCommonTypes())
        {
            if (seenNames.Add(name) && (string.IsNullOrEmpty(prefix) || name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            {
                completions.Add(new CompletionData(name, desc, CompletionKind.Type) { Priority = 40 });
            }
        }

        // Add custom class names
        foreach (var className in _customClasses.Keys)
        {
            if (seenNames.Add(className) && (string.IsNullOrEmpty(prefix) || className.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            {
                completions.Add(new CompletionData(className, $"Custom class: {className}", CompletionKind.Type) { Priority = 40 });
            }
        }

        return completions.OrderByDescending(c => ((CompletionData)c).Priority).ThenBy(c => c.Text);
    }

    public static IEnumerable<ICompletionData> GetMemberCompletions(string typeName, string? allCode = null)
    {
        // Safety check for null/empty type name
        if (string.IsNullOrEmpty(typeName))
            return Enumerable.Empty<ICompletionData>();

        // Check if it's a namespace first
        if (TypeInspector.IsNamespace(typeName))
        {
            var namespaceMembers = TypeInspector.GetNamespaceMembers(typeName);
            if (namespaceMembers.Count > 0)
            {
                return namespaceMembers.Select(m => new CompletionData(m.Name, m.Description, m.Kind));
            }
        }

        // Try to get members using reflection (for known types)
        var reflectedMembers = TypeInspector.GetTypeMembers(typeName);
        if (reflectedMembers.Count > 0)
        {
            return reflectedMembers.Select(m => new CompletionData(m.Name, m.Description, m.Kind));
        }

        // Check custom classes (parsed from user code)
        if (!string.IsNullOrEmpty(allCode))
        {
            UpdateCustomClasses(allCode);
        }

        if (_customClasses.TryGetValue(typeName, out var customMembers))
        {
            return customMembers.Select(m => new CompletionData(m.Name, m.Description, m.Kind));
        }

        // Return empty - no fallback to prevent showing unrelated completions
        return Enumerable.Empty<ICompletionData>();
    }

    private static void UpdateCustomClasses(string allCode)
    {
        // Simple hash to avoid re-parsing unchanged code
        var hash = allCode.GetHashCode().ToString();
        if (hash == _lastCodeHash)
            return;

        _lastCodeHash = hash;
        _customClasses.Clear();

        // Find all class definitions: public class ClassName or class ClassName
        var classPattern = @"\b(?:public\s+)?class\s+(\w+)";
        var classMatches = Regex.Matches(allCode, classPattern);

        foreach (Match classMatch in classMatches)
        {
            var className = classMatch.Groups[1].Value;

            // Skip built-in entry point class
            if (className == "Viz")
                continue;

            // Skip only if it's one of our explicitly known types (not any random type from assemblies)
            // This prevents shadowing important types like Math, Console, VPoint, etc.
            // but allows user-defined classes with names that might exist in other assemblies
            if (TypeInspector.IsKnownType(className))
                continue;

            var members = new List<(string Name, string Description, CompletionKind Kind)>();

            // Find the class body - look for matching braces after class declaration
            var classStart = classMatch.Index;
            var braceStart = allCode.IndexOf('{', classStart);
            if (braceStart == -1) continue;

            var braceCount = 1;
            var braceEnd = braceStart + 1;
            while (braceEnd < allCode.Length && braceCount > 0)
            {
                if (allCode[braceEnd] == '{') braceCount++;
                else if (allCode[braceEnd] == '}') braceCount--;
                braceEnd++;
            }

            var classBody = allCode.Substring(braceStart, braceEnd - braceStart);

            // Find public properties: public Type Name { get; set; } or public Type Name { get; private set; }
            var propPattern = @"public\s+(\w+)\s+(\w+)\s*\{[^}]*\}";
            var propMatches = Regex.Matches(classBody, propPattern);
            foreach (Match propMatch in propMatches)
            {
                var propType = propMatch.Groups[1].Value;
                var propName = propMatch.Groups[2].Value;
                members.Add((propName, $"{propType} property", CompletionKind.Property));
            }

            // Find public fields: public Type Name;
            var fieldPattern = @"public\s+(\w+)\s+(\w+)\s*;";
            var fieldMatches = Regex.Matches(classBody, fieldPattern);
            foreach (Match fieldMatch in fieldMatches)
            {
                var fieldType = fieldMatch.Groups[1].Value;
                var fieldName = fieldMatch.Groups[2].Value;
                members.Add((fieldName, $"{fieldType} field", CompletionKind.Property));
            }

            // Find public methods: public ReturnType MethodName(
            var methodPattern = @"public\s+(?:static\s+)?(\w+)\s+(\w+)\s*\(";
            var methodMatches = Regex.Matches(classBody, methodPattern);
            foreach (Match methodMatch in methodMatches)
            {
                var returnType = methodMatch.Groups[1].Value;
                var methodName = methodMatch.Groups[2].Value;

                // Skip constructors (method name == class name)
                if (methodName == className)
                    continue;

                members.Add((methodName, $"{returnType} method", CompletionKind.Method));
            }

            _customClasses[className] = members;
        }
    }

    public static string? FindVariableType(string text, string variableName, string? allCode = null)
    {
        // Update custom classes if code provided
        if (!string.IsNullOrEmpty(allCode))
        {
            UpdateCustomClasses(allCode);
        }

        // Check for known static classes first
        if (TypeInspector.ResolveType(variableName) != null)
        {
            return variableName;
        }

        // Check if variable name is a custom class (static access)
        if (_customClasses.ContainsKey(variableName))
        {
            return variableName;
        }

        // Check if the variable is an inherited member from a base class
        var currentClass = FindCurrentClass(text);
        if (!string.IsNullOrEmpty(currentClass))
        {
            // Find the base class of the current class
            var baseClassName = FindBaseClass(allCode ?? text, currentClass);
            if (!string.IsNullOrEmpty(baseClassName))
            {
                // Check if the variable is a property/field on the base class
                var baseType = TypeInspector.ResolveType(baseClassName);
                if (baseType != null)
                {
                    var memberType = TypeInspector.GetMemberReturnType(baseType, variableName);
                    if (memberType != null)
                    {
                        return memberType;
                    }
                }
            }
        }

        // Escape variable name for regex
        var escapedVarName = Regex.Escape(variableName);

        // Try to find variable declaration with type (including nested generics like List<List<VPoint>>)
        // Pattern: Type variableName = or Type variableName;
        // Use balanced matching for nested generics by finding the type that ends just before varName
        var typePattern = $@"([\w]+\s*<[^;=]*>|[\w]+)\s+{escapedVarName}\s*[=;]";
        var match = Regex.Match(text, typePattern);
        if (match.Success)
        {
            // Remove internal spaces (e.g., "List <VPoint>" -> "List<VPoint>")
            var typeName = Regex.Replace(match.Groups[1].Value, @"\s+", "");
            // Skip keywords that look like types
            if (typeName != "var" && typeName != "new" && !Keywords.Contains(typeName))
            {
                return typeName;
            }
        }

        // Pattern: var variableName = new Type( or var variableName = new Type<T>(
        var varPattern = $@"\bvar\s+{escapedVarName}\s*=\s*new\s+([\w]+\s*<[^;(]*>|[\w]+)";
        match = Regex.Match(text, varPattern);
        if (match.Success)
        {
            // Remove internal spaces
            return Regex.Replace(match.Groups[1].Value, @"\s+", "");
        }

        // Pattern: var variableName = TypeName.FactoryMethod( - factory method pattern (e.g., VCuboid.ByLengths)
        // This assumes the factory method returns an instance of the class
        var factoryPattern = $@"\bvar\s+{escapedVarName}\s*=\s*([\w]+)\s*\.\s*\w+\s*\(";
        match = Regex.Match(text, factoryPattern);
        if (match.Success)
        {
            var typeName = match.Groups[1].Value;
            // Verify this is a known type (not a variable calling a method)
            if (TypeInspector.IsKnownType(typeName))
            {
                return typeName;
            }
        }

        // Pattern: Type PropertyName { get; - property declaration
        var propPattern = $@"\b(\w+)\s+{escapedVarName}\s*\{{";
        match = Regex.Match(text, propPattern);
        if (match.Success)
        {
            var typeName = match.Groups[1].Value;
            if (typeName != "var" && typeName != "new" && !Keywords.Contains(typeName))
            {
                return typeName;
            }
        }

        // Pattern: Method parameter - (Type? paramName) or (, Type paramName,)
        // Handles nullable types like VPoint? and ref/out/in/params modifiers
        var paramPattern = $@"[\(,]\s*(?:ref\s+|out\s+|in\s+|params\s+)?([\w<>\[\]]+\??)\s+{escapedVarName}\s*[,\)]";
        match = Regex.Match(text, paramPattern);
        if (match.Success)
        {
            var typeName = match.Groups[1].Value;
            // Remove nullable suffix for type lookup
            return typeName.TrimEnd('?');
        }

        // Pattern: foreach (var varName in collection) or foreach (Type varName in collection)
        // Need to infer element type from collection
        // Use non-greedy .+? to match type including nested generics like List<List<VPoint>>
        var foreachPattern = $@"foreach\s*\(\s*(.+?)\s+{escapedVarName}\s+in\s+(\w+)";
        match = Regex.Match(text, foreachPattern);
        if (match.Success)
        {
            var declaredType = match.Groups[1].Value.Trim();
            var collectionName = match.Groups[2].Value;

            if (declaredType != "var")
            {
                // Explicit type declaration
                return Regex.Replace(declaredType, @"\s+", "");
            }

            // Need to find the collection's element type
            // First, find the type of the collection
            var collectionType = FindVariableType(text, collectionName, allCode);
            if (!string.IsNullOrEmpty(collectionType))
            {
                // Extract element type from generic collection (e.g., List<VPoint> -> VPoint)
                var genericMatch = Regex.Match(collectionType, @"<(.+)>$");
                if (genericMatch.Success)
                {
                    var elementType = genericMatch.Groups[1].Value;
                    // Handle nested generics - take the outermost type parameter
                    // For Dictionary<K,V>, take K (but this is simplified)
                    if (elementType.Contains(',') && !elementType.Contains('<'))
                    {
                        // Simple case: Dictionary<string, int> -> string (key type for KeyValuePair)
                        // For now, just return the first type
                        elementType = elementType.Split(',')[0].Trim();
                    }
                    return elementType;
                }
            }
        }

        // Pattern: for loop with index variable - for (int i = 0; ...)
        var forPattern = $@"for\s*\(\s*([\w]+)\s+{escapedVarName}\s*=";
        match = Regex.Match(text, forPattern);
        if (match.Success)
        {
            return match.Groups[1].Value;
        }

        return null;
    }

    /// <summary>
    /// Gets constructor signatures for a custom class defined in user code.
    /// Returns list of signatures in format: "ClassName(Type1 param1, Type2 param2)"
    /// </summary>
    public static List<string> GetCustomConstructorSignatures(string className, string? allCode = null)
    {
        var signatures = new List<string>();

        if (string.IsNullOrEmpty(allCode))
            return signatures;

        // Update cache if needed
        UpdateCustomClasses(allCode);

        // Check if it's a known custom class
        if (!_customClasses.ContainsKey(className))
            return signatures;

        // Find the class definition and its constructors
        var classPattern = $@"\b(?:public\s+)?class\s+{Regex.Escape(className)}\b";
        var classMatch = Regex.Match(allCode, classPattern);
        if (!classMatch.Success)
            return signatures;

        // Find the class body
        var classStart = classMatch.Index;
        var braceStart = allCode.IndexOf('{', classStart);
        if (braceStart == -1)
            return signatures;

        var braceCount = 1;
        var braceEnd = braceStart + 1;
        while (braceEnd < allCode.Length && braceCount > 0)
        {
            if (allCode[braceEnd] == '{') braceCount++;
            else if (allCode[braceEnd] == '}') braceCount--;
            braceEnd++;
        }

        var classBody = allCode.Substring(braceStart, braceEnd - braceStart);

        // Find constructors: public ClassName(params)
        // Pattern matches: public ClassName(anything until closing paren, handling nested parens)
        var ctorPattern = $@"public\s+{Regex.Escape(className)}\s*\(([^)]*)\)";
        var ctorMatches = Regex.Matches(classBody, ctorPattern);

        foreach (Match ctorMatch in ctorMatches)
        {
            var paramsStr = ctorMatch.Groups[1].Value.Trim();

            // Clean up the parameters - remove default values
            var cleanedParams = CleanupParameters(paramsStr);

            signatures.Add($"{className}({cleanedParams})");
        }

        // If no explicit constructor found, add default constructor
        if (signatures.Count == 0)
        {
            signatures.Add($"{className}()");
        }

        return signatures;
    }

    /// <summary>
    /// Finds the base class of a custom class from user code.
    /// </summary>
    private static string? FindBaseClass(string allCode, string className)
    {
        // Pattern: class ClassName : BaseClass or class ClassName : BaseClass, IInterface
        var pattern = $@"\bclass\s+{Regex.Escape(className)}\s*:\s*(\w+)";
        var match = Regex.Match(allCode, pattern);
        if (match.Success)
        {
            return match.Groups[1].Value;
        }
        return null;
    }

    /// <summary>
    /// Cleans up parameter string by removing default values and simplifying.
    /// </summary>
    private static string CleanupParameters(string paramsStr)
    {
        if (string.IsNullOrWhiteSpace(paramsStr))
            return "";

        var result = new List<string>();
        var parameters = SplitParameterList(paramsStr);

        foreach (var param in parameters)
        {
            var trimmed = param.Trim();
            if (string.IsNullOrEmpty(trimmed))
                continue;

            // Remove default value (everything after =)
            var equalsIndex = trimmed.IndexOf('=');
            if (equalsIndex > 0)
                trimmed = trimmed.Substring(0, equalsIndex).Trim();

            // Remove ref/out/in/params modifiers for cleaner display but keep the core type and name
            trimmed = Regex.Replace(trimmed, @"^(ref|out|in|params)\s+", "");

            // Should now be "Type name" or "Type? name"
            result.Add(trimmed);
        }

        return string.Join(", ", result);
    }

    /// <summary>
    /// Splits a parameter list handling nested angle brackets for generics.
    /// </summary>
    private static List<string> SplitParameterList(string paramsStr)
    {
        var result = new List<string>();
        var depth = 0;
        var parenDepth = 0;
        var start = 0;

        for (int i = 0; i < paramsStr.Length; i++)
        {
            var c = paramsStr[i];
            if (c == '<') depth++;
            else if (c == '>') depth--;
            else if (c == '(') parenDepth++;
            else if (c == ')') parenDepth--;
            else if (c == ',' && depth == 0 && parenDepth == 0)
            {
                result.Add(paramsStr.Substring(start, i - start));
                start = i + 1;
            }
        }

        result.Add(paramsStr.Substring(start));
        return result;
    }

    /// <summary>
    /// Extracts the expected type from a "Type variable = new " expression.
    /// Properly handles generic types with commas like Tuple&lt;int, int&gt;.
    /// </summary>
    private static string? ExtractExpectedTypeFromNewExpression(string textBeforeCursor)
    {
        // Check if we're right after "= new " or "= new"
        if (!textBeforeCursor.TrimEnd().EndsWith("new"))
            return null;

        // Work backwards to find the pattern: Type variableName = new
        // We need to parse backwards handling generics properly
        var text = textBeforeCursor.TrimEnd();

        // Remove "new" from the end
        if (text.EndsWith("new"))
            text = text.Substring(0, text.Length - 3).TrimEnd();
        else
            return null;

        // Should now end with "="
        if (!text.EndsWith("="))
            return null;
        text = text.Substring(0, text.Length - 1).TrimEnd();

        // Now extract the variable name (identifier before =)
        var varNameMatch = Regex.Match(text, @"(\w+)\s*$");
        if (!varNameMatch.Success)
            return null;

        text = text.Substring(0, text.Length - varNameMatch.Length).TrimEnd();

        // Now extract the type, handling generics with balanced angle brackets
        // Work backwards from the end
        var typeEnd = text.Length;
        var angleBracketDepth = 0;
        var typeStart = -1;

        for (int i = text.Length - 1; i >= 0; i--)
        {
            var c = text[i];

            if (c == '>')
            {
                angleBracketDepth++;
            }
            else if (c == '<')
            {
                angleBracketDepth--;
            }
            else if (angleBracketDepth == 0)
            {
                // Outside of generic brackets
                if (char.IsLetterOrDigit(c) || c == '_' || c == '?' || c == '[' || c == ']')
                {
                    // Part of the type
                    continue;
                }
                else
                {
                    // Found the start of the type (whitespace or other delimiter)
                    typeStart = i + 1;
                    break;
                }
            }
        }

        if (typeStart == -1)
            typeStart = 0;

        var typeName = text.Substring(typeStart, typeEnd - typeStart).Trim();

        // Validate it's not a keyword or "var"
        if (string.IsNullOrEmpty(typeName) || typeName == "var" || Keywords.Contains(typeName))
            return null;

        return typeName;
    }
}
