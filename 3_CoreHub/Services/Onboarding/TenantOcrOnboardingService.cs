using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.Onboarding;

/// <summary>
/// OCR Tenant Onboarding (S2 — 2026-10-25): orchestrates scan-to-tenant flow.
/// Wizard ShopERP gửi field đã OCR + human-review → service này tạo tenant theo
/// flow Pending → Verify (D3/D5 — uniform với Crawl-to-Onboard pipeline).
///
/// Case A (CompanyWithLicense — có GPKD): bắt buộc MST (10/13 số). Dedup MST
/// CHẶN CỨNG — tenant trùng MST là vi phạm pháp lý/kế toán (sysadmin xử lý
/// tenant cũ trước khi tạo mới).
/// Case B (SmallShopNoLicense — không GPKD): bắt buộc HKDGroup (sổ sách TT152)
/// + tên hiệu. Dedup tên+địa chỉ CHỈ GỢI Ý (ExistingDuplicateTenantId), không chặn.
///
/// Product import (menu → ShopERP SQLite) không nằm ở đây — chạy phía ShopERP
/// sau khi NATS sync tenant (Option C: Products sống ở ShopERP SQLite).
/// </summary>
public class TenantOcrOnboardingService(
    ITenantOnboardingService onboardingService,
    IVanAnDbContext dbContext,
    ILogger<TenantOcrOnboardingService> logger) : ITenantOcrOnboardingService
{
    public async Task<OcrOnboardingResult> ExecuteAsync(
        OcrOnboardingRequest request,
        Guid approvedByUserId,
        CancellationToken ct = default)
    {
        // ── 1. Validate theo case ─────────────────────────────────────────
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Name);
        if (request.Latitude is null != request.Longitude is null)
            throw new ArgumentException("Latitude và Longitude phải được cung cấp cùng nhau.", nameof(request.Latitude));

        var warnings = new List<string>();
        string? mst = null;

        if (request.Case == OcrOnboardingCase.CompanyWithLicense)
        {
            mst = request.GpdkInfo?.Mst?.Trim();
            if (!IsValidTaxCode(mst))
                throw new ArgumentException("Mã số thuế bắt buộc và phải là 10 hoặc 13 chữ số (Case A — có GPKD).");
        }
        else // SmallShopNoLicense
        {
            if (request.HkdGroup is null)
                throw new ArgumentException("HKDGroup bắt buộc với shop không GPKD (Case B) — quyết định sổ sách TT152.");
            if (request.BusinessType != BusinessType.HouseholdBusiness)
                throw new ArgumentException("BusinessType phải là HouseholdBusiness với shop không GPKD (Case B).");
            if (string.IsNullOrWhiteSpace(request.SignName) && string.IsNullOrWhiteSpace(request.Name))
                throw new ArgumentException("Tên hiệu bắt buộc với shop không GPKD (Case B).");
        }

        // ── 2. Dedup ──────────────────────────────────────────────────────
        Guid? duplicateId = null;
        if (request.Case == OcrOnboardingCase.CompanyWithLicense)
        {
            // Case A: MST chặn cứng — không tạo tenant trùng MST
            var existingId = await dbContext.Tenants
                .IgnoreQueryFilters()
                .Where(t => t.Settings.TaxCode == mst)
                .Select(t => t.Id.Value)
                .FirstOrDefaultAsync(ct);
            if (existingId != Guid.Empty)
            {
                logger.LogWarning("OCR Onboarding chặn tạo tenant: MST {Mst} đã tồn tại ({ExistingId})", mst, existingId);
                return new OcrOnboardingResult(
                    TenantId: existingId,
                    OwnerUsername: string.Empty,
                    OwnerPassword: string.Empty,
                    Slug: string.Empty,
                    ExistingDuplicateTenantId: existingId,
                    Warnings: [$"Mã số thuế {mst} đã tồn tại — không tạo trùng. Xử lý tenant hiện có trước."]);
            }
        }
        else
        {
            // Case B: tên + địa chỉ chuẩn hóa → gợi ý, không chặn
            var normalizedName = request.Name.Trim().ToLowerInvariant();
            var address = request.Address?.Trim();
            var dupe = await dbContext.Tenants
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    t => t.Name.ToLower() == normalizedName
                        && (address == null || t.Settings.Address == address), ct);
            if (dupe is not null)
            {
                duplicateId = dupe.Id.Value;
                warnings.Add($"Tìm thấy tenant trùng tên/địa chỉ ({dupe.Name}) — kiểm tra trước khi bàn giao credentials.");
            }
        }

        // ── 3. Build listing → Pending tenant (Company | HKD) ─────────────
        var listing = new CrawlListingDto(
            Name: request.Name,
            TaxCode: mst,                                  // Case B: null → không dedup MST trong OnboardUnverifiedAsync
            Address: request.Address,
            CrawledPhone: null,                            // M3: SĐT consent đi qua VerifyTenantRequest.OwnerPhone
            ContactName: request.GpdkInfo?.Owner,
            IndustryCode: null,
            SourceSite: request.Case == OcrOnboardingCase.CompanyWithLicense ? "OCR-GPKD" : "OCR-SIGN",
            SourceUrl: "ocr://onboarding",               // CrawlSource.Create yêu cầu URL non-empty — sentinel cho nguồn OCR (provenance thật = ảnh trong PhotoUrls)
            CrawledAt: DateTime.UtcNow,
            Lat: request.Latitude,
            Lng: request.Longitude,
            PhotoUrls: request.PhotoUrls);                 // provenance ảnh (Cloudinary)

        var tenantId = await onboardingService.OnboardUnverifiedAsync(
            listing, ct,
            businessType: request.BusinessType,
            hkdGroup: request.HkdGroup);
        logger.LogInformation("OCR Onboarding tạo Pending tenant {TenantId} ({Case}) — name '{Name}'",
            tenantId, request.Case, request.Name);

        // ── 4. Verify → Active + owner + groups + NATS sync ───────────────
        var username = $"owner{(mst ?? Guid.NewGuid().ToString("N")[..6])}".ToLowerInvariant();
        var password = GenerateOwnerPassword();
        var displayName = request.Name.Length > 80 ? request.Name[..80] : request.Name;

        var verifyResult = await onboardingService.VerifyAsync(tenantId, new VerifyTenantRequest(
            OwnerUsername: username,
            OwnerPassword: password,
            OwnerDisplayName: displayName,
            OwnerPhone: request.ContactPhone,              // M3: consent từ wizard (sysadmin hỏi chủ hộ)
            OwnerEmail: request.ContactEmail,
            ShopInstanceId: request.ShopInstanceId,
            Slug: null,                                    // auto từ tên (Slugify)
            ApprovedByUserId: approvedByUserId), ct);

        logger.LogInformation(
            "OCR Onboarding hoàn tất: tenant {TenantId} Active — slug {Slug} — owner {Username} ({Case})",
            tenantId, verifyResult.PublishedSlug, username, request.Case);

        return new OcrOnboardingResult(
            TenantId: tenantId,
            OwnerUsername: username,
            OwnerPassword: password,                       // CHỈ hiện 1 lần trên UI
            Slug: verifyResult.PublishedSlug,
            ExistingDuplicateTenantId: duplicateId,
            Warnings: warnings);
    }

    /// <summary>MST Việt Nam: 10 chữ số, hoặc 13 chữ số với hậu tố chi nhánh (có dấu gạch).</summary>
    private static bool IsValidTaxCode(string? taxCode)
    {
        if (string.IsNullOrWhiteSpace(taxCode)) return false;
        var digits = taxCode.Replace("-", "").Trim();
        return (digits.Length == 10 || digits.Length == 13) && digits.All(char.IsDigit);
    }

    private static string GenerateOwnerPassword() =>
        $"VanAn{Convert.ToBase64String(Guid.NewGuid().ToByteArray())[..12].Replace('+', 'x').Replace('/', 'y').Replace('=', 'z')}!2026";
}