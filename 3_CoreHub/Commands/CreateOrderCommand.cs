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

        /// <summary>Issue 4: Customer note for the order (e.g. "ít đá", "giao trước 12h"). Saved to Order.CustomerNotes.</summary>
        public string? CustomerNotes { get; set; }
    }

    public class OrderItemRequest
    {
        public Guid ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }

        // Phase 3 (Multi-VPS Checkout): Client snapshot — Gateway creates order WITHOUT querying Products table.
        // TenantId: for multi-tenant grouping + routing key (ShopInstanceId lookup).
        // ProductName + VatRate: snapshot from QR code / catalog at scan time.
        // Backward compat: if ProductName is empty, OrderService falls back to LoadProductsForSnapshotAsync.
        public Guid TenantId { get; set; }
        public string ProductName { get; set; } = "";
        public decimal VatRate { get; set; } = 0.10m;
    }
}
