using Microsoft.Data.Sqlite;
using VanAn.CoreHub.Infrastructure;

namespace VanAn.CoreHub.Tests.TestInfrastructure
{
    /// <summary>
    /// Wrapper class to bind SQLite connection lifespan to DbContext lifespan.
    /// Ensures proper disposal of both context and connection.
    /// NO DI - direct instantiation only.
    /// </summary>
    public sealed class TestContextScope(VanAnDbContext context, SqliteConnection? connection = null) : IDisposable
    {
        private readonly SqliteConnection? _connection = connection;
        public VanAnDbContext Context { get; } = context;

        public void Dispose()
        {
            Context?.Dispose();
            _connection?.Dispose();
        }
    }
}
