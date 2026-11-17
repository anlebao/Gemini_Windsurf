using Microsoft.EntityFrameworkCore;
using VanAn.Atrs.Contracts;
using VanAn.Atrs.Domain;

namespace VanAn.Atrs.Infra;

/// <summary>
/// ATRS-6 subset (Wave 4) — PersonalEvidenceService: U02 backend (FR-006) + consent (§11.2).
/// Nguyên tắc:
/// - FieldKey allowlist — user chỉ khai báo FULL_NAME/CONTACT_PHONE/RECOVERY_NOTE.
/// - USER_DECLARED luôn VerificationStatus=UNVERIFIED (domain enforce).
/// - GET trả MaskedValue — không trả plaintext field nhạy cảm.
/// - Mọi write → AccountSecurityEvent (audit).
/// </summary>
public interface IPersonalEvidenceService
{
    Task<IReadOnlyList<PersonalEvidenceItemDto>> GetMineAsync(Guid trustSubjectId, CancellationToken ct = default);
    Task<PersonalEvidenceItemDto?> UpsertAsync(Guid trustSubjectId, UpsertPersonalEvidenceRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<ConsentDto>> ListConsentsAsync(Guid trustSubjectId, CancellationToken ct = default);
    Task<ConsentDto> GrantConsentAsync(Guid trustSubjectId, GrantConsentRequest request, CancellationToken ct = default);
    Task<bool> WithdrawConsentAsync(Guid trustSubjectId, Guid consentId, CancellationToken ct = default);
}

public sealed class PersonalEvidenceService(AtrsDbContext db) : IPersonalEvidenceService
{
    /// <summary>Allowlist field user được khai báo (FR-006 v1 — DOB omit theo Q1).</summary>
    public static readonly IReadOnlySet<string> AllowedFieldKeys = new HashSet<string>(StringComparer.Ordinal)
    {
        PersonalEvidenceItem.FieldFullName,
        PersonalEvidenceItem.FieldContactPhone,
        PersonalEvidenceItem.FieldRecoveryNote
    };

    /// <summary>Purpose consent hợp lệ (allowlist — tránh purpose rác trong audit).</summary>
    public static readonly IReadOnlySet<string> AllowedConsentPurposes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "evidence_processing",
        "security_notifications",
        "recovery_support"
    };

    public async Task<IReadOnlyList<PersonalEvidenceItemDto>> GetMineAsync(Guid trustSubjectId, CancellationToken ct = default)
    {
        var items = await db.PersonalEvidenceItems.AsNoTracking()
            .Where(e => e.TrustSubjectId == trustSubjectId)
            .OrderBy(e => e.FieldKey)
            .ToListAsync(ct);
        return items.Select(ToDto).ToList();
    }

    public async Task<PersonalEvidenceItemDto?> UpsertAsync(Guid trustSubjectId, UpsertPersonalEvidenceRequest request, CancellationToken ct = default)
    {
        var fieldKey = (request.FieldKey ?? string.Empty).Trim().ToUpperInvariant();
        if (!AllowedFieldKeys.Contains(fieldKey))
            return null;
        if (string.IsNullOrWhiteSpace(request.PrivacyNoticeVersion))
            return null;
        if (fieldKey == PersonalEvidenceItem.FieldRecoveryNote && (request.Value?.Length ?? 0) > 1000)
            return null;
        if ((request.Value?.Length ?? 0) > 500)
            return null;

        // Consent link: evidence_processing consent active → gắn ConsentRecordId.
        var consent = await db.ConsentRecords
            .Where(c => c.TrustSubjectId == trustSubjectId
                && c.Purpose == "evidence_processing"
                && c.WithdrawnAt == null)
            .OrderByDescending(c => c.GrantedAt)
            .FirstOrDefaultAsync(ct);

        var existing = await db.PersonalEvidenceItems
            .FirstOrDefaultAsync(e => e.TrustSubjectId == trustSubjectId && e.FieldKey == fieldKey, ct);

        if (existing is null)
        {
            existing = new PersonalEvidenceItem(
                Guid.NewGuid(), trustSubjectId, fieldKey, request.Value,
                PersonalEvidenceSource.USER_DECLARED, request.PrivacyNoticeVersion,
                consent?.Id);
            db.PersonalEvidenceItems.Add(existing);
        }
        else
        {
            existing.UpdateDeclaredValue(request.Value, request.PrivacyNoticeVersion, consent?.Id);
        }

        db.AccountSecurityEvents.Add(AccountSecurityEvent.Create(
            Guid.NewGuid(), trustSubjectId, "evidence.updated", SecurityEventActorType.User, null,
            sourceApplication: "vanan-atrs", deviceId: null, sessionCorrelationId: null,
            SecurityEventOutcome.Success, null,
            minimalMetadataJson: $"{{\"fieldKey\":\"{fieldKey}\"}}"));
        await db.SaveChangesAsync(ct);
        return ToDto(existing);
    }

    public async Task<IReadOnlyList<ConsentDto>> ListConsentsAsync(Guid trustSubjectId, CancellationToken ct = default)
    {
        var items = await db.ConsentRecords.AsNoTracking()
            .Where(c => c.TrustSubjectId == trustSubjectId)
            .OrderByDescending(c => c.GrantedAt)
            .Take(100)
            .ToListAsync(ct);
        return items.Select(ToDto).ToList();
    }

    public async Task<ConsentDto> GrantConsentAsync(Guid trustSubjectId, GrantConsentRequest request, CancellationToken ct = default)
    {
        var purpose = (request.Purpose ?? string.Empty).Trim();
        if (!AllowedConsentPurposes.Contains(purpose))
            throw new ArgumentException($"Invalid consent purpose: {request.Purpose}");
        if (string.IsNullOrWhiteSpace(request.PrivacyNoticeVersion))
            throw new ArgumentException("PrivacyNoticeVersion is required.");

        // Active consent cùng purpose đã tồn tại → trả lại (idempotent).
        var active = await db.ConsentRecords
            .Where(c => c.TrustSubjectId == trustSubjectId && c.Purpose == purpose && c.WithdrawnAt == null)
            .OrderByDescending(c => c.GrantedAt)
            .FirstOrDefaultAsync(ct);
        if (active is not null)
            return ToDto(active);

        var consent = new ConsentRecord(Guid.NewGuid(), trustSubjectId, purpose, request.PrivacyNoticeVersion);
        db.ConsentRecords.Add(consent);
        db.AccountSecurityEvents.Add(AccountSecurityEvent.Create(
            Guid.NewGuid(), trustSubjectId, "consent.granted", SecurityEventActorType.User, null,
            sourceApplication: "vanan-atrs", deviceId: null, sessionCorrelationId: null,
            SecurityEventOutcome.Success, null,
            minimalMetadataJson: $"{{\"purpose\":\"{purpose}\"}}"));
        await db.SaveChangesAsync(ct);
        return ToDto(consent);
    }

    public async Task<bool> WithdrawConsentAsync(Guid trustSubjectId, Guid consentId, CancellationToken ct = default)
    {
        var consent = await db.ConsentRecords
            .FirstOrDefaultAsync(c => c.Id == consentId && c.TrustSubjectId == trustSubjectId, ct);
        if (consent is null || consent.WithdrawnAt is not null)
            return false;

        consent.Withdraw();
        db.AccountSecurityEvents.Add(AccountSecurityEvent.Create(
            Guid.NewGuid(), trustSubjectId, "consent.withdrawn", SecurityEventActorType.User, null,
            sourceApplication: "vanan-atrs", deviceId: null, sessionCorrelationId: null,
            SecurityEventOutcome.Success, null,
            minimalMetadataJson: $"{{\"purpose\":\"{consent.Purpose}\"}}"));
        await db.SaveChangesAsync(ct);
        return true;
    }

    private static PersonalEvidenceItemDto ToDto(PersonalEvidenceItem e)
        => new(
            e.FieldKey,
            e.Value is not null,
            MaskValue(e.FieldKey, e.Value),
            e.Source.ToString(),
            e.VerificationStatus.ToString(),
            e.PrivacyNoticeVersion,
            e.UpdatedAt ?? e.CollectedAt);

    private static ConsentDto ToDto(ConsentRecord c)
        => new(c.Id, c.Purpose, c.PrivacyNoticeVersion, c.GrantedAt, c.WithdrawnAt);

    /// <summary>Mask giá trị nhạy cảm — không trả plaintext (FR-006/U02).</summary>
    internal static string? MaskValue(string fieldKey, string? value)
    {
        if (value is null) return null;
        if (fieldKey == PersonalEvidenceItem.FieldRecoveryNote)
            return value.Length <= 2 ? "••" : value[..1] + new string('•', Math.Min(value.Length - 2, 20)) + value[^1..];
        // phone/full-name: giữ 2 ký tự đầu + 2 cuối, mask giữa
        if (value.Length <= 4) return "••••";
        return value[..2] + new string('*', Math.Min(value.Length - 4, 20)) + value[^2..];
    }
}
