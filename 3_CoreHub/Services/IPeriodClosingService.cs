using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// Service for accounting period closing operations.
    /// AccountingEntry immutability is enforced — period reopening uses Reversal Entry pattern.
    /// </summary>
    public interface IPeriodClosingService
    {
        Task<PeriodClosingCheckResult> ValidatePeriodAsync(
            AccountingPeriod period,
            TenantId tenantId,
            CancellationToken cancellationToken = default);

        Task<ClosingEntry> ClosePeriodAsync(
            AccountingPeriod period,
            TenantId tenantId,
            Guid userId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// W4 (P1.2): non-mutating preview trước khi mở lại kỳ — số bút toán sẽ bị đảo ngược + tổng giá trị.
        /// </summary>
        Task<ReopenPreview> PreviewReopenAsync(
            AccountingPeriod period,
            TenantId tenantId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Mở lại kỳ kế toán đã đóng — toàn bộ bút toán trong kỳ bị đảo ngược (Reversal Entry).
        /// W4 (P1.2): <paramref name="approverRoles"/> phải chứa Owner, ChiefAccountant hoặc
        /// SystemAdmin; <paramref name="reason"/> bắt buộc không rỗng.
        /// </summary>
        Task ReopenPeriodAsync(
            AccountingPeriod period,
            TenantId tenantId,
            Guid userId,
            string reason,
            IReadOnlyList<string> approverRoles,
            CancellationToken cancellationToken = default);

        // SRS v1.2 W5/F: bút toán kết chuyển 911 (regime-gated TT133/TT99/TT71 —
        // HKD/TT152 + DNSN/TT58 skip). Idempotent per period; gọi tự động trong
        // ClosePeriodAsync, hoặc thủ công qua nút "Tạo bút toán kết chuyển".
        Task<int> GenerateClosingEntriesAsync(
            AccountingPeriod period,
            TenantId tenantId,
            Guid userId,
            CancellationToken cancellationToken = default);

        Task<PeriodClosingStatus> GetPeriodStatusAsync(
            AccountingPeriod period,
            TenantId tenantId,
            CancellationToken cancellationToken = default);
    }
}
