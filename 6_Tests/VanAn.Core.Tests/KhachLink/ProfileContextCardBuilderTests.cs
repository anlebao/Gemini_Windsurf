using FluentAssertions;
using VanAn.KhachLink.Services.ProfileContext;
using Xunit;

namespace VanAn.Core.Tests.KhachLink;

/// <summary>
/// ProfileContextCardBuilder — rule ordering, max-3 cap, fallback, per-rule triggers.
/// </summary>
public class ProfileContextCardBuilderTests
{
    private static readonly DateTime Now = new(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc);

    private static ProfileContextSnapshot Snapshot(
        string? identity = "Verified",
        int points = 0,
        DateTime? birthday = null,
        bool? push = true,
        int activeOrders = 0,
        int incompleteMissions = 0,
        int maxMissionPoints = 0) => new()
    {
        IdentityLevel = identity,
        PointBalance = points,
        Birthday = birthday,
        PushEnabled = push,
        ActiveOrderCount = activeOrders,
        IncompleteMissionCount = incompleteMissions,
        MaxIncompleteMissionPoints = maxMissionPoints,
        UtcNow = Now
    };

    [Fact]
    public void EmptySnapshot_ReturnsFallbackCard()
    {
        // Far-away birthday (Jan 1, now Jun 15) + 0 points → no rule fires.
        var cards = ProfileContextCardBuilder.Build(Snapshot(
            birthday: new DateTime(1990, 1, 1)));

        cards.Should().ContainSingle();
        cards[0].Id.Should().Be("discover-stores");
        cards[0].Href.Should().Be("/stores");
    }

    [Fact]
    public void ActiveOrder_IsTopPriority()
    {
        var cards = ProfileContextCardBuilder.Build(Snapshot(
            activeOrders: 2, push: false, incompleteMissions: 3, identity: "Social"));

        cards[0].Id.Should().Be("active-order");
        cards[0].Reason.Should().Contain("2 đơn");
    }

    [Fact]
    public void SocialIdentity_UpgradeCardShown()
    {
        var cards = ProfileContextCardBuilder.Build(Snapshot(identity: "Social"));

        cards.Should().Contain(c => c.Id == "identity-upgrade");
    }

    [Fact]
    public void VerifiedIdentity_NoUpgradeCard()
    {
        var cards = ProfileContextCardBuilder.Build(Snapshot(identity: "Verified"));

        cards.Should().NotContain(c => c.Id == "identity-upgrade");
    }

    [Fact]
    public void PushOff_CardShown_OnlyWhenKnownOff()
    {
        ProfileContextCardBuilder.Build(Snapshot(push: false))
            .Should().Contain(c => c.Id == "push-off");

        // null = unknown → no card
        ProfileContextCardBuilder.Build(Snapshot(push: null))
            .Should().NotContain(c => c.Id == "push-off");
    }

    [Fact]
    public void IncompleteMissions_CardWithPoints()
    {
        var cards = ProfileContextCardBuilder.Build(Snapshot(
            incompleteMissions: 2, maxMissionPoints: 150));

        var card = cards.Should().ContainSingle(c => c.Id == "mission-pending").Subject;
        card.Reason.Should().Contain("150");
        card.Href.Should().Be("/missions");
    }

    [Fact]
    public void TierProgress_ShowsNextTierAndRemaining()
    {
        // 600 points → past halfway to Silver (500), next tier Silver at 1000 → remaining 400
        var cards = ProfileContextCardBuilder.Build(Snapshot(points: 600));

        var card = cards.Should().ContainSingle(c => c.Id == "tier-progress").Subject;
        card.Title.Should().Contain("Silver");
        card.Reason.Should().Contain("400");
    }

    [Fact]
    public void TierProgress_HiddenBeforeHalfway()
    {
        // 100 points < 500 (half of Silver threshold) → no nag card
        var cards = ProfileContextCardBuilder.Build(Snapshot(points: 100));

        cards.Should().NotContain(c => c.Id == "tier-progress");
    }

    [Fact]
    public void PlatinumPoints_NoTierCard()
    {
        var cards = ProfileContextCardBuilder.Build(Snapshot(points: 25_000));

        cards.Should().NotContain(c => c.Id == "tier-progress");
    }

    [Fact]
    public void MissingBirthday_CardPointsToSettings()
    {
        var cards = ProfileContextCardBuilder.Build(Snapshot(birthday: null));

        var card = cards.Should().ContainSingle(c => c.Id == "birthday-missing").Subject;
        card.Href.Should().Be("#profile-settings");
    }

    [Fact]
    public void BirthdayWithin14Days_CardShown()
    {
        // Birthday June 25, now June 15 → 10 days away
        var cards = ProfileContextCardBuilder.Build(Snapshot(birthday: new DateTime(1990, 6, 25)));

        cards.Should().Contain(c => c.Id == "birthday-soon");
        cards.Should().NotContain(c => c.Id == "birthday-missing");
    }

    [Fact]
    public void BirthdayOutsideWindow_NoCard()
    {
        // Birthday Dec 25 → far away
        var cards = ProfileContextCardBuilder.Build(Snapshot(birthday: new DateTime(1990, 12, 25)));

        cards.Should().NotContain(c => c.Id == "birthday-soon");
        cards.Should().NotContain(c => c.Id == "birthday-missing");
    }

    [Fact]
    public void BirthdayToday_DaysUntilIsZero()
    {
        var cards = ProfileContextCardBuilder.Build(Snapshot(birthday: new DateTime(1990, 6, 15)));

        var card = cards.Should().ContainSingle(c => c.Id == "birthday-soon").Subject;
        card.Reason.Should().Contain("Hôm nay");
    }

    [Fact]
    public void MaxThreeCards_Enforced()
    {
        // Fire every rule at once
        var cards = ProfileContextCardBuilder.Build(Snapshot(
            identity: "Social", points: 400, birthday: null, push: false,
            activeOrders: 1, incompleteMissions: 2, maxMissionPoints: 100));

        cards.Should().HaveCountLessThanOrEqualTo(ProfileContextCardBuilder.MaxCards);
        // Highest priorities survive: active-order, identity-upgrade, push-off
        cards.Select(c => c.Id).Should().Equal("active-order", "identity-upgrade", "push-off");
    }

    [Fact]
    public void Results_SortedByPriority()
    {
        var cards = ProfileContextCardBuilder.Build(Snapshot(
            activeOrders: 1, push: false, incompleteMissions: 1));

        cards.Select(c => c.Priority).Should().BeInAscendingOrder();
    }

    [Fact]
    public void Feb29Birthday_DoesNotThrow()
    {
        var act = () => ProfileContextCardBuilder.Build(Snapshot(
            birthday: new DateTime(2000, 2, 29)));

        act.Should().NotThrow();
    }
}
