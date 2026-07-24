using VanAn.Shared.Domain;

namespace VanAn.Shared.Services
{
    /// <summary>
    /// Loyalty-B: Redemption system service contract.
    /// Catalog CRUD + redeem flow + fulfillment + cancel (with refund).
    /// </summary>
    public interface IRedemptionService
    {
        // === Catalog CRUD (admin) ===
        Task<IReadOnlyList<RedemptionCatalogItem>> GetActiveCatalogAsync();
        Task<IReadOnlyList<RedemptionCatalogItem>> GetAllCatalogAsync();
        Task<RedemptionCatalogItem?> GetCatalogItemAsync(Guid id);
        Task<RedemptionCatalogItem> CreateCatalogItemAsync(string productName, string? description, string? imageUrl,
            int pointsRequired, int? stockCount, DateTime? validTo, int voucherExpiryDays);
        Task<RedemptionCatalogItem> UpdateCatalogItemAsync(Guid id, string productName, string? description, string? imageUrl,
            int pointsRequired, int? stockCount, DateTime? validTo, int voucherExpiryDays);
        Task<bool> DeactivateCatalogItemAsync(Guid id);
        Task<bool> DeleteCatalogItemAsync(Guid id);

        // === Redeem flow (customer) ===
        Task<RedemptionResult> RedeemAsync(Guid customerId, Guid catalogItemId);
        Task<IReadOnlyList<RedemptionRecord>> GetCustomerRedemptionsAsync(Guid customerId);
        Task<IReadOnlyList<Voucher>> GetCustomerVouchersAsync(Guid customerId);

        // === Fulfillment (admin) ===
        Task<bool> FulfillAsync(string voucherCode, string? notes = null);
        Task<bool> CancelAsync(Guid redemptionRecordId, string? notes = null);
        Task<IReadOnlyList<RedemptionRecord>> GetRecentRedemptionsAsync(int count = 50);
        Task<Voucher?> GetVoucherByCodeAsync(string voucherCode);
    }

    /// <summary>
    /// Result of a redemption attempt — carries voucher info on success, error reason on failure.
    /// </summary>
    public record RedemptionResult
    {
        public bool Success { get; init; }
        public string? Error { get; init; }
        public Voucher? Voucher { get; init; }
        public RedemptionRecord? Record { get; init; }
        public int PointsSpent { get; init; }
        public int NewPointBalance { get; init; }

        public static RedemptionResult Ok(Voucher voucher, RedemptionRecord record, int pointsSpent, int newBalance) => new()
        {
            Success = true,
            Voucher = voucher,
            Record = record,
            PointsSpent = pointsSpent,
            NewPointBalance = newBalance
        };

        public static RedemptionResult Fail(string error) => new() { Success = false, Error = error };
    }
}
