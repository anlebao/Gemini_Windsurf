namespace VanAn.CoreHub.Infrastructure;

/// <summary>
/// Configuration options for the R2 photo cleanup background service.
/// Bind from appsettings.json section "R2Cleanup".
/// </summary>
public class R2CleanupOptions
{
    /// <summary>Whether the cleanup service is enabled. Default: true.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>How long to retain photos after checkout/void. Default: 30 days.</summary>
    public int RetentionDays { get; set; } = 30;

    /// <summary>How often the cleanup job runs. Default: 24 hours.</summary>
    public int RunIntervalHours { get; set; } = 24;

    /// <summary>Max objects per R2 DeleteObjects batch. R2/S3 limit: 1000.</summary>
    public int BatchSize { get; set; } = 1000;
}
