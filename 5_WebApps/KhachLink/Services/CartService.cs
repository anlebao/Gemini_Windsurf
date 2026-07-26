using Microsoft.JSInterop;
using System.Text.Json;
using VanAn.KhachLink.Models;
using VanAn.Shared.Domain;
using VanAn.Shared.DTOs;

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
                        // Issue 4: Restore order note from localStorage
                        _cartState.OrderNote = cart.OrderNote ?? string.Empty;
                    }
                }
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("statically rendered"))
            {
                // Prerendering mode - JS not available yet, cart will be loaded after hydration
                Console.WriteLine("Cart storage skipped during prerendering");
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
            catch (InvalidOperationException ex) when (ex.Message.Contains("statically rendered"))
            {
                // Prerendering mode - JS not available yet
                Console.WriteLine("Cart save skipped during prerendering");
            }
            catch (Exception ex)
            {
                // Handle storage errors gracefully
                Console.WriteLine($"Error saving cart to storage: {ex.Message}");
            }
        }

        public async Task AddItemAsync(ProductDto product, int quantity = 1)
        {
            _cartState.AddItem(product, quantity);
            await SaveCartToStorageAsync();
            NotifyCartChanged();
        }

        /// <summary>
        /// Phase 5: Add a pre-constructed CartItem directly (used by partial cart clear
        /// after multi-tenant checkout â€” items from failed tenants are re-added for retry).
        /// </summary>
        public async Task AddItemAsync(CartItem item)
        {
            _cartState.Items.Add(item);
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
            _cartState.OrderNote = string.Empty;
            await SaveCartToStorageAsync();
            NotifyCartChanged();
        }

        /// <summary>Issue 4: Update order note (entered on cart page, sent at checkout).</summary>
        public async Task UpdateOrderNoteAsync(string note)
        {
            _cartState.OrderNote = note ?? string.Empty;
            await SaveCartToStorageAsync();
            NotifyCartChanged();
        }

        private void NotifyCartChanged()
        {
            OnCartChanged?.Invoke();
        }
    }
}
