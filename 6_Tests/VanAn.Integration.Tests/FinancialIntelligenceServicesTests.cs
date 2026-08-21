using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Infrastructure.Seed;
using VanAn.CoreHub.Repositories;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Services.FinancialIntelligence;
using VanAn.Integration.Tests.Infrastructure;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.TenantAggregate;
using Xunit;
using TenantAggregate = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant;

namespace VanAn.Integration.Tests
{
    /// <summary>
    /// VA-FI-MVP2 Phase 2 (2026-08-21): Integration tests for Financial Intelligence calculation services.
    /// Cross-checks ProfitSummary + BreakEven + TargetProfit against real IncomeStatementService output
    /// (real SQLite + AccountChart + JournalEntries).
    ///
    /// Seeds: AccountChart + tenant (Enterprise_SME, TT133_2016) + period 2026-08 JournalEntries
    ///   - Revenue 10M (511 credit), COGS 7M (632 debit), OpEx 2M (6421 debit)
    ///   - NetProfit = 10M - 7M - 2M = 1M
    ///   - BusinessProfile: fixed cost 5M (rent), capacity 200/day, 30 days
    ///
    /// Expected cross-checks:
    ///   - ProfitSummary.Revenue == 10M, COGS == 7M, OpEx == 2M, NetProfit == 1M
    ///   - BreakEven.CM = 3M, CMRatio = 0.3, BreakEvenRevenue = 5M / 0.3 ≈ 16.67M, Status = BelowBreakEven (10M < 16.67M)
    ///   - TargetProfit (target 5M): RequiredRevenue = (5M + 5M) / 0.3 ≈ 33.33M
    /// </summary>
    [Trait("Category", "FinancialIntelligence")]
    public class FinancialIntelligenceServicesTests : IClassFixture<GatewayWebApplicationFactory>
    {
        private static readonly Guid TestTenantGuid = Guid.Parse("12345678-1234-1234-1234-123456789abc");
        private static readonly TenantId TestTenantId = new(TestTenantGuid);
        private static readonly AccountingPeriod Period = AccountingPeriod.FromDateTime(new DateTime(2026, 8, 1));

        private readonly GatewayWebApplicationFactory _factory;

        public FinancialIntelligenceServicesTests(GatewayWebApplicationFactory factory)
        {
            _factory = factory;
        }

        /// <summary>Seed AccountChart + test tenant + balanced JournalEntries for period 2026-08.</summary>
        private async Task SeedAsync()
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<VanAnDbContext>();

            await db.Database.EnsureCreatedAsync();
            await AccountChartSeeder.SeedAsync(db, NullLogger.Instance);

            var existing = await db.Tenants.FirstOrDefaultAsync(t => t.Id == TestTenantId);
            if (existing is null)
            {
                var settings = new TenantSettings("test@vanan.vn", "028-1234-5678", "123 Test St", "0301234567");
                var tenant = TenantAggregate.CreateCompany(TestTenantId, "FI Test Tenant", settings);
                tenant.SetTenantType(TenantType.Enterprise_SME, AccountingStandard.TT133_2016);
                db.Tenants.Add(tenant);
            }

            // Idempotent: skip journal entries if FI-Aug entries already seeded.
            bool alreadySeeded = await db.JournalEntries
                .AnyAsync(e => e.TenantId == TestTenantId && e.Description == "FI-Aug-Sale");
            if (alreadySeeded)
            {
                await db.SaveChangesAsync();
                return;
            }

            // Sale entry 2026-08-10 — Revenue 10M (511 credit).
            var sale = new JournalEntry(TestTenantId, new DateTime(2026, 8, 10), "FI-Aug-Sale", "Sale", null);
            sale.AddLine("111", 11_000_000m, 0, "Tiền mặt");
            sale.AddLine("511", 0, 10_000_000m, "Doanh thu");
            sale.AddLine("3331", 0, 1_000_000m, "VAT đầu ra");
            db.JournalEntries.Add(sale);

            // COGS entry — 632 debit 7M, 156 credit 7M.
            var cogs = new JournalEntry(TestTenantId, new DateTime(2026, 8, 10), "FI-Aug-COGS", "COGS", null);
            cogs.AddLine("632", 7_000_000m, 0, "Giá vốn");
            cogs.AddLine("156", 0, 7_000_000m, "Xuất kho");
            db.JournalEntries.Add(cogs);

            // OpEx entry — 642 debit 2M, 111 credit 2M. (TT 133 chart has 642 at level-1 — no 6421/6422 sub-accounts.)
            var opex = new JournalEntry(TestTenantId, new DateTime(2026, 8, 15), "FI-Aug-OpEx", "Expense", null);
            opex.AddLine("642", 2_000_000m, 0, "CP quản lý DN");
            opex.AddLine("111", 0, 2_000_000m, "Trả tiền mặt");
            db.JournalEntries.Add(opex);

            await db.SaveChangesAsync();
        }

        private async Task SeedBusinessProfileAsync()
        {
            using var scope = _factory.Services.CreateScope();
            var profileRepo = scope.ServiceProvider.GetRequiredService<IBusinessProfileRepository>();
            var existing = await profileRepo.GetByTenantAsync(TestTenantId);
            if (existing is not null)
                return;
            var profile = new BusinessProfile(
                TestTenantId,
                monthlyRent: 5_000_000m, monthlyPayroll: 0m, monthlyUtilities: 0m,
                monthlyMarketing: 0m, monthlyLogistics: 0m, monthlyOtherOpEx: 0m,
                monthlyDepreciation: 0m,
                dailyCapacityUnits: 200, operatingDaysPerMonth: 30,
                pricingModel: PricingModel.FixedPrice, notes: "integration test");
            await profileRepo.AddAsync(profile);
        }

        [Fact(DisplayName = "FI-P2.8.1: ProfitSummary cross-checks with real IncomeStatement")]
        public async Task ProfitSummary_MatchesIncomeStatementValues()
        {
            await SeedAsync();

            using var scope = _factory.Services.CreateScope();
            var profitSvc = scope.ServiceProvider.GetRequiredService<IProfitSummaryService>();
            var incomeSvc = scope.ServiceProvider.GetRequiredService<IIncomeStatementService>();

            IncomeStatement income = await incomeSvc.GenerateAsync(TestTenantId, Period, AccountingStandard.TT133_2016);
            ProfitSummary summary = await profitSvc.GetAsync(TestTenantId, Period, AccountingStandard.TT133_2016);

            // Cross-check: ProfitSummary values must equal IncomeStatement extended fields.
            summary.Revenue.Should().Be(income.TotalRevenueEnding);
            summary.COGS.Should().Be(income.TotalCogsEnding);
            summary.OperatingExpenses.Should().Be(income.TotalOpExEnding);
            summary.NetProfit.Should().Be(income.NetProfitEnding);

            // Sanity check with seeded numbers: Revenue 10M, COGS 7M, OpEx 2M, Net 1M.
            summary.Revenue.Should().Be(10_000_000m);
            summary.COGS.Should().Be(7_000_000m);
            summary.OperatingExpenses.Should().Be(2_000_000m);
            summary.GrossProfit.Should().Be(3_000_000m);
            summary.OperatingProfit.Should().Be(1_000_000m);
            summary.Status.Should().Be(ProfitStatus.Profitable);
        }

        [Fact(DisplayName = "FI-P2.8.2: BreakEven computes BelowBreakEven for seeded period")]
        public async Task BreakEven_ComputesBelowBreakEvenAndFormulaConsistency()
        {
            await SeedAsync();
            await SeedBusinessProfileAsync();

            using var scope = _factory.Services.CreateScope();
            var breakEvenSvc = scope.ServiceProvider.GetRequiredService<IBreakEvenAnalysisService>();

            BreakEvenAnalysis result = await breakEvenSvc.AnalyzeAsync(TestTenantId, Period, AccountingStandard.TT133_2016);

            // Fixed 5M, Revenue 10M, COGS 7M → CM 3M, CMRatio 0.3 → BreakEvenRevenue = 5M / 0.3 ≈ 16.67M
            result.TotalFixedCost.Should().Be(5_000_000m);
            result.TotalRevenue.Should().Be(10_000_000m);
            result.TotalVariableCost.Should().Be(7_000_000m);
            result.TotalContributionMargin.Should().Be(3_000_000m);
            result.ContributionMarginRatio.Should().BeApproximately(0.3m, 0.0001m);
            result.BreakEvenRevenue.Should().BeApproximately(16_666_666.67m, 1m);
            // Revenue 10M < BreakEven 16.67M → BelowBreakEven
            result.Status.Should().Be(BreakEvenStatus.BelowBreakEven);
            result.MarginOfSafetyRevenue.Should().BeNegative(); // 10M - 16.67M < 0
        }

        [Fact(DisplayName = "FI-P2.8.3: BreakEven PROFILE_MISSING returns InsufficientData for fresh tenant")]
        public async Task BreakEven_NoProfile_ReturnsInsufficientData()
        {
            await SeedAsync();
            // Note: deliberately do NOT seed BusinessProfile for this tenant — but the shared
            // fixture may already have one from a prior test. Use a fresh tenant GUID to ensure isolation.
            var freshTenant = new TenantId(Guid.NewGuid());

            using var scope = _factory.Services.CreateScope();
            var breakEvenSvc = scope.ServiceProvider.GetRequiredService<IBreakEvenAnalysisService>();

            BreakEvenAnalysis result = await breakEvenSvc.AnalyzeAsync(freshTenant, Period, AccountingStandard.TT133_2016);

            result.Status.Should().Be(BreakEvenStatus.InsufficientData);
            result.WarningMessage.Should().Contain("PROFILE_MISSING");
        }

        [Fact(DisplayName = "FI-P2.8.4: TargetProfit RequiredRevenue formula consistency")]
        public async Task TargetProfit_RequiredRevenueFormulaConsistent()
        {
            await SeedAsync();
            await SeedBusinessProfileAsync();

            using var scope = _factory.Services.CreateScope();
            var targetSvc = scope.ServiceProvider.GetRequiredService<ITargetProfitService>();

            // CMRatio 0.3, Fixed 5M, Target 5M → RequiredRevenue = (5M + 5M) / 0.3 ≈ 33.33M
            TargetProfitAnalysis result = await targetSvc.AnalyzeAsync(TestTenantId, Period, AccountingStandard.TT133_2016, targetProfit: 5_000_000m);

            result.TotalFixedCost.Should().Be(5_000_000m);
            result.AverageContributionMargin.Should().BeApproximately(0.3m, 0.0001m);
            result.RequiredRevenue.Should().BeApproximately(33_333_333.33m, 1m);
            result.RequiredDailyUnits.Should().BeLessThanOrEqualTo(200m);
            result.Feasible.Should().BeTrue(); // low target → required daily < 200
        }

        [Fact(DisplayName = "FI-P2.8.5: DI resolves all 4 Phase 2 calculation services")]
        public async Task DI_ResolvesAllPhase2Services()
        {
            await SeedAsync();
            using var scope = _factory.Services.CreateScope();

            scope.ServiceProvider.GetRequiredService<IProfitSummaryService>().Should().NotBeNull();
            scope.ServiceProvider.GetRequiredService<IBreakEvenAnalysisService>().Should().NotBeNull();
            scope.ServiceProvider.GetRequiredService<IUnitEconomicsService>().Should().NotBeNull();
            scope.ServiceProvider.GetRequiredService<ITargetProfitService>().Should().NotBeNull();

            await Task.CompletedTask;
        }
    }
}
