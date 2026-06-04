using Xunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;
using VanAn.Integration.Tests.Infrastructure;
using System;
using System.IO;
using System.Linq;

namespace VanAn.Integration.Tests;

public class IsolatedSQLiteTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly VanAnDbContext _dbContext;
    private readonly string _dbPath;

    public IsolatedSQLiteTests()
    {
        // Use file-based SQLite for reliability in test constructor pattern
        _dbPath = Path.Combine(Path.GetTempPath(), $"test_db_{Guid.NewGuid():N}.db");

        // Configure DI with file-based SQLite
        var services = new ServiceCollection();

        services.AddDbContext<VanAnDbContext>(options =>
            options.UseSqlite($"DataSource={_dbPath}", sqliteOptions =>
                sqliteOptions.CommandTimeout(30)));

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<VanAnDbContext>();

        // Ensure database is created
        _dbContext.Database.EnsureCreated();
    }

    [Fact(DisplayName = "SQLite Integration: Simple Entity Insert")]
    public async Task SQLite_SimpleEntity_Insert_WithBehavior_Works()
    {
        // Arrange - Use TestEntityBuilder for domain-compliant creation
        var testTenantId = TestEntityBuilder.CreateTenantId();

        // Insert Tenant first to satisfy FOREIGN KEY constraint
        var testTenant = VanAn.Shared.Domain.Tenant.CreateCompany(testTenantId, "Test Tenant");
        _dbContext.Tenants.Add(testTenant);
        await _dbContext.SaveChangesAsync();

        // Create Customer with valid TenantId
        var testCustomer = TestEntityBuilder.CreateCustomer(testTenantId);
        _dbContext.Customers.Add(testCustomer);

        try
        {
            await _dbContext.SaveChangesAsync();
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException ex)
        {
            Console.WriteLine("=== FULL EXCEPTION ===");
            Console.WriteLine(ex.ToString());
            
            if (ex.InnerException != null)
            {
                Console.WriteLine("=== INNER EXCEPTION ===");
                Console.WriteLine(ex.InnerException.ToString());
            }
            
            Console.WriteLine("=== CHANGE TRACKER ENTRIES ===");
            foreach (var entry in _dbContext.ChangeTracker.Entries())
            {
                Console.WriteLine($"{entry.Entity.GetType().Name} - {entry.State}");
            }
            
            throw;
        }

        // Verify customer was saved (with IgnoreQueryFilters)
        var savedCustomer = await _dbContext.Customers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == testCustomer.Id);
        Assert.NotNull(savedCustomer);

        var testOrder = TestEntityBuilder.CreateOrder(testTenantId, testCustomer.Id, 100.0m);

        // Act - Insert order without items first
        _dbContext.Orders.Add(testOrder);
        await _dbContext.SaveChangesAsync();

        // Assert - Verify order was saved
        var savedOrder = await _dbContext.Orders
            .FirstOrDefaultAsync(o => o.Id == testOrder.Id);

        Assert.NotNull(savedOrder);

        // NEW: Business behavior verification
        Assert.Equal(testTenantId.Value, savedOrder.TenantId.Value);
        Assert.Equal(100.0m, savedOrder.TotalAmount);
        Assert.True(savedOrder.CreatedAt <= DateTime.UtcNow);
        Assert.True(savedOrder.UpdatedAt >= savedOrder.CreatedAt);
        Assert.True(savedOrder.OrderDate <= DateTime.UtcNow);

        // Verify database count increased by exactly 1
        var orderCount = await _dbContext.Orders.CountAsync();
        Assert.Equal(1, orderCount);
    }

    [Fact(DisplayName = "SQLite Integration: Multi-Tenant Isolation")]
    public async Task SQLite_MultiTenant_WithBusinessRules_Isolation_Works()
    {
        // Arrange - Use TestEntityBuilder for domain-compliant creation
        var tenant1Id = Guid.NewGuid();
        var tenant2Id = Guid.NewGuid();

        // Create Tenants first to satisfy FK constraints (business flow)
        var tenant1 = VanAn.Shared.Domain.Tenant.CreateCompany(new TenantId(tenant1Id), "Test Tenant 1");
        var tenant2 = VanAn.Shared.Domain.Tenant.CreateCompany(new TenantId(tenant2Id), "Test Tenant 2");
        _dbContext.Tenants.AddRange(tenant1, tenant2);
        await _dbContext.SaveChangesAsync();

        var customer1 = TestEntityBuilder.CreateCustomer(new TenantId(tenant1Id));
        var customer2 = TestEntityBuilder.CreateCustomer(new TenantId(tenant2Id));

        // Create customers second to satisfy Order FK constraints
        // Use IgnoreQueryFilters to bypass soft delete filter
        _dbContext.Customers.AddRange(customer1, customer2);
        await _dbContext.SaveChangesAsync();

        // Verify customers were saved (with IgnoreQueryFilters)
        var savedCustomer1 = await _dbContext.Customers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == customer1.Id);
        var savedCustomer2 = await _dbContext.Customers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == customer2.Id);
        Assert.NotNull(savedCustomer1);
        Assert.NotNull(savedCustomer2);

        var order1 = TestEntityBuilder.CreateOrder(new TenantId(tenant1Id), customer1.Id, 100.0m);
        var order2 = TestEntityBuilder.CreateOrder(new TenantId(tenant2Id), customer2.Id, 200.0m);

        // Act - Insert orders for different tenants
        _dbContext.Orders.AddRange(order1, order2);
        await _dbContext.SaveChangesAsync();

        // Assert - Verify tenant isolation
        // Use TenantId.FromGuid() for DDD compliance and SQLite translation
        var tenant1Orders = await _dbContext.Orders
            .Where(o => o.TenantId == TenantId.FromGuid(tenant1Id))
            .ToListAsync();

        var tenant2Orders = await _dbContext.Orders
            .Where(o => o.TenantId == TenantId.FromGuid(tenant2Id))
            .ToListAsync();

        Assert.Single(tenant1Orders);
        Assert.Single(tenant2Orders);
        Assert.NotEqual(tenant1Orders[0].Id, tenant2Orders[0].Id);
        Assert.Equal(tenant1Id, tenant1Orders[0].TenantId.Value);
        Assert.Equal(tenant2Id, tenant2Orders[0].TenantId.Value);

        // NEW: Business behavior verification
        Assert.Equal(100.0m, tenant1Orders[0].TotalAmount);
        Assert.Equal(200.0m, tenant2Orders[0].TotalAmount);
        Assert.True(tenant1Orders[0].OrderDate <= DateTime.UtcNow);
        Assert.True(tenant2Orders[0].OrderDate <= DateTime.UtcNow);
        Assert.True(tenant1Orders[0].CreatedAt <= DateTime.UtcNow);
        Assert.True(tenant2Orders[0].CreatedAt <= DateTime.UtcNow);
    }

    [Fact(DisplayName = "SQLite Integration: Database Connection Status")]
    public void SQLite_DatabaseConnection_CanPing()
    {
        // Act & Assert - Verify database connection is working
        Assert.True(_dbContext.Database.CanConnect());
        
        // Note: In-memory SQLite doesn't have a file path, so we skip file existence check
        
        // Verify we can create a simple table
        var connection = _dbContext.Database.GetDbConnection();
        connection.Open();
        
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1;";
        var result = command.ExecuteScalar();
        
        connection.Close();
        
        Assert.Equal(1L, result);
    }

    [Fact(DisplayName = "SQLite Integration: WAL Mode Verification")]
    public void SQLite_WALMode_IsEnabled()
    {
        // Act - Check journal mode
        var journalMode = _dbContext.Database.SqlQueryRaw<string>("PRAGMA journal_mode;").ToList().FirstOrDefault();

        // Assert - File-based SQLite uses WAL mode by default
        // Changed from in-memory to file-based SQLite for better test reliability
        Assert.Equal("wal", journalMode);
    }

    [Fact(DisplayName = "DEBUG: Dump Schema and FKs")]
    public void Debug_DumpSchemaAndForeignKeys()
    {
        var schema = _dbContext.Database.GenerateCreateScript();
        var fkList = new System.Text.StringBuilder();
        
        foreach (var fk in _dbContext.Model.GetEntityTypes().SelectMany(x => x.GetForeignKeys()))
        {
            fkList.AppendLine($"{fk.DeclaringEntityType.Name} -> {fk.PrincipalEntityType.Name}");
        }

        // Write to file for inspection
        var debugFile = Path.Combine(Directory.GetCurrentDirectory(), "schema_debug.txt");
        File.WriteAllText(debugFile, $"=== SCHEMA ===\n{schema}\n\n=== FOREIGN KEYS ===\n{fkList}");
        
        Assert.True(true); // Always pass
    }

    [Fact(DisplayName = "DEBUG: Test Customer Insert Only")]
    public async Task Debug_CustomerInsertOnly()
    {
        var testTenantId = TestEntityBuilder.CreateTenantId();
        
        // Create Tenant first to satisfy potential FK constraint
        var tenant = VanAn.Shared.Domain.Tenant.CreateCompany(testTenantId, "Test Tenant");
        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync();
        
        var testCustomer = TestEntityBuilder.CreateCustomer(testTenantId);
        
        _dbContext.Customers.Add(testCustomer);
        
        try
        {
            await _dbContext.SaveChangesAsync();
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException ex)
        {
            var errorInfo = $"Customer insert failed: {ex.Message}\nInner: {ex.InnerException?.Message}\n\nChangeTracker:\n";
            foreach (var entry in _dbContext.ChangeTracker.Entries())
            {
                errorInfo += $"{entry.Entity.GetType().Name} - {entry.State}\n";
            }
            
            File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "customer_error.txt"), errorInfo);
            throw;
        }
        
        Assert.True(true);
    }

    public void Dispose()
    {
        // Clean up: Dispose database and service provider
        _dbContext?.Dispose();
        _serviceProvider?.Dispose();

        // Clean up temporary database file
        try
        {
            if (File.Exists(_dbPath))
            {
                File.Delete(_dbPath);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
}
