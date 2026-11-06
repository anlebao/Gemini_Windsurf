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

// Route stubs — auth shape đúng ngay từ P0b (T6/T12 test targets); business lands P2/P3.
static IResult NotImplemented() => Results.Json(
    new { error = new { code = "not_implemented", message = "Endpoint lands in a later task card.", retryable = false } },
    statusCode: StatusCodes.Status501NotImplemented);

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

app.MapGet("/v1/config/widget", NotImplemented)
    .RequireScope(ScopeNames.PublicChat)
    .RequireTenantContext();

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

// ---- Approval gate [G14] — P1 operator tooling chính ----
app.MapGet("/v1/admin/knowledge/pending", async (
        HttpContext ctx, VanAn.TenantAi.Api.Services.KnowledgeIngestService svc, CancellationToken ct) =>
        Results.Ok(await svc.GetPendingAsync(TenantOf(ctx), ct)))
    .RequireScope(ScopeNames.Admin)
    .RequireTenantContext();

app.MapPost("/v1/admin/knowledge/{id:guid}/approve", async (
        HttpContext ctx, VanAn.TenantAi.Api.Services.KnowledgeIngestService svc, Guid id, CancellationToken ct) =>
    {
        var result = await svc.ApproveDocumentAsync(TenantOf(ctx), id, CallerOf(ctx), ct);
        return result.Value == null
            ? result.ErrorCode == "not_found"
                ? Error(StatusCodes.Status404NotFound, "not_found", "Document not found.")
                : Error(StatusCodes.Status409Conflict, result.ErrorCode ?? "conflict", "Document is not pending review.")
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

app.MapGet("/v1/admin/tenants/{tenantId:guid}/profile", NotImplemented)
    .RequireScope(ScopeNames.Admin)
    .RequireTenantContext();

await app.RunAsync();

// Expose for WebApplicationFactory<Program> in tests.
public partial class Program;
