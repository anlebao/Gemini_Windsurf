using Microsoft.Data.Sqlite;
using VanAn.CoreHub.Infrastructure;

namespace VanAn.CoreHub.Tests.TestInfrastructure;

/// <summary>
/// Wrapper class to bind SQLite connection lifespan to DbContext lifespan.
/// Ensures proper disposal of both context and connection.
/// NO DI - direct instantiation only.
/// </summary>
public sealed class TestContextScope : IDisposable
{
    private readonly SqliteConnection? _connection;
    public VanAnDbContext Context { get; }

    public TestContextScope(VanAnDbContext context, SqliteConnection? connection = null)
    {
        Context = context;
        _connection = connection;
    }

    public void Dispose()
    {
        Context?.Dispose();
        _connection?.Dispose();
    }
}
