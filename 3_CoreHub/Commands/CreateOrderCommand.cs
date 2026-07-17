namespace VanAn.CoreHub.Commands
{
    /// <summary>
    /// Command for creating new orders - Application Layer
    /// Phase 2.5.4: Unified API Integration - Single Backend Service
    /// Clean Architecture: Application Commands belong in CoreHub layer
    /// </summary>
    public class CreateOrderCommand
    {
        public Guid CustomerDeviceId { get; set; }
        public List<OrderItemRequest> Items { get; set; } = [];

        // Bucket A feature (approved 2026-07-07): Guest checkout customer info.
        // Nullable — anonymous checkout (no form) still supported for backward compat.
        public string? CustomerName { get; set; }
        public string? CustomerPhone { get; set; }
        public string? CustomerAddress { get; set; }

        // Logged-in customer: when set, Order.CustomerId is linked so the order
        // appears in the customer's order history (/api/customerorders).
        // Null for anonymous/guest checkout.
        public Guid? CustomerId { get; set; }
    }

    public class OrderItemRequest
    {
        public Guid ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }
}
