// Canvas Renderer for Code2Viz Web
// Renders shapes from the geometry registry onto an HTML5 Canvas
// Uses mathematical coordinate system (Y-up, origin at center)

import { getRegistry, LineTypes, VPoint, VLine, VCircle, VArc, VRectangle, VEllipse,
         VPolygon, VPolyline, VSpline, VBezier, VArrow, VText, VDimension,
         VRadialDimension, VGroup, VGrid, EPSILON } from './geometry/index.js';

export class CanvasRenderer {
    constructor(canvas) {
        this._canvas = canvas;
        this._ctx = canvas.getContext('2d');
        this._panX = 0;
        this._panY = 0;
        this._zoom = 1.0;
        this._bgColor = '#1e1e2e';
        this._gridColor = '#2a2a3a';
        this._axisColor = '#3a3a5a';
        this._showGrid = true;
        this._gridSpacing = 50;

        // Overlay renderers (selection, tools, minimap)
        this._overlays = [];
        this._layerManager = null;

        this._setupInteraction();
    }

    get zoom() { return this._zoom; }
    get panX() { return this._panX; }
    get panY() { return this._panY; }

    setLayerManager(lm) { this._layerManager = lm; }

    addOverlay(overlayFn) {
        this._overlays.push(overlayFn);
    }

    // World (math) to screen coordinates
    worldToScreen(wx, wy) {
        const cx = this._canvas.width / 2;
        const cy = this._canvas.height / 2;
        return {
            x: cx + (wx + this._panX) * this._zoom,
            y: cy - (wy + this._panY) * this._zoom  // Y inverted
        };
    }

    // Screen to world coordinates
    screenToWorld(sx, sy) {
        const cx = this._canvas.width / 2;
        const cy = this._canvas.height / 2;
        return {
            x: (sx - cx) / this._zoom - this._panX,
            y: -(sy - cy) / this._zoom - this._panY
        };
    }

    _setupInteraction() {
        let isPanning = false, lastX = 0, lastY = 0;

        this._canvas.addEventListener('mousedown', (e) => {
            if (e.button === 1 || (e.button === 0 && e.shiftKey)) {
                isPanning = true;
                lastX = e.clientX;
                lastY = e.clientY;
                this._canvas.style.cursor = 'grabbing';
                e.preventDefault();
            }
        });

        this._canvas.addEventListener('mousemove', (e) => {
            if (isPanning) {
                const dx = (e.clientX - lastX) / this._zoom;
                const dy = (e.clientY - lastY) / this._zoom;
                this._panX += dx;
                this._panY -= dy;  // Y inverted
                lastX = e.clientX;
                lastY = e.clientY;
                this.render();
            }

            // Update coord display
            const rect = this._canvas.getBoundingClientRect();
            const world = this.screenToWorld(e.clientX - rect.left, e.clientY - rect.top);
            if (this._onCoordsUpdate) {
                this._onCoordsUpdate(world.x, world.y);
            }
        });

        window.addEventListener('mouseup', () => {
            if (isPanning) {
                isPanning = false;
                this._canvas.style.cursor = 'default';
            }
        });

        this._canvas.addEventListener('wheel', (e) => {
            e.preventDefault();
            const rect = this._canvas.getBoundingClientRect();
            const mx = e.clientX - rect.left;
            const my = e.clientY - rect.top;

            const worldBefore = this.screenToWorld(mx, my);
            const factor = e.deltaY < 0 ? 1.1 : 0.9;
            this._zoom *= factor;
            this._zoom = Math.max(0.01, Math.min(100, this._zoom));
            const worldAfter = this.screenToWorld(mx, my);

            this._panX += worldAfter.x - worldBefore.x;
            this._panY += worldAfter.y - worldBefore.y;
            this.render();
        }, { passive: false });
    }

    onCoordsUpdate(callback) { this._onCoordsUpdate = callback; }

    zoomToFit() {
        const shapes = getRegistry();
        if (shapes.length === 0) {
            this._panX = 0; this._panY = 0; this._zoom = 1.0;
            this.render();
            return;
        }

        let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;
        for (const shape of shapes) {
            if (!shape.isVisible) continue;
            try {
                const bb = shape.getBounds();
                if (bb.min.X < minX) minX = bb.min.X;
                if (bb.min.Y < minY) minY = bb.min.Y;
                if (bb.max.X > maxX) maxX = bb.max.X;
                if (bb.max.Y > maxY) maxY = bb.max.Y;
            } catch (e) { /* skip shapes without bounds */ }
        }

        if (!isFinite(minX)) { this._panX = 0; this._panY = 0; this._zoom = 1.0; this.render(); return; }

        const w = maxX - minX || 1;
        const h = maxY - minY || 1;
        const cx = (minX + maxX) / 2;
        const cy = (minY + maxY) / 2;
        const padding = 60;

        this._zoom = Math.min(
            (this._canvas.width - padding * 2) / w,
            (this._canvas.height - padding * 2) / h
        );
        this._zoom = Math.max(0.01, Math.min(100, this._zoom));
        this._panX = -cx;
        this._panY = -cy;
        this.render();
    }

    resize(width, height) {
        this._canvas.width = width;
        this._canvas.height = height;
        this.render();
    }

    render() {
        const ctx = this._ctx;
        const w = this._canvas.width;
        const h = this._canvas.height;

        // Clear background
        ctx.fillStyle = this._bgColor;
        ctx.fillRect(0, 0, w, h);

        // Draw grid
        if (this._showGrid) this._drawGrid(ctx, w, h);

        // Draw all registered shapes
        const shapes = getRegistry();
        for (const shape of shapes) {
            if (!shape.isVisible || shape.drawFactor <= 0) continue;
            // Layer visibility check
            if (this._layerManager && !this._layerManager.isShapeVisible(shape)) continue;
            ctx.save();
            ctx.globalAlpha = shape.opacity;
            this._drawShape(ctx, shape);
            ctx.restore();
        }

        // Draw overlays (selection, tools, snap, minimap)
        for (const overlay of this._overlays) {
            ctx.save();
            overlay(ctx);
            ctx.restore();
        }
    }

    _drawGrid(ctx, w, h) {
        const tl = this.screenToWorld(0, 0);
        const br = this.screenToWorld(w, h);

        // Adaptive grid spacing
        let spacing = this._gridSpacing;
        while (spacing * this._zoom < 20) spacing *= 5;
        while (spacing * this._zoom > 200) spacing /= 5;

        ctx.lineWidth = 0.5;
        ctx.strokeStyle = this._gridColor;
        ctx.beginPath();

        const startX = Math.floor(tl.x / spacing) * spacing;
        const startY = Math.floor(br.y / spacing) * spacing;

        for (let x = startX; x <= br.x; x += spacing) {
            const s = this.worldToScreen(x, 0);
            ctx.moveTo(s.x, 0);
            ctx.lineTo(s.x, h);
        }
        for (let y = startY; y <= tl.y; y += spacing) {
            const s = this.worldToScreen(0, y);
            ctx.moveTo(0, s.y);
            ctx.lineTo(w, s.y);
        }
        ctx.stroke();

        // Draw axes
        ctx.lineWidth = 1;
        ctx.strokeStyle = this._axisColor;
        ctx.beginPath();
        const ox = this.worldToScreen(0, 0);
        ctx.moveTo(ox.x, 0); ctx.lineTo(ox.x, h);
        ctx.moveTo(0, ox.y); ctx.lineTo(w, ox.y);
        ctx.stroke();
    }

    _resolveColor(colorStr) {
        if (!colorStr || colorStr === 'Transparent') return null;
        return colorStr;
    }

    _applyLineType(ctx, lineType, lineTypeScale) {
        const pattern = LineTypes[lineType] || [];
        if (pattern.length > 0) {
            ctx.setLineDash(pattern.map(v => v * (lineTypeScale || 1)));
        } else {
            ctx.setLineDash([]);
        }
    }

    _drawShape(ctx, shape) {
        if (shape instanceof VGroup) {
            for (const child of shape.shapes) {
                if (child.isVisible && child.drawFactor > 0) {
                    ctx.save();
                    ctx.globalAlpha *= child.opacity;
                    this._drawShape(ctx, child);
                    ctx.restore();
                }
            }
            return;
        }

        if (shape instanceof VGrid) { this._drawGrid2(ctx, shape); return; }
        if (shape instanceof VPoint) { this._drawPoint(ctx, shape); return; }
        if (shape instanceof VLine) { this._drawLine(ctx, shape); return; }
        if (shape instanceof VCircle) { this._drawCircle(ctx, shape); return; }
        if (shape instanceof VArc) { this._drawArc(ctx, shape); return; }
        if (shape instanceof VRectangle) { this._drawRectangle(ctx, shape); return; }
        if (shape instanceof VEllipse) { this._drawEllipse(ctx, shape); return; }
        if (shape instanceof VPolygon) { this._drawPolygon(ctx, shape); return; }
        if (shape instanceof VPolyline) { this._drawPolyline(ctx, shape); return; }
        if (shape instanceof VSpline) { this._drawSpline(ctx, shape); return; }
        if (shape instanceof VBezier) { this._drawBezier(ctx, shape); return; }
        if (shape instanceof VArrow) { this._drawArrow(ctx, shape); return; }
        if (shape instanceof VText) { this._drawText(ctx, shape); return; }
        if (shape instanceof VDimension) { this._drawDimension(ctx, shape); return; }
        if (shape instanceof VRadialDimension) { this._drawRadialDimension(ctx, shape); return; }
    }

    _drawPoint(ctx, point) {
        const s = this.worldToScreen(point.X + point.offsetX, point.Y + point.offsetY);
        const r = 4;
        const color = this._resolveColor(point.color) || '#FFFFFF';

        ctx.fillStyle = color;
        ctx.beginPath();
        ctx.arc(s.x, s.y, r, 0, Math.PI * 2);
        ctx.fill();

        // Cross marker
        ctx.strokeStyle = color;
        ctx.lineWidth = 1.5;
        ctx.beginPath();
        ctx.moveTo(s.x - r - 1, s.y); ctx.lineTo(s.x + r + 1, s.y);
        ctx.moveTo(s.x, s.y - r - 1); ctx.lineTo(s.x, s.y + r + 1);
        ctx.stroke();
    }

    _drawLine(ctx, shape) {
        const s1 = this.worldToScreen(shape.start.X + shape.offsetX, shape.start.Y + shape.offsetY);
        const s2 = this.worldToScreen(shape.end.X + shape.offsetX, shape.end.Y + shape.offsetY);

        const strokeColor = this._resolveColor(shape.color);
        if (strokeColor) {
            ctx.strokeStyle = strokeColor;
            ctx.lineWidth = shape.lineWeight;
            this._applyLineType(ctx, shape.lineType, shape.lineTypeScale);
            ctx.beginPath();

            if (shape.drawFactor < 1) {
                const dx = s2.x - s1.x, dy = s2.y - s1.y;
                ctx.moveTo(s1.x, s1.y);
                ctx.lineTo(s1.x + dx * shape.drawFactor, s1.y + dy * shape.drawFactor);
            } else {
                ctx.moveTo(s1.x, s1.y);
                ctx.lineTo(s2.x, s2.y);
            }
            ctx.stroke();
            ctx.setLineDash([]);
        }
    }

    _drawCircle(ctx, shape) {
        const s = this.worldToScreen(shape.center.X + shape.offsetX, shape.center.Y + shape.offsetY);
        const r = shape.radius * this._zoom;

        // Fill
        const fillColor = this._resolveColor(shape.fillColor);
        if (fillColor) {
            ctx.fillStyle = fillColor;
            ctx.beginPath();
            ctx.arc(s.x, s.y, r, 0, Math.PI * 2);
            ctx.fill();
        }

        // Stroke
        const strokeColor = this._resolveColor(shape.color);
        if (strokeColor) {
            ctx.strokeStyle = strokeColor;
            ctx.lineWidth = shape.lineWeight;
            this._applyLineType(ctx, shape.lineType, shape.lineTypeScale);
            ctx.beginPath();
            if (shape.drawFactor < 1) {
                ctx.arc(s.x, s.y, r, 0, Math.PI * 2 * shape.drawFactor);
            } else {
                ctx.arc(s.x, s.y, r, 0, Math.PI * 2);
            }
            ctx.stroke();
            ctx.setLineDash([]);
        }
    }

    _drawArc(ctx, shape) {
        const s = this.worldToScreen(shape.center.X + shape.offsetX, shape.center.Y + shape.offsetY);
        const r = shape.radius * this._zoom;
        const startRad = -shape.endAngle * Math.PI / 180;
        const endRad = -shape.startAngle * Math.PI / 180;

        const strokeColor = this._resolveColor(shape.color);
        if (strokeColor) {
            ctx.strokeStyle = strokeColor;
            ctx.lineWidth = shape.lineWeight;
            this._applyLineType(ctx, shape.lineType, shape.lineTypeScale);
            ctx.beginPath();
            if (shape.drawFactor < 1) {
                const sweep = endRad - startRad;
                ctx.arc(s.x, s.y, r, startRad, startRad + sweep * shape.drawFactor);
            } else {
                ctx.arc(s.x, s.y, r, startRad, endRad);
            }
            ctx.stroke();
            ctx.setLineDash([]);
        }
    }

    _drawRectangle(ctx, shape) {
        const pts = shape.points;
        this._drawPolygonPoints(ctx, pts, shape);
    }

    _drawEllipse(ctx, shape) {
        const s = this.worldToScreen(shape.center.X + shape.offsetX, shape.center.Y + shape.offsetY);
        const rx = shape.radiusX * this._zoom;
        const ry = shape.radiusY * this._zoom;

        const fillColor = this._resolveColor(shape.fillColor);
        if (fillColor) {
            ctx.fillStyle = fillColor;
            ctx.beginPath();
            ctx.ellipse(s.x, s.y, rx, ry, 0, 0, Math.PI * 2);
            ctx.fill();
        }

        const strokeColor = this._resolveColor(shape.color);
        if (strokeColor) {
            ctx.strokeStyle = strokeColor;
            ctx.lineWidth = shape.lineWeight;
            this._applyLineType(ctx, shape.lineType, shape.lineTypeScale);
            ctx.beginPath();
            ctx.ellipse(s.x, s.y, rx, ry, 0, 0, Math.PI * 2);
            ctx.stroke();
            ctx.setLineDash([]);
        }
    }

    _drawPolygon(ctx, shape) {
        this._drawPolygonPoints(ctx, shape.points, shape);
    }

    _drawPolygonPoints(ctx, pts, shape) {
        if (pts.length < 2) return;
        const screenPts = pts.map(p => this.worldToScreen(p.X + shape.offsetX, p.Y + shape.offsetY));

        // Fill
        const fillColor = this._resolveColor(shape.fillColor);
        if (fillColor) {
            ctx.fillStyle = fillColor;
            ctx.beginPath();
            ctx.moveTo(screenPts[0].x, screenPts[0].y);
            for (let i = 1; i < screenPts.length; i++) ctx.lineTo(screenPts[i].x, screenPts[i].y);
            ctx.closePath();
            ctx.fill();
        }

        // Stroke
        const strokeColor = this._resolveColor(shape.color);
        if (strokeColor) {
            ctx.strokeStyle = strokeColor;
            ctx.lineWidth = shape.lineWeight;
            this._applyLineType(ctx, shape.lineType, shape.lineTypeScale);
            ctx.beginPath();
            ctx.moveTo(screenPts[0].x, screenPts[0].y);
            for (let i = 1; i < screenPts.length; i++) ctx.lineTo(screenPts[i].x, screenPts[i].y);
            ctx.closePath();
            ctx.stroke();
            ctx.setLineDash([]);
        }
    }

    _drawPolyline(ctx, shape) {
        if (shape.points.length < 2) return;
        const screenPts = shape.points.map(p => this.worldToScreen(p.X + shape.offsetX, p.Y + shape.offsetY));

        const strokeColor = this._resolveColor(shape.color);
        if (strokeColor) {
            ctx.strokeStyle = strokeColor;
            ctx.lineWidth = shape.lineWeight;
            this._applyLineType(ctx, shape.lineType, shape.lineTypeScale);
            ctx.beginPath();
            ctx.moveTo(screenPts[0].x, screenPts[0].y);

            const drawCount = shape.drawFactor < 1 ? Math.ceil(screenPts.length * shape.drawFactor) : screenPts.length;
            for (let i = 1; i < drawCount; i++) ctx.lineTo(screenPts[i].x, screenPts[i].y);
            ctx.stroke();
            ctx.setLineDash([]);
        }
    }

    _drawSpline(ctx, shape) {
        const pts = shape.getRenderPoints();
        if (pts.length < 2) return;
        const screenPts = pts.map(p => this.worldToScreen(p.X + shape.offsetX, p.Y + shape.offsetY));

        const strokeColor = this._resolveColor(shape.color);
        if (strokeColor) {
            ctx.strokeStyle = strokeColor;
            ctx.lineWidth = shape.lineWeight;
            this._applyLineType(ctx, shape.lineType, shape.lineTypeScale);
            ctx.beginPath();
            ctx.moveTo(screenPts[0].x, screenPts[0].y);
            const drawCount = shape.drawFactor < 1 ? Math.ceil(screenPts.length * shape.drawFactor) : screenPts.length;
            for (let i = 1; i < drawCount; i++) ctx.lineTo(screenPts[i].x, screenPts[i].y);
            ctx.stroke();
            ctx.setLineDash([]);
        }
    }

    _drawBezier(ctx, shape) {
        const p0 = this.worldToScreen(shape.p0.X + shape.offsetX, shape.p0.Y + shape.offsetY);
        const p1 = this.worldToScreen(shape.p1.X + shape.offsetX, shape.p1.Y + shape.offsetY);
        const p2 = this.worldToScreen(shape.p2.X + shape.offsetX, shape.p2.Y + shape.offsetY);
        const p3 = this.worldToScreen(shape.p3.X + shape.offsetX, shape.p3.Y + shape.offsetY);

        const strokeColor = this._resolveColor(shape.color);
        if (strokeColor) {
            ctx.strokeStyle = strokeColor;
            ctx.lineWidth = shape.lineWeight;
            this._applyLineType(ctx, shape.lineType, shape.lineTypeScale);
            ctx.beginPath();
            ctx.moveTo(p0.x, p0.y);
            ctx.bezierCurveTo(p1.x, p1.y, p2.x, p2.y, p3.x, p3.y);
            ctx.stroke();
            ctx.setLineDash([]);
        }
    }

    _drawArrow(ctx, shape) {
        const s1 = this.worldToScreen(shape.start.X + shape.offsetX, shape.start.Y + shape.offsetY);
        const s2 = this.worldToScreen(shape.end.X + shape.offsetX, shape.end.Y + shape.offsetY);

        const strokeColor = this._resolveColor(shape.color) || '#FFFFFF';
        ctx.strokeStyle = strokeColor;
        ctx.fillStyle = strokeColor;
        ctx.lineWidth = shape.lineWeight;

        // Line
        ctx.beginPath();
        ctx.moveTo(s1.x, s1.y);
        ctx.lineTo(s2.x, s2.y);
        ctx.stroke();

        // End arrowhead
        const ah = shape.getEndArrowhead();
        const w1 = this.worldToScreen(ah.wing1.X + shape.offsetX, ah.wing1.Y + shape.offsetY);
        const w2 = this.worldToScreen(ah.wing2.X + shape.offsetX, ah.wing2.Y + shape.offsetY);
        ctx.beginPath();
        ctx.moveTo(s2.x, s2.y);
        ctx.lineTo(w1.x, w1.y);
        ctx.lineTo(w2.x, w2.y);
        ctx.closePath();
        ctx.fill();

        // Start arrowhead
        if (shape.doubleEnded) {
            const sah = shape.getStartArrowhead();
            const sw1 = this.worldToScreen(sah.wing1.X + shape.offsetX, sah.wing1.Y + shape.offsetY);
            const sw2 = this.worldToScreen(sah.wing2.X + shape.offsetX, sah.wing2.Y + shape.offsetY);
            ctx.beginPath();
            ctx.moveTo(s1.x, s1.y);
            ctx.lineTo(sw1.x, sw1.y);
            ctx.lineTo(sw2.x, sw2.y);
            ctx.closePath();
            ctx.fill();
        }
    }

    _drawText(ctx, shape) {
        const s = this.worldToScreen(shape.location.X + shape.offsetX, shape.location.Y + shape.offsetY);
        const fontSize = shape.height * this._zoom;
        const color = this._resolveColor(shape.color) || '#FFFFFF';

        ctx.fillStyle = color;
        ctx.font = `${shape.fontWeight} ${fontSize}px ${shape.font}`;

        const anchor = shape.anchor;
        if (anchor.includes('Center')) ctx.textAlign = 'center';
        else if (anchor.includes('Right')) ctx.textAlign = 'right';
        else ctx.textAlign = 'left';

        if (anchor.includes('Top')) ctx.textBaseline = 'top';
        else if (anchor.includes('Middle')) ctx.textBaseline = 'middle';
        else ctx.textBaseline = 'bottom';

        ctx.fillText(shape.content, s.x, s.y);
    }

    _drawDimension(ctx, shape) {
        const geom = shape.getDimensionGeometry();
        if (!geom) return;

        const color = this._resolveColor(shape.color) || '#888888';
        const textColor = this._resolveColor(shape.textColor) || color;
        const dimLineColor = this._resolveColor(shape.dimensionLineColor) || color;
        const extLineColor = this._resolveColor(shape.extensionLineColor) || color;

        ctx.lineWidth = 1;

        if (!shape.suppressExtLine1) {
            const e1s = this.worldToScreen(geom.ext1Start.X + shape.offsetX, geom.ext1Start.Y + shape.offsetY);
            const e1e = this.worldToScreen(geom.ext1End.X + shape.offsetX, geom.ext1End.Y + shape.offsetY);
            ctx.strokeStyle = extLineColor;
            ctx.beginPath(); ctx.moveTo(e1s.x, e1s.y); ctx.lineTo(e1e.x, e1e.y); ctx.stroke();
        }
        if (!shape.suppressExtLine2) {
            const e2s = this.worldToScreen(geom.ext2Start.X + shape.offsetX, geom.ext2Start.Y + shape.offsetY);
            const e2e = this.worldToScreen(geom.ext2End.X + shape.offsetX, geom.ext2End.Y + shape.offsetY);
            ctx.strokeStyle = extLineColor;
            ctx.beginPath(); ctx.moveTo(e2s.x, e2s.y); ctx.lineTo(e2e.x, e2e.y); ctx.stroke();
        }

        const ds = this.worldToScreen(geom.dimStart.X + shape.offsetX, geom.dimStart.Y + shape.offsetY);
        const de = this.worldToScreen(geom.dimEnd.X + shape.offsetX, geom.dimEnd.Y + shape.offsetY);
        ctx.strokeStyle = dimLineColor;
        ctx.beginPath(); ctx.moveTo(ds.x, ds.y); ctx.lineTo(de.x, de.y); ctx.stroke();

        const arrowSize = shape.arrowSize * this._zoom;
        this._drawArrowhead(ctx, ds, de, arrowSize, dimLineColor);
        this._drawArrowhead(ctx, de, ds, arrowSize, dimLineColor);

        const tp = this.worldToScreen(geom.textPos.X + shape.offsetX, geom.textPos.Y + shape.offsetY);
        const fontSize = shape.textHeight * this._zoom;
        ctx.font = `${fontSize}px Arial`;
        ctx.textAlign = 'center';
        ctx.textBaseline = 'middle';

        if (shape.textBackgroundOpaque) {
            const metrics = ctx.measureText(shape.displayText);
            const pad = 2;
            ctx.fillStyle = this._bgColor;
            ctx.fillRect(tp.x - metrics.width / 2 - pad, tp.y - fontSize / 2 - pad, metrics.width + pad * 2, fontSize + pad * 2);
        }

        ctx.fillStyle = textColor;
        ctx.fillText(shape.displayText, tp.x, tp.y);
    }

    _drawRadialDimension(ctx, shape) {
        const geom = shape.getDimensionGeometry();
        const color = this._resolveColor(shape.color) || '#888888';

        const cs = this.worldToScreen(shape.center.X + shape.offsetX, shape.center.Y + shape.offsetY);
        const cp = this.worldToScreen(geom.circumPoint.X + shape.offsetX, geom.circumPoint.Y + shape.offsetY);
        const le = this.worldToScreen(geom.leaderEnd.X + shape.offsetX, geom.leaderEnd.Y + shape.offsetY);

        ctx.strokeStyle = color;
        ctx.lineWidth = 1;
        ctx.beginPath(); ctx.moveTo(cs.x, cs.y); ctx.lineTo(le.x, le.y); ctx.stroke();

        const arrowSize = shape.arrowSize * this._zoom;
        this._drawArrowhead(ctx, cp, cs, arrowSize, color);

        const fontSize = shape.textHeight * this._zoom;
        ctx.font = `${fontSize}px Arial`;
        ctx.textAlign = 'left';
        ctx.textBaseline = 'middle';
        ctx.fillStyle = color;
        ctx.fillText(shape.displayText, le.x + 4, le.y);
    }

    _drawArrowhead(ctx, tip, tail, size, color) {
        const dx = tip.x - tail.x, dy = tip.y - tail.y;
        const len = Math.sqrt(dx * dx + dy * dy);
        if (len < 1) return;
        const ux = dx / len, uy = dy / len;
        const angle = 25 * Math.PI / 180;
        const cos = Math.cos(angle), sin = Math.sin(angle);

        ctx.fillStyle = color;
        ctx.beginPath();
        ctx.moveTo(tip.x, tip.y);
        ctx.lineTo(tip.x - size * (ux * cos - uy * sin), tip.y - size * (ux * sin + uy * cos));
        ctx.lineTo(tip.x - size * (ux * cos + uy * sin), tip.y - size * (-ux * sin + uy * cos));
        ctx.closePath();
        ctx.fill();
    }

    _drawGrid2(ctx, grid) {
        const color = this._resolveColor(grid.color) || '#FFFFFF';
        ctx.fillStyle = color;
        for (const p of grid.points) {
            const s = this.worldToScreen(p.X + grid.offsetX, p.Y + grid.offsetY);
            ctx.beginPath();
            ctx.arc(s.x, s.y, 3, 0, Math.PI * 2);
            ctx.fill();
        }
    }
}
