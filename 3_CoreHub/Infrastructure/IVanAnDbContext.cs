using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure;

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
    DbSet<AccountingEntry> AccountingEntries { get; }
    DbSet<LoyaltyRewards> LoyaltyRewards { get; }
    DbSet<SocialCampaign> SocialCampaigns { get; }
    DbSet<OutboxMessage> OutboxMessages { get; }
    DbSet<JournalEntry> JournalEntries { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
}
