using VanAn.Shared.Services;

namespace VanAn.ShopERP.Services;

/// <summary>
/// W3b mode A — sau khi sysadmin import sản phẩm từ menu OCR trong onboarding,
/// tự đẩy menu thành TenantAI knowledge document (type "menu-note", pending-review —
/// approval gate giữ nguyên, KHÔNG auto-publish).
///
/// Gate bằng ops flag per-tenant: SystemSetting "ShopFeature:{tenantId}:OcrMenuKnowledgeAuto"
/// (mặc định TẮT = mode B — owner tự upload ảnh menu ở Tenant AI → Knowledge).
/// Sysadmin bật flag trên /settings/shop-features của tenant tương ứng.
/// SourceRef cố định "ocr-menu:{tenantId}" → vanan-ai dedupe: bấm import lại
/// không tạo document trùng (trừ khi doc cũ đã bị reject/xóa).
/// Fail-safe: mọi lỗi → trả message, không throw — import sản phẩm không bị ảnh hưởng.
/// </summary>
public interface IOcrMenuKnowledgeService
{
    /// <summary>Trả message để hiển thị trên UI; null = flag tắt hoặc không có dòng menu.</summary>
    Task<string?> IngestMenuIfEnabledAsync(Guid tenantId, IReadOnlyList<MenuOcrLine> lines, CancellationToken ct = default);
}

public sealed class OcrMenuKnowledgeService(
    IShopFeatureSettingsService settings,
    ITenantAiClient aiClient,
    ILogger<OcrMenuKnowledgeService> logger) : IOcrMenuKnowledgeService
{
    public const string FlagName = "OcrMenuKnowledgeAuto";

    public async Task<string?> IngestMenuIfEnabledAsync(Guid tenantId, IReadOnlyList<MenuOcrLine> lines, CancellationToken ct = default)
    {
        var items = lines.Where(l => !string.IsNullOrWhiteSpace(l.Name)).ToList();
        if (items.Count == 0) return null;

        bool enabled;
        try
        {
            enabled = await settings.IsEnabledAsync(tenantId, FlagName, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "OcrMenuKnowledge flag check failed for {TenantId} — skip", tenantId);
            return null;
        }
        if (!enabled) return null;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Menu / sản phẩm của quán (tự động import từ onboarding OCR):");
        foreach (var l in items)
            sb.AppendLine(l.Price.HasValue ? $"- {l.Name.Trim()} — {l.Price.Value:N0}đ" : $"- {l.Name.Trim()}");

        try
        {
            var ok = await aiClient.IngestDocumentAsync(tenantId, "menu-note",
                $"Menu OCR onboarding — {DateTime.UtcNow:dd/MM/yyyy}", sb.ToString(), "public",
                sourceRef: $"ocr-menu:{tenantId}");
            return ok
                ? $"AI knowledge: đã tạo tài liệu menu ({items.Count} món) trong hàng chờ duyệt — Tenant AI → Knowledge."
                : "AI knowledge: gửi menu thất bại — owner có thể tự upload ảnh menu ở Tenant AI → Knowledge.";
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "OcrMenuKnowledge ingest failed for {TenantId} — non-fatal", tenantId);
            return "AI knowledge: gửi menu thất bại — owner có thể tự upload ảnh menu ở Tenant AI → Knowledge.";
        }
    }
}
