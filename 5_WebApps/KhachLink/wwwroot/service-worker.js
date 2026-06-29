const CACHE_NAME = 'vanan-khachlink-v2';
const STATIC_CACHE = 'vanan-static-v2';
const DYNAMIC_CACHE = 'vanan-dynamic-v2';

// Core static assets to cache
const staticUrlsToCache = [
  '/',
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
