using VanAn.CoreHub.Models;
using VanAn.Shared.Omnichannel;

namespace VanAn.CoreHub.Common.Mappers
{
    /// <summary>
    /// Anti-corruption layer for health status mapping
    /// Prevents direct coupling between CoreHub and Shared domain models
    /// </summary>
    public static class HealthMapper
    {
        public static SystemHealth ToSystemHealth(HealthStatus status)
        {
            return status switch
            {
                HealthStatus.Excellent => SystemHealth.Excellent,
                HealthStatus.Good => SystemHealth.Good,
                HealthStatus.Warning => SystemHealth.Good, // Map warning to good for compatibility
                HealthStatus.Critical => SystemHealth.Critical,
                HealthStatus.Unknown => SystemHealth.Good, // Default to good for unknown
                _ => SystemHealth.Good
            };
        }

        public static HealthStatus ToHealthStatus(SystemHealth status)
        {
            return status switch
            {
                SystemHealth.Excellent => HealthStatus.Excellent,
                SystemHealth.Good => HealthStatus.Good,
                SystemHealth.Fair => HealthStatus.Warning, // Map fair to warning
                SystemHealth.Poor => HealthStatus.Critical, // Map poor to critical
                SystemHealth.Critical => HealthStatus.Critical,
                _ => HealthStatus.Warning // Default to warning for unknown
            };
        }
    }
}
