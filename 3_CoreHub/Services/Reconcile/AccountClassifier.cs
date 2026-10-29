using System.Text.RegularExpressions;

namespace VanAn.CoreHub.Services.Reconcile;

/// <summary>
/// Phase 0 (tra soát số liệu kế toán — user directive 2026-10-29, SRS G7): chuẩn hoá
/// phân loại tài khoản cho các check R3/R4/R7/R10.
/// - Regex hợp lệ: `^[1-9]\d{0,3}$` (1-4 chữ số, không bắt đầu 0) — khớp danh mục TK VN
///   (1xx-9xx chính + 4 số chi tiết: 3331/3387/6421/2294…).
/// - Nhóm theo prefix: 1-2 Asset · 3 Liability · 4 Equity · 5/7 Revenue · 6/8 Expense ·
///   9 Temporary (kết chuyển — 911 Xác định KQKD) · còn lại Unknown (malformed/rỗng).
/// - 9xx (911): khi hệ thống làm bút toán kết chuyển cuối kỳ (Nợ 511/Có 911 · Nợ 911/Có 632)
///   → 511/632 bị zero, lợi nhuận nằm ở 911 → công thức R4 (chỉ 5/7−6/8) lệch → R4 phải
///   Warning + gợi ý mở rộng NET gộp 9xx (read-only — KHÔNG tự sửa). Verify production
///   2026-10-29: journal chỉ prefix 1-6 — chưa có 9xx, an toàn hiện tại.
/// - Malformed/rỗng (null, "", "ABC", "01", "1111"): Unknown → R4 Warning + liệt kê.
/// Pure static helper — KHÔNG phụ thuộc DI/DB.
/// </summary>
public enum AccountGroup
{
    Asset,      // 1xx/2xx — tài sản (gồm contra 214/229 — normal credit nhưng vẫn nhóm Asset)
    Liability,  // 3xx — nợ phải trả
    Equity,     // 4xx — vốn chủ sở hữu
    Revenue,    // 5xx/7xx — doanh thu / thu nhập khác (gồm 512 HTX nội bộ)
    Expense,    // 6xx/8xx — chi phí (gồm 612 HTX nội bộ)
    Temporary,  // 9xx — kết chuyển (911 Xác định KQKD) — hiện production chưa dùng
    Unknown     // malformed / rỗng / ngoài danh mục
}

public static class AccountClassifier
{
    /// <summary>1-4 chữ số, không bắt đầu 0 (khớp danh mục TK VN: 1xx-9xx chính + 4 số chi tiết 3331/3387/6421/2294).</summary>
    public static readonly Regex ValidCodeRegex = new("^[1-9]\\d{0,3}$", RegexOptions.Compiled);

    public static bool IsValid(string? code)
        => !string.IsNullOrWhiteSpace(code) && ValidCodeRegex.IsMatch(code);

    /// <summary>Phân nhóm theo prefix. Malformed/rỗng → <see cref="AccountGroup.Unknown"/>.</summary>
    public static AccountGroup Classify(string? code)
    {
        if (!IsValid(code))
            return AccountGroup.Unknown;
        return code![0] switch
        {
            '1' or '2' => AccountGroup.Asset,
            '3' => AccountGroup.Liability,
            '4' => AccountGroup.Equity,
            '5' or '7' => AccountGroup.Revenue,
            '6' or '8' => AccountGroup.Expense,
            '9' => AccountGroup.Temporary,
            _ => AccountGroup.Unknown
        };
    }

    /// <summary>Nhóm được tính vào Balance identity R4 (prefix 1-8). 9xx/Unknown → cần review.</summary>
    public static bool IsRecognizedForBalanceIdentity(string? code)
    {
        AccountGroup g = Classify(code);
        return g is AccountGroup.Asset or AccountGroup.Liability or AccountGroup.Equity
            or AccountGroup.Revenue or AccountGroup.Expense;
    }

    /// <summary>Mô tả cho suggestion khi R4 gặp tài khoản cần review.</summary>
    public static string Describe(string? code)
    {
        AccountGroup g = Classify(code);
        string label = g switch
        {
            AccountGroup.Asset => "Tài sản (1xx/2xx)",
            AccountGroup.Liability => "Nợ phải trả (3xx)",
            AccountGroup.Equity => "Vốn chủ sở hữu (4xx)",
            AccountGroup.Revenue => "Doanh thu (5xx/7xx)",
            AccountGroup.Expense => "Chi phí (6xx/8xx)",
            _ => g.ToString()
        };
        return g switch
        {
            AccountGroup.Temporary =>
                $"TK '{code}' (9xx — kết chuyển, ví dụ 911 Xác định KQKD): khi hệ thống làm bút toán kết chuyển cuối kỳ (Nợ 511/Có 911 · Nợ 911/Có 632), 511/632 bị zero và lợi nhuận nằm ở 911 → công thức R4 (chỉ 5/7−6/8) sẽ lệch. Cần mở rộng NET gộp 9xx (hiện production chưa có 9xx — an toàn).",
            AccountGroup.Unknown =>
                $"TK '{code}' không hợp lệ (phải khớp ^[1-9]\\d{{0,3}}$ — 1-4 chữ số, không bắt đầu 0; rỗng/null cũng là lỗi). Kiểm tra dữ liệu AccountNumber.",
            _ => $"TK '{code}' — nhóm {label}."
        };
    }
}