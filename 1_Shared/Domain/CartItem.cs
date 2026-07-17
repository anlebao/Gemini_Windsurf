namespace VanAn.Shared.Domain
{
    /// <summary>
    /// Shopping cart item entity
    /// Phase 2.5.2: KhachLink PWA - Customer-Facing Offline-First Interface
    /// </summary>
    public record CartItem
    {
        public required Guid Id { get; init; }
        public required Guid ProductId { get; init; }
        public required string ProductName { get; init; } = string.Empty;
        public required string Description { get; init; } = string.Empty;
        public required int Quantity { get; init; }
        public required decimal UnitPrice { get; init; }
        public decimal VatRate { get; init; } = 0.10m;  // RC-7: snapshot from Product

        // Computed properties
        public decimal TotalPrice => Quantity * UnitPrice;
        // VAT-inclusive extraction: UnitPrice is gross, net = gross / (1 + rate)
        public decimal VatAmount => TotalPrice - (TotalPrice / (1 + VatRate));
        public decimal NetAmount => TotalPrice - VatAmount;
    }
}
