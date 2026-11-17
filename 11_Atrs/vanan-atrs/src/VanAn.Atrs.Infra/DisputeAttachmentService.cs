using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using VanAn.Atrs.Contracts;
using VanAn.Atrs.Domain;

namespace VanAn.Atrs.Infra;

/// <summary>
/// D9 (Wave 4) — DisputeAttachmentService: upload/list evidence attachments (R2).
/// Ownership: chỉ owner của DisputeCase upload/list; case terminal → không thêm mới
/// (list vẫn xem được). Validate content-type allowlist + size cap 2 phía.
/// Signed GET URL TTL 15' — không public bucket.
/// </summary>
public interface IDisputeAttachmentService
{
    Task<UploadAttachmentResultDto?> UploadAsync(Guid caseId, Guid trustSubjectId, UploadAttachmentRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<EvidenceAttachmentDto>?> ListAsync(Guid caseId, Guid trustSubjectId, CancellationToken ct = default);
}

public sealed class DisputeAttachmentService(
    AtrsDbContext db,
    IAtrsObjectStorage storage) : IDisputeAttachmentService
{
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png", "image/jpeg", "image/webp", "application/pdf"
    };

    private static readonly Dictionary<string, string> ExtensionByType = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/png"] = "png",
        ["image/jpeg"] = "jpg",
        ["image/webp"] = "webp",
        ["application/pdf"] = "pdf"
    };

    public async Task<UploadAttachmentResultDto?> UploadAsync(Guid caseId, Guid trustSubjectId, UploadAttachmentRequest request, CancellationToken ct = default)
    {
        if (!storage.IsConfigured)
            throw new InvalidOperationException("Attachment storage chưa được cấu hình.");

        var contentType = (request.ContentType ?? string.Empty).Trim();
        if (!AllowedContentTypes.Contains(contentType))
            throw new ArgumentException($"Unsupported ContentType: {contentType}");

        byte[] data;
        try
        {
            data = Convert.FromBase64String(request.DataBase64 ?? string.Empty);
        }
        catch (FormatException)
        {
            throw new ArgumentException("DataBase64 không hợp lệ.");
        }
        if (data.Length == 0 || data.Length > EvidenceAttachment.MaxSizeBytes)
            throw new ArgumentException($"File size phải trong khoảng 1B..{EvidenceAttachment.MaxSizeBytes / (1024 * 1024)}MB.");

        var fileName = Path.GetFileName(request.FileName ?? string.Empty);
        if (string.IsNullOrWhiteSpace(fileName) || fileName.Length > 200)
            throw new ArgumentException("FileName không hợp lệ.");

        var entity = await db.DisputeCases.FirstOrDefaultAsync(c => c.Id == caseId, ct);
        if (entity is null || entity.TrustSubjectId != trustSubjectId)
            return null; // anti-enumeration
        if (entity.IsTerminal)
            throw new InvalidOperationException("Case đã đóng — không thể thêm đính kèm.");

        // Cap 10 attachments/case (chống abuse storage).
        var count = await db.EvidenceAttachments.CountAsync(a => a.DisputeCaseId == caseId, ct);
        if (count >= 10)
            throw new InvalidOperationException("Case đã đạt giới hạn 10 đính kèm.");

        var ext = ExtensionByType[contentType];
        var key = $"atrs-evidence/{caseId:N}/{Guid.NewGuid():N}.{ext}";
        await storage.UploadAsync(key, data, contentType, ct);

        var attachment = new EvidenceAttachment(
            Guid.NewGuid(), caseId, trustSubjectId, key, fileName, contentType, data.Length);
        db.EvidenceAttachments.Add(attachment);

        // Evidence item metadata (USER_VISIBLE) trỏ tới attachment — audit trail trên case.
        db.DisputeEvidenceItems.Add(new DisputeEvidenceItem(
            Guid.NewGuid(), caseId, DisputeEvidenceType.OTHER,
            sourceSystem: "khachlink-attachment",
            sourceReference: key,
            capturedAt: null,
            integrityHash: Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(data)),
            submittedBy: $"user:{trustSubjectId:N}",
            reliabilityClass: EvidenceReliabilityClass.USER_CLAIM,
            description: $"Attachment: {fileName}",
            accessClassification: EvidenceAccessClass.USER_VISIBLE));

        db.AccountSecurityEvents.Add(AccountSecurityEvent.Create(
            Guid.NewGuid(), trustSubjectId, "dispute.attachment_added", SecurityEventActorType.User, null,
            sourceApplication: "vanan-atrs", deviceId: null, sessionCorrelationId: null,
            SecurityEventOutcome.Success, null,
            minimalMetadataJson: $"{{\"caseId\":\"{caseId:N}\",\"size\":{data.Length}}}"));
        await db.SaveChangesAsync(ct);

        return new UploadAttachmentResultDto(ToDto(attachment, signedUrl: true));
    }

    public async Task<IReadOnlyList<EvidenceAttachmentDto>?> ListAsync(Guid caseId, Guid trustSubjectId, CancellationToken ct = default)
    {
        var entity = await db.DisputeCases.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == caseId && c.TrustSubjectId == trustSubjectId, ct);
        if (entity is null)
            return null;

        var items = await db.EvidenceAttachments.AsNoTracking()
            .Where(a => a.DisputeCaseId == caseId)
            .OrderBy(a => a.CreatedAt)
            .ToListAsync(ct);
        return items.Select(a => ToDto(a, signedUrl: true)).ToList();
    }

    private EvidenceAttachmentDto ToDto(EvidenceAttachment a, bool signedUrl)
    {
        string? url = null;
        if (signedUrl && storage.IsConfigured)
        {
            try { url = storage.GetPresignedGetUrl(a.StorageKey, ttlMinutes: 15); }
            catch { url = null; }
        }
        return new EvidenceAttachmentDto(a.Id, a.FileName, a.ContentType, a.SizeBytes, a.CreatedAt, url);
    }
}
