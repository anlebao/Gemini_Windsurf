using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using VanAn.CoreHub.Infrastructure.Entities;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Services.GeneralLedger;
using VanAn.Shared.Domain;
using Xunit;

namespace VanAn.ShopERP.Tests.Components.Accounting;

/// <summary>
/// W5/F (FR-RECNC-001) — bUnit cho BankBook.razor: section "Đối chiếu sao kê ngân hàng"
/// (InputFile + nút Import/Đối chiếu) hiển thị khi tenant có TK 112x.
/// NOTE: @rendermode InteractiveServer — bUnit không click được; render-only.
/// </summary>
[Trait("Category", "AccountingUI")]
public class BankBookPageTests : ComponentTestBase
{
    private static readonly TenantId TestTenantId = new(Guid.Parse("00000000-0000-0000-0000-000000000001"));

    private void RegisterServices(IReadOnlyList<BankAccountEntity> accounts)
    {
        var gl = new Mock<IGeneralLedgerService>();
        gl.Setup(g => g.GetSubLedgerAsync(
                It.IsAny<TenantId>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubLedgerReportDto("1121", "Tiền gửi ngân hàng", null, 0, 0, 0, 0, []));
        Services.AddSingleton(gl.Object);

        var banks = new Mock<IBankAccountService>();
        banks.Setup(b => b.ListAsync(It.IsAny<TenantId>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(accounts);
        Services.AddSingleton(banks.Object);
    }

    [Fact(DisplayName = "W5-BB-1: BankBook renders 'Đối chiếu sao kê ngân hàng' section khi có TK 112x")]
    public void BankBook_Renders_StatementSection_WhenBankAccountExists()
    {
        var account = new BankAccountEntity(TestTenantId, "1121", "TK VCB", "Vietcombank", "001122");
        RegisterServices([account]);

        var cut = RenderComponent<ShopERP.Components.Pages.Accounting.BankBook>();

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("Đối chiếu sao kê ngân hàng");
            cut.Find("[data-testid='bankstmt-file-input']").Should().NotBeNull();
            cut.Find("[data-testid='bankstmt-reconcile']").Should().NotBeNull();
        }, TimeSpan.FromSeconds(5));
    }

    [Fact(DisplayName = "W5-BB-2: BankBook ẩn statement section khi chưa có TK 112x")]
    public void BankBook_Hides_StatementSection_WhenNoBankAccount()
    {
        RegisterServices([]);

        var cut = RenderComponent<ShopERP.Components.Pages.Accounting.BankBook>();

        cut.WaitForAssertion(() =>
            cut.Markup.Should().Contain("Chưa có tài khoản ngân hàng"), TimeSpan.FromSeconds(5));
        cut.Markup.Should().NotContain("bankstmt-file-input");
    }
}
