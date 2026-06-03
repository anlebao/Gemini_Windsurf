using VanAn.Shared.Domain;

namespace VanAn.Shared.Services;

/// <summary>
/// Shopping cart management service interface
/// Phase 2.5.2: KhachLink PWA - Customer-Facing Offline-First Interface
/// </summary>
public interface ICartService
{
    Task<CartItem?> GetItemAsync(string productId);
    Task<List<CartItem>> GetItemsAsync();
    Task<bool> AddItemAsync(CartItem item);
    Task<bool> RemoveItemAsync(string productId);
    Task<bool> UpdateQuantityAsync(string productId, int quantity);
    Task<decimal> GetTotalAsync();
    Task<bool> ClearCartAsync();
    Task<CartSummary> GetSummaryAsync();
}

/// <summary>
/// Cart summary for checkout and display
/// </summary>
public record CartSummary
{
    public required int ItemCount { get; init; }
    public required decimal Subtotal { get; init; }
    public required decimal Tax { get; init; }
    public required decimal Total { get; init; }
    public required string Currency { get; init; } = "VND";
    public required List<CartItem> Items { get; init; } = new();
}
