// Code2Viz Web - Export Module
// Exports canvas shapes to SVG, PNG, PDF, DXF

import {
    getRegistry, VPoint, VLine, VCircle, VArc, VRectangle, VEllipse,
    VPolygon, VPolyline, VSpline, VBezier, VArrow, VText, VDimension,
    VRadialDimension, VGroup, VGrid, LineTypes, EPSILON,
} from './geometry/index.js';

// ============================================================================
// Shared: compute scene bounds
// ============================================================================
function getSceneBounds(padding = 20) {
    const shapes = getRegistry();
    if (shapes.length === 0) return { minX: -100, minY: -100, maxX: 100, maxY: 100 };

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
    if (!isFinite(minX)) return { minX: -100, minY: -100, maxX: 100, maxY: 100 };
    return { minX: minX - padding, minY: minY - padding, maxX: maxX + padding, maxY: maxY + padding };
}

// Resolve a color string to a hex or rgb value for SVG/DXF
function resolveColor(c) {
    if (!c || c === 'Transparent') return null;
    return c;
}

function lineDashAttr(lineType, scale) {
    const pat = LineTypes[lineType];
    if (!pat || pat.length === 0) return '';
    return ` stroke-dasharray="${pat.map(v => v * (scale || 1)).join(',')}"`;
}

// ============================================================================
// SVG Export
// ============================================================================
export function exportSVG() {
    const bounds = getSceneBounds();
    const w = bounds.maxX - bounds.minX;
    const h = bounds.maxY - bounds.minY;

    // SVG uses top-left origin with Y-down. We flip Y.
    // Transform: translate(-minX, maxY) then scale(1, -1)
    // But for text we need to un-flip. Easier approach: just transform each coord.
    const tx = (wx) => wx - bounds.minX;
    const ty = (wy) => bounds.maxY - wy; // flip Y

    let svg = `<?xml version="1.0" encoding="UTF-8"?>\n`;
    svg += `<svg xmlns="http://www.w3.org/2000/svg" width="${w}" height="${h}" viewBox="0 0 ${w} ${h}">\n`;
    svg += `  <rect width="100%" height="100%" fill="#1e1e2e"/>\n`;

    const shapes = getRegistry();
    for (const s of shapes) {
        if (!s.isVisible || s.drawFactor <= 0) continue;
        svg += shapeToSVG(s, tx, ty);
    }

    svg += `</svg>\n`;
    return svg;
}

function shapeToSVG(s, tx, ty) {
    const stroke = resolveColor(s.color);
    const fill = resolveColor(s.fillColor);
    const sw = s.lineWeight;
    const opacity = s.opacity < 1 ? ` opacity="${s.opacity}"` : '';
    const dash = lineDashAttr(s.lineType, s.lineTypeScale);
    const strokeAttr = stroke ? ` stroke="${stroke}" stroke-width="${sw}"${dash}` : ' stroke="none"';
    const fillAttr = fill ? ` fill="${fill}"` : ' fill="none"';

    if (s instanceof VGroup) {
        let g = `  <g${opacity}>\n`;
        for (const child of s.shapes) {
            if (child.isVisible) g += '  ' + shapeToSVG(child, tx, ty);
        }
        return g + `  </g>\n`;
    }

    if (s instanceof VGrid) {
        let g = `  <g${opacity}>\n`;
        for (const p of s.points) {
            g += `    <circle cx="${tx(p.X)}" cy="${ty(p.Y)}" r="3"${fillAttr.replace('none', stroke || 'white')} stroke="none"/>\n`;
        }
        return g + `  </g>\n`;
    }

    if (s instanceof VPoint) {
        const x = tx(s.X), y = ty(s.Y);
        const c = stroke || 'white';
        return `  <g${opacity}><circle cx="${x}" cy="${y}" r="4" fill="${c}" stroke="none"/>` +
               `<line x1="${x - 5}" y1="${y}" x2="${x + 5}" y2="${y}" stroke="${c}" stroke-width="1.5"/>` +
               `<line x1="${x}" y1="${y - 5}" x2="${x}" y2="${y + 5}" stroke="${c}" stroke-width="1.5"/></g>\n`;
    }

    if (s instanceof VArrow) {
        let svg = `  <g${opacity}>\n`;
        svg += `    <line x1="${tx(s.start.X)}" y1="${ty(s.start.Y)}" x2="${tx(s.end.X)}" y2="${ty(s.end.Y)}"${strokeAttr}/>\n`;
        const ah = s.getEndArrowhead();
        svg += `    <polygon points="${tx(s.end.X)},${ty(s.end.Y)} ${tx(ah.wing1.X)},${ty(ah.wing1.Y)} ${tx(ah.wing2.X)},${ty(ah.wing2.Y)}" fill="${stroke || 'white'}" stroke="none"/>\n`;
        if (s.doubleEnded) {
            const sah = s.getStartArrowhead();
            svg += `    <polygon points="${tx(s.start.X)},${ty(s.start.Y)} ${tx(sah.wing1.X)},${ty(sah.wing1.Y)} ${tx(sah.wing2.X)},${ty(sah.wing2.Y)}" fill="${stroke || 'white'}" stroke="none"/>\n`;
        }
        return svg + `  </g>\n`;
    }

    if (s instanceof VLine) {
        return `  <line x1="${tx(s.start.X)}" y1="${ty(s.start.Y)}" x2="${tx(s.end.X)}" y2="${ty(s.end.Y)}"${strokeAttr}${opacity}/>\n`;
    }

    if (s instanceof VCircle) {
        return `  <circle cx="${tx(s.center.X)}" cy="${ty(s.center.Y)}" r="${s.radius}"${strokeAttr}${fillAttr}${opacity}/>\n`;
    }

    if (s instanceof VArc) {
        const sp = s.startPoint, ep = s.endPoint;
        const r = s.radius;
        const sweep = s.sweepAngle;
        const largeArc = sweep > 180 ? 1 : 0;
        // SVG arc: Y is flipped so sweep direction flips
        return `  <path d="M ${tx(sp.X)} ${ty(sp.Y)} A ${r} ${r} 0 ${largeArc} 0 ${tx(ep.X)} ${ty(ep.Y)}"${strokeAttr} fill="none"${opacity}/>\n`;
    }

    if (s instanceof VRectangle) {
        const pts = s.points;
        const pstr = pts.map(p => `${tx(p.X)},${ty(p.Y)}`).join(' ');
        return `  <polygon points="${pstr}"${strokeAttr}${fillAttr}${opacity}/>\n`;
    }

    if (s instanceof VEllipse) {
        return `  <ellipse cx="${tx(s.center.X)}" cy="${ty(s.center.Y)}" rx="${s.radiusX}" ry="${s.radiusY}"${strokeAttr}${fillAttr}${opacity}/>\n`;
    }

    if (s instanceof VPolygon) {
        const pstr = s.points.map(p => `${tx(p.X)},${ty(p.Y)}`).join(' ');
        return `  <polygon points="${pstr}"${strokeAttr}${fillAttr}${opacity}/>\n`;
    }

    if (s instanceof VPolyline) {
        const pstr = s.points.map(p => `${tx(p.X)},${ty(p.Y)}`).join(' ');
        return `  <polyline points="${pstr}"${strokeAttr} fill="none"${opacity}/>\n`;
    }

    if (s instanceof VSpline || s instanceof VBezier) {
        const pts = s.getRenderPoints ? s.getRenderPoints() : s.divide(64);
        const d = pts.map((p, i) => `${i === 0 ? 'M' : 'L'} ${tx(p.X)} ${ty(p.Y)}`).join(' ');
        return `  <path d="${d}"${strokeAttr} fill="none"${opacity}/>\n`;
    }

    if (s instanceof VText) {
        const x = tx(s.location.X), y = ty(s.location.Y);
        const c = stroke || 'white';
        const anchor = s.anchor;
        let textAnchor = 'start';
        if (anchor.includes('Center')) textAnchor = 'middle';
        else if (anchor.includes('Right')) textAnchor = 'end';
        let dy = '0';
        if (anchor.includes('Top')) dy = `${s.height}`;
        else if (anchor.includes('Middle')) dy = `${s.height / 2}`;
        // SVG rotate is CW; world is CCW → negate. tx,ty already accounts for any parent Y-flip.
        const angle = s.angle || 0;
        const transform = angle !== 0 ? ` transform="rotate(${-angle} ${x} ${y})"` : '';
        // Text in SVG with Y-flip needs dominant-baseline handling
        return `  <text x="${x}" y="${y}" fill="${c}" font-family="${s.font}" font-size="${s.height}" font-weight="${s.fontWeight}" text-anchor="${textAnchor}" dy="${dy}"${transform}${opacity}>${escXml(s.content)}</text>\n`;
    }

    if (s instanceof VDimension) {
        const geom = s.getDimensionGeometry();
        if (!geom) return '';
        const c = resolveColor(s.color) || '#888';
        let svg = `  <g${opacity}>\n`;
        // Extension lines
        if (!s.suppressExtLine1) {
            svg += `    <line x1="${tx(geom.ext1Start.X)}" y1="${ty(geom.ext1Start.Y)}" x2="${tx(geom.ext1End.X)}" y2="${ty(geom.ext1End.Y)}" stroke="${resolveColor(s.extensionLineColor) || c}" stroke-width="1"/>\n`;
        }
        if (!s.suppressExtLine2) {
            svg += `    <line x1="${tx(geom.ext2Start.X)}" y1="${ty(geom.ext2Start.Y)}" x2="${tx(geom.ext2End.X)}" y2="${ty(geom.ext2End.Y)}" stroke="${resolveColor(s.extensionLineColor) || c}" stroke-width="1"/>\n`;
        }
        // Dim line
        svg += `    <line x1="${tx(geom.dimStart.X)}" y1="${ty(geom.dimStart.Y)}" x2="${tx(geom.dimEnd.X)}" y2="${ty(geom.dimEnd.Y)}" stroke="${resolveColor(s.dimensionLineColor) || c}" stroke-width="1"/>\n`;
        // Text
        svg += `    <text x="${tx(geom.textPos.X)}" y="${ty(geom.textPos.Y)}" fill="${resolveColor(s.textColor) || c}" font-size="${s.textHeight}" text-anchor="middle" dy="4">${escXml(s.displayText)}</text>\n`;
        return svg + `  </g>\n`;
    }

    if (s instanceof VRadialDimension) {
        const geom = s.getDimensionGeometry();
        const c = resolveColor(s.color) || '#888';
        let svg = `  <g${opacity}>\n`;
        svg += `    <line x1="${tx(s.center.X)}" y1="${ty(s.center.Y)}" x2="${tx(geom.leaderEnd.X)}" y2="${ty(geom.leaderEnd.Y)}" stroke="${c}" stroke-width="1"/>\n`;
        svg += `    <text x="${tx(geom.leaderEnd.X)}" y="${ty(geom.leaderEnd.Y)}" fill="${c}" font-size="${s.textHeight}" dx="4" dy="4">${escXml(s.displayText)}</text>\n`;
        return svg + `  </g>\n`;
    }

    return '';
}

function escXml(s) {
    return s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
}

// ============================================================================
// PNG Export (from canvas)
// ============================================================================
export function exportPNG(renderer) {
    return renderer._canvas.toDataURL('image/png');
}

// ============================================================================
// DXF Export (AutoCAD R12 text format)
// ============================================================================
export function exportDXF() {
    const shapes = getRegistry();
    let dxf = '';

    // Header
    dxf += '0\nSECTION\n2\nHEADER\n';
    dxf += '9\n$ACADVER\n1\nAC1009\n'; // R12
    dxf += '0\nENDSEC\n';

    // Tables (minimal)
    dxf += '0\nSECTION\n2\nTABLES\n';
    dxf += '0\nTABLE\n2\nLAYER\n70\n1\n';
    dxf += '0\nLAYER\n2\n0\n70\n0\n62\n7\n6\nCONTINUOUS\n';
    dxf += '0\nENDTAB\n';
    dxf += '0\nENDSEC\n';

    // Entities
    dxf += '0\nSECTION\n2\nENTITIES\n';

    for (const s of shapes) {
        if (!s.isVisible || s.drawFactor <= 0) continue;
        dxf += shapeToDXF(s);
    }

    dxf += '0\nENDSEC\n';
    dxf += '0\nEOF\n';
    return dxf;
}

function dxfColor(colorStr) {
    // Map common colors to ACI (AutoCAD Color Index)
    const map = {
        'red': 1, '#ff0000': 1,
        'yellow': 2, '#ffff00': 2,
        'green': 3, '#00ff00': 3, 'lime': 3,
        'cyan': 4, '#00ffff': 4,
        'blue': 5, '#0000ff': 5,
        'magenta': 6, '#ff00ff': 6,
        'white': 7, '#ffffff': 7,
        'gray': 8, 'grey': 8, '#808080': 8,
        'crimson': 1, 'dodgerblue': 5, 'limegreen': 3,
        'orange': 30, '#ffa500': 30,
        'pink': 221, '#ffc0cb': 221,
        'purple': 200, '#800080': 200,
    };
    if (!colorStr) return 7;
    return map[colorStr.toLowerCase()] || 7;
}

function shapeToDXF(s) {
    const col = dxfColor(s.color);

    if (s instanceof VGroup) {
        return s.shapes.filter(c => c.isVisible).map(c => shapeToDXF(c)).join('');
    }

    if (s instanceof VGrid) {
        return s.points.map(p =>
            `0\nPOINT\n8\n0\n62\n${col}\n10\n${p.X}\n20\n${p.Y}\n30\n0\n`
        ).join('');
    }

    if (s instanceof VPoint) {
        return `0\nPOINT\n8\n0\n62\n${col}\n10\n${s.X}\n20\n${s.Y}\n30\n0\n`;
    }

    if (s instanceof VLine || s instanceof VArrow) {
        const start = s instanceof VArrow ? s.start : s.start;
        const end = s instanceof VArrow ? s.end : s.end;
        return `0\nLINE\n8\n0\n62\n${col}\n10\n${start.X}\n20\n${start.Y}\n30\n0\n11\n${end.X}\n21\n${end.Y}\n31\n0\n`;
    }

    if (s instanceof VCircle) {
        return `0\nCIRCLE\n8\n0\n62\n${col}\n10\n${s.center.X}\n20\n${s.center.Y}\n30\n0\n40\n${s.radius}\n`;
    }

    if (s instanceof VArc) {
        return `0\nARC\n8\n0\n62\n${col}\n10\n${s.center.X}\n20\n${s.center.Y}\n30\n0\n40\n${s.radius}\n50\n${s.startAngle}\n51\n${s.endAngle}\n`;
    }

    if (s instanceof VEllipse) {
        // DXF ELLIPSE entity (AC1012+, but most readers handle it)
        return `0\nELLIPSE\n8\n0\n62\n${col}\n10\n${s.center.X}\n20\n${s.center.Y}\n30\n0\n11\n${s.radiusX}\n21\n0\n31\n0\n40\n${s.radiusY / s.radiusX}\n41\n0\n42\n${2 * Math.PI}\n`;
    }

    if (s instanceof VRectangle) {
        const pts = s.points;
        return polylineToDXF(pts, true, col);
    }

    if (s instanceof VPolygon) {
        return polylineToDXF(s.points, true, col);
    }

    if (s instanceof VPolyline) {
        return polylineToDXF(s.points, false, col);
    }

    if (s instanceof VSpline || s instanceof VBezier) {
        const pts = s.getRenderPoints ? s.getRenderPoints() : s.divide(64);
        return polylineToDXF(pts, false, col);
    }

    if (s instanceof VText) {
        const angle = s.angle || 0;
        const angleStr = angle !== 0 ? `50\n${angle}\n` : '';
        return `0\nTEXT\n8\n0\n62\n${col}\n10\n${s.location.X}\n20\n${s.location.Y}\n30\n0\n40\n${s.height}\n1\n${s.content}\n${angleStr}`;
    }

    if (s instanceof VDimension) {
        // Export as lines + text
        const geom = s.getDimensionGeometry();
        if (!geom) return '';
        let dxf = '';
        dxf += `0\nLINE\n8\n0\n62\n${col}\n10\n${geom.ext1Start.X}\n20\n${geom.ext1Start.Y}\n30\n0\n11\n${geom.ext1End.X}\n21\n${geom.ext1End.Y}\n31\n0\n`;
        dxf += `0\nLINE\n8\n0\n62\n${col}\n10\n${geom.ext2Start.X}\n20\n${geom.ext2Start.Y}\n30\n0\n11\n${geom.ext2End.X}\n21\n${geom.ext2End.Y}\n31\n0\n`;
        dxf += `0\nLINE\n8\n0\n62\n${col}\n10\n${geom.dimStart.X}\n20\n${geom.dimStart.Y}\n30\n0\n11\n${geom.dimEnd.X}\n21\n${geom.dimEnd.Y}\n31\n0\n`;
        dxf += `0\nTEXT\n8\n0\n62\n${col}\n10\n${geom.textPos.X}\n20\n${geom.textPos.Y}\n30\n0\n40\n${s.textHeight}\n1\n${s.displayText}\n72\n1\n11\n${geom.textPos.X}\n21\n${geom.textPos.Y}\n31\n0\n`;
        return dxf;
    }

    if (s instanceof VRadialDimension) {
        const geom = s.getDimensionGeometry();
        let dxf = '';
        dxf += `0\nLINE\n8\n0\n62\n${col}\n10\n${s.center.X}\n20\n${s.center.Y}\n30\n0\n11\n${geom.leaderEnd.X}\n21\n${geom.leaderEnd.Y}\n31\n0\n`;
        dxf += `0\nTEXT\n8\n0\n62\n${col}\n10\n${geom.leaderEnd.X}\n20\n${geom.leaderEnd.Y}\n30\n0\n40\n${s.textHeight}\n1\n${s.displayText}\n`;
        return dxf;
    }

    return '';
}

function polylineToDXF(points, closed, color) {
    if (points.length < 2) return '';
    let dxf = `0\nPOLYLINE\n8\n0\n62\n${color}\n66\n1\n70\n${closed ? 1 : 0}\n`;
    for (const p of points) {
        dxf += `0\nVERTEX\n8\n0\n10\n${p.X}\n20\n${p.Y}\n30\n0\n`;
    }
    dxf += `0\nSEQEND\n8\n0\n`;
    return dxf;
}

// ============================================================================
// PDF Export (using jsPDF from CDN, loaded on demand)
// ============================================================================
let _jsPDF = null;

async function loadJsPDF() {
    if (_jsPDF) return _jsPDF;
    // Dynamic import from CDN
    const script = document.createElement('script');
    script.src = 'https://cdnjs.cloudflare.com/ajax/libs/jspdf/2.5.2/jspdf.umd.min.js';
    document.head.appendChild(script);
    await new Promise((resolve, reject) => {
        script.onload = resolve;
        script.onerror = reject;
    });
    _jsPDF = window.jspdf.jsPDF;
    return _jsPDF;
}

export async function exportPDF() {
    const JsPDF = await loadJsPDF();
    const bounds = getSceneBounds();
    const w = bounds.maxX - bounds.minX;
    const h = bounds.maxY - bounds.minY;

    // Determine orientation
    const orientation = w > h ? 'landscape' : 'portrait';
    const doc = new JsPDF({ orientation, unit: 'pt', format: [w, h] });

    // Background
    doc.setFillColor(30, 30, 46);
    doc.rect(0, 0, w, h, 'F');

    const tx = (wx) => wx - bounds.minX;
    const ty = (wy) => bounds.maxY - wy;

    const shapes = getRegistry();
    for (const s of shapes) {
        if (!s.isVisible || s.drawFactor <= 0) continue;
        drawShapePDF(doc, s, tx, ty);
    }

    return doc.output('blob');
}

function parseColorRGB(colorStr) {
    if (!colorStr || colorStr === 'Transparent') return null;
    // Use a temp canvas to resolve CSS color names
    const ctx = document.createElement('canvas').getContext('2d');
    ctx.fillStyle = colorStr;
    const hex = ctx.fillStyle; // returns #rrggbb
    if (hex.startsWith('#')) {
        const r = parseInt(hex.slice(1, 3), 16);
        const g = parseInt(hex.slice(3, 5), 16);
        const b = parseInt(hex.slice(5, 7), 16);
        return [r, g, b];
    }
    // rgba()
    const m = hex.match(/rgba?\((\d+),\s*(\d+),\s*(\d+)/);
    if (m) return [+m[1], +m[2], +m[3]];
    return [255, 255, 255];
}

function drawShapePDF(doc, s, tx, ty) {
    const stroke = parseColorRGB(s.color);
    const fill = parseColorRGB(s.fillColor);

    if (s instanceof VGroup) {
        for (const child of s.shapes) {
            if (child.isVisible) drawShapePDF(doc, child, tx, ty);
        }
        return;
    }

    if (s instanceof VGrid) {
        const c = stroke || [255, 255, 255];
        doc.setFillColor(c[0], c[1], c[2]);
        for (const p of s.points) {
            doc.circle(tx(p.X), ty(p.Y), 2, 'F');
        }
        return;
    }

    if (s instanceof VPoint) {
        const c = stroke || [255, 255, 255];
        doc.setFillColor(c[0], c[1], c[2]);
        doc.circle(tx(s.X), ty(s.Y), 3, 'F');
        return;
    }

    if (stroke) {
        doc.setDrawColor(stroke[0], stroke[1], stroke[2]);
        doc.setLineWidth(s.lineWeight * 0.75);
    }
    if (fill) {
        doc.setFillColor(fill[0], fill[1], fill[2]);
    }

    if (s instanceof VLine || s instanceof VArrow) {
        const start = s instanceof VArrow ? s.start : s.start;
        const end = s instanceof VArrow ? s.end : s.end;
        doc.line(tx(start.X), ty(start.Y), tx(end.X), ty(end.Y));
        return;
    }

    if (s instanceof VCircle) {
        const mode = fill ? (stroke ? 'FD' : 'F') : 'S';
        doc.circle(tx(s.center.X), ty(s.center.Y), s.radius, mode);
        return;
    }

    if (s instanceof VArc) {
        // Approximate with polyline
        const pts = s.divide(36);
        drawPolylinePDF(doc, pts, tx, ty, false);
        return;
    }

    if (s instanceof VEllipse) {
        const mode = fill ? (stroke ? 'FD' : 'F') : 'S';
        doc.ellipse(tx(s.center.X), ty(s.center.Y), s.radiusX, s.radiusY, mode);
        return;
    }

    if (s instanceof VRectangle || s instanceof VPolygon) {
        const pts = s instanceof VRectangle ? s.points : s.points;
        drawPolygonPDF(doc, pts, tx, ty, !!fill);
        return;
    }

    if (s instanceof VPolyline) {
        drawPolylinePDF(doc, s.points, tx, ty, false);
        return;
    }

    if (s instanceof VSpline || s instanceof VBezier) {
        const pts = s.getRenderPoints ? s.getRenderPoints() : s.divide(64);
        drawPolylinePDF(doc, pts, tx, ty, false);
        return;
    }

    if (s instanceof VText) {
        const c = stroke || [255, 255, 255];
        doc.setTextColor(c[0], c[1], c[2]);
        doc.setFontSize(s.height);
        const angle = s.angle || 0;
        if (angle !== 0) {
            // jsPDF text angle option rotates CCW in degrees.
            doc.text(s.content, tx(s.location.X), ty(s.location.Y), { angle });
        } else {
            doc.text(s.content, tx(s.location.X), ty(s.location.Y));
        }
        return;
    }

    if (s instanceof VDimension) {
        const geom = s.getDimensionGeometry();
        if (!geom) return;
        const c = stroke || [136, 136, 136];
        doc.setDrawColor(c[0], c[1], c[2]);
        doc.setLineWidth(0.75);
        doc.line(tx(geom.ext1Start.X), ty(geom.ext1Start.Y), tx(geom.ext1End.X), ty(geom.ext1End.Y));
        doc.line(tx(geom.ext2Start.X), ty(geom.ext2Start.Y), tx(geom.ext2End.X), ty(geom.ext2End.Y));
        doc.line(tx(geom.dimStart.X), ty(geom.dimStart.Y), tx(geom.dimEnd.X), ty(geom.dimEnd.Y));
        doc.setTextColor(c[0], c[1], c[2]);
        doc.setFontSize(s.textHeight);
        doc.text(s.displayText, tx(geom.textPos.X), ty(geom.textPos.Y), { align: 'center' });
        return;
    }

    if (s instanceof VRadialDimension) {
        const geom = s.getDimensionGeometry();
        const c = stroke || [136, 136, 136];
        doc.setDrawColor(c[0], c[1], c[2]);
        doc.setLineWidth(0.75);
        doc.line(tx(s.center.X), ty(s.center.Y), tx(geom.leaderEnd.X), ty(geom.leaderEnd.Y));
        doc.setTextColor(c[0], c[1], c[2]);
        doc.setFontSize(s.textHeight);
        doc.text(s.displayText, tx(geom.leaderEnd.X) + 4, ty(geom.leaderEnd.Y));
        return;
    }
}

function drawPolylinePDF(doc, pts, tx, ty, closed) {
    if (pts.length < 2) return;
    for (let i = 0; i < pts.length - 1; i++) {
        doc.line(tx(pts[i].X), ty(pts[i].Y), tx(pts[i + 1].X), ty(pts[i + 1].Y));
    }
    if (closed && pts.length > 2) {
        doc.line(tx(pts[pts.length - 1].X), ty(pts[pts.length - 1].Y), tx(pts[0].X), ty(pts[0].Y));
    }
}

function drawPolygonPDF(doc, pts, tx, ty, hasFill) {
    if (pts.length < 3) return;
    // jsPDF doesn't have a polygon method with fill, use lines
    const lines = pts.map(p => [tx(p.X), ty(p.Y)]);
    // Move to first, line to rest, close
    doc.lines(
        lines.slice(1).map((p, i) => [p[0] - lines[i][0], p[1] - lines[i][1]]),
        lines[0][0], lines[0][1],
        [1, 1],
        hasFill ? 'FD' : 'S',
        true // closed
    );
}

// ============================================================================
// Download helper
// ============================================================================
export function downloadBlob(blob, filename) {
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
}

export function downloadText(text, filename, mimeType = 'text/plain') {
    downloadBlob(new Blob([text], { type: mimeType }), filename);
}

export function downloadDataURL(dataUrl, filename) {
    const a = document.createElement('a');
    a.href = dataUrl;
    a.download = filename;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
}
