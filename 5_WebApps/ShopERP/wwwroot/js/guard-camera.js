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
let _scanLoopId = null;
let _scanStartTime = 0;
let _voteBuffer = []; // [{plate, confidence, timestamp}]
const _voteConfig = {
    maxBufferSize: 10,
    minVotes: 3,           // Need at least 3 matching results to accept
    minAvgConfidence: 70,  // With avg confidence >= 70
    timeoutMs: 15000,      // Show fallback hint after 15s with no stable result
    frameIntervalMs: 100   // Delay between frames (Tesseract takes ~500ms-2s per frame)
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
                    // R-OCR-3 (2026-08-18): 1920x1080 — higher resolution for plate OCR.
                    // 1280x720 left plate region ~200-300px → too small for Tesseract.
                    width: { ideal: 1920 },
                    height: { ideal: 1080 }
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
            // Hide manual entry button
            const manualBtn = document.getElementById('plateManualBtn');
            if (manualBtn) manualBtn.style.display = 'none';
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
        // Clear scan status + manual entry button.
        const scanStatus = document.getElementById('plateScanStatus');
        if (scanStatus) { scanStatus.textContent = ''; scanStatus.style.display = 'none'; }
        const manualBtn = document.getElementById('plateManualBtn');
        if (manualBtn) manualBtn.style.display = 'none';
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
        this._runScanLoop();
        return true;
    },

    /** R-SCANNER: Stop live scanning — stops camera, clears guide box, stops loop. */
    stopLiveScan() {
        _scanLoopActive = false;
        if (_scanLoopId) {
            clearTimeout(_scanLoopId);
            _scanLoopId = null;
        }
        _voteBuffer = [];
        this._showGuideBox(false);
        this.stopCamera();
        _cameraActive['plate'] = false;
        this._updateCameraUI('plate', false);
    },

    /** R-SCANNER: Main scan loop — captures frame, crops ROI, OCRs, votes. Sequential (not FPS-based)
     *  because Tesseract recognize takes ~500ms-2s per frame. Loop continues until auto-accept or stop. */
    async _runScanLoop() {
        if (!_scanLoopActive) return;

        // Check timeout — show hint after 15s with no stable result
        const elapsed = Date.now() - _scanStartTime;
        if (elapsed > _voteConfig.timeoutMs && _voteBuffer.length < 2) {
            this._updateScanStatus(
                '⚠️ Chưa nhìn rõ biển số.\n• Đưa camera gần hơn\n• Giữ điện thoại ổn định\n• Đảm bảo biển số đủ sáng',
                'warning'
            );
            // Show manual entry button
            const manualBtn = document.getElementById('plateManualBtn');
            if (manualBtn) manualBtn.style.display = '';
        }

        try {
            const video = document.getElementById('plateVideo');
            if (!video || !video.videoWidth) {
                _scanLoopId = setTimeout(() => this._runScanLoop(), _voteConfig.frameIntervalMs);
                return;
            }

            // Crop ROI from guide box region
            const roiDataUrl = this._cropRoiFromVideo(video);
            if (!roiDataUrl) {
                _scanLoopId = setTimeout(() => this._runScanLoop(), _voteConfig.frameIntervalMs);
                return;
            }

            // Preprocess + OCR on ROI only
            const ocrResult = await this._ocrRoi(roiDataUrl);

            if (ocrResult && ocrResult.plate) {
                // Add to temporal vote buffer
                _voteBuffer.push({
                    plate: ocrResult.plate,
                    confidence: ocrResult.confidence,
                    timestamp: Date.now()
                });
                if (_voteBuffer.length > _voteConfig.maxBufferSize) {
                    _voteBuffer.shift();
                }

                // Update status with latest result
                this._updateScanStatus(
                    `Đang quét... ${ocrResult.plate} (${Math.round(ocrResult.confidence)}%)`,
                    'scanning'
                );

                // Check temporal voting — auto-accept if stable
                const voteResult = this._checkTemporalVote();
                if (voteResult) {
                    this._onPlateAccepted(voteResult.plate, voteResult.confidence, video);
                    return; // Stop loop
                }
            }
        } catch (err) {
            console.error('[Scanner] Frame error:', err);
        }

        // Continue loop — sequential, wait for next frame
        _scanLoopId = setTimeout(() => this._runScanLoop(), _voteConfig.frameIntervalMs);
    },

    /** R-SCANNER: Crop ROI from video based on guide box position.
     *  Guide box is a CSS overlay on the video — map its display coords to video resolution.
     *  Returns cropped dataUrl or null. */
    _cropRoiFromVideo(video) {
        const guideBox = document.getElementById('plateGuideBox');
        if (!guideBox || !video.videoWidth) return null;

        const videoRect = video.getBoundingClientRect();
        const boxRect = guideBox.getBoundingClientRect();

        // Calculate relative position (0-1) of guide box within video
        const relX = (boxRect.left - videoRect.left) / videoRect.width;
        const relY = (boxRect.top - videoRect.top) / videoRect.height;
        const relW = boxRect.width / videoRect.width;
        const relH = boxRect.height / videoRect.height;

        // Map to actual video resolution
        const sx = Math.max(0, Math.round(relX * video.videoWidth));
        const sy = Math.max(0, Math.round(relY * video.videoHeight));
        const sw = Math.min(video.videoWidth - sx, Math.round(relW * video.videoWidth));
        const sh = Math.min(video.videoHeight - sy, Math.round(relH * video.videoHeight));

        if (sw < 20 || sh < 10) return null;

        const canvas = document.createElement('canvas');
        canvas.width = sw;
        canvas.height = sh;
        const ctx = canvas.getContext('2d');
        ctx.drawImage(video, sx, sy, sw, sh, 0, 0, sw, sh);
        return canvas.toDataURL('image/jpeg', 0.95);
    },

    /** R-SCANNER: OCR on ROI — preprocess + Tesseract + VN normalize + validate.
     *  Returns {plate, confidence} or null. */
    async _ocrRoi(dataUrl) {
        const worker = await this.preloadOcrWorker();
        const canvas = await this._preprocessRoi(dataUrl);

        // PSM 7 (single line) — best for license plates
        await worker.setParameters({ tessedit_pageseg_mode: '7' });
        const { data } = await worker.recognize(canvas);
        const raw = (data.text || '').toUpperCase();
        const confidence = data.confidence || 0;

        // VN plate normalization + validation
        const normalized = this._normalizeVnPlate(raw);
        if (!normalized) return null;

        return { plate: normalized, confidence };
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

    /** R-SCANNER: Called when plate is auto-accepted — fill input, capture photo, stop scan, show success. */
    _onPlateAccepted(plate, confidence, video) {
        // Fill plate input
        this.setInputValue('plateInput', plate);
        this.saveState('plateNumber', plate);

        // Capture current frame as plate photo (full frame, not just ROI)
        const photoUrl = this.capturePhoto('plateVideo');
        if (photoUrl) {
            _capturedPhotos['plate'] = photoUrl;
            this.saveState('platePhoto', photoUrl);
            this._renderPreview('plate', photoUrl);
        }

        // Stop scanning
        _scanLoopActive = false;
        if (_scanLoopId) {
            clearTimeout(_scanLoopId);
            _scanLoopId = null;
        }
        this._showGuideBox(false);
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

    /** R-SCANNER: Show/hide guide box overlay on video. */
    _showGuideBox(show) {
        const guideBox = document.getElementById('plateGuideBox');
        const scanStatus = document.getElementById('plateScanStatus');
        if (guideBox) guideBox.style.display = show ? '' : 'none';
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

    /** Preload Tesseract worker on page load — eliminates ~3s delay on first capture. */
    async preloadOcrWorker() {
        if (this._ocrWorkerPromise) return this._ocrWorkerPromise;
        this._ocrWorkerPromise = (async () => {
            try {
                await this._ensureOcrLibrary();
                const whitelist = '0123456789ABCDEFGHKLMNPRSTUVXYZĐ-';
                const worker = await Tesseract.createWorker('eng', 1, {
                    workerPath: '/js/lib/ocr/worker.min.js',
                    corePath: '/js/lib/ocr',
                    langPath: '/js/lib/ocr',
                    logger: () => {}
                });
                await worker.setParameters({ tessedit_char_whitelist: whitelist });
                console.log('[OCR] Tesseract worker preloaded successfully');
                return worker;
            } catch (err) {
                console.error('[OCR] Failed to preload Tesseract worker:', err);
                this._ocrWorkerPromise = null; // Reset so retry is possible
                throw err;
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

    /** R-SCANNER: Enhanced preprocessing for ROI — resize 3x, grayscale, contrast boost, sharpen.
     *  Returns canvas ready for Tesseract.
     *  Pipeline: ROI → resize 3x → grayscale → contrast 1.5x → unsharp mask → output.
     *  Tesseract.js v5 has internal binarization, so we don't apply threshold here —
     *  grayscale + contrast + sharpen gives Tesseract the best input for its own thresholding. */
    async _preprocessRoi(dataUrl) {
        const img = await new Promise((resolve, reject) => {
            const i = new Image();
            i.onload = () => resolve(i);
            i.onerror = reject;
            i.src = dataUrl;
        });

        // Resize 3x for better OCR resolution (plates are small in ROI)
        const scale = 3;
        const w = img.width * scale;
        const h = img.height * scale;
        const canvas = document.createElement('canvas');
        canvas.width = w;
        canvas.height = h;
        const ctx = canvas.getContext('2d');
        ctx.imageSmoothingEnabled = true;
        ctx.imageSmoothingQuality = 'high';
        ctx.drawImage(img, 0, 0, w, h);

        // Get image data for pixel manipulation
        const imageData = ctx.getImageData(0, 0, w, h);
        const data = imageData.data;

        // Step 1: Grayscale + contrast enhancement
        const contrast = 1.5;
        const intercept = 128 * (1 - contrast);
        const gray = new Uint8ClampedArray(w * h);
        for (let i = 0; i < data.length; i += 4) {
            let g = 0.299 * data[i] + 0.587 * data[i + 1] + 0.114 * data[i + 2];
            g = g * contrast + intercept;
            g = Math.max(0, Math.min(255, g));
            gray[i / 4] = g;
        }

        // Step 2: Unsharp mask (sharpen) — convolution with sharpen kernel
        // Kernel: [0,-1,0, -1,5,-1, 0,-1,0]
        const sharpened = new Uint8ClampedArray(w * h);
        for (let y = 0; y < h; y++) {
            for (let x = 0; x < w; x++) {
                const idx = y * w + x;
                if (x === 0 || x === w - 1 || y === 0 || y === h - 1) {
                    sharpened[idx] = gray[idx];
                    continue;
                }
                const center = gray[idx] * 5;
                const top = gray[(y - 1) * w + x] * -1;
                const bottom = gray[(y + 1) * w + x] * -1;
                const left = gray[y * w + (x - 1)] * -1;
                const right = gray[y * w + (x + 1)] * -1;
                let val = center + top + bottom + left + right;
                sharpened[idx] = Math.max(0, Math.min(255, val));
            }
        }

        // Write back to imageData (RGB = sharpened gray, Alpha = 255)
        for (let i = 0; i < data.length; i += 4) {
            data[i] = data[i + 1] = data[i + 2] = sharpened[i / 4];
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
