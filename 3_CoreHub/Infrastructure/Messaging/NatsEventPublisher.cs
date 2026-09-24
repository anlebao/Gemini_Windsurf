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
    private static readonly TimeSpan ReconnectThrottle = TimeSpan.FromSeconds(30);

    private readonly Func<IConnection?>? _connectionFactory;
    private readonly ILogger<NatsEventPublisher> _logger;
    private readonly object _reconnectLock = new();
    private IConnection? _connection;
    private DateTime _lastReconnectAttempt = DateTime.MinValue;
    private bool _disposed;

    /// <summary>True when the NATS connection is established and healthy.</summary>
    public bool IsConnected => _connection?.State == ConnState.CONNECTED;

    /// <summary>Production constructor — creates its own NATS connection from config.</summary>
    public NatsEventPublisher(IConfiguration configuration, ILogger<NatsEventPublisher> logger)
    {
        _logger = logger;
        var url = ResolveUrl(configuration);
        _connectionFactory = () => CreateConnection(url, _logger);
        _connection = _connectionFactory();
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
        // E9: if the initial connect failed (or reconnects were exhausted), the publisher used to
        // stay dead forever. Try a throttled reconnect at publish-time instead of skipping silently.
        if (!IsConnected)
            TryReconnect();

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

    /// <summary>
    /// Throttled reconnect attempt (E9). NATS.Client already auto-reconnects while the
    /// initial connection is alive — this only covers startup-failure (connection == null)
    /// or permanently-closed connections. Safe to call from PublishAsync.
    /// </summary>
    private void TryReconnect()
    {
        if (_connectionFactory == null || _disposed)
            return;

        lock (_reconnectLock)
        {
            if (IsConnected)
                return;
            var now = DateTime.UtcNow;
            if (now - _lastReconnectAttempt < ReconnectThrottle)
                return;
            _lastReconnectAttempt = now;

            try
            {
                _connection?.Dispose();
            }
            catch { /* stale connection — ignore dispose errors */ }

            _connection = _connectionFactory();
        }
    }

    private static string ResolveUrl(IConfiguration configuration)
    {
        // Try multiple config keys — env var naming varies (NATS__Url, Nats__Url, ConnectionStrings:Nats)
        return configuration.GetValue<string>("NATS__Url")
            ?? configuration.GetValue<string>("Nats__Url")
            ?? configuration.GetValue<string>("NATS:Url")
            ?? configuration.GetValue<string>("Nats:Url")
            ?? configuration.GetValue<string>("ConnectionStrings:Nats")
            ?? configuration.GetValue<string>("ConnectionStrings__Nats")
            ?? "nats://localhost:4222";
    }

    private static IConnection? CreateConnection(string url, ILogger logger)
    {
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
