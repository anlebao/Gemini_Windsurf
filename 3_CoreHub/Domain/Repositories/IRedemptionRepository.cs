using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Domain.Repositories
{
    /// <summary>
    /// Loyalty-B: Repository for redemption system (catalog items, records, vouchers).
    /// ShopERP SQLite (tenant-scoped).
    /// </summary>
    public interface IRedemptionRepository
    {
        // === Catalog Items ===
        Task<RedemptionCatalogItem?> GetCatalogItemByIdAsync(Guid id);
        Task<IReadOnlyList<RedemptionCatalogItem>> GetActiveCatalogItemsAsync();
        Task<IReadOnlyList<RedemptionCatalogItem>> GetAllCatalogItemsAsync();
        Task<RedemptionCatalogItem> AddCatalogItemAsync(RedemptionCatalogItem item);
        Task<RedemptionCatalogItem> UpdateCatalogItemAsync(RedemptionCatalogItem item);
        Task<bool> SoftDeleteCatalogItemAsync(Guid id);

        // === Redemption Records ===
        Task<RedemptionRecord?> GetRecordByIdAsync(Guid id);
        Task<IReadOnlyList<RedemptionRecord>> GetRecordsByCustomerAsync(Guid customerId);
        Task<IReadOnlyList<RedemptionRecord>> GetRecentRecordsAsync(int count = 50);
        Task<RedemptionRecord> AddRecordAsync(RedemptionRecord record);
        Task<RedemptionRecord> UpdateRecordAsync(RedemptionRecord record);

        // === Vouchers ===
        Task<Voucher?> GetVoucherByIdAsync(Guid id);
        Task<Voucher?> GetVoucherByCodeAsync(string voucherCode);
        Task<IReadOnlyList<Voucher>> GetVouchersByCustomerAsync(Guid customerId);
        Task<IReadOnlyList<Voucher>> GetVouchersExpiringWithinAsync(int days);
        Task<Voucher> AddVoucherAsync(Voucher voucher);
        Task<Voucher> UpdateVoucherAsync(Voucher voucher);

        // === Save ===
        Task<int> SaveChangesAsync();
    }
}
