using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VanAn.Shared.Domain;
using VanAn.CoreHub.Repositories;
using VanAn.CoreHub.Services.Journal;
using CoreAccountingEntry = VanAn.Shared.Domain.AccountingEntry;
using Xunit;
using FluentAssertions;

namespace VanAn.Core.Tests.Accounting
{
    /// <summary>
    /// NHẬP LIỆU & SỔ SÁCH P2 (#4): backfill JournalEntry cho phiếu thu/chi tồn tại trước P2.
    /// Preview (dry-run) đếm eligible — không ghi · Run tạo qua bridge (idempotent) + đối chiếu.
    /// </summary>
    public class BackfillJournalEntriesServiceTests
    {
        private readonly Mock<IAccountingEntryRepository> _entryRepo = new();
        private readonly Mock<IHKDBookRepository> _hkdRepo = new();
        private readonly Mock<IManualEntryJournalBridge> _bridge = new();
        private readonly BackfillJournalEntriesService _service;

        public BackfillJournalEntriesServiceTests()
        {
            _service = new BackfillJournalEntriesService(
                _entryRepo.Object, _hkdRepo.Object, _bridge.Object, NullLogger<BackfillJournalEntriesService>.Instance);
            _ = _hkdRepo.Setup(r => r.ExistsByReferenceAsync(It.IsAny<TenantId>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
            _ = _bridge.Setup(b => b.CreateForAccountingEntryAsync(It.IsAny<CoreAccountingEntry>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
        }

        private static CoreAccountingEntry Revenue(TenantId tenant, decimal amount, string accountCode, DateTime date)
            => CoreAccountingEntry.CreateRevenue(tenant, AccountingPeriod.FromDateTime(date), new Money(amount), "Thu", accountCode: accountCode, transactionDate: date);

        private static List<CoreAccountingEntry> MixedEntries(TenantId tenant)
        {
            // 3 eligible: 511, 642, 711 — 1 công nợ (131) — 1 phiếu đã đảo (reversal)
            var revenue = Revenue(tenant, 1_000_000m, "511", new DateTime(2026, 9, 1));
            var expense = CoreAccountingEntry.CreateExpense(tenant, new AccountingPeriod(2026, 9), new Money(500_000m), "Chi", accountCode: "642", transactionDate: new DateTime(2026, 9, 2));
            var otherIncome = Revenue(tenant, 200_000m, "711", new DateTime(2026, 9, 3));
            var congNo = CoreAccountingEntry.CreateDebt(tenant, new AccountingPeriod(2026, 9), new Money(5_000_000m), "Bán chịu", "131", vendor: "X", transactionDate: new DateTime(2026, 9, 4));
            var reversed = Revenue(tenant, 300_000m, "511", new DateTime(2026, 9, 5));
            var reversal = CoreAccountingEntry.CreateReversal(reversed, "Sai");
            return [revenue, expense, otherIncome, congNo, reversed, reversal];
        }

        [Fact]
        public async Task Preview_CountsOnlyEligible_DryRunNoWrite()
        {
            // Arrange
            TenantId tenant = new(Guid.NewGuid());
            _ = _entryRepo.Setup(r => r.GetByTenantAsync(tenant, It.IsAny<CancellationToken>()))
                .ReturnsAsync(MixedEntries(tenant));

            // Act
            BackfillJournalEntriesResult result = await _service.PreviewAsync(tenant);

            // Assert — 3 eligible (511, 642, 711); công nợ + phiếu đảo + reversal bị loại; không ghi gì
            result.EligibleCount.Should().Be(3);
            result.CreatedCount.Should().Be(0);
            result.Message.Should().Contain("3");
            _bridge.Verify(b => b.CreateForAccountingEntryAsync(It.IsAny<CoreAccountingEntry>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Run_CreatesJournalEntries_ForEligibleOnly()
        {
            // Arrange
            TenantId tenant = new(Guid.NewGuid());
            _ = _entryRepo.Setup(r => r.GetByTenantAsync(tenant, It.IsAny<CancellationToken>()))
                .ReturnsAsync(MixedEntries(tenant));

            // Act
            BackfillJournalEntriesResult result = await _service.RunAsync(tenant);

            // Assert — tạo đúng 3 JE (511/642/711), không đụng công nợ/phiếu đảo
            result.EligibleCount.Should().Be(3);
            result.CreatedCount.Should().Be(3);
            _bridge.Verify(b => b.CreateForAccountingEntryAsync(
                It.Is<CoreAccountingEntry>(e => e.AccountCode == "511" || e.AccountCode == "642" || e.AccountCode == "711"),
                It.IsAny<CancellationToken>()), Times.Exactly(3));
            _bridge.Verify(b => b.CreateForAccountingEntryAsync(
                It.Is<CoreAccountingEntry>(e => e.AccountCode == "131"),
                It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Preview_ExcludesEntriesWithExistingJournalEntry()
        {
            // Arrange — 1 entry đã có JE → không tính vào eligible
            TenantId tenant = new(Guid.NewGuid());
            var entries = MixedEntries(tenant);
            _ = _entryRepo.Setup(r => r.GetByTenantAsync(tenant, It.IsAny<CancellationToken>()))
                .ReturnsAsync(entries);
            _ = _hkdRepo.Setup(r => r.ExistsByReferenceAsync(tenant, ManualEntryJournalBridge.ManualEntryReferenceType, entries[0].Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true); // revenue 511 đã có JE

            // Act
            BackfillJournalEntriesResult result = await _service.PreviewAsync(tenant);

            // Assert — còn 2 eligible (642, 711)
            result.EligibleCount.Should().Be(2);
        }
    }
}
