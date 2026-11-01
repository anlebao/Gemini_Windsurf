using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Audit;
using VanAn.CoreHub.Repositories;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Services.Journal;
using Xunit;
using FluentAssertions;

namespace VanAn.Core.Tests.Accounting
{
    /// <summary>
    /// Task card 2026-11-01 (P2): Phiếu hạch toán CTGS — validate (≥2 dòng, TK, Nợ xor Có,
    /// ΣNợ=ΣCó) · period-closing guard · voucher CTGS fail-safe · JE JournalVoucher/Other ·
    /// AddToBookAsync(S2b_HKD) · audit fail-safe.
    /// </summary>
    public class ManualJournalVoucherServiceTests
    {
        private readonly Mock<IHKDBookRepository> _hkdRepo = new();
        private readonly Mock<IPeriodClosingService> _periodClosing = new();
        private readonly Mock<IAuditTrailService> _audit = new();
        private readonly Mock<IVoucherNumberService> _voucherNumbers = new();
        private readonly ManualJournalVoucherService _service;

        public ManualJournalVoucherServiceTests()
        {
            _ = _periodClosing.Setup(p => p.GetPeriodStatusAsync(
                    It.IsAny<AccountingPeriod>(), It.IsAny<TenantId>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(PeriodClosingStatus.Open);
            _ = _voucherNumbers.Setup(v => v.NextNumberAsync(
                    It.IsAny<TenantId>(), VoucherNumberService.TypeJournalVoucher, It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("CTGS-2026-00001");
            _ = _hkdRepo.Setup(r => r.AddToBookAsync(
                    It.IsAny<JournalEntry>(), It.IsAny<AccountingBookType>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _ = _audit.Setup(a => a.LogCreateAsync(
                    It.IsAny<AuditableEntityType>(), It.IsAny<Guid>(), It.IsAny<string>(),
                    It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((AuditLog?)null);

            _service = new ManualJournalVoucherService(
                _hkdRepo.Object, _periodClosing.Object, _audit.Object,
                NullLogger<ManualJournalVoucherService>.Instance, _voucherNumbers.Object);
        }

        private static List<JournalVoucherLine> Balanced3Lines() =>
        [
            new("111", 1_000_000m, 0m, "Thu tiền"),
            new("642", 300_000m, 0m, "Chi phí"),
            new("511", 0m, 1_300_000m, "Đối ứng")
        ];

        [Fact]
        public async Task CreateAsync_Balanced3Lines_PostsJournalVoucherEntry()
        {
            // Arrange
            TenantId tenant = new(Guid.NewGuid());
            DateTime date = new(2026, 11, 1);

            // Act
            JournalVoucherResult result = await _service.CreateAsync(
                tenant, date, "Phân bổ chi phí T11", Balanced3Lines());

            // Assert — JE: ReferenceType JournalVoucher, purpose Other, ReferenceId = VoucherId, 3 dòng
            result.VoucherNumber.Should().Be("CTGS-2026-00001");
            result.LineCount.Should().Be(3);
            result.VoucherId.Should().NotBeEmpty();
            _hkdRepo.Verify(r => r.AddToBookAsync(
                It.Is<JournalEntry>(je =>
                    je.EntryDate == date &&
                    je.ReferenceType == ManualJournalVoucherService.JournalVoucherReferenceType &&
                    je.ReferenceId == result.VoucherId &&
                    je.PostingPurpose == AccountingPostingPurposes.Other &&
                    je.Lines.Count == 3 &&
                    je.Lines.Sum(l => l.DebitAmount) == je.Lines.Sum(l => l.CreditAmount)),
                AccountingBookType.S2b_HKD, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CreateAsync_Unbalanced_ThrowsValidationException()
        {
            TenantId tenant = new(Guid.NewGuid());
            var lines = new List<JournalVoucherLine>
            {
                new("111", 1_000_000m, 0m),
                new("511", 0m, 900_000m)
            };

            Func<Task> act = () => _service.CreateAsync(tenant, new DateTime(2026, 11, 1), "Lệch", lines);

            _ = await act.Should().ThrowAsync<VanAn.CoreHub.Services.ValidationException>();
            _hkdRepo.Verify(r => r.AddToBookAsync(It.IsAny<JournalEntry>(), It.IsAny<AccountingBookType>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task CreateAsync_LessThan2Lines_Throws()
        {
            TenantId tenant = new(Guid.NewGuid());
            var lines = new List<JournalVoucherLine> { new("111", 100m, 0m) };

            Func<Task> act = () => _service.CreateAsync(tenant, new DateTime(2026, 11, 1), "1 dòng", lines);

            _ = await act.Should().ThrowAsync<VanAn.CoreHub.Services.ValidationException>();
        }

        [Theory]
        [InlineData("ABC")]
        [InlineData("0123")]   // bắt đầu bằng 0 — không hợp lệ
        [InlineData("12345")]  // quá 4 chữ số
        public async Task CreateAsync_InvalidAccount_Throws(string account)
        {
            TenantId tenant = new(Guid.NewGuid());
            var lines = new List<JournalVoucherLine>
            {
                new(account, 100m, 0m),
                new("511", 0m, 100m)
            };

            Func<Task> act = () => _service.CreateAsync(tenant, new DateTime(2026, 11, 1), "TK lỗi", lines);

            _ = await act.Should().ThrowAsync<VanAn.CoreHub.Services.ValidationException>();
        }

        [Fact]
        public async Task CreateAsync_BothDebitAndCredit_Throws()
        {
            TenantId tenant = new(Guid.NewGuid());
            var lines = new List<JournalVoucherLine>
            {
                new("111", 100m, 100m), // cả 2 phía > 0
                new("511", 0m, 100m)
            };

            Func<Task> act = () => _service.CreateAsync(tenant, new DateTime(2026, 11, 1), "Nợ+Có", lines);

            _ = await act.Should().ThrowAsync<VanAn.CoreHub.Services.ValidationException>();
        }

        [Fact]
        public async Task CreateAsync_ZeroAmounts_Throws()
        {
            TenantId tenant = new(Guid.NewGuid());
            var lines = new List<JournalVoucherLine>
            {
                new("111", 0m, 0m), // cả 2 = 0
                new("511", 0m, 100m)
            };

            Func<Task> act = () => _service.CreateAsync(tenant, new DateTime(2026, 11, 1), "Dòng 0", lines);

            _ = await act.Should().ThrowAsync<VanAn.CoreHub.Services.ValidationException>();
        }

        [Fact]
        public async Task CreateAsync_PeriodClosed_ThrowsInvalidOperation()
        {
            TenantId tenant = new(Guid.NewGuid());
            _ = _periodClosing.Setup(p => p.GetPeriodStatusAsync(
                    It.IsAny<AccountingPeriod>(), tenant, It.IsAny<CancellationToken>()))
                .ReturnsAsync(PeriodClosingStatus.Closed);

            Func<Task> act = () => _service.CreateAsync(tenant, new DateTime(2026, 10, 15), "Kỳ đóng", Balanced3Lines());

            _ = await act.Should().ThrowAsync<InvalidOperationException>();
            _hkdRepo.Verify(r => r.AddToBookAsync(It.IsAny<JournalEntry>(), It.IsAny<AccountingBookType>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task CreateAsync_VoucherNumberFails_StillPosts()
        {
            // Voucher service lỗi → fail-safe: phiếu vẫn ghi sổ, VoucherNumber = null.
            TenantId tenant = new(Guid.NewGuid());
            _ = _voucherNumbers.Setup(v => v.NextNumberAsync(
                    It.IsAny<TenantId>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("sequence error"));

            JournalVoucherResult result = await _service.CreateAsync(
                tenant, new DateTime(2026, 11, 1), "Voucher lỗi", Balanced3Lines());

            result.VoucherNumber.Should().BeNull();
            _hkdRepo.Verify(r => r.AddToBookAsync(It.IsAny<JournalEntry>(), AccountingBookType.S2b_HKD, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CreateAsync_NoDescription_Throws()
        {
            TenantId tenant = new(Guid.NewGuid());

            Func<Task> act = () => _service.CreateAsync(tenant, new DateTime(2026, 11, 1), " ", Balanced3Lines());

            _ = await act.Should().ThrowAsync<VanAn.CoreHub.Services.ValidationException>();
        }
    }
}
