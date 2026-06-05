using Xunit;
using Xunit.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Json;
using System.Text.Json;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Common;
using VanAn.CoreHub.Infrastructure;
using VanAn.Integration.Tests.Infrastructure;
using VanAn.KhachLink;
using DomainLoyaltyRewards = VanAn.Shared.Domain.LoyaltyRewards;

namespace VanAn.Integration.Tests.Api;

/// <summary>
/// Customer API Integration Tests - Tests business behavior through HTTP endpoints
/// Includes ITestOutputHelper for debugging
/// </summary>
[Trait("Category", "Integration")]
public class CustomerApiIntegrationTests : HttpIntegrationTestBase, IClassFixture<CustomWebApplicationFactory>
{
    private readonly new VanAnDbContext _dbContext;

    public CustomerApiIntegrationTests(CustomWebApplicationFactory factory, ITestOutputHelper output)
        : base(factory, output)
    {
        var scope = _factory.Services.CreateScope();
        _dbContext = scope.ServiceProvider.GetRequiredService<VanAnDbContext>();
        _dbContext.Database.EnsureCreated();
    }

    [Fact(DisplayName = "API: Create Customer - Valid Request")]
    public async Task CreateCustomer_ValidRequest_ShouldReturnSuccess()
    {
        // Arrange
        var testTenantId = TestEntityBuilder.CreateTenantId();
        var customerRequest = new
        {
            TenantId = testTenantId.Value,
            FullName = "John Doe",
            PhoneNumber = "1234567890",
            Email = "john.doe@example.com",
            CustomerTier = "Regular"
        };

        // Act
        var response = await PostAndParseAsync<Dictionary<string, object>>("/api/customers", customerRequest);

        // Assert
        Assert.NotNull(response);
        Assert.True(response.ContainsKey("customerId"));
        Assert.True(response.ContainsKey("fullName"));
        
        var customerId = Guid.Parse(response["customerId"].ToString());
        var fullName = response["fullName"].ToString();
        
        Assert.Equal("John Doe", fullName);
        
        // Verify customer was saved in database
        var savedCustomer = await _dbContext.Customers.FindAsync(customerId);
        Assert.NotNull(savedCustomer);
        Assert.Equal(testTenantId.Value, savedCustomer.TenantId.Value);
        Assert.Equal("John Doe", savedCustomer.FullName);
        Assert.Equal("1234567890", savedCustomer.PhoneNumber);
        Assert.Equal("john.doe@example.com", savedCustomer.Email);
        
        _output.WriteLine($"Created Customer: {customerId} - {fullName}");
    }

    [Fact(DisplayName = "API: Get Customer by ID - Valid Request")]
    public async Task GetCustomerById_ValidRequest_ShouldReturnCustomer()
    {
        // Arrange
        var testTenantId = TestEntityBuilder.CreateTenantId();
        var customer = TestEntityBuilder.CreateCustomer(testTenantId, "Jane Smith", "9876543210", "jane.smith@example.com");
        
        _dbContext.Customers.Add(customer);
        await _dbContext.SaveChangesAsync();

        // Act
        var response = await GetAndParseAsync<Dictionary<string, object>>($"/api/customers/{customer.CustomerId.Value}");

        // Assert
        Assert.NotNull(response);
        Assert.Equal(customer.CustomerId.Value.ToString(), response["id"].ToString());
        Assert.Equal("Jane Smith", response["fullName"].ToString());
        Assert.Equal("9876543210", response["phoneNumber"].ToString());
        Assert.Equal("jane.smith@example.com", response["email"].ToString());
    }

    [Fact(DisplayName = "API: Update Customer Details - Valid Request")]
    public async Task UpdateCustomerDetails_ValidRequest_ShouldReturnSuccess()
    {
        // Arrange
        var testTenantId = TestEntityBuilder.CreateTenantId();
        var customer = TestEntityBuilder.CreateCustomer(testTenantId, "Bob Johnson", "5551234567", "bob@example.com");
        
        _dbContext.Customers.Add(customer);
        await _dbContext.SaveChangesAsync();

        var updateRequest = new
        {
            FullName = "Robert Johnson",
            PhoneNumber = "5559876543",
            Email = "robert.johnson@example.com",
            CustomerTier = "Premium"
        };

        // Act
        var response = await PutAndParseAsync<Dictionary<string, object>>($"/api/customers/{customer.CustomerId.Value}", updateRequest);

        // Assert
        Assert.NotNull(response);
        Assert.True(response.ContainsKey("success"));
        Assert.True(bool.Parse(response["success"].ToString()));
        
        // Verify customer was updated
        var updatedCustomer = await _dbContext.Customers.FindAsync(customer.CustomerId.Value);
        Assert.NotNull(updatedCustomer);
        Assert.Equal("Robert Johnson", updatedCustomer.FullName);
        Assert.Equal("5559876543", updatedCustomer.PhoneNumber);
        Assert.Equal("robert.johnson@example.com", updatedCustomer.Email);
        
        _output.WriteLine($"Updated Customer: {customer.CustomerId.Value} - Robert Johnson");
    }

    [Fact(DisplayName = "API: Customer Loyalty Rewards - Valid Request")]
    public async Task CustomerLoyaltyRewards_ValidRequest_ShouldReturnRewards()
    {
        // Arrange
        var testTenantId = TestEntityBuilder.CreateTenantId();
        var customer = TestEntityBuilder.CreateCustomer(testTenantId, "Alice Brown", "1112223333", "alice@example.com");
        var rewards = TestEntityBuilder.CreateLoyaltyRewards(testTenantId, customer.CustomerId, 150);
        
        _dbContext.Customers.Add(customer);
        _dbContext.LoyaltyRewards.Add(rewards);
        await _dbContext.SaveChangesAsync();

        // Act
        var response = await GetAndParseAsync<Dictionary<string, object>>($"/api/customers/{customer.CustomerId.Value}/rewards");

        // Assert
        Assert.NotNull(response);
        Assert.True(response.ContainsKey("pointBalance"));
        Assert.True(response.ContainsKey("customerId"));
        
        var pointBalance = int.Parse(response["pointBalance"].ToString());
        var customerId = Guid.Parse(response["customerId"].ToString());
        
        Assert.Equal(150, pointBalance);
        Assert.Equal(customer.CustomerId.Value, customerId);
        
        _output.WriteLine($"Customer Rewards: {customerId} - {pointBalance} points");
    }

    [Fact(DisplayName = "API: Add Loyalty Points - Valid Request")]
    public async Task AddLoyaltyPoints_ValidRequest_ShouldReturnSuccess()
    {
        // Arrange
        var testTenantId = TestEntityBuilder.CreateTenantId();
        var customer = TestEntityBuilder.CreateCustomer(testTenantId, "Charlie Wilson", "4445556666", "charlie@example.com");
        var rewards = TestEntityBuilder.CreateLoyaltyRewards(testTenantId, customer.CustomerId, 100);
        
        _dbContext.Customers.Add(customer);
        _dbContext.LoyaltyRewards.Add(rewards);
        await _dbContext.SaveChangesAsync();

        var addPointsRequest = new
        {
            Points = 50,
            Reason = "Test purchase",
            TransactionId = Guid.NewGuid()
        };

        // Act
        var response = await PostAndParseAsync<Dictionary<string, object>>($"/api/customers/{customer.CustomerId.Value}/rewards/add", addPointsRequest);

        // Assert
        Assert.NotNull(response);
        Assert.True(response.ContainsKey("newBalance"));
        Assert.True(response.ContainsKey("pointsAdded"));
        
        var newBalance = int.Parse(response["newBalance"].ToString());
        var pointsAdded = int.Parse(response["pointsAdded"].ToString());
        
        Assert.Equal(50, pointsAdded);
        Assert.Equal(150, newBalance); // 100 + 50
        
        // Verify rewards were updated
        var updatedRewards = await _dbContext.LoyaltyRewards.FindAsync(((BaseEntity)rewards).Id);
        Assert.NotNull(updatedRewards);
        Assert.Equal(150, updatedRewards.PointBalance);
        
        _output.WriteLine($"Added Points: {customer.CustomerId.Value} - +{pointsAdded} = {newBalance}");
    }

    [Fact(DisplayName = "API: Multi-Tenant Customer Isolation")]
    public async Task MultiTenant_CustomerIsolation_ShouldWork()
    {
        // Arrange
        var tenant1Id = TestEntityBuilder.CreateTenantId();
        var tenant2Id = TestEntityBuilder.CreateTenantId();
        
        var customer1 = TestEntityBuilder.CreateCustomer(tenant1Id, "Tenant1 Customer", "1111111111", "tenant1@example.com");
        var customer2 = TestEntityBuilder.CreateCustomer(tenant2Id, "Tenant2 Customer", "2222222222", "tenant2@example.com");
        
        _dbContext.Customers.AddRange(customer1, customer2);
        await _dbContext.SaveChangesAsync();

        // Act - Get customers for tenant 1
        var response = await GetAndParseAsync<List<Dictionary<string, object>>>($"/api/customers?tenantId={tenant1Id.Value}");

        // Assert
        Assert.NotNull(response);
        Assert.Single(response);
        Assert.Equal(customer1.CustomerId.Value.ToString(), response[0]["id"].ToString());
        Assert.Equal("Tenant1 Customer", response[0]["fullName"].ToString());
        
        // Verify tenant isolation
        Assert.NotEqual(customer2.CustomerId.Value.ToString(), response[0]["id"].ToString());
        
        _output.WriteLine($"Tenant1 Customers: {response.Count} found");
    }

    [Fact(DisplayName = "API: Delete Customer - Valid Request")]
    public async Task DeleteCustomer_ValidRequest_ShouldReturnSuccess()
    {
        // Arrange
        var testTenantId = TestEntityBuilder.CreateTenantId();
        var customer = TestEntityBuilder.CreateCustomer(testTenantId, "David Lee", "7778889999", "david@example.com");
        
        _dbContext.Customers.Add(customer);
        await _dbContext.SaveChangesAsync();

        // Act
        var success = await DeleteAsync($"/api/customers/{customer.CustomerId.Value}");

        // Assert
        Assert.True(success);
        
        // Verify customer was soft deleted (if using soft delete) or actually deleted
        var deletedCustomer = await _dbContext.Customers.FindAsync(customer.CustomerId.Value);
        if (deletedCustomer != null)
        {
            // If soft delete, check IsActive flag or similar
            Assert.True(false, "Customer should be deleted but still exists");
        }
        
        _output.WriteLine($"Deleted Customer: {customer.CustomerId.Value}");
    }

    public new void Dispose()
    {
        _dbContext?.Dispose();
        base.Dispose();
    }
}
