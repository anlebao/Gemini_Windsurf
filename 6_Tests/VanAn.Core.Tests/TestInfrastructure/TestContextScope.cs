using Microsoft.Data.Sqlite;
using VanAn.CoreHub.Infrastructure;

namespace VanAn.CoreHub.Tests.TestInfrastructure
{
    /// <summary>
    /// Wrapper class to bind SQLite connection lifespan to DbContext lifespan.
    /// Ensures proper disposal of both context and connection.
    /// NO DI - direct instantiation only.
    /// </summary>
    public sealed class TestContextScope(VanAnDbContext context, SqliteConnection? connection = null, TestTenantProvider? tenantProvider = null) : IDisposable
    {
        private readonly SqliteConnection? _connection = connection;
        public VanAnDbContext Context { get; } = context;

        /// <summary>
        /// The TestTenantProvider used by this context - allows tests to read
        /// or update the active tenant after seeding data.
        /// </summary>
        public TestTenantProvider? TenantProvider { get; } = tenantProvider;

        /// <summary>
        /// Convenience: the Guid used by the global query filter for this context.
        /// </summary>
        public Guid ActiveTenantId => TenantProvider?.TenantId ?? context.CurrentTenantId;

        public void Dispose()
        {
            Context?.Dispose();
            _connection?.Dispose();
        }
    }
}
