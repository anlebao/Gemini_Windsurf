using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure.Entities;
using VanAn.CoreHub.Services.Journal;

namespace VanAn.CoreHub.Infrastructure.Seed;

/// <summary>
/// SRS v1.2 W2/B (R5 — approved 2026-10-31): seed AccountMappingRules regime-scope =
/// parity với hardcode hiện tại (zero behavior change khi flag AccountMappingEnabled ON).
/// Idempotent per (Regime, EventCode, TenantId=null) — KHÔNG cleanup (rule là dữ liệu
/// admin-editable, khác AccountCharts reference-data clear+reseed).
///
/// payable cố tình KHÔNG seed — resolver trả null → Skipped("no-rule") cho đến khi
/// tenant cấu hình counter-account (156/6xx — Phase E gợi ý).
/// </summary>
public static class AccountMapSeeder
{
    private static readonly string[] AllRegimes =
        [AccountingRegimes.TT152, AccountingRegimes.TT58, AccountingRegimes.TT133, AccountingRegimes.TT99, AccountingRegimes.TT71];

    private static readonly string[] AccrualRegimes =
        [AccountingRegimes.TT133, AccountingRegimes.TT99, AccountingRegimes.TT71];

    /// <summary>Seed regime rules. Idempotent — skip (regime,event) đã có row global.</summary>
    public static async Task<int> SeedAsync(IAccountingDbContext dbContext, ILogger? logger = null, CancellationToken ct = default)
    {
        var wanted = new List<(string Regime, string Event, string? Debit, string? Credit)>();

        foreach (string regime in AllRegimes)
        {
            // Universal events — parity ResolveAccounts cũ + order-vat (GL completeness fix).
            wanted.Add((regime, AccountMapEventCodes.ManualReceipt, "{cash}", "{src}"));
            wanted.Add((regime, AccountMapEventCodes.ManualExpense, "{src}", "{cash}"));
            wanted.Add((regime, AccountMapEventCodes.DepositOffset, "3387", "{src}"));
            wanted.Add((regime, AccountMapEventCodes.OrderRevenue, "111", "{src}"));
            wanted.Add((regime, AccountMapEventCodes.OrderVat, "111", "3331"));
            wanted.Add((regime, AccountMapEventCodes.OrderCogs, "{src}", "{cash}"));
        }

        foreach (string regime in AccrualRegimes)
        {
            // Accrual JEs chỉ cho DN/HTX (gate AccrualReceivableJE giữ nguyên ở bridge).
            string receivableCredit = regime == AccountingRegimes.TT71 ? "512" : "511";
            wanted.Add((regime, AccountMapEventCodes.Receivable, "{src}", receivableCredit));
            wanted.Add((regime, AccountMapEventCodes.ReceivablePayment, "{cash}", "{src}"));
            wanted.Add((regime, AccountMapEventCodes.PayablePayment, "{src}", "{cash}"));
        }

        List<string> eventCodes = wanted.Select(w => w.Event).Distinct().ToList();
        List<AccountMappingRuleEntity> existing = await dbContext.AccountMappingRules
            .Where(r => r.TenantId == null && eventCodes.Contains(r.EventCode))
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var existingKeys = existing
            .Select(r => (r.Regime ?? string.Empty, r.EventCode))
            .ToHashSet();

        int added = 0;
        foreach ((string regime, string eventCode, string? debit, string? credit) in wanted)
        {
            if (existingKeys.Contains((regime, eventCode)))
            {
                continue;
            }
            dbContext.AccountMappingRules.Add(new AccountMappingRuleEntity(
                eventCode, debit, credit, tenantId: null, regime: regime));
            added++;
        }

        if (added > 0)
        {
            _ = await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        logger?.LogInformation("W2 AccountMapSeeder: seeded {Count} account mapping rules (skipped {Skipped} existing)", added, existing.Count);
        return added;
    }
}
