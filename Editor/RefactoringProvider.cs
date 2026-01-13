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

        public class RenameResult
        {
            public bool Success { get; set; }
            public string? Error { get; set; }
            public string? OriginalName { get; set; }
            public ISymbol? Symbol { get; set; }
            
            // Map of FilePath -> List of (Offset, Length, NewText)
            public Dictionary<string, List<(int Offset, int Length, string NewText)>>? Changes { get; set; }
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
    }
}
