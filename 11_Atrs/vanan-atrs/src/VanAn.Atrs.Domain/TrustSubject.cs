namespace VanAn.Atrs.Domain;

/// <summary>
/// TrustSubject — canonical identity của ATRS (ADR-001, decision D1).
/// KHÔNG dùng SĐT/email/DeviceId làm PK; <see cref="PrimaryContactHash"/> chỉ là
/// lookup index (HMAC-SHA256, key từ secret manager), có thể null khi chưa có
/// contact đã verify. Profile bảo mật là XUYÊN tenant (một người, nhiều cửa hàng).
/// </summary>
public sealed class TrustSubject
{
    public Guid Id { get; private set; }
    public string? PrimaryContactHash { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private TrustSubject() { }

    public TrustSubject(Guid id, string? primaryContactHash)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Id cannot be empty.", nameof(id));
        Id = id;
        PrimaryContactHash = primaryContactHash;
        CreatedAt = DateTime.UtcNow;
    }

    /// <summary>Set/đổi contact hash khi có contact verified mới (nâng cấp lookup).</summary>
    public void SetPrimaryContactHash(string hash)
    {
        if (string.IsNullOrWhiteSpace(hash))
            throw new ArgumentException("Contact hash cannot be empty.", nameof(hash));
        PrimaryContactHash = hash;
        UpdatedAt = DateTime.UtcNow;
    }
}