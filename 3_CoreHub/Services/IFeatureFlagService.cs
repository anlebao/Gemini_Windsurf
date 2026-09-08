namespace VanAn.CoreHub.Services;

/// <summary>
/// VALCN v2.0 feature toggle — SystemAdmin ON/OFF runtime.
/// Keys: "Features:Enable{FeatureName}" → "true"/"false".
/// Default: DISABLED (returns false if setting doesn't exist) — preserves existing behavior.
/// Cached 30s. Admin UI: /admin/valcn-features (SystemAdmin role).
/// </summary>
public interface IFeatureFlagService
{
    /// <summary>
    /// Check if a feature is enabled. Default when no SystemSetting row exists:
    /// VALCN v2 flags → false (disabled); Sprint 3 audit flags → gọi với defaultWhenMissing: true (audit ON mặc định).
    /// </summary>
    Task<bool> IsEnabledAsync(string featureName, bool defaultWhenMissing = false, CancellationToken ct = default);

    /// <summary>Get all known feature toggles with current state.</summary>
    Task<IReadOnlyList<FeatureFlagDto>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Set toggle state for a feature. Creates SystemSetting row if not exists.</summary>
    Task SetEnabledAsync(string featureName, bool enabled, Guid updatedBy, CancellationToken ct = default);
}

public record FeatureFlagDto(
    string FeatureName,
    string DisplayName,
    string Description,
    string Phase,
    bool IsEnabled);
