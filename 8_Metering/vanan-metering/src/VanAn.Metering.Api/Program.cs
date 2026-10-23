using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using VanAn.Metering.Contracts;
using VanAn.Metering.Domain;
using VanAn.Metering.Infra;

var builder = WebApplication.CreateBuilder(args);

var dbPath = builder.Configuration.GetValue<string>("Metering:DbPath") ?? "data/metering.db";
builder.Services.AddDbContext<MeteringDbContext>(o => o.UseSqlite($"Data Source={dbPath}"));
builder.Services.AddScoped<RateCardService>();
builder.Services.AddScoped<UsageService>();

var app = builder.Build();

// Init store (P1 skeleton: EnsureCreated; real EF migrations land with the P13 PG move).
var dbDir = Path.GetDirectoryName(Path.GetFullPath(dbPath));
if (!string.IsNullOrEmpty(dbDir)) Directory.CreateDirectory(dbDir);
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MeteringDbContext>();
    db.Database.EnsureCreated();
    SeedBootstrapCaller(app.Configuration, db, "Metering:BootstrapCaller", isAdmin: false);
    SeedBootstrapCaller(app.Configuration, db, "Metering:BootstrapAdmin", isAdmin: true);
}

static void SeedBootstrapCaller(IConfiguration config, MeteringDbContext db, string configKey, bool isAdmin)
{
    // "callerId:apiKey" — seeds credentials so a fresh container is callable before
    // CallerKey management exists (P7+). BootstrapAdmin seeds the rate-card admin (D5-A).
    var bootstrap = config.GetValue<string>(configKey);
    if (string.IsNullOrWhiteSpace(bootstrap)) return;
    var sep = bootstrap.IndexOf(':', StringComparison.Ordinal);
    if (sep <= 0) return;
    var callerId = bootstrap[..sep].Trim();
    var apiKey = bootstrap[(sep + 1)..].Trim();
    if (callerId.Length == 0 || apiKey.Length == 0) return;
    if (db.CallerKeys.Any(k => k.CallerId == callerId)) return;
    db.CallerKeys.Add(CallerKey.Create(callerId, Sha256Hex(apiKey), isAdmin));
    db.SaveChanges();
}

static string Sha256Hex(string input)
    => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input))).ToUpperInvariant();

// Health — unauthenticated, used by docker HEALTHCHECK + CD smoke.
app.MapGet("/v1/health", () => Results.Ok(new { status = "ok", service = "vanan-metering", utc = DateTime.UtcNow }));

// Caller auth: X-Caller-Id + X-Metering-Key → SHA-256 match on CallerKeys (SRS [G3]/NFR-8).
app.Use(async (ctx, next) =>
{
    if (!ctx.Request.Path.StartsWithSegments("/v1", StringComparison.Ordinal) || ctx.Request.Path == "/v1/health")
    {
        await next();
        return;
    }

    var callerId = ctx.Request.Headers["X-Caller-Id"].FirstOrDefault();
    var apiKey = ctx.Request.Headers["X-Metering-Key"].FirstOrDefault();
    if (string.IsNullOrEmpty(callerId) || string.IsNullOrEmpty(apiKey))
    {
        ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await ctx.Response.WriteAsJsonAsync(new { error = new { code = "missing_credentials", message = "X-Caller-Id and X-Metering-Key headers are required.", retryable = false } });
        return;
    }

    var db = ctx.RequestServices.GetRequiredService<MeteringDbContext>();
    var key = await db.CallerKeys.AsNoTracking().FirstOrDefaultAsync(k => k.CallerId == callerId && k.IsActive);
    if (key == null || key.KeyHash != Sha256Hex(apiKey))
    {
        ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await ctx.Response.WriteAsJsonAsync(new { error = new { code = "invalid_credentials", message = "Unknown caller or invalid key.", retryable = false } });
        return;
    }

    ctx.Items["CallerId"] = callerId;
    ctx.Items["CallerIsAdmin"] = key.IsAdmin;
    await next();
});

// Auth probe — verifies the pipeline end-to-end before P3 ingest lands.
app.MapGet("/v1/auth/check", (HttpContext ctx) => Results.Ok(new { callerId = ctx.Items["CallerId"] }));

// ---- Rate cards (P2 — task_p2_ratecard) ----
static bool IsAdmin(HttpContext ctx) => ctx.Items["CallerIsAdmin"] is true;

static IResult Forbidden() => Results.Json(
    new { error = new { code = "forbidden", message = "Admin caller required.", retryable = false } },
    statusCode: StatusCodes.Status403Forbidden);

static IResult MapError(RateCardOpResult r) => Results.Json(
    new { error = new { code = r.ErrorCode, message = r.ErrorMessage, retryable = false } },
    statusCode: r.ErrorCode switch
    {
        "not_found" => StatusCodes.Status404NotFound,
        "serviceCode_active_exists" or "version_not_current" => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status400BadRequest
    });

static RateCardDto ToDto(RateCardVersion v) => new(
    v.Id, v.ServiceCode, v.MeterUnit, v.CreditRate, v.Enforcement.ToString(),
    v.EffectiveFrom, v.EffectiveTo, v.IsActive, v.Label, v.ChangedBy, v.ChangedAtUtc, v.AuditNote);

app.MapGet("/v1/rate-cards", async (bool includeInactive, RateCardService svc, CancellationToken ct) =>
    Results.Ok((await svc.ListAsync(includeInactive, ct)).Select(ToDto)));

app.MapPost("/v1/rate-cards", async (RateCardCreateRequest req, HttpContext ctx, RateCardService svc, CancellationToken ct) =>
{
    if (!IsAdmin(ctx)) return Forbidden();
    var r = await svc.CreateAsync(req, (string)ctx.Items["CallerId"]!, DateTime.UtcNow, ct);
    return r.Ok
        ? Results.Created($"/v1/rate-cards/{r.Version!.Id}", ToDto(r.Version))
        : MapError(r);
});

app.MapPatch("/v1/rate-cards/{id:guid}", async (Guid id, RateCardPatchRequest req, HttpContext ctx, RateCardService svc, CancellationToken ct) =>
{
    if (!IsAdmin(ctx)) return Forbidden();
    var r = await svc.PatchAsync(id, req, (string)ctx.Items["CallerId"]!, DateTime.UtcNow, ct);
    return r.Ok ? Results.Ok(ToDto(r.Version!)) : MapError(r);
});

app.MapDelete("/v1/rate-cards/{id:guid}", async (Guid id, HttpContext ctx, RateCardService svc, CancellationToken ct) =>
{
    if (!IsAdmin(ctx)) return Forbidden();
    var r = await svc.DeactivateAsync(id, (string)ctx.Items["CallerId"]!, DateTime.UtcNow, ct);
    return r.Ok ? Results.NoContent() : MapError(r);
});

app.MapGet("/v1/rate-cards/resolve", async (string serviceCode, DateTime? atUtc, RateCardService svc, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(serviceCode))
        return Results.Json(new { error = new { code = "invalid_serviceCode", message = "serviceCode is required.", retryable = false } },
            statusCode: StatusCodes.Status400BadRequest);
    var v = await svc.ResolveAsync(serviceCode, atUtc ?? DateTime.UtcNow, ct);
    return v == null
        ? Results.Ok(new { resolved = false, serviceCode })
        : Results.Ok(ToDto(v));
});

// ---- Usage ingest + reporting (P3 — task_p3_ingest) ----

static IResult BadRequest(string code, string message) => Results.Json(
    new { error = new { code, message, retryable = false } },
    statusCode: StatusCodes.Status400BadRequest);

app.MapPost("/v1/usage/events", async (UsageEventRequest req, HttpContext ctx, UsageService svc, CancellationToken ct) =>
{
    var idemKey = ctx.Request.Headers["Idempotency-Key"].FirstOrDefault();
    var r = await svc.IngestAsync((string)ctx.Items["CallerId"]!, idemKey, req, DateTime.UtcNow, ct);
    if (r.ErrorCode != null) return BadRequest(r.ErrorCode, r.ErrorMessage!);
    return r.Replay
        ? Results.Ok(new UsageEventResponse(r.EventId!.Value, true))
        : Results.Created($"/v1/usage/events/{r.EventId}", new UsageEventResponse(r.EventId!.Value));
});

// Google-style custom verb: URL `/v1/usage/events:batch` is a single segment —
// route param captures it ('/' splits, ':' doesn't). WebApplication runs routing
// before user middleware, so a path-rewrite middleware can't work (task_p3 D2).
app.MapPost("/v1/usage/{verb}", async (string verb, UsageBatchItem[]? items, HttpContext ctx, UsageService svc, CancellationToken ct) =>
{
    if (verb != "events:batch") return Results.NotFound();
    if (items == null || items.Length == 0)
        return BadRequest("empty_batch", "Batch must contain at least one event.");
    if (items.Length > UsageService.MaxBatchSize)
        return BadRequest("batch_too_large", $"Batch size must be <= {UsageService.MaxBatchSize}.");
    return Results.Ok(await svc.IngestBatchAsync((string)ctx.Items["CallerId"]!, items, DateTime.UtcNow, ct));
});

app.MapGet("/v1/usage/events", async (Guid tenantId, string? serviceCode, DateTime? from, DateTime? to, int? page, int? pageSize, UsageService svc, CancellationToken ct) =>
{
    if (tenantId == Guid.Empty) return BadRequest("invalid_tenantId", "tenantId must not be empty.");
    var p = page ?? 1;
    var ps = pageSize ?? UsageService.DefaultPageSize;
    if (p < 1) return BadRequest("invalid_page", "page must be >= 1.");
    if (ps < 1) return BadRequest("invalid_pageSize", "pageSize must be >= 1.");
    return Results.Ok(await svc.ListEventsAsync(tenantId, serviceCode, from, to, p, Math.Min(ps, UsageService.MaxPageSize), ct));
});

app.MapGet("/v1/usage/summary", async (Guid tenantId, string period, UsageService svc, CancellationToken ct) =>
{
    var (r, errCode, errMsg) = await svc.GetSummaryAsync(tenantId, period, ct);
    return r != null ? Results.Ok(r) : BadRequest(errCode!, errMsg!);
});

app.Run();
