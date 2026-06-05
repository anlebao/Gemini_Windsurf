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

        public bool IsInstalled { get; private set; }
        public bool IsOnline { get; private set; } = true;

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
                // Check if app is already installed
                IsInstalled = await _jsRuntime.InvokeAsync<bool>("vananPWA.isInstalled");

                // Check online status
                IsOnline = await _jsRuntime.InvokeAsync<bool>("vananPWA.isOnline");

                // Register service worker
                await RegisterServiceWorkerAsync();

                // Setup event listeners
                await SetupEventListenersAsync();

                _logger.LogInformation("PWA Service initialized. Installed: {Installed}, Online: {Online}",
                    IsInstalled, IsOnline);
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
