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

app.MapPost("/v1/chat", NotImplemented)
    .RequireScope(ScopeNames.PublicChat)
    .RequireTenantContext();

app.MapGet("/v1/config/widget", NotImplemented)
    .RequireScope(ScopeNames.PublicChat)
    .RequireTenantContext();

app.MapPost("/v1/knowledge/products:batch", NotImplemented)
    .RequireScope(ScopeNames.Ingest)
    .RequireTenantContext();

app.MapGet("/v1/admin/tenants/{tenantId:guid}/profile", NotImplemented)
    .RequireScope(ScopeNames.Admin)
    .RequireTenantContext();

await app.RunAsync();

// Expose for WebApplicationFactory<Program> in tests.
public partial class Program;
