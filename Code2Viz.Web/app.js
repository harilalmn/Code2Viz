// Code2Viz Web - Main Application (Folder-based project)
import { CanvasRenderer } from './renderer.js';
import {
    EPSILON, VXYZ, BoundingBox, GeometryHelper, Shape, ShapeDefaults,
    VPoint, VLine, VCircle, VArc, VRectangle, VEllipse,
    VPolygon, VPolyline, VSpline, VBezier, VArrow,
    VText, VFont, VFontWeight, VTextAnchor,
    VDimension, VRadialDimension, VGroup, VGrid, VColor,
    LineTypes, VizConsole,
    resetIdCounter, clearRegistry, getRegistry,
} from './geometry/index.js';
import { vizHint, setupParameterHints } from './intellisense.js';
import { exportSVG, exportPNG, exportDXF, exportPDF, downloadText, downloadDataURL, downloadBlob } from './exporter.js';
import { SelectionManager } from './selection.js';
import { ToolManager, SnapEngine } from './tools.js';
import { PropertiesPanel } from './properties.js';
import { Minimap } from './minimap.js';
import { Animator, Timeline, Easing, DrawAnimation, MoveAnimation, FadeAnimation, ScaleAnimation, RotateAnimation, ColorAnimation } from './animation.js';
import { LayerManager } from './layers.js';
import { polygonIntersection, polygonDifference, polygonUnion, pointInPolygon } from './geometry/boolean.js';
import { linearArray, rectangularArray, polarArray, pathArray, mirror } from './geometry/arrays.js';
import { HelpPanel } from './help.js';

// ============================================================================
// App State
// ============================================================================
let renderer = null;
let editor = null;
let vizConsole = new VizConsole();
let selectionMgr = null;
let toolMgr = null;
let snapEngine = null;
let propertiesPanel = null;
let minimap = null;
let animator = null;
let layerMgr = null;
let helpPanel = null;

const ENTRY_FILE = 'main.js';
const STORAGE_KEY = 'code2viz_project';

// Error markers
let _errorMarkers = [];

// ============================================================================
// Project Model
// ============================================================================
const files = new Map();
let activeFile = ENTRY_FILE;
let dirHandle = null;
let projectName = '';

const DEFAULT_MAIN = `// main.js — Entry point
// Other .js files in this folder are loaded first.
// Their classes and functions are available here.

let origin = new VPoint(0, 0);
origin.color = "Yellow";

let circle = new VCircle(origin, 50);
circle.color = "Crimson";
circle.lineWeight = 3;

let rect = new VRectangle(-80, -30, 160, 60);
rect.color = "DodgerBlue";
rect.fillColor = "rgba(30, 144, 255, 0.15)";

let tri = new VPolygon(
    VPoint.internal(0, 80),
    VPoint.internal(-40, 30),
    VPoint.internal(40, 30)
);
tri.color = "LimeGreen";
tri.fillColor = "rgba(50, 205, 50, 0.2)";
tri.lineWeight = 2;

for (let i = 0; i < 12; i++) {
    let angle = (i * 30) * Math.PI / 180;
    let line = new VLine(0, 0, 100 * Math.cos(angle), 100 * Math.sin(angle));
    line.color = VColor.getRandomVibrantColor();
    line.lineWeight = 1;
    line.lineType = "Dashed";
}

let label = new VText(VPoint.internal(-60, -50), "Code2Viz Web!", 14);
label.color = "White";
label.font = VFont.Consolas;
label.fontWeight = VFontWeight.Bold;

console.log("Shapes created: " + getRegistry().length);
`;

// ============================================================================
// Folder / File System Access API
// ============================================================================
async function openFolder() {
    if (!('showDirectoryPicker' in window)) {
        alert('Your browser does not support the File System Access API.\\nPlease use Chrome or Edge.');
        return;
    }

    try {
        dirHandle = await window.showDirectoryPicker({ mode: 'readwrite' });
    } catch (e) {
        if (e.name === 'AbortError') return;
        throw e;
    }

    projectName = dirHandle.name;
    document.getElementById('project-name').textContent = projectName;
    await loadFilesFromFolder();
}

async function loadFilesFromFolder() {
    flushEditorToFile();
    files.clear();

    const entries = [];
    for await (const [name, handle] of dirHandle.entries()) {
        if (handle.kind === 'file' && name.endsWith('.js')) {
            entries.push({ name, handle });
        }
    }

    entries.sort((a, b) => {
        if (a.name === ENTRY_FILE) return -1;
        if (b.name === ENTRY_FILE) return 1;
        return a.name.localeCompare(b.name);
    });

    for (const { name, handle } of entries) {
        const file = await handle.getFile();
        const content = await file.text();
        files.set(name, { content, dirty: false, handle, history: null });
    }

    if (!files.has(ENTRY_FILE)) {
        const handle = await dirHandle.getFileHandle(ENTRY_FILE, { create: true });
        const writable = await handle.createWritable();
        await writable.write(DEFAULT_MAIN);
        await writable.close();
        files.set(ENTRY_FILE, { content: DEFAULT_MAIN, dirty: false, handle, history: null });
    }

    activeFile = ENTRY_FILE;
    const mf = files.get(ENTRY_FILE);
    editor.setValue(mf.content);
    editor.getDoc().clearHistory();

    renderTree();
    renderTabs();
    appendConsole(`Opened project: ${projectName} (${files.size} file${files.size !== 1 ? 's' : ''})`, 'info');
}

async function saveFileToDisk(name) {
    const f = files.get(name);
    if (!f) return;

    if (f.handle) {
        try {
            const writable = await f.handle.createWritable();
            await writable.write(f.content);
            await writable.close();
            f.dirty = false;
        } catch (e) {
            appendConsole(`Save error (${name}): ${e.message}`, 'error');
        }
    } else if (dirHandle) {
        try {
            const handle = await dirHandle.getFileHandle(name, { create: true });
            const writable = await handle.createWritable();
            await writable.write(f.content);
            await writable.close();
            f.handle = handle;
            f.dirty = false;
        } catch (e) {
            appendConsole(`Save error (${name}): ${e.message}`, 'error');
        }
    } else {
        downloadText(f.content, name, 'application/javascript');
        f.dirty = false;
    }
}

async function saveAll() {
    flushEditorToFile();
    saveProjectToStorage();

    let savedCount = 0;
    for (const [name, f] of files) {
        if (f.dirty || dirHandle) {
            await saveFileToDisk(name);
            savedCount++;
        }
    }

    renderTree();
    renderTabs();
    if (dirHandle) {
        appendConsole(`Saved ${savedCount} file(s) to ${projectName}/`, 'info');
    } else {
        appendConsole('Saved to browser storage', 'info');
    }
}

async function createNewFileInFolder(name) {
    const fname = name.endsWith('.js') ? name : name + '.js';
    if (files.has(fname)) {
        switchToFile(fname);
        return;
    }

    const content = `// ${fname}\n\n`;
    let handle = null;

    if (dirHandle) {
        try {
            handle = await dirHandle.getFileHandle(fname, { create: true });
            const writable = await handle.createWritable();
            await writable.write(content);
            await writable.close();
        } catch (e) {
            appendConsole(`Error creating ${fname}: ${e.message}`, 'error');
            return;
        }
    }

    files.set(fname, { content, dirty: false, handle, history: null });
    renderTree();
    switchToFile(fname);
}

async function deleteFileFromFolder(name) {
    if (name === ENTRY_FILE) return;
    if (!confirm(`Delete "${name}"?`)) return;

    if (dirHandle) {
        try {
            await dirHandle.removeEntry(name);
        } catch (e) {
            appendConsole(`Delete error: ${e.message}`, 'error');
        }
    }

    files.delete(name);
    if (activeFile === name) {
        activeFile = ENTRY_FILE;
        const mf = files.get(ENTRY_FILE);
        editor.setValue(mf.content);
        if (mf.history) editor.getDoc().setHistory(mf.history);
        else editor.getDoc().clearHistory();
    }

    renderTree();
    renderTabs();
}

async function renameFileInFolder(oldName) {
    if (oldName === ENTRY_FILE) return;
    const newName = prompt('Rename file:', oldName);
    if (!newName || newName === oldName) return;
    const fname = newName.endsWith('.js') ? newName : newName + '.js';
    if (files.has(fname)) { alert(`"${fname}" already exists.`); return; }

    const f = files.get(oldName);

    if (dirHandle) {
        try {
            const handle = await dirHandle.getFileHandle(fname, { create: true });
            const writable = await handle.createWritable();
            await writable.write(f.content);
            await writable.close();
            await dirHandle.removeEntry(oldName);
            f.handle = handle;
        } catch (e) {
            appendConsole(`Rename error: ${e.message}`, 'error');
            return;
        }
    }

    files.delete(oldName);
    files.set(fname, f);
    if (activeFile === oldName) activeFile = fname;
    renderTree();
    renderTabs();
}

// ============================================================================
// localStorage fallback
// ============================================================================
function saveProjectToStorage() {
    const proj = { files: {}, activeFile, projectName };
    for (const [name, f] of files) proj.files[name] = f.content;
    localStorage.setItem(STORAGE_KEY, JSON.stringify(proj));
}

function loadProjectFromStorage() {
    const saved = localStorage.getItem(STORAGE_KEY);
    if (!saved) return false;
    try {
        const proj = JSON.parse(saved);
        for (const [name, content] of Object.entries(proj.files)) {
            files.set(name, { content, dirty: false, handle: null, history: null });
        }
        activeFile = proj.activeFile || ENTRY_FILE;
        projectName = proj.projectName || '';
        return true;
    } catch (e) { return false; }
}

// ============================================================================
// File Tree
// ============================================================================
function renderTree() {
    const emptyEl = document.getElementById('file-tree-empty');
    const listEl = document.getElementById('file-tree-list');

    if (files.size === 0) {
        emptyEl.style.display = '';
        listEl.style.display = 'none';
        return;
    }

    emptyEl.style.display = 'none';
    listEl.style.display = '';
    listEl.innerHTML = '';

    const names = [...files.keys()].sort((a, b) => {
        if (a === ENTRY_FILE) return -1;
        if (b === ENTRY_FILE) return 1;
        return a.localeCompare(b);
    });

    for (const name of names) {
        const f = files.get(name);
        const item = document.createElement('div');
        item.className = 'tree-item' + (name === activeFile ? ' active' : '');
        item.dataset.file = name;

        const icon = document.createElement('span');
        icon.className = 'tree-item-icon ' + (name === ENTRY_FILE ? 'file-entry' : 'file-js');
        icon.textContent = name === ENTRY_FILE ? '\u25B6' : 'JS';
        item.appendChild(icon);

        const label = document.createElement('span');
        label.className = 'tree-item-name';
        label.textContent = name;
        item.appendChild(label);

        if (name === ENTRY_FILE) {
            const badge = document.createElement('span');
            badge.className = 'tree-item-badge';
            badge.textContent = 'entry';
            item.appendChild(badge);
        }

        if (f.dirty) {
            const dot = document.createElement('span');
            dot.className = 'tree-item-dirty';
            dot.textContent = '\u25CF';
            item.appendChild(dot);
        }

        item.addEventListener('click', () => switchToFile(name));
        item.addEventListener('contextmenu', (e) => {
            e.preventDefault();
            showTreeContextMenu(e, name);
        });

        listEl.appendChild(item);
    }
}

let _ctxMenu = null;
function showTreeContextMenu(e, fileName) {
    hideTreeContextMenu();

    const menu = document.createElement('div');
    menu.className = 'dropdown-menu open';
    menu.style.position = 'fixed';
    menu.style.left = e.clientX + 'px';
    menu.style.top = e.clientY + 'px';
    menu.style.zIndex = '600';

    const items = [];
    items.push({ label: 'Open', action: () => switchToFile(fileName) });
    if (fileName !== ENTRY_FILE) {
        items.push({ label: 'Rename...', action: () => renameFileInFolder(fileName) });
        items.push({ label: 'Delete', action: () => deleteFileFromFolder(fileName) });
    }

    for (const { label, action } of items) {
        const btn = document.createElement('button');
        btn.className = 'dropdown-item';
        btn.textContent = label;
        btn.addEventListener('click', () => {
            hideTreeContextMenu();
            action();
        });
        menu.appendChild(btn);
    }

    document.body.appendChild(menu);
    _ctxMenu = menu;
    setTimeout(() => {
        document.addEventListener('click', hideTreeContextMenu, { once: true });
    }, 0);
}

function hideTreeContextMenu() {
    if (_ctxMenu) { _ctxMenu.remove(); _ctxMenu = null; }
}

// ============================================================================
// Tab Bar
// ============================================================================
const openTabs = new Set();

function ensureTab(name) {
    openTabs.add(name);
}

function renderTabs() {
    const tabList = document.getElementById('tab-list');
    tabList.innerHTML = '';
    ensureTab(activeFile);

    const tabNames = [...openTabs].sort((a, b) => {
        if (a === ENTRY_FILE) return -1;
        if (b === ENTRY_FILE) return 1;
        return 0;
    });

    for (const name of tabNames) {
        if (!files.has(name)) { openTabs.delete(name); continue; }
        const f = files.get(name);

        const tab = document.createElement('div');
        tab.className = 'tab' + (name === activeFile ? ' tab-active' : '');

        if (name === ENTRY_FILE) {
            const badge = document.createElement('span');
            badge.className = 'tab-entry-badge';
            badge.textContent = '\u25B6';
            tab.appendChild(badge);
        }

        const label = document.createElement('span');
        label.className = 'tab-label';
        label.textContent = name;
        tab.appendChild(label);

        if (f.dirty) {
            const dot = document.createElement('span');
            dot.className = 'tab-dirty';
            dot.textContent = '\u25CF';
            tab.appendChild(dot);
        }

        const close = document.createElement('span');
        close.className = 'tab-close';
        close.textContent = '\u00D7';
        close.title = 'Close tab';
        close.addEventListener('click', (e) => {
            e.stopPropagation();
            closeTab(name);
        });
        tab.appendChild(close);

        tab.addEventListener('click', () => switchToFile(name));
        tabList.appendChild(tab);
    }

    document.title = `${activeFile} - ${projectName || 'Code2Viz Web'}`;
}

function closeTab(name) {
    if (openTabs.size <= 1) return;
    openTabs.delete(name);
    if (activeFile === name) {
        const next = [...openTabs][0] || ENTRY_FILE;
        ensureTab(next);
        switchToFile(next);
    } else {
        renderTabs();
    }
}

// ============================================================================
// File Switching
// ============================================================================
function flushEditorToFile() {
    if (editor && files.has(activeFile)) {
        const f = files.get(activeFile);
        f.content = editor.getValue();
        f.history = editor.getDoc().getHistory();
    }
}

function switchToFile(name) {
    if (!files.has(name)) return;
    if (name === activeFile && openTabs.has(name)) return;

    flushEditorToFile();
    activeFile = name;
    ensureTab(name);

    const f = files.get(name);
    editor.setValue(f.content);
    if (f.history) editor.getDoc().setHistory(f.history);
    else editor.getDoc().clearHistory();

    renderTabs();
    renderTree();
    editor.focus();
}

// ============================================================================
// Error Line Highlighting
// ============================================================================
function clearErrorMarkers() {
    for (const marker of _errorMarkers) {
        editor.removeLineClass(marker.line, 'background');
        if (marker.widget) marker.widget.clear();
    }
    _errorMarkers = [];
}

function addErrorMarker(lineNum, message) {
    if (lineNum < 0 || lineNum >= editor.lineCount()) return;
    editor.addLineClass(lineNum, 'background', 'cm-error-line');

    // Error message widget below the line
    const msgEl = document.createElement('div');
    msgEl.className = 'cm-error-msg';
    msgEl.textContent = message;
    const widget = editor.addLineWidget(lineNum, msgEl, { coverGutter: false, noHScroll: true });

    _errorMarkers.push({ line: lineNum, widget });
}

// ============================================================================
// Code Formatting
// ============================================================================
function formatCode() {
    const code = editor.getValue();
    let formatted = '';
    let indent = 0;
    const lines = code.split('\n');

    for (let line of lines) {
        line = line.trim();
        if (line === '') { formatted += '\n'; continue; }

        // Decrease indent for closing braces
        if (line.startsWith('}') || line.startsWith(']') || line.startsWith(')')) {
            indent = Math.max(0, indent - 1);
        }

        formatted += '    '.repeat(indent) + line + '\n';

        // Increase indent for opening braces
        const opens = (line.match(/[{[(]/g) || []).length;
        const closes = (line.match(/[}\])]/g) || []).length;
        indent += opens - closes;
        if (indent < 0) indent = 0;
    }

    editor.setValue(formatted.trimEnd() + '\n');
    if (files.has(activeFile)) {
        files.get(activeFile).dirty = true;
        renderTabs();
        renderTree();
    }
}

// ============================================================================
// CodeMirror Editor
// ============================================================================
function initEditor() {
    editor = CodeMirror(document.getElementById('editor-container'), {
        value: '',
        mode: 'javascript',
        theme: 'material-darker',
        lineNumbers: true,
        matchBrackets: true,
        autoCloseBrackets: true,
        styleActiveLine: true,
        foldGutter: true,
        gutters: ['CodeMirror-linenumbers', 'CodeMirror-foldgutter'],
        indentUnit: 4,
        tabSize: 4,
        indentWithTabs: false,
        lineWrapping: false,
        highlightSelectionMatches: { showToken: /\w/, annotateScrollbar: false },
        hintOptions: { hint: vizHint, completeSingle: false, alignWithWord: true },
        extraKeys: {
            'F5': () => runCode(),
            'Ctrl-Enter': () => runCode(),
            'Ctrl-/': 'toggleComment',
            'Ctrl-Space': (cm) => cm.showHint(),
            'Ctrl-F': 'findPersistent',
            'Ctrl-H': 'replace',
            'Ctrl-Shift-F': () => formatCode(),
            'Ctrl-G': 'jumpToLine',
        },
    });

    editor.on('inputRead', (cm, change) => {
        if (change.origin === '+input' || change.origin === '+delete') {
            const ch = change.text[change.text.length - 1];
            if (ch === '.') {
                cm.showHint({ hint: vizHint, completeSingle: false });
            } else if (/\w/.test(ch)) {
                const cursor = cm.getCursor();
                const line = cm.getLine(cursor.line);
                let s = cursor.ch;
                while (s > 0 && /\w/.test(line[s - 1])) s--;
                if (cursor.ch - s >= 2) cm.showHint({ hint: vizHint, completeSingle: false });
            }
        }
    });

    setupParameterHints(editor);

    editor.on('change', () => {
        clearErrorMarkers();
        if (files.has(activeFile)) {
            const f = files.get(activeFile);
            if (!f.dirty) { f.dirty = true; renderTabs(); renderTree(); }
        }
    });

    // Load initial content
    if (files.has(activeFile)) {
        const f = files.get(activeFile);
        editor.setValue(f.content);
        editor.getDoc().clearHistory();
    }
}

// ============================================================================
// Code Execution
// ============================================================================
function runCode() {
    flushEditorToFile();
    clearErrorMarkers();

    const consolePanel = document.getElementById('console-output');
    resetIdCounter();
    clearRegistry();
    ShapeDefaults.reset();
    vizConsole.clear();
    consolePanel.innerHTML = '';

    // Stop any running animation
    if (animator) animator.stop();

    const startTime = performance.now();

    try {
        let combinedCode = '';
        const fileOrder = [];
        const lineOffsets = [];

        let currentLine = 0;
        for (const [name, f] of [...files.entries()].sort((a, b) => {
            if (a[0] === ENTRY_FILE) return 1;
            if (b[0] === ENTRY_FILE) return -1;
            return a[0].localeCompare(b[0]);
        })) {
            const header = `// === ${name} ===\n`;
            combinedCode += header;
            currentLine += 1;
            lineOffsets.push({ file: name, startLine: currentLine });
            combinedCode += f.content + '\n\n';
            currentLine += f.content.split('\n').length + 1;
            fileOrder.push(name);
        }

        const fn = new Function(
            'EPSILON', 'VXYZ', 'BoundingBox', 'GeometryHelper', 'Shape', 'ShapeDefaults',
            'VPoint', 'VLine', 'VCircle', 'VArc', 'VRectangle', 'VEllipse',
            'VPolygon', 'VPolyline', 'VSpline', 'VBezier', 'VArrow',
            'VText', 'VFont', 'VFontWeight', 'VTextAnchor',
            'VDimension', 'VRadialDimension', 'VGroup', 'VGrid', 'VColor',
            'LineTypes', 'getRegistry', 'console',
            // Animation
            'Animator', 'Timeline', 'Easing', 'DrawAnimation', 'MoveAnimation',
            'FadeAnimation', 'ScaleAnimation', 'RotateAnimation', 'ColorAnimation',
            // Boolean ops
            'polygonIntersection', 'polygonDifference', 'polygonUnion', 'pointInPolygon',
            // Array ops
            'linearArray', 'rectangularArray', 'polarArray', 'pathArray', 'mirror',
            combinedCode
        );

        const proxyConsole = {
            log: (...args) => vizConsole.log(...args),
            warn: (...args) => vizConsole.warn(...args),
            error: (...args) => vizConsole.error(...args),
            info: (...args) => vizConsole.info(...args),
            clear: () => vizConsole.clear(),
        };

        // Create animation proxy for user code
        const userAnimator = {
            play: () => { if (animator.timeline) animator.play(); },
            pause: () => animator.pause(),
            stop: () => animator.stop(),
            setTimeline: (tl) => {
                animator.setTimeline(tl);
                showAnimationBar(true);
            },
            get isPlaying() { return animator.isPlaying; },
            set speed(v) { animator.speed = v; },
            set loop(v) { animator.loop = v; },
        };

        fn(
            EPSILON, VXYZ, BoundingBox, GeometryHelper, Shape, ShapeDefaults,
            VPoint, VLine, VCircle, VArc, VRectangle, VEllipse,
            VPolygon, VPolyline, VSpline, VBezier, VArrow,
            VText, VFont, VFontWeight, VTextAnchor,
            VDimension, VRadialDimension, VGroup, VGrid, VColor,
            LineTypes, getRegistry, proxyConsole,
            // Animation
            userAnimator, Timeline, Easing, DrawAnimation, MoveAnimation,
            FadeAnimation, ScaleAnimation, RotateAnimation, ColorAnimation,
            // Boolean ops
            polygonIntersection, polygonDifference, polygonUnion, pointInPolygon,
            // Array ops
            linearArray, rectangularArray, polarArray, pathArray, mirror
        );

        const elapsed = (performance.now() - startTime).toFixed(1);
        const shapeCount = getRegistry().length;

        for (const msg of vizConsole.messages) appendConsole(msg.text, msg.type);

        const fileInfo = fileOrder.length > 1 ? ` (${fileOrder.length} files)` : '';
        appendConsole(`Executed in ${elapsed}ms${fileInfo} \u2014 ${shapeCount} shape${shapeCount !== 1 ? 's' : ''} created`, 'info');

        // Reset selection
        if (selectionMgr) selectionMgr.deselectAll();

        renderer.render();
        if (shapeCount > 0) renderer.zoomToFit();
        if (minimap) minimap.render();
        if (layerMgr) layerMgr.render();

    } catch (err) {
        let lineInfo = '';
        let errorFile = null;
        let errorLine = -1;

        if (err.stack) {
            const match = err.stack.match(/<anonymous>:(\d+):(\d+)/);
            if (match) {
                const mapped = mapLineToFile(parseInt(match[1]));
                if (mapped) {
                    lineInfo = ` (${mapped.file}:${mapped.line}, col ${match[2]})`;
                    errorFile = mapped.file;
                    errorLine = mapped.line - 1; // 0-indexed
                } else {
                    lineInfo = ` (line ${match[1]})`;
                }
            }
        }

        for (const msg of vizConsole.messages) appendConsole(msg.text, msg.type);
        appendConsole(`Error${lineInfo}: ${err.message}`, 'error');

        // Highlight error line in editor
        if (errorFile && errorFile === activeFile && errorLine >= 0) {
            addErrorMarker(errorLine, err.message);
        } else if (errorFile && errorFile !== activeFile) {
            // Switch to the file with the error
            switchToFile(errorFile);
            if (errorLine >= 0) {
                setTimeout(() => addErrorMarker(errorLine, err.message), 100);
            }
        }
    }
}

function mapLineToFile(rawLine) {
    let offset = 0;
    const sorted = [...files.entries()].sort((a, b) => {
        if (a[0] === ENTRY_FILE) return 1;
        if (b[0] === ENTRY_FILE) return -1;
        return a[0].localeCompare(b[0]);
    });
    for (const [name, f] of sorted) {
        const headerLines = 1;
        const contentLines = f.content.split('\n').length;
        const blockLines = headerLines + contentLines + 1;
        if (rawLine <= offset + blockLines) {
            return { file: name, line: rawLine - offset - headerLines };
        }
        offset += blockLines;
    }
    return null;
}

function appendConsole(text, type = 'log') {
    const el = document.getElementById('console-output');
    const div = document.createElement('div');
    div.className = `console-${type}`;
    div.textContent = text;
    el.appendChild(div);
    el.scrollTop = el.scrollHeight;
}

// ============================================================================
// Animation Bar
// ============================================================================
function showAnimationBar(show) {
    document.getElementById('animation-bar').style.display = show ? 'flex' : 'none';
}

function initAnimationControls() {
    document.getElementById('btn-anim-play').addEventListener('click', () => animator.play());
    document.getElementById('btn-anim-pause').addEventListener('click', () => animator.pause());
    document.getElementById('btn-anim-stop').addEventListener('click', () => {
        animator.stop();
        showAnimationBar(false);
    });
    document.getElementById('anim-loop').addEventListener('change', (e) => {
        animator.loop = e.target.checked;
    });

    animator.onUpdate((t, duration) => {
        const slider = document.getElementById('anim-progress');
        slider.value = (t / duration) * 100;
        document.getElementById('anim-time').textContent = `${t.toFixed(1)}s / ${duration.toFixed(1)}s`;
        if (minimap) minimap.render();
    });

    animator.onComplete(() => {
        appendConsole('Animation complete', 'info');
    });
}

// ============================================================================
// Canvas & Modules
// ============================================================================
function initCanvas() {
    const canvas = document.getElementById('viz-canvas');
    renderer = new CanvasRenderer(canvas);

    renderer.onCoordsUpdate((x, y) => {
        document.getElementById('coords').textContent = `X: ${x.toFixed(2)}  Y: ${y.toFixed(2)}`;
    });

    // Initialize snap engine
    snapEngine = new SnapEngine(renderer);

    // Initialize selection manager
    selectionMgr = new SelectionManager(renderer);
    selectionMgr.onChange((shapes) => {
        if (propertiesPanel) propertiesPanel.update(shapes);
    });

    // Initialize tool manager
    toolMgr = new ToolManager(renderer, snapEngine, selectionMgr, (shape) => {
        appendConsole(`Created ${shape.constructor.name} #${shape.id}`, 'info');
        renderer.render();
        if (minimap) minimap.render();
        if (layerMgr && layerMgr.activeLayer) {
            shape._layer = layerMgr.activeLayer;
        }
    });

    // Initialize animator
    animator = new Animator(renderer);

    // Initialize minimap
    minimap = new Minimap(renderer, document.getElementById('minimap-container'));

    // Initialize properties panel
    propertiesPanel = new PropertiesPanel(document.getElementById('properties-panel'), renderer);

    // Initialize layer manager
    layerMgr = new LayerManager(document.getElementById('layers-panel'), renderer);
    renderer.setLayerManager(layerMgr);

    helpPanel = new HelpPanel();

    // Help button
    document.getElementById('btn-help').addEventListener('click', () => helpPanel.toggle());

    // Register overlays
    renderer.addOverlay((ctx) => selectionMgr.renderOverlay(ctx));
    renderer.addOverlay((ctx) => toolMgr.renderOverlay(ctx));

    // Canvas mouse events for selection and tools
    const canvasEl = canvas;
    canvasEl.addEventListener('mousedown', (e) => {
        if (e.button !== 0) return; // left click only
        if (e.shiftKey) return; // shift = pan

        const rect = canvasEl.getBoundingClientRect();
        const sx = e.clientX - rect.left, sy = e.clientY - rect.top;

        // Try tools first
        if (toolMgr.activeTool !== 'pointer') {
            toolMgr.handleMouseDown(e.clientX, e.clientY, e.ctrlKey, e.shiftKey);
            return;
        }

        // Then selection
        selectionMgr.handleMouseDown(sx, sy, e.ctrlKey, e.shiftKey);
    });

    canvasEl.addEventListener('mousemove', (e) => {
        const rect = canvasEl.getBoundingClientRect();
        const sx = e.clientX - rect.left, sy = e.clientY - rect.top;

        if (toolMgr.activeTool !== 'pointer') {
            toolMgr.handleMouseMove(e.clientX, e.clientY);
        } else {
            selectionMgr.handleMouseMove(sx, sy);
        }
    });

    canvasEl.addEventListener('mouseup', (e) => {
        const rect = canvasEl.getBoundingClientRect();
        const sx = e.clientX - rect.left, sy = e.clientY - rect.top;
        selectionMgr.handleMouseUp(sx, sy, e.ctrlKey);
        toolMgr.handleMouseUp();
    });

    // Resize
    function resize() {
        const c = document.getElementById('canvas-container');
        renderer.resize(c.clientWidth, c.clientHeight);
        if (minimap) minimap.render();
    }

    new ResizeObserver(resize).observe(document.getElementById('canvas-container'));
    resize();
}

// ============================================================================
// Export
// ============================================================================
function handleExport(format) {
    const baseName = projectName || 'code2viz';
    switch (format) {
        case 'svg':
            downloadText(exportSVG(), `${baseName}.svg`, 'image/svg+xml');
            appendConsole(`Exported: ${baseName}.svg`, 'info');
            break;
        case 'png':
            downloadDataURL(exportPNG(renderer), `${baseName}.png`);
            appendConsole(`Exported: ${baseName}.png`, 'info');
            break;
        case 'pdf':
            appendConsole('Generating PDF...', 'info');
            exportPDF().then(blob => {
                downloadBlob(blob, `${baseName}.pdf`);
                appendConsole(`Exported: ${baseName}.pdf`, 'info');
            }).catch(err => appendConsole(`PDF error: ${err.message}`, 'error'));
            break;
        case 'dxf':
            downloadText(exportDXF(), `${baseName}.dxf`, 'application/dxf');
            appendConsole(`Exported: ${baseName}.dxf`, 'info');
            break;
    }
}

// ============================================================================
// UI Setup
// ============================================================================
function initUI() {
    // Toolbar
    document.getElementById('btn-run').addEventListener('click', runCode);
    document.getElementById('btn-fit').addEventListener('click', () => { renderer.zoomToFit(); if (minimap) minimap.render(); });
    document.getElementById('btn-clear').addEventListener('click', () => {
        resetIdCounter(); clearRegistry(); renderer.render();
        document.getElementById('console-output').innerHTML = '';
        appendConsole('Canvas cleared', 'info');
        if (selectionMgr) selectionMgr.deselectAll();
        if (minimap) minimap.render();
        if (layerMgr) layerMgr.render();
    });

    // File management
    document.getElementById('btn-open-folder').addEventListener('click', openFolder);
    document.getElementById('btn-open-folder-tree').addEventListener('click', openFolder);
    document.getElementById('btn-new-file').addEventListener('click', () => {
        const name = prompt('New file name:', 'utils.js');
        if (name) createNewFileInFolder(name);
    });
    document.getElementById('btn-add-tab').addEventListener('click', () => {
        const name = prompt('New file name:', 'utils.js');
        if (name) createNewFileInFolder(name);
    });
    document.getElementById('btn-save').addEventListener('click', saveAll);

    // Toggle project browser
    document.getElementById('btn-toggle-tree').addEventListener('click', () => {
        document.getElementById('project-panel').classList.toggle('collapsed');
    });

    // Export dropdown
    const exportBtn = document.getElementById('btn-export');
    const exportMenu = document.getElementById('export-menu');
    exportBtn.addEventListener('click', (e) => { e.stopPropagation(); exportMenu.classList.toggle('open'); });
    document.addEventListener('click', () => exportMenu.classList.remove('open'));
    exportMenu.querySelectorAll('.dropdown-item').forEach(item => {
        item.addEventListener('click', (e) => {
            e.stopPropagation(); exportMenu.classList.remove('open');
            handleExport(item.dataset.format);
        });
    });

    // Drawing tools
    const toolBtns = document.querySelectorAll('.tool-btn');
    toolBtns.forEach(btn => {
        btn.addEventListener('click', () => {
            const tool = btn.dataset.tool;
            toolMgr.setTool(tool);
            toolBtns.forEach(b => b.classList.remove('tool-active'));
            btn.classList.add('tool-active');
            const toolNames = { pointer: 'Pointer', point: 'Point', line: 'Line', circle: 'Circle', rectangle: 'Rectangle', measure: 'Measure' };
            document.getElementById('active-tool').textContent = toolNames[tool] || tool;
        });
    });

    // Toggle buttons
    const snapBtn = document.getElementById('btn-snap');
    snapBtn.addEventListener('click', () => {
        snapEngine.enabled = !snapEngine.enabled;
        snapBtn.classList.toggle('btn-toggle-on', snapEngine.enabled);
    });

    document.getElementById('btn-minimap').addEventListener('click', () => {
        minimap.toggle();
        document.getElementById('btn-minimap').classList.toggle('btn-toggle-on', minimap.visible);
    });

    document.getElementById('btn-props').addEventListener('click', () => {
        propertiesPanel.toggle();
        document.getElementById('btn-props').classList.toggle('btn-toggle-on', propertiesPanel.visible);
        document.getElementById('properties-splitter').style.display = propertiesPanel.visible ? '' : 'none';
    });

    document.getElementById('btn-close-props').addEventListener('click', () => {
        propertiesPanel.visible = false;
        document.getElementById('btn-props').classList.remove('btn-toggle-on');
        document.getElementById('properties-splitter').style.display = 'none';
    });

    document.getElementById('btn-layers').addEventListener('click', () => {
        layerMgr.toggle();
        document.getElementById('btn-layers').classList.toggle('btn-toggle-on', layerMgr.visible);
    });

    document.getElementById('btn-close-layers').addEventListener('click', () => {
        layerMgr.visible = false;
        document.getElementById('btn-layers').classList.remove('btn-toggle-on');
    });

    // Keyboard shortcuts
    document.addEventListener('keydown', (e) => {
        if (e.ctrlKey && e.key === 's') { e.preventDefault(); saveAll(); }
        if (e.ctrlKey && e.key === 'n') { e.preventDefault(); const n = prompt('New file name:', 'utils.js'); if (n) createNewFileInFolder(n); }
        if (e.ctrlKey && e.key === 'o') { e.preventDefault(); openFolder(); }
        if (e.ctrlKey && e.key === 'Tab') {
            e.preventDefault();
            const tabs = [...openTabs];
            const idx = tabs.indexOf(activeFile);
            const next = e.shiftKey ? (idx - 1 + tabs.length) % tabs.length : (idx + 1) % tabs.length;
            switchToFile(tabs[next]);
        }
        if (e.ctrlKey && e.key === 'w') {
            e.preventDefault();
            if (openTabs.size > 1) closeTab(activeFile);
        }

        // F1 - Help
        if (e.key === 'F1') {
            e.preventDefault();
            helpPanel.toggle();
        }

        // F4 - Properties
        if (e.key === 'F4') {
            e.preventDefault();
            propertiesPanel.toggle();
            document.getElementById('btn-props').classList.toggle('btn-toggle-on', propertiesPanel.visible);
            document.getElementById('properties-splitter').style.display = propertiesPanel.visible ? '' : 'none';
        }

        // F9 - Snap toggle
        if (e.key === 'F9') {
            e.preventDefault();
            snapEngine.enabled = !snapEngine.enabled;
            snapBtn.classList.toggle('btn-toggle-on', snapEngine.enabled);
        }

        // Ctrl+Shift+M - Minimap
        if (e.ctrlKey && e.shiftKey && e.key === 'M') {
            e.preventDefault();
            minimap.toggle();
            document.getElementById('btn-minimap').classList.toggle('btn-toggle-on', minimap.visible);
        }

        // Ctrl+M - Measuring tape
        if (e.ctrlKey && !e.shiftKey && e.key === 'm') {
            e.preventDefault();
            toolMgr.setTool('measure');
            toolBtns.forEach(b => b.classList.toggle('tool-active', b.dataset.tool === 'measure'));
            document.getElementById('active-tool').textContent = 'Measure';
        }

        // Escape - close help or back to pointer
        if (e.key === 'Escape') {
            if (helpPanel && helpPanel.visible) {
                helpPanel.hide();
            } else {
                toolMgr.setTool('pointer');
                toolBtns.forEach(b => b.classList.toggle('tool-active', b.dataset.tool === 'pointer'));
                document.getElementById('active-tool').textContent = 'Pointer';
            }
        }

        // Delete - delete selected shapes
        if (e.key === 'Delete' && selectionMgr.hasSelection) {
            if (!editor.hasFocus()) {
                selectionMgr.deleteSelected();
                if (minimap) minimap.render();
            }
        }

        // Drawing tool shortcuts (only when editor not focused)
        if (!editor.hasFocus() && !e.ctrlKey && !e.altKey) {
            const toolKeys = { 'v': 'pointer', 'p': 'point', 'l': 'line', 'c': 'circle', 'r': 'rectangle', 'm': 'measure' };
            const tool = toolKeys[e.key.toLowerCase()];
            if (tool) {
                toolMgr.setTool(tool);
                toolBtns.forEach(b => b.classList.toggle('tool-active', b.dataset.tool === tool));
                const toolNames = { pointer: 'Pointer', point: 'Point', line: 'Line', circle: 'Circle', rectangle: 'Rectangle', measure: 'Measure' };
                document.getElementById('active-tool').textContent = toolNames[tool];
            }
        }

        // Shift key for ortho constraint
        if (e.key === 'Shift') toolMgr.setOrthoConstrain(true);
    });

    document.addEventListener('keyup', (e) => {
        if (e.key === 'Shift') toolMgr.setOrthoConstrain(false);
    });

    initSplitters();
    initAnimationControls();
}

function initSplitters() {
    setupSplitter('splitter', 'editor-panel', 'canvas-panel', 'col', () => { renderer.render(); editor.refresh(); if (minimap) minimap.render(); });
    setupTreeSplitter();

    // Console splitter
    const consoleSplitter = document.getElementById('console-splitter');
    let isDragging = false;
    consoleSplitter.addEventListener('mousedown', (e) => { isDragging = true; document.body.style.cursor = 'row-resize'; document.body.style.userSelect = 'none'; e.preventDefault(); });
    window.addEventListener('mousemove', (e) => {
        if (!isDragging) return;
        const rect = document.getElementById('canvas-area').getBoundingClientRect();
        document.getElementById('console-panel').style.height = Math.max(40, Math.min(rect.height - 100, rect.bottom - e.clientY)) + 'px';
    });
    window.addEventListener('mouseup', () => { if (isDragging) { isDragging = false; document.body.style.cursor = ''; document.body.style.userSelect = ''; renderer.render(); } });
}

function setupSplitter(splitterId, leftId, rightId, dir, onEnd) {
    const splitter = document.getElementById(splitterId);
    let isDragging = false;
    splitter.addEventListener('mousedown', (e) => { isDragging = true; document.body.style.cursor = 'col-resize'; document.body.style.userSelect = 'none'; e.preventDefault(); });
    window.addEventListener('mousemove', (e) => {
        if (!isDragging) return;
        const container = document.getElementById('main-content');
        const rect = container.getBoundingClientRect();
        const projPanel = document.getElementById('project-panel');
        const projWidth = projPanel.classList.contains('collapsed') ? 0 : projPanel.offsetWidth;
        const treeSplitWidth = projPanel.classList.contains('collapsed') ? 0 : 4;
        const available = rect.width - projWidth - treeSplitWidth - 5;
        const editorLeft = projWidth + treeSplitWidth;
        const editorWidth = e.clientX - rect.left - editorLeft;
        const pct = (editorWidth / available) * 100;
        const clamped = Math.max(15, Math.min(85, pct));
        document.getElementById(leftId).style.flex = `0 0 ${clamped}%`;
        document.getElementById(rightId).style.flex = `0 0 ${100 - clamped}%`;
    });
    window.addEventListener('mouseup', () => { if (isDragging) { isDragging = false; document.body.style.cursor = ''; document.body.style.userSelect = ''; if (onEnd) onEnd(); } });
}

function setupTreeSplitter() {
    const splitter = document.getElementById('tree-splitter');
    let isDragging = false;
    splitter.addEventListener('mousedown', (e) => { isDragging = true; document.body.style.cursor = 'col-resize'; document.body.style.userSelect = 'none'; e.preventDefault(); });
    window.addEventListener('mousemove', (e) => {
        if (!isDragging) return;
        const container = document.getElementById('main-content');
        const rect = container.getBoundingClientRect();
        const w = Math.max(140, Math.min(400, e.clientX - rect.left));
        document.getElementById('project-panel').style.width = w + 'px';
    });
    window.addEventListener('mouseup', () => { if (isDragging) { isDragging = false; document.body.style.cursor = ''; document.body.style.userSelect = ''; editor.refresh(); } });
}

// ============================================================================
// Init
// ============================================================================
function init() {
    const restored = loadProjectFromStorage();
    if (!restored) {
        files.set(ENTRY_FILE, { content: DEFAULT_MAIN, dirty: false, handle: null, history: null });
    }
    if (!files.has(activeFile)) activeFile = ENTRY_FILE;
    ensureTab(ENTRY_FILE);

    initCanvas();
    initUI();
    initEditor();
    renderTree();
    renderTabs();

    if (projectName) document.getElementById('project-name').textContent = projectName;

    setTimeout(runCode, 300);
}

document.addEventListener('DOMContentLoaded', init);
