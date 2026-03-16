// Code2Viz Web - Array Operations
// Clone shapes in linear, rectangular, and polar patterns

import { VPoint, getRegistry, Shape, EPSILON } from './index.js';

function cloneShape(shape) {
    if (shape.clone) {
        const c = shape.clone();
        return c;
    }
    return null;
}

/**
 * Create a linear array of copies along a direction
 * @param {Shape} shape - Source shape to clone
 * @param {number} count - Number of copies (including original)
 * @param {number} dx - Spacing in X
 * @param {number} dy - Spacing in Y
 * @returns {Shape[]} Array of cloned shapes
 */
export function linearArray(shape, count, dx, dy) {
    const results = [];
    for (let i = 1; i < count; i++) {
        const c = cloneShape(shape);
        if (c) {
            c.move({ X: dx * i, Y: dy * i });
            results.push(c);
        }
    }
    return results;
}

/**
 * Create a rectangular grid array
 * @param {Shape} shape - Source shape to clone
 * @param {number} rows - Number of rows
 * @param {number} cols - Number of columns
 * @param {number} dx - Column spacing
 * @param {number} dy - Row spacing
 * @returns {Shape[]} Array of cloned shapes
 */
export function rectangularArray(shape, rows, cols, dx, dy) {
    const results = [];
    for (let r = 0; r < rows; r++) {
        for (let c = 0; c < cols; c++) {
            if (r === 0 && c === 0) continue; // skip original
            const clone = cloneShape(shape);
            if (clone) {
                clone.move({ X: dx * c, Y: dy * r });
                results.push(clone);
            }
        }
    }
    return results;
}

/**
 * Create a polar (circular) array of copies around a center point
 * @param {Shape} shape - Source shape to clone
 * @param {number} count - Total number of copies (including original)
 * @param {VPoint|{X:number,Y:number}} center - Center of rotation
 * @param {number} totalAngle - Total angle span in degrees (360 for full circle)
 * @returns {Shape[]} Array of cloned shapes
 */
export function polarArray(shape, count, center, totalAngle = 360) {
    const results = [];
    const angleStep = totalAngle / count;
    for (let i = 1; i < count; i++) {
        const c = cloneShape(shape);
        if (c) {
            c.rotate(center, angleStep * i);
            results.push(c);
        }
    }
    return results;
}

/**
 * Create copies along a path (polyline/spline)
 * @param {Shape} shape - Source shape to clone
 * @param {object} path - Path with divide() method
 * @param {number} count - Number of copies
 * @returns {Shape[]} Array of cloned shapes at path points
 */
export function pathArray(shape, path, count) {
    if (!path.divide) return [];
    const pts = path.divide(count - 1);
    const origin = pts[0];
    const results = [];
    for (let i = 1; i < pts.length; i++) {
        const c = cloneShape(shape);
        if (c) {
            c.move({ X: pts[i].X - origin.X, Y: pts[i].Y - origin.Y });
            results.push(c);
        }
    }
    return results;
}

/**
 * Mirror a shape across a line
 * @param {Shape} shape - Source shape
 * @param {VLine} mirrorLine - Mirror axis line
 * @returns {Shape} Mirrored copy
 */
export function mirror(shape, mirrorLine) {
    const c = cloneShape(shape);
    if (c && c.flip) {
        c.flip(mirrorLine);
    }
    return c;
}
