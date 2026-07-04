using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Infrastructure.Seed;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Tests.TestInfrastructure;
using VanAn.Shared.Domain;
using Xunit;
// W3: Alias to disambiguate from legacy VanAn.CoreHub.Services.AccountType (7 values)
using AccountType = VanAn.Shared.Domain.AccountType;

namespace VanAn.Core.Tests.Services;

/// <summary>
/// W3 FIX-7: Tests for AccountChartService.
/// Covers task card verification checkboxes 1-5.
/// NOTE: Tests use IVanAnDbContext (VanAnDbContext concrete in tests). Seeder called in setup.
/// </summary>
public class AccountChartServiceTests
{
    private async Task<(VanAnDbContext db, AccountChartService svc)> SetupAsync()
    {
        TestContextScope scope = VanAnDbContextTestFactory.Create();
        VanAnDbContext db = scope.Context;
        _ = await AccountChartSeeder.SeedAsync(db, NullLogger.Instance);
        var svc = new AccountChartService(db, NullLogger<AccountChartService>.Instance);
        return (db, svc);
    }

    // W3-AC1: Task card checkbox 1
    [Fact]
    public async Task W3_AC1_GetAccountNameAsync_511_TT133_ReturnsFullDisplayName()
    {
        var (_, svc) = await SetupAsync();
        string name = await svc.GetAccountNameAsync("511", AccountingStandard.TT133_2016);
        Assert.Equal("Doanh thu bán hàng và cung cấp dịch vụ", name);
    }

    // W3-AC2: Task card checkbox 2
    [Fact]
    public async Task W3_AC2_GetAccountTypeAsync_511_TT133_ReturnsRevenue()
    {
        var (_, svc) = await SetupAsync();
        AccountType type = await svc.GetAccountTypeAsync("511", AccountingStandard.TT133_2016);
        Assert.Equal(AccountType.Revenue, type);
    }

    // W3-AC3: Task card checkbox 3 (J1+J2 contra-asset)
    [Fact]
    public async Task W3_AC3_GetAccountAsync_214_TT133_ReturnsAssetContraNormalCredit()
    {
        var (_, svc) = await SetupAsync();
        AccountChartEntry? entry = await svc.GetAccountAsync("214", AccountingStandard.TT133_2016);
        Assert.NotNull(entry);
        Assert.Equal(AccountType.Asset, entry!.Type);
        Assert.True(entry.IsNormalCredit); // contra-asset, normal credit
    }

    // W3-AC4: Task card checkbox 4 (F9 contra-revenue — TT 99 only, NOT TT 133)
    [Fact]
    public async Task W3_AC4_GetAccountAsync_521_TT99_ReturnsRevenueContraNormalDebit()
    {
        var (_, svc) = await SetupAsync();
        AccountChartEntry? entry = await svc.GetAccountAsync("521", AccountingStandard.TT99_2025);
        Assert.NotNull(entry);
        Assert.Equal(AccountType.Revenue, entry!.Type);
        Assert.False(entry.IsNormalCredit); // contra-revenue, normal debit
    }

    // W3-AC4b: Verify 521 NOT in TT 133 (FIX-3 — removed in TT 133)
    [Fact]
    public async Task W3_AC4b_GetAccountAsync_521_TT133_ReturnsNull_RemovedInTt133()
    {
        var (_, svc) = await SetupAsync();
        AccountChartEntry? entry = await svc.GetAccountAsync("521", AccountingStandard.TT133_2016);
        Assert.Null(entry); // 521 removed in TT 133 — discounts go to 511 directly
    }

    // W3-AC5: Task card checkbox 5 — fallback for unknown code
    [Fact]
    public async Task W3_AC5_GetAccountNameAsync_UnknownCode_ReturnsFallback()
    {
        var (_, svc) = await SetupAsync();
        string name = await svc.GetAccountNameAsync("999", AccountingStandard.TT133_2016);
        Assert.Equal("Tài khoản 999", name);
    }
}
