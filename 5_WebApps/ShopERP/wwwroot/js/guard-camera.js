// #126: Guard Scanner camera interop for photo capture (plate + customer).
// QR scanning reuses existing vananQrScanner (html5-qrcode) in qr-scanner.js.
// #126-fix2: JS-first capture — camera/capture/preview/OCR entirely in JS,
// bypassing Blazor SignalR. Prevents circuit disconnect → page reload from
// killing camera stream. Blazor only reads captured photo when user clicks "Tạo QR".

let _cameraStream = null;
let _cameraVideo = null;
let _keepAliveTimer = null;
const _keepAliveKey = 'vanan_guard_keepalive';
// JS-side photo storage — survives circuit disconnect (unlike Blazor state).
const _capturedPhotos = { plate: null, customer: null };
const _cameraActive = { plate: false, customer: false };
// #130: Photo compression config — fetched from Gateway /api/guard/photo-config.
// Default values used until config is fetched (loadPhotoConfig).
const _photoConfig = { maxDimension: 1024, jpegQuality: 0.7, maxSizeKB: 100 };

window.vananGuardCamera = {
    // === #126-fix: Circuit keep-alive — ping Blazor every 15s to prevent idle disconnect ===
    // #130-fix: invokeMethodAsync can throw SYNCHRONOUSLY when SignalR connection is in
    // Reconnecting/Disconnected state. The .catch() only catches promise rejections, not
    // sync throws — causing "Uncaught (in promise) Error: Cannot send data if the connection
    // is not in the 'Connected' State". Wrap in try/catch to stop the timer cleanly.
    startKeepAlive(dotNetRef) {
        this.stopKeepAlive();
        _keepAliveTimer = setInterval(() => {
            if (dotNetRef && dotNetRef.invokeMethodAsync) {
                try {
                    dotNetRef.invokeMethodAsync('KeepAlivePingAsync').catch(() => {
                        this.stopKeepAlive();
                    });
                } catch (e) {
                    // Sync throw — connection not in Connected state. Stop pinging.
                    this.stopKeepAlive();
                }
            }
        }, 15000);
        try { sessionStorage.setItem(_keepAliveKey, '1'); } catch (e) {}
    },

    stopKeepAlive() {
        if (_keepAliveTimer) {
            clearInterval(_keepAliveTimer);
            _keepAliveTimer = null;
        }
        try { sessionStorage.removeItem(_keepAliveKey); } catch (e) {}
    },

    // === #126-fix: sessionStorage persistence ===
    saveState(key, value) {
        try {
            sessionStorage.setItem('vanan_guard_' + key, value || '');
        } catch (e) {
            console.warn('Guard sessionStorage save failed for', key, e);
        }
    },

    loadState(key) {
        try {
            return sessionStorage.getItem('vanan_guard_' + key) || null;
        } catch (e) {
            return null;
        }
    },

    clearState() {
        try {
            Object.keys(sessionStorage)
                .filter(k => k.startsWith('vanan_guard_'))
                .forEach(k => sessionStorage.removeItem(k));
            _capturedPhotos.plate = null;
            _capturedPhotos.customer = null;
        } catch (e) {}
    },

    /** Start camera preview for photo capture. videoElementId = <video> element id. facingMode = 'environment' (back) or 'user' (front). */
    async startCamera(videoElementId, facingMode) {
        try {
            this.stopCamera();
            const video = document.getElementById(videoElementId);
            if (!video) {
                console.error('Video element not found:', videoElementId);
                return false;
            }
            _cameraVideo = video;
            const constraints = {
                video: {
                    facingMode: facingMode || 'environment',
                    width: { ideal: 1280 },
                    height: { ideal: 720 }
                },
                audio: false
            };
            _cameraStream = await navigator.mediaDevices.getUserMedia(constraints);
            video.srcObject = _cameraStream;
            video.setAttribute('playsinline', 'true');
            await video.play();
            return true;
        } catch (err) {
            console.error('Camera start failed:', err);
            return false;
        }
    },

    /** Capture current camera frame as base64 JPEG. Returns { dataUrl, blob } or null on failure. */
    async capturePhoto(videoElementId) {
        try {
            const video = document.getElementById(videoElementId);
            if (!video || !video.videoWidth) return null;
            const canvas = document.createElement('canvas');
            canvas.width = video.videoWidth;
            canvas.height = video.videoHeight;
            const ctx = canvas.getContext('2d');
            ctx.drawImage(video, 0, 0, canvas.width, canvas.height);
            const dataUrl = canvas.toDataURL('image/jpeg', 0.85);
            return dataUrl;
        } catch (err) {
            console.error('Photo capture failed:', err);
            return null;
        }
    },

    /** #130: Compress photo to target size. Resizes to maxDimension (keep aspect ratio)
     *  and exports JPEG at quality. Iteratively reduces quality if still > maxSizeKB
     *  until min quality 0.3. Returns compressed data URL or null on failure. */
    async compressPhoto(dataUrl, maxDimension, quality, maxSizeKB) {
        try {
            const img = await new Promise((resolve, reject) => {
                const i = new Image();
                i.onload = () => resolve(i);
                i.onerror = reject;
                i.src = dataUrl;
            });
            // Resize: keep aspect ratio, cap at maxDimension
            let w = img.width, h = img.height;
            if (w > maxDimension || h > maxDimension) {
                const ratio = Math.min(maxDimension / w, maxDimension / h);
                w = Math.round(w * ratio);
                h = Math.round(h * ratio);
            }
            const canvas = document.createElement('canvas');
            canvas.width = w;
            canvas.height = h;
            const ctx = canvas.getContext('2d');
            ctx.drawImage(img, 0, 0, w, h);
            // Iteratively reduce quality until under maxSizeKB (base64 ~1.37x binary size)
            let q = quality;
            let compressed = canvas.toDataURL('image/jpeg', q);
            const targetBytes = maxSizeKB * 1024 * 1.37; // base64 overhead ~37%
            while (compressed.length > targetBytes && q > 0.3) {
                q = Math.max(0.3, q - 0.1);
                compressed = canvas.toDataURL('image/jpeg', q);
            }
            return compressed;
        } catch (err) {
            console.error('Photo compression failed:', err);
            return null;
        }
    },

    /** #130: Fetch photo compression config from Gateway. Called on page load.
     *  Stores in _photoConfig global. Falls back to defaults if fetch fails. */
    async loadPhotoConfig(publicGatewayBaseUrl) {
        try {
            const resp = await fetch((publicGatewayBaseUrl || '') + '/api/guard/photo-config');
            if (resp.ok) {
                const cfg = await resp.json();
                _photoConfig.maxDimension = cfg.maxDimension || 1024;
                _photoConfig.jpegQuality = cfg.jpegQuality || 0.7;
                _photoConfig.maxSizeKB = cfg.maxSizeKB || 100;
            }
        } catch (e) {
            // Use defaults — config fetch is non-critical
            console.warn('[Guard] Photo config fetch failed, using defaults:', e);
        }
    },

    /** Stop camera and release stream. */
    stopCamera() {
        if (_cameraStream) {
            _cameraStream.getTracks().forEach(t => t.stop());
            _cameraStream = null;
        }
        if (_cameraVideo) {
            _cameraVideo.srcObject = null;
            _cameraVideo = null;
        }
    },

    // === #126-fix2: JS-first capture — no Blazor SignalR roundtrip ===
    // These methods are called directly from HTML onclick attributes (not Blazor OnClick).
    // Camera + capture + preview + OCR all happen in JS without involving Blazor.
    // Blazor only reads the captured photo via getCapturedPhoto() when user clicks "Tạo QR".

    /** Open camera for a slot ('plate' or 'customer'). Pure JS — no Blazor call. */
    async openCamera(slot) {
        const videoId = slot === 'plate' ? 'plateVideo' : 'customerVideo';
        const facing = slot === 'plate' ? 'environment' : 'user';
        const ok = await this.startCamera(videoId, facing);
        if (ok) {
            _cameraActive[slot] = true;
            this._updateCameraUI(slot, true);
        } else {
            this._showError('Không mở được camera. Kiểm tra quyền truy cập camera.');
        }
        return ok;
    },

    /** Capture photo for a slot. Pure JS — stores in _capturedPhotos, renders preview, stops camera.
     *  #130: Compress photo before storing — reduces size from ~650KB to <100KB. */
    async captureAndStore(slot) {
        const videoId = slot === 'plate' ? 'plateVideo' : 'customerVideo';
        const rawUrl = await this.capturePhoto(videoId);
        if (!rawUrl) {
            this._showError('Chụp ảnh thất bại. Đảm bảo camera đã mở và có hình.');
            return false;
        }
        // #130: Compress photo to configured max size
        const dataUrl = await this.compressPhoto(rawUrl, _photoConfig.maxDimension, _photoConfig.jpegQuality, _photoConfig.maxSizeKB);
        if (!dataUrl) {
            this._showError('Nén ảnh thất bại. Vui lòng chụp lại.');
            return false;
        }
        _capturedPhotos[slot] = dataUrl;
        this.stopCamera();
        _cameraActive[slot] = false;
        // Render preview image directly in DOM — no Blazor re-render needed.
        this._renderPreview(slot, dataUrl);
        this._updateCameraUI(slot, false);
        // Show "Nhận diện" button for plate slot.
        if (slot === 'plate') {
            const ocrBtn = document.getElementById('ocrButton');
            if (ocrBtn) ocrBtn.style.display = '';
        }
        // Persist to sessionStorage (survives reload).
        this.saveState(slot + 'Photo', dataUrl);
        return true;
    },

    /** Cancel camera for a slot (user clicked Hủy). Pure JS. */
    cancelCamera(slot) {
        this.stopCamera();
        _cameraActive[slot] = false;
        this._updateCameraUI(slot, false);
    },

    /** Retake photo for a slot (user clicked Chụp lại). Clears stored photo, removes preview,
     *  shows video + open button, hides capture/cancel/retake/ocr. Pure JS.
     *  #130-fix: video is now hidden (not removed) by _renderPreview, so it's still in DOM. */
    retakePhoto(slot) {
        _capturedPhotos[slot] = null;
        try { sessionStorage.removeItem('vanan_guard_' + slot + 'Photo'); } catch (e) {}
        // Remove preview img, show video again.
        const box = document.querySelector(`[data-photo-slot="${slot}"]`);
        if (box) {
            const img = box.querySelector('img.guard-photo-preview');
            if (img) img.remove();
            const video = box.querySelector('video');
            if (video) video.style.display = '';
        }
        // Hide OCR button + hints if plate slot.
        if (slot === 'plate') {
            const ocrBtn = document.getElementById('ocrButton');
            if (ocrBtn) ocrBtn.style.display = 'none';
            const ocrHint = document.getElementById('ocrHint');
            if (ocrHint) { ocrHint.textContent = ''; ocrHint.style.display = 'none'; }
            const ocrStatus = document.getElementById('ocrStatus');
            if (ocrStatus) ocrStatus.style.display = 'none';
        }
        this._updateCameraUI(slot, false);
        // Auto-reopen camera for convenience.
        this.openCamera(slot);
    },

    /** Get captured photo data URL for a slot. Called by Blazor when user clicks "Tạo QR".
     *  #130-WARNING: Returns base64 string (~200-650KB) — DO NOT pass this through SignalR JS interop.
     *  SignalR default MaximumReceiveMessageSize=32KB → circuit disconnect if photo > 32KB.
     *  Use hasCapturedPhoto(slot) for existence check + uploadCapturedPhoto(slot, url) for upload
     *  instead — those don't transfer base64 over SignalR. */
    getCapturedPhoto(slot) {
        // Try JS memory first, then sessionStorage (in case of reload).
        if (_capturedPhotos[slot]) return _capturedPhotos[slot];
        const saved = this.loadState(slot + 'Photo');
        if (saved) {
            _capturedPhotos[slot] = saved;
            return saved;
        }
        return null;
    },

    /** #130-fix: Check if a photo exists for a slot WITHOUT transferring base64 over SignalR.
     *  Returns boolean only — safe for JS interop (no large payload). */
    hasCapturedPhoto(slot) {
        return !!_capturedPhotos[slot] || !!this.loadState(slot + 'Photo');
    },

    /** #130-fix: Upload captured photo for a slot to Gateway API (server-side R2 upload).
     *  JS sends base64 photo to Gateway via HTTP fetch — no SignalR, no R2 CORS needed.
     *  Gateway already has CORS configured for app2.khachvip.online.
     *  Blazor passes (slot, jwtToken, gatewayBaseUrl) — all small strings, safe for SignalR.
     *  Returns { success, key } or { success: false, error }. */
    async uploadCapturedPhoto(slot, jwtToken, gatewayBaseUrl) {
        const dataUrl = this.getCapturedPhoto(slot);
        if (!dataUrl) {
            return { success: false, error: 'No captured photo for slot: ' + slot };
        }
        try {
            const response = await fetch((gatewayBaseUrl || '') + '/api/guard/upload-photo', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Authorization': 'Bearer ' + jwtToken
                },
                body: JSON.stringify({
                    slot: slot,
                    base64Data: dataUrl,
                    contentType: 'image/jpeg'
                })
            });
            if (response.ok) {
                const result = await response.json();
                return { success: true, key: result.key || '' };
            } else {
                const errText = await response.text().catch(() => '');
                return { success: false, error: 'HTTP ' + response.status + ': ' + errText };
            }
        } catch (err) {
            return { success: false, error: err.message || 'Network error' };
        }
    },

    /** Get current value of a DOM input by id. Used by Blazor to read plate number + phone
     *  that may have been set by JS (OCR) without triggering Blazor @bind change event. */
    getInputValue(id) {
        const el = document.getElementById(id);
        return el ? el.value : '';
    },

    /** Set input value AND dispatch change event so Blazor @bind syncs.
     *  Setting .value via JS alone does NOT trigger Blazor's change listener. */
    setInputValue(id, value) {
        const el = document.getElementById(id);
        if (!el) return;
        el.value = value || '';
        // Dispatch native change event so Blazor @bind picks up the new value.
        el.dispatchEvent(new Event('change', { bubbles: true }));
        el.dispatchEvent(new Event('input', { bubbles: true }));
    },

    /** Render preview image directly in DOM (hides <video>, shows <img>). Pure JS.
     *  #130-fix: HIDE video instead of removing it — retakePhoto needs the video element
     *  to reopen camera. Removing it caused getElementById('plateVideo') to return null. */
    _renderPreview(slot, dataUrl) {
        const box = document.querySelector(`[data-photo-slot="${slot}"]`);
        if (!box) return;
        // Remove any existing preview img (from previous capture).
        const existingImg = box.querySelector('img.guard-photo-preview');
        if (existingImg) existingImg.remove();
        // Hide the video element (keep in DOM so retakePhoto can reopen camera).
        const video = box.querySelector('video');
        if (video) video.style.display = 'none';
        const img = document.createElement('img');
        img.src = dataUrl;
        img.alt = slot === 'plate' ? 'Biển số' : 'Khách';
        img.className = 'guard-photo-preview';
        // Insert before the actions div.
        const actions = box.querySelector('.guard-photo-actions');
        if (actions) {
            box.insertBefore(img, actions);
        } else {
            box.appendChild(img);
        }
    },

    /** Update camera button visibility. States:
     *  - isActive=true: camera streaming → show capture+cancel, hide open+retake.
     *  - isActive=false + hasPhoto: show retake, hide open/capture/cancel.
     *  - isActive=false + noPhoto: show open, hide capture/cancel/retake. */
    _updateCameraUI(slot, isActive) {
        const openBtn = document.getElementById(slot + 'OpenBtn');
        const captureBtn = document.getElementById(slot + 'CaptureBtn');
        const cancelBtn = document.getElementById(slot + 'CancelBtn');
        const retakeBtn = document.getElementById(slot + 'RetakeBtn');
        const hasPhoto = !!_capturedPhotos[slot];
        if (openBtn) openBtn.style.display = (isActive || hasPhoto) ? 'none' : '';
        if (captureBtn) captureBtn.style.display = isActive ? '' : 'none';
        if (cancelBtn) cancelBtn.style.display = isActive ? '' : 'none';
        if (retakeBtn) retakeBtn.style.display = (!isActive && hasPhoto) ? '' : 'none';
    },

    /** Show error message in the guard error alert area. Pure JS. */
    _showError(msg) {
        const el = document.getElementById('guardErrorAlert');
        if (el) {
            el.textContent = msg;
            el.style.display = '';
        }
        console.error('[Guard]', msg);
    },

    /** Clear error message. Pure JS. */
    _clearError() {
        const el = document.getElementById('guardErrorAlert');
        if (el) {
            el.textContent = '';
            el.style.display = 'none';
        }
    },

    /** Reset DOM preview for both slots — remove <img>, show <video>, reset buttons. Pure JS.
     *  #130-fix: video is hidden (not removed) by _renderPreview, so just show it again. */
    _clearDOMPreview() {
        ['plate', 'customer'].forEach(slot => {
            _capturedPhotos[slot] = null;
            const box = document.querySelector(`[data-photo-slot="${slot}"]`);
            if (box) {
                const img = box.querySelector('img.guard-photo-preview');
                if (img) img.remove();
                // Show video element again (hidden by _renderPreview, still in DOM).
                const video = box.querySelector('video');
                if (video) video.style.display = '';
            }
            this._updateCameraUI(slot, false);
        });
        // Hide OCR button + hints.
        const ocrBtn = document.getElementById('ocrButton');
        if (ocrBtn) ocrBtn.style.display = 'none';
        const ocrStatus = document.getElementById('ocrStatus');
        if (ocrStatus) ocrStatus.style.display = 'none';
        const ocrHint = document.getElementById('ocrHint');
        if (ocrHint) {
            ocrHint.textContent = '';
            ocrHint.style.display = 'none';
        }
        // Clear plate input.
        const plateInput = document.getElementById('plateInput');
        if (plateInput) plateInput.value = '';
        const phoneInput = document.getElementById('customerPhoneInput');
        if (phoneInput) phoneInput.value = '';
    },

    /** OCR button handler — runs Tesseract.js, sets plate input value. Pure JS. */
    async recognizeAndFill() {
        const dataUrl = this.getCapturedPhoto('plate');
        if (!dataUrl) {
            this._showError('Chưa chụp ảnh biển số.');
            return;
        }
        const ocrStatus = document.getElementById('ocrStatus');
        const ocrHint = document.getElementById('ocrHint');
        const ocrBtn = document.getElementById('ocrButton');
        if (ocrStatus) ocrStatus.style.display = '';
        if (ocrHint) ocrHint.style.display = 'none';
        if (ocrBtn) ocrBtn.disabled = true;
        try {
            const plate = await this.recognizePlate(dataUrl);
            if (plate) {
                this.setInputValue('plateInput', plate);
                if (ocrHint) {
                    ocrHint.textContent = 'Đã nhận diện: ' + plate + ' — kiểm tra lại trước khi tạo QR.';
                    ocrHint.style.display = '';
                }
                this.saveState('plateNumber', plate);
            } else {
                if (ocrHint) {
                    ocrHint.textContent = 'Không nhận diện được — nhập thủ công.';
                    ocrHint.style.display = '';
                }
            }
        } catch (err) {
            if (ocrHint) {
                ocrHint.textContent = 'OCR lỗi — nhập biển số thủ công.';
                ocrHint.style.display = '';
            }
        } finally {
            if (ocrStatus) ocrStatus.style.display = 'none';
            if (ocrBtn) ocrBtn.disabled = false;
        }
    },

    /** Restore UI state from sessionStorage after page reload. Pure JS — called on DOMContentLoaded. */
    restoreUIFromSession() {
        const platePhoto = this.loadState('platePhoto');
        const customerPhoto = this.loadState('customerPhoto');
        if (platePhoto) {
            _capturedPhotos.plate = platePhoto;
            this._renderPreview('plate', platePhoto);
            this._updateCameraUI('plate', false);
            const ocrBtn = document.getElementById('ocrButton');
            if (ocrBtn) ocrBtn.style.display = '';
        }
        if (customerPhoto) {
            _capturedPhotos.customer = customerPhoto;
            this._renderPreview('customer', customerPhoto);
            this._updateCameraUI('customer', false);
        }
        const plateNumber = this.loadState('plateNumber');
        if (plateNumber) {
            const input = document.getElementById('plateInput');
            if (input) input.value = plateNumber;
        }
        const customerPhone = this.loadState('customerPhone');
        if (customerPhone) {
            const input = document.getElementById('customerPhoneInput');
            if (input) input.value = customerPhone;
        }
        const ocrHint = this.loadState('ocrHint');
        if (ocrHint) {
            const el = document.getElementById('ocrHint');
            if (el) {
                el.textContent = ocrHint;
                el.style.display = '';
            }
        }
    },

    /** Upload a base64 JPEG to a presigned PUT URL (R2). Returns true on success.
     *  #130-fix: Add 30s timeout via AbortController — without it, fetch hangs forever
     *  if R2 is unreachable or presigned URL is invalid, causing "loading mãi" on UI. */
    async uploadToPresignedUrl(dataUrl, presignedUrl) {
        try {
            // Convert base64 data URL to Blob
            const response = await fetch(dataUrl);
            const blob = await response.blob();
            // 30s timeout — AbortController cancels fetch if R2 is unreachable.
            const controller = new AbortController();
            const timeoutId = setTimeout(() => controller.abort(), 30000);
            const uploadResp = await fetch(presignedUrl, {
                method: 'PUT',
                headers: { 'Content-Type': 'image/jpeg' },
                body: blob,
                signal: controller.signal
            });
            clearTimeout(timeoutId);
            return uploadResp.ok;
        } catch (err) {
            console.error('Upload to presigned URL failed:', err);
            return false;
        }
    },

    /** Generate QR code image (base64 PNG) from text using qrcode.js (loaded from CDN). */
    async generateQrImage(text, size) {
        try {
            await this._ensureQrLibrary();
            const canvas = document.createElement('canvas');
            // QRCode.toCanvas is the qrcode library API
            await QRCode.toCanvas(canvas, text, { width: size || 300, margin: 2 });
            return canvas.toDataURL('image/png');
        } catch (err) {
            console.error('QR generation failed:', err);
            return null;
        }
    },

    async _ensureQrLibrary() {
        if (window.QRCode) return;
        return new Promise((resolve, reject) => {
            const script = document.createElement('script');
            script.src = 'https://cdn.jsdelivr.net/npm/qrcode@1.5.3/build/qrcode.min.js';
            script.onload = () => resolve();
            script.onerror = () => reject(new Error('Failed to load qrcode library'));
            document.head.appendChild(script);
        });
    },

    /** Generate QR code directly onto an existing canvas element (for print tickets). */
    async generateQrToCanvas(canvasId, text, size) {
        try {
            await this._ensureQrLibrary();
            const canvas = document.getElementById(canvasId);
            if (!canvas) {
                console.error('Canvas element not found:', canvasId);
                return false;
            }
            await QRCode.toCanvas(canvas, text, { width: size || 200, margin: 2 });
            return true;
        } catch (err) {
            console.error('QR generation to canvas failed:', err);
            return false;
        }
    },

    // === #126 OCR: License plate recognition (Tesseract.js, client-side) ===

    /** Recognize license plate text from a base64 JPEG data URL. Returns cleaned plate string or ''.
     *  #130-fix: Use ONE worker for all PSM modes (previous code created 4 workers sequentially,
     *  each downloading ~2MB WASM — very slow and prone to failure). Explicit worker/core/lang
     *  paths ensure reliable loading from CDN when script is injected dynamically.
     *  Tries PSM 7 (single line) first — best for plates — then 6, 8, 13 as fallback. */
    async recognizePlate(dataUrl) {
        let worker = null;
        try {
            if (!dataUrl) return '';
            await this._ensureOcrLibrary();
            const canvas = await this._preprocessForOcr(dataUrl);
            const whitelist = '0123456789ABCDEFGHKLMNPRSTUVXYZ-';
            // Create ONE worker with explicit paths for reliable CDN loading.
            worker = await Tesseract.createWorker('eng', 1, {
                workerPath: 'https://cdn.jsdelivr.net/npm/tesseract.js@5/dist/worker.min.js',
                corePath: 'https://cdn.jsdelivr.net/npm/tesseract.js-core@5',
                langPath: 'https://cdn.jsdelivr.net/npm/tesseract.js@5/lang-data',
                logger: () => {}
            });
            await worker.setParameters({ tessedit_char_whitelist: whitelist });
            // Try PSM modes in order of likelihood for plates (reuse same worker).
            const psmModes = ['7', '6', '8', '13'];
            let bestPlate = '';
            let bestScore = -1;
            for (const psm of psmModes) {
                try {
                    await worker.setParameters({ tessedit_pageseg_mode: psm });
                    const { data } = await worker.recognize(canvas);
                    const raw = (data.text || '').toUpperCase();
                    const plate = this._normalizePlate(raw);
                    const score = this._scorePlate(plate);
                    if (score > bestScore) {
                        bestScore = score;
                        bestPlate = plate;
                    }
                } catch (e) {
                    // Continue to next PSM mode.
                }
            }
            return bestPlate;
        } catch (err) {
            console.error('Plate OCR failed:', err);
            return '';
        } finally {
            if (worker) { try { await worker.terminate(); } catch (e) {} }
        }
    },

    /** Score a plate string: higher = more plate-like. Must have both letters and digits,
     *  length 5-12, and not be all one type. */
    _scorePlate(s) {
        if (!s || s.length < 4) return -1;
        const digits = (s.match(/[0-9]/g) || []).length;
        const letters = (s.match(/[A-Z]/g) || []).length;
        if (digits === 0 || letters === 0) return -1; // Need both.
        // Prefer length 6-9 (typical VN plate after normalization).
        let score = digits + letters;
        if (s.length >= 6 && s.length <= 9) score += 5;
        // Penalize too long (likely garbage).
        if (s.length > 12) score -= 10;
        return score;
    },

    /** Load image from data URL, upscale 2x + grayscale to improve OCR accuracy. Returns canvas. */
    async _preprocessForOcr(dataUrl) {
        const img = await new Promise((resolve, reject) => {
            const i = new Image();
            i.onload = () => resolve(i);
            i.onerror = reject;
            i.src = dataUrl;
        });
        const scale = 2;
        const canvas = document.createElement('canvas');
        canvas.width = img.width * scale;
        canvas.height = img.height * scale;
        const ctx = canvas.getContext('2d');
        ctx.drawImage(img, 0, 0, canvas.width, canvas.height);
        // Grayscale conversion — improves Tesseract binarization on plate photos.
        const imgData = ctx.getImageData(0, 0, canvas.width, canvas.height);
        const d = imgData.data;
        for (let i = 0; i < d.length; i += 4) {
            const gray = (d[i] * 0.299 + d[i + 1] * 0.587 + d[i + 2] * 0.114);
            d[i] = d[i + 1] = d[i + 2] = gray;
        }
        ctx.putImageData(imgData, 0, 0);
        return canvas;
    },

    /** Clean raw OCR output to a plate-like string: keep [0-9A-Z-], collapse spaces, trim. */
    _normalizePlate(raw) {
        if (!raw) return '';
        // Keep only digits, uppercase letters, and dashes.
        let s = raw.replace(/[^0-9A-Z\-]/g, '');
        // Collapse repeated dashes, strip leading/trailing dashes.
        s = s.replace(/-+/g, '-').replace(/^-+|-+$/g, '');
        return s;
    },

    async _ensureOcrLibrary() {
        if (window.Tesseract) return;
        return new Promise((resolve, reject) => {
            const script = document.createElement('script');
            script.src = 'https://cdn.jsdelivr.net/npm/tesseract.js@5/dist/tesseract.min.js';
            script.onload = () => resolve();
            script.onerror = () => reject(new Error('Failed to load Tesseract.js library'));
            document.head.appendChild(script);
        });
    }
};

// #126-fix2: Restore UI on page load (survives reload — pure JS, no Blazor dependency).
document.addEventListener('DOMContentLoaded', function() {
    if (window.vananGuardCamera) {
        window.vananGuardCamera.restoreUIFromSession();
    }
});
