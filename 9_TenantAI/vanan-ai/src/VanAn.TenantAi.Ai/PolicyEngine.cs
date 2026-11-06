using VanAn.TenantAi.Domain;

namespace VanAn.TenantAi.Ai;

/// <summary>Outcome of the intent→sources policy check [FR-3][G6].</summary>
public sealed record PolicyDecision(
    IntentRoute? Route,
    bool Refuse,
    string? RefuseReason);

/// <summary>
/// task_ai_p3 / SRS FR-3 — policy-as-data intent routing. Reads `intent_routes`
/// (tenant override → system default). OutOfScope/PolicyViolation and intents
/// with no allowed sources refuse BEFORE any LLM call [G6]. Unknown intent +
/// no route = fail closed.
/// </summary>
public static class PolicyEngine
{
    /// <summary>Tenant route wins; system default (TenantId null) is the fallback.</summary>
    public static IntentRoute? ResolveRoute(
        string intent,
        IEnumerable<IntentRoute> tenantRoutes,
        IEnumerable<IntentRoute> systemRoutes)
        => tenantRoutes.FirstOrDefault(r => r.Intent == intent)
            ?? systemRoutes.FirstOrDefault(r => r.Intent == intent);

    public static PolicyDecision Decide(string intent, IntentRoute? route)
    {
        if (intent is ChatIntents.OutOfScope or ChatIntents.PolicyViolation)
            return new(route, true, route?.RefuseTemplate);
        if (route == null)
            return new(null, true, null); // unknown intent → fail closed
        if (route.AllowedSourceFlags == KnowledgeSourceFlags.None)
            return new(route, true, route.RefuseTemplate);
        return new(route, false, null);
    }

    /// <summary>Static-template intents (OrderHelp) answer from the route template — no LLM.</summary>
    public static bool IsStaticTemplateOnly(IntentRoute route)
        => route.AllowedSourceFlags == KnowledgeSourceFlags.StaticTemplate;
}

/// <summary>
/// System-default policy rows seeded at startup (runtime seed — policy is data,
/// not schema). Mirrors the FR-3 mandatory table. Tenant overrides land as
/// TenantId rows via the admin policy API (P3+).
/// </summary>
public static class DefaultPolicySeed
{
    public static IReadOnlyList<IntentRoute> IntentRoutes() =>
    [
        IntentRoute.Create(null, ChatIntents.MenuQuery, KnowledgeSourceFlags.ProductKnowledge, requiresFacts: true),
        IntentRoute.Create(null, ChatIntents.NutritionQuery, KnowledgeSourceFlags.ProductKnowledge, requiresFacts: true),
        IntentRoute.Create(null, ChatIntents.PriceQuery, KnowledgeSourceFlags.ProductKnowledge, requiresFacts: true),
        IntentRoute.Create(null, ChatIntents.AvailabilityQuery, KnowledgeSourceFlags.ProductKnowledge, requiresFacts: true),
        // FR-3: recipe docs + public fact fields (ingredients/allergens) — both sources.
        IntentRoute.Create(null, ChatIntents.RecipeQuery,
            KnowledgeSourceFlags.PublicDocuments | KnowledgeSourceFlags.ProductKnowledge, requiresFacts: true),
        IntentRoute.Create(null, ChatIntents.BrandStory, KnowledgeSourceFlags.PublicDocuments, requiresFacts: true),
        IntentRoute.Create(null, ChatIntents.Faq, KnowledgeSourceFlags.PublicDocuments, requiresFacts: true),
        // ExternalKnowledge off by default (FR-3) — enabled per-tenant later via route override.
        IntentRoute.Create(null, ChatIntents.ExternalKnowledge, KnowledgeSourceFlags.None, requiresFacts: false,
            ChatTemplates.RefuseOutOfScope),
        IntentRoute.Create(null, ChatIntents.OrderHelp, KnowledgeSourceFlags.StaticTemplate, requiresFacts: false,
            ChatTemplates.OrderHelp),
        IntentRoute.Create(null, ChatIntents.ChitChat, KnowledgeSourceFlags.Persona, requiresFacts: false),
        IntentRoute.Create(null, ChatIntents.OutOfScope, KnowledgeSourceFlags.None, requiresFacts: false,
            ChatTemplates.RefuseOutOfScope),
        IntentRoute.Create(null, ChatIntents.PolicyViolation, KnowledgeSourceFlags.None, requiresFacts: false,
            ChatTemplates.RefusePolite)
    ];

    /// <summary>
    /// [G15] default matrix — public/preview channels pass Public docs only;
    /// everything else denies. Intent "*" = applies to every intent.
    /// </summary>
    public static IReadOnlyList<KnowledgeUsagePermission> UsagePermissions()
    {
        var rows = new List<KnowledgeUsagePermission>();
        foreach (var channel in new[] { "public", "preview" })
        {
            rows.Add(KnowledgeUsagePermission.Create(null, "*", DocumentClassification.Public, channel, UsagePermission.Allow));
            rows.Add(KnowledgeUsagePermission.Create(null, "*", DocumentClassification.Internal, channel, UsagePermission.Deny));
            rows.Add(KnowledgeUsagePermission.Create(null, "*", DocumentClassification.Confidential, channel, UsagePermission.Deny));
            rows.Add(KnowledgeUsagePermission.Create(null, "*", DocumentClassification.Restricted, channel, UsagePermission.Deny));
        }
        return rows;
    }
}

/// <summary>vi-VN response templates — FR-10 polite, never a bare "not allowed".</summary>
public static class ChatTemplates
{
    public const string RefuseOutOfScope =
        "Xin lỗi, câu hỏi này nằm ngoài phạm vi mình hỗ trợ — bạn hỏi mình về menu, món ăn hay quán nhé!";

    public const string RefusePolite =
        "Xin lỗi, mình không thể hỗ trợ yêu cầu này. Mình vẫn sẵn sàng giúp bạn chọn món hoặc trả lời về quán nhé!";

    public const string NoData =
        "Xin lỗi, mình chưa có dữ liệu xác nhận cho câu hỏi này.";

    public const string DegradedUnavailable =
        "Xin lỗi, trợ lý đang tạm bận một chút — bạn thử lại sau giúp mình nhé.";

    public const string StaleSuffix =
        "(thông tin có thể chưa cập nhật — bạn kiểm tra lại với quán giúp mình nhé)";

    public const string OrderHelp =
        "Bạn có thể đặt món ngay trên trang quán: chọn món → thêm vào giỏ → xác nhận đơn. "
        + "Cần hỗ trợ thêm cứ hỏi mình nhé!";

    public const string ChitChatFallback =
        "Chào bạn! Mình là trợ lý của quán — hỏi mình về menu, món ăn hay câu chuyện quán nhé!";

    /// <summary>Structured-only fallback when the LLM path fails but SQL facts exist.</summary>
    public static string ProductFactsFallback(IReadOnlyList<string> lines)
        => "Mình tìm thấy thông tin cho bạn: " + string.Join("; ", lines) + ".";
}
