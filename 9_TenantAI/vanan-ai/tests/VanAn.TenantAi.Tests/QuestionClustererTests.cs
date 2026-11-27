using FluentAssertions;
using VanAn.TenantAi.Ai;
using Xunit;

namespace VanAn.TenantAi.Tests;

/// <summary>
/// W3f — pure clustering cho chat learning suggest job: gom câu hỏi NoData
/// trùng/đồng nghĩa theo (tenant, cụm) trước khi LLM soạn FAQ draft.
/// </summary>
public sealed class QuestionClustererTests
{
    [Fact]
    public void Cluster_NullOrEmpty_ReturnsEmpty()
    {
        QuestionClusterer.Cluster(null).Should().BeEmpty();
        QuestionClusterer.Cluster([]).Should().BeEmpty();
    }

    [Fact]
    public void Cluster_IdenticalQuestions_GroupIntoOneCluster()
    {
        var clusters = QuestionClusterer.Cluster(
            ["Quán mở cửa mấy giờ?", "quán mở cửa mấy giờ", "Quán mở cửa mấy giờ ạ"]);

        clusters.Should().ContainSingle();
        clusters[0].Count.Should().Be(3);
    }

    [Fact]
    public void Cluster_TypoVariants_MergeAboveDefaultThreshold()
    {
        var clusters = QuestionClusterer.Cluster(
            ["quan mo cua may gio", "quán mở cửa mấy giờ", "quan mo cua may gio a"]);

        clusters.Should().ContainSingle();
        clusters[0].Count.Should().Be(3);
    }

    [Fact]
    public void Cluster_UnrelatedQuestions_StaySeparate_EvenWhenFrequent()
    {
        var clusters = QuestionClusterer.Cluster(
            ["quán mở cửa mấy giờ", "quán mở cửa mấy giờ", "quán mở cửa mấy giờ",
             "giá phở chay bao nhiêu", "giá phở chay bao nhiêu", "giá phở chay bao nhiêu"]);

        clusters.Should().HaveCount(2);
        clusters.Should().OnlyContain(c => c.Count == 3);
    }

    [Fact]
    public void Cluster_BelowMinSize_Excluded()
    {
        var clusters = QuestionClusterer.Cluster(
            ["quán mở cửa mấy giờ", "quán mở cửa mấy giờ"], minSize: 3);

        clusters.Should().BeEmpty();
    }

    [Fact]
    public void Cluster_GarbageOrTooShort_Filtered()
    {
        var clusters = QuestionClusterer.Cluster(
            ["a", "ab", "abc", "!!!", "   ",
             "quán mở cửa mấy giờ", "quán mở cửa mấy giờ", "quán mở cửa mấy giờ"]);

        clusters.Should().ContainSingle();
        clusters[0].Count.Should().Be(3);
    }

    [Fact]
    public void Cluster_Representative_IsMostFrequentRawQuestion()
    {
        var clusters = QuestionClusterer.Cluster(
            ["quán mở cửa mấy giờ", "quán mở cửa mấy giờ",
             "quán mở cửa mấy giờ ạ", "quán mở cửa mấy giờ ạ", "quán mở cửa mấy giờ ạ"]);

        clusters.Should().ContainSingle();
        clusters[0].Representative.Should().Be("quán mở cửa mấy giờ ạ");
    }

    [Fact]
    public void Cluster_LowThreshold_MergesNearVariants()
    {
        var clusters = QuestionClusterer.Cluster(
            ["quán mở cửa mấy giờ", "quán mở cửa lúc mấy giờ", "quán mở cửa lúc mấy giờ"],
            similarityThreshold: 0.6, minSize: 3);

        clusters.Should().ContainSingle();
        clusters[0].Count.Should().Be(3);
    }
}