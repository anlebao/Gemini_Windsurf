using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QRCoder;
using System.Security.Cryptography;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Infrastructure.DataProtection;
using VanAn.Shared.Domain;
// "Tenant" ambiguous (obsolete record vs AggregateRoot) → alias entity.
using TenantEntity = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant;
using TenantStatus = VanAn.Shared.Domain.Aggregates.TenantAggregate.TenantStatus;

namespace VanAn.CoreHub.Services.Booking;

/// <summary>
/// QRAttributionService — QR resolve + attribution session (SRS §7). Backend authoritative:
/// tenant context được suy từ QR record (KHÔNG tin client — Risk 5 / §18.2).
/// Token lưu dạng SHA-256 hash (opaque §7.2 — không nhúng commission rule/dữ liệu nhạy cảm).
/// </summary>
public sealed class QRAttributionService(
    VanAnDbContext context,
    ILogger<QRAttributionService> logger) : IQRAttributionService
{
    /// <summary>Mặc định attribution expiry khi create QR không truyền (null = không hết hạn).</summary>
    public const int DefaultAttributionExpiryDays = 30;

    private readonly VanAnDbContext _context = context;
    private readonly ILogger<QRAttributionService> _logger = logger;

    public async Task<QrResolveResult> ResolveQrAsync(string qrToken, string anonymousSessionId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(qrToken))
            throw new ValidationException("qr_token không hợp lệ.");
        if (string.IsNullOrWhiteSpace(anonymousSessionId) || anonymousSessionId.Length > 128)
            throw new ValidationException("anonymous_session_id không hợp lệ.");

        // §7.2 — token opaque, lookup theo hash.
        string tokenHash = HashToken(qrToken);

        // §7.3 — token phải tồn tại ∧ active.
        QRChannel? qr = await _context.QRChannels.IgnoreQueryFilters()
            .FirstOrDefaultAsync(q => q.QrTokenHash == tokenHash, ct);
        if (qr is null)
            throw new NotFoundException("QR không tồn tại.");
        if (!qr.IsActive)
        {
            _logger.LogWarning("QR resolve bị từ chối — token revoked: qr={QrId}", qr.Id);
            throw new ValidationException("QR đã bị vô hiệu hóa.");
        }

        // §7.3 — tenant phải active.
        TenantId tenantId = qr.TenantId;
        TenantEntity? tenant = await _context.Tenants.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == tenantId && t.Status == TenantStatus.Active, ct);
        if (tenant is null)
        {
            _logger.LogWarning("QR resolve bị từ chối — tenant không active: tenant={TenantId} qr={QrId}", tenantId.Value, qr.Id);
            throw new ValidationException("QR không khả dụng.");
        }

        // §7.3 — salesman phải thuộc tenant được phép (active CommunityRole Salesman).
        if (qr.SalesmanId is not null)
        {
            bool salesmanValid = await _context.CommunityRoles.IgnoreQueryFilters()
                .AnyAsync(r => r.TenantId == tenantId
                               && r.CustomerId == qr.SalesmanId.Value
                               && r.RoleType == CommunityRoleType.Salesman
                               && r.IsActive, ct);
            if (!salesmanValid)
            {
                _logger.LogWarning("QR resolve bị từ chối — salesman không thuộc tenant: qr={QrId} salesman={SalesmanId}",
                    qr.Id, qr.SalesmanId.Value);
                throw new ValidationException("QR không hợp lệ cho kênh bán hàng này.");
            }
        }

        // §7.4-7.5 — first-qualified-wins trong (tenant, anonymous session).
        // Nếu anonymous session đã có attribution qualified (QR khác) → KHÔNG đổi salesman (refresh LastSeenAt).
        AttributionSession? existingQualified = await _context.AttributionSessions.IgnoreQueryFilters()
            .Where(s => s.TenantId == tenantId
                        && s.AnonymousSessionId == anonymousSessionId
                        && s.IsQualified)
            .OrderByDescending(s => s.LastSeenAt)
            .FirstOrDefaultAsync(ct);

        if (existingQualified is not null && IsValid(existingQualified) && existingQualified.QrId != qr.Id)
        {
            existingQualified.Refresh();
            await _context.SaveChangesAsync(ct);
            _logger.LogInformation("QR resolve — attribution giữ nguyên (first-qualified-wins): session={SessionId} qr={QrId}",
                existingQualified.Id, qr.Id);
            return new QrResolveResult(qr.Id, tenantId, existingQualified.SalesmanId, existingQualified.CampaignId,
                existingQualified.Id, IsQualified: true, IsNewSession: false);
        }

        // Get-or-create session cho (tenant, qr, anonymous session) — §26.3 rapid-scan guard (unique index).
        AttributionSession? session = await _context.AttributionSessions.IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.QrId == qr.Id && s.AnonymousSessionId == anonymousSessionId, ct);

        bool isNewSession;
        if (session is null)
        {
            session = new AttributionSession(tenantId, qr.Id, anonymousSessionId,
                qr.SalesmanId, qr.CampaignId, AttributionExpiry());
            session.MarkQualified();   // first qualified attribution wins
            isNewSession = true;
            try
            {
                _context.AttributionSessions.Add(session);
                _ = await _context.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                // Race: 2 request resolve cùng lúc → load lại session hiện có, refresh.
                _ = await _context.SaveChangesAsync(ct); // clear tracker
                session = await _context.AttributionSessions.IgnoreQueryFilters()
                    .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.QrId == qr.Id && s.AnonymousSessionId == anonymousSessionId, ct)
                    ?? throw new ValidationException("Không thể tạo attribution session.");
                isNewSession = false;
                session.Refresh();
                _ = await _context.SaveChangesAsync(ct);
            }
        }
        else
        {
            // Refresh không tạo session mới, không đổi salesman (§7.5). Hết hạn → reset attribution period.
            if (!IsValid(session))
            {
                session.ResetPeriod(qr.SalesmanId, qr.CampaignId, AttributionExpiry());
                isNewSession = true;
            }
            else
            {
                session.Refresh();
                isNewSession = false;
            }
            _ = await _context.SaveChangesAsync(ct);
        }

        _logger.LogInformation("QR resolve: qr={QrId} tenant={TenantId} salesman={SalesmanId} session={SessionId} new={IsNew}",
            qr.Id, tenantId.Value, qr.SalesmanId, session.Id, isNewSession);

        return new QrResolveResult(qr.Id, tenantId, session.SalesmanId, session.CampaignId,
            session.Id, session.IsQualified, isNewSession);
    }

    public async Task<QRChannel> CreateQrChannelAsync(
        TenantId tenantId, string qrToken, Guid? salesmanId = null, Guid? campaignId = null,
        DateTime? attributionExpiryAt = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(qrToken))
            throw new ValidationException("qr_token là bắt buộc.");

        // §7.3 — salesman ∈ tenant: validate ngay khi tạo (active CommunityRole Salesman).
        if (salesmanId is not null)
        {
            bool salesmanValid = await _context.CommunityRoles.IgnoreQueryFilters()
                .AnyAsync(r => r.TenantId == tenantId
                               && r.CustomerId == salesmanId.Value
                               && r.RoleType == CommunityRoleType.Salesman
                               && r.IsActive, ct);
            if (!salesmanValid)
                throw new ValidationException("Salesman không thuộc tenant hoặc chưa kích hoạt vai trò cộng tác viên.");
        }

        // Q1 (2026-10-08 — issue #188 bug 2): lưu thêm token mã hóa (DataProtection key ring server)
        // để tenant xem lại link/QR sau khi tạo. Hash vẫn giữ cho resolve (§7.2).
        string? encryptedToken = DataProtectionProviderAccessor.CreateProtector(QrTokenProtectorPurpose).Protect(qrToken);
        var qr = new QRChannel(tenantId, HashToken(qrToken), salesmanId, campaignId, encryptedToken);
        _context.QRChannels.Add(qr);
        try
        {
            _ = await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            throw new ValidationException("qr_token đã tồn tại.");
        }

        _logger.LogInformation("QR channel created: qr={QrId} tenant={TenantId} salesman={SalesmanId}", qr.Id, tenantId.Value, salesmanId);
        return qr;
    }

    public async Task<QrChannelDetailResult> GetQrChannelDetailAsync(TenantId tenantId, Guid qrId, string bookingBaseUrl, CancellationToken ct = default)
    {
        QRChannel? qr = await _context.QRChannels.IgnoreQueryFilters()
            .FirstOrDefaultAsync(q => q.TenantId == tenantId && q.Id == qrId, ct)
            ?? throw new NotFoundException("QR không tồn tại trong tenant này.");

        // Revoked hoặc legacy (tạo trước Q1 — không có EncryptedToken) → không render lại được.
        if (!qr.IsActive || string.IsNullOrWhiteSpace(qr.EncryptedToken))
            return new QrChannelDetailResult(qr.Id, null, null, qr.IsActive, qr.RevokedAt);

        string token;
        try
        {
            token = DataProtectionProviderAccessor.CreateProtector(QrTokenProtectorPurpose).Unprotect(qr.EncryptedToken);
        }
        catch (CryptographicException)
        {
            _logger.LogWarning("QR token decrypt failed (key ring đổi?): qr={QrId}", qr.Id);
            return new QrChannelDetailResult(qr.Id, null, null, qr.IsActive, qr.RevokedAt);
        }

        string link = $"{bookingBaseUrl.TrimEnd('/')}/booking/{token}";
        return new QrChannelDetailResult(qr.Id, link, GenerateQrPngBase64(link), qr.IsActive, qr.RevokedAt);
    }

    public async Task RevokeQrAsync(TenantId tenantId, Guid qrId, CancellationToken ct = default)
    {
        QRChannel? qr = await _context.QRChannels.IgnoreQueryFilters()
            .FirstOrDefaultAsync(q => q.TenantId == tenantId && q.Id == qrId, ct)
            ?? throw new NotFoundException("QR không tồn tại trong tenant này.");
        qr.Revoke();
        _ = await _context.SaveChangesAsync(ct);
        _logger.LogInformation("QR channel revoked: qr={QrId} tenant={TenantId}", qrId, tenantId.Value);
    }

    public async Task<AttributionSession?> GetAttributionAsync(TenantId tenantId, Guid attributionId, CancellationToken ct = default)
        => await _context.AttributionSessions.IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Id == attributionId, ct);

    // ── Helpers ─────────────────────────────────────────────────────────

    private static DateTime? AttributionExpiry()
        => DateTime.UtcNow.AddDays(DefaultAttributionExpiryDays);

    /// <summary>Attribution valid khi chưa hết hạn (expiry null = vĩnh viễn).</summary>
    private static bool IsValid(AttributionSession session)
        => session.AttributionExpiryAt is null || session.AttributionExpiryAt > DateTime.UtcNow;

    /// <summary>Purpose cho DataProtection — token QR (Q1 2026-10-08).</summary>
    private const string QrTokenProtectorPurpose = "Booking.QrToken";

    /// <summary>SHA-256 hash — token opaque, không lưu plaintext (§7.2).</summary>
    public static string HashToken(string token)
        => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));

    /// <summary>Render QR PNG (base64 data URI) từ content — QRCoder ECC Q (chuẩn repo, precedent QrCodeService).</summary>
    private static string GenerateQrPngBase64(string content)
    {
        using QRCodeGenerator generator = new();
        QRCodeData data = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
        using PngByteQRCode qr = new(data);
        byte[] png = qr.GetGraphic(20);
        return $"data:image/png;base64,{Convert.ToBase64String(png)}";
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
    {
        for (Exception? e = ex; e is not null; e = e.InnerException)
        {
            string msg = e.Message;
            if (msg.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase)
                || msg.Contains("duplicate key", StringComparison.OrdinalIgnoreCase)
                || msg.Contains("23505", StringComparison.Ordinal)
                || msg.Contains("SQLite Error 19", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
