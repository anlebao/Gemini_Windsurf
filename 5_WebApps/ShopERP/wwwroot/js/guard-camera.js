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

// === R-SCANNER (2026-08-18): Live License Plate Scanner state ===
// Replaces capture-then-OCR flow with: Camera live → ROI crop → Preprocess → OCR → Vote → Auto-confirm
let _scanLoopActive = false;
let _scanRafId = null;       // requestAnimationFrame ID for camera sampling (30 FPS)
let _ocrBusy = false;        // Is OCR worker currently processing a frame?
let _ocrLastTime = 0;        // Timestamp of last OCR submission
let _camResLogged = false;   // R-TELEMETRY: Log actual camera resolution once
let _scanStartTime = 0;
let _voteBuffer = []; // [{plate, confidence, timestamp}]
// Guide box orientation: 'portrait' (square, default for motorbikes) or 'landscape' (rect, for cars)
let _guideOrientation = 'portrait';
const _voteConfig = {
    maxBufferSize: 10,
    minVotes: 3,           // Need at least 3 matching results to accept
    minAvgConfidence: 60,  // Lowered from 70 — Tesseract confidence varies with lighting/angle
    timeoutMs: 5000,       // Show fallback hint after 5s (was 15s — too slow for UX)
    ocrIntervalMs: 600     // Issue #147: 600ms interval (~1.7 OCR/sec) — reduce mobile CPU load
};

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

    /** Start camera preview for photo capture. videoElementId = <video> element id.
     *  R-BACK-CAM: Always use 'environment' (back camera) — guard scans plates from behind.
     *  facingMode param ignored — forced to back camera for consistency on smartphones. */
    async startCamera(videoElementId, facingMode) {
        try {
            this.stopCamera();
            const video = document.getElementById(videoElementId);
            if (!video) {
                console.error('Video element not found:', videoElementId);
                return false;
            }
            _cameraVideo = video;
            // R-BACK-CAM: Force back camera — ignore facingMode param
            const constraints = {
                video: {
                    facingMode: { exact: 'environment' },
                    // R-OCR-3 (2026-08-18): 1920x1080 — higher resolution for plate OCR.
                    width: { ideal: 1920 },
                    height: { ideal: 1080 }
                },
                audio: false
            };
            try {
                _cameraStream = await navigator.mediaDevices.getUserMedia(constraints);
            } catch (e) {
                // Fallback: 'ideal' instead of 'exact' — some devices don't support exact facingMode
                console.warn('[Camera] exact environment failed, trying ideal:', e.message);
                _cameraStream = await navigator.mediaDevices.getUserMedia({
                    video: { facingMode: { ideal: 'environment' }, width: { ideal: 1920 }, height: { ideal: 1080 } },
                    audio: false
                });
            }
            video.srcObject = _cameraStream;
            video.setAttribute('playsinline', 'true');
            await video.play();
            return true;
        } catch (err) {
            console.error('Camera start failed:', err);
            return false;
        }
    },

    /** Capture current camera frame as base64 JPEG. Returns { dataUrl, blob } or null on failure.
     *  R-OCR-4 (2026-08-18): Capture at quality 0.95 (was 0.85) — keep detail for OCR.
     *  Compression happens only at upload time (uploadCapturedPhoto), not at capture. */
    async capturePhoto(videoElementId) {
        try {
            const video = document.getElementById(videoElementId);
            if (!video || !video.videoWidth) return null;
            const canvas = document.createElement('canvas');
            canvas.width = video.videoWidth;
            canvas.height = video.videoHeight;
            const ctx = canvas.getContext('2d');
            ctx.drawImage(video, 0, 0, canvas.width, canvas.height);
            const dataUrl = canvas.toDataURL('image/jpeg', 0.95);
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

    /** Open camera for a slot ('plate' or 'customer'). Pure JS — no Blazor call.
     *  R-SCANNER: For 'plate' slot, starts live scanning (guide box + continuous OCR).
     *  For 'customer' slot, simple camera preview for photo capture. */
    async openCamera(slot) {
        if (slot === 'plate') {
            // Plate slot → start live scanner (camera + guide box + continuous OCR)
            return this.startLiveScan();
        }
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

    /** Capture photo for a slot. Pure JS — stores in _capturedPhotos, renders preview.
     *  R-SCANNER: For plate slot during live scan — captures photo WITHOUT stopping scanner.
     *  Scanner continues running so guard can capture a good photo while OCR runs.
     *  For customer slot — captures and stops camera as before. */
    async captureAndStore(slot) {
        const videoId = slot === 'plate' ? 'plateVideo' : 'customerVideo';
        const rawUrl = await this.capturePhoto(videoId);
        if (!rawUrl) {
            this._showError('Chụp ảnh thất bại. Đảm bảo camera đã mở và có hình.');
            return false;
        }
        _capturedPhotos[slot] = rawUrl;
        // R-SCANNER: For plate slot during live scan — don't stop camera, just store photo.
        // Scanner continues. Camera stops when plate is accepted or user stops scan.
        if (slot === 'plate' && _scanLoopActive) {
            // Render preview but keep camera running for scanner
            this._renderPreview(slot, rawUrl);
            this.saveState(slot + 'Photo', rawUrl);
            // Don't stop camera, don't update UI — scanner is still running
            return true;
        }
        // Normal flow (customer slot, or plate without scanner) — stop camera
        this.stopCamera();
        _cameraActive[slot] = false;
        this._renderPreview(slot, rawUrl);
        this._updateCameraUI(slot, false);
        // Persist to sessionStorage (survives reload).
        this.saveState(slot + 'Photo', rawUrl);
        return true;
    },

    /** Cancel camera for a slot (user clicked Hủy/Dừng). Pure JS.
     *  R-SCANNER: For plate slot — stops live scanner. */
    cancelCamera(slot) {
        if (slot === 'plate' && _scanLoopActive) {
            this.stopLiveScan();
            return;
        }
        this.stopCamera();
        _cameraActive[slot] = false;
        this._updateCameraUI(slot, false);
    },

    /** Retake photo for a slot (user clicked Chụp lại/Quét lại). Clears stored photo, removes preview,
     *  shows video + open button, hides capture/cancel/retake/ocr. Pure JS.
     *  R-SCANNER: For plate slot — stops any active scanner, clears plate input, restarts live scan.
     *  #130-fix: video is now hidden (not removed) by _renderPreview, so it's still in DOM. */
    retakePhoto(slot) {
        // Stop any active scanner first
        if (slot === 'plate' && _scanLoopActive) {
            this.stopLiveScan();
        }
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
        // Hide OCR button + hints + scan status if plate slot.
        if (slot === 'plate') {
            const ocrBtn = document.getElementById('ocrButton');
            if (ocrBtn) ocrBtn.style.display = 'none';
            const ocrHint = document.getElementById('ocrHint');
            if (ocrHint) { ocrHint.textContent = ''; ocrHint.style.display = 'none'; }
            const ocrStatus = document.getElementById('ocrStatus');
            if (ocrStatus) ocrStatus.style.display = 'none';
            // Clear plate input
            const plateInput = document.getElementById('plateInput');
            if (plateInput) plateInput.value = '';
            try { sessionStorage.removeItem('vanan_guard_plateNumber'); } catch (e) {}
            // Hide manual entry + OK button
            const manualBtn = document.getElementById('plateManualBtn');
            if (manualBtn) manualBtn.style.display = 'none';
            const okBtn = document.getElementById('plateOkBtn');
            if (okBtn) okBtn.style.display = 'none';
        }
        this._updateCameraUI(slot, false);
        // Auto-reopen camera / restart scanner for convenience.
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
     *  R-OCR-4 (2026-08-18): Compress on-the-fly before upload (stored photo is raw quality 0.95).
     *  Returns { success, key } or { success: false, error }. */
    async uploadCapturedPhoto(slot, jwtToken, gatewayBaseUrl) {
        const dataUrl = this.getCapturedPhoto(slot);
        if (!dataUrl) {
            return { success: false, error: 'No captured photo for slot: ' + slot };
        }
        // R-OCR-4: Compress for upload — stored photo is raw (quality 0.95, full resolution).
        // Gateway rejects photos > Guard:MaxPhotoSizeKB (default 100KB). Compress to fit.
        const compressedUrl = await this.compressPhoto(dataUrl, _photoConfig.maxDimension, _photoConfig.jpegQuality, _photoConfig.maxSizeKB);
        const uploadUrl = compressedUrl || dataUrl; // Fallback to raw if compression fails
        try {
            const response = await fetch((gatewayBaseUrl || '') + '/api/guard/upload-photo', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Authorization': 'Bearer ' + jwtToken
                },
                body: JSON.stringify({
                    slot: slot,
                    base64Data: uploadUrl,
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
     *  R-SCANNER: Also stops live scanner if active, clears scan status + manual entry button.
     *  #130-fix: video is hidden (not removed) by _renderPreview, so just show it again. */
    _clearDOMPreview() {
        // Stop scanner if active
        if (_scanLoopActive) {
            this.stopLiveScan();
        }
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
        // Clear scan status + manual entry + OK button.
        const scanStatus = document.getElementById('plateScanStatus');
        if (scanStatus) { scanStatus.textContent = ''; scanStatus.style.display = 'none'; }
        const manualBtn = document.getElementById('plateManualBtn');
        if (manualBtn) manualBtn.style.display = 'none';
        const okBtn = document.getElementById('plateOkBtn');
        if (okBtn) okBtn.style.display = 'none';
        // Clear plate input.
        const plateInput = document.getElementById('plateInput');
        if (plateInput) plateInput.value = '';
        const phoneInput = document.getElementById('customerPhoneInput');
        if (phoneInput) phoneInput.value = '';
    },

    /** R-SCANNER: Start live plate scanning — opens camera with guide box, runs continuous OCR.
     *  Flow: Camera live → crop ROI from guide box → preprocess → OCR → VN normalize → temporal vote → auto-accept.
     *  Replaces the old capture-then-OCR flow. No need for user to take a photo first. */
    async startLiveScan() {
        if (_scanLoopActive) return true; // Already scanning
        // Start camera directly (not via openCamera — that would be circular)
        const ok = await this.startCamera('plateVideo', 'environment');
        if (!ok) {
            this._showError('Không mở được camera. Kiểm tra quyền truy cập camera.');
            return false;
        }
        _cameraActive['plate'] = true;
        this._updateCameraUI('plate', true);

        _scanLoopActive = true;
        _scanStartTime = Date.now();
        _voteBuffer = [];

        // Show guide box + scan status overlay
        this._showGuideBox(true);
        this._updateScanStatus('🔍 Đang tìm biển số... Đưa biển số vào khung.', 'scanning');

        // Preload OCR worker (non-blocking — first frame may wait for it)
        this.preloadOcrWorker().catch(err => {
            console.error('[Scanner] OCR worker preload failed:', err);
            this._updateScanStatus('⚠️ Không tải được thư viện OCR. Nhập biển số thủ công.', 'error');
        });

        // Start frame sampling loop
        this._runCameraSample();
        return true;
    },

    /** R-SCANNER: Stop live scanning — stops camera, clears guide box, stops rAF. */
    stopLiveScan() {
        _scanLoopActive = false;
        if (_scanRafId) {
            cancelAnimationFrame(_scanRafId);
            _scanRafId = null;
        }
        _ocrBusy = false;
        _camResLogged = false;
        _voteBuffer = [];
        this._showGuideBox(false);
        this._hideRoiDebug();
        this.stopCamera();
        _cameraActive['plate'] = false;
        this._updateCameraUI('plate', false);
    },

    /** R-SCANNER: Camera sampling loop — runs at 30 FPS via requestAnimationFrame.
     *  Crops ROI from guide box EVERY frame (cheap — just drawImage).
     *  Shows ROI debug overlay so user can verify crop is correct.
     *  Submits ROI to OCR worker only when:
     *    1. OCR worker is NOT busy (_ocrBusy === false)
     *    2. At least ocrIntervalMs since last OCR (rate limit ~2.5/sec)
     *  This DECOUPLES camera smoothness (30 FPS) from OCR throughput (~2.5/sec). */
    _runCameraSample() {
        if (!_scanLoopActive) return;

        const video = document.getElementById('plateVideo');
        if (video && video.videoWidth) {
            // R-TELEMETRY: Log actual camera resolution once (not every frame)
            if (!this._camResLogged) {
                console.log('[Scanner] Camera resolution:', {
                    requested: '1920x1080 (ideal)',
                    actual: `${video.videoWidth}x${video.videoHeight}`
                });
                this._camResLogged = true;
            }

            // Crop ROI every frame — cheap operation, keeps debug overlay live
            const roiCanvas = this._cropRoiFromVideo(video);
            if (roiCanvas) {
                // Show ROI debug overlay (so user + dev can verify crop is correct)
                this._showRoiDebug(roiCanvas);

                // Quality gate — skip OCR if ROI too small or too dark/bright
                if (this._isGoodFrame(roiCanvas)) {
                    // Submit to OCR only if worker is free + rate limit satisfied
                    const now = Date.now();
                    if (!_ocrBusy && (now - _ocrLastTime) > _voteConfig.ocrIntervalMs) {
                        _ocrBusy = true;
                        _ocrLastTime = now;
                        // Fire-and-forget — don't await, camera loop continues at 30 FPS
                        this._processOcrFrame(roiCanvas, video);
                    }
                }
            }
        }

        // Check timeout — show hint after 5s with no stable result
        const elapsed = Date.now() - _scanStartTime;
        if (elapsed > _voteConfig.timeoutMs && _voteBuffer.length < 2) {
            this._updateScanStatus(
                '⚠️ Chưa nhìn rõ biển số.\n• Đưa camera gần hơn\n• Giữ điện thoại ổn định\n• Đảm bảo biển số đủ sáng',
                'warning'
            );
            const manualBtn = document.getElementById('plateManualBtn');
            if (manualBtn) manualBtn.style.display = '';
        }

        // Continue camera loop at 30 FPS — NOT blocked by OCR
        _scanRafId = requestAnimationFrame(() => this._runCameraSample());
    },

    /** R-SCANNER: Process one OCR frame — async, runs in parallel with camera loop.
     *  R-UX: Every OCR result fills the plate textbox immediately + shows "OK" button.
     *  Guard reviews result vs real plate, clicks OK to confirm (stops scan).
     *  Scanner continues running so guard can wait for a better result if first is wrong.
     *  Sets _ocrBusy=false when done so next camera sample can submit a new frame. */
    async _processOcrFrame(roiCanvas, video) {
        try {
            const ocrResult = await this._ocrRoi(roiCanvas);

            if (ocrResult && ocrResult.plate) {
                _voteBuffer.push({
                    plate: ocrResult.plate,
                    confidence: ocrResult.confidence,
                    timestamp: Date.now()
                });
                if (_voteBuffer.length > _voteConfig.maxBufferSize) {
                    _voteBuffer.shift();
                }

                // R-UX: Fill plate textbox IMMEDIATELY with latest OCR result
                // Guard sees result in real-time and can click OK when it matches
                this.setInputValue('plateInput', ocrResult.plate);

                // Show OK button so guard can confirm
                const okBtn = document.getElementById('plateOkBtn');
                if (okBtn) okBtn.style.display = '';

                // Update status with latest result
                this._updateScanStatus(
                    `Đang quét... ${ocrResult.plate} (${Math.round(ocrResult.confidence)}%) — bấm OK nếu đúng`,
                    'scanning'
                );

                // R-UX: Auto-accept removed — was too rigid (confidence threshold + vote count).
                // Guard manually confirms via OK button. Scanner continues for better results.
            }
        } catch (err) {
            console.error('[Scanner] OCR frame error:', err);
        } finally {
            _ocrBusy = false; // Free worker for next frame
        }
    },

    /** R-SCANNER: Quality gate — skip OCR if ROI is too small, too dark, or too bright.
     *  Saves OCR cycles on bad frames (motion blur, glare, dark, empty guide box).
     *  Returns true if frame is worth OCR-ing. */
    _isGoodFrame(canvas) {
        if (canvas.width < 60 || canvas.height < 20) return false; // Too small
        try {
            const ctx = canvas.getContext('2d');
            const imageData = ctx.getImageData(0, 0, canvas.width, canvas.height);
            const data = imageData.data;
            let sum = 0;
            const sampleStep = 16; // Sample every 4th pixel (4 bytes RGBA)
            let count = 0;
            for (let i = 0; i < data.length; i += sampleStep) {
                sum += 0.299 * data[i] + 0.587 * data[i + 1] + 0.114 * data[i + 2];
                count++;
            }
            const avgBrightness = sum / count;
            // Skip if too dark (<30) or too bright/washed out (>240)
            return avgBrightness > 30 && avgBrightness < 240;
        } catch (e) {
            return true; // If getImageData fails, don't block OCR
        }
    },

    /** R-SCANNER: Show ROI debug overlay — renders the actual cropped ROI next to camera
     *  so user/developer can verify the crop contains the license plate.
     *  This is the P0 debug step — if ROI is wrong, no OCR tuning will help. */
    _showRoiDebug(canvas) {
        let debugEl = document.getElementById('plateRoiDebug');
        if (!debugEl) return; // Debug overlay not in DOM — skip silently
        // Copy ROI canvas to debug canvas
        let debugCanvas = debugEl.querySelector('canvas');
        if (!debugCanvas) {
            debugCanvas = document.createElement('canvas');
            debugCanvas.style.cssText = 'width:100%;border:1px solid #3b82f6;border-radius:4px;';
            debugEl.appendChild(debugCanvas);
        }
        debugCanvas.width = canvas.width;
        debugCanvas.height = canvas.height;
        debugCanvas.getContext('2d').drawImage(canvas, 0, 0);
        debugEl.style.display = '';
    },

    /** R-SCANNER: Hide ROI debug overlay. */
    _hideRoiDebug() {
        const debugEl = document.getElementById('plateRoiDebug');
        if (debugEl) debugEl.style.display = 'none';
    },

    /** R-SCANNER: Crop ROI from video based on guide box position.
     *  CRITICAL: Video uses object-fit:cover — displayed area is CROPPED, not just scaled.
     *  Must account for cover crop offset when mapping display coords → video resolution.
     *  R-SPEED: Returns canvas DIRECTLY (not dataURL) — avoids toDataURL → Image load roundtrip.
     *  Tesseract.recognize() accepts canvas, so no conversion needed. */
    _cropRoiFromVideo(video) {
        const guideBox = document.getElementById('plateGuideBox');
        if (!guideBox || !video.videoWidth) return null;

        const videoRect = video.getBoundingClientRect();
        const boxRect = guideBox.getBoundingClientRect();

        // object-fit:cover — video scaled to cover display area, excess cropped.
        const videoAspect = video.videoWidth / video.videoHeight;
        const displayAspect = videoRect.width / videoRect.height;

        let scale, coverOffsetX, coverOffsetY;
        if (videoAspect > displayAspect) {
            scale = videoRect.height / video.videoHeight;
            coverOffsetX = (video.videoWidth * scale - videoRect.width) / 2;
            coverOffsetY = 0;
        } else {
            scale = videoRect.width / video.videoWidth;
            coverOffsetX = 0;
            coverOffsetY = (video.videoHeight * scale - videoRect.height) / 2;
        }

        const sx = Math.max(0, Math.round((boxRect.left - videoRect.left + coverOffsetX) / scale));
        const sy = Math.max(0, Math.round((boxRect.top - videoRect.top + coverOffsetY) / scale));
        const sw = Math.min(video.videoWidth - sx, Math.round(boxRect.width / scale));
        const sh = Math.min(video.videoHeight - sy, Math.round(boxRect.height / scale));

        if (sw < 20 || sh < 10) return null;

        // R-SPEED: Return canvas directly — Tesseract accepts canvas, no dataURL roundtrip
        const canvas = document.createElement('canvas');
        canvas.width = sw;
        canvas.height = sh;
        const ctx = canvas.getContext('2d');
        ctx.drawImage(video, sx, sy, sw, sh, 0, 0, sw, sh);
        return canvas;
    },

    /** R-SCANNER: OCR on ROI — Tesseract + VN normalize + validate.
     *  OCR Hub S2: Now delegates to vananOcrHub.recognize() (configurable engine).
     *  OCR Hub S1: 2-row crop for VN plates (top: ##X, bottom: ####.##) + tilt detection.
     *  If tilt > 15° → fallback to full-ROI OCR (review fix — prevent midY crop error).
     *  Returns {plate, confidence, raw} or null. */
    async _ocrRoi(canvas) {
        const t0 = performance.now();
        const ocrCanvas = this._preprocessRoiForOcr(canvas);

        // OCR Hub S1: Try 2-row OCR first (VN plates have 2 rows: top=##X, bottom=####.##)
        const twoRowResult = await this._ocrTwoRows(ocrCanvas);
        if (twoRowResult) {
            const ocrMs = Math.round(performance.now() - t0);
            console.log('[Scanner] OCR 2-row', {
                plate: twoRowResult.plate,
                confidence: Math.round(twoRowResult.confidence),
                ocrMs,
                roiW: canvas.width,
                roiH: canvas.height
            });
            return { plate: twoRowResult.plate, confidence: twoRowResult.confidence, raw: twoRowResult.plate };
        }

        // Fallback: full-ROI OCR via OCR Hub (configurable engine)
        const hubResult = await window.vananOcrHub.recognize(ocrCanvas);
        const ocrMs = Math.round(performance.now() - t0);
        const raw = (hubResult?.text || '').toUpperCase().trim();
        const confidence = hubResult?.confidence || 0;

        console.log('[Scanner] OCR full-ROI', {
            raw: raw.substring(0, 40),
            confidence: Math.round(confidence),
            ocrMs,
            roiW: canvas.width,
            roiH: canvas.height,
            ocrW: ocrCanvas.width,
            ocrH: ocrCanvas.height
        });

        if (!raw) return null;

        // Try normalizer first
        const normalized = this._normalizeVnPlate(raw);

        // R-BUGFIX: If normalizer killed it, check if raw is still plate-like
        if (!normalized) {
            const cleaned = raw.replace(/[^0-9A-ZĐ\-\.]/g, '').replace(/^-+|-+$/g, '');
            const digits = (cleaned.match(/[0-9]/g) || []).length;
            const letters = (cleaned.match(/[A-ZĐ]/g) || []).length;
            if (digits > 0 && letters > 0 && cleaned.length >= 5 && cleaned.length <= 12) {
                console.log('[Scanner] Normalizer failed, using cleaned raw:', cleaned);
                return { plate: cleaned, confidence, raw };
            }
            return null;
        }

        return { plate: normalized, confidence, raw };
    },

    /** OCR Hub S1: Detect tilt angle in ROI canvas using horizontal projection profile.
     *  Returns approximate tilt in degrees (0 = straight, > 0 = tilted).
     *  If text rows are not horizontal → plate is skewed → don't crop midY. */
    _detectTilt(canvas) {
        try {
            const ctx = canvas.getContext('2d');
            const w = Math.min(canvas.width, 200); // Downscale for speed
            const h = Math.min(canvas.height, 100);
            const tmpCanvas = document.createElement('canvas');
            tmpCanvas.width = w;
            tmpCanvas.height = h;
            const tmpCtx = tmpCanvas.getContext('2d');
            tmpCtx.drawImage(canvas, 0, 0, w, h);
            const imageData = tmpCtx.getImageData(0, 0, w, h);
            const data = imageData.data;

            // Horizontal projection profile: count dark pixels per row
            const rowSums = new Array(h).fill(0);
            for (let y = 0; y < h; y++) {
                let sum = 0;
                for (let x = 0; x < w; x++) {
                    const idx = (y * w + x) * 4;
                    const brightness = (data[idx] + data[idx + 1] + data[idx + 2]) / 3;
                    if (brightness < 128) sum++; // Dark pixel = text
                }
                rowSums[y] = sum;
            }

            // Find text row boundaries (rows with > 20% dark pixels)
            const threshold = w * 0.2;
            let firstTextRow = -1, lastTextRow = -1;
            for (let y = 0; y < h; y++) {
                if (rowSums[y] > threshold) {
                    if (firstTextRow < 0) firstTextRow = y;
                    lastTextRow = y;
                }
            }

            if (firstTextRow < 0 || lastTextRow < 0) return 0; // No text found

            // Check if text region is centered vertically (straight plate)
            // If text spans < 60% of height → likely tilted (text shifted to one side)
            const textSpan = lastTextRow - firstTextRow;
            const textSpanRatio = textSpan / h;
            if (textSpanRatio < 0.4) return 20; // Text too compact → likely tilted

            return 0; // Looks straight enough
        } catch (e) {
            return 0; // Can't detect — assume straight
        }
    },

    /** OCR Hub S1: OCR 2 rows separately for VN plates.
     *  OCR Hub S2: Now uses vananOcrHub.recognize() (configurable engine).
     *  Fix #2: Detect actual gap between rows via horizontal projection profile
     *  instead of blind 50/50 cut. Top row: ##X, Bottom row: ####.##.
     *  If tilt detected or either row fails → return null (fallback to full-ROI). */
    async _ocrTwoRows(canvas) {
        // Review fix: check tilt before crop — don't crop midY if plate is skewed
        const tilt = this._detectTilt(canvas);
        if (tilt > 15) {
            console.log('[Scanner] Tilt detected (' + tilt + '°) — fallback to full-ROI OCR');
            return null;
        }

        // Fix #2: Detect actual gap between 2 rows using horizontal projection profile
        const splitY = this._detectRowGap(canvas);
        console.log('[Scanner] 2-row split at y=' + splitY + '/' + canvas.height);

        const topCanvas = this._cropCanvas(canvas, 0, 0, canvas.width, splitY);
        const bottomCanvas = this._cropCanvas(canvas, 0, splitY, canvas.width, canvas.height - splitY);

        // OCR top row (province + letters: ##X) via OCR Hub
        const topRaw = await this._ocrSingleRow(topCanvas);
        if (!topRaw) return null;

        // OCR bottom row (numbers: ####.##) via OCR Hub
        const bottomRaw = await this._ocrSingleRow(bottomCanvas);
        if (!bottomRaw) return null;

        // Normalize + validate each row
        const topPlate = this._normalizeTopRow(topRaw.text);
        const bottomPlate = this._normalizeBottomRow(bottomRaw.text);

        if (topPlate && bottomPlate) {
            const plate = topPlate + '-' + bottomPlate;
            const confidence = (topRaw.confidence + bottomRaw.confidence) / 2;
            console.log('[Scanner] 2-row OK:', { top: topPlate, bottom: bottomPlate, plate });
            return { plate, confidence };
        }

        return null; // One row failed → fallback
    },

    /** Fix #2: Detect the horizontal gap between 2 rows of a VN plate.
     *  Uses horizontal projection profile — finds the row with fewest dark pixels
     *  in the middle 60% of the canvas height (the gap between top and bottom rows).
     *  Returns the Y coordinate to split at. */
    _detectRowGap(canvas) {
        try {
            const ctx = canvas.getContext('2d');
            const w = canvas.width;
            const h = canvas.height;
            const imageData = ctx.getImageData(0, 0, w, h);
            const data = imageData.data;

            // Horizontal projection: count dark pixels per row
            const rowSums = new Array(h).fill(0);
            for (let y = 0; y < h; y++) {
                let sum = 0;
                for (let x = 0; x < w; x++) {
                    const idx = (y * w + x) * 4;
                    const brightness = (data[idx] + data[idx + 1] + data[idx + 2]) / 3;
                    if (brightness < 128) sum++;
                }
                rowSums[y] = sum;
            }

            // Search for gap in middle 60% of height (between 20% and 80%)
            const searchStart = Math.round(h * 0.2);
            const searchEnd = Math.round(h * 0.8);
            let minSum = Infinity;
            let gapY = Math.round(h * 0.5); // fallback to 50%

            for (let y = searchStart; y < searchEnd; y++) {
                if (rowSums[y] < minSum) {
                    minSum = rowSums[y];
                    gapY = y;
                }
            }

            return gapY;
        } catch (e) {
            console.warn('[Scanner] _detectRowGap failed, using 50%:', e);
            return Math.round(canvas.height * 0.5);
        }
    },

    /** OCR Hub S2: OCR a single row canvas via vananOcrHub (configurable engine). */
    async _ocrSingleRow(canvas) {
        try {
            const result = await window.vananOcrHub.recognize(canvas);
            if (!result || !result.text) return null;
            const text = result.text.toUpperCase().trim();
            if (!text) return null;
            return { text, confidence: result.confidence || 0 };
        } catch (e) {
            return null;
        }
    },

    /** OCR Hub S1: Normalize top row — 2 digits + 1-2 letters (##X or ##XX).
     *  Valid: 51F, 59P1, 60LD, 51ĐAB. Returns normalized string or null. */
    _normalizeTopRow(raw) {
        if (!raw) return null;
        let s = raw.replace(/[^0-9A-ZĐ]/g, '');
        if (s.length < 3 || s.length > 5) return null;
        // First 2 chars must be digits (province code)
        const province = s.substring(0, 2).replace(/O/g, '0').replace(/I/g, '1').replace(/S/g, '5').replace(/B/g, '8').replace(/Z/g, '2').replace(/G/g, '6');
        if (!/^\d{2}$/.test(province)) return null;
        // Rest must be letters (1-2 chars)
        const letters = s.substring(2).replace(/0/g, 'O').replace(/1/g, 'I').replace(/5/g, 'S').replace(/8/g, 'B').replace(/2/g, 'Z').replace(/6/g, 'G');
        if (!/^[A-ZĐ]{1,3}$/.test(letters)) return null;
        return province + letters;
    },

    /** OCR Hub S1: Normalize bottom row — 3-5 digits, optional dot separator (#### or ###.##).
     *  Valid: 12345, 123.45, 6789. Returns normalized string or null. */
    _normalizeBottomRow(raw) {
        if (!raw) return null;
        let s = raw.replace(/[^0-9\.]/g, '');
        if (s.length < 3 || s.length > 8) return null;
        // Convert any letter confusions to digits
        s = s.replace(/O/g, '0').replace(/I/g, '1').replace(/S/g, '5').replace(/B/g, '8').replace(/Z/g, '2').replace(/G/g, '6');
        // Validate: digits with optional dot
        if (!/^\d{3,5}(\.\d{2})?$/.test(s)) return null;
        return s;
    },

    /** OCR Hub S1: Crop a sub-canvas from source canvas. */
    _cropCanvas(src, sx, sy, sw, sh) {
        const canvas = document.createElement('canvas');
        canvas.width = sw;
        canvas.height = sh;
        const ctx = canvas.getContext('2d');
        ctx.drawImage(src, sx, sy, sw, sh, 0, 0, sw, sh);
        return canvas;
    },

    /** OCR Hub S1: Preprocess ROI canvas for OCR — downscale + contrast boost.
     *  Downscale to max 300px wide (S1: reduced from 400px — 2-row plates need less width).
     *  Apply contrast(1.4) brightness(1.1) filter to improve binarization. */
    _preprocessRoiForOcr(srcCanvas) {
        const maxW = 300;
        let w = srcCanvas.width;
        let h = srcCanvas.height;
        if (w > maxW) {
            const ratio = maxW / w;
            w = Math.round(w * ratio);
            h = Math.round(h * ratio);
        }
        const canvas = document.createElement('canvas');
        canvas.width = w;
        canvas.height = h;
        const ctx = canvas.getContext('2d');
        try { ctx.filter = 'contrast(1.4) brightness(1.1)'; } catch (e) { /* older browsers */ }
        ctx.drawImage(srcCanvas, 0, 0, w, h);
        return canvas;
    },

    /** R-SCANNER: Check temporal voting — returns {plate, confidence} if stable result, null otherwise.
     *  Requires at least minVotes matching plates with avg confidence >= minAvgConfidence. */
    _checkTemporalVote() {
        if (_voteBuffer.length < _voteConfig.minVotes) return null;

        // Count votes per plate (exact match after normalization)
        const counts = {};
        let bestPlate = null;
        let bestCount = 0;
        let bestConfidenceSum = 0;

        for (const entry of _voteBuffer) {
            if (!counts[entry.plate]) {
                counts[entry.plate] = { count: 0, confidenceSum: 0 };
            }
            counts[entry.plate].count++;
            counts[entry.plate].confidenceSum += entry.confidence;

            if (counts[entry.plate].count > bestCount) {
                bestCount = counts[entry.plate].count;
                bestPlate = entry.plate;
                bestConfidenceSum = counts[entry.plate].confidenceSum;
            }
        }

        if (bestCount >= _voteConfig.minVotes) {
            const avgConfidence = bestConfidenceSum / bestCount;
            if (avgConfidence >= _voteConfig.minAvgConfidence) {
                return { plate: bestPlate, confidence: avgConfidence };
            }
        }

        // Also check near-matches — plates differing by 1 char (OCR confusion)
        const nearMatch = this._checkNearMatchVotes();
        if (nearMatch) return nearMatch;

        return null;
    },

    /** R-SCANNER: Check near-match votes — plates that differ by exactly 1 character.
     *  Groups plates by "stem" (all chars except one) and counts votes per stem.
     *  If a stem group has enough votes, pick the variant with highest confidence. */
    _checkNearMatchVotes() {
        if (_voteBuffer.length < _voteConfig.minVotes) return null;

        // Group by stem (remove one char at each position)
        const stemGroups = {};
        for (const entry of _voteBuffer) {
            const plate = entry.plate;
            for (let i = 0; i < plate.length; i++) {
                const stem = plate.substring(0, i) + '*' + plate.substring(i + 1);
                if (!stemGroups[stem]) {
                    stemGroups[stem] = { count: 0, entries: [] };
                }
                stemGroups[stem].count++;
                stemGroups[stem].entries.push(entry);
            }
        }

        // Find the stem group with most votes
        let bestStem = null;
        let bestStemCount = 0;
        for (const [stem, group] of Object.entries(stemGroups)) {
            if (group.count > bestStemCount) {
                bestStemCount = group.count;
                bestStem = group;
            }
        }

        if (bestStem && bestStemCount >= _voteConfig.minVotes) {
            // Pick the most common variant within this stem group
            const variantCounts = {};
            for (const entry of bestStem.entries) {
                if (!variantCounts[entry.plate]) {
                    variantCounts[entry.plate] = { count: 0, confidenceSum: 0 };
                }
                variantCounts[entry.plate].count++;
                variantCounts[entry.plate].confidenceSum += entry.confidence;
            }
            let bestVariant = null;
            let bestVariantCount = 0;
            for (const [plate, vc] of Object.entries(variantCounts)) {
                if (vc.count > bestVariantCount) {
                    bestVariantCount = vc.count;
                    bestVariant = { plate, confidence: vc.confidenceSum / vc.count };
                }
            }
            if (bestVariant && bestVariant.confidence >= _voteConfig.minAvgConfidence) {
                return bestVariant;
            }
        }
        return null;
    },

    /** R-SCANNER: Called when plate is auto-accepted — fill input, capture photo, stop scan, show success.
     *  R-BUGFIX: Must be async + await capturePhoto() — capturePhoto returns Promise<dataUrl>,
     *  not dataUrl. Without await, _capturedPhotos stores a Promise object → preview broken. */
    async _onPlateAccepted(plate, confidence, video) {
        // Fill plate input
        this.setInputValue('plateInput', plate);
        this.saveState('plateNumber', plate);

        // Capture current frame as plate photo (full frame, not just ROI)
        const photoUrl = await this.capturePhoto('plateVideo');
        if (photoUrl) {
            _capturedPhotos['plate'] = photoUrl;
            this.saveState('platePhoto', photoUrl);
            this._renderPreview('plate', photoUrl);
        }

        // Stop scanning
        _scanLoopActive = false;
        if (_scanRafId) {
            cancelAnimationFrame(_scanRafId);
            _scanRafId = null;
        }
        _ocrBusy = false;
        this._showGuideBox(false);
        this._hideRoiDebug();
        this.stopCamera();
        _cameraActive['plate'] = false;
        this._updateCameraUI('plate', false);

        // Show success status
        this._updateScanStatus(`✓ Đã nhận diện: ${plate} (${Math.round(confidence)}%)`, 'success');

        // Show retake button
        const retakeBtn = document.getElementById('plateRetakeBtn');
        if (retakeBtn) retakeBtn.style.display = '';

        // Hide manual entry button
        const manualBtn = document.getElementById('plateManualBtn');
        if (manualBtn) manualBtn.style.display = 'none';

        console.log('[Scanner] Plate accepted:', plate, 'confidence:', confidence);
    },

    /** R-UX: Guard clicks OK to confirm the plate shown in textbox.
     *  Stops scanner, captures photo, saves plate. Guard can edit textbox before clicking OK. */
    async confirmPlate() {
        const plateInput = document.getElementById('plateInput');
        const plate = plateInput ? plateInput.value.trim().toUpperCase() : '';
        if (!plate) {
            this._showError('Chưa có biển số. Quét hoặc nhập tay trước.');
            return;
        }

        // Capture current frame as plate photo
        // R-BUGFIX: await capturePhoto — it's async, returns Promise<dataUrl>
        const photoUrl = await this.capturePhoto('plateVideo');
        if (photoUrl) {
            _capturedPhotos['plate'] = photoUrl;
            this.saveState('platePhoto', photoUrl);
            this._renderPreview('plate', photoUrl);
        }

        // Stop scanning
        _scanLoopActive = false;
        if (_scanRafId) {
            cancelAnimationFrame(_scanRafId);
            _scanRafId = null;
        }
        _ocrBusy = false;
        this._showGuideBox(false);
        this._hideRoiDebug();
        this.stopCamera();
        _cameraActive['plate'] = false;
        this._updateCameraUI('plate', false);

        // Save plate
        this.saveState('plateNumber', plate);

        // Show success status
        this._updateScanStatus(`✓ Đã xác nhận: ${plate}`, 'success');

        // Show retake button, hide OK + manual buttons
        const retakeBtn = document.getElementById('plateRetakeBtn');
        if (retakeBtn) retakeBtn.style.display = '';
        const okBtn = document.getElementById('plateOkBtn');
        if (okBtn) okBtn.style.display = 'none';
        const manualBtn = document.getElementById('plateManualBtn');
        if (manualBtn) manualBtn.style.display = 'none';

        console.log('[Scanner] Plate confirmed by guard:', plate);
    },

    /** R-SCANNER: Toggle guide box orientation between portrait (square) and landscape (rect). */
    toggleGuideOrientation() {
        _guideOrientation = (_guideOrientation === 'portrait') ? 'landscape' : 'portrait';
        const guideBox = document.getElementById('plateGuideBox');
        if (guideBox) {
            guideBox.className = 'guard-guide-box guard-guide-box--' + _guideOrientation;
        }
        const toggleBtn = document.getElementById('plateOrientationBtn');
        if (toggleBtn) {
            toggleBtn.textContent = _guideOrientation === 'portrait' ? '📐 Dọc (xe máy)' : '📐 Ngang (xe hơi)';
        }
    },

    /** R-SCANNER: Show/hide guide box overlay on video. Applies current orientation class. */
    _showGuideBox(show) {
        const guideBox = document.getElementById('plateGuideBox');
        const scanStatus = document.getElementById('plateScanStatus');
        if (guideBox) {
            guideBox.className = 'guard-guide-box guard-guide-box--' + _guideOrientation;
            guideBox.style.display = show ? '' : 'none';
        }
        if (scanStatus) scanStatus.style.display = show ? '' : 'none';
    },

    /** R-SCANNER: Update scan status text + style. type: 'scanning' | 'warning' | 'success' | 'error'. */
    _updateScanStatus(text, type) {
        const el = document.getElementById('plateScanStatus');
        if (!el) return;
        el.textContent = text;
        el.className = 'guard-scan-status guard-scan-status--' + (type || 'scanning');
        // Show element
        el.style.display = '';
    },

    /** R-SCANNER: Legacy OCR button handler — runs single-frame OCR on already-captured photo.
     *  Kept as fallback when live scan is not available or user has a pre-captured photo. */
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
                const formatOk = this._isVnPlateFormat(plate);
                if (ocrHint) {
                    if (formatOk) {
                        ocrHint.textContent = 'Đã nhận diện: ' + plate + ' — kiểm tra lại trước khi tạo QR.';
                        ocrHint.style.color = '#6b7280';
                    } else {
                        ocrHint.textContent = '⚠️ Đã nhận diện: ' + plate + ' — KHÔNG đúng format biển VN. Kiểm tra lại trước khi tạo QR.';
                        ocrHint.style.color = '#b45309';
                    }
                    ocrHint.style.display = '';
                }
                this.saveState('plateNumber', plate);
            } else {
                if (ocrHint) {
                    ocrHint.textContent = 'Không nhận diện được — nhập thủ công.';
                    ocrHint.style.color = '#6b7280';
                    ocrHint.style.display = '';
                }
            }
        } catch (err) {
            console.error('OCR error:', err);
            if (ocrHint) {
                ocrHint.textContent = 'OCR lỗi — nhập biển số thủ công.';
                ocrHint.style.color = '#6b7280';
                ocrHint.style.display = '';
            }
        } finally {
            if (ocrStatus) ocrStatus.style.display = 'none';
            if (ocrBtn) ocrBtn.disabled = false;
        }
    },

    /** R-OCR-6 (2026-08-18): Validate VN license plate format.
     *  Xe máy: \d{2}[A-ZĐ]{1,2}-\d{4,5} (VD: 51F-12345, 59P1-67890)
     *  Xe hơi:  \d{2}[A-Z]{1,2}-\d{3}\.\d{2} (VD: 51F-123.45) — dấu chấm có thể bị OCR bỏ
     *  Điện:    \d{2}[A-ZĐ]{1,2}-\d{4,5} (VD: 51ĐAB-123.45) — Đ cho xe máy điện
     *  Returns true if matches any VN format (with or without dot separator). */
    _isVnPlateFormat(s) {
        if (!s || s.length < 5) return false;
        // Normalize: OCR may strip dots — accept both dash-only and dash+dot.
        // Pattern: 2 digits, 1-2 letters (incl Đ), dash, 3-5 digits (optionally dot-separated).
        const vnPlateRegex = /^\d{2}[A-ZĐ]{1,2}-\d{3,5}(\.\d{2})?$/;
        return vnPlateRegex.test(s);
    },

    /** Restore UI state from sessionStorage after page reload. Pure JS — called on DOMContentLoaded.
     *  R-SCANNER: Also restores scan status if plate was previously accepted. */
    restoreUIFromSession() {
        const platePhoto = this.loadState('platePhoto');
        const customerPhoto = this.loadState('customerPhoto');
        if (platePhoto) {
            _capturedPhotos.plate = platePhoto;
            this._renderPreview('plate', platePhoto);
            this._updateCameraUI('plate', false);
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
            // Show success status if plate was previously accepted
            if (platePhoto) {
                this._updateScanStatus(`✓ Đã nhận diện: ${plateNumber}`, 'success');
            }
        }
        const customerPhone = this.loadState('customerPhone');
        if (customerPhone) {
            const input = document.getElementById('customerPhoneInput');
            if (input) input.value = customerPhone;
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

    /** Generate QR code image (base64 PNG) from text using vendored qrcode-generator (no CDN). */
    async generateQrImage(text, size) {
        try {
            await this._ensureQrLibrary();
            const targetSize = size || 300;
            const canvas = document.createElement('canvas');
            vananQR._drawToCanvas(canvas, text, targetSize, targetSize);
            return canvas.toDataURL('image/png');
        } catch (err) {
            console.error('QR generation failed:', err);
            return null;
        }
    },

    async _ensureQrLibrary() {
        if (window.vananQR) return;
        return new Promise((resolve, reject) => {
            const script = document.createElement('script');
            script.src = '/js/lib/qrcode.js';
            script.onload = () => resolve();
            script.onerror = () => reject(new Error('Failed to load vendored qrcode library'));
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
            const targetSize = size || 200;
            vananQR._drawToCanvas(canvas, text, targetSize, targetSize);
            return true;
        } catch (err) {
            console.error('QR generation to canvas failed:', err);
            return false;
        }
    },

    // === #126 OCR: License plate recognition (Tesseract.js, client-side) ===
    // #130-fix: Preload Tesseract on page load (not lazy on first capture) for faster first OCR.
    // #130-fix: Reduced PSM modes from 4→2 (7=single line best for plates, 6=fallback).
    // #130-fix: Added "Đ" to whitelist for xe máy điện plates (e.g., "ĐAB-123.45").
    // #130-fix: Reduced upscale 2x→1.5x for faster preprocessing (accuracy still good).

    _ocrWorkerPromise: null,  // Preloaded worker promise (reuse across captures)

    /** Preload Tesseract worker on page load — eliminates ~3s delay on first capture.
     *  R-Đ: Use 'eng+vie' languages — vie traineddata recognizes Vietnamese "Đ" char
     *  for xe máy điện plates (e.g., 41MĐ-123456). eng alone cannot recognize "Đ". */
    async preloadOcrWorker() {
        if (this._ocrWorkerPromise) return this._ocrWorkerPromise;
        this._ocrWorkerPromise = (async () => {
            try {
                await this._ensureOcrLibrary();
                // OCR Hub S1: Stricter whitelist — removed Q, J, U, W, V, I (not in VN plates)
                const whitelist = '0123456789ABCDEFGHKLMNPRSTXYZĐ-.';
                // R-Đ: eng+vie — eng for digits/letters, vie for "Đ" character
                const worker = await Tesseract.createWorker('eng+vie', 1, {
                    workerPath: '/js/lib/ocr/worker.min.js',
                    corePath: '/js/lib/ocr',
                    langPath: '/js/lib/ocr',
                    logger: () => {}
                });
                await worker.setParameters({ tessedit_char_whitelist: whitelist });
                console.log('[OCR] Tesseract worker preloaded (eng+vie)');
                return worker;
            } catch (err) {
                console.error('[OCR] Failed to preload Tesseract worker (eng+vie), falling back to eng:', err);
                // Fallback to eng only if vie fails to load
                try {
                    const worker = await Tesseract.createWorker('eng', 1, {
                        workerPath: '/js/lib/ocr/worker.min.js',
                        corePath: '/js/lib/ocr',
                        langPath: '/js/lib/ocr',
                        logger: () => {}
                    });
                    await worker.setParameters({ tessedit_char_whitelist: '0123456789ABCDEFGHKLMNPRSTXYZĐ-.' });
                    console.log('[OCR] Tesseract worker preloaded (eng fallback)');
                    return worker;
                } catch (err2) {
                    console.error('[OCR] eng fallback also failed:', err2);
                    this._ocrWorkerPromise = null;
                    throw err2;
                }
            }
        })();
        return this._ocrWorkerPromise;
    },

    /** Recognize license plate text from a base64 JPEG data URL. Returns cleaned plate string or ''.
     *  Uses preloaded worker (preloadOcrWorker) for fast first capture.
     *  R-OCR-5 (2026-08-18): Multi-PSM [7,6,8,13,4] + confidence filter (>=60).
     *  R-OCR-10 (2026-08-18): Telemetry logging to console for tuning.
     *  Tries PSM 7 (single line — best for plates), 6 (uniform block), 8 (single word),
     *  13 (raw line — no segmentation), 4 (single column) as fallbacks. */
    async recognizePlate(dataUrl) {
        try {
            if (!dataUrl) return '';
            const worker = await this.preloadOcrWorker();
            const canvas = await this._preprocessForOcr(dataUrl);
            // R-OCR-fix3: PSM 7 (single line) is best for plates — try first, then 6 (uniform block).
            // Removed 8/13/4 — they produce garbage on full-image OCR (reading background text).
            const psmModes = ['7', '6'];
            const minConfidence = 70;  // Raised from 60 — filter more noise
            let bestPlate = '';
            let bestScore = -1;
            const telemetry = { psms: [], bestConfidence: 0, bestPsm: null };
            for (const psm of psmModes) {
                try {
                    await worker.setParameters({ tessedit_pageseg_mode: psm });
                    const { data } = await worker.recognize(canvas);
                    const raw = (data.text || '').toUpperCase();
                    const plate = this._normalizePlate(raw);
                    const confidence = data.confidence || 0;
                    const score = this._scorePlate(plate);
                    telemetry.psms.push({ psm, raw: raw.substring(0, 40), plate, confidence: Math.round(confidence), score });
                    // R-OCR-fix3: Skip if score < 0 (garbage — too long or no letters/digits)
                    if (score < 0) continue;
                    // R-OCR-5: Filter low-confidence results — if confidence < minConfidence, skip
                    // (unless score is high — format match can override low confidence).
                    if (confidence < minConfidence && score < 8) continue;
                    if (score > bestScore || (score === bestScore && confidence > telemetry.bestConfidence)) {
                        bestScore = score;
                        bestPlate = plate;
                        telemetry.bestConfidence = Math.round(confidence);
                        telemetry.bestPsm = psm;
                    }
                } catch (e) {
                    // Continue to next PSM mode.
                }
            }
            // R-OCR-10: Telemetry log for tuning — identifies fail patterns (glare? góc? biển điện?).
            console.log('[OCR Telemetry]', telemetry);
            return bestPlate;
        } catch (err) {
            console.error('Plate OCR failed:', err);
            return '';
        }
        // Worker NOT terminated — reused for next capture (preload pattern).
    },

    /** Score a plate string: higher = more plate-like. Must have both letters and digits,
     *  length 5-12, and not be all one type.
     *  R-OCR-fix3 (2026-08-18): Strict length filter — reject >12 chars (garbage from full-image OCR).
     *  VN plates: 51F-12345 (8), 59P1-67890 (10), 51F-123.45 (10) — max ~12 chars. */
    _scorePlate(s) {
        if (!s || s.length < 4) return -1;
        // R-OCR-fix3: Hard reject too long — Tesseract reading background noise.
        if (s.length > 12) return -1;
        const digits = (s.match(/[0-9]/g) || []).length;
        const letters = (s.match(/[A-ZĐ]/g) || []).length;
        if (digits === 0 || letters === 0) return -1; // Need both.
        // Prefer length 6-9 (typical VN plate after normalization).
        let score = digits + letters;
        if (s.length >= 6 && s.length <= 9) score += 5;
        // R-OCR-fix3: Bonus for VN plate format match (digits-letters-digits pattern).
        if (/^\d{2}[A-ZĐ]{1,2}-?\d{3,5}$/.test(s)) score += 10;
        return score;
    },

    /** R-SCANNER: Fast preprocessing for ROI — resize 2x, grayscale, contrast boost.
     *  Returns canvas ready for Tesseract.
     *  Pipeline: ROI → resize 2x → grayscale → contrast 1.4x → output.
     *  Skipped unsharp mask (O(n) convolution too slow for real-time scanning).
     *  Tesseract.js v5 has internal binarization — grayscale + contrast is sufficient. */
    async _preprocessRoi(dataUrl) {
        const img = await new Promise((resolve, reject) => {
            const i = new Image();
            i.onload = () => resolve(i);
            i.onerror = reject;
            i.src = dataUrl;
        });

        // Resize 2x — enough for Tesseract, 3x was too slow
        const scale = 2;
        const w = img.width * scale;
        const h = img.height * scale;
        const canvas = document.createElement('canvas');
        canvas.width = w;
        canvas.height = h;
        const ctx = canvas.getContext('2d');
        ctx.imageSmoothingEnabled = true;
        ctx.imageSmoothingQuality = 'high';
        ctx.drawImage(img, 0, 0, w, h);

        // Grayscale + contrast enhancement (single pass — fast)
        const imageData = ctx.getImageData(0, 0, w, h);
        const data = imageData.data;
        const contrast = 1.4;
        const intercept = 128 * (1 - contrast);
        for (let i = 0; i < data.length; i += 4) {
            let g = 0.299 * data[i] + 0.587 * data[i + 1] + 0.114 * data[i + 2];
            g = g * contrast + intercept;
            g = Math.max(0, Math.min(255, g));
            data[i] = data[i + 1] = data[i + 2] = g;
            data[i + 3] = 255;
        }
        ctx.putImageData(imageData, 0, 0);
        return canvas;
    },

    /** Legacy preprocessing — kept for recognizePlate fallback (single-frame OCR on full photo). */
    async _preprocessForOcr(dataUrl) {
        return this._preprocessRoi(dataUrl);
    },

    /** R-OCR-2 (2026-08-18): Otsu's method — DISABLED (broke OCR on real photos).
     *  Kept for reference — do NOT call from _preprocessForOcr. */
    _otsuThreshold(grayValues) {
        const histogram = new Array(256).fill(0);
        for (let i = 0; i < grayValues.length; i++) {
            histogram[grayValues[i]]++;
        }
        const total = grayValues.length;
        let sum = 0;
        for (let t = 0; t < 256; t++) sum += t * histogram[t];
        let sumB = 0, wB = 0, maxVariance = 0, threshold = 127;
        for (let t = 0; t < 256; t++) {
            wB += histogram[t];
            if (wB === 0) continue;
            const wF = total - wB;
            if (wF === 0) break;
            sumB += t * histogram[t];
            const mB = sumB / wB;
            const mF = (sum - sumB) / wF;
            const variance = wB * wF * (mB - mF) * (mB - mF);
            if (variance > maxVariance) {
                maxVariance = variance;
                threshold = t;
            }
        }
        return threshold;
    },

    /** R-SCANNER: VN plate normalizer — applies character confusion mapping based on position.
     *  OCR commonly confuses: O↔0, I↔1, S↔5, B↔8, Z↔2, G↔6
     *  In letter positions (before dash): 0→O, 1→I, 5→S, 8→B, 2→Z, 6→G
     *  In numeric positions (after dash): O→0, I→1, S→5, B→8, Z→2, G→6
     *  Returns normalized plate string, or '' if not plate-like. */
    _normalizeVnPlate(raw) {
        if (!raw) return '';
        // Clean: keep only [0-9A-ZĐ-.]
        let s = raw.replace(/[^0-9A-ZĐ\-\.]/g, '');
        // Collapse repeated dashes/dots, strip leading/trailing dashes
        s = s.replace(/-+/g, '-').replace(/\.+/g, '.').replace(/^-+|-+$/g, '');
        if (s.length < 5 || s.length > 14) return '';

        // Try to find/insert the dash separator
        let dashIdx = s.indexOf('-');
        if (dashIdx < 0) {
            // No dash — try to insert one. VN plate: 2 digits + 1-2 letters + numbers
            const match = s.match(/^(\d{2})([A-ZĐ]{1,2})(\d.*)$/);
            if (match) {
                s = match[1] + match[2] + '-' + match[3];
                dashIdx = s.indexOf('-');
            } else {
                // Can't parse structure — return cleaned string if plate-like
                return (s.length >= 5 && s.length <= 12) ? s : '';
            }
        }

        const parts = s.split('-');
        if (parts.length < 2) return '';

        // Left part: province (2 digits) + letters (1-2)
        let left = parts[0];
        let right = parts.slice(1).join('-');

        // Normalize left part: first 2 chars = province digits, rest = letters
        if (left.length >= 2) {
            const province = left.substring(0, 2);
            const letters = left.substring(2);
            // Province should be digits — convert letter confusions to digits
            const provinceDigits = province
                .replace(/O/g, '0').replace(/I/g, '1').replace(/S/g, '5')
                .replace(/B/g, '8').replace(/Z/g, '2').replace(/G/g, '6');
            // Letters should be letters — convert digit confusions to letters
            const letterChars = letters
                .replace(/0/g, 'O').replace(/1/g, 'I').replace(/5/g, 'S')
                .replace(/8/g, 'B').replace(/2/g, 'Z').replace(/6/g, 'G');
            left = provinceDigits + letterChars;
        }

        // Normalize right part: should be digits (and optional dot separator)
        // Convert letter confusions to digits
        right = right
            .replace(/O/g, '0').replace(/I/g, '1').replace(/S/g, '5')
            .replace(/B/g, '8').replace(/Z/g, '2').replace(/G/g, '6');

        s = left + '-' + right;

        // Validate against VN plate regex
        if (this._isVnPlateFormat(s)) return s;

        // If not valid format but still plate-like, return it (guard can override)
        if (s.length >= 5 && s.length <= 12) return s;
        return '';
    },

    /** Legacy plate normalizer — kept for backward compatibility with recognizePlate. */
    _normalizePlate(raw) {
        if (!raw) return '';
        let s = raw.replace(/[^0-9A-ZĐ\-]/g, '');
        s = s.replace(/-+/g, '-').replace(/^-+|-+$/g, '');
        // Apply VN normalization for better results
        return this._normalizeVnPlate(raw) || s;
    },

    async _ensureOcrLibrary() {
        if (window.Tesseract) return;
        return new Promise((resolve, reject) => {
            const script = document.createElement('script');
            script.src = '/js/lib/ocr/tesseract.min.js';
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
