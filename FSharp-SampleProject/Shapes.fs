namespace FSharpSample

open System
open Code2Viz.Geometry
open Code2Viz.Canvas

/// Helper module for creating common shapes
module Shapes =
	/// Create a colored circle at the specified position
	let createCircle (x: float) (y: float) (radius: float) (fillColor: string) =
		let circle = VCircle(VPoint(x, y), radius)
		circle.FillColor <- fillColor
		circle.StrokeColor <- Colors.White
		circle.StrokeThickness <- 2.0
		circle

	/// Create a rectangle at the specified position
	let createRect (x: float) (y: float) (width: float) (height: float) (fillColor: string) =
		let rect = VRectangle(VPoint(x, y), width, height)
		rect.FillColor <- fillColor
		rect.StrokeColor <- Colors.White
		rect.StrokeThickness <- 2.0
		rect

	/// Create a line between two points
	let createLine (x1: float) (y1: float) (x2: float) (y2: float) (color: string) =
		let line = VLine(VPoint(x1, y1), VPoint(x2, y2))
		line.StrokeColor <- color
		line.StrokeThickness <- 2.0
		line

	/// Create labeled text at a position
	let createText (x: float) (y: float) (content: string) (color: string) =
		let text = VText(VPoint(x, y), content)
		text.Height <- 20.0
		text.Color <- color
		text

	/// Draw a row of circles
	let drawCircleRow (startX: float) (y: float) (count: int) (radius: float) (spacing: float) =
		for i in 0 .. count - 1 do
			let x = startX + float i * spacing
			let color = Colors.getColorByIndex i
			let circle = createCircle x y radius color
			circle.Draw()

	/// Draw a grid of rectangles
	let drawRectGrid (startX: float) (startY: float) (cols: int) (rows: int) (size: float) (spacing: float) =
		for row in 0 .. rows - 1 do
			for col in 0 .. cols - 1 do
				let x = startX + float col * spacing
				let y = startY - float row * spacing
				let colorIndex = row * cols + col
				let color = Colors.getColorByIndex colorIndex
				let rect = createRect x y size size color
				rect.Draw()
