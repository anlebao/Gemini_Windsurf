// Phase 2 Scaling: Circuit keepalive — gọi .NET method mỗi 10s để giữ circuit sống qua proxy idle timeout.
// Tương tự guard-camera.js 15s ping (Program.cs L78-79 comment).
// Chỉ chạy khi Blazor circuit active (DotNet.invokeMethodAsync available).
// Fixes: circuit bị proxy idle kill khi user không tương tác lâu (vd. nhân viên POS đứng xem danh sách đơn 5 phút).
window.vananCircuitKeepalive = (function () {
    var intervalId = null;

    function start() {
        if (intervalId) return;
        intervalId = setInterval(function () {
            try {
                if (window.DotNet && DotNet.invokeMethodAsync) {
                    DotNet.invokeMethodAsync('VanAn.ShopERP', 'CircuitKeepalivePing')
                        .catch(function () {
                            // Circuit disposed hoặc đang reconnect — không log, không throw
                        });
                }
            } catch (e) {
                // Blazor chưa load — bỏ qua
            }
        }, 10000); // 10s — nhỏ hơn KeepAliveInterval 15s để double-safe
    }

    function stop() {
        if (intervalId) { clearInterval(intervalId); intervalId = null; }
    }

    // Auto-start khi script load
    document.addEventListener('DOMContentLoaded', start);
    return { start: start, stop: stop };
})();
