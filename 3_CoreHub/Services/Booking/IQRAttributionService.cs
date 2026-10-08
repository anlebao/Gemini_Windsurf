using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.Booking;

/// <summary>Kết quả resolve QR (SRS §7.3-7.6). AttributionSessionId = session booking phải gắn (immutable snapshot §7.6).</summary>
public sealed record QrResolveResult(
    Guid QrId,
    TenantId TenantId,
    Guid? SalesmanId,
    Guid? CampaignId,
    Guid AttributionSessionId,
    bool IsQualified,
    bool IsNewSession);

/// <summary>Chi tiết QR cho tenant (Q1 2026-10-08 — issue #188 bug 2): link + QR PNG render lại sau khi tạo.</summary>
public sealed record QrChannelDetailResult(
    Guid QrId,
    string? BookingLink,
    string? QrCodePngBase64,
    bool IsActive,
    DateTime? RevokedAt);

/// <summary>
/// IQRAttributionService — QR attribution (SRS §7). Resolve qr_token → tenant + campaign + salesman + attribution policy.
///
/// Rules (backend authoritative — KHÔNG tin tenant từ client, Risk 5):
/// - §7.3: token tồn tại ∧ active ∧ tenant active ∧ salesman ∈ tenant (active CommunityRole Salesman trong tenant).
/// - §7.5: first-qualified-wins — session qualified đầu tiên (trong tenant + anonymous session) giữ attribution;
///   refresh không tạo session mới, không đổi salesman; cross-tenant contamination bị từ chối.
/// - §26.3: rapid-scan guard — 1 session / (tenant, qr, anonymous session) (unique index) — không tạo vô hạn.
/// - §26.1: QR revoked → không tạo attribution mới.
/// </summary>
public interface IQRAttributionService
{
    /// <summary>Resolve QR + get-or-create/refresh AttributionSession (SRS §7.4-7.5). Throw khi token không hợp lệ.</summary>
    Task<QrResolveResult> ResolveQrAsync(string qrToken, string anonymousSessionId, CancellationToken ct = default);

    /// <summary>Tạo QR channel — raw token được hash SHA-256 khi lưu (opaque, không nhúng dữ liệu nhạy cảm §7.2).</summary>
    Task<QRChannel> CreateQrChannelAsync(
        TenantId tenantId, string qrToken, Guid? salesmanId = null, Guid? campaignId = null,
        DateTime? attributionExpiryAt = null, CancellationToken ct = default);

    /// <summary>Revoke QR (§26.1) — token cũ không tạo attribution mới; booking/ledger đã snapshot giữ nguyên.</summary>
    Task RevokeQrAsync(TenantId tenantId, Guid qrId, CancellationToken ct = default);

    /// <summary>Đọc AttributionSession (P3.2 commission — attribution valid = IsQualified ∧ chưa hết hạn).</summary>
    Task<AttributionSession?> GetAttributionAsync(TenantId tenantId, Guid attributionId, CancellationToken ct = default);

    /// <summary>
    /// Chi tiết QR cho tenant (Q1 2026-10-08): decrypt EncryptedToken → link đặt lịch + QR PNG (QRCoder).
    /// Chỉ tenant sở hữu + channel active. Revoked/legacy (không EncryptedToken) → BookingLink/QR null.
    /// </summary>
    Task<QrChannelDetailResult> GetQrChannelDetailAsync(TenantId tenantId, Guid qrId, string bookingBaseUrl, CancellationToken ct = default);
}
