using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Tests.TestInfrastructure;

/// <summary>
/// TT 71/2024 (HTX) — sample data seeder cho Phase 3 tests (B01-HTX / B02-HTX).
/// Số dư đầu kỳ (2026-05-01) + 9 chứng từ kỳ 2026-05, số liệu cố định để assert chính xác:
///   B02-HTX: 01a=10M (511) · 01b=5M (512) · 02a=1M (521) · 10a=9M · 10b=5M · 10=14M
///            11a=6M (611) · 11b=3M (612) · 11=9M · 12=4M (642) · 20a=3M · 20b=2M · 20=1M
///            31=2M (558) · 32=1M (658) · 40=1M · 50=2M · 51=0.5M (659) · 60=1.5M
///   B01-HTX: 110=163.5M · 130=29M · 140=41M · 150=200M · 160=100M · 200=533.5M
///            300=32M · 400=501.5M (420 plug=1.5M) · 500=533.5M (= 200, W2 PASS)
/// </summary>
public static class Tt71SampleDataSeeder
{
    public static readonly Guid Tt71TenantGuid = Guid.Parse("a5b6c7d8-0000-0000-0000-000000000071");
    public static readonly TenantId Tt71TenantId = new(Tt71TenantGuid);

    public static async Task SeedAsync(VanAnDbContext db, CancellationToken ct = default)
    {
        // Số dư đầu kỳ (2026-05-01) — cân bằng: Nợ 530M = Có 530M
        var opening = new JournalEntry(Tt71TenantId, new DateTime(2026, 5, 1), "Số dư đầu kỳ 2026-05-01 (TT 71)", "OpeningBalance", null);
        opening.AddLine("111", 100_000_000m, 0, "Tiền mặt đầu kỳ");
        opening.AddLine("112", 50_000_000m, 0, "Tiền gửi NH đầu kỳ");
        opening.AddLine("131", 30_000_000m, 0, "Phải thu khách hàng đầu kỳ");
        opening.AddLine("156", 50_000_000m, 0, "Hàng hóa đầu kỳ");
        opening.AddLine("211", 200_000_000m, 0, "TSCĐ đầu kỳ");
        opening.AddLine("212", 100_000_000m, 0, "Tài sản chung không chia đầu kỳ");
        opening.AddLine("411", 0, 400_000_000m, "Vốn góp thành viên đầu kỳ");
        opening.AddLine("331", 0, 30_000_000m, "Phải trả người bán đầu kỳ");
        opening.AddLine("442", 0, 100_000_000m, "Quỹ chung không chia đầu kỳ");
        db.JournalEntries.Add(opening);

        var d = new DateTime(2026, 5, 15);

        // T1: Bán hàng bên ngoài thu tiền mặt (net 10M + VAT 1M)
        var t1 = new JournalEntry(Tt71TenantId, d, "Bán hàng bên ngoài (TT 71)", "Sale", null);
        t1.AddLine("111", 11_000_000m, 0, "Thu tiền mặt");
        t1.AddLine("511", 0, 10_000_000m, "Doanh thu giao dịch bên ngoài");
        t1.AddLine("3331", 0, 1_000_000m, "Thuế GTGT đầu ra");
        db.JournalEntries.Add(t1);

        // T1b: Giá vốn giao dịch bên ngoài
        var t1b = new JournalEntry(Tt71TenantId, d, "Giá vốn hàng bán bên ngoài (TT 71)", "COGS", null);
        t1b.AddLine("611", 6_000_000m, 0, "Giá vốn giao dịch bên ngoài");
        t1b.AddLine("156", 0, 6_000_000m, "Xuất kho");
        db.JournalEntries.Add(t1b);

        // T2: Bán hàng nội bộ (net 5M + VAT 0.5M)
        var t2 = new JournalEntry(Tt71TenantId, d, "Bán hàng nội bộ (TT 71)", "Sale", null);
        t2.AddLine("112", 5_500_000m, 0, "Thu chuyển khoản");
        t2.AddLine("512", 0, 5_000_000m, "Doanh thu giao dịch nội bộ");
        t2.AddLine("3331", 0, 500_000m, "Thuế GTGT đầu ra");
        db.JournalEntries.Add(t2);

        // T2b: Chi phí giao dịch nội bộ
        var t2b = new JournalEntry(Tt71TenantId, d, "Chi phí giao dịch nội bộ (TT 71)", "COGS", null);
        t2b.AddLine("612", 3_000_000m, 0, "Chi phí giao dịch nội bộ");
        t2b.AddLine("156", 0, 3_000_000m, "Xuất kho");
        db.JournalEntries.Add(t2b);

        // T3: Thu nhập khác
        var t3 = new JournalEntry(Tt71TenantId, d, "Thu nhập khác (TT 71)", "OtherIncome", null);
        t3.AddLine("111", 2_000_000m, 0, "Thu khác bằng tiền mặt");
        t3.AddLine("558", 0, 2_000_000m, "Thu nhập khác");
        db.JournalEntries.Add(t3);

        // T4: Chi phí khác
        var t4 = new JournalEntry(Tt71TenantId, d, "Chi phí khác (TT 71)", "OtherExpense", null);
        t4.AddLine("658", 1_000_000m, 0, "Chi phí khác");
        t4.AddLine("111", 0, 1_000_000m, "Chi tiền mặt");
        db.JournalEntries.Add(t4);

        // T5: Chi phí quản lý kinh doanh
        var t5 = new JournalEntry(Tt71TenantId, d, "Chi phí QLKD (TT 71)", "OpEx", null);
        t5.AddLine("642", 4_000_000m, 0, "Chi phí quản lý kinh doanh");
        t5.AddLine("111", 0, 4_000_000m, "Chi tiền mặt");
        db.JournalEntries.Add(t5);

        // T6: Giảm trừ doanh thu (hàng bán bị trả lại)
        var t6 = new JournalEntry(Tt71TenantId, d, "Giảm trừ doanh thu (TT 71)", "SalesReturn", null);
        t6.AddLine("521", 1_000_000m, 0, "Giảm trừ doanh thu");
        t6.AddLine("131", 0, 1_000_000m, "Giảm phải thu khách hàng");
        db.JournalEntries.Add(t6);

        // T7: Chi phí thuế TNDN
        var t7 = new JournalEntry(Tt71TenantId, d, "Chi phí thuế TNDN (TT 71)", "Tax", null);
        t7.AddLine("659", 500_000m, 0, "Chi phí thuế TNDN");
        t7.AddLine("3334", 0, 500_000m, "Thuế TNDN phải nộp");
        db.JournalEntries.Add(t7);

        await db.SaveChangesAsync(ct);
    }
}
