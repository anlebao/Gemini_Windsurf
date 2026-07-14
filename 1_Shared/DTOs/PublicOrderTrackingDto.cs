namespace VanAn.Shared.DTOs
{
    /// <summary>
    /// W6/Bucket D: Limited DTO for public customer-facing order tracking.
    /// NO TenantId, NO CustomerId, NO CustomerPhone, NO address, NO internal notes.
    /// Customer already knows their own order id (they just placed it).
    /// </summary>
    public sealed class PublicOrderTrackingDto
    {
        public Guid OrderId { get; init; }
        public string Status { get; init; } = string.Empty;
        /// <summary>ISSUE #5 FIX: Expose PaymentStatus so customer can see payment confirmation on tracking page.</summary>
        public string PaymentStatus { get; init; } = string.Empty;
        public DateTime CreatedAt { get; init; }
        public decimal TotalPrice { get; init; }
        public int ItemCount { get; init; }
        public List<PublicOrderItemDto> Items { get; init; } = new();
        /// <summary>W1-T9: Tenant GUID — needed by KhachLink to fetch shop feature toggles (kitchen workflow visibility).</summary>
        public Guid TenantId { get; init; }
    }

    /// <summary>
    /// W6/Bucket D: Limited order item DTO for public tracking.
    /// NO ProductId, NO Product name (could leak inventory), NO notes.
    /// </summary>
    public sealed class PublicOrderItemDto
    {
        public int Quantity { get; init; }
        public decimal UnitPrice { get; init; }
        public decimal TotalPrice { get; init; }
    }
}
