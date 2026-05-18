using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Animator.Editor;

/// <summary>
/// Maintains a cached CSharpCompilation that supports incremental updates.
/// Adapted from Code2Viz's editor.
/// </summary>
public class CachedCompilationWorkspace
{
    private readonly object _lock = new();
    private CSharpCompilation _compilation;
    private readonly Dictionary<string, SyntaxTree> _trees = new();

    public CachedCompilationWorkspace(IEnumerable<MetadataReference> references)
    {
        _compilation = CSharpCompilation.Create(
            "AnimatorCompletionAnalysis",
            System.Array.Empty<SyntaxTree>(),
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    public void UpdateFile(string fileId, string content)
    {
        var newTree = CSharpSyntaxTree.ParseText(content, path: fileId);
        lock (_lock)
        {
            if (_trees.TryGetValue(fileId, out var oldTree))
                _compilation = _compilation.ReplaceSyntaxTree(oldTree, newTree);
            else
                _compilation = _compilation.AddSyntaxTrees(newTree);
            _trees[fileId] = newTree;
        }
    }

    public CSharpCompilation GetCompilation()
    {
        lock (_lock) return _compilation;
    }

    public SemanticModel? GetSemanticModel(string fileId)
    {
        lock (_lock)
        {
            if (_trees.TryGetValue(fileId, out var tree))
                return _compilation.GetSemanticModel(tree);
            return null;
        }
    }

    public SyntaxTree? GetSyntaxTree(string fileId)
    {
        lock (_lock) return _trees.GetValueOrDefault(fileId);
    }
}
