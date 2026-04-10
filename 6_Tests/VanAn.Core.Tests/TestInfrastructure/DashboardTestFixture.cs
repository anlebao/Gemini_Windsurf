using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Tests.TestInfrastructure;
using Xunit.Abstractions;

namespace VanAn.Core.Tests.TestInfrastructure
{
    public class DashboardTestFixture : IDisposable
    {
        public VanAnDbContext Context { get; private set; }
        public DashboardService Service { get; private set; }
        public Mock<IConfiguration> ConfigMock { get; private set; }

        public DashboardTestFixture()
        {
            // Initialize context using Test Harness 4 layer
            var provider = TestDbProviderFactory.CreateSqlite();
            Context = provider.Create();
            
            // Setup database schema using Test Harness extensions
            Context.SetupTestDatabaseAsync().Wait();
            
            // Setup test data using Test Harness extensions
            Context.SeedTestDataAsync(TestDataBuilder.CreateBasicScenario()).Wait();
            
            // Create mocks
            ConfigMock = new Mock<IConfiguration>();
            ConfigMock.Setup(c => c["KhachLink:DatabasePath"]).Returns("test-data");
            ConfigMock.Setup(c => c["ShopERP:DatabasePath"]).Returns("test-data");
            
            // Create service
            var loggerMock = new Mock<ILogger<DashboardService>>();
            Service = new DashboardService(Context, loggerMock.Object, ConfigMock.Object);
        }

        public void Dispose()
        {
            Context?.Dispose();
        }
    }
}
