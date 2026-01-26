namespace FSharpSample

open System
open VizDsl
open Code2Viz.Geometry
open Code2Viz.Animation

module AnimationSample =
    let Run() =
        Console.WriteLine("Running F# Animation Sample...")

        // 1. Define Shapes using functional DSL
        let lineShape =
            line 0.0 0.0 100.0 0.0
            |> withStroke "Cyan"

        let circleShape =
            circle 50.0 50.0 30.0
            |> withStroke "Yellow"

        let arcShape =
            arc 150.0 50.0 30.0 0.0 180.0
            |> withStroke "Orange"

        let polylineShape =
            polyline [(200.0, 0.0); (250.0, 80.0); (150.0, 80.0); (200.0, 0.0)]
            |> withStroke "LightGreen"

        let bezierShape =
            VBezier(
                VPoint(300.0, 50.0),
                VPoint(320.0, 100.0),
                VPoint(380.0, 0.0),
                VPoint(400.0, 50.0)
            )
        bezierShape.Color <- "Magenta"

        // Collect all shapes for animation
        let shapesArray =
            [| lineShape :> Shape
               circleShape :> Shape
               arcShape :> Shape
               polylineShape :> Shape
               bezierShape :> Shape |]

        let timeline = Timeline(shapesArray)
        timeline.Duration <- 10.0
        timeline.Repeat <- true

        // 2. Add Animations using functional iteration

        // Draw Animations (0s to 2s) - add to all shapes
        shapesArray
        |> Array.iter (fun shape ->
            timeline.AddAnimation(DrawAnimation(shape, 0.0, 2.0)))

        // Move Animations (2s to 4s)
        timeline.AddAnimation(MoveAnimation(lineShape, VXYZ(0.0, 50.0, 0.0), 2.0, 2.0))
        timeline.AddAnimation(MoveAnimation(circleShape, VXYZ(20.0, 0.0, 0.0), 2.0, 2.0))

        // Rotate Animations (4s to 7s)
        let triCenter = VPoint(200.0, 53.0)
        timeline.AddAnimation(RotateAnimation(polylineShape, triCenter, 360.0, 4.0, 3.0))
        timeline.AddAnimation(RotateAnimation(bezierShape, bezierShape.P0, 45.0, 4.0, 3.0))

        // Flip Animations (7s to 9s)
        let mirrorAxis = VLine(150.0, 0.0, 150.0, 100.0)
        timeline.AddAnimation(FlipAnimation(arcShape, mirrorAxis, 7.0, 2.0))

        // 3. Start the animation
        timeline.Play()
        Console.WriteLine("Animation timeline started.")

        Console.WriteLine("\nF# Animation Sample Completed.")
