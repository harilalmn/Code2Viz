// Code2Viz Web - Help Topics & Documentation Panel
// Searchable help with API reference, examples, and guides

// ============================================================================
// Help Topic Data
// ============================================================================

const HELP_TOPICS = [
    // ── Getting Started ──────────────────────────────────────────────────
    {
        id: 'welcome',
        category: 'Getting Started',
        title: 'Welcome to Code2Viz Web',
        content: `
<h2>Code2Viz Web</h2>
<p>Code2Viz Web is a browser-based geometry visualization app. Write JavaScript code to create and visualize 2D shapes on an interactive canvas.</p>

<h3>Quick Start</h3>
<ol>
<li>Write code in the editor (left panel)</li>
<li>Press <kbd>F5</kbd> or click <b>Run</b> to execute</li>
<li>Shapes appear on the canvas (right panel)</li>
<li>Pan with <kbd>Shift+Click</kbd> or middle mouse, zoom with scroll wheel</li>
</ol>

<h3>Your First Shape</h3>
<pre>let circle = new VCircle(0, 0, 50);
circle.color = "Crimson";
circle.lineWeight = 3;</pre>

<p>Shapes auto-register on the canvas when constructed — no need to call <code>draw()</code>.</p>

<h3>Key Features</h3>
<ul>
<li>15 shape types with full styling (color, fill, line type, opacity)</li>
<li>IntelliSense with parameter hints (type <code>.</code> after a variable)</li>
<li>Multi-file projects with folder support</li>
<li>Export to SVG, PNG, PDF, DXF</li>
<li>Interactive drawing tools, selection, and measuring</li>
<li>Animation system with timeline and easing</li>
<li>Boolean operations and array patterns</li>
</ul>`,
    },
    {
        id: 'keyboard-shortcuts',
        category: 'Getting Started',
        title: 'Keyboard Shortcuts',
        content: `
<h2>Keyboard Shortcuts</h2>

<h3>File / Run</h3>
<table>
<tr><td><kbd>F5</kbd> / <kbd>Ctrl+Enter</kbd></td><td>Run code</td></tr>
<tr><td><kbd>Ctrl+S</kbd></td><td>Save all files</td></tr>
<tr><td><kbd>Ctrl+N</kbd></td><td>New file</td></tr>
<tr><td><kbd>Ctrl+O</kbd></td><td>Open project folder</td></tr>
<tr><td><kbd>Ctrl+W</kbd></td><td>Close tab</td></tr>
<tr><td><kbd>Ctrl+Tab</kbd></td><td>Next tab</td></tr>
</table>

<h3>Editor</h3>
<table>
<tr><td><kbd>Ctrl+F</kbd></td><td>Find</td></tr>
<tr><td><kbd>Ctrl+H</kbd></td><td>Find and Replace</td></tr>
<tr><td><kbd>Ctrl+G</kbd></td><td>Go to Line</td></tr>
<tr><td><kbd>Ctrl+/</kbd></td><td>Toggle comment</td></tr>
<tr><td><kbd>Ctrl+Shift+F</kbd></td><td>Format code</td></tr>
<tr><td><kbd>Ctrl+Space</kbd></td><td>Show IntelliSense</td></tr>
</table>

<h3>Canvas & Tools</h3>
<table>
<tr><td><kbd>V</kbd></td><td>Pointer tool</td></tr>
<tr><td><kbd>P</kbd></td><td>Point tool</td></tr>
<tr><td><kbd>L</kbd></td><td>Line tool</td></tr>
<tr><td><kbd>C</kbd></td><td>Circle tool</td></tr>
<tr><td><kbd>R</kbd></td><td>Rectangle tool</td></tr>
<tr><td><kbd>M</kbd></td><td>Measuring tape</td></tr>
<tr><td><kbd>Escape</kbd></td><td>Back to Pointer</td></tr>
<tr><td><kbd>Delete</kbd></td><td>Delete selected shapes</td></tr>
<tr><td><kbd>Shift</kbd> (hold)</td><td>Orthogonal constraint</td></tr>
</table>

<h3>Panels</h3>
<table>
<tr><td><kbd>F1</kbd></td><td>Help</td></tr>
<tr><td><kbd>F4</kbd></td><td>Properties panel</td></tr>
<tr><td><kbd>F9</kbd></td><td>Toggle snap</td></tr>
<tr><td><kbd>Ctrl+Shift+M</kbd></td><td>Toggle minimap</td></tr>
<tr><td><kbd>Ctrl+M</kbd></td><td>Measuring tape</td></tr>
</table>`,
    },
    {
        id: 'coordinate-system',
        category: 'Getting Started',
        title: 'Coordinate System',
        content: `
<h2>Coordinate System</h2>
<p>Code2Viz uses a <b>mathematical coordinate system</b>:</p>
<ul>
<li>Origin <code>(0, 0)</code> is at the canvas center</li>
<li><b>X</b> increases to the right</li>
<li><b>Y</b> increases upward (not downward like screen coordinates)</li>
</ul>
<p>This matches standard math/engineering conventions. The grid and axes are always visible on the canvas.</p>

<h3>Navigation</h3>
<ul>
<li><b>Pan:</b> Shift+Click and drag, or middle-click and drag</li>
<li><b>Zoom:</b> Mouse scroll wheel (zoom follows cursor)</li>
<li><b>Fit:</b> Click the Fit button to zoom to show all shapes</li>
</ul>

<h3>Coordinate Display</h3>
<p>The current cursor position in world coordinates is shown in the toolbar (top right).</p>`,
    },

    // ── Shapes ───────────────────────────────────────────────────────────
    {
        id: 'vpoint',
        category: 'Shapes',
        title: 'VPoint',
        content: `
<h2>VPoint</h2>
<p>A 2D point that displays as a dot with crosshairs on the canvas.</p>

<h3>Constructors</h3>
<pre>new VPoint(x, y)</pre>

<h3>Static Methods</h3>
<pre>VPoint.internal(x, y)  // Creates unregistered point (for calculations)</pre>

<h3>Properties</h3>
<table>
<tr><td><code>X</code>, <code>Y</code></td><td>Coordinates (read/write)</td></tr>
</table>

<h3>Methods</h3>
<table>
<tr><td><code>distanceTo(other)</code></td><td>Distance to another point</td></tr>
<tr><td><code>add(other)</code></td><td>Returns new point offset by other</td></tr>
<tr><td><code>subtract(other)</code></td><td>Returns difference as new point</td></tr>
<tr><td><code>multiplyScalar(s)</code></td><td>Scale coordinates</td></tr>
<tr><td><code>clone()</code></td><td>Create copy</td></tr>
<tr><td><code>move(vector)</code></td><td>Translate in place</td></tr>
<tr><td><code>rotate(pivot, angleDeg)</code></td><td>Rotate around pivot</td></tr>
</table>

<h3>Example</h3>
<pre>let origin = new VPoint(0, 0);
origin.color = "Yellow";

let p = new VPoint(50, 30);
p.color = "Cyan";

console.log(origin.distanceTo(p)); // 58.31</pre>`,
    },
    {
        id: 'vline',
        category: 'Shapes',
        title: 'VLine',
        content: `
<h2>VLine</h2>
<p>A line segment between two points.</p>

<h3>Constructors</h3>
<pre>new VLine(startPoint, endPoint)
new VLine(x1, y1, x2, y2)</pre>

<h3>Static Methods</h3>
<pre>VLine.fromPointAngleLength(start, angleDeg, length)</pre>

<h3>Properties</h3>
<table>
<tr><td><code>start</code>, <code>end</code></td><td>Endpoint VPoints</td></tr>
<tr><td><code>midPoint</code></td><td>Center point</td></tr>
<tr><td><code>direction</code></td><td>Unit direction vector (VXYZ)</td></tr>
</table>

<h3>Methods</h3>
<table>
<tr><td><code>getLength()</code></td><td>Line length</td></tr>
<tr><td><code>evaluate(t)</code></td><td>Point at parameter t (0..1)</td></tr>
<tr><td><code>divide(n)</code></td><td>Split into n equal segments</td></tr>
<tr><td><code>offset(distance)</code></td><td>Parallel line at distance</td></tr>
<tr><td><code>project(point)</code></td><td>Closest point on line to given point</td></tr>
<tr><td><code>distanceTo(point)</code></td><td>Distance from point to line</td></tr>
</table>

<h3>Example</h3>
<pre>let line = new VLine(0, 0, 100, 50);
line.color = "DodgerBlue";
line.lineWeight = 3;

// Dashed line
let dashed = new VLine(-50, 0, 50, 0);
dashed.lineType = "Dashed";
dashed.color = "Orange";

// From angle
let angled = VLine.fromPointAngleLength(
    VPoint.internal(0, 0), 45, 80
);
angled.color = "LimeGreen";</pre>`,
    },
    {
        id: 'vcircle',
        category: 'Shapes',
        title: 'VCircle',
        content: `
<h2>VCircle</h2>
<p>A circle defined by center and radius.</p>

<h3>Constructors</h3>
<pre>new VCircle(center, radius)
new VCircle(cx, cy, radius)</pre>

<h3>Static Methods</h3>
<pre>VCircle.fromThreePoints(p1, p2, p3)
VCircle.fromTwoPoints(p1, p2)  // diameter endpoints
VCircle.fromCenterDiameter(center, diameter)</pre>

<h3>Properties</h3>
<table>
<tr><td><code>center</code></td><td>Center point</td></tr>
<tr><td><code>radius</code></td><td>Radius</td></tr>
<tr><td><code>area</code></td><td>Area (read-only)</td></tr>
<tr><td><code>circumference</code></td><td>Perimeter (read-only)</td></tr>
</table>

<h3>Methods</h3>
<table>
<tr><td><code>divide(n)</code></td><td>n equidistant points on circumference</td></tr>
<tr><td><code>pointAtAngle(radians)</code></td><td>Point at angle</td></tr>
<tr><td><code>project(point)</code></td><td>Closest point on circle</td></tr>
<tr><td><code>contains(point)</code></td><td>Is point inside?</td></tr>
<tr><td><code>offset(distance)</code></td><td>Concentric circle</td></tr>
</table>

<h3>Example</h3>
<pre>let c = new VCircle(0, 0, 50);
c.color = "Crimson";
c.fillColor = "rgba(220, 20, 60, 0.15)";

// Concentric circles
for (let r = 20; r <= 80; r += 20) {
    let ring = new VCircle(0, 0, r);
    ring.color = VColor.getRandomVibrantColor();
}</pre>`,
    },
    {
        id: 'varc',
        category: 'Shapes',
        title: 'VArc',
        content: `
<h2>VArc</h2>
<p>A circular arc from startAngle to endAngle (in degrees).</p>

<h3>Constructor</h3>
<pre>new VArc(center, radius, startAngle, endAngle)
new VArc(cx, cy, radius, startAngle, endAngle)</pre>

<h3>Properties</h3>
<table>
<tr><td><code>center</code>, <code>radius</code></td><td>Geometry</td></tr>
<tr><td><code>startAngle</code>, <code>endAngle</code></td><td>Angles in degrees</td></tr>
<tr><td><code>sweepAngle</code></td><td>Angular span</td></tr>
<tr><td><code>startPoint</code>, <code>endPoint</code></td><td>Arc endpoints</td></tr>
</table>

<h3>Example</h3>
<pre>let arc = new VArc(0, 0, 60, 0, 270);
arc.color = "Gold";
arc.lineWeight = 3;</pre>`,
    },
    {
        id: 'vrectangle',
        category: 'Shapes',
        title: 'VRectangle',
        content: `
<h2>VRectangle</h2>
<p>A rectangle defined by a corner point and dimensions.</p>

<h3>Constructors</h3>
<pre>new VRectangle(corner, width, height)
new VRectangle(x, y, width, height)</pre>

<h3>Properties</h3>
<table>
<tr><td><code>width</code>, <code>height</code></td><td>Dimensions</td></tr>
<tr><td><code>points</code></td><td>Array of 4 corner VPoints</td></tr>
</table>

<h3>Example</h3>
<pre>let rect = new VRectangle(-60, -30, 120, 60);
rect.color = "DodgerBlue";
rect.fillColor = "rgba(30, 144, 255, 0.2)";
rect.lineWeight = 2;</pre>`,
    },
    {
        id: 'vellipse',
        category: 'Shapes',
        title: 'VEllipse',
        content: `
<h2>VEllipse</h2>
<p>An ellipse with independent X and Y radii.</p>

<h3>Constructor</h3>
<pre>new VEllipse(center, radiusX, radiusY)
new VEllipse(cx, cy, radiusX, radiusY)</pre>

<h3>Example</h3>
<pre>let e = new VEllipse(0, 0, 80, 40);
e.color = "Orchid";
e.fillColor = "rgba(218, 112, 214, 0.15)";</pre>`,
    },
    {
        id: 'vpolygon',
        category: 'Shapes',
        title: 'VPolygon',
        content: `
<h2>VPolygon</h2>
<p>A closed polygon from a sequence of points.</p>

<h3>Constructors</h3>
<pre>new VPolygon(p1, p2, p3, ...)
new VPolygon([pointsArray])</pre>

<h3>Example</h3>
<pre>// Triangle
let tri = new VPolygon(
    VPoint.internal(0, 60),
    VPoint.internal(-50, -20),
    VPoint.internal(50, -20)
);
tri.color = "LimeGreen";
tri.fillColor = "rgba(50, 205, 50, 0.2)";

// Regular hexagon
let pts = [];
for (let i = 0; i < 6; i++) {
    let a = i * Math.PI / 3;
    pts.push(VPoint.internal(40 * Math.cos(a), 40 * Math.sin(a)));
}
let hex = new VPolygon(pts);
hex.color = "Gold";</pre>`,
    },
    {
        id: 'vpolyline',
        category: 'Shapes',
        title: 'VPolyline',
        content: `
<h2>VPolyline</h2>
<p>An open polyline (sequence of connected line segments).</p>

<h3>Constructors</h3>
<pre>new VPolyline(p1, p2, p3, ...)
new VPolyline([pointsArray])</pre>

<h3>Example</h3>
<pre>let wave = new VPolyline(
    VPoint.internal(-80, 0),
    VPoint.internal(-40, 30),
    VPoint.internal(0, -30),
    VPoint.internal(40, 30),
    VPoint.internal(80, 0)
);
wave.color = "Cyan";
wave.lineWeight = 2;</pre>`,
    },
    {
        id: 'vspline',
        category: 'Shapes',
        title: 'VSpline',
        content: `
<h2>VSpline</h2>
<p>A Catmull-Rom spline that passes through all control points.</p>

<h3>Constructors</h3>
<pre>new VSpline(p1, p2, p3, ...)
new VSpline([pointsArray])</pre>

<h3>Example</h3>
<pre>let spline = new VSpline(
    VPoint.internal(-60, 0),
    VPoint.internal(-20, 40),
    VPoint.internal(20, -40),
    VPoint.internal(60, 0)
);
spline.color = "HotPink";
spline.lineWeight = 2;</pre>`,
    },
    {
        id: 'vbezier',
        category: 'Shapes',
        title: 'VBezier',
        content: `
<h2>VBezier</h2>
<p>A cubic Bezier curve. P0 and P3 are endpoints, P1 and P2 are control points.</p>

<h3>Constructor</h3>
<pre>new VBezier(p0, p1, p2, p3)</pre>

<h3>Example</h3>
<pre>let bez = new VBezier(
    VPoint.internal(-60, 0),
    VPoint.internal(-20, 80),
    VPoint.internal(20, -80),
    VPoint.internal(60, 0)
);
bez.color = "MediumPurple";
bez.lineWeight = 2;</pre>`,
    },
    {
        id: 'varrow',
        category: 'Shapes',
        title: 'VArrow',
        content: `
<h2>VArrow</h2>
<p>A line with arrowhead(s).</p>

<h3>Constructors</h3>
<pre>new VArrow(start, end)
new VArrow(x1, y1, x2, y2)</pre>

<h3>Properties</h3>
<table>
<tr><td><code>doubleEnded</code></td><td>If true, arrowheads on both ends</td></tr>
<tr><td><code>arrowSize</code></td><td>Arrowhead size</td></tr>
</table>

<h3>Example</h3>
<pre>let arrow = new VArrow(0, 0, 80, 40);
arrow.color = "Orange";

let biArrow = new VArrow(-60, -20, 60, -20);
biArrow.doubleEnded = true;
biArrow.color = "Cyan";</pre>`,
    },
    {
        id: 'vtext',
        category: 'Shapes',
        title: 'VText',
        content: `
<h2>VText</h2>
<p>Text annotation on the canvas.</p>

<h3>Constructor</h3>
<pre>new VText(location, content, height?)</pre>

<h3>Properties</h3>
<table>
<tr><td><code>content</code></td><td>Text string</td></tr>
<tr><td><code>height</code></td><td>Font size in world units</td></tr>
<tr><td><code>font</code></td><td>Font family (use VFont constants)</td></tr>
<tr><td><code>fontWeight</code></td><td>VFontWeight.Normal or VFontWeight.Bold</td></tr>
<tr><td><code>anchor</code></td><td>Text anchor position (VTextAnchor)</td></tr>
</table>

<h3>Example</h3>
<pre>let label = new VText(VPoint.internal(0, 0), "Hello!", 16);
label.color = "White";
label.font = VFont.Consolas;
label.fontWeight = VFontWeight.Bold;</pre>`,
    },
    {
        id: 'vdimension',
        category: 'Shapes',
        title: 'VDimension & VRadialDimension',
        content: `
<h2>VDimension</h2>
<p>Linear dimension with extension lines and arrows.</p>
<pre>new VDimension(point1, point2)</pre>

<h2>VRadialDimension</h2>
<p>Radius/diameter dimension for circles.</p>
<pre>new VRadialDimension(circle)
new VRadialDimension(center, radius)</pre>

<h3>Example</h3>
<pre>let p1 = VPoint.internal(-40, 0);
let p2 = VPoint.internal(40, 0);
let line = new VLine(p1, p2);
line.color = "White";

let dim = new VDimension(p1, p2);
dim.offset = 15;
dim.color = "#888";

let c = new VCircle(0, 50, 25);
let rdim = new VRadialDimension(c);
rdim.color = "#888";</pre>`,
    },
    {
        id: 'vgroup',
        category: 'Shapes',
        title: 'VGroup',
        content: `
<h2>VGroup</h2>
<p>Container for grouping shapes together. Operations on the group apply to all children.</p>

<h3>Constructors</h3>
<pre>new VGroup(shape1, shape2, ...)
new VGroup([shapesArray])</pre>

<h3>Example</h3>
<pre>let c = new VCircle(0, 0, 30);
let l1 = new VLine(-30, 0, 30, 0);
let l2 = new VLine(0, -30, 0, 30);

let group = new VGroup(c, l1, l2);
group.color = "Gold";</pre>`,
    },
    {
        id: 'vgrid',
        category: 'Shapes',
        title: 'VGrid',
        content: `
<h2>VGrid</h2>
<p>A regular grid of points.</p>

<h3>Constructor</h3>
<pre>new VGrid(location, xCount, yCount, spacing?, centered?)</pre>

<h3>Example</h3>
<pre>let grid = new VGrid(VPoint.internal(0, 0), 5, 5, 20, true);
grid.color = "Gray";</pre>`,
    },

    // ── Styling ──────────────────────────────────────────────────────────
    {
        id: 'styling',
        category: 'Styling',
        title: 'Colors & Styling',
        content: `
<h2>Colors & Styling</h2>
<p>All shapes inherit these style properties from the <code>Shape</code> base class:</p>

<table>
<tr><td><code>color</code></td><td>Stroke color — any CSS color name, hex, or rgba</td></tr>
<tr><td><code>fillColor</code></td><td>Fill color — <code>"Transparent"</code> for no fill</td></tr>
<tr><td><code>lineWeight</code></td><td>Stroke thickness (default: 2.0)</td></tr>
<tr><td><code>lineType</code></td><td><code>"Continuous"</code>, <code>"Dashed"</code>, <code>"Dotted"</code>, <code>"DashDot"</code>, <code>"Center"</code>, <code>"Hidden"</code>, <code>"Phantom"</code></td></tr>
<tr><td><code>lineTypeScale</code></td><td>Scale factor for dash pattern</td></tr>
<tr><td><code>opacity</code></td><td>0.0 (invisible) to 1.0 (opaque)</td></tr>
<tr><td><code>isVisible</code></td><td>Show/hide shape</td></tr>
</table>

<h3>VColor Utilities</h3>
<pre>VColor.Red         // Named color constant
VColor.getRandomColor()
VColor.getRandomVibrantColor()</pre>

<h3>ShapeDefaults</h3>
<p>Set defaults for all subsequently created shapes:</p>
<pre>ShapeDefaults.globalColor = "Cyan";
ShapeDefaults.globalLineWeight = 1;
// All new shapes will use these defaults
let c = new VCircle(0, 0, 50); // Cyan, weight 1

ShapeDefaults.reset(); // Clear defaults</pre>`,
    },
    {
        id: 'line-types',
        category: 'Styling',
        title: 'Line Types',
        content: `
<h2>Line Types</h2>
<p>Available line dash patterns:</p>

<table>
<tr><td><code>"Continuous"</code></td><td>Solid line (default)</td></tr>
<tr><td><code>"Dashed"</code></td><td>Long dashes</td></tr>
<tr><td><code>"Dotted"</code></td><td>Dots</td></tr>
<tr><td><code>"DashDot"</code></td><td>Dash-dot pattern</td></tr>
<tr><td><code>"DashDotDot"</code></td><td>Dash-dot-dot pattern</td></tr>
<tr><td><code>"Center"</code></td><td>Long-short-long pattern</td></tr>
<tr><td><code>"Phantom"</code></td><td>Long-short-short pattern</td></tr>
<tr><td><code>"Hidden"</code></td><td>Short dashes</td></tr>
</table>

<h3>Example</h3>
<pre>let types = ["Continuous","Dashed","Dotted","DashDot","Center","Hidden"];
for (let i = 0; i < types.length; i++) {
    let line = new VLine(-80, 40 - i * 15, 80, 40 - i * 15);
    line.lineType = types[i];
    line.color = "White";
    let label = new VText(VPoint.internal(90, 40 - i * 15), types[i], 8);
    label.color = "Gray";
}</pre>`,
    },

    // ── Operations ───────────────────────────────────────────────────────
    {
        id: 'shape-methods',
        category: 'Operations',
        title: 'Common Shape Methods',
        content: `
<h2>Common Shape Methods</h2>
<p>All shapes inherit these methods:</p>

<table>
<tr><td><code>clone()</code></td><td>Create a registered copy</td></tr>
<tr><td><code>move({X, Y})</code></td><td>Translate the shape in-place</td></tr>
<tr><td><code>rotate(pivot, angleDeg)</code></td><td>Rotate around a point</td></tr>
<tr><td><code>flip(mirrorLine)</code></td><td>Mirror across a VLine</td></tr>
<tr><td><code>scale(center, factor)</code></td><td>Scale from a center point</td></tr>
<tr><td><code>getBounds()</code></td><td>Get BoundingBox</td></tr>
<tr><td><code>draw()</code></td><td>Register on canvas (auto-called)</td></tr>
<tr><td><code>remove()</code></td><td>Unregister from canvas</td></tr>
<tr><td><code>show()</code> / <code>hide()</code></td><td>Toggle visibility</td></tr>
</table>

<h3>Example</h3>
<pre>let rect = new VRectangle(0, 0, 40, 20);
rect.color = "White";

let copy = rect.clone();
copy.move({X: 60, Y: 0});
copy.color = "Cyan";

let rotated = rect.clone();
rotated.rotate(VPoint.internal(0, 0), 45);
rotated.color = "Orange";</pre>`,
    },
    {
        id: 'boolean-ops',
        category: 'Operations',
        title: 'Boolean Operations',
        content: `
<h2>Boolean Operations</h2>
<p>Perform geometric boolean operations on polygons.</p>

<table>
<tr><td><code>polygonIntersection(a, b)</code></td><td>Area common to both</td></tr>
<tr><td><code>polygonDifference(a, b)</code></td><td>A minus B</td></tr>
<tr><td><code>polygonUnion(a, b)</code></td><td>Combined area (convex hull)</td></tr>
<tr><td><code>pointInPolygon(pt, poly)</code></td><td>Is point inside polygon?</td></tr>
</table>

<h3>Example</h3>
<pre>let sq1 = new VRectangle(-30, -30, 60, 60);
sq1.color = "DodgerBlue";
sq1.opacity = 0.5;

let sq2 = new VRectangle(0, 0, 60, 60);
sq2.color = "Crimson";
sq2.opacity = 0.5;

let inter = polygonIntersection(sq1, sq2);
if (inter) {
    inter.color = "White";
    inter.fillColor = "rgba(255,255,255,0.3)";
}</pre>`,
    },
    {
        id: 'array-ops',
        category: 'Operations',
        title: 'Array Operations',
        content: `
<h2>Array Operations</h2>
<p>Create patterns of shape copies.</p>

<table>
<tr><td><code>linearArray(shape, count, dx, dy)</code></td><td>Linear copies along direction</td></tr>
<tr><td><code>rectangularArray(shape, rows, cols, dx, dy)</code></td><td>Grid pattern</td></tr>
<tr><td><code>polarArray(shape, count, center, totalAngle?)</code></td><td>Circular pattern</td></tr>
<tr><td><code>pathArray(shape, path, count)</code></td><td>Copies along a curve</td></tr>
<tr><td><code>mirror(shape, mirrorLine)</code></td><td>Reflected copy</td></tr>
</table>

<h3>Example</h3>
<pre>// Linear array
let dot = new VCircle(0, 0, 5);
dot.color = "Cyan";
linearArray(dot, 8, 20, 0);

// Polar array
let petal = new VEllipse(30, 0, 15, 5);
petal.color = "HotPink";
petal.fillColor = "rgba(255,105,180,0.2)";
polarArray(petal, 6, VPoint.internal(0, 0), 360);

// Rectangular grid
let sq = new VRectangle(-100, -100, 10, 10);
sq.color = "Gold";
rectangularArray(sq, 5, 5, 15, 15);</pre>`,
    },
    {
        id: 'geometry-helper',
        category: 'Operations',
        title: 'GeometryHelper',
        content: `
<h2>GeometryHelper</h2>
<p>Static utility methods for geometric calculations.</p>

<table>
<tr><td><code>GeometryHelper.rotatePoint(point, pivot, angleDeg)</code></td><td>Rotate a point</td></tr>
<tr><td><code>GeometryHelper.flipPoint(point, mirrorLine)</code></td><td>Mirror a point</td></tr>
<tr><td><code>GeometryHelper.normalizeAngle(deg)</code></td><td>Normalize to 0-360</td></tr>
<tr><td><code>GeometryHelper.lineLineIntersection(p1, p2, p3, p4)</code></td><td>Segment intersection point</td></tr>
<tr><td><code>GeometryHelper.pointToLineDistance(pt, start, end)</code></td><td>Distance from point to segment</td></tr>
<tr><td><code>GeometryHelper.circleCircleIntersection(c1, r1, c2, r2)</code></td><td>Circle intersection points</td></tr>
</table>`,
    },

    // ── Animation ────────────────────────────────────────────────────────
    {
        id: 'animation',
        category: 'Animation',
        title: 'Animation System',
        content: `
<h2>Animation System</h2>
<p>Create timeline-based animations with easing.</p>

<h3>Steps</h3>
<ol>
<li>Create shapes</li>
<li>Create a <code>Timeline</code></li>
<li>Add animations to the timeline</li>
<li>Pass the timeline to <code>Animator</code></li>
<li>Call <code>Animator.play()</code></li>
</ol>

<h3>Animation Types</h3>
<table>
<tr><td><code>DrawAnimation(shape, duration, easing?, delay?)</code></td><td>Animate drawFactor 0→1</td></tr>
<tr><td><code>MoveAnimation(shape, dx, dy, duration, easing?, delay?)</code></td><td>Move by offset</td></tr>
<tr><td><code>FadeAnimation(shape, from, to, duration, easing?, delay?)</code></td><td>Fade opacity</td></tr>
<tr><td><code>ScaleAnimation(shape, from, to, duration, easing?, delay?)</code></td><td>Scale line weight</td></tr>
<tr><td><code>RotateAnimation(shape, angle, duration, easing?, delay?)</code></td><td>Rotate</td></tr>
<tr><td><code>ColorAnimation(shape, from, to, duration, easing?, delay?)</code></td><td>Transition color</td></tr>
</table>

<h3>Example</h3>
<pre>let circle = new VCircle(0, 0, 50);
circle.color = "Crimson";
circle.drawFactor = 0;

let line = new VLine(-80, 0, 80, 0);
line.color = "DodgerBlue";
line.drawFactor = 0;

let tl = new Timeline();
tl.sequence(
    new DrawAnimation(circle, 1.0, 'easeOutCubic'),
    new DrawAnimation(line, 0.5, 'easeInOutQuad'),
    new MoveAnimation(circle, 0, 30, 0.8, 'easeOutBounce')
);

Animator.setTimeline(tl);
Animator.play();</pre>`,
    },
    {
        id: 'easing',
        category: 'Animation',
        title: 'Easing Functions',
        content: `
<h2>Easing Functions</h2>
<p>Pass as string name to animation constructors.</p>

<table>
<tr><td><code>"linear"</code></td><td>Constant speed</td></tr>
<tr><td><code>"easeInQuad"</code></td><td>Accelerate (quadratic)</td></tr>
<tr><td><code>"easeOutQuad"</code></td><td>Decelerate (quadratic)</td></tr>
<tr><td><code>"easeInOutQuad"</code></td><td>Smooth acceleration/deceleration</td></tr>
<tr><td><code>"easeInCubic"</code></td><td>Accelerate (cubic)</td></tr>
<tr><td><code>"easeOutCubic"</code></td><td>Decelerate (cubic)</td></tr>
<tr><td><code>"easeInOutCubic"</code></td><td>Smooth cubic</td></tr>
<tr><td><code>"easeInSine"</code></td><td>Sine ease in</td></tr>
<tr><td><code>"easeOutSine"</code></td><td>Sine ease out</td></tr>
<tr><td><code>"easeInOutSine"</code></td><td>Sine ease in/out</td></tr>
<tr><td><code>"easeInExpo"</code></td><td>Exponential in</td></tr>
<tr><td><code>"easeOutExpo"</code></td><td>Exponential out</td></tr>
<tr><td><code>"easeOutBounce"</code></td><td>Bouncing effect</td></tr>
<tr><td><code>"easeInElastic"</code></td><td>Elastic spring in</td></tr>
<tr><td><code>"easeOutElastic"</code></td><td>Elastic spring out</td></tr>
</table>`,
    },

    // ── Tools ────────────────────────────────────────────────────────────
    {
        id: 'drawing-tools',
        category: 'Tools',
        title: 'Drawing Tools',
        content: `
<h2>Drawing Tools</h2>
<p>Create shapes interactively by clicking on the canvas.</p>

<table>
<tr><td><b>Pointer</b> (V)</td><td>Select, move, and edit shapes</td></tr>
<tr><td><b>Point</b> (P)</td><td>Click to place a point</td></tr>
<tr><td><b>Line</b> (L)</td><td>Click start, click end</td></tr>
<tr><td><b>Circle</b> (C)</td><td>Click center, click edge (sets radius)</td></tr>
<tr><td><b>Rectangle</b> (R)</td><td>Click corner, click opposite corner</td></tr>
<tr><td><b>Measure</b> (M)</td><td>Click two points to measure distance and angle</td></tr>
</table>

<p>Hold <kbd>Shift</kbd> during Line/Rectangle for orthogonal constraint.</p>
<p>Press <kbd>Escape</kbd> to return to Pointer tool.</p>`,
    },
    {
        id: 'snap',
        category: 'Tools',
        title: 'Snap System',
        content: `
<h2>Snap System</h2>
<p>When drawing tools are active, the cursor snaps to nearby geometry. Toggle with <kbd>F9</kbd>.</p>

<h3>Snap Types</h3>
<table>
<tr><td style="color:#a6e3a1">&#9633; Green square</td><td><b>Endpoint</b> — snaps to shape endpoints</td></tr>
<tr><td style="color:#f9e2af">&#9651; Yellow triangle</td><td><b>Midpoint</b> — snaps to segment midpoints</td></tr>
<tr><td style="color:#f38ba8">&#9675; Red circle</td><td><b>Center</b> — snaps to shape centers</td></tr>
<tr><td style="color:#89b4fa">+ Blue cross</td><td><b>Grid</b> — snaps to grid intersections</td></tr>
</table>`,
    },
    {
        id: 'selection',
        category: 'Tools',
        title: 'Selection & Editing',
        content: `
<h2>Selection & Editing</h2>
<p>With the Pointer tool (V), click shapes to select them.</p>

<h3>Selection</h3>
<ul>
<li><b>Click</b> a shape to select it (deselects others)</li>
<li><b>Ctrl+Click</b> to add/remove from selection</li>
<li><b>Click and drag</b> on empty space to rubber-band select</li>
<li><b>Delete key</b> removes selected shapes</li>
</ul>

<h3>Moving</h3>
<p>Click and drag selected shapes to move them.</p>

<h3>Properties Panel (F4)</h3>
<p>When shapes are selected, the Properties panel shows editable properties: color, fill, line weight, line type, opacity, and type-specific values like position and radius.</p>`,
    },
    {
        id: 'layers',
        category: 'Tools',
        title: 'Layers',
        content: `
<h2>Layers</h2>
<p>Organize shapes into layers for visibility control and logical grouping.</p>

<ul>
<li>Click <b>Layers</b> button in toolbar to show the layer panel</li>
<li>Click <b>+ Add Layer</b> to create a new layer</li>
<li>Click a layer to set it as active (new shapes go here)</li>
<li>Toggle the eye icon to show/hide a layer</li>
<li>Toggle the lock icon to lock/unlock a layer</li>
</ul>`,
    },

    // ── Export ────────────────────────────────────────────────────────────
    {
        id: 'export',
        category: 'Export',
        title: 'Exporting',
        content: `
<h2>Exporting</h2>
<p>Export your visualization in multiple formats via the Export dropdown.</p>

<table>
<tr><td><b>SVG</b></td><td>Scalable vector graphics — ideal for web and print</td></tr>
<tr><td><b>PNG</b></td><td>Raster image of the current canvas view</td></tr>
<tr><td><b>PDF</b></td><td>PDF document (loaded on-demand from CDN)</td></tr>
<tr><td><b>DXF</b></td><td>AutoCAD R12 format for CAD software</td></tr>
</table>`,
    },

    // ── Utilities ────────────────────────────────────────────────────────
    {
        id: 'vxyz',
        category: 'Utilities',
        title: 'VXYZ Vector',
        content: `
<h2>VXYZ</h2>
<p>Immutable 3D vector, useful for direction calculations and transformations.</p>

<h3>Constructor</h3>
<pre>new VXYZ(x, y, z)</pre>

<h3>Methods</h3>
<table>
<tr><td><code>add(other)</code>, <code>subtract(other)</code></td><td>Vector arithmetic</td></tr>
<tr><td><code>multiply(scalar)</code>, <code>divide(scalar)</code></td><td>Scalar operations</td></tr>
<tr><td><code>getLength()</code></td><td>Magnitude</td></tr>
<tr><td><code>normalize()</code></td><td>Unit vector</td></tr>
<tr><td><code>dotProduct(other)</code></td><td>Dot product</td></tr>
<tr><td><code>crossProduct(other)</code></td><td>Cross product</td></tr>
<tr><td><code>angleTo(other)</code></td><td>Angle between vectors</td></tr>
<tr><td><code>rotate(deg)</code></td><td>2D rotation</td></tr>
<tr><td><code>distanceTo(other)</code></td><td>Distance</td></tr>
</table>`,
    },
    {
        id: 'boundingbox',
        category: 'Utilities',
        title: 'BoundingBox',
        content: `
<h2>BoundingBox</h2>
<p>Axis-aligned bounding box, returned by <code>shape.getBounds()</code>.</p>

<h3>Properties</h3>
<table>
<tr><td><code>min</code>, <code>max</code></td><td>Corner VPoints</td></tr>
<tr><td><code>width</code>, <code>height</code></td><td>Dimensions</td></tr>
<tr><td><code>area</code></td><td>Area</td></tr>
<tr><td><code>center</code></td><td>Center VPoint</td></tr>
</table>

<h3>Methods</h3>
<table>
<tr><td><code>contains(point)</code></td><td>Point inside?</td></tr>
<tr><td><code>intersects(other)</code></td><td>Boxes overlap?</td></tr>
<tr><td><code>union(other)</code></td><td>Combined box</td></tr>
<tr><td><code>expand(distance)</code></td><td>Grow by distance</td></tr>
</table>`,
    },
    {
        id: 'console',
        category: 'Utilities',
        title: 'Console Output',
        content: `
<h2>Console Output</h2>
<p>Use <code>console.log()</code> to output messages to the Console panel below the canvas.</p>

<pre>console.log("Hello!");       // Normal text
console.warn("Warning!");    // Yellow text
console.error("Error!");     // Red text
console.info("Info");        // Dim text</pre>

<p>The console also shows execution time, shape count, and any runtime errors with file/line info.</p>`,
    },
];

// ============================================================================
// Help Panel UI
// ============================================================================
export class HelpPanel {
    constructor() {
        this._visible = false;
        this._overlay = null;
        this._searchQuery = '';
        this._selectedTopic = null;
        this._build();
    }

    get visible() { return this._visible; }

    toggle() {
        this._visible ? this.hide() : this.show();
    }

    show() {
        this._visible = true;
        this._overlay.style.display = 'flex';
        this._overlay.querySelector('.help-search').focus();
        if (!this._selectedTopic) this._selectTopic('welcome');
    }

    hide() {
        this._visible = false;
        this._overlay.style.display = 'none';
    }

    _build() {
        this._overlay = document.createElement('div');
        this._overlay.className = 'help-overlay';
        this._overlay.style.display = 'none';

        this._overlay.innerHTML = `
        <div class="help-panel">
            <div class="help-sidebar">
                <div class="help-sidebar-header">
                    <input type="text" class="help-search" placeholder="Search help topics..." />
                </div>
                <div class="help-topic-list"></div>
            </div>
            <div class="help-content">
                <div class="help-content-header">
                    <span class="help-content-title"></span>
                    <button class="help-close-btn" title="Close (Esc)">&times;</button>
                </div>
                <div class="help-content-body"></div>
            </div>
        </div>`;

        // Close
        this._overlay.querySelector('.help-close-btn').addEventListener('click', () => this.hide());
        this._overlay.addEventListener('click', (e) => {
            if (e.target === this._overlay) this.hide();
        });

        // Search
        const searchInput = this._overlay.querySelector('.help-search');
        searchInput.addEventListener('input', () => {
            this._searchQuery = searchInput.value.toLowerCase();
            this._renderTopicList();
        });

        // Escape key
        this._overlay.addEventListener('keydown', (e) => {
            if (e.key === 'Escape') this.hide();
        });

        document.body.appendChild(this._overlay);
        this._renderTopicList();
    }

    _renderTopicList() {
        const list = this._overlay.querySelector('.help-topic-list');
        list.innerHTML = '';

        const filtered = this._searchQuery
            ? HELP_TOPICS.filter(t =>
                t.title.toLowerCase().includes(this._searchQuery) ||
                t.category.toLowerCase().includes(this._searchQuery) ||
                t.content.toLowerCase().includes(this._searchQuery))
            : HELP_TOPICS;

        // Group by category
        const categories = {};
        for (const topic of filtered) {
            if (!categories[topic.category]) categories[topic.category] = [];
            categories[topic.category].push(topic);
        }

        for (const [cat, topics] of Object.entries(categories)) {
            const catEl = document.createElement('div');
            catEl.className = 'help-category';
            catEl.textContent = cat;
            list.appendChild(catEl);

            for (const topic of topics) {
                const item = document.createElement('div');
                item.className = 'help-topic-item' + (this._selectedTopic === topic.id ? ' active' : '');
                item.textContent = topic.title;
                item.addEventListener('click', () => this._selectTopic(topic.id));
                list.appendChild(item);
            }
        }
    }

    _selectTopic(id) {
        this._selectedTopic = id;
        const topic = HELP_TOPICS.find(t => t.id === id);
        if (!topic) return;

        this._overlay.querySelector('.help-content-title').textContent = topic.title;
        this._overlay.querySelector('.help-content-body').innerHTML = topic.content;
        this._renderTopicList();
    }
}
