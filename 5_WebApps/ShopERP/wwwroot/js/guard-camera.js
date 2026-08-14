// #126: Guard Scanner camera interop for photo capture (plate + customer).
// QR scanning reuses existing vananQrScanner (html5-qrcode) in qr-scanner.js.
// This file adds: camera preview + photo capture (getUserMedia + canvas).

let _cameraStream = null;
let _cameraVideo = null;

window.vananGuardCamera = {
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

    /** Upload a base64 JPEG to a presigned PUT URL (R2). Returns true on success. */
    async uploadToPresignedUrl(dataUrl, presignedUrl) {
        try {
            // Convert base64 data URL to Blob
            const response = await fetch(dataUrl);
            const blob = await response.blob();
            const uploadResp = await fetch(presignedUrl, {
                method: 'PUT',
                headers: { 'Content-Type': 'image/jpeg' },
                body: blob
            });
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
    // After capturing the plate photo, run OCR to prefill the plate textbox.
    // Returns recognized plate string (cleaned) or '' on failure. User must confirm/edit.

    /** Recognize license plate text from a base64 JPEG data URL. Returns cleaned plate string or ''. */
    async recognizePlate(dataUrl) {
        try {
            if (!dataUrl) return '';
            await this._ensureOcrLibrary();
            const canvas = await this._preprocessForOcr(dataUrl);
            // PSM 7 = single line of text (a plate is one line).
            // Whitelist: VN plates use digits + uppercase letters (no I/O/Q which are visually ambiguous).
            const worker = await Tesseract.createWorker('eng', 1, { logger: () => {} });
            await worker.setParameters({
                tessedit_char_whitelist: '0123456789ABCDEFGHKLMNPRSTUVXYZ-',
                tessedit_pageseg_mode: '7'
            });
            const { data } = await worker.recognize(canvas);
            await worker.terminate();
            const raw = (data.text || '').toUpperCase();
            return this._normalizePlate(raw);
        } catch (err) {
            console.error('Plate OCR failed:', err);
            return '';
        }
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
