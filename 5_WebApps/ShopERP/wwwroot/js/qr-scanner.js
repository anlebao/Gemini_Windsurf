// QR Scanner interop for RedemptionHistory.razor
// Uses html5-qrcode library (loaded dynamically from CDN)
// Supports: camera scanning + file upload fallback

let _html5QrCode = null;
let _dotNetRef = null;

window.vananQrScanner = {
    /** Load html5-qrcode library dynamically if not already loaded. */
    async _ensureLibrary() {
        if (window.Html5Qrcode) return;
        return new Promise((resolve, reject) => {
            const script = document.createElement('script');
            script.src = 'https://cdn.jsdelivr.net/npm/html5-qrcode@2.3.8/html5-qrcode.min.js';
            script.onload = () => resolve();
            script.onerror = () => reject(new Error('Failed to load html5-qrcode library'));
            document.head.appendChild(script);
        });
    },

    /** Start camera scanning. elementId = div container, dotNetRef = Blazor ref for callback. */
    async startScanner(elementId, dotNetRef) {
        try {
            await this._ensureLibrary();
            _dotNetRef = dotNetRef;

            if (_html5QrCode) {
                try { await _html5QrCode.stop(); } catch { }
                _html5QrCode.clear();
            }

            _html5QrCode = new Html5Qrcode(elementId);

            const config = {
                fps: 10,
                qrbox: { width: 250, height: 250 },
                aspectRatio: 1.0
            };

            await _html5QrCode.start(
                { facingMode: 'environment' },
                config,
                (decodedText) => {
                    // On successful scan — send to Blazor
                    if (_dotNetRef) {
                        _dotNetRef.invokeMethodAsync('OnQrScanned', decodedText);
                    }
                },
                (errorMessage) => {
                    // Per-frame decode failure — ignore (normal)
                }
            );

            return true;
        } catch (err) {
            console.error('QR scanner start failed:', err);
            if (_dotNetRef) {
                _dotNetRef.invokeMethodAsync('OnQrError', err.message || 'Camera access failed');
            }
            return false;
        }
    },

    /** Stop camera scanning. */
    async stopScanner() {
        if (_html5QrCode) {
            try {
                await _html5QrCode.stop();
                _html5QrCode.clear();
            } catch { }
            _html5QrCode = null;
        }
        _dotNetRef = null;
    },

    /** Scan from file upload (fallback when camera not available). */
    async scanFromFile(fileInputId, dotNetRef) {
        try {
            await this._ensureLibrary();
            const fileInput = document.getElementById(fileInputId);
            if (!fileInput || fileInput.files.length === 0) return;

            _dotNetRef = dotNetRef;
            _html5QrCode = _html5QrCode || new Html5Qrcode('qr-reader-file');

            const result = await _html5QrCode.scanFile(fileInput.files[0], false);
            if (_dotNetRef) {
                _dotNetRef.invokeMethodAsync('OnQrScanned', result);
            }
        } catch (err) {
            console.error('QR file scan failed:', err);
            if (_dotNetRef) {
                _dotNetRef.invokeMethodAsync('OnQrError', err.message || 'File scan failed');
            }
        }
    }
};
