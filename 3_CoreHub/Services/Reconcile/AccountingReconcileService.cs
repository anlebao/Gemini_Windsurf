using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Audit;
using TenantEntity = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant;

namespace VanAn.CoreHub.Services.Reconcile;

/// <summary>
/// Tra soát số liệu kế toán (SRS v1.1) — READ-ONLY tuyệt đối (NFR-1: không Add/Update/Save).
/// Port C1-C10 từ `6_Testing/rv-scripts/accounting-balance-rv.sh` (đã validate production
/// 2026-10-29 — 18 PASS sau cleanup 114 journal duplicate). Tolerance tiền tệ 0.01.
/// Semantic đã xác minh: VAT 3331 EntryType=1 nhưng KHÔNG tạo journal (bridge chỉ 5/7, 6/8);
/// offset cọc EntryType=5 (Nợ 3387/Có 511) — journal credit 5/7; legacy Order-ref journals
/// pre-2026-10-29 là data lịch sử hợp lệ (R8a chỉ bắt sau ngày fix).
/// Fail-safe: từng check try/catch riêng → check error + log, không chặn check khác (NFR-4).
/// </summary>
public sealed class AccountingReconcileService(
    IAccountingDbContext accountingDb,
    IVanAnDbContext db,
    IAuditTrailService auditTrailService,
    ILogger<AccountingReconcileService> logger,
    IMirrorOrderProvider? mirrorProvider = null,
    IReconciliationItemService? itemService = null) : IAccountingReconcileService
{
    private const decimal Tolerance = 0.01m;
    private static readonly DateTime BridgeSingleSourceSince = new(2026, 10, 29); // R8a — ngày bỏ journal trực tiếp

    public async Task<ReconcileResult> RunAsync(TenantId tenantId, AccountingPeriod period, CancellationToken ct = default)
    {
        DateTime ranAt = DateTime.UtcNow;
        string periodStr = $"{period.Year:D4}-{period.Month:D2}";
        var checks = new List<ReconcileCheck>();
        var suggestions = new List<string>();

        TenantEntity? tenant = await db.Tenants.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId, ct);
        string tenantName = tenant?.Name ?? tenantId.Value.ToString("D");

        // ── Load dữ liệu 1 lần (NFR-2 — gộp query) ──────────────────────────
        List<JournalEntry> periodJournals = await accountingDb.JournalEntries.IgnoreQueryFilters()
            .Where(j => j.TenantId == tenantId && j.EntryDate >= period.StartDate && j.EntryDate < period.EndDate)
            .Include(j => j.Lines)
            .OrderBy(j => j.EntryDate)
            .ToListAsync(ct);
        List<JournalEntryLine> allLines = await accountingDb.JournalEntries.IgnoreQueryFilters().AsNoTracking()
            .Where(j => j.TenantId == tenantId)
            .SelectMany(j => j.Lines)
            .ToListAsync(ct);
        List<JournalEntryLine> beforeLines = await accountingDb.JournalEntries.IgnoreQueryFilters().AsNoTracking()
            .Where(j => j.TenantId == tenantId && j.EntryDate < period.StartDate)
            .SelectMany(j => j.Lines)
            .ToListAsync(ct);
        List<AccountingEntry> periodEntries = await accountingDb.AccountingEntries.IgnoreQueryFilters()
            .Where(e => e.TenantId == tenantId && e.TransactionDate >= period.StartDate && e.TransactionDate < period.EndDate)
            .ToListAsync(ct);
        List<Order> paidOrders = await db.Orders.IgnoreQueryFilters()
            .Where(o => o.TenantId == tenantId && o.PaymentStatus == "Paid"
                && o.CreatedAt >= period.StartDate && o.CreatedAt < period.EndDate)
            .ToListAsync(ct);

        // Entries của các order paid kỳ (R8b/R10) — Reference = orderId hoặc orderId-DEPOSIT-OFFSET
        var orderRefs = paidOrders.SelectMany(o => new[] { o.Id.ToString(), $"{o.Id}-DEPOSIT-OFFSET" }).ToHashSet();
        List<AccountingEntry> orderEntries = await accountingDb.AccountingEntries.IgnoreQueryFilters()
            .Where(e => e.TenantId == tenantId && orderRefs.Contains(e.Reference))
            .ToListAsync(ct);
        var orderEntryIds = orderEntries.Select(e => e.Id).ToHashSet();
        List<JournalEntry> journalsByOrderEntries = await accountingDb.JournalEntries.IgnoreQueryFilters()
            .Where(j => j.TenantId == tenantId && j.ReferenceId != null && orderEntryIds.Contains(j.ReferenceId.Value))
            .ToListAsync(ct);

        // ── R1. Journal cân bằng ─────────────────────────────────────────────
        await RunSafeAsync("R1", "Journal cân bằng (ΣNợ == ΣCó per journal)", checks, suggestions, async () =>
        {
            var unbalanced = periodJournals
                .Select(j => new { j.Id, D = j.Lines.Sum(l => l.DebitAmount), C = j.Lines.Sum(l => l.CreditAmount) })
                .Where(x => Math.Abs(x.D - x.C) > Tolerance)
                .ToList();
            if (unbalanced.Count == 0)
                checks.Add(new("R1", "Journal cân bằng", ReconcileLevel.Pass, $"0/{periodJournals.Count} journal lệch", ""));
            else
            {
                checks.Add(new("R1", "Journal cân bằng", ReconcileLevel.Error,
                    $"{unbalanced.Count}/{periodJournals.Count} journal lệch Nợ/Có",
                    string.Join("; ", unbalanced.Take(20).Select(u => $"{u.Id}: Nợ {u.D:N0} / Có {u.C:N0}"))));
                suggestions.Add("R1: Kiểm tra các journal lệch — bút toán không cân đối (xem danh sách chi tiết).");
            }
        });

        // ── R2. Trial balance kỳ ─────────────────────────────────────────────
        await RunSafeAsync("R2", "Trial balance kỳ (ΣNợ == ΣCó)", checks, suggestions, () =>
        {
            decimal td = periodJournals.SelectMany(j => j.Lines).Sum(l => l.DebitAmount);
            decimal tc = periodJournals.SelectMany(j => j.Lines).Sum(l => l.CreditAmount);
            bool ok = Math.Abs(td - tc) <= Tolerance;
            checks.Add(new("R2", "Trial balance kỳ", ok ? ReconcileLevel.Pass : ReconcileLevel.Error,
                $"ΣNợ={td:N0} · ΣCó={tc:N0} · diff={Math.Abs(td - tc):N2}", ""));
            if (!ok) suggestions.Add("R2: Tổng Nợ ≠ Tổng Có trong kỳ — dữ liệu journal không cân đối.");
        });

        // ── R3. Revenue/Expense đồng bộ ──────────────────────────────────────
        await RunSafeAsync("R3", "Doanh thu/Chi phí đồng bộ (journals ↔ entries)", checks, suggestions, async () =>
        {
            var periodLines = periodJournals.SelectMany(j => j.Lines).ToList();
            // Doanh thu/chi phí theo phát sinh THUẦN (Có−Nợ cho 5xx/7xx; Nợ−Có cho 6xx/8xx) —
            // bút toán đảo ManualReversal/contra phải triệt tiêu, đúng bản chất kế toán.
            // Bug thật (Vạn An Cafe 2026-10): phiếu thu test + reversal 778,480 → revJ gross
            // chỉ cộng credit → dư ảo +778,480 dù số liệu đúng.
            decimal revJ = periodLines.Where(l => AccountClassifier.Classify(l.AccountNumber) == AccountGroup.Revenue).Sum(l => l.CreditAmount - l.DebitAmount);
            decimal expJ = periodLines.Where(l => AccountClassifier.Classify(l.AccountNumber) == AccountGroup.Expense).Sum(l => l.DebitAmount - l.CreditAmount);
            decimal revE = periodEntries.Where(e => e.EntryType == AccountingEntryType.Revenue && AccountClassifier.Classify(e.AccountCode) == AccountGroup.Revenue).Sum(e => e.Amount);
            decimal offE = periodEntries.Where(e => e.EntryType == AccountingEntryType.DepositOffset && AccountClassifier.Classify(e.AccountCode) == AccountGroup.Revenue).Sum(e => e.Amount);
            decimal vatE = periodEntries.Where(e => e.EntryType == AccountingEntryType.Revenue && e.AccountCode == "3331").Sum(e => e.Amount);
            decimal expE = periodEntries.Where(e => e.EntryType == AccountingEntryType.Expense && AccountClassifier.Classify(e.AccountCode) == AccountGroup.Expense).Sum(e => e.Amount);
            decimal dRev = Math.Abs(revJ - (revE + offE));
            decimal dExp = Math.Abs(expJ - expE);
            bool ok = dRev <= Tolerance && dExp <= Tolerance;

            string detail = "";
            if (!ok)
            {
                // Drill-down per-document: chỉ ra journal/entry nào gây lệch (map journal.ReferenceId → AccountingEntries.Id).
                var entryIdsInPeriod = periodEntries.Select(e => e.Id).ToHashSet();
                var danglingRefIds = periodJournals
                    .Where(j => j.ReferenceId != null && !entryIdsInPeriod.Contains(j.ReferenceId.Value))
                    .Select(j => j.ReferenceId!.Value).Distinct().ToList();
                var extraEntries = danglingRefIds.Count == 0 ? new List<AccountingEntry>()
                    : await accountingDb.AccountingEntries.IgnoreQueryFilters()
                        .Where(e => e.TenantId == tenantId && danglingRefIds.Contains(e.Id)).ToListAsync(ct);
                var entryById = periodEntries.Concat(extraEntries).ToDictionary(e => e.Id);

                var jOrphans = new List<string>();
                var jAmountDiffs = new List<string>();
                foreach (var j in periodJournals)
                {
                    decimal r = j.Lines.Where(l => AccountClassifier.Classify(l.AccountNumber) == AccountGroup.Revenue).Sum(l => l.CreditAmount - l.DebitAmount);
                    decimal x = j.Lines.Where(l => AccountClassifier.Classify(l.AccountNumber) == AccountGroup.Expense).Sum(l => l.DebitAmount - l.CreditAmount);
                    if (r == 0 && x == 0) continue;
                    if (j.ReferenceId != null && entryById.TryGetValue(j.ReferenceId.Value, out var e))
                    {
                        decimal side = AccountClassifier.Classify(e.AccountCode) == AccountGroup.Revenue ? r : x;
                        if (Math.Abs(side - e.Amount) > Tolerance)
                            jAmountDiffs.Add($"{j.EntryDate:yyyy-MM-dd} journal {j.Id}: {side:N0} ≠ entry {e.AccountCode} {e.Amount:N0} (ref={e.Reference})");
                    }
                    else
                    {
                        string kind = j.ReferenceType switch
                        {
                            "Order" => "journal trực tiếp ref 'Order' (legacy)",
                            "ManualReversal" => "bút toán đảo",
                            "ManualEntry" => "entry đã bị xóa/tham chiếu sai",
                            "OpeningBalance" => "số dư đầu kỳ",
                            _ => "phiếu nhập tay không qua AccountingEntry",
                        };
                        jOrphans.Add($"{j.EntryDate:yyyy-MM-dd} journal {j.Id}: rev {r:N0}/exp {x:N0} — {kind}");
                    }
                }

                var revExpEntries = periodEntries.Where(e =>
                        (e.EntryType is AccountingEntryType.Revenue or AccountingEntryType.DepositOffset
                            && AccountClassifier.Classify(e.AccountCode) == AccountGroup.Revenue)
                        || (e.EntryType == AccountingEntryType.Expense && AccountClassifier.Classify(e.AccountCode) == AccountGroup.Expense))
                    .ToList();
                var refEntryIds = revExpEntries.Select(e => e.Id).ToList();
                var jRefs = await accountingDb.JournalEntries.IgnoreQueryFilters()
                    .Where(j => j.TenantId == tenantId && j.ReferenceId != null && refEntryIds.Contains(j.ReferenceId.Value))
                    .Select(j => new { j.Id, j.ReferenceId, j.EntryDate }).ToListAsync(ct);
                var inPeriodJIds = periodJournals.Select(j => j.Id).ToHashSet();
                var eMissing = new List<string>();
                var eOutPeriod = new List<string>();
                foreach (var e in revExpEntries)
                {
                    var js = jRefs.Where(j => j.ReferenceId == e.Id).ToList();
                    if (js.Count == 0)
                        eMissing.Add($"{e.TransactionDate:yyyy-MM-dd} entry {e.Id}: {e.AccountCode} {e.Amount:N0} (ref={e.Reference}) — không có journal nào");
                    else if (!js.Any(j => inPeriodJIds.Contains(j.Id)))
                        eOutPeriod.Add($"{e.TransactionDate:yyyy-MM-dd} entry {e.Id}: {e.AccountCode} {e.Amount:N0} (ref={e.Reference}) — journal ngoài kỳ (lệch EntryDate vs TransactionDate)");
                }

                var parts = new List<string>();
                if (jOrphans.Count > 0) parts.Add($"● Journal KHÔNG có entry tương ứng ({jOrphans.Count}):\n  " + string.Join("\n  ", jOrphans.Take(15)));
                if (jAmountDiffs.Count > 0) parts.Add($"● Journal ≠ entry số tiền ({jAmountDiffs.Count}):\n  " + string.Join("\n  ", jAmountDiffs.Take(10)));
                if (eMissing.Count > 0) parts.Add($"● Entry THIẾU journal ({eMissing.Count}):\n  " + string.Join("\n  ", eMissing.Take(15)));
                if (eOutPeriod.Count > 0) parts.Add($"● Entry có journal NGOÀI kỳ ({eOutPeriod.Count}):\n  " + string.Join("\n  ", eOutPeriod.Take(10)));
                detail = string.Join("\n", parts);

                string tip = jOrphans.Count >= eMissing.Count && jOrphans.Count > 0
                    ? "journal không qua AccountingEntry (phiếu tay/legacy/reversal) — tạo AccountingEntry bù cho journal, hoặc xóa journal thừa (backup trước)"
                    : eMissing.Count > 0
                        ? "entry thiếu journal — chạy bridge tạo journal cho các entry trong danh sách"
                        : "lệch số tiền journal↔entry hoặc journal ngoài kỳ — đối chiếu từng dòng trong chi tiết";
                suggestions.Add($"R3: Lệch {Math.Max(dRev, dExp):N0} — nguồn chính: {tip}. Mở chi tiết R3 xem journal/entry cụ thể.");
            }

            checks.Add(new("R3", "Doanh thu/Chi phí đồng bộ", ok ? ReconcileLevel.Pass : ReconcileLevel.Error,
                $"Revenue journal={revJ:N0} = entries {revE:N0} + offset {offE:N0} (VAT 3331={vatE:N0}) · Expense journal={expJ:N0} = entries {expE:N0} · diff rev={dRev:N2} exp={dExp:N2}", detail));
        });

        // ── R4. Balance identity (toàn lịch sử) + sub-check G7 ───────────────
        await RunSafeAsync("R4", "Balance identity (Tài sản = Nợ + Vốn + Lãi)", checks, suggestions, () =>
        {
            decimal A = allLines.Where(l => AccountClassifier.Classify(l.AccountNumber) == AccountGroup.Asset).Sum(l => l.DebitAmount - l.CreditAmount);
            decimal L = allLines.Where(l => AccountClassifier.Classify(l.AccountNumber) == AccountGroup.Liability).Sum(l => l.CreditAmount - l.DebitAmount);
            decimal E = allLines.Where(l => AccountClassifier.Classify(l.AccountNumber) == AccountGroup.Equity).Sum(l => l.CreditAmount - l.DebitAmount);
            decimal P = allLines.Where(l => AccountClassifier.Classify(l.AccountNumber) == AccountGroup.Revenue).Sum(l => l.CreditAmount - l.DebitAmount);
            decimal X = allLines.Where(l => AccountClassifier.Classify(l.AccountNumber) == AccountGroup.Expense).Sum(l => l.DebitAmount - l.CreditAmount);
            decimal net = P - X;
            decimal rhs = L + E + net;
            decimal diff = Math.Abs(A - rhs);
            var review = allLines.Select(l => l.AccountNumber).Distinct()
                .Where(c => !AccountClassifier.IsRecognizedForBalanceIdentity(c))
                .Take(20).ToList();
            bool ok = diff <= Tolerance;
            checks.Add(new("R4", "Balance identity", ok ? ReconcileLevel.Pass : ReconcileLevel.Error,
                $"Tài sản={A:N0} · Nợ={L:N0} · Vốn={E:N0} · Lãi={net:N0} → Nợ+Vốn+Lãi={rhs:N0} · diff={diff:N2}", ""));
            if (review.Count > 0)
            {
                checks.Add(new("R4-sub", "Account ngoài prefix 1-8 / malformed", ReconcileLevel.Warning,
                    $"{review.Count} tài khoản cần review: {string.Join(", ", review)}",
                    string.Join("\n", review.Select(AccountClassifier.Describe))));
                suggestions.Add("R4: Tồn tại tài khoản ngoài prefix 1-8 hoặc malformed — xem chi tiết (9xx = kết chuyển 911 cần mở rộng NET gộp 9xx).");
            }
            if (!ok) suggestions.Add("R4: Tài sản ≠ Nợ + Vốn + Lãi — kiểm tra journal lệch hoặc account chưa phân loại.");
        });

        // ── R5. Sổ cái ───────────────────────────────────────────────────────
        await RunSafeAsync("R5", "Sổ cái (key accounts)", checks, suggestions, () =>
        {
            string[] keys = ["111", "112", "131", "156", "331", "3387", "421", "511", "632", "3331"];
            var rows = new List<string>();
            foreach (string ac in keys)
            {
                decimal op = beforeLines.Where(l => l.AccountNumber == ac).Sum(l => l.DebitAmount - l.CreditAmount);
                decimal total = allLines.Where(l => l.AccountNumber == ac).Sum(l => l.DebitAmount - l.CreditAmount);
                rows.Add($"{ac}: đầu={op:N0} → cuối={total:N0}");
            }
            checks.Add(new("R5", "Sổ cái (key accounts)", ReconcileLevel.Info, string.Join(" · ", rows), string.Join("\n", rows)));
        });

        // ── R6. Nhật ký chung ────────────────────────────────────────────────
        await RunSafeAsync("R6", "Nhật ký chung (period)", checks, suggestions, () =>
        {
            decimal td = periodJournals.SelectMany(j => j.Lines).Sum(l => l.DebitAmount);
            decimal tc = periodJournals.SelectMany(j => j.Lines).Sum(l => l.CreditAmount);
            bool ok = Math.Abs(td - tc) <= Tolerance;
            checks.Add(new("R6", "Nhật ký chung", ok ? ReconcileLevel.Pass : ReconcileLevel.Error,
                $"{periodJournals.Count} journal · ΣNợ={td:N0} · ΣCó={tc:N0}", ""));
            if (!ok) suggestions.Add("R6: Nhật ký chung không cân đối — kiểm tra journal kỳ.");
        });

        // ── R7. Sổ HKD (chỉ tenant HKD) ──────────────────────────────────────
        await RunSafeAsync("R7", "Sổ HKD (tenant HKD)", checks, suggestions, () =>
        {
            if (tenant?.Type != TenantType.HKD)
            {
                checks.Add(new("R7", "Sổ HKD", ReconcileLevel.Info, "Tenant không phải HKD — bỏ qua (dùng R4/R6 BCTC)", ""));
                return;
            }
            if (periodJournals.Count == 0)
            {
                checks.Add(new("R7", "Sổ HKD", ReconcileLevel.Warning, "Tenant HKD không có journal trong kỳ", ""));
                suggestions.Add("R7: Tenant HKD không có journal kỳ — kiểm tra nghiệp vụ kỳ (không có đơn/phiếu?).");
                return;
            }
            decimal revJ = periodJournals.SelectMany(j => j.Lines)
                .Where(l => AccountClassifier.Classify(l.AccountNumber) == AccountGroup.Revenue).Sum(l => l.CreditAmount - l.DebitAmount);
            checks.Add(new("R7", "Sổ HKD", ReconcileLevel.Pass,
                $"S1a/S2b/S3a = {periodJournals.Count} journal · revenue 5xx/7xx net = {revJ:N0} (khớp R3)", ""));
        });

        // ── R8. Single-source bridge ─────────────────────────────────────────
        await RunSafeAsync("R8", "Single-source bridge (chống double-revenue)", checks, suggestions, async () =>
        {
            int orderRefAfterFix = await accountingDb.JournalEntries.IgnoreQueryFilters()
                .CountAsync(j => j.TenantId == tenantId && j.ReferenceType == "Order"
                    && j.EntryDate >= BridgeSingleSourceSince, ct);
            bool okA = orderRefAfterFix == 0;
            checks.Add(new("R8a", "Không journal ref 'Order' sau ngày fix", okA ? ReconcileLevel.Pass : ReconcileLevel.Error,
                $"journal ref 'Order' từ {BridgeSingleSourceSince:yyyy-MM-dd} = {orderRefAfterFix} (expect 0)", ""));
            if (!okA) suggestions.Add("R8a: Có journal ref 'Order' mới — đường journal trực tiếp đã bị bỏ, kiểm tra code/backfill.");

            var bad = new List<string>();
            foreach (var o in paidOrders)
            {
                var refs = new[] { o.Id.ToString(), $"{o.Id}-DEPOSIT-OFFSET" };
                var entries = orderEntries.Where(e => refs.Contains(e.Reference)).Select(e => e.Id).ToHashSet();
                int n = journalsByOrderEntries.Count(j => entries.Contains(j.ReferenceId!.Value));
                if (n is not (2 or 3)) bad.Add($"{o.Id}: {n} journal (expect 2-3)");
            }
            checks.Add(new("R8b", "Journal đúng 1 bộ/order paid", bad.Count == 0 ? ReconcileLevel.Pass : ReconcileLevel.Error,
                $"{paidOrders.Count} order paid kỳ · lệch: {bad.Count}", string.Join("; ", bad.Take(20))));
            if (bad.Count > 0) suggestions.Add("R8b: Order paid có số journal lệch — kiểm tra double journal (xóa journal ref 'Order' trùng) hoặc thiếu journal.");
        });

        // ── R9. Deposit 3387 residual ────────────────────────────────────────
        await RunSafeAsync("R9", "Deposit 3387 residual", checks, suggestions, () =>
        {
            decimal residual = allLines.Where(l => l.AccountNumber == "3387").Sum(l => l.CreditAmount - l.DebitAmount);
            checks.Add(new("R9", "Deposit 3387 residual", ReconcileLevel.Info,
                $"3387 (credit − debit, toàn lịch sử) = {residual:N0} (0 = mọi cọc đã đối trừ; khác 0 = cọc đã thu chưa thanh toán cuối / thiếu receipt journal)", ""));
        });

        // ── R10. Order → entries coverage ────────────────────────────────────
        await RunSafeAsync("R10", "Order paid → entries coverage", checks, suggestions, () =>
        {
            var bad = new List<string>();
            foreach (var o in paidOrders)
            {
                var refs = new[] { o.Id.ToString(), $"{o.Id}-DEPOSIT-OFFSET" };
                var entries = orderEntries.Where(e => refs.Contains(e.Reference)).ToList();
                bool hasRevenue = entries.Any(e => AccountClassifier.Classify(e.AccountCode) == AccountGroup.Revenue);
                bool hasVat = o.TotalVatAmount <= 0 || entries.Any(e => e.AccountCode == "3331");
                bool hasCogs = entries.Any(e => AccountClassifier.Classify(e.AccountCode) == AccountGroup.Expense);
                bool hasOffset = o.DepositPaidAmount <= 0 || entries.Any(e => e.EntryType == AccountingEntryType.DepositOffset);
                if (!(hasRevenue && hasVat && hasCogs && hasOffset))
                    bad.Add($"{o.Id}: revenue={hasRevenue} vat={hasVat} cogs={hasCogs} offset={hasOffset}");
            }
            checks.Add(new("R10", "Order paid → entries", bad.Count == 0 ? ReconcileLevel.Pass : ReconcileLevel.Error,
                $"{paidOrders.Count} order paid kỳ · thiếu entries: {bad.Count}", string.Join("; ", bad.Take(20))));
            if (bad.Count > 0) suggestions.Add("R10: Order paid thiếu entries kế toán (511/3331/632/offset) — kiểm tra luồng GenerateAccountingEntriesAsync.");
        });

        // ── R11. Cross-store PG ↔ SQLite mirror ──────────────────────────────
        await RunSafeAsync("R11", "Cross-store PG ↔ SQLite mirror", checks, suggestions, async () =>
        {
            if (mirrorProvider is null)
            {
                checks.Add(new("R11", "Cross-store", ReconcileLevel.Info, "MirrorOrderProvider chưa cấu hình — bỏ qua", ""));
                return;
            }
            IReadOnlyList<MirrorOrderDto>? mirror = await mirrorProvider.GetMirrorOrdersAsync(periodStr, ct);
            if (mirror is null)
            {
                checks.Add(new("R11", "Cross-store", ReconcileLevel.Info, "Không đọc được SQLite mirror (HTTP lỗi) — bỏ qua", ""));
                return;
            }
            var mirrorById = mirror.ToDictionary(m => m.OrderId);
            var bad = new List<string>();
            foreach (var o in paidOrders)
            {
                if (!mirrorById.TryGetValue(o.Id, out var m))
                { bad.Add($"{o.Id}: thiếu trong mirror"); continue; }
                if (m.PaymentStatus != o.PaymentStatus || m.Status != o.Status.Value
                    || Math.Abs(m.DepositPaidAmount - o.DepositPaidAmount) > Tolerance)
                    bad.Add($"{o.Id}: mirror {m.PaymentStatus}/{m.Status}/cọc {m.DepositPaidAmount:N0} ≠ PG {o.PaymentStatus}/{o.Status.Value}/cọc {o.DepositPaidAmount:N0}");
            }
            checks.Add(new("R11", "Cross-store", bad.Count == 0 ? ReconcileLevel.Pass : ReconcileLevel.Warning,
                $"{paidOrders.Count} order paid kỳ · lệch mirror: {bad.Count}", string.Join("; ", bad.Take(20))));
            if (bad.Count > 0) suggestions.Add("R11: Order lệch SQLite mirror — kiểm tra NATS sync (OrderSyncSubscriber).");
        });

        // ── R12. Ngày nghiệp vụ ──────────────────────────────────────────────
        await RunSafeAsync("R12", "Ngày nghiệp vụ (TransactionDate)", checks, suggestions, async () =>
        {
            // Query toàn lịch sử (không filter period — date xấu 01-01-0001 nằm ngoài mọi range).
            var bad = await accountingDb.AccountingEntries.IgnoreQueryFilters()
                .Where(e => e.TenantId == tenantId && (e.TransactionDate.Year < 1900 || e.TransactionDate == default))
                .Select(e => e.Id)
                .ToListAsync(ct);
            int total = await accountingDb.AccountingEntries.IgnoreQueryFilters()
                .CountAsync(e => e.TenantId == tenantId, ct);
            checks.Add(new("R12", "Ngày nghiệp vụ", bad.Count == 0 ? ReconcileLevel.Pass : ReconcileLevel.Warning,
                $"{bad.Count}/{total} entries ngày nghiệp vụ không hợp lệ (01-01-0001/-infinity)",
                string.Join("; ", bad.Take(20).Select(id => id.ToString()))));
            if (bad.Count > 0) suggestions.Add("R12: Entries có TransactionDate xấu — chạy backfill ngày nghiệp vụ (đã làm 1 lần 2026-10-08, còn sót data cũ).");
        });

        // ── Audit (G12 — không lưu lịch sử, chỉ audit log) ───────────────────
        try
        {
            var summary = Summarize(checks);
            await auditTrailService.LogCreateAsync(AuditableEntityType.Tenant, tenantId.Value,
                System.Text.Json.JsonSerializer.Serialize(new { period = periodStr, checks = checks.Count, error = summary.Error, warning = summary.Warning }),
                correlationId: $"{tenantId.Value}:{periodStr}:reconcile");
        }
        catch (Exception ex) { logger.LogWarning(ex, "Reconcile audit log failed (non-blocking): tenant={Tenant}", tenantId.Value); }

        var result = new ReconcileResult(periodStr, tenantId.Value, tenantName, ranAt, Summarize(checks), checks, suggestions);

        // W5/F (FR-RECNC-005): emit ReconciliationItems cho check Warning/Error
        // (dedup per check+period) + auto-resolve check đã pass — fail-safe không
        // chặn kết quả tra soát (read-path vẫn pure, emit là side effect tách).
        if (itemService != null)
        {
            try
            {
                await itemService.EmitFromReconcileAsync(result, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Reconcile emit items failed — result vẫn trả về");
            }
        }

        return result;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static ReconcileSummary Summarize(IReadOnlyList<ReconcileCheck> checks)
        => new(
            checks.Count(c => c.Level == ReconcileLevel.Pass),
            checks.Count(c => c.Level == ReconcileLevel.Info),
            checks.Count(c => c.Level == ReconcileLevel.Warning),
            checks.Count(c => c.Level == ReconcileLevel.Error),
            checks.Count);

    /// <summary>Fail-safe: 1 check exception → check error + log, KHÔNG chặn check khác (NFR-4).</summary>
    private async Task RunSafeAsync(string code, string name, List<ReconcileCheck> checks, List<string> suggestions,
        Func<Task> action)
    {
        try { await action(); }
        catch (Exception ex)
        {
            logger.LogError(ex, "Reconcile check {Code} failed (fail-safe): {Name}", code, name);
            checks.Add(new(code, name, ReconcileLevel.Error, "Check không chạy được (exception)", ex.Message));
        }
    }

    /// <summary>Overload cho check đồng bộ (không await).</summary>
    private async Task RunSafeAsync(string code, string name, List<ReconcileCheck> checks, List<string> suggestions,
        Action action)
        => await RunSafeAsync(code, name, checks, suggestions, () => { action(); return Task.CompletedTask; });
}