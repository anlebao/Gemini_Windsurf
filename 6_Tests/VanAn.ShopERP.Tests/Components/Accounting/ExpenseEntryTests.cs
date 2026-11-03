using Bunit;
using System.Reflection;
using Xunit;
using Moq;
using VanAn.Shared.DTOs;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Services.CongNo;
using VanAn.Shared.Domain;
using VanAn.ShopERP.Services;
using VanAn.ShopERP.Components.Accounting;
using FluentAssertions;

namespace VanAn.ShopERP.Tests.Components.Accounting;

public class ExpenseEntryTests : ComponentTestBase
{
    [Fact]
    public void ExpenseEntry_ShouldRender_WhenComponentMounted()
    {
        // Act - Render component with layout
        var cut = RenderComponent<ShopERP.Components.Pages.Accounting.ExpenseEntry>();

        // Assert - Verify component renders
        cut.Markup.Should().Contain("Nhập Chi Phí");
    }

    [Fact]
    public void ExpenseEntry_ShouldHaveServiceRegistered_WhenComponentMounted()
    {
        // Arrange
        var mockService = new Mock<IAccountingService>();
        Services.AddSingleton(mockService.Object);

        // Act
        var cut = RenderComponent<ShopERP.Components.Pages.Accounting.ExpenseEntry>();

        // Assert - Component renders without errors, service is available
        cut.Markup.Should().NotBeNullOrEmpty();
    }

    // T6 (TT 71 Phase 4a): tenant HTX → phiếu chi chỉ 642/658 (KHÔNG 621/622/627/641)
    [Fact]
    public void ExpenseEntry_HtxTenant_ShowsOnlyTt71ExpenseAccounts()
    {
        // Arrange — override FeatureFlagService mock: tenant type = HTX
        var featureFlagMock = new Mock<IVasFeatureFlagService>();
        featureFlagMock.Setup(f => f.GetTenantTypeAsync(It.IsAny<TenantId>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(TenantType.HTX);
        Services.AddSingleton(featureFlagMock.Object);

        // Act
        var cut = RenderComponent<ShopERP.Components.Pages.Accounting.ExpenseEntry>();

        // Assert — options theo TT 71 (Phụ lục I): 642/658, không có 621/622/627/641
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("658 - Chi phí khác"));
        cut.Markup.Should().Contain("642 - Chi phí quản lý kinh doanh");
        cut.Markup.Should().NotContain("621 - Chi phí nguyên vật liệu");
        cut.Markup.Should().NotContain("622 - Chi phí nhân công");
        cut.Markup.Should().NotContain("627 - Chi phí bán hàng");
        cut.Markup.Should().NotContain("641 - Chi phí quản lý doanh nghiệp");
    }

    // T6 (TT 71 Phase 4a): tenant DN → options giữ nguyên (không regress)
    [Fact]
    public void ExpenseEntry_EnterpriseTenant_KeepsExistingAccounts()
    {
        // Arrange — tenant type = Enterprise_SME (DN vừa → TT 133)
        var featureFlagMock = new Mock<IVasFeatureFlagService>();
        featureFlagMock.Setup(f => f.GetTenantTypeAsync(It.IsAny<TenantId>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(TenantType.Enterprise_SME);
        Services.AddSingleton(featureFlagMock.Object);

        // Act
        var cut = RenderComponent<ShopERP.Components.Pages.Accounting.ExpenseEntry>();

        // Assert — options DN giữ nguyên (621/622/627/641/642), không có 658
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("627 - Chi phí bán hàng"));
        cut.Markup.Should().Contain("621 - Chi phí nguyên vật liệu");
        cut.Markup.Should().NotContain("658 - Chi phí khác");
    }

    // THU CHI & CÔNG NỢ MVP (P2.2, SRS §5.2 FR-4): phiếu chi 3 loại
    [Fact]
    public void ExpenseEntry_ShouldRenderVoucherTypeSelect_WithThreeOptions()
    {
        // Act
        var cut = RenderComponent<ShopERP.Components.Pages.Accounting.ExpenseEntry>();

        // Assert — dropdown "Loại Phiếu Chi" + 3 lựa chọn
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Loại Phiếu Chi"));
        cut.Markup.Should().Contain("Chi phí");
        cut.Markup.Should().Contain("Ghi nhận phải trả");
        cut.Markup.Should().Contain("Trả tiền người bán");
    }

    [Fact]
    public void ExpenseEntry_CongNoType_ShowsDoiTuongRequired_AndAccount331()
    {
        // Act
        var cut = RenderComponent<ShopERP.Components.Pages.Accounting.ExpenseEntry>();

        // Chọn "Ghi nhận phải trả" → hiện Đối tượng + TK 331, ẩn category/vendor thường
        cut.Find("#voucherType").Change("payable");
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Đối Tượng (nhà cung cấp)"));
        cut.Markup.Should().Contain("331 - Phải trả người bán");
        cut.Markup.Should().NotContain("627 - Chi phí bán hàng");
        cut.Markup.Should().NotContain("Loại Chi Phí");
    }

    [Fact]
    public void ExpenseEntry_ExpenseType_DoesNotShowDoiTuong()
    {
        // Act — loại mặc định "Chi phí"
        var cut = RenderComponent<ShopERP.Components.Pages.Accounting.ExpenseEntry>();

        // Assert — không có ô Đối tượng công nợ
        cut.WaitForAssertion(() => cut.Markup.Should().NotContain("Đối Tượng (nhà cung cấp)"));
    }

    [Fact]
    public void ExpenseEntry_CongNoType_MstLookup_FillsDoiTuong()
    {
        // Arrange — mock tra MST
        var businessInfoMock = new Mock<IBusinessInfoApiClient>();
        businessInfoMock.Setup(b => b.GetByMstAsync("0312345678", It.IsAny<CancellationToken>()))
                        .ReturnsAsync(new BusinessInfoDto
                        {
                            TaxCode = "0312345678",
                            BusinessName = "Công Ty TNHH X",
                            Status = "active"
                        });
        Services.AddSingleton(businessInfoMock.Object);

        // Act
        var cut = RenderComponent<ShopERP.Components.Pages.Accounting.ExpenseEntry>();

        // Chọn loại công nợ + nhập MST + bấm Tra cứu [G11]
        cut.Find("#voucherType").Change("payable");
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Đối Tượng (nhà cung cấp)"));
        cut.Find("#mst").Change("0312345678");
        cut.FindAll("button").First(b => b.TextContent.Contains("Tra cứu MST")).Click();

        // Assert — tự điền tên công ty vào Đối tượng [G11]
        cut.WaitForAssertion(() => cut.Find("#doiTuong").GetAttribute("value").Should().Be("Công Ty TNHH X"));
    }

    [Fact]
    public void ExpenseEntry_CongNoType_ShowsDoiTuongMarkedRequired()
    {
        // Act
        var cut = RenderComponent<ShopERP.Components.Pages.Accounting.ExpenseEntry>();

        // Chọn "Ghi nhận phải trả" → ô Đối tượng hiện kèm dấu * bắt buộc
        cut.Find("#voucherType").Change("payable");
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Đối Tượng (nhà cung cấp)"));
        cut.Markup.Should().Contain("<span class=\"text-danger\">*</span>");
    }

    [Fact]
    public void ExpenseEntry_BalanceWarning_RendersBanner()
    {
        // W4 (P1.3): banner cảnh báo âm quỹ 111 render khi balanceWarning được set
        // (non-blocking — set trước khi lưu, không chặn phiếu chi).
        var cut = RenderComponent<ShopERP.Components.Pages.Accounting.ExpenseEntry>();
        var pageType = typeof(ShopERP.Components.Pages.Accounting.ExpenseEntry);
        pageType.GetField("balanceWarning", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(cut.Instance, "⚠ Âm quỹ tiền mặt: TK 111 hiện có 0đ, khoản chi 500.000đ sẽ làm quỹ âm -500.000đ.");
        cut.Render();

        cut.Markup.Should().Contain("expense-balance-warning");
        cut.Markup.Should().Contain("Âm quỹ tiền mặt");
    }

    // ---------- PH3: session recovery (full reload — deterministic, không mất session) ----------

    private void EnableSmartEntry()
    {
        var settings = new Mock<VanAn.Shared.Services.IShopFeatureSettingsService>();
        settings.Setup(s => s.IsEnabledAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
        Services.AddSingleton(settings.Object);
    }

    [Fact]
    public void ExpenseEntry_Recovery_MultiJson_RestoresMultiWithSavedIndices()
    {
        // PH3: khôi phục từ multi JSON — không re-parse LLM, không mất session.
        EnableSmartEntry();
        var multi = new List<ParsedVoucher>
        {
            new(VoucherIntent.Revenue, 2_000_000m, new DateTime(2026, 11, 3), "cash", null, null, null, null, null, "thu tiền bán hàng 2 triệu", "High", Array.Empty<string>(), Array.Empty<string>()),
            new(VoucherIntent.Expense, 1_000_000m, new DateTime(2026, 11, 3), "cash", null, null, null, "utilities", null, "chi tiền điện 1 triệu", "High", Array.Empty<string>(), Array.Empty<string>()),
        };
        JSInterop.Setup<string>("vananLoadSmartMulti").SetResult(SmartEntrySessionCodec.Serialize(multi));
        JSInterop.Setup<string>("vananLoadSmartText").SetResult("thu tiền bán hàng 2 triệu và chi tiền điện 1 triệu");
        JSInterop.Setup<List<int>>("vananLoadSmartProcessed").SetResult(new List<int> { 0 });

        var cut = RenderComponent<ShopERP.Components.Pages.Accounting.ExpenseEntry>();

        // Bar hiện + 2 phiếu + phiếu thu (index 0 — đã lưu ở trang trước) đánh dấu "✓ Đã lưu"
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("[data-testid='smart-entry-multi-item']").Count));
        Assert.Single(cut.FindAll("[data-testid='smart-entry-multi-done']"));
        // Nút Áp của phiếu đã lưu bị khoá (disabled — chống áp lại) → đúng 1 nút áp được
        var applyButtons = cut.FindAll("[data-testid='smart-entry-multi-apply']");
        Assert.Equal(2, applyButtons.Count);
        Assert.Equal(1, applyButtons.Count(b => !b.HasAttribute("disabled")));
    }

    [Fact]
    public void ExpenseEntry_Recovery_TextOnly_ShowsSingleResult_NoWipe()
    {
        // PH3: fallback text-only (dữ liệu cũ) — single được HIỂN THỊ (không vứt bỏ), không wipe.
        EnableSmartEntry();
        JSInterop.Setup<string>("vananLoadSmartMulti").SetResult("");
        JSInterop.Setup<string>("vananLoadSmartText").SetResult("chi tiền điện 1 triệu");

        var cut = RenderComponent<ShopERP.Components.Pages.Accounting.ExpenseEntry>();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='smart-entry-result']")));
        cut.WaitForAssertion(() => Assert.Equal("Chi phí", cut.Find("[data-testid='smart-entry-intent']").TextContent.Trim()));
    }

    [Fact]
    public void ExpenseEntry_SpaNav_SessionMulti_RestoresWithSavedIndices()
    {
        // PH3 RV#4 regression: SPA navigation (circuit giữ) — restore từ SmartSessionState
        // (OnInitializedAsync) PHẢI truyền ProcessedIndices → badge "✓ Đã lưu" giữ qua trang.
        EnableSmartEntry();
        var session = Services.GetRequiredService<SmartEntrySessionState>();
        var multi = new List<ParsedVoucher>
        {
            new(VoucherIntent.Revenue, 2_000_000m, new DateTime(2026, 11, 3), "cash", null, null, null, null, null, "thu tiền bán hàng 2 triệu", "High", Array.Empty<string>(), Array.Empty<string>()),
            new(VoucherIntent.Expense, 1_000_000m, new DateTime(2026, 11, 3), "cash", null, null, null, "utilities", null, "chi tiền điện 1 triệu", "High", Array.Empty<string>(), Array.Empty<string>()),
        };
        session.Set(multi, "thu tiền bán hàng 2 triệu và chi tiền điện 1 triệu", new List<int> { 0 });

        var cut = RenderComponent<ShopERP.Components.Pages.Accounting.ExpenseEntry>();

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("[data-testid='smart-entry-multi-item']").Count));
        Assert.Single(cut.FindAll("[data-testid='smart-entry-multi-done']"));
    }
}
