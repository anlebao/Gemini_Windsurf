using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Infrastructure.Entities;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// SRS v1.2 W2/C1 (G-H3, M6): cấp số chứng từ tuần tự per (tenant, loại phiếu, năm).
    /// Format "{VoucherType}-{yyyy}-{seq:00000}" (VD: PT-2026-00001 · PT-CN-2026-00012).
    /// </summary>
    public interface IVoucherNumberService
    {
        /// <summary>Số chứng từ kế tiếp — duy nhất per (tenant, type, year), tuần tự.</summary>
        Task<string> NextNumberAsync(TenantId tenantId, string voucherType, int year, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// VoucherNumberService — read-modify-write VoucherSequenceEntity + retry khi
    /// unique index (TenantId,VoucherType,Year) bắt race insert (tối đa 3 lần).
    /// FR-VCH-004: 2 phiếu đồng thời không trùng số.
    /// </summary>
    public sealed class VoucherNumberService(
        IAccountingDbContext dbContext,
        ILogger<VoucherNumberService> logger) : IVoucherNumberService
    {
        public const string TypeReceipt = "PT";
        public const string TypePayment = "PC";
        public const string TypeReceiptCongNo = "PT-CN";
        public const string TypePaymentCongNo = "PC-CN";
        public const string TypeTransfer = "CT"; // Phase D

        private const int MaxAttempts = 3;

        private readonly IAccountingDbContext _dbContext = dbContext;
        private readonly ILogger<VoucherNumberService> _logger = logger;

        /// <inheritdoc />
        public async Task<string> NextNumberAsync(TenantId tenantId, string voucherType, int year, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(voucherType))
            {
                throw new ArgumentException("VoucherType is required.", nameof(voucherType));
            }

            for (int attempt = 1; ; attempt++)
            {
                VoucherSequenceEntity? row = await _dbContext.VoucherSequences
                    .FirstOrDefaultAsync(s => s.TenantId == tenantId.Value
                                              && s.VoucherType == voucherType
                                              && s.Year == year, cancellationToken);

                if (row == null)
                {
                    row = new VoucherSequenceEntity(tenantId.Value, voucherType, year);
                    _ = await _dbContext.VoucherSequences.AddAsync(row, cancellationToken);
                }

                long next = row.Next();
                try
                {
                    _ = await _dbContext.SaveChangesAsync(cancellationToken);
                    return Format(voucherType, year, next);
                }
                catch (DbUpdateException ex) when (attempt < MaxAttempts)
                {
                    // Race: phiếu khác đã insert sequence row trước → detach + đọc lại + retry.
                    _logger.LogWarning(ex,
                        "VoucherNumberService: race khi cấp số {VoucherType}/{Year} tenant {TenantId} — retry {Attempt}/{Max}",
                        voucherType, year, tenantId.Value, attempt);
                    if (_dbContext is DbContext concrete)
                    {
                        concrete.Entry(row).State = EntityState.Detached;
                    }
                }
            }
        }

        private static string Format(string voucherType, int year, long number)
            => $"{voucherType}-{year}-{number:00000}";
    }
}
