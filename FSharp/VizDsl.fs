/// Functional DSL for Code2Viz - Provides pipeline-based shape creation
module VizDsl

open System
open Code2Viz.Geometry
open Code2Viz.Canvas

// ============================================================================
// Shape Constructors
// ============================================================================

/// Create a point at the given coordinates
let point (x: float) (y: float) = VPoint(x, y)

/// Create a circle at (x, y) with the given radius
let circle (x: float) (y: float) (radius: float) = VCircle(x, y, radius)

/// Create a circle from a center point and radius
let circleAt (center: VPoint) (radius: float) = VCircle(center, radius)

/// Create a line from (x1, y1) to (x2, y2)
let line (x1: float) (y1: float) (x2: float) (y2: float) = VLine(x1, y1, x2, y2)

/// Create a line from two points
let lineFromPoints (p1: VPoint) (p2: VPoint) = VLine(p1, p2)

/// Create a rectangle at (x, y) with given width and height
let rectangle (x: float) (y: float) (width: float) (height: float) = VRectangle(x, y, width, height)

/// Create a rectangle from a corner point with given dimensions
let rectangleAt (corner: VPoint) (width: float) (height: float) = VRectangle(corner, width, height)

/// Create an ellipse at (x, y) with given radii
let ellipse (x: float) (y: float) (radiusX: float) (radiusY: float) = VEllipse(x, y, radiusX, radiusY)

/// Create an ellipse from a center point with given radii
let ellipseAt (center: VPoint) (radiusX: float) (radiusY: float) = VEllipse(center, radiusX, radiusY)

/// Create an arc at (cx, cy) with given radius and angle range (in degrees)
let arc (cx: float) (cy: float) (radius: float) (startAngle: float) (endAngle: float) = VArc(cx, cy, radius, startAngle, endAngle)

/// Create an arc from a center point with given radius and angle range
let arcAt (center: VPoint) (radius: float) (startAngle: float) (endAngle: float) = VArc(center, radius, startAngle, endAngle)

/// Create a polygon from a list of (x, y) tuples
let polygon (points: (float * float) list) =
    VPolygon(points |> List.map (fun (x, y) -> VPoint(x, y)))

/// Create a polygon from a list of VPoints
let polygonFromPoints (points: VPoint list) = VPolygon(points)

/// Create a polyline from a list of (x, y) tuples
let polyline (points: (float * float) list) =
    VPolyline(points |> List.map (fun (x, y) -> VPoint(x, y)))

/// Create a polyline from a list of VPoints
let polylineFromPoints (points: VPoint list) = VPolyline(points)

/// Create text at (x, y) with the given content
let text (x: float) (y: float) (content: string) = VText(x, y, content)

/// Create text at a point with the given content
let textAt (location: VPoint) content = VText(location, content)

/// Create an arrow from (x1, y1) to (x2, y2)
let arrow (x1: float) (y1: float) (x2: float) (y2: float) = VArrow(VPoint(x1, y1), VPoint(x2, y2))

/// Create an arrow from two points
let arrowFromPoints (p1: VPoint) (p2: VPoint) = VArrow(p1, p2)

// ============================================================================
// Style Modifiers (return shape for chaining)
// ============================================================================

/// Set the fill color of a shape
let withFill (color: string) (shape: #Shape) =
    shape.FillColor <- color
    shape

/// Set the stroke color of a shape
let withStroke (color: string) (shape: #Shape) =
    shape.Color <- color
    shape

/// Set stroke color and thickness together
let withStrokeStyle color thickness (shape: #Shape) =
    shape.Color <- color
    shape.StrokeThickness <- thickness
    shape

/// Set the stroke thickness of a shape
let withThickness (thickness: float) (shape: #Shape) =
    shape.StrokeThickness <- thickness
    shape

/// Set the opacity of a shape (0.0 to 1.0)
let withOpacity (opacity: float) (shape: #Shape) =
    shape.Opacity <- opacity
    shape

/// Set the name of a shape
let withName (name: string) (shape: #Shape) =
    shape.Name <- name
    shape

/// Set the height of a VText shape
let withHeight (height: float) (text: VText) =
    text.Height <- height
    text

/// Set the color of a VText shape (alias for stroke color)
let withTextColor (color: string) (text: VText) =
    text.Color <- color
    text

// ============================================================================
// Drawing Functions
// ============================================================================

/// Draw a shape to the canvas and return it for further chaining
let draw (shape: #Shape) =
    shape.Draw()
    shape

/// Draw all shapes in a list
let drawAll (shapes: #Shape list) =
    shapes |> List.iter (fun s -> s.Draw())
    shapes

/// Draw all shapes in a sequence
let drawAllSeq (shapes: #Shape seq) =
    shapes |> Seq.iter (fun s -> s.Draw())
    shapes

// ============================================================================
// Transformation Functions
// ============================================================================

/// Move a shape by (dx, dy)
let move dx dy (shape: #Shape) =
    shape.Move(VXYZ(dx, dy, 0.0))
    shape

/// Rotate a shape around a pivot point by the given angle (degrees)
let rotate (pivot: VPoint) angleDegrees (shape: #Shape) =
    shape.Rotate(pivot, angleDegrees)
    shape

/// Rotate a shape around its center by the given angle (degrees)
let rotateAroundCenter angleDegrees (shape: #Shape) =
    let bounds = shape.GetBounds()
    let center = VPoint((bounds.Min.X + bounds.Max.X) / 2.0, (bounds.Min.Y + bounds.Max.Y) / 2.0)
    shape.Rotate(center, angleDegrees)
    shape

/// Scale a shape around a center point
let scale (center: VPoint) factor (shape: #Shape) =
    shape.Scale(center, factor)
    shape

/// Flip a shape across a mirror line
let flip (mirrorLine: VLine) (shape: #Shape) =
    shape.Flip(mirrorLine)
    shape

/// Clone a shape
let clone (shape: #Shape) = shape.Clone()

// ============================================================================
// Utility Functions for Generating Multiple Shapes
// ============================================================================

/// Generate a list by applying f to indices 0 to n-1
let times n f = [0..n-1] |> List.map f

/// Generate a grid of values by applying f to (col, row) pairs
let grid cols rows f =
    [for row in 0..rows-1 do
        for col in 0..cols-1 -> f col row]

/// Generate points along a line
let pointsOnLine (p1: VPoint) (p2: VPoint) (count: int) =
    [0..count-1]
    |> List.map (fun i ->
        let t = if count > 1 then float i / float (count - 1) else 0.0
        VPoint(p1.X + (p2.X - p1.X) * t, p1.Y + (p2.Y - p1.Y) * t))

/// Generate points along a circle
let pointsOnCircle (center: VPoint) (radius: float) (count: int) =
    [0..count-1]
    |> List.map (fun i ->
        let angle = 2.0 * Math.PI * float i / float count
        VPoint(center.X + radius * cos angle, center.Y + radius * sin angle))

/// Generate shapes in a row
let row count spacing startX y createShape =
    [0..count-1]
    |> List.map (fun i -> createShape (startX + float i * spacing) y i)

/// Generate shapes in a column
let column count spacing x startY createShape =
    [0..count-1]
    |> List.map (fun i -> createShape x (startY + float i * spacing) i)

// ============================================================================
// Convenience Aliases and Shortcuts
// ============================================================================

/// Shorthand for creating a circle at origin
let circleOrigin (radius: float) = circle 0.0 0.0 radius

/// Shorthand for creating a point at origin
let origin = point 0.0 0.0

/// Create a horizontal line at y from x1 to x2
let hline (y: float) (x1: float) (x2: float) = line x1 y x2 y

/// Create a vertical line at x from y1 to y2
let vline (x: float) (y1: float) (y2: float) = line x y1 x y2

/// Create a square at (x, y) with given size
let square (x: float) (y: float) (size: float) = rectangle x y size size

// ============================================================================
// Color Constants (common colors for convenience)
// ============================================================================

module Color =
    let red = "#E74C3C"
    let blue = "#3498DB"
    let green = "#2ECC71"
    let yellow = "#F1C40F"
    let orange = "#E67E22"
    let purple = "#9B59B6"
    let white = "#FFFFFF"
    let black = "#000000"
    let gray = "#95A5A6"
    let darkGray = "#34495E"
    let cyan = "#1ABC9C"
    let pink = "#E91E63"
    let transparent = "Transparent"

    /// Get a color by index (cycles through a palette)
    let byIndex index =
        let palette = [| red; blue; green; yellow; orange; purple; cyan; pink |]
        palette.[index % palette.Length]
