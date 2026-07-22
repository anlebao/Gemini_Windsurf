namespace VanAn.Shared.DTOs
{
    /// <summary>
    /// R2-0c: Shared display DTO for order items â€” used by OrderSummaryCard component.
    /// Decouples UI.Platform from domain entities and page-specific DTOs.
    /// Consumers map their items (CartItem, OrderItem, OrderItemDto) to this DTO.
    /// </summary>
    public sealed class OrderItemDisplayDto
    {
        public string ProductName { get; init; } = string.Empty;
        public int Quantity { get; init; }
        public decimal UnitPrice { get; init; }
        public decimal TotalAmount { get; init; }
    }
}
