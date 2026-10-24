// ============================================================================
// VanAn Directory — PWA install helper (2026-10-16)
//
// Frictionless install goal (user directive 2026-10-16): installing the app on
// a phone must NOT require complex browser operations (no chrome://settings
// data-clearing steps). Strategy:
//   - Registers the Directory service worker (directory-sw.js).
//   - Captures beforeinstallprompt as soon as it fires.
//   - Android: shows a floating "Cài đặt ứng dụng" button. Tapping it while
//     the prompt is ready → native prompt(). If Chrome isn't ready yet (fresh
//     user — engagement heuristic: 1 tap + ~30s of use), we persist an
//     "install requested" flag and AUTO-FIRE the native prompt on the next
//     visit — zero user steps after the first tap.
//   - iOS: no beforeinstallprompt exists → shows a one-time Share →
//     "Add to Home Screen" hint banner (the only flow iOS supports).
// ============================================================================
(function () {
  'use strict';

  if (!('serviceWorker' in navigator)) return;

  var INSTALL_REQUESTED_KEY = 'vanan_dir_install_requested';
  var deferredPrompt = null;
  var isInstalled =
    window.matchMedia('(display-mode: standalone)').matches ||
    window.navigator.standalone === true;
  var isIOS = /iPad|iPhone|iPod/.test(navigator.userAgent) && !window.MSStream;
  var button = null;
  var hintShown = false;

  function registerServiceWorker() {
    navigator.serviceWorker.register('/directory-sw.js').catch(function (err) {
      console.warn('[DirectoryPWA] SW registration failed:', err);
    });
  }

  function clearInstallRequested() {
    try { localStorage.removeItem(INSTALL_REQUESTED_KEY); } catch (err) {}
  }

  function tryInstall() {
    if (!deferredPrompt || isInstalled) return;
    var prompt = deferredPrompt;
    deferredPrompt = null;
    try {
      prompt.prompt();
    } catch (err) {
      // prompt() without a user gesture can throw on some Chrome versions.
      console.warn('[DirectoryPWA] prompt() threw:', err);
      showInstallButton();
      return;
    }
    (prompt.userChoice || Promise.resolve({ outcome: 'unknown' }))
      .then(function (choice) {
        if (choice.outcome === 'accepted') {
          isInstalled = true;
          clearInstallRequested();
          hideInstallButton();
        } else if (choice.outcome === 'dismissed') {
          // User actively declined — clear the flag so we stop auto-prompting.
          clearInstallRequested();
        } else {
          // 'unknown' — prompt may have been silently blocked (no user
          // gesture). Keep the flag (next visit retries) + keep the button
          // so the user can tap → real gesture → prompt.
          showInstallButton();
        }
      })
      .catch(function (err) {
        console.warn('[DirectoryPWA] install prompt failed:', err);
        showInstallButton();
      });
  }

  function hideInstallButton() {
    if (button && button.parentNode) button.parentNode.removeChild(button);
    button = null;
  }

  function showInstallButton() {
    if (isInstalled || isIOS || button) return;
    button = document.createElement('button');
    button.id = 'vanan-dir-install-btn';
    button.setAttribute('aria-label', 'Cài đặt ứng dụng');
    button.style.cssText =
      'position:fixed;bottom:16px;left:50%;transform:translateX(-50%);z-index:9999;' +
      'background:#2c5f2d;color:#fff;border:none;border-radius:24px;padding:12px 22px;' +
      'font-size:15px;font-weight:600;box-shadow:0 4px 16px rgba(0,0,0,.3);' +
      'cursor:pointer;display:flex;align-items:center;gap:8px;';
    button.innerHTML =
      '<i class="bi bi-download" style="font-size:16px;"></i> Cài đặt ứng dụng';
    button.onclick = function () {
      if (deferredPrompt) {
        tryInstall();
      } else {
        // Chrome not ready yet (engagement heuristic: needs ~30s of use).
        // Ask for an auto-prompt on the next visit — no user steps needed.
        var alreadyRequested = false;
        try {
          alreadyRequested = localStorage.getItem(INSTALL_REQUESTED_KEY) === '1';
          localStorage.setItem(INSTALL_REQUESTED_KEY, '1');
        } catch (err) {}
        // Repeat tap with the flag already set = Chrome is NOT firing
        // beforeinstallprompt at all (app already installed, or prompt
        // suppressed). Show real guidance instead of looping the same toast.
        if (alreadyRequested) {
          showBlockedToast();
        } else {
          showPreparingToast();
        }
        hideInstallButton();
      }
    };
    document.body.appendChild(button);
  }

  function showPreparingToast() {
    var toast = document.createElement('div');
    toast.id = 'vanan-dir-install-toast';
    toast.style.cssText =
      'position:fixed;bottom:80px;left:50%;transform:translateX(-50%);z-index:9999;' +
      'background:rgba(44,95,45,0.97);color:#fff;padding:14px 18px;border-radius:14px;' +
      'font-size:14px;line-height:1.5;max-width:88vw;text-align:center;' +
      'box-shadow:0 4px 16px rgba(0,0,0,.3);';
    toast.innerHTML =
      '<div style="font-weight:600;margin-bottom:4px;">App đang chuẩn bị cài đặt</div>' +
      '<div style="opacity:.92;">Hộp thoại cài đặt sẽ <b>tự động hiện</b> ở lần mở app kế tiếp — ' +
      'bạn không cần làm gì thêm. Nếu không hiện, hãy dùng menu trình duyệt ⋮ → "Thêm vào Màn hình chính".</div>' +
      '<button onclick="var t=document.getElementById(\'vanan-dir-install-toast\'); if(t) t.remove();" ' +
      'style="margin-top:10px;background:#fff;color:#2c5f2d;border:none;padding:7px 18px;border-radius:20px;' +
      'font-weight:600;cursor:pointer;font-size:13px;">Đã hiểu</button>';
    document.body.appendChild(toast);
    setTimeout(function () {
      if (toast.parentNode) toast.parentNode.removeChild(toast);
    }, 15000);
  }

  function showBlockedToast() {
    var toast = document.createElement('div');
    toast.id = 'vanan-dir-install-toast';
    toast.style.cssText =
      'position:fixed;bottom:80px;left:50%;transform:translateX(-50%);z-index:9999;' +
      'background:rgba(120,60,20,0.97);color:#fff;padding:14px 18px;border-radius:14px;' +
      'font-size:14px;line-height:1.5;max-width:88vw;text-align:center;' +
      'box-shadow:0 4px 16px rgba(0,0,0,.3);';
    toast.innerHTML =
      '<div style="font-weight:600;margin-bottom:4px;">Chrome chưa cho phép cài tự động</div>' +
      '<div style="opacity:.92;text-align:left;">Có thể app <b>đã được cài đặt trước đó</b> ' +
      '(kiểm tra trong Cài đặt → Ứng dụng trên điện thoại — xóa icon màn hình không gỡ app), ' +
      'hoặc Chrome đang chặn hộp thoại cài đặt.<br><br>' +
      'Thử: menu trình duyệt <b>⋮ → "Thêm vào Màn hình chính"</b>.</div>' +
      '<button onclick="var t=document.getElementById(\'vanan-dir-install-toast\'); if(t) t.remove();" ' +
      'style="margin-top:10px;background:#fff;color:#783c14;border:none;padding:7px 18px;border-radius:20px;' +
      'font-weight:600;cursor:pointer;font-size:13px;">Đã hiểu</button>';
    document.body.appendChild(toast);
    setTimeout(function () {
      if (toast.parentNode) toast.parentNode.removeChild(toast);
    }, 20000);
  }

  function showIosHint() {
    if (hintShown || isInstalled) return;
    hintShown = true;
    var hint = document.createElement('div');
    hint.id = 'vanan-dir-ios-hint';
    hint.style.cssText =
      'position:fixed;bottom:16px;left:12px;right:12px;z-index:9999;' +
      'background:linear-gradient(135deg,#2c5f2d,#4a8c4a);color:#fff;padding:14px 16px;' +
      'border-radius:14px;font-size:14px;line-height:1.6;box-shadow:0 4px 16px rgba(0,0,0,.3);' +
      'max-width:400px;margin:0 auto;';
    hint.innerHTML =
      '<div style="font-weight:600;margin-bottom:6px;">Cài đặt app trên iPhone</div>' +
      '<div style="opacity:.94;">Nhấn nút <b>Share</b> (biểu tượng ô vuông mũi tên lên) ở thanh dưới trình duyệt, ' +
      'rồi chọn <b>"Thêm vào Màn hình chính"</b>.</div>' +
      '<button onclick="var h=document.getElementById(\'vanan-dir-ios-hint\'); if(h) h.remove();" ' +
      'style="margin-top:10px;background:#fff;color:#2c5f2d;border:none;padding:7px 18px;border-radius:20px;' +
      'font-weight:600;cursor:pointer;font-size:13px;">Đã hiểu</button>';
    document.body.appendChild(hint);
  }

  window.addEventListener('beforeinstallprompt', function (e) {
    e.preventDefault();
    deferredPrompt = e;
    console.log('[DirectoryPWA] beforeinstallprompt FIRED — app is installable');

    var installRequested = false;
    try { installRequested = localStorage.getItem(INSTALL_REQUESTED_KEY) === '1'; } catch (err) {}

    if (!isInstalled && installRequested) {
      // User tapped install on a previous visit but Chrome wasn't ready.
      // Auto-fire the native prompt now — the flag is cleared ONLY when the
      // prompt resolves 'accepted' (inside tryInstall), so a silently-blocked
      // auto-prompt doesn't strand the flow. The floating button stays as a
      // gesture-based fallback.
      setTimeout(function () {
        tryInstall();
        if (!isInstalled) showInstallButton();
      }, 800);
    } else if (!isInstalled) {
      showInstallButton();
    }
  });

  window.addEventListener('appinstalled', function () {
    isInstalled = true;
    hideInstallButton();
    console.log('[DirectoryPWA] appinstalled fired');
  });

  document.addEventListener('DOMContentLoaded', function () {
    registerServiceWorker();
    if (isInstalled) return;
    if (isIOS) {
      // iOS has no beforeinstallprompt — show the Share → Add to Home Screen hint.
      setTimeout(showIosHint, 4000);
    } else {
      // Android: show the floating install button right away. If Chrome has not
      // fired beforeinstallprompt yet, tapping it schedules an auto-prompt for
      // the next visit (no complex browser steps).
      setTimeout(showInstallButton, 2500);
    }
  });
})();