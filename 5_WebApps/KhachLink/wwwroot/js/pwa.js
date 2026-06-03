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

    checkInstallStatus() {
        // Check if running in standalone mode
        this.isInstalled = window.matchMedia('(display-mode: standalone)').matches ||
                         window.navigator.standalone === true ||
                         document.referrer.includes('android-app://');
        return this.isInstalled;
    },

    async showInstallPrompt() {
        if (this.isInstalled) {
            return false;
        }

        if (this.deferredPrompt) {
            try {
                const result = await this.deferredPrompt.prompt();
                const outcome = await result.userChoice;
                
                if (outcome === 'accepted') {
                    this.isInstalled = true;
                    console.log('PWA installation accepted');
                    return true;
                } else {
                    console.log('PWA installation dismissed');
                    return false;
                }
            } catch (error) {
                console.error('Failed to show install prompt:', error);
                return false;
            }
        }
        
        return false;
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

    // Push Notifications
    async subscribeToPush() {
        if ('serviceWorker' in navigator && 'PushManager' in window) {
            try {
                const registration = await navigator.serviceWorker.ready;
                const subscription = await registration.pushManager.subscribe({
                    userVisibleOnly: true,
                    applicationServerKey: this.urlB64ToUint8Array('YOUR_VAPID_PUBLIC_KEY')
                });
                
                return JSON.stringify(subscription);
            } catch (error) {
                console.error('Failed to subscribe to push:', error);
                return null;
            }
        }
        return null;
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

        // Install prompt event
        window.addEventListener('beforeinstallprompt', (e) => {
            e.preventDefault();
            this.deferredPrompt = e;
            console.log('Install prompt ready');
        });

        // Installed event
        window.addEventListener('appinstalled', () => {
            this.isInstalled = true;
            console.log('PWA installed successfully');
            if (this.dotNetRef) {
                this.dotNetRef.invokeMethodAsync('HandleInstallStateChanged', true);
            }
        });

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
    }
};

// Initialize PWA on page load
document.addEventListener('DOMContentLoaded', () => {
    // Check install status
    window.vananPWA.checkInstallStatus();
    
    // Setup network status indicator
    if (!navigator.onLine) {
        document.body.classList.add('offline');
    }
});
