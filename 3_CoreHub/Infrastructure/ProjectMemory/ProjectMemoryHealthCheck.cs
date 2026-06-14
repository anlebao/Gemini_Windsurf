using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace VanAn.CoreHub.Infrastructure.ProjectMemory;

/// <summary>
/// Health check for Project Memory database connectivity.
/// </summary>
public class ProjectMemoryHealthCheck(ProjectMemoryDbContext dbContext) : IHealthCheck
{
    private readonly ProjectMemoryDbContext _dbContext = dbContext;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _ = await _dbContext.Database.CanConnectAsync(cancellationToken);
            return HealthCheckResult.Healthy("Project Memory database is reachable.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Project Memory database is unreachable.", ex);
        }
    }
}
