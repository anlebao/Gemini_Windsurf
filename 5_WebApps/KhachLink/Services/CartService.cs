using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System.Text.Json;
using VanAn.Shared.Domain;

namespace VanAn.KhachLink.Services;

public class CartService
{
    private readonly IJSRuntime _jsRuntime;
    private readonly CartState _cartState = new();
    
    public event Action? OnCartChanged;
    
    public CartService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }
    
    public CartState GetCartState()
    {
        return _cartState;
    }
    
    public async Task LoadCartFromStorageAsync()
    {
        try
        {
            var cartJson = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", "vanan_cart");
            if (!string.IsNullOrEmpty(cartJson))
            {
                var cart = JsonSerializer.Deserialize<CartState>(cartJson);
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
            var cartJson = JsonSerializer.Serialize(_cartState);
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
