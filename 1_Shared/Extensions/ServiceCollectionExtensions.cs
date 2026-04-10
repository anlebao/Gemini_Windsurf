using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using VanAn.Shared.Domain;
using System;

namespace VanAn.Shared.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add SQLite DbContext with WAL Mode and optimized settings for Edge Nodes
    /// </summary>
    public static IServiceCollection AddVanAnSqlite(this IServiceCollection services, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(connectionString);

        // 🛡️ PHASE 5: Connection Pooling for SQLite - Prevent connection exhaustion
        services.AddDbContextPool<DbContext>(options =>
        {
            // 🛡️ PHASE 5: WAL Mode - Prevent write-lock concurrency
            var walConnectionString = $"{connectionString};Mode=ReadWrite;Cache=Shared";
            options.UseSqlite(walConnectionString, sqliteOptions =>
            {
                sqliteOptions.CommandTimeout(30);
            });
        }, poolSize: 128); // 🛡️ PHASE 5: Optimized pool size for Edge Nodes

        return services;
    }

    /// <summary>
    /// Initialize SQLite database with WAL mode and optimizations
    /// </summary>
    public static async Task InitializeSqliteDatabaseAsync(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbContext>();
        
        // Ensure database is created
        await context.Database.EnsureCreatedAsync();
        
        // 🛡️ PHASE 5: Enable WAL Mode and optimize SQLite
        await context.Database.OpenConnectionAsync();
        var command = context.Database.GetDbConnection().CreateCommand();
        
        // Enable WAL Mode for concurrent access
        command.CommandText = "PRAGMA journal_mode=WAL;";
        await command.ExecuteNonQueryAsync();
        
        // Close connection to allow proper database file creation
        await context.Database.CloseConnectionAsync();
        
        // Reopen for additional optimizations
        await context.Database.OpenConnectionAsync();
        command = context.Database.GetDbConnection().CreateCommand();
        
        // Optimize for Edge Node performance
        command.CommandText = "PRAGMA synchronous=NORMAL;";
        await command.ExecuteNonQueryAsync();
        
        command.CommandText = "PRAGMA cache_size=10000;";
        await command.ExecuteNonQueryAsync();
        
        command.CommandText = "PRAGMA temp_store=MEMORY;";
        await command.ExecuteNonQueryAsync();
        
        command.CommandText = "PRAGMA mmap_size=268435456;"; // 256MB memory-mapped I/O
        await command.ExecuteNonQueryAsync();
        
        // 🛡️ PHASE 5: Additional optimizations for Edge Nodes
        command.CommandText = "PRAGMA locking_mode=NORMAL;";
        await command.ExecuteNonQueryAsync();
        
        command.CommandText = "PRAGMA page_size=4096;";
        await command.ExecuteNonQueryAsync();
        
        await context.Database.CloseConnectionAsync();
        
        // Apply migrations
        await context.Database.MigrateAsync();
    }
}
