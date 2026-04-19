using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Tests.TestInfrastructure;

namespace VanAn.CoreHub.Tests.TestInfrastructure
{
    public abstract class IntegrationTestBase : IDisposable
    {
        protected VanAnDbContext Context { get; private set; } = null!;
        protected ITestDbProvider DbProvider { get; private set; }
        protected ILogger Logger { get; private set; }
        protected SchemaSyncEngine SchemaEngine { get; private set; }

        protected IntegrationTestBase(ITestDbProvider dbProvider = null!, ILogger logger = null!)
        {
            DbProvider = dbProvider ?? TestDbProviderFactory.CreateSqlite();
            Logger = logger;
            SchemaEngine = new SchemaSyncEngine(logger as ILogger<SchemaSyncEngine> ?? new NullLogger<SchemaSyncEngine>());
        }

        protected async Task CreateContextAsync()
        {
            var testContext = TestDbFactory.CreateSqliteInMemory();
            
            // For backward compatibility, assign to Context property
            // Note: This breaks strict typing but allows existing tests to work
            Context = testContext;
            
            await testContext.Database.EnsureCreatedAsync();
        }

        protected async Task SeedTestDataAsync(TestDataBuilder builder = null!)
        {
            await Context.SeedTestDataAsync(builder);
        }

        protected async Task ResetDatabaseAsync()
        {
            await SchemaEngine.ResetAndRecreateAsync(Context);
        }

        public virtual void Dispose()
        {
            DbProvider?.Dispose(Context);
            Context = null!;
        }

        // Helper methods for common test scenarios
        protected async Task SetupBasicTestDataAsync()
        {
            // Context should already be created, just seed the data
            await SeedTestDataAsync(TestDataBuilder.CreateBasicScenario());
        }

        protected async Task SetupLargeTestDataAsync()
        {
            await CreateContextAsync();
            await SeedTestDataAsync(TestDataBuilder.CreateLargeScenario());
        }

        protected async Task SetupEmptyDatabaseAsync()
        {
            await CreateContextAsync();
            await SeedTestDataAsync(TestDataBuilder.CreateEmptyScenario());
        }

        // Legacy method for backward compatibility
        protected virtual async Task SetupAsync()
        {
            await SetupBasicTestDataAsync();
        }
    }
}
