namespace VanAn.Metering.Contracts;

/// <summary>SRS §6.1 — POST /v1/rate-cards. Enforcement = "MeterOnly"|"Deny" (default MeterOnly).</summary>
public sealed record RateCardCreateRequest(
    string ServiceCode,
    string MeterUnit,
    decimal CreditRate,
    string? Enforcement = null,
    DateTime? EffectiveFrom = null,
    DateTime? EffectiveTo = null,
    string? Label = null,
    string? AuditNote = null);

/// <summary>PATCH /v1/rate-cards/{id} — null fields keep the current version's values.
/// Produces a NEW version (append-only): the patched version is closed at EffectiveFrom.</summary>
public sealed record RateCardPatchRequest(
    decimal? CreditRate = null,
    string? MeterUnit = null,
    string? Enforcement = null,
    DateTime? EffectiveFrom = null,
    string? Label = null,
    string? AuditNote = null);

public sealed record RateCardDto(
    Guid Id,
    string ServiceCode,
    string MeterUnit,
    decimal CreditRate,
    string Enforcement,
    DateTime EffectiveFrom,
    DateTime? EffectiveTo,
    bool IsActive,
    string? Label,
    string ChangedBy,
    DateTime ChangedAtUtc,
    string? AuditNote);
