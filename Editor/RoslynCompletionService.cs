using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using ICSharpCode.AvalonEdit.CodeCompletion;
using Code2Viz.Execution;

namespace Code2Viz.Editor;

public class RoslynCompletionService
{
    private readonly IEnumerable<MetadataReference> _references;

    public RoslynCompletionService(IEnumerable<MetadataReference>? references = null)
    {
        _references = references ?? new ModuleCompiler().GetReferences();
    }

    public async Task<(List<ICompletionData> Completions, bool IsAfterNew, string Prefix, string? ExpectedType)> GetCompletionsAsync(string code, int position)
    {
        // Single file overload - delegates to multi-file version
        return await GetCompletionsAsync(code, position, Array.Empty<string>());
    }

    public async Task<(List<ICompletionData> Completions, bool IsAfterNew, string Prefix, string? ExpectedType)> GetCompletionsAsync(string code, int position, IEnumerable<string> otherProjectFiles)
    {
        var completions = new List<ICompletionData>();
        bool isAfterNew = false;
        string prefix = "";
        string? expectedType = null;

        try
        {
            // 1. Create Compilation with all project files
            var syntaxTree = CSharpSyntaxTree.ParseText(code);
            
            // Parse other project files as additional syntax trees
            var allTrees = new List<SyntaxTree> { syntaxTree };
            foreach (var otherFile in otherProjectFiles)
            {
                if (!string.IsNullOrWhiteSpace(otherFile))
                {
                    allTrees.Add(CSharpSyntaxTree.ParseText(otherFile));
                }
            }
            
            var compilation = CSharpCompilation.Create(
                "CompletionAnalysis",
                allTrees,
                _references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var semanticModel = compilation.GetSemanticModel(syntaxTree);

            // 2. Determine Context
            var root = await syntaxTree.GetRootAsync();
            var token = root.FindToken(position);

            // Get the prefix being typed (word before cursor)
            prefix = GetPrefixBeforePosition(code, position);

            // Check if we're after 'new' keyword
            isAfterNew = IsAfterNewKeyword(root, position, code);

            // Get expected type from left-hand side of assignment
            if (isAfterNew)
            {
                expectedType = GetExpectedTypeName(code, position);
            }

            // 3. Lookup Symbols
            ImmutableArray<ISymbol> symbols;
            bool isEnumMemberAccess = false;
            bool isStaticTypeAccess = false;
            ITypeSymbol? memberAccessType = null;

            // Handle Member Access (dot)
            var tokenLeft = position > 0 ? root.FindToken(position - 1) : default; // Get token BEFORE cursor

            // Check for proper MemberAccessExpressionSyntax
            if (tokenLeft != default && tokenLeft.IsKind(SyntaxKind.DotToken) && tokenLeft.Parent is MemberAccessExpressionSyntax memberAccess)
            {
                 // First try to get the type of the expression (for instance members)
                 memberAccessType = semanticModel.GetTypeInfo(memberAccess.Expression).Type;

                 // If that fails, check if it's a type name (for static members/enum values)
                 if (memberAccessType == null)
                 {
                     var symbolInfo = semanticModel.GetSymbolInfo(memberAccess.Expression);
                     if (symbolInfo.Symbol is INamedTypeSymbol typeSymbol)
                     {
                         memberAccessType = typeSymbol;
                         isStaticTypeAccess = true;
                     }
                 }
            }
            // Fallback: Check for incomplete member access (e.g., "VFont." with cursor after dot)
            else if (position > 1 && code[position - 1] == '.')
            {
                // Find the identifier before the dot
                var dotPos = position - 1;
                var identEnd = dotPos;
                var identStart = dotPos - 1;
                while (identStart >= 0 && (char.IsLetterOrDigit(code[identStart]) || code[identStart] == '_'))
                {
                    identStart--;
                }
                identStart++;

                if (identStart < identEnd)
                {
                    var identName = code.Substring(identStart, identEnd - identStart);

                    // Try to find this identifier as a type or variable
                    var availableSymbols = semanticModel.LookupSymbols(identStart);
                    foreach (var sym in availableSymbols)
                    {
                        if (sym.Name == identName)
                        {
                            if (sym is INamedTypeSymbol typeSymbol)
                            {
                                memberAccessType = typeSymbol;
                                isStaticTypeAccess = true;
                            }
                            else if (sym is ILocalSymbol local)
                            {
                                memberAccessType = local.Type;
                            }
                            else if (sym is IParameterSymbol param)
                            {
                                memberAccessType = param.Type;
                            }
                            else if (sym is IFieldSymbol field)
                            {
                                memberAccessType = field.Type;
                            }
                            else if (sym is IPropertySymbol prop)
                            {
                                memberAccessType = prop.Type;
                            }
                            break;
                        }
                    }
                }
            }

            // If we found a member access type, lookup its members
            if (memberAccessType != null)
            {
                // Check if we're accessing an enum type (for static enum value completion)
                if (memberAccessType.TypeKind == TypeKind.Enum)
                {
                    isEnumMemberAccess = true;
                }

                symbols = await Task.Run(() =>
                    semanticModel.LookupSymbols(position, container: memberAccessType, includeReducedExtensionMethods: true));
            }
            else
            {
                // Global/Local completion
                symbols = await Task.Run(() =>
                    semanticModel.LookupSymbols(position, includeReducedExtensionMethods: true));
            }

            // Find the statement containing the cursor to detect incomplete declarations
            var containingStatement = token.Parent?.AncestorsAndSelf()
                .OfType<LocalDeclarationStatementSyntax>()
                .FirstOrDefault();

            // Get variable names being declared in the current statement (incomplete declarations)
            var declaringVariables = new HashSet<string>();
            if (containingStatement != null)
            {
                foreach (var variable in containingStatement.Declaration.Variables)
                {
                    declaringVariables.Add(variable.Identifier.Text);
                }
            }

            // 4. Convert to Completion Data
            foreach (var symbol in symbols)
            {
                if (ShouldHide(symbol)) continue;

                // Skip variables that are being declared in the current statement
                if (symbol.Kind == SymbolKind.Local && declaringVariables.Contains(symbol.Name))
                    continue;

                // For enum member access, only show enum fields (the actual values)
                if (isEnumMemberAccess)
                {
                    // Only include fields that are enum members
                    if (symbol.Kind != SymbolKind.Field)
                        continue;
                }

                // For static type access (not enum), only show static members
                if (isStaticTypeAccess && !isEnumMemberAccess)
                {
                    if (!symbol.IsStatic)
                        continue;
                }

                // Create CompletionData based on symbol kind
                var kind = ConvertToCompletionKind(symbol.Kind);

                // If after 'new', only include instantiable types
                if (isAfterNew)
                {
                    if (symbol is INamedTypeSymbol namedType)
                    {
                        // Include classes and structs that can be instantiated
                        if (namedType.TypeKind == TypeKind.Class || namedType.TypeKind == TypeKind.Struct)
                        {
                            if (!namedType.IsAbstract && !namedType.IsStatic)
                            {
                                completions.Add(new CompletionData(symbol.Name, GetDescription(symbol), kind));
                            }
                        }
                    }
                }
                else
                {
                    // Normal completion - include everything
                    var text = symbol.Name;
                    completions.Add(new CompletionData(text, GetDescription(symbol), kind));
                }
            }

            // 5. Add Code2Viz.Geometry types even when not imported (for convenience)
            if (memberAccessType == null && !isAfterNew)
            {
                AddCode2VizTypes(completions, compilation, prefix);
            }

            // 6. Add Keywords (Simple fallback if not handled by LookupSymbols which primarily does identifiers)
            // Roslyn's RecommendSymbols API is better for keywords but internal/more complex.
            // We can stick to a basic list or use the existing keyword list from CompletionProvider for now.
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Roslyn Completion Error: {ex.Message}");
        }

        return (completions, isAfterNew, prefix, expectedType);
    }

    /// <summary>
    /// Gets the word being typed before the cursor position.
    /// </summary>
    private string GetPrefixBeforePosition(string code, int position)
    {
        if (position <= 0 || position > code.Length)
            return "";

        int start = position - 1;
        while (start >= 0 && (char.IsLetterOrDigit(code[start]) || code[start] == '_'))
        {
            start--;
        }
        start++; // Move back to the first character of the word

        if (start < position)
            return code.Substring(start, position - start);
        return "";
    }

    /// <summary>
    /// Checks if the cursor position is after a 'new' keyword.
    /// </summary>
    private bool IsAfterNewKeyword(SyntaxNode root, int position, string code)
    {
        // Simple text-based check: look backwards for 'new' keyword
        var prefix = GetPrefixBeforePosition(code, position);
        var searchStart = position - prefix.Length - 1;

        // Skip whitespace backwards
        while (searchStart >= 0 && char.IsWhiteSpace(code[searchStart]))
        {
            searchStart--;
        }

        // Check if we have 'new' keyword ending at searchStart
        if (searchStart >= 2)
        {
            var potentialNew = code.Substring(searchStart - 2, 3);
            if (potentialNew == "new")
            {
                // Make sure it's not part of a longer identifier
                if (searchStart - 3 < 0 || !char.IsLetterOrDigit(code[searchStart - 3]))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Gets the expected type name from the left-hand side of an assignment.
    /// For example, in "VPoint p1 = new |", returns "VPoint".
    /// </summary>
    private string? GetExpectedTypeName(string code, int position)
    {
        // Look backwards for pattern: TypeName varName = new
        var searchStart = position - 1;

        // Skip whitespace and any partial word being typed
        while (searchStart >= 0 && (char.IsLetterOrDigit(code[searchStart]) || code[searchStart] == '_'))
            searchStart--;
        while (searchStart >= 0 && char.IsWhiteSpace(code[searchStart]))
            searchStart--;

        // Should be at 'new' keyword - skip it
        if (searchStart >= 2 && code.Substring(searchStart - 2, 3) == "new")
        {
            searchStart -= 3;
        }
        else
        {
            return null;
        }

        // Skip whitespace before 'new'
        while (searchStart >= 0 && char.IsWhiteSpace(code[searchStart]))
            searchStart--;

        // Should be at '=' sign
        if (searchStart < 0 || code[searchStart] != '=')
            return null;
        searchStart--;

        // Skip whitespace before '='
        while (searchStart >= 0 && char.IsWhiteSpace(code[searchStart]))
            searchStart--;

        // Now we should be at the end of the variable name - skip it
        if (searchStart < 0 || !(char.IsLetterOrDigit(code[searchStart]) || code[searchStart] == '_'))
            return null;

        while (searchStart >= 0 && (char.IsLetterOrDigit(code[searchStart]) || code[searchStart] == '_'))
            searchStart--;

        // Skip whitespace before variable name
        while (searchStart >= 0 && char.IsWhiteSpace(code[searchStart]))
            searchStart--;

        // Now extract the type name (could include generics, arrays, namespaces)
        if (searchStart < 0)
            return null;

        int typeEnd = searchStart + 1;

        // Handle generic types like List<T> by tracking angle brackets
        int angleBrackets = 0;
        if (code[searchStart] == '>')
        {
            angleBrackets = 1;
            searchStart--;
            while (searchStart >= 0 && angleBrackets > 0)
            {
                if (code[searchStart] == '>') angleBrackets++;
                else if (code[searchStart] == '<') angleBrackets--;
                searchStart--;
            }
        }

        // Now get the type identifier
        while (searchStart >= 0 && (char.IsLetterOrDigit(code[searchStart]) || code[searchStart] == '_' || code[searchStart] == '.'))
            searchStart--;

        int typeStart = searchStart + 1;
        if (typeStart < typeEnd)
        {
            var fullType = code.Substring(typeStart, typeEnd - typeStart);
            // Extract just the type name (last part after any dots, before any <)
            var ltIndex = fullType.IndexOf('<');
            if (ltIndex > 0) fullType = fullType.Substring(0, ltIndex);
            var dotIndex = fullType.LastIndexOf('.');
            if (dotIndex >= 0) fullType = fullType.Substring(dotIndex + 1);
            return fullType;
        }

        return null;
    }

    /// <summary>
    /// Adds Code2Viz.Geometry types to completions even when not imported.
    /// This makes it easier for users to discover and use geometry types.
    /// </summary>
    private void AddCode2VizTypes(List<ICompletionData> completions, CSharpCompilation compilation, string prefix)
    {
        var existingNames = new HashSet<string>(completions.Select(c => c.Text));

        // Find Code2Viz.Geometry namespace in the compilation
        foreach (var reference in compilation.References)
        {
            var assemblySymbol = compilation.GetAssemblyOrModuleSymbol(reference) as IAssemblySymbol;
            if (assemblySymbol == null) continue;

            // Look for Code2Viz.Geometry namespace
            var geometryNs = assemblySymbol.GlobalNamespace
                .GetNamespaceMembers()
                .FirstOrDefault(ns => ns.Name == "Code2Viz")?
                .GetNamespaceMembers()
                .FirstOrDefault(ns => ns.Name == "Geometry");

            if (geometryNs != null)
            {
                foreach (var type in geometryNs.GetTypeMembers())
                {
                    // Only add public types that start with V (our naming convention)
                    if (type.DeclaredAccessibility == Microsoft.CodeAnalysis.Accessibility.Public &&
                        type.Name.StartsWith("V") &&
                        !existingNames.Contains(type.Name))
                    {
                        var kind = type.TypeKind == TypeKind.Enum ? CompletionKind.Type : CompletionKind.Type;
                        completions.Add(new CompletionData(type.Name, GetDescription(type), kind));
                        existingNames.Add(type.Name);
                    }
                }

                // Also add enums like VFont, VFontWeight, LineType
                foreach (var type in geometryNs.GetTypeMembers())
                {
                    if (type.DeclaredAccessibility == Microsoft.CodeAnalysis.Accessibility.Public &&
                        type.TypeKind == TypeKind.Enum &&
                        !type.Name.StartsWith("V") &&
                        !existingNames.Contains(type.Name))
                    {
                        completions.Add(new CompletionData(type.Name, GetDescription(type), CompletionKind.Type));
                        existingNames.Add(type.Name);
                    }
                }
            }

            // Also check Code2Viz.Console namespace
            var consoleNs = assemblySymbol.GlobalNamespace
                .GetNamespaceMembers()
                .FirstOrDefault(ns => ns.Name == "Code2Viz")?
                .GetNamespaceMembers()
                .FirstOrDefault(ns => ns.Name == "Console");

            if (consoleNs != null)
            {
                foreach (var type in consoleNs.GetTypeMembers())
                {
                    if (type.DeclaredAccessibility == Microsoft.CodeAnalysis.Accessibility.Public &&
                        !existingNames.Contains(type.Name))
                    {
                        completions.Add(new CompletionData(type.Name, GetDescription(type), CompletionKind.Type));
                        existingNames.Add(type.Name);
                    }
                }
            }
        }
    }

    private bool ShouldHide(ISymbol symbol)
    {
        // Hide backing fields, generated code, etc.
        if (symbol.Name.Contains("<") || symbol.Name.Contains("$")) return true;

        // Hide explicit interface implementations (contain dots)
        if (symbol.Name.Contains(".")) return true;

        // Hide constructor methods (they appear as .ctor)
        if (symbol.IsImplicitlyDeclared && symbol.Kind == SymbolKind.Method) return true;
        if (symbol.Name == ".ctor") return true;

        // Hide obsolete symbols
        if (symbol.GetAttributes().Any(a => a.AttributeClass?.Name == "ObsoleteAttribute")) return true;

        // Hide base Object members that clutter completions (users rarely need these)
        if (symbol is IMethodSymbol method)
        {
            var objectMembers = new HashSet<string>
            {
                "GetHashCode", "GetType", "Equals", "ToString", "MemberwiseClone",
                "Finalize", "ReferenceEquals"
            };
            if (objectMembers.Contains(method.Name) && 
                method.ContainingType?.SpecialType == SpecialType.System_Object)
            {
                return true;
            }
            
            // Also hide these when inherited (shown on all types)
            if (objectMembers.Contains(method.Name) && method.IsOverride)
            {
                // Check if it's the default object override
                var baseMethod = method.OverriddenMethod;
                while (baseMethod != null)
                {
                    if (baseMethod.ContainingType?.SpecialType == SpecialType.System_Object)
                        return true;
                    baseMethod = baseMethod.OverriddenMethod;
                }
            }
        }

        // Hide generic type parameters (T, TKey, TValue, etc.)
        if (symbol is ITypeParameterSymbol)
            return true;

        // For types, filter out irrelevant system types
        if (symbol is INamedTypeSymbol typeSymbol)
        {
            var ns = typeSymbol.ContainingNamespace?.ToDisplayString() ?? "";

            // Hide types from low-level runtime namespaces
            if (ns.StartsWith("System.Runtime") ||
                ns.StartsWith("System.Reflection") ||
                ns.StartsWith("System.Diagnostics") ||
                ns.StartsWith("System.Threading") ||
                ns.StartsWith("System.Security") ||
                ns.StartsWith("System.Globalization") ||
                ns.StartsWith("System.ComponentModel") ||
                ns.StartsWith("System.CodeDom") ||
                ns.StartsWith("System.Configuration") ||
                ns.StartsWith("System.Resources") ||
                ns.StartsWith("System.Text.RegularExpressions") ||
                ns.StartsWith("System.Buffers") ||
                ns.StartsWith("System.Private") ||
                ns.StartsWith("Internal") ||
                ns.StartsWith("Microsoft.") ||
                ns.StartsWith("FSharp.") ||
                ns.StartsWith("Interop"))
            {
                return true;
            }

            // Hide Action/Func delegates with many type parameters (keep simple ones)
            if ((typeSymbol.Name == "Action" || typeSymbol.Name == "Func") &&
                typeSymbol.TypeParameters.Length > 4)
            {
                return true;
            }

            // Hide obscure System types that aren't useful for geometry coding
            var hiddenTypes = new HashSet<string>
            {
                "Buffer", "DBNull", "GCKind", "GCNotificationStatus", "GCCollectionMode",
                "IntPtr", "UIntPtr", "Int128", "UInt128", "Half", "NFloat",
                "RuntimeTypeHandle", "RuntimeMethodHandle", "RuntimeFieldHandle",
                "TypedReference", "ArgIterator", "ModuleHandle", "GCHandle",
                "Span", "ReadOnlySpan", "Memory", "ReadOnlyMemory",
                "Index", "Range", "HashCode", "MemoryExtensions",
                "ArraySegment", "Nullable", "WeakReference",
                "Activator", "AppDomain", "AppContext", "Environment",
                "GC", "BitConverter", "Convert", "FormattableString",
                "Progress", "Lazy", "Lookup", "Grouping",
                "ValueType", "Enum", "Delegate", "MulticastDelegate",
                "Attribute", "MarshalByRefObject", "ContextBoundObject"
            };
            if (hiddenTypes.Contains(typeSymbol.Name))
            {
                return true;
            }
        }

        return false;
    }

    private CompletionKind ConvertToCompletionKind(SymbolKind kind)
    {
        return kind switch
        {
            SymbolKind.Field => CompletionKind.Property, // Reusing Property color for fields
            SymbolKind.Property => CompletionKind.Property,
            SymbolKind.Method => CompletionKind.Method,
            SymbolKind.Local => CompletionKind.Property, // Local variable
            SymbolKind.Parameter => CompletionKind.Property,
            SymbolKind.Event => CompletionKind.Property,
            SymbolKind.NamedType => CompletionKind.Type,
            SymbolKind.Namespace => CompletionKind.Type, // Use Type color for namespace
            _ => CompletionKind.Property
        };
    }

    private string GetDescription(ISymbol symbol)
    {
        return symbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
    }

    public async Task<(List<string> Signatures, int CurrentParameterIndex)> GetSignatureHelpAsync(string code, int position)
    {
        try 
        {
             var syntaxTree = CSharpSyntaxTree.ParseText(code);
             var compilation = CSharpCompilation.Create(
                "SignatureAnalysis",
                new[] { syntaxTree },
                _references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

             var semanticModel = compilation.GetSemanticModel(syntaxTree);
             var root = await syntaxTree.GetRootAsync();
             var token = root.FindToken(position);
             
             // Move back if we are at the closing parenthesis or inside
             if (token.IsKind(SyntaxKind.CloseParenToken))
             {
                 token = root.FindToken(position - 1);
             }

             // Find Invocation or ObjectCreation
             var node = token.Parent;
             while (node != null && 
                    !(node is InvocationExpressionSyntax) && 
                    !(node is ObjectCreationExpressionSyntax) &&
                    !(node is BaseObjectCreationExpressionSyntax)) // Covers both new T() and implicit new()
             {
                 node = node.Parent;
             }

             if (node == null) return (new List<string>(), 0);

             ArgumentListSyntax? argList = null;
             if (node is InvocationExpressionSyntax inv) argList = inv.ArgumentList;
             else if (node is ObjectCreationExpressionSyntax obj) argList = obj.ArgumentList;
             // else if (node is ImplicitObjectCreationExpressionSyntax impl) argList = impl.ArgumentList; // C# 9

             if (argList == null) return (new List<string>(), 0);

             // Calculate current parameter index
             int paramIndex = 0;
             var spanBefore = TextSpan.FromBounds(argList.OpenParenToken.Span.End, position);
             // Count commas in the span
             var textInSpan = code.Substring(spanBefore.Start, spanBefore.Length);
             paramIndex = textInSpan.Count(c => c == ',');

             // Get symbols
             var symbolInfo = semanticModel.GetSymbolInfo(node);
             var candidates = symbolInfo.CandidateSymbols.Any() ? symbolInfo.CandidateSymbols : (symbolInfo.Symbol != null ? ImmutableArray.Create(symbolInfo.Symbol) : ImmutableArray<ISymbol>.Empty);

             var signatures = new List<string>();
             foreach (var symbol in candidates)
             {
                 if (symbol is IMethodSymbol method)
                 {
                     signatures.Add(method.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
                 }
             }

             return (signatures, paramIndex);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Signature Help Error: {ex.Message}");
            return (new List<string>(), 0);
        }
    }
    public async Task<(string Kind, string TypeName, string Name, string Documentation)?> GetQuickInfoAsync(string code, int position)
    {
        try
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(code);
            var compilation = CSharpCompilation.Create(
                "QuickInfoAnalysis",
                new[] { syntaxTree },
                _references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var semanticModel = compilation.GetSemanticModel(syntaxTree);
            var root = await syntaxTree.GetRootAsync();
            var token = root.FindToken(position);

            // If we are on a keyword or whitespace, maybe move slightly?
            // Actually FindToken(position) finds the token that contains the position.
            
            // Check lookup symbol
            var node = token.Parent;
            if (node == null) return null;

            ISymbol? symbol = null;
            
            // Try GetSymbolInfo
            var symbolInfo = semanticModel.GetSymbolInfo(node);
            symbol = symbolInfo.Symbol;

            // Fallback for declarations (GetDeclaredSymbol)
            if (symbol == null)
            {
                 symbol = semanticModel.GetDeclaredSymbol(node);
            }

            if (symbol == null) return null;

            // Extract Info
            var kind = GetKindString(symbol);
            var typeName = GetSymbolType(symbol);
            var name = symbol.Name;
            var doc = symbol.GetDocumentationCommentXml();
            
            // If internal XML doc is empty, try to get standard description
            if (string.IsNullOrEmpty(doc))
            {
                // We will rely on UI to render simple description if doc is missing
                // Or we can parse the XML here.
                // Let's return the raw XML or summary.
                // For simplicity, let's just return the DisplayString as fallback documentation if XML is missing?
                // Actually, let's leave documentation null if missing, UI handles it.
            }
            else
            {
                // Parse XML to just get summary
                try 
                {
                    var xmlDoc = new System.Xml.XmlDocument();
                    xmlDoc.LoadXml(doc);
                    var summary = xmlDoc.SelectSingleNode("//summary")?.InnerText?.Trim();
                    if (!string.IsNullOrEmpty(summary)) doc = summary;
                }
                catch { /* ignore xml parse error */ }
            }

            return (kind, typeName, name, doc);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"QuickInfo Error: {ex.Message}");
            return null;
        }
    }

    private string GetKindString(ISymbol symbol)
    {
        return symbol.Kind switch
        {
            SymbolKind.Local => "local",
            SymbolKind.Parameter => "parameter",
            SymbolKind.Field => "field",
            SymbolKind.Property => "property",
            SymbolKind.Method => "method",
            SymbolKind.NamedType => symbol is INamedTypeSymbol nt && nt.TypeKind == TypeKind.Interface ? "interface" : 
                                    symbol is INamedTypeSymbol nt2 && nt2.TypeKind == TypeKind.Struct ? "struct" : "class",
            _ => "symbol"
        };
    }

    private string GetSymbolType(ISymbol symbol)
    {
        if (symbol is ILocalSymbol local) return local.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        if (symbol is IParameterSymbol param) return param.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        if (symbol is IFieldSymbol field) return field.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        if (symbol is IPropertySymbol prop) return prop.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        if (symbol is IMethodSymbol method) return method.ReturnType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        if (symbol is INamedTypeSymbol type) return type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        
        return symbol.Name;
    }
}
