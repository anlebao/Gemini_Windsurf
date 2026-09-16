using System.Text.Json.Serialization;

namespace VanAn.KhachLink.Models
{
    /// <summary>
    /// Phase 5: CheckoutResponse shape (matches Gateway CheckoutResponse DTO).
    /// Shared between Checkout.razor + KhachLinkLayout (Phase B direct free-order flow).
    /// </summary>
    public class CheckoutResponseResult
    {
        [JsonPropertyName("orders")]
        public List<CreatedOrderItem> Orders { get; set; } = new();
        [JsonPropertyName("successCount")]
        public int SuccessCount { get; set; }
        [JsonPropertyName("failureCount")]
        public int FailureCount { get; set; }
        [JsonPropertyName("errors")]
        public List<CheckoutErrorItem> Errors { get; set; } = new();
    }

    public class CreatedOrderItem
    {
        [JsonPropertyName("orderId")]
        public string OrderId { get; set; } = string.Empty;
        [JsonPropertyName("tenantId")]
        public Guid TenantId { get; set; }
        [JsonPropertyName("tenantName")]
        public string TenantName { get; set; } = string.Empty;
        [JsonPropertyName("amount")]
        public decimal Amount { get; set; }
        [JsonPropertyName("subTotal")]
        public decimal SubTotal { get; set; }
        [JsonPropertyName("totalVatAmount")]
        public decimal TotalVatAmount { get; set; }
    }

    public class CheckoutErrorItem
    {
        [JsonPropertyName("tenantId")]
        public Guid TenantId { get; set; }
        [JsonPropertyName("tenantName")]
        public string TenantName { get; set; } = string.Empty;
        [JsonPropertyName("error")]
        public string Error { get; set; } = string.Empty;
    }
}
