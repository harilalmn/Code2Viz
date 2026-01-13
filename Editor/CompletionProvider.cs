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
            var match = Regex.Match(textBeforeCursor, @"(?:\b|^)([\w<>\[\]?]+)\s+(\w+)\s*=\s*new\s*$");
            if (match.Success)
            {
                var typeName = match.Groups[1].Value;
                if (typeName != "var" && !Keywords.Contains(typeName))
                {
                    expectedType = typeName;
                }
            }
        }

        // Add matching type first if found
        if (expectedType != null)
        {
            // Check common types
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
            if (!string.IsNullOrEmpty(currentClass) && _customClasses.TryGetValue(currentClass, out var classMembers))
            {
                foreach (var member in classMembers)
                {
                    if (string.IsNullOrEmpty(prefix) || member.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        completions.Add(new CompletionData(member.Name, member.Description, member.Kind));
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
    /// Finds local variables declared before the cursor position in the current scope.
    /// </summary>
    private static List<(string Name, string Type)> FindLocalVariables(string textBeforeCursor)
    {
        var variables = new List<(string Name, string Type)>();

        // Find the start of the current method body
        var lastMethodBrace = textBeforeCursor.LastIndexOf('{');
        if (lastMethodBrace == -1)
            return variables;

        // Find the innermost scope by tracking brace depth from the end
        var braceDepth = 0;
        var scopeStart = textBeforeCursor.Length;
        for (int i = textBeforeCursor.Length - 1; i >= 0; i--)
        {
            if (textBeforeCursor[i] == '}') braceDepth++;
            else if (textBeforeCursor[i] == '{')
            {
                if (braceDepth == 0)
                {
                    scopeStart = i + 1;
                    break;
                }
                braceDepth--;
            }
        }

        var scopeText = textBeforeCursor.Substring(scopeStart);

        // Match variable declarations: "Type name = " or "Type name;" or "var name = "
        var varPattern = @"\b(var|[\w<>\[\]?]+)\s+(\w+)\s*[=;]";
        var matches = Regex.Matches(scopeText, varPattern);

        foreach (Match match in matches)
        {
            var varType = match.Groups[1].Value;
            var varName = match.Groups[2].Value;

            // Skip if it looks like a method call or keyword
            if (!Keywords.Contains(varType) && !Keywords.Contains(varName))
            {
                variables.Add((varName, varType));
            }
        }

        return variables;
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
            // Regex to find: Type variable = new $
            // We look backwards from the end
            var match = Regex.Match(textBeforeCursor, @"(?:\b|^)([\w<>\[\]?]+)\s+(\w+)\s*=\s*new\s*$");
            if (match.Success)
            {
                var typeName = match.Groups[1].Value;
                if (typeName != "var" && !Keywords.Contains(typeName))
                {
                    expectedType = typeName;
                }
            }
        }

        // Add matching type first if found
        if (expectedType != null)
        {
            // Check common types
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

        // Escape variable name for regex
        var escapedVarName = Regex.Escape(variableName);

        // Try to find variable declaration with type
        // Pattern: Type variableName = or Type variableName;
        var typePattern = $@"\b(\w+)\s+{escapedVarName}\s*[=;]";
        var match = Regex.Match(text, typePattern);
        if (match.Success)
        {
            var typeName = match.Groups[1].Value;
            // Skip keywords that look like types
            if (typeName != "var" && typeName != "new" && !Keywords.Contains(typeName))
            {
                return typeName;
            }
        }

        // Pattern: var variableName = new Type(
        var varPattern = $@"\bvar\s+{escapedVarName}\s*=\s*new\s+(\w+)";
        match = Regex.Match(text, varPattern);
        if (match.Success)
        {
            return match.Groups[1].Value;
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

        // Pattern: var variableName = SomeMethod() - harder to resolve, skip for now

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
}
