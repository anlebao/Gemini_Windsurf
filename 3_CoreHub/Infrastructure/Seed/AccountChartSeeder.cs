using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Infrastructure.Entities;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.Seed;

/// <summary>
/// W3: Seeds the AccountCharts reference-data table with 2 Vietnamese accounting standards.
/// TT 133/2016 (DN vừa/nhỏ, 47 level-1 + 2 level-2 = 49 accounts) + TT 99/2025 (DN lớn, 71 level-1 + 2 level-2 = 73 accounts).
/// TT 58/2026 NOT seeded — TT 58 "bỏ hoàn toàn hệ thống tài khoản kế toán, thay bằng sổ theo dõi đơn giản hóa" (C5).
///
/// Reference data — NOT tenant-scoped. Clear+Reseed on every startup (matches code, not user-editable).
///
/// Contra account handling (J1+J2+F9):
/// - TK 214 (Hao mòn TSCĐ): Type=Asset, IsNormalCredit=true (contra-asset, normal credit)
/// - TK 229 (Dự phòng tổn thất tài sản): Type=Asset, IsNormalCredit=true (contra-asset)
/// - TK 521 (Các khoản giảm trừ DT — TT 99 ONLY, removed in TT 133): Type=Revenue, IsNormalCredit=false
///   → W4 Net Revenue = 511 (Credit) − 521 (Debit)
/// </summary>
public static class AccountChartSeeder
{
    /// <summary>
    /// Seeds all supported standards. Idempotent — skips accounts that already exist (by Standard + AccountCode).
    /// For startup usage, prefer <see cref="CleanupAsync"/> + <see cref="SeedAsync"/> to ensure chart matches code.
    /// </summary>
    public static async Task<int> SeedAsync(IVanAnDbContext dbContext, ILogger? logger = null, CancellationToken ct = default)
    {
        int totalAdded = 0;

        totalAdded += await SeedStandardAsync(dbContext, AccountingStandard.TT133_2016, GetTt133Accounts(), logger, ct).ConfigureAwait(false);
        totalAdded += await SeedStandardAsync(dbContext, AccountingStandard.TT99_2025, GetTt99Accounts(), logger, ct).ConfigureAwait(false);

        logger?.LogInformation("W3 AccountChartSeeder: seeded {Count} total account chart entries across 2 standards", totalAdded);
        return totalAdded;
    }

    /// <summary>
    /// Clears ALL AccountCharts entries. Use before <see cref="SeedAsync"/> at startup to ensure
    /// chart matches code (label corrections, account additions/removals propagate on every restart).
    /// Safe because AccountCharts is reference data with no FK dependencies and no user edits.
    /// </summary>
    public static async Task CleanupAsync(IVanAnDbContext db, CancellationToken ct = default)
    {
        var all = await db.AccountCharts.ToListAsync(ct).ConfigureAwait(false);
        db.AccountCharts.RemoveRange(all);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <summary>Seed a single standard. Idempotent per (Standard, AccountCode).</summary>
    private static async Task<int> SeedStandardAsync(
        IVanAnDbContext dbContext,
        AccountingStandard standard,
        IEnumerable<(string Code, string Name, AccountType Type, bool IsNormalCredit)> accounts,
        ILogger? logger,
        CancellationToken ct)
    {
        // Idempotency: get existing codes for this standard
        var existingCodes = await dbContext.AccountCharts
            .Where(e => e.Standard == standard)
            .Select(e => e.AccountCode)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var existingSet = new HashSet<string>(existingCodes);
        int added = 0;

        foreach (var (code, name, type, isNormalCredit) in accounts)
        {
            if (existingSet.Contains(code))
                continue;

            dbContext.AccountCharts.Add(new AccountChartEntity(code, name, type, standard, isNormalCredit));
            added++;
        }

        if (added > 0)
        {
            await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
            logger?.LogInformation("W3 AccountChartSeeder: seeded {Count} accounts for standard {Standard}", added, standard);
        }

        return added;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // TT 133/2016 — DN vừa và nhỏ (47 level-1 + 2 level-2 = 49 accounts)
    // Priority per R3. Source: TT 133/2016/TT-BTC Phụ lục II (baocaotaichinh.vn).
    // FIX-3: Removed 311 (replaced by 341), 213 (is 2113 sub), 641 (is 6421 sub), 521 (removed in TT 133).
    // FIX-3: Added 18 missing accounts. FIX-3: Fixed 155 label (Thành phẩm) + 411 label (Nguồn vốn kinh doanh).
    // FIX-6: Added 3331 + 1331 level-2 accounts (used by HkdToEnterpriseAccountMapper).
    // ═══════════════════════════════════════════════════════════════════════════

    private static IEnumerable<(string, string, AccountType, bool)> GetTt133Accounts()
    {
        // Type 1: Tài sản (Asset) — normal debit, IsNormalCredit=false
        // Exception: 214, 229 are contra-assets (normal credit)

        // Nhóm Vốn bằng tiền (2)
        yield return ("111", "Tiền mặt", AccountType.Asset, false);
        yield return ("112", "Tiền gửi ngân hàng", AccountType.Asset, false);

        // Nhóm Đầu tư tài chính (2)
        yield return ("121", "Chứng khoán kinh doanh", AccountType.Asset, false);
        yield return ("128", "Đầu tư nắm giữ đến ngày đáo hạn", AccountType.Asset, false);

        // Nhóm Các khoản phải thu (5)
        yield return ("131", "Phải thu khách hàng", AccountType.Asset, false);
        yield return ("133", "Thuế GTGT được khấu trừ", AccountType.Asset, false);
        yield return ("136", "Phải thu nội bộ", AccountType.Asset, false);
        yield return ("138", "Phải thu khác", AccountType.Asset, false);
        yield return ("141", "Tạm ứng", AccountType.Asset, false);

        // Nhóm Hàng tồn kho (7)
        yield return ("151", "Hàng mua đang đi đường", AccountType.Asset, false);
        yield return ("152", "Nguyên liệu, vật liệu", AccountType.Asset, false);
        yield return ("153", "Công cụ, dụng cụ", AccountType.Asset, false);
        yield return ("154", "Chi phí SXKD dở dang", AccountType.Asset, false);
        yield return ("155", "Thành phẩm", AccountType.Asset, false); // FIX-3 M5: TT 133 uses "Thành phẩm" (NOT "Sản phẩm" — that's TT 99)
        yield return ("156", "Hàng hóa", AccountType.Asset, false);
        yield return ("157", "Hàng gửi đi bán", AccountType.Asset, false);

        // Nhóm TSCĐ + BĐSĐT + XDCB (4)
        yield return ("211", "Tài sản cố định hữu hình", AccountType.Asset, false);
        yield return ("214", "Hao mòn TSCĐ", AccountType.Asset, true);   // J1+J2: contra-asset, normal credit
        yield return ("217", "Bất động sản đầu tư", AccountType.Asset, false);
        yield return ("241", "Xây dựng cơ bản dở dang", AccountType.Asset, false);

        // Nhóm Đầu tư vốn (1)
        yield return ("228", "Đầu tư khác", AccountType.Asset, false);

        // Nhóm Tài sản khác (2)
        yield return ("229", "Dự phòng tổn thất tài sản", AccountType.Asset, true); // contra-asset
        yield return ("242", "Chi phí trả trước", AccountType.Asset, false);

        // Type 3: Nợ phải trả (Liability) — normal credit, IsNormalCredit=true
        // Nhóm 33 (6) + Nhóm 34 (1) + Nhóm 35 (3) = 10
        yield return ("331", "Phải trả người bán", AccountType.Liability, true);
        yield return ("333", "Thuế và các khoản phải nộp nhà nước", AccountType.Liability, true);
        yield return ("3331", "Thuế GTGT đầu ra", AccountType.Liability, true); // FIX-6: level-2 for mapper
        yield return ("334", "Phải trả người lao động", AccountType.Liability, true);
        yield return ("335", "Chi phí phải trả", AccountType.Liability, true);
        yield return ("336", "Phải trả nội bộ", AccountType.Liability, true);
        yield return ("338", "Phải trả, phải nộp khác", AccountType.Liability, true);
        yield return ("341", "Vay và nợ thuê tài chính", AccountType.Liability, true);
        yield return ("352", "Dự phòng phải trả", AccountType.Liability, true);
        yield return ("353", "Quỹ khen thưởng, phúc lợi", AccountType.Liability, true);
        yield return ("356", "Quỹ phát triển KH&CN", AccountType.Liability, true);

        // Type 4: Vốn chủ sở hữu (Equity) — normal credit, IsNormalCredit=true
        // 5 accounts (411, 413, 418, 419, 421)
        yield return ("411", "Nguồn vốn kinh doanh", AccountType.Equity, true); // FIX-3 M6: TT 133 name is "Nguồn vốn kinh doanh" (NOT "Vốn đầu tư CSH" — that's TT 99)
        yield return ("413", "Chênh lệch tỷ giá hối đoái", AccountType.Equity, true);
        yield return ("418", "Các quỹ khác thuộc vốn chủ sở hữu", AccountType.Equity, true);
        yield return ("419", "Cổ phiếu quỹ", AccountType.Equity, true);
        yield return ("421", "Lợi nhuận chưa phân phối", AccountType.Equity, true);

        // Type 5: Doanh thu (Revenue) — normal credit, IsNormalCredit=true
        // NOTE: TT 133 REMOVED TK 521 (chiết khấu → 511 directly per W1 seeder note #3). Only 511 + 515.
        yield return ("511", "Doanh thu bán hàng và cung cấp dịch vụ", AccountType.Revenue, true);
        yield return ("515", "Doanh thu hoạt động tài chính", AccountType.Revenue, true);

        // Type 6: Chi phí (Expense) — normal debit, IsNormalCredit=false
        // NOTE: TT 133 gộp 641+642 → 642 (with 6421/6422 sub-accounts). No 641 at level-1.
        // 5 accounts: 611, 631, 632, 635, 642
        yield return ("611", "Mua hàng", AccountType.Expense, false);
        yield return ("631", "Giá thành sản xuất", AccountType.Expense, false);
        yield return ("632", "Giá vốn bán hàng", AccountType.Expense, false);
        yield return ("635", "Chi phí tài chính", AccountType.Expense, false);
        yield return ("642", "Chi phí quản lý doanh nghiệp", AccountType.Expense, false);

        // Type 7: Thu nhập khác (Revenue) — normal credit
        yield return ("711", "Thu nhập khác", AccountType.Revenue, true);

        // Type 8: Chi phí khác (Expense) — normal debit
        yield return ("811", "Chi phí khác", AccountType.Expense, false);
        yield return ("821", "Chi phí thuế TNDN", AccountType.Expense, false);

        // Type 9: Xác định kết quả
        yield return ("911", "Xác định kết quả kinh doanh", AccountType.Revenue, true);

        // Level-2 accounts for mapper (FIX-6)
        yield return ("1331", "Thuế GTGT đầu vào", AccountType.Asset, false); // FIX-6: level-2 for mapper
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // TT 99/2025 — DN lớn (71 level-1 + 2 level-2 = 73 accounts, effective 01/01/2026)
    // Source: TT 99/2025/TT-BTC Phụ lục II, verified via einvoice.vn + luatvietnam.vn + ketoan.vn.
    // Key changes vs TT 200: 112 renamed, 155→Sản phẩm, 215 NEW, 332 NEW (split from 338),
    // 161/417/441/461/466/611/631 REMOVED.
    // FIX-4: Added TK 332 "Phải trả cổ tức, lợi nhuận" (NEW in TT 99, split from 338).
    // FIX-6: Added 3331 + 1331 level-2 accounts (used by HkdToEnterpriseAccountMapper).
    // ═══════════════════════════════════════════════════════════════════════════

    private static IEnumerable<(string, string, AccountType, bool)> GetTt99Accounts()
    {
        // Type 1: Tài sản
        yield return ("111", "Tiền mặt", AccountType.Asset, false);
        yield return ("112", "Tiền gửi không kỳ hạn", AccountType.Asset, false); // TT 99 renamed
        yield return ("113", "Tiền đang chuyển", AccountType.Asset, false);
        yield return ("121", "Chứng khoán kinh doanh", AccountType.Asset, false);
        yield return ("128", "Đầu tư nắm giữ đến ngày đáo hạn", AccountType.Asset, false);
        yield return ("131", "Phải thu khách hàng", AccountType.Asset, false);
        yield return ("133", "Thuế GTGT được khấu trừ", AccountType.Asset, false);
        yield return ("136", "Phải thu nội bộ", AccountType.Asset, false);
        yield return ("138", "Phải thu khác", AccountType.Asset, false);
        yield return ("141", "Tạm ứng", AccountType.Asset, false);
        yield return ("151", "Hàng mua đang đi đường", AccountType.Asset, false);
        yield return ("152", "Nguyên liệu, vật liệu", AccountType.Asset, false);
        yield return ("153", "Công cụ, dụng cụ", AccountType.Asset, false);
        yield return ("154", "Chi phí SXKD dở dang", AccountType.Asset, false);
        yield return ("155", "Sản phẩm", AccountType.Asset, false); // TT 99 renamed (was "Thành phẩm" in TT 200)
        yield return ("156", "Hàng hóa", AccountType.Asset, false);
        yield return ("157", "Hàng gửi đi bán", AccountType.Asset, false);
        yield return ("158", "Nguyên liệu, vật tư tại kho bảo thuế", AccountType.Asset, false); // TT 99 renamed
        yield return ("171", "Giao dịch mua bán lại TPCP", AccountType.Asset, false);
        yield return ("211", "TSCĐ hữu hình", AccountType.Asset, false);
        yield return ("212", "TSCĐ thuê tài chính", AccountType.Asset, false);
        yield return ("213", "TSCĐ vô hình", AccountType.Asset, false);
        yield return ("214", "Hao mòn TSCĐ", AccountType.Asset, true);   // contra-asset
        yield return ("215", "Tài sản sinh học", AccountType.Asset, false); // F8: NEW in TT 99
        yield return ("217", "BĐS đầu tư", AccountType.Asset, false);
        yield return ("221", "Đầu tư vào công ty con", AccountType.Asset, false);
        yield return ("222", "Đầu tư vào công ty liên kết", AccountType.Asset, false);
        yield return ("228", "Đầu tư khác", AccountType.Asset, false);
        yield return ("229", "Dự phòng tổn thất tài sản", AccountType.Asset, true); // contra-asset
        yield return ("241", "XDCB dở dang", AccountType.Asset, false);
        yield return ("242", "Chi phí chờ phân bổ", AccountType.Asset, false); // TT 99 renamed
        yield return ("243", "Tài sản thuế TNDN hoãn lại", AccountType.Asset, false);
        yield return ("244", "Ký quỹ, ký cược", AccountType.Asset, false); // TT 99 renamed

        // Type 3: Nợ phải trả
        yield return ("331", "Phải trả người bán", AccountType.Liability, true);
        yield return ("332", "Phải trả cổ tức, lợi nhuận", AccountType.Liability, true); // FIX-4: NEW in TT 99 (split from 338)
        yield return ("333", "Thuế & các khoản phải nộp Nhà nước", AccountType.Liability, true);
        yield return ("3331", "Thuế GTGT đầu ra", AccountType.Liability, true); // FIX-6: level-2 for mapper
        yield return ("334", "Phải trả NLĐ", AccountType.Liability, true);
        yield return ("335", "Chi phí phải trả", AccountType.Liability, true);
        yield return ("336", "Phải trả nội bộ", AccountType.Liability, true);
        yield return ("337", "Thanh toán theo tiến độ kế hoạch", AccountType.Liability, true);
        yield return ("338", "Phải trả, phải nộp khác", AccountType.Liability, true);
        yield return ("341", "Vay & nợ thuê tài chính", AccountType.Liability, true);
        yield return ("343", "Trái phiếu phát hành", AccountType.Liability, true);
        yield return ("344", "Nhận ký quỹ, ký cược", AccountType.Liability, true);
        yield return ("347", "Thuế TNDN hoãn lại phải trả", AccountType.Liability, true);
        yield return ("352", "Dự phòng phải trả", AccountType.Liability, true);
        yield return ("353", "Quỹ khen thưởng, phúc lợi", AccountType.Liability, true);
        yield return ("356", "Quỹ phát triển KH&CN", AccountType.Liability, true);
        yield return ("357", "Quỹ bình ổn giá", AccountType.Liability, true);

        // Type 4: Vốn chủ sở hữu
        yield return ("411", "Vốn đầu tư của chủ sở hữu", AccountType.Equity, true);
        yield return ("412", "Chênh lệch đánh giá lại tài sản", AccountType.Equity, true);
        yield return ("413", "Chênh lệch tỷ giá hối đoái", AccountType.Equity, true);
        yield return ("414", "Quỹ đầu tư phát triển", AccountType.Equity, true);
        yield return ("418", "Các quỹ khác thuộc vốn chủ sở hữu", AccountType.Equity, true);
        yield return ("419", "Cổ phiếu mua lại của chính công ty", AccountType.Equity, true); // TT 99 renamed
        yield return ("421", "LN sau thuế chưa phân phối", AccountType.Equity, true);

        // Type 5: Doanh thu
        yield return ("511", "Doanh thu bán hàng và cung cấp dịch vụ", AccountType.Revenue, true);
        yield return ("515", "Doanh thu hoạt động tài chính", AccountType.Revenue, true);
        yield return ("521", "Các khoản giảm trừ doanh thu", AccountType.Revenue, false); // F9: contra-revenue (exists in TT 99, NOT TT 133)

        // Type 6: Chi phí
        yield return ("621", "Chi phí NVL trực tiếp", AccountType.Expense, false);
        yield return ("622", "Chi phí nhân công trực tiếp", AccountType.Expense, false);
        yield return ("623", "Chi phí sử dụng máy thi công", AccountType.Expense, false);
        yield return ("627", "Chi phí sản xuất chung", AccountType.Expense, false);
        yield return ("632", "Giá vốn hàng bán", AccountType.Expense, false);
        yield return ("635", "Chi phí tài chính", AccountType.Expense, false);
        yield return ("641", "Chi phí bán hàng", AccountType.Expense, false);
        yield return ("642", "Chi phí quản lý DN", AccountType.Expense, false);

        // Type 7: Thu nhập khác
        yield return ("711", "Thu nhập khác", AccountType.Revenue, true);

        // Type 8: Chi phí khác
        yield return ("811", "Chi phí khác", AccountType.Expense, false);
        yield return ("821", "Chi phí thuế TNDN", AccountType.Expense, false);

        // Type 9: Xác định kết quả
        yield return ("911", "Xác định kết quả", AccountType.Revenue, true);

        // Level-2 accounts for mapper (FIX-6)
        yield return ("1331", "Thuế GTGT đầu vào", AccountType.Asset, false); // FIX-6: level-2 for mapper
    }
}
