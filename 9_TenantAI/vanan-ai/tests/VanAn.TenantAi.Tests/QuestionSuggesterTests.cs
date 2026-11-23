using VanAn.TenantAi.Ai;
using Xunit;

namespace VanAn.TenantAi.Tests;

/// <summary>
/// W3e — QuestionSuggester: "did you mean" correction for unclear/typo'd
/// customer questions, matched against product names + approved doc titles.
/// </summary>
public sealed class QuestionSuggesterTests
{
    private static readonly string[] Menu = ["Phở bò", "Cơm tấm sườn", "Trà đào"];

    [Fact]
    public void Suggests_Product_On_Typo()
    {
        // "pho bot" ~ "pho bo" — one char off in the last token.
        var s = QuestionSuggester.Suggest("quán có món pho bot không", Menu);
        Assert.Equal("Phở bò", s);
    }

    [Fact]
    public void Suggests_Best_Of_Multiple_Candidates()
    {
        var s = QuestionSuggester.Suggest("com tam suom bao nhieu", Menu);
        Assert.Equal("Cơm tấm sườn", s);
    }

    [Fact]
    public void Returns_Null_When_Question_Already_Names_Candidate()
    {
        // Correct spelling already present → retrieval should have facts; no correction needed.
        Assert.Null(QuestionSuggester.Suggest("giá pho bo bao nhiêu", Menu));
        Assert.Null(QuestionSuggester.Suggest("Phở bò ngon không", Menu));
    }

    [Fact]
    public void Returns_Null_For_Unrelated_Question()
    {
        Assert.Null(QuestionSuggester.Suggest("thời tiết hôm nay thế nào", Menu));
        Assert.Null(QuestionSuggester.Suggest("bạn là ai", Menu));
    }

    [Fact]
    public void Returns_Null_For_Single_Vague_Token()
    {
        // "tra" alone is too ambiguous to correct confidently.
        Assert.Null(QuestionSuggester.Suggest("tra", Menu));
    }

    [Fact]
    public void Suggests_Doc_Title_On_Typo()
    {
        var docs = new[] { "Chính sách đổi trả", "Giới thiệu quán" };
        var s = QuestionSuggester.Suggest("chinh sach doi trar the nao vay", docs);
        Assert.Equal("Chính sách đổi trả", s);
    }

    [Fact]
    public void Handles_Empty_Inputs()
    {
        Assert.Null(QuestionSuggester.Suggest(null, Menu));
        Assert.Null(QuestionSuggester.Suggest("   ", Menu));
        Assert.Null(QuestionSuggester.Suggest("pho bot", null));
        Assert.Null(QuestionSuggester.Suggest("pho bot", Array.Empty<string>()));
        Assert.Null(QuestionSuggester.Suggest("pho bot", new[] { "", "  " }));
    }

    [Fact]
    public void Does_Not_Suggest_When_Score_Below_Threshold()
    {
        // Shares only one weak token with a candidate.
        Assert.Null(QuestionSuggester.Suggest("cho toi xin menu day du", Menu));
    }
}
