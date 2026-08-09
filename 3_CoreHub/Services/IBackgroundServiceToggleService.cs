namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// REQ-1.2: Runtime toggle for background services via SystemSetting (PG).
    /// Keys: "BackgroundServices:Enable{ServiceName}" → "true"/"false".
    /// Default: enabled (returns true if setting doesn't exist).
    /// Cached 30s to avoid DB query on every poll cycle.
    /// Admin UI: /admin/background-services (SystemAdmin role).
    /// </summary>
    public interface IBackgroundServiceToggleService
    {
        /// <summary>Check if a background service is enabled. Returns true by default (no setting = enabled).</summary>
        Task<bool> IsEnabledAsync(string serviceName, CancellationToken ct = default);

        /// <summary>Get all known service toggles with current state.</summary>
        Task<IReadOnlyList<BackgroundServiceToggleDto>> GetAllAsync(CancellationToken ct = default);

        /// <summary>Set toggle state for a service. Creates SystemSetting row if not exists.</summary>
        Task SetEnabledAsync(string serviceName, bool enabled, Guid updatedBy, CancellationToken ct = default);
    }

    public record BackgroundServiceToggleDto(
        string ServiceName,
        string DisplayName,
        string Description,
        string Vps,
        bool IsEnabled);
}
