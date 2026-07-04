using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services;

/// <summary>
/// W3 (D9): Static HKD→DN account mapping table.
/// Design-time lookup — no DB persistence needed (fixed mapping per D9 decision).
///
/// HKD single-entry (TT 88/2021) uses internal synthetic keys; DN double-entry uses
/// account codes from TT 99/133/58. The mapping is identical across all 3 DN standards
/// for the core accounts (verified during INVESTIGATE 2026-07-04) — only the standard
/// enum differs for downstream service routing.
/// </summary>
public class HkdToEnterpriseAccountMapper : IHkdToEnterpriseAccountMapper
{
    /// <summary>
    /// Canonical HKD→DN mapping. Codes are identical across TT 99/133/58 for these
    /// core accounts (verified via TT 99/2025 chart comparison + TT 133 chart).
    /// If a future standard diverges, add a per-standard override dictionary.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> CanonicalMapping = new Dictionary<string, string>
    {
        { "Revenue", "511" },          // Doanh thu
        { "COGS", "632" },             // Giá vốn
        { "Cash", "111" },             // Tiền mặt
        { "CashBank", "112" },         // Tiền gửi NH / Tiền gửi không kỳ hạn (TT 99)
        { "Inventory", "156" },        // Hàng hóa
        { "Materials", "152" },        // Nguyên liệu, vật liệu
        { "SellingExpense", "641" },   // CP bán hàng (W3-T6 fixed label)
        { "AdminExpense", "642" },     // CP quản lý DN (W3-T6 fixed label)
        { "TaxOutput", "3331" },       // Thuế GTGT đầu ra
        { "TaxInput", "1331" },        // Thuế GTGT đầu vào
        { "Payroll", "334" },          // Phải trả người lao động
        { "FixedAsset", "211" },       // TSCĐ hữu hình
        { "Depreciation", "214" },     // Hao mòn TSCĐ (contra-asset — IsNormalCredit=true in chart)
        { "Equity", "411" },           // Vốn đầu tư của CSH
    };

    /// <inheritdoc />
    public string MapToEnterpriseAccount(string hkdAccountKey, AccountingStandard standard)
    {
        if (string.IsNullOrWhiteSpace(hkdAccountKey))
            throw new ArgumentException("HKD account key is required.", nameof(hkdAccountKey));

        return CanonicalMapping.TryGetValue(hkdAccountKey, out string? code)
            ? code
            : throw new KeyNotFoundException(
                $"No HKD→DN mapping found for key '{hkdAccountKey}' under standard {standard}. " +
                $"Known keys: {string.Join(", ", CanonicalMapping.Keys.OrderBy(k => k))}.");
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> GetMappings(AccountingStandard standard)
        => CanonicalMapping;
}
