using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using VanAn.CoreHub.Infrastructure;

namespace VanAn.CoreHub.Tests.TestInfrastructure
{
    /// <summary>
    /// Base class for integration tests using TestContextScope wrapper.
    /// FIX: Uses TestContextScope to bind DI scope lifespan to DbContext lifespan
    /// </summary>
    public abstract class IntegrationTestBase : IDisposable
    {
        protected TestContextScope ContextScope { get; private set; } = null!;
        protected VanAnDbContext Context => ContextScope?.Context ?? throw new InvalidOperationException("Context not initialized. Call CreateContextAsync first.");
        protected ILogger Logger { get; private set; }
        protected SchemaSyncEngine SchemaEngine { get; private set; }

        protected IntegrationTestBase(ILogger logger = null!)
        {
            Logger = logger;
            SchemaEngine = new SchemaSyncEngine(logger as ILogger<SchemaSyncEngine> ?? new NullLogger<SchemaSyncEngine>());
        }

        protected async Task CreateContextAsync()
        {
            // FIX: Use TestContextScope wrapper to bind DI scope lifespan to context
            ContextScope = VanAnDbContextTestFactory.Create();

            await Context.Database.EnsureCreatedAsync();
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
            // Dispose context scope (which disposes both context and DI scope)
            ContextScope?.Dispose();
            ContextScope = null!;

            GC.SuppressFinalize(this);
        }

        // Helper methods for common test scenarios
        protected async Task SetupBasicTestDataAsync()
        {
            await CreateContextAsync();
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
