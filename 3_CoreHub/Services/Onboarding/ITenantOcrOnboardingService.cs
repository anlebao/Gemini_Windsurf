namespace VanAn.CoreHub.Services.Onboarding;

/// <summary>
/// OCR Tenant Onboarding (S2 — 2026-10-25): orchestrates scan-to-tenant flow.
/// Nhận field đã OCR + human-review từ wizard ShopERP → tạo tenant theo
/// flow Pending → Verify (D3/D5 — uniform với Crawl-to-Onboard pipeline).
///
/// Case A (có GPKD): bắt buộc MST; dedup MST chặn cứng (tenant trùng MST là
/// vi phạm pháp lý/kế toán — sysadmin phải xử lý tenant cũ trước).
/// Case B (không GPKD): bắt buộc HKDGroup + tên hiệu; trùng tên+địa chỉ
/// chỉ gợi ý (ExistingDuplicateTenantId), không chặn.
///
/// Product import (menu → ShopERP SQLite) KHÔNG nằm ở đây — chạy phía ShopERP
/// sau khi NATS sync tenant (Option C: Products sống ở ShopERP SQLite).
/// </summary>
public interface ITenantOcrOnboardingService
{
    /// <summary>
    /// Tạo tenant từ kết quả OCR onboarding:
    /// 1. Validate theo case (Case A: MST · Case B: HKDGroup + tên hiệu)
    /// 2. Dedup (Case A: MST — chặn · Case B: tên+địa chỉ — gợi ý)
    /// 3. OnboardUnverifiedAsync → Pending tenant (Company | HKD)
    /// 4. CrawlSource provenance (SourceSite="OCR-GPKD"|"OCR-SIGN", payload = ảnh + fields)
    /// 5. VerifyAsync → Active + owner user + 4 groups + NATS sync SQLite
    /// 6. Owner credentials auto-sinh, trả về 1 lần.
    /// </summary>
    /// <param name="request">Field đã review trên wizard (OCR prefill + sửa tay).</param>
    /// <param name="approvedByUserId">SysAdmin đang thực hiện onboarding (claims sub).</param>
    /// <param name="ct">Cancellation token.</param>
    Task<OcrOnboardingResult> ExecuteAsync(
        OcrOnboardingRequest request,
        Guid approvedByUserId,
        CancellationToken ct = default);
}