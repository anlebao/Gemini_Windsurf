const CACHE_NAME = 'vanan-khachlink-v8-offline-shell';
const STATIC_CACHE = 'vanan-static-v8-offline-shell';
const DYNAMIC_CACHE = 'vanan-dynamic-v8-offline-shell';

// Core static assets to cache (must all return 200 — addAll fails on any 404)
const staticUrlsToCache = [
  '/manifest.json',
  '/app.css',
  '/js/pwa.js',
  '/favicon.png',
  '/icons/icon-192x192.png',
  '/icons/icon-512x512.png'
];

// Dynamic content that can be cached
const dynamicCachePatterns = [
  '/api/menu',
  '/api/products',
  '/api/orders'
];

// Install Service Worker
// IMPORTANT: cache.addAll() is atomic — if any URL fails, the entire install fails
// and the service worker never activates, breaking PWA install on Android.
// Use individual cache.add() with per-URL catch so one missing asset doesn't
// block the whole SW lifecycle.
self.addEventListener('install', event => {
  event.waitUntil(
    caches.open(STATIC_CACHE)
      .then(cache => {
        console.log('Caching static assets (best-effort, per-URL)');
        return Promise.allSettled(
          staticUrlsToCache.map(url =>
            cache.add(url).catch(err => {
              console.warn('SW install: failed to cache', url, err);
            })
          )
        );
      })
      .then(() => {
        console.log('Static assets cached (best-effort) — activating SW');
        return self.skipWaiting(); // Force activation
      })
  );
});

// Enhanced fetch strategy with cache-first for static, network-first for dynamic
self.addEventListener('fetch', event => {
  const request = event.request;
  const url = new URL(request.url);

  // Only intercept GET requests — POST/PUT/DELETE go straight to network
  if (request.method !== 'GET') {
    return;
  }

  // Cache-first strategy for static assets
  if ((request.destination === 'script' && !url.pathname.startsWith('/_framework/')) ||
      request.destination === 'style' ||
      request.destination === 'image' ||
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

  // Network-first strategy for API GET calls with offline fallback
  if (url.pathname.startsWith('/api/')) {
    event.respondWith(
      fetch(request)
        .then(response => {
          if (!response || response.status !== 200) {
            return response;
          }
          
          // Cache successful GET API responses
          const responseToCache = response.clone();
          caches.open(DYNAMIC_CACHE).then(cache => {
            cache.put(request, responseToCache);
          });
          
          return response;
        })
        .catch(() => {
          // Try cache if network fails
          return caches.match(request).then(cachedResponse => {
            if (cachedResponse) {
              return cachedResponse;
            }
            
            // Offline fallback for API
            if (url.pathname.includes('/menu')) {
              return new Response(JSON.stringify({ error: 'Offline mode' }), {
                headers: { 'Content-Type': 'application/json' }
              });
            }
            
            return new Response(JSON.stringify({ error: 'Offline mode' }), {
              status: 503,
              headers: { 'Content-Type': 'application/json' }
            });
          });
        })
    );
    return;
  }

  // Default: network-first for navigation, fallback to cache, then offline shell
  event.respondWith(
    fetch(request)
      .catch(() => caches.match(request).then(cached => cached || new Response(
        OFFLINE_SHELL_HTML,
        { headers: { 'Content-Type': 'text/html; charset=utf-8' } }
      )))
  );
});

// Phase 0 quick fix: Beautiful offline shell (replaces plain "Vui lòng kết nối internet" text)
// Inline CSS + cached icon (icon-192x192.png is in staticUrlsToCache, available offline).
// No JS interactivity — this is a static fallback shown when Blazor circuit is dead.
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
    <h1> Bạn đang ngoại tuyến</h1>
    <p>App Vạn An cần kết nối internet để đặt hàng và xem cửa hàng.<br>Vui lòng kiểm tra mạng và thử lại.</p>
    <button class="retry-btn" onclick="window.location.reload()"> Thử lại</button>
    <div class="footer">Vạn An Group — Hệ thống đặt hàng thông minh</div>
  </div>
</body>
</html>`;

// Enhanced activation with cache cleanup
self.addEventListener('activate', event => {
  const cacheWhitelist = [STATIC_CACHE, DYNAMIC_CACHE];
  event.waitUntil(
    caches.keys().then(cacheNames => {
      return Promise.all(
        cacheNames.map(cacheName => {
          if (cacheWhitelist.indexOf(cacheName) === -1) {
            console.log('Deleting old cache:', cacheName);
            return caches.delete(cacheName);
          }
        })
      );
    }).then(() => {
      console.log('Service Worker activated');
      return self.clients.claim(); // Take control of all pages
    })
  );
});

// Push notification handler - Wave 9: Enhanced with order-specific parsing
self.addEventListener('push', event => {
  let notificationData = {
    title: 'Vạn An Group',
    body: 'Bạn có thông báo mới từ Vạn An Group',
    icon: '/images/icon-192x192.png',
    badge: '/images/badge-72x72.png',
    vibrate: [100, 50, 100],
    data: {
      dateOfArrival: Date.now(),
      primaryKey: 1,
      url: '/' // Default URL
    },
    actions: [
      {
        action: 'explore',
        title: 'Xem ngay',
        icon: '/images/checkmark.png'
      },
      {
        action: 'close',
        title: 'Đóng',
        icon: '/images/xmark.png'
      }
    ]
  };

  // Parse push payload for order-specific notifications
  try {
    if (event.data) {
      const payload = event.data.json();
      
      if (payload && payload.type === 'order_status_changed') {
        // Order status change notification
        notificationData.title = '📦 Cập nhật đơn hàng';
        notificationData.body = payload.message || `Trạng thái đơn hàng: ${payload.status}`;
        notificationData.data.url = payload.actionUrl || `/order-tracking/${payload.orderId}`;
        notificationData.data.orderId = payload.orderId;
        notificationData.data.status = payload.status;
        
        console.log('Order status push notification:', payload);
      } else if (payload && payload.message) {
        // Generic notification with custom message
        notificationData.body = payload.message;
        if (payload.title) {
          notificationData.title = payload.title;
        }
        if (payload.url) {
          notificationData.data.url = payload.url;
        }
      } else {
        // Fallback to text content
        notificationData.body = event.data.text() || 'Bạn có thông báo mới từ Vạn An Group';
      }
    }
  } catch (error) {
    console.error('Error parsing push payload:', error);
    // Fallback to simple text
    notificationData.body = event.data ? event.data.text() : 'Bạn có thông báo mới từ Vạn An Group';
  }

  event.waitUntil(
    self.registration.showNotification(notificationData.title, notificationData)
  );
});

// Notification click handler - Wave 9: Enhanced with order-specific URL handling
self.addEventListener('notificationclick', event => {
  console.log('Notification click received.', event);

  event.notification.close();

  if (event.action === 'explore') {
    // Use the URL from notification data, or fallback to home
    const targetUrl = event.notification.data?.url || '/';
    const absoluteUrl = new URL(targetUrl, self.location.origin).href;
    
    event.waitUntil(
      clients.matchAll({ type: 'window' }).then(clientList => {
        // Check if there's already a window open
        for (const client of clientList) {
          if (client.url === absoluteUrl && 'focus' in client) {
            return client.focus();
          }
        }
        // If no window found, open a new one
        if (clients.openWindow) {
          return clients.openWindow(absoluteUrl);
        }
      })
    );
  }
});
