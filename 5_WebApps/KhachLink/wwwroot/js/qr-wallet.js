// #126 R2 Sprint 4: QR Wallet localStorage management + fullscreen QR generation.
// Manages claimed QR sessions in localStorage + generates QR on canvas for fullscreen display.

window.vananQrWallet = {
    _STORAGE_KEY: 'vanan_qr_wallet',

    /** Get all claimed QR sessions from localStorage. Returns JSON string or null. */
    async getSessions() {
        try {
            return localStorage.getItem(this._STORAGE_KEY);
        } catch (e) {
            console.error('QR wallet getSessions error:', e);
            return null;
        }
    },

    /** Save sessions JSON to localStorage. */
    async saveSessions(json) {
        try {
            localStorage.setItem(this._STORAGE_KEY, json);
        } catch (e) {
            console.error('QR wallet saveSessions error:', e);
        }
    },

    /** Add a new claimed session to localStorage. */
    async addSession(session) {
        try {
            var existing = localStorage.getItem(this._STORAGE_KEY);
            var list = existing ? JSON.parse(existing) : [];
            // Avoid duplicates
            list = list.filter(s => s.sessionId !== session.sessionId);
            list.push(session);
            localStorage.setItem(this._STORAGE_KEY, JSON.stringify(list));
        } catch (e) {
            console.error('QR wallet addSession error:', e);
        }
    },

    /** Generate QR code on a canvas element using vananQR (vendored qrcode-generator). */
    async generateQrOnCanvas(canvasId, text, size) {
        try {
            // Use vananQR (vendored qrcode-generator in /js/qrcode.js)
            if (typeof window.vananQR === 'undefined') {
                await this._loadQrLibrary();
            }
            if (!window.vananQR || !window.vananQR.generate) {
                console.error('vananQR.generate not available after loading library');
                return false;
            }
            // Retry up to 5 times — Blazor WASM may not have rendered the canvas yet
            var canvas = null;
            for (var i = 0; i < 5; i++) {
                canvas = document.getElementById(canvasId);
                if (canvas) break;
                await new Promise(function (r) { setTimeout(r, 100); });
            }
            if (!canvas) {
                console.error('Canvas not found after 5 retries:', canvasId);
                return false;
            }
            window.vananQR.generate(canvasId, text, size || 300, size || 300);
            return true;
        } catch (e) {
            console.error('QR generation failed:', e);
            return false;
        }
    },

    /** Generate QR code as data URL string (no DOM canvas needed — avoids Blazor render timing issues). */
    async generateQrDataUrl(text, size) {
        try {
            if (typeof window.vananQR === 'undefined') {
                await this._loadQrLibrary();
            }
            if (!window.vananQR || !window.vananQR.generateDataUrl) {
                console.error('vananQR.generateDataUrl not available');
                return null;
            }
            return window.vananQR.generateDataUrl(text, size || 300);
        } catch (e) {
            console.error('QR data URL generation failed:', e);
            return null;
        }
    },

    /** Set screen brightness to max (for fullscreen QR display). */
    async setBrightness(level) {
        try {
            // Use CSS filter on body as brightness proxy (Web Screen Brightness API not widely supported)
            document.body.style.filter = `brightness(${level})`;
        } catch (e) {
            console.warn('setBrightness failed:', e);
        }
    },

    /** Reset screen brightness to default. */
    async resetBrightness() {
        try {
            document.body.style.filter = '';
        } catch (e) {
            console.warn('resetBrightness failed:', e);
        }
    },

    async _loadQrLibrary() {
        return new Promise((resolve, reject) => {
            // Load vendored qrcode-generator (exposes window.vananQR.generate)
            var script = document.createElement('script');
            script.src = '/js/qrcode.js';
            script.onload = () => resolve();
            script.onerror = () => reject(new Error('Failed to load qrcode.js'));
            document.head.appendChild(script);
        });
    }
};
