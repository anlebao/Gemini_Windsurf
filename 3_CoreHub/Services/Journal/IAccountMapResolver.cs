using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.Journal
{
    /// <summary>Kết quả resolve event→TK: cặp Nợ/Có + version rule đã dùng (JE.MappingVersion).</summary>
    public sealed record AccountMapResult(string Debit, string Credit, string? MappingVersion);

    /// <summary>
    /// SRS v1.2 W2/B (G-H1, M5): resolve event code → cặp TK Nợ/Có bằng DATA
    /// (AccountMappingRules) — precedence tenant rule &gt; regime rule &gt; builtin
    /// hardcode fallback (seed = parity với hardcode hiện tại — zero behavior change).
    /// Trả null khi không có rule → caller ghi PostingStatus=Skipped("no-rule"), KHÔNG đoán.
    /// </summary>
    public interface IAccountMapResolver
    {
        /// <summary>
        /// Resolve event code cho tenant. Placeholder "{src}" = accountHint (entry.AccountCode),
        /// "{cash}" = cashHint (entry.CashAccountCode, default "111").
        /// </summary>
        Task<AccountMapResult?> ResolveAsync(
            string eventCode,
            TenantId tenantId,
            string? accountHint,
            string? cashHint = null,
            CancellationToken cancellationToken = default);
    }
}
