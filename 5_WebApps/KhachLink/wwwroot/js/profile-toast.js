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
