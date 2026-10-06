using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.Booking;

/// <summary>Inputs tính khấu trừ thuế (SRS §17.3) — snapshot vào CommissionLedgerEntry.</summary>
public sealed record TaxWithholdingInput(
    TaxPayeeType PayeeType,
    string? Residency,        // "VN" | "FOREIGN" | null (không rõ)
    string? ContractType,     // null | "UNDER_3_MONTHS" | "3_MONTHS_OR_MORE"
    decimal GrossAmount,
    string TaxRuleVersion);

/// <summary>Kết quả — gross → withheld → net + reason code (SRS §17.3: KHÔNG hard-code 10% cho mọi payee).</summary>
public sealed record TaxWithholdingResult(
    decimal GrossAmount,
    decimal TaxWithheldAmount,
    decimal NetPayable,
    string? WithholdingReasonCode,
    string TaxRuleVersion);

/// <summary>
/// ITaxWithholdingPolicyAdapter — versioned tax rule (SRS §17.3/§43.2).
/// NĐ 253/2026 (hiệu lực 01/07/2026): chi trả cho cá nhân cư trú KHÔNG ký HĐLĐ hoặc ký HĐLĐ &lt; 03 tháng
/// mà mức chi trả từ 5 triệu đồng/lần trở lên → khấu trừ 10% trước khi trả. HĐLĐ ≥ 03 tháng → cơ chế khác
/// (không mặc định 10%). Booking module KHÔNG là engine tư vấn thuế — chỉ lưu đủ facts + calculation snapshot.
/// </summary>
public interface ITaxWithholdingPolicyAdapter
{
    /// <summary>Version rule đang hiệu lực (snapshot vào ledger entry).</summary>
    string CurrentVersion { get; }

    /// <summary>Tính toán khấu trừ theo inputs + version (pure function — không I/O).</summary>
    TaxWithholdingResult Calculate(TaxWithholdingInput input);
}
