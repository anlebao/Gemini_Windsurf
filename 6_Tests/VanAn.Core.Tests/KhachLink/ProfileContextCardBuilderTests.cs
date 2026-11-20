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
        int maxMissionPoints = 0,
        int expiringVouchers = 0,
        int? voucherDaysLeft = null,
        int totalOrders = 0,
        int? daysSinceLastOrder = null,
        IReadOnlySet<string>? dismissed = null) => new()
    {
        IdentityLevel = identity,
        PointBalance = points,
        Birthday = birthday,
        PushEnabled = push,
        ActiveOrderCount = activeOrders,
        IncompleteMissionCount = incompleteMissions,
        MaxIncompleteMissionPoints = maxMissionPoints,
        ExpiringVoucherCount = expiringVouchers,
        VoucherDaysLeft = voucherDaysLeft,
        TotalOrderCount = totalOrders,
        DaysSinceLastOrder = daysSinceLastOrder,
        DismissedCardIds = dismissed,
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

    // === v2 rules ===

    [Fact]
    public void ExpiringVoucher_CardShown_BelowActiveOrder()
    {
        var cards = ProfileContextCardBuilder.Build(Snapshot(
            activeOrders: 1, expiringVouchers: 2, voucherDaysLeft: 3));

        cards[0].Id.Should().Be("active-order");
        cards.Should().Contain(c => c.Id == "voucher-expiring" && c.Href == "/rewards");
    }

    [Fact]
    public void ExpiringVoucher_Tomorrow_ReasonSaysNgayMai()
    {
        var cards = ProfileContextCardBuilder.Build(Snapshot(
            expiringVouchers: 1, voucherDaysLeft: 1));

        var card = cards.Should().ContainSingle(c => c.Id == "voucher-expiring").Subject;
        card.Reason.Should().Contain("ngày mai");
    }

    [Fact]
    public void NoExpiringVoucher_NoCard()
    {
        var cards = ProfileContextCardBuilder.Build(Snapshot(expiringVouchers: 0));

        cards.Should().NotContain(c => c.Id == "voucher-expiring");
    }

    [Fact]
    public void WinBack_Shown_WhenInactive30DaysAndHasOrders()
    {
        var cards = ProfileContextCardBuilder.Build(Snapshot(
            totalOrders: 3, daysSinceLastOrder: 45));

        var card = cards.Should().ContainSingle(c => c.Id == "win-back").Subject;
        card.Href.Should().Be("/campaigns");
        card.Reason.Should().Contain("45");
    }

    [Fact]
    public void WinBack_Hidden_WhenRecentOrderOrNoOrdersOrActiveOrder()
    {
        // Recent order
        ProfileContextCardBuilder.Build(Snapshot(totalOrders: 2, daysSinceLastOrder: 10))
            .Should().NotContain(c => c.Id == "win-back");
        // Never ordered
        ProfileContextCardBuilder.Build(Snapshot(totalOrders: 0, daysSinceLastOrder: null))
            .Should().NotContain(c => c.Id == "win-back");
        // Active order in flight
        ProfileContextCardBuilder.Build(Snapshot(totalOrders: 2, daysSinceLastOrder: 60, activeOrders: 1))
            .Should().NotContain(c => c.Id == "win-back");
    }

    [Fact]
    public void DismissedCard_IsFilteredBeforeCap()
    {
        // active-order dismissed → next cards move up, cap still 3
        var cards = ProfileContextCardBuilder.Build(Snapshot(
            identity: "Social", push: false, activeOrders: 1, incompleteMissions: 1,
            dismissed: new HashSet<string> { "active-order" }));

        cards.Should().NotContain(c => c.Id == "active-order");
        cards.Should().HaveCountLessThanOrEqualTo(ProfileContextCardBuilder.MaxCards);
        cards[0].Id.Should().Be("identity-upgrade");
    }

    [Fact]
    public void NoRuleCards_FallbackShown_UlessFallbackDismissed()
    {
        // Verified + far birthday + push on + 0 points → only fallback card.
        ProfileContextCardBuilder.Build(new ProfileContextSnapshot
        {
            IdentityLevel = "Verified",
            Birthday = new DateTime(1990, 12, 25),
            PushEnabled = true,
            PointBalance = 0,
            UtcNow = Now
        }).Should().ContainSingle(c => c.Id == "discover-stores");

        // Same snapshot but fallback dismissed → zone renders nothing.
        ProfileContextCardBuilder.Build(new ProfileContextSnapshot
        {
            IdentityLevel = "Verified",
            Birthday = new DateTime(1990, 12, 25),
            PushEnabled = true,
            PointBalance = 0,
            UtcNow = Now,
            DismissedCardIds = new HashSet<string> { "discover-stores" }
        }).Should().BeEmpty();
    }
}
