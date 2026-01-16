using System;
using System.Collections.Generic;
using Code2Viz.Geometry;
using Code2Viz.Animation;
using Code2Viz.Canvas;

namespace CSharpSample
{
    /// <summary>
    /// Demonstrates various animation capabilities.
    /// </summary>
    public class AnimationSample
    {
        public static void Run()
        {
            Console.WriteLine("Running Animation Sample...");

            // 1. Create shapes

            // A line that will be drawn progressively
            var line = new VLine(0, 0, 150, 0);
            line.StrokeColor = "Cyan";
            line.StrokeThickness = 3;

            // A circle that will move
            var circle = new VCircle(50, 50, 25);
            circle.StrokeColor = "Yellow";
            circle.FillColor = "#4000FFFF";

            // An arc that will be drawn
            var arc = new VArc(150, 50, 30, 0, 270);
            arc.StrokeColor = "Orange";
            arc.StrokeThickness = 4;

            // A triangle that will rotate
            var triangle = new VPolygon(
                new VPoint(200, 0),
                new VPoint(260, 0),
                new VPoint(230, 60)
            );
            triangle.StrokeColor = "LimeGreen";
            triangle.FillColor = "#4000FF00";

            // A bezier curve
            var bezier = new VBezier(
                new VPoint(-100, 50),
                new VPoint(-80, 120),
                new VPoint(-20, -20),
                new VPoint(0, 50)
            );
            bezier.StrokeColor = "Magenta";
            bezier.StrokeThickness = 2;

            // 2. Create timeline
            var shapes = new List<Shape> { line, circle, arc, triangle, bezier };
            var timeline = new Timeline(shapes);
            timeline.Duration = 10.0;
            timeline.Repeat = true;

            // 3. Add animations

            // Draw animations - shapes appear progressively
            timeline.AddAnimation(new DrawAnimation(line, 0.0, 2.0));
            timeline.AddAnimation(new DrawAnimation(circle, 0.5, 2.0));
            timeline.AddAnimation(new DrawAnimation(arc, 1.0, 2.0));
            timeline.AddAnimation(new DrawAnimation(triangle, 1.5, 2.0));
            timeline.AddAnimation(new DrawAnimation(bezier, 2.0, 2.0));

            // Move animations - shapes translate
            timeline.AddAnimation(new MoveAnimation(line, new VXYZ(0, 80, 0), 3.0, 2.0));
            timeline.AddAnimation(new MoveAnimation(circle, new VXYZ(100, 0, 0), 3.5, 2.5));

            // Rotate animations - shapes spin
            var triangleCenter = new VPoint(230, 20);
            timeline.AddAnimation(new RotateAnimation(triangle, triangleCenter, 360, 5.0, 3.0));

            // 4. Start playback
            timeline.Play();
            Console.WriteLine("Animation timeline started.");
            Console.WriteLine("Duration: 10 seconds, Repeating: true");

            Console.WriteLine("\nAnimation Sample Completed.");
        }
    }
}
