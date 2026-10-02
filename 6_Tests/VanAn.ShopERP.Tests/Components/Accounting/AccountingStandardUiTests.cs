using Bunit;
using Xunit;
using Moq;
using VanAn.CoreHub.Services;
using VanAn.ShopERP.Services;
using VanAn.Shared.Domain;
using FluentAssertions;

namespace VanAn.ShopERP.Tests.Components.Accounting;

/// <summary>
/// TT 71 Phase 6 (S4) — T7: UI standard select auto-map theo tenant type.
/// Tenant HTX → auto chọn TT 71 trên màn hình báo cáo; hub ẩn B03 cho HTX.
/// </summary>
public class AccountingStandardUiTests : ComponentTestBase
{
    private void OverrideTenantType(TenantType? type)
    {
        var featureFlagMock = new Mock<IVasFeatureFlagService>();
        featureFlagMock.Setup(f => f.GetTenantTypeAsync(It.IsAny<TenantId>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(type);
        Services.AddSingleton(featureFlagMock.Object);
    }

    // T7: TrialBalance — tenant HTX auto chọn TT 71 (select value + service được gọi với TT71)
    [Fact]
    public void TrialBalance_HtxTenant_AutoSelectsTt71()
    {
        // Arrange — service mocks cần thiết cho page
        var tbMock = new Mock<ITrialBalanceService>();
        tbMock.Setup(s => s.GenerateAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<AccountingStandard>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TrialBalance(new AccountingPeriod(2026, 5), DateTime.UtcNow, Array.Empty<TrialBalanceAccount>(), 0, 0, true));
        Services.AddSingleton(tbMock.Object);
        Services.AddSingleton(new Mock<IFinancialReportExportService>().Object);
        OverrideTenantType(TenantType.HTX);

        // Act
        var cut = RenderComponent<ShopERP.Components.Pages.Accounting.TrialBalance>();

        // Assert — option TT 71 hiển thị + auto-map select value (option TT71 được chọn)
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("TT 71/2024 (HTX)"));
        var tt71Option = cut.Find($"option[value=\"{AccountingStandard.TT71_2024}\"]");
        tt71Option.HasAttribute("selected").Should().BeTrue("tenant HTX phải auto-map sang TT 71");

        // Report được generate với standard TT 71 (không phải TT 133 default)
        tbMock.Verify(s => s.GenerateAsync(
            It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(),
            AccountingStandard.TT71_2024, It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    // T7: TrialBalance — tenant DN vừa (Enterprise_SME) giữ TT 133 (không regress)
    [Fact]
    public void TrialBalance_EnterpriseTenant_KeepsTt133()
    {
        var tbMock = new Mock<ITrialBalanceService>();
        tbMock.Setup(s => s.GenerateAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<AccountingStandard>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TrialBalance(new AccountingPeriod(2026, 5), DateTime.UtcNow, Array.Empty<TrialBalanceAccount>(), 0, 0, true));
        Services.AddSingleton(tbMock.Object);
        Services.AddSingleton(new Mock<IFinancialReportExportService>().Object);
        OverrideTenantType(TenantType.Enterprise_SME);

        var cut = RenderComponent<ShopERP.Components.Pages.Accounting.TrialBalance>();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("TT 133/2016 (DN vừa)"));
        tbMock.Verify(s => s.GenerateAsync(
            It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(),
            AccountingStandard.TT133_2016, It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    // T7: FinancialReports hub — tenant HTX: B01/B02/B09-HTX + ẨN B03
    [Fact]
    public void FinancialReports_HtxTenant_HidesCashFlow_ShowsHtxForms()
    {
        OverrideTenantType(TenantType.HTX);

        var cut = RenderComponent<ShopERP.Components.Pages.Accounting.FinancialReports>();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("B 01-HTX"));
        cut.Markup.Should().Contain("B 02-HTX");
        cut.Markup.Should().Contain("B 09-HTX");
        cut.Markup.Should().NotContain("B 03-DN");
        cut.Markup.Should().Contain("TT 71/2024/TT-BTC");
    }

    // T7: FinancialReports hub — tenant DN: giữ B01/B02/B03/B09-DN (không regress)
    [Fact]
    public void FinancialReports_EnterpriseTenant_KeepsDnForms()
    {
        OverrideTenantType(TenantType.Enterprise_SME);

        var cut = RenderComponent<ShopERP.Components.Pages.Accounting.FinancialReports>();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("B 01-DN"));
        cut.Markup.Should().Contain("B 03-DN");
        cut.Markup.Should().NotContain("B 01-HTX");
    }
}
