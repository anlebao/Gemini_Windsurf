using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VanAn.Shared.Domain;
using VanAn.CoreHub.Repositories;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Services.Journal;
using CoreAccountingEntry = VanAn.Shared.Domain.AccountingEntry;
using Xunit;
using FluentAssertions;

namespace VanAn.Core.Tests.Accounting
{
    /// <summary>
    /// NHẬP LIỆU & SỔ SÁCH P2 (#4 — user chốt Q1/Q5): phiếu tay → JournalEntry (Sổ HKD B01-B09/BCTC).
    /// Thu 5xx/7xx: Nợ 111/Có TK · chi 6xx: Nợ TK/Có 111 · công nợ KHÔNG tạo JE [G6] ·
    /// idempotent · fail-safe · reversal đồng bộ.
    /// </summary>
    public class ManualEntryJournalBridgeTests
    {
        private readonly Mock<IHKDBookRepository> _hkdRepo = new();
        private readonly ManualEntryJournalBridge _bridge;

        public ManualEntryJournalBridgeTests()
        {
            _bridge = new ManualEntryJournalBridge(_hkdRepo.Object, NullLogger<ManualEntryJournalBridge>.Instance);
        }

        private static CoreAccountingEntry Revenue(TenantId tenant, decimal amount, string accountCode, DateTime date)
            => CoreAccountingEntry.CreateRevenue(tenant, AccountingPeriod.FromDateTime(date), new Money(amount), "Thu doanh thu", accountCode: accountCode, transactionDate: date);

        private static CoreAccountingEntry Expense(TenantId tenant, decimal amount, string accountCode, DateTime date)
            => CoreAccountingEntry.CreateExpense(tenant, AccountingPeriod.FromDateTime(date), new Money(amount), "Chi phí", accountCode: accountCode, transactionDate: date);

        [Fact]
        public async Task CreateFor_Revenue511_CreatesBalancedJournalEntry()
        {
            // Arrange
            TenantId tenant = new(Guid.NewGuid());
            DateTime date = new(2026, 10, 5);
            CoreAccountingEntry entry = Revenue(tenant, 1_000_000m, "511", date);

            // Act
            bool created = await _bridge.CreateForAccountingEntryAsync(entry);

            // Assert — JE: Nợ 111 / Có 511, cân bằng, đúng ngày nghiệp vụ + reference
            created.Should().BeTrue();
            _hkdRepo.Verify(r => r.AddToBookAsync(
                It.Is<JournalEntry>(je =>
                    je.EntryDate == date &&
                    je.ReferenceType == ManualEntryJournalBridge.ManualEntryReferenceType &&
                    je.ReferenceId == entry.Id &&
                    je.Lines.Count == 2 &&
                    je.Lines.First().AccountNumber == "111" && je.Lines.First().DebitAmount == 1_000_000m && je.Lines.First().CreditAmount == 0m &&
                    je.Lines.ElementAt(1).AccountNumber == "511" && je.Lines.ElementAt(1).CreditAmount == 1_000_000m && je.Lines.ElementAt(1).DebitAmount == 0m),
                AccountingBookType.S2b_HKD, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CreateFor_Expense642_CreatesJournalEntry_Debit642_Credit111()
        {
            // Arrange
            TenantId tenant = new(Guid.NewGuid());
            CoreAccountingEntry entry = Expense(tenant, 500_000m, "642", new DateTime(2026, 10, 6));

            // Act
            bool created = await _bridge.CreateForAccountingEntryAsync(entry);

            // Assert — Nợ 642 / Có 111
            created.Should().BeTrue();
            _hkdRepo.Verify(r => r.AddToBookAsync(
                It.Is<JournalEntry>(je =>
                    je.Lines.First().AccountNumber == "642" && je.Lines.First().DebitAmount == 500_000m &&
                    je.Lines.ElementAt(1).AccountNumber == "111" && je.Lines.ElementAt(1).CreditAmount == 500_000m),
                AccountingBookType.S2b_HKD, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CreateFor_CongNo131_DoesNotCreateJournalEntry_G6()
        {
            // Arrange — phiếu công nợ (131, EntryType Adjustment) — [G6] bán chịu không ghi doanh thu
            TenantId tenant = new(Guid.NewGuid());
            CoreAccountingEntry entry = CoreAccountingEntry.CreateDebt(
                tenant, new AccountingPeriod(2026, 10), new Money(5_000_000m), "Bán chịu — Khách A", "131", vendor: "Khách A", transactionDate: new DateTime(2026, 10, 1));

            // Act
            bool created = await _bridge.CreateForAccountingEntryAsync(entry);

            // Assert — KHÔNG tạo JE
            created.Should().BeFalse();
            _hkdRepo.Verify(r => r.AddToBookAsync(It.IsAny<JournalEntry>(), It.IsAny<AccountingBookType>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task CreateFor_ReversalEntry_Skips()
        {
            // Arrange — phiếu đảo (ReversalEntryId != null) — JE reversal tạo riêng
            TenantId tenant = new(Guid.NewGuid());
            CoreAccountingEntry original = Revenue(tenant, 1_000_000m, "511", new DateTime(2026, 10, 1));
            CoreAccountingEntry reversal = CoreAccountingEntry.CreateReversal(original, "Nhập sai");

            // Act
            bool created = await _bridge.CreateForAccountingEntryAsync(reversal);

            // Assert
            created.Should().BeFalse();
            _hkdRepo.Verify(r => r.AddToBookAsync(It.IsAny<JournalEntry>(), It.IsAny<AccountingBookType>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task CreateFor_AlreadyExists_IsIdempotent()
        {
            // Arrange — JE đã tồn tại (reference-aware)
            TenantId tenant = new(Guid.NewGuid());
            CoreAccountingEntry entry = Revenue(tenant, 1_000_000m, "511", new DateTime(2026, 10, 1));
            _ = _hkdRepo.Setup(r => r.ExistsByReferenceAsync(tenant, ManualEntryJournalBridge.ManualEntryReferenceType, entry.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            bool created = await _bridge.CreateForAccountingEntryAsync(entry);

            // Assert — skip, không ghi
            created.Should().BeFalse();
            _hkdRepo.Verify(r => r.AddToBookAsync(It.IsAny<JournalEntry>(), It.IsAny<AccountingBookType>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task CreateFor_RepositoryThrows_IsFailSafe()
        {
            // Arrange
            TenantId tenant = new(Guid.NewGuid());
            CoreAccountingEntry entry = Revenue(tenant, 1_000_000m, "511", new DateTime(2026, 10, 1));
            _ = _hkdRepo.Setup(r => r.AddToBookAsync(It.IsAny<JournalEntry>(), It.IsAny<AccountingBookType>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("DB error"));

            // Act & Assert — KHÔNG throw (fail-safe — phiếu vẫn lưu)
            bool created = await _bridge.CreateForAccountingEntryAsync(entry);
            created.Should().BeFalse();
        }

        // ===== Reversal đồng bộ =====

        [Fact]
        public async Task CreateReversalFor_WithOriginalJournalEntry_CreatesReversalJournalEntry()
        {
            // Arrange — phiếu gốc CÓ JE
            TenantId tenant = new(Guid.NewGuid());
            CoreAccountingEntry original = Revenue(tenant, 1_000_000m, "511", new DateTime(2026, 10, 1));
            JournalEntry originalJe = new(tenant, original.TransactionDate, original.Description, "ManualEntry", original.Id);
            originalJe.AddLine("111", 1_000_000m, 0m, "Thu tiền");
            originalJe.AddLine("511", 0m, 1_000_000m, "Doanh thu");

            _ = _hkdRepo.Setup(r => r.GetByReferenceAsync(tenant, ManualEntryJournalBridge.ManualEntryReferenceType, original.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(originalJe);
            _ = _hkdRepo.Setup(r => r.ExistsByReferenceAsync(tenant, ManualEntryJournalBridge.ManualReversalReferenceType, original.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            // Act
            bool created = await _bridge.CreateReversalForAsync(original);

            // Assert — JE reversal: đảo Nợ/Có, IsReversal, ReversedJournalId trỏ JE gốc
            created.Should().BeTrue();
            _hkdRepo.Verify(r => r.AddToBookAsync(
                It.Is<JournalEntry>(je =>
                    je.IsReversal &&
                    je.ReversedJournalId == originalJe.JournalEntryId &&
                    je.ReferenceType == ManualEntryJournalBridge.ManualReversalReferenceType &&
                    je.ReferenceId == original.Id &&
                    je.Lines.First().AccountNumber == "111" && je.Lines.First().CreditAmount == 1_000_000m && je.Lines.First().DebitAmount == 0m &&
                    je.Lines.ElementAt(1).AccountNumber == "511" && je.Lines.ElementAt(1).DebitAmount == 1_000_000m && je.Lines.ElementAt(1).CreditAmount == 0m),
                AccountingBookType.S2b_HKD, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CreateReversalFor_WithoutOriginalJournalEntry_Skips()
        {
            // Arrange — phiếu không có JE gốc (công nợ / phiếu cũ trước P2)
            TenantId tenant = new(Guid.NewGuid());
            CoreAccountingEntry original = Revenue(tenant, 1_000_000m, "511", new DateTime(2026, 10, 1));
            _ = _hkdRepo.Setup(r => r.GetByReferenceAsync(tenant, ManualEntryJournalBridge.ManualEntryReferenceType, original.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync((JournalEntry?)null);

            // Act
            bool created = await _bridge.CreateReversalForAsync(original);

            // Assert
            created.Should().BeFalse();
            _hkdRepo.Verify(r => r.AddToBookAsync(It.IsAny<JournalEntry>(), It.IsAny<AccountingBookType>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task CreateReversalFor_AlreadyExists_IsIdempotent()
        {
            // Arrange
            TenantId tenant = new(Guid.NewGuid());
            CoreAccountingEntry original = Revenue(tenant, 1_000_000m, "511", new DateTime(2026, 10, 1));
            JournalEntry originalJe = new(tenant, original.TransactionDate, original.Description, "ManualEntry", original.Id);
            originalJe.AddLine("111", 1_000_000m, 0m);
            originalJe.AddLine("511", 0m, 1_000_000m);

            _ = _hkdRepo.Setup(r => r.GetByReferenceAsync(tenant, ManualEntryJournalBridge.ManualEntryReferenceType, original.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(originalJe);
            _ = _hkdRepo.Setup(r => r.ExistsByReferenceAsync(tenant, ManualEntryJournalBridge.ManualReversalReferenceType, original.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            bool created = await _bridge.CreateReversalForAsync(original);

            // Assert
            created.Should().BeFalse();
            _hkdRepo.Verify(r => r.AddToBookAsync(It.IsAny<JournalEntry>(), It.IsAny<AccountingBookType>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        // ===== Hooks: AccountingEntryService + ReversalService =====

        [Fact]
        public async Task CreateRevenueEntryAsync_WithBridge_CreatesJournalEntry()
        {
            // Arrange
            TenantId tenant = new(Guid.NewGuid());
            AccountingPeriod period = new(2026, 10);
            var bridge = new Mock<IManualEntryJournalBridge>();
            var repo = new Mock<IAccountingEntryRepository>();
            var audit = new Mock<IAuditTrailService>();
            var closing = new Mock<IPeriodClosingService>();
            closing.Setup(p => p.GetPeriodStatusAsync(It.IsAny<AccountingPeriod>(), It.IsAny<TenantId>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(PeriodClosingStatus.Open);
            repo.Setup(r => r.AddAsync(It.IsAny<CoreAccountingEntry>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            repo.Setup(r => r.GetByTenantAndTransactionDateRangeAsync(It.IsAny<TenantId>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([]);

            var service = new AccountingEntryService(repo.Object, audit.Object, closing.Object, NullLogger<AccountingEntryService>.Instance, bridge.Object);

            // Act
            _ = await service.CreateRevenueEntryAsync(tenant, period, 1_000_000m, "Thu", accountCode: "511", transactionDate: new DateTime(2026, 10, 5));

            // Assert — bridge được gọi đúng 1 lần với entry vừa tạo
            bridge.Verify(b => b.CreateForAccountingEntryAsync(
                It.Is<CoreAccountingEntry>(e => e.AccountCode == "511" && e.Amount == 1_000_000m),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CreateRevenueEntryAsync_BridgeThrows_EntryStillSaved()
        {
            // Arrange
            TenantId tenant = new(Guid.NewGuid());
            AccountingPeriod period = new(2026, 10);
            var bridge = new Mock<IManualEntryJournalBridge>();
            bridge.Setup(b => b.CreateForAccountingEntryAsync(It.IsAny<CoreAccountingEntry>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("bridge boom"));
            var repo = new Mock<IAccountingEntryRepository>();
            var audit = new Mock<IAuditTrailService>();
            var closing = new Mock<IPeriodClosingService>();
            closing.Setup(p => p.GetPeriodStatusAsync(It.IsAny<AccountingPeriod>(), It.IsAny<TenantId>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(PeriodClosingStatus.Open);
            repo.Setup(r => r.AddAsync(It.IsAny<CoreAccountingEntry>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            repo.Setup(r => r.GetByTenantAndTransactionDateRangeAsync(It.IsAny<TenantId>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([]);

            var service = new AccountingEntryService(repo.Object, audit.Object, closing.Object, NullLogger<AccountingEntryService>.Instance, bridge.Object);

            // Act & Assert — phiếu vẫn lưu (fail-safe), không throw
            var result = await service.CreateRevenueEntryAsync(tenant, period, 1_000_000m, "Thu", accountCode: "511", transactionDate: new DateTime(2026, 10, 5));
            result.Id.Should().NotBe(Guid.Empty);
            repo.Verify(r => r.AddAsync(It.IsAny<CoreAccountingEntry>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ReversalService_WithBridge_CreatesReversalJournalEntry()
        {
            // Arrange
            TenantId tenant = new(Guid.NewGuid());
            CoreAccountingEntry original = Revenue(tenant, 1_000_000m, "511", new DateTime(2026, 10, 1));
            var bridge = new Mock<IManualEntryJournalBridge>();
            var repo = new Mock<IAccountingEntryRepository>();
            repo.Setup(r => r.GetByIdAsync(original.Id, It.IsAny<CancellationToken>())).ReturnsAsync(original);
            repo.Setup(r => r.AddAsync(It.IsAny<CoreAccountingEntry>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            var service = new ReversalService(repo.Object, NullLogger<ReversalService>.Instance, bridge.Object);

            // Act
            _ = await service.CreateReversalEntryAsync(new AccountingEntryId(original.Id), tenant, "Nhập sai");

            // Assert — bridge.CreateReversalForAsync(originalEntry) được gọi
            bridge.Verify(b => b.CreateReversalForAsync(
                It.Is<CoreAccountingEntry>(e => e.Id == original.Id),
                It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
