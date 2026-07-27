namespace VanAn.Shared.Domain
{
    /// <summary>
    /// Phase 5 + WS-2: Segmentation criteria for customer filtering (bulk push campaigns + CRM list).
    /// All fields optional — null means no filter on that field.
    /// WS-2 additions: MinPointBalance, MaxPointBalance, BirthdayMonth, LastOrderWithinDays.
    /// AF-P0-T2: Moved from 3_CoreHub/Domain/Repositories/ICustomerRepository.cs to 1_Shared/Domain/
    /// to break circular dependency (1_Shared/Services/IPromoCampaignService depends on this criteria).
    /// </summary>
    public record CustomerSegmentCriteria(
        string? CustomerTier = null,
        IdentityLevel? MinIdentityLevel = null,
        decimal? MinTotalSpent = null,
        decimal? MaxTotalSpent = null,
        DateTime? LastOrderAfter = null,
        DateTime? LastOrderBefore = null,
        bool HasPushSubscription = false,
        // WS-2: Loyalty points range filter (joins LoyaltyRewards table)
        int? MinPointBalance = null,
        int? MaxPointBalance = null,
        // WS-2: Birthday month filter (1-12, null = no filter)
        int? BirthdayMonth = null,
        // WS-2: Convenience filter — last order within N days (converted to LastOrderAfter = Now.AddDays(-N))
        int? LastOrderWithinDays = null);
}
