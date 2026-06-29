using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NATS.Client;

namespace VanAn.CoreHub.Infrastructure.Messaging;

/// <summary>
/// NATS.Client-based publisher for the Outbox → NATS sync path.
/// Registered as Singleton in edge/sync-worker DI (ADR-001 v2 Edge).
///
/// Connection is established once at startup and held for the service lifetime.
/// Constructor does NOT throw if NATS is unavailable — logs warning and runs in degraded mode.
/// </summary>
public sealed class NatsEventPublisher : INatsEventPublisher
{
    private readonly IConnection? _connection;
    private readonly ILogger<NatsEventPublisher> _logger;
    private bool _disposed;

    /// <summary>True when the NATS connection is established and healthy.</summary>
    public bool IsConnected => _connection?.State == ConnState.CONNECTED;

    /// <summary>Production constructor — creates its own NATS connection from config.</summary>
    public NatsEventPublisher(IConfiguration configuration, ILogger<NatsEventPublisher> logger)
        : this(CreateConnection(configuration, logger), logger)
    {
    }

    /// <summary>Internal constructor for unit-testing — accepts a pre-built (or mock) connection.</summary>
    internal NatsEventPublisher(IConnection? connection, ILogger<NatsEventPublisher> logger)
    {
        _connection = connection;
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task PublishAsync(string subject, byte[] payload, CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
        {
            _logger.LogWarning("NatsEventPublisher: not connected, skipping publish to {Subject}", subject);
            return Task.CompletedTask;
        }

        _connection!.Publish(subject, payload);
        _logger.LogDebug("Published {Bytes} bytes to NATS subject {Subject}", payload.Length, subject);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            _connection?.Drain();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "NatsEventPublisher: error during Drain on Dispose");
        }

        _connection?.Dispose();
        _logger.LogInformation("NatsEventPublisher disposed.");
    }

    // ──────────────────────────────────────────────────────────
    // Private helpers
    // ──────────────────────────────────────────────────────────

    private static IConnection? CreateConnection(IConfiguration configuration, ILogger logger)
    {
        var url = configuration.GetValue<string>("NATS__Url") ?? "nats://localhost:4222";
        try
        {
            var opts = ConnectionFactory.GetDefaultOptions();
            opts.Url = url;
            opts.MaxReconnect = 5;
            opts.ReconnectWait = 2000; // ms
            opts.Name = "vanan-shoperp-nats-sync";

            var connection = new ConnectionFactory().CreateConnection(opts);
            logger.LogInformation("NatsEventPublisher connected to {Url}", url);
            return connection;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "NatsEventPublisher: could not connect to NATS at {Url}. Publisher will run in degraded mode.",
                url);
            return null;
        }
    }
}
