using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Formatting;
using System.Globalization;
using System.Reflection;

namespace VanAn.Shared.Extensions;

public static class SerilogExtensions
{
    public static IServiceCollection AddVanAnSerilog(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        
        // Configure Serilog with Seq sink and Rolling File sink
        var appName = Assembly.GetExecutingAssembly().GetName().Name;
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "VanAn")
            .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
            .WriteTo.Seq(
                serverUrl: "http://vanan-seq:5341",
                apiKey: null,
                controlLevelSwitch: null,
                restrictedToMinimumLevel: LogEventLevel.Information)
            .WriteTo.File(
                path: $"Logs/{appName}/log-.txt",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 2,
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}",
                shared: true,
                formatProvider: CultureInfo.InvariantCulture)
            .CreateLogger();

        // Add Serilog as logging provider
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog();
        });

        return services;
    }

    public static IServiceCollection AddVanAnSerilogDevelopment(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        
        // Development configuration with more verbose logging and Rolling File sink
        var appName = Assembly.GetExecutingAssembly().GetName().Name;
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .MinimumLevel.Override("System", LogEventLevel.Information)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "VanAn")
            .Enrich.WithProperty("Environment", "Development")
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Application} [{SourceContext}] {Message:lj}{NewLine}{Exception}",
                formatProvider: CultureInfo.InvariantCulture)
            .WriteTo.Seq(
                serverUrl: "http://vanan-seq:5341",
                apiKey: null,
                controlLevelSwitch: null,
                restrictedToMinimumLevel: LogEventLevel.Debug)
            .WriteTo.File(
                path: $"Logs/{appName}/log-.txt",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 2,
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}",
                shared: true,
                formatProvider: CultureInfo.InvariantCulture)
            .CreateLogger();

        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog();
        });

        return services;
    }
}
