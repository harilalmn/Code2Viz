using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;
using Code2Viz.Execution;

namespace Code2Viz.Tests;

/// <summary>
/// Regression guard: user code compiled by ModuleCompiler must be able to `using C2VGeometry;`
/// AND `using Code2Viz.Console;` / `Code2Viz.Animation;` / `Code2Viz.Sketching;`. The geometry
/// types live in C2VGeometry.dll; Console/Animation/Sketching live in the Code2Viz host assembly.
/// Both must be in the default reference set or those usings fail (CS0246) in the editor and at run.
/// </summary>
public class CompilerReferenceTests
{
    private static bool ReferencesAssemblyOf(Type t)
    {
        var path = t.Assembly.Location;
        return new ModuleCompiler().GetReferences()
            .OfType<PortableExecutableReference>()
            .Any(r => string.Equals(r.FilePath, path, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DefaultReferences_IncludeC2VGeometryAssembly()
        => Assert.True(ReferencesAssemblyOf(typeof(C2VGeometry.Shape)));

    [Fact]
    public void DefaultReferences_IncludeHostAssembly_ForVizConsole()
        => Assert.True(ReferencesAssemblyOf(typeof(Code2Viz.Console.VizConsole)));
}
