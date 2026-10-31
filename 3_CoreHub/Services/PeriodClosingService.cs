using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Infrastructure.Entities;
using VanAn.CoreHub.Repositories;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// Period closing service implementation.
    /// AccountingEntry immutability is preserved — period reopening uses Reversal Entry pattern.
    /// Multi-tenancy enforced at every query.
    ///
    /// W5: Replaced in-memory <c>static Dictionary</c> with DB-persisted <see cref="PeriodClosingStatusEntity"/>.
    /// Period close/reopen state now survives application restarts.
    /// </summary>
    public class PeriodClosingService(
        IAccountingEntryRepository entryRepository,
        IReversalService reversalService,
        IAuditTrailService auditTrailService,
        IAccountingDbContext dbContext,
        ILogger<PeriodClosingService> logger,
        IHKDBookRepository? hkdBookRepository = null,
        IVasFeatureFlagService? vasFeatureFlagService = null,
        Reconcile.IAccountingReconcileService? reconcileService = null) : IPeriodClosingService
    {
        private readonly IAccountingEntryRepository _entryRepository = entryRepository;
        private readonly IReversalService _reversalService = reversalService;
        private readonly IAuditTrailService _auditTrailService = auditTrailService;
        private readonly IAccountingDbContext _dbContext = dbContext;
        private readonly ILogger<PeriodClosingService> _logger = logger;
        // SRS v1.2 W5/F: optional deps — closing JEs (hkdBook), regime (flags), checklist (reconcile).
        private readonly IHKDBookRepository? _hkdBookRepository = hkdBookRepository;
        private readonly IVasFeatureFlagService? _vasFeatureFlagService = vasFeatureFlagService;
        private readonly Reconcile.IAccountingReconcileService? _reconcileService = reconcileService;

        public async Task<PeriodClosingCheckResult> ValidatePeriodAsync(
            AccountingPeriod period,
            TenantId tenantId,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Validating period {Period} for tenant {TenantId}", period, tenantId.Value);

            List<string> errors = [];
            List<string> warnings = [];

            List<AccountingEntry> entries = (await _entryRepository.GetByTenantAndPeriodAsync(tenantId, period, cancellationToken)).ToList();

            if (entries.Count == 0)
            {
                errors.Add($"No accounting entries found for period {period.Year}-{period.Month:D2}");
                return new PeriodClosingCheckResult(false, errors, warnings);
            }

            List<AccountingEntry> revenueEntries = entries.Where(e => e.AccountingBookType == AccountingBookType.RevenueBook).ToList();
            List<AccountingEntry> expenseEntries = entries.Where(e => e.AccountingBookType == AccountingBookType.ExpenseBook).ToList();

            decimal totalRevenue = revenueEntries.Sum(e => e.Amount);
            decimal totalExpense = expenseEntries.Sum(e => e.Amount);

            if (Math.Abs(totalRevenue + totalExpense) > 0.01m && revenueEntries.Count > 0 && expenseEntries.Count > 0)
            {
                warnings.Add($"Revenue/Expense ratio check: Revenue={totalRevenue:N0}, Expense={totalExpense:N0}");
            }

            // W5/F (FR-CLS-003): checklist tra soát trước khóa — Error checks chặn đóng,
            // Warning pass kèm cảnh báo (precedent "cảnh báo không chặn cứng").
            // Fail-safe: reconcile lỗi → warning, KHÔNG chặn đóng kỳ.
            if (_reconcileService != null)
            {
                try
                {
                    var reconcile = await _reconcileService.RunAsync(tenantId, period, cancellationToken);
                    foreach (var check in reconcile.Checks)
                    {
                        if (check.Level == Reconcile.ReconcileLevel.Error)
                        {
                            errors.Add($"{check.Code} {check.Name}: {check.Summary}");
                        }
                        else if (check.Level == Reconcile.ReconcileLevel.Warning)
                        {
                            warnings.Add($"{check.Code} {check.Name}: {check.Summary}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "ValidatePeriod: reconcile checklist failed — warning only");
                    warnings.Add($"Tra soát checklist không chạy được: {ex.Message}");
                }
            }

            _logger.LogInformation("Period {Period} validation: {Errors} errors, {Warnings} warnings",
                period, errors.Count, warnings.Count);
            return new PeriodClosingCheckResult(errors.Count == 0, errors, warnings);
        }

        /// <inheritdoc />
        /// <remarks>
        /// W5/F (KT07-09): kết chuyển cuối kỳ cho regime accrual (TT133/TT99/TT71) —
        /// Nợ 5xx|711|515/Có 911 · Nợ 911/Có 6xx|635|811 · Nợ 911/Có 421 (lãi) hoặc Nợ 421/Có 911 (lỗ).
        /// HKD (TT152) + DNSN (TT58) skip — sổ không kết chuyển (§18.14, card §2.1).
        /// Idempotent: JE purpose "closing" + period — re-run skip nếu đã có closing JE.
        /// Quyết định IMPLEMENT: hardcode per-prefix (không qua closing-* account-map — batch
        /// per-account, resolver là per-event hint; đủ nắp theo regime account sets).
        /// </remarks>
        public async Task<int> GenerateClosingEntriesAsync(
            AccountingPeriod period,
            TenantId tenantId,
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            // Regime gate: chỉ DN vừa/lớn (TT133/TT99) + HTX (TT71) kết chuyển.
            TenantType? type = _vasFeatureFlagService != null
                ? await _vasFeatureFlagService.GetTenantTypeAsync(tenantId, cancellationToken)
                : null;
            if (type is not (TenantType.Enterprise_SME or TenantType.Enterprise_Large or TenantType.HTX))
            {
                _logger.LogInformation(
                    "GenerateClosingEntries: tenant {TenantId} type {Type} — skip (chế độ không kết chuyển 911)",
                    tenantId.Value, type?.ToString() ?? "null");
                return 0;
            }
            if (_hkdBookRepository == null)
            {
                _logger.LogWarning("GenerateClosingEntries: IHKDBookRepository not available — skip");
                return 0;
            }

            DateTime periodStart = new(period.Year, period.Month, 1);
            DateTime periodEnd = periodStart.AddMonths(1);

            // Idempotent: đã có closing JE (purpose=closing, không reversal) trong kỳ → skip.
            bool already = await _dbContext.JournalEntries
                .AsNoTracking()
                .AnyAsync(j => j.TenantId == tenantId
                    && j.EntryDate >= periodStart && j.EntryDate < periodEnd
                    && j.PostingPurpose == AccountingPostingPurposes.Closing
                    && !j.IsReversal, cancellationToken);
            if (already)
            {
                _logger.LogInformation("GenerateClosingEntries: closing JEs already exist for {TenantId} {Period} — skip (idempotent)",
                    tenantId.Value, period);
                return 0;
            }

            // Σ phát sinh per-account trong kỳ (không tính reversal) — client-side group
            // (SQLite không hỗ trợ Sum(decimal) server-side).
            var lines = await _dbContext.JournalEntries
                .Where(j => j.TenantId == tenantId && j.EntryDate >= periodStart && j.EntryDate < periodEnd && !j.IsReversal)
                .SelectMany(j => j.Lines)
                .Select(l => new { l.AccountNumber, l.DebitAmount, l.CreditAmount })
                .ToListAsync(cancellationToken);
            var balances = lines
                .GroupBy(l => l.AccountNumber)
                .Select(g => (Account: g.Key, Net: g.Sum(l => l.DebitAmount - l.CreditAmount)))
                .ToList();

            // Revenue accounts: 5xx (511/512/515) + 711 — số dư CÓ (net âm → đảo Nợ để kết chuyển).
            var revAccounts = balances
                .Where(b => (b.Account.StartsWith('5') || b.Account.StartsWith("711")) && b.Net < 0)
                .ToList();
            // Expense accounts: 6xx (632/641/642/635/611/612/658) + 811 — số dư NỢ.
            var expAccounts = balances
                .Where(b => (b.Account.StartsWith('6') || b.Account.StartsWith("811")) && b.Net > 0)
                .ToList();

            if (revAccounts.Count == 0 && expAccounts.Count == 0)
            {
                _logger.LogInformation("GenerateClosingEntries: no P&L activity for {TenantId} {Period} — nothing to close", tenantId.Value, period);
                return 0;
            }

            DateTime closeDate = periodEnd.AddDays(-1);
            int created = 0;

            // 1) closing-revenue: Nợ 5xx/711 / Có 911 — một JE nhiều dòng.
            if (revAccounts.Count > 0)
            {
                JournalEntry je = new(tenantId, closeDate,
                    $"Kết chuyển doanh thu kỳ {period.Year}-{period.Month:D2}",
                    "PeriodClosing", null, postingPurpose: AccountingPostingPurposes.Closing);
                decimal total = 0;
                foreach (var a in revAccounts)
                {
                    decimal amount = -a.Net; // net âm (dư Có) → Nợ để về 0
                    je.AddLine(a.Account, amount, 0, $"Kết chuyển doanh thu {a.Account}");
                    total += amount;
                }
                je.AddLine("911", 0, total, "Kết chuyển doanh thu → 911");
                await _hkdBookRepository.AddToBookAsync(je, AccountingBookType.S2b_HKD, cancellationToken);
                created++;
            }

            // 2) closing-expense: Nợ 911 / Có 6xx/811.
            if (expAccounts.Count > 0)
            {
                JournalEntry je = new(tenantId, closeDate,
                    $"Kết chuyển chi phí kỳ {period.Year}-{period.Month:D2}",
                    "PeriodClosing", null, postingPurpose: AccountingPostingPurposes.Closing);
                decimal total = 0;
                foreach (var a in expAccounts)
                {
                    je.AddLine("911", a.Net, 0, "Kết chuyển chi phí → 911");
                    je.AddLine(a.Account, 0, a.Net, $"Kết chuyển chi phí {a.Account}");
                    total += a.Net;
                }
                await _hkdBookRepository.AddToBookAsync(je, AccountingBookType.S2b_HKD, cancellationToken);
                created++;
            }

            // 3) closing-result: lãi → Nợ 911/Có 421; lỗ → Nợ 421/Có 911.
            decimal revTotal = revAccounts.Sum(a => -a.Net);
            decimal expTotal = expAccounts.Sum(a => a.Net);
            decimal result = revTotal - expTotal;
            if (result != 0)
            {
                JournalEntry je = new(tenantId, closeDate,
                    $"Kết chuyển kết quả kỳ {period.Year}-{period.Month:D2} ({(result > 0 ? "lãi" : "lỗ")} {Math.Abs(result):N0})",
                    "PeriodClosing", null, postingPurpose: AccountingPostingPurposes.Closing);
                if (result > 0)
                {
                    je.AddLine("911", result, 0, "Lãi → 421");
                    je.AddLine("421", 0, result, "Lãi → 421");
                }
                else
                {
                    je.AddLine("421", -result, 0, "Lỗ → 421");
                    je.AddLine("911", 0, -result, "Lỗ → 421");
                }
                await _hkdBookRepository.AddToBookAsync(je, AccountingBookType.S2b_HKD, cancellationToken);
                created++;
            }

            _logger.LogInformation(
                "GenerateClosingEntries: {Count} closing JEs for {TenantId} {Period} (type {Type}) — revenue {Rev:N0} / expense {Exp:N0} / result {Result:N0}",
                created, tenantId.Value, period, type, revTotal, expTotal, result);
            return created;
        }

        public async Task<ClosingEntry> ClosePeriodAsync(
            AccountingPeriod period,
            TenantId tenantId,
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Closing period {Period} for tenant {TenantId}", period, tenantId.Value);

            PeriodClosingStatusEntity? statusEntity = await GetOrCreateStatusEntityAsync(period, tenantId, cancellationToken);

            if (statusEntity.Status == PeriodClosingStatus.Closed)
            {
                throw new InvalidOperationException($"Period {period.Year}-{period.Month:D2} is already closed.");
            }

            PeriodClosingCheckResult validation = await ValidatePeriodAsync(period, tenantId, cancellationToken);
            if (!validation.IsValid)
            {
                string errorSummary = string.Join("; ", validation.Errors);
                throw new InvalidOperationException($"Period validation failed. Cannot close period with errors: {errorSummary}");
            }

            IEnumerable<AccountingEntry> entries = await _entryRepository.GetByTenantAndPeriodAsync(tenantId, period, cancellationToken);
            int pendingCount = entries.Count(e => e.Amount == 0);
            if (pendingCount > 0)
            {
                throw new InvalidOperationException($"Cannot close period: {pendingCount} pending transactions exist.");
            }

            // W5/F (KT07-09): bút toán kết chuyển 911 TRƯỚC MarkClosed — regime-gated
            // (TT133/TT99/TT71; HKD/TT152 + DNSN/TT58 tự skip). Idempotent per period.
            await GenerateClosingEntriesAsync(period, tenantId, userId, cancellationToken);

            // W5: Persist closed status to DB (survives app restart)
            statusEntity.MarkClosed(userId.ToString());
            _ = await _dbContext.SaveChangesAsync(cancellationToken);

            ClosingEntry closingEntry = new(Guid.NewGuid(), period, DateTime.UtcNow, userId);

            // Audit log: Period closing
            await _auditTrailService.LogPeriodCloseAsync(
                period,
                "Period closed after validation",
                correlationId: closingEntry.PeriodId.ToString(),
                cancellationToken: cancellationToken);

            _logger.LogInformation("Period {Period} closed successfully by user {UserId}", period, userId);

            return closingEntry;
        }

        /// <summary>W4 (P1.2): vai trò được phép mở lại kỳ đã đóng (phê duyệt cấp cao).</summary>
        private static readonly string[] ReopenApproverRoles = ["Owner", "ChiefAccountant", "SystemAdmin"];

        /// <inheritdoc />
        public async Task<ReopenPreview> PreviewReopenAsync(
            AccountingPeriod period,
            TenantId tenantId,
            CancellationToken cancellationToken = default)
        {
            // Non-mutating: chỉ đọc số bút toán + tổng giá trị sẽ bị đảo ngược.
            IEnumerable<AccountingEntry> entries = await _entryRepository.GetByTenantAndPeriodAsync(
                tenantId, period, cancellationToken);
            List<AccountingEntry> list = entries.ToList();
            return new ReopenPreview(list.Count, list.Sum(e => Math.Abs(e.Amount)));
        }

        public async Task ReopenPeriodAsync(
            AccountingPeriod period,
            TenantId tenantId,
            Guid userId,
            string reason,
            IReadOnlyList<string> approverRoles,
            CancellationToken cancellationToken = default)
        {
            // W4 (P1.2 — TT 152/2025): service-level phê duyệt — mở lại kỳ đã đóng là thao tác
            // đảo ngược TOÀN BỘ bút toán, chỉ Owner/ChiefAccountant/SystemAdmin được phép.
            if (approverRoles == null || !approverRoles.Any(r => ReopenApproverRoles.Contains(r)))
            {
                throw new UnauthorizedAccessException(
                    "Mở lại kỳ kế toán yêu cầu phê duyệt của Owner, Kế toán trưởng hoặc SystemAdmin.");
            }
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new ArgumentException("Lý do mở lại kỳ kế toán là bắt buộc.", nameof(reason));
            }

            _logger.LogInformation("Reopening period {Period} for tenant {TenantId}. Reason: {Reason}", period, tenantId.Value, reason);

            // Tracked query (no AsNoTracking) — entity will be modified + saved
            PeriodClosingStatusEntity? statusEntity = await _dbContext.PeriodClosingStatuses
                .FirstOrDefaultAsync(
                    e => e.TenantId == tenantId && e.PeriodYear == period.Year && e.PeriodMonth == period.Month,
                    cancellationToken);
            if (statusEntity == null || statusEntity.Status != PeriodClosingStatus.Closed)
            {
                throw new InvalidOperationException($"Cannot reopen period {period.Year}-{period.Month:D2}: it is not closed.");
            }

            // W5: Persist reopening transition to DB
            statusEntity.MarkReopening(reason);
            _ = await _dbContext.SaveChangesAsync(cancellationToken);

            IEnumerable<AccountingEntry> entries = await _entryRepository.GetByTenantAndPeriodAsync(tenantId, period, cancellationToken);
            foreach (AccountingEntry entry in entries)
            {
                _ = await _reversalService.CreateReversalEntryAsync(
                    new AccountingEntryId(entry.Id),
                    tenantId,
                    $"Period reopening: {reason}",
                    cancellationToken);
            }

            // W5/F (FR-CLS-006): đảo closing JEs của kỳ — audit đầy đủ qua reversal
            // (IsReversal + ReversedJournalId link, purpose "reversal").
            if (_hkdBookRepository != null)
            {
                DateTime reopenStart = new(period.Year, period.Month, 1);
                DateTime reopenEnd = reopenStart.AddMonths(1);
                var closingJes = await _dbContext.JournalEntries
                    .Where(j => j.TenantId == tenantId
                        && j.EntryDate >= reopenStart && j.EntryDate < reopenEnd
                        && j.PostingPurpose == AccountingPostingPurposes.Closing
                        && !j.IsReversal)
                    .ToListAsync(cancellationToken);
                foreach (var closingJe in closingJes)
                {
                    JournalEntry reversal = closingJe.CreateReversal($"Mở lại kỳ {period.Year}-{period.Month:D2}: {reason}");
                    await _hkdBookRepository.AddToBookAsync(reversal, AccountingBookType.S2b_HKD, cancellationToken);
                }
                if (closingJes.Count > 0)
                {
                    _logger.LogInformation("Reopened period {Period}: {Count} closing JEs reversed", period, closingJes.Count);
                }
            }

            // W5: Complete reopen transition (Reopening → Open)
            statusEntity.MarkReopened();
            _ = await _dbContext.SaveChangesAsync(cancellationToken);

            // Audit log: Period reopening
            await _auditTrailService.LogPeriodReopenAsync(
                period,
                reason,
                cancellationToken: cancellationToken);

            _logger.LogInformation("Period {Period} reopened by user {UserId}", period, userId);
        }

        public async Task<PeriodClosingStatus> GetPeriodStatusAsync(
            AccountingPeriod period,
            TenantId tenantId,
            CancellationToken cancellationToken = default)
        {
            PeriodClosingStatusEntity? statusEntity = await GetStatusEntityAsync(period, tenantId, cancellationToken);
            return statusEntity?.Status ?? PeriodClosingStatus.Open;
        }

        /// <summary>
        /// Query the DB for an existing period status record. Returns null if not found (period is implicitly Open).
        /// </summary>
        private async Task<PeriodClosingStatusEntity?> GetStatusEntityAsync(
            AccountingPeriod period,
            TenantId tenantId,
            CancellationToken cancellationToken)
        {
            return await _dbContext.PeriodClosingStatuses
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    e => e.TenantId == tenantId && e.PeriodYear == period.Year && e.PeriodMonth == period.Month,
                    cancellationToken);
        }

        /// <summary>
        /// Get an existing status entity for update (tracked), or create a new Open-status entity if none exists.
        /// The new entity is NOT yet saved — caller must call SaveChangesAsync after mutation.
        /// </summary>
        private async Task<PeriodClosingStatusEntity> GetOrCreateStatusEntityAsync(
            AccountingPeriod period,
            TenantId tenantId,
            CancellationToken cancellationToken)
        {
            PeriodClosingStatusEntity? existing = await _dbContext.PeriodClosingStatuses
                .FirstOrDefaultAsync(
                    e => e.TenantId == tenantId && e.PeriodYear == period.Year && e.PeriodMonth == period.Month,
                    cancellationToken);

            if (existing != null)
            {
                return existing;
            }

            PeriodClosingStatusEntity created = new(tenantId, period.Year, period.Month);
            _ = await _dbContext.PeriodClosingStatuses.AddAsync(created, cancellationToken);
            _ = await _dbContext.SaveChangesAsync(cancellationToken);
            return created;
        }
    }
}
