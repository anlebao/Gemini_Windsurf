namespace VanAn.KhachLink.Services.ProfileContext;

/// <summary>
/// Rule-based, explainable "next best action" builder for the customer profile page.
/// Pure function over <see cref="ProfileContextSnapshot"/> — deterministic, unit-testable,
/// no HTTP/JS/EF. Returns at most <see cref="MaxCards"/> cards sorted by priority;
/// falls back to a neutral discovery card when no rule fires.
/// </summary>
public static class ProfileContextCardBuilder
{
    public const int MaxCards = 3;

    /// <summary>Days before birthday that trigger the "birthday soon" card.</summary>
    public const int BirthdayWindowDays = 14;

    // Loyalty tier point thresholds — mirrors _tiers in LoyaltyCard.razor.
    // Keep in sync if tier config moves server-side (v2 aggregation endpoint).
    private static readonly (string Name, int Threshold)[] TierLadder =
    {
        ("Silver",   1_000),
        ("Gold",     5_000),
        ("Platinum", 20_000),
    };

    private const string SettingsAnchor = "#profile-settings";

    /// <summary>
    /// Builds the prioritized context card list. Never returns null; always ≥1 card
    /// (fallback discovery card when no rule matches).
    /// </summary>
    public static IReadOnlyList<ProfileContextCard> Build(ProfileContextSnapshot s)
    {
        var cards = new List<ProfileContextCard>();

        // 1. Active order — highest priority: customer is mid-transaction.
        if (s.ActiveOrderCount > 0)
        {
            cards.Add(new ProfileContextCard
            {
                Id = "active-order",
                Priority = 10,
                Icon = "bi bi-bag-check-fill",
                Title = "Đơn đang xử lý",
                Reason = s.ActiveOrderCount == 1
                    ? "Bạn có 1 đơn đang chờ hoặc đang giao."
                    : $"Bạn có {s.ActiveOrderCount} đơn đang chờ hoặc đang giao.",
                Href = "/my-orders",
                CtaLabel = "Theo dõi đơn"
            });
        }

        // 1b. Voucher expiring soon — money about to be lost; just below active-order.
        if (s.ExpiringVoucherCount > 0)
        {
            cards.Add(new ProfileContextCard
            {
                Id = "voucher-expiring",
                Priority = 15,
                Icon = "bi bi-ticket-perforated-fill",
                Title = "Voucher sắp hết hạn",
                Reason = s.VoucherDaysLeft is int d && d <= 1
                    ? $"Voucher của bạn hết hạn {(d <= 0 ? "hôm nay" : "ngày mai")} — dùng ngay."
                    : $"Bạn có {s.ExpiringVoucherCount} voucher sắp hết hạn trong 7 ngày tới.",
                Href = "/rewards",
                CtaLabel = "Xem voucher"
            });
        }

        // 2. Identity upgrade — Social/Guest cannot redeem points; functional blocker.
        if (s.IdentityLevel is "Social" or "Guest" or null)
        {
            cards.Add(new ProfileContextCard
            {
                Id = "identity-upgrade",
                Priority = 20,
                Icon = "bi bi-shield-lock",
                Title = "Nâng cấp xác thực",
                Reason = "Xác thực đầy đủ để đổi điểm thưởng và bảo vệ tài khoản.",
                Href = "/my-loyalty",
                CtaLabel = "Nâng cấp"
            });
        }

        // 3. Push notifications off — only when status is known-off.
        if (s.PushEnabled == false)
        {
            cards.Add(new ProfileContextCard
            {
                Id = "push-off",
                Priority = 30,
                Icon = "bi bi-bell-slash",
                Title = "Bật thông báo",
                Reason = "Không bỏ lỡ cập nhật đơn hàng và ưu đãi mới.",
                Href = SettingsAnchor,
                CtaLabel = "Bật ngay"
            });
        }

        // 4. Incomplete missions.
        if (s.IncompleteMissionCount > 0)
        {
            cards.Add(new ProfileContextCard
            {
                Id = "mission-pending",
                Priority = 40,
                Icon = "bi bi-trophy",
                Title = "Nhiệm vụ đang chờ",
                Reason = s.MaxIncompleteMissionPoints > 0
                    ? $"Còn {s.IncompleteMissionCount} nhiệm vụ — nhận tới {s.MaxIncompleteMissionPoints:N0} điểm."
                    : $"Bạn có {s.IncompleteMissionCount} nhiệm vụ chưa hoàn thành.",
                Href = "/missions",
                CtaLabel = "Xem nhiệm vụ"
            });
        }

        // 5. Tier progress — only once past halfway to the next tier, so low-engagement
        //    accounts aren't nagged with "còn 9.990 điểm" noise.
        var nextTier = NextTier(s.PointBalance);
        if (nextTier is not null)
        {
            var (tierName, threshold) = nextTier.Value;
            var remaining = threshold - s.PointBalance;
            if (remaining > 0 && s.PointBalance >= threshold / 2)
            {
                cards.Add(new ProfileContextCard
                {
                    Id = "tier-progress",
                    Priority = 50,
                    Icon = "bi bi-star-half",
                    Title = $"Lên hạng {tierName}",
                    Reason = $"Còn {remaining:N0} điểm để đạt hạng {tierName}.",
                    Href = "/my-loyalty",
                    CtaLabel = "Xem điểm"
                });
            }
        }

        // 6. Birthday missing.
        if (!s.Birthday.HasValue)
        {
            cards.Add(new ProfileContextCard
            {
                Id = "birthday-missing",
                Priority = 60,
                Icon = "bi bi-cake2",
                Title = "Nhập ngày sinh",
                Reason = "Nhận điểm thưởng sinh nhật hàng năm.",
                Href = SettingsAnchor,
                CtaLabel = "Nhập ngay"
            });
        }
        else if (DaysUntilBirthday(s.Birthday.Value, s.UtcNow) is var days && days >= 0 && days <= BirthdayWindowDays)
        {
            cards.Add(new ProfileContextCard
            {
                Id = "birthday-soon",
                Priority = 65,
                Icon = "bi bi-gift",
                Title = "Sinh nhật sắp tới",
                Reason = days == 0
                    ? "Hôm nay là sinh nhật bạn — kiểm tra quà tặng."
                    : $"Sinh nhật còn {days} ngày nữa — chuẩn bị nhận quà.",
                Href = "/campaigns",
                CtaLabel = "Xem ưu đãi"
            });
        }

        // 6b. Win-back — past customer inactive ≥30 days, no active order.
        if (s.TotalOrderCount > 0 && s.ActiveOrderCount == 0
            && s.DaysSinceLastOrder is int inactiveDays && inactiveDays >= 30)
        {
            cards.Add(new ProfileContextCard
            {
                Id = "win-back",
                Priority = 70,
                Icon = "bi bi-emoji-smile",
                Title = "Lâu rồi không ghé",
                Reason = $"Đã {inactiveDays} ngày kể từ đơn gần nhất — xem ưu đãi mới.",
                Href = "/campaigns",
                CtaLabel = "Xem ưu đãi"
            });
        }

        // 6c. Dismissed cards (v2) — filter BEFORE fallback/cap so dismissed cards
        //     don't consume slots. Applied to rule cards AND the fallback below.
        var dismissed = s.DismissedCardIds;
        if (dismissed is { Count: > 0 })
            cards.RemoveAll(c => dismissed.Contains(c.Id));

        if (cards.Count == 0)
        {
            var fallback = new ProfileContextCard
            {
                Id = "discover-stores",
                Priority = 100,
                Icon = "bi bi-geo-alt",
                Title = "Khám phá cửa hàng",
                Reason = "Tìm cửa hàng và sản phẩm gần bạn.",
                Href = "/stores",
                CtaLabel = "Tìm cửa hàng"
            };
            if (dismissed is null || !dismissed.Contains(fallback.Id))
                cards.Add(fallback);
        }

        return cards
            .OrderBy(c => c.Priority)
            .ThenBy(c => c.Id, StringComparer.Ordinal)
            .Take(MaxCards)
            .ToList();
    }

    /// <summary>Next tier above current point balance, or null when Platinum/maxed.</summary>
    private static (string Name, int Threshold)? NextTier(int pointBalance)
    {
        foreach (var (name, threshold) in TierLadder)
        {
            if (pointBalance < threshold)
                return (name, threshold);
        }
        return null;
    }

    /// <summary>Days from <paramref name="now"/> to next occurrence of birthday (0 = today).</summary>
    internal static int DaysUntilBirthday(DateTime birthday, DateTime now)
    {
        var today = now.Date;
        var year = today.Year;
        // Feb 29 birthdays: clamp to Feb 28 on non-leap years.
        var day = birthday.Day;
        var month = birthday.Month;
        var maxDay = DateTime.DaysInMonth(year, month);
        if (day > maxDay) day = maxDay;
        var next = new DateTime(year, month, day);
        if (next < today)
        {
            year++;
            maxDay = DateTime.DaysInMonth(year, month);
            if (day > maxDay) day = maxDay;
            next = new DateTime(year, month, day);
        }
        return (next - today).Days;
    }
}
