namespace VanAn.Gateway.Controllers
{
    /// <summary>
    /// Phase 3 (Multi-VPS Checkout): Response for POST /api/public/orders/checkout.
    /// Supports multi-tenant grouping — cart with items from N tenants creates N orders.
    /// Partial failure: some orders succeed, some fail — all results returned in one response.
    /// </summary>
    public class CheckoutResponse
    {
        public List<CreatedOrderDto> Orders { get; set; } = [];
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public List<CheckoutErrorDto> Errors { get; set; } = [];
    }

    public class CreatedOrderDto
    {
        public Guid OrderId { get; set; }
        public Guid TenantId { get; set; }
        public decimal Amount { get; set; }
        public decimal SubTotal { get; set; }
        public decimal TotalVatAmount { get; set; }
    }

    public class CheckoutErrorDto
    {
        public Guid TenantId { get; set; }
        public string Error { get; set; } = string.Empty;
    }
}
