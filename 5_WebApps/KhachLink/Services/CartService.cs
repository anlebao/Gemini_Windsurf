using Microsoft.JSInterop;
using System.Text.Json;
using VanAn.Shared.Domain;

namespace VanAn.KhachLink.Services
{
    public class CartService(IJSRuntime jsRuntime)
    {
        private readonly IJSRuntime _jsRuntime = jsRuntime;
        private readonly CartState _cartState = new();

        public event Action? OnCartChanged;

        public CartState GetCartState()
        {
            return _cartState;
        }

        public async Task LoadCartFromStorageAsync()
        {
            try
            {
                string cartJson = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", "vanan_cart");
                if (!string.IsNullOrEmpty(cartJson))
                {
                    CartState? cart = JsonSerializer.Deserialize<CartState>(cartJson);
                    if (cart != null)
                    {
                        _cartState.Items.Clear();
                        _cartState.Items.AddRange(cart.Items);
                    }
                }
            }
            catch (Exception ex)
            {
                // Handle storage errors gracefully
                Console.WriteLine($"Error loading cart from storage: {ex.Message}");
            }
        }

        public async Task SaveCartToStorageAsync()
        {
            try
            {
                string cartJson = JsonSerializer.Serialize(_cartState);
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "vanan_cart", cartJson);
            }
            catch (Exception ex)
            {
                // Handle storage errors gracefully
                Console.WriteLine($"Error saving cart to storage: {ex.Message}");
            }
        }

        public async Task AddItemAsync(Product product, int quantity = 1)
        {
            _cartState.AddItem(product, quantity);
            await SaveCartToStorageAsync();
            NotifyCartChanged();
        }

        public async Task RemoveItemAsync(Guid productId)
        {
            _cartState.RemoveItem(productId);
            await SaveCartToStorageAsync();
            NotifyCartChanged();
        }

        public async Task UpdateQuantityAsync(Guid productId, int quantity)
        {
            _cartState.UpdateQuantity(productId, quantity);
            await SaveCartToStorageAsync();
            NotifyCartChanged();
        }

        public async Task ClearCartAsync()
        {
            _cartState.Clear();
            await SaveCartToStorageAsync();
            NotifyCartChanged();
        }

        private void NotifyCartChanged()
        {
            OnCartChanged?.Invoke();
        }
    }
}
