using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Infrastructure.Repositories;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Tests.TestInfrastructure;

namespace VanAn.Core.Tests.TestInfrastructure
{
    public class DashboardTestFixture : IDisposable
    {
        public TestContextScope ContextScope { get; private set; } = null!;
        public VanAnDbContext Context => ContextScope?.Context!;
        public DashboardService Service { get; private set; }
        public Mock<IConfiguration> ConfigMock { get; private set; }

        public DashboardTestFixture()
        {
            // FIX: Use TestContextScope wrapper to bind DI scope lifespan to context
            ContextScope = VanAnDbContextTestFactory.Create();

            // Setup database schema using Test Harness extensions
            Context.SetupTestDatabaseAsync().Wait();

            // Setup test data using Test Harness extensions
            Context.SeedTestDataAsync(TestDataBuilder.CreateBasicScenario()).Wait();

            // Create mocks
            ConfigMock = new Mock<IConfiguration>();
            _ = ConfigMock.Setup(c => c["KhachLink:DatabasePath"]).Returns("test-data");
            _ = ConfigMock.Setup(c => c["ShopERP:DatabasePath"]).Returns("test-data");

            // Create service
            Mock<ILogger<DashboardService>> loggerMock = new();
            SystemMetricsRepository systemMetricsRepo = new(Context);
            Service = new DashboardService(systemMetricsRepo, loggerMock.Object, ConfigMock.Object);
        }

        public void Dispose()
        {
            ContextScope?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
