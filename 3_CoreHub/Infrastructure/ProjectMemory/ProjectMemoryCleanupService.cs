using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace VanAn.CoreHub.Infrastructure.ProjectMemory;

/// <summary>
/// Background service that periodically cleans up old Project Memory data.
/// </summary>
public class ProjectMemoryCleanupService(
    IServiceScopeFactory scopeFactory,
    IOptions<ProjectMemoryCleanupOptions> options,
    ILogger<ProjectMemoryCleanupService> logger) : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly ProjectMemoryCleanupOptions _options = options.Value;
    private readonly ILogger<ProjectMemoryCleanupService> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("ProjectMemoryCleanupService is disabled via configuration.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(_options.Interval, stoppingToken);

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IProjectMemoryService>();
                var result = await service.CleanupOldDataAsync(_options.RetentionPeriod);

                _logger.LogInformation(
                    "ProjectMemory cleanup completed: sessions={Sessions}, tasks={Tasks}, history={History}, decisions={Decisions}, elapsed={Elapsed}",
                    result.SessionsDeleted, result.TasksDeleted,
                    result.HistoryEntriesDeleted, result.DecisionsArchived,
                    result.ExecutionTime);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "ProjectMemoryCleanupService encountered an error during cleanup.");
            }
        }
    }
}
