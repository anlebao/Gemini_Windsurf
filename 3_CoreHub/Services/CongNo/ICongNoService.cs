using VanAn.Shared.Domain;
using VanAn.Shared.DTOs;

namespace VanAn.CoreHub.Services.CongNo
{
    /// <summary>
    /// THU CHI & CÔNG NỢ MVP (2026-10-06, SRS v1.1 — user chốt Q1-Q5):
    /// Phiếu công nợ (TK 131/331 + Vendor + dấu +/−) · báo cáo tổng hợp (đầu kỳ/PS/cuối kỳ +
    /// tuổi nợ <30/30-60/60-90/&gt;90 — FIFO động [G12]) · sổ đối tượng (cộng dồn FR-8) ·
    /// lịch sử thanh toán 1 khoản nợ (FR-8.1) · reversal tái dùng cơ chế có sẵn [G10].
    /// KHÔNG entity/bảng/migration mới — tận dụng AccountingEntry immutable + AccountCode + Vendor.
    /// </summary>
    public interface ICongNoService
    {
        /// <summary>Ghi nhận phải thu (131): bán chịu (+) hoặc thu tiền khách trả nợ (−) [G4][G5][G9].</summary>
        Task<AccountingEntryDto> CreateReceivableAsync(
            TenantId tenantId,
            decimal amount,
            string doiTuong,
            string? description,
            DateTime transactionDate,
            bool isPayment,
            string? mst = null,
            string? reference = null,
            CancellationToken cancellationToken = default);

        /// <summary>Ghi nhận phải trả (331): mua chịu (+) hoặc trả tiền người bán (−) [G4][G5][G9].</summary>
        Task<AccountingEntryDto> CreatePayableAsync(
            TenantId tenantId,
            decimal amount,
            string doiTuong,
            string? description,
            DateTime transactionDate,
            bool isPayment,
            string? mst = null,
            string? reference = null,
            CancellationToken cancellationToken = default);

        /// <summary>Báo cáo công nợ tổng hợp theo tháng (FR-7): 2 khối 131/331 + tuổi nợ FIFO [G7][G12].</summary>
        Task<CongNoReportDto> GetCongNoReportAsync(TenantId tenantId, int year, int month, CancellationToken cancellationToken = default);

        /// <summary>Sổ theo dõi phải thu/phải trả 1 đối tượng (FR-8): cộng dồn Ngày · Diễn giải · Tăng · Giảm · Số dư.</summary>
        Task<DoiTuongLedgerDto> GetDoiTuongLedgerAsync(
            TenantId tenantId,
            string accountCode,
            string doiTuong,
            int year,
            int month,
            CancellationToken cancellationToken = default);

        /// <summary>Truy ngược lịch sử thanh toán 1 khoản nợ (FR-8.1 [G12]): các lần thu/trả + số dư còn lại sau mỗi lần.
        /// Trả null nếu khoản không tồn tại / đã bị đảo bút toán / không phải khoản ghi nợ.</summary>
        Task<KhoanNoPaymentsDto?> GetKhoanNoPaymentsAsync(TenantId tenantId, Guid khoanNoEntryId, CancellationToken cancellationToken = default);
    }
}
