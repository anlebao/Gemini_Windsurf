// ============================================================================
// VanAn Directory (Blazor SSR) — minimal PWA service worker (v2, 2026-10-24)
//
// Purpose: satisfy Chrome installability (registered SW with a fetch handler)
// and provide a basic offline shell for the Directory app.
//
// NOTE: the app pages are server-rendered (SSR), so navigations are
// network-first — offline fallback returns the last cached page shell.
// The PWA manifest (/manifest.json) + icons are served by the KhachLink nginx
// proxy (dynamic per instance) — never intercepted here.
//
// v2 (2026-10-24):
//   - Icon-poisoning guard (same bug as KhachLink SW v25): the static
//     cache-first branch cached ANY 200 response — an HTML fallback/error
//     page for khachlink-icons/*.png would be served forever as a "PNG" →
//     Chrome: "resource isn't a valid image" → install blocked.
//   - Fix: /khachlink-icons/ added to the never-intercept list (was only
//     documented, not actually skipped) + never cache text/html in the
//     static branch + cache bump v1 → v2 to purge stale entries.
//
// v3 (2026-10-24):
//   - directory-pwa.js install flow hardening (flag consumed only on
//     'accepted', blocked auto-prompt keeps flag + button fallback, repeat
//     tap shows guidance toast). Static JS is cache-first → bump v2 → v3
//     so clients re-fetch the fixed script.
// ============================================================================

const CACHE_NAME = 'vanan-directory-v3';
const SHELL_URLS = ['/'];

self.addEventListener('install', event => {
  // Best-effort precache of the shell — addAll-style failure (any 404) must
  // NOT block SW install, so use per-URL allSettled.
  event.waitUntil(
    caches.open(CACHE_NAME).then(cache =>
      Promise.allSettled(
        SHELL_URLS.map(url =>
          cache.add(url).catch(err => {
            console.warn('SW install: failed to cache shell', url, err);
          })
        )
      )
    ).then(() => self.skipWaiting()) // Force activation
  );
});

self.addEventListener('activate', event => {
  event.waitUntil(
    caches.keys()
      .then(keys => Promise.all(
        keys.filter(key => key !== CACHE_NAME).map(key => caches.delete(key))
      ))
      .then(() => self.clients.claim())
  );
});

self.addEventListener('fetch', event => {
  const request = event.request;
  if (request.method !== 'GET') return;

  const url = new URL(request.url);
  if (url.origin !== self.location.origin) return;

  // Never intercept PWA metadata — always fresh from network
  // (dynamic per-instance manifest + icons, SW updates).
  if (url.pathname === '/manifest.json' ||
      url.pathname === '/directory-sw.js' ||
      url.pathname === '/service-worker-assets.js' ||
      url.pathname.startsWith('/khachlink-icons/')) {
    return;
  }

  // Navigations: network-first, cached shell fallback when offline.
  if (request.mode === 'navigate') {
    event.respondWith(
      fetch(request)
        .then(response => {
          if (response && response.status === 200) {
            const clone = response.clone();
            caches.open(CACHE_NAME).then(cache => cache.put(request, clone));
          }
          return response;
        })
        .catch(() =>
          caches.match(request).then(cached => cached || caches.match('/'))
        )
    );
    return;
  }

  // Static assets: cache-first, network fallback (cache on success).
  // Never cache text/html here — non-navigation requests that get an HTML
  // response (error page, fallback) would poison the cache permanently.
  event.respondWith(
    caches.match(request).then(cached => {
      if (cached) return cached;
      return fetch(request).then(response => {
        const ct = response ? (response.headers.get('Content-Type') || '') : '';
        if (response && response.status === 200 && response.type === 'basic' &&
            ct.indexOf('text/html') === -1) {
          const clone = response.clone();
          caches.open(CACHE_NAME).then(cache => cache.put(request, clone));
        }
        return response;
      });
    })
  );
});