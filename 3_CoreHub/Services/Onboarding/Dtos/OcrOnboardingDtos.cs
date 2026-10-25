using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.Onboarding;

// ── OCR Tenant Onboarding DTOs (S2 — 2026-10-25) ────────────────────────────
// User quyết định 2026-10-25: tách 2 case —
//   Case A (CompanyWithLicense): công ty / HKD có GPKD → scan GPKD + biển hiệu + menu
//   Case B (SmallShopNoLicense): shop nhỏ KHÔNG GPKD → chỉ scan biển hiệu + menu
// OCR chạy client-side (browser sysadmin); endpoint nhận field đã prefill + sửa tay.

/// <summary>Phân loại tenant theo giấy tờ: có GPKD (Case A) / không GPKD (Case B).</summary>
public enum OcrOnboardingCase
{
    CompanyWithLicense = 0,   // Công ty / HKD có GPKD
    SmallShopNoLicense = 1    // Shop nhỏ không GPKD (chỉ biển hiệu)
}

/// <summary>Thông tin trích xuất từ ảnh GPKD (Case A). Null với Case B.</summary>
public record OcrGpdkInfo(
    string? Mst,          // Mã số thuế (10/13 số — bắt buộc Case A)
    string? Name,         // Tên doanh nghiệp
    string? Address,      // Địa chỉ
    string? LegalForm,    // Loại hình pháp lý (TNHH / Cổ phần / HKD / HTX / DNTN)
    string? Owner);       // Người đại diện / chủ hộ

/// <summary>Một dòng sản phẩm từ menu (OCR client-side, review/edit trước khi submit).</summary>
public record OcrProductLineDto(
    string Name,
    decimal? Price,       // null = không đọc được giá (cần nhập tay)
    int LineNo);

/// <summary>
/// Request tạo tenant qua OCR Onboarding (SystemAdmin — đứng tại cửa hàng).
/// Tất cả field đã qua bước human-in-the-loop review trên wizard.
/// </summary>
public record OcrOnboardingRequest(
    OcrOnboardingCase Case,
    string Name,                          // Case A: từ GPKD (fallback biển hiệu) · Case B: từ biển hiệu — editable
    string? Address,                      // Case A: prefill từ GPKD · Case B: nhập tay
    BusinessType BusinessType,            // Case A: map từ legalForm · Case B: luôn HouseholdBusiness
    HKDGroup? HkdGroup,                   // Case B: bắt buộc (TT152) · Case A: null (gợi ý qua UI)
    List<OcrProductLineDto> ProductLines, // Từ menu OCR — import vào ShopERP sau NATS sync
    double? Latitude,                     // GPS sysadmin tại cửa hàng (bản đồ xác nhận)
    double? Longitude,
    OcrGpdkInfo? GpdkInfo = null,         // null với Case B
    string? SignName = null,              // Tên hiệu từ biển hiệu (Case B nguồn chính)
    Guid? ShopInstanceId = null,          // Multi-VPS routing (có thể null = default instance)
    string? ContactPhone = null,          // SĐT chủ hộ (consented trên wizard — M3 pattern)
    string? ContactEmail = null,
    List<string>? PhotoUrls = null);      // Cloudinary URLs ảnh GPKD/biển hiệu/menu — provenance + audit

/// <summary>Kết quả: tenant đã Active (Pending → Verify) + owner credentials hiện 1 lần.</summary>
public record OcrOnboardingResult(
    Guid TenantId,
    string OwnerUsername,
    string OwnerPassword,                 // CHỈ hiện 1 lần (pattern ActivatedTenantCredential)
    string Slug,
    Guid? ExistingDuplicateTenantId = null, // Case A: MST đã tồn tại → chặn tạo, trả tenant cũ.
                                            // Case B: trùng tên+địa chỉ → vẫn tạo, gợi ý để sysadmin xử lý.
    List<string>? Warnings = null);