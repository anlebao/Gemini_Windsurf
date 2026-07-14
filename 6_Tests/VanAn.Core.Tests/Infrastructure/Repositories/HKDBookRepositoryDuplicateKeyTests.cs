using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Repositories;
using VanAn.CoreHub.Tests.TestInfrastructure;
using VanAn.Shared.Domain;
using Xunit;
using FluentAssertions;

namespace VanAn.Core.Tests.Infrastructure.Repositories
{
    /// <summary>
    /// Reproduces the Payment Webhook 500 root cause: HKDBookRepository.AddToBookAsync
    /// is called twice with the SAME JournalEntry instance (once for S2b_HKD, once for S2c_HKD)
    /// and throws on the second AddAsync due to EF Core tracking conflict.
    ///
    /// Bug context: OrderService.GenerateAccountingEntriesAsync line 162-163 adds the same
    /// revenueJournalEntry to two book types. HKDBookRepository.AddToBookAsync line 144-145
    /// calls _context.JournalEntries.AddAsync(entry) + SaveChangesAsync() each time.
    /// Second call throws InvalidOperationException (entity already tracked with same PK).
    ///
    /// Fix: Option B (defense-in-depth) — AddToBookAsync checks Local tracker before AddAsync.
    /// </summary>
    public class HKDBookRepositoryDuplicateKeyTests : IDisposable
    {
        private readonly TestContextScope _contextScope;
        private readonly VanAnDbContext _context;
        private readonly HKDBookRepository _repository;
        private readonly TenantId _testTenantId;

        public HKDBookRepositoryDuplicateKeyTests()
        {
            _contextScope = VanAnDbContextTestFactory.Create();
            _context = _contextScope.Context;
            // Use the SAME TenantId as the TestTenantProvider — VanAnDbContext applies a global
            // query filter (e => e.TenantId == CurrentTenantIdValue). If the test entities use a
            // different TenantId, CountAsync/FirstOrDefaultAsync return 0/null (filter excludes them).
            _testTenantId = new TenantId(_contextScope.TenantProvider!.TenantId);
            _repository = new HKDBookRepository(_context, NullLogger<HKDBookRepository>.Instance);
        }

        public void Dispose() => _contextScope.Dispose();

        /// <summary>
        /// SC_B1: Adding the SAME JournalEntry instance twice (S2b then S2c) MUST NOT throw.
        /// This reproduces the original 500 bug — before the fix, the second AddAsync throws
        /// InvalidOperationException because the entity is already tracked with the same key.
        /// </summary>
        [Fact]
        public async Task AddToBookAsync_CalledTwiceWithSameEntity_ShouldNotThrow_B1()
        {
            // Arrange: create a single JournalEntry (simulates CreateRevenueEntryAsync output)
            JournalEntry entry = new(
                _testTenantId,
                new DateTime(2025, 1, 15),
                "Test revenue entry",
                "Order",
                Guid.NewGuid());

            // Act: call AddToBookAsync twice with the SAME instance — must not throw
            await _repository.AddToBookAsync(entry, AccountingBookType.S2b_HKD);
            await _repository.AddToBookAsync(entry, AccountingBookType.S2c_HKD);

            // Assert: exactly ONE row persisted (not 2, not 0)
            int persistedCount = await _context.JournalEntries.CountAsync();
            persistedCount.Should().Be(1,
                "same entity added twice should persist once — book membership is a future mapping table concern");
        }

        /// <summary>
        /// SC_B2: Adding the SAME JournalEntry instance twice should persist only ONCE
        /// (total row count = 1, and the row's Description matches the entry).
        /// </summary>
        [Fact]
        public async Task AddToBookAsync_CalledTwiceWithSameEntity_PersistsSingleRow_B2()
        {
            // Arrange
            JournalEntry entry = new(
                _testTenantId,
                new DateTime(2025, 2, 20),
                "Single revenue entry",
                "Order",
                Guid.NewGuid());

            // Act
            await _repository.AddToBookAsync(entry, AccountingBookType.S2b_HKD);
            await _repository.AddToBookAsync(entry, AccountingBookType.S2c_HKD);

            // Assert: only one row persisted (JournalEntryId has a value converter — can't
            // filter by .Value in EF query, so use total count + Description match instead).
            int totalCount = await _context.JournalEntries.CountAsync();
            totalCount.Should().Be(1, "duplicate add should be a no-op, not a second insert");

            JournalEntry? persisted = await _context.JournalEntries.FirstOrDefaultAsync();
            persisted.Should().NotBeNull("entry should be persisted on first call");
            persisted!.Description.Should().Be("Single revenue entry",
                "persisted row should match the original entry's Description");
        }

        /// <summary>
        /// SC_B3: Adding DIFFERENT JournalEntry instances to the same book should still work
        /// (regression guard — the tracking check must not block legitimate new entries).
        /// </summary>
        [Fact]
        public async Task AddToBookAsync_DifferentEntities_StillPersist_B3()
        {
            // Arrange: two distinct entries
            JournalEntry entry1 = new(_testTenantId, new DateTime(2025, 3, 1), "Revenue A", "Order", Guid.NewGuid());
            JournalEntry entry2 = new(_testTenantId, new DateTime(2025, 3, 2), "Revenue B", "Order", Guid.NewGuid());

            // Act
            await _repository.AddToBookAsync(entry1, AccountingBookType.S2b_HKD);
            await _repository.AddToBookAsync(entry2, AccountingBookType.S2b_HKD);

            // Assert: both persisted (2 rows, not 1)
            int persistedCount = await _context.JournalEntries.CountAsync();
            persistedCount.Should().Be(2, "different entity instances must each persist");
        }
    }
}
