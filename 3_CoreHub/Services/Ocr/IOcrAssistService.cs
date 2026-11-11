namespace VanAn.CoreHub.Services.Ocr;

public interface IOcrAssistService
{
    /// <summary>
    /// Gọi provider cloud OCR theo config. Trả Provider="none" (kèm Warning) khi
    /// chưa cấu hình key / ảnh không tải được / mọi provider lỗi — controller map 503.
    /// </summary>
    Task<OcrAssistResponse> AssistAsync(OcrAssistRequest request, CancellationToken ct);
}