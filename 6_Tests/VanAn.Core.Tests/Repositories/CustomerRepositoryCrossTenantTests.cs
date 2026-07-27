using FluentAssertions;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Infrastructure.Repositories;
using VanAn.CoreHub.Tests.TestInfrastructure;
using VanAn.Shared.Domain;
using Xunit;

namespace VanAn.Core.Tests.Repositories
{
    /// <summary>
    /// AF-P1-T1 (TDD): Cross-tenant customer query tests for
    /// ICustomerRepository.GetAllCustomersAcrossTenantsAsync.
    ///
    /// Verifies:
    ///  - Returns active, non-deleted customers from MULTIPLE tenants (not just the ambient tenant).
    ///  - Bypasses the global TenantId query filter via IgnoreQueryFilters
    ///    (returns data even when ITenantProvider.TenantId = Guid.Empty).
    ///  - Excludes soft-deleted and inactive customers.
    ///
    /// SystemAdmin-only endpoint contract: this repository method MUST NOT be exposed to
    /// Owner/Staff roles — only the SystemAdmin-scoped controller action consumes it.
    /// </summary>
    public class CustomerRepositoryCrossTenantTests : IDisposable
    {
        private readonly TestContextScope _scope;
        private readonly VanAnDbContext _context;
        private readonly CustomerRepository _repository;
        private readonly TenantId _tenantA;
        private readonly TenantId _tenantB;

        public CustomerRepositoryCrossTenantTests()
        {
            _scope = VanAnDbContextTestFactory.Create();
            _context = _scope.Context;
            _repository = new CustomerRepository(_context);

            // Use two distinct tenant IDs — neither needs to match the ambient TestTenantProvider
            // because the method under test bypasses the global tenant filter.
            _tenantA = new TenantId(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
            _tenantB = new TenantId(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
        }

        public void Dispose()
        {
            _scope.Dispose();
        }

        [Fact(DisplayName = "AF-P1-T1-A: GetAllCustomersAcrossTenantsAsync returns customers from multiple tenants")]
        public async Task GetAllCustomersAcrossTenantsAsync_ReturnsCustomersFromMultipleTenants()
        {
            // Arrange — seed one active customer in each of two tenants
            var customerA = new Customer(_tenantA, "Alice TenantA", "0900000001", "alice@example.com");
            var customerB = new Customer(_tenantB, "Bob TenantB", "0900000002", "bob@example.com");
            _ = await _context.Customers.AddAsync(customerA);
            _ = await _context.Customers.AddAsync(customerB);
            _ = await _context.SaveChangesAsync();

            // Act
            System.Collections.Generic.IReadOnlyList<Customer> result =
                await _repository.GetAllCustomersAcrossTenantsAsync();

            // Assert — both tenants represented
            _ = result.Should().NotBeNull();
            _ = result.Should().Contain(c => c.Id == customerA.Id);
            _ = result.Should().Contain(c => c.Id == customerB.Id);
            _ = result.Should().Contain(c => c.TenantId == _tenantA);
            _ = result.Should().Contain(c => c.TenantId == _tenantB);
        }

        [Fact(DisplayName = "AF-P1-T1-B: GetAllCustomersAcrossTenantsAsync bypasses global TenantId query filter")]
        public async Task GetAllCustomersAcrossTenantsAsync_BypassesGlobalTenantFilter()
        {
            // Arrange — simulate "no tenant context" (e.g. SystemAdmin not impersonating any tenant).
            // The global query filter would normally exclude every row because TenantId != Guid.Empty.
            _scope.TenantProvider!.SetTenant(Guid.Empty);

            var customerA = new Customer(_tenantA, "Carol TenantA", "0900000003", "carol@example.com");
            var customerB = new Customer(_tenantB, "Dave TenantB", "0900000004", "dave@example.com");
            _ = await _context.Customers.AddAsync(customerA);
            _ = await _context.Customers.AddAsync(customerB);
            _ = await _context.SaveChangesAsync();

            // Act
            System.Collections.Generic.IReadOnlyList<Customer> result =
                await _repository.GetAllCustomersAcrossTenantsAsync();

            // Assert — IgnoreQueryFilters applied: rows returned despite ambient TenantId = Guid.Empty
            _ = result.Should().NotBeEmpty();
            _ = result.Should().Contain(c => c.Id == customerA.Id);
            _ = result.Should().Contain(c => c.Id == customerB.Id);
        }

        [Fact(DisplayName = "AF-P1-T1-C: GetAllCustomersAcrossTenantsAsync excludes soft-deleted and inactive customers")]
        public async Task GetAllCustomersAcrossTenantsAsync_ExcludesDeletedAndInactive()
        {
            // Arrange
            var active = new Customer(_tenantA, "Active A", "0900000010", "active@example.com");
            var inactive = new Customer(_tenantA, "Inactive A", "0900000011", "inactive@example.com");
            inactive.UpdateCustomerDetails(inactive.FullName, inactive.PhoneNumber, inactive.Email, inactive.CustomerTier, inactive.DeviceId, false);

            var deleted = new Customer(_tenantB, "Deleted B", "0900000012", "deleted@example.com");
            deleted.SoftDelete();

            _ = await _context.Customers.AddAsync(active);
            _ = await _context.Customers.AddAsync(inactive);
            _ = await _context.Customers.AddAsync(deleted);
            _ = await _context.SaveChangesAsync();

            // Act
            System.Collections.Generic.IReadOnlyList<Customer> result =
                await _repository.GetAllCustomersAcrossTenantsAsync();

            // Assert — only the active, non-deleted customer is returned
            _ = result.Should().Contain(c => c.Id == active.Id);
            _ = result.Should().NotContain(c => c.Id == inactive.Id);
            _ = result.Should().NotContain(c => c.Id == deleted.Id);
        }
    }
}
