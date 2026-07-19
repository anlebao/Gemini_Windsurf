namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// Phase 2 (Multi-VPS Checkout): Result of a health check probe against a ShopInstance.
    /// Distinct from the Omnichannel HealthCheckResult (different namespace, different shape).
    /// </summary>
    public sealed record ShopInstanceHealthResult
    {
        public string Status { get; init; } = "Unknown";
        public long LatencyMs { get; init; }
        public DateTime CheckedAt { get; init; } = DateTime.UtcNow;
        public string? ErrorMessage { get; init; }

        public static ShopInstanceHealthResult Healthy(long latencyMs) => new()
        {
            Status = "Healthy",
            LatencyMs = latencyMs,
            CheckedAt = DateTime.UtcNow
        };

        public static ShopInstanceHealthResult Down(string error, long latencyMs = 0) => new()
        {
            Status = "Down",
            LatencyMs = latencyMs,
            CheckedAt = DateTime.UtcNow,
            ErrorMessage = error
        };
    }
}
