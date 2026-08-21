using VanAn.Shared.Domain;
using Xunit;

namespace VanAn.Core.Tests.FinancialIntelligence;

/// <summary>
/// VA-FI-MVP2 (2026-08-21): Unit tests for BusinessProfile entity.
/// Verifies: construction sets Id = BusinessProfileId.Value, validation (Clamp, Max(0)),
/// TotalMonthlyFixedCost computed, Update increments Version (BR-006).
/// </summary>
public class BusinessProfileTests
{
    private static readonly Guid TenantGuid = Guid.NewGuid();
    private static readonly TenantId Tenant = new(TenantGuid);

    [Fact]
    public void Constructor_SetsId_ToBusinessProfileIdValue()
    {
        // Act
        var profile = NewDefaultProfile();

        // Assert — Single-Identity Pattern: Id (PK) = BusinessProfileId.Value
        Assert.Equal(profile.BusinessProfileId.Value, profile.Id);
        Assert.NotEqual(Guid.Empty, profile.Id);
    }

    [Fact]
    public void Constructor_SetsAllFixedCosts_AndVersion_Initial()
    {
        // Act
        var profile = new BusinessProfile(
            Tenant,
            monthlyRent: 5_000_000m, monthlyPayroll: 12_000_000m, monthlyUtilities: 1_500_000m,
            monthlyMarketing: 500_000m, monthlyLogistics: 800_000m, monthlyOtherOpEx: 300_000m,
            monthlyDepreciation: 1_000_000m,
            dailyCapacityUnits: 200, operatingDaysPerMonth: 30,
            pricingModel: PricingModel.FixedPrice, notes: "test");

        // Assert
        Assert.Equal(5_000_000m, profile.MonthlyRent);
        Assert.Equal(12_000_000m, profile.MonthlyPayroll);
        Assert.Equal(1_500_000m, profile.MonthlyUtilities);
        Assert.Equal(500_000m, profile.MonthlyMarketing);
        Assert.Equal(800_000m, profile.MonthlyLogistics);
        Assert.Equal(300_000m, profile.MonthlyOtherOpEx);
        Assert.Equal(1_000_000m, profile.MonthlyDepreciation);
        Assert.Equal(200, profile.DailyCapacityUnits);
        Assert.Equal(30, profile.OperatingDaysPerMonth);
        Assert.Equal(PricingModel.FixedPrice, profile.PricingModel);
        Assert.Equal("test", profile.Notes);
        Assert.Equal(FinancialModelVersion.Initial, profile.Version);
    }

    [Fact]
    public void Constructor_NegativeCosts_ClampedToZero()
    {
        // Act
        var profile = new BusinessProfile(
            Tenant,
            monthlyRent: -1_000_000m, monthlyPayroll: -500_000m, monthlyUtilities: -100m,
            monthlyMarketing: -50m, monthlyLogistics: -25m, monthlyOtherOpEx: -10m,
            monthlyDepreciation: -1m,
            dailyCapacityUnits: -10, operatingDaysPerMonth: 0,
            pricingModel: PricingModel.FixedPrice);

        // Assert — Max(0, ...) clamp
        Assert.Equal(0m, profile.MonthlyRent);
        Assert.Equal(0m, profile.MonthlyPayroll);
        Assert.Equal(0m, profile.MonthlyUtilities);
        Assert.Equal(0m, profile.MonthlyMarketing);
        Assert.Equal(0m, profile.MonthlyLogistics);
        Assert.Equal(0m, profile.MonthlyOtherOpEx);
        Assert.Equal(0m, profile.MonthlyDepreciation);
        Assert.Equal(0, profile.DailyCapacityUnits);
        // OperatingDaysPerMonth: Clamp(value, 1, 31) — 0 → 1
        Assert.Equal(1, profile.OperatingDaysPerMonth);
    }

    [Fact]
    public void Constructor_OperatingDaysPerMonth_Clamps_1_To_31()
    {
        // Act — below min (0 → 1)
        var p1 = new BusinessProfile(Tenant, 0, 0, 0, 0, 0, 0, 0, 0, 0, PricingModel.FixedPrice);
        Assert.Equal(1, p1.OperatingDaysPerMonth);

        // Act — above max (50 → 31)
        var p2 = new BusinessProfile(Tenant, 0, 0, 0, 0, 0, 0, 0, 0, 50, PricingModel.FixedPrice);
        Assert.Equal(31, p2.OperatingDaysPerMonth);

        // Act — in range (15 → 15)
        var p3 = new BusinessProfile(Tenant, 0, 0, 0, 0, 0, 0, 0, 0, 15, PricingModel.FixedPrice);
        Assert.Equal(15, p3.OperatingDaysPerMonth);
    }

    [Fact]
    public void TotalMonthlyFixedCost_SumsAll7Costs()
    {
        // Act
        var profile = new BusinessProfile(
            Tenant,
            monthlyRent: 5_000_000m, monthlyPayroll: 12_000_000m, monthlyUtilities: 1_500_000m,
            monthlyMarketing: 500_000m, monthlyLogistics: 800_000m, monthlyOtherOpEx: 300_000m,
            monthlyDepreciation: 1_000_000m,
            dailyCapacityUnits: 200, operatingDaysPerMonth: 30,
            pricingModel: PricingModel.FixedPrice);

        // Assert — computed (not stored)
        Assert.Equal(21_100_000m, profile.TotalMonthlyFixedCost);
    }

    [Fact]
    public void TotalMonthlyFixedCost_AllZeros_ReturnsZero()
    {
        // Act
        var profile = NewDefaultProfile();

        // Assert
        Assert.Equal(0m, profile.TotalMonthlyFixedCost);
    }

    [Fact]
    public void Update_ChangesAllFields_AndIncrementsVersion()
    {
        // Arrange
        var profile = NewDefaultProfile();
        var initialVersion = profile.Version;

        // Act
        profile.Update(
            monthlyRent: 8_000_000m, monthlyPayroll: 15_000_000m, monthlyUtilities: 2_000_000m,
            monthlyMarketing: 1_000_000m, monthlyLogistics: 1_200_000m, monthlyOtherOpEx: 500_000m,
            monthlyDepreciation: 1_500_000m,
            dailyCapacityUnits: 250, operatingDaysPerMonth: 26,
            pricingModel: PricingModel.DynamicPricing, notes: "updated");

        // Assert — all fields updated
        Assert.Equal(8_000_000m, profile.MonthlyRent);
        Assert.Equal(15_000_000m, profile.MonthlyPayroll);
        Assert.Equal(2_000_000m, profile.MonthlyUtilities);
        Assert.Equal(1_000_000m, profile.MonthlyMarketing);
        Assert.Equal(1_200_000m, profile.MonthlyLogistics);
        Assert.Equal(500_000m, profile.MonthlyOtherOpEx);
        Assert.Equal(1_500_000m, profile.MonthlyDepreciation);
        Assert.Equal(250, profile.DailyCapacityUnits);
        Assert.Equal(26, profile.OperatingDaysPerMonth);
        Assert.Equal(PricingModel.DynamicPricing, profile.PricingModel);
        Assert.Equal("updated", profile.Notes);

        // Assert — Version incremented (BR-006)
        Assert.Equal(initialVersion with { Minor = initialVersion.Minor + 1 }, profile.Version);
    }

    [Fact]
    public void Update_NegativeCosts_ClampedToZero()
    {
        // Arrange
        var profile = NewDefaultProfile();

        // Act
        profile.Update(
            monthlyRent: -100m, monthlyPayroll: -50m, monthlyUtilities: -10m,
            monthlyMarketing: -5m, monthlyLogistics: -3m, monthlyOtherOpEx: -1m,
            monthlyDepreciation: -1m,
            dailyCapacityUnits: -10, operatingDaysPerMonth: 0,
            pricingModel: PricingModel.Mixed);

        // Assert
        Assert.Equal(0m, profile.MonthlyRent);
        Assert.Equal(0m, profile.MonthlyPayroll);
        Assert.Equal(0m, profile.MonthlyUtilities);
        Assert.Equal(0m, profile.MonthlyMarketing);
        Assert.Equal(0m, profile.MonthlyLogistics);
        Assert.Equal(0m, profile.MonthlyOtherOpEx);
        Assert.Equal(0m, profile.MonthlyDepreciation);
        Assert.Equal(0, profile.DailyCapacityUnits);
        Assert.Equal(1, profile.OperatingDaysPerMonth);
    }

    [Fact]
    public void MultipleUpdates_ContinuouslyIncrementVersion()
    {
        // Arrange
        var profile = NewDefaultProfile();
        var initialMinor = profile.Version.Minor;

        // Act — 3 updates
        profile.Update(1, 0, 0, 0, 0, 0, 0, 0, 30, PricingModel.FixedPrice);
        profile.Update(2, 0, 0, 0, 0, 0, 0, 0, 30, PricingModel.FixedPrice);
        profile.Update(3, 0, 0, 0, 0, 0, 0, 0, 30, PricingModel.FixedPrice);

        // Assert — Minor = initial + 3
        Assert.Equal(initialMinor + 3, profile.Version.Minor);
    }

    private static BusinessProfile NewDefaultProfile() => new(
        Tenant,
        monthlyRent: 0m, monthlyPayroll: 0m, monthlyUtilities: 0m,
        monthlyMarketing: 0m, monthlyLogistics: 0m, monthlyOtherOpEx: 0m,
        monthlyDepreciation: 0m,
        dailyCapacityUnits: 0, operatingDaysPerMonth: 30,
        pricingModel: PricingModel.FixedPrice);
}

/// <summary>
/// VA-FI-MVP2 (2026-08-21): Unit tests for FinancialModelVersion value object.
/// Verifies: Initial, Increment, Parse, ToString.
/// </summary>
public class FinancialModelVersionTests
{
    [Fact]
    public void Initial_Is_1_0()
    {
        Assert.Equal(1, FinancialModelVersion.Initial.Major);
        Assert.Equal(0, FinancialModelVersion.Initial.Minor);
    }

    [Fact]
    public void Increment_IncrementsMinor_KeepsMajor()
    {
        // Arrange
        var v = FinancialModelVersion.Initial; // 1.0

        // Act
        var v2 = v.Increment();

        // Assert
        Assert.Equal(1, v2.Major);
        Assert.Equal(1, v2.Minor);
    }

    [Fact]
    public void MultipleIncrements_AllIncrementsMinor()
    {
        var v = FinancialModelVersion.Initial;
        v = v.Increment(); // 1.1
        v = v.Increment(); // 1.2
        v = v.Increment(); // 1.3

        Assert.Equal("1.3", v.ToString());
    }

    [Fact]
    public void ToString_Returns_Major_Minor()
    {
        Assert.Equal("1.0", new FinancialModelVersion(1, 0).ToString());
        Assert.Equal("2.5", new FinancialModelVersion(2, 5).ToString());
    }

    [Fact]
    public void Parse_ValidString_ReturnsVersion()
    {
        Assert.Equal(new FinancialModelVersion(1, 0), FinancialModelVersion.Parse("1.0"));
        Assert.Equal(new FinancialModelVersion(2, 7), FinancialModelVersion.Parse("2.7"));
    }

    [Fact]
    public void Parse_InvalidString_ReturnsInitial()
    {
        Assert.Equal(FinancialModelVersion.Initial, FinancialModelVersion.Parse(""));
        Assert.Equal(FinancialModelVersion.Initial, FinancialModelVersion.Parse("   "));
        Assert.Equal(FinancialModelVersion.Initial, FinancialModelVersion.Parse("invalid"));
        Assert.Equal(FinancialModelVersion.Initial, FinancialModelVersion.Parse("1"));
        Assert.Equal(FinancialModelVersion.Initial, FinancialModelVersion.Parse("1.2.3"));
        Assert.Equal(FinancialModelVersion.Initial, FinancialModelVersion.Parse("a.b"));
    }
}
