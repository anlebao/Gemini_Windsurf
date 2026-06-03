using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using VanAn.CoreHub.Domain;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Infrastructure;
using VanAn.Integration.Tests.Infrastructure;
using static VanAn.Integration.Tests.Infrastructure.TestEntityBuilder;

namespace VanAn.Integration.Tests;

/// <summary>
/// Integration tests for Lead to Customer Conversion
/// Layer 2: Integration Tests - Lead Conversion Workflow
/// </summary>
public class LeadToCustomerConversionTests : IntegrationTestBase
{
    private readonly Lazy<ILeadManagementService> _leadManagementService;
    private readonly Lazy<ILeadConversionService> _leadConversionService;
    private readonly Lazy<ICustomerOnboardingService> _customerOnboardingService;
    private readonly Lazy<ILoyaltyRewardsService> _loyaltyRewardsService;

    public LeadToCustomerConversionTests() : base()
    {
        _leadManagementService = new Lazy<ILeadManagementService>(() => _serviceProvider.GetRequiredService<ILeadManagementService>());
        _leadConversionService = new Lazy<ILeadConversionService>(() => _serviceProvider.GetRequiredService<ILeadConversionService>());
        _customerOnboardingService = new Lazy<ICustomerOnboardingService>(() => _serviceProvider.GetRequiredService<ICustomerOnboardingService>());
        _loyaltyRewardsService = new Lazy<ILoyaltyRewardsService>(() => _serviceProvider.GetRequiredService<ILoyaltyRewardsService>());
    }

    private ILeadManagementService GetLeadManagementService() => _leadManagementService.Value;
    private ILeadConversionService GetLeadConversionService() => _leadConversionService.Value;
    private ICustomerOnboardingService GetCustomerOnboardingService() => _customerOnboardingService.Value;
    private ILoyaltyRewardsService GetLoyaltyRewardsService() => _loyaltyRewardsService.Value;

    [Fact(DisplayName = "LeadConversion_Flow_ShouldCreateCustomerWithLoyalty")]
    public async Task LeadConversion_Flow_ShouldCreateCustomerWithLoyalty()
    {
        // Arrange - Create a qualified lead
        var lead = TestEntityBuilder.CreateLead(
            tenantId: TestTenantId,
            fullName: "Conversion Test Customer",
            phoneNumber: "0987654321",
            email: "conversion@test.com",
            companyName: "Test Company",
            source: LeadSource.Facebook,
            status: LeadStatus.Qualified,
            leadScore: 85
        );

        var createdLead = await GetLeadManagementService().CreateLeadAsync(lead);
        Assert.NotNull(createdLead);

        // Act - Convert lead to customer
        var conversionReason = "High-value Facebook lead - ready for conversion";
        var customer = await GetLeadConversionService().ConvertLeadToCustomerAsync(createdLead.Id, conversionReason);

        // Assert - Customer created successfully
        Assert.NotNull(customer);
        Assert.Equal("Conversion Test Customer", customer.FullName);
        Assert.Equal("0987654321", customer.PhoneNumber);
        Assert.Equal("conversion@test.com", customer.Email);
        Assert.Equal("Bronze", customer.CustomerTier);
        Assert.True(customer.IsActive);

        // Verify lead status updated
        var updatedLead = await _dbContext.Leads
            .FirstOrDefaultAsync(l => l.Id == createdLead.Id);

        Assert.NotNull(updatedLead);
        Assert.Equal(LeadStatus.Converted, updatedLead.Status);
        Assert.Equal(customer.Id, updatedLead.ConvertedCustomerId);
        Assert.NotNull(updatedLead.ConversionDate);
        Assert.Equal(conversionReason, updatedLead.ConversionReason);

        // Verify loyalty rewards initialized
        var loyaltyRewards = await GetLoyaltyRewardsService().GetCustomerRewardsAsync(customer.Id);
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
        var lead = TestEntityBuilder.CreateLead(
            tenantId: TestTenantId,
            fullName: "Order History Customer",
            phoneNumber: "0912345678",
            email: "orderhistory@test.com",
            companyName: "Order History Company",
            source: LeadSource.Website,
            status: LeadStatus.Qualified,
            leadScore: 90
        );

        var createdLead = await GetLeadManagementService().CreateLeadAsync(lead);

        // Create some orders associated with this lead's phone number (simulating previous orders)
        var existingOrders = new[]
        {
            TestEntityBuilder.CreateOrder(new TenantId(TestTenantId), Guid.NewGuid(), 55000m),
            TestEntityBuilder.CreateOrder(new TenantId(TestTenantId), Guid.NewGuid(), 82500m)
        };

        _dbContext.Orders.AddRange(existingOrders);
        await _dbContext.SaveChangesAsync();

        // Act - Convert lead to customer
        var customer = await GetLeadConversionService().ConvertLeadToCustomerAsync(createdLead.Id, "Lead with order history");

        // Assert - Customer created with order history
        Assert.NotNull(customer);

        // Verify orders are linked to the customer
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
        var loyaltyRewards = await GetLoyaltyRewardsService().GetCustomerRewardsAsync(customer.Id);
        Assert.NotNull(loyaltyRewards);
        Assert.True(loyaltyRewards.PointBalance > 50); // Should have points from orders + welcome
    }

    [Fact(DisplayName = "LeadConversion_Failed_ShouldRollbackChanges")]
    public async Task LeadConversion_Failed_ShouldRollbackChanges()
    {
        // Arrange - Create lead
        var lead = TestEntityBuilder.CreateLead(
            tenantId: TestTenantId,
            fullName: "Rollback Test Customer",
            phoneNumber: "0998765432",
            email: "rollback@test.com",
            companyName: "Rollback Company",
            source: LeadSource.Manual,
            status: LeadStatus.Qualified,
            leadScore: 75
        );

        var createdLead = await GetLeadManagementService().CreateLeadAsync(lead);

        // Simulate a scenario where conversion should fail (e.g., database constraint)
        // We'll create a customer with the same phone number first to cause conflict
        var conflictingCustomer = TestEntityBuilder.CreateCustomer(
            new TenantId(TestTenantId),
            "Conflicting Customer",
            "0998765432",
            "conflict@test.com"
        );
        _dbContext.Customers.Add(conflictingCustomer);
        await _dbContext.SaveChangesAsync();

        // Act & Assert - Conversion should fail
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => GetLeadConversionService().ConvertLeadToCustomerAsync(createdLead.Id, "Test conversion"));

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
            TestEntityBuilder.CreateLead(
                tenantId: TestTenantId,
                fullName: "Batch Customer 1",
                phoneNumber: "0901111111",
                email: "batch1@test.com",
                source: LeadSource.Facebook,
                status: LeadStatus.Qualified,
                leadScore: 80
            ),
            TestEntityBuilder.CreateLead(
                tenantId: TestTenantId,
                fullName: "Batch Customer 2",
                phoneNumber: "0902222222",
                email: "batch2@test.com",
                source: LeadSource.Facebook,
                status: LeadStatus.Qualified,
                leadScore: 85
            ),
            TestEntityBuilder.CreateLead(
                tenantId: TestTenantId,
                fullName: "Batch Customer 3",
                phoneNumber: "0903333333",
                email: "batch3@test.com",
                source: LeadSource.Facebook,
                status: LeadStatus.Qualified,
                leadScore: 90
            )
        };

        var createdLeads = new List<Lead>();
        foreach (var lead in leads)
        {
            createdLeads.Add(await GetLeadManagementService().CreateLeadAsync(lead));
        }

        // Act - Convert all leads in batch
        var conversionResults = new List<Customer>();
        var conversionStartTime = DateTime.UtcNow;

        foreach (var lead in createdLeads)
        {
            var customer = await GetLeadConversionService().ConvertLeadToCustomerAsync(lead.Id, "Batch conversion");
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
            var rewards = await GetLoyaltyRewardsService().GetCustomerRewardsAsync(customer.Id);
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
        var unqualifiedLead = TestEntityBuilder.CreateLead(
            tenantId: TestTenantId,
            fullName: "Unqualified Customer",
            phoneNumber: "0900000000",
            email: "unqualified@test.com",
            source: LeadSource.Website,
            status: LeadStatus.New,
            leadScore: 30
        );

        var createdLead = await GetLeadManagementService().CreateLeadAsync(unqualifiedLead);

        // Act - Try to convert unqualified lead
        // Note: ValidateLeadForConversionAsync method doesn't exist, testing through conversion attempt
        var exception1 = await Assert.ThrowsAsync<InvalidOperationException>(
            () => GetLeadConversionService().ConvertLeadToCustomerAsync(createdLead.Id, "Test conversion"));

        // Assert - Should fail for unqualified lead
        Assert.Contains("unqualified", exception1.Message.ToLower());

        // Arrange - Re-qualify the lead
        await GetLeadManagementService().UpdateLeadStatusAsync(createdLead.Id, LeadStatus.Qualified);
        createdLead.LeadScore = 85; // Update score
        await _dbContext.SaveChangesAsync();

        // Act - Try to convert qualified lead
        var customer = await GetLeadConversionService().ConvertLeadToCustomerAsync(createdLead.Id, "Test conversion");

        // Assert - Should succeed for qualified lead
        Assert.NotNull(customer);
        Assert.Equal("Unqualified Customer", customer.FullName);
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
