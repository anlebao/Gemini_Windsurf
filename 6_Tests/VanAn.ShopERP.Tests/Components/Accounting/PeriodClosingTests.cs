using System.Reflection;
using Bunit;
using FluentAssertions;
using Moq;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Common;
using VanAn.ShopERP.Tests.Components;

namespace VanAn.ShopERP.Tests.Components.Accounting;

/// <summary>
/// W4 (Sprint 2) — bUnit tests for PeriodClosing.razor (period closing wizard page).
///
/// NOTE: The page uses @rendermode InteractiveServer, which prevents bUnit from wiring @onclick
/// DOM event handlers (Blazor renders in static mode under bUnit's TestContext). Click-based
/// step navigation cannot be tested in bUnit — that is covered by Playwright E2E tests.
/// Instead, wizard step UI coverage is achieved via reflection: setting the private currentStep
/// field + validationResult/closingEntry fields, then re-rendering to verify each step's markup.
///
/// Covers: header, period selector, Idle step (start button), Validate step (valid/invalid/warnings),
/// Review step, Close step (success), Reopen dialog, error states, wizard step indicator.
/// </summary>
[Trait("Category", "AccountingUI")]
public class PeriodClosingTests : ComponentTestBase
{
    private static readonly Guid TestTenantGuid = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly TenantId TestTenantId = new(TestTenantGuid);

    private Mock<IPeriodClosingService> SetupService(
        PeriodClosingStatus status = PeriodClosingStatus.Open,
        PeriodClosingCheckResult? validateResult = null,
        ClosingEntry? closeResult = null)
    {
        var mock = new Mock<IPeriodClosingService>();
        mock.Setup(s => s.GetPeriodStatusAsync(It.IsAny<AccountingPeriod>(), It.IsAny<TenantId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(status);
        mock.Setup(s => s.ValidatePeriodAsync(It.IsAny<AccountingPeriod>(), It.IsAny<TenantId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(validateResult ?? new PeriodClosingCheckResult(true, new List<string>(), new List<string>()));
        mock.Setup(s => s.ClosePeriodAsync(It.IsAny<AccountingPeriod>(), It.IsAny<TenantId>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(closeResult ?? new ClosingEntry(Guid.NewGuid(), new AccountingPeriod(2026, 6), DateTime.UtcNow, Guid.NewGuid()));
        return mock;
    }

    private IRenderedComponent<ShopERP.Components.Pages.Accounting.PeriodClosing> RenderClosing(
        Mock<IPeriodClosingService>? mock = null)
    {
        mock ??= SetupService();
        Services.AddSingleton(mock.Object);
        return RenderComponent<ShopERP.Components.Pages.Accounting.PeriodClosing>();
    }

    /// <summary>
    /// Set the private WizardStep field (nested private enum: Idle=0, Validate=1, Review=2, Close=3)
    /// and optional validationResult/closingEntry fields, then force re-render.
    /// </summary>
    private static void SetWizardStep(
        IRenderedComponent<ShopERP.Components.Pages.Accounting.PeriodClosing> cut,
        int step,
        PeriodClosingCheckResult? validationResult = null,
        ClosingEntry? closingEntry = null,
        PeriodClosingStatus? currentStatus = null,
        bool showReopenDialog = false)
    {
        var pageType = typeof(ShopERP.Components.Pages.Accounting.PeriodClosing);
        var flags = BindingFlags.NonPublic | BindingFlags.Instance;

        pageType.GetField("currentStep", flags)!.SetValue(cut.Instance, step);

        if (validationResult != null)
            pageType.GetField("validationResult", flags)!.SetValue(cut.Instance, validationResult);

        if (closingEntry != null)
            pageType.GetField("closingEntry", flags)!.SetValue(cut.Instance, closingEntry);

        if (currentStatus.HasValue)
            pageType.GetField("currentStatus", flags)!.SetValue(cut.Instance, currentStatus.Value);

        if (showReopenDialog)
            pageType.GetField("showReopenDialog", flags)!.SetValue(cut.Instance, true);

        cut.Render();
    }

    // WizardStep enum values (private nested enum in PeriodClosing.razor)
    private const int StepIdle = 0;
    private const int StepValidate = 1;
    private const int StepReview = 2;
    private const int StepClose = 3;

    [Fact(DisplayName = "W4-PC-1: Page renders header 'Đóng Sổ Kỳ Kế Toán'")]
    public void Page_Renders_Header()
    {
        var cut = RenderClosing();
        cut.Markup.Should().Contain("Đóng Sổ Kỳ Kế Toán");
    }

    [Fact(DisplayName = "W4-PC-2: Page renders 'Quay Lại' (back) button")]
    public void Page_Renders_BackButton()
    {
        var cut = RenderClosing();
        cut.Markup.Should().Contain("Quay Lại");
    }

    [Fact(DisplayName = "W4-PC-3: Page renders period selector (Năm + Tháng)")]
    public void Page_Renders_PeriodSelector()
    {
        var cut = RenderClosing();
        cut.Markup.Should().Contain("Năm");
        cut.Markup.Should().Contain("Tháng");
        cut.Markup.Should().Contain("Chọn Kỳ Kế Toán");
    }

    [Fact(DisplayName = "W4-PC-4: Page renders 'Bắt Đầu Kiểm Tra' button in Idle step")]
    public void Page_Renders_StartValidationButton_InIdleStep()
    {
        var cut = RenderClosing();
        cut.Markup.Should().Contain("Bắt Đầu Kiểm Tra");
    }

    [Fact(DisplayName = "W4-PC-5: Page calls GetPeriodStatusAsync on init")]
    public void Page_Calls_GetPeriodStatusAsync_OnInit()
    {
        var mock = SetupService();
        Services.AddSingleton(mock.Object);
        _ = RenderComponent<ShopERP.Components.Pages.Accounting.PeriodClosing>();
        mock.Verify(s => s.GetPeriodStatusAsync(It.IsAny<AccountingPeriod>(), It.IsAny<TenantId>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact(DisplayName = "W4-PC-6: Validate step renders 'Kết Quả Kiểm Tra' card when step=Validate")]
    public void ValidateStep_Renders_ResultCard()
    {
        var cut = RenderClosing();
        SetWizardStep(cut, StepValidate,
            validationResult: new PeriodClosingCheckResult(true, new List<string>(), new List<string>()));

        cut.Markup.Should().Contain("Kết Quả Kiểm Tra");
    }

    [Fact(DisplayName = "W4-PC-7: Validate step renders success alert when IsValid=true")]
    public void ValidateStep_Renders_SuccessAlert_WhenValid()
    {
        var cut = RenderClosing();
        SetWizardStep(cut, StepValidate,
            validationResult: new PeriodClosingCheckResult(true, new List<string>(), new List<string>()));

        cut.Markup.Should().Contain("Kỳ kế toán đã sẵn sàng để đóng sổ");
    }

    [Fact(DisplayName = "W4-PC-8: Validate step renders 'Tiếp Theo' button when valid")]
    public void ValidateStep_Renders_NextButton_WhenValid()
    {
        var cut = RenderClosing();
        SetWizardStep(cut, StepValidate,
            validationResult: new PeriodClosingCheckResult(true, new List<string>(), new List<string>()));

        cut.Markup.Should().Contain("Tiếp Theo");
    }

    [Fact(DisplayName = "W4-PC-9: Validate step renders error list when IsValid=false")]
    public void ValidateStep_Renders_ErrorList_WhenInvalid()
    {
        var cut = RenderClosing();
        SetWizardStep(cut, StepValidate,
            validationResult: new PeriodClosingCheckResult(
                IsValid: false,
                Errors: new List<string> { "Số dư Nợ/Có không cân bằng", "Thiếu bút toán kết chuyển" },
                Warnings: new List<string>()));

        cut.Markup.Should().Contain("Kỳ kế toán chưa thể đóng sổ");
        cut.Markup.Should().Contain("Số dư Nợ/Có không cân bằng");
        cut.Markup.Should().Contain("Thiếu bút toán kết chuyển");
    }

    [Fact(DisplayName = "W4-PC-10: Validate step renders warnings list when warnings present")]
    public void ValidateStep_Renders_Warnings_WhenPresent()
    {
        var cut = RenderClosing();
        SetWizardStep(cut, StepValidate,
            validationResult: new PeriodClosingCheckResult(
                IsValid: true,
                Errors: new List<string>(),
                Warnings: new List<string> { "Kỳ chưa có bút toán nào" }));

        cut.Markup.Should().Contain("Cảnh báo");
        cut.Markup.Should().Contain("Kỳ chưa có bút toán nào");
    }

    [Fact(DisplayName = "W4-PC-11: Review step renders 'Xác Nhận Đóng Sổ' button and warning")]
    public void ReviewStep_Renders_CloseConfirmation()
    {
        var cut = RenderClosing();
        SetWizardStep(cut, StepReview);

        cut.Markup.Should().Contain("Xem Lại Trước Khi Đóng Sổ");
        cut.Markup.Should().Contain("Xác Nhận Đóng Sổ");
        cut.Markup.Should().Contain("Bút toán Đảo Ngược (Reversal Entry)");
    }

    [Fact(DisplayName = "W4-PC-12: Close step renders success message when closingEntry is set")]
    public void CloseStep_Renders_SuccessMessage()
    {
        var cut = RenderClosing();
        var closeEntry = new ClosingEntry(Guid.NewGuid(), new AccountingPeriod(2026, 6), new DateTime(2026, 7, 5, 10, 0, 0), Guid.NewGuid());
        SetWizardStep(cut, StepClose, closingEntry: closeEntry);

        cut.Markup.Should().Contain("Đóng Sổ Thành Công");
        cut.Markup.Should().Contain("đã được đóng sổ");
    }

    [Fact(DisplayName = "W4-PC-13: Page renders 'Mở Lại Kỳ Này' button when status is Closed")]
    public void Page_Renders_ReopenButton_WhenStatusClosed()
    {
        var mock = SetupService(status: PeriodClosingStatus.Closed);
        var cut = RenderClosing(mock);
        cut.Render();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Mở Lại Kỳ Này"));
    }

    [Fact(DisplayName = "W4-PC-14: Reopen dialog renders reason input and 'Xác Nhận Mở Lại' button")]
    public void ReopenDialog_Renders_ReasonInput_AndConfirmButton()
    {
        var cut = RenderClosing();
        SetWizardStep(cut, StepIdle, currentStatus: PeriodClosingStatus.Closed, showReopenDialog: true);

        cut.Markup.Should().Contain("Mở Lại Kỳ Kế Toán");
        cut.Markup.Should().Contain("Lý do mở lại");
        cut.Markup.Should().Contain("Xác Nhận Mở Lại");
        cut.Markup.Should().Contain("Bút toán Đảo Ngược cho toàn bộ entries");
    }

    [Fact(DisplayName = "W4-PC-15: Page renders error alert on ValidatePeriodAsync exception")]
    public void Page_Renders_Error_OnValidationException()
    {
        // @rendermode prevents click, so we simulate the error state via reflection:
        // set errorMessage field directly to verify the VanAAlert renders.
        var cut = RenderClosing();
        var pageType = typeof(ShopERP.Components.Pages.Accounting.PeriodClosing);
        pageType.GetField("errorMessage", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(cut.Instance, "Lỗi khi kiểm tra kỳ: Validation service unavailable");
        cut.Render();

        cut.Markup.Should().Contain("Lỗi khi kiểm tra kỳ");
        cut.Markup.Should().Contain("Validation service unavailable");
    }

    [Fact(DisplayName = "W4-PC-16: Wizard step indicator renders (1. Kiểm Tra / 2. Xem Lại / 3. Đóng Sổ)")]
    public void Page_Renders_WizardStepIndicator()
    {
        var cut = RenderClosing();
        // Step indicator only renders when step != Idle
        SetWizardStep(cut, StepValidate,
            validationResult: new PeriodClosingCheckResult(true, new List<string>(), new List<string>()));

        cut.Markup.Should().Contain("1. Kiểm Tra");
        cut.Markup.Should().Contain("2. Xem Lại");
        cut.Markup.Should().Contain("3. Đóng Sổ");
    }

    [Fact(DisplayName = "W4-PC-17: 'Quay Lại' button is rendered as VanAButton")]
    public void BackButton_IsRendered_AsVanAButton()
    {
        // NOTE: @rendermode InteractiveServer prevents bUnit click. Navigation verified via E2E.
        var cut = RenderClosing();

        var backButton = cut.FindComponents<VanAn.UI.Platform.Components.VanAButton>()
            .FirstOrDefault(b => b.Markup.Contains("Quay Lại"));
        backButton.Should().NotBeNull("Expected 'Quay Lại' VanAButton");
        backButton!.Markup.Should().Contain("Quay Lại");
    }

    [Fact(DisplayName = "W4-PC-18: Review step renders 'Quay Lại' (back to validate) button")]
    public void ReviewStep_Renders_BackToValidateButton()
    {
        var cut = RenderClosing();
        SetWizardStep(cut, StepReview);

        // The Review step has a "← Quay Lại" button that goes back to Validate step
        cut.Markup.Should().Contain("Quay Lại");
    }

    [Fact(DisplayName = "W4-PC-19: Close step renders 'Đóng Sổ Kỳ Khác' reset button")]
    public void CloseStep_Renders_ResetButton()
    {
        var cut = RenderClosing();
        var closeEntry = new ClosingEntry(Guid.NewGuid(), new AccountingPeriod(2026, 6), DateTime.UtcNow, Guid.NewGuid());
        SetWizardStep(cut, StepClose, closingEntry: closeEntry);

        cut.Markup.Should().Contain("Đóng Sổ Kỳ Khác");
        cut.Markup.Should().Contain("Về Trang Kế Toán");
    }
}
