using Microsoft.Extensions.Logging;
using VanAn.Shared.Domain;
using VanAn.CoreHub.Repositories;
using CoreAccountingEntry = VanAn.Shared.Domain.AccountingEntry;

namespace VanAn.CoreHub.Services.Journal
{
    /// <summary>
    /// NHẬP LIỆU & SỔ SÁCH P2 (#4): backfill JournalEntry cho các phiếu thu/chi NHẬP TAY
    /// tồn tại TRƯỚC khi P2 deploy (chưa từng tạo JE → không vào Sổ HKD B01-B09 / BCTC).
    /// - Preview (dry-run): đếm phiếu eligible (EntryType Revenue/Expense, AccountCode 5xx/6xx/7xx,
    ///   chưa đảo, chưa có JE theo ReferenceType "ManualEntry") — KHÔNG ghi gì.
    /// - Run: tạo JE qua ManualEntryJournalBridge (idempotent — chạy lại an toàn) + đối chiếu đếm.
    /// KHÔNG tự chạy khi startup — gọi qua endpoint SystemAdmin/tenant-scoped (dryRun mặc định true).
    /// </summary>
    public interface IBackfillJournalEntriesService
    {
        Task<BackfillJournalEntriesResult> PreviewAsync(TenantId tenantId, CancellationToken cancellationToken = default);
        Task<BackfillJournalEntriesResult> RunAsync(TenantId tenantId, CancellationToken cancellationToken = default);
    }

    public record BackfillJournalEntriesResult(int EligibleCount, int CreatedCount, int SkippedCount, string Message);

    public sealed class BackfillJournalEntriesService(
        IAccountingEntryRepository accountingEntryRepository,
        IHKDBookRepository hkdBookRepository,
        IManualEntryJournalBridge journalBridge,
        ILogger<BackfillJournalEntriesService> logger) : IBackfillJournalEntriesService
    {
        private readonly IAccountingEntryRepository _accountingEntryRepository = accountingEntryRepository;
        private readonly IHKDBookRepository _hkdBookRepository = hkdBookRepository;
        private readonly IManualEntryJournalBridge _journalBridge = journalBridge;
        private readonly ILogger<BackfillJournalEntriesService> _logger = logger;

        /// <inheritdoc />
        public async Task<BackfillJournalEntriesResult> PreviewAsync(TenantId tenantId, CancellationToken cancellationToken = default)
        {
            List<CoreAccountingEntry> eligible = await GetEligibleWithoutJournalEntryAsync(tenantId, cancellationToken);
            return new BackfillJournalEntriesResult(
                eligible.Count,
                0,
                0,
                $"Dry-run: {eligible.Count} phiếu thu/chi (5xx/6xx/7xx, chưa đảo, chưa có JE) chờ backfill. Chạy Run để tạo JournalEntry.");
        }

        /// <inheritdoc />
        public async Task<BackfillJournalEntriesResult> RunAsync(TenantId tenantId, CancellationToken cancellationToken = default)
        {
            List<CoreAccountingEntry> eligible = await GetEligibleWithoutJournalEntryAsync(tenantId, cancellationToken);

            int created = 0;
            foreach (CoreAccountingEntry entry in eligible)
            {
                if (await _journalBridge.CreateForAccountingEntryAsync(entry, cancellationToken))
                {
                    created++;
                }
            }

            _logger.LogInformation("Backfill JE: {Created}/{Eligible} cho tenant {TenantId} (skip {Skipped})",
                created, eligible.Count, tenantId.Value, eligible.Count - created);

            return new BackfillJournalEntriesResult(
                eligible.Count,
                created,
                eligible.Count - created,
                $"Đã tạo {created}/{eligible.Count} JournalEntry (skip {eligible.Count - created} — đã có hoặc lỗi fail-safe).");
        }

        private async Task<List<CoreAccountingEntry>> GetEligibleWithoutJournalEntryAsync(TenantId tenantId, CancellationToken cancellationToken)
        {
            IEnumerable<CoreAccountingEntry> all = await _accountingEntryRepository.GetByTenantAsync(tenantId, cancellationToken);
            List<CoreAccountingEntry> allList = all.ToList();

            // Phiếu đã bị đảo (có reversal trỏ tới): bỏ qua — cặp gốc+đảo pre-P2 đều không có JE
            // → net zero trong báo cáo (không tạo JE gốc lẻ). Phiếu đảo sau P2 tự tạo JE reversal.
            HashSet<Guid> reversedIds = allList
                .Where(e => e.ReversalEntryId.HasValue)
                .Select(e => e.ReversalEntryId!.Value)
                .ToHashSet();

            var eligible = new List<CoreAccountingEntry>();
            foreach (CoreAccountingEntry entry in allList)
            {
                if (entry.ReversalEntryId != null || reversedIds.Contains(entry.Id))
                {
                    continue; // bản thân là phiếu đảo hoặc đã bị đảo — không backfill
                }

                if (entry.EntryType is not (AccountingEntryType.Revenue or AccountingEntryType.Expense))
                {
                    continue; // công nợ 131/331 (Adjustment) — [G6] không tạo JE
                }

                string? accountCode = entry.AccountCode;
                if (string.IsNullOrWhiteSpace(accountCode) ||
                    !(accountCode.StartsWith("5") || accountCode.StartsWith("6") || accountCode.StartsWith("7")))
                {
                    continue; // không phải phiếu thu doanh thu / chi phí
                }

                if (await _hkdBookRepository.ExistsByReferenceAsync(tenantId, ManualEntryJournalBridge.ManualEntryReferenceType, entry.Id, cancellationToken))
                {
                    continue; // đã có JE (tạo từ sau P2 deploy)
                }

                eligible.Add(entry);
            }

            return eligible;
        }
    }
}
