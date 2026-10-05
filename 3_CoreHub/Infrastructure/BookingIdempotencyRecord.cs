namespace VanAn.CoreHub.Infrastructure;

/// <summary>
/// Durable idempotency record cho create booking (SRS §21.1) — DB-backed, survives process restart.
/// Infra concern only (pattern: ProcessedWebhookKey). KHÔNG phải domain entity.
/// Inserted atomically với Booking trong cùng transaction → retry cùng Idempotency-Key
/// không tạo booking trùng (§21.1) và trả về booking gốc.
/// Lưu ý: namespace KHÔNG chứa "Booking" (xung đột type Booking — lesson P1).
/// </summary>
public class BookingIdempotencyRecord
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string IdempotencyKey { get; private set; } = string.Empty;
    public Guid TenantId { get; private set; }
    public Guid BookingId { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private BookingIdempotencyRecord() { }

    public BookingIdempotencyRecord(string idempotencyKey, Guid tenantId, Guid bookingId)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new ArgumentException("IdempotencyKey is required.", nameof(idempotencyKey));
        IdempotencyKey = idempotencyKey;
        TenantId = tenantId;
        BookingId = bookingId;
        CreatedAt = DateTime.UtcNow;
    }
}
