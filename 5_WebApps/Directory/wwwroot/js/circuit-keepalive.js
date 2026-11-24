// Phase 2 Scaling pattern (copy từ ShopERP wwwroot/js/circuit-keepalive.js):
// Circuit keepalive — gọi .NET method mỗi 10s để giữ circuit sống qua proxy idle timeout.
// Chỉ chạy khi Blazor circuit active (DotNet.invokeMethodAsync available).
// Fixes: circuit bị proxy/host idle kill khi user đọc directory không tương tác lâu
// → "Could not reconnect to the server" (Blazor Server reconnect modal).
window.vananCircuitKeepalive = (function () {
    var intervalId = null;

    function start() {
        if (intervalId) return;
        intervalId = setInterval(function () {
            try {
                if (window.DotNet && DotNet.invokeMethodAsync) {
                    DotNet.invokeMethodAsync('VanAn.Directory', 'CircuitKeepalivePing')
                        .catch(function () {
                            // Circuit disposed hoặc đang reconnect — không log, không throw
                        });
                }
            } catch (e) {
                // Blazor chưa load — bỏ qua
            }
        }, 10000); // 10s — nhỏ hơn proxy idle timeout điển hình (60s+)
    }

    function stop() {
        if (intervalId) { clearInterval(intervalId); intervalId = null; }
    }

    // Auto-start khi script load
    document.addEventListener('DOMContentLoaded', start);
    return { start: start, stop: stop };
})();
