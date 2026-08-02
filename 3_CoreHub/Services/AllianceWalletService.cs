using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Infrastructure.Messaging;
using VanAn.Shared.Domain;
using VanAn.Shared.Services;

namespace VanAn.CoreHub.Services;

/// <summary>
/// Cross-tenant alliance wallet operations. PG-only.
/// Every mutation appends an immutable AllianceTransaction row and publishes a NATS event
/// so ShopERP can sync local LoyaltyRewards.PointBalance.
/// Spec: docs/specs/loyalty-alliance-spec.md v1.0.
/// </summary>
public class AllianceWalletService(
    IVanAnDbContext dbContext,
    ILoyaltyModeResolver modeResolver,
    INatsEventPublisher? natsEventPublisher,
    ILogger<AllianceWalletService> logger) : IAllianceWalletService
{
    private readonly IVanAnDbContext _dbContext = dbContext;
    private readonly ILoyaltyModeResolver _modeResolver = modeResolver;
    private readonly INatsEventPublisher? _natsEventPublisher = natsEventPublisher;
    private readonly ILogger<AllianceWalletService> _logger = logger;

    private static readonly JsonSerializerOptions EventJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <inheritdoc/>
    public Task<AllianceWallet?> GetWalletByDeviceIdAsync(Guid customerDeviceId)
        => _dbContext.AllianceWallets.FirstOrDefaultAsync(w => w.CustomerDeviceId == customerDeviceId);

    /// <inheritdoc/>
    public async Task<AllianceWallet> GetOrCreateWalletAsync(Guid customerDeviceId, string? phoneNumber)
    {
        AllianceWallet? wallet = await _dbContext.AllianceWallets
            .FirstOrDefaultAsync(w => w.CustomerDeviceId == customerDeviceId);

        if (wallet is not null)
        {
            return wallet;
        }

        wallet = new AllianceWallet(customerDeviceId, phoneNumber);
        _ = _dbContext.AllianceWallets.Add(wallet);
        await _dbContext.SaveChangesAsync();
        _logger.LogInformation("Created AllianceWallet for device {CustomerDeviceId}", customerDeviceId);
        return wallet;
    }

    /// <inheritdoc/>
    public async Task<(bool Success, int NewBalance, string? Error)> AddPointsAsync(
        Guid customerDeviceId, Guid tenantId, int points, string reason, Guid? sourceOrderId = null)
    {
        if (points <= 0)
        {
            return (false, 0, "Points must be positive");
        }

        AllianceWallet wallet = await GetOrCreateWalletAsync(customerDeviceId, phoneNumber: null);
        int maxWallet = await _modeResolver.GetEffectiveMaxWalletPointsAsync(tenantId);

        if (wallet.TotalPointBalance + points > maxWallet)
        {
            _logger.LogWarning(
                "AllianceWallet AddPoints rejected: device={Device} balance={Balance} +points={Points} exceeds max={Max}",
                customerDeviceId, wallet.TotalPointBalance, points, maxWallet);
            return (false, wallet.TotalPointBalance,
                $"Wallet cap exceeded: {wallet.TotalPointBalance} + {points} > {maxWallet}");
        }

        wallet.AddPoints(points);
        var tx = new AllianceTransaction(
            walletId: wallet.Id,
            transactionTenantId: tenantId,
            type: AllianceTransactionType.EARN,
            points: points,
            balanceAfter: wallet.TotalPointBalance,
            reason: reason,
            sourceOrderId: sourceOrderId);
        _ = _dbContext.AllianceTransactions.Add(tx);
        await _dbContext.SaveChangesAsync();

        await PublishLoyaltyChangedAsync(customerDeviceId, wallet.TotalPointBalance);
        _logger.LogInformation(
            "AllianceWallet AddPoints: device={Device} tenant={Tenant} +{Points} → balance={Balance}",
            customerDeviceId, tenantId, points, wallet.TotalPointBalance);
        return (true, wallet.TotalPointBalance, null);
    }

    /// <inheritdoc/>
    public async Task<(bool Success, int NewBalance, string? Error)> DeductPointsAsync(
        Guid customerDeviceId, Guid tenantId, int points, string reason, string? voucherCode = null)
    {
        if (points <= 0)
        {
            return (false, 0, "Points must be positive");
        }

        AllianceWallet? wallet = await GetWalletByDeviceIdAsync(customerDeviceId);
        if (wallet is null)
        {
            return (false, 0, "Wallet not found");
        }

        if (wallet.TotalPointBalance < points)
        {
            _logger.LogWarning(
                "AllianceWallet DeductPoints rejected: device={Device} balance={Balance} < points={Points}",
                customerDeviceId, wallet.TotalPointBalance, points);
            return (false, wallet.TotalPointBalance, "Insufficient balance");
        }

        wallet.DeductPoints(points);
        var tx = new AllianceTransaction(
            walletId: wallet.Id,
            transactionTenantId: tenantId,
            type: AllianceTransactionType.REDEEM,
            points: -points,
            balanceAfter: wallet.TotalPointBalance,
            reason: reason,
            voucherCode: voucherCode,
            refundTenantId: tenantId); // Q4: refund returns to tenant where redeem occurred
        _ = _dbContext.AllianceTransactions.Add(tx);
        await _dbContext.SaveChangesAsync();

        await PublishLoyaltyChangedAsync(customerDeviceId, wallet.TotalPointBalance);
        _logger.LogInformation(
            "AllianceWallet DeductPoints: device={Device} tenant={Tenant} -{Points} → balance={Balance}",
            customerDeviceId, tenantId, points, wallet.TotalPointBalance);
        return (true, wallet.TotalPointBalance, null);
    }

    /// <inheritdoc/>
    public async Task<(bool Success, int NewBalance, string? Error)> RefundAsync(
        Guid customerDeviceId, Guid tenantId, int points, string reason, string voucherCode)
    {
        if (points <= 0)
        {
            return (false, 0, "Points must be positive");
        }

        AllianceWallet? wallet = await GetWalletByDeviceIdAsync(customerDeviceId);
        if (wallet is null)
        {
            return (false, 0, "Wallet not found");
        }

        // Q4: refund returns points to the wallet; attributed to the tenant where redeem occurred.
        wallet.AddPoints(points);
        var tx = new AllianceTransaction(
            walletId: wallet.Id,
            transactionTenantId: tenantId,
            type: AllianceTransactionType.ADJUST,
            points: points,
            balanceAfter: wallet.TotalPointBalance,
            reason: reason,
            voucherCode: voucherCode,
            refundTenantId: tenantId);
        _ = _dbContext.AllianceTransactions.Add(tx);
        await _dbContext.SaveChangesAsync();

        await PublishLoyaltyChangedAsync(customerDeviceId, wallet.TotalPointBalance);
        _logger.LogInformation(
            "AllianceWallet Refund: device={Device} tenant={Tenant} +{Points} → balance={Balance} (voucher={Voucher})",
            customerDeviceId, tenantId, points, wallet.TotalPointBalance, voucherCode);
        return (true, wallet.TotalPointBalance, null);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AllianceTransaction>> GetTransactionsAsync(Guid walletId, int limit = 20)
        => await _dbContext.AllianceTransactions
            .Where(t => t.WalletId == walletId)
            .OrderByDescending(t => t.TransactionAt)
            .Take(limit)
            .ToListAsync();

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AllianceTransaction>> GetTransactionsByTenantAsync(
        Guid walletId, Guid tenantId, int limit = 20)
        => await _dbContext.AllianceTransactions
            .Where(t => t.WalletId == walletId && t.TransactionTenantId == tenantId)
            .OrderByDescending(t => t.TransactionAt)
            .Take(limit)
            .ToListAsync();

    // ──────────────────────────────────────────────────────────
    // NATS publish
    // ──────────────────────────────────────────────────────────

    private async Task PublishLoyaltyChangedAsync(Guid customerDeviceId, int newBalance)
    {
        if (_natsEventPublisher is null || !_natsEventPublisher.IsConnected)
        {
            return;
        }

        string subject = $"vanan.cloud.loyalty.changed.{customerDeviceId}";
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(
            new { customerDeviceId, pointBalance = newBalance, updatedAt = DateTime.UtcNow },
            EventJsonOptions);
        try
        {
            await _natsEventPublisher.PublishAsync(subject, payload);
        }
        catch (Exception ex)
        {
            // NATS publish is best-effort — Outbox pattern handles retry for critical sync.
            _logger.LogWarning(ex, "AllianceWallet: NATS publish failed for {Subject}", subject);
        }
    }
}
