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
    public DbSet<CreditWallet> CreditWallets => Set<CreditWallet>();
    public DbSet<CreditLedgerEntry> CreditLedgerEntries => Set<CreditLedgerEntry>();
    public DbSet<WalletAlert> WalletAlerts => Set<WalletAlert>();
    public DbSet<TopupConfig> TopupConfigs => Set<TopupConfig>();
    public DbSet<TopupRequest> TopupRequests => Set<TopupRequest>();
    public DbSet<EnforcementConfig> EnforcementConfigs => Set<EnforcementConfig>();

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
            b.Property(e => e.CreditsApplied).HasPrecision(18, 4);
            b.Property(e => e.UnpaidCredits).HasPrecision(18, 4);
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
            // One version per (serviceCode, scope, effectiveFrom) — append-only chain;
            // issue #197: the same serviceCode may carry one chain per scope.
            b.HasIndex(e => new { e.ServiceCode, e.Scope, e.EffectiveFrom }).IsUnique();
            b.HasIndex(e => new { e.ServiceCode, e.IsActive });
            b.Property(e => e.ServiceCode).HasMaxLength(64);
            b.Property(e => e.MeterUnit).HasMaxLength(32);
            b.Property(e => e.CreditRate).HasPrecision(18, 4);
            b.Property(e => e.Label).HasMaxLength(256);
            b.Property(e => e.ChangedBy).HasMaxLength(64);
            b.Property(e => e.AuditNote).HasMaxLength(512);
            b.Property(e => e.ScopeValue).HasMaxLength(64);
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

        // P7 — credit wallet: one wallet per tenant; atomic deduct via conditional UPDATE.
        modelBuilder.Entity<CreditWallet>(b =>
        {
            b.HasKey(e => e.Id);
            b.HasIndex(e => e.TenantId).IsUnique();
            b.Property(e => e.BalancePurchased).HasPrecision(18, 4);
            b.Property(e => e.BalancePool).HasPrecision(18, 4);
            b.Property(e => e.BalanceEarned).HasPrecision(18, 4);
            b.Property(e => e.GrantedTotal).HasPrecision(18, 4);
            b.Property(e => e.CycleKey).HasMaxLength(64);
        });

        // P7 — append-only ledger; corrections are reversal entries (Type=Adjust).
        modelBuilder.Entity<CreditLedgerEntry>(b =>
        {
            b.HasKey(e => e.Id);
            b.HasIndex(e => new { e.TenantId, e.CreatedAtUtc });
            b.HasIndex(e => e.UsageEventId);
            // One Grant per topup — the DB arbiter against concurrent confirms (T25).
            b.HasIndex(e => e.TopupRequestId).IsUnique().HasFilter("\"TopupRequestId\" IS NOT NULL");
            b.Property(e => e.Credits).HasPrecision(18, 4);
            b.Property(e => e.BalanceAfter).HasPrecision(18, 4);
            b.Property(e => e.Reason).HasMaxLength(256);
            b.Property(e => e.CreatedBy).HasMaxLength(64);
        });

        // P7 D4 — one alert per (tenant, level, alert-cycle); cycle resets on Grant.
        modelBuilder.Entity<WalletAlert>(b =>
        {
            b.HasKey(e => e.Id);
            b.HasIndex(e => new { e.TenantId, e.Level, e.CycleKey }).IsUnique();
            b.Property(e => e.CycleKey).HasMaxLength(64);
            b.Property(e => e.NotifiedChannels).HasMaxLength(64);
        });

        modelBuilder.Entity<TopupConfig>(b =>
        {
            b.HasKey(e => e.Id);
            b.Property(e => e.BankCode).HasMaxLength(32);
            b.Property(e => e.BankName).HasMaxLength(128);
            b.Property(e => e.AccountNumber).HasMaxLength(64);
            b.Property(e => e.AccountName).HasMaxLength(128);
            b.Property(e => e.UpdatedBy).HasMaxLength(64);
        });

        modelBuilder.Entity<TopupRequest>(b =>
        {
            b.HasKey(e => e.Id);
            b.HasIndex(e => e.ReferenceCode).IsUnique();
            b.HasIndex(e => new { e.CallerId, e.IdempotencyKey }).IsUnique();
            b.HasIndex(e => new { e.TenantId, e.Status });
            b.Property(e => e.ReferenceCode).HasMaxLength(32);
            b.Property(e => e.AmountVnd).HasPrecision(18, 4);
            b.Property(e => e.CreditsGranted).HasPrecision(18, 4);
            b.Property(e => e.ConfirmedBy).HasMaxLength(64);
            b.Property(e => e.Note).HasMaxLength(256);
            b.Property(e => e.CallerId).HasMaxLength(64);
            b.Property(e => e.IdempotencyKey).HasMaxLength(128);
        });

        // Issue #197 — master switch (singleton).
        modelBuilder.Entity<EnforcementConfig>(b =>
        {
            b.HasKey(e => e.Id);
            b.Property(e => e.UpdatedBy).HasMaxLength(64);
        });
    }
}
