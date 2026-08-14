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
    }
};
