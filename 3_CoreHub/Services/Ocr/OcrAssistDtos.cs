namespace VanAn.CoreHub.Services.Ocr;

// ── OCR Cloud Assist DTOs (P6 — 2026-11-11) ─────────────────────────────────
// SRS: docs/requirements/van_an_self_serve_onboarding_srs_v1.md — FR-17.
// Cloud OCR fallback khi Tesseract client-side confidence thấp.
// Provider: gemini (default — structured JSON) | vision (raw lines → client heuristic).
// Bảo mật: ImageUrl chỉ được fetch host trong AllowedUrlHost (chống SSRF);
// không log nội dung ảnh / extraction PII.

/// <summary>Loại ảnh cần nhận diện.</summary>
public enum OcrAssistKind
{
    Gpdk = 0,
    Sign = 1,
    Menu = 2
}

/// <summary>Request cloud OCR — ảnh qua ImageUrl (presigned R2, host allowlist) hoặc ImageBase64 inline.</summary>
public record OcrAssistRequest(
    OcrAssistKind Kind,
    string? ImageUrl = null,
    string? ImageBase64 = null,
    string? ImageContentType = null);

/// <summary>Một dòng text thô (vision provider) — client giữ heuristic parse hiện có.</summary>
public record OcrAssistLine(string Text, double? Confidence = null);

/// <summary>Một dòng sản phẩm menu (gemini provider).</summary>
public record OcrAssistProductLine(string Name, decimal? Price, int LineNo = 0);

/// <summary>
/// Response thống nhất. Provider = "none" → chưa cấu hình / không lấy được ảnh
/// (controller trả 503 với Warning). Warning = message thân thiện khi provider lỗi.
/// </summary>
public record OcrAssistResponse(
    string Provider,
    OcrAssistKind Kind,
    string? Mst = null,
    string? Name = null,
    string? Address = null,
    string? LegalForm = null,
    string? Owner = null,
    string? SignName = null,
    string? Phone = null,
    List<OcrAssistProductLine>? ProductLines = null,
    List<OcrAssistLine>? Lines = null,
    string? Warning = null);

/// <summary>Config OcrAssist (section "OcrAssist") — key API qua env, KHÔNG commit secret.</summary>
public sealed class OcrAssistOptions
{
    public const string SectionName = "OcrAssist";
    public string Provider { get; set; } = "gemini";   // gemini | vision | none
    public string GeminiApiKey { get; set; } = "";
    public string GeminiModel { get; set; } = "gemini-3.5-flash"; // RV 2026-11-11: 2.0-flash/2.5-flash bị deprecate cho key mới — 3.5-flash nhanh (8s) + pin bản cụ thể
    public string VisionApiKey { get; set; } = "";
    public string AllowedUrlHost { get; set; } = "";   // host duy nhất được fetch ImageUrl
    public int MaxImageBytes { get; set; } = 8 * 1024 * 1024;
}