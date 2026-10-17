using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services.GeneralLedger;
using VanAn.Integration.Tests.Infrastructure;
using VanAn.Shared.Domain;
using Xunit;

namespace VanAn.Integration.Tests.Accounting;

/// <summary>
/// TUÂN THỦ PHÁP LÝ P0.2-Đ1 (2026-10-16): Sổ Nhật ký chung + Sổ Cái + Số dư TK từ JournalEntry
/// (SQLite in-memory — TestDatabaseFixture). Multi-tenancy enforced ở mọi query.
/// </summary>
public class GeneralLedgerServiceTests : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _fixture;

    public GeneralLedgerServiceTests(TestDatabaseFixture fixture) => _fixture = fixture;

    private static GeneralLedgerService CreateService(IAccountingDbContext db) =>
        new(db, NullLogger<GeneralLedgerService>.Instance);

    private static JournalEntry CreateEntry(
        TenantId tenantId,
        DateTime date,
        string description,
        params (string Account, decimal Debit, decimal Credit)[] lines)
    {
        JournalEntry entry = new(tenantId, date, description);
        foreach ((string account, decimal debit, decimal credit) in lines)
        {
            entry.AddLine(account, debit, credit);
        }
        return entry;
    }

    [Fact]
    public async Task GetJournalAsync_ReturnsEntriesInPeriodOrdered()
    {
        // Arrange
        TenantId tenantId = new(Guid.NewGuid());
        _fixture.SetCurrentTenant(tenantId);
        VanAnDbContext db = _fixture.CreateFreshDbContext();

        JournalEntry first = CreateEntry(tenantId, new DateTime(2026, 7, 5), "Thu tiền",
            ("111", 1_000_000, 0), ("511", 0, 1_000_000));
        JournalEntry second = CreateEntry(tenantId, new DateTime(2026, 7, 10), "Chi phí",
            ("642", 200_000, 0), ("111", 0, 200_000));
        db.JournalEntries.AddRange(first, second);
        _ = await db.SaveChangesAsync();

        // Act
        GeneralLedgerService service = CreateService(db);
        IReadOnlyList<JournalEntryDto> result = await service.GetJournalAsync(tenantId, 2026, 7);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal("Thu tiền", result[0].Description);
        Assert.Equal(2, result[0].Lines.Count);
        Assert.Equal("111", result[0].Lines[0].AccountNumber);
        Assert.Equal(1_000_000m, result[0].Lines[0].DebitAmount);
        Assert.Equal(1_000_000m, result[0].TotalDebit);
        Assert.Equal(1_000_000m, result[0].TotalCredit);
        Assert.Equal(200_000m, result[1].TotalDebit);
        Assert.Equal(200_000m, result[1].TotalCredit);
    }

    [Fact]
    public async Task GetJournalAsync_FiltersByPeriod()
    {
        // Arrange
        TenantId tenantId = new(Guid.NewGuid());
        _fixture.SetCurrentTenant(tenantId);
        VanAnDbContext db = _fixture.CreateFreshDbContext();

        db.JournalEntries.Add(CreateEntry(tenantId, new DateTime(2026, 6, 30), "Tháng 6",
            ("111", 100_000, 0), ("511", 0, 100_000)));
        db.JournalEntries.Add(CreateEntry(tenantId, new DateTime(2026, 7, 1), "Tháng 7",
            ("111", 200_000, 0), ("511", 0, 200_000)));
        _ = await db.SaveChangesAsync();

        // Act
        GeneralLedgerService service = CreateService(db);
        IReadOnlyList<JournalEntryDto> result = await service.GetJournalAsync(tenantId, 2026, 7);

        // Assert — chỉ 1 bút toán tháng 7
        JournalEntryDto? entry = Assert.Single(result);
        Assert.Equal("Tháng 7", entry.Description);
    }

    [Fact]
    public async Task GetLedgerAsync_ComputesOpeningAndClosing()
    {
        // Arrange
        TenantId tenantId = new(Guid.NewGuid());
        _fixture.SetCurrentTenant(tenantId);
        VanAnDbContext db = _fixture.CreateFreshDbContext();

        // Tháng 6: 111 +5.000.000 (dư đầu kỳ tháng 7)
        db.JournalEntries.Add(CreateEntry(tenantId, new DateTime(2026, 6, 20), "Thu 6",
            ("111", 5_000_000, 0), ("511", 0, 5_000_000)));
        // Tháng 7: 111 +3.000.000 (Nợ) · −2.000.000 (Có)
        db.JournalEntries.Add(CreateEntry(tenantId, new DateTime(2026, 7, 5), "Thu 7",
            ("111", 3_000_000, 0), ("511", 0, 3_000_000)));
        db.JournalEntries.Add(CreateEntry(tenantId, new DateTime(2026, 7, 15), "Chi 7",
            ("642", 2_000_000, 0), ("111", 0, 2_000_000)));
        _ = await db.SaveChangesAsync();

        // Act
        GeneralLedgerService service = CreateService(db);
        LedgerReportDto report = await service.GetLedgerAsync(tenantId, 2026, 7, "111");

        // Assert
        Assert.Equal("111", report.AccountCode);
        Assert.Equal(5_000_000m, report.OpeningBalance);   // từ kỳ trước
        Assert.Equal(3_000_000m, report.PeriodDebitTotal);
        Assert.Equal(2_000_000m, report.PeriodCreditTotal);
        Assert.Equal(6_000_000m, report.ClosingBalance);   // 5tr + 3tr − 2tr
        Assert.Equal(2, report.Rows.Count);
        Assert.Equal(8_000_000m, report.Rows[0].RunningBalance); // 5tr + 3tr
        Assert.Equal(6_000_000m, report.Rows[1].RunningBalance); // 8tr − 2tr
    }

    [Fact]
    public async Task GetBalancesAsync_ReturnsPerAccountTotals()
    {
        // Arrange
        TenantId tenantId = new(Guid.NewGuid());
        _fixture.SetCurrentTenant(tenantId);
        VanAnDbContext db = _fixture.CreateFreshDbContext();

        db.JournalEntries.Add(CreateEntry(tenantId, new DateTime(2026, 6, 20), "Thu 6",
            ("111", 5_000_000, 0), ("511", 0, 5_000_000)));
        db.JournalEntries.Add(CreateEntry(tenantId, new DateTime(2026, 7, 5), "Thu 7",
            ("111", 3_000_000, 0), ("511", 0, 3_000_000)));
        _ = await db.SaveChangesAsync();

        // Act
        GeneralLedgerService service = CreateService(db);
        IReadOnlyList<AccountBalanceDto> result = await service.GetBalancesAsync(tenantId, 2026, 7);

        // Assert
        AccountBalanceDto acc111 = result.Single(a => a.AccountCode == "111");
        Assert.Equal(5_000_000m, acc111.OpeningBalance);
        Assert.Equal(3_000_000m, acc111.DebitTotal);
        Assert.Equal(0m, acc111.CreditTotal);
        Assert.Equal(8_000_000m, acc111.ClosingBalance);

        AccountBalanceDto acc511 = result.Single(a => a.AccountCode == "511");
        Assert.Equal(-5_000_000m, acc511.OpeningBalance); // 5tr Có từ tháng 6 → debit−credit = −5tr
        Assert.Equal(0m, acc511.DebitTotal);
        Assert.Equal(3_000_000m, acc511.CreditTotal);
        Assert.Equal(-8_000_000m, acc511.ClosingBalance); // −5tr − 3tr
    }

    [Fact]
    public async Task GetAccountsAsync_ReturnsDistinctAccountsWithTotals()
    {
        // Arrange
        TenantId tenantId = new(Guid.NewGuid());
        _fixture.SetCurrentTenant(tenantId);
        VanAnDbContext db = _fixture.CreateFreshDbContext();

        db.JournalEntries.Add(CreateEntry(tenantId, new DateTime(2026, 7, 5), "Thu 7",
            ("111", 3_000_000, 0), ("511", 0, 3_000_000)));
        _ = await db.SaveChangesAsync();

        // Act
        GeneralLedgerService service = CreateService(db);
        IReadOnlyList<AccountActivityDto> result = await service.GetAccountsAsync(tenantId, 2026, 7);

        // Assert
        Assert.Contains(result, a => a.AccountCode == "111" && a.DebitTotal == 3_000_000m);
        Assert.Contains(result, a => a.AccountCode == "511" && a.CreditTotal == 3_000_000m);
    }

    [Fact]
    public async Task GetLedgerAsync_IsTenantIsolated()
    {
        // Arrange — tenant A có dữ liệu
        TenantId tenantA = new(Guid.NewGuid());
        _fixture.SetCurrentTenant(tenantA);
        VanAnDbContext dbA = _fixture.CreateFreshDbContext();
        dbA.JournalEntries.Add(CreateEntry(tenantA, new DateTime(2026, 7, 5), "Thu A",
            ("111", 1_000_000, 0), ("511", 0, 1_000_000)));
        _ = await dbA.SaveChangesAsync();

        // Act — tenant B (context mới) không thấy dữ liệu của A
        TenantId tenantB = new(Guid.NewGuid());
        _fixture.SetCurrentTenant(tenantB);
        VanAnDbContext dbB = _fixture.CreateFreshDbContext();
        GeneralLedgerService service = CreateService(dbB);
        IReadOnlyList<JournalEntryDto> result = await service.GetJournalAsync(tenantB, 2026, 7);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetLedgerAsync_RowsIncludeCounterpartAccount()
    {
        // Arrange — bút toán 1: Nợ 111/Có 511 · bút toán 2: Nợ 642/Có 111
        TenantId tenantId = new(Guid.NewGuid());
        _fixture.SetCurrentTenant(tenantId);
        VanAnDbContext db = _fixture.CreateFreshDbContext();
        db.JournalEntries.Add(CreateEntry(tenantId, new DateTime(2026, 7, 5), "Thu tiền",
            ("111", 3_000_000, 0), ("511", 0, 3_000_000)));
        db.JournalEntries.Add(CreateEntry(tenantId, new DateTime(2026, 7, 15), "Chi phí",
            ("642", 2_000_000, 0), ("111", 0, 2_000_000)));
        _ = await db.SaveChangesAsync();

        // Act — Sổ Cái TK 111
        GeneralLedgerService service = CreateService(db);
        LedgerReportDto report = await service.GetLedgerAsync(tenantId, 2026, 7, "111");

        // Assert — TK đối ứng của từng dòng (phía còn lại của bút toán)
        Assert.Equal(2, report.Rows.Count);
        Assert.Equal("511", report.Rows[0].CounterpartAccount);
        Assert.Equal("642", report.Rows[1].CounterpartAccount);
    }

    [Fact]
    public async Task GetQuarterTotalsAsync_SumsFromQuarterStart()
    {
        // Arrange — tháng 6 (Q2) Nợ 5tr (ngoài quý) · tháng 7 (Q3) Nợ 3tr · tháng 9 (Q3) Có 2tr
        TenantId tenantId = new(Guid.NewGuid());
        _fixture.SetCurrentTenant(tenantId);
        VanAnDbContext db = _fixture.CreateFreshDbContext();
        db.JournalEntries.Add(CreateEntry(tenantId, new DateTime(2026, 6, 20), "Tháng 6 — Q2",
            ("111", 5_000_000, 0), ("511", 0, 5_000_000)));
        db.JournalEntries.Add(CreateEntry(tenantId, new DateTime(2026, 7, 5), "Tháng 7 — Q3",
            ("111", 3_000_000, 0), ("511", 0, 3_000_000)));
        db.JournalEntries.Add(CreateEntry(tenantId, new DateTime(2026, 9, 10), "Tháng 9 — Q3",
            ("642", 2_000_000, 0), ("111", 0, 2_000_000)));
        _ = await db.SaveChangesAsync();

        // Act — kỳ báo cáo tháng 7 (Q3): lũy kế từ 01/07 → 31/07 (chỉ tháng 7, không gồm tháng 9 sau kỳ)
        GeneralLedgerService service = CreateService(db);
        QuarterTotalsDto result = await service.GetQuarterTotalsAsync(tenantId, 2026, 7, "111");

        // Assert — 5tr tháng 6 (Q2) bị loại; tháng 9 (sau kỳ 7) không nằm trong phạm vi 01/07–31/07
        Assert.Equal(3_000_000m, result.DebitTotal);
        Assert.Equal(0m, result.CreditTotal);
    }

    [Fact]
    public async Task GetTenantInfoAsync_ReturnsNameAndAddress()
    {
        // Arrange — tạo tenant có Settings.Address
        TenantId tenantId = new(Guid.NewGuid());
        _fixture.SetCurrentTenant(tenantId);
        VanAnDbContext db = _fixture.CreateFreshDbContext();
        var tenant = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant.CreateCompany(
            tenantId, "Vạn An Test DN",
            new VanAn.Shared.Domain.Aggregates.TenantAggregate.TenantSettings(null, null, "123 Lê Lợi, Q.1"));
        db.Tenants.Add(tenant);
        db.Entry(tenant).Property("TenantId").CurrentValue = tenantId;
        _ = await db.SaveChangesAsync();

        // Act
        GeneralLedgerService service = CreateService(db);
        TenantInfoDto? info = await service.GetTenantInfoAsync(tenantId);

        // Assert
        Assert.NotNull(info);
        Assert.Equal("Vạn An Test DN", info.Name);
        Assert.Equal("123 Lê Lợi, Q.1", info.Address);
    }
}