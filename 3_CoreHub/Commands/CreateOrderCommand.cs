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
    }

    public class OrderItemRequest
    {
        public Guid ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }
}
