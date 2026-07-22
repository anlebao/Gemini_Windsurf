// ============================================================================
// VanAn KhachLink PWA Service Worker — Phase 3 + SRI Hotfix (v12-sri-fix)
// ============================================================================
// Cache strategy:
//   - _framework/*.wasm/.dll/.js → network-first + cache fallback (WASM_CACHE)
//     · Was cache-first — caused SRI mismatch after deploys (stale cached wasm
//       vs fresh blazor.boot.json integrity hashes). Network-first ensures wasm
//       always matches the fresh boot.json. Offline: cached fallback (same deploy
//       as cached boot.json → SRI passes).
//   - blazor.boot.json → network-first + cache fallback (detect updates)
//   - Static assets (CSS/JS/icons) → cache-first (STATIC_CACHE)
//   - API GETs (whitelisted) → network-first + cache fallback (DYNAMIC_CACHE)
//     · catalog/campaigns → stale-while-revalidate (show cached, refresh bg)
//     · 24h cache expiration via x-sw-cached-at header
//   - Navigation → network-first → cache → offline shell
//
// Phase 3 changes:
//   - dynamicCachePatterns is now ACTUALLY USED (was dead code in Phase 2)
//   - Whitelist approach: only cache listed endpoints (not all /api/* GETs)
//   - Corrected endpoints: /api/public/orders/, /api/customerorders (was /api/orders)
//   - Removed dead /api/menu pattern (endpoint does not exist)
//   - Stale-while-revalidate for /api/catalog/ and /api/campaigns/
//   - 24h cache expiration (x-sw-cached-at header, evict on retrieval)
//   - Cache version bumped v10-batched → v11-phase3
//
// SRI Hotfix (v12-sri-fix, 2026-07-22):
//   - WASM/DLL assets changed from cache-first → network-first + cache fallback
//     (cache-first caused SRI integrity mismatch after deploys: stale cached wasm
//     vs fresh blazor.boot.json integrity hashes → browser blocked wasm load)
//   - Added activate event to delete stale caches from old SW versions
//     (caches.match() checks ALL caches — old entries caused SRI mismatches)
//   - Cache version bumped v11-phase3 → v12-sri-fix
// ============================================================================

// Load auto-generated asset manifest (Blazor WASM SDK generates this with
// hashes + URLs for all _framework/* assets). Used in install event to precache.
importScripts('/service-worker-assets.js');

const CACHE_NAME = 'vanan-khachlink-v12-sri-fix';
const STATIC_CACHE = 'vanan-static-v12-sri-fix';
const DYNAMIC_CACHE = 'vanan-dynamic-v12-sri-fix';
const WASM_CACHE = 'vanan-wasm-v12-sri-fix';

// Core static assets to cache (must all return 200 — addAll fails on any 404)
const staticUrlsToCache = [
  '/',
  '/index.html',
  '/manifest.json',
  '/app.css',
  '/js/pwa.js',
  '/js/qr-scanner.js',
  '/js/cart-animation.js',
  '/favicon.png',
  '/icons/icon-192x192.png',
  '/icons/icon-512x512.png'
];

// Dynamic API content that can be cached (Option C endpoints — verified against
// Gateway controllers + KhachLink pages/services on 2026-07-22).
// Whitelist approach: only listed prefixes are cacheable. Auth/user-specific
// GET endpoints (/api/customers/me, /api/loyalty/my, /api/customer-identity/me)
// are intentionally EXCLUDED to avoid cross-user cache leaks on shared devices.
const dynamicCachePatterns = [
  '/api/tenants/search',
  '/api/tenants/nearby',
  '/api/tenants/by-slug/',
  '/api/tenants/',            // covers /{id}/store-info, /{id}/feature-settings
  '/api/catalog/',            // /api/catalog/recommended
  '/api/campaigns/',          // /api/campaigns/by-tenant/{id}, /{trackingCode}, /{id}
  '/api/products/',           // /api/products/recommended, /grouped-by-tenant, /{id}/qr
  '/api/public/orders/',      // OrderTracking: /api/public/orders/{id}
  '/api/customerorders'       // OrderHistory: /api/customerorders?page=... (auth-scoped, same-device only)
];

// Stale-while-revalidate patterns: return cached response immediately (if fresh
// enough), then fetch in background to update cache. Used for catalog/campaigns
// where showing slightly stale data is acceptable and perceived latency matters.
const swrPatterns = [
  '/api/catalog/',
  '/api/campaigns/'
];

// Cache expiration: 24h for API responses. Cached responses older than this are
// treated as stale (still returned offline as last resort, but refreshed when
// network is available).
const CACHE_EXPIRY_MS = 24 * 60 * 60 * 1000; // 24 hours
const CACHE_TIMESTAMP_HEADER = 'x-sw-cached-at';

// WASM assets: _framework/* (DLLs, .wasm, .wasm.br, .wasm.gz, blazor.boot.json, blazor.webassembly.js)
const wasmCachePattern = /^\/_framework\//;
// blazor.boot.json: network-first (detect updates), not cache-first
const bootManifestUrl = '/_framework/blazor.boot.json';

// ============================================================================
// Install: precache static assets + WASM assets from SDK manifest
// ============================================================================
self.addEventListener('install', event => {
  event.waitUntil(
    Promise.all([
      // 1. Cache static assets (best-effort, per-URL)
      caches.open(STATIC_CACHE).then(cache => {
        console.log('SW install: caching static assets (best-effort)');
        return Promise.allSettled(
          staticUrlsToCache.map(url =>
            cache.add(url).catch(err => {
              console.warn('SW install: failed to cache static', url, err);
            })
          )
        );
      }),
      // 2. Cache WASM assets from service-worker-assets.js manifest (auto-generated by SDK)
      // Batched to avoid overwhelming nginx rate limiter (burst=20 → 503 on 80 concurrent).
      // Process 5 URLs at a time, sequentially per batch.
      caches.open(WASM_CACHE).then(cache => {
        console.log('SW install: caching WASM assets from manifest');
        // service-worker-assets.js is auto-generated by Blazor WASM SDK
        // It contains self.assetsManifest with { assets: [{ url, hash }] }
        if (self.assetsManifest && self.assetsManifest.assets) {
          const wasmAssets = self.assetsManifest.assets
            .filter(a => a.url.startsWith('_framework/') || a.url === 'service-worker-assets.js')
            .map(a => '/' + a.url.replace(/\\/g, '/'));
          console.log('SW install: precaching', wasmAssets.length, 'WASM assets (batched, 5/batch)');
          const BATCH_SIZE = 5;
          const cacheBatch = (startIndex) => {
            if (startIndex >= wasmAssets.length) return Promise.resolve();
            const batch = wasmAssets.slice(startIndex, startIndex + BATCH_SIZE);
            return Promise.allSettled(
              batch.map(url =>
                cache.add(url).catch(err => {
                  console.warn('SW install: failed to cache WASM asset', url, err);
                })
              )
            ).then(() => cacheBatch(startIndex + BATCH_SIZE));
          };
          return cacheBatch(0);
        }
        // Fallback: cache blazor.boot.json + blazor.webassembly.js if manifest not loaded
        return Promise.allSettled([
          cache.add('/_framework/blazor.boot.json').catch(() => {}),
          cache.add('/_framework/blazor.webassembly.js').catch(() => {})
        ]);
      })
    ]).then(() => {
      console.log('SW install complete — activating');
      return self.skipWaiting(); // Force activation
    })
  );
});

// ============================================================================
// Activate: clean up old cache versions + claim clients immediately
// ============================================================================
// Without this, caches.match() could return stale entries from old SW versions
// (e.g., v10-batched, v11-phase3) — causing SRI mismatches after deploys.
self.addEventListener('activate', event => {
  const allowedCaches = [CACHE_NAME, STATIC_CACHE, DYNAMIC_CACHE, WASM_CACHE];
  event.waitUntil(
    caches.keys()
      .then(keys => {
        const staleKeys = keys.filter(key => !allowedCaches.includes(key));
        return Promise.all(staleKeys.map(key => {
          console.log('SW activate: deleting stale cache', key);
          return caches.delete(key);
        }));
      })
      .then(() => self.clients.claim())
  );
});

// ============================================================================
// Helpers: cache timestamp + expiration check
// ============================================================================
// Stamp a response with x-sw-cached-at header (ms since epoch) so we can evict
// expired entries on retrieval. Returns a new Response (headers are immutable).
function stampResponse(response) {
  const headers = new Headers(response.headers);
  headers.set(CACHE_TIMESTAMP_HEADER, Date.now().toString());
  return new Response(response.body, {
    status: response.status,
    statusText: response.statusText,
    headers: headers
  });
}

// Check if a cached response is older than CACHE_EXPIRY_MS. Returns true if
// expired (or if timestamp missing — defensive, treat unset as expired).
function isExpired(response) {
  const stamped = response.headers.get(CACHE_TIMESTAMP_HEADER);
  if (!stamped) return true;
  const cachedAt = parseInt(stamped, 10);
  if (isNaN(cachedAt)) return true;
  return (Date.now() - cachedAt) > CACHE_EXPIRY_MS;
}

// ============================================================================
// Fetch: cache strategy by asset type
// ============================================================================
self.addEventListener('fetch', event => {
  const request = event.request;
  const url = new URL(request.url);

  // Only intercept GET requests — POST/PUT/DELETE go straight to network
  if (request.method !== 'GET') {
    return;
  }

  // Skip cross-origin requests (CDN scripts like html5-qrcode, jsQR)
  if (url.origin !== self.location.origin) {
    return;
  }

  // --- 1. blazor.boot.json: network-first + cache fallback (detect updates) ---
  if (url.pathname === bootManifestUrl) {
    event.respondWith(
      fetch(request)
        .then(response => {
          if (response && response.status === 200) {
            const responseToCache = response.clone();
            caches.open(WASM_CACHE).then(cache => {
              cache.put(request, responseToCache);
            });
          }
          return response;
        })
        .catch(() => {
          // Offline: return cached boot manifest
          return caches.match(request).then(cached => {
            if (cached) return cached;
            return new Response(JSON.stringify({ error: 'Offline' }), {
              status: 503,
              headers: { 'Content-Type': 'application/json' }
            });
          });
        })
    );
    return;
  }

  // --- 2. _framework/* (DLLs, .wasm, .wasm.br, .wasm.gz): network-first + cache fallback ---
  // Network-first ensures wasm content always matches the fresh blazor.boot.json
  // integrity hashes. Was cache-first — caused SRI mismatch after deploys because
  // stale cached wasm (old build) was returned with fresh boot.json (new hashes).
  // Offline fallback: cached wasm from same deploy as cached boot.json → SRI passes.
  if (wasmCachePattern.test(url.pathname)) {
    event.respondWith(
      fetch(request)
        .then(response => {
          if (response && response.status === 200) {
            const responseToCache = response.clone();
            caches.open(WASM_CACHE).then(cache => {
              cache.put(request, responseToCache);
            });
          }
          return response;
        })
        .catch(() => {
          // Offline: return cached wasm (from same deploy as cached boot.json)
          return caches.match(request).then(cached => cached || Response.error());
        })
    );
    return;
  }

  // --- 3. Static assets (CSS/JS/images): cache-first ---
  if (request.destination === 'style' ||
      request.destination === 'image' ||
      (request.destination === 'script' && !wasmCachePattern.test(url.pathname)) ||
      staticUrlsToCache.some(staticUrl => url.pathname === staticUrl)) {
    event.respondWith(
      caches.match(request)
        .then(response => {
          if (response) {
            return response;
          }
          return fetch(request).then(response => {
            if (!response || response.status !== 200) {
              return response;
            }
            const responseToCache = response.clone();
            caches.open(STATIC_CACHE).then(cache => {
              cache.put(request, responseToCache);
            });
            return response;
          });
        })
    );
    return;
  }

  // --- 4. API GETs (whitelisted): network-first + cache fallback, SWR for catalog/campaigns ---
  // Phase 3: only cache endpoints in dynamicCachePatterns (whitelist). Auth
  // endpoints (/api/customers/me, /api/loyalty/my) are NOT cached — avoids
  // cross-user leaks on shared devices. 24h expiration via x-sw-cached-at.
  const isCacheable = dynamicCachePatterns.some(p => url.pathname.startsWith(p));
  if (isCacheable) {
    const isSwr = swrPatterns.some(p => url.pathname.startsWith(p));

    if (isSwr) {
      // Stale-while-revalidate: return cached immediately (if any), refresh bg.
      // If cached is still fresh (< 24h), skip background fetch entirely — no
      // network hit. If expired or missing, fetch from network to refresh.
      event.respondWith(
        caches.match(request).then(cachedResponse => {
          const cachedIsFresh = cachedResponse && !isExpired(cachedResponse);

          if (cachedIsFresh) {
            // Fresh cache: return immediately, no background fetch needed.
            return cachedResponse;
          }

          // No cache or expired: fetch from network. If we have stale cache,
          // return it immediately and update in background (true SWR).
          const fetchPromise = fetch(request)
            .then(response => {
              if (response && response.status === 200) {
                const responseToCache = stampResponse(response.clone());
                caches.open(DYNAMIC_CACHE).then(cache => {
                  cache.put(request, responseToCache);
                });
              }
              return response;
            })
            .catch(() => {
              // Network failed — if no cached response, return offline JSON
              if (!cachedResponse) {
                return new Response(
                  JSON.stringify({ error: 'Offline mode', cached: false }),
                  { status: 503, headers: { 'Content-Type': 'application/json' } }
                );
              }
              // Network failed but we have stale cache: return it.
              return cachedResponse;
            });

          if (cachedResponse) {
            // Expired cache: return stale immediately, refresh in background.
            return cachedResponse;
          }
          // No cache at all: wait for network.
          return fetchPromise;
        })
      );
      return;
    }

    // Network-first (non-SWR): try network, fall back to cache if offline.
    event.respondWith(
      fetch(request)
        .then(response => {
          if (response && response.status === 200) {
            const responseToCache = stampResponse(response.clone());
            caches.open(DYNAMIC_CACHE).then(cache => {
              cache.put(request, responseToCache);
            });
          }
          return response;
        })
        .catch(() => {
          // Offline: return cached response (stale is better than blank offline).
          // Expiration is enforced on the SWR path; here any cache hit wins.
          return caches.match(request).then(cachedResponse => {
            if (cachedResponse) {
              return cachedResponse;
            }
            // No cached response: return offline error
            return new Response(
              JSON.stringify({ error: 'Offline mode', cached: false }),
              { status: 503, headers: { 'Content-Type': 'application/json' } }
            );
          });
        })
    );
    return;
  }

  // --- 5. Navigation (HTML pages): network-first → cache → offline shell ---
  if (request.mode === 'navigate' || request.destination === 'document') {
    event.respondWith(
      fetch(request)
        .then(response => {
          // Cache successful navigation responses (index.html for SPA routes)
          if (response && response.status === 200) {
            const responseToCache = response.clone();
            caches.open(STATIC_CACHE).then(cache => {
              cache.put(request, responseToCache);
            });
          }
          return response;
        })
        .catch(() => {
          // Offline: try cached navigation
          return caches.match(request).then(cached => {
            if (cached) return cached;
            // Fallback to cached index.html (SPA)
            return caches.match('/index.html').then(indexCached => {
              if (indexCached) return indexCached;
              // Last resort: offline shell
              return new Response(
                OFFLINE_SHELL_HTML,
                { headers: { 'Content-Type': 'text/html; charset=utf-8' } }
              );
            });
          });
        })
    );
    return;
  }

  // --- 6. Default: network-first ---
  event.respondWith(
    fetch(request).catch(() => caches.match(request))
  );
});

// ============================================================================
// Phase 0 quick fix: Beautiful offline shell
// (shown only when ALL fallbacks fail — WASM cached = app loads normally offline)
// ============================================================================
const OFFLINE_SHELL_HTML = `<!DOCTYPE html>
<html lang="vi">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0, viewport-fit=cover">
  <title>Vạn An — Ngoại tuyến</title>
  <style>
    * { box-sizing: border-box; margin: 0; padding: 0; }
    body {
      font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
      background: linear-gradient(135deg, #8B4513 0%, #A0522D 100%);
      color: #fff;
      min-height: 100vh;
      display: flex;
      align-items: center;
      justify-content: center;
      padding: 20px;
    }
    .offline-card {
      background: rgba(255,255,255,0.1);
      backdrop-filter: blur(10px);
      border-radius: 24px;
      padding: 40px 28px;
      max-width: 420px;
      width: 100%;
      text-align: center;
      box-shadow: 0 20px 60px rgba(0,0,0,0.3);
    }
    .logo {
      width: 96px;
      height: 96px;
      margin: 0 auto 20px;
      background: #fff;
      border-radius: 22px;
      display: flex;
      align-items: center;
      justify-content: center;
      box-shadow: 0 8px 24px rgba(0,0,0,0.2);
    }
    .logo img { width: 72px; height: 72px; border-radius: 16px; }
    h1 { font-size: 22px; font-weight: 700; margin-bottom: 8px; }
    p { font-size: 15px; line-height: 1.6; opacity: 0.92; margin-bottom: 24px; }
    .icon-offline {
      font-size: 56px;
      margin-bottom: 16px;
      display: inline-block;
    }
    .retry-btn {
      background: #fff;
      color: #8B4513;
      border: none;
      padding: 14px 32px;
      border-radius: 30px;
      font-size: 16px;
      font-weight: 600;
      cursor: pointer;
      transition: transform 0.2s, box-shadow 0.2s;
      box-shadow: 0 4px 16px rgba(0,0,0,0.2);
    }
    .retry-btn:hover { transform: translateY(-2px); box-shadow: 0 6px 20px rgba(0,0,0,0.3); }
    .retry-btn:active { transform: translateY(0); }
    .footer { margin-top: 24px; font-size: 12px; opacity: 0.7; }
  </style>
</head>
<body>
  <div class="offline-card">
    <div class="logo">
      <img src="/icons/icon-192x192.png" alt="Vạn An" onerror="this.style.display='none'">
    </div>
    <div class="icon-offline" aria-hidden="true">📡</div>
    <h1>Bạn đang ngoại tuyến</h1>
    <p>App Vạn An cần kết nối internet để đặt hàng và xem cửa hàng.<br>Vui lòng kiểm tra mạng và thử lại.</p>
    <button class="retry-btn" onclick="window.location.reload()">Thử lại</button>
    <div class="footer">Vạn An Group — Hệ thống đặt hàng thông minh</div>
  </div>
</body>
</html>`;

// ============================================================================
// Activate: clean old caches + claim clients
// ============================================================================
self.addEventListener('activate', event => {
  const cacheWhitelist = [STATIC_CACHE, DYNAMIC_CACHE, WASM_CACHE];
  event.waitUntil(
    caches.keys().then(cacheNames => {
      return Promise.all(
        cacheNames.map(cacheName => {
          if (cacheWhitelist.indexOf(cacheName) === -1) {
            console.log('SW activate: deleting old cache:', cacheName);
            return caches.delete(cacheName);
          }
        })
      );
    }).then(() => {
      console.log('SW activated — Phase 3 offline API fallback hardening ready');
      return self.clients.claim(); // Take control of all pages
    })
  );
});

// ============================================================================
// Push notification handler (unchanged from Phase 0)
// ============================================================================
self.addEventListener('push', event => {
  let notificationData = {
    title: 'Vạn An Group',
    body: 'Bạn có thông báo mới từ Vạn An Group',
    icon: '/icons/icon-192x192.png',
    badge: '/icons/icon-192x192.png',
    vibrate: [100, 50, 100],
    data: {
      dateOfArrival: Date.now(),
      primaryKey: 1,
    },
  };

  if (event.data) {
    try {
      const parsed = event.data.json();
      notificationData = { ...notificationData, ...parsed };
      if (parsed.data) {
        notificationData.data = { ...notificationData.data, ...parsed.data };
      }
    } catch (e) {
      notificationData.body = event.data.text();
    }
  }

  event.waitUntil(
    self.registration.showNotification(notificationData.title, notificationData)
  );
});

// Notification click handler
self.addEventListener('notificationclick', event => {
  event.notification.close();
  event.waitUntil(
    clients.matchAll({ type: 'window' }).then(clientList => {
      for (const client of clientList) {
        if ('focus' in client) {
          return client.focus();
        }
      }
      if (clients.openWindow) {
        return clients.openWindow('/');
      }
    })
  );
});

// Message handler for skipWaiting
self.addEventListener('message', event => {
  if (event.data && event.data.type === 'SKIP_WAITING') {
    self.skipWaiting();
  }
});
