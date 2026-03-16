// Code2Viz Web - Boolean Operations on Polygons
// Union, Intersection, Difference using Sutherland-Hodgman clipping

import { VPoint, VPolygon, EPSILON } from './index.js';

// Sutherland-Hodgman polygon clipping algorithm
function clipPolygon(subject, clip) {
    let output = subject.map(p => ({ x: p.X, y: p.Y }));
    const clipPts = clip.map(p => ({ x: p.X, y: p.Y }));

    for (let i = 0; i < clipPts.length; i++) {
        if (output.length === 0) return [];
        const input = output;
        output = [];
        const edgeStart = clipPts[i];
        const edgeEnd = clipPts[(i + 1) % clipPts.length];

        for (let j = 0; j < input.length; j++) {
            const current = input[j];
            const prev = input[(j + input.length - 1) % input.length];
            const currInside = isInside(current, edgeStart, edgeEnd);
            const prevInside = isInside(prev, edgeStart, edgeEnd);

            if (currInside) {
                if (!prevInside) {
                    const inter = lineIntersect(prev, current, edgeStart, edgeEnd);
                    if (inter) output.push(inter);
                }
                output.push(current);
            } else if (prevInside) {
                const inter = lineIntersect(prev, current, edgeStart, edgeEnd);
                if (inter) output.push(inter);
            }
        }
    }

    return output;
}

function isInside(point, edgeStart, edgeEnd) {
    return (edgeEnd.x - edgeStart.x) * (point.y - edgeStart.y) -
           (edgeEnd.y - edgeStart.y) * (point.x - edgeStart.x) >= 0;
}

function lineIntersect(p1, p2, p3, p4) {
    const x1 = p1.x, y1 = p1.y, x2 = p2.x, y2 = p2.y;
    const x3 = p3.x, y3 = p3.y, x4 = p4.x, y4 = p4.y;
    const denom = (x1 - x2) * (y3 - y4) - (y1 - y2) * (x3 - x4);
    if (Math.abs(denom) < EPSILON) return null;
    const t = ((x1 - x3) * (y3 - y4) - (y1 - y3) * (x3 - x4)) / denom;
    return { x: x1 + t * (x2 - x1), y: y1 + t * (y2 - y1) };
}

function getPolygonPoints(shape) {
    if (shape.points) return shape.points;
    if (shape.divide) return shape.divide(64);
    return [];
}

function isClockwise(pts) {
    let sum = 0;
    for (let i = 0; i < pts.length; i++) {
        const c = pts[i], n = pts[(i + 1) % pts.length];
        sum += (n.X - c.X) * (n.Y + c.Y);
    }
    return sum > 0;
}

function reversePoints(pts) {
    return [...pts].reverse();
}

function ensureCounterClockwise(pts) {
    if (isClockwise(pts)) return reversePoints(pts);
    return pts;
}

function ensureClockwise(pts) {
    if (!isClockwise(pts)) return reversePoints(pts);
    return pts;
}

// ============================================================================
// Public API
// ============================================================================

/**
 * Compute intersection of two polygons
 */
export function polygonIntersection(shapeA, shapeB) {
    let ptsA = ensureCounterClockwise(getPolygonPoints(shapeA));
    let ptsB = ensureCounterClockwise(getPolygonPoints(shapeB));
    const clipped = clipPolygon(ptsA, ptsB);
    if (clipped.length < 3) return null;
    const result = new VPolygon(clipped.map(p => VPoint.internal(p.x, p.y)));
    return result;
}

/**
 * Compute difference A - B (parts of A not in B)
 */
export function polygonDifference(shapeA, shapeB) {
    let ptsA = ensureCounterClockwise(getPolygonPoints(shapeA));
    let ptsB = ensureClockwise(getPolygonPoints(shapeB)); // reversed = complement
    const clipped = clipPolygon(ptsA, ptsB);
    if (clipped.length < 3) return null;
    const result = new VPolygon(clipped.map(p => VPoint.internal(p.x, p.y)));
    return result;
}

/**
 * Approximate union of two polygons using convex hull
 * Note: For complex concave unions, a proper polygon clipping library is needed
 */
export function polygonUnion(shapeA, shapeB) {
    // Check if polygons overlap
    const intersection = polygonIntersection(shapeA, shapeB);
    if (!intersection) {
        // No overlap - return both as separate shapes (no union possible without overlap)
        return null;
    }

    // For simple convex polygons, compute the convex hull of all points
    const allPts = [
        ...getPolygonPoints(shapeA).map(p => ({ x: p.X, y: p.Y })),
        ...getPolygonPoints(shapeB).map(p => ({ x: p.X, y: p.Y })),
    ];

    const hull = convexHull(allPts);
    if (hull.length < 3) return null;
    const result = new VPolygon(hull.map(p => VPoint.internal(p.x, p.y)));
    return result;
}

/**
 * Compute convex hull using Graham scan
 */
function convexHull(points) {
    if (points.length < 3) return points;

    // Find lowest-then-leftmost point
    let pivot = points[0];
    for (const p of points) {
        if (p.y < pivot.y || (p.y === pivot.y && p.x < pivot.x)) pivot = p;
    }

    // Sort by polar angle
    const sorted = points.filter(p => p !== pivot).sort((a, b) => {
        const angleA = Math.atan2(a.y - pivot.y, a.x - pivot.x);
        const angleB = Math.atan2(b.y - pivot.y, b.x - pivot.x);
        if (Math.abs(angleA - angleB) < EPSILON) {
            const dA = (a.x - pivot.x) ** 2 + (a.y - pivot.y) ** 2;
            const dB = (b.x - pivot.x) ** 2 + (b.y - pivot.y) ** 2;
            return dA - dB;
        }
        return angleA - angleB;
    });

    const stack = [pivot];
    for (const p of sorted) {
        while (stack.length >= 2) {
            const a = stack[stack.length - 2], b = stack[stack.length - 1];
            const cross = (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);
            if (cross <= 0) stack.pop();
            else break;
        }
        stack.push(p);
    }
    return stack;
}

/**
 * Check if a point is inside a polygon (ray casting)
 */
export function pointInPolygon(point, polygon) {
    const pts = getPolygonPoints(polygon);
    let inside = false;
    for (let i = 0, j = pts.length - 1; i < pts.length; j = i++) {
        if (((pts[i].Y > point.Y) !== (pts[j].Y > point.Y)) &&
            (point.X < (pts[j].X - pts[i].X) * (point.Y - pts[i].Y) / (pts[j].Y - pts[i].Y) + pts[i].X)) {
            inside = !inside;
        }
    }
    return inside;
}
