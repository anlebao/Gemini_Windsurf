using VanAn.Shared.Domain;

namespace VanAn.ShopERP.Services.Accounting;

/// <summary>
/// P3 — Nhãn mẫu biểu + tên thông tư theo AccountingStandard (single source of truth).
/// Dùng cho header trang, hub link, và title file export.
/// </summary>
public static class AccountingFormLabels
{
    public static string CircularName(AccountingStandard standard) => standard switch
    {
        AccountingStandard.TT99_2025 => "Thông tư 99/2025/TT-BTC",
        AccountingStandard.TT133_2016 => "Thông tư 133/2016/TT-BTC",
        AccountingStandard.TT58_2026 => "Thông tư 58/2026/TT-BTC",
        AccountingStandard.TT71_2024 => "Thông tư 71/2024/TT-BTC",
        _ => standard.ToString()
    };

    /// <summary>
    /// Mã mẫu biểu theo chuẩn.
    ///   BS = Bảng cân đối (B01) · IS = KQKD (B02) · CF = LCTT (B03)
    ///   NT = Thuyết minh (B09) · TB = Cân đối TK (F01)
    /// </summary>
    public static string FormCode(AccountingStandard standard, string reportKind) => (standard, reportKind) switch
    {
        (AccountingStandard.TT99_2025, "BS") => "B 01-DN",
        (AccountingStandard.TT133_2016, "BS") => "B 01a-DNN",
        (AccountingStandard.TT58_2026, "BS") => "B 01-DNSN",
        (AccountingStandard.TT71_2024, "BS") => "B 01-HTX",

        (AccountingStandard.TT99_2025, "IS") => "B 02-DN",
        (AccountingStandard.TT133_2016, "IS") => "B 02-DNN",
        (AccountingStandard.TT58_2026, "IS") => "B 02-DNSN",
        (AccountingStandard.TT71_2024, "IS") => "B 02-HTX",

        (AccountingStandard.TT99_2025, "CF") => "B 03-DN",
        (AccountingStandard.TT133_2016, "CF") => "B 03-DNN",
        (AccountingStandard.TT71_2024, "CF") => "B 03-HTX",   // không bắt buộc — ẩn trên hub
        (AccountingStandard.TT58_2026, "CF") => "",           // TT58 không có B03

        (AccountingStandard.TT99_2025, "NT") => "B 09-DN",
        (AccountingStandard.TT133_2016, "NT") => "B 09-DNN",
        (AccountingStandard.TT71_2024, "NT") => "B 09-HTX",
        (AccountingStandard.TT58_2026, "NT") => "",           // TT58 không có B09

        (AccountingStandard.TT133_2016, "TB") => "F 01-DNN",
        (AccountingStandard.TT99_2025, "TB") => "F 01-DN",
        _ => ""
    };

    /// <summary>Title đầy đủ cho export: "BÁO CÁO TÌNH HÌNH TÀI CHÍNH (Mẫu số B 01a-DNN — Thông tư 133/2016/TT-BTC)".</summary>
    public static string ExportTitle(AccountingStandard standard, string reportKind, string reportName)
    {
        string form = FormCode(standard, reportKind);
        return string.IsNullOrEmpty(form)
            ? reportName
            : $"{reportName} (Mẫu số {form} — {CircularName(standard)})";
    }
}
