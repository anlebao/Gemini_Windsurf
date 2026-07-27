using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using QRCoder;
using VanAn.CoreHub.Domain.Repositories;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Common;
using VanAn.Shared.Services;

namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// Loyalty-B: Redemption system service implementation.
    /// Catalog CRUD + redeem flow (SubtractPointsAsync → RedemptionRecord + Voucher) + fulfillment + cancel (refund).
    /// Storage: ShopERP SQLite (tenant-scoped).
    /// ACID: RedeemAsync wraps all steps in a single transaction via IVanAnDbContext.
    ///   SubtractPointsAsync uses the same DbContext (scoped DI) → nested transaction = savepoint.
    ///   If any step after deduction fails → rollback (points deduction also rolled back).
    /// </summary>
    public class RedemptionService(
        IRedemptionRepository repository,
        ILoyaltyRewardsService loyaltyRewardsService,
        ITenantProvider tenantProvider,
        IVanAnDbContext dbContext,
        IShopFeatureSettingsService? shopFeatureSettingsService,
        PushNotificationService? pushNotificationService,
        ILogger<RedemptionService> logger) : IRedemptionService
    {
        private readonly IRedemptionRepository _repository = repository;
        private readonly ILoyaltyRewardsService _loyaltyRewardsService = loyaltyRewardsService;
        private readonly ITenantProvider _tenantProvider = tenantProvider;
        private readonly IVanAnDbContext _dbContext = dbContext;
        private readonly IShopFeatureSettingsService? _shopFeatureSettingsService = shopFeatureSettingsService;
        private readonly PushNotificationService? _pushNotificationService = pushNotificationService;
        private readonly ILogger<RedemptionService> _logger = logger;

        // === Catalog CRUD (admin) ===

        public Task<IReadOnlyList<RedemptionCatalogItem>> GetActiveCatalogAsync()
            => _repository.GetActiveCatalogItemsAsync();

        public Task<IReadOnlyList<RedemptionCatalogItem>> GetAllCatalogAsync()
            => _repository.GetAllCatalogItemsAsync();

        public Task<RedemptionCatalogItem?> GetCatalogItemAsync(Guid id)
            => _repository.GetCatalogItemByIdAsync(id);

        public async Task<RedemptionCatalogItem> CreateCatalogItemAsync(string productName, string? description, string? imageUrl,
            int pointsRequired, int? stockCount, DateTime? validTo, int voucherExpiryDays)
        {
            var item = new RedemptionCatalogItem(new TenantId(_tenantProvider.TenantId), productName, pointsRequired);
            item.UpdateDetails(productName, description, imageUrl, pointsRequired, stockCount, validTo, voucherExpiryDays);
            return await _repository.AddCatalogItemAsync(item);
        }

        public async Task<RedemptionCatalogItem> UpdateCatalogItemAsync(Guid id, string productName, string? description, string? imageUrl,
            int pointsRequired, int? stockCount, DateTime? validTo, int voucherExpiryDays)
        {
            var item = await _repository.GetCatalogItemByIdAsync(id)
                ?? throw new KeyNotFoundException($"Catalog item {id} not found.");
            item.UpdateDetails(productName, description, imageUrl, pointsRequired, stockCount, validTo, voucherExpiryDays);
            return await _repository.UpdateCatalogItemAsync(item);
        }

        public async Task<bool> DeactivateCatalogItemAsync(Guid id)
        {
            var item = await _repository.GetCatalogItemByIdAsync(id);
            if (item == null) return false;
            item.Deactivate();
            _ = await _repository.UpdateCatalogItemAsync(item);
            return true;
        }

        public async Task<bool> DeleteCatalogItemAsync(Guid id)
            => await _repository.SoftDeleteCatalogItemAsync(id);

        // === Redeem flow (customer) ===

        public async Task<RedemptionResult> RedeemAsync(Guid customerId, Guid catalogItemId)
        {
            var catalogItem = await _repository.GetCatalogItemByIdAsync(catalogItemId);
            if (catalogItem == null)
            {
                _logger.LogWarning("Redeem failed: catalog item {CatalogItemId} not found", catalogItemId);
                return RedemptionResult.Fail("Sản phẩm đổi điểm không tồn tại.");
            }

            if (!catalogItem.IsAvailable)
            {
                _logger.LogWarning("Redeem failed: catalog item {CatalogItemId} not available (IsActive={Active}, Stock={Stock})",
                    catalogItemId, catalogItem.IsActive, catalogItem.StockCount);
                return RedemptionResult.Fail("Sản phẩm hiện không khả dụng (hết hàng hoặc hết hạn).");
            }

            // ACID: Wrap entire redeem flow in a single transaction.
            // SubtractPointsAsync uses the same IVanAnDbContext (scoped DI) → its internal
            // BeginTransactionAsync creates a savepoint within this outer transaction.
            // If any step fails → rollback undoes points deduction + record + voucher (atomic).
            await using IDbContextTransaction transaction = await _dbContext.BeginTransactionAsync();
            try
            {
                // SubtractPointsAsync enforces IdentityLevel >= Verified + balance check.
                bool deducted = await _loyaltyRewardsService.SubtractPointsAsync(customerId, catalogItem.PointsRequired, $"Redeem: {catalogItem.ProductName}");
                if (!deducted)
                {
                    _logger.LogWarning("Redeem failed: insufficient points for customer {CustomerId} (needed {Points})",
                        customerId, catalogItem.PointsRequired);
                    await transaction.RollbackAsync();
                    return RedemptionResult.Fail("Không đủ điểm để đổi sản phẩm này.");
                }

                // Create RedemptionRecord (Pending)
                var record = new RedemptionRecord(new TenantId(_tenantProvider.TenantId), customerId, catalogItemId, catalogItem.PointsRequired);
                record = await _repository.AddRecordAsync(record);

                // Generate voucher code + QR
                string voucherCode = GenerateVoucherCode();
                DateTime expiresAt = DateTime.UtcNow.AddDays(catalogItem.VoucherExpiryDays);
                var voucher = new Voucher(new TenantId(_tenantProvider.TenantId), record.Id, customerId, voucherCode, expiresAt);
                string qrData = GenerateVoucherQrPngBase64(voucherCode);
                voucher.SetQRCodeData(qrData);
                voucher = await _repository.AddVoucherAsync(voucher);

                // Link voucher to record
                record.AssignVoucher(voucher.Id);
                _ = await _repository.UpdateRecordAsync(record);

                // Decrement stock (if tracked)
                if (catalogItem.StockCount.HasValue)
                {
                    catalogItem.DecrementStock();
                    _ = await _repository.UpdateCatalogItemAsync(catalogItem);
                }

                await transaction.CommitAsync();

                // Read new balance for response (after commit — reflects final state)
                var rewards = await _loyaltyRewardsService.GetCustomerRewardsAsync(customerId);
                int newBalance = rewards?.PointBalance ?? 0;

                _logger.LogInformation("Redeem success: customer {CustomerId} redeemed {Points} points for catalog item {CatalogItemId}. Voucher {VoucherCode}",
                    customerId, catalogItem.PointsRequired, catalogItemId, voucherCode);

                return RedemptionResult.Ok(voucher, record, catalogItem.PointsRequired, newBalance);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Redeem failed with exception for customer {CustomerId}, catalog item {CatalogItemId}. Rolling back transaction.",
                    customerId, catalogItemId);
                await transaction.RollbackAsync();
                return RedemptionResult.Fail("Lỗi hệ thống khi đổi điểm. Vui lòng thử lại.");
            }
        }

        public Task<IReadOnlyList<RedemptionRecord>> GetCustomerRedemptionsAsync(Guid customerId)
            => _repository.GetRecordsByCustomerAsync(customerId);

        public Task<IReadOnlyList<Voucher>> GetCustomerVouchersAsync(Guid customerId)
            => _repository.GetVouchersByCustomerAsync(customerId);

        // === Fulfillment (admin) ===

        public async Task<bool> FulfillAsync(string voucherCode, string? notes = null)
        {
            var voucher = await _repository.GetVoucherByCodeAsync(voucherCode);
            if (voucher == null)
            {
                _logger.LogWarning("Fulfill failed: voucher {VoucherCode} not found", voucherCode);
                return false;
            }

            if (voucher.Status == "Used")
            {
                _logger.LogWarning("Fulfill failed: voucher {VoucherCode} already used", voucherCode);
                return false;
            }

            if (!voucher.IsValid)
            {
                _logger.LogWarning("Fulfill failed: voucher {VoucherCode} expired", voucherCode);
                voucher.MarkAsExpired();
                _ = await _repository.UpdateVoucherAsync(voucher);
                return false;
            }

            voucher.MarkAsUsed();
            _ = await _repository.UpdateVoucherAsync(voucher);

            // Mark redemption record as fulfilled
            var record = await _repository.GetRecordByIdAsync(voucher.RedemptionRecordId);
            string? productName = null;
            if (record != null)
            {
                record.MarkAsFulfilled(notes);
                _ = await _repository.UpdateRecordAsync(record);
                // Look up catalog item for product name (notification personalization)
                var catalogItem = await _repository.GetCatalogItemByIdAsync(record.CatalogItemId);
                productName = catalogItem?.ProductName;
            }

            // Loyalty-C WS-C: Send redemption fulfilled push notification (if toggle enabled)
            try
            {
                if (_shopFeatureSettingsService != null && _pushNotificationService != null && record != null)
                {
                    var settings = await _shopFeatureSettingsService.GetSettingsAsync(record.TenantId);
                    if (settings.Notify_RedemptionFulfilled)
                    {
                        _ = await _pushNotificationService.SendRedemptionFulfilledNotificationAsync(
                            voucher.CustomerId, voucher.VoucherCode, productName);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send redemption fulfilled notification for voucher {VoucherCode}", voucherCode);
            }

            _logger.LogInformation("Fulfill success: voucher {VoucherCode} fulfilled", voucherCode);
            return true;
        }

        public async Task<bool> CancelAsync(Guid redemptionRecordId, string? notes = null)
        {
            var record = await _repository.GetRecordByIdAsync(redemptionRecordId);
            if (record == null)
            {
                _logger.LogWarning("Cancel failed: redemption record {RecordId} not found", redemptionRecordId);
                return false;
            }

            if (record.Status != "Pending")
            {
                _logger.LogWarning("Cancel failed: redemption record {RecordId} status is {Status} (not Pending)", redemptionRecordId, record.Status);
                return false;
            }

            // Refund points
            _ = await _loyaltyRewardsService.AddPointsAsync(record.CustomerId, record.PointsSpent, $"Refund: cancelled redemption {redemptionRecordId}");

            // Mark record as cancelled
            record.MarkAsCancelled(notes);
            _ = await _repository.UpdateRecordAsync(record);

            // Mark voucher as expired (if exists)
            Voucher? cancelledVoucher = null;
            if (record.VoucherId.HasValue)
            {
                cancelledVoucher = await _repository.GetVoucherByIdAsync(record.VoucherId.Value);
                if (cancelledVoucher != null && cancelledVoucher.Status == "Active")
                {
                    cancelledVoucher.MarkAsExpired();
                    _ = await _repository.UpdateVoucherAsync(cancelledVoucher);
                }
            }

            // Loyalty-C WS-C: Send redemption cancelled push notification (if toggle enabled)
            try
            {
                if (_shopFeatureSettingsService != null && _pushNotificationService != null)
                {
                    var settings = await _shopFeatureSettingsService.GetSettingsAsync(record.TenantId);
                    if (settings.Notify_RedemptionCancelled)
                    {
                        _ = await _pushNotificationService.SendRedemptionCancelledNotificationAsync(
                            record.CustomerId, cancelledVoucher?.VoucherCode ?? "", record.PointsSpent);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send redemption cancelled notification for record {RecordId}", redemptionRecordId);
            }

            _logger.LogInformation("Cancel success: redemption record {RecordId} cancelled + {Points} points refunded",
                redemptionRecordId, record.PointsSpent);
            return true;
        }

        public Task<IReadOnlyList<RedemptionRecord>> GetRecentRedemptionsAsync(int count = 50)
            => _repository.GetRecentRecordsAsync(count);

        public Task<Voucher?> GetVoucherByCodeAsync(string voucherCode)
            => _repository.GetVoucherByCodeAsync(voucherCode);

        // === Helpers ===

        private static string GenerateVoucherCode()
        {
            // Format: VAN-XXXXXXXX (8 random alphanumeric uppercase chars)
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // no ambiguous chars (0/O, 1/I)
            var random = new Random();
            var code = new string(Enumerable.Range(0, 8).Select(_ => chars[random.Next(chars.Length)]).ToArray());
            return $"VAN-{code}";
        }

        private static string GenerateVoucherQrPngBase64(string voucherCode)
        {
            using var qrGenerator = new QRCodeGenerator();
            var qrCodeData = qrGenerator.CreateQrCode(voucherCode, QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new PngByteQRCode(qrCodeData);
            byte[] png = qrCode.GetGraphic(20);
            return Convert.ToBase64String(png);
        }
    }
}
