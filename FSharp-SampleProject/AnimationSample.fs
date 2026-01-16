namespace FSharpSample

open System
open System.Collections.Generic
open Code2Viz.Geometry
open Code2Viz.Animation

module AnimationSample =
    let Run() =
        Console.WriteLine("Running F# Animation Sample...")

        // 1. Define Shapes
        let line = new VLine(0.0, 0.0, 100.0, 0.0)
        line.StrokeColor <- "Cyan"

        let circle = new VCircle(50.0, 50.0, 30.0)
        circle.StrokeColor <- "Yellow"

        let arc = new VArc(150.0, 50.0, 30.0, 0.0, 180.0)
        arc.StrokeColor <- "Orange"

        let polyline = new VPolyline(
            new VPoint(200.0, 0.0),
            new VPoint(250.0, 80.0),
            new VPoint(150.0, 80.0),
            new VPoint(200.0, 0.0)
        )
        polyline.StrokeColor <- "LightGreen"

        let bezier = new VBezier(
            new VPoint(300.0, 50.0),
            new VPoint(320.0, 100.0),
            new VPoint(380.0, 0.0),
            new VPoint(400.0, 50.0)
        )
        bezier.StrokeColor <- "Magenta"

        let shapesArray = [| line :> Shape; circle :> Shape; arc :> Shape; polyline :> Shape; bezier :> Shape |]

        let timeline = new Timeline(shapesArray)
        timeline.Duration <- 10.0
        timeline.Repeat <- true

        // 2. Add Animations
        
        // Draw Animations (0s to 2s)
        for shape in shapesArray do
            timeline.AddAnimation(new DrawAnimation(shape, 0.0, 2.0))

        // Move Animations (2s to 4s)
        timeline.AddAnimation(new MoveAnimation(line, new VXYZ(0.0, 50.0, 0.0), 2.0, 2.0))
        timeline.AddAnimation(new MoveAnimation(circle, new VXYZ(20.0, 0.0, 0.0), 2.0, 2.0))

        // Rotate Animations (4s to 7s)
        let triCenter = new VPoint(200.0, 53.0)
        timeline.AddAnimation(new RotateAnimation(polyline, triCenter, 360.0, 4.0, 3.0))

        timeline.AddAnimation(new RotateAnimation(bezier, bezier.P0, 45.0, 4.0, 3.0))

        // Flip Animations (7s to 9s)
        let mirrorAxis = new VLine(150.0, 0.0, 150.0, 100.0)
        timeline.AddAnimation(new FlipAnimation(arc, mirrorAxis, 7.0, 2.0))

        // 3. Start the animation
        timeline.Play()
        Console.WriteLine("Animation timeline started.")

        Console.WriteLine("\nF# Animation Sample Completed.")
