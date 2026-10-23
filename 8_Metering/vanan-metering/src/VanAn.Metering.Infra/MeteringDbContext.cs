using Microsoft.EntityFrameworkCore;
using VanAn.Metering.Domain;

namespace VanAn.Metering.Infra;

/// <summary>
/// Metering-owned store (SQLite GĐ-1/2 → PostgreSQL GĐ-3, provider swap only).
/// No FK to Vạn An tables — tenantId/shopInstanceId are opaque caller claims (SRS [G3]).
/// </summary>
public sealed class MeteringDbContext(DbContextOptions<MeteringDbContext> options) : DbContext(options)
{
    public DbSet<UsageEvent> UsageEvents => Set<UsageEvent>();
    public DbSet<CallerKey> CallerKeys => Set<CallerKey>();
    public DbSet<RateCardVersion> RateCardVersions => Set<RateCardVersion>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UsageEvent>(b =>
        {
            b.HasKey(e => e.Id);
            // Idempotency per caller — replay returns first result (SRS [G6]).
            b.HasIndex(e => new { e.CallerId, e.IdempotencyKey }).IsUnique();
            b.HasIndex(e => new { e.TenantId, e.OccurredAtUtc });
            b.HasIndex(e => new { e.ServiceCode, e.OccurredAtUtc });
            b.Property(e => e.ServiceCode).HasMaxLength(64);
            b.Property(e => e.Unit).HasMaxLength(32);
            b.Property(e => e.CallerId).HasMaxLength(64);
            b.Property(e => e.IdempotencyKey).HasMaxLength(128);
            b.Property(e => e.Quantity).HasPrecision(18, 4);
        });

        modelBuilder.Entity<CallerKey>(b =>
        {
            b.HasKey(e => e.Id);
            b.HasIndex(e => e.CallerId).IsUnique();
            b.Property(e => e.CallerId).HasMaxLength(64);
            b.Property(e => e.KeyHash).HasMaxLength(64);
        });

        modelBuilder.Entity<RateCardVersion>(b =>
        {
            b.HasKey(e => e.Id);
            // One version per (serviceCode, effectiveFrom) — append-only chain.
            b.HasIndex(e => new { e.ServiceCode, e.EffectiveFrom }).IsUnique();
            b.HasIndex(e => new { e.ServiceCode, e.IsActive });
            b.Property(e => e.ServiceCode).HasMaxLength(64);
            b.Property(e => e.MeterUnit).HasMaxLength(32);
            b.Property(e => e.CreditRate).HasPrecision(18, 4);
            b.Property(e => e.Label).HasMaxLength(256);
            b.Property(e => e.ChangedBy).HasMaxLength(64);
            b.Property(e => e.AuditNote).HasMaxLength(512);
        });

        // Append-only by convention — no endpoint mutates or deletes audit rows.
        modelBuilder.Entity<AuditLog>(b =>
        {
            b.HasKey(e => e.Id);
            b.HasIndex(e => new { e.EntityType, e.EntityId });
            b.Property(e => e.Actor).HasMaxLength(64);
            b.Property(e => e.Action).HasMaxLength(32);
            b.Property(e => e.EntityType).HasMaxLength(64);
        });
    }
}
