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
            var canvas = document.getElementById(canvasId);
            if (!canvas) {
                console.error('Canvas not found:', canvasId);
                return false;
            }
            // Use vananQR (vendored qrcode-generator in /js/qrcode.js)
            // Exposes window.vananQR.generate(elementId, text, width, height)
            if (typeof window.vananQR === 'undefined') {
                await this._loadQrLibrary();
            }
            if (window.vananQR && window.vananQR.generate) {
                window.vananQR.generate(canvasId, text, size || 300, size || 300);
                return true;
            }
            console.error('vananQR.generate not available after loading library');
            return false;
        } catch (e) {
            console.error('QR generation failed:', e);
            return false;
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
