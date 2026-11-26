// VanAn Toast — shared lightweight toast notification system.
// Single source of truth: vananShowToast(message, variant) handles ALL toast styles.
// Convenience wrappers (vananShowProfileToast, vananShowCartToast) delegate to it
// so fixes/improvements only need to happen in ONE place.

window.vananShowToast = function(message, variant) {
    variant = variant || 'info'; // 'info' | 'success' | 'warning'

    var styles = {
        info:    { bg: '#1a1a2e',             icon: 'bi-info-circle-fill' },
        success: { bg: 'linear-gradient(135deg,#2c5f2d,#4a8c4a)', icon: 'bi-check-circle-fill' },
        warning: { bg: '#8B4513',             icon: 'bi-exclamation-triangle-fill' }
    };
    var s = styles[variant] || styles.info;

    // Reuse existing toast container if present (avoids stacking)
    var existing = document.getElementById('vanan-toast');
    if (existing) existing.remove();

    var toast = document.createElement('div');
    toast.id = 'vanan-toast';
    toast.style.cssText = [
        'position:fixed',
        'top:80px',
        'left:50%',
        'transform:translateX(-50%)',
        'background:' + s.bg,
        'color:#fff',
        'padding:12px 20px',
        'border-radius:8px',
        'box-shadow:0 4px 12px rgba(0,0,0,0.2)',
        'z-index:9999',
        'font-size:0.875rem',
        'max-width:90vw',
        'text-align:center',
        'display:flex',
        'align-items:center',
        'gap:0.5rem',
        'animation:vananToastSlide 0.3s ease-out'
    ].join(';');
    toast.innerHTML = '<i class="bi ' + s.icon + '" style="font-size:1.2rem"></i> ' + message;
    document.body.appendChild(toast);

    // Auto-dismiss (info=4s, success=2s, warning=4s)
    var duration = variant === 'success' ? 2000 : 4000;
    setTimeout(function() {
        if (toast.parentNode) toast.parentNode.removeChild(toast);
    }, duration);
};

// Convenience wrappers — delegate to vananShowToast so all fixes happen in ONE place.
window.vananShowProfileToast = function(message) {
    window.vananShowToast(message, 'info');
};

window.vananShowCartToast = function(message) {
    window.vananShowToast(message, 'success');
};

// 2026-11-26: pháo hoa tại floating cart + lời cảm ơn khi "Thêm vào giỏ".
// Nhẹ, pure DOM/CSS — không cần canvas lib. Burst neo vào .cart-drawer-card
// (floating cart của layout); fallback góc phải trên nếu drawer chưa render.
window.vananCartCelebrate = function() {
    try {
        var target = document.querySelector('.cart-drawer-card');
        var x, y;
        if (target) {
            var r = target.getBoundingClientRect();
            x = r.left + Math.min(r.width, 60) / 2;
            y = r.top + 24;
        } else {
            x = window.innerWidth - 60;
            y = 110;
        }

        var colors = ['#ffd700', '#ff6b6b', '#4ecdc4', '#a29bfe', '#fd79a8', '#55efc4', '#74b9ff'];
        var n = 16;
        for (var i = 0; i < n; i++) {
            var p = document.createElement('span');
            var ang = (Math.PI * 2 * i / n) + (Math.random() * 0.4);
            var dist = 34 + Math.random() * 46;
            p.className = 'vanan-confetti';
            p.style.cssText = [
                'position:fixed',
                'left:' + x + 'px',
                'top:' + y + 'px',
                'width:8px',
                'height:8px',
                'border-radius:' + (Math.random() > 0.5 ? '50%' : '2px'),
                'background:' + colors[i % colors.length],
                'z-index:10000',
                'pointer-events:none',
                '--dx:' + (Math.cos(ang) * dist).toFixed(1) + 'px',
                '--dy:' + (Math.sin(ang) * dist).toFixed(1) + 'px',
                'animation:vananConfettiFly 0.75s ease-out forwards'
            ].join(';');
            document.body.appendChild(p);
            (function(el){ setTimeout(function(){ if (el.parentNode) el.parentNode.removeChild(el); }, 800); })(p);
        }

        // Lời cảm ơn nổi lên từ tâm burst
        var ty = document.createElement('div');
        ty.textContent = 'Cảm ơn bạn! 🎉';
        ty.style.cssText = [
            'position:fixed',
            'left:' + (x - 55) + 'px',
            'top:' + (y - 14) + 'px',
            'background:linear-gradient(135deg,#2c5f2d,#4a8c4a)',
            'color:#fff',
            'padding:6px 14px',
            'border-radius:20px',
            'font-size:0.85rem',
            'font-weight:600',
            'box-shadow:0 4px 12px rgba(0,0,0,0.25)',
            'z-index:10000',
            'pointer-events:none',
            'animation:vananThanksFloat 1.4s ease-out forwards',
            'white-space:nowrap'
        ].join(';');
        document.body.appendChild(ty);
        setTimeout(function(){ if (ty.parentNode) ty.parentNode.removeChild(ty); }, 1400);

        // Pulse nhẹ floating cart
        if (target) {
            target.style.animation = 'vananCartPulse 0.5s ease';
            setTimeout(function(){ target.style.animation = ''; }, 550);
        }
    } catch (e) { /* best-effort — đừng để hiệu ứng phá flow mua hàng */ }
};

// Keyframes cho confetti + thanks (idempotent)
(function() {
    if (document.getElementById('vanan-celebrate-keyframes')) return;
    var style = document.createElement('style');
    style.id = 'vanan-celebrate-keyframes';
    style.textContent =
        '@keyframes vananConfettiFly {' +
        '  0%   { transform: translate(0,0) scale(1); opacity: 1; }' +
        '  100% { transform: translate(var(--dx),var(--dy)) scale(0.2) rotate(180deg); opacity: 0; }' +
        '}' +
        '@keyframes vananThanksFloat {' +
        '  0%   { transform: translateY(0);      opacity: 0; }' +
        '  15%  { opacity: 1; }' +
        '  70%  { transform: translateY(-38px); opacity: 1; }' +
        '  100% { transform: translateY(-52px); opacity: 0; }' +
        '}' +
        '@keyframes vananCartPulse {' +
        '  0%,100% { transform: scale(1); }' +
        '  40%     { transform: scale(1.06); }' +
        '}';
    document.head.appendChild(style);
})();

// Inject keyframes once (idempotent — checks if already added)
(function() {
    if (document.getElementById('vanan-toast-keyframes')) return;
    var style = document.createElement('style');
    style.id = 'vanan-toast-keyframes';
    style.textContent = '@keyframes vananToastSlide {' +
        'from { transform: translateX(-50%) translateY(-20px); opacity: 0; }' +
        'to   { transform: translateX(-50%) translateY(0);      opacity: 1; }' +
    '}';
    document.head.appendChild(style);
})();
