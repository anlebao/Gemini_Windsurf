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

    // ===================== P0.2-Đ2 (W2): SỔ CHI TIẾT TK =====================

    private static AccountingEntry CreateDebtEntry(
        TenantId tenantId,
        string accountCode,
        decimal signedAmount,
        string? vendor,
        DateTime transactionDate,
        string description = "Công nợ")
    {
        return AccountingEntry.CreateDebt(
            tenantId,
            new AccountingPeriod(transactionDate.Year, transactionDate.Month),
            new Money(signedAmount),
            description,
            accountCode,
            vendor: vendor,
            transactionDate: transactionDate);
    }

    [Fact]
    public async Task GetSubLedgerAsync_JournalAccount_ReturnsRowsWithDoiTuong()
    {
        // Arrange — JE tham chiếu AccountingEntry có Vendor → đối tượng resolve qua ReferenceId
        TenantId tenantId = new(Guid.NewGuid());
        _fixture.SetCurrentTenant(tenantId);
        VanAnDbContext db = _fixture.CreateFreshDbContext();

        AccountingEntry expense = AccountingEntry.CreateDebt(
            tenantId, new AccountingPeriod(2026, 7), new Money(2_000_000m), "Chi phí NCC A",
            "642", vendor: "NCC A", transactionDate: new DateTime(2026, 7, 15));
        db.AccountingEntries.Add(expense);
        JournalEntry entry = new(tenantId, new DateTime(2026, 7, 15), "Chi phí NCC A",
            "ManualEntry", expense.Id);
        entry.AddLine("642", 2_000_000m, 0m);
        entry.AddLine("111", 0m, 2_000_000m);
        db.JournalEntries.Add(entry);
        _ = await db.SaveChangesAsync();

        // Act
        GeneralLedgerService service = CreateService(db);
        SubLedgerReportDto report = await service.GetSubLedgerAsync(tenantId, 2026, 7, "642");

        // Assert
        Assert.Equal("642", report.AccountCode);
        Assert.Single(report.Rows);
        Assert.Equal(2_000_000m, report.Rows[0].Debit);
        Assert.Equal("NCC A", report.Rows[0].DoiTuong);
        Assert.Equal("111", report.Rows[0].CounterpartAccount);
        Assert.Equal(0m, report.OpeningBalance);
        Assert.Equal(2_000_000m, report.ClosingBalance);
    }

    [Fact]
    public async Task GetSubLedgerAsync_131_BuildsFromAccountingEntries_ByDoiTuong()
    {
        // Arrange — công nợ 131 KHÔNG có JournalEntry (G6): nguồn AccountingEntry + Vendor
        TenantId tenantId = new(Guid.NewGuid());
        _fixture.SetCurrentTenant(tenantId);
        VanAnDbContext db = _fixture.CreateFreshDbContext();

        db.AccountingEntries.AddRange(
            CreateDebtEntry(tenantId, "131", 3_000_000m, "Khách A", new DateTime(2026, 6, 20), "Bán chịu — Khách A"),
            CreateDebtEntry(tenantId, "131", 5_000_000m, "Khách A", new DateTime(2026, 7, 5), "Bán chịu — Khách A"),
            CreateDebtEntry(tenantId, "131", -2_000_000m, "Khách A", new DateTime(2026, 7, 10), "Thu tiền khách trả nợ — Khách A"),
            CreateDebtEntry(tenantId, "131", 1_000_000m, "Khách B", new DateTime(2026, 7, 12), "Bán chịu — Khách B"));
        _ = await db.SaveChangesAsync();

        // Act — sổ chi tiết 131 lọc đối tượng "Khách A"
        GeneralLedgerService service = CreateService(db);
        SubLedgerReportDto report = await service.GetSubLedgerAsync(tenantId, 2026, 7, "131", "Khách A");

        // Assert — đầu kỳ 3tr (tháng 6) · PS Nợ 5tr · PS Có 2tr · cuối kỳ 6tr — chỉ Khách A
        Assert.Equal("131", report.AccountCode);
        Assert.Equal("Khách A", report.DoiTuong);
        Assert.Equal(3_000_000m, report.OpeningBalance);
        Assert.Equal(5_000_000m, report.PeriodDebitTotal);
        Assert.Equal(2_000_000m, report.PeriodCreditTotal);
        Assert.Equal(6_000_000m, report.ClosingBalance);
        Assert.Equal(2, report.Rows.Count);
        Assert.All(report.Rows, r => Assert.Equal("Khách A", r.DoiTuong));
        Assert.Equal(8_000_000m, report.Rows[0].RunningBalance);
        Assert.Equal(6_000_000m, report.Rows[1].RunningBalance);
    }

    [Fact]
    public async Task GetSubLedgerAsync_131_AllDoiTuong_IncludesBothParties()
    {
        // Arrange
        TenantId tenantId = new(Guid.NewGuid());
        _fixture.SetCurrentTenant(tenantId);
        VanAnDbContext db = _fixture.CreateFreshDbContext();
        db.AccountingEntries.AddRange(
            CreateDebtEntry(tenantId, "131", 5_000_000m, "Khách A", new DateTime(2026, 7, 5)),
            CreateDebtEntry(tenantId, "131", 1_000_000m, "Khách B", new DateTime(2026, 7, 12)));
        _ = await db.SaveChangesAsync();

        // Act — không lọc đối tượng → toàn bộ đối tượng trên TK 131
        GeneralLedgerService service = CreateService(db);
        SubLedgerReportDto report = await service.GetSubLedgerAsync(tenantId, 2026, 7, "131");

        // Assert
        Assert.Null(report.DoiTuong);
        Assert.Equal(2, report.Rows.Count);
        Assert.Equal(6_000_000m, report.PeriodDebitTotal);
        Assert.Equal(6_000_000m, report.ClosingBalance);
    }

    [Fact]
    public async Task GetSubLedgerAsync_331_MapsCreditConvention()
    {
        // Arrange — 331 dư Có: Amount>0 (mua chịu) → PS Có · Amount<0 (trả nợ) → PS Nợ · dư âm = dư Có
        TenantId tenantId = new(Guid.NewGuid());
        _fixture.SetCurrentTenant(tenantId);
        VanAnDbContext db = _fixture.CreateFreshDbContext();
        db.AccountingEntries.AddRange(
            CreateDebtEntry(tenantId, "331", 4_000_000m, "NCC X", new DateTime(2026, 7, 3), "Mua chịu — NCC X"),
            CreateDebtEntry(tenantId, "331", -1_500_000m, "NCC X", new DateTime(2026, 7, 20), "Trả tiền người bán — NCC X"));
        _ = await db.SaveChangesAsync();

        // Act
        GeneralLedgerService service = CreateService(db);
        SubLedgerReportDto report = await service.GetSubLedgerAsync(tenantId, 2026, 7, "331", "NCC X");

        // Assert — convention Debit−Credit: cuối kỳ −2.5tr → hiển thị Dư Có 2.5tr trên mẫu S38
        Assert.Equal(4_000_000m, report.PeriodCreditTotal);
        Assert.Equal(1_500_000m, report.PeriodDebitTotal);
        Assert.Equal(-2_500_000m, report.ClosingBalance);
        Assert.Equal(0m, report.Rows[0].Debit);
        Assert.Equal(4_000_000m, report.Rows[0].Credit);
        Assert.Equal(1_500_000m, report.Rows[1].Debit);
        Assert.Equal(0m, report.Rows[1].Credit);
    }

    [Fact]
    public async Task GetSubLedgerAsync_ReversalResolvesDoiTuongFromRoot()
    {
        // Arrange — entry đảo bút toán không copy Vendor → resolve về Vendor entry gốc.
        // Lưu ý: CreateReversal giữ Period gốc nhưng TransactionDate = UtcNow
        // (reversal thuộc kỳ phát sinh — convention CongNoService BuildLedgerViewAsync)
        // → seed gốc cùng ngày với reversal để cả cặp nằm trong kỳ query.
        TenantId tenantId = new(Guid.NewGuid());
        _fixture.SetCurrentTenant(tenantId);
        VanAnDbContext db = _fixture.CreateFreshDbContext();

        DateTime entryDate = DateTime.UtcNow;
        AccountingEntry original = CreateDebtEntry(tenantId, "131", 3_000_000m, "Khách A", entryDate, "Bán chịu — Khách A");
        AccountingEntry reversal = AccountingEntry.CreateReversal(original, "RV test");
        db.AccountingEntries.AddRange(original, reversal);
        _ = await db.SaveChangesAsync();

        // Act — lọc đúng đối tượng gốc vẫn thấy cả cặp gốc + đảo
        GeneralLedgerService service = CreateService(db);
        SubLedgerReportDto report = await service.GetSubLedgerAsync(
            tenantId, reversal.TransactionDate.Year, reversal.TransactionDate.Month, "131", "Khách A");

        // Assert — reversal resolve Vendor về "Khách A" → 2 dòng, net về 0
        Assert.Equal(2, report.Rows.Count);
        Assert.All(report.Rows, r => Assert.Equal("Khách A", r.DoiTuong));
        Assert.Equal(0m, report.ClosingBalance);
    }

    [Fact]
    public async Task GetSubLedgerDoiTuongAsync_ReturnsDistinctVendors()
    {
        // Arrange
        TenantId tenantId = new(Guid.NewGuid());
        _fixture.SetCurrentTenant(tenantId);
        VanAnDbContext db = _fixture.CreateFreshDbContext();
        db.AccountingEntries.AddRange(
            CreateDebtEntry(tenantId, "131", 5_000_000m, "Khách B", new DateTime(2026, 7, 5)),
            CreateDebtEntry(tenantId, "131", 1_000_000m, "Khách A", new DateTime(2026, 7, 12)),
            CreateDebtEntry(tenantId, "331", 2_000_000m, "NCC X", new DateTime(2026, 7, 3)));
        _ = await db.SaveChangesAsync();

        // Act — đối tượng của TK 131 không lẫn NCC của 331
        GeneralLedgerService service = CreateService(db);
        IReadOnlyList<string> result = await service.GetSubLedgerDoiTuongAsync(tenantId, 2026, 7, "131");

        // Assert — OrderBy Ordinal
        Assert.Equal(new[] { "Khách A", "Khách B" }, result);
    }

    [Fact]
    public async Task GetSubLedgerAsync_IsTenantIsolated()
    {
        // Arrange — tenant A có công nợ 131
        TenantId tenantA = new(Guid.NewGuid());
        _fixture.SetCurrentTenant(tenantA);
        VanAnDbContext dbA = _fixture.CreateFreshDbContext();
        dbA.AccountingEntries.Add(CreateDebtEntry(tenantA, "131", 5_000_000m, "Khách A", new DateTime(2026, 7, 5)));
        _ = await dbA.SaveChangesAsync();

        // Act — tenant B không thấy
        TenantId tenantB = new(Guid.NewGuid());
        _fixture.SetCurrentTenant(tenantB);
        VanAnDbContext dbB = _fixture.CreateFreshDbContext();
        GeneralLedgerService service = CreateService(dbB);
        SubLedgerReportDto report = await service.GetSubLedgerAsync(tenantB, 2026, 7, "131");

        // Assert
        Assert.Empty(report.Rows);
        Assert.Equal(0m, report.ClosingBalance);
    }
}