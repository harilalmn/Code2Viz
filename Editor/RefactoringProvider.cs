using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Code2Viz.Execution;
using Code2Viz.Project;

namespace Code2Viz.Editor
{
    public class RefactoringProvider
    {
        private readonly ModuleCompiler _compiler;

        public RefactoringProvider(ModuleCompiler compiler)
        {
            _compiler = compiler;
        }

        public class QuickActionItem
        {
            public string Title { get; set; } = "";
            public string ActionId { get; set; } = ""; // e.g., "Rename", "MoveType", "ExtractInterface"
            public Dictionary<string, string> Data { get; set; } = new();
        }

        // Overload that uses current editor content directly to avoid line ending/caching issues
        public async Task<List<QuickActionItem>> GetQuickActionsAsync(VizCodeProject project, string filePath, string currentContent, int offset, int selectionLength)
        {
            var actions = new List<QuickActionItem>();

            try
            {
                // Parse the current content directly to ensure offset matches
                var tree = CSharpSyntaxTree.ParseText(
                    currentContent,
                    path: filePath,
                    options: new CSharpParseOptions(LanguageVersion.Latest));
                
                // Still need compilation for semantic model
                var (compilation, _) = await _compiler.CreateCompilationAsync(project);
                
                // Replace the tree in compilation for semantic analysis
                var oldTree = compilation.SyntaxTrees.FirstOrDefault(t => 
                    string.Equals(t.FilePath, filePath, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(System.IO.Path.GetFileName(t.FilePath), System.IO.Path.GetFileName(filePath), StringComparison.OrdinalIgnoreCase));
                
                if (oldTree != null)
                {
                    compilation = compilation.ReplaceSyntaxTree(oldTree, tree);
                }
                else
                {
                    compilation = compilation.AddSyntaxTrees(tree);
                }

                var root = await tree.GetRootAsync();
                var model = compilation.GetSemanticModel(tree);
                var token = root.FindToken(offset);

                // Adjust token if we are at the end of an identifier
                if (!token.IsKind(SyntaxKind.IdentifierToken) && offset > 0)
                {
                    var prev = root.FindToken(offset - 1);
                    if (prev.IsKind(SyntaxKind.IdentifierToken))
                    {
                        token = prev;
                    }
                }

                var node = token.Parent;

                // 1. Rename (General)
                if (token.IsKind(SyntaxKind.IdentifierToken))
                {
                    actions.Add(new QuickActionItem 
                    { 
                        Title = "Rename...", 
                        ActionId = "Rename",
                        Data = { ["Name"] = token.Text }
                    });
                }

                // Generate Method Check - look for invocation in ancestors
                InvocationExpressionSyntax? invocation = null;
                
                // Check if we're directly on the method name of an invocation
                if (node != null && node.Parent is InvocationExpressionSyntax inv1 && node == inv1.Expression)
                {
                    invocation = inv1;
                }
                // Also check if the identifier is part of a member access being invoked
                else if (node != null && node.Parent is MemberAccessExpressionSyntax memberAccess && 
                         memberAccess.Parent is InvocationExpressionSyntax inv2)
                {
                    invocation = inv2;
                }
                // Fallback: look up the tree for any invocation
                else if (token.IsKind(SyntaxKind.IdentifierToken))
                {
                    var potentialInvocation = node?.Ancestors().OfType<InvocationExpressionSyntax>().FirstOrDefault();
                    if (potentialInvocation != null)
                    {
                        // Check if this identifier is the method name being called
                        var exprText = potentialInvocation.Expression.ToString();
                        if (exprText == token.Text || exprText.EndsWith("." + token.Text))
                        {
                            invocation = potentialInvocation;
                        }
                    }
                }
                
                if (invocation != null)
                {
                    var symbolInfo = model.GetSymbolInfo(invocation);
                    if (symbolInfo.Symbol == null)
                    {
                        // Method likely doesn't exist
                        var tokenText = token.Text;
                        
                        // Check if we're in a static method context
                        var enclosingMethod = node.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
                        bool isStatic = enclosingMethod?.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword)) ?? false;
                        
                        // Infer parameter types from arguments
                        var parameters = new List<string>();
                        var argIndex = 0;
                        foreach (var arg in invocation.ArgumentList.Arguments)
                        {
                            var argType = model.GetTypeInfo(arg.Expression);
                            var typeName = argType.Type?.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat) ?? "object";
                            var paramName = $"arg{argIndex}";
                            
                            // Try to use the argument's name if it's an identifier
                            if (arg.Expression is IdentifierNameSyntax argId)
                            {
                                paramName = argId.Identifier.Text;
                            }
                            
                            parameters.Add($"{typeName} {paramName}");
                            argIndex++;
                        }
                        
                        var parametersStr = string.Join(", ", parameters);
                        
                        actions.Add(new QuickActionItem 
                        { 
                            Title = $"Generate method '{tokenText}'", 
                            ActionId = "GenerateMethod",
                            Data = { 
                                ["MethodName"] = tokenText, 
                                ["InvocationSpan"] = invocation.Span.ToString(),
                                ["IsStatic"] = isStatic.ToString(),
                                ["Parameters"] = parametersStr
                            }
                        });
                    }
                }
                // Fallback: Check if we are on a standalone identifier in a statement position that isn't resolved
                else if (token.IsKind(SyntaxKind.IdentifierToken) && node is IdentifierNameSyntax idName && node.Parent is ExpressionStatementSyntax)
                {
                     // Standalone unknown identifier call like "Method();"
                     var symbolInfo = model.GetSymbolInfo(idName);
                     if (symbolInfo.Symbol == null)
                     {
                        // Check if we're in a static method context
                        var enclosingMethod = node.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
                        bool isStatic = enclosingMethod?.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword)) ?? false;
                        
                        actions.Add(new QuickActionItem 
                        { 
                            Title = $"Generate method '{token.Text}'", 
                            ActionId = "GenerateMethod",
                            Data = { 
                                ["MethodName"] = token.Text, 
                                ["InvocationSpan"] = idName.Span.ToString(),
                                ["IsStatic"] = isStatic.ToString(),
                                ["Parameters"] = ""
                            }
                        });
                     }
                }


                // 2. Class Context
                var classDecl = node?.AncestorsAndSelf().OfType<ClassDeclarationSyntax>().FirstOrDefault();
                if (classDecl != null)
                {
                    // If cursor is on the class identifier
                    if (token.Parent == classDecl && token.IsKind(SyntaxKind.IdentifierToken))
                    {
                        actions.Add(new QuickActionItem 
                        { 
                            Title = "Move type to file matching name", 
                            ActionId = "MoveTypeToFile",
                            Data = { ["TypeName"] = classDecl.Identifier.Text }
                        });

                        actions.Add(new QuickActionItem 
                        { 
                            Title = "Extract Interface...", 
                            ActionId = "ExtractInterface",
                            Data = { ["TypeName"] = classDecl.Identifier.Text }
                        });
                        
                        actions.Add(new QuickActionItem 
                        { 
                            Title = "Sync File Name", 
                            ActionId = "SyncFileName",
                            Data = { ["TypeName"] = classDecl.Identifier.Text }
                        });
                    }

                    // Constructor generation (if inside class body)
                    actions.Add(new QuickActionItem 
                    { 
                        Title = "Generate Constructor...", 
                        ActionId = "GenerateConstructor",
                        Data = { ["TypeName"] = classDecl.Identifier.Text }
                    });
                }

                // 3. Method Context
                var methodDecl = node?.AncestorsAndSelf().OfType<MethodDeclarationSyntax>().FirstOrDefault();
                if (methodDecl != null)
                {
                    // Cursor on method name
                    if (token.Parent == methodDecl && token.IsKind(SyntaxKind.IdentifierToken))
                    {
                         actions.Add(new QuickActionItem 
                        { 
                            Title = "Change Signature...", 
                            ActionId = "ChangeSignature",
                            Data = { ["MethodName"] = methodDecl.Identifier.Text }
                        });

                        if (!methodDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword)))
                        {
                            actions.Add(new QuickActionItem 
                            { 
                                Title = "Make Method Static", 
                                ActionId = "MakeStatic",
                                Data = { ["MethodName"] = methodDecl.Identifier.Text }
                            });
                        }
                    }

                    actions.Add(new QuickActionItem 
                    { 
                        Title = "Add Parameter...", 
                        ActionId = "AddParameter",
                        Data = { ["MethodName"] = methodDecl.Identifier.Text }
                    });
                }

                // 4. Variable/Field Context
                var fieldDecl = node?.AncestorsAndSelf().OfType<FieldDeclarationSyntax>().FirstOrDefault();
                if (fieldDecl != null)
                {
                    // If private field, offer encapsulation
                    if (fieldDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.PrivateKeyword) || m.IsKind(SyntaxKind.InternalKeyword)))
                    {
                        var variable = fieldDecl.Declaration.Variables.FirstOrDefault();
                        if (variable != null)
                        {
                             actions.Add(new QuickActionItem 
                            { 
                                Title = "Encapsulate Field", 
                                ActionId = "EncapsulateField",
                                Data = { ["FieldName"] = variable.Identifier.Text }
                            });
                        }
                    }
                }

                var localDecl = node?.AncestorsAndSelf().OfType<LocalDeclarationStatementSyntax>().FirstOrDefault();
                if (localDecl != null && localDecl.Declaration.Type.IsVar)
                {
                     actions.Add(new QuickActionItem 
                    { 
                        Title = "Use Explicit Type", 
                        ActionId = "UseExplicitType",
                    });
                }
                else if (localDecl != null && !localDecl.Declaration.Type.IsVar)
                {
                    actions.Add(new QuickActionItem 
                    { 
                        Title = "Use 'var'", 
                        ActionId = "UseImplicitType",
                    });
                }

                // 5. Selection based (Extract Method)
                if (selectionLength > 0)
                {
                    actions.Add(new QuickActionItem 
                    { 
                        Title = "Extract Method...", 
                        ActionId = "ExtractMethod",
                        Data = { ["SelectionLength"] = selectionLength.ToString() }
                    });
                }

                // 6. General
                actions.Add(new QuickActionItem { Title = "Fix Formatting", ActionId = "FixFormatting" });
                actions.Add(new QuickActionItem { Title = "Remove Unused Usings", ActionId = "RemoveUnusedUsings" });

                return actions;
            }
            catch (Exception ex)
            {
                // Fallback
                System.Diagnostics.Debug.WriteLine($"GetQuickActionsAsync failed: {ex.Message}");
                return actions;
            }
        }

        public class RenameResult
        {
            public bool Success { get; set; }
            public string? Error { get; set; }
            public string? OriginalName { get; set; }
            public ISymbol? Symbol { get; set; }

            // Map of FilePath -> List of (Offset, Length, NewText)
            public Dictionary<string, List<(int Offset, int Length, string NewText)>>? Changes { get; set; }
        }

        public class DefinitionResult
        {
            public bool Success { get; set; }
            public string? Error { get; set; }
            public string? SymbolName { get; set; }
            public string? SymbolKind { get; set; }
            public string? FilePath { get; set; }
            public int Line { get; set; }
            public int Column { get; set; }
            public int Offset { get; set; }
            public int Length { get; set; }
        }

        public class ReferenceResult
        {
            public bool Success { get; set; }
            public string? Error { get; set; }
            public string? SymbolName { get; set; }
            public string? SymbolKind { get; set; }
            public List<ReferenceLocation> References { get; set; } = new();
        }

        public class ReferenceLocation
        {
            public string FilePath { get; set; } = "";
            public int Line { get; set; }
            public int Column { get; set; }
            public int Offset { get; set; }
            public int Length { get; set; }
            public string LineText { get; set; } = "";
            public bool IsDefinition { get; set; }
        }

        public class DocumentSymbol
        {
            public string Name { get; set; } = "";
            public string Kind { get; set; } = "";
            public string Detail { get; set; } = "";
            public string FilePath { get; set; } = "";
            public int Line { get; set; }
            public int Column { get; set; }
            public int Offset { get; set; }
            public int EndOffset { get; set; }
            public List<DocumentSymbol> Children { get; set; } = new();
        }

        public class DocumentSymbolsResult
        {
            public bool Success { get; set; }
            public string? Error { get; set; }
            public List<DocumentSymbol> Symbols { get; set; } = new();
        }

        public async Task<RenameResult> GetRenameEditsAsync(VizCodeProject project, string filePath, int offset, string newName)
        {
            var result = new RenameResult();

            try
            {
                var (compilation, _) = await _compiler.CreateCompilationAsync(project);
                
                // Find the document/tree for the requested file
                var tree = compilation.SyntaxTrees.FirstOrDefault(t => 
                    string.Equals(t.FilePath, filePath, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(System.IO.Path.GetFileName(t.FilePath), System.IO.Path.GetFileName(filePath), StringComparison.OrdinalIgnoreCase));
                
                if (tree == null)
                {
                    result.Error = "File not found in compilation.";
                    return result;
                }

                var model = compilation.GetSemanticModel(tree);
                var root = await tree.GetRootAsync();
                var token = root.FindToken(offset);

                // Ensure we are on an identifier
                if (!token.IsKind(SyntaxKind.IdentifierToken))
                {
                    // Try adjusting offset slightly if at end of word
                    var prev = root.FindToken(offset - 1);
                    if (prev.IsKind(SyntaxKind.IdentifierToken))
                    {
                        token = prev;
                    }
                    else
                    {
                        result.Error = "Cursor is not on an identifier.";
                        return result;
                    }
                }

                // Get the symbol
                var node = token.Parent;
                ISymbol? symbol = null;
                
                if (node != null)
                {
                    symbol = model.GetSymbolInfo(node).Symbol ?? model.GetDeclaredSymbol(node);
                }

                if (symbol == null)
                {
                    result.Error = "Could not resolve symbol.";
                    return result;
                }

                result.Symbol = symbol;
                result.OriginalName = symbol.Name;
                result.Changes = new Dictionary<string, List<(int, int, string)>>();

                // Scan all files for references
                foreach (var t in compilation.SyntaxTrees)
                {
                    var fileModel = compilation.GetSemanticModel(t);
                    var fileRoot = await t.GetRootAsync();
                    var fileChanges = new List<(int, int, string)>();

                    // Basic optimization: if file doesn't contain the name, skip it
                    if (!t.ToString().Contains(symbol.Name)) continue;

                    foreach (var fileToken in fileRoot.DescendantTokens().Where(k => k.IsKind(SyntaxKind.IdentifierToken)))
                    {
                        if (fileToken.ValueText == symbol.Name)
                        {
                            var tokenNode = fileToken.Parent;
                            if (tokenNode != null)
                            {
                                var tokenSymbol = fileModel.GetSymbolInfo(tokenNode).Symbol 
                                                ?? fileModel.GetDeclaredSymbol(tokenNode);
                                
                                if (SymbolEqualityComparer.Default.Equals(symbol, tokenSymbol))
                                {
                                    fileChanges.Add((fileToken.SpanStart, fileToken.Span.Length, newName));
                                }
                            }
                        }
                    }

                    if (fileChanges.Any())
                    {
                        // Sort changes by offset descending to apply them safely (though caller handles applied logic)
                        fileChanges.Sort((a, b) => b.Item1.CompareTo(a.Item1));
                        
                        var path = t.FilePath;
                        if (string.IsNullOrEmpty(path)) path = filePath; // Fallback for single script

                        result.Changes[path] = fileChanges;
                    }
                }

                result.Success = true;
                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = $"Rename failed: {ex.Message}";
                return result;
            }
        }

        /// <summary>
        /// Gets the definition location of the symbol at the specified offset.
        /// </summary>
        public async Task<DefinitionResult> GetDefinitionAsync(VizCodeProject project, string filePath, int offset)
        {
            var result = new DefinitionResult();

            try
            {
                var (compilation, _) = await _compiler.CreateCompilationAsync(project);

                // Find the document/tree for the requested file
                var tree = compilation.SyntaxTrees.FirstOrDefault(t =>
                    string.Equals(t.FilePath, filePath, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(System.IO.Path.GetFileName(t.FilePath), System.IO.Path.GetFileName(filePath), StringComparison.OrdinalIgnoreCase));

                if (tree == null)
                {
                    result.Error = "File not found in compilation.";
                    return result;
                }

                var model = compilation.GetSemanticModel(tree);
                var root = await tree.GetRootAsync();
                var token = root.FindToken(offset);

                // Ensure we are on an identifier
                if (!token.IsKind(SyntaxKind.IdentifierToken))
                {
                    var prev = root.FindToken(offset - 1);
                    if (prev.IsKind(SyntaxKind.IdentifierToken))
                    {
                        token = prev;
                    }
                    else
                    {
                        result.Error = "Cursor is not on an identifier.";
                        return result;
                    }
                }

                // Get the symbol
                var node = token.Parent;
                ISymbol? symbol = null;

                if (node != null)
                {
                    symbol = model.GetSymbolInfo(node).Symbol ?? model.GetDeclaredSymbol(node);
                }

                if (symbol == null)
                {
                    result.Error = "Could not resolve symbol.";
                    return result;
                }

                result.SymbolName = symbol.Name;
                result.SymbolKind = symbol.Kind.ToString();

                // Get the definition location
                var locations = symbol.Locations;
                var sourceLocation = locations.FirstOrDefault(l => l.IsInSource);

                if (sourceLocation == null)
                {
                    // Symbol is from metadata (e.g., System types)
                    result.Error = $"'{symbol.Name}' is defined in external metadata ({symbol.ContainingAssembly?.Name}).";
                    return result;
                }

                var defTree = sourceLocation.SourceTree;
                if (defTree == null)
                {
                    result.Error = "Could not find source tree for definition.";
                    return result;
                }

                var lineSpan = sourceLocation.GetLineSpan();
                result.FilePath = defTree.FilePath;
                result.Line = lineSpan.StartLinePosition.Line + 1;
                result.Column = lineSpan.StartLinePosition.Character + 1;
                result.Offset = sourceLocation.SourceSpan.Start;
                result.Length = sourceLocation.SourceSpan.Length;
                result.Success = true;

                return result;
            }
            catch (Exception ex)
            {
                result.Error = $"Go to definition failed: {ex.Message}";
                return result;
            }
        }

        /// <summary>
        /// Finds all references to the symbol at the specified offset.
        /// </summary>
        public async Task<ReferenceResult> FindAllReferencesAsync(VizCodeProject project, string filePath, int offset)
        {
            var result = new ReferenceResult();

            try
            {
                var (compilation, _) = await _compiler.CreateCompilationAsync(project);

                // Find the document/tree for the requested file
                var tree = compilation.SyntaxTrees.FirstOrDefault(t =>
                    string.Equals(t.FilePath, filePath, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(System.IO.Path.GetFileName(t.FilePath), System.IO.Path.GetFileName(filePath), StringComparison.OrdinalIgnoreCase));

                if (tree == null)
                {
                    result.Error = "File not found in compilation.";
                    return result;
                }

                var model = compilation.GetSemanticModel(tree);
                var root = await tree.GetRootAsync();
                var token = root.FindToken(offset);

                // Ensure we are on an identifier
                if (!token.IsKind(SyntaxKind.IdentifierToken))
                {
                    var prev = root.FindToken(offset - 1);
                    if (prev.IsKind(SyntaxKind.IdentifierToken))
                    {
                        token = prev;
                    }
                    else
                    {
                        result.Error = "Cursor is not on an identifier.";
                        return result;
                    }
                }

                // Get the symbol
                var node = token.Parent;
                ISymbol? symbol = null;

                if (node != null)
                {
                    symbol = model.GetSymbolInfo(node).Symbol ?? model.GetDeclaredSymbol(node);
                }

                if (symbol == null)
                {
                    result.Error = "Could not resolve symbol.";
                    return result;
                }

                result.SymbolName = symbol.Name;
                result.SymbolKind = symbol.Kind.ToString();

                // Get definition location first
                var defLocation = symbol.Locations.FirstOrDefault(l => l.IsInSource);

                // Scan all files for references
                foreach (var t in compilation.SyntaxTrees)
                {
                    var fileModel = compilation.GetSemanticModel(t);
                    var fileRoot = await t.GetRootAsync();
                    var fileText = t.GetText();

                    // Basic optimization: if file doesn't contain the name, skip it
                    if (!t.ToString().Contains(symbol.Name)) continue;

                    foreach (var fileToken in fileRoot.DescendantTokens().Where(k => k.IsKind(SyntaxKind.IdentifierToken)))
                    {
                        if (fileToken.ValueText == symbol.Name)
                        {
                            var tokenNode = fileToken.Parent;
                            if (tokenNode != null)
                            {
                                var tokenSymbol = fileModel.GetSymbolInfo(tokenNode).Symbol
                                                ?? fileModel.GetDeclaredSymbol(tokenNode);

                                if (SymbolEqualityComparer.Default.Equals(symbol, tokenSymbol))
                                {
                                    var lineSpan = fileToken.GetLocation().GetLineSpan();
                                    var line = lineSpan.StartLinePosition.Line;
                                    var lineText = fileText.Lines[line].ToString();

                                    var isDefinition = defLocation != null &&
                                                       defLocation.SourceTree == t &&
                                                       defLocation.SourceSpan.Start == fileToken.SpanStart;

                                    result.References.Add(new ReferenceLocation
                                    {
                                        FilePath = t.FilePath ?? filePath,
                                        Line = line + 1,
                                        Column = lineSpan.StartLinePosition.Character + 1,
                                        Offset = fileToken.SpanStart,
                                        Length = fileToken.Span.Length,
                                        LineText = lineText.Trim(),
                                        IsDefinition = isDefinition
                                    });
                                }
                            }
                        }
                    }
                }

                // Sort: definition first, then by file and line
                result.References = result.References
                    .OrderByDescending(r => r.IsDefinition)
                    .ThenBy(r => r.FilePath)
                    .ThenBy(r => r.Line)
                    .ToList();

                result.Success = true;
                return result;
            }
            catch (Exception ex)
            {
                result.Error = $"Find references failed: {ex.Message}";
                return result;
            }
        }

        /// <summary>
        /// Gets all symbols in a single document (for Ctrl+Shift+O).
        /// </summary>
        public async Task<DocumentSymbolsResult> GetDocumentSymbolsAsync(VizCodeProject project, string filePath)
        {
            var result = new DocumentSymbolsResult();

            try
            {
                var (compilation, _) = await _compiler.CreateCompilationAsync(project);

                var tree = compilation.SyntaxTrees.FirstOrDefault(t =>
                    string.Equals(t.FilePath, filePath, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(System.IO.Path.GetFileName(t.FilePath), System.IO.Path.GetFileName(filePath), StringComparison.OrdinalIgnoreCase));

                if (tree == null)
                {
                    result.Error = "File not found in compilation.";
                    return result;
                }

                var root = await tree.GetRootAsync();
                var model = compilation.GetSemanticModel(tree);

                // Extract symbols from syntax tree
                foreach (var node in root.DescendantNodes())
                {
                    DocumentSymbol? symbol = null;

                    if (node is ClassDeclarationSyntax classDecl)
                    {
                        symbol = new DocumentSymbol
                        {
                            Name = classDecl.Identifier.Text,
                            Kind = "Class",
                            Detail = GetModifiers(classDecl.Modifiers),
                            FilePath = filePath,
                            Line = classDecl.Identifier.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                            Column = classDecl.Identifier.GetLocation().GetLineSpan().StartLinePosition.Character + 1,
                            Offset = classDecl.Identifier.SpanStart
                        };

                        // Add class members as children
                        foreach (var member in classDecl.Members)
                        {
                            var childSymbol = GetMemberSymbol(member, filePath);
                            if (childSymbol != null)
                            {
                                symbol.Children.Add(childSymbol);
                            }
                        }
                    }
                    else if (node is InterfaceDeclarationSyntax interfaceDecl)
                    {
                        symbol = new DocumentSymbol
                        {
                            Name = interfaceDecl.Identifier.Text,
                            Kind = "Interface",
                            Detail = GetModifiers(interfaceDecl.Modifiers),
                            FilePath = filePath,
                            Line = interfaceDecl.Identifier.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                            Column = interfaceDecl.Identifier.GetLocation().GetLineSpan().StartLinePosition.Character + 1,
                            Offset = interfaceDecl.Identifier.SpanStart
                        };
                    }
                    else if (node is EnumDeclarationSyntax enumDecl)
                    {
                        symbol = new DocumentSymbol
                        {
                            Name = enumDecl.Identifier.Text,
                            Kind = "Enum",
                            Detail = GetModifiers(enumDecl.Modifiers),
                            FilePath = filePath,
                            Line = enumDecl.Identifier.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                            Column = enumDecl.Identifier.GetLocation().GetLineSpan().StartLinePosition.Character + 1,
                            Offset = enumDecl.Identifier.SpanStart
                        };
                    }
                    else if (node is StructDeclarationSyntax structDecl)
                    {
                        symbol = new DocumentSymbol
                        {
                            Name = structDecl.Identifier.Text,
                            Kind = "Struct",
                            Detail = GetModifiers(structDecl.Modifiers),
                            FilePath = filePath,
                            Line = structDecl.Identifier.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                            Column = structDecl.Identifier.GetLocation().GetLineSpan().StartLinePosition.Character + 1,
                            Offset = structDecl.Identifier.SpanStart
                        };
                    }
                    else if (node is RecordDeclarationSyntax recordDecl)
                    {
                        symbol = new DocumentSymbol
                        {
                            Name = recordDecl.Identifier.Text,
                            Kind = "Record",
                            Detail = GetModifiers(recordDecl.Modifiers),
                            FilePath = filePath,
                            Line = recordDecl.Identifier.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                            Column = recordDecl.Identifier.GetLocation().GetLineSpan().StartLinePosition.Character + 1,
                            Offset = recordDecl.Identifier.SpanStart
                        };
                    }

                    // Only add top-level types (not nested)
                    if (symbol != null && node.Parent is BaseNamespaceDeclarationSyntax or CompilationUnitSyntax)
                    {
                        result.Symbols.Add(symbol);
                    }
                }

                // Sort by line number
                result.Symbols = result.Symbols.OrderBy(s => s.Line).ToList();
                result.Success = true;
                return result;
            }
            catch (Exception ex)
            {
                result.Error = $"Get document symbols failed: {ex.Message}";
                return result;
            }
        }

        /// <summary>
        /// Gets all symbols across all files in the project (for Ctrl+T).
        /// </summary>
        public async Task<DocumentSymbolsResult> GetWorkspaceSymbolsAsync(VizCodeProject project, string? filter = null)
        {
            var result = new DocumentSymbolsResult();

            try
            {
                var (compilation, _) = await _compiler.CreateCompilationAsync(project);

                foreach (var tree in compilation.SyntaxTrees)
                {
                    var root = await tree.GetRootAsync();
                    var filePath = tree.FilePath;

                    foreach (var node in root.DescendantNodes())
                    {
                        DocumentSymbol? symbol = null;

                        if (node is ClassDeclarationSyntax classDecl)
                        {
                            symbol = new DocumentSymbol
                            {
                                Name = classDecl.Identifier.Text,
                                Kind = "Class",
                                Detail = System.IO.Path.GetFileName(filePath),
                                FilePath = filePath,
                                Line = classDecl.Identifier.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                                Column = classDecl.Identifier.GetLocation().GetLineSpan().StartLinePosition.Character + 1,
                                Offset = classDecl.Identifier.SpanStart
                            };
                        }
                        else if (node is MethodDeclarationSyntax methodDecl)
                        {
                            symbol = new DocumentSymbol
                            {
                                Name = methodDecl.Identifier.Text,
                                Kind = "Method",
                                Detail = System.IO.Path.GetFileName(filePath),
                                FilePath = filePath,
                                Line = methodDecl.Identifier.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                                Column = methodDecl.Identifier.GetLocation().GetLineSpan().StartLinePosition.Character + 1,
                                Offset = methodDecl.Identifier.SpanStart
                            };
                        }
                        else if (node is PropertyDeclarationSyntax propDecl)
                        {
                            symbol = new DocumentSymbol
                            {
                                Name = propDecl.Identifier.Text,
                                Kind = "Property",
                                Detail = System.IO.Path.GetFileName(filePath),
                                FilePath = filePath,
                                Line = propDecl.Identifier.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                                Column = propDecl.Identifier.GetLocation().GetLineSpan().StartLinePosition.Character + 1,
                                Offset = propDecl.Identifier.SpanStart
                            };
                        }
                        else if (node is InterfaceDeclarationSyntax interfaceDecl)
                        {
                            symbol = new DocumentSymbol
                            {
                                Name = interfaceDecl.Identifier.Text,
                                Kind = "Interface",
                                Detail = System.IO.Path.GetFileName(filePath),
                                FilePath = filePath,
                                Line = interfaceDecl.Identifier.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                                Column = interfaceDecl.Identifier.GetLocation().GetLineSpan().StartLinePosition.Character + 1,
                                Offset = interfaceDecl.Identifier.SpanStart
                            };
                        }
                        else if (node is EnumDeclarationSyntax enumDecl)
                        {
                            symbol = new DocumentSymbol
                            {
                                Name = enumDecl.Identifier.Text,
                                Kind = "Enum",
                                Detail = System.IO.Path.GetFileName(filePath),
                                FilePath = filePath,
                                Line = enumDecl.Identifier.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                                Column = enumDecl.Identifier.GetLocation().GetLineSpan().StartLinePosition.Character + 1,
                                Offset = enumDecl.Identifier.SpanStart
                            };
                        }

                        if (symbol != null)
                        {
                            // Apply filter if provided
                            if (string.IsNullOrEmpty(filter) ||
                                symbol.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
                            {
                                result.Symbols.Add(symbol);
                            }
                        }
                    }
                }

                // Sort by name
                result.Symbols = result.Symbols.OrderBy(s => s.Name).ToList();
                result.Success = true;
                return result;
            }
            catch (Exception ex)
            {
                result.Error = $"Get workspace symbols failed: {ex.Message}";
                return result;
            }
        }

        private static DocumentSymbol? GetMemberSymbol(MemberDeclarationSyntax member, string filePath)
        {
            if (member is MethodDeclarationSyntax methodDecl)
            {
                var paramList = string.Join(", ", methodDecl.ParameterList.Parameters.Select(p => $"{p.Type} {p.Identifier}"));
                return new DocumentSymbol
                {
                    Name = methodDecl.Identifier.Text,
                    Kind = "Method",
                    Detail = $"({paramList}) : {methodDecl.ReturnType}",
                    FilePath = filePath,
                    Line = methodDecl.Identifier.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                    Column = methodDecl.Identifier.GetLocation().GetLineSpan().StartLinePosition.Character + 1,
                    Offset = methodDecl.Identifier.SpanStart
                };
            }
            else if (member is PropertyDeclarationSyntax propDecl)
            {
                return new DocumentSymbol
                {
                    Name = propDecl.Identifier.Text,
                    Kind = "Property",
                    Detail = propDecl.Type.ToString(),
                    FilePath = filePath,
                    Line = propDecl.Identifier.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                    Column = propDecl.Identifier.GetLocation().GetLineSpan().StartLinePosition.Character + 1,
                    Offset = propDecl.Identifier.SpanStart
                };
            }
            else if (member is FieldDeclarationSyntax fieldDecl)
            {
                var firstVar = fieldDecl.Declaration.Variables.FirstOrDefault();
                if (firstVar != null)
                {
                    return new DocumentSymbol
                    {
                        Name = firstVar.Identifier.Text,
                        Kind = "Field",
                        Detail = fieldDecl.Declaration.Type.ToString(),
                        FilePath = filePath,
                        Line = firstVar.Identifier.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                        Column = firstVar.Identifier.GetLocation().GetLineSpan().StartLinePosition.Character + 1,
                        Offset = firstVar.Identifier.SpanStart
                    };
                }
            }
            else if (member is ConstructorDeclarationSyntax ctorDecl)
            {
                var paramList = string.Join(", ", ctorDecl.ParameterList.Parameters.Select(p => $"{p.Type} {p.Identifier}"));
                return new DocumentSymbol
                {
                    Name = ctorDecl.Identifier.Text,
                    Kind = "Constructor",
                    Detail = $"({paramList})",
                    FilePath = filePath,
                    Line = ctorDecl.Identifier.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                    Column = ctorDecl.Identifier.GetLocation().GetLineSpan().StartLinePosition.Character + 1,
                    Offset = ctorDecl.Identifier.SpanStart
                };
            }
            else if (member is EventDeclarationSyntax eventDecl)
            {
                return new DocumentSymbol
                {
                    Name = eventDecl.Identifier.Text,
                    Kind = "Event",
                    Detail = eventDecl.Type.ToString(),
                    FilePath = filePath,
                    Line = eventDecl.Identifier.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                    Column = eventDecl.Identifier.GetLocation().GetLineSpan().StartLinePosition.Character + 1,
                    Offset = eventDecl.Identifier.SpanStart
                };
            }

            return null;
        }

        private static string GetModifiers(SyntaxTokenList modifiers)
        {
            return string.Join(" ", modifiers.Select(m => m.Text));
        }
    }
}
