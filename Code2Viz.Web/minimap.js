// Code2Viz Web - Minimap Widget
// Shows a scaled-down view of all shapes with viewport indicator

import { getRegistry, VPoint, VLine, VCircle, VArc, VRectangle, VEllipse,
         VPolygon, VPolyline, VSpline, VBezier, VArrow, VText,
         VGroup, VGrid } from './geometry/index.js';

export class Minimap {
    constructor(renderer, containerEl) {
        this._renderer = renderer;
        this._container = containerEl;
        this._visible = false;

        // Create minimap canvas
        this._canvas = document.createElement('canvas');
        this._canvas.className = 'minimap-canvas';
        this._canvas.width = 180;
        this._canvas.height = 120;
        this._ctx = this._canvas.getContext('2d');

        this._container.appendChild(this._canvas);

        this._sceneBounds = null;
        this._setupInteraction();
    }

    get visible() { return this._visible; }
    set visible(v) {
        this._visible = v;
        this._container.style.display = v ? 'block' : 'none';
        if (v) this.render();
    }

    toggle() {
        this.visible = !this._visible;
    }

    _setupInteraction() {
        let isDragging = false;

        const navigate = (e) => {
            const rect = this._canvas.getBoundingClientRect();
            const mx = e.clientX - rect.left;
            const my = e.clientY - rect.top;
            if (!this._sceneBounds) return;

            const sb = this._sceneBounds;
            const sw = sb.maxX - sb.minX || 1;
            const sh = sb.maxY - sb.minY || 1;

            const scaleX = this._canvas.width / sw;
            const scaleY = this._canvas.height / sh;
            const scale = Math.min(scaleX, scaleY) * 0.9;

            const offsetX = (this._canvas.width - sw * scale) / 2;
            const offsetY = (this._canvas.height - sh * scale) / 2;

            const worldX = (mx - offsetX) / scale + sb.minX;
            const worldY = sb.maxY - (my - offsetY) / scale;

            this._renderer._panX = -worldX;
            this._renderer._panY = -worldY;
            this._renderer.render();
            this.render();
        };

        this._canvas.addEventListener('mousedown', (e) => {
            isDragging = true;
            navigate(e);
            e.preventDefault();
            e.stopPropagation();
        });

        this._canvas.addEventListener('mousemove', (e) => {
            if (isDragging) {
                navigate(e);
                e.preventDefault();
            }
        });

        window.addEventListener('mouseup', () => { isDragging = false; });
    }

    render() {
        if (!this._visible) return;
        const ctx = this._ctx;
        const w = this._canvas.width;
        const h = this._canvas.height;

        // Clear
        ctx.fillStyle = '#11111b';
        ctx.fillRect(0, 0, w, h);

        // Compute scene bounds
        const shapes = getRegistry();
        let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;
        for (const s of shapes) {
            if (!s.isVisible) continue;
            try {
                const bb = s.getBounds();
                if (bb.min.X < minX) minX = bb.min.X;
                if (bb.min.Y < minY) minY = bb.min.Y;
                if (bb.max.X > maxX) maxX = bb.max.X;
                if (bb.max.Y > maxY) maxY = bb.max.Y;
            } catch (e) { /* skip */ }
        }

        if (!isFinite(minX)) {
            minX = -100; minY = -100; maxX = 100; maxY = 100;
        }

        // Add padding
        const pad = Math.max(maxX - minX, maxY - minY) * 0.1 || 10;
        minX -= pad; minY -= pad; maxX += pad; maxY += pad;
        this._sceneBounds = { minX, minY, maxX, maxY };

        const sw = maxX - minX;
        const sh = maxY - minY;
        const scaleX = w / sw;
        const scaleY = h / sh;
        const scale = Math.min(scaleX, scaleY) * 0.9;
        const offsetX = (w - sw * scale) / 2;
        const offsetY = (h - sh * scale) / 2;

        const tx = (wx) => (wx - minX) * scale + offsetX;
        const ty = (wy) => (maxY - wy) * scale + offsetY; // flip Y

        // Draw shapes (simplified)
        ctx.lineWidth = 1;
        for (const s of shapes) {
            if (!s.isVisible || s.drawFactor <= 0) continue;
            this._drawShapeMini(ctx, s, tx, ty, scale);
        }

        // Draw viewport rectangle
        const mainCanvas = this._renderer._canvas;
        const tl = this._renderer.screenToWorld(0, 0);
        const br = this._renderer.screenToWorld(mainCanvas.width, mainCanvas.height);
        const vx1 = tx(tl.x), vy1 = ty(tl.y);
        const vx2 = tx(br.x), vy2 = ty(br.y);

        ctx.strokeStyle = 'rgba(137, 180, 250, 0.8)';
        ctx.lineWidth = 1.5;
        ctx.strokeRect(vx1, vy1, vx2 - vx1, vy2 - vy1);
        ctx.fillStyle = 'rgba(137, 180, 250, 0.05)';
        ctx.fillRect(vx1, vy1, vx2 - vx1, vy2 - vy1);

        // Border
        ctx.strokeStyle = '#45475a';
        ctx.lineWidth = 1;
        ctx.strokeRect(0, 0, w, h);
    }

    _drawShapeMini(ctx, shape, tx, ty, scale) {
        const color = shape.color || 'White';
        ctx.strokeStyle = color;
        ctx.globalAlpha = shape.opacity * 0.8;

        if (shape instanceof VGroup) {
            for (const child of shape.shapes) {
                if (child.isVisible) this._drawShapeMini(ctx, child, tx, ty, scale);
            }
            ctx.globalAlpha = 1;
            return;
        }

        if (shape instanceof VPoint) {
            ctx.fillStyle = color;
            ctx.fillRect(tx(shape.X) - 1, ty(shape.Y) - 1, 2, 2);
        } else if (shape instanceof VLine || shape instanceof VArrow) {
            ctx.beginPath();
            ctx.moveTo(tx(shape.start.X), ty(shape.start.Y));
            ctx.lineTo(tx(shape.end.X), ty(shape.end.Y));
            ctx.stroke();
        } else if (shape instanceof VCircle) {
            ctx.beginPath();
            ctx.arc(tx(shape.center.X), ty(shape.center.Y), shape.radius * scale, 0, Math.PI * 2);
            ctx.stroke();
        } else if (shape instanceof VRectangle || shape instanceof VPolygon) {
            const pts = shape instanceof VRectangle ? shape.points : shape.points;
            if (pts.length >= 2) {
                ctx.beginPath();
                ctx.moveTo(tx(pts[0].X), ty(pts[0].Y));
                for (let i = 1; i < pts.length; i++) ctx.lineTo(tx(pts[i].X), ty(pts[i].Y));
                ctx.closePath();
                ctx.stroke();
            }
        } else if (shape instanceof VPolyline) {
            if (shape.points.length >= 2) {
                ctx.beginPath();
                ctx.moveTo(tx(shape.points[0].X), ty(shape.points[0].Y));
                for (let i = 1; i < shape.points.length; i++) ctx.lineTo(tx(shape.points[i].X), ty(shape.points[i].Y));
                ctx.stroke();
            }
        } else if (shape instanceof VEllipse) {
            ctx.beginPath();
            ctx.ellipse(tx(shape.center.X), ty(shape.center.Y), shape.radiusX * scale, shape.radiusY * scale, 0, 0, Math.PI * 2);
            ctx.stroke();
        } else if (shape instanceof VArc) {
            const startRad = -shape.endAngle * Math.PI / 180;
            const endRad = -shape.startAngle * Math.PI / 180;
            ctx.beginPath();
            ctx.arc(tx(shape.center.X), ty(shape.center.Y), shape.radius * scale, startRad, endRad);
            ctx.stroke();
        }

        ctx.globalAlpha = 1;
    }
}
