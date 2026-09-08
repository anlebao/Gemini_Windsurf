// P2.3: ProfileGuard toast — hiển thị thông báo 1 lần khi route guard redirect.
// Lightweight: không phụ thuộc toast service, dùng div tạm + auto-dismiss.
window.vananShowProfileToast = function(message) {
    // Reuse existing toast container if present
    var existing = document.getElementById('vanan-profile-toast');
    if (existing) {
        existing.remove();
    }
    var toast = document.createElement('div');
    toast.id = 'vanan-profile-toast';
    toast.style.cssText = [
        'position:fixed',
        'top:80px',
        'left:50%',
        'transform:translateX(-50%)',
        'background:#1a1a2e',
        'color:#fff',
        'padding:12px 20px',
        'border-radius:8px',
        'box-shadow:0 4px 12px rgba(0,0,0,0.2)',
        'z-index:9999',
        'font-size:0.875rem',
        'max-width:90vw',
        'text-align:center'
    ].join(';');
    toast.textContent = message;
    document.body.appendChild(toast);
    setTimeout(function() {
        if (toast.parentNode) toast.parentNode.removeChild(toast);
    }, 4000);
};
