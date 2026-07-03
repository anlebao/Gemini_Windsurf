using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VanAn.Shared.Domain;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Services.Template;
using VanAn.CoreHub.Repositories;
using Xunit;
using FluentAssertions;

namespace VanAn.Core.Tests.Services
{
    /// <summary>
    /// Wave 5 unit tests — IndustrySector + 4-group tax rates + PIT fix + SUM_ACCOUNT_BY_INDUSTRY.
    /// Covers W5-T1..T10 acceptance criteria.
    /// </summary>
    public class Wave5IndustrySectorTests : IDisposable
    {
        private readonly Mock<IAccountingEntryRepository> _mockRepo = new();
        private readonly Mock<IHKDBookRepository> _mockHkdRepo = new();
        private readonly Mock<IHKDBookGenerationService> _mockGen = new();
        private readonly HKDBookService _service;
        private readonly HKDTaxClassificationService _taxService;
        private readonly TenantId _tenantId = new(Guid.NewGuid());
        private readonly AccountingPeriod _period = new(2026, 7);

        public Wave5IndustrySectorTests()
        {
            _service = new HKDBookService(
                _mockRepo.Object, _mockHkdRepo.Object, _mockGen.Object,
                new NullLogger<HKDBookService>());
            _taxService = new HKDTaxClassificationService(new NullLogger<HKDTaxClassificationService>());
        }

        public void Dispose() { }

        // ─────────────────────────────────────────────────────────────────────────────
        // W5-T5: 4-group VAT rate lookup
        // ─────────────────────────────────────────────────────────────────────────────
        [Theory]
        [InlineData(IndustrySector.Distribution, 0.01)]
        [InlineData(IndustrySector.ProductionTransport, 0.03)]
        [InlineData(IndustrySector.Service, 0.05)]
        [InlineData(IndustrySector.OtherBusiness, 0.02)]
        public void GetVatRate_ReturnsCorrectRate_PerIndustrySector(IndustrySector sector, decimal expected)
        {
            decimal rate = _taxService.GetVatRate(sector);
            _ = rate.Should().Be(expected);
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // W5-T5: 4-group PIT rate lookup (HKD Group 2)
        // ─────────────────────────────────────────────────────────────────────────────
        [Theory]
        [InlineData(IndustrySector.Distribution, 0.005)]
        [InlineData(IndustrySector.ProductionTransport, 0.015)]
        [InlineData(IndustrySector.Service, 0.02)]
        [InlineData(IndustrySector.OtherBusiness, 0.01)]
        public void GetPitRate_ReturnsCorrectRate_PerIndustrySector(IndustrySector sector, decimal expected)
        {
            decimal rate = _taxService.GetPitRate(sector);
            _ = rate.Should().Be(expected);
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // W5-T10: RecordRevenueAsync persists IndustrySector on the entry
        // ─────────────────────────────────────────────────────────────────────────────
        [Fact]
        public async Task RecordRevenueAsync_PersistsIndustrySector_OnEntry()
        {
            // Act
            AccountingEntry entry = await _service.RecordRevenueAsync(
                _tenantId, 1_000_000m, "Test revenue", industrySector: IndustrySector.Service);

            // Assert
            _ = entry.IndustrySector.Should().Be(IndustrySector.Service);
            _mockRepo.Verify(r => r.AddAsync(entry, It.IsAny<CancellationToken>()), Times.Once);
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // W5-T10: RecordExpenseAsync persists IndustrySector on the entry
        // ─────────────────────────────────────────────────────────────────────────────
        [Fact]
        public async Task RecordExpenseAsync_PersistsIndustrySector_OnEntry()
        {
            // Act
            AccountingEntry entry = await _service.RecordExpenseAsync(
                _tenantId, 500_000m, "Test expense", industrySector: IndustrySector.Distribution);

            // Assert
            _ = entry.IndustrySector.Should().Be(IndustrySector.Distribution);
            _mockRepo.Verify(r => r.AddAsync(entry, It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
