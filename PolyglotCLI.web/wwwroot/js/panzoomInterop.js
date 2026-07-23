// Módulo de interoperabilidad para inicializar/manipular una instancia de panzoom
// (https://github.com/timmywil/panzoom) sobre un elemento del DOM.

const instances = new Map();
let nextId = 1;

function ensurePanzoom() {
    return typeof window.panzoom === 'function';
}

function disposeInstance(id) {
    const inst = instances.get(String(id));
    if (!inst) return;
    try {
        if (typeof inst.dispose === 'function') inst.dispose();
    } catch (e) {
        console.warn('panzoom dispose error:', e);
    }
    instances.delete(String(id));
}

export function init(element, options) {
    if (!element || !ensurePanzoom()) return null;
    if (element.__pzId != null) disposeInstance(element.__pzId);

    const opts = Object.assign({
        maxZoom: 10,
        minZoom: 0.1,
        bounds: false,
        boundsPadding: 0.5,
        zoomDoubleClickSpeed: 1,
        smoothScroll: false,
        zoomSpeed: 0.25,
        beforeWheel: (e) => {
            // Permitir zoom también con Ctrl+Wheel más fino (trackpad con pinch).
            if (!e.ctrlKey) e.preventDefault();
            return true;
        }
    }, options || {});

    try {
        const instance = window.panzoom(element, opts);
        const id = String(nextId++);
        instances.set(id, instance);
        element.__pzId = id;
        return id;
    } catch (e) {
        console.error('panzoom init failed:', e);
        return null;
    }
}

export function zoomIn(id) {
    const inst = instances.get(String(id));
    if (!inst) return false;
    try { inst.zoomIn(); return true; } catch (e) { return false; }
}

export function zoomOut(id) {
    const inst = instances.get(String(id));
    if (!inst) return false;
    try { inst.zoomOut(); return true; } catch (e) { return false; }
}

export function reset(id) {
    const inst = instances.get(String(id));
    if (!inst) return false;
    try { inst.reset(); return true; } catch (e) { return false; }
}

export function zoomTo(id, x, y, scale) {
    const inst = instances.get(String(id));
    if (!inst) return false;
    try {
        if (typeof inst.zoomTo === 'function') inst.zoomTo(x, y, scale);
        else if (typeof inst.smoothZoomAbs === 'function') inst.smoothZoomAbs(x, y, scale);
        return true;
    } catch (e) { return false; }
}

export function dispose(id) {
    if (id == null) return false;
    const ok = instances.has(String(id));
    disposeInstance(id);
    return ok;
}

export function disposeAll() {
    for (const id of [...instances.keys()]) disposeInstance(id);
}
