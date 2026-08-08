// CC-S4 (Sprint 4): App-install attribution tracker.
// Listens for PWA 'appinstalled' event + manual trigger.
// On install: reads referralCode from localStorage + POST to Gateway /api/community/app-install/attributed.

window.vananAppInstall = {
    // Initialize — register event listeners
    init: function () {
        // Listen for PWA install event
        window.addEventListener('appinstalled', function (event) {
            console.log('[vananAppInstall] PWA installed — attributing...');
            window.vananAppInstall.attribute();
        });

        // Also listen for beforeinstallprompt to know install is available
        window.addEventListener('beforeinstallprompt', function (event) {
            console.log('[vananAppInstall] Install prompt available');
            window.vananAppInstall._deferredPrompt = event;
        });

        console.log('[vananAppInstall] Initialized — listening for appinstalled event');
    },

    // Attribute the install to a salesman via referral code
    attribute: function () {
        var referralCode = localStorage.getItem('vanan_referral_code');
        if (!referralCode) {
            console.log('[vananAppInstall] No referral code in localStorage — skipping attribution');
            return;
        }

        var customerToken = localStorage.getItem('customerToken') || localStorage.getItem('customer_token');
        if (!customerToken) {
            console.log('[vananAppInstall] No customer token — cannot attribute');
            return;
        }

        // Collect fingerprint if available
        var fingerprintHash = null;
        var fingerprintSignals = null;
        if (window.fingerprint && typeof window.fingerprint.collect === 'function') {
            try {
                var fp = window.fingerprint.collect();
                fingerprintHash = fp.hash || null;
                fingerprintSignals = JSON.stringify(fp.signals || {});
            } catch (e) {
                console.warn('[vananAppInstall] Fingerprint collection failed:', e);
            }
        }

        // Get device token from localStorage or generate
        var deviceToken = localStorage.getItem('vanan_device_token') || null;

        var body = {
            referralCode: referralCode,
            fingerprintHash: fingerprintHash,
            fingerprintSignals: fingerprintSignals,
            deviceToken: deviceToken
        };

        // Determine Gateway base URL
        var gatewayUrl = window.vananAppInstall._getGatewayUrl();

        fetch(gatewayUrl + '/api/community/app-install/attributed', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'X-Customer-Token': customerToken
            },
            body: JSON.stringify(body)
        }).then(function (resp) {
            if (resp.ok) {
                console.log('[vananAppInstall] Attribution successful');
                localStorage.removeItem('vanan_referral_code');
            } else if (resp.status === 409) {
                console.log('[vananAppInstall] Already attributed — clearing referral code');
                localStorage.removeItem('vanan_referral_code');
            } else {
                console.warn('[vananAppInstall] Attribution failed:', resp.status);
            }
        }).catch(function (err) {
            console.error('[vananAppInstall] Attribution error:', err);
        });
    },

    // Save referral code from scanned QR URL
    saveReferralCode: function (compositeCode) {
        if (!compositeCode || compositeCode.indexOf('|') === -1) return false;
        localStorage.setItem('vanan_referral_code', compositeCode);
        console.log('[vananAppInstall] Saved referral code:', compositeCode);
        return true;
    },

    // Get saved referral code (for UI display)
    getReferralCode: function () {
        return localStorage.getItem('vanan_referral_code') || null;
    },

    // Clear referral code
    clearReferralCode: function () {
        localStorage.removeItem('vanan_referral_code');
    },

    // Determine Gateway URL from current host
    _getGatewayUrl: function () {
        var host = window.location.hostname;
        if (host.indexOf('khachvip.online') !== -1) {
            // GCP VPS uses "2" suffix (diemthuong2, app2, api2)
            // Oracle VPS uses no suffix (diemthuong, app, api)
            var prefix = (host.indexOf('2.khachvip.online') !== -1) ? 'api2' : 'api';
            return 'https://' + prefix + '.khachvip.online';
        }
        return 'https://localhost:5001'; // dev fallback
    },

    _deferredPrompt: null
};

// Auto-init on script load
window.vananAppInstall.init();
