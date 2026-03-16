// Code2Viz Web - IntelliSense Provider
// Provides autocompletion and parameter hints for the geometry API

// ============================================================================
// API Completion Data
// ============================================================================

// Global types available in user code
const GLOBAL_COMPLETIONS = [
    // Classes
    { text: 'VPoint', kind: 'class', detail: 'new VPoint(x, y)', doc: '2D point shape. Auto-registers on canvas.' },
    { text: 'VLine', kind: 'class', detail: 'new VLine(start, end) | new VLine(x1, y1, x2, y2)', doc: 'Line segment between two points.' },
    { text: 'VCircle', kind: 'class', detail: 'new VCircle(center, radius) | new VCircle(cx, cy, r)', doc: 'Circle defined by center and radius.' },
    { text: 'VArc', kind: 'class', detail: 'new VArc(center, radius, startAngle, endAngle)', doc: 'Circular arc from startAngle to endAngle (degrees).' },
    { text: 'VRectangle', kind: 'class', detail: 'new VRectangle(corner, width, height) | new VRectangle(x, y, w, h)', doc: 'Rectangle defined by corner point and dimensions.' },
    { text: 'VEllipse', kind: 'class', detail: 'new VEllipse(center, radiusX, radiusY)', doc: 'Ellipse with independent X and Y radii.' },
    { text: 'VPolygon', kind: 'class', detail: 'new VPolygon(p1, p2, p3, ...) | new VPolygon([points])', doc: 'Closed polygon from a sequence of points.' },
    { text: 'VPolyline', kind: 'class', detail: 'new VPolyline(p1, p2, p3, ...) | new VPolyline([points])', doc: 'Open polyline through a sequence of points.' },
    { text: 'VSpline', kind: 'class', detail: 'new VSpline(p1, p2, p3, ...) | new VSpline([points])', doc: 'Catmull-Rom spline passing through all control points.' },
    { text: 'VBezier', kind: 'class', detail: 'new VBezier(p0, p1, p2, p3)', doc: 'Cubic Bezier curve. P0/P3 are endpoints, P1/P2 are control points.' },
    { text: 'VArrow', kind: 'class', detail: 'new VArrow(start, end) | new VArrow(x1, y1, x2, y2)', doc: 'Line with arrowhead(s).' },
    { text: 'VText', kind: 'class', detail: 'new VText(location, content, height?)', doc: 'Text annotation on canvas.' },
    { text: 'VDimension', kind: 'class', detail: 'new VDimension(point1, point2)', doc: 'Linear dimension with extension lines and arrows.' },
    { text: 'VRadialDimension', kind: 'class', detail: 'new VRadialDimension(circle) | new VRadialDimension(center, radius)', doc: 'Radius/diameter dimension.' },
    { text: 'VGroup', kind: 'class', detail: 'new VGroup(...shapes) | new VGroup([shapes])', doc: 'Container for grouping shapes together.' },
    { text: 'VGrid', kind: 'class', detail: 'new VGrid(location, xCount, yCount, spacing?, centered?)', doc: 'Regular grid of points.' },
    { text: 'VXYZ', kind: 'class', detail: 'new VXYZ(x, y, z)', doc: 'Immutable 3D vector.' },
    { text: 'BoundingBox', kind: 'class', detail: 'new BoundingBox(min, max)', doc: 'Axis-aligned bounding box.' },

    // Utility objects
    { text: 'VColor', kind: 'object', detail: 'VColor.Red, VColor.getRandomColor(), ...', doc: 'Color constants and utilities.' },
    { text: 'VFont', kind: 'object', detail: 'VFont.Arial, VFont.Consolas, ...', doc: 'Font family constants.' },
    { text: 'VFontWeight', kind: 'object', detail: 'VFontWeight.Normal | VFontWeight.Bold', doc: 'Font weight constants.' },
    { text: 'VTextAnchor', kind: 'object', detail: 'VTextAnchor.BottomLeft, MiddleCenter, ...', doc: 'Text anchor positions.' },
    { text: 'ShapeDefaults', kind: 'object', detail: 'ShapeDefaults.globalColor = "Red"', doc: 'Global default styles for new shapes.' },
    { text: 'GeometryHelper', kind: 'object', detail: 'GeometryHelper.rotatePoint(...)', doc: 'Static geometry utilities.' },
    { text: 'Shape', kind: 'class', detail: 'Shape.AutoRegister', doc: 'Base class for all shapes.' },

    // Functions
    { text: 'getRegistry', kind: 'function', detail: 'getRegistry() → Shape[]', doc: 'Returns array of all registered shapes.' },

    // Constants
    { text: 'EPSILON', kind: 'constant', detail: '1e-9', doc: 'Floating point comparison tolerance.' },
    { text: 'LineTypes', kind: 'object', detail: 'Continuous, Dashed, Dotted, ...', doc: 'Line dash patterns.' },
    { text: 'Math', kind: 'object', detail: 'Math.PI, Math.sin, Math.cos, ...', doc: 'JavaScript Math object.' },
    { text: 'console', kind: 'object', detail: 'console.log, console.warn, console.error', doc: 'Console output (shown in panel below).' },

    // Animation
    { text: 'Animator', kind: 'object', detail: 'Animator.play(), .pause(), .stop()', doc: 'Global animation controller. Call setTimeline() then play().' },
    { text: 'Timeline', kind: 'class', detail: 'new Timeline()', doc: 'Animation timeline. Add animations with .add(), .sequence(), .parallel().' },
    { text: 'Easing', kind: 'object', detail: 'Easing.linear, .easeInQuad, .easeOutBounce, ...', doc: 'Easing functions for animations.' },
    { text: 'DrawAnimation', kind: 'class', detail: 'new DrawAnimation(shape, duration, easing?, delay?)', doc: 'Animates shape drawing from 0 to 1 (drawFactor).' },
    { text: 'MoveAnimation', kind: 'class', detail: 'new MoveAnimation(shape, dx, dy, duration, easing?, delay?)', doc: 'Moves shape by (dx, dy) over duration.' },
    { text: 'FadeAnimation', kind: 'class', detail: 'new FadeAnimation(shape, fromOpacity, toOpacity, duration, easing?, delay?)', doc: 'Fades shape between opacities.' },
    { text: 'ScaleAnimation', kind: 'class', detail: 'new ScaleAnimation(shape, fromScale, toScale, duration, easing?, delay?)', doc: 'Scales shape line weight.' },
    { text: 'RotateAnimation', kind: 'class', detail: 'new RotateAnimation(shape, angleDeg, duration, easing?, delay?)', doc: 'Rotates shape by angle over duration.' },
    { text: 'ColorAnimation', kind: 'class', detail: 'new ColorAnimation(shape, fromColor, toColor, duration, easing?, delay?)', doc: 'Transitions shape color.' },

    // Boolean operations
    { text: 'polygonIntersection', kind: 'function', detail: 'polygonIntersection(shapeA, shapeB) → VPolygon', doc: 'Compute intersection of two polygons.' },
    { text: 'polygonDifference', kind: 'function', detail: 'polygonDifference(shapeA, shapeB) → VPolygon', doc: 'Compute difference A - B.' },
    { text: 'polygonUnion', kind: 'function', detail: 'polygonUnion(shapeA, shapeB) → VPolygon', doc: 'Compute union of two polygons (convex hull approximation).' },
    { text: 'pointInPolygon', kind: 'function', detail: 'pointInPolygon(point, polygon) → boolean', doc: 'Test if point is inside polygon.' },

    // Array operations
    { text: 'linearArray', kind: 'function', detail: 'linearArray(shape, count, dx, dy) → Shape[]', doc: 'Create linear array of shape copies.' },
    { text: 'rectangularArray', kind: 'function', detail: 'rectangularArray(shape, rows, cols, dx, dy) → Shape[]', doc: 'Create rectangular grid of shape copies.' },
    { text: 'polarArray', kind: 'function', detail: 'polarArray(shape, count, center, totalAngle?) → Shape[]', doc: 'Create polar array around center point.' },
    { text: 'pathArray', kind: 'function', detail: 'pathArray(shape, path, count) → Shape[]', doc: 'Distribute shape copies along a path.' },
    { text: 'mirror', kind: 'function', detail: 'mirror(shape, mirrorLine) → Shape', doc: 'Create mirrored copy of shape.' },
];

// Instance members by type
const MEMBER_COMPLETIONS = {
    // Shared Shape properties (inherited by all shapes)
    _shape: [
        { text: 'id', kind: 'property', detail: '→ number', doc: 'Unique auto-incrementing shape ID.' },
        { text: 'name', kind: 'property', detail: '→ string', doc: 'User-assigned name.' },
        { text: 'color', kind: 'property', detail: '→ string', doc: 'Stroke color (e.g., "Red", "#FF0000").' },
        { text: 'fillColor', kind: 'property', detail: '→ string', doc: 'Fill color. "Transparent" for no fill.' },
        { text: 'lineWeight', kind: 'property', detail: '→ number', doc: 'Stroke thickness (default: 2.0).' },
        { text: 'lineType', kind: 'property', detail: '→ string', doc: '"Continuous", "Dashed", "Dotted", "DashDot", "DashDotDot", "Center", "Phantom", "Hidden"' },
        { text: 'lineTypeScale', kind: 'property', detail: '→ number', doc: 'Scale factor for dash patterns (default: 1.0).' },
        { text: 'isVisible', kind: 'property', detail: '→ boolean', doc: 'Whether shape renders on canvas.' },
        { text: 'isPlaced', kind: 'property', detail: '→ boolean (readonly)', doc: 'Whether shape is in the registry.' },
        { text: 'opacity', kind: 'property', detail: '→ number', doc: 'Opacity 0..1 (default: 1).' },
        { text: 'drawFactor', kind: 'property', detail: '→ number', doc: 'Progressive drawing 0..1.' },
        { text: 'offsetX', kind: 'property', detail: '→ number', doc: 'Translation X offset.' },
        { text: 'offsetY', kind: 'property', detail: '→ number', doc: 'Translation Y offset.' },
        { text: 'rotationAngle', kind: 'property', detail: '→ number', doc: 'Rotation in degrees.' },
        { text: 'draw', kind: 'method', detail: '() → void', doc: 'Register shape on canvas (auto-called on creation).' },
        { text: 'remove', kind: 'method', detail: '() → void', doc: 'Remove shape from canvas.' },
        { text: 'show', kind: 'method', detail: '() → void', doc: 'Make shape visible.' },
        { text: 'hide', kind: 'method', detail: '() → void', doc: 'Make shape invisible.' },
        { text: 'clone', kind: 'method', detail: '() → Shape', doc: 'Deep copy of the shape.' },
        { text: 'move', kind: 'method', detail: '(vector: VXYZ) → void', doc: 'Translate shape by vector.' },
        { text: 'rotate', kind: 'method', detail: '(pivot: VPoint, angleDegrees: number) → void', doc: 'Rotate around pivot point.' },
        { text: 'flip', kind: 'method', detail: '(mirrorLine: VLine) → void', doc: 'Mirror across a line.' },
        { text: 'scale', kind: 'method', detail: '(center: VPoint, factor: number) → void', doc: 'Scale from center point.' },
        { text: 'getBounds', kind: 'method', detail: '() → BoundingBox', doc: 'Axis-aligned bounding box.' },
        { text: 'distanceTo', kind: 'method', detail: '(point: VPoint) → number', doc: 'Distance to a point.' },
        { text: 'contains', kind: 'method', detail: '(point: VPoint) → boolean', doc: 'Point containment test.' },
        { text: 'bringAbove', kind: 'method', detail: '(other: Shape) → void', doc: 'Move above another shape in Z-order.' },
        { text: 'sendBehind', kind: 'method', detail: '(other: Shape) → void', doc: 'Move behind another shape in Z-order.' },
    ],

    VPoint: [
        { text: 'X', kind: 'property', detail: '→ number', doc: 'X coordinate.' },
        { text: 'Y', kind: 'property', detail: '→ number', doc: 'Y coordinate.' },
        { text: 'add', kind: 'method', detail: '(other: VPoint | VXYZ) → VPoint', doc: 'Vector addition.' },
        { text: 'subtract', kind: 'method', detail: '(other: VPoint) → VPoint', doc: 'Vector subtraction.' },
        { text: 'multiplyScalar', kind: 'method', detail: '(s: number) → VPoint', doc: 'Scalar multiplication.' },
        { text: 'divideScalar', kind: 'method', detail: '(s: number) → VPoint', doc: 'Scalar division.' },
        { text: 'asVXYZ', kind: 'method', detail: '() → VXYZ', doc: 'Convert to 3D vector (Z=0).' },
        { text: 'distanceTo', kind: 'method', detail: '(other: VPoint) → number', doc: 'Distance to another point.' },
    ],

    VLine: [
        { text: 'start', kind: 'property', detail: '→ VPoint', doc: 'Start point.' },
        { text: 'end', kind: 'property', detail: '→ VPoint', doc: 'End point.' },
        { text: 'midPoint', kind: 'property', detail: '→ VPoint', doc: 'Midpoint of line.' },
        { text: 'direction', kind: 'property', detail: '→ VXYZ', doc: 'Normalized direction vector.' },
        { text: 'vertices', kind: 'property', detail: '→ VPoint[]', doc: '[start, end]' },
        { text: 'getLength', kind: 'method', detail: '() → number', doc: 'Length of the line segment.' },
        { text: 'evaluate', kind: 'method', detail: '(t: number) → VPoint', doc: 'Point at parameter t ∈ [0,1].' },
        { text: 'pointAtParameter', kind: 'method', detail: '(t: number) → VPoint', doc: 'Same as evaluate(t).' },
        { text: 'parameterAtPoint', kind: 'method', detail: '(point: VPoint) → number', doc: 'Parameter of closest point.' },
        { text: 'divide', kind: 'method', detail: '(n: number) → VPoint[]', doc: 'Divide into n segments.' },
        { text: 'measure', kind: 'method', detail: '(segLen: number) → VPoint[]', doc: 'Points at fixed intervals.' },
        { text: 'project', kind: 'method', detail: '(point: VPoint) → VPoint', doc: 'Closest point on line.' },
        { text: 'pointAtSegmentLength', kind: 'method', detail: '(d: number) → VPoint', doc: 'Point at distance from start.' },
        { text: 'offset', kind: 'method', detail: '(distance: number) → VLine', doc: 'Parallel offset line.' },
        { text: 'normalAtPoint', kind: 'method', detail: '(point: VPoint) → VXYZ', doc: 'Normal vector at point.' },
    ],

    VCircle: [
        { text: 'center', kind: 'property', detail: '→ VPoint', doc: 'Center point.' },
        { text: 'radius', kind: 'property', detail: '→ number', doc: 'Radius.' },
        { text: 'area', kind: 'property', detail: '→ number', doc: 'π * r²' },
        { text: 'circumference', kind: 'property', detail: '→ number', doc: '2 * π * r' },
        { text: 'startPoint', kind: 'property', detail: '→ VPoint', doc: 'Point at angle 0 (East).' },
        { text: 'getLength', kind: 'method', detail: '() → number', doc: 'Circumference.' },
        { text: 'divide', kind: 'method', detail: '(n: number) → VPoint[]', doc: 'n equally-spaced points.' },
        { text: 'measure', kind: 'method', detail: '(segLen: number) → VPoint[]', doc: 'Points at arc-length intervals.' },
        { text: 'project', kind: 'method', detail: '(point: VPoint) → VPoint', doc: 'Closest point on circle.' },
        { text: 'pointAtAngle', kind: 'method', detail: '(radians: number) → VPoint', doc: 'Point at angle.' },
        { text: 'pointAtParameter', kind: 'method', detail: '(t: number) → VPoint', doc: 'Point at parameter t ∈ [0,1].' },
        { text: 'offset', kind: 'method', detail: '(distance: number) → VCircle', doc: 'Concentric circle.' },
        { text: 'contains', kind: 'method', detail: '(point: VPoint) → boolean', doc: 'Point inside circle.' },
    ],

    VArc: [
        { text: 'center', kind: 'property', detail: '→ VPoint', doc: 'Center point.' },
        { text: 'radius', kind: 'property', detail: '→ number', doc: 'Radius.' },
        { text: 'startAngle', kind: 'property', detail: '→ number', doc: 'Start angle in degrees.' },
        { text: 'endAngle', kind: 'property', detail: '→ number', doc: 'End angle in degrees.' },
        { text: 'sweepAngle', kind: 'property', detail: '→ number', doc: 'Sweep angle in degrees.' },
        { text: 'startPoint', kind: 'property', detail: '→ VPoint', doc: 'Point at start angle.' },
        { text: 'endPoint', kind: 'property', detail: '→ VPoint', doc: 'Point at end angle.' },
        { text: 'midPoint', kind: 'property', detail: '→ VPoint', doc: 'Point at parameter 0.5.' },
        { text: 'getLength', kind: 'method', detail: '() → number', doc: 'Arc length.' },
        { text: 'pointAtParameter', kind: 'method', detail: '(t: number) → VPoint', doc: 'Point at parameter t ∈ [0,1].' },
        { text: 'divide', kind: 'method', detail: '(n: number) → VPoint[]', doc: 'Divide into n segments.' },
        { text: 'measure', kind: 'method', detail: '(segLen: number) → VPoint[]', doc: 'Points at arc-length intervals.' },
        { text: 'project', kind: 'method', detail: '(point: VPoint) → VPoint', doc: 'Closest point on arc.' },
        { text: 'offset', kind: 'method', detail: '(distance: number) → VArc', doc: 'Concentric arc.' },
    ],

    VRectangle: [
        { text: 'corner', kind: 'property', detail: '→ VPoint', doc: 'Lower-left corner.' },
        { text: 'width', kind: 'property', detail: '→ number', doc: 'Width.' },
        { text: 'height', kind: 'property', detail: '→ number', doc: 'Height.' },
        { text: 'area', kind: 'property', detail: '→ number', doc: 'Width * Height.' },
        { text: 'center', kind: 'property', detail: '→ VPoint', doc: 'Center point.' },
        { text: 'points', kind: 'property', detail: '→ VPoint[4]', doc: 'Corner points (CCW from bottom-left).' },
        { text: 'contains', kind: 'method', detail: '(point: VPoint) → boolean', doc: 'Point containment test.' },
    ],

    VEllipse: [
        { text: 'center', kind: 'property', detail: '→ VPoint', doc: 'Center point.' },
        { text: 'radiusX', kind: 'property', detail: '→ number', doc: 'X-axis radius.' },
        { text: 'radiusY', kind: 'property', detail: '→ number', doc: 'Y-axis radius.' },
        { text: 'area', kind: 'property', detail: '→ number', doc: 'π * radiusX * radiusY.' },
        { text: 'circumference', kind: 'property', detail: '→ number', doc: 'Ramanujan approximation.' },
        { text: 'pointAtParameter', kind: 'method', detail: '(t: number) → VPoint', doc: 'Point at parameter t ∈ [0,1].' },
        { text: 'getLength', kind: 'method', detail: '() → number', doc: 'Perimeter length.' },
        { text: 'divide', kind: 'method', detail: '(n: number) → VPoint[]', doc: 'Divide into n segments.' },
        { text: 'project', kind: 'method', detail: '(point: VPoint) → VPoint', doc: 'Closest point on ellipse.' },
        { text: 'contains', kind: 'method', detail: '(point: VPoint) → boolean', doc: 'Point inside ellipse.' },
    ],

    VPolygon: [
        { text: 'points', kind: 'property', detail: '→ VPoint[]', doc: 'Vertices in order.' },
        { text: 'area', kind: 'property', detail: '→ number', doc: 'Unsigned area (Shoelace formula).' },
        { text: 'signedArea', kind: 'property', detail: '→ number', doc: 'Signed area (+CCW, -CW).' },
        { text: 'centroid', kind: 'property', detail: '→ VPoint', doc: 'Average of all vertices.' },
        { text: 'startPoint', kind: 'property', detail: '→ VPoint', doc: 'First vertex.' },
        { text: 'getLength', kind: 'method', detail: '() → number', doc: 'Perimeter length.' },
        { text: 'divide', kind: 'method', detail: '(n: number) → VPoint[]', doc: 'Divide perimeter into n segments.' },
        { text: 'measure', kind: 'method', detail: '(segLen: number) → VPoint[]', doc: 'Points at fixed intervals.' },
        { text: 'project', kind: 'method', detail: '(point: VPoint) → VPoint', doc: 'Closest point on polygon edge.' },
        { text: 'contains', kind: 'method', detail: '(point: VPoint) → boolean', doc: 'Point inside polygon (ray casting).' },
    ],

    VPolyline: [
        { text: 'points', kind: 'property', detail: '→ VPoint[]', doc: 'Vertices in order.' },
        { text: 'startPoint', kind: 'property', detail: '→ VPoint', doc: 'First vertex.' },
        { text: 'endPoint', kind: 'property', detail: '→ VPoint', doc: 'Last vertex.' },
        { text: 'addPoint', kind: 'method', detail: '(point: VPoint) | (x, y) → void', doc: 'Append a point.' },
        { text: 'getLength', kind: 'method', detail: '() → number', doc: 'Total polyline length.' },
        { text: 'divide', kind: 'method', detail: '(n: number) → VPoint[]', doc: 'Divide into n segments.' },
        { text: 'measure', kind: 'method', detail: '(segLen: number) → VPoint[]', doc: 'Points at fixed intervals.' },
        { text: 'project', kind: 'method', detail: '(point: VPoint) → VPoint', doc: 'Closest point on polyline.' },
        { text: 'pointAtSegmentLength', kind: 'method', detail: '(d: number) → VPoint', doc: 'Point at distance from start.' },
        { text: 'offset', kind: 'method', detail: '(distance: number) → VPolyline', doc: 'Offset polyline.' },
    ],

    VSpline: [
        { text: 'controlPoints', kind: 'property', detail: '→ VPoint[]', doc: 'Spline control points.' },
        { text: 'segmentsPerSpan', kind: 'property', detail: '→ number', doc: 'Segments per span (default: 16).' },
        { text: 'tension', kind: 'property', detail: '→ number', doc: 'Tension (default: 0.5).' },
        { text: 'startPoint', kind: 'property', detail: '→ VPoint', doc: 'First control point.' },
        { text: 'endPoint', kind: 'property', detail: '→ VPoint', doc: 'Last control point.' },
        { text: 'evaluate', kind: 'method', detail: '(t: number) → VPoint', doc: 'Point at parameter t ∈ [0,1].' },
        { text: 'getRenderPoints', kind: 'method', detail: '() → VPoint[]', doc: 'All computed render points.' },
        { text: 'getLength', kind: 'method', detail: '() → number', doc: 'Spline length.' },
        { text: 'divide', kind: 'method', detail: '(n: number) → VPoint[]', doc: 'Divide into n segments.' },
    ],

    VBezier: [
        { text: 'p0', kind: 'property', detail: '→ VPoint', doc: 'Start point.' },
        { text: 'p1', kind: 'property', detail: '→ VPoint', doc: 'First control point.' },
        { text: 'p2', kind: 'property', detail: '→ VPoint', doc: 'Second control point.' },
        { text: 'p3', kind: 'property', detail: '→ VPoint', doc: 'End point.' },
        { text: 'segments', kind: 'property', detail: '→ number', doc: 'Render segments (default: 32).' },
        { text: 'startPoint', kind: 'property', detail: '→ VPoint', doc: 'Same as p0.' },
        { text: 'endPoint', kind: 'property', detail: '→ VPoint', doc: 'Same as p3.' },
        { text: 'midPoint', kind: 'property', detail: '→ VPoint', doc: 'Point at t=0.5.' },
        { text: 'evaluate', kind: 'method', detail: '(t: number) → VPoint', doc: 'Point at parameter t ∈ [0,1].' },
        { text: 'getRenderPoints', kind: 'method', detail: '() → VPoint[]', doc: 'All computed render points.' },
        { text: 'getLength', kind: 'method', detail: '() → number', doc: 'Bezier length.' },
        { text: 'divide', kind: 'method', detail: '(n: number) → VPoint[]', doc: 'Divide into n segments.' },
        { text: 'project', kind: 'method', detail: '(point: VPoint) → VPoint', doc: 'Closest point on curve.' },
    ],

    VArrow: [
        { text: 'start', kind: 'property', detail: '→ VPoint', doc: 'Start point.' },
        { text: 'end', kind: 'property', detail: '→ VPoint', doc: 'End point (arrow tip).' },
        { text: 'headLength', kind: 'property', detail: '→ number', doc: 'Arrowhead length (default: 15).' },
        { text: 'headAngle', kind: 'property', detail: '→ number', doc: 'Arrowhead wing angle in degrees (default: 30).' },
        { text: 'doubleEnded', kind: 'property', detail: '→ boolean', doc: 'Arrowhead at both ends.' },
        { text: 'midPoint', kind: 'property', detail: '→ VPoint', doc: 'Midpoint.' },
        { text: 'getEndArrowhead', kind: 'method', detail: '() → { wing1, wing2 }', doc: 'End arrowhead wing points.' },
        { text: 'getStartArrowhead', kind: 'method', detail: '() → { wing1, wing2 }', doc: 'Start arrowhead wing points (if doubleEnded).' },
    ],

    VText: [
        { text: 'location', kind: 'property', detail: '→ VPoint', doc: 'Text position.' },
        { text: 'content', kind: 'property', detail: '→ string', doc: 'Text string.' },
        { text: 'height', kind: 'property', detail: '→ number', doc: 'Font size (default: 12).' },
        { text: 'width', kind: 'property', detail: '→ number', doc: 'Explicit width (0 = auto).' },
        { text: 'font', kind: 'property', detail: '→ string', doc: 'Font family. Use VFont.Arial etc.' },
        { text: 'fontWeight', kind: 'property', detail: '→ string', doc: 'VFontWeight.Normal or VFontWeight.Bold.' },
        { text: 'anchor', kind: 'property', detail: '→ string', doc: 'VTextAnchor.BottomLeft, MiddleCenter, etc.' },
    ],

    VDimension: [
        { text: 'point1', kind: 'property', detail: '→ VPoint', doc: 'First endpoint.' },
        { text: 'point2', kind: 'property', detail: '→ VPoint', doc: 'Second endpoint.' },
        { text: 'offset', kind: 'property', detail: '→ number', doc: 'Dimension line offset (default: 20).' },
        { text: 'arrowSize', kind: 'property', detail: '→ number', doc: 'Arrowhead size (default: 8).' },
        { text: 'customText', kind: 'property', detail: '→ string?', doc: 'Override calculated text.' },
        { text: 'decimalPlaces', kind: 'property', detail: '→ number', doc: 'Decimal precision (default: 2).' },
        { text: 'textHeight', kind: 'property', detail: '→ number', doc: 'Font size (default: 12).' },
        { text: 'prefix', kind: 'property', detail: '→ string', doc: 'Text prefix.' },
        { text: 'suffix', kind: 'property', detail: '→ string', doc: 'Text suffix.' },
        { text: 'suppressExtLine1', kind: 'property', detail: '→ boolean', doc: 'Hide first extension line.' },
        { text: 'suppressExtLine2', kind: 'property', detail: '→ boolean', doc: 'Hide second extension line.' },
        { text: 'textBackgroundOpaque', kind: 'property', detail: '→ boolean', doc: 'Opaque text background.' },
        { text: 'extensionLineColor', kind: 'property', detail: '→ string?', doc: 'Extension line color override.' },
        { text: 'dimensionLineColor', kind: 'property', detail: '→ string?', doc: 'Dimension line color override.' },
        { text: 'textColor', kind: 'property', detail: '→ string?', doc: 'Text color override.' },
        { text: 'distance', kind: 'property', detail: '→ number (readonly)', doc: 'Calculated distance.' },
        { text: 'displayText', kind: 'property', detail: '→ string (readonly)', doc: 'Formatted display text.' },
        { text: 'getDimensionGeometry', kind: 'method', detail: '() → { dimStart, dimEnd, textPos, ... }', doc: 'Computed dimension geometry.' },
    ],

    VRadialDimension: [
        { text: 'center', kind: 'property', detail: '→ VPoint', doc: 'Circle/arc center.' },
        { text: 'radius', kind: 'property', detail: '→ number', doc: 'Radius value.' },
        { text: 'leaderAngle', kind: 'property', detail: '→ number', doc: 'Leader angle in degrees (default: 45).' },
        { text: 'showDiameter', kind: 'property', detail: '→ boolean', doc: 'Show diameter instead of radius.' },
        { text: 'arrowSize', kind: 'property', detail: '→ number', doc: 'Arrowhead size.' },
        { text: 'textHeight', kind: 'property', detail: '→ number', doc: 'Font size.' },
        { text: 'value', kind: 'property', detail: '→ number (readonly)', doc: 'Radius or diameter.' },
        { text: 'displayText', kind: 'property', detail: '→ string (readonly)', doc: 'Formatted text (R... or Ø...).' },
        { text: 'getDimensionGeometry', kind: 'method', detail: '() → { leaderStart, leaderEnd, textPos, circumPoint }', doc: 'Computed geometry.' },
    ],

    VGroup: [
        { text: 'shapes', kind: 'property', detail: '→ Shape[]', doc: 'Contained shapes.' },
        { text: 'count', kind: 'property', detail: '→ number', doc: 'Number of shapes.' },
        { text: 'at', kind: 'method', detail: '(index: number) → Shape', doc: 'Get shape by index.' },
        { text: 'add', kind: 'method', detail: '(shape: Shape) → VGroup', doc: 'Add shape (chainable).' },
        { text: 'addRange', kind: 'method', detail: '(shapes: Shape[]) → VGroup', doc: 'Add multiple shapes.' },
        { text: 'removeShape', kind: 'method', detail: '(shape: Shape) → boolean', doc: 'Remove shape.' },
        { text: 'removeAt', kind: 'method', detail: '(index: number) → void', doc: 'Remove by index.' },
        { text: 'clear', kind: 'method', detail: '() → void', doc: 'Remove all shapes.' },
        { text: 'containsShape', kind: 'method', detail: '(shape: Shape) → boolean', doc: 'Check if shape is in group.' },
    ],

    VGrid: [
        { text: 'points', kind: 'property', detail: '→ VPoint[]', doc: 'All grid points.' },
        { text: 'location', kind: 'property', detail: '→ VPoint', doc: 'Reference location.' },
        { text: 'xCount', kind: 'property', detail: '→ number', doc: 'Points along X.' },
        { text: 'yCount', kind: 'property', detail: '→ number', doc: 'Points along Y.' },
        { text: 'xSpacing', kind: 'property', detail: '→ number', doc: 'X-axis spacing.' },
        { text: 'ySpacing', kind: 'property', detail: '→ number', doc: 'Y-axis spacing.' },
        { text: 'centered', kind: 'property', detail: '→ boolean', doc: 'Whether centered at location.' },
        { text: 'count', kind: 'property', detail: '→ number', doc: 'Total point count.' },
        { text: 'at', kind: 'method', detail: '(index) | (col, row) → VPoint', doc: 'Get point by index or position.' },
    ],

    VXYZ: [
        { text: 'X', kind: 'property', detail: '→ number', doc: 'X component.' },
        { text: 'Y', kind: 'property', detail: '→ number', doc: 'Y component.' },
        { text: 'Z', kind: 'property', detail: '→ number', doc: 'Z component.' },
        { text: 'add', kind: 'method', detail: '(other: VXYZ) → VXYZ', doc: 'Vector addition.' },
        { text: 'subtract', kind: 'method', detail: '(other: VXYZ) → VXYZ', doc: 'Vector subtraction.' },
        { text: 'multiply', kind: 'method', detail: '(scalar: number) → VXYZ', doc: 'Scalar multiply.' },
        { text: 'divide', kind: 'method', detail: '(scalar: number) → VXYZ', doc: 'Scalar divide.' },
        { text: 'negate', kind: 'method', detail: '() → VXYZ', doc: 'Negated vector.' },
        { text: 'clone', kind: 'method', detail: '() → VXYZ', doc: 'Copy.' },
        { text: 'getLength', kind: 'method', detail: '() → number', doc: 'Magnitude.' },
        { text: 'normalize', kind: 'method', detail: '() → VXYZ', doc: 'Unit vector.' },
        { text: 'distanceTo', kind: 'method', detail: '(other: VXYZ) → number', doc: 'Distance.' },
        { text: 'dotProduct', kind: 'method', detail: '(other: VXYZ) → number', doc: 'Dot product.' },
        { text: 'crossProduct', kind: 'method', detail: '(other: VXYZ) → VXYZ', doc: 'Cross product.' },
        { text: 'angleTo', kind: 'method', detail: '(other: VXYZ) → number', doc: 'Angle in radians.' },
        { text: 'rotate', kind: 'method', detail: '(degrees: number) → VXYZ', doc: 'Rotate around Z axis.' },
        { text: 'isAlmostEqualTo', kind: 'method', detail: '(other: VXYZ, tol?) → boolean', doc: 'Fuzzy equality.' },
        { text: 'isZeroLength', kind: 'method', detail: '() → boolean', doc: 'Is zero vector.' },
        { text: 'asVPoint', kind: 'method', detail: '() → VPoint', doc: 'Convert to VPoint (drops Z).' },
    ],

    BoundingBox: [
        { text: 'min', kind: 'property', detail: '→ VPoint', doc: 'Lower-left corner.' },
        { text: 'max', kind: 'property', detail: '→ VPoint', doc: 'Upper-right corner.' },
        { text: 'width', kind: 'property', detail: '→ number', doc: 'X extent.' },
        { text: 'height', kind: 'property', detail: '→ number', doc: 'Y extent.' },
        { text: 'area', kind: 'property', detail: '→ number', doc: 'Width * Height.' },
        { text: 'center', kind: 'property', detail: '→ VPoint', doc: 'Center point.' },
        { text: 'contains', kind: 'method', detail: '(point: VPoint) → boolean', doc: 'Point containment.' },
        { text: 'intersects', kind: 'method', detail: '(other: BoundingBox) → boolean', doc: 'Box intersection test.' },
        { text: 'union', kind: 'method', detail: '(other: BoundingBox) → BoundingBox', doc: 'Combined bounding box.' },
        { text: 'expand', kind: 'method', detail: '(distance: number) → BoundingBox', doc: 'Expand by distance.' },
    ],

    // Static members (accessed via ClassName.method)
    'VPoint.': [
        { text: 'internal', kind: 'method', detail: '(x: number, y: number) → VPoint', doc: 'Create unregistered point for calculations.' },
    ],
    'VCircle.': [
        { text: 'fromThreePoints', kind: 'method', detail: '(p1, p2, p3) → VCircle', doc: 'Circumcircle through three points.' },
        { text: 'fromCenterDiameter', kind: 'method', detail: '(center, diameter) → VCircle', doc: 'From center and diameter.' },
        { text: 'fromTwoPoints', kind: 'method', detail: '(p1, p2) → VCircle', doc: 'Diameter endpoints.' },
    ],
    'VArc.': [
        { text: 'fromThreePoints', kind: 'method', detail: '(start, mid, end) → VArc', doc: 'Arc through three points.' },
        { text: 'fromCenterStartAngle', kind: 'method', detail: '(center, start, sweepAngle) → VArc', doc: 'From center, start point, and sweep angle.' },
    ],
    'VLine.': [
        { text: 'fromPointAngleLength', kind: 'method', detail: '(start, angleDeg, length) → VLine', doc: 'From point, angle, and length.' },
    ],
    'VArrow.': [
        { text: 'fromDirection', kind: 'method', detail: '(start, direction, length) → VArrow', doc: 'From start point, direction vector, and length.' },
    ],
    'VXYZ.': [
        { text: 'Zero', kind: 'property', detail: '→ VXYZ(0,0,0)', doc: 'Zero vector.' },
        { text: 'BasisX', kind: 'property', detail: '→ VXYZ(1,0,0)', doc: 'Unit X vector.' },
        { text: 'BasisY', kind: 'property', detail: '→ VXYZ(0,1,0)', doc: 'Unit Y vector.' },
        { text: 'BasisZ', kind: 'property', detail: '→ VXYZ(0,0,1)', doc: 'Unit Z vector.' },
    ],
    'Shape.': [
        { text: 'AutoRegister', kind: 'property', detail: '→ boolean', doc: 'Enable/disable auto-registration (default: true).' },
    ],

    VColor: [
        // Color constants
        ...['Red', 'Green', 'Blue', 'Yellow', 'Orange', 'Purple', 'Pink', 'Cyan', 'Magenta',
            'White', 'Black', 'Gray', 'Brown', 'Coral', 'Crimson', 'DarkBlue', 'DarkGreen',
            'DarkRed', 'Gold', 'Indigo', 'Lime', 'Maroon', 'Navy', 'Olive', 'Silver', 'Teal',
            'Violet', 'SkyBlue', 'SteelBlue', 'Salmon', 'Plum', 'DodgerBlue', 'RoyalBlue',
            'ForestGreen', 'Tomato', 'OrangeRed', 'HotPink', 'Turquoise', 'Chartreuse',
            'SpringGreen', 'Khaki', 'LightBlue', 'LightGreen', 'LightCoral', 'LightPink',
        ].map(c => ({ text: c, kind: 'property', detail: '→ string', doc: `Color constant: ${c}` })),
        { text: 'getRandomColor', kind: 'method', detail: '(pastel?: boolean) → string', doc: 'Random color.' },
        { text: 'getRandomVibrantColor', kind: 'method', detail: '() → string', doc: 'Random vibrant color.' },
        { text: 'getRandomPastelColor', kind: 'method', detail: '() → string', doc: 'Random pastel color.' },
        { text: 'fromRgb', kind: 'method', detail: '(r, g, b) → string', doc: 'Hex color from RGB.' },
        { text: 'fromArgb', kind: 'method', detail: '(a, r, g, b) → string', doc: 'RGBA color.' },
        { text: 'withOpacity', kind: 'method', detail: '(r, g, b, opacity) → string', doc: 'RGBA color with opacity.' },
        { text: 'getVibrantColors', kind: 'method', detail: '() → string[]', doc: 'Array of vibrant colors.' },
        { text: 'getPastelColors', kind: 'method', detail: '() → string[]', doc: 'Array of pastel colors.' },
    ],

    VFont: [
        ...['Arial', 'TimesNewRoman', 'CourierNew', 'Verdana', 'Georgia', 'Consolas',
            'Calibri', 'SegoeUI', 'ComicSansMS', 'Impact', 'LucidaConsole',
        ].map(f => ({ text: f, kind: 'property', detail: `→ "${f}"`, doc: `Font: ${f}` })),
    ],

    VFontWeight: [
        { text: 'Normal', kind: 'property', detail: '→ "normal"', doc: 'Normal weight.' },
        { text: 'Bold', kind: 'property', detail: '→ "bold"', doc: 'Bold weight.' },
    ],

    VTextAnchor: [
        ...['BottomLeft', 'BottomCenter', 'BottomRight', 'MiddleLeft', 'MiddleCenter',
            'MiddleRight', 'TopLeft', 'TopCenter', 'TopRight',
        ].map(a => ({ text: a, kind: 'property', detail: `→ "${a}"`, doc: `Anchor: ${a}` })),
    ],

    ShapeDefaults: [
        { text: 'globalColor', kind: 'property', detail: '→ string?', doc: 'Default stroke color for new shapes.' },
        { text: 'globalFillColor', kind: 'property', detail: '→ string?', doc: 'Default fill color.' },
        { text: 'globalLineWeight', kind: 'property', detail: '→ number?', doc: 'Default line weight.' },
        { text: 'globalLineType', kind: 'property', detail: '→ string?', doc: 'Default line type.' },
        { text: 'globalLineTypeScale', kind: 'property', detail: '→ number?', doc: 'Default line type scale.' },
        { text: 'reset', kind: 'method', detail: '() → void', doc: 'Reset all defaults to null.' },
    ],

    GeometryHelper: [
        { text: 'rotatePoint', kind: 'method', detail: '(point, pivot, angleDeg) → VPoint', doc: 'Rotate point around pivot.' },
        { text: 'flipPoint', kind: 'method', detail: '(point, mirrorLine) → VPoint', doc: 'Reflect point across line.' },
        { text: 'normalizeAngle', kind: 'method', detail: '(degrees) → number', doc: 'Normalize to [0, 360).' },
        { text: 'lineLineIntersection', kind: 'method', detail: '(p1, p2, p3, p4) → VPoint?', doc: 'Line segment intersection.' },
        { text: 'pointToLineDistance', kind: 'method', detail: '(point, lineStart, lineEnd) → number', doc: 'Point-to-segment distance.' },
        { text: 'circleCircleIntersection', kind: 'method', detail: '(c1, r1, c2, r2) → VPoint[]', doc: 'Circle-circle intersections.' },
    ],
};

// Constructor signatures for parameter hints
const CONSTRUCTOR_SIGNATURES = {
    VPoint: [
        { params: ['x: number', 'y: number'], doc: 'Create a visible point at (x, y).' },
    ],
    VLine: [
        { params: ['start: VPoint', 'end: VPoint'], doc: 'Line from start to end point.' },
        { params: ['x1: number', 'y1: number', 'x2: number', 'y2: number'], doc: 'Line from (x1,y1) to (x2,y2).' },
    ],
    VCircle: [
        { params: ['center: VPoint', 'radius: number'], doc: 'Circle at center with radius.' },
        { params: ['cx: number', 'cy: number', 'radius: number'], doc: 'Circle at (cx,cy) with radius.' },
    ],
    VArc: [
        { params: ['center: VPoint', 'radius: number', 'startAngle: number', 'endAngle: number'], doc: 'Arc (angles in degrees).' },
        { params: ['cx: number', 'cy: number', 'radius: number', 'startAngle: number', 'endAngle: number'], doc: 'Arc from coordinates.' },
    ],
    VRectangle: [
        { params: ['corner: VPoint', 'width: number', 'height: number'], doc: 'Rectangle from corner.' },
        { params: ['x: number', 'y: number', 'width: number', 'height: number'], doc: 'Rectangle from (x,y).' },
        { params: ['bottomLeft: VPoint', 'topRight: VPoint'], doc: 'Rectangle from two corners.' },
    ],
    VEllipse: [
        { params: ['center: VPoint', 'radiusX: number', 'radiusY: number'], doc: 'Ellipse at center.' },
        { params: ['cx: number', 'cy: number', 'radiusX: number', 'radiusY: number'], doc: 'Ellipse from coordinates.' },
    ],
    VPolygon: [
        { params: ['...points: VPoint[]'], doc: 'Polygon from points.' },
        { params: ['points: VPoint[]'], doc: 'Polygon from array.' },
    ],
    VPolyline: [
        { params: ['...points: VPoint[]'], doc: 'Polyline from points.' },
        { params: ['points: VPoint[]'], doc: 'Polyline from array.' },
    ],
    VSpline: [
        { params: ['...points: VPoint[]'], doc: 'Catmull-Rom spline through points.' },
    ],
    VBezier: [
        { params: ['p0: VPoint', 'p1: VPoint', 'p2: VPoint', 'p3: VPoint'], doc: 'Cubic Bezier. P0/P3 endpoints, P1/P2 control.' },
        { params: ['x0', 'y0', 'x1', 'y1', 'x2', 'y2', 'x3', 'y3'], doc: 'Cubic Bezier from coordinates.' },
    ],
    VArrow: [
        { params: ['start: VPoint', 'end: VPoint'], doc: 'Arrow from start to end.' },
        { params: ['x1: number', 'y1: number', 'x2: number', 'y2: number'], doc: 'Arrow from coordinates.' },
    ],
    VText: [
        { params: ['location: VPoint', 'content: string', 'height?: number'], doc: 'Text at location. Height default: 12.' },
    ],
    VDimension: [
        { params: ['point1: VPoint', 'point2: VPoint'], doc: 'Linear dimension between two points.' },
        { params: ['x1: number', 'y1: number', 'x2: number', 'y2: number'], doc: 'Dimension from coordinates.' },
    ],
    VRadialDimension: [
        { params: ['circle: VCircle'], doc: 'Radial dimension from circle.' },
        { params: ['arc: VArc'], doc: 'Radial dimension from arc.' },
        { params: ['center: VPoint', 'radius: number'], doc: 'Radial dimension from center + radius.' },
    ],
    VGroup: [
        { params: ['...shapes: Shape[]'], doc: 'Group of shapes.' },
        { params: ['shapes: Shape[]'], doc: 'Group from array.' },
    ],
    VGrid: [
        { params: ['location: VPoint', 'xCount: number', 'yCount: number', 'spacing?: number', 'centered?: boolean'], doc: 'Grid of points.' },
        { params: ['location: VPoint', 'xCount: number', 'yCount: number', 'xSpacing: number', 'ySpacing: number', 'centered?: boolean'], doc: 'Grid with separate spacing.' },
    ],
    VXYZ: [
        { params: ['x?: number', 'y?: number', 'z?: number'], doc: 'Immutable 3D vector.' },
    ],
};

// Map variable names to types (built by analyzing user code)
const KNOWN_TYPES = {
    // Constructors → type name
    VPoint: 'VPoint', VLine: 'VLine', VCircle: 'VCircle', VArc: 'VArc',
    VRectangle: 'VRectangle', VEllipse: 'VEllipse', VPolygon: 'VPolygon',
    VPolyline: 'VPolyline', VSpline: 'VSpline', VBezier: 'VBezier',
    VArrow: 'VArrow', VText: 'VText', VDimension: 'VDimension',
    VRadialDimension: 'VRadialDimension', VGroup: 'VGroup', VGrid: 'VGrid',
    VXYZ: 'VXYZ', BoundingBox: 'BoundingBox',
};

// ============================================================================
// Hint Provider
// ============================================================================
export function vizHint(cmEditor, options) {
    const cursor = cmEditor.getCursor();
    const line = cmEditor.getLine(cursor.line);
    const end = cursor.ch;

    // Find the token before cursor
    let start = end;
    while (start > 0 && /[\w.]/.test(line[start - 1])) start--;
    const token = line.slice(start, end);

    // Determine context
    const dotIndex = token.lastIndexOf('.');
    let list = [];

    if (dotIndex >= 0) {
        // Member completion: "something.xxx"
        const objPart = token.slice(0, dotIndex);
        const memberPrefix = token.slice(dotIndex + 1).toLowerCase();

        // Check for static access first (e.g., "VPoint.", "VColor.")
        const staticKey = objPart + '.';
        const staticMembers = MEMBER_COMPLETIONS[staticKey];
        const instanceType = resolveType(cmEditor, objPart, cursor.line);

        // Collect all matching members
        const candidates = new Map();

        // Add static members if they exist
        if (staticMembers) {
            for (const m of staticMembers) candidates.set(m.text, m);
        }

        // Add instance members
        if (instanceType && MEMBER_COMPLETIONS[instanceType]) {
            for (const m of MEMBER_COMPLETIONS[instanceType]) candidates.set(m.text, m);
        }

        // If it's a known utility object (VColor, VFont, etc.)
        if (MEMBER_COMPLETIONS[objPart]) {
            for (const m of MEMBER_COMPLETIONS[objPart]) candidates.set(m.text, m);
        }

        // Add base Shape members for all shape types
        if (instanceType && instanceType !== 'VXYZ' && instanceType !== 'BoundingBox') {
            for (const m of MEMBER_COMPLETIONS._shape) {
                if (!candidates.has(m.text)) candidates.set(m.text, m);
            }
        }

        // Filter by prefix
        for (const [text, m] of candidates) {
            if (text.toLowerCase().startsWith(memberPrefix)) {
                list.push(makeCompletionItem(m, start + dotIndex + 1));
            }
        }
    } else {
        // Global completion
        const prefix = token.toLowerCase();
        if (prefix.length < 1) return null;

        for (const item of GLOBAL_COMPLETIONS) {
            if (item.text.toLowerCase().startsWith(prefix)) {
                list.push(makeCompletionItem(item, start));
            }
        }

        // Also add user-defined variables from the document
        const userVars = extractUserVariables(cmEditor, cursor.line);
        for (const v of userVars) {
            if (v.toLowerCase().startsWith(prefix) && !list.some(l => l.text === v)) {
                list.push({
                    text: v,
                    displayText: v,
                    className: 'hint-variable',
                    render: (el, self, data) => renderHintItem(el, v, 'variable', '', ''),
                });
            }
        }
    }

    if (list.length === 0) return null;

    const result = {
        list,
        from: CodeMirror.Pos(cursor.line, list[0]._from !== undefined ? list[0]._from : start),
        to: CodeMirror.Pos(cursor.line, end),
    };

    // Show documentation tooltip when selection changes
    CodeMirror.on(result, 'select', (completion, el) => {
        showDocTooltip(completion, el);
    });
    CodeMirror.on(result, 'close', () => {
        hideDocTooltip();
    });

    return result;
}

function makeCompletionItem(m, from) {
    return {
        text: m.text,
        displayText: m.text,
        _from: from,
        _kind: m.kind,
        _detail: m.detail,
        _doc: m.doc,
        className: `hint-${m.kind}`,
        render: (el, self, data) => renderHintItem(el, m.text, m.kind, m.detail, m.doc),
    };
}

function renderHintItem(el, text, kind, detail, doc) {
    // Icon
    const icon = document.createElement('span');
    icon.className = 'hint-icon hint-icon-' + kind;
    icon.textContent = kind === 'class' ? 'C' : kind === 'method' ? 'M' : kind === 'property' ? 'P' :
                       kind === 'function' ? 'F' : kind === 'object' ? 'O' : kind === 'constant' ? '#' : 'V';
    el.appendChild(icon);

    // Name
    const name = document.createElement('span');
    name.className = 'hint-name';
    name.textContent = text;
    el.appendChild(name);

    // Detail
    if (detail) {
        const det = document.createElement('span');
        det.className = 'hint-detail';
        det.textContent = detail;
        el.appendChild(det);
    }
}

// ============================================================================
// Type Resolution (simple heuristic)
// ============================================================================
function resolveType(cmEditor, varName, currentLine) {
    // Check if it's a known global type (for static access)
    if (KNOWN_TYPES[varName]) return KNOWN_TYPES[varName];

    // Search backwards for assignment: let/const/var varName = new TypeName(
    const doc = cmEditor.getDoc();
    const totalLines = doc.lineCount();
    const patterns = [
        new RegExp(`(?:let|const|var)\\s+${escapeRegex(varName)}\\s*=\\s*new\\s+(\\w+)`),
        new RegExp(`(?:let|const|var)\\s+${escapeRegex(varName)}\\s*=\\s*(\\w+)\\.fromThreePoints`),
        new RegExp(`(?:let|const|var)\\s+${escapeRegex(varName)}\\s*=\\s*(\\w+)\\.from\\w+`),
        new RegExp(`(?:let|const|var)\\s+${escapeRegex(varName)}\\s*=\\s*(\\w+)\\.internal`),
    ];

    for (let i = 0; i <= currentLine && i < totalLines; i++) {
        const lineText = doc.getLine(i);
        for (const pat of patterns) {
            const match = lineText.match(pat);
            if (match && KNOWN_TYPES[match[1]]) {
                return KNOWN_TYPES[match[1]];
            }
        }
    }

    // Check method return types (e.g., .clone(), .offset(), .midPoint, etc.)
    // This is a simplified heuristic
    return null;
}

function extractUserVariables(cmEditor, currentLine) {
    const doc = cmEditor.getDoc();
    const vars = new Set();
    const totalLines = doc.lineCount();
    for (let i = 0; i < totalLines; i++) {
        const line = doc.getLine(i);
        const matches = line.matchAll(/(?:let|const|var)\s+(\w+)/g);
        for (const m of matches) vars.add(m[1]);
    }
    return [...vars];
}

function escapeRegex(str) {
    return str.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

// ============================================================================
// Documentation Tooltip (sidecar)
// ============================================================================
let _docTooltip = null;

function showDocTooltip(completion, hintEl) {
    hideDocTooltip();
    if (!completion._doc && !completion._detail) return;

    const tooltip = document.createElement('div');
    tooltip.className = 'hint-doc-tooltip';

    if (completion._detail) {
        const sig = document.createElement('div');
        sig.className = 'hint-doc-signature';
        sig.textContent = completion._detail;
        tooltip.appendChild(sig);
    }
    if (completion._doc) {
        const doc = document.createElement('div');
        doc.className = 'hint-doc-text';
        doc.textContent = completion._doc;
        tooltip.appendChild(doc);
    }

    document.body.appendChild(tooltip);
    _docTooltip = tooltip;

    // Position next to the hint list
    const hints = document.querySelector('.CodeMirror-hints');
    if (hints) {
        const rect = hints.getBoundingClientRect();
        tooltip.style.left = (rect.right + 4) + 'px';
        tooltip.style.top = rect.top + 'px';

        // If it would go off-screen right, show on left side
        const tooltipRect = tooltip.getBoundingClientRect();
        if (tooltipRect.right > window.innerWidth - 10) {
            tooltip.style.left = (rect.left - tooltipRect.width - 4) + 'px';
        }
    }
}

function hideDocTooltip() {
    if (_docTooltip) {
        _docTooltip.remove();
        _docTooltip = null;
    }
}

// ============================================================================
// Parameter Hint (signature help)
// ============================================================================
let _paramWidget = null;

export function setupParameterHints(cmEditor) {
    cmEditor.on('cursorActivity', () => {
        updateParameterHint(cmEditor);
    });
    cmEditor.on('change', () => {
        setTimeout(() => updateParameterHint(cmEditor), 50);
    });
}

function updateParameterHint(cmEditor) {
    const cursor = cmEditor.getCursor();
    const line = cmEditor.getLine(cursor.line);
    const beforeCursor = line.slice(0, cursor.ch);

    // Find the innermost unclosed function call
    const callInfo = findFunctionCall(beforeCursor);
    if (!callInfo) {
        hideParameterHint();
        return;
    }

    const { funcName, argIndex } = callInfo;

    // Check if it's a constructor call
    let sigs = null;
    if (CONSTRUCTOR_SIGNATURES[funcName]) {
        sigs = CONSTRUCTOR_SIGNATURES[funcName];
    }

    // Check if it's a static method call like VCircle.fromThreePoints
    if (!sigs) {
        const dotMatch = funcName.match(/^(\w+)\.(\w+)$/);
        if (dotMatch) {
            const staticKey = dotMatch[1] + '.';
            const methods = MEMBER_COMPLETIONS[staticKey];
            if (methods) {
                const method = methods.find(m => m.text === dotMatch[2]);
                if (method) {
                    sigs = [{ params: [method.detail.replace(/^.*?\(/, '').replace(/\).*$/, '')], doc: method.doc }];
                }
            }
        }
    }

    if (!sigs || sigs.length === 0) {
        hideParameterHint();
        return;
    }

    showParameterHint(cmEditor, sigs, argIndex, cursor);
}

function findFunctionCall(text) {
    let depth = 0;
    let argIndex = 0;
    let funcEnd = -1;

    // Scan backwards from end to find the opening paren of current call
    for (let i = text.length - 1; i >= 0; i--) {
        const ch = text[i];
        if (ch === ')') depth++;
        else if (ch === '(') {
            if (depth === 0) {
                funcEnd = i;
                break;
            }
            depth--;
        } else if (ch === ',' && depth === 0) {
            argIndex++;
        }
    }

    if (funcEnd < 0) return null;

    // Extract function name (including possible "new " prefix and dots)
    let funcStart = funcEnd - 1;
    while (funcStart >= 0 && /[\w.]/.test(text[funcStart])) funcStart--;
    funcStart++;

    let funcName = text.slice(funcStart, funcEnd).trim();

    // Check for "new" keyword
    const beforeFunc = text.slice(0, funcStart).trimEnd();
    if (beforeFunc.endsWith('new')) {
        funcName = funcName; // It's a constructor, use as-is
    }

    return { funcName, argIndex };
}

function showParameterHint(cmEditor, sigs, argIndex, cursor) {
    hideParameterHint();

    const widget = document.createElement('div');
    widget.className = 'param-hint';

    for (let si = 0; si < sigs.length; si++) {
        const sig = sigs[si];
        const sigDiv = document.createElement('div');
        sigDiv.className = 'param-hint-sig';

        // If multiple overloads, show index
        if (sigs.length > 1) {
            const idx = document.createElement('span');
            idx.className = 'param-hint-overload';
            idx.textContent = `${si + 1}/${sigs.length}  `;
            sigDiv.appendChild(idx);
        }

        sigDiv.appendChild(document.createTextNode('('));

        const params = Array.isArray(sig.params) ? sig.params : [sig.params];
        for (let pi = 0; pi < params.length; pi++) {
            if (pi > 0) sigDiv.appendChild(document.createTextNode(', '));
            const span = document.createElement('span');
            span.textContent = params[pi];
            if (pi === argIndex) {
                span.className = 'param-hint-active';
            }
            sigDiv.appendChild(span);
        }

        sigDiv.appendChild(document.createTextNode(')'));
        widget.appendChild(sigDiv);

        if (sig.doc && si === 0) {
            const docDiv = document.createElement('div');
            docDiv.className = 'param-hint-doc';
            docDiv.textContent = sig.doc;
            widget.appendChild(docDiv);
        }
    }

    document.body.appendChild(widget);
    _paramWidget = widget;

    // Position above cursor
    const coords = cmEditor.cursorCoords(true, 'page');
    widget.style.left = coords.left + 'px';
    widget.style.top = (coords.top - widget.offsetHeight - 4) + 'px';

    // Keep on screen
    const rect = widget.getBoundingClientRect();
    if (rect.left + rect.width > window.innerWidth - 10) {
        widget.style.left = (window.innerWidth - rect.width - 10) + 'px';
    }
    if (rect.top < 0) {
        widget.style.top = (coords.bottom + 4) + 'px';
    }
}

function hideParameterHint() {
    if (_paramWidget) {
        _paramWidget.remove();
        _paramWidget = null;
    }
}
