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

        // Phase 5: Tenant that owns this product. Required for multi-tenant cart â†’ multi-order checkout.
        // Defaults to Guid.Empty so legacy/uninitialized CartItem objects remain valid (no `required` modifier).
        // Validation (TenantId != Guid.Empty) happens at checkout.
        public Guid TenantId { get; init; } = Guid.Empty;

        // Tenant display name â€” resolved from PG Tenants table at checkout/tracking time.
        // Stored in cart so Cart/Checkout pages can show "Cá»­a hÃ ng: {name}" instead of raw GUID.
        // Empty string for legacy items (resolved lazily at checkout response).
        public string TenantName { get; init; } = string.Empty;

        // Computed properties
        public decimal TotalPrice => Quantity * UnitPrice;
        // VAT-inclusive extraction: UnitPrice is gross, net = gross / (1 + rate)
        public decimal VatAmount => TotalPrice - (TotalPrice / (1 + VatRate));
        public decimal NetAmount => TotalPrice - VatAmount;
    }
}
