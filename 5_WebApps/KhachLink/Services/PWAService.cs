using Microsoft.JSInterop;
using Microsoft.AspNetCore.Components;

namespace VanAn.KhachLink.Services.PWA
{
    /// <summary>
    /// Progressive Web App Service - Phase 2.5.1
    /// Handles PWA installation, notifications, and offline capabilities
    /// </summary>
    public class PWAService : IAsyncDisposable
    {
        private readonly IJSRuntime _jsRuntime;
        private readonly NavigationManager _navigationManager;
        private readonly ILogger<PWAService> _logger;
        private DotNetObjectReference<PWAService>? _dotNetRef;

        public event Action<bool>? OnInstallStateChanged;
        public event Action<bool>? OnOnlineStateChanged;
        public event Action<string>? OnNotificationReceived;
        public event Action? OnBeforeInstallPromptFired;

        public bool IsInstalled { get; private set; }
        public bool IsOnline { get; private set; } = true;
        public bool CanInstallNative { get; private set; }

        public PWAService(
            IJSRuntime jsRuntime,
            NavigationManager navigationManager,
            ILogger<PWAService> logger)
        {
            _jsRuntime = jsRuntime;
            _navigationManager = navigationManager;
            _logger = logger;
            _dotNetRef = DotNetObjectReference.Create(this);
        }

        /// <summary>
        /// Initialize PWA service and register event handlers
        /// </summary>
        public async Task InitializeAsync()
        {
            try
            {
                // Try to call JS - will fail silently during prerendering
                IsInstalled = await _jsRuntime.InvokeAsync<bool>("vananPWA.isInstalledFunc");
                IsOnline = await _jsRuntime.InvokeAsync<bool>("vananPWA.isOnline");
                await RegisterServiceWorkerAsync();
                await SetupEventListenersAsync();

                _logger.LogInformation("PWA Service initialized. Installed: {Installed}, Online: {Online}",
                    IsInstalled, IsOnline);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("statically rendered"))
            {
                // Prerendering mode - JS not available yet, use defaults
                _logger.LogDebug("PWA Service skipped during prerendering: {Message}", ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize PWA service");
            }
        }

        /// <summary>
        /// Register service worker
        /// </summary>
        private async Task RegisterServiceWorkerAsync()
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("vananPWA.registerServiceWorker");
                _logger.LogInformation("Service worker registered successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to register service worker");
            }
        }

        /// <summary>
        /// Setup JavaScript event listeners
        /// </summary>
        private async Task SetupEventListenersAsync()
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("vananPWA.setupEventListeners", _dotNetRef);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to setup PWA event listeners");
            }
        }

        /// <summary>
        /// Show install prompt if available
        /// </summary>
        public async Task<bool> ShowInstallPromptAsync()
        {
            try
            {
                if (IsInstalled)
                {
                    _logger.LogWarning("App is already installed");
                    return false;
                }

                bool result = await _jsRuntime.InvokeAsync<bool>("vananPWA.showInstallPrompt");

                if (result)
                {
                    IsInstalled = true;
                    OnInstallStateChanged?.Invoke(true);
                    _logger.LogInformation("PWA installation completed");
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to show install prompt");
                return false;
            }
        }

        /// <summary>
        /// Check if PWA can be installed (not already installed and not in standalone mode)
        /// </summary>
        public async Task<bool> CanInstallAsync()
        {
            try
            {
                if (IsInstalled) return false;
                bool standalone = await IsStandaloneAsync();
                return !standalone;
            }
            catch { return false; }
        }

        /// <summary>
        /// Request notification permission
        /// </summary>
        public async Task<bool> RequestNotificationPermissionAsync()
        {
            try
            {
                string permission = await _jsRuntime.InvokeAsync<string>("vananPWA.requestNotificationPermission");
                bool granted = permission == "granted";

                _logger.LogInformation("Notification permission: {Permission}", permission);
                return granted;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to request notification permission");
                return false;
            }
        }

        /// <summary>
        /// Show local notification
        /// </summary>
        public async Task ShowNotificationAsync(string title, string body, string? icon = null)
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("vananPWA.showNotification", title, body, icon);
                _logger.LogDebug("Notification shown: {Title}", title);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to show notification: {Title}", title);
            }
        }

        /// <summary>
        /// Subscribe to push notifications
        /// </summary>
        public async Task<string?> SubscribeToPushAsync()
        {
            try
            {
                string subscription = await _jsRuntime.InvokeAsync<string>("vananPWA.subscribeToPush");

                if (!string.IsNullOrEmpty(subscription))
                {
                    _logger.LogInformation("Push subscription created successfully");
                }

                return subscription;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to subscribe to push notifications");
                return null;
            }
        }

        /// <summary>
        /// Phase 5: Unsubscribe from push notifications (browser side — removes PushSubscription from SW).
        /// Server-side DELETE is called separately by Profile.razor.
        /// </summary>
        public async Task UnsubscribeFromPushAsync()
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("vananPWA.unsubscribeFromPush");
                _logger.LogInformation("Push subscription removed (browser side)");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to unsubscribe from push notifications");
            }
        }

        /// <summary>
        /// Phase 5 Session 10 (5.10): Persist notification alert prefs (sound + vibrate)
        /// to Cache API so the service worker can read them at push time.
        /// Defaults: sound=true, vibrate=true (ON when customer grants permission).
        /// </summary>
        public async Task SetNotificationPrefsAsync(bool sound, bool vibrate)
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("vananPWA.setNotificationPrefs", sound, vibrate);
                _logger.LogInformation("Notification prefs saved: sound={Sound}, vibrate={Vibrate}", sound, vibrate);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save notification prefs");
            }
        }

        /// <summary>
        /// Phase 5 Session 10 (5.10): Read notification alert prefs from Cache API.
        /// Returns { sound: true, vibrate: true } defaults on cache miss.
        /// </summary>
        public async Task<(bool Sound, bool Vibrate)> GetNotificationPrefsAsync()
        {
            try
            {
                var prefs = await _jsRuntime.InvokeAsync<NotificationPrefsJson>("vananPWA.getNotificationPrefs");
                return (prefs?.Sound ?? true, prefs?.Vibrate ?? true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read notification prefs — using defaults");
                return (true, true);
            }
        }

        /// <summary>JSON shape returned by vananPWA.getNotificationPrefs.</summary>
        private sealed class NotificationPrefsJson
        {
            public bool Sound { get; set; } = true;
            public bool Vibrate { get; set; } = true;
        }

        /// <summary>
        /// Check if app is running in standalone mode (installed PWA)
        /// </summary>
        public async Task<bool> IsStandaloneAsync()
        {
            try
            {
                return await _jsRuntime.InvokeAsync<bool>("vananPWA.isStandalone");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check standalone mode");
                return false;
            }
        }

        /// <summary>
        /// Clear all caches
        /// </summary>
        public async Task ClearCachesAsync()
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("vananPWA.clearCaches");
                _logger.LogInformation("PWA caches cleared");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clear PWA caches");
            }
        }

        // JavaScript callback methods

        [JSInvokable]
        public void HandleInstallStateChanged(bool installed)
        {
            IsInstalled = installed;
            OnInstallStateChanged?.Invoke(installed);
            _logger.LogInformation("Install state changed: {Installed}", installed);
        }

        [JSInvokable]
        public void HandleOnlineStateChanged(bool online)
        {
            IsOnline = online;
            OnOnlineStateChanged?.Invoke(online);
            _logger.LogInformation("Online state changed: {Online}", online);
        }

        [JSInvokable]
        public void HandleNotificationReceived(string message)
        {
            OnNotificationReceived?.Invoke(message);
            _logger.LogInformation("Notification received: {Message}", message);
        }

        [JSInvokable]
        public void HandleServiceWorkerUpdated()
        {
            _logger.LogInformation("Service worker updated - refresh recommended");
            // Could trigger a user notification to refresh
        }

        [JSInvokable]
        public void HandleBeforeInstallPromptFired()
        {
            CanInstallNative = true;
            OnBeforeInstallPromptFired?.Invoke();
            _logger.LogInformation("beforeinstallprompt fired â€” app is installable");
        }

        [JSInvokable]
        public void HandlePageVisible()
        {
            // Called by pwa.js when document visibility changes (tab switch/navigate back).
            // No-op for now â€” prevents circuit crash from invoking a missing JSInvokable method.
            // Future: could trigger data sync or refresh order status.
            _logger.LogDebug("Page became visible - visibilitychange event");
        }

        public async ValueTask DisposeAsync()
        {
            if (_dotNetRef != null)
            {
                _dotNetRef.Dispose();
                _dotNetRef = null;
            }
        }
    }
}
