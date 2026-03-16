// Code2Viz Web - Layer System
// Manages shape layers with visibility, locking, and z-ordering

import { getRegistry } from './geometry/index.js';

export class Layer {
    constructor(name, color = '#cdd6f4') {
        this.name = name;
        this.color = color;
        this.visible = true;
        this.locked = false;
        this.expanded = true;
    }
}

export class LayerManager {
    constructor(containerEl, renderer) {
        this._container = containerEl;
        this._renderer = renderer;
        this._layers = [new Layer('Default')];
        this._activeLayer = 'Default';
        this._visible = false;
        this._onChange = null;
    }

    get layers() { return this._layers; }
    get activeLayer() { return this._activeLayer; }
    get visible() { return this._visible; }

    set visible(v) {
        this._visible = v;
        this._container.style.display = v ? 'flex' : 'none';
        if (v) this.render();
    }

    toggle() { this.visible = !this._visible; }
    onChange(callback) { this._onChange = callback; }

    addLayer(name, color) {
        if (this._layers.find(l => l.name === name)) return;
        this._layers.push(new Layer(name, color));
        this.render();
    }

    removeLayer(name) {
        if (name === 'Default') return;
        this._layers = this._layers.filter(l => l.name !== name);
        // Move shapes from deleted layer to Default
        for (const shape of getRegistry()) {
            if (shape._layer === name) shape._layer = 'Default';
        }
        if (this._activeLayer === name) this._activeLayer = 'Default';
        this.render();
        this._renderer.render();
    }

    setActiveLayer(name) {
        this._activeLayer = name;
        this.render();
    }

    getShapeLayer(shape) {
        return shape._layer || 'Default';
    }

    setShapeLayer(shape, layerName) {
        shape._layer = layerName;
    }

    moveShapeToLayer(shape, layerName) {
        shape._layer = layerName;
        this.render();
    }

    getLayerByName(name) {
        return this._layers.find(l => l.name === name);
    }

    isShapeVisible(shape) {
        const layer = this.getLayerByName(this.getShapeLayer(shape));
        return layer ? layer.visible : true;
    }

    isShapeLocked(shape) {
        const layer = this.getLayerByName(this.getShapeLayer(shape));
        return layer ? layer.locked : false;
    }

    // Get shapes grouped by layer
    getShapesByLayer() {
        const groups = {};
        for (const layer of this._layers) groups[layer.name] = [];
        for (const shape of getRegistry()) {
            const layerName = shape._layer || 'Default';
            if (!groups[layerName]) groups[layerName] = [];
            groups[layerName].push(shape);
        }
        return groups;
    }

    // Move shape up/down in z-order within its layer
    moveShapeUp(shape) {
        const reg = getRegistry();
        const idx = reg.indexOf(shape);
        if (idx < reg.length - 1) {
            reg.splice(idx, 1);
            reg.splice(idx + 1, 0, shape);
            this._renderer.render();
        }
    }

    moveShapeDown(shape) {
        const reg = getRegistry();
        const idx = reg.indexOf(shape);
        if (idx > 0) {
            reg.splice(idx, 1);
            reg.splice(idx - 1, 0, shape);
            this._renderer.render();
        }
    }

    bringToFront(shape) {
        const reg = getRegistry();
        const idx = reg.indexOf(shape);
        if (idx >= 0 && idx < reg.length - 1) {
            reg.splice(idx, 1);
            reg.push(shape);
            this._renderer.render();
        }
    }

    sendToBack(shape) {
        const reg = getRegistry();
        const idx = reg.indexOf(shape);
        if (idx > 0) {
            reg.splice(idx, 1);
            reg.unshift(shape);
            this._renderer.render();
        }
    }

    render() {
        if (!this._visible) return;
        const el = this._container.querySelector('.layers-content') || this._container;
        el.innerHTML = '';

        // Add layer button
        const addBtn = document.createElement('button');
        addBtn.className = 'btn btn-sm layer-add-btn';
        addBtn.textContent = '+ Add Layer';
        addBtn.addEventListener('click', () => {
            const name = prompt('Layer name:');
            if (name && name.trim()) this.addLayer(name.trim());
        });
        el.appendChild(addBtn);

        const shapesByLayer = this.getShapesByLayer();

        for (const layer of this._layers) {
            const layerEl = document.createElement('div');
            layerEl.className = 'layer-item' + (layer.name === this._activeLayer ? ' layer-active' : '');

            // Layer header
            const header = document.createElement('div');
            header.className = 'layer-header';

            // Visibility toggle
            const visBtn = document.createElement('button');
            visBtn.className = 'layer-vis-btn';
            visBtn.textContent = layer.visible ? '\u{1F441}' : '\u2014';
            visBtn.title = 'Toggle visibility';
            visBtn.addEventListener('click', (e) => {
                e.stopPropagation();
                layer.visible = !layer.visible;
                // Apply to shapes
                for (const s of getRegistry()) {
                    if ((s._layer || 'Default') === layer.name) {
                        s._layerVisible = layer.visible;
                    }
                }
                this._renderer.render();
                this.render();
            });
            header.appendChild(visBtn);

            // Lock toggle
            const lockBtn = document.createElement('button');
            lockBtn.className = 'layer-lock-btn';
            lockBtn.textContent = layer.locked ? '\u{1F512}' : '\u{1F513}';
            lockBtn.title = 'Toggle lock';
            lockBtn.addEventListener('click', (e) => {
                e.stopPropagation();
                layer.locked = !layer.locked;
                this.render();
            });
            header.appendChild(lockBtn);

            // Name
            const name = document.createElement('span');
            name.className = 'layer-name';
            name.textContent = layer.name;
            header.appendChild(name);

            // Shape count
            const count = document.createElement('span');
            count.className = 'layer-count';
            count.textContent = (shapesByLayer[layer.name] || []).length;
            header.appendChild(count);

            // Delete button (not for Default)
            if (layer.name !== 'Default') {
                const delBtn = document.createElement('button');
                delBtn.className = 'layer-del-btn';
                delBtn.textContent = '\u00D7';
                delBtn.title = 'Delete layer';
                delBtn.addEventListener('click', (e) => {
                    e.stopPropagation();
                    if (confirm(`Delete layer "${layer.name}"? Shapes will move to Default.`)) {
                        this.removeLayer(layer.name);
                    }
                });
                header.appendChild(delBtn);
            }

            header.addEventListener('click', () => this.setActiveLayer(layer.name));
            layerEl.appendChild(header);
            el.appendChild(layerEl);
        }
    }
}
