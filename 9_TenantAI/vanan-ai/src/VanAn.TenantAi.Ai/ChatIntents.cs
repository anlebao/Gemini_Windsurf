namespace VanAn.TenantAi.Ai;

/// <summary>SRS v1.1 FR-2 — canonical intent labels for the chat pipeline.</summary>
public static class ChatIntents
{
    public const string MenuQuery = "MenuQuery";
    public const string NutritionQuery = "NutritionQuery";
    public const string PriceQuery = "PriceQuery";
    public const string AvailabilityQuery = "AvailabilityQuery";
    public const string RecipeQuery = "RecipeQuery";
    public const string BrandStory = "BrandStory";
    public const string Faq = "Faq";
    public const string ExternalKnowledge = "ExternalKnowledge";
    public const string OrderHelp = "OrderHelp";
    public const string ChitChat = "ChitChat";
    public const string OutOfScope = "OutOfScope";
    public const string PolicyViolation = "PolicyViolation";

    public static readonly IReadOnlyList<string> All =
    [
        MenuQuery, NutritionQuery, PriceQuery, AvailabilityQuery, RecipeQuery,
        BrandStory, Faq, ExternalKnowledge, OrderHelp, ChitChat,
        OutOfScope, PolicyViolation
    ];
}
