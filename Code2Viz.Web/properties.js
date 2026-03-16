// Code2Viz Web - Properties Panel
// Displays and allows editing of selected shape properties

import { VPoint, VLine, VCircle, VArc, VRectangle, VEllipse,
         VPolygon, VPolyline, VSpline, VBezier, VArrow, VText,
         VDimension, VRadialDimension, VGroup, VGrid, LineTypes } from './geometry/index.js';

export class PropertiesPanel {
    constructor(containerEl, renderer) {
        this._container = containerEl;
        this._renderer = renderer;
        this._selectedShapes = [];
        this._visible = false;
    }

    get visible() { return this._visible; }
    set visible(v) {
        this._visible = v;
        this._container.style.display = v ? 'flex' : 'none';
    }

    toggle() { this.visible = !this._visible; }

    update(shapes) {
        this._selectedShapes = shapes;
        this._render();
    }

    _render() {
        const el = this._container.querySelector('.properties-content') || this._container;
        el.innerHTML = '';

        if (this._selectedShapes.length === 0) {
            el.innerHTML = '<div class="prop-empty">No shape selected</div>';
            return;
        }

        if (this._selectedShapes.length > 1) {
            el.innerHTML = `<div class="prop-header">${this._selectedShapes.length} shapes selected</div>`;
            this._addCommonProperties(el, this._selectedShapes);
            return;
        }

        const shape = this._selectedShapes[0];
        const typeName = shape.constructor.name;

        // Header
        const header = document.createElement('div');
        header.className = 'prop-header';
        header.textContent = `${typeName} #${shape.id}`;
        el.appendChild(header);

        // Name
        this._addTextProp(el, 'Name', shape.name, v => { shape.name = v; });

        // Type-specific properties
        if (shape instanceof VPoint) {
            this._addNumberProp(el, 'X', shape.X, v => { shape.X = v; });
            this._addNumberProp(el, 'Y', shape.Y, v => { shape.Y = v; });
        } else if (shape instanceof VLine || shape instanceof VArrow) {
            this._addNumberProp(el, 'Start X', shape.start.X, v => { shape._start = VPoint.internal(v, shape.start.Y); });
            this._addNumberProp(el, 'Start Y', shape.start.Y, v => { shape._start = VPoint.internal(shape.start.X, v); });
            this._addNumberProp(el, 'End X', shape.end.X, v => { shape._end = VPoint.internal(v, shape.end.Y); });
            this._addNumberProp(el, 'End Y', shape.end.Y, v => { shape._end = VPoint.internal(shape.end.X, v); });
            if (shape.getLength) this._addReadonlyProp(el, 'Length', shape.getLength().toFixed(4));
        } else if (shape instanceof VCircle) {
            this._addNumberProp(el, 'Center X', shape.center.X, v => { shape._center = VPoint.internal(v, shape.center.Y); });
            this._addNumberProp(el, 'Center Y', shape.center.Y, v => { shape._center = VPoint.internal(shape.center.X, v); });
            this._addNumberProp(el, 'Radius', shape.radius, v => { shape.radius = v; });
            this._addReadonlyProp(el, 'Area', shape.area.toFixed(2));
            this._addReadonlyProp(el, 'Circumference', shape.circumference.toFixed(2));
        } else if (shape instanceof VArc) {
            this._addNumberProp(el, 'Center X', shape.center.X, v => { shape._center = VPoint.internal(v, shape.center.Y); });
            this._addNumberProp(el, 'Center Y', shape.center.Y, v => { shape._center = VPoint.internal(shape.center.X, v); });
            this._addNumberProp(el, 'Radius', shape.radius, v => { shape.radius = v; });
            this._addNumberProp(el, 'Start Angle', shape.startAngle, v => { shape._startAngle = v; });
            this._addNumberProp(el, 'End Angle', shape.endAngle, v => { shape._endAngle = v; });
        } else if (shape instanceof VRectangle) {
            this._addReadonlyProp(el, 'Width', shape.width.toFixed(2));
            this._addReadonlyProp(el, 'Height', shape.height.toFixed(2));
        } else if (shape instanceof VEllipse) {
            this._addNumberProp(el, 'Center X', shape.center.X, v => { shape._center = VPoint.internal(v, shape.center.Y); });
            this._addNumberProp(el, 'Center Y', shape.center.Y, v => { shape._center = VPoint.internal(shape.center.X, v); });
            this._addNumberProp(el, 'Radius X', shape.radiusX, v => { shape._radiusX = v; });
            this._addNumberProp(el, 'Radius Y', shape.radiusY, v => { shape._radiusY = v; });
        } else if (shape instanceof VText) {
            this._addTextProp(el, 'Text', shape.content, v => { shape._content = v; });
            this._addNumberProp(el, 'Size', shape.height, v => { shape._height = v; });
        }

        // Separator
        el.appendChild(document.createElement('hr'));

        // Common properties
        this._addColorProp(el, 'Color', shape.color, v => { shape.color = v; });
        this._addColorProp(el, 'Fill', shape.fillColor, v => { shape.fillColor = v; });
        this._addNumberProp(el, 'Line Weight', shape.lineWeight, v => { shape.lineWeight = v; });
        this._addSelectProp(el, 'Line Type', shape.lineType, Object.keys(LineTypes), v => { shape.lineType = v; });
        this._addRangeProp(el, 'Opacity', shape.opacity, 0, 1, 0.05, v => { shape.opacity = v; });
        this._addCheckboxProp(el, 'Visible', shape.isVisible, v => { shape.isVisible = v; });

        // Position offsets
        el.appendChild(document.createElement('hr'));
        this._addNumberProp(el, 'Offset X', shape.offsetX, v => { shape.offsetX = v; });
        this._addNumberProp(el, 'Offset Y', shape.offsetY, v => { shape.offsetY = v; });

        // Layer
        if (shape._layer !== undefined) {
            this._addReadonlyProp(el, 'Layer', shape._layer || 'Default');
        }

        // ID
        this._addReadonlyProp(el, 'ID', shape.id);
    }

    _addCommonProperties(el, shapes) {
        el.appendChild(document.createElement('hr'));
        this._addColorProp(el, 'Color', shapes[0].color, v => {
            for (const s of shapes) s.color = v;
        });
        this._addColorProp(el, 'Fill', shapes[0].fillColor, v => {
            for (const s of shapes) s.fillColor = v;
        });
        this._addNumberProp(el, 'Line Weight', shapes[0].lineWeight, v => {
            for (const s of shapes) s.lineWeight = v;
        });
        this._addRangeProp(el, 'Opacity', shapes[0].opacity, 0, 1, 0.05, v => {
            for (const s of shapes) s.opacity = v;
        });
    }

    _createRow(parent, label) {
        const row = document.createElement('div');
        row.className = 'prop-row';
        const lbl = document.createElement('label');
        lbl.className = 'prop-label';
        lbl.textContent = label;
        row.appendChild(lbl);
        parent.appendChild(row);
        return row;
    }

    _addNumberProp(parent, label, value, onChange) {
        const row = this._createRow(parent, label);
        const input = document.createElement('input');
        input.type = 'number';
        input.className = 'prop-input';
        input.value = typeof value === 'number' ? value.toFixed(4) : value;
        input.step = 'any';
        input.addEventListener('change', () => {
            const v = parseFloat(input.value);
            if (!isNaN(v)) { onChange(v); this._renderer.render(); }
        });
        row.appendChild(input);
    }

    _addTextProp(parent, label, value, onChange) {
        const row = this._createRow(parent, label);
        const input = document.createElement('input');
        input.type = 'text';
        input.className = 'prop-input';
        input.value = value || '';
        input.addEventListener('change', () => {
            onChange(input.value); this._renderer.render();
        });
        row.appendChild(input);
    }

    _addColorProp(parent, label, value, onChange) {
        const row = this._createRow(parent, label);
        const wrapper = document.createElement('div');
        wrapper.className = 'prop-color-wrapper';

        const text = document.createElement('input');
        text.type = 'text';
        text.className = 'prop-input prop-color-text';
        text.value = value || '';
        text.addEventListener('change', () => {
            onChange(text.value); this._renderer.render();
        });

        const picker = document.createElement('input');
        picker.type = 'color';
        picker.className = 'prop-color-picker';
        picker.value = this._colorToHex(value);
        picker.addEventListener('input', () => {
            text.value = picker.value;
            onChange(picker.value); this._renderer.render();
        });

        wrapper.appendChild(text);
        wrapper.appendChild(picker);
        row.appendChild(wrapper);
    }

    _addSelectProp(parent, label, value, options, onChange) {
        const row = this._createRow(parent, label);
        const select = document.createElement('select');
        select.className = 'prop-input';
        for (const opt of options) {
            const o = document.createElement('option');
            o.value = opt; o.textContent = opt;
            if (opt === value) o.selected = true;
            select.appendChild(o);
        }
        select.addEventListener('change', () => {
            onChange(select.value); this._renderer.render();
        });
        row.appendChild(select);
    }

    _addRangeProp(parent, label, value, min, max, step, onChange) {
        const row = this._createRow(parent, label);
        const wrapper = document.createElement('div');
        wrapper.className = 'prop-range-wrapper';

        const range = document.createElement('input');
        range.type = 'range';
        range.className = 'prop-range';
        range.min = min; range.max = max; range.step = step;
        range.value = value;

        const num = document.createElement('span');
        num.className = 'prop-range-value';
        num.textContent = parseFloat(value).toFixed(2);

        range.addEventListener('input', () => {
            const v = parseFloat(range.value);
            num.textContent = v.toFixed(2);
            onChange(v); this._renderer.render();
        });

        wrapper.appendChild(range);
        wrapper.appendChild(num);
        row.appendChild(wrapper);
    }

    _addCheckboxProp(parent, label, value, onChange) {
        const row = this._createRow(parent, label);
        const cb = document.createElement('input');
        cb.type = 'checkbox';
        cb.className = 'prop-checkbox';
        cb.checked = value;
        cb.addEventListener('change', () => {
            onChange(cb.checked); this._renderer.render();
        });
        row.appendChild(cb);
    }

    _addReadonlyProp(parent, label, value) {
        const row = this._createRow(parent, label);
        const span = document.createElement('span');
        span.className = 'prop-readonly';
        span.textContent = value;
        row.appendChild(span);
    }

    _colorToHex(color) {
        if (!color || color === 'Transparent') return '#ffffff';
        if (color.startsWith('#')) return color.length === 7 ? color : '#ffffff';
        try {
            const ctx = document.createElement('canvas').getContext('2d');
            ctx.fillStyle = color;
            return ctx.fillStyle;
        } catch (e) { return '#ffffff'; }
    }
}
