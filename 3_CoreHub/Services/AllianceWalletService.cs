using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
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

    // ──────────────────────────────────────────────────────────
    // P0.1 (audit R1): per-wallet write serialization
    // ──────────────────────────────────────────────────────────

    private bool IsPostgres =>
        _dbContext.ProviderName.Contains("Npgsql") || _dbContext.ProviderName.Contains("PostgreSQL");

    /// <summary>
    /// Serializes concurrent writes for one wallet on PostgreSQL using a transaction-scoped
    /// advisory lock (precedent: WalletService/BookingService — pg_advisory_xact_lock +
    /// hashtextextended). Every wallet mutation below takes this lock, so two concurrent
    /// writers for the same device id can no longer observe the same balance and both win.
    /// Returns the OWNED transaction when the caller had no ambient transaction (caller must
    /// Commit/Dispose — Dispose rolls back if never committed); returns null when
    /// participating in the caller's ambient transaction (lock still applies, lock is held
    /// until the ambient tx ends) or on non-PG providers (SQLite tests serialize at the DB).
    /// </summary>
    private async Task<IDbContextTransaction?> AcquireWalletLockAsync(Guid customerDeviceId)
    {
        if (!IsPostgres || _dbContext is not DbContext ef)
        {
            return null;
        }

        IDbContextTransaction? ownedTx = ef.Database.CurrentTransaction is null
            ? await _dbContext.BeginTransactionAsync()
            : null;

        await ef.Database.ExecuteSqlRawAsync(
            "WITH l AS (SELECT pg_advisory_xact_lock(hashtextextended({0}, 0))) SELECT 1",
            $"loyalty_wallet:{customerDeviceId}");
        return ownedTx;
    }

    /// <summary>
    /// Idempotency dedup — returns the cached result when a transaction with this key
    /// already exists (retry-safe for HTTP proxy + client retries). Backed by the unique
    /// filtered index IX_AllianceTransactions_IdempotencyKey (P0.1) so concurrent same-key
    /// inserts can no longer both pass this check — the second caller hits the unique
    /// constraint OR re-checks under the wallet lock and finds the committed row.
    /// </summary>
    private async Task<(bool Success, int NewBalance, string? Error)?> CheckIdempotencyAsync(string? idempotencyKey)
    {
        if (idempotencyKey is null)
        {
            return null;
        }

        var existing = await _dbContext.AllianceTransactions
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.IdempotencyKey == idempotencyKey);
        if (existing is null)
        {
            return null;
        }

        _logger.LogInformation("Idempotency hit: key={Key} → cached balance={Balance}", idempotencyKey, existing.BalanceAfter);
        return (true, existing.BalanceAfter, null);
    }

    /// <summary>
    /// FOR UPDATE equivalent (P0.1): after the wallet lock is held, refresh the tracked
    /// wallet from the DB so the balance reflects the latest committed row — the identity
    /// map could otherwise hand us stale values loaded before the lock was acquired
    /// (or left stale by an earlier ExecuteUpdate on the same scoped context).
    /// </summary>
    private async Task ReloadWalletUnderLockAsync(AllianceWallet wallet)
    {
        if (_dbContext is DbContext ef)
        {
            await ef.Entry(wallet).ReloadAsync();
        }
    }

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
        string? idempotencyKey = null, Guid customerId = default)
    {
        if (points <= 0)
        {
            return (false, 0, "Points must be positive");
        }

        // Loyalty Consistency Fix Phase 0: idempotency check — retry-safe for HTTP proxy
        var cached = await CheckIdempotencyAsync(idempotencyKey);
        if (cached is not null)
        {
            return cached.Value;
        }

        // P0.1 (R1): serialize all writes for this wallet — concurrent writers can no
        // longer observe the same pre-state (TOCTOU). On PG this is an advisory lock;
        // on SQLite (tests) the single-writer DB provides the same serialization.
        await using IDbContextTransaction? ownedTx = await AcquireWalletLockAsync(customerDeviceId);

        // Re-check under the lock: a concurrent same-key write may have committed
        // while this call waited on the advisory lock.
        cached = await CheckIdempotencyAsync(idempotencyKey);
        if (cached is not null)
        {
            return cached.Value;
        }

        AllianceWallet wallet = await GetOrCreateWalletAsync(customerDeviceId, phoneNumber: null);
        await ReloadWalletUnderLockAsync(wallet);
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

        // P0.1 (R1): atomic conditional UPDATE — single statement, provider-agnostic.
        // Under the PG advisory lock this is belt & suspenders; on SQLite (no advisory
        // lock) it is the ONLY guard against a lost update from a concurrent writer.
        // Re-checks the wallet cap in the WHERE clause against the CURRENT committed
        // value, so a stale tracked balance can't push it over the cap.
        int newBalanceAfterAdd;
        {
            int affected = await _dbContext.AllianceWallets
                .Where(w => w.Id == wallet.Id && w.TotalPointBalance <= maxWallet - awardedPoints)
                .ExecuteUpdateAsync(s => s.SetProperty(w => w.TotalPointBalance, w => w.TotalPointBalance + awardedPoints));
            if (affected == 0)
            {
                int currentBalance = await _dbContext.AllianceTransactions
                    .Where(t => t.WalletId == wallet.Id)
                    .OrderByDescending(t => t.TransactionAt)
                    .Select(t => t.BalanceAfter)
                    .FirstOrDefaultAsync();
                _logger.LogWarning(
                    "AllianceWallet AddPoints rejected (concurrent state): device={Device} cap-check failed, current={Balance}",
                    customerDeviceId, currentBalance);
                return (false, currentBalance, $"Wallet cap exceeded: {currentBalance} + {awardedPoints} > {maxWallet}");
            }
            newBalanceAfterAdd = wallet.TotalPointBalance + awardedPoints;
            // Resync the tracked entity (ExecuteUpdate bypasses change tracking) so
            // later reads on this scoped context see the post-update balance.
            wallet.AddPoints(awardedPoints);
        }

        var tx = new AllianceTransaction(
            walletId: wallet.Id,
            transactionTenantId: tenantId,
            type: AllianceTransactionType.EARN,
            points: awardedPoints,
            balanceAfter: newBalanceAfterAdd,
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

        if (ownedTx is not null)
        {
            await ownedTx.CommitAsync();
        }

        await PublishLoyaltyChangedAsync(customerDeviceId, tenantId, newBalanceAfterAdd, tx, customerId);
        _logger.LogInformation(
            "AllianceWallet AddPoints: device={Device} tenant={Tenant} +{Points} → balance={Balance}",
            customerDeviceId, tenantId, awardedPoints, newBalanceAfterAdd);
        return (true, newBalanceAfterAdd, null);
    }

    /// <inheritdoc/>
    public async Task<(bool Success, int NewBalance, string? Error)> DeductPointsAsync(
        Guid customerDeviceId, Guid tenantId, int points, string reason, string? voucherCode = null,
        string? idempotencyKey = null, Guid customerId = default)
    {
        if (points <= 0)
        {
            return (false, 0, "Points must be positive");
        }

        // Loyalty Consistency Fix Phase 0: idempotency check
        var cached = await CheckIdempotencyAsync(idempotencyKey);
        if (cached is not null)
        {
            return cached.Value;
        }

        // P0.1 (R1): take the per-wallet lock BEFORE reading balance/history — this is
        // the TOCTOU fix. Two concurrent deducts no longer observe the same balance.
        await using IDbContextTransaction? ownedTx = await AcquireWalletLockAsync(customerDeviceId);

        cached = await CheckIdempotencyAsync(idempotencyKey);
        if (cached is not null)
        {
            return cached.Value;
        }

        AllianceWallet? wallet = await GetWalletByDeviceIdAsync(customerDeviceId);
        if (wallet is null)
        {
            return (false, 0, "Wallet not found");
        }
        await ReloadWalletUnderLockAsync(wallet);

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

        // P0.1 (R1): atomic conditional UPDATE — the actual double-spend guard.
        // WHERE TotalPointBalance >= points re-validates the CURRENT committed balance
        // (the tracked entity may be stale). affected == 0 → a concurrent writer won.
        int newBalance;
        {
            int affected = await _dbContext.AllianceWallets
                .Where(w => w.Id == wallet.Id && w.TotalPointBalance >= points)
                .ExecuteUpdateAsync(s => s.SetProperty(w => w.TotalPointBalance, w => w.TotalPointBalance - points));
            if (affected == 0)
            {
                _logger.LogWarning(
                    "AllianceWallet DeductPoints rejected (concurrent state): device={Device} points={Points} — balance guard failed",
                    customerDeviceId, points);
                return (false, wallet.TotalPointBalance, "Insufficient balance");
            }
            newBalance = wallet.TotalPointBalance - points;
            wallet.DeductPoints(points); // resync tracked entity (see AddPoints)
        }

        AllianceTransaction? firstTx = null;
        int sourceIndex = 0;
        foreach (var (sourceTenant, amount) in sources)
        {
            // P0.1 (R1): IdempotencyKey has a UNIQUE filtered index, but a multi-source
            // redeem writes one row per source tenant — only the FIRST row carries the
            // caller's key (it is the dedup carrier). Sibling rows get a derived key so
            // they stay traceable without tripping the constraint.
            var tx = new AllianceTransaction(
                walletId: wallet.Id,
                transactionTenantId: tenantId,
                type: AllianceTransactionType.REDEEM,
                points: -amount,
                balanceAfter: newBalance,
                reason: reason,
                voucherCode: voucherCode,
                refundTenantId: tenantId, // Q4: refund returns to tenant where redeem occurred
                idempotencyKey: sourceIndex == 0 || idempotencyKey is null
                    ? idempotencyKey
                    : $"{idempotencyKey}#src:{sourceIndex}");
            tx.SetSourceTenant(sourceTenant);
            firstTx ??= tx;
            sourceIndex++;
            _ = _dbContext.AllianceTransactions.Add(tx);
        }
        await _dbContext.SaveChangesAsync();

        if (ownedTx is not null)
        {
            await ownedTx.CommitAsync();
        }

        await PublishLoyaltyChangedAsync(customerDeviceId, tenantId, newBalance, firstTx, customerId);
        _logger.LogInformation(
            "AllianceWallet DeductPoints: device={Device} tenant={Tenant} -{Points} → balance={Balance} (sources: {Sources})",
            customerDeviceId, tenantId, points, newBalance,
            string.Join(", ", sources.Select(s => $"{s.TenantId}:{s.Amount}")));
        return (true, newBalance, null);
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
        string? idempotencyKey = null, Guid customerId = default)
    {
        if (points <= 0)
        {
            return (false, 0, "Points must be positive");
        }

        // Loyalty Consistency Fix Phase 0: idempotency check
        var cached = await CheckIdempotencyAsync(idempotencyKey);
        if (cached is not null)
        {
            return cached.Value;
        }

        // P0.1 (R1): same per-wallet serialization as Add/Deduct — refunds mutate the
        // same balance + append-only chain.
        await using IDbContextTransaction? ownedTx = await AcquireWalletLockAsync(customerDeviceId);

        cached = await CheckIdempotencyAsync(idempotencyKey);
        if (cached is not null)
        {
            return cached.Value;
        }

        AllianceWallet? wallet = await GetWalletByDeviceIdAsync(customerDeviceId);
        if (wallet is null)
        {
            return (false, 0, "Wallet not found");
        }
        await ReloadWalletUnderLockAsync(wallet);

        // Q4: refund returns points to the wallet; attributed to the tenant where redeem occurred.
        // P0.1 (R1): atomic delta UPDATE — a refund must not overwrite a concurrent
        // deduction (lost update on the tracked entity under SQLite/no-advisory-lock).
        int newBalanceAfterRefund;
        {
            int affected = await _dbContext.AllianceWallets
                .Where(w => w.Id == wallet.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(w => w.TotalPointBalance, w => w.TotalPointBalance + points));
            if (affected == 0)
            {
                _logger.LogWarning(
                    "AllianceWallet Refund rejected: device={Device} wallet row missing",
                    customerDeviceId);
                return (false, wallet.TotalPointBalance, "Wallet not found");
            }
            newBalanceAfterRefund = wallet.TotalPointBalance + points;
            wallet.AddPoints(points); // resync tracked entity (see AddPoints)
        }

        var tx = new AllianceTransaction(
            walletId: wallet.Id,
            transactionTenantId: tenantId,
            type: AllianceTransactionType.ADJUST,
            points: points,
            balanceAfter: newBalanceAfterRefund,
            reason: reason,
            voucherCode: voucherCode,
            refundTenantId: tenantId,
            idempotencyKey: idempotencyKey);
        _ = _dbContext.AllianceTransactions.Add(tx);
        await _dbContext.SaveChangesAsync();

        if (ownedTx is not null)
        {
            await ownedTx.CommitAsync();
        }

        await PublishLoyaltyChangedAsync(customerDeviceId, tenantId, newBalanceAfterRefund, tx, customerId);
        _logger.LogInformation(
            "AllianceWallet Refund: device={Device} tenant={Tenant} +{Points} → balance={Balance} (voucher={Voucher})",
            customerDeviceId, tenantId, points, newBalanceAfterRefund, voucherCode);
        return (true, newBalanceAfterRefund, null);
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

        // P0.1 (R1): the batch bypasses AddPointsAsync, so it must take the same
        // per-wallet lock — otherwise a migration can race a live redeem and corrupt
        // the append-only balance chain. Locks accumulate in one owned tx; inputs are
        // sorted by device id so two concurrent admin ops take locks in the same order
        // (no deadlock).
        IDbContextTransaction? ownedTx = null;
        var pendingPublishes = new List<(Guid DeviceId, int Balance)>();
        try
        {
            foreach (var input in customerBalances.OrderBy(c => c.CustomerDeviceId))
            {
                if (input.PointBalance <= 0)
                {
                    continue; // skip zero-balance customers
                }

                ownedTx ??= await AcquireWalletLockAsync(input.CustomerDeviceId);

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
                await ReloadWalletUnderLockAsync(wallet);
                wallet.AddPoints(input.PointBalance);

                var tx = new AllianceTransaction(
                    walletId: wallet.Id,
                    transactionTenantId: tenantId,
                    type: AllianceTransactionType.ADJUST,
                    points: input.PointBalance,
                    balanceAfter: wallet.TotalPointBalance,
                    reason: migrationReason);
                _ = _dbContext.AllianceTransactions.Add(tx);

                pendingPublishes.Add((input.CustomerDeviceId, wallet.TotalPointBalance));
                processed++;
                totalPoints += input.PointBalance;
            }

            if (processed > 0)
            {
                await _dbContext.SaveChangesAsync();
            }

            if (ownedTx is not null)
            {
                await ownedTx.CommitAsync();
            }
        }
        finally
        {
            if (ownedTx is not null)
            {
                await ownedTx.DisposeAsync();
            }
        }

        // Publish only after commit — events must reflect committed state.
        foreach (var (deviceId, balance) in pendingPublishes)
        {
            await PublishLoyaltyChangedAsync(deviceId, tenantId, balance);
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

        // P0.1 (R1): same per-wallet lock as Consolidate — a split must not race a live
        // redeem on the same wallet. Sorted order = deterministic lock acquisition.
        IDbContextTransaction? ownedTx = null;
        try
        {
        foreach (Guid walletId in walletIds.OrderBy(id => id))
        {
            var wallet = await _dbContext.AllianceWallets.FirstOrDefaultAsync(w => w.Id == walletId);
            if (wallet is null || !wallet.IsActive || wallet.TotalPointBalance <= 0)
            {
                continue;
            }

            ownedTx ??= await AcquireWalletLockAsync(wallet.CustomerDeviceId);
            await ReloadWalletUnderLockAsync(wallet);
            if (wallet.TotalPointBalance <= 0)
            {
                continue; // balance may have changed between pre-check and lock
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
        }

        if (ownedTx is not null)
        {
            await ownedTx.CommitAsync();
        }
        }
        finally
        {
            if (ownedTx is not null)
            {
                await ownedTx.DisposeAsync();
            }
        }

        if (processed > 0)
        {
            // Publish loyalty changed for each affected wallet — post-commit.
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
    /// <paramref name="customerId"/> (BUG-1 fix): the PG customer PK when the caller knows it
    /// (ledger/order/mission/redemption flows) — carried in the payload so the ShopERP subscriber
    /// can bootstrap the mirror stub with the SAME customer identity as PG (device-based stub would
    /// mismatch POS customers). Guid.Empty for raw wallet calls (e.g. internal API) → subscriber
    /// falls back to device-based matching for existing rows.
    /// Legacy callers (consolidate/split — no tx) pass type=ADJUST with no delta (balance-only).
    /// </summary>
    private async Task PublishLoyaltyChangedAsync(Guid customerDeviceId, Guid tenantId, int newBalance, AllianceTransaction? tx = null, Guid customerId = default)
    {
        if (_loyaltyBalanceSyncPublisher is not null)
        {
            await _loyaltyBalanceSyncPublisher.PublishAsync(
                customerId: customerId,
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
