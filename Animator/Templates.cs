namespace Animator;

public static class Templates
{
    public const string DefaultSketch = """
        using System;
        using C2VGeometry;
        using Animator.Sketching;
        using Animator.Console;

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
