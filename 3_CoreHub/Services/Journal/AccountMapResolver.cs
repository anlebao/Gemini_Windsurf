using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Infrastructure.Entities;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.Journal
{
    /// <summary>
    /// SRS v1.2 W2/B: AccountMapResolver — precedence tenant rule &gt; regime rule &gt;
    /// builtin hardcode (T-parity: output giống ResolveAccounts cũ cho mọi event đã seed).
    /// Cache DB rules 60s per (tenant, eventCode) — thay rule qua Update/Deactivate có
    /// hiệu lực sau tối đa 60s (ghi nhận: invalidation chủ động defer B2).
    /// </summary>
    public sealed class AccountMapResolver(
        IAccountingDbContext dbContext,
        ILogger<AccountMapResolver> logger,
        IVasFeatureFlagService? vasFeatureFlagService = null) : IAccountMapResolver
    {
        private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

        private readonly IAccountingDbContext _dbContext = dbContext;
        private readonly ILogger<AccountMapResolver> _logger = logger;
        private readonly IVasFeatureFlagService? _vasFeatureFlagService = vasFeatureFlagService;
        private readonly ConcurrentDictionary<string, (List<AccountMappingRuleEntity> Rules, DateTime ExpiresAt)> _cache = new();

        /// <inheritdoc />
        public async Task<AccountMapResult?> ResolveAsync(
            string eventCode,
            TenantId tenantId,
            string? accountHint,
            string? cashHint = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(eventCode))
            {
                return null;
            }

            string? regime = await GetRegimeAsync(tenantId, cancellationToken);
            List<AccountMappingRuleEntity> rules = await GetRulesAsync(tenantId, eventCode, cancellationToken);

            // Precedence 1: tenant rule (mới nhất theo Version)
            AccountMappingRuleEntity? rule = PickRule(rules.Where(r => r.TenantId == tenantId.Value));

            // Precedence 2: regime rule (global — TenantId null, Regime match hoặc null)
            rule ??= PickRule(rules.Where(r =>
                r.TenantId == null && (r.Regime == null || r.Regime == regime)));

            if (rule != null)
            {
                AccountMapResult? resolved = TryResolve(rule.DebitAccount, rule.CreditAccount, accountHint, cashHint, $"v{rule.Version}");
                if (resolved != null)
                {
                    return resolved;
                }
                // Rule thiếu placeholder hint → rơi xuống builtin (rule không dùng được).
                _logger.LogWarning(
                    "AccountMapResolver: rule {RuleId} (event {EventCode}, tenant {TenantId}) thiếu hint — fallback builtin",
                    rule.Id, eventCode, tenantId.Value);
            }

            // Precedence 3: builtin hardcode (parity).
            (string debit, string credit)? builtin = GetBuiltin(eventCode, regime);
            if (builtin == null)
            {
                return null; // không có rule → Skipped("no-rule") ở caller
            }
            return TryResolve(builtin.Value.debit, builtin.Value.credit, accountHint, cashHint, "builtin");
        }

        private static AccountMappingRuleEntity? PickRule(IEnumerable<AccountMappingRuleEntity> candidates)
        {
            DateTime now = DateTime.UtcNow;
            return candidates
                .Where(r => r.EffectiveFrom == null || r.EffectiveFrom <= now)
                .OrderByDescending(r => r.Version)
                .FirstOrDefault();
        }

        /// <summary>Thay "{src}"/"{cash}"; null khi tài khoản trống sau thay thế.</summary>
        private static AccountMapResult? TryResolve(string? debit, string? credit, string? accountHint, string? cashHint, string version)
        {
            string? resolvedDebit = Substitute(debit, accountHint, cashHint);
            string? resolvedCredit = Substitute(credit, accountHint, cashHint);
            return string.IsNullOrWhiteSpace(resolvedDebit) || string.IsNullOrWhiteSpace(resolvedCredit)
                ? null
                : new AccountMapResult(resolvedDebit, resolvedCredit, version);
        }

        private static string? Substitute(string? account, string? accountHint, string? cashHint)
        {
            if (string.IsNullOrWhiteSpace(account))
            {
                return null;
            }
            string result = account
                .Replace("{src}", accountHint ?? string.Empty)
                .Replace("{cash}", string.IsNullOrWhiteSpace(cashHint) ? "111" : cashHint);
            return string.IsNullOrWhiteSpace(result) ? null : result;
        }

        /// <summary>TenantType → regime key (TT152/TT58/TT133/TT99/TT71). null khi không xác định.</summary>
        private async Task<string?> GetRegimeAsync(TenantId tenantId, CancellationToken cancellationToken)
        {
            if (_vasFeatureFlagService == null)
            {
                return null;
            }
            TenantType? type = await _vasFeatureFlagService.GetTenantTypeAsync(tenantId, cancellationToken);
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

        private async Task<List<AccountMappingRuleEntity>> GetRulesAsync(TenantId tenantId, string eventCode, CancellationToken cancellationToken)
        {
            string cacheKey = $"{tenantId.Value:N}:{eventCode}";
            if (_cache.TryGetValue(cacheKey, out var cached) && cached.ExpiresAt > DateTime.UtcNow)
            {
                return cached.Rules;
            }

            // 1 query: tenant rules + toàn bộ global rules của event (regime filter ở memory).
            List<AccountMappingRuleEntity> rules = await _dbContext.AccountMappingRules
                .AsNoTracking()
                .Where(r => r.EventCode == eventCode && r.IsActive
                            && (r.TenantId == tenantId.Value || r.TenantId == null))
                .ToListAsync(cancellationToken);

            _cache[cacheKey] = (rules, DateTime.UtcNow.Add(CacheTtl));
            return rules;
        }

        /// <summary>
        /// Builtin hardcode = parity với ResolveAccounts cũ + order-vat (fix GL thiếu nợ thuế).
        /// Accrual events (131/331) chỉ tồn tại cho regime DN/HTX (TT133/TT99/TT71) —
        /// HKD/DNSN cash-basis không JE công nợ. payable: KHÔNG có builtin (needs-account-map).
        /// </summary>
        internal static (string Debit, string Credit)? GetBuiltin(string eventCode, string? regime)
        {
            switch (eventCode)
            {
                case AccountMapEventCodes.ManualReceipt: return ("{cash}", "{src}");
                case AccountMapEventCodes.ManualExpense: return ("{src}", "{cash}");
                case AccountMapEventCodes.DepositOffset: return ("3387", "{src}");
                case AccountMapEventCodes.OrderRevenue: return ("111", "{src}");
                case AccountMapEventCodes.OrderVat: return ("111", "3331");
                case AccountMapEventCodes.OrderCogs: return ("{src}", "{cash}");
                // W3/D: hints truyền sẵn — {src}=TK đích (hoặc 113 khi InTransit), {cash}=TK nguồn;
                // transfer-in: {src}=TK đích, {cash}=113; transfer-fee: {src}=TK phí (642), {cash}=nguồn.
                case AccountMapEventCodes.TransferOut: return ("{src}", "{cash}");
                case AccountMapEventCodes.TransferIn: return ("{src}", "{cash}");
                case AccountMapEventCodes.TransferFee: return ("{src}", "{cash}");
                case AccountMapEventCodes.Receivable:
                    return regime is AccountingRegimes.TT133 or AccountingRegimes.TT99
                        ? ("{src}", "511")
                        : regime == AccountingRegimes.TT71
                            ? ("{src}", "512")
                            : null;
                case AccountMapEventCodes.ReceivablePayment:
                    return regime is AccountingRegimes.TT133 or AccountingRegimes.TT99 or AccountingRegimes.TT71
                        ? ("{cash}", "{src}")
                        : null;
                case AccountMapEventCodes.PayablePayment:
                    return regime is AccountingRegimes.TT133 or AccountingRegimes.TT99 or AccountingRegimes.TT71
                        ? ("{src}", "{cash}")
                        : null;
                default:
                    return null; // payable + unknown → no-rule
            }
        }
    }

    /// <summary>Event codes chuẩn của AccountMappingRules (SRS v1.2 §4.B).</summary>
    public static class AccountMapEventCodes
    {
        public const string ManualReceipt = "manual-receipt";
        public const string ManualExpense = "manual-expense";
        public const string DepositOffset = "deposit-offset";
        public const string OrderRevenue = "order-revenue";
        public const string OrderVat = "order-vat";
        public const string OrderCogs = "order-cogs";
        public const string Receivable = "receivable";
        public const string ReceivablePayment = "receivable-payment";
        public const string Payable = "payable";
        public const string PayablePayment = "payable-payment";
        public const string Transfer = "transfer";      // Phase D
        public const string TransferOut = "transfer-out";   // W3/D: Nợ {đích|113} / Có {nguồn}
        public const string TransferIn = "transfer-in";     // W3/D: Nợ {đích} / Có 113
        public const string TransferFee = "transfer-fee";   // W3/D: Nợ {642} / Có {nguồn} (phí riêng)
        public const string ClosingPrefix = "closing-"; // Phase F
    }

    /// <summary>Regime keys trên AccountMappingRuleEntity.Regime.</summary>
    public static class AccountingRegimes
    {
        public const string TT152 = "TT152"; // HKD
        public const string TT58 = "TT58";   // DN siêu nhỏ
        public const string TT133 = "TT133"; // DN vừa/nhỏ
        public const string TT99 = "TT99";   // DN lớn
        public const string TT71 = "TT71";   // HTX
    }
}
