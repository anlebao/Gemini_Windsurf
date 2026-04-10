namespace VanAn.CoreHub.Models;

/// <summary>
/// Standardized health status levels for VanAn ecosystem
/// Used across all services for consistent health reporting
/// </summary>
public enum HealthStatus
{
    Unknown = 0,
    Critical = 1,
    Warning = 2,
    Good = 3,
    Excellent = 4
}
