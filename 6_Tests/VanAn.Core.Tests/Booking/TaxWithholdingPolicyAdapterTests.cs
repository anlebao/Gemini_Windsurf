using VanAn.CoreHub.Services.Booking;
using VanAn.Shared.Domain;
using Xunit;

namespace VanAn.Core.Tests.BookingScheduling
{
    /// <summary>
    /// ITaxWithholdingPolicyAdapter — versioned fixtures (§17.3 NĐ 253/2026 — mốc 5tr/lần, KHÔNG hard-code 10% cho mọi payee).
    /// </summary>
    public class TaxWithholdingPolicyAdapterTests
    {
        private static readonly ITaxWithholdingPolicyAdapter Adapter = new TaxWithholdingPolicyAdapter();

        private static TaxWithholdingResult Calc(TaxPayeeType payee, decimal gross, string? residency = "VN", string? contract = "UNDER_3_MONTHS", string version = TaxWithholdingPolicyAdapter.VersionNd253_2026)
            => Adapter.Calculate(new TaxWithholdingInput(payee, residency, contract, gross, version));

        [Fact]
        public void IndividualContractor_Above5M_Withholds10Percent()
        {
            var result = Calc(TaxPayeeType.IndividualContractor, 10_000_000m);

            Assert.Equal(1_000_000m, result.TaxWithheldAmount);
            Assert.Equal(9_000_000m, result.NetPayable);
            Assert.Equal("ND253-2026-10PCT-5M", result.WithholdingReasonCode);
            Assert.Equal(TaxWithholdingPolicyAdapter.VersionNd253_2026, result.TaxRuleVersion);
        }

        [Fact]
        public void Exactly5M_Withholds10Percent()
        {
            var result = Calc(TaxPayeeType.IndividualContractor, 5_000_000m);

            Assert.Equal(500_000m, result.TaxWithheldAmount);
            Assert.Equal(4_500_000m, result.NetPayable);
        }

        [Fact]
        public void Below5M_NoWithholding_ReasonBelowThreshold()
        {
            var result = Calc(TaxPayeeType.IndividualContractor, 4_999_999m);

            Assert.Equal(0m, result.TaxWithheldAmount);
            Assert.Equal(4_999_999m, result.NetPayable);
            Assert.Equal("ND253-2026-BELOW-5M", result.WithholdingReasonCode);
        }

        [Fact]
        public void RegularEmployee_Contract3MonthsOrMore_NoDefault10Percent()
        {
            // HĐLĐ ≥ 03 tháng → cơ chế khấu trừ riêng, KHÔNG mặc định 10% (§17.3).
            var result = Calc(TaxPayeeType.Employee, 20_000_000m, contract: "3_MONTHS_OR_MORE");

            Assert.Equal(0m, result.TaxWithheldAmount);
            Assert.Equal(20_000_000m, result.NetPayable);
            Assert.Equal("REGULAR-EMPLOYEE", result.WithholdingReasonCode);
        }

        [Fact]
        public void BusinessEntity_NoWithholding_ReasonNonIndividual()
        {
            var result = Calc(TaxPayeeType.BusinessEntity, 100_000_000m);

            Assert.Equal(0m, result.TaxWithheldAmount);
            Assert.Equal("NON-INDIVIDUAL", result.WithholdingReasonCode);
        }

        [Fact]
        public void NonResident_NoWithholding_ReasonNonResident()
        {
            var result = Calc(TaxPayeeType.IndividualContractor, 20_000_000m, residency: "FOREIGN");

            Assert.Equal(0m, result.TaxWithheldAmount);
            Assert.Equal("NON-RESIDENT", result.WithholdingReasonCode);
        }

        [Fact]
        public void UnknownVersion_NoRule_NoWithholding()
        {
            var result = Calc(TaxPayeeType.IndividualContractor, 20_000_000m, version: "LEGACY-2025-1");

            Assert.Equal(0m, result.TaxWithheldAmount);
            Assert.Equal("NO-RULE", result.WithholdingReasonCode);
            Assert.Equal("LEGACY-2025-1", result.TaxRuleVersion);
        }

        [Fact]
        public void NegativeGross_Throws()
        {
            Assert.Throws<ArgumentException>(() => Calc(TaxPayeeType.IndividualContractor, -1m));
        }

        [Fact]
        public void CurrentVersion_IsNd253_2026()
        {
            Assert.Equal(TaxWithholdingPolicyAdapter.VersionNd253_2026, Adapter.CurrentVersion);
        }
    }
}
