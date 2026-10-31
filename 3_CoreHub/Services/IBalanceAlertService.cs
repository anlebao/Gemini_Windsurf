using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services
{
    /// <summary>W4 (P1.3 — TT 152/2025): loại cảnh báo số dư âm.</summary>
    public enum BalanceAlertType
    {
        /// <summary>Âm quỹ tiền mặt — TK 111.</summary>
        CashNegative,
        /// <summary>Âm kho trên sổ sách kế toán — TK 152/156.</summary>
        InventoryNegative,
        /// <summary>Âm tồn kho nguyên liệu (số lượng — ShopERP SQLite).</summary>
        IngredientStockNegative
    }

    /// <summary>
    /// Kết quả cảnh báo số dư âm (non-blocking — chỉ cảnh báo, không chặn nghiệp vụ).
    /// </summary>
    public sealed record BalanceAlertResult(
        BalanceAlertType AlertType,
        string SubjectLabel,
        decimal CurrentBalance,
        decimal ProjectedChange,
        decimal ProjectedBalance,
        bool IsNegative,
        string Message);

    /// <summary>
    /// W4 (P1.3 — 2026-10-18, TT 152/2025): cảnh báo âm quỹ tiền mặt (TK 111) và
    /// âm kho (TK 152/156 + tồn kho nguyên liệu). Cảnh báo KHÔNG chặn nghiệp vụ —
    /// chỉ hiển thị cho kế toán/chủ shop trước khi lưu. Tenant isolation qua
    /// <paramref name="tenantId"/> trên mọi truy vấn số dư.
    /// </summary>
    public interface IBalanceAlertService
    {
        /// <summary>
        /// Âm quỹ tiền (mặc định TK 111; W3/D FR-PAY-006: truyền <paramref name="cashAccountCode"/>
        /// để kiểm tra đúng quỹ/TGNH theo hình thức thanh toán — vd 112x khi chi chuyển khoản):
        /// số dư cuối kỳ hiện tại trừ <paramref name="projectedOutflow"/> &lt; 0 → trả alert; đủ quỹ → null.
        /// </summary>
        Task<BalanceAlertResult?> CheckCashBalanceAsync(
            TenantId tenantId,
            decimal projectedOutflow,
            string? cashAccountCode = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Âm kho sổ sách TK 152+156 (tổng giá trị): dư cuối trừ <paramref name="projectedOutflowValue"/>
        /// &lt; 0 → trả alert; không âm → null.
        /// </summary>
        Task<BalanceAlertResult?> CheckInventoryBalanceAsync(
            TenantId tenantId,
            decimal projectedOutflowValue,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Âm tồn kho nguyên liệu theo SỐ LƯỢNG: <paramref name="currentStock"/> trừ
        /// <paramref name="projectedOutput"/> &lt; 0 → trả alert; đủ tồn → null.
        /// Caller truyền currentStock vì stock sống ở ShopERP SQLite (không cross-context).
        /// </summary>
        Task<BalanceAlertResult?> CheckStockLevelAsync(
            string itemLabel,
            decimal currentStock,
            decimal projectedOutput,
            CancellationToken cancellationToken = default);
    }
}
