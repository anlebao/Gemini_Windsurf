using Microsoft.Extensions.Logging;
using NATS.Client;

namespace VanAn.ShopERP.Services;

/// <summary>
/// Default implementation of NATS connection factory
/// Creates real NATS connections
/// </summary>
public class NatsConnectionFactory : INatsConnectionFactory
{
    private readonly ILogger<NatsConnectionFactory> _logger;

    public NatsConnectionFactory(ILogger<NatsConnectionFactory> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Creates a new NATS connection
    /// </summary>
    /// <param name="url">NATS server URL</param>
    /// <returns>NATS connection</returns>
    public IConnection CreateConnection(string url)
    {
        try
        {
            var connection = new ConnectionFactory().CreateConnection(url);
            _logger.LogInformation("Successfully connected to NATS at {Url}", url);
            return connection;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create NATS connection to {Url}", url);
            throw new InvalidOperationException($"Unable to connect to NATS server at {url}", ex);
        }
    }
}
