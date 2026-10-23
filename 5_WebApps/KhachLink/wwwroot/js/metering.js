// Phí nền tảng P4 (hook a): store.visit public ingest.
// Sends ONE visit event per page load to the Gateway public endpoint via
// navigator.sendBeacon (falls back to fetch keepalive) — never blocks rendering.
// Idempotency: visit:{tenantId}:{deviceId|anon}:{yyyyMMddHH UTC} — same device
// refreshing within the same hour does not double-count (deduped server-side).
// Any failure is swallowed — metering must never break the storefront.
window.vananMetering = {
    recordVisit: function (tenantId) {
        try {
            var device = null;
            try { device = localStorage.getItem('customer_device_id'); } catch (e) { /* ignore */ }
            var d = new Date();
            var pad = function (n) { return String(n).padStart(2, '0'); };
            var stamp = '' + d.getUTCFullYear() + pad(d.getUTCMonth() + 1) + pad(d.getUTCDate()) + pad(d.getUTCHours());
            var body = JSON.stringify({
                tenantId: tenantId,
                serviceCode: 'store.visit',
                quantity: 1,
                unit: 'visit',
                occurredAtUtc: d.toISOString(),
                idempotencyKey: 'visit:' + tenantId + ':' + (device || 'anon') + ':' + stamp
            });
            var url = '/api/public/metering/event';
            var blob = new Blob([body], { type: 'application/json' });
            if (navigator.sendBeacon && navigator.sendBeacon(url, blob)) return;
            fetch(url, {
                method: 'POST',
                body: body,
                headers: { 'Content-Type': 'application/json' },
                keepalive: true
            }).catch(function () { /* metering must never break the page */ });
        } catch (e) { /* metering must never break the page */ }
    }
};
