// NAMESPACE STRATEGY
using Microsoft.JSInterop;

namespace VanAn.KhachLink.Services
{
    public interface IIndexedDBService
    {
        Task<bool> InitializeAsync();
        Task<T?> GetAsync<T>(string key) where T : class;
        Task SetAsync<T>(string key, T value) where T : class;
        Task RemoveAsync(string key);
        Task ClearAsync();
        Task<List<T>> GetAllAsync<T>() where T : class;
    }

    public class IndexedDBService(IJSRuntime jsRuntime, ILogger<IndexedDBService> logger) : IIndexedDBService, IAsyncDisposable
    {
        private readonly IJSRuntime _jsRuntime = jsRuntime;
        private readonly ILogger<IndexedDBService> _logger = logger;
        private bool _isInitialized;

        // Database schema constants
        private const string DB_NAME = "VanAnKhachLink";
        private const string DB_VERSION = "1";
        private const string ORDERS_STORE = "orders";
        private const string CART_STORE = "cart";
        private const string PRODUCTS_STORE = "products";

        public async Task<bool> InitializeAsync()
        {
            if (_isInitialized)
            {
                return true;
            }

            try
            {
                bool result = await _jsRuntime.InvokeAsync<bool>("vananIndexedDB.initialize", DB_NAME, DB_VERSION);
                _isInitialized = result;
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize IndexedDB");
                return false;
            }
        }

        public async Task<T?> GetAsync<T>(string key) where T : class
        {
            if (!await InitializeAsync())
            {
                return null;
            }

            try
            {
                return await _jsRuntime.InvokeAsync<T>("vananIndexedDB.get", key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get item from IndexedDB: {Key}", key);
                return null;
            }
        }

        public async Task SetAsync<T>(string key, T value) where T : class
        {
            if (!await InitializeAsync())
            {
                return;
            }

            try
            {
                await _jsRuntime.InvokeVoidAsync("vananIndexedDB.set", key, value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set item in IndexedDB: {Key}", key);
            }
        }

        public async Task RemoveAsync(string key)
        {
            if (!await InitializeAsync())
            {
                return;
            }

            try
            {
                await _jsRuntime.InvokeVoidAsync("vananIndexedDB.remove", key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to remove item from IndexedDB: {Key}", key);
            }
        }

        public async Task ClearAsync()
        {
            if (!await InitializeAsync())
            {
                return;
            }

            try
            {
                await _jsRuntime.InvokeVoidAsync("vananIndexedDB.clear");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clear IndexedDB");
            }
        }

        public async Task<List<T>> GetAllAsync<T>() where T : class
        {
            if (!await InitializeAsync())
            {
                return [];
            }

            try
            {
                return await _jsRuntime.InvokeAsync<List<T>>("vananIndexedDB.getAll");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get all items from IndexedDB");
                return [];
            }
        }

        public async ValueTask DisposeAsync()
        {
            // No resources to dispose for IndexedDB service
            await Task.CompletedTask;
        }
    }

}
