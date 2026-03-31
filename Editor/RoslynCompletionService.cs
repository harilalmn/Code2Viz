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
    private readonly CachedCompilationWorkspace? _workspace;

    public RoslynCompletionService(IEnumerable<MetadataReference>? references = null)
    {
        _references = references ?? new ModuleCompiler().GetReferences();
    }

    /// <summary>
    /// Constructor that uses a CachedCompilationWorkspace for incremental compilation.
    /// The workspace must already have the files loaded.
    /// </summary>
    public RoslynCompletionService(CachedCompilationWorkspace workspace)
    {
        _workspace = workspace;
        _references = Array.Empty<MetadataReference>(); // Not used when workspace is provided
    }

    public async Task<(List<ICompletionData> Completions, bool IsAfterNew, string Prefix, string? ExpectedType)> GetCompletionsAsync(string code, int position)
    {
        // Single file overload - delegates to multi-file version
        return await GetCompletionsAsync(code, position, Array.Empty<string>());
    }

    /// <summary>
    /// Overload that uses the CachedCompilationWorkspace. The active file must be
    /// identified by fileId so the correct syntax tree / semantic model is used.
    /// </summary>
    public async Task<(List<ICompletionData> Completions, bool IsAfterNew, string Prefix, string? ExpectedType)> GetCompletionsAsync(
        string code, int position, CachedCompilationWorkspace workspace, string fileId)
    {
        // Ensure the workspace has the latest content for this file
        workspace.UpdateFile(fileId, code);

        var compilation = workspace.GetCompilation();
        var syntaxTree = workspace.GetSyntaxTree(fileId);
        var semanticModel = workspace.GetSemanticModel(fileId);

        if (syntaxTree == null || semanticModel == null)
            return (new List<ICompletionData>(), false, "", null);

        return await GetCompletionsInternal(code, position, compilation, syntaxTree, semanticModel);
    }

    public async Task<(List<ICompletionData> Completions, bool IsAfterNew, string Prefix, string? ExpectedType)> GetCompletionsAsync(string code, int position, IEnumerable<string> otherProjectFiles)
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

        return await GetCompletionsInternal(code, position, compilation, syntaxTree, semanticModel);
    }

    private async Task<(List<ICompletionData> Completions, bool IsAfterNew, string Prefix, string? ExpectedType)> GetCompletionsInternal(
        string code, int position, CSharpCompilation compilation, SyntaxTree syntaxTree, SemanticModel semanticModel)
    {
        var completions = new List<ICompletionData>();
        bool isAfterNew = false;
        string prefix = "";
        string? expectedType = null;

        try
        {

            // 2. Determine Context
            var root = await syntaxTree.GetRootAsync();
            var token = root.FindToken(position); // Finds the token at or immediately preceding position
            
            // If we are at the end of a word, FindToken might return that word. 
            // If we are int he middle of whitespace, it might return the previous token.
            // Adjust to ensure we are looking at the token to the left if we are strictly modifying it
            if (token.Span.Start >= position && position > 0)
            {
                token = root.FindToken(position - 1);
            }

            // Get the prefix being typed (word before cursor)
            prefix = GetPrefixBeforePosition(code, position);

            // Check if we're after 'new' keyword
            isAfterNew = IsAfterNewKeyword(root, position, code);

            // Determine expected type from context (Assignment or Method Argument)
            // valid even if not after 'new' (e.g. "VPoint p = |")
            expectedType = GetExpectedTypeFromContext(root, semanticModel, position, code);

            // 3. Lookup Symbols
            ImmutableArray<ISymbol> symbols;
            bool isEnumMemberAccess = false;
            bool isStaticTypeAccess = false;
            INamespaceOrTypeSymbol? memberAccessContainer = null;

            // Handle Member Access (dot)
            // Check if strict member access syntax
            var tokenLeft = position > 0 ? root.FindToken(position - 1) : default;
            
            // Refined check for dot:
            // Check both immediately before position (just typed dot) AND before the prefix
            // (user typed letters after a dot, e.g. "roomTypeQ.Enq" — prefix is "Enq", dot is before it)
            bool isDot = position > 0 && code[position - 1] == '.';
            if (!isDot && !string.IsNullOrEmpty(prefix))
            {
                var dotCheckPos = position - prefix.Length - 1;
                if (dotCheckPos >= 0 && code[dotCheckPos] == '.')
                {
                    isDot = true;
                    // Adjust position context: the dot is before the prefix
                }
            }
            
            if (isDot)
            {
                // We are explicitly after a dot.
                // Re-parsing or finding the expression before the dot is safest.
                // Simple approach: parse the expression before the dot.
                
                // Find start of expression before dot
                // dotPos is the actual position of the '.' character
                var dotPos = (position > 0 && code[position - 1] == '.') 
                    ? position - 1 
                    : position - prefix.Length - 1;
                // Walk back simple dotted identifiers
                var exprEnd = dotPos;
                var exprStart = dotPos - 1;
                int parenDepth = 0;
                
                // Allow walking back over . and identifier characters and parens (method calls)
                while (exprStart >= 0)
                {
                    char c = code[exprStart];
                    if (c == ')') parenDepth++;
                    else if (c == '(')
                    {
                        if (parenDepth > 0) parenDepth--;
                        else break; // Unbalanced
                    }
                    else if (c == ';' || c == '{' || c == '}') break; // Boundary
                    else if (char.IsWhiteSpace(c) && parenDepth == 0) 
                    {
                        // Stop at whitespace if not in parens? 
                        // Actually, "A . B" is valid, but usually "A.B". Let's assume no spaces for simple completion.
                         if (exprStart < dotPos - 1 && !char.IsWhiteSpace(code[exprStart+1])) break;
                    }
                    else if (!char.IsLetterOrDigit(c) && c != '_' && c != '.' && c != ')' && c != '(' && c != '"' && c != '\'')
                    {
                       // Stop at operators like +, =, etc.
                       break;
                    }
                    exprStart--;
                }
                exprStart++;
                
                if (exprStart < exprEnd)
                {
                    var exprText = code.Substring(exprStart, exprEnd - exprStart).Trim();
                    if (!string.IsNullOrEmpty(exprText))
                    {
                        // Try to bind this expression
                        // We need a proper syntax node. find the node at dotPos - 1
                        var nodeBeforeDot = root.FindToken(dotPos - 1).Parent;
                        
                        // If the tree is well-formed enough, Roslyn handles it.
                        // If we are adding a dot to a complete statement, the tree might be "Expression . <Missing>"
                        
                        if (nodeBeforeDot != null)
                        {
                            var typeInfo = semanticModel.GetTypeInfo(nodeBeforeDot);
                            memberAccessContainer = typeInfo.Type;
                            
                            if (memberAccessContainer == null)
                            {
                                // Maybe it's a namespace or type name (statics)
                                var symbolInfo = semanticModel.GetSymbolInfo(nodeBeforeDot);
                                if (symbolInfo.Symbol is INamedTypeSymbol typeSym)
                                {
                                    memberAccessContainer = typeSym;
                                    isStaticTypeAccess = true;
                                }
                                else if (symbolInfo.Symbol is INamespaceSymbol nsSym)
                                {
                                    memberAccessContainer = nsSym;
                                }
                            }
                        }
                    }
                }
            }

            // If we found a member access type, lookup its members
            if (memberAccessContainer != null)
            {
                // Check if we're accessing an enum type (for static enum value completion)
                if (memberAccessContainer is ITypeSymbol typeSym && typeSym.TypeKind == TypeKind.Enum)
                {
                    isEnumMemberAccess = true;
                }

                symbols = await Task.Run(() =>
                    semanticModel.LookupSymbols(position, container: memberAccessContainer, includeReducedExtensionMethods: true));
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
                var scope = ClassifyScope(symbol, token);

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
                                completions.Add(new CompletionData(symbol.Name, GetDescription(symbol), kind) { Symbol = symbol, Scope = scope });
                            }
                        }
                    }
                }
                else
                {
                    // Normal completion - include everything
                    var text = symbol.Name;
                    completions.Add(new CompletionData(text, GetDescription(symbol), kind) { Symbol = symbol, Scope = scope });
                }
            }

            // 5. Add expected type as a special completion item
            // This ensures expected types (e.g. from assignments or method args) are available
            if (expectedType != null)
            {
                var expectedTypeName = expectedType;
                
                // Cleanup generic names if coming fully qualified from Roslyn (e.g. System.Collections.Generic.List<int>)
                // ExpectedType from our helper might already be simplified, but let's ensure.
                // For 'new' context, we want the constructor call "new List<int>()"
                // For assignment context, we just want the type name "List<int>" or "VPoint"
                
                var existingNames = new HashSet<string>(completions.Select(c => c.Text));
                
                if (isAfterNew && expectedType.Contains("<"))
                {
                     if (!existingNames.Contains(expectedTypeName))
                     {
                         // Add the full generic type as a high-priority completion
                          completions.Insert(0, new CompletionData(expectedTypeName, $"new {expectedTypeName}()", CompletionKind.Type));
                     }
                }
                else if (!isAfterNew)
                {
                    // For local variable assignment: VPoint p = |
                    // We want "VPoint" to be in the list.
                    // If it's a simple type name, it might already be there from LookupSymbols.
                    // But if it was filtered or is a specific construction, ensure it's there.
                    
                    // If expected type is simple (VPoint), check if it's already there
                    if (!existingNames.Contains(expectedTypeName))
                    {
                         // Add it if missing (e.g. if it was filtered for some reason, or if we want to boost it)
                         // Note: We don't have the symbol here easily to get description, so simple description.
                         completions.Insert(0, new CompletionData(expectedTypeName, expectedTypeName, CompletionKind.Type));
                    }
                }
            }

            // 6. Add Code2Viz.Geometry types even when not imported (for convenience)
            if (memberAccessContainer == null && !isAfterNew)
            {
                AddCode2VizTypes(completions, compilation, prefix);
            }
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
    /// Determines the expected type at the current position based on context (Assignment, Method Argument, etc.)
    /// </summary>
    private string? GetExpectedTypeFromContext(SyntaxNode root, SemanticModel semanticModel, int position, string code)
    {
        try
        {
            var token = root.FindToken(position);

            // 1. Assignment Context: Type x = |
            // Look for VariableDeclarator or AssignmentExpression
            var node = token.Parent;
            while (node != null)
            {
                if (node is EqualsValueClauseSyntax equalsNode)
                {
                    // var x = ...; found the '='
                    if (equalsNode.Parent is VariableDeclaratorSyntax varDecl)
                    {
                        // It's a variable declaration: Type x = ...
                        // We need the type of 'x'
                        if (varDecl.Parent is VariableDeclarationSyntax decl)
                        {
                            var type = decl.Type;
                            // If 'var', we can't infer from LHS (that's the point of var)
                            if (type.IsVar) return null;
                            
                            var typeSymbol = semanticModel.GetTypeInfo(type).Type;
                            return typeSymbol?.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
                        }
                    }
                }
                else if (node is AssignmentExpressionSyntax assignment)
                {
                    // x = ...
                    // We need the type of 'x' (Left)
                     var left = assignment.Left;
                     var typeSymbol = semanticModel.GetTypeInfo(left).Type;
                     return typeSymbol?.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
                }
                else if (node is ArgumentListSyntax argList)
                {
                    // 2. Method Argument Context: Method(|)
                    // We are in an argument list. Find which argument we are.
                    
                    if (node.Parent is InvocationExpressionSyntax invocation)
                    {
                        // Determine parameter index
                        int paramIndex = 0;
                        // Simple comma counting similar to SignatureHelp
                        // (Ideally we map arguments to parameters properly using name: etc, but simpler for now)
                        var spanBefore = TextSpan.FromBounds(argList.OpenParenToken.Span.End, position);
                        var textInSpan = code.Substring(spanBefore.Start, Math.Min(spanBefore.Length, code.Length - spanBefore.Start));
                        paramIndex = textInSpan.Count(c => c == ',');
                        
                        var symbolInfo = semanticModel.GetSymbolInfo(invocation);
                        var methodSymbol = symbolInfo.Symbol as IMethodSymbol ?? symbolInfo.CandidateSymbols.FirstOrDefault() as IMethodSymbol;
                        
                        if (methodSymbol != null)
                        {
                            if (paramIndex < methodSymbol.Parameters.Length)
                            {
                                return methodSymbol.Parameters[paramIndex].Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
                            }
                            // Handle params array?
                            else if (methodSymbol.Parameters.Length > 0 && methodSymbol.Parameters.Last().IsParams)
                            {
                                var lastParam = methodSymbol.Parameters.Last();
                                if (lastParam.Type is IArrayTypeSymbol arrayType)
                                {
                                    return arrayType.ElementType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
                                }
                            }
                        }
                    }
                    // Break after finding arg list to avoid going up to other contexts
                    break;
                }
                
                node = node.Parent;
            }

            // Fallback to text-based parsing for 'new' context or incomplete trees if Roslyn failed
            return GetExpectedTypeName(code, position);

        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Legacy text-based fallback for expected type. (Updated to use new logic primarily)
    /// Left here as a backup for 'new' scenarios which might be incomplete syntax.
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

        // Check for 'new' keyword
        bool hasNew = false;
        if (searchStart >= 2 && code.Substring(searchStart - 2, 3) == "new")
        {
            hasNew = true;
            searchStart -= 3;
        }

        // If explicitly checking for assignment without new (text fallback), we need to be careful.
        // But for "VPoint p = new |", this logic works.
        // For "VPoint p = |", we should have caught it in Roslyn traversal above.
        
        if (!hasNew) return null; // Logic below relies on skipping 'new'

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
            // Remove namespace prefix but preserve generic type arguments
            // e.g., "System.Collections.Generic.List<double>" -> "List<double>"
            var ltIndex = fullType.IndexOf('<');
            if (ltIndex > 0)
            {
                var basePart = fullType.Substring(0, ltIndex);
                var genericPart = fullType.Substring(ltIndex); // includes <...>
                var dotIndex = basePart.LastIndexOf('.');
                if (dotIndex >= 0) basePart = basePart.Substring(dotIndex + 1);
                return basePart + genericPart;
            }
            else
            {
                var dotIndex = fullType.LastIndexOf('.');
                if (dotIndex >= 0) fullType = fullType.Substring(dotIndex + 1);
                return fullType;
            }
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

        // Hide helper namespaces (MS, ABI, etc.)
        if (symbol is INamespaceSymbol nsSymbol)
        {
            var name = nsSymbol.Name;
            if (name == "MS" || name == "ABI" || name == "Windows" || name == "Internal" || name == "XamlGeneratedNamespace" || name == "FXAssembly")
                return true;
        }

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
            // Hide System.Void
            if (typeSymbol.SpecialType == SpecialType.System_Void) return true;

            // Hide Primitives (Byte, Int32, String, Boolean, etc.)
            // Users should use keywords (int, string, bool) which are provided by AvalonEdit/Roslyn separately,
            // or we just want to declutter. Providing "Int32" AND "int" is noise. "int" is usually done via keywords.
            // Roslyn sometimes provides the struct Int32. Let's hide the struct if it matches a keyword type.
            switch (typeSymbol.SpecialType)
            {
                case SpecialType.System_Boolean:
                case SpecialType.System_Char:
                case SpecialType.System_SByte:
                case SpecialType.System_Byte:
                case SpecialType.System_Int16:
                case SpecialType.System_UInt16:
                case SpecialType.System_Int32:
                case SpecialType.System_UInt32:
                case SpecialType.System_Int64:
                case SpecialType.System_UInt64:
                case SpecialType.System_Decimal:
                case SpecialType.System_Single:
                case SpecialType.System_Double:
                case SpecialType.System_String:
                case SpecialType.System_Object:
                    return true;
            }

            var ns = typeSymbol.ContainingNamespace?.ToDisplayString() ?? "";

            // Hide types from low-level runtime namespaces
            // EXPANDED LIST based on analysis
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
                ns.StartsWith("FSharp.") ||
                ns.StartsWith("Interop") ||
                // New additions to further declutter
                ns.StartsWith("System.Collections.Concurrent") ||
                ns.StartsWith("System.IO.Compression") ||
                ns.StartsWith("System.IO.MemoryMappedFiles") ||
                ns.StartsWith("System.IO.Pipes") ||
                ns.StartsWith("System.Net") ||
                ns.StartsWith("System.Xml") ||
                ns.StartsWith("System.Data"))
            {
                return true;
            }

            // Hide generic Action/Func (unless specific context, but simpler to hide all for now)
            if (typeSymbol.Name == "Action" || typeSymbol.Name == "Func")
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
                "Attribute", "MarshalByRefObject", "ContextBoundObject",
                // Additional clutter reducers
                "Uri", "UriBuilder", "Version", "Random", "Timer", 
                "Console", "Tuple", "ValueTuple", "ParamArrayAttribute",
                "ObsoleteAttribute", "Thread", "ThreadStart", "Monitor",
                
                // Aggressive filtering
                "Guid", "Type", "Array", "Exception", "DateTime", "TimeSpan",
                "MathF", "Math", // We might want Math, but often users use our vector math? 
                                 // Wait, Math is useful. Let's keep Math. 
                                 // "Math", 
                "BitOperations", "Interlocked"
            };
            // Keeping Random available as it's common.
            // Keeping Math.
            
            if (hiddenTypes.Contains(typeSymbol.Name) && typeSymbol.Name != "Random" && typeSymbol.Name != "Math")
            {
                return true;
            }

            // Hide very short type names (1-2 chars) that are typically internal/abbreviated
            // Exception: common short types like C#'s T type parameter is already handled above
            if (typeSymbol.Name.Length <= 2 && !IsCommonShortType(typeSymbol.Name))
            {
                return true;
            }

            // Hide types with all-uppercase names (likely internal interop types like ABI, MS, etc.)
            if (typeSymbol.Name.Length >= 2 && typeSymbol.Name.All(c => char.IsUpper(c) || char.IsDigit(c)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns true for commonly used short type names that shouldn't be filtered.
    /// </summary>
    private bool IsCommonShortType(string name)
    {
        // Common short types that users might actually need
        var commonShortTypes = new HashSet<string> { "IO", "ID", "PI" };
        return commonShortTypes.Contains(name);
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
             var textInSpan = code.Substring(spanBefore.Start, Math.Min(spanBefore.Length, code.Length - spanBefore.Start));
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
    public async Task<(string Kind, string TypeName, string Name, string? Documentation)?> GetQuickInfoAsync(string code, int position)
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
            string? doc = symbol.GetDocumentationCommentXml();
            
            // If internal XML doc is empty, try to get standard description
            if (string.IsNullOrEmpty(doc))
            {
                // We will rely on UI to render simple description if doc is missing
                // Or we can parse the XML here.
                // Let's return the raw XML or summary.
                // For simplicity, let's just return the DisplayString as fallback documentation if XML is missing?
                // Actually, let's leave documentation null if missing, UI handles it.
                doc = null; 
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
        return "";
    }

    // ---- Context Detection Helpers (Phase 3) ----

    /// <summary>
    /// Checks if the position is inside a generic type argument list (e.g., List&lt;|&gt;).
    /// </summary>
    public static bool IsInGenericTypeArgument(SyntaxNode root, int position)
    {
        var token = root.FindToken(position);
        return token.Parent?.AncestorsAndSelf().OfType<TypeArgumentListSyntax>().Any() == true;
    }

    /// <summary>
    /// Checks if the position is inside an object initializer (e.g., new Type { | }).
    /// </summary>
    public static bool IsInObjectInitializer(SyntaxNode root, int position)
    {
        var token = root.FindToken(position);
        return token.Parent?.AncestorsAndSelf().OfType<InitializerExpressionSyntax>().Any() == true;
    }

    /// <summary>
    /// Gets the type being initialized in an object initializer, returns its settable properties.
    /// </summary>
    public static List<IPropertySymbol>? GetObjectInitializerProperties(SyntaxNode root, int position, SemanticModel model)
    {
        var token = root.FindToken(position);
        var initializer = token.Parent?.AncestorsAndSelf().OfType<InitializerExpressionSyntax>().FirstOrDefault();
        if (initializer == null) return null;

        // The initializer's parent should be an ObjectCreationExpression
        var creation = initializer.Parent as ObjectCreationExpressionSyntax;
        if (creation == null) return null;

        var typeInfo = model.GetTypeInfo(creation);
        var type = typeInfo.Type;
        if (type == null) return null;

        return type.GetMembers()
            .OfType<IPropertySymbol>()
            .Where(p => p.SetMethod != null && p.DeclaredAccessibility == Microsoft.CodeAnalysis.Accessibility.Public)
            .ToList();
    }

    /// <summary>
    /// Checks if the position is inside an attribute context (e.g., [|]).
    /// </summary>
    public static bool IsInAttributeContext(SyntaxNode root, int position)
    {
        var token = root.FindToken(position);
        return token.Parent?.AncestorsAndSelf().OfType<AttributeListSyntax>().Any() == true;
    }

    // ---- Scope Classification (Phase 5) ----

    /// <summary>
    /// Classifies a symbol's scope for priority sorting.
    /// </summary>
    private static SymbolScope ClassifyScope(ISymbol symbol, SyntaxToken cursorToken)
    {
        // Local variables and parameters
        if (symbol.Kind == SymbolKind.Local || symbol.Kind == SymbolKind.Parameter ||
            symbol.Kind == SymbolKind.RangeVariable)
            return SymbolScope.Local;

        // Members of the containing type
        var containingType = cursorToken.Parent?.AncestorsAndSelf()
            .OfType<TypeDeclarationSyntax>()
            .FirstOrDefault();

        if (containingType != null && symbol.ContainingType != null)
        {
            var containingTypeName = containingType.Identifier.Text;
            if (symbol.ContainingType.Name == containingTypeName)
                return SymbolScope.ClassMember;
        }

        // Imported types (from using directives)
        if (symbol is INamedTypeSymbol || symbol is INamespaceSymbol)
            return SymbolScope.Imported;

        return SymbolScope.Global;
    }
}
