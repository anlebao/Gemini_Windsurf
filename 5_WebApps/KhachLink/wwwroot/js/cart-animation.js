// Cart fly-to-cart animation
// Called from Blazor AddToCart — clones product image and animates to cart icon
window.vananFlyToCart = function (sourceElementId, cartIconSelector) {
    try {
        var sourceEl = document.getElementById(sourceElementId);
        if (!sourceEl) {
            // Fallback: find the closest product image from the button
            var btn = event && event.currentTarget;
            if (btn) {
                var card = btn.closest('.product-card') || btn.closest('.feature-card');
                if (card) {
                    var img = card.querySelector('img');
                    if (img) sourceEl = img;
                }
            }
        }
        if (!sourceEl) return false;

        var cartEl = document.querySelector(cartIconSelector || 'a[href="/cart"] .bi-cart3, a[aria-label="Giỏ hàng"] .bi-cart3, .header-icon-btn .bi-cart3');
        if (!cartEl) {
            // Fallback: find any cart icon
            cartEl = document.querySelector('.bi-cart3');
        }
        if (!cartEl) return false;

        var sourceRect = sourceEl.getBoundingClientRect();
        var cartRect = cartEl.getBoundingClientRect();

        // Create flying clone
        var clone = document.createElement('div');
        clone.style.cssText = [
            'position:fixed',
            'left:' + sourceRect.left + 'px',
            'top:' + sourceRect.top + 'px',
            'width:' + Math.min(sourceRect.width, 80) + 'px',
            'height:' + Math.min(sourceRect.height, 80) + 'px',
            'border-radius:50%',
            'background:linear-gradient(135deg,#8B4513,#D2691E)',
            'display:flex',
            'align-items:center',
            'justify-content:center',
            'color:white',
            'font-size:2rem',
            'z-index:9999',
            'pointer-events:none',
            'transition:all 0.8s cubic-bezier(0.5,-0.5,0.5,1.5)',
            'box-shadow:0 4px 15px rgba(0,0,0,0.3)',
            'opacity:1',
            'transform:scale(1) rotate(0deg)'
        ].join(';');

        clone.innerHTML = '<i class="bi bi-bag-fill"></i>';
        document.body.appendChild(clone);

        // Animate to cart
        var deltaX = cartRect.left + cartRect.width / 2 - sourceRect.left - 40;
        var deltaY = cartRect.top + cartRect.height / 2 - sourceRect.top - 40;

        requestAnimationFrame(function () {
            clone.style.transform = 'translate(' + deltaX + 'px,' + deltaY + 'px) scale(0.3) rotate(360deg)';
            clone.style.opacity = '0.3';
        });

        // Remove clone after animation
        setTimeout(function () {
            if (clone.parentNode) clone.parentNode.removeChild(clone);
            // Bounce cart icon
            cartEl.style.transition = 'transform 0.3s ease';
            cartEl.style.transform = 'scale(1.4)';
            setTimeout(function () {
                cartEl.style.transform = 'scale(1)';
            }, 300);
        }, 800);

        return true;
    } catch (e) {
        console.error('FlyToCart error:', e);
        return false;
    }
};

// CI trigger
