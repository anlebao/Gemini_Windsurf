using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using VanAn.TenantAi.Api.Auth;
using VanAn.TenantAi.Domain;
using VanAn.TenantAi.Infra;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<TenantAiDbContext>(opt =>
    opt.UseNpgsql(
        builder.Configuration.GetConnectionString("TenantAi"),
        npg => npg.UseVector())
       .UseSnakeCaseNamingConvention());

// task_ai_p2 — ingest pipeline services.
builder.Services.AddSingleton<VanAn.TenantAi.Ai.ChunkingService>();
builder.Services.AddHttpClient<VanAn.TenantAi.Ai.IEmbeddingProvider, VanAn.TenantAi.Ai.GeminiEmbeddingService>(c =>
{
    var endpoint = builder.Configuration["TenantAiLlm:Endpoint"] ?? "https://generativelanguage.googleapis.com/v1beta";
    c.BaseAddress = new Uri(endpoint.TrimEnd('/') + "/");
    c.Timeout = TimeSpan.FromSeconds(builder.Configuration.GetValue("TenantAiLlm:TimeoutSeconds", 30));
});
builder.Services.AddScoped<VanAn.TenantAi.Api.Services.KnowledgeIngestService>();

// task_ai_p3 — chat pipeline services (SRS §5).
builder.Services.AddHttpClient<VanAn.TenantAi.Ai.ILlmChatClient, VanAn.TenantAi.Ai.GeminiChatClient>(c =>
{
    var endpoint = builder.Configuration["TenantAiLlm:Endpoint"] ?? "https://generativelanguage.googleapis.com/v1beta";
    c.BaseAddress = new Uri(endpoint.TrimEnd('/') + "/");
    c.Timeout = TimeSpan.FromSeconds(builder.Configuration.GetValue("TenantAiLlm:TimeoutSeconds", 30));
});
builder.Services.AddScoped<VanAn.TenantAi.Ai.IntentRouter>();
builder.Services.AddSingleton<VanAn.TenantAi.Ai.PromptBuilder>();
builder.Services.AddSingleton<VanAn.TenantAi.Ai.AnswerValidator>();
builder.Services.AddScoped<VanAn.TenantAi.Api.Services.KnowledgeRetriever>();
builder.Services.AddScoped<VanAn.TenantAi.Api.Services.ChatOrchestrator>();

// task_ai_p5 — metering quota gate [G7] + post-response usage ingest (PO-2).
// Named client → singleton MeteringClient so the 5-fail/60s circuit persists.
builder.Services.AddHttpClient("metering", c =>
{
    var baseUrl = builder.Configuration["Metering:BaseUrl"];
    if (!string.IsNullOrWhiteSpace(baseUrl))
        c.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
    c.Timeout = TimeSpan.FromSeconds(builder.Configuration.GetValue("Metering:TimeoutSeconds", 3));
    var callerId = builder.Configuration["Metering:CallerId"];
    var callerKey = builder.Configuration["Metering:CallerKey"];
    if (!string.IsNullOrWhiteSpace(callerId))
        c.DefaultRequestHeaders.TryAddWithoutValidation("X-Caller-Id", callerId);
    if (!string.IsNullOrWhiteSpace(callerKey))
        c.DefaultRequestHeaders.TryAddWithoutValidation("X-Metering-Key", callerKey);
});
builder.Services.AddSingleton<VanAn.TenantAi.Infra.IMeteringService>(sp =>
    new VanAn.TenantAi.Infra.MeteringClient(
        sp.GetRequiredService<IHttpClientFactory>().CreateClient("metering"),
        sp.GetRequiredService<IConfiguration>(),
        sp.GetRequiredService<ILogger<VanAn.TenantAi.Infra.MeteringClient>>()));
builder.Services.AddSingleton<VanAn.TenantAi.Api.Services.QuotaGate>();
builder.Services.AddSingleton<VanAn.TenantAi.Api.Services.MeteringIngestQueue>();
builder.Services.AddSingleton<VanAn.TenantAi.Api.Services.IMeteringIngestQueue>(
    sp => sp.GetRequiredService<VanAn.TenantAi.Api.Services.MeteringIngestQueue>());
builder.Services.AddHostedService(
    sp => sp.GetRequiredService<VanAn.TenantAi.Api.Services.MeteringIngestQueue>());

// task_ai_p6b — admin API (SRS §6.3) + [G10] retention cleanup.
builder.Services.AddScoped<VanAn.TenantAi.Api.Services.TenantAdminService>();
builder.Services.AddHostedService<VanAn.TenantAi.Api.Services.RetentionCleanupService>();

var app = builder.Build();

// ---- Bootstrap: pgvector extension + schema + seed callers (idempotent) ----
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<TenantAiDbContext>();
    // P1: real EF migrations (not EnsureCreated — task_ai_p1_schema). Migration itself
    // creates the vector extension via HasPostgresExtension. Guard: InMemory (tests) is
    // not relational → EnsureCreated path stays for WAF factories.
    if (db.Database.IsRelational())
        await db.Database.MigrateAsync();
    else
        await db.Database.EnsureCreatedAsync();

    // gateway caller: public-chat scope, mọi tenant (null = "*").
    SeedCaller(db, app.Configuration["TenantAi:BootstrapCaller"],
        scopesJson: "[\"public-chat\"]", tenantScopeJson: null);
    // shoperp-* caller: ingest+admin, tenant allowlist từ TenantAi:BootstrapIngestScope ("guid1,guid2").
    var ingestScope = ParseGuidScope(app.Configuration["TenantAi:BootstrapIngestScope"]);
    SeedCaller(db, app.Configuration["TenantAi:BootstrapIngest"],
        scopesJson: "[\"ingest\",\"admin\"]", tenantScopeJson: ingestScope);

    // task_ai_p3 — policy-as-data system defaults (FR-3 matrix + [G15] matrix).
    // Runtime seed (not migration) so defaults can evolve without schema churn.
    foreach (var route in VanAn.TenantAi.Ai.DefaultPolicySeed.IntentRoutes())
        if (!db.IntentRoutes.Any(r => r.TenantId == null && r.Intent == route.Intent))
            db.IntentRoutes.Add(route);
    var sysPerms = db.KnowledgeUsagePermissions.Where(p => p.TenantId == null)
        .Select(p => new { p.Intent, p.Classification, p.Channel }).ToList();
    foreach (var perm in VanAn.TenantAi.Ai.DefaultPolicySeed.UsagePermissions())
        if (!sysPerms.Any(p => p.Intent == perm.Intent && p.Classification == perm.Classification && p.Channel == perm.Channel))
            db.KnowledgeUsagePermissions.Add(perm);
    db.SaveChanges();
}

static string? ParseGuidScope(string? csv)
{
    if (string.IsNullOrWhiteSpace(csv)) return null;
    var guids = csv.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    return guids.Length == 0 ? null : JsonSerializer.Serialize(guids);
}

static void SeedCaller(TenantAiDbContext db, string? pair, string scopesJson, string? tenantScopeJson)
{
    if (string.IsNullOrWhiteSpace(pair)) return;
    var parts = pair.Split(':', 2, StringSplitOptions.TrimEntries);
    if (parts.Length != 2 || parts[0].Length == 0 || parts[1].Length == 0) return;
    var callerId = parts[0];
    if (db.CallerKeys.Any(k => k.CallerId == callerId)) return;
    db.CallerKeys.Add(CallerKey.Create(callerId, Crypto.Sha256Hex(parts[1]), false, scopesJson, tenantScopeJson));
    db.SaveChanges();
}

// ---- Endpoints ----

app.MapGet("/v1/health", async (TenantAiDbContext db) =>
{
    var dbUp = await db.Database.CanConnectAsync();
    return Results.Ok(new { status = "ok", service = "vanan-ai", db = dbUp ? "up" : "down", utc = DateTime.UtcNow });
});

// 3-layer auth: key → scope → signed tenant ctx (SRS [D7]).
app.UseTenantAiAuth();

// Auth probes — verify pipeline trước khi business endpoints land.
app.MapGet("/v1/auth/check", (HttpContext ctx) => Results.Ok(new
{
    callerId = ctx.Items["CallerId"],
    isAdmin = ctx.Items["CallerIsAdmin"],
    scopes = ctx.Items["CallerScopesJson"]
}));

app.MapGet("/v1/auth/tenant-check", (HttpContext ctx) => Results.Ok(new
{
    callerId = ctx.Items["CallerId"],
    tenantId = ctx.Items["TenantId"],
    requestId = ctx.Items["RequestId"]
}))
.RequireScope(ScopeNames.PublicChat)
.RequireTenantContext();

// ---- task_ai_p3: grounded chat pipeline (SRS §5) ----
static IResult BadChatRequest() => Results.Json(
    new { error = new { code = "invalid_request", message = "sessionId, messageId and text are required.", retryable = false } },
    statusCode: StatusCodes.Status400BadRequest);

static bool ValidChatBody(VanAn.TenantAi.Contracts.ChatRequest b)
    => !string.IsNullOrWhiteSpace(b.SessionId) && b.SessionId.Length <= 64
        && !string.IsNullOrWhiteSpace(b.MessageId) && b.MessageId.Length <= 56
        && !string.IsNullOrWhiteSpace(b.Text) && b.Text.Length <= 2000;

static VanAn.TenantAi.Contracts.ChatResponse ToResponse(VanAn.TenantAi.Api.Services.ChatResult r)
    => new(r.Answer, r.Products, r.SourceIds, r.Confidence, r.Refused, r.RefuseReason, r.StaleData, r.Degraded);

app.MapPost("/v1/chat", async (
        HttpContext ctx, VanAn.TenantAi.Api.Services.ChatOrchestrator orch,
        VanAn.TenantAi.Contracts.ChatRequest body, CancellationToken ct) =>
    {
        if (!ValidChatBody(body)) return BadChatRequest();
        var result = await orch.ChatAsync(TenantOf(ctx), body.SessionId.Trim(), body.MessageId.Trim(),
            body.Text, channel: "public", useDraftConfig: false, ct);
        return Results.Ok(ToResponse(result));
    })
    .RequireScope(ScopeNames.PublicChat)
    .RequireTenantContext();

app.MapPost("/v1/admin/tenants/{tenantId:guid}/preview-chat", async (
        HttpContext ctx, VanAn.TenantAi.Api.Services.ChatOrchestrator orch, Guid tenantId,
        VanAn.TenantAi.Contracts.ChatRequest body, CancellationToken ct) =>
    {
        if (tenantId != TenantOf(ctx))
            return Results.Json(new { error = new { code = "forbidden", message = "tenant context mismatch.", retryable = false } },
                statusCode: StatusCodes.Status403Forbidden);
        if (!ValidChatBody(body)) return BadChatRequest();
        var result = await orch.ChatAsync(tenantId, body.SessionId.Trim(), body.MessageId.Trim(),
            body.Text, channel: "preview", useDraftConfig: true, ct);
        return Results.Ok(ToResponse(result));
    })
    .RequireScope(ScopeNames.Admin)
    .RequireTenantContext();

// task_ai_p4 — session history for storefront widget resume (SRS §6.1).
// ctx tenant must own the session — query is tenant-filtered, cross-tenant → empty.
app.MapGet("/v1/chat/sessions/{sid}/messages", async (
        HttpContext ctx, TenantAiDbContext db, IConfiguration config, string sid, CancellationToken ct) =>
    {
        var tenantId = TenantOf(ctx);
        var msgs = await db.ConversationMessages.AsNoTracking()
            .Where(m => m.TenantId == tenantId && m.SessionId == sid
                && m.ExpiresAtUtc > DateTime.UtcNow)
            .OrderBy(m => m.CreatedAtUtc)
            .Take(100)
            .ToListAsync(ct);

        // Resolve product cards for assistant turns — FR-5: only available,
        // non-stale products may be suggested to cart (same rule as live chat).
        var staleBefore = DateTime.UtcNow.AddHours(-config.GetValue("Knowledge:StaleHours", 24));
        var refs = msgs.Where(m => m.ProductRefsJson != null)
            .SelectMany(m => TryGuidList(m.ProductRefsJson)).Distinct().ToList();
        var products = refs.Count == 0
            ? new Dictionary<Guid, VanAn.TenantAi.Domain.ProductKnowledge>()
            : await db.ProductKnowledge.AsNoTracking()
                .Where(p => refs.Contains(p.ProductId))
                .ToDictionaryAsync(p => p.ProductId, ct);

        var result = msgs.Select(m => new VanAn.TenantAi.Contracts.SessionMessageDto(
            m.Role == VanAn.TenantAi.Domain.MessageRole.User ? "user" : "assistant",
            m.Content,
            TryGuidList(m.ProductRefsJson)
                .Where(r => products.TryGetValue(r, out var p) && p.Available && p.SyncedAtUtc >= staleBefore)
                .Select(r => new VanAn.TenantAi.Contracts.ChatProductDto(
                    r, products[r].Name, products[r].Price, null))
                .ToList(),
            m.CreatedAtUtc)).ToList();

        return Results.Ok(new VanAn.TenantAi.Contracts.SessionMessagesResponse(sid, result));
    })
    .RequireScope(ScopeNames.PublicChat)
    .RequireTenantContext();

static List<Guid> TryGuidList(string? json)
{
    if (string.IsNullOrWhiteSpace(json)) return [];
    try { return JsonSerializer.Deserialize<List<Guid>>(json) ?? []; }
    catch (JsonException) { return []; }
}

// task_ai_p5 — widget config: aiEnabled=false khi kill-switch off HOẶC quota deny
// (cached hoặc fresh — widget ẩn, không phải lỗi). Greeting/suggestions đọc từ
// persona_json nếu có ("greeting": string, "suggestions": string[]).
app.MapGet("/v1/config/widget", async (
        HttpContext ctx, TenantAiDbContext db, IConfiguration config,
        VanAn.TenantAi.Api.Services.QuotaGate quota, CancellationToken ct) =>
    {
        var tenantId = TenantOf(ctx);
        var globalOn = config.GetValue("TenantAi:Enabled", true);
        var outcome = globalOn
            ? await quota.EvaluateAsync(tenantId, ct)
            : VanAn.TenantAi.Api.Services.QuotaOutcome.Refuse;

        var aiConfig = await db.TenantAiConfigs.AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.Status == TenantAiConfigStatus.Published)
            .OrderByDescending(c => c.Version)
            .FirstOrDefaultAsync(ct);

        var (greeting, suggestions) = WidgetPersona(aiConfig?.PersonaJson);
        return Results.Ok(new VanAn.TenantAi.Contracts.WidgetConfigResponse(
            globalOn && outcome != VanAn.TenantAi.Api.Services.QuotaOutcome.Refuse,
            aiConfig?.AiName ?? "trợ lý quán",
            greeting ?? "Xin chào! Mình có thể giúp bạn chọn món hoặc trả lời về quán.",
            suggestions));
    })
    .RequireScope(ScopeNames.PublicChat)
    .RequireTenantContext();

static (string? Greeting, IReadOnlyList<string>? Suggestions) WidgetPersona(string? personaJson)
{
    if (string.IsNullOrWhiteSpace(personaJson)) return (null, null);
    try
    {
        using var doc = JsonDocument.Parse(personaJson);
        var root = doc.RootElement;
        string? greeting = root.TryGetProperty("greeting", out var g) && g.ValueKind == JsonValueKind.String
            ? g.GetString() : null;
        List<string>? suggestions = null;
        if (root.TryGetProperty("suggestions", out var s) && s.ValueKind == JsonValueKind.Array)
            suggestions = s.EnumerateArray()
                .Where(e => e.ValueKind == JsonValueKind.String)
                .Select(e => e.GetString()!)
                .Where(v => !string.IsNullOrWhiteSpace(v)).ToList();
        return (greeting, suggestions is { Count: > 0 } ? suggestions : null);
    }
    catch (JsonException) { return (null, null); }
}

// ---- task_ai_p2: knowledge ingest (caller shoperp-*; tenant từ signed ctx) ----
static Guid TenantOf(HttpContext ctx) => (Guid)ctx.Items["TenantId"]!;
static string CallerOf(HttpContext ctx) => (string)ctx.Items["CallerId"]!;
static IResult Error(int status, string code, string message) => Results.Json(
    new { error = new { code, message, retryable = false } }, statusCode: status);

app.MapPost("/v1/knowledge/products:batch", async (
        HttpContext ctx, VanAn.TenantAi.Api.Services.KnowledgeIngestService svc,
        VanAn.TenantAi.Contracts.ProductsBatchRequest body, CancellationToken ct) =>
    {
        var idem = ctx.Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(idem))
            return Error(StatusCodes.Status400BadRequest, "missing_idempotency_key", "Idempotency-Key header is required.");
        var result = await svc.UpsertProductsAsync(TenantOf(ctx), body, idem, ct);
        var r = result.Value!;
        return Results.Ok(new VanAn.TenantAi.Contracts.ProductsBatchResponse(r.Upserted, r.Removed, idem, r.Replayed));
    })
    .RequireScope(ScopeNames.Ingest)
    .RequireTenantContext();

app.MapDelete("/v1/knowledge/products/{id:guid}", async (
        HttpContext ctx, VanAn.TenantAi.Api.Services.KnowledgeIngestService svc, Guid id, CancellationToken ct) =>
        await svc.DeleteProductAsync(TenantOf(ctx), id, ct)
            ? Results.NoContent()
            : Error(StatusCodes.Status404NotFound, "not_found", "Product knowledge not found."))
    .RequireScope(ScopeNames.Ingest)
    .RequireTenantContext();

app.MapPost("/v1/knowledge/documents", async (
        HttpContext ctx, VanAn.TenantAi.Api.Services.KnowledgeIngestService svc,
        VanAn.TenantAi.Contracts.DocumentIngestRequest body, CancellationToken ct) =>
    {
        var created = await svc.AddDocumentAsync(TenantOf(ctx), body, ct);
        if (created.Value == null)
            return Error(StatusCodes.Status400BadRequest, created.ErrorCode ?? "invalid_document", "Document rejected at ingest.");
        return Results.Ok(new VanAn.TenantAi.Contracts.DocumentIngestResponse(
            created.Value.Id, "pending_review", created.Value.Sanitized));
    })
    .RequireScope(ScopeNames.Ingest)
    .RequireTenantContext();

app.MapDelete("/v1/knowledge/documents/{id:guid}", async (
        HttpContext ctx, VanAn.TenantAi.Api.Services.KnowledgeIngestService svc, Guid id, CancellationToken ct) =>
        await svc.DeleteDocumentAsync(TenantOf(ctx), id, ct)
            ? Results.NoContent()
            : Error(StatusCodes.Status404NotFound, "not_found", "Document not found."))
    .RequireScope(ScopeNames.Ingest)
    .RequireTenantContext();

app.MapGet("/v1/knowledge/status", async (
        HttpContext ctx, VanAn.TenantAi.Api.Services.KnowledgeIngestService svc, CancellationToken ct) =>
        Results.Ok(await svc.GetStatusAsync(TenantOf(ctx), ct)))
    .RequireScope(ScopeNames.Ingest)
    .RequireTenantContext();

// task_ai_p10 — paged docs list (filter ?status=pending_review|approved|rejected) for admin UI.
app.MapGet("/v1/admin/knowledge/documents", async (
        HttpContext ctx, VanAn.TenantAi.Api.Services.KnowledgeIngestService svc,
        string? status, int? page, int? size, CancellationToken ct) =>
        Results.Ok(await svc.GetDocumentsAsync(TenantOf(ctx), status, page ?? 1, size ?? 20, ct)))
    .RequireScope(ScopeNames.Admin)
    .RequireTenantContext();

// ---- Approval gate [G14] — P1 operator tooling chính ----
app.MapGet("/v1/admin/knowledge/pending", async (
        HttpContext ctx, VanAn.TenantAi.Api.Services.KnowledgeIngestService svc, CancellationToken ct) =>
        Results.Ok(await svc.GetPendingAsync(TenantOf(ctx), ct)))
    .RequireScope(ScopeNames.Admin)
    .RequireTenantContext();

app.MapPost("/v1/admin/knowledge/{id:guid}/approve", async (
        HttpContext ctx, VanAn.TenantAi.Api.Services.KnowledgeIngestService svc, Guid id,
        VanAn.TenantAi.Contracts.ApproveDocumentRequest? body, CancellationToken ct) =>
    {
        var result = await svc.ApproveDocumentAsync(TenantOf(ctx), id, CallerOf(ctx), body?.Classification, ct);
        return result.Value == null
            ? result.ErrorCode switch
            {
                "not_found" => Error(StatusCodes.Status404NotFound, "not_found", "Document not found."),
                "invalid_classification" or "restricted_not_approvable"
                    => Error(StatusCodes.Status400BadRequest, result.ErrorCode!, "Classification not allowed."),
                _ => Error(StatusCodes.Status409Conflict, result.ErrorCode ?? "conflict", "Document is not pending review.")
            }
            : Results.Ok(result.Value);
    })
    .RequireScope(ScopeNames.Admin)
    .RequireTenantContext();

app.MapPost("/v1/admin/knowledge/{id:guid}/reject", async (
        HttpContext ctx, VanAn.TenantAi.Api.Services.KnowledgeIngestService svc, Guid id,
        VanAn.TenantAi.Contracts.RejectDocumentRequest body, CancellationToken ct) =>
    {
        var result = await svc.RejectDocumentAsync(TenantOf(ctx), id, CallerOf(ctx), body.Reason, ct);
        return result.Value == null
            ? result.ErrorCode switch
            {
                "not_found" => Error(StatusCodes.Status404NotFound, "not_found", "Document not found."),
                "reason_required" => Error(StatusCodes.Status400BadRequest, "reason_required", "Reject reason is required."),
                _ => Error(StatusCodes.Status409Conflict, result.ErrorCode ?? "conflict", "Document is not pending review.")
            }
            : Results.Ok(result.Value);
    })
    .RequireScope(ScopeNames.Admin)
    .RequireTenantContext();

// ---- task_ai_p6b: admin API (SRS §6.3) — route tenant must equal ctx tenant ----
static bool WrongTenant(HttpContext ctx, Guid tenantId) => tenantId != TenantOf(ctx);

static IResult Forbidden() => Results.Json(
    new { error = new { code = "forbidden", message = "tenant context mismatch.", retryable = false } },
    statusCode: StatusCodes.Status403Forbidden);

static IResult MapError(string? code) => code switch
{
    "invalid_ai_name" or "invalid_json" or "external_p3" or "confirm_required"
        => Error(StatusCodes.Status400BadRequest, code!, code switch
        {
            "invalid_ai_name" => "aiName is required (max 128 chars).",
            "invalid_json" => "JSON field failed to parse.",
            "external_p3" => "externalAllow is a P3 feature — only empty [] accepted in v1.",
            _ => "X-Confirm-Purge header must equal the tenant id."
        }),
    "no_draft" => Error(StatusCodes.Status409Conflict, code!, "No draft config to publish."),
    "no_config" => Error(StatusCodes.Status409Conflict, code!, "Create a profile draft first."),
    "version_not_found" => Error(StatusCodes.Status404NotFound, code!, "Config version not found."),
    _ => Error(StatusCodes.Status409Conflict, code ?? "conflict", "Request conflicts with current state.")
};

app.MapPut("/v1/admin/tenants/{tenantId:guid}/profile", async (
        HttpContext ctx, VanAn.TenantAi.Api.Services.TenantAdminService svc, Guid tenantId,
        VanAn.TenantAi.Contracts.PutProfileRequest body, CancellationToken ct) =>
    {
        if (WrongTenant(ctx, tenantId)) return Forbidden();
        var result = await svc.PutProfileAsync(tenantId, body, ct);
        return result.Value == null ? MapError(result.ErrorCode) : Results.Ok(result.Value);
    })
    .RequireScope(ScopeNames.Admin)
    .RequireTenantContext();

app.MapGet("/v1/admin/tenants/{tenantId:guid}/profile", async (
        HttpContext ctx, VanAn.TenantAi.Api.Services.TenantAdminService svc, Guid tenantId,
        CancellationToken ct) =>
        WrongTenant(ctx, tenantId) ? Forbidden() : Results.Ok(await svc.GetProfileAsync(tenantId, ct)))
    .RequireScope(ScopeNames.Admin)
    .RequireTenantContext();

app.MapPost("/v1/admin/tenants/{tenantId:guid}/publish", async (
        HttpContext ctx, VanAn.TenantAi.Api.Services.TenantAdminService svc, Guid tenantId,
        CancellationToken ct) =>
    {
        if (WrongTenant(ctx, tenantId)) return Forbidden();
        var result = await svc.PublishAsync(tenantId, CallerOf(ctx), ct);
        return result.Value == null ? MapError(result.ErrorCode) : Results.Ok(result.Value);
    })
    .RequireScope(ScopeNames.Admin)
    .RequireTenantContext();

app.MapPost("/v1/admin/tenants/{tenantId:guid}/rollback", async (
        HttpContext ctx, VanAn.TenantAi.Api.Services.TenantAdminService svc, Guid tenantId,
        VanAn.TenantAi.Contracts.RollbackRequest body, CancellationToken ct) =>
    {
        if (WrongTenant(ctx, tenantId)) return Forbidden();
        var result = await svc.RollbackAsync(tenantId, body.Version, CallerOf(ctx), ct);
        return result.Value == null ? MapError(result.ErrorCode) : Results.Ok(result.Value);
    })
    .RequireScope(ScopeNames.Admin)
    .RequireTenantContext();

app.MapGet("/v1/admin/tenants/{tenantId:guid}/policy", async (
        HttpContext ctx, VanAn.TenantAi.Api.Services.TenantAdminService svc, Guid tenantId,
        CancellationToken ct) =>
        WrongTenant(ctx, tenantId) ? Forbidden() : Results.Ok(await svc.GetPolicyAsync(tenantId, ct)))
    .RequireScope(ScopeNames.Admin)
    .RequireTenantContext();

app.MapPut("/v1/admin/tenants/{tenantId:guid}/policy", async (
        HttpContext ctx, VanAn.TenantAi.Api.Services.TenantAdminService svc, Guid tenantId,
        VanAn.TenantAi.Contracts.PutPolicyRequest body, CancellationToken ct) =>
    {
        if (WrongTenant(ctx, tenantId)) return Forbidden();
        var result = await svc.PutPolicyAsync(tenantId, body, ct);
        return result.Value == null ? MapError(result.ErrorCode) : Results.Ok(result.Value);
    })
    .RequireScope(ScopeNames.Admin)
    .RequireTenantContext();

app.MapGet("/v1/admin/tenants/{tenantId:guid}/conversations", async (
        HttpContext ctx, VanAn.TenantAi.Api.Services.TenantAdminService svc, Guid tenantId,
        int? page, int? size, string? sessionId, CancellationToken ct) =>
    {
        if (WrongTenant(ctx, tenantId)) return Forbidden();
        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            var detail = await svc.GetConversationMessagesAsync(tenantId, sessionId, ct);
            return detail == null
                ? Error(StatusCodes.Status404NotFound, "not_found", "Session not found.")
                : Results.Ok(detail);
        }
        return Results.Ok(await svc.GetConversationsAsync(tenantId, page ?? 1, size ?? 20, ct));
    })
    .RequireScope(ScopeNames.Admin)
    .RequireTenantContext();

app.MapGet("/v1/admin/tenants/{tenantId:guid}/usage", async (
        HttpContext ctx, VanAn.TenantAi.Api.Services.TenantAdminService svc, Guid tenantId,
        int? days, CancellationToken ct) =>
        WrongTenant(ctx, tenantId) ? Forbidden() : Results.Ok(await svc.GetUsageAsync(tenantId, days ?? 30, ct)))
    .RequireScope(ScopeNames.Admin)
    .RequireTenantContext();

// [NFR-7] tenant purge — X-Confirm-Purge: <tenantId> required to prevent accidents.
app.MapDelete("/v1/admin/tenants/{tenantId:guid}", async (
        HttpContext ctx, VanAn.TenantAi.Api.Services.TenantAdminService svc, Guid tenantId,
        CancellationToken ct) =>
    {
        if (WrongTenant(ctx, tenantId)) return Forbidden();
        var confirm = ctx.Request.Headers["X-Confirm-Purge"].FirstOrDefault();
        if (confirm != tenantId.ToString())
            return MapError("confirm_required");
        return Results.Ok(await svc.PurgeTenantAsync(tenantId, ct));
    })
    .RequireScope(ScopeNames.Admin)
    .RequireTenantContext();

await app.RunAsync();

// Expose for WebApplicationFactory<Program> in tests.
public partial class Program;
