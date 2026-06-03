using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Tests.TestInfrastructure;
using Xunit;
using Xunit.Abstractions;

namespace VanAn.Core.Tests
{
    public class CustomerServiceTests : IntegrationTestBase, IAsyncLifetime
    {
        private CustomerService _customerService = null!;
        private readonly ILogger<CustomerService> _logger;
        private readonly ITestOutputHelper _output;

        public CustomerServiceTests(ITestOutputHelper output)
        {
            _output = output;
            _logger = new Logger<CustomerService>(new LoggerFactory());
            _output.WriteLine("[CustomerServiceTests] Test class initialized");
        }

        public async Task InitializeAsync()
        {
            await SetupEmptyDatabaseAsync();

            // Create repository for testing
            CoreHub.Infrastructure.Repositories.CustomerRepository repository = new(Context);
            _customerService = new CustomerService(repository, _logger);
            _output.WriteLine("[CustomerServiceTests] CustomerService created successfully");
        }

        public Task DisposeAsync()
        {
            return Task.CompletedTask;
        }

        [Fact]
        public async Task GetOrCreateCustomerByDeviceIdAsync_WhenCustomerNotExists_CreatesNewCustomer()
        {
            Guid deviceId = Guid.NewGuid();

            Shared.Domain.Customer customer = await _customerService.GetOrCreateCustomerByDeviceIdAsync(deviceId, "Test Customer");

            Assert.NotNull(customer);
            Assert.Equal(deviceId, customer.DeviceId);
            Assert.Equal("Test Customer", customer.FullName);
        }

        [Fact]
        public async Task GetOrCreateCustomerByDeviceIdAsync_WithMultipleCalls_ReturnsSameCustomer()
        {
            Guid deviceId = Guid.NewGuid();

            Shared.Domain.Customer customer1 = await _customerService.GetOrCreateCustomerByDeviceIdAsync(deviceId);
            Shared.Domain.Customer customer2 = await _customerService.GetOrCreateCustomerByDeviceIdAsync(deviceId);

            Assert.Equal(customer1.CustomerId.Value, customer2.CustomerId.Value);
            Assert.Equal(1, await Context.Customers.CountAsync(c => c.DeviceId == deviceId));
        }
    }
}
