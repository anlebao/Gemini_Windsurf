/**
 * OCR Hub S2: Client-side OCR abstraction layer.
 * guard-camera.js calls vananOcrHub.recognize() instead of Tesseract directly.
 * Fetches OCR engine config from /api/ocr/config (admin-configurable).
 * Delegates to the correct adapter (Tesseract or PaddleOCR).
 *
 * Default: Tesseract (backward compat — if config fetch fails or PaddleOCR not available).
 *
 * Adapters:
 * - TesseractAdapter: wraps existing Tesseract worker from guard-camera.js
 * - PaddleAdapter: ONNX/WASM — Sprint 3 (stub for now, falls back to Tesseract)
 */
window.vananOcrHub = (function () {
    let _engine = null;
    let _adapter = null;
    let _configPromise = null;

    /** Fetch OCR engine config from Gateway. Falls back to Tesseract on error. */
    async function _loadConfig() {
        if (_engine) return _engine;
        if (_configPromise) return _configPromise;

        _configPromise = (async () => {
            try {
                const resp = await fetch('/api/ocr/config', { credentials: 'include' });
                if (!resp.ok) throw new Error(`HTTP ${resp.status}`);
                const cfg = await resp.json();
                _engine = cfg.plateEngine || 'Tesseract';
            } catch (e) {
                console.warn('[OCR Hub] Config fetch failed, using Tesseract default:', e.message);
                _engine = 'Tesseract';
            }
            return _engine;
        })();

        return _configPromise;
    }

    /** Get the OCR adapter (lazy init — loads config + adapter on first call). */
    async function getAdapter() {
        if (_adapter) return _adapter;

        const engine = await _loadConfig();
        console.log('[OCR Hub] Engine selected:', engine);

        if (engine === 'PaddleOCR') {
            try {
                _adapter = await _loadPaddleAdapter();
            } catch (e) {
                console.warn('[OCR Hub] PaddleOCR load failed, falling back to Tesseract:', e.message);
                _engine = 'Tesseract';
                _adapter = await _loadTesseractAdapter();
            }
        } else {
            _adapter = await _loadTesseractAdapter();
        }

        return _adapter;
    }

    /** Recognize text from canvas. Returns { text, confidence } or null. */
    async function recognize(canvas) {
        try {
            const adapter = await getAdapter();
            return await adapter.recognize(canvas);
        } catch (e) {
            console.error('[OCR Hub] recognize failed:', e);
            return null;
        }
    }

    /** Preload adapter (call on page load for faster first scan). */
    async function preload() {
        try {
            await getAdapter();
            console.log('[OCR Hub] Preloaded engine:', _engine);
        } catch (e) {
            console.warn('[OCR Hub] Preload failed:', e.message);
        }
    }

    /** Get current engine name (for telemetry/debugging). */
    async function getEngine() {
        if (!_engine) await _loadConfig();
        return _engine;
    }

    // === Tesseract Adapter ===
    // Wraps existing Tesseract worker from guard-camera.js (preloadOcrWorker).
    async function _loadTesseractAdapter() {
        // Delegate to guard-camera.js's preloaded Tesseract worker
        if (!window.vananGuardCamera || !window.vananGuardCamera.preloadOcrWorker) {
            throw new Error('guard-camera.js not loaded — cannot init Tesseract');
        }
        const worker = await window.vananGuardCamera.preloadOcrWorker();
        return {
            async recognize(canvas) {
                const { data } = await worker.recognize(canvas);
                return { text: data.text || '', confidence: data.confidence || 0 };
            }
        };
    }

    // === PaddleOCR Adapter (Sprint 3 — stub) ===
    async function _loadPaddleAdapter() {
        // Sprint 3 will implement this with ONNX/WASM
        // For now, throw to trigger Tesseract fallback
        throw new Error('PaddleOCR adapter not yet implemented (Sprint 3)');
    }

    // Public API
    return {
        recognize: recognize,
        preload: preload,
        getEngine: getEngine,
        getAdapter: getAdapter,
        // Exposed for Sprint 3 — PaddleAdapter will be registered here
        _loadPaddleAdapter: _loadPaddleAdapter
    };
})();
