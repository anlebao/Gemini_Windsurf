namespace VanAn.CoreHub.Services.Membership
{
    /// <summary>
    /// Membership Infrastructure (2026-09-29): Hồ sơ HTX — đánh dấu tenant là HTX (A1)
    /// + phiên bản Điều lệ/Điều kiện hiện hành cho consent (SRS §14).
    /// 1 HtxProfile / tenant (unique index).
    /// </summary>
    public interface IHtxProfileService
    {
        /// <summary>Lấy profile HTX của tenant — null nếu tenant chưa đăng ký Membership Infrastructure.</summary>
        Task<HtxProfileDto?> GetAsync(Guid htxTenantId, CancellationToken ct = default);

        /// <summary>2026-10-02: danh sách MỌI HTX đã đăng ký Membership (SystemAdmin — bộ chọn review).</summary>
        Task<IReadOnlyList<HtxProfileSummaryDto>> ListAsync(CancellationToken ct = default);

        /// <summary>Tạo mới nếu chưa có (đánh dấu tenant là HTX), trả về profile.</summary>
        Task<HtxProfileDto> GetOrCreateAsync(Guid htxTenantId, string charterVersion, string termsVersion, CancellationToken ct = default);

        /// <summary>Cập nhật phiên bản Điều lệ hiện hành.</summary>
        Task UpdateCharterAsync(Guid htxTenantId, string charterVersion, string? charterUrl, CancellationToken ct = default);

        /// <summary>Cập nhật phiên bản điều kiện gia nhập.</summary>
        Task UpdateTermsAsync(Guid htxTenantId, string termsVersion, CancellationToken ct = default);
    }
}
