// ShopERP Admin Mobile PWA — service worker v1 (SRS van_an_admin_mobile_pwa_srs_v1)
// Blazor Server = online-only (G1): SW chỉ cache STATIC SHELL — KHÔNG offline nghiệp vụ.
//   - Navigation (HTML) → network-first → fallback /offline.html khi mất mạng
//   - Static assets (css/js/icons/_framework) GET → cache-first
//   - /api/* + POST/PUT/DELETE → KHÔNG bao giờ cache (G5)
//   - manifest.json + icons → bypass SW (bài học icon-poisoning v25)

const CACHE_NAME = 'shoperp-pwa-v1';
const STATIC_ASSETS = [
  '/offline.html',
  '/app.css',
  '/favicon.png',
  '/manifest.json',
];

self.addEventListener('install', (event) => {
  event.waitUntil(
    caches.open(CACHE_NAME)
      .then((cache) => cache.addAll(STATIC_ASSETS))
      .then(() => self.skipWaiting())
  );
});

self.addEventListener('activate', (event) => {
  event.waitUntil(
    caches.keys().then((keys) =>
      Promise.all(keys.filter((k) => k !== CACHE_NAME).map((k) => caches.delete(k)))
    ).then(() => self.clients.claim())
  );
});

self.addEventListener('fetch', (event) => {
  const req = event.request;
  if (req.method !== 'GET') return;

  const url = new URL(req.url);
  if (url.origin !== self.location.origin) return;

  // Never intercept: manifest + icons + API (G5) + SignalR endpoints.
  if (url.pathname === '/manifest.json' || url.pathname.startsWith('/icons/')
    || url.pathname.startsWith('/api/') || url.pathname.startsWith('/_blazor')
    || url.pathname.startsWith('/_framework/aspnetcore-browser-refresh')) {
    return;
  }

  // Navigation → network-first → offline.html fallback.
  if (req.mode === 'navigate') {
    event.respondWith(
      fetch(req).catch(() => caches.match('/offline.html'))
    );
    return;
  }

  // Static assets → cache-first, chỉ cache response hợp lệ (không phải HTML —
  // bài học v25: SPA fallback trả text/html cho file thiếu → poison cache).
  if (url.pathname.startsWith('/css/') || url.pathname.startsWith('/js/')
    || url.pathname.startsWith('/_framework/') || url.pathname.startsWith('/bootstrap/')
    || url.pathname.startsWith('/_content/')
    || url.pathname === '/app.css' || url.pathname === '/favicon.png'
    || url.pathname === '/favicon.ico' || url.pathname === '/offline.html') {
    event.respondWith(
      caches.match(req).then((cached) => {
        if (cached) return cached;
        return fetch(req).then((resp) => {
          if (resp.ok && (resp.headers.get('content-type') || '').indexOf('text/html') === -1) {
            const clone = resp.clone();
            caches.open(CACHE_NAME).then((cache) => cache.put(req, clone));
          }
          return resp;
        });
      })
    );
    return;
  }

  // Mọi request khác → network passthrough (không cache).
});
