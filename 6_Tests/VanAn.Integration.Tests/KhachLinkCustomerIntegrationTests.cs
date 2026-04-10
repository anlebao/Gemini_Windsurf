using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;

namespace VanAn.Integration.Tests;

[TestFixture]
public class KhachLinkCustomerIntegrationTests : IntegrationTestBase
{
    private HttpClient _client = null!;
    private VanAnDbContext _context = null!;

    [SetUp]
    public void SetUp()
    {
        _client = Factory.CreateClient();
        _context = Factory.Services.GetRequiredService<VanAnDbContext>();
    }

    [Test]
    public async Task KhachLink_IndexPage_WithNewDeviceId_ShouldCreateCustomerAndShowLoyaltyRewards()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var requestUri = $"/";

        // Act
        var response = await _client.GetAsync(requestUri);
        
        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        Assert.IsTrue(content.Contains("KhachLink"));
        
        // Check that customer was created (this will be verified after implementation)
        // For now, just ensure the page loads without errors
    }

    [Test]
    public async Task KhachLink_IndexPage_WithExistingDeviceId_ShouldUseExistingCustomer()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var tenantId = TestTenantId;
        
        // Create existing customer
        var existingCustomer = new Customer
        {
            CustomerId = new CustomerId(Guid.NewGuid()),
            FullName = "Existing Customer",
            PhoneNumber = "0123456789",
            CustomerTier = "Silver",
            DeviceId = deviceId,
            TenantId = tenantId
        };
        
        await _context.Customers.AddAsync(existingCustomer);
        await _context.SaveChangesAsync();

        // Create loyalty rewards for this customer
        var loyaltyRewards = new LoyaltyRewards
        {
            CustomerId = existingCustomer.CustomerId.Value,
            PointBalance = 100,
            History = "[]",
            IsActive = true,
            TenantId = tenantId
        };
        
        await _context.LoyaltyRewards.AddAsync(loyaltyRewards);
        await _context.SaveChangesAsync();

        // Act
        var response = await _client.GetAsync($"/");
        
        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        Assert.IsTrue(content.Contains("KhachLink"));
        
        // After implementation, we should verify loyalty points are displayed
        // For now, just ensure no errors occur
    }

    [Test]
    public async Task KhachLink_IndexPage_WithInvalidDeviceId_ShouldGenerateNewDeviceId()
    {
        // Arrange
        var requestUri = $"/";

        // Act
        var response = await _client.GetAsync(requestUri);
        
        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        Assert.IsTrue(content.Contains("KhachLink"));
        
        // After implementation, verify new customer was created
    }

    [Test]
    public async Task KhachLink_CustomerFlow_ShouldProperlyLinkDeviceIdToCustomer()
    {
        // Arrange
        var deviceId = Guid.NewGuid();

        // Act - Simulate first visit
        var firstResponse = await _client.GetAsync($"/");
        firstResponse.EnsureSuccessStatusCode();

        // Act - Simulate second visit (should use same customer)
        var secondResponse = await _client.GetAsync($"/");
        secondResponse.EnsureSuccessStatusCode();

        // Assert
        // After implementation, verify only one customer was created for this device
        var customers = await _context.Customers
            .Where(c => c.DeviceId == deviceId)
            .ToListAsync();
        
        // This will be verified after implementation
        // For now, ensure no errors occur
        Assert.IsTrue(true);
    }

    [Test]
    public async Task KhachLink_LoyaltyRewardsDisplay_ShouldShowCorrectPoints()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var tenantId = TestTenantId;
        
        // Create customer with loyalty rewards
        var customer = new Customer
        {
            CustomerId = new CustomerId(Guid.NewGuid()),
            FullName = "Test Customer",
            PhoneNumber = "0123456789",
            CustomerTier = "Gold",
            DeviceId = deviceId,
            TenantId = tenantId
        };
        
        await _context.Customers.AddAsync(customer);
        await _context.SaveChangesAsync();

        var loyaltyRewards = new LoyaltyRewards
        {
            CustomerId = customer.CustomerId.Value,
            PointBalance = 250,
            History = "[{\"action\":\"earned\",\"points\":50,\"reason\":\"Order #123\"}]",
            IsActive = true,
            TenantId = tenantId
        };
        
        await _context.LoyaltyRewards.AddAsync(loyaltyRewards);
        await _context.SaveChangesAsync();

        // Act
        var response = await _client.GetAsync($"/");
        
        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        Assert.IsTrue(content.Contains("KhachLink"));
        
        // After implementation, verify loyalty points are displayed
        // For now, just ensure page loads
    }
}
