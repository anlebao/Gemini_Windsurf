using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VanAn.Shared.Domain;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Globalization;

namespace VanAn.Shared.Services;

/// <summary>
/// 🛡️ PHASE 5: Local Data Pruning Service for Edge Nodes
/// Mục đích: Cắt tỉa dữ liệu cũ giữ file SQLite siêu nhẹ
/// Chạy ngầm mỗi ngày, xóa Orders cũ > 7 ngày đã sync thành công
/// </summary>
public partial class LocalDataPruningService : BackgroundService
{
    private readonly ILogger<LocalDataPruningService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly TimeSpan _pruningInterval = TimeSpan.FromDays(1); // Chạy mỗi ngày
    private readonly TimeSpan _retentionPeriod = TimeSpan.FromDays(7); // Giữ 7 ngày

    public LocalDataPruningService(
        ILogger<LocalDataPruningService> logger,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogPruningServiceStarted();

        // Đợi 1 phút sau khi start để đảm bảo app khởi động xong
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PerformDataPruningAsync(stoppingToken);
                await Task.Delay(_pruningInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown
                break;
            }
            catch (Exception ex)
            {
                LogPruningError(ex);
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
        }
    }

    private async Task PerformDataPruningAsync(CancellationToken cancellationToken)
    {
        LogPruningProcessStarted();

        using var scope = _serviceProvider.CreateScope();
        // TODO: Move EF Core DbContext usage to Infrastructure layer
        // var context = scope.ServiceProvider.GetRequiredService<DbContext>();

        try
        {
            // 🛡️ PHASE 5: Xóa Orders cũ hơn retention period VÀ đã sync thành công
            var cutoffDate = DateTime.UtcNow.Subtract(_retentionPeriod);
            
            // TODO: Move EF Core DbContext usage to Infrastructure layer
            // var oldSyncedOrders = await context.Set<Order>()
            //     .Where(o => o.OrderDate < cutoffDate && 
            //                o.IsSyncedToCoreHub == true) // 🛡️ PHASE 5: Chỉ xóa orders đã sync
            //     .ToListAsync(cancellationToken);
            
            var oldSyncedOrders = new List<Order>(); // Placeholder

            // PERFORMANCE: Use Count > 0 instead of Any() for better performance
            if (oldSyncedOrders.Count > 0)
            {
                var deletedCount = oldSyncedOrders.Count;
                // TODO: Move EF Core DbContext usage to Infrastructure layer
                // context.Set<Order>().RemoveRange(oldSyncedOrders);
                // await context.SaveChangesAsync(cancellationToken);

                LogOrdersPruned(deletedCount, cutoffDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

                // 🛡️ PHASE 5: VACUUM để chống phân mảnh file
                // TODO: Move EF Core DbContext usage to Infrastructure layer
                // await PerformVacuumAsync(context, cancellationToken);
            }
            else
            {
                LogNoOrdersToPrune();
            }

            // 🛡️ PHASE 5: Kiểm tra kích thước file database
            // TODO: Move EF Core DbContext usage to Infrastructure layer
            // await CheckDatabaseSizeAsync(context, cancellationToken);
        }
        catch (Exception ex)
        {
            LogPruningFailed(ex);
            throw;
        }
    }

    // TODO: Move EF Core DbContext methods to Infrastructure layer
    // private async Task PerformVacuumAsync(DbContext context, CancellationToken cancellationToken)
    // {
    //     try
    //     {
    //         await context.Database.OpenConnectionAsync();
    //         var command = context.Database.GetDbConnection().CreateCommand();
    //         
    //         // 🛡️ PHASE 5: VACUUM để chống phân mảnh file SQLite
    //         command.CommandText = "VACUUM;";
    //         await command.ExecuteNonQueryAsync();
    //         
    //         await context.Database.CloseConnectionAsync();
    //         
    //         LogVacuumCompleted();
    //     }
    //     catch (Exception ex)
    //     {
    //         LogVacuumFailed(ex);
    //         throw;
    //     }
    // }

    // TODO: Move EF Core DbContext methods to Infrastructure layer
    // private async Task CheckDatabaseSizeAsync(DbContext context, CancellationToken cancellationToken)
    // {
    //     try
    //     {
    //         await context.Database.OpenConnectionAsync();
    //         var command = context.Database.GetDbConnection().CreateCommand();
    //         
    //         // 🛡️ PHASE 5: Kiểm tra kích thước file database
    //         command.CommandText = "SELECT page_count * page_size as size FROM pragma_page_count(), pragma_page_size();";
    //         var result = await command.ExecuteScalarAsync();
    //         
    //         if (result != null && long.TryParse(result.ToString(), out var sizeInBytes))
    //         {
    //             var sizeInMB = sizeInBytes / (1024.0 * 1024.0);
    //             LogDatabaseSize(sizeInMB);
    //         }
    //         
    //         await context.Database.CloseConnectionAsync();
    //     }
    //     catch (Exception ex)
    //     {
    //         LogDatabaseSizeCheckFailed(ex);
    //         // Don't throw - size check is not critical
    //     }
    // }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        LogPruningServiceStopping();
        await base.StopAsync(cancellationToken);
    }

    // High-Performance Logging Methods
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "🛡️ PHASE 5: Local Data Pruning Service started")]
    private partial void LogPruningServiceStarted();

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "🔄 Starting data pruning process...")]
    private partial void LogPruningProcessStarted();

    [LoggerMessage(EventId = 3, Level = LogLevel.Error, Message = "❌ Error during data pruning. Retrying in 1 hour...")]
    private partial void LogPruningError(Exception ex);

    [LoggerMessage(EventId = 4, Level = LogLevel.Information, Message = "✅ Pruned {Count} old orders (older than {Date})")]
    private partial void LogOrdersPruned(int count, string date);

    [LoggerMessage(EventId = 5, Level = LogLevel.Information, Message = "ℹ️ No orders to prune. All data within retention period.")]
    private partial void LogNoOrdersToPrune();

    [LoggerMessage(EventId = 6, Level = LogLevel.Error, Message = "❌ Failed to perform data pruning")]
    private partial void LogPruningFailed(Exception ex);

    [LoggerMessage(EventId = 7, Level = LogLevel.Information, Message = "🧹 Performing VACUUM to compact database...")]
    private partial void LogVacuumStarted();

    [LoggerMessage(EventId = 8, Level = LogLevel.Information, Message = "✅ VACUUM completed successfully")]
    private partial void LogVacuumCompleted();

    [LoggerMessage(EventId = 9, Level = LogLevel.Error, Message = "❌ Failed to perform VACUUM")]
    private partial void LogVacuumFailed(Exception ex);

    [LoggerMessage(EventId = 10, Level = LogLevel.Information, Message = "📊 Database size: {Size:F2} MB")]
    private partial void LogDatabaseSize(double size);

    [LoggerMessage(EventId = 11, Level = LogLevel.Warning, Message = "⚠️ Database size is large ({Size:F2} MB). Consider reducing retention period.")]
    private partial void LogDatabaseSizeWarning(double size);

    [LoggerMessage(EventId = 12, Level = LogLevel.Error, Message = "❌ Failed to check database size")]
    private partial void LogDatabaseSizeCheckFailed(Exception ex);

    [LoggerMessage(EventId = 13, Level = LogLevel.Information, Message = "🛡️ PHASE 5: Local Data Pruning Service stopping...")]
    private partial void LogPruningServiceStopping();
}
