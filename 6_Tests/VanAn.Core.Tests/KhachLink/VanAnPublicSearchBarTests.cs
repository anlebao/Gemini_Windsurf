using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using VanAn.UI.Platform.Adapters;
using VanAn.UI.Platform.Components.Composite;
using VanAn.UI.Platform.Core.Interfaces;
using Xunit;

namespace VanAn.Core.Tests.KhachLink;

/// <summary>
/// Search unification (task_public_search_unification): additive params that let
/// StoreSearchBar/Home compose the shared bar without losing e2e CSS hooks —
/// GroupCssClass/SearchButtonCssClass/VoiceButtonCssClass/InputMaxLength,
/// listening class on mic, Loading → spinner on the search button.
/// </summary>
public class VanAnPublicSearchBarTests : TestContext
{
    public VanAnPublicSearchBarTests()
        => Services.AddSingleton<ICssAdapter, BootstrapAdapter>();

    [Fact]
    public void GroupCssClass_AppliesToRootInputGroup()
    {
        var cut = RenderComponent<VanAnPublicSearchBar>(p => p
            .Add(c => c.GroupCssClass, "input-group-lg"));

        cut.Find(".input-group").ClassList.Should().Contain("input-group-lg");
    }

    [Fact]
    public void SearchButtonCssClass_AppliesToSearchButton()
    {
        var cut = RenderComponent<VanAnPublicSearchBar>(p => p
            .Add(c => c.SearchButtonCssClass, "store-search-btn"));

        cut.Find("button.store-search-btn").Should().NotBeNull();
    }

    [Fact]
    public void VoiceButtonCssClass_AppliesToMicButton()
    {
        var cut = RenderComponent<VanAnPublicSearchBar>(p => p
            .Add(c => c.ShowVoiceButton, true)
            .Add(c => c.VoiceButtonCssClass, "store-search-mic"));

        cut.Find("button.store-search-mic").Should().NotBeNull();
    }

    [Fact]
    public void IsListening_AddsListeningClassToMicButton()
    {
        var cut = RenderComponent<VanAnPublicSearchBar>(p => p
            .Add(c => c.ShowVoiceButton, true)
            .Add(c => c.IsListening, true));

        var mic = cut.Find("button[aria-label='Tìm kiếm bằng giọng nói']");
        mic.ClassList.Should().Contain("listening");
        mic.ClassList.Should().Contain("text-danger", "existing listening style kept");
    }

    [Fact]
    public void InputMaxLength_RendersMaxlengthAttribute()
    {
        var cut = RenderComponent<VanAnPublicSearchBar>(p => p
            .Add(c => c.InputMaxLength, 500));

        cut.Find("input").GetAttribute("maxlength").Should().Be("500");
    }

    [Fact]
    public void InputMaxLength_NotSet_NoAttribute()
    {
        var cut = RenderComponent<VanAnPublicSearchBar>();

        cut.Find("input").GetAttribute("maxlength").Should().BeNull();
    }

    [Fact]
    public void Loading_DisablesInput_AndShowsSpinnerInSearchButton()
    {
        var cut = RenderComponent<VanAnPublicSearchBar>(p => p
            .Add(c => c.Loading, true));

        cut.Find("input").GetAttribute("disabled").Should().NotBeNull();
        cut.Find(".spinner-border").Should().NotBeNull("VanAnButton spinner keeps busy UX");
    }

    [Fact]
    public void EnterKey_InvokesOnSearch()
    {
        var searched = 0;
        var cut = RenderComponent<VanAnPublicSearchBar>(p => p
            .Add(c => c.OnSearch, () => searched++));

        cut.Find("input").KeyUp("Enter");

        searched.Should().Be(1);
    }

    [Fact]
    public void ClearButton_SetsEmptyValue_AndFiresOnCleared()
    {
        string? value = "phở";
        var cleared = 0;
        var cut = RenderComponent<VanAnPublicSearchBar>(p => p
            .Add(c => c.Value, value)
            .Add(c => c.ValueChanged, v => value = v)
            .Add(c => c.OnCleared, () => cleared++));

        cut.Find("button[aria-label='Xóa từ khóa']").Click();

        value.Should().Be(string.Empty);
        cleared.Should().Be(1);
    }

    [Fact]
    public void Pill_True_AddsPillClassToRootInputGroup()
    {
        var cut = RenderComponent<VanAnPublicSearchBar>(p => p
            .Add(c => c.Pill, true));

        cut.Find(".input-group").ClassList.Should().Contain("vanan-search-pill");
    }

    [Fact]
    public void Pill_Default_NoPillClass()
    {
        var cut = RenderComponent<VanAnPublicSearchBar>();

        cut.Find(".input-group").ClassList.Should().NotContain("vanan-search-pill");
    }

    [Fact]
    public void EnterKeyHint_DefaultsToSearch()
    {
        var cut = RenderComponent<VanAnPublicSearchBar>();

        cut.Find("input").GetAttribute("enterkeyhint").Should().Be("search");
    }
}
