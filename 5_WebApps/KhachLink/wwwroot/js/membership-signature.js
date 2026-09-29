// Membership Infrastructure (2026-09-29): Chữ ký online vẽ tay (canvas capture).
// EVIDENCE — theo SRS §14 chữ ký vẽ tay KHÔNG tương đương chữ ký số pháp lý.
window.membershipSignature = {
    _ctx: null,
    _hasInk: false,

    init(canvasId) {
        const canvas = document.getElementById(canvasId);
        if (!canvas) return false;
        const ctx = canvas.getContext('2d');
        this._ctx = ctx;

        // Retina-friendly: scale canvas to device pixels
        const dpr = window.devicePixelRatio || 1;
        canvas.width = canvas.offsetWidth * dpr;
        canvas.height = canvas.offsetHeight * dpr;
        ctx.scale(dpr, dpr);

        ctx.strokeStyle = '#1a1a1a';
        ctx.lineWidth = 2;
        ctx.lineCap = 'round';
        ctx.lineJoin = 'round';

        let drawing = false;
        const pos = e => {
            const r = canvas.getBoundingClientRect();
            return { x: (e.clientX - r.left) * (canvas.offsetWidth / r.width), y: (e.clientY - r.top) * (canvas.offsetHeight / r.height) };
        };

        canvas.addEventListener('pointerdown', e => {
            drawing = true;
            this._hasInk = true;
            const p = pos(e);
            ctx.beginPath();
            ctx.moveTo(p.x, p.y);
        });
        canvas.addEventListener('pointermove', e => {
            if (!drawing) return;
            const p = pos(e);
            ctx.lineTo(p.x, p.y);
            ctx.stroke();
        });
        ['pointerup', 'pointerleave'].forEach(ev => canvas.addEventListener(ev, () => { drawing = false; }));
        return true;
    },

    hasInk() { return this._hasInk; },

    toDataUrl() {
        return this._ctx ? this._ctx.canvas.toDataURL('image/png') : '';
    },

    clear() {
        if (this._ctx) {
            this._ctx.clearRect(0, 0, this._ctx.canvas.width, this._ctx.canvas.height);
            this._hasInk = false;
        }
    }
};
