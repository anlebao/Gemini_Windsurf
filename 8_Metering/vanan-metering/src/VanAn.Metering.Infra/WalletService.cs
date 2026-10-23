using Microsoft.EntityFrameworkCore;
using VanAn.Metering.Contracts;
using VanAn.Metering.Domain;

namespace VanAn.Metering.Infra;

/// <summary>
/// P7 (task_p7_wallet_topup) — tenant credit wallet.
/// Atomicity (D2): relational providers deduct via conditional UPDATE
/// (balance >= applied) inside the ingest transaction; the in-memory provider
/// used by unit tests falls back to tracked-entity updates.
/// Alerts (D4): % còn lại trên GrantedTotal — ≤50% Warn50, ≤10% Warn90, 0 Depleted.
/// </summary>
public sealed class WalletService(MeteringDbContext db, RateCardService rateCards)
{
    // D4 — configurable thresholds (remaining fraction), defaults 50% / 10%.
    private const decimal Warn50Fraction = 0.5m;
    private const decimal Warn90Fraction = 0.1m;

    public static string? AlertLevelOf(decimal balance, decimal grantedTotal)
    {
        if (balance <= 0) return "depleted";
        if (grantedTotal <= 0) return "depleted"; // ví chưa cấp + cạn → depleted (D4-a)
        var remaining = balance / grantedTotal;
        if (remaining <= Warn90Fraction) return "warn90";
        if (remaining <= Warn50Fraction) return "warn50";
        return null;
    }

    /// <summary>GET /v1/wallets/{t} — read-only view; missing wallet reads as zero-balance (D3-b).</summary>
    public async Task<WalletDto> GetWalletAsync(Guid tenantId, CancellationToken ct = default)
    {
        var w = await db.CreditWallets.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == tenantId, ct);
        return w == null
            ? new WalletDto(tenantId, 0, 0, 0, 0, AlertLevelOf(0, 0), DateTime.UtcNow)
            : new WalletDto(w.TenantId, w.Balance, w.BalancePurchased, w.GrantedTotal,
                w.RemainingFraction, AlertLevelOf(w.Balance, w.GrantedTotal), w.UpdatedAtUtc);
    }

    /// <summary>
    /// D3 — check/deny. required = rate × estimate (default 1 unit); deny only when
    /// the resolved head version is Enforcement=Deny AND balance &lt; required.
    /// Unpriced / MeterOnly → deny=false always (D11).
    /// </summary>
    public async Task<WalletCheckResponse> CheckAsync(
        Guid tenantId, string serviceCode, decimal? estimate, DateTime nowUtc, CancellationToken ct = default)
    {
        var version = await rateCards.ResolveAsync(serviceCode, nowUtc, ct);
        var enforcement = version?.Enforcement.ToString();
        var required = (version?.CreditRate ?? 0) * Math.Max(estimate ?? 1m, 0m);
        var w = await db.CreditWallets.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == tenantId, ct);
        var balance = w?.Balance ?? 0;
        var deny = version?.Enforcement == MeteringEnforcement.Deny && balance < required;
        return new WalletCheckResponse(
            tenantId, serviceCode, enforcement, balance, required, deny,
            AlertLevelOf(balance, w?.GrantedTotal ?? 0));
    }

    /// <summary>
    /// D10 — admin grant (initial quota / manual adjust). BalancePurchased + GrantedTotal
    /// increase; CycleKey bumps so threshold alerts may re-fire. Same SaveChanges as
    /// the ledger entry + audit (callers may wrap in an outer transaction).
    /// </summary>
    public async Task<CreditWallet> GrantAsync(
        Guid tenantId, decimal credits, string reason, string actor, DateTime nowUtc,
        Guid? topupRequestId = null, CancellationToken ct = default)
    {
        // New cycle identity per grant — alerts may re-fire on the new cycle (D4-b).
        var cycleKey = Guid.NewGuid().ToString("N");

        if (db.Database.IsRelational())
        {
            await EnsureWalletRelationalAsync(tenantId, nowUtc, ct);
            var rows = await db.Database.ExecuteSqlAsync($"""
                UPDATE "CreditWallets"
                SET "BalancePurchased" = CAST("BalancePurchased" AS NUMERIC) + CAST({credits} AS NUMERIC),
                    "GrantedTotal" = "GrantedTotal" + {credits},
                    "CycleKey" = {cycleKey},
                    "UpdatedAtUtc" = {nowUtc}
                WHERE "TenantId" = {tenantId}
                """, ct);
            if (rows == 0) throw new InvalidOperationException("wallet ensure failed");
        }
        else
        {
            var w = await GetOrCreateWalletAsync(tenantId, nowUtc, ct);
            w.ApplyGrant(credits, cycleKey, nowUtc);
        }

        var balanceAfter = await BalanceOfAsync(tenantId, ct);
        db.CreditLedgerEntries.Add(CreditLedgerEntry.Create(
            tenantId, LedgerEntryType.Grant, LedgerEntrySource.Purchased, credits,
            balanceAfter, reason, actor, nowUtc, topupRequestId: topupRequestId));
        db.AuditLogs.Add(AuditLog.Create(actor, "grant", nameof(CreditWallet),
            topupRequestId ?? tenantId, oldJson: null,
            newJson: $"{{\"tenantId\":\"{tenantId}\",\"credits\":{credits},\"reason\":\"{reason.Replace("\"", "\\\"", StringComparison.Ordinal)}\"}}",
            atUtc: nowUtc));
        await db.SaveChangesAsync(ct);
        return await db.CreditWallets.AsNoTracking().FirstAsync(x => x.TenantId == tenantId, ct);
    }

    /// <summary>
    /// T29 — append-only correction (signed credits, may be negative; clamped at 0
    /// on the debit side). Reverses a wrong grant/topup without mutating history.
    /// </summary>
    public async Task<CreditWallet> AdjustAsync(
        Guid tenantId, decimal credits, string reason, string actor, DateTime nowUtc, CancellationToken ct = default)
    {
        if (db.Database.IsRelational())
        {
            await EnsureWalletRelationalAsync(tenantId, nowUtc, ct);
            await db.Database.ExecuteSqlAsync($"""
                UPDATE "CreditWallets"
                SET "BalancePurchased" = MAX(CAST("BalancePurchased" AS NUMERIC) + CAST({credits} AS NUMERIC), 0),
                    "UpdatedAtUtc" = {nowUtc}
                WHERE "TenantId" = {tenantId}
                """, ct);
        }
        else
        {
            var w = await GetOrCreateWalletAsync(tenantId, nowUtc, ct);
            w.ApplyAdjust(credits, nowUtc);
        }

        var balanceAfter = await BalanceOfAsync(tenantId, ct);
        db.CreditLedgerEntries.Add(CreditLedgerEntry.Create(
            tenantId, LedgerEntryType.Adjust, LedgerEntrySource.Purchased, credits,
            balanceAfter, reason, actor, nowUtc));
        db.AuditLogs.Add(AuditLog.Create(actor, "adjust", nameof(CreditWallet), tenantId,
            oldJson: null, newJson: $"{{\"credits\":{credits},\"reason\":\"{reason.Replace("\"", "\\\"", StringComparison.Ordinal)}\"}}",
            atUtc: nowUtc));
        await db.SaveChangesAsync(ct);
        return await db.CreditWallets.AsNoTracking().FirstAsync(x => x.TenantId == tenantId, ct);
    }

    /// <summary>
    /// D1 — deduct at ingest, inside the caller's transaction (no SaveChanges here).
    /// Atomic conditional UPDATE clamps the deduction to the available balance;
    /// returns (applied). Ledger entry is added to the context for the caller's save.
    /// </summary>
    public async Task<decimal> DeductInTransactionAsync(
        Guid tenantId, Guid usageEventId, decimal credits, string reason, DateTime nowUtc, CancellationToken ct = default)
    {
        decimal applied = credits;
        if (db.Database.IsRelational())
        {
            await EnsureWalletRelationalAsync(tenantId, nowUtc, ct);
            // Decimals are stored as TEXT by EF's SQLite mapping — raw comparisons
            // must CAST both sides or '100' >= '60' becomes lexicographic (false).
            var rows = await db.Database.ExecuteSqlAsync($"""
                UPDATE "CreditWallets"
                SET "BalancePurchased" = CAST("BalancePurchased" AS NUMERIC) - CAST({applied} AS NUMERIC),
                    "UpdatedAtUtc" = {nowUtc}
                WHERE "TenantId" = {tenantId} AND CAST("BalancePurchased" AS NUMERIC) >= CAST({applied} AS NUMERIC)
                """, ct);
            if (rows == 0)
            {
                // Insufficient balance — clamp to what remains (card §5 deduct-f).
                var balance = await BalanceOfAsync(tenantId, ct);
                applied = Math.Min(credits, Math.Max(balance, 0));
                if (applied > 0)
                {
                    rows = await db.Database.ExecuteSqlAsync($"""
                        UPDATE "CreditWallets"
                        SET "BalancePurchased" = CAST("BalancePurchased" AS NUMERIC) - CAST({applied} AS NUMERIC),
                            "UpdatedAtUtc" = {nowUtc}
                        WHERE "TenantId" = {tenantId} AND CAST("BalancePurchased" AS NUMERIC) >= CAST({applied} AS NUMERIC)
                        """, ct);
                    if (rows == 0) applied = 0;
                }
            }
        }
        else
        {
            var w = await GetOrCreateWalletAsync(tenantId, nowUtc, ct);
            applied = Math.Min(credits, Math.Max(w.Balance, 0));
            if (applied > 0) w.ApplyDeduct(applied, nowUtc);
        }

        if (applied > 0)
        {
            var balanceAfter = await BalanceOfAsync(tenantId, ct);
            db.CreditLedgerEntries.Add(CreditLedgerEntry.Create(
                tenantId, LedgerEntryType.Deduct, LedgerEntrySource.Purchased, -applied,
                balanceAfter, reason, "system:ingest", nowUtc, usageEventId: usageEventId));
        }
        return applied;
    }

    /// <summary>
    /// D4 — fire threshold alerts for the CURRENT cycle (one per level per cycle;
    /// unique index guards races). Runs inside the caller's transaction.
    /// </summary>
    public async Task EvaluateAlertsAsync(Guid tenantId, DateTime nowUtc, CancellationToken ct = default)
    {
        // Check the tracker first — a lazily-created wallet (in-memory provider path)
        // may not be persisted yet within this transaction.
        var w = db.CreditWallets.Local.FirstOrDefault(x => x.TenantId == tenantId)
            ?? await db.CreditWallets.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == tenantId, ct);
        if (w == null) return;
        var level = AlertLevelOf(w.Balance, w.GrantedTotal);
        if (level == null) return;
        var levelEnum = level switch
        {
            "warn50" => WalletAlertLevel.Warn50,
            "warn90" => WalletAlertLevel.Warn90,
            _ => WalletAlertLevel.Depleted
        };
        var exists = await db.WalletAlerts.AsNoTracking()
            .AnyAsync(a => a.TenantId == tenantId && a.Level == levelEnum && a.CycleKey == w.CycleKey, ct);
        if (exists) return;
        db.WalletAlerts.Add(WalletAlert.Create(tenantId, levelEnum, w.CycleKey, nowUtc));
    }

    /// <summary>T30 — ledger paging, newest first.</summary>
    public async Task<LedgerListResponse> LedgerAsync(
        Guid tenantId, int page, int pageSize, CancellationToken ct = default)
    {
        var items = await db.CreditLedgerEntries.AsNoTracking()
            .Where(e => e.TenantId == tenantId)
            .OrderByDescending(e => e.CreatedAtUtc).ThenByDescending(e => e.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(e => new LedgerEntryDto(
                e.Id, e.Type.ToString(), e.Credits, e.BalanceAfter, e.Reason, e.CreatedBy,
                e.CreatedAtUtc, e.UsageEventId, e.TopupRequestId))
            .ToListAsync(ct);
        return new LedgerListResponse(items, page, pageSize);
    }

    private async Task<decimal> BalanceOfAsync(Guid tenantId, CancellationToken ct)
    {
        var local = db.CreditWallets.Local.FirstOrDefault(x => x.TenantId == tenantId);
        if (local != null) return local.Balance;
        var w = await db.CreditWallets.AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .Select(x => new { x.BalancePurchased, x.BalancePool, x.BalanceEarned })
            .FirstOrDefaultAsync(ct);
        return w == null ? 0 : w.BalancePurchased + w.BalancePool + w.BalanceEarned;
    }

    /// <summary>Lazy wallet (D3-b) — EF path for non-relational providers.</summary>
    private async Task<CreditWallet> GetOrCreateWalletAsync(Guid tenantId, DateTime nowUtc, CancellationToken ct)
    {
        var w = await db.CreditWallets.FirstOrDefaultAsync(x => x.TenantId == tenantId, ct);
        if (w != null) return w;
        w = CreditWallet.Create(tenantId, nowUtc);
        db.CreditWallets.Add(w);
        // Saved by the caller's SaveChanges (same transaction for grants/deducts).
        return w;
    }

    /// <summary>D3-b — INSERT-if-missing via raw SQL (race-safe: unique index is the arbiter).</summary>
    private async Task EnsureWalletRelationalAsync(Guid tenantId, DateTime nowUtc, CancellationToken ct)
    {
        var id = Guid.NewGuid();
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO "CreditWallets"
                ("Id", "TenantId", "BalancePurchased", "BalancePool", "BalanceEarned",
                 "GrantedTotal", "CycleKey", "UpdatedAtUtc")
            SELECT {id}, {tenantId}, 0, 0, 0, 0, '0', {nowUtc}
            WHERE NOT EXISTS (SELECT 1 FROM "CreditWallets" WHERE "TenantId" = {tenantId})
            """, ct);
    }
}
