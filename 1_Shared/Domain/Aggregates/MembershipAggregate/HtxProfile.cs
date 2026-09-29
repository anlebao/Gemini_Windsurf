using VanAn.Shared.Domain.Common;

namespace VanAn.Shared.Domain.Aggregates.MembershipAggregate
{
    /// <summary>
    /// Membership Infrastructure (2026-09-29): Hồ sơ HTX — đánh dấu tenant là HTX (quyết định A1)
    /// + phiên bản Điều lệ/Điều kiện hiện hành (cho consent versioning — SRS §14).
    ///
    /// Entity (BaseEntity — không lifecycle riêng).
    /// Một tenant chỉ có TỐI ĐA 1 HtxProfile (unique index TenantId — EF config Phase 2).
    /// KHÔNG sửa TenantSettings (tránh phá 12 With methods) — đây là nơi duy nhất đánh dấu HTX.
    /// </summary>
    public class HtxProfile : BaseEntity
    {
        public string CharterVersion { get; private set; } = string.Empty;   // Phiên bản Điều lệ hiện hành
        public string TermsVersion { get; private set; } = string.Empty;     // Phiên bản điều kiện gia nhập
        public string? CharterUrl { get; private set; }                      // Link/ref tài liệu Điều lệ

        // EF Core requires parameterless constructor
        private HtxProfile() { }

        /// <summary>
        /// Factory: tạo hồ sơ HTX cho 1 tenant (đánh dấu tenant tham gia Membership Infrastructure).
        /// </summary>
        public static HtxProfile Create(
            TenantId htxTenantId,
            string charterVersion,
            string termsVersion,
            string? charterUrl = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(charterVersion);
            ArgumentException.ThrowIfNullOrWhiteSpace(termsVersion);

            var profile = new HtxProfile
            {
                CharterVersion = charterVersion,
                TermsVersion = termsVersion,
                CharterUrl = charterUrl
            };
            profile.SetTenantId(htxTenantId);
            return profile;
        }

        /// <summary>Cập nhật phiên bản Điều lệ hiện hành (applicant mới consent theo bản mới).</summary>
        public void UpdateCharter(string charterVersion, string? charterUrl = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(charterVersion);
            CharterVersion = charterVersion;
            CharterUrl = charterUrl;
            UpdateAudit();
        }

        /// <summary>Cập nhật phiên bản điều kiện gia nhập.</summary>
        public void UpdateTerms(string termsVersion)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(termsVersion);
            TermsVersion = termsVersion;
            UpdateAudit();
        }
    }
}
