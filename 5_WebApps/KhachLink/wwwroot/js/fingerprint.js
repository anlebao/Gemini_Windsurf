// fingerprint.js — Community Commerce Sprint 0 v1.2 (F1 fix 2026-07-26)
// JS interop wrapper for FingerprintJS v5.2.0 (MIT, self-hosted).
// Collects browser signals, hashes them, returns fingerprint result to Blazor.
//
// Usage from Blazor WASM:
//   const result = await window.fingerprint.collect();
//   result = { hash: "sha256hex", signals: {...}, userAgent: "...", platform: "..." }
//
// F1 fix: Replaced Sprint 0 stub with real FingerprintJS v5.2.0 library.
// Note: Task card specified "v4 (MIT)" but FingerprintJS v4 is actually BUSL-1.1
// (Business Source License, restricts production use). v5+ is properly MIT licensed.
// API is compatible: FingerprintJS.load() + agent.get() → { visitorId, components }.
// Vendored at /lib/fingerprintjs/fingerprint.js (UMD build, ~37KB minified).

window.fingerprint = (function () {
    let _agent = null;

    async function loadAgent() {
        if (_agent) return _agent;
        // Load FingerprintJS v4 from vendored path (no CDN — zero external dependency)
        if (typeof FingerprintJS === 'undefined') {
            await import('/lib/fingerprintjs/fingerprint.js');
        }
        _agent = FingerprintJS.load({ monitoring: false });
        return _agent;
    }

    async function collect() {
        try {
            const agent = await loadAgent();
            const result = await agent.get();
            const signals = result.components || {};
            return {
                hash: result.visitorId || '',
                signals: JSON.stringify(signals),
                userAgent: navigator.userAgent || '',
                platform: navigator.platform || ''
            };
        } catch (err) {
            console.error('[fingerprint] collect failed:', err);
            return {
                hash: '',
                signals: '{}',
                userAgent: navigator.userAgent || '',
                platform: navigator.platform || '',
                error: String(err)
            };
        }
    }

    return { collect };
})();
