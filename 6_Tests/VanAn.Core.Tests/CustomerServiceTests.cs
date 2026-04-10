using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;
using VanAn.CoreHub.Tests.TestInfrastructure;
using Xunit;
using Xunit.Abstractions;

namespace VanAn.Core.Tests;

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
        var repository = new VanAn.CoreHub.Infrastructure.Repositories.CustomerRepository(Context);
        _customerService = new CustomerService(repository, _logger);
        _output.WriteLine("[CustomerServiceTests] CustomerService created successfully");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task GetOrCreateCustomerByDeviceIdAsync_WhenCustomerNotExists_CreatesNewCustomer()
    {
        var deviceId = Guid.NewGuid();

        var customer = await _customerService.GetOrCreateCustomerByDeviceIdAsync(deviceId, "Test Customer");

        Assert.NotNull(customer);
        Assert.Equal(deviceId, customer.DeviceId);
        Assert.Equal("Test Customer", customer.FullName);
    }

    [Fact]
    public async Task GetOrCreateCustomerByDeviceIdAsync_WithMultipleCalls_ReturnsSameCustomer()
    {
        var deviceId = Guid.NewGuid();

        var customer1 = await _customerService.GetOrCreateCustomerByDeviceIdAsync(deviceId);
        var customer2 = await _customerService.GetOrCreateCustomerByDeviceIdAsync(deviceId);

        Assert.Equal(customer1.CustomerId.Value, customer2.CustomerId.Value);
        Assert.Equal(1, await Context.Customers.CountAsync(c => c.DeviceId == deviceId));
    }
}
