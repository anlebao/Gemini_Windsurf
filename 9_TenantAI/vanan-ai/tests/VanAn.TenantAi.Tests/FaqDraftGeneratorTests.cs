using FluentAssertions;
using VanAn.TenantAi.Ai;
using Xunit;

namespace VanAn.TenantAi.Tests;

/// <summary>W3f — FAQ draft generator (prompt + JSON parse) cho chat learning suggest.</summary>
public sealed class FaqDraftGeneratorTests
{
    [Fact]
    public void BuildPrompt_ContainsQuestion_AndGuardrails()
    {
        var prompt = FaqDraftGenerator.BuildPrompt("Quán mở cửa mấy giờ?");

        prompt.Should().Contain("Quán mở cửa mấy giờ?");
        prompt.Should().Contain("chủ shop xác nhận");
        prompt.Should().Contain("\"title\"");
        prompt.Should().Contain("\"content\"");
    }

    [Fact]
    public void Parse_ValidJson_ReturnsDraft()
    {
        var draft = FaqDraftGenerator.Parse(
            """{"title":"Quán mở cửa mấy giờ?","content":"Quán mở cửa từ 7h sáng."}""");

        draft.Should().NotBeNull();
        draft!.Title.Should().Be("Quán mở cửa mấy giờ?");
        draft.Content.Should().Be("Quán mở cửa từ 7h sáng.");
    }

    [Fact]
    public void Parse_FencedJson_ReturnsDraft()
    {
        var draft = FaqDraftGenerator.Parse("```json\n{\"title\":\"T\",\"content\":\"C\"}\n```");

        draft.Should().NotBeNull();
        draft!.Title.Should().Be("T");
    }

    [Fact]
    public void Parse_NonJson_ReturnsNull()
        => FaqDraftGenerator.Parse("xin lỗi tôi không hiểu câu hỏi này").Should().BeNull();

    [Fact]
    public void Parse_MissingContent_ReturnsNull()
        => FaqDraftGenerator.Parse("""{"title":"T"}""").Should().BeNull();

    [Fact]
    public void Parse_EmptyFields_ReturnsNull()
        => FaqDraftGenerator.Parse("""{"title":"  ","content":"C"}""").Should().BeNull();

    [Fact]
    public void Parse_ContentTooLong_ReturnsNull()
    {
        var content = new string('x', 501);
        FaqDraftGenerator.Parse($$"""{"title":"T","content":"{{content}}"}""").Should().BeNull();
    }

    [Fact]
    public void Parse_TitleTooLong_ReturnsNull()
    {
        var title = new string('x', 121);
        FaqDraftGenerator.Parse($$"""{"title":"{{title}}","content":"C"}""").Should().BeNull();
    }
}