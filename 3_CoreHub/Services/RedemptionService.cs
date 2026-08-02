using Microsoft.EntityFrameworkCore;
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
    /// Loyalty Alliance Phase 2C: Mode routing — Alliance mode deducts from PG AllianceWallet
    ///   instead of local LoyaltyRewards. Voucher + RedemptionRecord still created in local SQLite.
    /// </summary>
    public class RedemptionService(
        IRedemptionRepository repository,
        ILoyaltyRewardsService loyaltyRewardsService,
        ITenantProvider tenantProvider,
        IVanAnDbContext dbContext,
        IShopFeatureSettingsService? shopFeatureSettingsService,
        PushNotificationService? pushNotificationService,
        ILogger<RedemptionService> logger,
        ILoyaltyModeResolver? loyaltyModeResolver = null,
        IAllianceWalletService? allianceWalletService = null) : IRedemptionService
    {
        private readonly IRedemptionRepository _repository = repository;
        private readonly ILoyaltyRewardsService _loyaltyRewardsService = loyaltyRewardsService;
        private readonly ITenantProvider _tenantProvider = tenantProvider;
        private readonly IVanAnDbContext _dbContext = dbContext;
        private readonly IShopFeatureSettingsService? _shopFeatureSettingsService = shopFeatureSettingsService;
        private readonly PushNotificationService? _pushNotificationService = pushNotificationService;
        private readonly ILogger<RedemptionService> _logger = logger;
        // Loyalty Alliance Phase 2C: mode resolver + cross-tenant wallet (null in Silo-only deployments)
        private readonly ILoyaltyModeResolver? _loyaltyModeResolver = loyaltyModeResolver;
        private readonly IAllianceWalletService? _allianceWalletService = allianceWalletService;

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

            // === Loyalty Alliance Phase 2C: Mode routing ===
            // If mode=Alliance + tenant is a member, deduct from PG AllianceWallet instead of local SQLite.
            // Voucher + RedemptionRecord are still created in local SQLite (same as Silo).
            // If mode=Silo, or tenant opted out, fall through to existing Silo flow.
            if (_loyaltyModeResolver is not null && _allianceWalletService is not null)
            {
                Guid tenantId = _tenantProvider.TenantId;
                LoyaltyMode effectiveMode = await _loyaltyModeResolver.GetEffectiveModeAsync(tenantId);
                if (effectiveMode == LoyaltyMode.Alliance)
                {
                    bool isMember = await _loyaltyModeResolver.IsAllianceMemberAsync(tenantId);
                    if (!isMember)
                    {
                        return RedemptionResult.Fail("Tenant không tham gia liên minh điểm thưởng.");
                    }

                    // Resolve customer's DeviceId for wallet lookup
                    var customer = await _dbContext.Customers.FirstOrDefaultAsync(c => c.Id == customerId);
                    Guid deviceGuid = customer?.DeviceId ?? customerId;

                    // Pre-generate voucher code (needed for both deduction reason + voucher entity)
                    string voucherCode = GenerateVoucherCode();
                    DateTime expiresAt = DateTime.UtcNow.AddDays(catalogItem.VoucherExpiryDays);

                    // Alliance REDEEM: deduct from PG wallet (atomic per AllianceWalletService)
                    // Idempotency key uses voucherCode — generated BEFORE deduction, unique per redeem attempt.
                    // If HTTP call retries with same voucherCode, Gateway returns cached result (no double deduction).
                    var (success, newBalance, error) = await _allianceWalletService.DeductPointsAsync(
                        deviceGuid, tenantId, catalogItem.PointsRequired,
                        $"Redeem: {catalogItem.ProductName}", voucherCode,
                        idempotencyKey: $"redeem:{voucherCode}");

                    if (!success)
                    {
                        _logger.LogWarning("Alliance redeem failed for customer {CustomerId}: {Error}", customerId, error);
                        return RedemptionResult.Fail(error ?? "Không đủ điểm để đổi sản phẩm này.");
                    }

                    // Create RedemptionRecord + Voucher in local SQLite (same as Silo, but no local deduction)
                    await using IDbContextTransaction allianceTx = await _dbContext.BeginTransactionAsync();
                    try
                    {
                        var record = new RedemptionRecord(new TenantId(tenantId), customerId, catalogItemId, catalogItem.PointsRequired);
                        record = await _repository.AddRecordAsync(record);

                        var voucher = new Voucher(new TenantId(tenantId), record.Id, customerId, voucherCode, expiresAt);
                        string qrData = GenerateVoucherQrPngBase64(voucherCode);
                        voucher.SetQRCodeData(qrData);
                        voucher = await _repository.AddVoucherAsync(voucher);

                        record.AssignVoucher(voucher.Id);
                        _ = await _repository.UpdateRecordAsync(record);

                        if (catalogItem.StockCount.HasValue)
                        {
                            catalogItem.DecrementStock();
                            _ = await _repository.UpdateCatalogItemAsync(catalogItem);
                        }

                        await allianceTx.CommitAsync();

                        _logger.LogInformation("🎁 ALLIANCE REDEEM: customer {CustomerId} redeemed {Points} points from PG wallet (new balance={Balance}). Voucher {VoucherCode}",
                            customerId, catalogItem.PointsRequired, newBalance, voucherCode);

                        return RedemptionResult.Ok(voucher, record, catalogItem.PointsRequired, newBalance);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Alliance redeem: PG deduction succeeded but local voucher creation failed for customer {CustomerId}. Rolling back local transaction. PG deduction NOT refunded (manual reconcile needed).",
                            customerId);
                        await allianceTx.RollbackAsync();
                        return RedemptionResult.Fail("Lỗi hệ thống khi tạo voucher. Điểm đã trừ — liên hệ hỗ trợ.");
                    }
                }
            }

            // === EXISTING: Silo flow (unchanged) ===
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

            // Loyalty Consistency Fix Phase 1 (BUG #2): mode routing for refund.
            // Restructure: fetch voucher FIRST (needed for refund audit trail), then route refund by mode.

            // 1. Fetch voucher first (needed for refund audit + expiry marking)
            Voucher? cancelledVoucher = null;
            if (record.VoucherId.HasValue)
            {
                cancelledVoucher = await _repository.GetVoucherByIdAsync(record.VoucherId.Value);
            }

            // 2. Refund points (route by mode — PG AllianceWallet in Alliance mode, SQLite in Silo)
            _ = await RefundPointsWithModeRoutingAsync(
                record.CustomerId, record.TenantId.Value, record.PointsSpent,
                $"Refund: cancelled redemption {redemptionRecordId}",
                cancelledVoucher?.VoucherCode,
                idempotencyKey: $"refund:{record.Id}");

            // 3. Mark record as cancelled
            record.MarkAsCancelled(notes);
            _ = await _repository.UpdateRecordAsync(record);

            // 4. Mark voucher as expired
            if (cancelledVoucher != null && cancelledVoucher.Status == "Active")
            {
                cancelledVoucher.MarkAsExpired();
                _ = await _repository.UpdateVoucherAsync(cancelledVoucher);
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

        /// <summary>
        /// Loyalty Consistency Fix Phase 1 (BUG #2): Route point refund to PG AllianceWallet (Alliance mode) or SQLite (Silo mode).
        /// Uses IAllianceWalletService.RefundAsync in Alliance mode (idempotent by recordId), AddPointsAsync in Silo mode.
        /// </summary>
        private async Task<bool> RefundPointsWithModeRoutingAsync(
            Guid customerId, Guid tenantId, int points, string reason, string? voucherCode, string idempotencyKey)
        {
            if (_loyaltyModeResolver is not null && _allianceWalletService is not null)
            {
                LoyaltyMode effectiveMode = await _loyaltyModeResolver.GetEffectiveModeAsync(tenantId);
                if (effectiveMode == LoyaltyMode.Alliance)
                {
                    bool isMember = await _loyaltyModeResolver.IsAllianceMemberAsync(tenantId);
                    if (isMember)
                    {
                        var customer = await _dbContext.Customers.FirstOrDefaultAsync(c => c.Id == customerId);
                        Guid deviceGuid = customer?.DeviceId ?? customerId;
                        var (success, _, error) = await _allianceWalletService.RefundAsync(
                            deviceGuid, tenantId, points, reason, voucherCode ?? "CANCEL", idempotencyKey);
                        if (!success)
                        {
                            _logger.LogWarning("Alliance refund failed for customer {CustomerId}: {Error}", customerId, error);
                            return false;
                        }
                        _logger.LogInformation("🎁 ALLIANCE REFUND: {Points} points refunded to PG wallet for device {DeviceId}", points, deviceGuid);
                        return true;
                    }
                }
            }

            // Silo fallback
            return await _loyaltyRewardsService.AddPointsAsync(customerId, points, reason);
        }

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
