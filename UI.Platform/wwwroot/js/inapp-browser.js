/* In-app browser detection + external-browser escape (2026-11).
   Dùng cho AI Link /a/{slug} khi khách mở link từ Zalo/Messenger/...
   — WebView thiếu SpeechRecognition/mic permission → hiện banner gợi ý
   mở bằng trình duyệt ngoài. */
(function () {
    'use strict';

    var PATTERNS = [
        { re: /Zalo|zalo/i, name: 'Zalo' },
        { re: /FBAN|FBAV|FB_IAB|Facebook/i, name: 'Facebook' },
        { re: /Messenger/i, name: 'Messenger' },
        { re: /TikTok|BytedanceWebview|musical_ly/i, name: 'TikTok' },
        { re: /Line\//i, name: 'LINE' },
        { re: /Instagram/i, name: 'Instagram' },
        { re: /MicroMessenger/i, name: 'WeChat' }
    ];

    function detect() {
        var ua = navigator.userAgent || '';
        for (var i = 0; i < PATTERNS.length; i++) {
            if (PATTERNS[i].re.test(ua)) {
                return {
                    inApp: true,
                    app: PATTERNS[i].name,
                    isAndroid: /Android/i.test(ua),
                    isIos: /iPhone|iPad|iPod/i.test(ua)
                };
            }
        }
        // Generic Android WebView: UA có "; wv)" mà không có Chrome.
        if (/; wv\)/.test(ua) && /Android/i.test(ua)) {
            return { inApp: true, app: 'trình duyệt trong app', isAndroid: true, isIos: false };
        }
        return { inApp: false, app: null, isAndroid: false, isIos: false };
    }

    // Android: intent:// URL escape khỏi WebView → mở trình duyệt mặc định.
    // iOS không có cơ chế escape tự động → caller hiện hướng dẫn thủ công.
    function openExternal(url) {
        try {
            var u = new URL(url);
            if (/Android/i.test(navigator.userAgent)) {
                var intent = 'intent://' + u.host + u.pathname + u.search +
                    '#Intent;scheme=' + u.protocol.replace(':', '') +
                    ';action=android.intent.action.VIEW;end';
                window.location.href = intent;
                return true;
            }
        } catch (e) { /* fallthrough */ }
        return false;
    }

    window.vananInAppBrowser = { detect: detect, openExternal: openExternal };
})();
