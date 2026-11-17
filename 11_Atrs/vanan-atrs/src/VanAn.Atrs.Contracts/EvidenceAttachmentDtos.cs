namespace VanAn.Atrs.Contracts;

// ═══ D9 (Wave 4) — Evidence attachment DTOs (FR-012) ═══

/// <summary>
/// Upload attachment — base64 JSON body (size cap 5MB decoded, validate cả 2 phía).
/// Binary đi qua API → server upload R2 (presigned PUT nội bộ, tránh CORS/trailer issue).
/// </summary>
public sealed record UploadAttachmentRequest(
    string FileName,
    string ContentType,
    string DataBase64);

/// <summary>Attachment metadata + signed download URL (TTL ngắn — generate mỗi lần đọc).</summary>
public sealed record EvidenceAttachmentDto(
    Guid Id,
    string FileName,
    string ContentType,
    long SizeBytes,
    DateTime CreatedAt,
    string? DownloadUrl);

/// <summary>Kết quả upload.</summary>
public sealed record UploadAttachmentResultDto(EvidenceAttachmentDto Attachment);
