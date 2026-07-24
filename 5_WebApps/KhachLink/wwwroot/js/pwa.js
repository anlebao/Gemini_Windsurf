// Van An PWA JavaScript Helper - Phase 2.5.1
// Handles PWA installation, notifications, and offline capabilities

window.vananPWA = {
    // Service Worker registration
    async registerServiceWorker() {
        if ('serviceWorker' in navigator) {
            try {
                const registration = await navigator.serviceWorker.register('/service-worker.js');
                console.log('Service Worker registered successfully:', registration);
                
                // Check for updates
                registration.addEventListener('updatefound', () => {
                    const newWorker = registration.installing;
                    if (newWorker) {
                        newWorker.addEventListener('statechange', () => {
                            if (newWorker.state === 'installed' && navigator.serviceWorker.controller) {
                                // New service worker available
                                this.notifyServiceWorkerUpdated();
                            }
                        });
                    }
                });

                // Silent SW update: when new service worker takes control, do NOT auto-reload.
                // Previous behavior (auto-reload on controllerchange) caused disruptive page
                // refresh every ~60s on Home page after deploys. Instead, show a subtle toast
                // so the user can refresh at their convenience. Next page load picks up new SW.
                if (navigator.serviceWorker.controller) {
                    navigator.serviceWorker.addEventListener('controllerchange', () => {
                        console.log('Service Worker controller changed — new version active (silent update)');
                        // Show subtle toast notification instead of disruptive reload
                        if (window.vananPWA && window.vananPWA.dotNetRef) {
                            window.vananPWA.dotNetRef.invokeMethodAsync('HandleServiceWorkerUpdated')
                                .catch(() => { /* Blazor not ready — silent */ });
                        }
                        // Show non-blocking toast
                        var toast = document.createElement('div');
                        toast.id = 'vanan-sw-update-toast';
                        toast.style.cssText = 'position:fixed;bottom:20px;left:50%;transform:translateX(-50%);background:rgba(139,69,19,0.95);color:white;padding:10px 20px;border-radius:24px;font-size:14px;z-index:99999;box-shadow:0 4px 16px rgba(0,0,0,0.3);cursor:pointer;transition:opacity 0.3s;';
                        toast.textContent = 'App đã cập nhật — nhấn để tải lại';
                        toast.onclick = function() { window.location.reload(); };
                        document.body.appendChild(toast);
                        // Auto-dismiss after 8s if user doesn't click
                        setTimeout(function() {
                            if (toast && toast.parentNode) {
                                toast.style.opacity = '0';
                                setTimeout(function() { if (toast.parentNode) toast.remove(); }, 300);
                            }
                        }, 8000);
                    });
                }
                
                return registration;
            } catch (error) {
                console.error('Service Worker registration failed:', error);
                throw error;
            }
        }
    },

    // PWA Installation
    deferredPrompt: null,
    isInstalled: false,

    // Function wrapper for Blazor JSInterop (InvokeAsync expects a function, not a property)
    isInstalledFunc() {
        return this.checkInstallStatus();
    },

    checkInstallStatus() {
        // Check if running in standalone mode
        this.isInstalled = window.matchMedia('(display-mode: standalone)').matches ||
                         window.navigator.standalone === true ||
                         document.referrer.includes('android-app://');
        // Loyalty-C WS-B: If standalone mode detected (user already installed PWA, e.g., iOS Add to Home Screen),
        // notify backend to award PWAInstall mission points. Fire-and-forget — silent on failure.
        if (this.isInstalled) {
            this.notifyPwaInstalledBackend();
        }
        return this.isInstalled;
    },

    // Loyalty-C WS-B: Notify backend that PWA is installed (triggers PWAInstall mission — one-time reward).
    // Fire-and-forget: silent on no-token, network error, or already-awarded (backend is idempotent).
    async notifyPwaInstalledBackend() {
        try {
            var token = localStorage.getItem('customer_token');
            if (!token) return; // Not logged in — mission will trigger on next login + install detection
            var resp = await fetch('/api/customer-profile/pwa-installed', {
                method: 'POST',
                headers: { 'X-Customer-Token': token }
            });
            if (resp.ok) {
                var data = await resp.json();
                if (data && data.pointsAwarded > 0) {
                    console.log('PWA install mission awarded: +' + data.pointsAwarded + ' points');
                    // Show subtle toast for points reward
                    if (window.vananPWA && window.vananPWA.showPointsToast) {
                        window.vananPWA.showPointsToast(data.pointsAwarded);
                    }
                }
            }
        } catch (e) {
            console.warn('notifyPwaInstalledBackend failed (non-blocking):', e);
        }
    },

    // Loyalty-C WS-B: Show subtle toast for awarded mission points.
    showPointsToast(points) {
        var toast = document.createElement('div');
        toast.style.cssText = 'position:fixed;bottom:20px;left:50%;transform:translateX(-50%);background:rgba(34,139,34,0.95);color:white;padding:10px 20px;border-radius:24px;font-size:14px;z-index:99999;box-shadow:0 4px 16px rgba(0,0,0,0.3);';
        toast.textContent = '🎉 +' + points + ' điểm thưởng!';
        document.body.appendChild(toast);
        setTimeout(function() {
            if (toast && toast.parentNode) {
                toast.style.opacity = '0';
                toast.style.transition = 'opacity 0.4s';
                setTimeout(function() { if (toast.parentNode) toast.remove(); }, 400);
            }
        }, 3500);
    },

    async showInstallPrompt() {
        if (this.isInstalled) {
            return false;
        }

        // Android/Desktop Chrome: use beforeinstallprompt
        if (this.deferredPrompt) {
            try {
                const result = await this.deferredPrompt.prompt();
                const outcome = await result.userChoice;

                if (outcome === 'accepted') {
                    this.isInstalled = true;
                    console.log('PWA installation accepted');
                    // Loyalty-C WS-B: Notify backend to award PWAInstall mission points.
                    this.notifyPwaInstalledBackend();
                    return true;
                } else {
                    console.log('PWA installation dismissed');
                    // Clear deferredPrompt — Chrome only allows prompt() once per event.
                    // Next page load may fire beforeinstallprompt again (if engagement heuristic met).
                    this.deferredPrompt = null;
                    return false;
                }
            } catch (error) {
                console.error('Failed to show install prompt:', error);
                // Clear stale deferredPrompt — prompt() may have already been called
                this.deferredPrompt = null;
                return false;
            }
        }

        // iOS Safari: no beforeinstallprompt support — show instructions
        const isIOS = /iPad|iPhone|iPod/.test(navigator.userAgent) && !window.MSStream;
        if (isIOS) {
            this.showInstallHelpToast('iOS', 'Nhấn nút Share → "Thêm vào Màn hình chính"');
            return false;
        }

        // Android/Desktop: beforeinstallprompt hasn't fired or was already consumed.
        // This means Chrome has SUPPRESSED the install prompt — reloading won't help.
        // Common causes: user dismissed prompt too many times, or app was previously installed.
        // Fix: user must clear site data in Chrome settings, then reload.
        this.showInstallHelpToast('Android', null);
        return false;
    },

    // Show non-blocking toast with install help.
    // When hint is null, shows clear-site-data instructions (Chrome suppressed prompt).
    showInstallHelpToast(platform, hint) {
        // Remove any existing toast
        var existing = document.getElementById('vanan-install-help-toast');
        if (existing) existing.remove();

        var toast = document.createElement('div');
        toast.id = 'vanan-install-help-toast';
        toast.style.cssText = 'position:fixed;bottom:20px;left:50%;transform:translateX(-50%);background:rgba(139,69,19,0.95);color:white;padding:16px 20px;border-radius:16px;font-size:14px;z-index:99999;box-shadow:0 4px 16px rgba(0,0,0,0.3);max-width:92vw;text-align:center;line-height:1.5;';

        if (hint) {
            // iOS: show Share → Add to Home Screen instructions
            toast.innerHTML = '<div style="font-weight:600;margin-bottom:8px;">Cài đặt Vạn An App</div>' +
                '<div style="opacity:0.9;margin-bottom:12px;">' + hint + '</div>' +
                '<button onclick="window.location.reload()" style="background:white;color:#8B4513;border:none;padding:8px 16px;border-radius:8px;font-weight:600;cursor:pointer;font-size:13px;">Đã hiểu</button>';
        } else {
            // Android/Desktop: Chrome suppressed beforeinstallprompt — need clear site data
            toast.innerHTML =
                '<div style="font-weight:600;margin-bottom:8px;font-size:15px;">Chrome đã tắt prompt cài đặt</div>' +
                '<div style="opacity:0.9;margin-bottom:10px;font-size:13px;">Để cài app, cần xóa dữ liệu site:</div>' +
                '<div style="background:rgba(255,255,255,0.15);border-radius:8px;padding:10px;margin-bottom:12px;text-align:left;font-size:12px;line-height:1.6;">' +
                '1. Mở <b>chrome://settings/content/all</b><br>' +
                '2. Tìm <b>diemthuong.khachvip.online</b><br>' +
                '3. Nhấn <b>Xóa dữ liệu</b> (Delete data)<br>' +
                '4. Quay lại đây — prompt cài đặt sẽ hiện' +
                '</div>' +
                '<button onclick="var t=document.getElementById(\'vanan-install-help-toast\'); if(t) t.remove();" style="background:white;color:#8B4513;border:none;padding:8px 16px;border-radius:8px;font-weight:600;cursor:pointer;font-size:13px;">Đã hiểu</button>';
        }
        document.body.appendChild(toast);

        // Auto-dismiss after 30s (longer for reading instructions)
        setTimeout(function() {
            if (toast && toast.parentNode) {
                toast.style.opacity = '0';
                toast.style.transition = 'opacity 0.3s';
                setTimeout(function() { if (toast.parentNode) toast.remove(); }, 300);
            }
        }, 30000);
    },

    // Detect if running on iOS (no beforeinstallprompt support)
    isIOS() {
        return /iPad|iPhone|iPod/.test(navigator.userAgent) && !window.MSStream;
    },

    // Check if beforeinstallprompt is available (Android/Desktop)
    canInstallNative() {
        return this.deferredPrompt !== null;
    },

    // Notifications
    async requestNotificationPermission() {
        if ('Notification' in window) {
            const permission = await Notification.requestPermission();
            return permission;
        }
        return 'denied';
    },

    async showNotification(title, body, icon = '/icons/icon-192x192.png') {
        if ('Notification' in window && Notification.permission === 'granted') {
            try {
                const notification = new Notification(title, {
                    body: body,
                    icon: icon,
                    badge: '/icons/badge-72x72.png',
                    vibrate: [100, 50, 100],
                    tag: 'vanan-notification',
                    renotify: true
                });

                // Auto-close after 5 seconds
                setTimeout(() => {
                    notification.close();
                }, 5000);

                return true;
            } catch (error) {
                console.error('Failed to show notification:', error);
                return false;
            }
        }
        return false;
    },

    // Push Notifications - ENABLED in Wave 9 (KhachLink-W4)
    async subscribeToPush() {
        if ('serviceWorker' in navigator && 'PushManager' in window) {
            try {
                const registration = await navigator.serviceWorker.ready;
                const subscription = await registration.pushManager.subscribe({
                    userVisibleOnly: true,
                    applicationServerKey: this.urlB64ToUint8Array('BJIeg2XokT35UrNdXV26uTiMa0CxwbRI5Fmb9j4djeSdXO74U1wS6BD15MlnvYppLtDx2Rbm01TSkcVcf7p58RE')
                });

                console.log('Push subscription successful:', subscription);
                return JSON.stringify(subscription);
            } catch (error) {
                console.error('Failed to subscribe to push:', error);
                return null;
            }
        }
        console.warn('Push notifications not supported in this browser');
        return null;
    },

    // Phase 5: Unsubscribe from push (browser side — removes PushSubscription from SW)
    async unsubscribeFromPush() {
        if ('serviceWorker' in navigator && 'PushManager' in window) {
            try {
                const registration = await navigator.serviceWorker.ready;
                const subscription = await registration.pushManager.getSubscription();
                if (subscription) {
                    await subscription.unsubscribe();
                    console.log('Push subscription removed (browser side)');
                }
            } catch (error) {
                console.error('Failed to unsubscribe from push:', error);
            }
        }
    },

    // Network status
    isOnline() {
        return navigator.onLine;
    },

    // Standalone mode detection
    isStandalone() {
        return window.matchMedia('(display-mode: standalone)').matches ||
               window.navigator.standalone === true ||
               document.referrer.includes('android-app://');
    },

    // Cache management
    async clearCaches() {
        if ('caches' in window) {
            try {
                const cacheNames = await caches.keys();
                await Promise.all(
                    cacheNames.map(cacheName => caches.delete(cacheName))
                );
                console.log('All caches cleared');
                return true;
            } catch (error) {
                console.error('Failed to clear caches:', error);
                return false;
            }
        }
        return false;
    },

    // Event listeners setup
    dotNetRef: null,

    setupEventListeners(dotNetRef) {
        this.dotNetRef = dotNetRef;

        // beforeinstallprompt + appinstalled listeners are registered at bottom of file
        // (immediately on script load) to avoid race condition where Chrome fires
        // beforeinstallprompt before Blazor WASM boots and calls setupEventListeners().

        // Network status events
        window.addEventListener('online', () => {
            console.log('Network connection restored');
            if (this.dotNetRef) {
                this.dotNetRef.invokeMethodAsync('HandleOnlineStateChanged', true);
            }
        });

        window.addEventListener('offline', () => {
            console.log('Network connection lost');
            if (this.dotNetRef) {
                this.dotNetRef.invokeMethodAsync('HandleOnlineStateChanged', false);
            }
        });

        // Service worker messages
        if ('serviceWorker' in navigator) {
            navigator.serviceWorker.addEventListener('message', (event) => {
                if (event.data && event.data.type === 'NOTIFICATION') {
                    if (this.dotNetRef) {
                        this.dotNetRef.invokeMethodAsync('HandleNotificationReceived', event.data.message);
                    }
                }
            });
        }

        // Page visibility for background sync
        document.addEventListener('visibilitychange', () => {
            if (!document.hidden && this.dotNetRef) {
                // Page became visible, could trigger data sync
                this.dotNetRef.invokeMethodAsync('HandlePageVisible');
            }
        });
    },

    // Utility methods
    urlB64ToUint8Array(base64String) {
        const padding = '='.repeat((4 - base64String.length % 4) % 4);
        const base64 = (base64String + padding)
            .replace(/-/g, '+')
            .replace(/_/g, '/');

        const rawData = window.atob(base64);
        const outputArray = new Uint8Array(rawData.length);

        for (let i = 0; i < rawData.length; ++i) {
            outputArray[i] = rawData.charCodeAt(i);
        }
        return outputArray;
    },

    // Background sync simulation
    async syncData(data) {
        if ('serviceWorker' in navigator) {
            try {
                const registration = await navigator.serviceWorker.ready;
                
                // Store data for background sync
                const db = await this.openDB();
                const tx = db.transaction('sync-queue', 'readwrite');
                const store = tx.objectStore('sync-queue');
                await store.add({
                    id: Date.now(),
                    data: data,
                    timestamp: new Date().toISOString()
                });
                
                console.log('Data queued for background sync');
                return true;
            } catch (error) {
                console.error('Failed to queue data for sync:', error);
                return false;
            }
        }
        return false;
    },

    // IndexedDB helper for offline storage
    async openDB() {
        return new Promise((resolve, reject) => {
            const request = indexedDB.open('VanAnPWA', 1);
            
            request.onerror = () => reject(request.error);
            request.onsuccess = () => resolve(request.result);
            
            request.onupgradeneeded = (event) => {
                const db = event.target.result;
                
                if (!db.objectStoreNames.contains('sync-queue')) {
                    const store = db.createObjectStore('sync-queue', { keyPath: 'id', autoIncrement: true });
                    store.createIndex('timestamp', 'timestamp', { unique: false });
                }
                
                if (!db.objectStoreNames.contains('offline-data')) {
                    const store = db.createObjectStore('offline-data', { keyPath: 'id' });
                    store.createIndex('type', 'type', { unique: false });
                }
            };
        });
    },

    // Notify service worker update
    notifyServiceWorkerUpdated() {
        if (this.dotNetRef) {
            this.dotNetRef.invokeMethodAsync('HandleServiceWorkerUpdated');
        }
    },

    // W17-T5: Get current GPS position for Store Finder
    getCurrentPosition() {
        return new Promise((resolve, reject) => {
            if (!navigator.geolocation) {
                reject(new Error('Geolocation not supported'));
                return;
            }
            navigator.geolocation.getCurrentPosition(
                (pos) => resolve({ latitude: pos.coords.latitude, longitude: pos.coords.longitude }),
                (err) => reject(err),
                { timeout: 8000, maximumAge: 60000 }
            );
        });
    }
};

// Utility functions called from Blazor components
window.updatePageTitle = (title) => { document.title = title; };
window.applyThemeClass = (themeClass) => {
    const body = document.body;
    body.classList.forEach(c => { if (c.startsWith('theme-')) body.classList.remove(c); });
    body.classList.add(themeClass);
};

// ============================================================================
// IMMEDIATE EVENT LISTENERS — registered on script load, NOT waiting for Blazor.
// Race condition fix: Chrome fires `beforeinstallprompt` right after evaluating
// manifest.json (during page load), but Blazor WASM takes 3-5s to boot before
// calling setupEventListeners(). By then the event is already gone.
// These listeners capture the event immediately and store it in deferredPrompt.
// ============================================================================

window.addEventListener('beforeinstallprompt', (e) => {
    e.preventDefault();
    window.vananPWA.deferredPrompt = e;
    console.log('[PWA] beforeinstallprompt FIRED — app is installable, deferredPrompt captured');
    console.log('[PWA] platforms:', e.platforms, 'userChoice result:', e.userChoice);
    // Notify Blazor so it can show the install button at the right time
    // (only when the browser confirms the app is installable)
    if (window.vananPWA.dotNetRef) {
        window.vananPWA.dotNetRef.invokeMethodAsync('HandleBeforeInstallPromptFired')
            .catch(() => { /* Blazor not ready yet — prompt will show on next render */ });
    }
});

// Debug: log if beforeinstallprompt did NOT fire after 5s (helps diagnose install issues)
setTimeout(() => {
    if (!window.vananPWA.deferredPrompt && !window.vananPWA.isInstalled) {
        console.log('[PWA] beforeinstallprompt did NOT fire after 5s — possible causes:');
        console.log('[PWA]   1. Chrome suppressed prompt (user dismissed too many times)');
        console.log('[PWA]   2. App already installed (check matchMedia standalone)');
        console.log('[PWA]   3. Engagement heuristic not met (visit + interact more)');
        console.log('[PWA]   4. Chrome version too old (need Chrome 70+)');
        console.log('[PWA] Fix: chrome://settings/content/all → search "diemthuong" → Delete data → reload');
    }
}, 5000);

window.addEventListener('appinstalled', () => {
    window.vananPWA.isInstalled = true;
    console.log('[PWA] appinstalled fired (immediate listener)');
    if (window.vananPWA.dotNetRef) {
        window.vananPWA.dotNetRef.invokeMethodAsync('HandleInstallStateChanged', true);
    }
});

// Initialize PWA on page load
document.addEventListener('DOMContentLoaded', () => {
    // Check install status
    window.vananPWA.checkInstallStatus();
    
    // Setup network status indicator
    if (!navigator.onLine) {
        document.body.classList.add('offline');
    }
});
