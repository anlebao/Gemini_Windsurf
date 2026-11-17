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
    public DbSet<RecoveryCaseNote> RecoveryCaseNotes => Set<RecoveryCaseNote>();
    public DbSet<UserDevice> UserDevices => Set<UserDevice>();
    public DbSet<DeviceCredential> DeviceCredentials => Set<DeviceCredential>();
    public DbSet<WebAuthnChallenge> WebAuthnChallenges => Set<WebAuthnChallenge>();
    public DbSet<DisputeCase> DisputeCases => Set<DisputeCase>();
    public DbSet<DisputeEvidenceItem> DisputeEvidenceItems => Set<DisputeEvidenceItem>();
    public DbSet<DisputeDecision> DisputeDecisions => Set<DisputeDecision>();
    public DbSet<StaffPermissionGrant> StaffPermissionGrants => Set<StaffPermissionGrant>();
    public DbSet<PersonalEvidenceItem> PersonalEvidenceItems => Set<PersonalEvidenceItem>();
    public DbSet<EvidenceAttachment> EvidenceAttachments => Set<EvidenceAttachment>();
    public DbSet<SecurityPolicy> SecurityPolicies => Set<SecurityPolicy>();

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
        // Npgsql concurrency: RowVersion (uint) map tới system column xmin (kiểu xid).
        // xmin tồn tại sẵn trong mọi bảng PG — EF không tạo cột, không insert, DB tự quản.
        profile.Property(e => e.RowVersion)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();
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

        var note = modelBuilder.Entity<RecoveryCaseNote>();
        note.ToTable("recovery_case_notes");
        note.HasKey(e => e.Id);
        note.Property(e => e.Id).ValueGeneratedNever();
        note.Property(e => e.NoteType).HasConversion<int>();
        note.Property(e => e.Body).HasMaxLength(2000).IsRequired();
        note.HasIndex(e => e.RecoveryRequestId).HasDatabaseName("ix_recovery_case_notes_request");

        var device = modelBuilder.Entity<UserDevice>();
        device.ToTable("user_devices");
        device.HasKey(e => e.Id);
        device.Property(e => e.Id).ValueGeneratedNever();
        device.Property(e => e.DeviceId).HasMaxLength(128).IsRequired();
        device.Property(e => e.DeviceType).HasConversion<int>();
        device.Property(e => e.Platform).HasMaxLength(128);
        device.Property(e => e.FriendlyName).HasMaxLength(128);
        device.Property(e => e.RegistrationMethod).HasConversion<int>();
        device.Property(e => e.Status).HasConversion<int>();
        device.Property(e => e.RevokedReason).HasMaxLength(64);
        device.Property(e => e.RiskMetadataJson).HasColumnType("TEXT");
        device.HasIndex(e => new { e.TrustSubjectId, e.DeviceId })
            .IsUnique()
            .HasDatabaseName("ux_user_devices_subject_device");
        device.HasIndex(e => new { e.TrustSubjectId, e.Status })
            .HasDatabaseName("ix_user_devices_subject_status");
        device.HasIndex(e => e.LastSeenAt).HasDatabaseName("ix_user_devices_last_seen");

        var credential = modelBuilder.Entity<DeviceCredential>();
        credential.ToTable("device_credentials");
        credential.HasKey(e => e.Id);
        credential.Property(e => e.Id).ValueGeneratedNever();
        credential.Property(e => e.CredentialId).HasMaxLength(512).IsRequired();
        credential.Property(e => e.PublicKey).HasMaxLength(2048).IsRequired();
        credential.Property(e => e.RpId).HasMaxLength(255).IsRequired();
        credential.Property(e => e.TransportsJson).HasColumnType("TEXT");
        credential.Property(e => e.Status).HasConversion<int>();
        credential.HasIndex(e => e.CredentialId)
            .IsUnique()
            .HasDatabaseName("ux_device_credentials_credential_id");
        credential.HasIndex(e => new { e.TrustSubjectId, e.Status })
            .HasDatabaseName("ix_device_credentials_subject_status");
        credential.HasIndex(e => e.DeviceRecordId)
            .HasDatabaseName("ix_device_credentials_device");

        var challenge = modelBuilder.Entity<WebAuthnChallenge>();
        challenge.ToTable("webauthn_challenges");
        challenge.HasKey(e => e.Id);
        challenge.Property(e => e.Id).ValueGeneratedNever();
        challenge.Property(e => e.Purpose).HasConversion<int>();
        challenge.Property(e => e.Challenge).HasMaxLength(256).IsRequired();
        challenge.Property(e => e.Origin).HasMaxLength(255).IsRequired();
        challenge.Property(e => e.RpId).HasMaxLength(255).IsRequired();
        challenge.HasIndex(e => e.Challenge)
            .IsUnique()
            .HasDatabaseName("ux_webauthn_challenges_challenge");
        challenge.HasIndex(e => new { e.TrustSubjectId, e.Purpose, e.ConsumedAt })
            .HasDatabaseName("ix_webauthn_challenges_subject_purpose_consumed");
        challenge.HasIndex(e => e.ExpiresAt).HasDatabaseName("ix_webauthn_challenges_expires");

        // ═══ ATRS-9/ATRS-10 (Wave 3) — Dispute vertical + staff RBAC ═══

        var dispute = modelBuilder.Entity<DisputeCase>();
        dispute.ToTable("dispute_cases");
        dispute.HasKey(e => e.Id);
        dispute.Property(e => e.Id).ValueGeneratedNever();
        dispute.Property(e => e.CaseType).HasConversion<int>();
        dispute.Property(e => e.Status).HasConversion<int>();
        dispute.Property(e => e.ClaimedIssue).HasMaxLength(4000).IsRequired();
        dispute.Property(e => e.RelatedTransactionIdsJson).HasColumnType("TEXT");
        dispute.Property(e => e.RelatedLedgerReferencesJson).HasColumnType("TEXT");
        dispute.Property(e => e.IdempotencyKey).HasMaxLength(128);
        dispute.Property(e => e.UserFacingStatusNote).HasMaxLength(1000);
        dispute.HasIndex(e => e.IdempotencyKey).HasDatabaseName("ix_dispute_cases_idempotency");
        dispute.HasIndex(e => new { e.TrustSubjectId, e.Status }).HasDatabaseName("ix_dispute_cases_subject_status");
        dispute.HasIndex(e => new { e.Status, e.SubmittedAt }).HasDatabaseName("ix_dispute_cases_status_submitted");
        dispute.HasIndex(e => e.TenantId).HasDatabaseName("ix_dispute_cases_tenant");

        var evidence = modelBuilder.Entity<DisputeEvidenceItem>();
        evidence.ToTable("dispute_evidence_items");
        evidence.HasKey(e => e.Id);
        evidence.Property(e => e.Id).ValueGeneratedNever();
        evidence.Property(e => e.EvidenceType).HasConversion<int>();
        evidence.Property(e => e.SourceSystem).HasMaxLength(64).IsRequired();
        evidence.Property(e => e.SourceReference).HasMaxLength(1024);
        evidence.Property(e => e.IntegrityHash).HasMaxLength(64);
        evidence.Property(e => e.SubmittedBy).HasMaxLength(128).IsRequired();
        evidence.Property(e => e.ReliabilityClass).HasConversion<int>();
        evidence.Property(e => e.Description).HasMaxLength(2000);
        evidence.Property(e => e.AccessClassification).HasConversion<int>();
        evidence.HasIndex(e => e.DisputeCaseId).HasDatabaseName("ix_dispute_evidence_case");

        var decision = modelBuilder.Entity<DisputeDecision>();
        decision.ToTable("dispute_decisions");
        decision.HasKey(e => e.Id);
        decision.Property(e => e.Id).ValueGeneratedNever();
        decision.Property(e => e.Decision).HasConversion<int>();
        decision.Property(e => e.Status).HasConversion<int>();
        decision.Property(e => e.DecisionReason).HasMaxLength(2000).IsRequired();
        decision.Property(e => e.UserFacingExplanation).HasMaxLength(2000);
        decision.Property(e => e.InternalRationale).HasMaxLength(4000);
        decision.Property(e => e.PolicyVersion).HasMaxLength(32).IsRequired();
        decision.Property(e => e.ProposedAdjustmentReason).HasMaxLength(500);
        decision.Property(e => e.ResolutionReference).HasMaxLength(256);
        decision.Property(e => e.IdempotencyKey).HasMaxLength(128);
        decision.Property(e => e.RejectionReason).HasMaxLength(1000);
        decision.HasIndex(e => e.DisputeCaseId).HasDatabaseName("ix_dispute_decisions_case");
        decision.HasIndex(e => e.IdempotencyKey).IsUnique().HasDatabaseName("ux_dispute_decisions_idempotency");

        var grant = modelBuilder.Entity<StaffPermissionGrant>();
        grant.ToTable("staff_permission_grants");
        grant.HasKey(e => e.Id);
        grant.Property(e => e.Id).ValueGeneratedNever();
        grant.Property(e => e.Permission).HasMaxLength(64).IsRequired();
        grant.Property(e => e.GrantedBy).HasMaxLength(128).IsRequired();
        grant.HasIndex(e => new { e.DemoUserId, e.Permission }).HasDatabaseName("ix_staff_permission_grants_user_perm");

        // ═══ ATRS-6 subset + D9 (Wave 4) — personal evidence + attachments ═══

        var evidenceItem = modelBuilder.Entity<PersonalEvidenceItem>();
        evidenceItem.ToTable("personal_evidence_items");
        evidenceItem.HasKey(e => e.Id);
        evidenceItem.Property(e => e.Id).ValueGeneratedNever();
        evidenceItem.Property(e => e.FieldKey).HasMaxLength(32).IsRequired();
        evidenceItem.Property(e => e.Value).HasMaxLength(500);
        evidenceItem.Property(e => e.Source).HasConversion<int>();
        evidenceItem.Property(e => e.VerificationStatus).HasConversion<int>();
        evidenceItem.Property(e => e.PrivacyNoticeVersion).HasMaxLength(32).IsRequired();
        evidenceItem.HasIndex(e => new { e.TrustSubjectId, e.FieldKey })
            .IsUnique()
            .HasDatabaseName("ux_personal_evidence_subject_field");

        var attachment = modelBuilder.Entity<EvidenceAttachment>();
        attachment.ToTable("evidence_attachments");
        attachment.HasKey(e => e.Id);
        attachment.Property(e => e.Id).ValueGeneratedNever();
        attachment.Property(e => e.StorageKey).HasMaxLength(512).IsRequired();
        attachment.Property(e => e.FileName).HasMaxLength(255).IsRequired();
        attachment.Property(e => e.ContentType).HasMaxLength(64).IsRequired();
        attachment.HasIndex(e => e.DisputeCaseId).HasDatabaseName("ix_evidence_attachments_case");
        attachment.HasIndex(e => e.TrustSubjectId).HasDatabaseName("ix_evidence_attachments_subject");

        // ═══ ATRS-10 A21 (Wave 5 W5c) — security policy versioned ═══

        var policy = modelBuilder.Entity<SecurityPolicy>();
        policy.ToTable("security_policies");
        policy.HasKey(e => e.Id);
        policy.Property(e => e.Id).ValueGeneratedNever();
        policy.Property(e => e.PolicyKey).HasMaxLength(64).IsRequired();
        policy.Property(e => e.PolicyGroup).HasMaxLength(32).IsRequired();
        policy.Property(e => e.JsonValue).HasColumnType("TEXT").IsRequired();
        policy.Property(e => e.Status).HasConversion<int>();
        policy.Property(e => e.ChangeNote).HasMaxLength(1000);
        policy.HasIndex(e => new { e.PolicyKey, e.Version }).IsUnique().HasDatabaseName("ux_security_policies_key_version");
        policy.HasIndex(e => new { e.PolicyKey, e.Status }).HasDatabaseName("ix_security_policies_key_status");
    }
}