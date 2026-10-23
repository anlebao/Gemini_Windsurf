namespace VanAn.Metering.Domain;

/// <summary>
/// P7 (task_p7_wallet_topup) — tenant credit wallet. Balance is DECREMENTED at
/// usage ingest (deduct-at-ingest, D1); grants increase BalancePurchased +
/// GrantedTotal. Balance never goes below 0 — undercharged usage is recorded
/// on UsageEvent.UnpaidCredits instead (MeterOnly continues; Deny decided by
/// the check API, not ingest).
/// CycleKey identifies the current alert cycle — bumped on every Grant so
/// threshold alerts may re-fire for the new cycle (D4).
/// </summary>
public sealed class CreditWallet
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }

    /// <summary>Spendable balance (purchased credits). GĐ-1 uses only this pool.</summary>
    public decimal BalancePurchased { get; private set; }

    /// <summary>Reserved for GĐ-2 pool credits (promo/shared) — always 0 in GĐ-1.</summary>
    public decimal BalancePool { get; private set; }

    /// <summary>Reserved for GĐ-3 earned credits (loyalty/conversion) — always 0 in GĐ-1.</summary>
    public decimal BalanceEarned { get; private set; }

    /// <summary>Total credits ever granted (denominator of the "% remaining" alert, D4).</summary>
    public decimal GrantedTotal { get; private set; }

    /// <summary>Current alert-cycle key — the latest Grant ledger entry id ("0" before first grant).</summary>
    public string CycleKey { get; private set; } = "0";

    /// <summary>Concurrency-safe update marker (informational; atomicity enforced by conditional UPDATE).</summary>
    public DateTime UpdatedAtUtc { get; private set; }

    private CreditWallet() { } // EF Core

    public static CreditWallet Create(Guid tenantId, DateTime nowUtc)
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BalancePurchased = 0,
            BalancePool = 0,
            BalanceEarned = 0,
            GrantedTotal = 0,
            CycleKey = "0",
            UpdatedAtUtc = nowUtc
        };

    /// <summary>Spendable balance across pools — GĐ-1 = BalancePurchased only.</summary>
    public decimal Balance => BalancePurchased + BalancePool + BalanceEarned;

    public void ApplyGrant(decimal credits, string cycleKey, DateTime nowUtc)
    {
        BalancePurchased += credits;
        GrantedTotal += credits;
        CycleKey = cycleKey;
        UpdatedAtUtc = nowUtc;
    }

    /// <summary>Signed correction (T29) — moves BalancePurchased only, clamps at 0;
    /// does NOT touch GrantedTotal (a reversal is not a new quota).</summary>
    public void ApplyAdjust(decimal delta, DateTime nowUtc)
    {
        BalancePurchased = Math.Max(BalancePurchased + delta, 0);
        UpdatedAtUtc = nowUtc;
    }

    public void ApplyDeduct(decimal applied, DateTime nowUtc)
    {
        BalancePurchased -= applied;
        UpdatedAtUtc = nowUtc;
    }

    /// <summary>D4 — % còn lại trên GrantedTotal; ví chưa cấp (GrantedTotal=0) coi như cạn.</summary>
    public decimal RemainingFraction => GrantedTotal <= 0 ? 0m : Balance / GrantedTotal;
}
