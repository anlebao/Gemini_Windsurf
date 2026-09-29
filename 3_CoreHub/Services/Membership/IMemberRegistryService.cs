namespace VanAn.CoreHub.Services.Membership
{
    /// <summary>
    /// Membership Infrastructure (2026-09-29): Sổ đăng ký thành viên điện tử (SRS §17, §22).
    /// Registry PG-only (A3 — không sync SQLite).
    ///
    /// Public status verify (GetStatusAsync) KHÔNG trả PII — chỉ status + HTX name + joined (SRS §22).
    /// </summary>
    public interface IMemberRegistryService
    {
        /// <summary>Danh sách thành viên của 1 HTX (HTX officer, tenant-scoped).</summary>
        Task<IReadOnlyList<MemberDto>> ListForHtxAsync(Guid htxTenantId, string? status = null, CancellationToken ct = default);

        /// <summary>Chi tiết 1 member (HTX officer).</summary>
        Task<MemberDto?> GetAsync(Guid memberId, CancellationToken ct = default);

        /// <summary>
        /// Public verification (SRS §22 — quét Member QR). Tra theo MemberNumber.
        /// KHÔNG PII: chỉ Active flag + status + member number + HTX name + joined.
        /// Cross-tenant read CÓ CHỦ ĐÍCH (IgnoreQueryFilters) — token/QR là credential.
        /// </summary>
        Task<MemberStatusDto?> GetStatusAsync(string memberNumber, CancellationToken ct = default);

        /// <summary>Active → Suspended (tạm đình chỉ — chỉ Active).</summary>
        Task SuspendAsync(Guid memberId, string reason, CancellationToken ct = default);

        /// <summary>Suspended → Active (khôi phục — chỉ Suspended).</summary>
        Task ReactivateAsync(Guid memberId, CancellationToken ct = default);

        /// <summary>Active/Suspended → Resigned (tự rút).</summary>
        Task ResignAsync(Guid memberId, string reason, CancellationToken ct = default);

        /// <summary>Active/Suspended → Terminated (chấm dứt tư cách).</summary>
        Task TerminateAsync(Guid memberId, string reason, CancellationToken ct = default);

        /// <summary>Danh sách HTX mà 1 network identity (customer) đang là member — cross-tenant read có chủ đích.</summary>
        Task<IReadOnlyList<MemberDto>> ListForCustomerAsync(Guid customerId, CancellationToken ct = default);
    }
}
