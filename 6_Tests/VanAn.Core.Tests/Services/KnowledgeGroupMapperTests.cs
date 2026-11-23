using VanAn.ShopERP.Services;
using Xunit;

namespace VanAn.Core.Tests.Services;

/// <summary>W3c-multi — group→docType mapping + sourceRef format (plan D3/D4).</summary>
public class KnowledgeGroupMapperTests
{
    [Theory]
    [InlineData("menu", "menu-note")]
    [InlineData("about", "story")]
    [InlineData("policy", "policy")]
    [InlineData("promotion", "policy")]
    public void DocTypeFor_MapsGroups(string group, string expected)
        => Assert.Equal(expected, KnowledgeGroupMapper.DocTypeFor(group));

    [Fact]
    public void DocTypeFor_UnknownGroup_FallsBackToPolicy()
        => Assert.Equal("policy", KnowledgeGroupMapper.DocTypeFor("unknown"));

    [Fact]
    public void SourceRefFor_HasGroupAndUniqueGuid()
    {
        var a = KnowledgeGroupMapper.SourceRefFor("menu");
        var b = KnowledgeGroupMapper.SourceRefFor("menu");
        Assert.StartsWith("extract:menu:", a);
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void DocTitleFor_IncludesLabel()
        => Assert.Contains("Menu", KnowledgeGroupMapper.DocTitleFor("menu"));
}
