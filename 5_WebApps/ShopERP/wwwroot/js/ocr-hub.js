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

    // === PaddleOCR Adapter (Sprint 3 — ONNX Runtime Web) ===
    // Uses rec.onnx only (guard-camera.js already crops plate ROI — skip det model).
    // Flow: canvas → resize 48px H → normalize → ONNX inference → CTC greedy decode.
    async function _loadPaddleAdapter() {
        const MODEL_DIR = '/js/lib/ocr/paddle';
        const REC_HEIGHT = 48;  // PP-OCRv4 rec mobile expects 48px height

        // Load ONNX Runtime Web from CDN
        if (!window.ort) {
            await _loadScript('https://cdn.jsdelivr.net/npm/onnxruntime-web@1.18.0/dist/ort.min.js');
        }
        ort.env.wasm.wasmPaths = 'https://cdn.jsdelivr.net/npm/onnxruntime-web@1.18.0/dist/';
        ort.env.wasm.numThreads = 1;  // Single thread — avoid SharedArrayBuffer requirement

        // Load dict.txt (character dictionary for CTC decoding)
        const dictResp = await fetch(`${MODEL_DIR}/dict.txt`);
        if (!dictResp.ok) throw new Error(`dict.txt fetch failed: ${dictResp.status}`);
        const dictText = await dictResp.text();
        // PaddleOCR dict: index 0 = blank (CTC), indices 1..N = chars, last = special end token
        const chars = dictText.split('\n').map(l => l.trim()).filter(l => l.length > 0);
        // Build decode table: index 0 = blank, 1..len = chars
        const decodeTable = ['blank', ...chars];

        // Load rec.onnx model
        console.log('[OCR Hub] Loading PaddleOCR rec.onnx...');
        const tInit = performance.now();
        const session = await ort.InferenceSession.create(`${MODEL_DIR}/rec.onnx`, {
            executionProviders: ['wasm'],
            graphOptimizationLevel: 'all'
        });
        console.log('[BENCH] paddle_init_ms=' + Math.round(performance.now() - tInit));
        const inputName = session.inputNames[0];   // 'x'
        const outputName = session.outputNames[0]; // 'sigmoid_0.tmp_0' or similar
        console.log('[OCR Hub] PaddleOCR rec model loaded. Input:', inputName, 'Output:', outputName);

        /** Preprocess canvas → NCHW float32 tensor [1, 3, 48, W]. */
        function _preprocess(canvas) {
            // Resize to 48px height, maintain aspect ratio
            const scale = REC_HEIGHT / canvas.height;
            const targetW = Math.max(1, Math.round(canvas.width * scale));
            // Pad width to multiple of 4 (ONNX dynamic dim constraint)
            const paddedW = Math.ceil(targetW / 4) * 4;

            const tmp = document.createElement('canvas');
            tmp.width = paddedW;
            tmp.height = REC_HEIGHT;
            const ctx = tmp.getContext('2d', { willReadFrequently: true });
            // Draw scaled image at left, pad rest with white (255)
            ctx.fillStyle = '#ffffff';
            ctx.fillRect(0, 0, paddedW, REC_HEIGHT);
            ctx.drawImage(canvas, 0, 0, targetW, REC_HEIGHT);

            const imageData = ctx.getImageData(0, 0, paddedW, REC_HEIGHT);
            const pixels = imageData.data;  // RGBA

            // Convert to NCHW float32, normalize: (pixel / 255 - 0.5) / 0.5
            const tensorData = new Float32Array(3 * REC_HEIGHT * paddedW);
            const mean = 0.5, std = 0.5;
            for (let c = 0; c < 3; c++) {
                for (let h = 0; h < REC_HEIGHT; h++) {
                    for (let w = 0; w < paddedW; w++) {
                        const pixelIdx = (h * paddedW + w) * 4 + c;
                        const normalized = (pixels[pixelIdx] / 255.0 - mean) / std;
                        tensorData[c * REC_HEIGHT * paddedW + h * paddedW + w] = normalized;
                    }
                }
            }

            return new ort.Tensor('float32', tensorData, [1, 3, REC_HEIGHT, paddedW]);
        }

        /** CTC greedy decode: argmax → remove consecutive duplicates → remove blank → map to chars. */
        function _ctcDecode(outputData, batchSize, timesteps, numClasses) {
            const results = [];
            for (let b = 0; b < batchSize; b++) {
                let decoded = '';
                let prevIdx = -1;
                let confidenceSum = 0;
                let confidenceCount = 0;

                for (let t = 0; t < timesteps; t++) {
                    // Find argmax for this timestep
                    let maxIdx = 0;
                    let maxProb = outputData[b * timesteps * numClasses + t * numClasses];
                    for (let c = 1; c < numClasses; c++) {
                        const prob = outputData[b * timesteps * numClasses + t * numClasses + c];
                        if (prob > maxProb) {
                            maxProb = prob;
                            maxIdx = c;
                        }
                    }

                    // CTC: skip blank (index 0) and consecutive duplicates
                    if (maxIdx !== 0 && maxIdx !== prevIdx) {
                        if (maxIdx < decodeTable.length) {
                            decoded += decodeTable[maxIdx];
                        }
                        confidenceSum += maxProb;
                        confidenceCount++;
                    }
                    prevIdx = maxIdx;
                }

                const confidence = confidenceCount > 0 ? (confidenceSum / confidenceCount) * 100 : 0;
                results.push({ text: decoded, confidence: confidence });
            }
            return results[0];  // batchSize = 1
        }

        return {
            async recognize(canvas) {
                const tensor = _preprocess(canvas);
                const feeds = {};
                feeds[inputName] = tensor;
                const tInf = performance.now();
                const results = await session.run(feeds);
                console.log('[BENCH] paddle_infer_ms=' + Math.round(performance.now() - tInf));
                const output = results[outputName];
                console.log('[BENCH] paddle_output_dims=' + JSON.stringify(output.dims));
                // Output shape: [batchSize, timesteps, numClasses]
                const [batchSize, timesteps, numClasses] = output.dims;
                const decoded = _ctcDecode(output.data, batchSize, timesteps, numClasses);
                return decoded;
            }
        };
    }

    /** Helper: dynamically load a script tag (returns Promise). */
    function _loadScript(src) {
        return new Promise((resolve, reject) => {
            const existing = document.querySelector(`script[src="${src}"]`);
            if (existing) { resolve(); return; }
            const script = document.createElement('script');
            script.src = src;
            script.async = true;
            script.onload = () => resolve();
            script.onerror = () => reject(new Error(`Failed to load script: ${src}`));
            document.head.appendChild(script);
        });
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
