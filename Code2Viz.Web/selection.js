// Code2Viz Web - Selection & Interaction System
// Shape selection, multi-select, drag, and selection highlights

import { getRegistry, VPoint, VLine, VCircle, VArc, VRectangle, VEllipse,
         VPolygon, VPolyline, VSpline, VBezier, VArrow, VText, VDimension,
         VRadialDimension, VGroup, VGrid, GeometryHelper, EPSILON } from './geometry/index.js';

export class SelectionManager {
    constructor(renderer) {
        this._renderer = renderer;
        this._selected = new Set();
        this._hoveredShape = null;
        this._isDragging = false;
        this._dragStart = null;
        this._dragOffset = null;
        this._rubberBand = null; // { x1, y1, x2, y2 } in world coords
        this._isRubberBanding = false;
        this._hitTolerance = 8; // pixels
        this._onChange = null;
        this._enabled = true;
    }

    get selectedShapes() { return [...this._selected]; }
    get hasSelection() { return this._selected.size > 0; }
    get hoveredShape() { return this._hoveredShape; }
    get isEnabled() { return this._enabled; }
    set isEnabled(v) { this._enabled = v; }

    onChange(callback) { this._onChange = callback; }

    _notify() {
        if (this._onChange) this._onChange(this.selectedShapes);
    }

    selectShape(shape) {
        this._selected.add(shape);
        this._notify();
    }

    deselectShape(shape) {
        this._selected.delete(shape);
        this._notify();
    }

    deselectAll() {
        if (this._selected.size === 0) return;
        this._selected.clear();
        this._notify();
    }

    toggleSelection(shape) {
        if (this._selected.has(shape)) this._selected.delete(shape);
        else this._selected.add(shape);
        this._notify();
    }

    isSelected(shape) {
        return this._selected.has(shape);
    }

    // Hit test: find shape at screen position
    hitTest(screenX, screenY) {
        const world = this._renderer.screenToWorld(screenX, screenY);
        const tolerance = this._hitTolerance / this._renderer.zoom;
        const shapes = getRegistry();

        // Reverse order: shapes drawn later are "on top"
        for (let i = shapes.length - 1; i >= 0; i--) {
            const shape = shapes[i];
            if (!shape.isVisible || shape.drawFactor <= 0) continue;
            if (this._hitTestShape(shape, world, tolerance)) return shape;
        }
        return null;
    }

    _hitTestShape(shape, worldPt, tolerance) {
        if (shape instanceof VGroup) {
            for (let i = shape.shapes.length - 1; i >= 0; i--) {
                if (this._hitTestShape(shape.shapes[i], worldPt, tolerance)) return true;
            }
            return false;
        }

        if (shape instanceof VPoint) {
            const dx = worldPt.x - (shape.X + shape.offsetX);
            const dy = worldPt.y - (shape.Y + shape.offsetY);
            return Math.sqrt(dx * dx + dy * dy) <= tolerance + 4 / this._renderer.zoom;
        }

        if (shape instanceof VLine || shape instanceof VArrow) {
            const d = this._pointToSegmentDist(
                worldPt,
                { x: shape.start.X + shape.offsetX, y: shape.start.Y + shape.offsetY },
                { x: shape.end.X + shape.offsetX, y: shape.end.Y + shape.offsetY }
            );
            return d <= tolerance;
        }

        if (shape instanceof VCircle) {
            const dx = worldPt.x - (shape.center.X + shape.offsetX);
            const dy = worldPt.y - (shape.center.Y + shape.offsetY);
            const dist = Math.sqrt(dx * dx + dy * dy);
            // Hit if on stroke or inside fill
            if (shape.fillColor && shape.fillColor !== 'Transparent') {
                return dist <= shape.radius + tolerance;
            }
            return Math.abs(dist - shape.radius) <= tolerance;
        }

        if (shape instanceof VArc) {
            const dx = worldPt.x - (shape.center.X + shape.offsetX);
            const dy = worldPt.y - (shape.center.Y + shape.offsetY);
            const dist = Math.sqrt(dx * dx + dy * dy);
            if (Math.abs(dist - shape.radius) > tolerance) return false;
            let angle = Math.atan2(dy, dx) * 180 / Math.PI;
            if (angle < 0) angle += 360;
            const start = GeometryHelper.normalizeAngle(shape.startAngle);
            const end = GeometryHelper.normalizeAngle(shape.endAngle);
            if (start < end) return angle >= start - 1 && angle <= end + 1;
            return angle >= start - 1 || angle <= end + 1;
        }

        if (shape instanceof VEllipse) {
            const dx = (worldPt.x - (shape.center.X + shape.offsetX)) / shape.radiusX;
            const dy = (worldPt.y - (shape.center.Y + shape.offsetY)) / shape.radiusY;
            const dist = Math.sqrt(dx * dx + dy * dy);
            if (shape.fillColor && shape.fillColor !== 'Transparent') return dist <= 1 + tolerance / Math.min(shape.radiusX, shape.radiusY);
            return Math.abs(dist - 1) <= tolerance / Math.min(shape.radiusX, shape.radiusY);
        }

        if (shape instanceof VRectangle || shape instanceof VPolygon) {
            const pts = shape instanceof VRectangle ? shape.points : shape.points;
            const offset = { x: shape.offsetX, y: shape.offsetY };
            // Check fill
            if (shape.fillColor && shape.fillColor !== 'Transparent') {
                if (this._pointInPolygon(worldPt, pts, offset)) return true;
            }
            // Check edges
            for (let i = 0; i < pts.length; i++) {
                const p1 = pts[i], p2 = pts[(i + 1) % pts.length];
                const d = this._pointToSegmentDist(worldPt,
                    { x: p1.X + offset.x, y: p1.Y + offset.y },
                    { x: p2.X + offset.x, y: p2.Y + offset.y });
                if (d <= tolerance) return true;
            }
            return false;
        }

        if (shape instanceof VPolyline) {
            for (let i = 0; i < shape.points.length - 1; i++) {
                const p1 = shape.points[i], p2 = shape.points[i + 1];
                const d = this._pointToSegmentDist(worldPt,
                    { x: p1.X + shape.offsetX, y: p1.Y + shape.offsetY },
                    { x: p2.X + shape.offsetX, y: p2.Y + shape.offsetY });
                if (d <= tolerance) return true;
            }
            return false;
        }

        if (shape instanceof VSpline || shape instanceof VBezier) {
            const pts = shape.getRenderPoints ? shape.getRenderPoints() : [];
            for (let i = 0; i < pts.length - 1; i++) {
                const d = this._pointToSegmentDist(worldPt,
                    { x: pts[i].X + shape.offsetX, y: pts[i].Y + shape.offsetY },
                    { x: pts[i + 1].X + shape.offsetX, y: pts[i + 1].Y + shape.offsetY });
                if (d <= tolerance) return true;
            }
            return false;
        }

        if (shape instanceof VText) {
            // Rough bounding box hit test
            try {
                const bb = shape.getBounds();
                return worldPt.x >= bb.min.X + shape.offsetX - tolerance &&
                       worldPt.x <= bb.max.X + shape.offsetX + tolerance &&
                       worldPt.y >= bb.min.Y + shape.offsetY - tolerance &&
                       worldPt.y <= bb.max.Y + shape.offsetY + tolerance;
            } catch (e) { return false; }
        }

        // Fallback: bounding box test
        try {
            const bb = shape.getBounds();
            const expanded = {
                minX: bb.min.X + shape.offsetX - tolerance,
                minY: bb.min.Y + shape.offsetY - tolerance,
                maxX: bb.max.X + shape.offsetX + tolerance,
                maxY: bb.max.Y + shape.offsetY + tolerance
            };
            return worldPt.x >= expanded.minX && worldPt.x <= expanded.maxX &&
                   worldPt.y >= expanded.minY && worldPt.y <= expanded.maxY;
        } catch (e) { return false; }
    }

    _pointToSegmentDist(pt, a, b) {
        const dx = b.x - a.x, dy = b.y - a.y;
        const lenSq = dx * dx + dy * dy;
        if (lenSq < EPSILON) return Math.sqrt((pt.x - a.x) ** 2 + (pt.y - a.y) ** 2);
        const t = Math.max(0, Math.min(1, ((pt.x - a.x) * dx + (pt.y - a.y) * dy) / lenSq));
        const px = a.x + t * dx, py = a.y + t * dy;
        return Math.sqrt((pt.x - px) ** 2 + (pt.y - py) ** 2);
    }

    _pointInPolygon(pt, polygon, offset) {
        let inside = false;
        for (let i = 0, j = polygon.length - 1; i < polygon.length; j = i++) {
            const xi = polygon[i].X + offset.x, yi = polygon[i].Y + offset.y;
            const xj = polygon[j].X + offset.x, yj = polygon[j].Y + offset.y;
            if (((yi > pt.y) !== (yj > pt.y)) && (pt.x < (xj - xi) * (pt.y - yi) / (yj - yi) + xi)) {
                inside = !inside;
            }
        }
        return inside;
    }

    // Get shapes in rubber band rectangle
    getShapesInRect(x1, y1, x2, y2) {
        const minX = Math.min(x1, x2), maxX = Math.max(x1, x2);
        const minY = Math.min(y1, y2), maxY = Math.max(y1, y2);
        const result = [];
        for (const shape of getRegistry()) {
            if (!shape.isVisible) continue;
            try {
                const bb = shape.getBounds();
                if (bb.min.X + shape.offsetX >= minX && bb.max.X + shape.offsetX <= maxX &&
                    bb.min.Y + shape.offsetY >= minY && bb.max.Y + shape.offsetY <= maxY) {
                    result.push(shape);
                }
            } catch (e) { /* skip */ }
        }
        return result;
    }

    // Handle mouse events
    handleMouseDown(screenX, screenY, ctrlKey, shiftKey) {
        if (!this._enabled) return false;
        if (shiftKey) return false; // shift = pan

        const shape = this.hitTest(screenX, screenY);
        const world = this._renderer.screenToWorld(screenX, screenY);

        if (shape) {
            if (ctrlKey) {
                this.toggleSelection(shape);
            } else if (!this._selected.has(shape)) {
                this.deselectAll();
                this.selectShape(shape);
            }
            // Start drag
            this._isDragging = true;
            this._dragStart = { x: world.x, y: world.y };
            this._dragOffset = { x: 0, y: 0 };
            return true;
        } else if (!ctrlKey) {
            this.deselectAll();
            // Start rubber band
            this._isRubberBanding = true;
            this._rubberBand = { x1: world.x, y1: world.y, x2: world.x, y2: world.y };
            return true;
        }
        return false;
    }

    handleMouseMove(screenX, screenY) {
        if (!this._enabled) return false;

        if (this._isDragging && this._dragStart) {
            const world = this._renderer.screenToWorld(screenX, screenY);
            const dx = world.x - this._dragStart.x - this._dragOffset.x;
            const dy = world.y - this._dragStart.y - this._dragOffset.y;
            this._dragOffset.x += dx;
            this._dragOffset.y += dy;
            // Move selected shapes
            for (const shape of this._selected) {
                shape.offsetX += dx;
                shape.offsetY += dy;
            }
            this._renderer.render();
            return true;
        }

        if (this._isRubberBanding && this._rubberBand) {
            const world = this._renderer.screenToWorld(screenX, screenY);
            this._rubberBand.x2 = world.x;
            this._rubberBand.y2 = world.y;
            this._renderer.render();
            return true;
        }

        // Hover highlight
        const shape = this.hitTest(screenX, screenY);
        if (shape !== this._hoveredShape) {
            this._hoveredShape = shape;
            this._renderer._canvas.style.cursor = shape ? 'pointer' : 'default';
            this._renderer.render();
            return true;
        }
        return false;
    }

    handleMouseUp(screenX, screenY, ctrlKey) {
        if (this._isDragging) {
            this._isDragging = false;
            this._dragStart = null;
            this._dragOffset = null;
            this._notify();
            return true;
        }

        if (this._isRubberBanding && this._rubberBand) {
            const rb = this._rubberBand;
            this._isRubberBanding = false;
            this._rubberBand = null;
            const dx = Math.abs(rb.x2 - rb.x1), dy = Math.abs(rb.y2 - rb.y1);
            if (dx > 1 || dy > 1) {
                const shapes = this.getShapesInRect(rb.x1, rb.y1, rb.x2, rb.y2);
                if (!ctrlKey) this._selected.clear();
                for (const s of shapes) this._selected.add(s);
                this._notify();
            }
            this._renderer.render();
            return true;
        }
        return false;
    }

    // Render selection overlay
    renderOverlay(ctx) {
        if (!this._enabled) return;

        const renderer = this._renderer;

        // Hover highlight
        if (this._hoveredShape && !this._selected.has(this._hoveredShape)) {
            this._drawShapeHighlight(ctx, this._hoveredShape, 'rgba(137, 180, 250, 0.3)', 1);
        }

        // Selection highlights
        for (const shape of this._selected) {
            this._drawShapeHighlight(ctx, shape, 'rgba(137, 180, 250, 0.6)', 2);
            this._drawSelectionHandles(ctx, shape);
        }

        // Rubber band
        if (this._isRubberBanding && this._rubberBand) {
            const rb = this._rubberBand;
            const s1 = renderer.worldToScreen(rb.x1, rb.y1);
            const s2 = renderer.worldToScreen(rb.x2, rb.y2);
            ctx.strokeStyle = 'rgba(137, 180, 250, 0.8)';
            ctx.fillStyle = 'rgba(137, 180, 250, 0.1)';
            ctx.lineWidth = 1;
            ctx.setLineDash([4, 4]);
            const rx = Math.min(s1.x, s2.x), ry = Math.min(s1.y, s2.y);
            const rw = Math.abs(s2.x - s1.x), rh = Math.abs(s2.y - s1.y);
            ctx.fillRect(rx, ry, rw, rh);
            ctx.strokeRect(rx, ry, rw, rh);
            ctx.setLineDash([]);
        }
    }

    _drawShapeHighlight(ctx, shape, color, lineWidth) {
        try {
            const bb = shape.getBounds();
            const margin = 4 / this._renderer.zoom;
            const s1 = this._renderer.worldToScreen(bb.min.X + shape.offsetX - margin, bb.max.Y + shape.offsetY + margin);
            const s2 = this._renderer.worldToScreen(bb.max.X + shape.offsetX + margin, bb.min.Y + shape.offsetY - margin);
            ctx.strokeStyle = color;
            ctx.lineWidth = lineWidth;
            ctx.setLineDash([3, 3]);
            ctx.strokeRect(s1.x, s1.y, s2.x - s1.x, s2.y - s1.y);
            ctx.setLineDash([]);
        } catch (e) { /* skip shapes without bounds */ }
    }

    _drawSelectionHandles(ctx, shape) {
        try {
            const bb = shape.getBounds();
            const margin = 4 / this._renderer.zoom;
            const corners = [
                { x: bb.min.X + shape.offsetX - margin, y: bb.max.Y + shape.offsetY + margin },
                { x: bb.max.X + shape.offsetX + margin, y: bb.max.Y + shape.offsetY + margin },
                { x: bb.max.X + shape.offsetX + margin, y: bb.min.Y + shape.offsetY - margin },
                { x: bb.min.X + shape.offsetX - margin, y: bb.min.Y + shape.offsetY - margin },
            ];
            ctx.fillStyle = '#89b4fa';
            ctx.strokeStyle = '#1e1e2e';
            ctx.lineWidth = 1;
            for (const c of corners) {
                const s = this._renderer.worldToScreen(c.x, c.y);
                ctx.fillRect(s.x - 3, s.y - 3, 6, 6);
                ctx.strokeRect(s.x - 3, s.y - 3, 6, 6);
            }
        } catch (e) { /* skip */ }
    }

    deleteSelected() {
        for (const shape of this._selected) {
            shape.remove();
        }
        this._selected.clear();
        this._notify();
        this._renderer.render();
    }
}
