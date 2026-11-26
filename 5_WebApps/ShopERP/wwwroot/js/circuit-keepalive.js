// Phase 2 Scaling: Circuit keepalive — gọi .NET method mỗi 10s để giữ circuit sống qua proxy idle timeout.
// Tương tự guard-camera.js 15s ping (Program.cs L78-79 comment).
// Chỉ chạy khi Blazor circuit active (DotNet.invokeMethodAsync available).
// Fixes: circuit bị proxy idle kill khi user không tương tác lâu (vd. nhân viên POS đứng xem danh sách đơn 5 phút).
//
// 2026-11-26 (issue #200, áp dụng cho cả Directory): hardening — KHÔNG ping khi circuit
// chưa connect hoặc đang reconnect/failed/rejected. Trước đây ping mù mỗi 10s →
// "Circuit not initialized" + "Cannot send data..." spam console.
// Backoff: fail → giãn nhịp lên 60s; success → về 10s (tự resume sau reconnect).
window.vananCircuitKeepalive = (function () {
    var timerId = null;
    var failStreak = 0;
    var INTERVAL_OK_MS = 10000;
    var INTERVAL_FAIL_MS = 60000;

    // Circuit chết hẳn (rejected/failed → user phải reload) → ngừng ping hoàn toàn.
    function isCircuitDead() {
        var m = document.getElementById('components-reconnect-modal');
        return !!(m && (m.classList.contains('components-reconnect-rejected') ||
                        m.classList.contains('components-reconnect-failed')));
    }

    function schedule() {
        var delay = failStreak > 0 ? INTERVAL_FAIL_MS : INTERVAL_OK_MS;
        timerId = setTimeout(tick, delay);
    }

    function tick() {
        timerId = null;
        if (isCircuitDead()) { schedule(); return; }
        try {
            if (window.DotNet && DotNet.invokeMethodAsync) {
                DotNet.invokeMethodAsync('VanAn.ShopERP', 'CircuitKeepalivePing')
                    .then(function () { failStreak = 0; })
                    .catch(function () {
                        // Circuit disposed hoặc đang reconnect — backoff, không log, không throw
                        failStreak++;
                    });
            }
        } catch (e) {
            // Blazor chưa load — bỏ qua
            failStreak++;
        }
        schedule();
    }

    function start() {
        if (timerId !== null) return;
        schedule();
    }

    function stop() {
        if (timerId !== null) { clearTimeout(timerId); timerId = null; }
    }

    // Auto-start khi script load
    document.addEventListener('DOMContentLoaded', start);
    return { start: start, stop: stop };
})();
