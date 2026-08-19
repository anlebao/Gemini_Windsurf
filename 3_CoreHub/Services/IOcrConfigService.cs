namespace VanAn.CoreHub.Services;

/// <summary>
/// OCR Hub S2: OCR engine configuration service — SystemAdmin selects OCR engine per use case.
/// Keys: "Ocr:PlateEngine" → "Tesseract" | "PaddleOCR"
///       "Ocr:MenuEngine"  → "EasyOCR" | "Tesseract"
/// Default: Tesseract (backward compat — preserves existing guard-camera.js behavior).
/// Cached 60s. Admin UI: /admin/ocr-settings (SystemAdmin role).
/// Pattern: copied from FeatureFlagService (SystemSetting key-value + IMemoryCache).
/// </summary>
public interface IOcrConfigService
{
    /// <summary>Get OCR engine config. Returns defaults if no SystemSetting rows exist.</summary>
    Task<OcrEngineConfig> GetConfigAsync(CancellationToken ct = default);

    /// <summary>Update OCR engine config. Creates SystemSetting rows if not exist.</summary>
    Task UpdateConfigAsync(OcrEngineConfig config, Guid updatedBy, CancellationToken ct = default);
}

/// <summary>OCR engine configuration — per use case.</summary>
public record OcrEngineConfig
{
    /// <summary>Engine for license plate scanning (client-side). Default: Tesseract.</summary>
    public string PlateEngine { get; init; } = "Tesseract";

    /// <summary>Engine for menu input (server-side, future). Default: Tesseract.</summary>
    public string MenuEngine { get; init; } = "Tesseract";
}
