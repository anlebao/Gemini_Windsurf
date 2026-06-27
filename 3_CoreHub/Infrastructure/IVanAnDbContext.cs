using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Audit;
using VanAn.Shared.Domain.Aggregates.TenantAggregate;
using Tenant = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant;
using DemoUser = VanAn.Shared.Domain.Aggregates.UserAggregate.DemoUser;
using UserTenant = VanAn.Shared.Domain.Aggregates.UserAggregate.UserTenant;
using PermissionGroup = VanAn.Shared.Domain.Aggregates.UserAggregate.PermissionGroup;
using UserPermissionGroup = VanAn.Shared.Domain.Aggregates.UserAggregate.UserPermissionGroup;

namespace VanAn.CoreHub.Infrastructure
{
    /// <summary>
    /// Abstraction for DbContext to allow different implementations (PostgreSQL vs SQLite)
    /// This enables Offline-First architecture without tight coupling to specific database provider
    /// </summary>
    public interface IVanAnDbContext : IDisposable
    {
        DbSet<Order> Orders { get; }
        DbSet<OrderItem> OrderItems { get; }
        DbSet<Customer> Customers { get; }
        DbSet<Product> Products { get; }
        DbSet<Inventory> Inventories { get; }
        DbSet<Ingredient> Ingredients { get; }
        DbSet<AccountingEntry> AccountingEntries { get; }
        DbSet<LoyaltyRewards> LoyaltyRewards { get; }
        DbSet<SocialCampaign> SocialCampaigns { get; }
        DbSet<OutboxMessage> OutboxMessages { get; }
        DbSet<JournalEntry> JournalEntries { get; }
        DbSet<AuditLog> AuditLogs { get; }
        DbSet<PendingInvoiceQueue> PendingInvoiceQueues { get; }

        // Wave 0: Demo users for BCrypt authentication
        DbSet<DemoUser> Users { get; }

        // Wave 5: Rich Domain Tenant aggregate
        DbSet<Tenant> Tenants { get; }

        // Wave 1 Phase 2: User-Tenant mapping for multi-tenancy
        DbSet<UserTenant> UserTenants { get; }

        // Wave 6: Permission groups for bundle-based RBAC
        DbSet<PermissionGroup> PermissionGroups { get; }
        DbSet<UserPermissionGroup> UserPermissionGroups { get; }

        // Wave 14: API Keys for HMAC request signing
        DbSet<ApiKey> ApiKeys { get; }

        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
        Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
    }
}
