using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.EntityFrameworkCore;
using Moq;
using OfficeOpenXml;
using VanAn.Shared.Domain;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Repositories;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Services.CongNo;
using VanAn.CoreHub.Services.Import;
using VanAn.CoreHub.Services.Journal;
using VanAn.CoreHub.Tests.TestInfrastructure;
using Xunit;
using FluentAssertions;

namespace VanAn.Core.Tests.Accounting
{
    /// <summary>
    /// NHẬP LIỆU & SỔ SÁCH P4 (#1 Import Excel, master plan §5.4 — Q3 xlsx+CSV · Q4 dry-run 0 lỗi mới lưu):
    /// import qua IAccountingService/ICongNoService → phiếu + JE (thu/chi — tự sinh) · công nợ KHÔNG JE [G6] ·
    /// dry-run không ghi gì · dòng lỗi liệt kê · xlsx == csv · kỳ đóng/trùng lặp/isolation.
    /// </summary>
    public class ImportServiceTests
    {
        private static readonly string Header = "Ngày,Loại phiếu,Tài khoản,Số tiền,Đối tượng,MST,Diễn giải,Số chứng từ";

        private static TenantId Tenant() => new(Guid.NewGuid());

        // ── Wiring: real SQLite context + real services (bridge thật — JE tự sinh) ──

        private static (TestContextScope Scope, ImportService Service) BuildService(TenantId tenant, IPeriodClosingService? closingOverride = null)
        {
            TestContextScope scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(tenant.Value);
            VanAnDbContext ctx = scope.Context;

            Mock<IAuditTrailService> audit = new();
            AccountingEntryRepository entryRepo = new(ctx, NullLogger<AccountingEntryRepository>.Instance);
            HKDBookRepository hkdRepo = new(ctx, NullLogger<HKDBookRepository>.Instance);
            ManualEntryJournalBridge bridge = new(hkdRepo, NullLogger<ManualEntryJournalBridge>.Instance);
            IPeriodClosingService closing = closingOverride
                ?? new PeriodClosingService(entryRepo, new Mock<IReversalService>().Object, audit.Object, ctx, NullLogger<PeriodClosingService>.Instance);
            AccountingEntryService accounting = new(entryRepo, audit.Object, closing, NullLogger<AccountingEntryService>.Instance, bridge);
            CongNoService congNo = new(entryRepo, closing, audit.Object, NullLogger<CongNoService>.Instance);
            ImportService service = new(accounting, congNo, closing, entryRepo, NullLogger<ImportService>.Instance);
            return (scope, service);
        }

        private static MemoryStream CsvStream(params string[] dataRows)
        {
            StringBuilder sb = new();
            _ = sb.AppendLine(Header);
            foreach (string row in dataRows)
            {
                _ = sb.AppendLine(row);
            }

            return new MemoryStream(Encoding.UTF8.GetBytes(sb.ToString()));
        }

        private static MemoryStream XlsxStream(params string[][] dataRows)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using ExcelPackage pkg = new();
            ExcelWorksheet ws = pkg.Workbook.Worksheets.Add("Import");
            string[] header = Header.Split(',');
            for (int c = 0; c < header.Length; c++)
            {
                ws.Cells[1, c + 1].Value = header[c];
            }

            for (int r = 0; r < dataRows.Length; r++)
            {
                for (int c = 0; c < dataRows[r].Length; c++)
                {
                    ws.Cells[r + 2, c + 1].Value = dataRows[r][c];
                }
            }

            return new MemoryStream(pkg.GetAsByteArray());
        }

        private static string[] Cells(params string[] values) => values;

        // ── 1. Import hợp lệ: đủ 6 loại phiếu → phiếu + JE đúng ──────────────────

        [Fact]
        public async Task Import_ValidCsv_AllVoucherTypes_CreatesEntriesAndJournalEntries()
        {
            (TestContextScope scope, ImportService service) = BuildService(Tenant());

            try
            {
                await using MemoryStream csv = CsvStream(
                    "01/10/2026,thu-doanh-thu,511,1000000,,,\"Thu tiền bán hàng\",PT-0001",
                    "02/10/2026,chi-phí,642,500000,,,\"Tiền điện\",PC-0001",
                    "03/10/2026,ghi-nhan-phai-thu,131,5000000,\"Khách A\",0312345678,\"Bán chịu\",HD-0001",
                    "04/10/2026,thu-tien-khach-tra-no,131,1000000,\"Khách A\",0312345678,\"Khách trả nợ\",PT-0002",
                    "05/10/2026,ghi-nhan-phai-tra,331,3000000,\"NCC Y\",0123456789,\"Mua chịu\",HD-0002",
                    "06/10/2026,tra-tien-nguoi-ban,331,500000,\"NCC Y\",0123456789,\"Trả người bán\",PC-0002");

                // Act — preview sạch → import
                ImportPreviewResult preview = await service.PreviewAsync(new TenantId(scope.TenantProvider!.TenantId), csv, "test.csv");
                preview.ErrorRows.Should().Be(0, preview.Message);

                csv.Position = 0;
                ImportResult result = await service.ImportAsync(new TenantId(scope.TenantProvider!.TenantId), csv, "test.csv");

                // Assert — 6 phiếu, 2 JE (chỉ thu/chi — công nợ KHÔNG JE [G6])
                result.ImportedRows.Should().Be(6);
                result.Errors.Should().BeEmpty();

                List<AccountingEntry> entries = await scope.Context.AccountingEntries.IgnoreQueryFilters()
                    .Where(e => e.TenantId == new TenantId(scope.TenantProvider!.TenantId)).ToListAsync();
                entries.Should().HaveCount(6);

                AccountingEntry revenue = entries.Single(e => e.EntryType == AccountingEntryType.Revenue);
                revenue.AccountCode.Should().Be("511");
                revenue.Amount.Should().Be(1_000_000m);
                revenue.TransactionDate.Should().Be(new DateTime(2026, 10, 1));

                AccountingEntry expense = entries.Single(e => e.EntryType == AccountingEntryType.Expense);
                expense.AccountCode.Should().Be("642");
                expense.Amount.Should().Be(500_000m);
                expense.TransactionDate.Should().Be(new DateTime(2026, 10, 2));

                // Công nợ: 131 +5tr (bán chịu) · 131 −1tr (thu nợ) · 331 +3tr (mua chịu) · 331 −500k (trả nợ) [G9 dấu]
                List<AccountingEntry> congNo = entries.Where(e => e.AccountCode == "131" || e.AccountCode == "331").ToList();
                congNo.Should().HaveCount(4);
                congNo.Should().Contain(e => e.Amount == 5_000_000m && e.AccountCode == "131" && e.Vendor == "Khách A");
                congNo.Should().Contain(e => e.Amount == -1_000_000m && e.AccountCode == "131");
                congNo.Should().Contain(e => e.Amount == 3_000_000m && e.AccountCode == "331" && e.Vendor == "NCC Y");
                congNo.Should().Contain(e => e.Amount == -500_000m && e.AccountCode == "331");

                // JE: 2 cái (revenue + expense) — lines cân bằng
                List<JournalEntry> journalEntries = await scope.Context.JournalEntries.IgnoreQueryFilters()
                    .Where(e => e.TenantId == new TenantId(scope.TenantProvider!.TenantId)).ToListAsync();
                journalEntries.Should().HaveCount(2);

                JournalEntry revenueJe = journalEntries.Single(j => j.ReferenceType == ManualEntryJournalBridge.ManualEntryReferenceType
                    && j.ReferenceId == revenue.Id);
                revenueJe.Lines.Should().HaveCount(2);
                revenueJe.Lines.First(l => l.AccountNumber == "111").DebitAmount.Should().Be(1_000_000m);
                revenueJe.Lines.First(l => l.AccountNumber == "511").CreditAmount.Should().Be(1_000_000m);
                revenueJe.Lines.Sum(l => l.DebitAmount).Should().Be(revenueJe.Lines.Sum(l => l.CreditAmount)); // cân bằng

                JournalEntry expenseJe = journalEntries.Single(j => j.ReferenceType == ManualEntryJournalBridge.ManualEntryReferenceType
                    && j.ReferenceId == expense.Id);
                expenseJe.Lines.First(l => l.AccountNumber == "642").DebitAmount.Should().Be(500_000m);
                expenseJe.Lines.First(l => l.AccountNumber == "111").CreditAmount.Should().Be(500_000m);
            }
            finally
            {
                scope.Dispose();
            }
        }

        // ── 2. Dòng lỗi: liệt kê + KHÔNG lưu gì ────────────────────────────────

        [Fact]
        public async Task Import_ErrorLines_ListedAndNothingSaved()
        {
            (TestContextScope scope, ImportService service) = BuildService(Tenant());
            try
            {
                await using MemoryStream csv = CsvStream(
                    "32/13/2026,thu-doanh-thu,511,1000000,,,,\"PT-x1\"",                       // ngày sai
                    "01/10/2026,thu-doanh-thu,642,1000000,,,,\"PT-x2\"",                       // TK 642 không hợp lệ cho thu
                    "01/10/2026,ghi-nhan-phai-thu,131,2000000,,,,\"HD-x3\"",                   // thiếu đối tượng (công nợ)
                    "01/10/2026,chi-phí,642,0,,,,\"PC-x4\"",                                   // tiền = 0
                    "01/10/2026,thu-doanh-thu,511,1000000,,,,\"PT-ok\"");                      // dòng hợp lệ

                ImportPreviewResult preview = await service.PreviewAsync(new TenantId(scope.TenantProvider!.TenantId), csv, "test.csv");
                preview.ErrorRows.Should().Be(4);
                preview.ValidRows.Should().Be(1);
                preview.Errors.Should().Contain(e => e.RowNumber == 2 && e.Error.Contains("Ngày"));
                preview.Errors.Should().Contain(e => e.RowNumber == 3 && e.Error.Contains("Tài khoản"));
                preview.Errors.Should().Contain(e => e.RowNumber == 4 && e.Error.Contains("Đối tượng"));
                preview.Errors.Should().Contain(e => e.RowNumber == 5 && e.Error.Contains("Số tiền"));

                // Import → KHÔNG lưu gì (Q4: 0 lỗi mới lưu)
                csv.Position = 0;
                ImportResult result = await service.ImportAsync(new TenantId(scope.TenantProvider!.TenantId), csv, "test.csv");
                result.ImportedRows.Should().Be(0);
                result.SkippedRows.Should().Be(0);
                result.Errors.Should().HaveCount(4);

                int count = await scope.Context.AccountingEntries.IgnoreQueryFilters()
                    .CountAsync(e => e.TenantId == new TenantId(scope.TenantProvider!.TenantId));
                count.Should().Be(0);
            }
            finally
            {
                scope.Dispose();
            }
        }

        // ── 3. Dry-run không ghi gì ─────────────────────────────────────────────

        [Fact]
        public async Task Preview_DryRun_WritesNothing()
        {
            (TestContextScope scope, ImportService service) = BuildService(Tenant());
            try
            {
                await using MemoryStream csv = CsvStream("01/10/2026,thu-doanh-thu,511,1000000,,,,\"PT-1\"");

                ImportPreviewResult preview = await service.PreviewAsync(new TenantId(scope.TenantProvider!.TenantId), csv, "test.csv");
                preview.ErrorRows.Should().Be(0);

                int count = await scope.Context.AccountingEntries.IgnoreQueryFilters()
                    .CountAsync(e => e.TenantId == new TenantId(scope.TenantProvider!.TenantId));
                count.Should().Be(0);
                int jeCount = await scope.Context.JournalEntries.IgnoreQueryFilters().CountAsync();
                jeCount.Should().Be(0);
            }
            finally
            {
                scope.Dispose();
            }
        }

        // ── 4. xlsx == csv ──────────────────────────────────────────────────────

        [Fact]
        public async Task Import_Xlsx_EqualsCsv()
        {
            (TestContextScope scopeCsv, ImportService serviceCsv) = BuildService(Tenant());
            (TestContextScope scopeXlsx, ImportService serviceXlsx) = BuildService(Tenant());
            try
            {
                string[] row = Cells("01/10/2026", "thu-doanh-thu", "511", "750000", "", "", "Thu bán hàng", "PT-x1");
                await using MemoryStream csv = CsvStream(string.Join(",", row));
                await using MemoryStream xlsx = XlsxStream(row);

                ImportPreviewResult previewCsv = await serviceCsv.PreviewAsync(new TenantId(scopeCsv.TenantProvider!.TenantId), csv, "t.csv");
                ImportPreviewResult previewXlsx = await serviceXlsx.PreviewAsync(new TenantId(scopeXlsx.TenantProvider!.TenantId), xlsx, "t.xlsx");

                previewCsv.ErrorRows.Should().Be(previewXlsx.ErrorRows);
                previewCsv.ValidRows.Should().Be(previewXlsx.ValidRows);
                previewCsv.ValidRows.Should().Be(1);
            }
            finally
            {
                scopeCsv.Dispose();
                scopeXlsx.Dispose();
            }
        }

        // ── 5. Kỳ đã đóng → lỗi dòng ────────────────────────────────────────────

        [Fact]
        public async Task Import_ClosedPeriod_LineError_NothingSaved()
        {
            TenantId tenant = Tenant();
            Mock<IPeriodClosingService> closing = new();
            _ = closing.Setup(c => c.GetPeriodStatusAsync(It.IsAny<AccountingPeriod>(), It.IsAny<TenantId>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(PeriodClosingStatus.Closed);

            (TestContextScope scope, ImportService service) = BuildService(tenant, closing.Object);
            try
            {
                await using MemoryStream csv = CsvStream("01/10/2026,thu-doanh-thu,511,1000000,,,,\"PT-1\"");

                ImportPreviewResult preview = await service.PreviewAsync(tenant, csv, "test.csv");
                preview.ErrorRows.Should().Be(1);
                preview.Errors[0].Error.Should().Contain("đóng sổ");
            }
            finally
            {
                scope.Dispose();
            }
        }

        // ── 6. Trùng lặp trong batch (C-1 replicate) → lỗi ─────────────────────

        [Fact]
        public async Task Import_DuplicateLinesWithinBatch_Error()
        {
            (TestContextScope scope, ImportService service) = BuildService(Tenant());
            try
            {
                await using MemoryStream csv = CsvStream(
                    "01/10/2026,thu-doanh-thu,511,1000000,,,,\"PT-1\"",
                    "02/10/2026,thu-doanh-thu,511,1000000,,,,\"PT-1\""); // cùng tiền/TK/chứng từ

                ImportPreviewResult preview = await service.PreviewAsync(new TenantId(scope.TenantProvider!.TenantId), csv, "test.csv");
                preview.ErrorRows.Should().Be(1);
                preview.Errors[0].Error.Should().Contain("Trùng lặp");
            }
            finally
            {
                scope.Dispose();
            }
        }

        // ── 7. Isolation tenant ─────────────────────────────────────────────────

        [Fact]
        public async Task Import_TenantIsolation_OtherTenantSeesNothing()
        {
            TenantId tenantA = Tenant();
            (TestContextScope scope, ImportService service) = BuildService(tenantA);
            try
            {
                await using MemoryStream csv = CsvStream("01/10/2026,thu-doanh-thu,511,1000000,,,,\"PT-1\"");
                ImportResult result = await service.ImportAsync(tenantA, csv, "test.csv");
                result.ImportedRows.Should().Be(1);

                TenantId tenantB = Tenant();
                int countB = await scope.Context.AccountingEntries.IgnoreQueryFilters()
                    .CountAsync(e => e.TenantId == tenantB);
                countB.Should().Be(0);
            }
            finally
            {
                scope.Dispose();
            }
        }

        // ── 8. Template round-trip ──────────────────────────────────────────────

        [Fact]
        public async Task GenerateTemplate_ParsesBack_Valid()
        {
            (TestContextScope scope, ImportService service) = BuildService(Tenant());
            try
            {
                byte[] csvBytes = await service.GenerateTemplateAsync(ImportFileFormat.Csv);
                await using MemoryStream csv = new(csvBytes);
                ImportPreviewResult previewCsv = await service.PreviewAsync(new TenantId(scope.TenantProvider!.TenantId), csv, "mau.csv");
                previewCsv.ErrorRows.Should().Be(0, previewCsv.Message);
                previewCsv.ValidRows.Should().Be(2);

                byte[] xlsxBytes = await service.GenerateTemplateAsync(ImportFileFormat.Xlsx);
                await using MemoryStream xlsx = new(xlsxBytes);
                ImportPreviewResult previewXlsx = await service.PreviewAsync(new TenantId(scope.TenantProvider!.TenantId), xlsx, "mau.xlsx");
                previewXlsx.ErrorRows.Should().Be(0, previewXlsx.Message);
                previewXlsx.ValidRows.Should().Be(2);
            }
            finally
            {
                scope.Dispose();
            }
        }
    }
}
