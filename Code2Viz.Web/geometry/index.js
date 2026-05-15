// Code2Viz Geometry Library - JavaScript Edition
// Complete 2D geometry library for interactive visualization

const EPSILON = 1e-9;

// ============================================================================
// VXYZ - Immutable 3D Vector
// ============================================================================
class VXYZ {
    constructor(x = 0, y = 0, z = 0) {
        this._x = x; this._y = y; this._z = z;
    }
    get X() { return this._x; }
    get Y() { return this._y; }
    get Z() { return this._z; }

    static get Zero() { return new VXYZ(0, 0, 0); }
    static get BasisX() { return new VXYZ(1, 0, 0); }
    static get BasisY() { return new VXYZ(0, 1, 0); }
    static get BasisZ() { return new VXYZ(0, 0, 1); }

    add(o) { return new VXYZ(this._x + o.X, this._y + o.Y, this._z + o.Z); }
    subtract(o) { return new VXYZ(this._x - o.X, this._y - o.Y, this._z - o.Z); }
    multiply(s) { return new VXYZ(this._x * s, this._y * s, this._z * s); }
    divide(s) { return new VXYZ(this._x / s, this._y / s, this._z / s); }
    negate() { return new VXYZ(-this._x, -this._y, -this._z); }
    clone() { return new VXYZ(this._x, this._y, this._z); }
    getLength() { return Math.sqrt(this._x * this._x + this._y * this._y + this._z * this._z); }
    normalize() { const l = this.getLength(); return l < EPSILON ? this : this.divide(l); }
    distanceTo(o) { return this.subtract(o).getLength(); }
    dotProduct(o) { return this._x * o.X + this._y * o.Y + this._z * o.Z; }
    crossProduct(o) {
        return new VXYZ(this._y * o.Z - this._z * o.Y, this._z * o.X - this._x * o.Z, this._x * o.Y - this._y * o.X);
    }
    angleTo(o) {
        const d = this.dotProduct(o);
        const l = this.getLength() * o.getLength();
        return l < EPSILON ? 0 : Math.acos(Math.max(-1, Math.min(1, d / l)));
    }
    rotate(deg) {
        const r = deg * Math.PI / 180;
        return new VXYZ(this._x * Math.cos(r) - this._y * Math.sin(r), this._x * Math.sin(r) + this._y * Math.cos(r), this._z);
    }
    isAlmostEqualTo(o, tol = EPSILON) {
        return Math.abs(this._x - o.X) < tol && Math.abs(this._y - o.Y) < tol && Math.abs(this._z - o.Z) < tol;
    }
    isZeroLength() { return this.getLength() < EPSILON; }
    asVPoint() { return VPoint.internal(this._x, this._y); }
    toString() { return `(${this._x.toFixed(4)}, ${this._y.toFixed(4)}, ${this._z.toFixed(4)})`; }
}

// ============================================================================
// BoundingBox
// ============================================================================
class BoundingBox {
    constructor(min, max) { this.min = min; this.max = max; }
    get width() { return this.max.X - this.min.X; }
    get height() { return this.max.Y - this.min.Y; }
    get area() { return this.width * this.height; }
    get center() { return VPoint.internal((this.min.X + this.max.X) / 2, (this.min.Y + this.max.Y) / 2); }
    contains(p) { return p.X >= this.min.X && p.X <= this.max.X && p.Y >= this.min.Y && p.Y <= this.max.Y; }
    intersects(o) { return this.min.X <= o.max.X && this.max.X >= o.min.X && this.min.Y <= o.max.Y && this.max.Y >= o.min.Y; }
    union(o) {
        return new BoundingBox(
            VPoint.internal(Math.min(this.min.X, o.min.X), Math.min(this.min.Y, o.min.Y)),
            VPoint.internal(Math.max(this.max.X, o.max.X), Math.max(this.max.Y, o.max.Y))
        );
    }
    expand(d) {
        return new BoundingBox(
            VPoint.internal(this.min.X - d, this.min.Y - d),
            VPoint.internal(this.max.X + d, this.max.Y + d)
        );
    }
}

// ============================================================================
// GeometryHelper
// ============================================================================
class GeometryHelper {
    static rotatePoint(point, pivot, angleDeg) {
        const r = angleDeg * Math.PI / 180;
        const c = Math.cos(r), s = Math.sin(r);
        const dx = point.X - pivot.X, dy = point.Y - pivot.Y;
        return VPoint.internal(pivot.X + dx * c - dy * s, pivot.Y + dx * s + dy * c);
    }

    static flipPoint(point, mirrorLine) {
        const dx = mirrorLine.end.X - mirrorLine.start.X;
        const dy = mirrorLine.end.Y - mirrorLine.start.Y;
        const lenSq = dx * dx + dy * dy;
        if (lenSq < EPSILON) return point;
        const t = ((point.X - mirrorLine.start.X) * dx + (point.Y - mirrorLine.start.Y) * dy) / lenSq;
        const px = mirrorLine.start.X + t * dx, py = mirrorLine.start.Y + t * dy;
        return VPoint.internal(2 * px - point.X, 2 * py - point.Y);
    }

    static normalizeAngle(deg) {
        let a = deg % 360;
        if (a < 0) a += 360;
        return a;
    }

    static lineLineIntersection(p1, p2, p3, p4) {
        const d1x = p2.X - p1.X, d1y = p2.Y - p1.Y;
        const d2x = p4.X - p3.X, d2y = p4.Y - p3.Y;
        const denom = d1x * d2y - d1y * d2x;
        if (Math.abs(denom) < EPSILON) return null;
        const t = ((p3.X - p1.X) * d2y - (p3.Y - p1.Y) * d2x) / denom;
        const u = ((p3.X - p1.X) * d1y - (p3.Y - p1.Y) * d1x) / denom;
        if (t >= -EPSILON && t <= 1 + EPSILON && u >= -EPSILON && u <= 1 + EPSILON) {
            return VPoint.internal(p1.X + t * d1x, p1.Y + t * d1y);
        }
        return null;
    }

    static pointToLineDistance(point, lineStart, lineEnd) {
        const dx = lineEnd.X - lineStart.X, dy = lineEnd.Y - lineStart.Y;
        const lenSq = dx * dx + dy * dy;
        if (lenSq < EPSILON) return Math.sqrt((point.X - lineStart.X) ** 2 + (point.Y - lineStart.Y) ** 2);
        let t = Math.max(0, Math.min(1, ((point.X - lineStart.X) * dx + (point.Y - lineStart.Y) * dy) / lenSq));
        const px = lineStart.X + t * dx, py = lineStart.Y + t * dy;
        return Math.sqrt((point.X - px) ** 2 + (point.Y - py) ** 2);
    }

    static circleCircleIntersection(c1, r1, c2, r2) {
        const dx = c2.X - c1.X, dy = c2.Y - c1.Y;
        const d = Math.sqrt(dx * dx + dy * dy);
        if (d > r1 + r2 + EPSILON || d < Math.abs(r1 - r2) - EPSILON || d < EPSILON) return [];
        const a = (r1 * r1 - r2 * r2 + d * d) / (2 * d);
        const h = Math.sqrt(Math.max(0, r1 * r1 - a * a));
        const mx = c1.X + a * dx / d, my = c1.Y + a * dy / d;
        if (h < EPSILON) return [VPoint.internal(mx, my)];
        return [
            VPoint.internal(mx + h * dy / d, my - h * dx / d),
            VPoint.internal(mx - h * dy / d, my + h * dx / d)
        ];
    }
}

// ============================================================================
// Shape Registry & Base Class
// ============================================================================
let _nextId = 1;
let _shapeRegistry = [];

function resetIdCounter() { _nextId = 1; }
function clearRegistry() { _shapeRegistry = []; }
function getRegistry() { return _shapeRegistry; }

class Shape {
    static AutoRegister = true;

    constructor(register = true) {
        this._id = _nextId++;
        this._name = '';
        this._color = 'White';
        this._fillColor = 'Transparent';
        this._lineWeight = 2.0;
        this._lineType = 'Continuous';
        this._lineTypeScale = 1.0;
        this._isVisible = true;
        this._isPlaced = false;
        this._drawFactor = 1.0;
        this._offsetX = 0;
        this._offsetY = 0;
        this._rotationAngle = 0;
        this._rotationPivot = null;
        this._opacity = 1.0;

        // Apply global defaults
        const d = ShapeDefaults;
        if (d.globalColor) this._color = d.globalColor;
        if (d.globalFillColor) this._fillColor = d.globalFillColor;
        if (d.globalLineWeight != null) this._lineWeight = d.globalLineWeight;
        if (d.globalLineType) this._lineType = d.globalLineType;
        if (d.globalLineTypeScale != null) this._lineTypeScale = d.globalLineTypeScale;

        if (register && Shape.AutoRegister) {
            this._isPlaced = true;
            _shapeRegistry.push(this);
        }
    }

    get id() { return this._id; }
    get name() { return this._name; }
    set name(v) { this._name = v; }
    get color() { return this._color; }
    set color(v) { this._color = v; }
    get fillColor() { return this._fillColor; }
    set fillColor(v) { this._fillColor = v; }
    get lineWeight() { return this._lineWeight; }
    set lineWeight(v) { this._lineWeight = v; }
    get lineType() { return this._lineType; }
    set lineType(v) { this._lineType = v; }
    get lineTypeScale() { return this._lineTypeScale; }
    set lineTypeScale(v) { this._lineTypeScale = v; }
    get isVisible() { return this._isVisible; }
    set isVisible(v) { this._isVisible = v; }
    get isPlaced() { return this._isPlaced; }
    get drawFactor() { return this._drawFactor; }
    set drawFactor(v) { this._drawFactor = v; }
    get offsetX() { return this._offsetX; }
    set offsetX(v) { this._offsetX = v; }
    get offsetY() { return this._offsetY; }
    set offsetY(v) { this._offsetY = v; }
    get rotationAngle() { return this._rotationAngle; }
    set rotationAngle(v) { this._rotationAngle = v; }
    get rotationPivot() { return this._rotationPivot; }
    set rotationPivot(v) { this._rotationPivot = v; }
    get opacity() { return this._opacity; }
    set opacity(v) { this._opacity = v; }

    draw() {
        if (!this._isPlaced) {
            this._isPlaced = true;
            _shapeRegistry.push(this);
        }
    }
    remove() {
        this._isPlaced = false;
        const i = _shapeRegistry.indexOf(this);
        if (i >= 0) _shapeRegistry.splice(i, 1);
    }
    show() { this._isVisible = true; }
    hide() { this._isVisible = false; }

    clone() { throw new Error('clone() not implemented'); }
    move(vector) { throw new Error('move() not implemented'); }
    rotate(pivot, angleDegrees) { throw new Error('rotate() not implemented'); }
    flip(mirrorLine) { throw new Error('flip() not implemented'); }
    scale(center, factor) { throw new Error('scale() not implemented'); }
    getBounds() { throw new Error('getBounds() not implemented'); }
    distanceTo(point) { return Infinity; }
    contains(point) { return false; }
}

// ============================================================================
// ShapeDefaults
// ============================================================================
const ShapeDefaults = {
    globalColor: null,
    globalFillColor: null,
    globalLineWeight: null,
    globalLineType: null,
    globalLineTypeScale: null,
    reset() {
        this.globalColor = null;
        this.globalFillColor = null;
        this.globalLineWeight = null;
        this.globalLineType = null;
        this.globalLineTypeScale = null;
    }
};

// ============================================================================
// VPoint
// ============================================================================
class VPoint extends Shape {
    constructor(x, y) {
        super(true);
        this._x = x;
        this._y = y;
    }

    static internal(x, y) {
        const p = new VPoint.__Internal(x, y);
        return p;
    }

    get X() { return this._x; }
    set X(v) { this._x = v; }
    get Y() { return this._y; }
    set Y(v) { this._y = v; }

    asVXYZ() { return new VXYZ(this._x, this._y, 0); }

    add(other) {
        if (other instanceof VXYZ) return VPoint.internal(this._x + other.X, this._y + other.Y);
        return VPoint.internal(this._x + other.X, this._y + other.Y);
    }
    subtract(other) { return VPoint.internal(this._x - other.X, this._y - other.Y); }
    multiplyScalar(s) { return VPoint.internal(this._x * s, this._y * s); }
    divideScalar(s) { return VPoint.internal(this._x / s, this._y / s); }

    distanceTo(other) {
        const dx = this._x - other.X, dy = this._y - other.Y;
        return Math.sqrt(dx * dx + dy * dy);
    }

    clone() {
        const p = new VPoint(this._x, this._y);
        p._color = this._color;
        return p;
    }

    move(vector) {
        this._x += vector.X;
        this._y += vector.Y;
    }

    rotate(pivot, angleDegrees) {
        const r = GeometryHelper.rotatePoint(this, pivot, angleDegrees);
        this._x = r.X;
        this._y = r.Y;
    }

    flip(mirrorLine) {
        const r = GeometryHelper.flipPoint(this, mirrorLine);
        this._x = r.X;
        this._y = r.Y;
    }

    scale(center, factor) {
        this._x = center.X + (this._x - center.X) * factor;
        this._y = center.Y + (this._y - center.Y) * factor;
    }

    getBounds() {
        return new BoundingBox(VPoint.internal(this._x, this._y), VPoint.internal(this._x, this._y));
    }

    toString() { return `(${this._x.toFixed(4)}, ${this._y.toFixed(4)})`; }
}

// Internal unregistered point
VPoint.__Internal = class extends VPoint {
    constructor(x, y) {
        super.__skipRegistration = true;
        // Bypass Shape constructor registration
        Shape.AutoRegister = false;
        try {
            // Call VPoint constructor but it won't register
            Object.defineProperty(this, '_x', { value: x, writable: true });
            Object.defineProperty(this, '_y', { value: y, writable: true });
        } finally {
            Shape.AutoRegister = true;
        }
    }
};

// Fix VPoint.internal to properly create unregistered points
VPoint.internal = function(x, y) {
    const prevAR = Shape.AutoRegister;
    Shape.AutoRegister = false;
    const p = new VPoint(x, y);
    Shape.AutoRegister = prevAR;
    return p;
};

// ============================================================================
// VLine
// ============================================================================
class VLine extends Shape {
    constructor(x1OrStart, y1OrEnd, x2, y2) {
        super(true);
        if (x1OrStart instanceof VPoint || (x1OrStart && typeof x1OrStart.X === 'number' && y1OrEnd && typeof y1OrEnd.X === 'number')) {
            this._start = VPoint.internal(x1OrStart.X, x1OrStart.Y);
            this._end = VPoint.internal(y1OrEnd.X, y1OrEnd.Y);
        } else {
            this._start = VPoint.internal(x1OrStart, y1OrEnd);
            this._end = VPoint.internal(x2, y2);
        }
    }

    static fromPointAngleLength(startPoint, angleInDegrees, length) {
        const rad = angleInDegrees * Math.PI / 180;
        const end = VPoint.internal(startPoint.X + length * Math.cos(rad), startPoint.Y + length * Math.sin(rad));
        return new VLine(startPoint, end);
    }

    get start() { return this._start; }
    set start(v) { this._start = VPoint.internal(v.X, v.Y); }
    get end() { return this._end; }
    set end(v) { this._end = VPoint.internal(v.X, v.Y); }
    get startPoint() { return this._start; }
    get endPoint() { return this._end; }
    get midPoint() { return VPoint.internal((this._start.X + this._end.X) / 2, (this._start.Y + this._end.Y) / 2); }
    get vertices() { return [this._start, this._end]; }

    get direction() {
        const dx = this._end.X - this._start.X, dy = this._end.Y - this._start.Y;
        const len = Math.sqrt(dx * dx + dy * dy);
        return len < EPSILON ? new VXYZ(0, 0, 0) : new VXYZ(dx / len, dy / len, 0);
    }

    getLength() {
        const dx = this._end.X - this._start.X, dy = this._end.Y - this._start.Y;
        return Math.sqrt(dx * dx + dy * dy);
    }

    evaluate(t) {
        return VPoint.internal(
            this._start.X + t * (this._end.X - this._start.X),
            this._start.Y + t * (this._end.Y - this._start.Y)
        );
    }
    pointAtParameter(t) { return this.evaluate(t); }

    parameterAtPoint(p) {
        const dx = this._end.X - this._start.X, dy = this._end.Y - this._start.Y;
        const lenSq = dx * dx + dy * dy;
        if (lenSq < EPSILON) return 0;
        return Math.max(0, Math.min(1, ((p.X - this._start.X) * dx + (p.Y - this._start.Y) * dy) / lenSq));
    }

    divide(n) {
        const pts = [];
        for (let i = 0; i <= n; i++) pts.push(this.evaluate(i / n));
        return pts;
    }

    measure(segmentLength) {
        const len = this.getLength();
        const pts = [VPoint.internal(this._start.X, this._start.Y)];
        let d = segmentLength;
        while (d < len - EPSILON) {
            pts.push(this.evaluate(d / len));
            d += segmentLength;
        }
        pts.push(VPoint.internal(this._end.X, this._end.Y));
        return pts;
    }

    project(point) {
        const t = this.parameterAtPoint(point);
        return this.evaluate(t);
    }

    pointAtSegmentLength(d) {
        const len = this.getLength();
        return len < EPSILON ? VPoint.internal(this._start.X, this._start.Y) : this.evaluate(d / len);
    }

    offset(distance) {
        const dir = this.direction;
        const nx = -dir.Y * distance, ny = dir.X * distance;
        return new VLine(
            VPoint.internal(this._start.X + nx, this._start.Y + ny),
            VPoint.internal(this._end.X + nx, this._end.Y + ny)
        );
    }

    normalAtPoint(_p) {
        const dir = this.direction;
        return new VXYZ(-dir.Y, dir.X, 0);
    }

    clone() {
        const l = new VLine(this._start, this._end);
        l._color = this._color; l._lineWeight = this._lineWeight;
        l._lineType = this._lineType; l._fillColor = this._fillColor;
        return l;
    }

    move(vector) {
        this._start = VPoint.internal(this._start.X + vector.X, this._start.Y + vector.Y);
        this._end = VPoint.internal(this._end.X + vector.X, this._end.Y + vector.Y);
    }

    rotate(pivot, angle) {
        this._start = GeometryHelper.rotatePoint(this._start, pivot, angle);
        this._end = GeometryHelper.rotatePoint(this._end, pivot, angle);
    }

    flip(mirrorLine) {
        this._start = GeometryHelper.flipPoint(this._start, mirrorLine);
        this._end = GeometryHelper.flipPoint(this._end, mirrorLine);
    }

    scale(center, factor) {
        this._start = VPoint.internal(center.X + (this._start.X - center.X) * factor, center.Y + (this._start.Y - center.Y) * factor);
        this._end = VPoint.internal(center.X + (this._end.X - center.X) * factor, center.Y + (this._end.Y - center.Y) * factor);
    }

    getBounds() {
        return new BoundingBox(
            VPoint.internal(Math.min(this._start.X, this._end.X), Math.min(this._start.Y, this._end.Y)),
            VPoint.internal(Math.max(this._start.X, this._end.X), Math.max(this._start.Y, this._end.Y))
        );
    }

    distanceTo(point) {
        return GeometryHelper.pointToLineDistance(point, this._start, this._end);
    }
}

// ============================================================================
// VCircle
// ============================================================================
class VCircle extends Shape {
    constructor(centerOrX, radiusOrY, radius) {
        super(true);
        if (centerOrX instanceof VPoint || (centerOrX && typeof centerOrX.X === 'number' && radius === undefined && typeof radiusOrY === 'number')) {
            if (typeof centerOrX.X === 'number' && typeof centerOrX.Y === 'number') {
                this._center = VPoint.internal(centerOrX.X, centerOrX.Y);
                this._radius = radiusOrY;
            }
        } else {
            this._center = VPoint.internal(centerOrX, radiusOrY);
            this._radius = radius;
        }
    }

    static fromThreePoints(p1, p2, p3) {
        const ax = p1.X, ay = p1.Y, bx = p2.X, by = p2.Y, cx = p3.X, cy = p3.Y;
        const D = 2 * (ax * (by - cy) + bx * (cy - ay) + cx * (ay - by));
        if (Math.abs(D) < EPSILON) throw new Error("Points are collinear");
        const ux = ((ax * ax + ay * ay) * (by - cy) + (bx * bx + by * by) * (cy - ay) + (cx * cx + cy * cy) * (ay - by)) / D;
        const uy = ((ax * ax + ay * ay) * (cx - bx) + (bx * bx + by * by) * (ax - cx) + (cx * cx + cy * cy) * (bx - ax)) / D;
        const center = VPoint.internal(ux, uy);
        const r = Math.sqrt((ax - ux) ** 2 + (ay - uy) ** 2);
        return new VCircle(center, r);
    }

    static fromCenterDiameter(center, diameter) { return new VCircle(center, diameter / 2); }
    static fromTwoPoints(p1, p2) {
        const cx = (p1.X + p2.X) / 2, cy = (p1.Y + p2.Y) / 2;
        const r = Math.sqrt((p2.X - p1.X) ** 2 + (p2.Y - p1.Y) ** 2) / 2;
        return new VCircle(VPoint.internal(cx, cy), r);
    }

    get center() { return this._center; }
    set center(v) { this._center = VPoint.internal(v.X, v.Y); }
    get radius() { return this._radius; }
    set radius(v) { this._radius = v; }
    get area() { return Math.PI * this._radius * this._radius; }
    get circumference() { return 2 * Math.PI * this._radius; }
    get startPoint() { return VPoint.internal(this._center.X + this._radius, this._center.Y); }
    get endPoint() { return this.startPoint; }
    get vertices() { return [this._center]; }

    getLength() { return this.circumference; }

    divide(n) {
        const pts = [];
        for (let i = 0; i < n; i++) {
            const a = (2 * Math.PI * i) / n;
            pts.push(VPoint.internal(this._center.X + this._radius * Math.cos(a), this._center.Y + this._radius * Math.sin(a)));
        }
        return pts;
    }

    measure(segLen) {
        const circ = this.circumference;
        const pts = [];
        let d = 0;
        while (d < circ - EPSILON) {
            const a = (d / circ) * 2 * Math.PI;
            pts.push(VPoint.internal(this._center.X + this._radius * Math.cos(a), this._center.Y + this._radius * Math.sin(a)));
            d += segLen;
        }
        return pts;
    }

    project(point) {
        const dx = point.X - this._center.X, dy = point.Y - this._center.Y;
        const d = Math.sqrt(dx * dx + dy * dy);
        if (d < EPSILON) return VPoint.internal(this._center.X + this._radius, this._center.Y);
        return VPoint.internal(this._center.X + this._radius * dx / d, this._center.Y + this._radius * dy / d);
    }

    pointAtAngle(angleRadians) {
        return VPoint.internal(this._center.X + this._radius * Math.cos(angleRadians), this._center.Y + this._radius * Math.sin(angleRadians));
    }

    pointAtParameter(t) { return this.pointAtAngle(t * 2 * Math.PI); }

    offset(distance) { return new VCircle(this._center, this._radius + distance); }

    contains(point) {
        const dx = point.X - this._center.X, dy = point.Y - this._center.Y;
        return dx * dx + dy * dy <= (this._radius + EPSILON) * (this._radius + EPSILON);
    }

    clone() {
        const c = new VCircle(this._center, this._radius);
        c._color = this._color; c._fillColor = this._fillColor; c._lineWeight = this._lineWeight;
        return c;
    }

    move(vector) { this._center = VPoint.internal(this._center.X + vector.X, this._center.Y + vector.Y); }

    rotate(pivot, angle) { this._center = GeometryHelper.rotatePoint(this._center, pivot, angle); }

    flip(mirrorLine) { this._center = GeometryHelper.flipPoint(this._center, mirrorLine); }

    scale(center, factor) {
        this._center = VPoint.internal(center.X + (this._center.X - center.X) * factor, center.Y + (this._center.Y - center.Y) * factor);
        this._radius *= Math.abs(factor);
    }

    getBounds() {
        return new BoundingBox(
            VPoint.internal(this._center.X - this._radius, this._center.Y - this._radius),
            VPoint.internal(this._center.X + this._radius, this._center.Y + this._radius)
        );
    }

    distanceTo(point) {
        const dx = point.X - this._center.X, dy = point.Y - this._center.Y;
        return Math.abs(Math.sqrt(dx * dx + dy * dy) - this._radius);
    }
}

// ============================================================================
// VArc
// ============================================================================
class VArc extends Shape {
    constructor(centerOrX, radiusOrY, startAngleOrRadius, endAngleOrStart, endAngle) {
        super(true);
        if (centerOrX instanceof VPoint || (centerOrX && typeof centerOrX.X === 'number')) {
            this._center = VPoint.internal(centerOrX.X, centerOrX.Y);
            this._radius = radiusOrY;
            this._startAngle = startAngleOrRadius;
            this._endAngle = endAngleOrStart;
        } else {
            this._center = VPoint.internal(centerOrX, radiusOrY);
            this._radius = startAngleOrRadius;
            this._startAngle = endAngleOrStart;
            this._endAngle = endAngle;
        }
    }

    static fromThreePoints(start, mid, end) {
        const circle = VCircle.fromThreePoints(start, mid, end);
        const cx = circle.center.X, cy = circle.center.Y;
        const sa = Math.atan2(start.Y - cy, start.X - cx) * 180 / Math.PI;
        const ma = Math.atan2(mid.Y - cy, mid.X - cx) * 180 / Math.PI;
        const ea = Math.atan2(end.Y - cy, end.X - cx) * 180 / Math.PI;

        let startAngle = GeometryHelper.normalizeAngle(sa);
        let midAngle = GeometryHelper.normalizeAngle(ma);
        let endAngle = GeometryHelper.normalizeAngle(ea);

        // Determine if we go CCW from start through mid to end
        const ccwSM = GeometryHelper.normalizeAngle(midAngle - startAngle);
        const ccwSE = GeometryHelper.normalizeAngle(endAngle - startAngle);

        if (ccwSM <= ccwSE) {
            return new VArc(circle.center, circle.radius, startAngle, endAngle);
        } else {
            return new VArc(circle.center, circle.radius, endAngle, startAngle);
        }
    }

    static fromCenterStartAngle(center, start, sweepAngleDegrees) {
        const r = Math.sqrt((start.X - center.X) ** 2 + (start.Y - center.Y) ** 2);
        const sa = Math.atan2(start.Y - center.Y, start.X - center.X) * 180 / Math.PI;
        return new VArc(center, r, GeometryHelper.normalizeAngle(sa), GeometryHelper.normalizeAngle(sa + sweepAngleDegrees));
    }

    get center() { return this._center; }
    get radius() { return this._radius; }
    get startAngle() { return this._startAngle; }
    set startAngle(v) { this._startAngle = v; }
    get endAngle() { return this._endAngle; }
    set endAngle(v) { this._endAngle = v; }

    get sweepAngle() {
        let s = this._endAngle - this._startAngle;
        if (s <= 0) s += 360;
        return s;
    }

    get startPoint() {
        const a = this._startAngle * Math.PI / 180;
        return VPoint.internal(this._center.X + this._radius * Math.cos(a), this._center.Y + this._radius * Math.sin(a));
    }

    get endPoint() {
        const a = this._endAngle * Math.PI / 180;
        return VPoint.internal(this._center.X + this._radius * Math.cos(a), this._center.Y + this._radius * Math.sin(a));
    }

    get midPoint() { return this.pointAtParameter(0.5); }
    get vertices() { return [this._center, this.startPoint, this.endPoint]; }

    getLength() { return this._radius * this.sweepAngle * Math.PI / 180; }

    pointAtParameter(t) {
        const a = (this._startAngle + t * this.sweepAngle) * Math.PI / 180;
        return VPoint.internal(this._center.X + this._radius * Math.cos(a), this._center.Y + this._radius * Math.sin(a));
    }

    divide(n) {
        const pts = [];
        for (let i = 0; i <= n; i++) pts.push(this.pointAtParameter(i / n));
        return pts;
    }

    measure(segLen) {
        const len = this.getLength();
        const pts = [];
        let d = 0;
        while (d < len - EPSILON) {
            pts.push(this.pointAtParameter(d / len));
            d += segLen;
        }
        pts.push(this.endPoint);
        return pts;
    }

    project(point) {
        const dx = point.X - this._center.X, dy = point.Y - this._center.Y;
        let a = Math.atan2(dy, dx) * 180 / Math.PI;
        a = GeometryHelper.normalizeAngle(a);
        const sa = GeometryHelper.normalizeAngle(this._startAngle);
        const rel = GeometryHelper.normalizeAngle(a - sa);
        if (rel <= this.sweepAngle) {
            const rad = a * Math.PI / 180;
            return VPoint.internal(this._center.X + this._radius * Math.cos(rad), this._center.Y + this._radius * Math.sin(rad));
        }
        const ds = this.startPoint.distanceTo(point);
        const de = this.endPoint.distanceTo(point);
        return ds < de ? this.startPoint : this.endPoint;
    }

    offset(distance) { return new VArc(this._center, this._radius + distance, this._startAngle, this._endAngle); }

    clone() {
        const a = new VArc(this._center, this._radius, this._startAngle, this._endAngle);
        a._color = this._color; a._lineWeight = this._lineWeight;
        return a;
    }

    move(vector) { this._center = VPoint.internal(this._center.X + vector.X, this._center.Y + vector.Y); }
    rotate(pivot, angle) {
        this._center = GeometryHelper.rotatePoint(this._center, pivot, angle);
        this._startAngle = GeometryHelper.normalizeAngle(this._startAngle + angle);
        this._endAngle = GeometryHelper.normalizeAngle(this._endAngle + angle);
    }
    flip(mirrorLine) {
        this._center = GeometryHelper.flipPoint(this._center, mirrorLine);
        const sp = GeometryHelper.flipPoint(this.startPoint, mirrorLine);
        const ep = GeometryHelper.flipPoint(this.endPoint, mirrorLine);
        this._startAngle = Math.atan2(ep.Y - this._center.Y, ep.X - this._center.X) * 180 / Math.PI;
        this._endAngle = Math.atan2(sp.Y - this._center.Y, sp.X - this._center.X) * 180 / Math.PI;
    }
    scale(center, factor) {
        this._center = VPoint.internal(center.X + (this._center.X - center.X) * factor, center.Y + (this._center.Y - center.Y) * factor);
        this._radius *= Math.abs(factor);
    }

    getBounds() {
        const pts = this.divide(36);
        let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;
        for (const p of pts) {
            if (p.X < minX) minX = p.X; if (p.Y < minY) minY = p.Y;
            if (p.X > maxX) maxX = p.X; if (p.Y > maxY) maxY = p.Y;
        }
        return new BoundingBox(VPoint.internal(minX, minY), VPoint.internal(maxX, maxY));
    }
}

// ============================================================================
// VRectangle
// ============================================================================
class VRectangle extends Shape {
    constructor(cornerOrX, widthOrY, heightOrWidth, height) {
        super(true);
        if (cornerOrX instanceof VPoint || (cornerOrX && typeof cornerOrX.X === 'number' && height === undefined)) {
            if (heightOrWidth !== undefined) {
                // (corner, width, height)
                this._corner = VPoint.internal(cornerOrX.X, cornerOrX.Y);
                this._width = widthOrY;
                this._height = heightOrWidth;
            } else {
                // (bottomLeft, topRight)
                this._corner = VPoint.internal(
                    Math.min(cornerOrX.X, widthOrY.X),
                    Math.min(cornerOrX.Y, widthOrY.Y)
                );
                this._width = Math.abs(widthOrY.X - cornerOrX.X);
                this._height = Math.abs(widthOrY.Y - cornerOrX.Y);
            }
        } else {
            // (x, y, width, height)
            this._corner = VPoint.internal(cornerOrX, widthOrY);
            this._width = heightOrWidth;
            this._height = height;
        }
        this._rotAngle = 0;
    }

    get corner() { return this._corner; }
    get width() { return this._width; }
    set width(v) { this._width = v; }
    get height() { return this._height; }
    set height(v) { this._height = v; }
    get area() { return this._width * this._height; }

    get points() {
        const c = this._corner;
        const pts = [
            VPoint.internal(c.X, c.Y),
            VPoint.internal(c.X + this._width, c.Y),
            VPoint.internal(c.X + this._width, c.Y + this._height),
            VPoint.internal(c.X, c.Y + this._height)
        ];
        if (Math.abs(this._rotAngle) > EPSILON) {
            const pivot = VPoint.internal(c.X + this._width / 2, c.Y + this._height / 2);
            return pts.map(p => GeometryHelper.rotatePoint(p, pivot, this._rotAngle));
        }
        return pts;
    }

    get center() {
        const pts = this.points;
        const cx = (pts[0].X + pts[2].X) / 2, cy = (pts[0].Y + pts[2].Y) / 2;
        return VPoint.internal(cx, cy);
    }

    contains(point) {
        // Transform point to local space
        let px = point.X, py = point.Y;
        if (Math.abs(this._rotAngle) > EPSILON) {
            const center = VPoint.internal(this._corner.X + this._width / 2, this._corner.Y + this._height / 2);
            const rp = GeometryHelper.rotatePoint(point, center, -this._rotAngle);
            px = rp.X; py = rp.Y;
        }
        return px >= this._corner.X - EPSILON && px <= this._corner.X + this._width + EPSILON &&
               py >= this._corner.Y - EPSILON && py <= this._corner.Y + this._height + EPSILON;
    }

    clone() {
        const r = new VRectangle(this._corner, this._width, this._height);
        r._color = this._color; r._fillColor = this._fillColor; r._lineWeight = this._lineWeight;
        r._rotAngle = this._rotAngle;
        return r;
    }

    move(vector) { this._corner = VPoint.internal(this._corner.X + vector.X, this._corner.Y + vector.Y); }

    rotate(pivot, angle) {
        this._corner = GeometryHelper.rotatePoint(this._corner, pivot, angle);
        this._rotAngle += angle;
    }

    flip(mirrorLine) {
        const pts = this.points.map(p => GeometryHelper.flipPoint(p, mirrorLine));
        this._corner = VPoint.internal(
            Math.min(...pts.map(p => p.X)),
            Math.min(...pts.map(p => p.Y))
        );
    }

    scale(center, factor) {
        this._corner = VPoint.internal(
            center.X + (this._corner.X - center.X) * factor,
            center.Y + (this._corner.Y - center.Y) * factor
        );
        this._width *= Math.abs(factor);
        this._height *= Math.abs(factor);
    }

    getBounds() {
        const pts = this.points;
        let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;
        for (const p of pts) {
            if (p.X < minX) minX = p.X; if (p.Y < minY) minY = p.Y;
            if (p.X > maxX) maxX = p.X; if (p.Y > maxY) maxY = p.Y;
        }
        return new BoundingBox(VPoint.internal(minX, minY), VPoint.internal(maxX, maxY));
    }
}

// ============================================================================
// VEllipse
// ============================================================================
class VEllipse extends Shape {
    constructor(centerOrX, radiusXOrY, radiusYOrRX, startAngleOrRY, endAngle) {
        super(true);
        if (centerOrX instanceof VPoint || (centerOrX && typeof centerOrX.X === 'number')) {
            this._center = VPoint.internal(centerOrX.X, centerOrX.Y);
            this._radiusX = radiusXOrY;
            this._radiusY = radiusYOrRX;
            this._startAngle = startAngleOrRY || 0;
            this._endAngle = endAngle || 360;
        } else {
            this._center = VPoint.internal(centerOrX, radiusXOrY);
            this._radiusX = radiusYOrRX;
            this._radiusY = startAngleOrRY;
            this._startAngle = 0;
            this._endAngle = 360;
        }
    }

    get center() { return this._center; }
    get radiusX() { return this._radiusX; }
    set radiusX(v) { this._radiusX = v; }
    get radiusY() { return this._radiusY; }
    set radiusY(v) { this._radiusY = v; }
    get startAngle() { return this._startAngle; }
    get endAngle() { return this._endAngle; }
    get area() { return Math.PI * this._radiusX * this._radiusY; }
    get circumference() {
        // Ramanujan's approximation
        const a = this._radiusX, b = this._radiusY;
        const h = ((a - b) * (a - b)) / ((a + b) * (a + b));
        return Math.PI * (a + b) * (1 + 3 * h / (10 + Math.sqrt(4 - 3 * h)));
    }

    pointAtParameter(t) {
        const sweep = this._endAngle - this._startAngle;
        const a = (this._startAngle + t * sweep) * Math.PI / 180;
        return VPoint.internal(this._center.X + this._radiusX * Math.cos(a), this._center.Y + this._radiusY * Math.sin(a));
    }

    getLength() { return this.circumference * (this._endAngle - this._startAngle) / 360; }

    divide(n) {
        const pts = [];
        for (let i = 0; i <= n; i++) pts.push(this.pointAtParameter(i / n));
        return pts;
    }

    project(point) {
        // Approximate: find closest among sampled points
        let best = null, bestDist = Infinity;
        for (let i = 0; i <= 360; i++) {
            const p = this.pointAtParameter(i / 360);
            const d = Math.sqrt((p.X - point.X) ** 2 + (p.Y - point.Y) ** 2);
            if (d < bestDist) { bestDist = d; best = p; }
        }
        return best;
    }

    contains(point) {
        const dx = (point.X - this._center.X) / this._radiusX;
        const dy = (point.Y - this._center.Y) / this._radiusY;
        return dx * dx + dy * dy <= 1 + EPSILON;
    }

    clone() {
        const e = new VEllipse(this._center, this._radiusX, this._radiusY, this._startAngle, this._endAngle);
        e._color = this._color; e._fillColor = this._fillColor; e._lineWeight = this._lineWeight;
        return e;
    }

    move(vector) { this._center = VPoint.internal(this._center.X + vector.X, this._center.Y + vector.Y); }
    rotate(pivot, angle) { this._center = GeometryHelper.rotatePoint(this._center, pivot, angle); }
    flip(mirrorLine) { this._center = GeometryHelper.flipPoint(this._center, mirrorLine); }
    scale(center, factor) {
        this._center = VPoint.internal(center.X + (this._center.X - center.X) * factor, center.Y + (this._center.Y - center.Y) * factor);
        this._radiusX *= Math.abs(factor);
        this._radiusY *= Math.abs(factor);
    }

    getBounds() {
        return new BoundingBox(
            VPoint.internal(this._center.X - this._radiusX, this._center.Y - this._radiusY),
            VPoint.internal(this._center.X + this._radiusX, this._center.Y + this._radiusY)
        );
    }
}

// ============================================================================
// VPolygon
// ============================================================================
class VPolygon extends Shape {
    constructor(...args) {
        super(true);
        if (args.length === 1 && Array.isArray(args[0])) {
            this._points = args[0].map(p => VPoint.internal(p.X, p.Y));
        } else {
            this._points = args.map(p => VPoint.internal(p.X, p.Y));
        }
    }

    get points() { return this._points; }
    set points(v) { this._points = v.map(p => VPoint.internal(p.X, p.Y)); }
    get vertices() { return this._points; }
    get startPoint() { return this._points[0]; }
    get endPoint() { return this._points[0]; }

    get area() { return Math.abs(this.signedArea); }
    get signedArea() {
        let a = 0;
        const n = this._points.length;
        for (let i = 0; i < n; i++) {
            const j = (i + 1) % n;
            a += this._points[i].X * this._points[j].Y;
            a -= this._points[j].X * this._points[i].Y;
        }
        return a / 2;
    }

    get centroid() {
        let cx = 0, cy = 0;
        for (const p of this._points) { cx += p.X; cy += p.Y; }
        const n = this._points.length;
        return VPoint.internal(cx / n, cy / n);
    }

    getLength() {
        let len = 0;
        for (let i = 0; i < this._points.length; i++) {
            const j = (i + 1) % this._points.length;
            len += this._points[i].distanceTo(this._points[j]);
        }
        return len;
    }

    divide(n) {
        const totalLen = this.getLength();
        const segLen = totalLen / n;
        return this.measure(segLen);
    }

    measure(segLen) {
        const pts = [VPoint.internal(this._points[0].X, this._points[0].Y)];
        let accumulated = 0;
        let target = segLen;

        for (let i = 0; i < this._points.length; i++) {
            const j = (i + 1) % this._points.length;
            const edgeLen = this._points[i].distanceTo(this._points[j]);
            while (accumulated + edgeLen >= target - EPSILON && target < this.getLength() - EPSILON) {
                const rem = target - accumulated;
                const t = rem / edgeLen;
                pts.push(VPoint.internal(
                    this._points[i].X + t * (this._points[j].X - this._points[i].X),
                    this._points[i].Y + t * (this._points[j].Y - this._points[i].Y)
                ));
                target += segLen;
            }
            accumulated += edgeLen;
        }
        return pts;
    }

    project(point) {
        let best = null, bestDist = Infinity;
        for (let i = 0; i < this._points.length; i++) {
            const j = (i + 1) % this._points.length;
            const d = GeometryHelper.pointToLineDistance(point, this._points[i], this._points[j]);
            if (d < bestDist) {
                bestDist = d;
                // Project onto the edge
                const dx = this._points[j].X - this._points[i].X;
                const dy = this._points[j].Y - this._points[i].Y;
                const lenSq = dx * dx + dy * dy;
                let t = 0;
                if (lenSq > EPSILON) {
                    t = Math.max(0, Math.min(1, ((point.X - this._points[i].X) * dx + (point.Y - this._points[i].Y) * dy) / lenSq));
                }
                best = VPoint.internal(this._points[i].X + t * dx, this._points[i].Y + t * dy);
            }
        }
        return best;
    }

    contains(point) {
        // Ray casting
        let inside = false;
        const n = this._points.length;
        for (let i = 0, j = n - 1; i < n; j = i++) {
            const xi = this._points[i].X, yi = this._points[i].Y;
            const xj = this._points[j].X, yj = this._points[j].Y;
            if ((yi > point.Y) !== (yj > point.Y) &&
                point.X < (xj - xi) * (point.Y - yi) / (yj - yi) + xi) {
                inside = !inside;
            }
        }
        return inside;
    }

    clone() {
        const p = new VPolygon(this._points);
        p._color = this._color; p._fillColor = this._fillColor; p._lineWeight = this._lineWeight;
        return p;
    }

    move(vector) {
        this._points = this._points.map(p => VPoint.internal(p.X + vector.X, p.Y + vector.Y));
    }
    rotate(pivot, angle) {
        this._points = this._points.map(p => GeometryHelper.rotatePoint(p, pivot, angle));
    }
    flip(mirrorLine) {
        this._points = this._points.map(p => GeometryHelper.flipPoint(p, mirrorLine));
    }
    scale(center, factor) {
        this._points = this._points.map(p => VPoint.internal(
            center.X + (p.X - center.X) * factor,
            center.Y + (p.Y - center.Y) * factor
        ));
    }

    getBounds() {
        let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;
        for (const p of this._points) {
            if (p.X < minX) minX = p.X; if (p.Y < minY) minY = p.Y;
            if (p.X > maxX) maxX = p.X; if (p.Y > maxY) maxY = p.Y;
        }
        return new BoundingBox(VPoint.internal(minX, minY), VPoint.internal(maxX, maxY));
    }
}

// ============================================================================
// VPolyline
// ============================================================================
class VPolyline extends Shape {
    constructor(...args) {
        super(true);
        if (args.length === 1 && Array.isArray(args[0])) {
            this._points = args[0].map(p => VPoint.internal(p.X, p.Y));
        } else {
            this._points = args.map(p => VPoint.internal(p.X, p.Y));
        }
    }

    get points() { return this._points; }
    get vertices() { return this._points; }
    get startPoint() { return this._points[0]; }
    get endPoint() { return this._points[this._points.length - 1]; }

    addPoint(xOrPoint, y) {
        if (typeof xOrPoint === 'number') {
            this._points.push(VPoint.internal(xOrPoint, y));
        } else {
            this._points.push(VPoint.internal(xOrPoint.X, xOrPoint.Y));
        }
    }

    getLength() {
        let len = 0;
        for (let i = 0; i < this._points.length - 1; i++) {
            len += this._points[i].distanceTo(this._points[i + 1]);
        }
        return len;
    }

    divide(n) {
        const totalLen = this.getLength();
        const segLen = totalLen / n;
        return this.measure(segLen);
    }

    measure(segLen) {
        const pts = [VPoint.internal(this._points[0].X, this._points[0].Y)];
        let accumulated = 0, target = segLen;

        for (let i = 0; i < this._points.length - 1; i++) {
            const edgeLen = this._points[i].distanceTo(this._points[i + 1]);
            while (accumulated + edgeLen >= target - EPSILON && target < this.getLength() - EPSILON) {
                const rem = target - accumulated;
                const t = rem / edgeLen;
                pts.push(VPoint.internal(
                    this._points[i].X + t * (this._points[i + 1].X - this._points[i].X),
                    this._points[i].Y + t * (this._points[i + 1].Y - this._points[i].Y)
                ));
                target += segLen;
            }
            accumulated += edgeLen;
        }
        pts.push(VPoint.internal(this.endPoint.X, this.endPoint.Y));
        return pts;
    }

    project(point) {
        let best = null, bestDist = Infinity;
        for (let i = 0; i < this._points.length - 1; i++) {
            const d = GeometryHelper.pointToLineDistance(point, this._points[i], this._points[i + 1]);
            if (d < bestDist) {
                bestDist = d;
                const dx = this._points[i + 1].X - this._points[i].X;
                const dy = this._points[i + 1].Y - this._points[i].Y;
                const lenSq = dx * dx + dy * dy;
                let t = 0;
                if (lenSq > EPSILON) {
                    t = Math.max(0, Math.min(1, ((point.X - this._points[i].X) * dx + (point.Y - this._points[i].Y) * dy) / lenSq));
                }
                best = VPoint.internal(this._points[i].X + t * dx, this._points[i].Y + t * dy);
            }
        }
        return best;
    }

    pointAtSegmentLength(d) {
        let accumulated = 0;
        for (let i = 0; i < this._points.length - 1; i++) {
            const edgeLen = this._points[i].distanceTo(this._points[i + 1]);
            if (accumulated + edgeLen >= d - EPSILON) {
                const t = (d - accumulated) / edgeLen;
                return VPoint.internal(
                    this._points[i].X + t * (this._points[i + 1].X - this._points[i].X),
                    this._points[i].Y + t * (this._points[i + 1].Y - this._points[i].Y)
                );
            }
            accumulated += edgeLen;
        }
        return VPoint.internal(this.endPoint.X, this.endPoint.Y);
    }

    offset(distance) {
        if (this._points.length < 2) return this.clone();
        const offsetPoints = [];
        for (let i = 0; i < this._points.length; i++) {
            if (i === 0) {
                const dx = this._points[1].X - this._points[0].X;
                const dy = this._points[1].Y - this._points[0].Y;
                const len = Math.sqrt(dx * dx + dy * dy);
                if (len > EPSILON) {
                    offsetPoints.push(VPoint.internal(this._points[0].X - dy / len * distance, this._points[0].Y + dx / len * distance));
                }
            } else if (i === this._points.length - 1) {
                const dx = this._points[i].X - this._points[i - 1].X;
                const dy = this._points[i].Y - this._points[i - 1].Y;
                const len = Math.sqrt(dx * dx + dy * dy);
                if (len > EPSILON) {
                    offsetPoints.push(VPoint.internal(this._points[i].X - dy / len * distance, this._points[i].Y + dx / len * distance));
                }
            } else {
                const dx1 = this._points[i].X - this._points[i - 1].X;
                const dy1 = this._points[i].Y - this._points[i - 1].Y;
                const dx2 = this._points[i + 1].X - this._points[i].X;
                const dy2 = this._points[i + 1].Y - this._points[i].Y;
                const len1 = Math.sqrt(dx1 * dx1 + dy1 * dy1);
                const len2 = Math.sqrt(dx2 * dx2 + dy2 * dy2);
                if (len1 > EPSILON && len2 > EPSILON) {
                    const nx = (-dy1 / len1 - dy2 / len2) / 2;
                    const ny = (dx1 / len1 + dx2 / len2) / 2;
                    const nLen = Math.sqrt(nx * nx + ny * ny);
                    if (nLen > EPSILON) {
                        offsetPoints.push(VPoint.internal(
                            this._points[i].X + nx / nLen * distance,
                            this._points[i].Y + ny / nLen * distance
                        ));
                    }
                }
            }
        }
        return new VPolyline(offsetPoints);
    }

    clone() {
        const p = new VPolyline(this._points);
        p._color = this._color; p._fillColor = this._fillColor; p._lineWeight = this._lineWeight;
        return p;
    }

    move(vector) {
        this._points = this._points.map(p => VPoint.internal(p.X + vector.X, p.Y + vector.Y));
    }
    rotate(pivot, angle) {
        this._points = this._points.map(p => GeometryHelper.rotatePoint(p, pivot, angle));
    }
    flip(mirrorLine) {
        this._points = this._points.map(p => GeometryHelper.flipPoint(p, mirrorLine));
    }
    scale(center, factor) {
        this._points = this._points.map(p => VPoint.internal(
            center.X + (p.X - center.X) * factor,
            center.Y + (p.Y - center.Y) * factor
        ));
    }

    getBounds() {
        let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;
        for (const p of this._points) {
            if (p.X < minX) minX = p.X; if (p.Y < minY) minY = p.Y;
            if (p.X > maxX) maxX = p.X; if (p.Y > maxY) maxY = p.Y;
        }
        return new BoundingBox(VPoint.internal(minX, minY), VPoint.internal(maxX, maxY));
    }
}

// ============================================================================
// VSpline - Catmull-Rom Spline
// ============================================================================
class VSpline extends Shape {
    constructor(...args) {
        super(true);
        if (args.length === 1 && Array.isArray(args[0])) {
            this._controlPoints = args[0].map(p => VPoint.internal(p.X, p.Y));
        } else {
            this._controlPoints = args.map(p => VPoint.internal(p.X, p.Y));
        }
        this._segmentsPerSpan = 16;
        this._tension = 0.5;
    }

    get controlPoints() { return this._controlPoints; }
    get segmentsPerSpan() { return this._segmentsPerSpan; }
    set segmentsPerSpan(v) { this._segmentsPerSpan = v; }
    get tension() { return this._tension; }
    set tension(v) { this._tension = v; }
    get startPoint() { return this._controlPoints[0]; }
    get endPoint() { return this._controlPoints[this._controlPoints.length - 1]; }
    get vertices() { return this._controlPoints; }

    _catmullRom(p0, p1, p2, p3, t) {
        const t2 = t * t, t3 = t2 * t;
        const tau = this._tension;
        const x = tau * (2 * p1.X + (-p0.X + p2.X) * t +
            (2 * p0.X - 5 * p1.X + 4 * p2.X - p3.X) * t2 +
            (-p0.X + 3 * p1.X - 3 * p2.X + p3.X) * t3);
        const y = tau * (2 * p1.Y + (-p0.Y + p2.Y) * t +
            (2 * p0.Y - 5 * p1.Y + 4 * p2.Y - p3.Y) * t2 +
            (-p0.Y + 3 * p1.Y - 3 * p2.Y + p3.Y) * t3);
        return VPoint.internal(x, y);
    }

    getRenderPoints() {
        const pts = this._controlPoints;
        if (pts.length < 2) return [...pts];
        const result = [];
        for (let i = 0; i < pts.length - 1; i++) {
            const p0 = pts[Math.max(0, i - 1)];
            const p1 = pts[i];
            const p2 = pts[Math.min(pts.length - 1, i + 1)];
            const p3 = pts[Math.min(pts.length - 1, i + 2)];
            for (let j = 0; j < this._segmentsPerSpan; j++) {
                result.push(this._catmullRom(p0, p1, p2, p3, j / this._segmentsPerSpan));
            }
        }
        result.push(VPoint.internal(pts[pts.length - 1].X, pts[pts.length - 1].Y));
        return result;
    }

    evaluate(t) {
        const renderPts = this.getRenderPoints();
        const idx = Math.min(Math.floor(t * (renderPts.length - 1)), renderPts.length - 2);
        const localT = t * (renderPts.length - 1) - idx;
        const p1 = renderPts[idx], p2 = renderPts[idx + 1];
        return VPoint.internal(p1.X + localT * (p2.X - p1.X), p1.Y + localT * (p2.Y - p1.Y));
    }

    getLength() {
        const pts = this.getRenderPoints();
        let len = 0;
        for (let i = 0; i < pts.length - 1; i++) len += pts[i].distanceTo(pts[i + 1]);
        return len;
    }

    divide(n) {
        const pts = [];
        for (let i = 0; i <= n; i++) pts.push(this.evaluate(i / n));
        return pts;
    }

    clone() {
        const s = new VSpline(this._controlPoints);
        s._color = this._color; s._lineWeight = this._lineWeight;
        s._segmentsPerSpan = this._segmentsPerSpan; s._tension = this._tension;
        return s;
    }

    move(vector) {
        this._controlPoints = this._controlPoints.map(p => VPoint.internal(p.X + vector.X, p.Y + vector.Y));
    }
    rotate(pivot, angle) {
        this._controlPoints = this._controlPoints.map(p => GeometryHelper.rotatePoint(p, pivot, angle));
    }
    flip(mirrorLine) {
        this._controlPoints = this._controlPoints.map(p => GeometryHelper.flipPoint(p, mirrorLine));
    }
    scale(center, factor) {
        this._controlPoints = this._controlPoints.map(p => VPoint.internal(
            center.X + (p.X - center.X) * factor,
            center.Y + (p.Y - center.Y) * factor
        ));
    }

    getBounds() {
        const pts = this.getRenderPoints();
        let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;
        for (const p of pts) {
            if (p.X < minX) minX = p.X; if (p.Y < minY) minY = p.Y;
            if (p.X > maxX) maxX = p.X; if (p.Y > maxY) maxY = p.Y;
        }
        return new BoundingBox(VPoint.internal(minX, minY), VPoint.internal(maxX, maxY));
    }
}

// ============================================================================
// VBezier - Cubic Bezier Curve
// ============================================================================
class VBezier extends Shape {
    constructor(p0OrX0, p1OrY0, p2OrX1, p3OrY1, x2, y2, x3, y3) {
        super(true);
        if (p0OrX0 instanceof VPoint || (p0OrX0 && typeof p0OrX0.X === 'number')) {
            this._p0 = VPoint.internal(p0OrX0.X, p0OrX0.Y);
            this._p1 = VPoint.internal(p1OrY0.X, p1OrY0.Y);
            this._p2 = VPoint.internal(p2OrX1.X, p2OrX1.Y);
            this._p3 = VPoint.internal(p3OrY1.X, p3OrY1.Y);
        } else {
            this._p0 = VPoint.internal(p0OrX0, p1OrY0);
            this._p1 = VPoint.internal(p2OrX1, p3OrY1);
            this._p2 = VPoint.internal(x2, y2);
            this._p3 = VPoint.internal(x3, y3);
        }
        this._segments = 32;
    }

    get p0() { return this._p0; }
    get p1() { return this._p1; }
    get p2() { return this._p2; }
    get p3() { return this._p3; }
    get segments() { return this._segments; }
    set segments(v) { this._segments = v; }
    get startPoint() { return this._p0; }
    get endPoint() { return this._p3; }
    get midPoint() { return this.evaluate(0.5); }
    get vertices() { return [this._p0, this._p1, this._p2, this._p3]; }

    evaluate(t) {
        const u = 1 - t, u2 = u * u, u3 = u2 * u;
        const t2 = t * t, t3 = t2 * t;
        return VPoint.internal(
            u3 * this._p0.X + 3 * u2 * t * this._p1.X + 3 * u * t2 * this._p2.X + t3 * this._p3.X,
            u3 * this._p0.Y + 3 * u2 * t * this._p1.Y + 3 * u * t2 * this._p2.Y + t3 * this._p3.Y
        );
    }

    getRenderPoints() {
        const pts = [];
        for (let i = 0; i <= this._segments; i++) pts.push(this.evaluate(i / this._segments));
        return pts;
    }

    getLength() {
        const pts = this.getRenderPoints();
        let len = 0;
        for (let i = 0; i < pts.length - 1; i++) len += pts[i].distanceTo(pts[i + 1]);
        return len;
    }

    divide(n) {
        const pts = [];
        for (let i = 0; i <= n; i++) pts.push(this.evaluate(i / n));
        return pts;
    }

    project(point) {
        const pts = this.getRenderPoints();
        let best = pts[0], bestDist = Infinity;
        for (const p of pts) {
            const d = Math.sqrt((p.X - point.X) ** 2 + (p.Y - point.Y) ** 2);
            if (d < bestDist) { bestDist = d; best = p; }
        }
        return best;
    }

    clone() {
        const b = new VBezier(this._p0, this._p1, this._p2, this._p3);
        b._color = this._color; b._lineWeight = this._lineWeight;
        b._segments = this._segments;
        return b;
    }

    move(vector) {
        this._p0 = VPoint.internal(this._p0.X + vector.X, this._p0.Y + vector.Y);
        this._p1 = VPoint.internal(this._p1.X + vector.X, this._p1.Y + vector.Y);
        this._p2 = VPoint.internal(this._p2.X + vector.X, this._p2.Y + vector.Y);
        this._p3 = VPoint.internal(this._p3.X + vector.X, this._p3.Y + vector.Y);
    }
    rotate(pivot, angle) {
        this._p0 = GeometryHelper.rotatePoint(this._p0, pivot, angle);
        this._p1 = GeometryHelper.rotatePoint(this._p1, pivot, angle);
        this._p2 = GeometryHelper.rotatePoint(this._p2, pivot, angle);
        this._p3 = GeometryHelper.rotatePoint(this._p3, pivot, angle);
    }
    flip(mirrorLine) {
        this._p0 = GeometryHelper.flipPoint(this._p0, mirrorLine);
        this._p1 = GeometryHelper.flipPoint(this._p1, mirrorLine);
        this._p2 = GeometryHelper.flipPoint(this._p2, mirrorLine);
        this._p3 = GeometryHelper.flipPoint(this._p3, mirrorLine);
    }
    scale(center, factor) {
        const sc = (p) => VPoint.internal(center.X + (p.X - center.X) * factor, center.Y + (p.Y - center.Y) * factor);
        this._p0 = sc(this._p0); this._p1 = sc(this._p1);
        this._p2 = sc(this._p2); this._p3 = sc(this._p3);
    }

    getBounds() {
        const pts = this.getRenderPoints();
        let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;
        for (const p of pts) {
            if (p.X < minX) minX = p.X; if (p.Y < minY) minY = p.Y;
            if (p.X > maxX) maxX = p.X; if (p.Y > maxY) maxY = p.Y;
        }
        return new BoundingBox(VPoint.internal(minX, minY), VPoint.internal(maxX, maxY));
    }
}

// ============================================================================
// VArrow
// ============================================================================
class VArrow extends Shape {
    constructor(startOrX1, endOrY1, x2, y2) {
        super(true);
        if (startOrX1 instanceof VPoint || (startOrX1 && typeof startOrX1.X === 'number')) {
            this._start = VPoint.internal(startOrX1.X, startOrX1.Y);
            this._end = VPoint.internal(endOrY1.X, endOrY1.Y);
        } else {
            this._start = VPoint.internal(startOrX1, endOrY1);
            this._end = VPoint.internal(x2, y2);
        }
        this._headLength = 15;
        this._headAngle = 30;
        this._doubleEnded = false;
    }

    static fromDirection(startPoint, direction, length) {
        const dir = direction.normalize();
        const end = VPoint.internal(startPoint.X + dir.X * length, startPoint.Y + dir.Y * length);
        return new VArrow(startPoint, end);
    }

    get start() { return this._start; }
    get end() { return this._end; }
    get headLength() { return this._headLength; }
    set headLength(v) { this._headLength = v; }
    get headAngle() { return this._headAngle; }
    set headAngle(v) { this._headAngle = v; }
    get doubleEnded() { return this._doubleEnded; }
    set doubleEnded(v) { this._doubleEnded = v; }
    get midPoint() { return VPoint.internal((this._start.X + this._end.X) / 2, (this._start.Y + this._end.Y) / 2); }

    getEndArrowhead() {
        const dx = this._end.X - this._start.X, dy = this._end.Y - this._start.Y;
        const len = Math.sqrt(dx * dx + dy * dy);
        if (len < EPSILON) return { wing1: this._end, wing2: this._end };
        const ux = dx / len, uy = dy / len;
        const ha = this._headAngle * Math.PI / 180;
        const cos = Math.cos(ha), sin = Math.sin(ha);
        return {
            wing1: VPoint.internal(this._end.X - this._headLength * (ux * cos - uy * sin), this._end.Y - this._headLength * (ux * sin + uy * cos)),
            wing2: VPoint.internal(this._end.X - this._headLength * (ux * cos + uy * sin), this._end.Y - this._headLength * (-ux * sin + uy * cos))
        };
    }

    getStartArrowhead() {
        const dx = this._start.X - this._end.X, dy = this._start.Y - this._end.Y;
        const len = Math.sqrt(dx * dx + dy * dy);
        if (len < EPSILON) return { wing1: this._start, wing2: this._start };
        const ux = dx / len, uy = dy / len;
        const ha = this._headAngle * Math.PI / 180;
        const cos = Math.cos(ha), sin = Math.sin(ha);
        return {
            wing1: VPoint.internal(this._start.X - this._headLength * (ux * cos - uy * sin), this._start.Y - this._headLength * (ux * sin + uy * cos)),
            wing2: VPoint.internal(this._start.X - this._headLength * (ux * cos + uy * sin), this._start.Y - this._headLength * (-ux * sin + uy * cos))
        };
    }

    clone() {
        const a = new VArrow(this._start, this._end);
        a._color = this._color; a._lineWeight = this._lineWeight;
        a._headLength = this._headLength; a._headAngle = this._headAngle;
        a._doubleEnded = this._doubleEnded;
        return a;
    }

    move(vector) {
        this._start = VPoint.internal(this._start.X + vector.X, this._start.Y + vector.Y);
        this._end = VPoint.internal(this._end.X + vector.X, this._end.Y + vector.Y);
    }
    rotate(pivot, angle) {
        this._start = GeometryHelper.rotatePoint(this._start, pivot, angle);
        this._end = GeometryHelper.rotatePoint(this._end, pivot, angle);
    }
    flip(mirrorLine) {
        this._start = GeometryHelper.flipPoint(this._start, mirrorLine);
        this._end = GeometryHelper.flipPoint(this._end, mirrorLine);
    }
    scale(center, factor) {
        this._start = VPoint.internal(center.X + (this._start.X - center.X) * factor, center.Y + (this._start.Y - center.Y) * factor);
        this._end = VPoint.internal(center.X + (this._end.X - center.X) * factor, center.Y + (this._end.Y - center.Y) * factor);
        this._headLength *= Math.abs(factor);
    }

    getBounds() {
        const pts = [this._start, this._end];
        const ah = this.getEndArrowhead();
        pts.push(ah.wing1, ah.wing2);
        if (this._doubleEnded) {
            const sah = this.getStartArrowhead();
            pts.push(sah.wing1, sah.wing2);
        }
        let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;
        for (const p of pts) {
            if (p.X < minX) minX = p.X; if (p.Y < minY) minY = p.Y;
            if (p.X > maxX) maxX = p.X; if (p.Y > maxY) maxY = p.Y;
        }
        return new BoundingBox(VPoint.internal(minX, minY), VPoint.internal(maxX, maxY));
    }
}

// ============================================================================
// VText
// ============================================================================
const VFont = {
    Arial: 'Arial',
    TimesNewRoman: 'Times New Roman',
    CourierNew: 'Courier New',
    Verdana: 'Verdana',
    Georgia: 'Georgia',
    Consolas: 'Consolas',
    Calibri: 'Calibri',
    SegoeUI: 'Segoe UI',
    ComicSansMS: 'Comic Sans MS',
    Impact: 'Impact',
    LucidaConsole: 'Lucida Console',
};

const VFontWeight = { Normal: 'normal', Bold: 'bold' };
const VTextAnchor = {
    BottomLeft: 'BottomLeft', BottomCenter: 'BottomCenter', BottomRight: 'BottomRight',
    MiddleLeft: 'MiddleLeft', MiddleCenter: 'MiddleCenter', MiddleRight: 'MiddleRight',
    TopLeft: 'TopLeft', TopCenter: 'TopCenter', TopRight: 'TopRight',
};

class VText extends Shape {
    constructor(location, content, height) {
        super(true);
        this._location = VPoint.internal(location.X, location.Y);
        this._content = content;
        this._height = height || 12;
        this._width = 0;
        this._font = VFont.Arial;
        this._fontWeight = VFontWeight.Normal;
        this._anchor = VTextAnchor.BottomLeft;
        this._angle = 0;
    }

    get location() { return this._location; }
    set location(v) { this._location = VPoint.internal(v.X, v.Y); }
    get content() { return this._content; }
    set content(v) { this._content = v; }
    get height() { return this._height; }
    set height(v) { this._height = v; }
    get width() { return this._width; }
    set width(v) { this._width = v; }
    get font() { return this._font; }
    set font(v) { this._font = v; }
    get fontWeight() { return this._fontWeight; }
    set fontWeight(v) { this._fontWeight = v; }
    get anchor() { return this._anchor; }
    set anchor(v) { this._anchor = v; }
    get angle() { return this._angle; }
    set angle(v) { this._angle = v; }

    clone() {
        const t = new VText(this._location, this._content, this._height);
        t._color = this._color; t._font = this._font; t._fontWeight = this._fontWeight;
        t._anchor = this._anchor; t._width = this._width; t._angle = this._angle;
        return t;
    }

    move(vector) { this._location = VPoint.internal(this._location.X + vector.X, this._location.Y + vector.Y); }
    rotate(pivot, angle) { this._location = GeometryHelper.rotatePoint(this._location, pivot, angle); }
    flip(mirrorLine) { this._location = GeometryHelper.flipPoint(this._location, mirrorLine); }
    scale(center, factor) {
        this._location = VPoint.internal(center.X + (this._location.X - center.X) * factor, center.Y + (this._location.Y - center.Y) * factor);
        this._height *= Math.abs(factor);
    }

    getBounds() {
        const w = this._width || this._content.length * this._height * 0.6;
        const h = this._height;
        if (!this._angle) {
            return new BoundingBox(
                VPoint.internal(this._location.X, this._location.Y),
                VPoint.internal(this._location.X + w, this._location.Y + h)
            );
        }
        const rad = this._angle * Math.PI / 180;
        const cos = Math.cos(rad), sin = Math.sin(rad);
        const lx = this._location.X, ly = this._location.Y;
        const rot = (rx, ry) => [lx + rx * cos - ry * sin, ly + rx * sin + ry * cos];
        const corners = [rot(0, 0), rot(w, 0), rot(w, h), rot(0, h)];
        const xs = corners.map(c => c[0]), ys = corners.map(c => c[1]);
        return new BoundingBox(
            VPoint.internal(Math.min(...xs), Math.min(...ys)),
            VPoint.internal(Math.max(...xs), Math.max(...ys))
        );
    }
}

// ============================================================================
// VDimension
// ============================================================================
class VDimension extends Shape {
    constructor(point1OrX1, point2OrY1, x2, y2) {
        super(true);
        if (point1OrX1 instanceof VPoint || (point1OrX1 && typeof point1OrX1.X === 'number')) {
            this._point1 = VPoint.internal(point1OrX1.X, point1OrX1.Y);
            this._point2 = VPoint.internal(point2OrY1.X, point2OrY1.Y);
        } else {
            this._point1 = VPoint.internal(point1OrX1, point2OrY1);
            this._point2 = VPoint.internal(x2, y2);
        }
        this._offset = 20;
        this._extensionLength = 10;
        this._arrowSize = 8;
        this._customText = null;
        this._decimalPlaces = 2;
        this._textHeight = 12;
        this._extendBeyondDimLines = 1.25;
        this._offsetFromOrigin = 0.625;
        this._suppressExtLine1 = false;
        this._suppressExtLine2 = false;
        this._prefix = '';
        this._suffix = '';
        this._textBackgroundOpaque = false;
        this._extensionLineColor = null;
        this._dimensionLineColor = null;
        this._textColor = null;
        this._suppressDimensionLine = false;
    }

    get point1() { return this._point1; }
    get point2() { return this._point2; }
    get offset() { return this._offset; }
    set offset(v) { this._offset = v; }
    get arrowSize() { return this._arrowSize; }
    set arrowSize(v) { this._arrowSize = v; }
    get customText() { return this._customText; }
    set customText(v) { this._customText = v; }
    get decimalPlaces() { return this._decimalPlaces; }
    set decimalPlaces(v) { this._decimalPlaces = v; }
    get textHeight() { return this._textHeight; }
    set textHeight(v) { this._textHeight = v; }
    get prefix() { return this._prefix; }
    set prefix(v) { this._prefix = v; }
    get suffix() { return this._suffix; }
    set suffix(v) { this._suffix = v; }
    get suppressExtLine1() { return this._suppressExtLine1; }
    set suppressExtLine1(v) { this._suppressExtLine1 = v; }
    get suppressExtLine2() { return this._suppressExtLine2; }
    set suppressExtLine2(v) { this._suppressExtLine2 = v; }
    get textBackgroundOpaque() { return this._textBackgroundOpaque; }
    set textBackgroundOpaque(v) { this._textBackgroundOpaque = v; }
    get extensionLineColor() { return this._extensionLineColor; }
    set extensionLineColor(v) { this._extensionLineColor = v; }
    get dimensionLineColor() { return this._dimensionLineColor; }
    set dimensionLineColor(v) { this._dimensionLineColor = v; }
    get textColor() { return this._textColor; }
    set textColor(v) { this._textColor = v; }

    get distance() { return this._point1.distanceTo(this._point2); }
    get displayText() {
        const val = this._customText || this.distance.toFixed(this._decimalPlaces);
        return `${this._prefix}${val}${this._suffix}`;
    }

    getDimensionGeometry() {
        const dx = this._point2.X - this._point1.X;
        const dy = this._point2.Y - this._point1.Y;
        const len = Math.sqrt(dx * dx + dy * dy);
        if (len < EPSILON) return null;

        // Normal direction (perpendicular)
        const nx = -dy / len, ny = dx / len;
        const off = this._offset;

        const dimStart = VPoint.internal(this._point1.X + nx * off, this._point1.Y + ny * off);
        const dimEnd = VPoint.internal(this._point2.X + nx * off, this._point2.Y + ny * off);
        const textPos = VPoint.internal((dimStart.X + dimEnd.X) / 2, (dimStart.Y + dimEnd.Y) / 2);

        const ext1Start = VPoint.internal(this._point1.X + nx * this._offsetFromOrigin, this._point1.Y + ny * this._offsetFromOrigin);
        const ext1End = VPoint.internal(this._point1.X + nx * (off + this._extendBeyondDimLines), this._point1.Y + ny * (off + this._extendBeyondDimLines));
        const ext2Start = VPoint.internal(this._point2.X + nx * this._offsetFromOrigin, this._point2.Y + ny * this._offsetFromOrigin);
        const ext2End = VPoint.internal(this._point2.X + nx * (off + this._extendBeyondDimLines), this._point2.Y + ny * (off + this._extendBeyondDimLines));

        return { dimStart, dimEnd, textPos, ext1Start, ext1End, ext2Start, ext2End };
    }

    clone() {
        const d = new VDimension(this._point1, this._point2);
        d._offset = this._offset; d._arrowSize = this._arrowSize;
        d._textHeight = this._textHeight; d._decimalPlaces = this._decimalPlaces;
        d._color = this._color; d._prefix = this._prefix; d._suffix = this._suffix;
        return d;
    }

    move(vector) {
        this._point1 = VPoint.internal(this._point1.X + vector.X, this._point1.Y + vector.Y);
        this._point2 = VPoint.internal(this._point2.X + vector.X, this._point2.Y + vector.Y);
    }
    rotate(pivot, angle) {
        this._point1 = GeometryHelper.rotatePoint(this._point1, pivot, angle);
        this._point2 = GeometryHelper.rotatePoint(this._point2, pivot, angle);
    }
    flip(mirrorLine) {
        this._point1 = GeometryHelper.flipPoint(this._point1, mirrorLine);
        this._point2 = GeometryHelper.flipPoint(this._point2, mirrorLine);
    }
    scale(center, factor) {
        this._point1 = VPoint.internal(center.X + (this._point1.X - center.X) * factor, center.Y + (this._point1.Y - center.Y) * factor);
        this._point2 = VPoint.internal(center.X + (this._point2.X - center.X) * factor, center.Y + (this._point2.Y - center.Y) * factor);
        this._offset *= Math.abs(factor);
        this._arrowSize *= Math.abs(factor);
        this._textHeight *= Math.abs(factor);
    }

    getBounds() {
        const geom = this.getDimensionGeometry();
        if (!geom) return new BoundingBox(this._point1, this._point2);
        const pts = [this._point1, this._point2, geom.dimStart, geom.dimEnd, geom.ext1End, geom.ext2End];
        let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;
        for (const p of pts) {
            if (p.X < minX) minX = p.X; if (p.Y < minY) minY = p.Y;
            if (p.X > maxX) maxX = p.X; if (p.Y > maxY) maxY = p.Y;
        }
        return new BoundingBox(VPoint.internal(minX, minY), VPoint.internal(maxX, maxY));
    }
}

// ============================================================================
// VRadialDimension
// ============================================================================
class VRadialDimension extends Shape {
    constructor(centerOrCircle, radius) {
        super(true);
        if (centerOrCircle instanceof VCircle || centerOrCircle instanceof VArc) {
            this._center = VPoint.internal(centerOrCircle.center.X, centerOrCircle.center.Y);
            this._radius = centerOrCircle.radius;
        } else {
            this._center = VPoint.internal(centerOrCircle.X, centerOrCircle.Y);
            this._radius = radius;
        }
        this._leaderAngle = 45;
        this._showDiameter = false;
        this._arrowSize = 8;
        this._customText = null;
        this._decimalPlaces = 2;
        this._textHeight = 12;
        this._prefix = '';
        this._suffix = '';
    }

    get center() { return this._center; }
    get radius() { return this._radius; }
    get leaderAngle() { return this._leaderAngle; }
    set leaderAngle(v) { this._leaderAngle = v; }
    get showDiameter() { return this._showDiameter; }
    set showDiameter(v) { this._showDiameter = v; }
    get arrowSize() { return this._arrowSize; }
    set arrowSize(v) { this._arrowSize = v; }
    get textHeight() { return this._textHeight; }
    set textHeight(v) { this._textHeight = v; }
    get prefix() { return this._prefix; }
    set prefix(v) { this._prefix = v; }
    get suffix() { return this._suffix; }
    set suffix(v) { this._suffix = v; }

    get value() { return this._showDiameter ? this._radius * 2 : this._radius; }
    get displayText() {
        const prefix = this._showDiameter ? 'Ø' : 'R';
        const val = this._customText || this.value.toFixed(this._decimalPlaces);
        return `${this._prefix}${prefix}${val}${this._suffix}`;
    }

    getDimensionGeometry() {
        const a = this._leaderAngle * Math.PI / 180;
        const circumPoint = VPoint.internal(this._center.X + this._radius * Math.cos(a), this._center.Y + this._radius * Math.sin(a));
        const leaderEnd = VPoint.internal(this._center.X + (this._radius + 20) * Math.cos(a), this._center.Y + (this._radius + 20) * Math.sin(a));
        return { leaderStart: this._center, leaderEnd, textPos: leaderEnd, circumPoint };
    }

    clone() {
        const r = new VRadialDimension(this._center, this._radius);
        r._leaderAngle = this._leaderAngle; r._showDiameter = this._showDiameter;
        r._arrowSize = this._arrowSize; r._textHeight = this._textHeight;
        r._color = this._color;
        return r;
    }

    move(vector) { this._center = VPoint.internal(this._center.X + vector.X, this._center.Y + vector.Y); }
    rotate(pivot, angle) {
        this._center = GeometryHelper.rotatePoint(this._center, pivot, angle);
        this._leaderAngle += angle;
    }
    flip(mirrorLine) { this._center = GeometryHelper.flipPoint(this._center, mirrorLine); }
    scale(center, factor) {
        this._center = VPoint.internal(center.X + (this._center.X - center.X) * factor, center.Y + (this._center.Y - center.Y) * factor);
        this._radius *= Math.abs(factor);
    }

    getBounds() {
        const geom = this.getDimensionGeometry();
        const pts = [this._center, geom.circumPoint, geom.leaderEnd];
        let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;
        for (const p of pts) {
            if (p.X < minX) minX = p.X; if (p.Y < minY) minY = p.Y;
            if (p.X > maxX) maxX = p.X; if (p.Y > maxY) maxY = p.Y;
        }
        return new BoundingBox(VPoint.internal(minX, minY), VPoint.internal(maxX, maxY));
    }
}

// ============================================================================
// VGroup
// ============================================================================
class VGroup extends Shape {
    constructor(...args) {
        super(true);
        this._shapes = [];
        if (args.length === 1 && Array.isArray(args[0])) {
            this._shapes = [...args[0]];
        } else {
            this._shapes = [...args];
        }
    }

    get shapes() { return this._shapes; }
    get count() { return this._shapes.length; }
    at(index) { return this._shapes[index]; }

    add(shape) { this._shapes.push(shape); return this; }
    addRange(shapes) { this._shapes.push(...shapes); return this; }
    removeShape(shape) {
        const i = this._shapes.indexOf(shape);
        if (i >= 0) this._shapes.splice(i, 1);
        return i >= 0;
    }
    removeAt(index) { this._shapes.splice(index, 1); }
    clear() { this._shapes = []; }
    containsShape(shape) { return this._shapes.includes(shape); }

    clone() {
        const g = new VGroup(this._shapes.map(s => s.clone()));
        g._color = this._color;
        return g;
    }

    move(vector) { this._shapes.forEach(s => s.move(vector)); }
    rotate(pivot, angle) { this._shapes.forEach(s => s.rotate(pivot, angle)); }
    flip(mirrorLine) { this._shapes.forEach(s => s.flip(mirrorLine)); }
    scale(center, factor) { this._shapes.forEach(s => s.scale(center, factor)); }

    getBounds() {
        if (this._shapes.length === 0) return new BoundingBox(VPoint.internal(0, 0), VPoint.internal(0, 0));
        let bb = this._shapes[0].getBounds();
        for (let i = 1; i < this._shapes.length; i++) bb = bb.union(this._shapes[i].getBounds());
        return bb;
    }
}

// ============================================================================
// VGrid
// ============================================================================
class VGrid extends Shape {
    constructor(location, xCount, yCount, xSpacingOrSpacing, ySpacingOrCentered, centered) {
        super(true);
        this._location = VPoint.internal(location.X, location.Y);
        this._xCount = xCount;
        this._yCount = yCount;

        if (typeof ySpacingOrCentered === 'boolean') {
            // (location, xCount, yCount, spacing, centered)
            this._xSpacing = xSpacingOrSpacing || 1;
            this._ySpacing = xSpacingOrSpacing || 1;
            this._centered = ySpacingOrCentered;
        } else if (ySpacingOrCentered !== undefined) {
            this._xSpacing = xSpacingOrSpacing;
            this._ySpacing = ySpacingOrCentered;
            this._centered = centered !== undefined ? centered : true;
        } else {
            this._xSpacing = xSpacingOrSpacing || 1;
            this._ySpacing = xSpacingOrSpacing || 1;
            this._centered = true;
        }

        this._buildPoints();
    }

    _buildPoints() {
        this._points = [];
        let ox = this._location.X, oy = this._location.Y;
        if (this._centered) {
            ox -= (this._xCount - 1) * this._xSpacing / 2;
            oy -= (this._yCount - 1) * this._ySpacing / 2;
        }
        for (let row = 0; row < this._yCount; row++) {
            for (let col = 0; col < this._xCount; col++) {
                this._points.push(VPoint.internal(ox + col * this._xSpacing, oy + row * this._ySpacing));
            }
        }
    }

    get points() { return this._points; }
    get location() { return this._location; }
    get xCount() { return this._xCount; }
    get yCount() { return this._yCount; }
    get xSpacing() { return this._xSpacing; }
    get ySpacing() { return this._ySpacing; }
    get centered() { return this._centered; }
    get count() { return this._points.length; }

    at(indexOrCol, row) {
        if (row !== undefined) return this._points[row * this._xCount + indexOrCol];
        return this._points[indexOrCol];
    }

    clone() {
        const g = new VGrid(this._location, this._xCount, this._yCount, this._xSpacing, this._ySpacing, this._centered);
        g._color = this._color;
        return g;
    }

    move(vector) {
        this._location = VPoint.internal(this._location.X + vector.X, this._location.Y + vector.Y);
        this._buildPoints();
    }
    rotate(pivot, angle) {
        this._points = this._points.map(p => GeometryHelper.rotatePoint(p, pivot, angle));
        this._location = GeometryHelper.rotatePoint(this._location, pivot, angle);
    }
    flip(mirrorLine) {
        this._points = this._points.map(p => GeometryHelper.flipPoint(p, mirrorLine));
        this._location = GeometryHelper.flipPoint(this._location, mirrorLine);
    }
    scale(center, factor) {
        this._location = VPoint.internal(center.X + (this._location.X - center.X) * factor, center.Y + (this._location.Y - center.Y) * factor);
        this._xSpacing *= Math.abs(factor);
        this._ySpacing *= Math.abs(factor);
        this._buildPoints();
    }

    getBounds() {
        let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;
        for (const p of this._points) {
            if (p.X < minX) minX = p.X; if (p.Y < minY) minY = p.Y;
            if (p.X > maxX) maxX = p.X; if (p.Y > maxY) maxY = p.Y;
        }
        return new BoundingBox(VPoint.internal(minX, minY), VPoint.internal(maxX, maxY));
    }
}

// ============================================================================
// VColor - Static color utilities
// ============================================================================
const VColor = {
    Red: '#FF0000', Green: '#00FF00', Blue: '#0000FF', Yellow: '#FFFF00',
    Orange: '#FFA500', Purple: '#800080', Pink: '#FFC0CB', Cyan: '#00FFFF',
    Magenta: '#FF00FF', White: '#FFFFFF', Black: '#000000', Gray: '#808080',
    Brown: '#A52A2A', Coral: '#FF7F50', Crimson: '#DC143C', DarkBlue: '#00008B',
    DarkGreen: '#006400', DarkRed: '#8B0000', Gold: '#FFD700', Indigo: '#4B0082',
    Lime: '#00FF00', Maroon: '#800000', Navy: '#000080', Olive: '#808000',
    Silver: '#C0C0C0', Teal: '#008080', Violet: '#EE82EE', Wheat: '#F5DEB3',
    SkyBlue: '#87CEEB', SteelBlue: '#4682B4', Salmon: '#FA8072', Plum: '#DDA0DD',
    Orchid: '#DA70D6', MediumPurple: '#9370DB', LightBlue: '#ADD8E6', LightGreen: '#90EE90',
    LightCoral: '#F08080', LightPink: '#FFB6C1', LightGray: '#D3D3D3', DarkGray: '#A9A9A9',
    DimGray: '#696969', SlateGray: '#708090', DarkOrange: '#FF8C00', DeepPink: '#FF1493',
    Tomato: '#FF6347', OrangeRed: '#FF4500', HotPink: '#FF69B4', DodgerBlue: '#1E90FF',
    RoyalBlue: '#4169E1', MediumBlue: '#0000CD', ForestGreen: '#228B22', SeaGreen: '#2E8B57',
    DarkCyan: '#008B8B', Turquoise: '#40E0D0', Aquamarine: '#7FFFD4', SpringGreen: '#00FF7F',
    Chartreuse: '#7FFF00', GreenYellow: '#ADFF2F', Khaki: '#F0E68C', PaleGreen: '#98FB98',

    _vibrant: ['#FF0000', '#00FF00', '#0000FF', '#FFFF00', '#FF00FF', '#00FFFF', '#FFA500', '#FF1493', '#00FF7F', '#FFD700', '#1E90FF', '#FF6347'],
    _pastel: ['#FFB3BA', '#FFDFBA', '#FFFFBA', '#BAFFC9', '#BAE1FF', '#E8BAFF', '#FFD1DC', '#B5EAD7', '#C7CEEA', '#FFDAC1', '#FF9AA2', '#B5B9FF'],

    getRandomColor(pastel = true) { return pastel ? this.getRandomPastelColor() : this.getRandomVibrantColor(); },
    getRandomVibrantColor() { return this._vibrant[Math.floor(Math.random() * this._vibrant.length)]; },
    getRandomPastelColor() { return this._pastel[Math.floor(Math.random() * this._pastel.length)]; },
    fromRgb(r, g, b) { return `#${r.toString(16).padStart(2, '0')}${g.toString(16).padStart(2, '0')}${b.toString(16).padStart(2, '0')}`; },
    fromArgb(a, r, g, b) { return `rgba(${r},${g},${b},${(a / 255).toFixed(2)})`; },
    withOpacity(r, g, b, opacity) { return `rgba(${r},${g},${b},${opacity})`; },
    getVibrantColors() { return [...this._vibrant]; },
    getPastelColors() { return [...this._pastel]; },
};

// ============================================================================
// LineType patterns (for dashed/dotted rendering)
// ============================================================================
const LineTypes = {
    Continuous: [],
    Dashed: [12, 6],
    Dotted: [2, 4],
    DashDot: [12, 4, 2, 4],
    DashDotDot: [12, 4, 2, 4, 2, 4],
    Center: [18, 4, 4, 4],
    Phantom: [18, 4, 4, 4, 4, 4],
    Hidden: [6, 4],
};

// ============================================================================
// VizConsole - Console output for user code
// ============================================================================
class VizConsole {
    constructor() { this._messages = []; }
    get messages() { return this._messages; }
    clear() { this._messages = []; }

    log(...args) { this._messages.push({ type: 'log', text: args.map(a => String(a)).join(' ') }); }
    warn(...args) { this._messages.push({ type: 'warn', text: args.map(a => String(a)).join(' ') }); }
    error(...args) { this._messages.push({ type: 'error', text: args.map(a => String(a)).join(' ') }); }
    info(...args) { this._messages.push({ type: 'info', text: args.map(a => String(a)).join(' ') }); }
}

// ============================================================================
// Exports
// ============================================================================
export {
    EPSILON,
    VXYZ,
    BoundingBox,
    GeometryHelper,
    Shape,
    ShapeDefaults,
    VPoint,
    VLine,
    VCircle,
    VArc,
    VRectangle,
    VEllipse,
    VPolygon,
    VPolyline,
    VSpline,
    VBezier,
    VArrow,
    VText, VFont, VFontWeight, VTextAnchor,
    VDimension,
    VRadialDimension,
    VGroup,
    VGrid,
    VColor,
    LineTypes,
    VizConsole,
    resetIdCounter,
    clearRegistry,
    getRegistry,
};
