using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.TenantAggregate;

namespace VanAn.CoreHub.Services.Onboarding;

// ── Self-Serve Merchant Signup DTOs (2026-11-09) ────────────────────────────
// SRS: docs/requirements/van_an_self_serve_onboarding_srs_v1.md — FR-1..6, FR-22.
// Merchant tự onboard qua /onboard (KhachLink PWA) → Pending tenant +
// CrawlSource.RawJson chứa SelfServePayload (ảnh, menu, credentials protected).
// SysAdmin verify qua verify-signup — password merchant KHÔNG BAO GIỜ lộ.

/// <summary>3 case self-serve: có GPKD / shop nhỏ không GPKD / bán rong chưa đăng ký.</summary>
public enum SignupCase
{
    CompanyWithLicense = 0,      // Case A: công ty / HKD có GPKD (MST bắt buộc)
    SmallShopNoLicense = 1,      // Case B: shop/quán/tiệm không GPKD (HKDGroup bắt buộc)
    StreetVendorUnregistered = 2 // Case Rong: bán hàng rong chưa đăng ký (force HKD+Group1)
}

/// <summary>
/// Payload self-serve nhúng trong CrawlListingDto.SelfServe → serialize vào
/// CrawlSource.RawJson cùng listing. Chứa mọi dữ liệu wizard thu thập +
/// OwnerPasswordProtected (IDataProtection — KHÔNG BAO GIỜ plaintext).
/// </summary>
public record SelfServePayload(
    string SignupCase,                    // "CompanyWithLicense" | "SmallShopNoLicense" | "StreetVendorUnregistered"
    string Src,                           // entry channel: directory|audit|qr|zalo|other|direct
    List<OcrProductLineDto> ProductLines, // menu OCR — sysadmin import sau duyệt
    string ContactName,
    string ContactPhone,                  // đã normalize digits
    string? ContactEmail,
    string OwnerUsername,
    string OwnerPasswordProtected,        // IDataProtection payload — verify-signup Unprotect
    bool OtpVerified,                     // P4: phone OTP verified (P0 luôn false)
    bool Unregistered,                    // Case Rong: chưa đăng ký kinh doanh
    bool DuplicateWarning,                // Case B/Rong: tên trùng tenant khác (advisory)
    string? SignName,                     // tên hiệu từ biển hiệu (Case B/Rong)
    OcrGpdkInfo? GpdkInfo,                // thông tin GPKD đầy đủ (Case A — legalForm/owner…)
    string? ClientIpHash,                 // SHA256(ip + salt) — không lưu IP thô
    string? UserAgent,                    // truncate 256
    DateTime SubmittedAt,
    // P4 / ID-P3 (2026-11-10, user duyệt): provenance verify liên hệ — trailing optional
    string? OtpChannel = null,            // kênh đã verify: "email" | "sms" | "zns" ...
    string? OtpVerifiedContact = null,    // contact đã OTP-verify (email/SĐT)
    DateTime? OtpVerifiedAt = null);      // thời điểm verify

/// <summary>Request public submit self-serve signup (POST /api/v1/merchant-signups).</summary>
public record MerchantSignupRequest(
    SignupCase Case,
    string Name,
    string? Address,
    BusinessType BusinessType,
    HKDGroup? HkdGroup,
    OcrGpdkInfo? GpdkInfo,
    string? SignName,
    double? Latitude,
    double? Longitude,
    List<string>? PhotoUrls,
    List<OcrProductLineDto>? ProductLines,
    string ContactName,
    string ContactPhone,
    string? ContactEmail,
    string OwnerUsername,
    string OwnerPassword,
    string? TurnstileToken,
    string? HoneypotWebsite,
    string? OtpVerifiedPhone = null,      // P4: SĐT đã OTP-verify (RequireOtp=true mới check)
    string? Src = null,                   // entry channel (FR-19) — whitelist validate
    string? OtpSessionToken = null);      // P4: session token từ otp/verify — bắt buộc khi RequireOtp

/// <summary>Kết quả submit — generic, KHÔNG tiết lộ duplicate (G10 anti-enumeration).</summary>
public record MerchantSignupResult(string Message, Guid SignupId);

/// <summary>Chi tiết hồ sơ self-serve cho sysadmin modal — TUYỆT ĐỐI không chứa password/protected blob.</summary>
public record SelfServeDetailDto(
    string SignupCase,
    string Src,
    List<OcrProductLineDto> ProductLines,
    string ContactName,
    string ContactPhone,
    string? ContactEmail,
    string OwnerUsername,
    bool OtpVerified,
    bool Unregistered,
    bool DuplicateWarning,
    string? SignName,
    OcrGpdkInfo? GpdkInfo,
    List<string>? PhotoUrls,
    DateTime SubmittedAt,
    // P4 / ID-P3 (2026-11-10): provenance — trailing optional (sysadmin badge "Liên hệ đã xác minh qua kênh X")
    string? OtpChannel = null,
    string? OtpVerifiedContact = null,
    DateTime? OtpVerifiedAt = null);

/// <summary>GET signup-detail response — tenant info + self-serve payload (nếu có).</summary>
public record SignupDetailDto(
    Guid TenantId,
    string Name,
    TenantStatus Status,
    string? TaxCode,
    string? Address,
    BusinessType BusinessType,
    HKDGroup? HkdGroup,
    DateTime CreatedAt,
    bool HasSelfServePayload,
    SelfServeDetailDto? SelfServe,
    double? Latitude = null,    // P2 (2026-11-09): GPS từ listing — hiển thị bản đồ trong modal review
    double? Longitude = null);

/// <summary>SysAdmin verify-signup: override bất kỳ field nào (null = dùng credentials merchant).</summary>
public record VerifySignupRequest(
    Guid? ShopInstanceId = null,
    string? Slug = null,
    string? OwnerUsername = null,
    string? OwnerPassword = null,         // plaintext 1 lần — KHÔNG lưu, chỉ dùng tạo user
    string? OwnerDisplayName = null);

/// <summary>Kết quả verify-signup — UsedMerchantPassword=true nghĩa là dùng password merchant tự đặt.</summary>
public record VerifySignupResult(
    Guid TenantId,
    string Slug,
    string OwnerUsername,
    bool UsedMerchantPassword);

/// <summary>SysAdmin reject pending tenant (bắt buộc lý do — audit).</summary>
public record RejectPendingRequest(string Reason);

/// <summary>Status lookup public — anti-enumeration: sai cặp signupId+phone → "NotFound" generic.</summary>
public record SignupStatusResult(string Status, string? Slug, string? Message);
