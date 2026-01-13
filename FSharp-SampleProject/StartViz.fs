namespace FSharpSample

open System
open Code2Viz.Geometry
open Code2Viz.Canvas

module Viz =
	// Entry point - references Colors and Shapes modules
	let Main() =
		Console.WriteLine("F# Multi-Module Sample Project")
		Console.WriteLine("Testing tab indentation and module references")

		// Test Colors module
		Console.WriteLine($"Red color: {Colors.Red}")
		Console.WriteLine($"Color at index 3: {Colors.getColorByIndex 3}")

		// Draw title
		let title = Shapes.createText 0.0 350.0 "F# Module Test" Colors.White
		title.Height <- 32.0
		title.Draw()

		// Draw a row of circles using Shapes module
		Console.WriteLine("Drawing circle row...")
		Shapes.drawCircleRow -250.0 200.0 6 40.0 100.0

		// Draw a grid of rectangles
		Console.WriteLine("Drawing rectangle grid...")
		Shapes.drawRectGrid -200.0 50.0 5 3 60.0 80.0

		// Draw some individual shapes
		let bigCircle = Shapes.createCircle 0.0 -200.0 80.0 Colors.Purple
		bigCircle.Draw()

		let label = Shapes.createText 0.0 -200.0 "Center" Colors.White
		label.Draw()

		// Draw connecting lines
		let line1 = Shapes.createLine -250.0 200.0 150.0 200.0 Colors.Gray
		line1.StrokeThickness <- 1.0
		line1.Draw()

		Console.WriteLine("Done!")
