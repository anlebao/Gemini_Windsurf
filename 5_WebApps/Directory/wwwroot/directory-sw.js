// ============================================================================
// VanAn Directory (Blazor SSR) — minimal PWA service worker (v1, 2026-10-16)
//
// Purpose: satisfy Chrome installability (registered SW with a fetch handler)
// and provide a basic offline shell for the Directory app.
//
// NOTE: the app pages are server-rendered (SSR), so navigations are
// network-first — offline fallback returns the last cached page shell.
// The PWA manifest (/manifest.json) + icons are served by the KhachLink nginx
// proxy (dynamic per instance) — never intercepted here.
// ============================================================================

const CACHE_NAME = 'vanan-directory-v1';
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
  // (dynamic per-instance manifest, SW updates).
  if (url.pathname === '/manifest.json' ||
      url.pathname === '/directory-sw.js' ||
      url.pathname === '/service-worker-assets.js') {
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
  event.respondWith(
    caches.match(request).then(cached => {
      if (cached) return cached;
      return fetch(request).then(response => {
        if (response && response.status === 200 && response.type === 'basic') {
          const clone = response.clone();
          caches.open(CACHE_NAME).then(cache => cache.put(request, clone));
        }
        return response;
      });
    })
  );
});