namespace Animator;

public static class Templates
{
    // System, C2VGeometry, Animator.Sketching, and Animator.Console are injected as
    // global usings by SketchCompiler — see SketchCompiler.GlobalUsingsSource. The
    // boilerplate therefore needs no using directives.
    //
    // The file-scoped namespace matters: it keeps the sketch types out of the global
    // namespace, so reopening a saved file (or any transient state where the same source
    // is briefly seen twice by the IntelliSense compilation) reports a clearer error
    // scoped to `Sketches` rather than the bare global namespace.
    public const string DefaultSketch = """
        namespace Sketches;

        public class MySketch : Sketch
        {
            public override void Setup()
            {
                Size(800, 600);
                Background("Black");
            }

            public override void Draw()
            {
                // Orbit a cyan circle around the origin so it stays visible.
                var r = 200.0;
                var x = r * Math.Sin(ElapsedSeconds);
                var y = r * Math.Cos(ElapsedSeconds);
                new VCircle(new VXYZ(x, y), 12) { FillColor = "Cyan" };
            }
        }
        """;
}
