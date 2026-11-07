namespace VanAn.TenantAi.Contracts;

/// <summary>task_ai_p6b — admin DTOs (SRS §6.3). All tenant-bearing, scope=admin.</summary>
public sealed record PutProfileRequest(
    string AiName,
    string? PersonaJson,
    string? BusinessProfileJson);

public sealed record ProfileVersionDto(
    int Version,
    string Status,
    string AiName,
    string PersonaJson,
    string BusinessProfileJson,
    DateTime? PublishedAtUtc,
    string? PublishedBy);

public sealed record ProfileResponse(
    ProfileVersionDto? Draft,
    ProfileVersionDto? Published,
    /// <summary>task_ai_p10 — rollback dropdown needs version history (all statuses, newest first, ≤20).</summary>
    IReadOnlyList<ProfileVersionDto> RecentVersions);

public sealed record PutProfileResponse(int Version, string Status);

public sealed record PublishResponse(
    int Version,
    string Status,
    DateTime PublishedAtUtc,
    string PublishedBy);

public sealed record RollbackRequest(int Version);

public sealed record PolicyResponse(
    int? ConfigVersion,
    string IntentScopesJson,
    string ExternalAllowJson,
    string RefusedTopicsJson);

public sealed record PutPolicyRequest(
    string? IntentScopesJson,
    string? ExternalAllowJson,
    string? RefusedTopicsJson);

public sealed record ConversationSummaryDto(
    string SessionId,
    string Channel,
    DateTime LastActiveAtUtc,
    int MessageCount,
    int RefusedCount,
    int DegradedCount);

public sealed record ConversationListResponse(
    int Page,
    int Size,
    int Total,
    IReadOnlyList<ConversationSummaryDto> Items);

public sealed record ConversationMessageDto(
    string Role,
    string Content,
    string? Intent,
    double? Confidence,
    bool Refused,
    bool Degraded,
    DateTime CreatedAtUtc);

public sealed record ConversationDetailResponse(
    string SessionId,
    IReadOnlyList<ConversationMessageDto> Messages);

public sealed record UsageDayDto(
    string Date,
    int Messages,
    int Refused,
    int Degraded,
    long PromptTokens,
    long CompletionTokens);

public sealed record UsageResponse(
    int Days,
    IReadOnlyList<UsageDayDto> PerDay,
    UsageDayDto Total);

public sealed record PurgeResponse(
    int Configs,
    int Policies,
    int UsagePermissions,
    int Products,
    int Documents,
    int Chunks,
    int Sessions,
    int Messages,
    int IngestLogs,
    int IntentRoutes);
