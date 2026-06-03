using NATS.Client;

namespace VanAn.ShopERP.Services
{
    /// <summary>
    /// Factory interface for creating NATS connections
    /// Enables mocking in tests
    /// </summary>
    public interface INatsConnectionFactory
    {
        /// <summary>
        /// Creates a new NATS connection
        /// </summary>
        /// <param name="url">NATS server URL</param>
        /// <returns>NATS connection</returns>
        IConnection CreateConnection(string url);
    }
}
