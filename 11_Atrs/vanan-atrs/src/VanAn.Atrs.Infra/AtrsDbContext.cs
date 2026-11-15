using Microsoft.EntityFrameworkCore;
using VanAn.Atrs.Domain;

namespace VanAn.Atrs.Infra;

/// <summary>
/// AtrsDbContext — PostgreSQL riêng của vanan-atrs (ADR-001, D2).
/// Trust registry CROSS-tenant: entities account-level KHÔNG có TenantId bắt buộc;
/// multi-tenancy enforce ở tầng authorization + staff scope (không global query filter).
/// Naming: snake_case (convention EFCore.NamingConventions — bật ở Program).
/// </summary>
public sealed class AtrsDbContext(DbContextOptions<AtrsDbContext> options) : DbContext(options)
{
    public DbSet<TrustSubject> TrustSubjects => Set<TrustSubject>();
    public DbSet<TrustSubjectLink> TrustSubjectLinks => Set<TrustSubjectLink>();
    public DbSet<AccountTrustProfile> AccountTrustProfiles => Set<AccountTrustProfile>();
    public DbSet<AccountSecurityEvent> AccountSecurityEvents => Set<AccountSecurityEvent>();
    public DbSet<ConsentRecord> ConsentRecords => Set<ConsentRecord>();
    public DbSet<RecoveryCodeSet> RecoveryCodeSets => Set<RecoveryCodeSet>();
    public DbSet<RecoveryCode> RecoveryCodes => Set<RecoveryCode>();
    public DbSet<RecoveryRequest> RecoveryRequests => Set<RecoveryRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var subject = modelBuilder.Entity<TrustSubject>();
        subject.ToTable("trust_subjects");
        subject.HasKey(e => e.Id);
        subject.Property(e => e.Id).ValueGeneratedNever();
        subject.Property(e => e.PrimaryContactHash).HasMaxLength(64);
        subject.HasIndex(e => e.PrimaryContactHash).HasDatabaseName("ix_trust_subjects_contact_hash");

        var link = modelBuilder.Entity<TrustSubjectLink>();
        link.ToTable("trust_subject_links");
        link.HasKey(e => e.Id);
        link.Property(e => e.Id).ValueGeneratedNever();
        link.Property(e => e.IdentityType).HasConversion<int>();
        link.HasIndex(e => new { e.TrustSubjectId, e.IdentityType, e.IdentityId })
            .IsUnique()
            .HasDatabaseName("ux_trust_subject_links_subject_identity");

        var profile = modelBuilder.Entity<AccountTrustProfile>();
        profile.ToTable("account_trust_profiles");
        profile.HasKey(e => e.Id);
        profile.Property(e => e.Id).ValueGeneratedNever();
        profile.Property(e => e.Status).HasConversion<int>();
        profile.Property(e => e.AssuranceState).HasConversion<int>();
        profile.Property(e => e.RowVersion).IsRowVersion();
        profile.HasIndex(e => e.TrustSubjectId)
            .IsUnique()
            .HasDatabaseName("ux_account_trust_profiles_subject");

        var evt = modelBuilder.Entity<AccountSecurityEvent>();
        evt.ToTable("account_security_events");
        evt.HasKey(e => e.Id);
        evt.Property(e => e.Id).ValueGeneratedNever();
        evt.Property(e => e.EventType).HasMaxLength(64).IsRequired();
        evt.Property(e => e.ActorType).HasConversion<int>();
        evt.Property(e => e.Outcome).HasConversion<int>();
        evt.Property(e => e.ActorId).HasMaxLength(64);
        evt.Property(e => e.SourceApplication).HasMaxLength(32);
        evt.Property(e => e.SessionCorrelationId).HasMaxLength(64);
        evt.Property(e => e.ReasonCode).HasMaxLength(64);
        evt.Property(e => e.EvidenceReferencesJson).HasColumnType("TEXT");
        evt.Property(e => e.MinimalMetadataJson).HasColumnType("TEXT");
        evt.HasIndex(e => new { e.TrustSubjectId, e.OccurredAt })
            .HasDatabaseName("ix_account_security_events_subject_occurred");
        evt.HasIndex(e => new { e.EventType, e.OccurredAt })
            .HasDatabaseName("ix_account_security_events_type_occurred");
        evt.HasIndex(e => e.SessionCorrelationId)
            .HasDatabaseName("ix_account_security_events_correlation");

        var consent = modelBuilder.Entity<ConsentRecord>();
        consent.ToTable("consent_records");
        consent.HasKey(e => e.Id);
        consent.Property(e => e.Id).ValueGeneratedNever();
        consent.Property(e => e.Purpose).HasMaxLength(64).IsRequired();
        consent.Property(e => e.PrivacyNoticeVersion).HasMaxLength(32).IsRequired();
        consent.Property(e => e.EvidenceRef).HasMaxLength(128);
        consent.HasIndex(e => new { e.TrustSubjectId, e.Purpose })
            .HasDatabaseName("ix_consent_records_subject_purpose");

        var codeSet = modelBuilder.Entity<RecoveryCodeSet>();
        codeSet.ToTable("recovery_code_sets");
        codeSet.HasKey(e => e.Id);
        codeSet.Property(e => e.Id).ValueGeneratedNever();
        codeSet.Property(e => e.Status).HasConversion<int>();
        codeSet.HasIndex(e => new { e.TrustSubjectId, e.Status })
            .HasDatabaseName("ix_recovery_code_sets_subject_status");

        var code = modelBuilder.Entity<RecoveryCode>();
        code.ToTable("recovery_codes");
        code.HasKey(e => e.Id);
        code.Property(e => e.Id).ValueGeneratedNever();
        code.Property(e => e.CodeHash).HasMaxLength(64).IsRequired();
        code.HasIndex(e => e.SetId).HasDatabaseName("ix_recovery_codes_set");

        var req = modelBuilder.Entity<RecoveryRequest>();
        req.ToTable("recovery_requests");
        req.HasKey(e => e.Id);
        req.Property(e => e.Id).ValueGeneratedNever();
        req.Property(e => e.RequestType).HasConversion<int>();
        req.Property(e => e.Status).HasConversion<int>();
        req.Property(e => e.IdempotencyKey).HasMaxLength(64).IsRequired();
        req.Property(e => e.EvidenceSummaryJson).HasColumnType("TEXT");
        req.Property(e => e.RiskDecisionJson).HasColumnType("TEXT");
        req.Property(e => e.DecisionReasonCode).HasMaxLength(64);
        req.HasIndex(e => e.IdempotencyKey).IsUnique().HasDatabaseName("ux_recovery_requests_idempotency");
        req.HasIndex(e => new { e.TrustSubjectId, e.Status }).HasDatabaseName("ix_recovery_requests_subject_status");
    }
}