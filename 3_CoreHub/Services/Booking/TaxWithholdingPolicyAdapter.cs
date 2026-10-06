using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.Booking;

/// <summary>
/// TaxWithholdingPolicyAdapter — NĐ 253/2026 (effective 01/07/2026) khấu trừ 10% TNCN cho cá nhân cư trú
/// không ký HĐLĐ hoặc HĐLĐ &lt; 03 tháng khi chi trả từ 5.000.000đ/lần. KHÔNG hard-code 10% cho mọi payee:
/// - HĐLĐ ≥ 03 tháng → cơ chế khấu trừ riêng (0 tại booking layer — defer Accounting/Tax).
/// - BusinessEntity / non-resident → 0 (defer Accounting/Tax module — §17.3 "lưu đủ facts").
/// Versioned — rule mới (2027+, NĐ mới) thêm adapter version mới, ledger snapshot giữ version cũ (immutable §17.5).
/// </summary>
public sealed class TaxWithholdingPolicyAdapter : ITaxWithholdingPolicyAdapter
{
    public const string VersionNd253_2026 = "ND253-2026-1";
    public const decimal ThresholdPerOccasion = 5_000_000m;
    public const decimal Rate = 0.10m;

    public string CurrentVersion { get; } = VersionNd253_2026;

    public TaxWithholdingResult Calculate(TaxWithholdingInput input)
    {
        if (input.GrossAmount < 0)
            throw new ArgumentException("GrossAmount không được âm.", nameof(input));

        if (input.TaxRuleVersion != VersionNd253_2026)
            return new TaxWithholdingResult(input.GrossAmount, 0m, input.GrossAmount, "NO-RULE", input.TaxRuleVersion);

        // Chỉ khấu trừ cho cá nhân cư trú (Employee không HĐLĐ ≥ 03 tháng / IndividualContractor).
        bool residentIndividual = input.PayeeType is TaxPayeeType.Employee or TaxPayeeType.IndividualContractor
                                  && input.Residency is null or "VN";
        if (!residentIndividual)
        {
            return new TaxWithholdingResult(input.GrossAmount, 0m, input.GrossAmount,
                input.PayeeType == TaxPayeeType.BusinessEntity ? "NON-INDIVIDUAL" : "NON-RESIDENT",
                input.TaxRuleVersion);
        }

        // HĐLĐ ≥ 03 tháng → khấu trừ theo chế độ lương riêng, KHÔNG áp mặc định 10% (§17.3).
        if (input.ContractType == "3_MONTHS_OR_MORE")
            return new TaxWithholdingResult(input.GrossAmount, 0m, input.GrossAmount, "REGULAR-EMPLOYEE", input.TaxRuleVersion);

        // Không HĐLĐ / HĐLĐ < 03 tháng: ≥ 5tr/lần → 10%; dưới 5tr → 0 (khấu trừ 10% chỉ khi cá nhân yêu cầu — MVP không auto).
        if (input.GrossAmount >= ThresholdPerOccasion)
        {
            decimal withheld = Math.Round(input.GrossAmount * Rate, 0, MidpointRounding.AwayFromZero);
            return new TaxWithholdingResult(input.GrossAmount, withheld, input.GrossAmount - withheld,
                "ND253-2026-10PCT-5M", input.TaxRuleVersion);
        }

        return new TaxWithholdingResult(input.GrossAmount, 0m, input.GrossAmount, "ND253-2026-BELOW-5M", input.TaxRuleVersion);
    }
}
