using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.CoreHub.Infrastructure;

namespace VanAn.CoreHub.Infrastructure.Configurations;

/// <summary>
/// EF Core configuration cho BookingIdempotencyRecord — durable idempotency store
/// cho create booking (SRS §21.1). Unique IdempotencyKey = dedup chính.
/// </summary>
public class BookingIdempotencyRecordConfiguration : IEntityTypeConfiguration<BookingIdempotencyRecord>, IEntityConfiguration
{
    public void Configure(EntityTypeBuilder<BookingIdempotencyRecord> builder)
    {
        _ = builder.HasKey(e => e.Id);
        _ = builder.Property(e => e.IdempotencyKey).IsRequired().HasMaxLength(128);
        _ = builder.HasIndex(e => e.IdempotencyKey).IsUnique();
        _ = builder.HasIndex(e => new { e.TenantId, e.BookingId });
    }
}
