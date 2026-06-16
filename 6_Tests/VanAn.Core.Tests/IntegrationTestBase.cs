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

            _ = await Context.Database.EnsureCreatedAsync();
        }

        protected async Task SeedTestDataAsync(TestDataBuilder builder = null!)
        {
            _ = await Context.SeedTestDataAsync(builder);
        }

        protected async Task ResetDatabaseAsync()
        {
            _ = await SchemaEngine.ResetAndRecreateAsync(Context);
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
            await SeedTestDataAsync(TestDataBuilder.CreateBasicScenario(Context.CurrentTenantId));
        }

        protected async Task SetupLargeTestDataAsync()
        {
            await CreateContextAsync();
            await SeedTestDataAsync(TestDataBuilder.CreateLargeScenario(Context.CurrentTenantId));
        }

        protected async Task SetupEmptyDatabaseAsync()
        {
            await CreateContextAsync();
            await SeedTestDataAsync(TestDataBuilder.CreateEmptyScenario());
        }

        /// <summary>
        /// Gets the active Tenant ID from the test context's tenant provider.
        /// Use this as the shopId in kitchen tests so data seeded with this tenant
        /// is visible through the global multi-tenancy query filter.
        /// </summary>
        protected Guid ActiveTenantId => ContextScope?.ActiveTenantId ?? Guid.Empty;

        /// <summary>
        /// Changes the active tenant for this test context.
        /// Data must be seeded AFTER calling this for the global filter to include it.
        /// </summary>
        protected void SetActiveTenant(Guid tenantId)
        {
            ContextScope?.TenantProvider?.SetTenant(tenantId);
        }

        // Legacy method for backward compatibility
        protected virtual async Task SetupAsync()
        {
            await SetupBasicTestDataAsync();
        }
    }
}
