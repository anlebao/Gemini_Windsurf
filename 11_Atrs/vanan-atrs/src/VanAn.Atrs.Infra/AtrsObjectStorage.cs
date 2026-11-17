namespace VanAn.Atrs.Infra;

/// <summary>
/// D9 (Wave 4): object storage cho evidence attachments — Cloudflare R2 (S3-compatible).
/// Binary KHÔNG qua DB; chỉ StorageKey metadata trong evidence_attachments.
/// Upload/download qua presigned URL TTL ngắn (pattern R2StorageService Gateway —
/// presigned PUT + HttpClient byte[] để tránh R2 trailer-signing issue).
/// </summary>
public interface IAtrsObjectStorage
{
    bool IsConfigured { get; }

    /// <summary>Tạo presigned PUT URL (TTL 15').</summary>
    string GetPresignedPutUrl(string key, string contentType, int ttlMinutes = 15);

    /// <summary>Tạo presigned GET URL (TTL 15').</summary>
    string GetPresignedGetUrl(string key, int ttlMinutes = 15);

    /// <summary>Upload bytes qua presigned PUT (server-side). Throw khi fail.</summary>
    Task UploadAsync(string key, byte[] data, string contentType, CancellationToken ct = default);
}

/// <summary>R2 chưa cấu hình → attachments trả lỗi rõ ràng (không silent fail).</summary>
public sealed class UnconfiguredAtrsObjectStorage : IAtrsObjectStorage
{
    public bool IsConfigured => false;
    public string GetPresignedPutUrl(string key, string contentType, int ttlMinutes = 15)
        => throw new InvalidOperationException("R2 storage chưa được cấu hình (Atrs:R2:*).");
    public string GetPresignedGetUrl(string key, int ttlMinutes = 15)
        => throw new InvalidOperationException("R2 storage chưa được cấu hình (Atrs:R2:*).");
    public Task UploadAsync(string key, byte[] data, string contentType, CancellationToken ct = default)
        => Task.FromException(new InvalidOperationException("R2 storage chưa được cấu hình (Atrs:R2:*)."));
}
