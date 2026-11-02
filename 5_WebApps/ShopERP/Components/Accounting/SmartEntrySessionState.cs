using VanAn.CoreHub.Services;

namespace VanAn.ShopERP.Components.Accounting;

/// <summary>
/// Task G4 — giữ kết quả parse multi qua các trang (scoped per circuit): user parse
/// "thu 5tr, chi 3tr" ở form Doanh Thu → sang form Chi Phí (navigation) → block trích
/// xuất vẫn hiện 2 phiếu. Clear khi parse câu mới / hết phiếu chờ.
/// </summary>
public sealed class SmartEntrySessionState
{
    public List<ParsedVoucher>? Multi { get; private set; }
    public string? Text { get; private set; }

    public void Set(List<ParsedVoucher> multi, string text)
    {
        Multi = multi;
        Text = text;
    }

    public void Clear()
    {
        Multi = null;
        Text = null;
    }
}