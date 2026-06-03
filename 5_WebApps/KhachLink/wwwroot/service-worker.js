const CACHE_NAME = 'vanan-khachlink-v2';
const STATIC_CACHE = 'vanan-static-v2';
const DYNAMIC_CACHE = 'vanan-dynamic-v2';

// Core static assets to cache
const staticUrlsToCache = [
  '/',
  '/index.html',
  '/manifest.json',
  '/css/app.css',
  '/js/app.js',
  '/images/logo.png',
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
self.addEventListener('install', event => {
  event.waitUntil(
    caches.open(STATIC_CACHE)
      .then(cache => {
        console.log('Caching static assets');
        return cache.addAll(staticUrlsToCache);
      })
      .then(() => {
        console.log('Static assets cached successfully');
        return self.skipWaiting(); // Force activation
      })
  );
});

// Enhanced fetch strategy with cache-first for static, network-first for dynamic
self.addEventListener('fetch', event => {
  const request = event.request;
  const url = new URL(request.url);

  // Cache-first strategy for static assets
  if (request.destination === 'script' || 
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

  // Network-first strategy for API calls with offline fallback
  if (url.pathname.startsWith('/api/')) {
    event.respondWith(
      fetch(request)
        .then(response => {
          if (!response || response.status !== 200) {
            return response;
          }
          
          // Cache successful API responses
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
              return new Response(JSON.stringify({
                error: 'Offline mode',
                data: [
                  { id: 1, name: 'Trà sữa', price: 25000, available: true },
                  { id: 2, name: 'Cà phê', price: 20000, available: true }
                ]
              }), {
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

  // Default: cache-first for navigation
  event.respondWith(
    caches.match(request)
      .then(response => {
        if (response) {
          return response;
        }
        return fetch(request);
      })
  );
});

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

// Push notification handler
self.addEventListener('push', event => {
  const options = {
    body: event.data ? event.data.text() : 'Bạn có thông báo mới từ Vạn An Group',
    icon: '/images/icon-192x192.png',
    badge: '/images/badge-72x72.png',
    vibrate: [100, 50, 100],
    data: {
      dateOfArrival: Date.now(),
      primaryKey: 1
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

  event.waitUntil(
    self.registration.showNotification('Vạn An Group', options)
  );
});

// Notification click handler
self.addEventListener('notificationclick', event => {
  console.log('Notification click received.');

  event.notification.close();

  if (event.action === 'explore') {
    event.waitUntil(
      clients.openWindow('https://localhost:5002')
    );
  }
});
