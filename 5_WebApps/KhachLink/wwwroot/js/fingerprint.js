// fingerprint.js — Community Commerce Sprint 0 v1.2
// JS interop wrapper for FingerprintJS v4 (MIT, self-hosted).
// Collects browser signals, hashes them, returns fingerprint result to Blazor.
//
// Usage from Blazor WASM:
//   const result = await window.fingerprint.collect();
//   result = { hash: "sha256hex", signals: {...}, userAgent: "...", platform: "..." }
//
// v1.2: FingerprintJS v4 MIT library is vendored at /lib/fingerprintjs/fingerprint.js
// This wrapper loads it lazily and exposes a stable API for the .NET side.

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
