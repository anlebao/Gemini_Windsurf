// Sprint 2 P3.1: Onboarding tour via driver.js — 3-step tooltip tour cho Directory → Reseller/FullCommerce.
// driver.js vendored in wwwroot/lib/driver.js/ (loaded via index.html script tag).
// Init from OnboardingTour.razor OnAfterRenderAsync(firstRender) — sau khi nav rendered.
//
// driver.js v1.3.1 IIFE build: window.driver.js = { driver: function(config) {...} }
// Usage: const driver = window.driver.js.driver({ steps: [...] }); driver.drive();

window.vananStartOnboardingTour = function(direction) {
    // direction: "DirectoryToFullCommerce" | "DirectoryToReseller" | other
    if (!window.driver || !window.driver.js || typeof window.driver.js.driver !== 'function') {
        console.warn('[VanAn Onboarding] driver.js not loaded — skipping tour');
        return;
    }

    // Detect mobile vs desktop — target correct stores element (avoid duplicate IDs)
    var isMobile = window.matchMedia('(max-width: 767px)').matches;
    var storesSelector = isMobile ? '#nav-stores-mobile' : '#nav-stores';

    var steps = [
        {
            element: '#nav-cart',
            popover: {
                title: '🛒 Giỏ hàng',
                description: 'Mua sản phẩm trực tiếp trên app — thêm vào giỏ và đặt hàng.'
            }
        },
        {
            element: '#nav-rewards',
            popover: {
                title: '🎁 Tích điểm',
                description: 'Tích điểm mỗi đơn hàng, đổi quà hấp dẫn.'
            }
        },
        {
            element: storesSelector,
            popover: {
                title: '📍 Cửa hàng',
                description: 'Tìm cửa hàng gần bạn.'
            }
        }
    ];

    // Filter steps: chỉ show step nếu element tồn tại trong DOM (profile có thể ẩn 1 số nav items)
    var visibleSteps = steps.filter(function(s) {
        return document.querySelector(s.element) !== null;
    });

    if (visibleSteps.length === 0) {
        console.warn('[VanAn Onboarding] No target nav elements found — skipping tour');
        return;
    }

    var driver = window.driver.js.driver({
        showProgress: true,
        allowClose: true,
        steps: visibleSteps,
        doneBtnText: 'Hoàn thành',
        nextBtnText: 'Tiếp →',
        prevBtnText: '← Quay lại',
        progressText: '{{current}} / {{total}}'
    });

    driver.drive();
};
