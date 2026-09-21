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
    ILogger<AllianceWalletService> logger,
    // Loyalty Points Integrity (Batch 1): unified PG→SQLite mirror sync publisher
    // (replaces the local PublishLoyaltyChangedAsync so Silo + Alliance share one event shape).
    LoyaltyBalanceSyncPublisher? loyaltyBalanceSyncPublisher = null,
    // Loyalty Points Integrity (Batch 2, T1.5): defense-in-depth budget check — protects EVERY
    // wallet caller (Mission/Redemption refund/welcome/Internal API), not just the order workflow.
    ILoyaltyBudgetService? loyaltyBudgetService = null) : IAllianceWalletService
{
    private readonly IVanAnDbContext _dbContext = dbContext;
    private readonly ILoyaltyModeResolver _modeResolver = modeResolver;
    private readonly INatsEventPublisher? _natsEventPublisher = natsEventPublisher;
    private readonly ILogger<AllianceWalletService> _logger = logger;
    private readonly LoyaltyBalanceSyncPublisher? _loyaltyBalanceSyncPublisher = loyaltyBalanceSyncPublisher;
    private readonly ILoyaltyBudgetService? _loyaltyBudgetService = loyaltyBudgetService;

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
        Guid customerDeviceId, Guid tenantId, int points, string reason, Guid? sourceOrderId = null,
        string? idempotencyKey = null)
    {
        if (points <= 0)
        {
            return (false, 0, "Points must be positive");
        }

        // Loyalty Consistency Fix Phase 0: idempotency check — retry-safe for HTTP proxy
        if (idempotencyKey is not null)
        {
            var existing = await _dbContext.AllianceTransactions
                .FirstOrDefaultAsync(t => t.IdempotencyKey == idempotencyKey);
            if (existing is not null)
            {
                _logger.LogInformation("Idempotency hit: key={Key} → cached balance={Balance}", idempotencyKey, existing.BalanceAfter);
                return (true, existing.BalanceAfter, null);
            }
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

        // Batch 2 (T1.5): defense-in-depth budget check — tenant-level caps (monthly/daily) enforced
        // for EVERY caller. Per-order rate cap is skipped (orderAmount null — wallet has no order
        // context) and per-customer daily is skipped (customerId unknown here) — the ledger enforces
        // those with full context before routing to the wallet. No config row → no cap (no-op).
        int awardedPoints = points;
        if (_loyaltyBudgetService is not null)
        {
            int checkedPoints = await _loyaltyBudgetService.CheckAndAdjustPointsAsync(
                tenantId, customerId: Guid.Empty, orderAmount: null, requestedPoints: points);
            if (checkedPoints <= 0)
            {
                _logger.LogWarning(
                    "AllianceWallet AddPoints budget-rejected: device={Device} tenant={Tenant} points={Points} (budget exhausted)",
                    customerDeviceId, tenantId, points);
                return (false, wallet.TotalPointBalance, "Budget exhausted for this tenant");
            }

            if (checkedPoints < points)
            {
                _logger.LogInformation(
                    "AllianceWallet AddPoints budget-capped: device={Device} tenant={Tenant} {Orig}→{New}",
                    customerDeviceId, tenantId, points, checkedPoints);
                awardedPoints = checkedPoints;
            }
        }

        wallet.AddPoints(awardedPoints);
        var tx = new AllianceTransaction(
            walletId: wallet.Id,
            transactionTenantId: tenantId,
            type: AllianceTransactionType.EARN,
            points: awardedPoints,
            balanceAfter: wallet.TotalPointBalance,
            reason: reason,
            sourceOrderId: sourceOrderId,
            idempotencyKey: idempotencyKey);
        _ = _dbContext.AllianceTransactions.Add(tx);
        await _dbContext.SaveChangesAsync();

        // Batch 2 (T1.5): record issuance counters for the ACTUAL awarded amount (single count —
        // the ledger does NOT record for the Alliance branch; the wallet owns it).
        if (_loyaltyBudgetService is not null)
        {
            try
            {
                await _loyaltyBudgetService.RecordIssuanceAsync(tenantId, awardedPoints);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AllianceWallet AddPoints: RecordIssuanceAsync failed for tenant {TenantId} — counters may be stale", tenantId);
            }
        }

        await PublishLoyaltyChangedAsync(customerDeviceId, tenantId, wallet.TotalPointBalance, tx);
        _logger.LogInformation(
            "AllianceWallet AddPoints: device={Device} tenant={Tenant} +{Points} → balance={Balance}",
            customerDeviceId, tenantId, awardedPoints, wallet.TotalPointBalance);
        return (true, wallet.TotalPointBalance, null);
    }

    /// <inheritdoc/>
    public async Task<(bool Success, int NewBalance, string? Error)> DeductPointsAsync(
        Guid customerDeviceId, Guid tenantId, int points, string reason, string? voucherCode = null,
        string? idempotencyKey = null)
    {
        if (points <= 0)
        {
            return (false, 0, "Points must be positive");
        }

        // Loyalty Consistency Fix Phase 0: idempotency check
        if (idempotencyKey is not null)
        {
            var existing = await _dbContext.AllianceTransactions
                .FirstOrDefaultAsync(t => t.IdempotencyKey == idempotencyKey);
            if (existing is not null)
            {
                _logger.LogInformation("Idempotency hit: key={Key} → cached balance={Balance}", idempotencyKey, existing.BalanceAfter);
                return (true, existing.BalanceAfter, null);
            }
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

        // Loyalty Points Integrity (Batch 5, T5.1 — decision D2): attribution-aware consume.
        // Determine the source of the consumed points BEFORE touching the pool:
        //   1. Current (redeeming) tenant's own points first (netEarn at that tenant).
        //   2. If insufficient → other tenants with positive netEarn, FIFO by earliest EARN/ADJUST.
        //   3. Write ONE REDEEM entry per source tenant (append-only log):
        //      TransactionTenantId = tenant spending, SourceTenantId = tenant owning the points.
        var (netEarn, fifoOrder) = await ComputeNetEarnByTenantAsync(wallet.Id);

        var sources = new List<(Guid TenantId, int Amount)>();
        int remaining = points;

        int currentTenantNet = netEarn.GetValueOrDefault(tenantId);
        if (currentTenantNet > 0)
        {
            int take = Math.Min(currentTenantNet, remaining);
            sources.Add((tenantId, take));
            remaining -= take;
        }

        foreach (Guid sourceTenant in fifoOrder)
        {
            if (remaining <= 0)
            {
                break;
            }

            if (sourceTenant == tenantId)
            {
                continue; // current tenant already consumed above
            }

            int available = netEarn.GetValueOrDefault(sourceTenant);
            if (available <= 0)
            {
                continue;
            }

            int take = Math.Min(available, remaining);
            sources.Add((sourceTenant, take));
            remaining -= take;
        }

        if (remaining > 0)
        {
            // Invariant violated: Σ netEarn == wallet balance, so this should be unreachable after
            // the balance check. Refuse a partial deduction to keep the log consistent.
            _logger.LogWarning(
                "AllianceWallet DeductPoints rejected: device={Device} requested={Points} but only {Attributable} points attributable",
                customerDeviceId, points, points - remaining);
            return (false, wallet.TotalPointBalance, "Insufficient balance");
        }

        wallet.DeductPoints(points);
        AllianceTransaction? firstTx = null;
        foreach (var (sourceTenant, amount) in sources)
        {
            var tx = new AllianceTransaction(
                walletId: wallet.Id,
                transactionTenantId: tenantId,
                type: AllianceTransactionType.REDEEM,
                points: -amount,
                balanceAfter: wallet.TotalPointBalance,
                reason: reason,
                voucherCode: voucherCode,
                refundTenantId: tenantId, // Q4: refund returns to tenant where redeem occurred
                idempotencyKey: idempotencyKey);
            tx.SetSourceTenant(sourceTenant);
            firstTx ??= tx;
            _ = _dbContext.AllianceTransactions.Add(tx);
        }
        await _dbContext.SaveChangesAsync();

        await PublishLoyaltyChangedAsync(customerDeviceId, tenantId, wallet.TotalPointBalance, firstTx);
        _logger.LogInformation(
            "AllianceWallet DeductPoints: device={Device} tenant={Tenant} -{Points} → balance={Balance} (sources: {Sources})",
            customerDeviceId, tenantId, points, wallet.TotalPointBalance,
            string.Join(", ", sources.Select(s => $"{s.TenantId}:{s.Amount}")));
        return (true, wallet.TotalPointBalance, null);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<WalletTenantBalance>> GetTenantBalancesAsync(Guid walletId)
    {
        var (netEarn, _) = await ComputeNetEarnByTenantAsync(walletId);
        return netEarn
            .OrderBy(kv => kv.Key)
            .Select(kv => new WalletTenantBalance(kv.Key, kv.Value))
            .ToList();
    }

    /// <inheritdoc/>
    public async Task<(bool Success, int NewBalance, string? Error)> RefundAsync(
        Guid customerDeviceId, Guid tenantId, int points, string reason, string voucherCode,
        string? idempotencyKey = null)
    {
        if (points <= 0)
        {
            return (false, 0, "Points must be positive");
        }

        // Loyalty Consistency Fix Phase 0: idempotency check
        if (idempotencyKey is not null)
        {
            var existing = await _dbContext.AllianceTransactions
                .FirstOrDefaultAsync(t => t.IdempotencyKey == idempotencyKey);
            if (existing is not null)
            {
                _logger.LogInformation("Idempotency hit: key={Key} → cached balance={Balance}", idempotencyKey, existing.BalanceAfter);
                return (true, existing.BalanceAfter, null);
            }
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
            refundTenantId: tenantId,
            idempotencyKey: idempotencyKey);
        _ = _dbContext.AllianceTransactions.Add(tx);
        await _dbContext.SaveChangesAsync();

        await PublishLoyaltyChangedAsync(customerDeviceId, tenantId, wallet.TotalPointBalance, tx);
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
    // Phase 4: Mode Switch Migration
    // ──────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<MigrationResult> ConsolidateWalletsAsync(
        Guid tenantId,
        IReadOnlyList<CustomerBalanceInput> customerBalances,
        string changedBy)
    {
        if (customerBalances.Count == 0)
        {
            _logger.LogInformation("ConsolidateWallets: no customers to migrate for tenant {TenantId}", tenantId);
            return new MigrationResult();
        }

        int processed = 0;
        int totalPoints = 0;
        string migrationReason = $"Silo→Alliance migration (by {changedBy})";

        foreach (var input in customerBalances)
        {
            if (input.PointBalance <= 0)
            {
                continue; // skip zero-balance customers
            }

            // Idempotency: skip if this device already has an ADJUST migration tx for this tenant
            bool alreadyMigrated = await _dbContext.AllianceTransactions
                .AnyAsync(t => t.TransactionTenantId == tenantId
                    && t.Type == AllianceTransactionType.ADJUST
                    && t.Reason == migrationReason
                    && _dbContext.AllianceWallets.Any(w => w.Id == t.WalletId && w.CustomerDeviceId == input.CustomerDeviceId));

            if (alreadyMigrated)
            {
                _logger.LogDebug("ConsolidateWallets: device {Device} already migrated for tenant {Tenant} — skipping",
                    input.CustomerDeviceId, tenantId);
                continue;
            }

            AllianceWallet wallet = await GetOrCreateWalletAsync(input.CustomerDeviceId, input.PhoneNumber);
            wallet.AddPoints(input.PointBalance);

            var tx = new AllianceTransaction(
                walletId: wallet.Id,
                transactionTenantId: tenantId,
                type: AllianceTransactionType.ADJUST,
                points: input.PointBalance,
                balanceAfter: wallet.TotalPointBalance,
                reason: migrationReason);
            _ = _dbContext.AllianceTransactions.Add(tx);

            await PublishLoyaltyChangedAsync(input.CustomerDeviceId, tenantId, wallet.TotalPointBalance);
            processed++;
            totalPoints += input.PointBalance;
        }

        if (processed > 0)
        {
            await _dbContext.SaveChangesAsync();
        }

        _logger.LogInformation(
            "ConsolidateWallets: tenant {TenantId} — {Processed} customers migrated, {TotalPoints} points consolidated",
            tenantId, processed, totalPoints);

        return new MigrationResult
        {
            CustomersProcessed = processed,
            TotalPointsTransferred = totalPoints
        };
    }

    /// <inheritdoc/>
    public async Task<MigrationResult> SplitWalletsAsync(Guid tenantId, string changedBy)
    {
        // Find all wallets that have transactions with this tenant
        var walletIds = await _dbContext.AllianceTransactions
            .Where(t => t.TransactionTenantId == tenantId)
            .Select(t => t.WalletId)
            .Distinct()
            .ToListAsync();

        if (walletIds.Count == 0)
        {
            _logger.LogInformation("SplitWallets: no wallets with transactions for tenant {TenantId}", tenantId);
            return new MigrationResult();
        }

        string splitReason = $"Alliance→Silo split (by {changedBy})";
        var allocations = new List<WalletAllocation>();
        int processed = 0;
        int totalPoints = 0;

        foreach (Guid walletId in walletIds)
        {
            var wallet = await _dbContext.AllianceWallets.FirstOrDefaultAsync(w => w.Id == walletId);
            if (wallet is null || !wallet.IsActive || wallet.TotalPointBalance <= 0)
            {
                continue;
            }

            // Calculate net EARN per-tenant from transaction log
            var transactions = await _dbContext.AllianceTransactions
                .Where(t => t.WalletId == walletId)
                .ToListAsync();

            var netEarnByTenant = transactions
                .GroupBy(t => t.TransactionTenantId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Where(t => t.Type == AllianceTransactionType.EARN).Sum(t => t.Points)
                        - g.Where(t => t.Type == AllianceTransactionType.REDEEM).Sum(t => Math.Abs(t.Points)));

            decimal totalNetEarn = netEarnByTenant.Values.Where(v => v > 0).Sum();

            if (totalNetEarn <= 0)
            {
                _logger.LogWarning("SplitWallets: wallet {WalletId} has no positive net EARN — skipping", walletId);
                continue;
            }

            // Distribute TotalPointBalance proportionally to each tenant with netEarn > 0
            foreach (var (tenant, netEarn) in netEarnByTenant)
            {
                if (netEarn <= 0)
                {
                    continue; // edge case: tenant with net EARN ≤ 0 gets no allocation
                }

                int allocation = (int)Math.Round((netEarn / totalNetEarn) * wallet.TotalPointBalance);
                if (allocation <= 0)
                {
                    continue;
                }

                allocations.Add(new WalletAllocation(wallet.CustomerDeviceId, tenant, allocation));

                var tx = new AllianceTransaction(
                    walletId: wallet.Id,
                    transactionTenantId: tenant,
                    type: AllianceTransactionType.ADJUST,
                    points: -allocation,
                    balanceAfter: wallet.TotalPointBalance - allocation,
                    reason: splitReason);
                _ = _dbContext.AllianceTransactions.Add(tx);
            }

            // Deduct total balance to zero + freeze wallet
            int totalAllocated = allocations.Where(a => a.CustomerDeviceId == wallet.CustomerDeviceId).Sum(a => a.Points);
            wallet.DeductPoints(totalAllocated);
            wallet.Freeze();
            processed++;
            totalPoints += totalAllocated;
        }

        if (processed > 0)
        {
            await _dbContext.SaveChangesAsync();

            // Publish loyalty changed for each affected wallet
            foreach (Guid walletId in walletIds)
            {
                var wallet = await _dbContext.AllianceWallets.FirstOrDefaultAsync(w => w.Id == walletId);
                if (wallet is not null)
                {
                    await PublishLoyaltyChangedAsync(wallet.CustomerDeviceId, tenantId, wallet.TotalPointBalance);
                }
            }
        }

        _logger.LogInformation(
            "SplitWallets: tenant {TenantId} — {Processed} wallets split, {Allocations} allocations, {TotalPoints} points distributed",
            tenantId, processed, allocations.Count, totalPoints);

        return new MigrationResult
        {
            CustomersProcessed = processed,
            TotalPointsTransferred = totalPoints,
            Allocations = allocations
        };
    }

    // ──────────────────────────────────────────────────────────
    // Batch 5 (T5.1): per-tenant attribution
    // ──────────────────────────────────────────────────────────

    /// <summary>
    /// Computes per-tenant net EARN from the wallet's append-only transaction log.
    /// net[tenant] = Σ EARN/ADJUST (by TransactionTenantId) − Σ |REDEEM| (by SourceTenantId —
    /// the tenant that OWNS the consumed points). Legacy REDEEM rows (SourceTenantId null) are
    /// attributed to their TransactionTenantId (the pre-attribution behavior: the redeeming
    /// tenant implicitly consumed its own pool share). Invariant: Σ net == wallet balance.
    /// Also returns the FIFO order of tenants with positive net — earliest positive
    /// contribution (EARN or positive ADJUST) first — used by DeductPointsAsync (decision D2).
    /// </summary>
    private async Task<(Dictionary<Guid, int> NetEarn, List<Guid> FifoOrder)> ComputeNetEarnByTenantAsync(Guid walletId)
    {
        var transactions = await _dbContext.AllianceTransactions
            .Where(t => t.WalletId == walletId)
            .ToListAsync();

        var netEarn = new Dictionary<Guid, int>();
        var earliestPositive = new Dictionary<Guid, DateTime>();

        foreach (AllianceTransaction t in transactions)
        {
            Guid tenantKey = t.Type == AllianceTransactionType.REDEEM && t.SourceTenantId.HasValue
                ? t.SourceTenantId.Value
                : t.TransactionTenantId;

            netEarn[tenantKey] = netEarn.GetValueOrDefault(tenantKey) + t.Points;

            if (t.Points > 0 && t.Type != AllianceTransactionType.REDEEM)
            {
                if (!earliestPositive.TryGetValue(tenantKey, out DateTime current) || t.TransactionAt < current)
                {
                    earliestPositive[tenantKey] = t.TransactionAt;
                }
            }
        }

        List<Guid> fifoOrder = netEarn
            .Where(kv => kv.Value > 0)
            .OrderBy(kv => earliestPositive.GetValueOrDefault(kv.Key, DateTime.MaxValue))
            .Select(kv => kv.Key)
            .ToList();

        return (netEarn, fifoOrder);
    }

    // ──────────────────────────────────────────────────────────
    // NATS publish
    // ──────────────────────────────────────────────────────────

    /// <summary>
    /// Loyalty Consistency Fix Phase 3 (BUG #9) + Loyalty Points Integrity (Batch 1):
    /// publish loyalty change — delegated to the shared LoyaltyBalanceSyncPublisher
    /// (subject vanan.cloud.loyalty.changed.{deviceId}, extended payload + Outbox fallback).
    /// Alliance wallet operates on device identity — customerId is unknown here (Guid.Empty),
    /// the subscriber falls back to device-based matching.
    /// Legacy callers (consolidate/split — no tx) pass type=ADJUST with no delta (balance-only).
    /// </summary>
    private async Task PublishLoyaltyChangedAsync(Guid customerDeviceId, Guid tenantId, int newBalance, AllianceTransaction? tx = null)
    {
        if (_loyaltyBalanceSyncPublisher is not null)
        {
            await _loyaltyBalanceSyncPublisher.PublishAsync(
                customerId: Guid.Empty, // unknown — device-based matching on the subscriber side
                tenantId: tenantId,
                pointBalance: newBalance,
                type: tx?.Type.ToString() ?? "ADJUST",
                points: tx?.Points,
                reason: tx?.Reason,
                customerDeviceId: customerDeviceId,
                sourceOrderId: tx?.SourceOrderId);
            return;
        }

        // Legacy fallback (publisher not registered) — old behavior preserved.
        if (_natsEventPublisher is null || !_natsEventPublisher.IsConnected)
        {
            return;
        }

        string subject = $"vanan.cloud.loyalty.changed.{customerDeviceId}";

        object payloadObj;
        if (tx is not null)
        {
            // BUG #9 extended payload — LoyaltySyncSubscriber appends history entries
            payloadObj = new
            {
                customerDeviceId,
                pointBalance = newBalance,
                updatedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                type = tx.Type.ToString(),
                points = tx.Points,
                reason = tx.Reason,
                tenantId = tx.TransactionTenantId.ToString()
            };
        }
        else
        {
            // Legacy payload — balance-only (backward compat with old subscribers)
            payloadObj = new { customerDeviceId, pointBalance = newBalance, updatedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ") };
        }

        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(payloadObj, EventJsonOptions);
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
