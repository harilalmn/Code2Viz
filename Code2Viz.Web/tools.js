// Code2Viz Web - Drawing Tools, Snap Engine & Measuring Tape
// Tools: Pointer, Point, Line, Circle, Rectangle, Measuring

import { VPoint, VLine, VCircle, VRectangle, getRegistry, EPSILON } from './geometry/index.js';

// ============================================================================
// Snap Engine
// ============================================================================
export class SnapEngine {
    constructor(renderer) {
        this._renderer = renderer;
        this._enabled = true;
        this._gridSnap = true;
        this._endpointSnap = true;
        this._midpointSnap = true;
        this._centerSnap = true;
        this._intersectionSnap = false;
        this._gridSize = 10;
        this._snapRadius = 12; // pixels
        this._lastSnap = null;
    }

    get enabled() { return this._enabled; }
    set enabled(v) { this._enabled = v; }
    get gridSnap() { return this._gridSnap; }
    set gridSnap(v) { this._gridSnap = v; }
    get gridSize() { return this._gridSize; }
    set gridSize(v) { this._gridSize = v; }
    get lastSnap() { return this._lastSnap; }

    snap(worldX, worldY) {
        if (!this._enabled) {
            this._lastSnap = null;
            return { x: worldX, y: worldY, snapped: false };
        }

        const tolerance = this._snapRadius / this._renderer.zoom;
        let bestDist = tolerance;
        let bestPoint = null;
        let snapType = null;

        const shapes = getRegistry();

        // Endpoint snap
        if (this._endpointSnap) {
            for (const shape of shapes) {
                if (!shape.isVisible) continue;
                const endpoints = this._getEndpoints(shape);
                for (const ep of endpoints) {
                    const d = Math.sqrt((worldX - ep.x) ** 2 + (worldY - ep.y) ** 2);
                    if (d < bestDist) {
                        bestDist = d;
                        bestPoint = ep;
                        snapType = 'endpoint';
                    }
                }
            }
        }

        // Midpoint snap
        if (this._midpointSnap) {
            for (const shape of shapes) {
                if (!shape.isVisible) continue;
                const midpoints = this._getMidpoints(shape);
                for (const mp of midpoints) {
                    const d = Math.sqrt((worldX - mp.x) ** 2 + (worldY - mp.y) ** 2);
                    if (d < bestDist) {
                        bestDist = d;
                        bestPoint = mp;
                        snapType = 'midpoint';
                    }
                }
            }
        }

        // Center snap
        if (this._centerSnap) {
            for (const shape of shapes) {
                if (!shape.isVisible) continue;
                const center = this._getCenter(shape);
                if (center) {
                    const d = Math.sqrt((worldX - center.x) ** 2 + (worldY - center.y) ** 2);
                    if (d < bestDist) {
                        bestDist = d;
                        bestPoint = center;
                        snapType = 'center';
                    }
                }
            }
        }

        if (bestPoint) {
            this._lastSnap = { ...bestPoint, type: snapType };
            return { x: bestPoint.x, y: bestPoint.y, snapped: true, type: snapType };
        }

        // Grid snap
        if (this._gridSnap) {
            const gx = Math.round(worldX / this._gridSize) * this._gridSize;
            const gy = Math.round(worldY / this._gridSize) * this._gridSize;
            const d = Math.sqrt((worldX - gx) ** 2 + (worldY - gy) ** 2);
            if (d < tolerance) {
                this._lastSnap = { x: gx, y: gy, type: 'grid' };
                return { x: gx, y: gy, snapped: true, type: 'grid' };
            }
        }

        this._lastSnap = null;
        return { x: worldX, y: worldY, snapped: false };
    }

    _getEndpoints(shape) {
        const pts = [];
        const ox = shape.offsetX, oy = shape.offsetY;
        if (shape.start && shape.end) {
            pts.push({ x: shape.start.X + ox, y: shape.start.Y + oy });
            pts.push({ x: shape.end.X + ox, y: shape.end.Y + oy });
        }
        if (shape.points) {
            for (const p of shape.points) pts.push({ x: p.X + ox, y: p.Y + oy });
        }
        if (shape instanceof VPoint) {
            pts.push({ x: shape.X + ox, y: shape.Y + oy });
        }
        return pts;
    }

    _getMidpoints(shape) {
        if (shape.start && shape.end) {
            return [{ x: (shape.start.X + shape.end.X) / 2 + shape.offsetX, y: (shape.start.Y + shape.end.Y) / 2 + shape.offsetY }];
        }
        if (shape.points && shape.points.length >= 2) {
            const mids = [];
            for (let i = 0; i < shape.points.length - 1; i++) {
                const p1 = shape.points[i], p2 = shape.points[i + 1];
                mids.push({ x: (p1.X + p2.X) / 2 + shape.offsetX, y: (p1.Y + p2.Y) / 2 + shape.offsetY });
            }
            return mids;
        }
        return [];
    }

    _getCenter(shape) {
        const ox = shape.offsetX, oy = shape.offsetY;
        if (shape.center) return { x: shape.center.X + ox, y: shape.center.Y + oy };
        try {
            const bb = shape.getBounds();
            return { x: (bb.min.X + bb.max.X) / 2 + ox, y: (bb.min.Y + bb.max.Y) / 2 + oy };
        } catch (e) { return null; }
    }

    renderSnapIndicator(ctx) {
        if (!this._lastSnap) return;
        const s = this._renderer.worldToScreen(this._lastSnap.x, this._lastSnap.y);
        const type = this._lastSnap.type;

        ctx.lineWidth = 1.5;
        const size = 6;

        switch (type) {
            case 'endpoint':
                ctx.strokeStyle = '#a6e3a1';
                ctx.strokeRect(s.x - size, s.y - size, size * 2, size * 2);
                break;
            case 'midpoint':
                ctx.strokeStyle = '#f9e2af';
                ctx.beginPath();
                ctx.moveTo(s.x, s.y - size);
                ctx.lineTo(s.x + size, s.y + size);
                ctx.lineTo(s.x - size, s.y + size);
                ctx.closePath();
                ctx.stroke();
                break;
            case 'center':
                ctx.strokeStyle = '#f38ba8';
                ctx.beginPath();
                ctx.arc(s.x, s.y, size, 0, Math.PI * 2);
                ctx.stroke();
                ctx.beginPath();
                ctx.moveTo(s.x - 2, s.y); ctx.lineTo(s.x + 2, s.y);
                ctx.moveTo(s.x, s.y - 2); ctx.lineTo(s.x, s.y + 2);
                ctx.stroke();
                break;
            case 'grid':
                ctx.strokeStyle = '#89b4fa';
                ctx.beginPath();
                ctx.moveTo(s.x - size, s.y); ctx.lineTo(s.x + size, s.y);
                ctx.moveTo(s.x, s.y - size); ctx.lineTo(s.x, s.y + size);
                ctx.stroke();
                break;
        }
    }
}

// ============================================================================
// Tool Manager
// ============================================================================
export class ToolManager {
    constructor(renderer, snap, selectionManager, onShapeCreated) {
        this._renderer = renderer;
        this._snap = snap;
        this._selection = selectionManager;
        this._onShapeCreated = onShapeCreated;
        this._activeTool = 'pointer';
        this._toolState = {};
        this._preview = null; // preview shape data for rendering
        this._measuring = null; // measuring tape data
        this._onToolChange = null;
        this._orthoConstrain = false;
    }

    get activeTool() { return this._activeTool; }
    get preview() { return this._preview; }
    get measuring() { return this._measuring; }

    onToolChange(callback) { this._onToolChange = callback; }

    setTool(tool) {
        this._cancelCurrent();
        this._activeTool = tool;
        this._selection.isEnabled = (tool === 'pointer');
        this._renderer._canvas.style.cursor = tool === 'pointer' ? 'default' : 'crosshair';
        if (this._onToolChange) this._onToolChange(tool);
    }

    setOrthoConstrain(enabled) {
        this._orthoConstrain = enabled;
    }

    _cancelCurrent() {
        this._toolState = {};
        this._preview = null;
        this._measuring = null;
        this._renderer.render();
    }

    handleMouseDown(screenX, screenY, ctrlKey, shiftKey) {
        if (this._activeTool === 'pointer') return false;

        const rect = this._renderer._canvas.getBoundingClientRect();
        const sx = screenX - rect.left, sy = screenY - rect.top;
        const world = this._renderer.screenToWorld(sx, sy);
        const snapped = this._snap.snap(world.x, world.y);
        const pt = { x: snapped.x, y: snapped.y };

        switch (this._activeTool) {
            case 'point':
                this._createPoint(pt);
                return true;
            case 'line':
                return this._handleLineTool(pt);
            case 'circle':
                return this._handleCircleTool(pt);
            case 'rectangle':
                return this._handleRectangleTool(pt);
            case 'measure':
                return this._handleMeasureTool(pt);
        }
        return false;
    }

    handleMouseMove(screenX, screenY) {
        if (this._activeTool === 'pointer') return false;

        const rect = this._renderer._canvas.getBoundingClientRect();
        const sx = screenX - rect.left, sy = screenY - rect.top;
        const world = this._renderer.screenToWorld(sx, sy);
        const snapped = this._snap.snap(world.x, world.y);
        let pt = { x: snapped.x, y: snapped.y };

        if (this._orthoConstrain && this._toolState.p1) {
            pt = this._constrainOrtho(this._toolState.p1, pt);
        }

        switch (this._activeTool) {
            case 'line':
                if (this._toolState.p1) {
                    this._preview = { type: 'line', p1: this._toolState.p1, p2: pt };
                    this._renderer.render();
                    return true;
                }
                break;
            case 'circle':
                if (this._toolState.center) {
                    const r = Math.sqrt((pt.x - this._toolState.center.x) ** 2 + (pt.y - this._toolState.center.y) ** 2);
                    this._preview = { type: 'circle', center: this._toolState.center, radius: r };
                    this._renderer.render();
                    return true;
                }
                break;
            case 'rectangle':
                if (this._toolState.corner) {
                    this._preview = { type: 'rectangle', p1: this._toolState.corner, p2: pt };
                    this._renderer.render();
                    return true;
                }
                break;
            case 'measure':
                if (this._toolState.p1) {
                    const dx = pt.x - this._toolState.p1.x;
                    const dy = pt.y - this._toolState.p1.y;
                    const dist = Math.sqrt(dx * dx + dy * dy);
                    this._measuring = { p1: this._toolState.p1, p2: pt, distance: dist };
                    this._renderer.render();
                    return true;
                }
                break;
        }
        return false;
    }

    handleMouseUp() {
        // Most tools use click-click, not drag
        return false;
    }

    _constrainOrtho(p1, p2) {
        const dx = Math.abs(p2.x - p1.x);
        const dy = Math.abs(p2.y - p1.y);
        if (dx > dy) return { x: p2.x, y: p1.y };
        return { x: p1.x, y: p2.y };
    }

    _createPoint(pt) {
        const p = new VPoint(pt.x, pt.y);
        p.color = 'Yellow';
        if (this._onShapeCreated) this._onShapeCreated(p);
        this._renderer.render();
    }

    _handleLineTool(pt) {
        if (!this._toolState.p1) {
            this._toolState.p1 = pt;
            return true;
        }
        const line = new VLine(this._toolState.p1.x, this._toolState.p1.y, pt.x, pt.y);
        if (this._onShapeCreated) this._onShapeCreated(line);
        this._toolState = {};
        this._preview = null;
        this._renderer.render();
        return true;
    }

    _handleCircleTool(pt) {
        if (!this._toolState.center) {
            this._toolState.center = pt;
            return true;
        }
        const r = Math.sqrt((pt.x - this._toolState.center.x) ** 2 + (pt.y - this._toolState.center.y) ** 2);
        if (r > EPSILON) {
            const circle = new VCircle(this._toolState.center.x, this._toolState.center.y, r);
            if (this._onShapeCreated) this._onShapeCreated(circle);
        }
        this._toolState = {};
        this._preview = null;
        this._renderer.render();
        return true;
    }

    _handleRectangleTool(pt) {
        if (!this._toolState.corner) {
            this._toolState.corner = pt;
            return true;
        }
        const x = Math.min(this._toolState.corner.x, pt.x);
        const y = Math.min(this._toolState.corner.y, pt.y);
        const w = Math.abs(pt.x - this._toolState.corner.x);
        const h = Math.abs(pt.y - this._toolState.corner.y);
        if (w > EPSILON && h > EPSILON) {
            const rect = new VRectangle(x, y, w, h);
            if (this._onShapeCreated) this._onShapeCreated(rect);
        }
        this._toolState = {};
        this._preview = null;
        this._renderer.render();
        return true;
    }

    _handleMeasureTool(pt) {
        if (!this._toolState.p1) {
            this._toolState.p1 = pt;
            return true;
        }
        // Second click: keep the measurement visible and reset for new measurement
        this._toolState = {};
        return true;
    }

    // Render tool preview & measuring tape
    renderOverlay(ctx) {
        const renderer = this._renderer;

        // Draw tool preview
        if (this._preview) {
            ctx.strokeStyle = 'rgba(137, 180, 250, 0.8)';
            ctx.lineWidth = 1.5;
            ctx.setLineDash([6, 4]);

            switch (this._preview.type) {
                case 'line': {
                    const s1 = renderer.worldToScreen(this._preview.p1.x, this._preview.p1.y);
                    const s2 = renderer.worldToScreen(this._preview.p2.x, this._preview.p2.y);
                    ctx.beginPath();
                    ctx.moveTo(s1.x, s1.y);
                    ctx.lineTo(s2.x, s2.y);
                    ctx.stroke();
                    break;
                }
                case 'circle': {
                    const sc = renderer.worldToScreen(this._preview.center.x, this._preview.center.y);
                    const r = this._preview.radius * renderer.zoom;
                    ctx.beginPath();
                    ctx.arc(sc.x, sc.y, r, 0, Math.PI * 2);
                    ctx.stroke();
                    break;
                }
                case 'rectangle': {
                    const s1 = renderer.worldToScreen(this._preview.p1.x, this._preview.p1.y);
                    const s2 = renderer.worldToScreen(this._preview.p2.x, this._preview.p2.y);
                    ctx.strokeRect(Math.min(s1.x, s2.x), Math.min(s1.y, s2.y),
                        Math.abs(s2.x - s1.x), Math.abs(s2.y - s1.y));
                    break;
                }
            }
            ctx.setLineDash([]);
        }

        // Draw measuring tape
        if (this._measuring) {
            const m = this._measuring;
            const s1 = renderer.worldToScreen(m.p1.x, m.p1.y);
            const s2 = renderer.worldToScreen(m.p2.x, m.p2.y);

            // Line
            ctx.strokeStyle = '#fab387';
            ctx.lineWidth = 1.5;
            ctx.setLineDash([4, 4]);
            ctx.beginPath();
            ctx.moveTo(s1.x, s1.y);
            ctx.lineTo(s2.x, s2.y);
            ctx.stroke();
            ctx.setLineDash([]);

            // End markers
            ctx.fillStyle = '#fab387';
            ctx.beginPath();
            ctx.arc(s1.x, s1.y, 3, 0, Math.PI * 2);
            ctx.fill();
            ctx.beginPath();
            ctx.arc(s2.x, s2.y, 3, 0, Math.PI * 2);
            ctx.fill();

            // Distance label
            const mx = (s1.x + s2.x) / 2, my = (s1.y + s2.y) / 2;
            const distText = m.distance.toFixed(2);
            const dx = m.p2.x - m.p1.x, dy = m.p2.y - m.p1.y;
            const angleText = `${(Math.atan2(dy, dx) * 180 / Math.PI).toFixed(1)}°`;

            ctx.font = '12px Consolas, monospace';
            ctx.textAlign = 'center';
            ctx.textBaseline = 'bottom';

            // Background
            const text = `${distText}  ${angleText}`;
            const metrics = ctx.measureText(text);
            ctx.fillStyle = 'rgba(30, 30, 46, 0.85)';
            ctx.fillRect(mx - metrics.width / 2 - 4, my - 20, metrics.width + 8, 18);

            ctx.fillStyle = '#fab387';
            ctx.fillText(text, mx, my - 4);
        }

        // Snap indicator
        this._snap.renderSnapIndicator(ctx);
    }
}
