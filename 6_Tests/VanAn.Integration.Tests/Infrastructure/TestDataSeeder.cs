using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.TenantAggregate;
using VanAn.Shared.Domain.Aggregates.UserAggregate;
using VanAn.Shared.Domain.Common;
using TenantAggregate = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant;
using UserAggregate = VanAn.Shared.Domain.Aggregates.UserAggregate.DemoUser;
using UserRoleEnum = VanAn.Shared.Domain.Aggregates.UserAggregate.UserRole;

namespace VanAn.Integration.Tests.Infrastructure;

/// <summary>
/// Test data seeder for integration tests
/// Provides methods to seed test tenants, users, and accounting entries
/// </summary>
public static class TestDataSeeder
{
    private static readonly TenantId DefaultTenantId = new TenantId(Guid.Parse("12345678-1234-1234-1234-123456789abc"));
    private static readonly Guid DefaultUserId = Guid.Parse("87654321-4321-4321-4321-cba987654321");

    /// <summary>
    /// Seeds a test tenant with default configuration
    /// </summary>
    public static async Task<TenantAggregate> SeedTenantAsync(
        VanAnDbContext dbContext,
        TenantId? tenantId = null,
        string name = "Test Tenant")
    {
        var id = tenantId ?? DefaultTenantId;
        
        var tenant = TenantAggregate.CreateHouseholdBusiness(id, name, HKDGroup.Group2);

        dbContext.Tenants.Add(tenant);
        await dbContext.SaveChangesAsync();

        return tenant;
    }

    /// <summary>
    /// Seeds a test user with default configuration
    /// </summary>
    public static async Task<UserAggregate> SeedUserAsync(
        VanAnDbContext dbContext,
        Guid? userId = null,
        TenantId? tenantId = null,
        string username = "testuser",
        string displayName = "Test User")
    {
        var tenant = tenantId ?? DefaultTenantId;
        var passwordHash = BCrypt.Net.BCrypt.HashPassword("TestPassword123!");

        var user = UserAggregate.Create(tenant, username, passwordHash, displayName, UserRoleEnum.Staff);

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        return user;
    }

    /// <summary>
    /// Seeds test accounting entries for a tenant
    /// </summary>
    public static async Task<List<AccountingEntry>> SeedAccountingEntriesAsync(
        VanAnDbContext dbContext,
        TenantId tenantId,
        int count = 5)
    {
        var entries = new List<AccountingEntry>();
        var period = new AccountingPeriod(2026, 6);

        for (int i = 0; i < count; i++)
        {
            AccountingEntry entry;
            var amount = new Money(1000m * (i + 1));
            
            if (i % 2 == 0)
            {
                // Wave 5: assign industry sector cycling through 4 groups
                IndustrySector sector = (IndustrySector)(i % 4);
                entry = AccountingEntry.CreateRevenue(
                    tenantId,
                    period,
                    amount,
                    $"Test revenue entry {i + 1}",
                    accountCode: "511",
                    reference: $"REF-{i + 1:000}",
                    industrySector: sector);
            }
            else
            {
                IndustrySector sector = (IndustrySector)(i % 4);
                entry = AccountingEntry.CreateExpense(
                    tenantId,
                    period,
                    amount,
                    $"Test expense entry {i + 1}",
                    accountCode: "621",
                    vendor: "Test Vendor",
                    category: "Test Category",
                    reference: $"REF-{i + 1:000}",
                    industrySector: sector);
            }

            entries.Add(entry);
        }

        dbContext.AccountingEntries.AddRange(entries);
        await dbContext.SaveChangesAsync();

        return entries;
    }

    /// <summary>
    /// Seeds complete test data (tenant, user, accounting entries)
    /// </summary>
    public static async Task<(TenantAggregate Tenant, 
        UserAggregate User, 
        List<AccountingEntry> Entries)> SeedCompleteTestDataAsync(
        VanAnDbContext dbContext,
        ILogger? logger = null)
    {
        logger?.LogInformation("Seeding test tenant...");
        var tenant = await SeedTenantAsync(dbContext);

        logger?.LogInformation("Seeding test user...");
        var user = await SeedUserAsync(dbContext, tenantId: tenant.Id);

        logger?.LogInformation("Seeding test accounting entries...");
        var entries = await SeedAccountingEntriesAsync(dbContext, tenant.Id, count: 5);

        logger?.LogInformation("Test data seeding completed successfully");
        return (tenant, user, entries);
    }

    /// <summary>
    /// Cleans up all test data for a specific tenant
    /// </summary>
    public static async Task CleanupTenantDataAsync(VanAnDbContext dbContext, TenantId tenantId)
    {
        // Delete accounting entries for the tenant
        var entries = await dbContext.AccountingEntries
            .Where(e => e.TenantId == tenantId)
            .ToListAsync();

        if (entries.Any())
        {
            dbContext.AccountingEntries.RemoveRange(entries);
        }

        // Delete users for the tenant
        var users = await dbContext.Users
            .Where(u => u.TenantId == tenantId)
            .ToListAsync();

        if (users.Any())
        {
            dbContext.Users.RemoveRange(users);
        }

        // Delete the tenant
        var tenant = await dbContext.Tenants
            .FirstOrDefaultAsync(t => t.Id == tenantId);

        if (tenant != null)
        {
            dbContext.Tenants.Remove(tenant);
        }

        await dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Cleans up all test data (use with caution)
    /// </summary>
    public static async Task CleanupAllTestDataAsync(VanAnDbContext dbContext)
    {
        // Delete all accounting entries
        var allEntries = await dbContext.AccountingEntries.ToListAsync();
        if (allEntries.Any())
        {
            dbContext.AccountingEntries.RemoveRange(allEntries);
        }

        // Delete all users
        var allUsers = await dbContext.Users.ToListAsync();
        if (allUsers.Any())
        {
            dbContext.Users.RemoveRange(allUsers);
        }

        // Delete all tenants
        var allTenants = await dbContext.Tenants.ToListAsync();
        if (allTenants.Any())
        {
            dbContext.Tenants.RemoveRange(allTenants);
        }

        await dbContext.SaveChangesAsync();
    }
}
