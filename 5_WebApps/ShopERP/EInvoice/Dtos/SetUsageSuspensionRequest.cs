// P0.4 (W2, 2026-10-17 — Điều 8 TT 91/2026): ngừng/tạm ngừng sử dụng HĐĐT.
namespace VanAn.ShopERP.EInvoice.Dtos;

/// <summary>
/// Request bật/tắt cờ ngừng/tạm ngừng sử dụng hóa đơn điện tử
/// (SystemSetting EInvoice_Usage_Suspended theo tenant).
/// </summary>
/// <param name="Suspended">true = ngừng/tạm ngừng sử dụng HĐĐT (chặn submit).</param>
/// <param name="Reason">Số hiệu/lý do thông báo của cơ quan thuế (optional).</param>
public sealed record SetUsageSuspensionRequest(
    bool Suspended,
    string? Reason = null);
