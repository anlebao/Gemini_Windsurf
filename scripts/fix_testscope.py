filepath = r'C:\VibeCoding\Gemini_Windsurf\6_Tests\VanAn.Core.Tests\TestInfrastructure\TestContextScope.cs'
with open(filepath, 'rb') as f:
    raw = f.read()
content = raw.decode('utf-8-sig')

new_content = (
    'using Microsoft.Data.Sqlite;\r\n'
    'using VanAn.CoreHub.Infrastructure;\r\n'
    '\r\n'
    'namespace VanAn.CoreHub.Tests.TestInfrastructure\r\n'
    '{\r\n'
    '    /// <summary>\r\n'
    '    /// Wrapper class to bind SQLite connection lifespan to DbContext lifespan.\r\n'
    '    /// Ensures proper disposal of both context and connection.\r\n'
    '    /// NO DI - direct instantiation only.\r\n'
    '    /// </summary>\r\n'
    '    public sealed class TestContextScope(VanAnDbContext context, SqliteConnection? connection = null, TestTenantProvider? tenantProvider = null) : IDisposable\r\n'
    '    {\r\n'
    '        private readonly SqliteConnection? _connection = connection;\r\n'
    '        public VanAnDbContext Context { get; } = context;\r\n'
    '\r\n'
    '        /// <summary>\r\n'
    '        /// The TestTenantProvider used by this context - allows tests to read\r\n'
    '        /// or update the active tenant after seeding data.\r\n'
    '        /// </summary>\r\n'
    '        public TestTenantProvider? TenantProvider { get; } = tenantProvider;\r\n'
    '\r\n'
    '        /// <summary>\r\n'
    '        /// Convenience: the Guid used by the global query filter for this context.\r\n'
    '        /// </summary>\r\n'
    '        public Guid ActiveTenantId => TenantProvider?.TenantId ?? context.CurrentTenantId;\r\n'
    '\r\n'
    '        public void Dispose()\r\n'
    '        {\r\n'
    '            Context?.Dispose();\r\n'
    '            _connection?.Dispose();\r\n'
    '        }\r\n'
    '    }\r\n'
    '}\r\n'
)

with open(filepath, 'wb') as f:
    f.write(new_content.encode('utf-8'))
print('Done')
