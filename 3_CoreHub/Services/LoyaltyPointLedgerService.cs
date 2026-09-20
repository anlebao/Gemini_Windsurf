using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Domain.Repositories;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Common;
using VanAn.Shared.Services;

namespace VanAn.CoreHub.Services;

/// <summary>
/// Loyalty Points Integrity (Batch 2, Phase 1) — PG ledger implementation.
/// Single source of truth for loyalty award/spend/refund/reversal. See <see cref="ILoyaltyPointLedgerService"/>.
///
/// Write flow (AwardAsync): flag check → centralized orderId guard (RC3) → ensure customer →
/// budget check → mode routing (Silo row / Alliance wallet) → LoyaltyIssuanceRecord (PG) → budget counters.
///
/// Notes:
///   - All read/write queries are cross-tenant by design (internal API, no ambient tenant): every
///     lookup pins TenantId explicitly + IgnoreQueryFilters.
///   - Alliance branch delegates counter recording to AllianceWalletService (T1.5 defense-in-depth);
///     the ledger records counters for the Silo branch only (single counting).
///   - Emergency rollback: "LoyaltyLedgerV2" feature flag (default ON).
/// </summary>
public class LoyaltyPointLedgerService(
    IVanAnDbContext dbContext,
    ILoyaltyRewardsService loyaltyRewardsService,
    ILogger<LoyaltyPointLedgerService> logger,
    ILoyaltyModeResolver? loyaltyModeResolver = null,
    IAllianceWalletService? allianceWalletService = null,
    ILoyaltyBudgetService? loyaltyBudgetService = null,
    ICustomerRepository? customerRepository = null,
    IFeatureFlagService? featureFlagService = null) : ILoyaltyPointLedgerService
{
    public const string LedgerFlag = "LoyaltyLedgerV2";

    private readonly IVanAnDbContext _dbContext = dbContext;
    private readonly ILoyaltyRewardsService _loyaltyRewardsService = loyaltyRewardsService;
    private readonly ILogger<LoyaltyPointLedgerService> _logger = logger;
    private readonly ILoyaltyModeResolver? _loyaltyModeResolver = loyaltyModeResolver;
    private readonly IAllianceWalletService? _allianceWalletService = allianceWalletService;
    private readonly ILoyaltyBudgetService? _loyaltyBudgetService = loyaltyBudgetService;
    private readonly ICustomerRepository? _customerRepository = customerRepository;
    private readonly IFeatureFlagService? _featureFlagService = featureFlagService;

    private static readonly JsonSerializerOptions HistoryJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // ──────────────────────────────────────────────────────────
    // AWARD
    // ──────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<LedgerResult> AwardAsync(AwardRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Points <= 0)
        {
            return LedgerResult.Skipped("Points must be positive");
        }

        try
        {
            if (!await IsLedgerEnabledAsync(cancellationToken))
            {
                return LedgerResult.Skipped($"Ledger disabled by feature flag {LedgerFlag}");
            }

            // 1) Centralized double-award guard (RC3) — per order, on the PG ledger (not per-DB).
            if (request.SourceOrderId.HasValue)
            {
                int? alreadyAwarded = await GetAwardedPointsAsync(request.SourceOrderId.Value, request.TenantId, cancellationToken);
                if (alreadyAwarded.HasValue)
                {
                    _logger.LogInformation("Ledger: skipped duplicate award for order {OrderId} — issuance record exists ({Points} pts)",
                        request.SourceOrderId.Value, alreadyAwarded.Value);
                    return LedgerResult.Skipped($"Order {request.SourceOrderId.Value} already awarded ({alreadyAwarded.Value} pts)");
                }

                // Legacy fallback guard: PG Silo history EARN referencing the order (awards made
                // BEFORE the ledger existed — no issuance record yet). Keeps delivered→completed
                // transitions from double-awarding pre-cutover orders.
                if (await HasSiloEarnForOrderAsync(request.CustomerId, new TenantId(request.TenantId), request.SourceOrderId.Value, cancellationToken))
                {
                    _logger.LogInformation("Ledger: skipped duplicate award for order {OrderId} — Silo history already contains EARN", request.SourceOrderId.Value);
                    return LedgerResult.Skipped($"Order {request.SourceOrderId.Value} already awarded (Silo history)");
                }
            }

            // 2) Ensure the customer exists at PG (FK: LoyaltyRewards.CustomerId → Customers.Id).
            Customer? customer = await GetCustomerCrossTenantAsync(request.CustomerId, cancellationToken);
            if (customer is null)
            {
                customer = await EnsureCustomerStubAsync(request.CustomerId, new TenantId(request.TenantId), request.CustomerDeviceId, cancellationToken);
            }

            // 3) Budget check — ALWAYS runs (config==null → no caps → no-op). The flag is only an
            //    emergency OFF switch, not a gate (decision D3).
            int points = request.Points;
            if (_loyaltyBudgetService is not null)
            {
                int adjusted = await _loyaltyBudgetService.CheckAndAdjustPointsAsync(
                    request.TenantId, request.CustomerId, request.OrderAmount, points, cancellationToken);
                if (adjusted <= 0)
                {
                    _logger.LogInformation("Ledger: budget exhausted for tenant {TenantId} — skipped award for order {OrderId} (original points {Orig})",
                        request.TenantId, request.SourceOrderId?.ToString() ?? "n/a", points);
                    return LedgerResult.Skipped("Budget exhausted for this tenant");
                }

                if (adjusted < points)
                {
                    _logger.LogInformation("Ledger: budget cap applied for order {OrderId} — points adjusted {Orig}→{New}",
                        request.SourceOrderId?.ToString() ?? "n/a", points, adjusted);
                    points = adjusted;
                }
            }

            // 4) Mode routing.
            bool routedToAlliance = false;
            if (_loyaltyModeResolver is not null && _allianceWalletService is not null)
            {
                LoyaltyMode mode = await _loyaltyModeResolver.GetEffectiveModeAsync(request.TenantId);
                if (mode == LoyaltyMode.Alliance && await _loyaltyModeResolver.IsAllianceMemberAsync(request.TenantId))
                {
                    routedToAlliance = true;
                    Guid deviceGuid = request.CustomerDeviceId ?? customer?.DeviceId ?? request.CustomerId;
                    var (ok, balance, error) = await _allianceWalletService.AddPointsAsync(
                        deviceGuid, request.TenantId, points, request.Reason, request.SourceOrderId,
                        request.IdempotencyKey ?? (request.SourceOrderId.HasValue ? $"earn:{request.SourceOrderId.Value}" : null));
                    if (!ok)
                    {
                        _logger.LogWarning("Ledger: Alliance EARN failed for customer {CustomerId}: {Error}", request.CustomerId, error);
                        return LedgerResult.Rejected(error ?? "Alliance EARN failed");
                    }

                    _logger.LogInformation("🎁 LEDGER ALLIANCE EARN: {Points} pts → wallet (device {DeviceId}, balance={Balance})",
                        points, deviceGuid, balance);
                    await CreateIssuanceRecordIfOrderAsync(request, points, cancellationToken);
                    return LedgerResult.Ok(balance);
                }
            }

            // Silo — tenant-scoped row at (customer, awarding tenant).
            bool siloOk = await _loyaltyRewardsService.AddPointsAsync(request.CustomerId, request.TenantId, points, request.Reason);
            if (!siloOk)
            {
                _logger.LogWarning("Ledger: Silo EARN failed for customer {CustomerId} tenant {TenantId}", request.CustomerId, request.TenantId);
                return LedgerResult.Failure("Silo EARN failed");
            }

            int siloBalance = await GetSiloBalanceAsync(request.CustomerId, new TenantId(request.TenantId), cancellationToken);
            _logger.LogInformation("🎁 LEDGER SILO EARN: {Points} pts → customer {CustomerId} tenant {TenantId} (balance={Balance})",
                points, request.CustomerId, request.TenantId, siloBalance);

            await CreateIssuanceRecordIfOrderAsync(request, points, cancellationToken);

            // 6) Budget counters — Silo branch records here; the Alliance wallet already recorded (T1.5).
            if (_loyaltyBudgetService is not null)
            {
                try
                {
                    await _loyaltyBudgetService.RecordIssuanceAsync(request.TenantId, points, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Ledger: RecordIssuanceAsync failed for tenant {TenantId} — counters may be stale", request.TenantId);
                }
            }

            return LedgerResult.Ok(siloBalance);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ledger: AwardAsync failed for customer {CustomerId}", request.CustomerId);
            return LedgerResult.Failure(ex.Message);
        }
    }

    // ──────────────────────────────────────────────────────────
    // SPEND / REFUND / REVERSAL
    // ──────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<LedgerResult> SpendAsync(SpendRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Points <= 0)
        {
            return LedgerResult.Skipped("Points must be positive");
        }

        try
        {
            if (!await IsLedgerEnabledAsync(cancellationToken))
            {
                return LedgerResult.Rejected($"Ledger disabled by feature flag {LedgerFlag}");
            }

            // Alliance member → wallet deduct (FIFO SourceTenantId attribution arrives in Batch 4).
            if (_loyaltyModeResolver is not null && _allianceWalletService is not null)
            {
                LoyaltyMode mode = await _loyaltyModeResolver.GetEffectiveModeAsync(request.TenantId);
                if (mode == LoyaltyMode.Alliance && await _loyaltyModeResolver.IsAllianceMemberAsync(request.TenantId))
                {
                    Customer? customer = await GetCustomerCrossTenantAsync(request.CustomerId, cancellationToken);
                    Guid deviceGuid = customer?.DeviceId ?? request.CustomerId;
                    var (walletOk, balance, walletError) = await _allianceWalletService.DeductPointsAsync(
                        deviceGuid, request.TenantId, request.Points, request.Reason, request.VoucherCode, request.IdempotencyKey);
                    if (!walletOk)
                    {
                        _logger.LogWarning("Ledger: Alliance REDEEM rejected for customer {CustomerId}: {Error}", request.CustomerId, walletError);
                        return LedgerResult.Rejected(walletError ?? "Insufficient balance");
                    }

                    _logger.LogInformation("LEDGER ALLIANCE REDEEM: {Points} pts at tenant {TenantId} (device {DeviceId}, balance={Balance})",
                        request.Points, request.TenantId, deviceGuid, balance);
                    return LedgerResult.Ok(balance);
                }
            }

            // Silo — "luật Silo": only the row at (customer, REDEEMING tenant) may be spent.
            int siloBalance = await GetSiloBalanceAsync(request.CustomerId, new TenantId(request.TenantId), cancellationToken);
            if (siloBalance < request.Points)
            {
                _logger.LogWarning(
                    "Ledger: Silo SPEND rejected — customer {CustomerId} has {Balance} pts at tenant {TenantId} (needed {Points})",
                    request.CustomerId, siloBalance, request.TenantId, request.Points);
                return LedgerResult.Rejected("Không đủ điểm — điểm chỉ dùng được tại tenant đã tặng");
            }

            bool ok = await _loyaltyRewardsService.SubtractPointsAsync(request.CustomerId, request.TenantId, request.Points, request.Reason);
            if (!ok)
            {
                _logger.LogWarning("Ledger: Silo SPEND failed for customer {CustomerId} tenant {TenantId}", request.CustomerId, request.TenantId);
                return LedgerResult.Rejected("Không đủ điểm để đổi");
            }

            int newBalance = await GetSiloBalanceAsync(request.CustomerId, new TenantId(request.TenantId), cancellationToken);
            _logger.LogInformation("LEDGER SILO SPEND: {Points} pts at tenant {TenantId} (customer {CustomerId}, balance={Balance})",
                request.Points, request.TenantId, request.CustomerId, newBalance);
            return LedgerResult.Ok(newBalance);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ledger: SpendAsync failed for customer {CustomerId}", request.CustomerId);
            return LedgerResult.Failure(ex.Message);
        }
    }

    /// <inheritdoc/>
    public async Task<LedgerResult> RefundAsync(RefundRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Points <= 0)
        {
            return LedgerResult.Skipped("Points must be positive");
        }

        try
        {
            if (!await IsLedgerEnabledAsync(cancellationToken))
            {
                return LedgerResult.Skipped($"Ledger disabled by feature flag {LedgerFlag}");
            }

            // Alliance member → wallet refund (returns to the redeeming tenant, Q4 semantics).
            if (_loyaltyModeResolver is not null && _allianceWalletService is not null)
            {
                LoyaltyMode mode = await _loyaltyModeResolver.GetEffectiveModeAsync(request.TenantId);
                if (mode == LoyaltyMode.Alliance && await _loyaltyModeResolver.IsAllianceMemberAsync(request.TenantId))
                {
                    Customer? customer = await GetCustomerCrossTenantAsync(request.CustomerId, cancellationToken);
                    Guid deviceGuid = customer?.DeviceId ?? request.CustomerId;
                    var (walletOk, balance, walletError) = await _allianceWalletService.RefundAsync(
                        deviceGuid, request.TenantId, request.Points, request.Reason, request.VoucherCode ?? "CANCEL", request.IdempotencyKey);
                    if (!walletOk)
                    {
                        _logger.LogWarning("Ledger: Alliance REFUND failed for customer {CustomerId}: {Error}", request.CustomerId, walletError);
                        return LedgerResult.Rejected(walletError ?? "Alliance refund failed");
                    }

                    _logger.LogInformation("LEDGER ALLIANCE REFUND: {Points} pts → wallet (device {DeviceId}, balance={Balance})",
                        request.Points, deviceGuid, balance);
                    return LedgerResult.Ok(balance);
                }
            }

            // Silo — credit the row at (customer, redeeming tenant).
            bool ok = await _loyaltyRewardsService.AddPointsAsync(request.CustomerId, request.TenantId, request.Points, request.Reason);
            if (!ok)
            {
                _logger.LogWarning("Ledger: Silo REFUND failed for customer {CustomerId} tenant {TenantId}", request.CustomerId, request.TenantId);
                return LedgerResult.Failure("Silo refund failed");
            }

            int newBalance = await GetSiloBalanceAsync(request.CustomerId, new TenantId(request.TenantId), cancellationToken);
            _logger.LogInformation("LEDGER SILO REFUND: {Points} pts → customer {CustomerId} tenant {TenantId} (balance={Balance})",
                request.Points, request.CustomerId, request.TenantId, newBalance);
            return LedgerResult.Ok(newBalance);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ledger: RefundAsync failed for customer {CustomerId}", request.CustomerId);
            return LedgerResult.Failure(ex.Message);
        }
    }

    /// <inheritdoc/>
    public async Task<int> RevertOrderAsync(Guid orderId, TenantId tenantId, string reason, CancellationToken cancellationToken = default)
    {
        var records = await _dbContext.LoyaltyIssuanceRecords
            .IgnoreQueryFilters()
            .Where(r => r.OrderId == orderId && r.TenantId == tenantId && !r.IsReversed)
            .ToListAsync(cancellationToken);

        if (records.Count == 0)
        {
            _logger.LogDebug("Ledger: RevertOrder — no non-reversed issuance records for order {OrderId}", orderId);
            return 0;
        }

        int totalReversed = 0;
        foreach (LoyaltyIssuanceRecord record in records)
        {
            bool reversed = false;

            // Mode-aware reversal: Alliance → wallet deduct (takes the pool back); Silo → row subtract.
            if (_loyaltyModeResolver is not null && _allianceWalletService is not null
                && await IsAllianceMemberAsync(record.TenantId.Value))
            {
                Customer? customer = await GetCustomerCrossTenantAsync(record.CustomerId, cancellationToken);
                Guid deviceGuid = customer?.DeviceId ?? record.CustomerId;
                var (ok, _, error) = await _allianceWalletService.DeductPointsAsync(
                    deviceGuid, record.TenantId.Value, record.PointsIssued, $"Reversal: {reason}",
                    idempotencyKey: $"revert:{orderId}:{record.Id}");
                reversed = ok;
                if (!ok)
                {
                    _logger.LogWarning("Ledger: Alliance reversal skipped for order {OrderId} customer {CustomerId}: {Error}",
                        orderId, record.CustomerId, error);
                }
            }
            else
            {
                try
                {
                    reversed = await _loyaltyRewardsService.SubtractPointsAsync(
                        record.CustomerId, record.TenantId.Value, record.PointsIssued, $"Reversal: {reason}");
                }
                catch (IdentityLevelNotSufficientException ex)
                {
                    // Reversal must never fail the refund because the customer is unverified.
                    _logger.LogWarning(ex, "Ledger: Silo reversal skipped for order {OrderId} customer {CustomerId} (verification gate) — points retained",
                        orderId, record.CustomerId);
                }
            }

            // Always mark reversed: the order's issuance is undone even when the points were already
            // consumed elsewhere — prevents infinite re-reversal. Counters decrement by the ACTUAL
            // reversed amount only (caller uses the return value).
            record.MarkReversed();
            if (reversed)
            {
                totalReversed += record.PointsIssued;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Ledger: RevertOrder {OrderId} — {Records} record(s), {Points} pts actually reversed", orderId, records.Count, totalReversed);
        return totalReversed;
    }

    // ──────────────────────────────────────────────────────────
    // READS
    // ──────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<int?> GetAwardedPointsAsync(Guid orderId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        LoyaltyIssuanceRecord? record = await _dbContext.LoyaltyIssuanceRecords
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(r => r.OrderId == orderId && r.TenantId == new TenantId(tenantId) && !r.IsReversed)
            .OrderByDescending(r => r.IssuedAt)
            .FirstOrDefaultAsync(cancellationToken);
        return record?.PointsIssued;
    }

    /// <inheritdoc/>
    public async Task<int> GetBalanceAsync(Guid customerId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        if (_loyaltyModeResolver is not null && _allianceWalletService is not null)
        {
            try
            {
                LoyaltyMode mode = await _loyaltyModeResolver.GetEffectiveModeAsync(tenantId);
                if (mode == LoyaltyMode.Alliance && await _loyaltyModeResolver.IsAllianceMemberAsync(tenantId))
                {
                    Customer? customer = await GetCustomerCrossTenantAsync(customerId, cancellationToken);
                    Guid deviceGuid = customer?.DeviceId ?? customerId;
                    AllianceWallet? wallet = await _allianceWalletService.GetWalletByDeviceIdAsync(deviceGuid);
                    return wallet?.TotalPointBalance ?? 0;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Ledger: wallet balance query failed for customer {CustomerId} — falling back to Silo row", customerId);
            }
        }

        return await GetSiloBalanceAsync(customerId, new TenantId(tenantId), cancellationToken);
    }

    // ──────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────

    private async Task<bool> IsLedgerEnabledAsync(CancellationToken cancellationToken)
    {
        if (_featureFlagService is null)
        {
            return true;
        }

        try
        {
            return await _featureFlagService.IsEnabledAsync(LedgerFlag, defaultWhenMissing: true, cancellationToken);
        }
        catch (Exception ex)
        {
            // Fail-open: never block loyalty writes because the flag service is unavailable.
            _logger.LogWarning(ex, "Ledger: failed to check {Flag} — defaulting to ENABLED", LedgerFlag);
            return true;
        }
    }

    private async Task<bool> IsAllianceMemberAsync(Guid tenantId)
    {
        try
        {
            return _loyaltyModeResolver is not null && await _loyaltyModeResolver.IsAllianceMemberAsync(tenantId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ledger: IsAllianceMemberAsync failed for tenant {TenantId} — treating as non-member", tenantId);
            return false;
        }
    }

    private async Task<Customer?> GetCustomerCrossTenantAsync(Guid customerId, CancellationToken cancellationToken)
    {
        // Cross-tenant lookup (internal API scope has no ambient tenant) — mirrors
        // LoyaltyRewardsRepository.GetCustomerByIdAsync (Bug 6 fix pattern).
        return await _dbContext.Customers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == customerId && !c.IsDeleted, cancellationToken);
    }

    private async Task<Customer> EnsureCustomerStubAsync(Guid customerId, TenantId tenantId, Guid? deviceId, CancellationToken cancellationToken)
    {
        // FK safety: LoyaltyRewards.CustomerId → Customers.Id (single-identity sync, same pattern
        // as LoyaltySyncSubscriber/OrderWorkflowService.CreateCustomerStubAsync).
        var stub = new Customer(tenantId, "Khách hàng", "N/A");
        typeof(BaseEntity).GetProperty(nameof(BaseEntity.Id))!.SetValue(stub, customerId);
        typeof(Customer).GetProperty(nameof(Customer.CustomerId))!.SetValue(stub, new CustomerId(customerId));
        stub.UpdateCustomerDetails("Khách hàng", "N/A", null, "Bronze", deviceId, true);

        if (_customerRepository is not null)
        {
            _ = await _customerRepository.AddAsync(stub);
        }
        else
        {
            _ = _dbContext.Customers.Add(stub);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Ledger: created customer stub {CustomerId} at tenant {TenantId} (ledger FK safety)", customerId, tenantId.Value);
        return stub;
    }

    private async Task<int> GetSiloBalanceAsync(Guid customerId, TenantId tenantId, CancellationToken cancellationToken)
    {
        return await _dbContext.LoyaltyRewards
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(r => r.CustomerId == customerId && r.TenantId == tenantId)
            .Select(r => (int?)r.PointBalance)
            .FirstOrDefaultAsync(cancellationToken) ?? 0;
    }

    private async Task<bool> HasSiloEarnForOrderAsync(Guid customerId, TenantId tenantId, Guid orderId, CancellationToken cancellationToken)
    {
        LoyaltyRewards? row = await _dbContext.LoyaltyRewards
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.CustomerId == customerId && r.TenantId == tenantId, cancellationToken);
        if (row is null || string.IsNullOrEmpty(row.History))
        {
            return false;
        }

        try
        {
            var history = JsonSerializer.Deserialize<List<LoyaltyHistoryEntry>>(row.History);
            return history is not null && history.Any(h =>
                h.Type == "EARN" && h.Reason.Contains($"#{orderId}", StringComparison.OrdinalIgnoreCase));
        }
        catch (JsonException)
        {
            // Corrupt history — treat as no-match; the issuance-record guard still protects.
            return false;
        }
    }

    private async Task CreateIssuanceRecordIfOrderAsync(AwardRequest request, int points, CancellationToken cancellationToken)
    {
        if (!request.SourceOrderId.HasValue)
        {
            return;
        }

        var record = new LoyaltyIssuanceRecord(
            new TenantId(request.TenantId), request.SourceOrderId.Value, request.CustomerId, points);
        _ = _dbContext.LoyaltyIssuanceRecords.Add(record);
        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogDebug("Ledger: LoyaltyIssuanceRecord created for order {OrderId} ({Points} pts → customer {CustomerId})",
            request.SourceOrderId.Value, points, request.CustomerId);
    }
}
