using System;
using System.Collections.Generic;
using Code2Viz.Geometry;
using Code2Viz.Animation;
using Code2Viz.Canvas;

namespace CSharpSample
{
    public class AnimationSample
    {
        public static void Run()
        {
            Console.WriteLine("Running C# Animation Sample...");

            // 1. Define Shapes
            
            // Line
            var line = new VLine(0, 0, 100, 0);
            line.StrokeColor = "Cyan";

            // Circle
            var circle = new VCircle(50, 50, 30);
            circle.StrokeColor = "Yellow";
            circle.FillColor = "Green";

            // Arc
            var arc = new VArc(150, 50, 30, 0, 180);
            arc.StrokeColor = "Orange";

            // Polyline
            var polyline = new VPolyline(
                new VPoint(200, 0),
                new VPoint(250, 80),
                new VPoint(150, 80),
                new VPoint(200, 0)
            );
            polyline.StrokeColor = "LightGreen";

            // Bezier
            var bezier = new VBezier(
                new VPoint(300, 50),
                new VPoint(320, 100),
                new VPoint(380, 0),
                new VPoint(400, 50)
            );
            bezier.StrokeColor = "Magenta";

            var shapes = new List<Shape> { line, circle, arc, polyline, bezier };
            var timeline = new Timeline(shapes);
            timeline.Duration = 10.0;
            timeline.Repeat = true;

            // 2. Define Animations

            // Draw Animations
            foreach (var shape in shapes)
            {
                timeline.AddAnimation(new DrawAnimation(shape, 0.0, 2.0));
            }

            // Move Animations
            timeline.AddAnimation(new MoveAnimation(line, new VXYZ(0, 50, 0), 4.0, 3.0));
            timeline.AddAnimation(new MoveAnimation(circle, new VXYZ(20, 0, 0), 2.0, 2.0));

            // // Rotate Animations
            // var triCenter = new VPoint(200, 53);
            // timeline.AddAnimation(new RotateAnimation(polyline, triCenter, 360, 4.0, 3.0));
            // timeline.AddAnimation(new RotateAnimation(bezier, bezier.P0, 45, 4.0, 3.0));
// 
            // // Flip Animation
            // var mirrorAxis = new VLine(150, 0, 150, 100);
            // timeline.AddAnimation(new FlipAnimation(arc, mirrorAxis, 7.0, 2.0));
// 
            // 3. Start the animation
            timeline.Play();
            Console.WriteLine("Animation timeline started.");

            Console.WriteLine("\nC# Animation Sample Completed.");
        }
    }
}
