using VanAn.UI.Platform.Models;
using VanAn.UI.Platform.Components;

namespace VanAn.ShopERP.Tests.Components;

/// <summary>
/// 2026-10-17 (user directive — nav Kế Toán nhóm con): VanANavigation hỗ trợ nhóm lồng nhau
/// (children có Children → render sub-group details/summary) thay vì chỉ 1 cấp như trước.
/// </summary>
public class VanANavigationNestedTests : ComponentTestBase
{
    private static List<NavigationItem> BuildNestedMenu()
    {
        return
        [
            new NavigationItem
            {
                Title = "Kế Toán",
                Icon = "journal-text",
                Children =
                {
                    new NavigationItem { Title = "Kế Toán", Url = "/accounting" },
                    new NavigationItem
                    {
                        Title = "Sổ Sách",
                        Icon = "journal-bookmark",
                        Children =
                        {
                            new NavigationItem { Title = "Sổ Nhật Ký Chung", Url = "/accounting/journal" },
                            new NavigationItem { Title = "Sổ Cái", Url = "/accounting/ledger" },
                            new NavigationItem { Title = "Sổ Chi Tiết", Url = "/accounting/sub-ledger" },
                        }
                    },
                    new NavigationItem
                    {
                        Title = "Báo Cáo Quản Trị",
                        Icon = "clipboard2-data",
                        Children =
                        {
                            new NavigationItem { Title = "Công Nợ", Url = "/accounting/cong-no" },
                        }
                    },
                }
            }
        ];
    }

    [Fact]
    public void VanANavigation_RendersNestedSubGroups()
    {
        var cut = RenderComponent<VanANavigation>(p => p.Add(c => c.MenuItems, BuildNestedMenu()));

        // Nhóm top-level "Kế Toán" + 2 nhóm con render dạng details/summary.
        var summaries = cut.FindAll("summary.vanan-navigation__summary");
        summaries.Select(s => s.TextContent.Trim()).Should().Contain(["Kế Toán", "Sổ Sách", "Báo Cáo Quản Trị"]);

        // Link trong nhóm con cấp 2 render dạng sublink.
        cut.Markup.Should().Contain("href=\"/accounting/ledger\"");
        cut.Markup.Should().Contain("href=\"/accounting/sub-ledger\"");
        cut.Markup.Should().Contain("href=\"/accounting/cong-no\"");
        cut.FindAll("li.vanan-navigation__subitem--group").Count.Should().Be(2);
    }

    [Fact]
    public void VanANavigation_NestedGroupAutoOpens_WhenCurrentUrlInside()
    {
        // Đang ở /accounting/ledger → cả "Kế Toán" lẫn "Sổ Sách" phải open (GroupContainsUrl đệ quy).
        var nav = Services.GetService(typeof(Microsoft.AspNetCore.Components.NavigationManager))
                  as Microsoft.AspNetCore.Components.NavigationManager;
        nav!.NavigateTo("/accounting/ledger");

        var cut = RenderComponent<VanANavigation>(p => p.Add(c => c.MenuItems, BuildNestedMenu()));

        var openDetails = cut.FindAll("details.vanan-navigation__details[open]");
        openDetails.Count.Should().Be(2); // Kế Toán + Sổ Sách
        var bcqt = cut.FindAll("summary.vanan-navigation__summary")
                      .First(s => s.TextContent.Contains("Báo Cáo Quản Trị"));
        bcqt.ParentElement!.HasAttribute("open").Should().BeFalse();
    }
}
