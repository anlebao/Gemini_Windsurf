using VanAn.CoreHub.Services;
using VanAn.CoreHub.Services.Journal;
using VanAn.Shared.Domain;
using VanAn.Shared.Services;
using PlatformComposite = VanAn.UI.Platform.Components.Composite;

namespace VanAn.ShopERP.Services.Accounting;

/// <summary>Một dòng bút toán preview (form nhập liệu — read-only, không lưu).</summary>
public sealed record JournalPreviewLine(string AccountCode, decimal Debit, decimal Credit);

/// <summary>Kết quả preview: lines + nguồn rule (v1/builtin/tenant) + note khi không tạo JE.</summary>
public sealed record JournalPreviewResult(IReadOnlyList<JournalPreviewLine> Lines, string Source, string? Note);

/// <summary>
/// Issue #198 (item 3): helpers chung cho 2 form Nhập Doanh Thu / Nhập Chi Phí —
/// COA-driven account options (global chart + tenant override/custom qua
/// IAccountChartService) + preview định khoản read-only qua IAccountMapResolver
/// (cùng code path bridge — parity 100%) + category→TK gợi ý.
/// </summary>
public static class EntryFormAccountingHelper
{
    /// <summary>TenantType → chuẩn COA. HKD/SME/unknown → TT133 (khớp static list hiện tại).</summary>
    public static AccountingStandard StandardOf(TenantType? type) => type switch
    {
        TenantType.HTX => AccountingStandard.TT71_2024,
        TenantType.Enterprise_SuperSmall => AccountingStandard.TT58_2026,
        TenantType.Enterprise_Large => AccountingStandard.TT99_2025,
        _ => AccountingStandard.TT133_2016
    };

    /// <summary>
    /// Options TK từ effective tenant chart lọc theo prefix ("5","7" thu · "6" chi ·
    /// "15","6" đối ứng). Fallback về static provider khi chart lỗi/rỗng — không chặn nhập liệu.
    /// </summary>
    public static async Task<List<PlatformComposite.FieldOption>> LoadAccountOptionsAsync(
        IAccountChartService? chartService, TenantId tenantId, TenantType? tenantType,
        string[] prefixes, List<PlatformComposite.FieldOption> fallback, CancellationToken ct = default)
    {
        if (chartService == null)
        {
            return fallback;
        }
        try
        {
            var accounts = await chartService.GetTenantAccountsAsync(tenantId, StandardOf(tenantType), ct);
            var opts = accounts
                .Where(a => prefixes.Any(p => a.AccountCode.StartsWith(p, StringComparison.Ordinal)))
                .OrderBy(a => a.AccountCode)
                .Select(a => new PlatformComposite.FieldOption
                {
                    Value = a.AccountCode,
                    Label = $"{a.AccountCode} - {a.AccountName}"
                })
                .ToList();
            return opts.Count > 0 ? opts : fallback;
        }
        catch
        {
            return fallback;
        }
    }

    /// <summary>Category → TK gợi ý (chỉ auto-fill khi user chưa đụng account). HTX → 642/658.
    /// PH3 (khẩn — fix định khoản gợi ý sai): tax → 333 (thuế phải nộp) · interest → 635 (chi phí tài chính)
    /// — mọi loại hình (333/635 có trong chart HKD TT152 / DN TT133-TT99 / HTX TT71).</summary>
    public static string SuggestExpenseAccount(string? category, TenantType? tenantType)
    {
        bool htx = tenantType == TenantType.HTX;
        return category switch
        {
            "materials" => htx ? "642" : "621",
            "labor" => htx ? "642" : "622",
            "utilities" => htx ? "642" : "627",
            "rent" => htx ? "642" : "627",
            "marketing" => htx ? "642" : "641",
            "maintenance" => htx ? "642" : "627",
            "tax" => "333",
            "interest" => "635",
            _ => htx ? "658" : "642"
        };
    }

    /// <summary>Events công nợ — gated bởi loại hình DN/HTX + flag AccrualReceivableJE.</summary>
    private static bool IsAccrualEvent(string eventCode) =>
        eventCode is AccountMapEventCodes.Receivable or AccountMapEventCodes.ReceivablePayment
            or AccountMapEventCodes.Payable or AccountMapEventCodes.PayablePayment;

    /// <summary>
    /// Preview định khoản read-only — cùng derivation của bridge (eventCode do form truyền
    /// theo voucher type). VAT đầu vào (nếu có) → thêm dòng Nợ 133 / Có {cash}.
    /// </summary>
    public static async Task<JournalPreviewResult> PreviewAsync(
        string eventCode, TenantId tenantId, string? accountHint, string? paymentMethod,
        decimal amount, decimal vatAmount, TenantType? tenantType,
        IAccountMapResolver? resolver, IBankAccountService? bankAccounts,
        IShopFeatureSettingsService? featureSettings, CancellationToken ct = default)
    {
        bool accrualOk = tenantType is not (null or TenantType.HKD or TenantType.Enterprise_SuperSmall)
            && (featureSettings == null
                || await featureSettings.IsEnabledAsync(tenantId.Value, ManualEntryJournalBridge.AccrualReceivableFlag, ct));
        if (IsAccrualEvent(eventCode) && !accrualOk)
        {
            return new([], "none", "Chế độ sổ cash-basis — phiếu công nợ không tạo bút toán.");
        }

        string cash = "111";
        if (bankAccounts != null)
        {
            try { cash = await bankAccounts.ResolveCashAccountCodeAsync(tenantId, paymentMethod, ct); }
            catch { /* fallback 111 */ }
        }

        AccountMapResult? map = resolver == null || string.IsNullOrWhiteSpace(accountHint) ? null
            : await resolver.ResolveAsync(eventCode, tenantId, accountHint, cash, ct);
        if (map == null)
        {
            return new([], "none", "Chọn tài khoản để xem bút toán (hoặc chưa có rule — bút toán sẽ bị bỏ qua).");
        }

        decimal net = amount - vatAmount;
        var lines = new List<JournalPreviewLine> { new(map.Debit, Math.Max(net, 0m), 0m) };
        if (vatAmount > 0 && resolver != null)
        {
            AccountMapResult? vatMap = await resolver.ResolveAsync(
                AccountMapEventCodes.ManualVatIn, tenantId, "133", cash, ct);
            lines.Add(new JournalPreviewLine(vatMap?.Debit ?? "133", vatAmount, 0m));
        }
        lines.Add(new JournalPreviewLine(map.Credit, 0m, amount));
        return new(lines, map.MappingVersion ?? "rule", null);
    }
}
