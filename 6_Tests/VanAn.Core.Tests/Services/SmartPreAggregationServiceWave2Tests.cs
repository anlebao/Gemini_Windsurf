using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services.Data;
using VanAn.CoreHub.Services.Formula;
using VanAn.CoreHub.Services.PreAggregation;
using VanAn.CoreHub.Tests.TestInfrastructure;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.TenantAggregate;
using Xunit;
using FluentAssertions;
using TenantEntity = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant;

namespace VanAn.Core.Tests.Services
{
    public class SmartPreAggregationServiceWave2Tests : IDisposable
    {
        private readonly TestContextScope _contextScope;
        private readonly VanAnDbContext _context;
        private readonly TenantId _testTenantId;
        private readonly AccountingPeriod _testPeriod = new(2024, 1);

        public SmartPreAggregationServiceWave2Tests()
        {
            _contextScope = VanAnDbContextTestFactory.Create();
            _context = _contextScope.Context;
            _testTenantId = new TenantId(_contextScope.ActiveTenantId);
        }

        public void Dispose() => _contextScope.Dispose();

        private async Task SeedTenantAndEntriesAsync(params AccountingEntry[] entries)
        {
            TenantEntity tenant = TenantEntity.CreateHouseholdBusiness(_testTenantId, "Test HKD", HKDGroup.Group1);
            _ = await _context.Tenants.AddAsync(tenant);
            foreach (AccountingEntry e in entries)
                _ = await _context.AccountingEntries.AddAsync(e);
            _ = await _context.SaveChangesAsync();

            // Debug: verify entries are in DB
            var allEntries = await _context.AccountingEntries.ToListAsync();
            Console.WriteLine($"DEBUG: {allEntries.Count} AccountingEntries in DB");
            foreach (var ae in allEntries)
                Console.WriteLine($"  Entry: TenantId={ae.TenantId?.Value}, Amount={ae.Amount}, EntryType={ae.EntryType}, AccountCode={ae.AccountCode}, PeriodYear={ae.PeriodYear}, PeriodMonth={ae.PeriodMonth}");
        }

        private SmartPreAggregationService CreateService()
        {
            Mock<IDataProvider> mockDataProvider = new();
            mockDataProvider.Setup(x => x.GetAccountSum(It.IsAny<DataProviderContext>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(0m);
            mockDataProvider.Setup(x => x.GetPreAggregatedDataAsync(It.IsAny<DataProviderContext>()))
                .ReturnsAsync(new Dictionary<string, decimal>());

            return new SmartPreAggregationService(
                _context,
                _context,
                new Lazy<IFormulaEngine>(() => new ProductionFormulaEngine(mockDataProvider.Object, new NullLogger<ProductionFormulaEngine>())),
                new NullLogger<SmartPreAggregationService>());
        }

        [Fact]
        public async Task GetAccountSumAsync_RevenueEntry_ReturnsCreditSum()
        {
            AccountingEntry entry = AccountingEntry.CreateRevenue(
                _testTenantId, _testPeriod, new Money(1000m, "VND"), "Test Revenue", accountCode: "511");
            await SeedTenantAndEntriesAsync(entry);

            Dictionary<string, decimal> result = await CreateService().GetAccountAggregatesAsync(_testTenantId, _testPeriod);

            Console.WriteLine($"DEBUG: result keys = {string.Join(", ", result.Keys)}");
            foreach (var kv in result) Console.WriteLine($"  {kv.Key} = {kv.Value}");

            _ = result.Should().ContainKey("Account_5_Credit");
            _ = result["Account_5_Credit"].Should().Be(1000m);
        }

        [Fact]
        public async Task GetAccountSumAsync_ExpenseEntry_ReturnsDebitSum()
        {
            AccountingEntry entry = AccountingEntry.CreateExpense(
                _testTenantId, _testPeriod, new Money(500m, "VND"), "Test Expense", accountCode: "611");
            await SeedTenantAndEntriesAsync(entry);

            Dictionary<string, decimal> result = await CreateService().GetAccountAggregatesAsync(_testTenantId, _testPeriod);

            _ = result.Should().ContainKey("Account_6_Debit");
            _ = result["Account_6_Debit"].Should().Be(500m);
        }

        [Fact]
        public async Task GetAccountSumAsync_NullAccountCode_UsesEntryTypeHeuristic()
        {
            AccountingEntry entry = AccountingEntry.CreateRevenue(
                _testTenantId, _testPeriod, new Money(2000m, "VND"), "Test Revenue no AccountCode");
            await SeedTenantAndEntriesAsync(entry);

            Dictionary<string, decimal> result = await CreateService().GetAccountAggregatesAsync(_testTenantId, _testPeriod);

            _ = result.Should().ContainKey("Account_5_Credit");
            _ = result["Account_5_Credit"].Should().Be(2000m);
        }
    }
}
