using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using VanAn.CoreHub.Domain;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Infrastructure;
using VanAn.Integration.Tests.Infrastructure;

namespace VanAn.Integration.Tests;

/// <summary>
/// Integration tests for Lead to Customer Conversion
/// Layer 2: Integration Tests - Lead Conversion Workflow
/// </summary>
public class LeadToCustomerConversionTests : IntegrationTestBase
{
    private readonly ILeadManagementService _leadManagementService;
    private readonly ILeadConversionService _leadConversionService;
    private readonly ICustomerOnboardingService _customerOnboardingService;
    private readonly ILoyaltyRewardsService _loyaltyRewardsService;

    public LeadToCustomerConversionTests()
    {
        _leadManagementService = ServiceProvider.GetRequiredService<ILeadManagementService>();
        _leadConversionService = ServiceProvider.GetRequiredService<ILeadConversionService>();
        _customerOnboardingService = ServiceProvider.GetRequiredService<ICustomerOnboardingService>();
        _loyaltyRewardsService = ServiceProvider.GetRequiredService<ILoyaltyRewardsService>();
    }

    [Fact(DisplayName = "LeadConversion_Flow_ShouldCreateCustomerWithLoyalty")]
    public async Task LeadConversion_Flow_ShouldCreateCustomerWithLoyalty()
    {
        // Arrange - Create a qualified lead
        var lead = new Lead
        {
            FullName = "Conversion Test Customer",
            PhoneNumber = "0987654321",
            Email = "conversion@test.com",
            CompanyName = "Test Company",
            Source = LeadSource.Facebook,
            Status = LeadStatus.Qualified,
            LeadScore = 85,
            TenantId = TestTenantId
        };

        var createdLead = await _leadManagementService.CreateLeadAsync(lead);
        Assert.NotNull(createdLead);

        // Act - Convert lead to customer
        var conversionReason = "High-value Facebook lead - ready for conversion";
        var customer = await _leadConversionService.ConvertLeadToCustomerAsync(createdLead.Id, conversionReason);

        // Assert - Customer created successfully
        Assert.NotNull(customer);
        Assert.Equal("Conversion Test Customer", customer.FullName);
        Assert.Equal("0987654321", customer.PhoneNumber);
        Assert.Equal("conversion@test.com", customer.Email);
        Assert.Equal("Bronze", customer.CustomerTier);
        Assert.True(customer.IsActive);

        // Verify lead status updated
        var updatedLead = await _dbContext.Leads
            .Include(l => l.Activities)
            .FirstOrDefaultAsync(l => l.Id == createdLead.Id);

        Assert.NotNull(updatedLead);
        Assert.Equal(LeadStatus.Converted, updatedLead.Status);
        Assert.Equal(customer.Id, updatedLead.ConvertedCustomerId);
        Assert.NotNull(updatedLead.ConversionDate);
        Assert.Equal(conversionReason, updatedLead.ConversionReason);

        // Verify conversion activity logged
        Assert.Contains(updatedLead.Activities, a => a.ActivityType == LeadActivityType.Converted);

        // Verify loyalty rewards initialized
        var loyaltyRewards = await _loyaltyRewardsService.GetCustomerRewardsAsync(customer.Id);
        Assert.NotNull(loyaltyRewards);
        Assert.Equal(50, loyaltyRewards.PointBalance); // Welcome points
        Assert.Equal("Bronze", loyaltyRewards.Tier);

        // Verify onboarding started
        var onboarding = await _dbContext.CustomerOnboardings
            .FirstOrDefaultAsync(o => o.CustomerId == customer.Id);

        Assert.NotNull(onboarding);
        Assert.Equal(OnboardingStatus.InProgress, onboarding.Status);
        Assert.Equal(OnboardingStep.Welcome, onboarding.CurrentStep);
        Assert.NotNull(onboarding.StartedAt);
    }

    [Fact(DisplayName = "LeadConversion_WithOrders_ShouldImportOrderHistory")]
    public async Task LeadConversion_WithOrders_ShouldImportOrderHistory()
    {
        // Arrange - Create lead with existing orders (from previous interactions)
        var lead = new Lead
        {
            FullName = "Order History Customer",
            PhoneNumber = "0912345678",
            Email = "orders@test.com",
            Status = LeadStatus.Qualified,
            LeadScore = 90,
            TenantId = TestTenantId
        };

        var createdLead = await _leadManagementService.CreateLeadAsync(lead);

        // Create some orders associated with this lead's phone number (simulating previous orders)
        var existingOrders = new[]
        {
            new Order
            {
                OrderId = new OrderId(Guid.NewGuid()),
                CustomerDeviceId = "0912345678", // Linked by phone
                OrderType = "DINEIN",
                Status = new OrderStatusId("Completed"),
                SubTotal = 50000,
                TotalAmount = 55000,
                OrderDate = DateTime.UtcNow.AddDays(-10),
                CompletedAt = DateTime.UtcNow.AddDays(-10),
                TenantId = TestTenantId
            },
            new Order
            {
                OrderId = new OrderId(Guid.NewGuid()),
                CustomerDeviceId = "0912345678", // Linked by phone
                OrderType = "TAKEAWAY",
                Status = new OrderStatusId("Completed"),
                SubTotal = 75000,
                TotalAmount = 82500,
                OrderDate = DateTime.UtcNow.AddDays(-5),
                CompletedAt = DateTime.UtcNow.AddDays(-5),
                TenantId = TestTenantId
            }
        };

        _dbContext.Orders.AddRange(existingOrders);
        await _dbContext.SaveChangesAsync();

        // Act - Convert lead to customer
        var customer = await _leadConversionService.ConvertLeadToCustomerAsync(createdLead.Id, "Lead with order history");

        // Assert - Customer created with order history
        Assert.NotNull(customer);

        // Verify orders are now linked to the customer
        var linkedOrders = await _dbContext.Orders
            .Where(o => o.CustomerId == customer.Id)
            .ToListAsync();

        Assert.Equal(2, linkedOrders.Count);
        Assert.All(linkedOrders, order => Assert.Equal(customer.Id, order.CustomerId));

        // Verify customer's total spent and order count updated
        var customerWithOrders = await _dbContext.Customers
            .Include(c => c.Orders)
            .FirstOrDefaultAsync(c => c.Id == customer.Id);

        Assert.NotNull(customerWithOrders);
        Assert.Equal(2, customerWithOrders.Orders.Count);
        Assert.Equal(137500, customerWithOrders.TotalSpent); // 55000 + 82500
        Assert.NotNull(customerWithOrders.LastOrderDate);

        // Verify loyalty points calculated from order history
        var loyaltyRewards = await _loyaltyRewardsService.GetCustomerRewardsAsync(customer.Id);
        Assert.NotNull(loyaltyRewards);
        Assert.True(loyaltyRewards.PointBalance > 50); // Should have points from orders + welcome
    }

    [Fact(DisplayName = "LeadConversion_Failed_ShouldRollbackChanges")]
    public async Task LeadConversion_Failed_ShouldRollbackChanges()
    {
        // Arrange - Create lead
        var lead = new Lead
        {
            FullName = "Rollback Test Customer",
            PhoneNumber = "0998765432",
            Email = "rollback@test.com",
            Status = LeadStatus.Qualified,
            LeadScore = 80,
            TenantId = TestTenantId
        };

        var createdLead = await _leadManagementService.CreateLeadAsync(lead);

        // Simulate a scenario where conversion should fail (e.g., database constraint)
        // We'll create a customer with the same phone number first to cause conflict
        var conflictingCustomer = new Customer
        {
            FullName = "Conflicting Customer",
            PhoneNumber = "0998765432", // Same phone number
            TenantId = TestTenantId
        };
        _dbContext.Customers.Add(conflictingCustomer);
        await _dbContext.SaveChangesAsync();

        // Act & Assert - Conversion should fail
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _leadConversionService.ConvertLeadToCustomerAsync(createdLead.Id, "Test conversion"));

        Assert.Contains("already exists", exception.Message);

        // Verify rollback - lead should remain in original state
        var leadAfterFailedConversion = await _dbContext.Leads
            .FirstOrDefaultAsync(l => l.Id == createdLead.Id);

        Assert.NotNull(leadAfterFailedConversion);
        Assert.Equal(LeadStatus.Qualified, leadAfterFailedConversion.Status); // Should not be converted
        Assert.Null(leadAfterFailedConversion.ConvertedCustomerId);
        Assert.Null(leadAfterFailedConversion.ConversionDate);

        // Verify no new customer was created
        var newCustomers = await _dbContext.Customers
            .Where(c => c.FullName == "Rollback Test Customer")
            .ToListAsync();

        Assert.Empty(newCustomers);

        // Verify no onboarding was started
        var onboardings = await _dbContext.CustomerOnboardings
            .Where(o => o.CustomerId == createdLead.Id)
            .ToListAsync();

        Assert.Empty(onboardings);
    }

    [Fact(DisplayName = "LeadConversion_Batch_ShouldProcessMultipleLeads")]
    public async Task LeadConversion_Batch_ShouldProcessMultipleLeads()
    {
        // Arrange - Create multiple qualified leads
        var leads = new[]
        {
            new Lead
            {
                FullName = "Batch Customer 1",
                PhoneNumber = "0911111111",
                Email = "batch1@test.com",
                Status = LeadStatus.Qualified,
                LeadScore = 85,
                TenantId = TestTenantId
            },
            new Lead
            {
                FullName = "Batch Customer 2",
                PhoneNumber = "0922222222",
                Email = "batch2@test.com",
                Status = LeadStatus.Qualified,
                LeadScore = 90,
                TenantId = TestTenantId
            },
            new Lead
            {
                FullName = "Batch Customer 3",
                PhoneNumber = "0933333333",
                Email = "batch3@test.com",
                Status = LeadStatus.Qualified,
                LeadScore = 80,
                TenantId = TestTenantId
            }
        };

        var createdLeads = new List<Lead>();
        foreach (var lead in leads)
        {
            createdLeads.Add(await _leadManagementService.CreateLeadAsync(lead));
        }

        // Act - Convert all leads in batch
        var conversionResults = new List<Customer>();
        var conversionStartTime = DateTime.UtcNow;

        foreach (var lead in createdLeads)
        {
            var customer = await _leadConversionService.ConvertLeadToCustomerAsync(lead.Id, "Batch conversion");
            conversionResults.Add(customer);
        }

        var conversionEndTime = DateTime.UtcNow;
        var batchProcessingTime = conversionEndTime - conversionStartTime;

        // Assert - All conversions successful
        Assert.Equal(3, conversionResults.Count);
        Assert.All(conversionResults, c => Assert.NotNull(c));

        // Verify all customers created
        var createdCustomers = await _dbContext.Customers
            .Where(c => c.FullName.StartsWith("Batch Customer"))
            .ToListAsync();

        Assert.Equal(3, createdCustomers.Count);

        // Verify all leads converted
        var convertedLeads = await _dbContext.Leads
            .Where(l => l.Status == LeadStatus.Converted && l.FullName.StartsWith("Batch Customer"))
            .ToListAsync();

        Assert.Equal(3, convertedLeads.Count);

        // Verify all onboardings started
        var onboardings = await _dbContext.CustomerOnboardings
            .Where(o => conversionResults.Select(c => c.Id).Contains(o.CustomerId))
            .ToListAsync();

        Assert.Equal(3, onboardings.Count);

        // Verify batch processing efficiency
        Assert.True(batchProcessingTime.TotalSeconds < 5); // Should process quickly

        // Verify all loyalty rewards initialized
        var loyaltyRewardsList = new List<LoyaltyRewards>();
        foreach (var customer in conversionResults)
        {
            var rewards = await _loyaltyRewardsService.GetCustomerRewardsAsync(customer.Id);
            Assert.NotNull(rewards);
            Assert.Equal(50, rewards.PointBalance);
            loyaltyRewardsList.Add(rewards);
        }

        Assert.Equal(3, loyaltyRewardsList.Count);
    }

    [Fact(DisplayName = "LeadConversion_ValidateLead_ShouldCheckQualification")]
    public async Task LeadConversion_ValidateLead_ShouldCheckQualification()
    {
        // Arrange - Create unqualified lead
        var unqualifiedLead = new Lead
        {
            FullName = "Unqualified Customer",
            PhoneNumber = "0900000000",
            Email = "unqualified@test.com",
            Status = LeadStatus.New, // Not qualified yet
            LeadScore = 50, // Low score
            TenantId = TestTenantId
        };

        var createdLead = await _leadManagementService.CreateLeadAsync(unqualifiedLead);

        // Act - Try to convert unqualified lead
        var isValid = await _leadConversionService.ValidateLeadForConversionAsync(createdLead.Id);

        // Assert - Should not be valid for conversion
        Assert.False(isValid);

        // Arrange - Qualify the lead
        await _leadManagementService.UpdateLeadStatusAsync(createdLead.Id, LeadStatus.Qualified);
        createdLead.LeadScore = 85; // Update score
        await _dbContext.SaveChangesAsync();

        // Act - Try to convert qualified lead
        isValid = await _leadConversionService.ValidateLeadForConversionAsync(createdLead.Id);

        // Assert - Should be valid for conversion
        Assert.True(isValid);
    }
}

// Supporting interfaces and classes
public interface ILoyaltyRewardsService
{
    Task<LoyaltyRewards?> GetCustomerRewardsAsync(Guid customerId);
    Task InitializeCustomerRewardsAsync(Guid customerId, int welcomePoints);
}

public class LoyaltyRewards
{
    public Guid CustomerId { get; set; }
    public int PointBalance { get; set; }
    public string Tier { get; set; } = "Bronze";
    public DateTime LastUpdated { get; set; }
}

// Mock implementations
public class LoyaltyRewardsService : ILoyaltyRewardsService
{
    private readonly VanAnDbContext _context;

    public LoyaltyRewardsService(VanAnDbContext context)
    {
        _context = context;
    }

    public async Task<LoyaltyRewards?> GetCustomerRewardsAsync(Guid customerId)
    {
        // In real implementation, this would query from LoyaltyRewards table
        // For testing, we'll return mock data
        return await Task.FromResult(new LoyaltyRewards
        {
            CustomerId = customerId,
            PointBalance = 50,
            Tier = "Bronze",
            LastUpdated = DateTime.UtcNow
        });
    }

    public async Task InitializeCustomerRewardsAsync(Guid customerId, int welcomePoints)
    {
        // In real implementation, this would create LoyaltyRewards record
        await Task.CompletedTask;
    }
}
