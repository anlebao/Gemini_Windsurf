// vanan-interop.js — JS interop bridge for KhachLink WASM.
// Defines window.vanan object used by Blazor JSRuntime.InvokeAsync("vanan.*").
// Currently backs Cloudflare Turnstile widget on /claim (Register.razor).
//
// Turnstile site key is a PUBLIC value (safe to expose client-side).
// It is injected via window.vananTurnstileSiteKey (set in index.html at deploy time).
// When empty (dev/unconfigured) the widget is not rendered and the token stays
// empty — Gateway TurnstileVerificationService skips verification in dev mode.
(function () {
    var _turnstileToken = '';

    window.vanan = {
        // Returns the configured Turnstile site key (empty string when not configured).
        getTurnstileSiteKey: function () {
            return window.vananTurnstileSiteKey || '';
        },

        // Loads the Cloudflare Turnstile script (once) and renders the widget
        // into the container element with the given id.
        renderTurnstile: function (containerId, siteKey) {
            if (!siteKey) return;

            var container = document.getElementById(containerId);
            if (!container) return;

            // Load Turnstile API script once.
            if (!document.getElementById('vanan-turnstile-script')) {
                var script = document.createElement('script');
                script.id = 'vanan-turnstile-script';
                script.src = 'https://challenges.cloudflare.com/turnstile/v0/api.js';
                script.async = true;
                script.defer = true;
                document.head.appendChild(script);
            }

            // Wait for the Turnstile API to be ready, then render.
            var tryRender = function (attempts) {
                if (window.turnstile && typeof window.turnstile.render === 'function') {
                    window.turnstile.render('#' + containerId, {
                        sitekey: siteKey,
                        callback: function (token) { _turnstileToken = token; }
                    });
                } else if (attempts < 50) {
                    setTimeout(function () { tryRender(attempts + 1); }, 100);
                }
            };
            tryRender(0);
        },

        // Returns the Turnstile token captured by the widget callback (empty until solved).
        getTurnstileToken: function () {
            return _turnstileToken;
        }
    };
})();
