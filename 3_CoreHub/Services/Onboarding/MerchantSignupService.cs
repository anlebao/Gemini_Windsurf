using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.TenantAggregate;
using VanAn.Shared.Domain.Common;
using UserRole = VanAn.Shared.Domain.Aggregates.UserAggregate.UserRole;
using Tenant = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant;

namespace VanAn.CoreHub.Services.Onboarding;

/// <summary>
/// Self-Serve Merchant Signup (2026-11-09 — SRS FR-1..6): merchant tự onboard
/// qua /onboard (KhachLink PWA) → tạo Pending tenant + CrawlSource payload đầy đủ.
/// KHÔNG tạo owner user ở đây — sysadmin verify-signup mới kích hoạt (credentials
/// merchant được bảo vệ bằng IDataProtection, không bao giờ lộ cho sysadmin).
///
/// Anti-abuse: honeypot + Turnstile ở controller · rate-limit · master flag
/// SelfServeEnabled · phone normalize + ipHash (không lưu IP thô).
/// </summary>
public interface IMerchantSignupService
{
    Task<MerchantSignupResult> SubmitAsync(MerchantSignupRequest request, string? clientIp, string? userAgent, CancellationToken ct = default);
    Task<SignupDetailDto?> GetSignupDetailAsync(Guid tenantId, CancellationToken ct = default);
    Task<VerifySignupResult> VerifySignupAsync(Guid tenantId, VerifySignupRequest request, Guid sysAdminUserId, CancellationToken ct = default);
    Task RejectPendingAsync(Guid tenantId, string reason, Guid sysAdminUserId, CancellationToken ct = default);
    Task<SignupStatusResult> GetStatusAsync(Guid signupId, string phone, CancellationToken ct = default);
}

public class MerchantSignupService(
    ITenantOnboardingService onboardingService,
    IPlatformOnboardingFlagsService flagsService,
    IVanAnDbContext dbContext,
    IDataProtectionProvider dataProtection,
    IConfiguration configuration,
    ILogger<MerchantSignupService> logger,
    IMeteringClient? metering = null) : IMerchantSignupService
{
    private const string ProtectorPurpose = "VanAn.SelfServeSignup.v1";
    private static readonly Regex UsernameRegex = new("^[a-z0-9._-]{3,30}$", RegexOptions.Compiled);
    private static readonly HashSet<string> AllowedSrc = new(StringComparer.OrdinalIgnoreCase)
        { "directory", "audit", "qr", "zalo", "other", "direct" };

    // ── Submit (public) ────────────────────────────────────────────────────

    public async Task<MerchantSignupResult> SubmitAsync(
        MerchantSignupRequest request, string? clientIp, string? userAgent, CancellationToken ct = default)
    {
        // FR-22: master switch — sysadmin đóng kênh → chặn trước mọi xử lý
        if (!await flagsService.IsSelfServeEnabledAsync(ct))
            throw new InvalidOperationException("self-serve-closed");

        ArgumentNullException.ThrowIfNull(request);
        ValidateRequest(request);

        var src = NormalizeSrc(request.Src);

        // Case Rong: force HKD + Group1 + unregistered (ignore client gửi khác — FR-5)
        var (businessType, hkdGroup, _) = ResolveBusinessShape(request);

        // Case B/Rong: dedup advisory tên chuẩn hóa → DuplicateWarning trong payload
        var duplicateWarning = request.Case != SignupCase.CompanyWithLicense
            && await HasNameDuplicateAsync(request.Name, ct);

        var listing = BuildListing(request, src, businessType, hkdGroup, duplicateWarning, clientIp, userAgent);
        var tenantId = await onboardingService.OnboardUnverifiedAsync(listing, ct, businessType, hkdGroup);

        // Phí nền tảng (issue #197): feature.ocr fire-and-forget — tenant mới ví=0,
        // KHÔNG deny guard (giống TenantOcrOnboardingService).
        if (metering is not null)
        {
            try
            {
                _ = metering.RecordUsageAsync(new MeteringUsageEvent(
                    tenantId, "feature.ocr", 1, "onboarding", DateTime.UtcNow,
                    $"ocr-self-serve:{tenantId}",
                    TaskRef: tenantId.ToString(),
                    MetadataJson: $"{{\"case\":\"{request.Case}\",\"src\":\"{src}\"}}"), ct: CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Self-serve signup metering ingest failed for {TenantId} — skipped (fail-safe)", tenantId);
            }
        }

        logger.LogInformation(
            "Self-serve signup: tenant {TenantId} Pending ({Case}, src={Src}, dupWarn={Dup})",
            tenantId, request.Case, src, duplicateWarning);

        return new MerchantSignupResult(
            "Đăng ký thành công! Đội ngũ Vạn An sẽ liên hệ xác minh trong 1-2 ngày làm việc.",
            tenantId);
    }

    // ── SysAdmin: detail / verify / reject ─────────────────────────────────

    public async Task<SignupDetailDto?> GetSignupDetailAsync(Guid tenantId, CancellationToken ct = default)
    {
        var tenant = await LoadTenantAsync(tenantId, ct);
        if (tenant is null) return null;

        var (listing, _) = await LoadLatestListingAsync(tenantId, ct);
        var selfServe = listing?.SelfServe;

        return new SignupDetailDto(
            tenant.Id.Value, tenant.Name, tenant.Status,
            tenant.Settings?.TaxCode, tenant.Settings?.Address,
            tenant.BusinessType, tenant.HKDGroup, tenant.CreatedAt,
            HasSelfServePayload: selfServe is not null,
            SelfServe: selfServe is null ? null : new SelfServeDetailDto(
                selfServe.SignupCase, selfServe.Src, selfServe.ProductLines,
                selfServe.ContactName, selfServe.ContactPhone, selfServe.ContactEmail,
                selfServe.OwnerUsername, selfServe.OtpVerified, selfServe.Unregistered,
                selfServe.DuplicateWarning, selfServe.SignName, selfServe.GpdkInfo,
                listing!.PhotoUrls, selfServe.SubmittedAt),
            Latitude: listing?.Lat,
            Longitude: listing?.Lng);
    }

    public async Task<VerifySignupResult> VerifySignupAsync(
        Guid tenantId, VerifySignupRequest request, Guid sysAdminUserId, CancellationToken ct = default)
    {
        var tenant = await LoadTenantAsync(tenantId, ct)
            ?? throw new KeyNotFoundException($"Tenant {tenantId} not found.");

        if (tenant.Status != TenantStatus.Pending)
            throw new InvalidOperationException($"Chỉ tenant Pending mới verify được (hiện {tenant.Status}).");
        if (tenant.PotentialDuplicateOf is not null)
            throw new InvalidOperationException("Tenant bị chặn duplicate — resolve duplicate trước khi verify.");

        var (_, selfServe) = await LoadLatestListingAsync(tenantId, ct);
        if (selfServe is null)
            throw new ArgumentException("Tenant này không phải self-serve signup (không có payload).", nameof(tenantId));

        var usedMerchantPassword = request.OwnerPassword is null;
        var password = request.OwnerPassword
            ?? dataProtection.CreateProtector(ProtectorPurpose).Unprotect(selfServe.OwnerPasswordProtected);
        var username = string.IsNullOrWhiteSpace(request.OwnerUsername)
            ? selfServe.OwnerUsername
            : request.OwnerUsername.Trim().ToLowerInvariant();
        var displayName = string.IsNullOrWhiteSpace(request.OwnerDisplayName)
            ? (tenant.Name.Length > 80 ? tenant.Name[..80] : tenant.Name)
            : request.OwnerDisplayName.Trim();

        var verifyResult = await onboardingService.VerifyAsync(tenantId, new VerifyTenantRequest(
            OwnerUsername: username,
            OwnerPassword: password,
            OwnerDisplayName: displayName,
            OwnerPhone: selfServe.ContactPhone,
            OwnerEmail: selfServe.ContactEmail,
            ShopInstanceId: request.ShopInstanceId,
            Slug: request.Slug,
            ApprovedByUserId: sysAdminUserId,
            // ID-P1 (2026-11-10): OtpVerified → ContactVerified; channel fallback "otp" cho tới khi P4 có OtpChannel
            ContactVerified: selfServe.OtpVerified,
            VerifiedChannel: "otp"), ct);

        logger.LogInformation(
            "Self-serve verify-signup: tenant {TenantId} → Active, slug {Slug}, owner {Username}, usedMerchantPassword={Used} (sysadmin {SysAdmin})",
            tenantId, verifyResult.PublishedSlug, username, usedMerchantPassword, sysAdminUserId);

        return new VerifySignupResult(tenantId, verifyResult.PublishedSlug, username, usedMerchantPassword);
    }

    public async Task RejectPendingAsync(Guid tenantId, string reason, Guid sysAdminUserId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Lý do từ chối bắt buộc.", nameof(reason));

        var tenant = await LoadTenantAsync(tenantId, ct)
            ?? throw new KeyNotFoundException($"Tenant {tenantId} not found.");

        if (tenant.Status != TenantStatus.Pending)
            throw new InvalidOperationException($"Chỉ tenant Pending mới reject được (hiện {tenant.Status}).");

        tenant.Deactivate($"self-serve-rejected: {reason.Trim()}");

        // ID-P1 (REVIEW B3 — user duyệt 2026-11-10): tenant mất hiệu lực → owner BusinessVerified → ContactVerified
        await RevokeOwnerBusinessTrustAsync(tenantId, ct);

        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation(
            "Self-serve reject-pending: tenant {TenantId} ({Name}) — reason '{Reason}' by sysadmin {SysAdmin}",
            tenantId, tenant.Name, reason.Trim(), sysAdminUserId);
    }

    // ── ID-P1 (REVIEW B3, 2026-11-10): downgrade hook — tenant→inactive → owner trust ──
    // Owner user là người duy nhất có BusinessVerified (staff luôn None). Không xuống None —
    // liên hệ đã verify vẫn còn giá trị. No-op nếu tenant chưa có owner (reject trước verify).
    private async Task RevokeOwnerBusinessTrustAsync(Guid tenantId, CancellationToken ct)
    {
        var ownerUserId = await dbContext.UserTenants
            .IgnoreQueryFilters()
            .Where(ut => ut.TenantIdValue == tenantId && ut.Role == UserRole.Owner)
            .Select(ut => (Guid?)ut.UserId)
            .FirstOrDefaultAsync(ct);
        if (ownerUserId is null) return;

        var owner = await dbContext.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == ownerUserId.Value, ct);
        if (owner is null) return;

        owner.RevokeBusinessVerification();
        logger.LogInformation(
            "ID-P1: revoked BusinessVerified for owner {UserId} — tenant {TenantId} inactive",
            owner.Id, tenantId);
    }

    // ── Public status lookup (anti-enumeration) ────────────────────────────

    public async Task<SignupStatusResult> GetStatusAsync(Guid signupId, string phone, CancellationToken ct = default)
    {
        var NotFound = new SignupStatusResult("NotFound", null, "Không tìm thấy đăng ký phù hợp.");
        var tenant = await LoadTenantAsync(signupId, ct);
        if (tenant is null) return NotFound;

        var inputPhone = NormalizePhone(phone);
        if (string.IsNullOrEmpty(inputPhone)) return NotFound;

        // Phone phải khớp payload self-serve HOẶC ContactPhone (đã set sau verify)
        var (_, selfServe) = await LoadLatestListingAsync(signupId, ct);
        var payloadPhone = selfServe is null ? null : NormalizePhone(selfServe.ContactPhone);
        var contactPhone = NormalizePhone(tenant.Settings?.ContactPhone);
        if (!string.Equals(inputPhone, payloadPhone, StringComparison.Ordinal)
            && !string.Equals(inputPhone, contactPhone, StringComparison.Ordinal))
            return NotFound;

        return tenant.Status switch
        {
            TenantStatus.Pending => new SignupStatusResult("Pending", null,
                "Đăng ký đang chờ đội ngũ Vạn An xác minh — thường 1-2 ngày làm việc."),
            TenantStatus.Active => new SignupStatusResult("Active", tenant.Settings?.Slug,
                "Đăng ký đã được duyệt! Bạn có thể đăng nhập quản lý cửa hàng."),
            TenantStatus.Inactive when selfServe is not null => new SignupStatusResult("Rejected", null,
                "Đăng ký không được chấp nhận. Vui lòng liên hệ hỗ trợ để biết thêm chi tiết."),
            _ => new SignupStatusResult(tenant.Status.ToString(), null, null)
        };
    }

    // ── Validation ─────────────────────────────────────────────────────────

    private static void ValidateRequest(MerchantSignupRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Name);
        if (request.Name.Trim().Length > 200) throw new ArgumentException("Tên cửa hàng tối đa 200 ký tự.");
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ContactName);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ContactPhone);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.OwnerUsername);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.OwnerPassword);

        var username = request.OwnerUsername.Trim().ToLowerInvariant();
        if (!UsernameRegex.IsMatch(username))
            throw new ArgumentException("Tên đăng nhập chỉ gồm chữ thường, số, dấu chấm/gạch dưới/gạch ngang (3-30 ký tự).");
        if (request.OwnerPassword.Length < 8)
            throw new ArgumentException("Mật khẩu tối thiểu 8 ký tự.");
        if (NormalizePhone(request.ContactPhone).Length < 9)
            throw new ArgumentException("Số điện thoại không hợp lệ.");

        var (businessType, hkdGroup, _) = ResolveBusinessShape(request);
        if (businessType == BusinessType.HouseholdBusiness && hkdGroup is null)
            throw new ArgumentException("Vui lòng chọn nhóm thuế (sẽ được đội ngũ xác nhận khi duyệt).");

        if (request.Case == SignupCase.CompanyWithLicense)
        {
            var mst = request.GpdkInfo?.Mst?.Trim();
            if (!IsValidTaxCode(mst))
                throw new ArgumentException("Mã số thuế bắt buộc và phải là 10 hoặc 13 chữ số (có GPKD).");
        }
        else if (string.IsNullOrWhiteSpace(request.SignName) && string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("Tên cửa hàng/tên hiệu bắt buộc.");
        }
    }

    /// <summary>Case Rong force HKD+Group1+Unregistered; còn lại theo client (đã validate).</summary>
    private static (BusinessType, HKDGroup?, bool) ResolveBusinessShape(MerchantSignupRequest request)
    {
        if (request.Case == SignupCase.StreetVendorUnregistered)
            return (BusinessType.HouseholdBusiness, HKDGroup.Group1, true);

        var bt = request.BusinessType;
        var hg = bt == BusinessType.HouseholdBusiness ? request.HkdGroup : null;
        if (bt == BusinessType.Company && request.HkdGroup is not null)
            throw new ArgumentException("HKDGroup chỉ áp dụng cho hộ kinh doanh.");
        return (bt, hg, false);
    }

    // ── Builders / helpers ─────────────────────────────────────────────────

    private CrawlListingDto BuildListing(
        MerchantSignupRequest request,
        string src,
        BusinessType businessType,
        HKDGroup? hkdGroup,
        bool duplicateWarning,
        string? clientIp,
        string? userAgent)
    {
        var protector = dataProtection.CreateProtector(ProtectorPurpose);
        var selfServe = new SelfServePayload(
            SignupCase: request.Case.ToString(),
            Src: src,
            ProductLines: request.ProductLines ?? [],
            ContactName: request.ContactName.Trim(),
            ContactPhone: NormalizePhone(request.ContactPhone),
            ContactEmail: string.IsNullOrWhiteSpace(request.ContactEmail) ? null : request.ContactEmail.Trim(),
            OwnerUsername: request.OwnerUsername.Trim().ToLowerInvariant(),
            OwnerPasswordProtected: protector.Protect(request.OwnerPassword),
            OtpVerified: configuration.GetValue("MerchantSignup:RequireOtp", false)
                && string.Equals(NormalizePhone(request.OtpVerifiedPhone), NormalizePhone(request.ContactPhone), StringComparison.Ordinal),
            Unregistered: request.Case == SignupCase.StreetVendorUnregistered,
            DuplicateWarning: duplicateWarning,
            SignName: string.IsNullOrWhiteSpace(request.SignName) ? null : request.SignName.Trim(),
            GpdkInfo: request.GpdkInfo,
            ClientIpHash: HashIp(clientIp),
            UserAgent: userAgent is { Length: > 256 } ? userAgent[..256] : userAgent,
            SubmittedAt: DateTime.UtcNow);

        var sourceSite = request.Case switch
        {
            SignupCase.CompanyWithLicense => "OCR-SELF-GPKD",
            SignupCase.SmallShopNoLicense => "OCR-SELF-SIGN",
            _ => "OCR-SELF-RONG"
        };

        return new CrawlListingDto(
            Name: request.Name.Trim(),
            TaxCode: request.Case == SignupCase.CompanyWithLicense ? request.GpdkInfo?.Mst?.Trim() : null,
            Address: string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim(),
            CrawledPhone: null, // SĐT đi qua SelfServePayload.ContactPhone (consented)
            ContactName: request.GpdkInfo?.Owner ?? request.ContactName.Trim(),
            IndustryCode: null,
            SourceSite: sourceSite,
            SourceUrl: "ocr://self-serve",
            CrawledAt: DateTime.UtcNow,
            Lat: request.Latitude,
            Lng: request.Longitude,
            PhotoUrls: request.PhotoUrls,
            SelfServe: selfServe);
    }

    private async Task<bool> HasNameDuplicateAsync(string name, CancellationToken ct)
    {
        var normalized = name.Trim().ToLowerInvariant();
        return await dbContext.Tenants
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(t => t.Name.ToLower() == normalized, ct);
    }

    private async Task<Tenant?> LoadTenantAsync(Guid tenantId, CancellationToken ct)
        => await dbContext.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == new TenantId(tenantId), ct);

    private async Task<(CrawlListingDto? Listing, SelfServePayload? SelfServe)> LoadLatestListingAsync(
        Guid tenantId, CancellationToken ct)
    {
        var source = await dbContext.CrawlSources
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(c => c.TenantId == new TenantId(tenantId))
            .OrderByDescending(c => c.CrawledAt)
            .FirstOrDefaultAsync(ct);

        if (source is null) return (null, null);
        var listing = JsonSerializer.Deserialize<CrawlListingDto>(source.RawJson);
        return (listing, listing?.SelfServe);
    }

    /// <summary>MST Việt Nam: 10 chữ số, hoặc 13 chữ số với hậu tố chi nhánh (có dấu gạch).</summary>
    internal static bool IsValidTaxCode(string? taxCode)
    {
        if (string.IsNullOrWhiteSpace(taxCode)) return false;
        var digits = taxCode.Replace("-", "").Trim();
        return (digits.Length == 10 || digits.Length == 13) && digits.All(char.IsDigit);
    }

    /// <summary>SĐT: giữ digits; +84/84 → 0 (chuẩn hiển thị VN); còn lại giữ nguyên digits.</summary>
    internal static string NormalizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return string.Empty;
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.StartsWith("84", StringComparison.Ordinal) && digits.Length == 11)
            digits = "0" + digits[2..];
        return digits;
    }

    private string? HashIp(string? ip)
    {
        if (string.IsNullOrWhiteSpace(ip)) return null;
        var salt = configuration["MerchantSignup:IpHashSalt"] ?? "vanan-self-serve";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(salt + "|" + ip));
        return Convert.ToHexString(bytes)[..16];
    }

    private static string NormalizeSrc(string? src)
        => src is not null && AllowedSrc.Contains(src) ? src.ToLowerInvariant() : "direct";
}
