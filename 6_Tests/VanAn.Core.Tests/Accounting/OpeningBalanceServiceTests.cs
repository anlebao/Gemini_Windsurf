using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VanAn.Shared.Domain;
using VanAn.Shared.DTOs;
using VanAn.CoreHub.Repositories;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Services.CongNo;
using VanAn.CoreHub.Services.Journal;
using CoreAccountingEntry = VanAn.Shared.Domain.AccountingEntry;
using Xunit;
using FluentAssertions;

namespace VanAn.Core.Tests.Accounting
{
    /// <summary>
    /// NHẬP LIỆU & SỔ SÁCH P3 (#2 — user chốt Q2): khai báo số dư đầu kỳ —
    /// (a) AccountingEntry tổng + JE "OpeningBalance" (Nợ/Có theo TK → BalanceSheet/B01) ·
    /// (b) công nợ cũ theo đối tượng (phiếu 131/331 — KHÔNG JE) · ΣNợ=ΣCó · kỳ trước Open ·
    /// khai 1 lần/kỳ.
    /// </summary>
    public class OpeningBalanceServiceTests
    {
        private readonly Mock<IAccountingEntryRepository> _entryRepo = new();
        private readonly Mock<IHKDBookRepository> _hkdRepo = new();
        private readonly Mock<IPeriodClosingService> _closing = new();
        private readonly Mock<ICongNoService> _congNo = new();
        private readonly OpeningBalanceService _service;

        public OpeningBalanceServiceTests()
        {
            _service = new OpeningBalanceService(
                _entryRepo.Object, _hkdRepo.Object, _closing.Object, _congNo.Object, NullLogger<OpeningBalanceService>.Instance);

            _ = _closing.Setup(p => p.GetPeriodStatusAsync(It.IsAny<AccountingPeriod>(), It.IsAny<TenantId>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(PeriodClosingStatus.Open);
            _ = _entryRepo.Setup(r => r.AddAsync(It.IsAny<CoreAccountingEntry>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _ = _hkdRepo.Setup(r => r.AddToBookAsync(It.IsAny<JournalEntry>(), It.IsAny<AccountingBookType>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _ = _entryRepo.Setup(r => r.GetByTenantAndTransactionDatePeriodAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([]);
        }

        private static OpeningBalanceRequestDto Request(
            int year = 2026, int month = 10,
            List<OpeningBalanceLineDto>? lines = null,
            List<OpeningBalanceCongNoLineDto>? congNo = null)
        {
            return new OpeningBalanceRequestDto
            {
                Year = year,
                Month = month,
                Lines = lines ??
                [
                    new OpeningBalanceLineDto { AccountCode = "111", AccountName = "Tiền mặt", Debit = 10_000_000m },
                    new OpeningBalanceLineDto { AccountCode = "331", AccountName = "Phải trả người bán", Credit = 3_000_000m },
                    new OpeningBalanceLineDto { AccountCode = "421", AccountName = "Lợi nhuận chưa phân phối", Credit = 7_000_000m }
                ],
                CongNoLines = congNo ?? []
            };
        }

        [Fact]
        public async Task Save_ValidBalances_CreatesOpeningEntryAndJournalEntry()
        {
            // Arrange — kỳ bắt đầu 10/2026 → số dư đầu kỳ TransactionDate = 30/09/2026
            TenantId tenant = new(Guid.NewGuid());
            var request = Request();

            // Act
            OpeningBalanceSaveResultDto result = await _service.SaveAsync(tenant, request);

            // Assert — 1 entry tổng + 1 JE (3 lines: 111 Nợ 10tr, 331 Có 3tr, 421 Có 7tr) — không công nợ
            result.JournalEntryCount.Should().Be(1);
            result.CongNoEntryCount.Should().Be(0);

            _entryRepo.Verify(r => r.AddAsync(
                It.Is<CoreAccountingEntry>(e =>
                    e.EntryType == AccountingEntryType.Adjustment &&
                    e.Description.StartsWith("Số dư đầu kỳ") &&
                    e.TransactionDate == new DateTime(2026, 9, 30) &&
                    e.Amount == 10_000_000m),
                It.IsAny<CancellationToken>()), Times.Once);

            _hkdRepo.Verify(r => r.AddToBookAsync(
                It.Is<JournalEntry>(je =>
                    je.ReferenceType == ManualEntryJournalBridge.OpeningBalanceReferenceType &&
                    je.EntryDate == new DateTime(2026, 9, 30) &&
                    je.Lines.Count == 3 &&
                    je.Lines.First(l => l.AccountNumber == "111").DebitAmount == 10_000_000m &&
                    je.Lines.First(l => l.AccountNumber == "331").CreditAmount == 3_000_000m &&
                    je.Lines.First(l => l.AccountNumber == "421").CreditAmount == 7_000_000m),
                AccountingBookType.S2b_HKD, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Save_WithCongNoLines_Creates131And331Entries_NoJournalForCongNo()
        {
            // Arrange
            TenantId tenant = new(Guid.NewGuid());
            var request = Request(congNo:
            [
                new OpeningBalanceCongNoLineDto { AccountCode = "131", DoiTuong = "Khách A", Amount = 5_000_000m },
                new OpeningBalanceCongNoLineDto { AccountCode = "331", DoiTuong = "Nhà cung cấp Y", Amount = 2_000_000m }
            ]);

            // Act
            OpeningBalanceSaveResultDto result = await _service.SaveAsync(tenant, request);

            // Assert — công nợ qua CongNoService (phiếu 131/331 — KHÔNG JE [Q1/G6])
            result.CongNoEntryCount.Should().Be(2);
            _congNo.Verify(c => c.CreateReceivableAsync(tenant, 5_000_000m, "Khách A",
                It.Is<string>(d => d.StartsWith("Số dư đầu kỳ")), new DateTime(2026, 9, 30), false,
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
            _congNo.Verify(c => c.CreatePayableAsync(tenant, 2_000_000m, "Nhà cung cấp Y",
                It.IsAny<string?>(), new DateTime(2026, 9, 30), false,
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Save_Unbalanced_Throws()
        {
            // Arrange — Nợ 10tr vs Có 3tr (thiếu 7tr bù 421)
            TenantId tenant = new(Guid.NewGuid());
            var request = Request(lines:
            [
                new OpeningBalanceLineDto { AccountCode = "111", Debit = 10_000_000m },
                new OpeningBalanceLineDto { AccountCode = "331", Credit = 3_000_000m }
            ]);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<ArgumentException>(() => _service.SaveAsync(tenant, request));
            ex.Message.Should().Contain("421");
            _entryRepo.Verify(r => r.AddAsync(It.IsAny<CoreAccountingEntry>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Save_RevenueAccount_Throws()
        {
            // Arrange — TK 511 không được có số dư đầu kỳ
            TenantId tenant = new(Guid.NewGuid());
            var request = Request(lines:
            [
                new OpeningBalanceLineDto { AccountCode = "111", Debit = 10_000_000m },
                new OpeningBalanceLineDto { AccountCode = "511", Credit = 10_000_000m }
            ]);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<ArgumentException>(() => _service.SaveAsync(tenant, request));
            ex.Message.Should().Contain("511");
        }

        [Fact]
        public async Task Save_PreviousPeriodClosed_Throws()
        {
            // Arrange — kỳ 09/2026 (trước mốc 10/2026) đã đóng
            TenantId tenant = new(Guid.NewGuid());
            _ = _closing.Setup(p => p.GetPeriodStatusAsync(new AccountingPeriod(2026, 9), tenant, It.IsAny<CancellationToken>()))
                .ReturnsAsync(PeriodClosingStatus.Closed);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.SaveAsync(tenant, Request()));
            ex.Message.Should().Contain("đã đóng sổ");
        }

        [Fact]
        public async Task Save_AlreadyOpened_Throws()
        {
            // Arrange — đã có entry "Số dư đầu kỳ" trong kỳ 09/2026
            TenantId tenant = new(Guid.NewGuid());
            var existing = CoreAccountingEntry.CreateDebt(tenant, new AccountingPeriod(2026, 9), new Money(1m), "Số dư đầu kỳ 10/2026", accountCode: string.Empty, transactionDate: new DateTime(2026, 9, 30));
            _ = _entryRepo.Setup(r => r.GetByTenantAndTransactionDatePeriodAsync(tenant, new AccountingPeriod(2026, 9), It.IsAny<CancellationToken>()))
                .ReturnsAsync([existing]);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.SaveAsync(tenant, Request()));
            ex.Message.Should().Contain("đã khai số dư đầu kỳ");
        }

        [Fact]
        public async Task Save_TenantIsolation_OtherTenantEntriesIgnored()
        {
            // Arrange — tenant khác đã khai, tenant này chưa. Multi-tenancy enforced tại repo
            // (mọi query filter TenantId — lesson a21f97f2): repo trả [] cho query của tenant này
            // dù tenant khác có entry "Số dư đầu kỳ" (không bao giờ lọt qua).
            TenantId tenant = new(Guid.NewGuid());
            TenantId otherTenant = new(Guid.NewGuid());
            _ = CoreAccountingEntry.CreateDebt(otherTenant, new AccountingPeriod(2026, 9), new Money(1m), "Số dư đầu kỳ 10/2026", accountCode: string.Empty, transactionDate: new DateTime(2026, 9, 30));

            // Default setup: GetByTenantAndTransactionDatePeriodAsync(tenant, ...) trả [] (repo đã filter tenant)

            // Act — không throw (tenant khác không ảnh hưởng)
            OpeningBalanceSaveResultDto result = await _service.SaveAsync(tenant, Request());
            result.JournalEntryCount.Should().Be(1);
        }

        [Fact]
        public async Task Get_ReturnsExistingOpeningEntries()
        {
            // Arrange
            TenantId tenant = new(Guid.NewGuid());
            var opening = CoreAccountingEntry.CreateDebt(tenant, new AccountingPeriod(2026, 9), new Money(10_000_000m), "Số dư đầu kỳ 10/2026", accountCode: string.Empty, transactionDate: new DateTime(2026, 9, 30));
            var congNo = CoreAccountingEntry.CreateDebt(tenant, new AccountingPeriod(2026, 9), new Money(5_000_000m), "Số dư đầu kỳ — Khách A", accountCode: "131", vendor: "Khách A", transactionDate: new DateTime(2026, 9, 30));
            _ = _entryRepo.Setup(r => r.GetByTenantAndTransactionDatePeriodAsync(tenant, new AccountingPeriod(2026, 9), It.IsAny<CancellationToken>()))
                .ReturnsAsync([opening, congNo]);

            // Act
            OpeningBalanceDto dto = await _service.GetAsync(tenant, 2026, 10);

            // Assert
            dto.Exists.Should().BeTrue();
            dto.Lines.Should().ContainSingle(l => l.Debit == 10_000_000m);
            dto.CongNoLines.Should().ContainSingle(l => l.DoiTuong == "Khách A" && l.AccountCode == "131" && l.Amount == 5_000_000m);
        }
    }
}
