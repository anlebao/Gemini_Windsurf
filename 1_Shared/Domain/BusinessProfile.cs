using VanAn.Shared.Domain.Common;

namespace VanAn.Shared.Domain
{
    /// <summary>
    /// VA-FI-MVP2 (2026-08-21): Pricing model — how the tenant prices its products/services.
    /// Used by BusinessProfile for financial intelligence calculations.
    /// </summary>
    public enum PricingModel
    {
        /// <summary>Fixed price per product/service (most HKD).</summary>
        FixedPrice = 0,
        /// <summary>Dynamic pricing (happy hour, peak hours, surge).</summary>
        DynamicPricing = 1,
        /// <summary>Mix of fixed + dynamic.</summary>
        Mixed = 2
    }

    /// <summary>
    /// VA-FI-MVP2 (BR-006 compliance): Every financial calculation must be versioned.
    /// Starts at 1.0; minor increments on each BusinessProfile update.
    /// Stored as string "Major.Minor" in EF (HasConversion string).
    /// </summary>
    public readonly record struct FinancialModelVersion(int Major, int Minor)
    {
        public static FinancialModelVersion Initial => new(1, 0);

        public FinancialModelVersion Increment() => new(Major, Minor + 1);

        public override string ToString() => $"{Major}.{Minor}";

        public static FinancialModelVersion Parse(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return Initial;
            var parts = value.Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2 || !int.TryParse(parts[0], out var major) || !int.TryParse(parts[1], out var minor))
                return Initial;
            return new(major, minor);
        }
    }

    /// <summary>
    /// VA-FI-MVP2: Tenant business profile for Financial Intelligence Layer.
    /// Stores fixed costs + capacity + pricing model — declared by Owner (1-time + updates).
    /// NOT auto-derived from accounting (fixed costs = subjective estimate by Owner).
    ///
    /// Single-Identity Pattern compliant:
    /// - Id (PK from BaseEntity) is the only identity column.
    /// - BusinessProfileId VO is ignored in EF config.
    /// - Constructor sets Id = BusinessProfileId.Value.
    /// </summary>
    public class BusinessProfile : BaseEntity
    {
        public BusinessProfileId BusinessProfileId { get; private set; } = new(Guid.NewGuid());

        // === Fixed Costs (monthly, VND) ===
        public decimal MonthlyRent { get; private set; }
        public decimal MonthlyPayroll { get; private set; }
        public decimal MonthlyUtilities { get; private set; }
        public decimal MonthlyMarketing { get; private set; }
        public decimal MonthlyLogistics { get; private set; }
        public decimal MonthlyOtherOpEx { get; private set; }
        public decimal MonthlyDepreciation { get; private set; }  // CAPEX amortization

        // === Capacity ===
        public int DailyCapacityUnits { get; private set; }
        public int OperatingDaysPerMonth { get; private set; }

        // === Pricing model ===
        public PricingModel PricingModel { get; private set; }

        // === Metadata ===
        public string? Notes { get; private set; }
        public FinancialModelVersion Version { get; private set; }

        // Computed (not stored — calculated on read; EF Ignore in config)
        public decimal TotalMonthlyFixedCost =>
            MonthlyRent + MonthlyPayroll + MonthlyUtilities + MonthlyMarketing +
            MonthlyLogistics + MonthlyOtherOpEx + MonthlyDepreciation;

        // EF Core constructor for materialization
        private BusinessProfile() { }

        public BusinessProfile(
            TenantId tenantId,
            decimal monthlyRent, decimal monthlyPayroll, decimal monthlyUtilities,
            decimal monthlyMarketing, decimal monthlyLogistics, decimal monthlyOtherOpEx,
            decimal monthlyDepreciation,
            int dailyCapacityUnits, int operatingDaysPerMonth,
            PricingModel pricingModel, string? notes = null)
            : base(tenantId)
        {
            Id = BusinessProfileId.Value;
            MonthlyRent = Math.Max(0, monthlyRent);
            MonthlyPayroll = Math.Max(0, monthlyPayroll);
            MonthlyUtilities = Math.Max(0, monthlyUtilities);
            MonthlyMarketing = Math.Max(0, monthlyMarketing);
            MonthlyLogistics = Math.Max(0, monthlyLogistics);
            MonthlyOtherOpEx = Math.Max(0, monthlyOtherOpEx);
            MonthlyDepreciation = Math.Max(0, monthlyDepreciation);
            DailyCapacityUnits = Math.Max(0, dailyCapacityUnits);
            OperatingDaysPerMonth = Math.Clamp(operatingDaysPerMonth, 1, 31);
            PricingModel = pricingModel;
            Notes = notes;
            Version = FinancialModelVersion.Initial;
        }

        /// <summary>Update fixed costs + capacity + pricing. Increments Version (BR-006).</summary>
        public void Update(
            decimal monthlyRent, decimal monthlyPayroll, decimal monthlyUtilities,
            decimal monthlyMarketing, decimal monthlyLogistics, decimal monthlyOtherOpEx,
            decimal monthlyDepreciation,
            int dailyCapacityUnits, int operatingDaysPerMonth,
            PricingModel pricingModel, string? notes = null)
        {
            MonthlyRent = Math.Max(0, monthlyRent);
            MonthlyPayroll = Math.Max(0, monthlyPayroll);
            MonthlyUtilities = Math.Max(0, monthlyUtilities);
            MonthlyMarketing = Math.Max(0, monthlyMarketing);
            MonthlyLogistics = Math.Max(0, monthlyLogistics);
            MonthlyOtherOpEx = Math.Max(0, monthlyOtherOpEx);
            MonthlyDepreciation = Math.Max(0, monthlyDepreciation);
            DailyCapacityUnits = Math.Max(0, dailyCapacityUnits);
            OperatingDaysPerMonth = Math.Clamp(operatingDaysPerMonth, 1, 31);
            PricingModel = pricingModel;
            Notes = notes;
            Version = Version.Increment();
            UpdateAudit();
        }
    }
}
