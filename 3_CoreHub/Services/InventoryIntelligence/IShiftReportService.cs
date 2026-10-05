using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.InventoryIntelligence
{
    /// <summary>
    /// VA-IIE (Sprint B, P2.1): quản lý ca — mở/đóng ca, kiểm kê đầu/cuối, restock, submit/acknowledge,
    /// cash count (SRS §3.1, §3.5). Đóng ca = orchestration: tiêu hao lý thuyết → variance → alert engine.
    /// InventoryCount immutable sau Closed (NFR-7 — sửa qua adjustment).
    /// </summary>
    public interface IShiftReportService
    {
        /// <summary>Mở ca mới (Draft) — SRS §3.1.1.</summary>
        Task<Shift> OpenShiftAsync(TenantId tenantId, ShiftType shiftType, Guid staffUserId, DateTime? startTime = null, CancellationToken ct = default);

        /// <summary>Kiểm kê đầu/cuối ca (upsert theo ingredient+countType; chặn sau Closed — NFR-7).</summary>
        Task<InventoryCount> AddInventoryCountAsync(Guid shiftId, Guid ingredientId, CountType countType, decimal quantity, string unit, decimal? midShiftStockIn = null, CancellationToken ct = default);

        /// <summary>Nhập thêm trong ca — cộng dồn vào MidShiftStockIn của opening count (SRS §3.1.2).</summary>
        Task<InventoryCount> AddRestockAsync(Guid shiftId, Guid ingredientId, decimal quantity, string unit, CancellationToken ct = default);

        /// <summary>
        /// "Tính &amp; Đóng ca" — orchestration (SRS §3.1.3): submit ca + tính tiêu hao lý thuyết
        /// (POS × Recipe) + variance + sinh cảnh báo, persist toàn bộ trong 1 transaction.
        /// </summary>
        Task<Shift> SubmitShiftAsync(Guid shiftId, Guid staffUserId, decimal cashCount, decimal posCashTotal, string? handoverNotes = null, DateTime? endTime = null, CancellationToken ct = default);

        /// <summary>Xác nhận bàn giao ca (SRS §3.5).</summary>
        Task<Shift> AcknowledgeShiftAsync(Guid shiftId, Guid acknowledgedBy, CancellationToken ct = default);

        /// <summary>Chốt ca sau khi đã xác nhận.</summary>
        Task<Shift> CloseShiftAsync(Guid shiftId, Guid acknowledgedBy, CancellationToken ct = default);

        /// <summary>Báo cáo cuối ca đầy đủ — 6 phần (SRS §5.1).</summary>
        Task<Dtos.ShiftReportDto> GetShiftReportAsync(Guid shiftId, CancellationToken ct = default);

        /// <summary>Danh sách ca của 1 tenant (theo thời gian, mới nhất trước) — multi-tenancy filter.</summary>
        Task<IReadOnlyList<Shift>> ListShiftsAsync(TenantId tenantId, DateTime? from = null, DateTime? to = null, CancellationToken ct = default);

        /// <summary>Danh sách ingredient của tenant (đơn vị cơ sở) cho form kiểm kê — "số + chọn đơn vị" (Q3-C). Multi-tenancy filter.</summary>
        Task<IReadOnlyList<Ingredient>> GetIngredientsAsync(TenantId tenantId, CancellationToken ct = default);
    }
}
