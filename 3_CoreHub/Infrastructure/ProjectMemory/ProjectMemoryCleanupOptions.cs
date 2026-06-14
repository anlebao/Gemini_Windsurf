namespace VanAn.CoreHub.Infrastructure.ProjectMemory;

/// <summary>
/// Configuration options for the Project Memory cleanup background service.
/// Bind from appsettings.json section "ProjectMemoryCleanup".
/// </summary>
public class ProjectMemoryCleanupOptions
{
    /// <summary>How often the cleanup job runs. Default: 24 hours.</summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromHours(24);

    /// <summary>How long to retain completed sessions/tasks/history. Default: 30 days.</summary>
    public TimeSpan RetentionPeriod { get; set; } = TimeSpan.FromDays(30);

    /// <summary>Whether the cleanup service is enabled. Default: true.</summary>
    public bool Enabled { get; set; } = true;
}
