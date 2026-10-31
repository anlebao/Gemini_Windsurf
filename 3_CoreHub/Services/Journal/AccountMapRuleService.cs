using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Infrastructure.Entities;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.Journal
{
    /// <summary>1 dòng rule effective cho UI: tài khoản (raw — có thể chứa {src}/{cash}),
    /// nguồn áp dụng (tenant/regime/builtin/none), version + id rule tenant (nếu có override).</summary>
    public sealed record AccountMapRuleRow(
        string EventCode,
        string? DebitAccount,
        string? CreditAccount,
        string Source,
        int Version,
        Guid? TenantRuleId,
        bool TenantRuleActive);

    /// <summary>
    /// W2/B follow-up: quản trị account-map per tenant — xem rule effective (merged
    /// tenant→regime→builtin) + tạo/sửa/vô hiệu tenant override. Resolver cache 60s →
    /// thay đổi có hiệu lực sau tối đa 60s (ghi nhận trên UI).
    /// </summary>
    public interface IAccountMapRuleService
    {
        Task<IReadOnlyList<AccountMapRuleRow>> GetEffectiveRulesAsync(TenantId tenantId, CancellationToken ct = default);
        Task<AccountMappingRuleEntity> UpsertTenantRuleAsync(TenantId tenantId, string eventCode,
            string? debitAccount, string? creditAccount, string? amountKind = "amount",
            DateTime? effectiveFrom = null, CancellationToken ct = default);
        Task DeactivateTenantRuleAsync(TenantId tenantId, string eventCode, CancellationToken ct = default);
    }

    public sealed class AccountMapRuleService(
        IAccountingDbContext dbContext,
        ILogger<AccountMapRuleService> logger,
        IVasFeatureFlagService? vasFeatureFlagService = null) : IAccountMapRuleService
    {
        private readonly IAccountingDbContext _dbContext = dbContext;
        private readonly ILogger<AccountMapRuleService> _logger = logger;
        private readonly IVasFeatureFlagService? _vasFeatureFlagService = vasFeatureFlagService;

        /// <summary>Event list hiển thị (canonical order — khớp seeder + builtin).</summary>
        private static readonly string[] EventList =
        [
            AccountMapEventCodes.ManualReceipt,
            AccountMapEventCodes.ManualExpense,
            AccountMapEventCodes.OrderRevenue,
            AccountMapEventCodes.OrderVat,
            AccountMapEventCodes.OrderCogs,
            AccountMapEventCodes.DepositOffset,
            AccountMapEventCodes.Receivable,
            AccountMapEventCodes.ReceivablePayment,
            AccountMapEventCodes.Payable,
            AccountMapEventCodes.PayablePayment,
            AccountMapEventCodes.TransferOut,
            AccountMapEventCodes.TransferIn,
            AccountMapEventCodes.TransferFee,
        ];

        /// <inheritdoc />
        public async Task<IReadOnlyList<AccountMapRuleRow>> GetEffectiveRulesAsync(TenantId tenantId, CancellationToken ct = default)
        {
            string? regime = await GetRegimeAsync(tenantId, ct);
            List<AccountMappingRuleEntity> rules = await _dbContext.AccountMappingRules
                .AsNoTracking()
                .Where(r => r.IsActive && (r.TenantId == tenantId.Value || r.TenantId == null))
                .ToListAsync(ct);

            DateTime now = DateTime.UtcNow;
            List<AccountMapRuleRow> rows = [];
            foreach (string eventCode in EventList)
            {
                var candidates = rules.Where(r => r.EventCode == eventCode
                    && (r.EffectiveFrom == null || r.EffectiveFrom <= now));

                AccountMappingRuleEntity? tenantRule = candidates
                    .Where(r => r.TenantId == tenantId.Value)
                    .OrderByDescending(r => r.Version).FirstOrDefault();
                AccountMappingRuleEntity? regimeRule = candidates
                    .Where(r => r.TenantId == null && (r.Regime == null || r.Regime == regime))
                    .OrderByDescending(r => r.Version).FirstOrDefault();

                if (tenantRule != null)
                {
                    rows.Add(new(eventCode, tenantRule.DebitAccount, tenantRule.CreditAccount,
                        "tenant", tenantRule.Version, tenantRule.Id, true));
                }
                else if (regimeRule != null)
                {
                    rows.Add(new(eventCode, regimeRule.DebitAccount, regimeRule.CreditAccount,
                        "regime", regimeRule.Version, null, false));
                }
                else
                {
                    var builtin = AccountMapResolver.GetBuiltin(eventCode, regime);
                    rows.Add(builtin != null
                        ? new(eventCode, builtin.Value.Debit, builtin.Value.Credit, "builtin", 0, null, false)
                        : new(eventCode, null, null, "none", 0, null, false));
                }
            }
            return rows;
        }

        /// <inheritdoc />
        public async Task<AccountMappingRuleEntity> UpsertTenantRuleAsync(TenantId tenantId, string eventCode,
            string? debitAccount, string? creditAccount, string? amountKind = "amount",
            DateTime? effectiveFrom = null, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(eventCode))
                throw new ArgumentException("EventCode is required.", nameof(eventCode));
            if (string.IsNullOrWhiteSpace(debitAccount) || string.IsNullOrWhiteSpace(creditAccount))
                throw new ArgumentException("Cả TK Nợ và TK Có đều bắt buộc (rule thiếu sẽ fallback builtin).");

            AccountMappingRuleEntity? existing = await _dbContext.AccountMappingRules
                .IgnoreQueryFilters()
                .Where(r => r.TenantId == tenantId.Value && r.EventCode == eventCode && r.IsActive)
                .OrderByDescending(r => r.Version)
                .FirstOrDefaultAsync(ct);

            if (existing != null)
            {
                existing.Update(debitAccount, creditAccount, amountKind, effectiveFrom);
                await _dbContext.SaveChangesAsync(ct);
                _logger.LogInformation("AccountMapRule updated: tenant {TenantId} event {EventCode} → v{Version} (Nợ {Debit}/Có {Credit})",
                    tenantId.Value, eventCode, existing.Version, debitAccount, creditAccount);
                return existing;
            }

            var rule = new AccountMappingRuleEntity(eventCode, debitAccount, creditAccount,
                tenantId: tenantId.Value, regime: null, amountKind: amountKind,
                version: 1, effectiveFrom: effectiveFrom);
            _dbContext.AccountMappingRules.Add(rule);
            await _dbContext.SaveChangesAsync(ct);
            _logger.LogInformation("AccountMapRule created: tenant {TenantId} event {EventCode} v1 (Nợ {Debit}/Có {Credit})",
                tenantId.Value, eventCode, debitAccount, creditAccount);
            return rule;
        }

        /// <inheritdoc />
        public async Task DeactivateTenantRuleAsync(TenantId tenantId, string eventCode, CancellationToken ct = default)
        {
            var active = await _dbContext.AccountMappingRules
                .IgnoreQueryFilters()
                .Where(r => r.TenantId == tenantId.Value && r.EventCode == eventCode && r.IsActive)
                .ToListAsync(ct);
            foreach (var rule in active)
            {
                rule.Deactivate();
            }
            if (active.Count > 0)
            {
                await _dbContext.SaveChangesAsync(ct);
                _logger.LogInformation("AccountMapRule deactivated: tenant {TenantId} event {EventCode} ({Count} rule) — về regime/builtin",
                    tenantId.Value, eventCode, active.Count);
            }
        }

        private async Task<string?> GetRegimeAsync(TenantId tenantId, CancellationToken ct)
        {
            if (_vasFeatureFlagService == null) return null;
            TenantType? type = await _vasFeatureFlagService.GetTenantTypeAsync(tenantId, ct);
            return type switch
            {
                TenantType.HKD => AccountingRegimes.TT152,
                TenantType.Enterprise_SuperSmall => AccountingRegimes.TT58,
                TenantType.Enterprise_SME => AccountingRegimes.TT133,
                TenantType.Enterprise_Large => AccountingRegimes.TT99,
                TenantType.HTX => AccountingRegimes.TT71,
                _ => null
            };
        }
    }
}
