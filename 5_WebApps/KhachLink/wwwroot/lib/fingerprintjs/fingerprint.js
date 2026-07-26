// fingerprint.js — FingerprintJS v4 (MIT License) vendored stub.
//
// This is a PLACEHOLDER for the actual FingerprintJS v4 library.
// Sprint 0 v1.2: The real library file (~50KB minified) must be downloaded from
// https://github.com/fingerprintjs/fingerprintjs/releases (MIT license) and
// placed at this exact path: 5_WebApps/KhachLink/wwwroot/lib/fingerprintjs/fingerprint.js
//
// Until the real library is vendored, this stub exposes a minimal FingerprintJS
// global that returns a deterministic visitorId derived from basic browser signals.
// This allows Sprint 0 unit tests + Sprint 1-4 development to proceed without
// the full library. Replace this file before production deployment.
//
// License: MIT (FingerprintJS v4 open-source edition)
// Source: https://github.com/fingerprintjs/fingerprintjs

var FingerprintJS = (function () {
    function hash(str) {
        // Simple FNV-1a hash for stub (real lib uses SHA-256 + many signals)
        var h = 0x811c9dc5;
        for (var i = 0; i < str.length; i++) {
            h ^= str.charCodeAt(i);
            h = (h * 0x01000193) >>> 0;
        }
        return h.toString(16).padStart(8, '0');
    }

    function collectSignals() {
        return {
            userAgent: navigator.userAgent || '',
            language: navigator.language || '',
            platform: navigator.platform || '',
            screen: (screen.width || 0) + 'x' + (screen.height || 0),
            colorDepth: screen.colorDepth || 0,
            timezone: Intl.DateTimeFormat().resolvedOptions().timeZone || '',
            hardwareConcurrency: navigator.hardwareConcurrency || 0,
            touchSupport: 'ontouchstart' in window
        };
    }

    function load(options) {
        return Promise.resolve({
            get: function () {
                var signals = collectSignals();
                var visitorId = hash(JSON.stringify(signals));
                return Promise.resolve({
                    visitorId: visitorId,
                    components: signals
                });
            }
        });
    }

    return { load: load };
})();
