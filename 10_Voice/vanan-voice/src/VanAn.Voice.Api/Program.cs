using Microsoft.EntityFrameworkCore;
using VanAn.Voice.Api.Auth;
using VanAn.Voice.Api.Hubs;
using VanAn.Voice.Api.Services;
using VanAn.Voice.Infra;

var builder = WebApplication.CreateBuilder(args);

// CORS — host applications connect cross-origin (SDK + token); origins configured via
// Voice:CorsOrigins (comma-separated). Empty = same-origin only.
var corsOrigins = (builder.Configuration["Voice:CorsOrigins"] ?? string.Empty)
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
if (corsOrigins.Length > 0)
{
    builder.Services.AddCors(o => o.AddPolicy("voice-cors", p =>
        p.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
}

var dbPath = builder.Configuration["Voice:DbPath"] ?? "data/voice.db";
builder.Services.AddDbContext<VoiceDbContext>(o => o.UseSqlite($"Data Source={dbPath}"));

builder.Services.AddSignalR();
builder.Services.AddSingleton<VoiceStateStore>();
builder.Services.AddSingleton<TenantFeatureStore>();
builder.Services.AddSingleton<RateLimiter>();
builder.Services.AddSingleton<TurnCredentialService>();
builder.Services.AddSingleton<ICallEventPublisher, CallEventPublisher>();
builder.Services.AddScoped<PermissionService>();
builder.Services.AddScoped<CallService>();
builder.Services.AddScoped<VoiceMetricsService>();

// Auth validators (SRS v1.1 O2): staff JWT first, then customer token.
builder.Services.AddScoped<IVoiceTokenValidator, StaffJwtValidator>();
builder.Services.AddScoped<IVoiceTokenValidator, CustomerTokenValidator>();
builder.Services.AddScoped<VoiceIdentityResolver>();

// Context verification (O6): PostgreSQL read-only when configured, otherwise fail closed.
if (!string.IsNullOrWhiteSpace(builder.Configuration["Voice:GatewayDb"]))
    builder.Services.AddScoped<IContextVerifier, PostgresContextVerifier>();
else
    builder.Services.AddScoped<IContextVerifier, NullContextVerifier>();

// Metering client — thin, fail-open (SRS §14, metering convention G9).
builder.Services.AddHttpClient<MeteringClient>(client =>
{
    var baseUrl = builder.Configuration["Metering:BaseUrl"];
    if (!string.IsNullOrWhiteSpace(baseUrl))
    {
        if (!baseUrl.EndsWith('/')) baseUrl += "/";
        client.BaseAddress = new Uri(baseUrl);
    }
    client.Timeout = TimeSpan.FromSeconds(3);
    var callerId = builder.Configuration["Metering:CallerId"];
    var callerKey = builder.Configuration["Metering:CallerKey"];
    if (!string.IsNullOrWhiteSpace(callerId)) client.DefaultRequestHeaders.Add("X-Caller-Id", callerId);
    if (!string.IsNullOrWhiteSpace(callerKey)) client.DefaultRequestHeaders.Add("X-Metering-Key", callerKey);
});

// Customer identity forward (customer token validation).
builder.Services.AddHttpClient("shoperp", client =>
{
    var baseUrl = builder.Configuration["Voice:ShopErpBaseUrl"];
    if (!string.IsNullOrWhiteSpace(baseUrl)) client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(3);
});

var app = builder.Build();

// Production guard: Jwt:Secret is mandatory (mirrors Gateway ValidateProductionConfig).
var jwtSecret = app.Configuration["Jwt:Secret"];
if (string.IsNullOrWhiteSpace(jwtSecret) || jwtSecret.Contains("__REPLACE_", StringComparison.Ordinal))
    throw new InvalidOperationException("Jwt:Secret is missing or still has __REPLACE_* sentinel. Set via Jwt__Secret env var.");
if (jwtSecret.Length < 32)
    throw new InvalidOperationException("Jwt:Secret must be at least 32 characters for HS256 security.");

// Init store: idempotent schema bootstrap + startup reconciliation (SRS §7).
var dbDir = Path.GetDirectoryName(Path.GetFullPath(dbPath));
if (!string.IsNullOrEmpty(dbDir)) Directory.CreateDirectory(dbDir);
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<VoiceDbContext>();
    db.EnsureVoiceSchema();
    var swept = await VoiceReconciliation.MarkStaleAsync(db, DateTime.UtcNow);
    if (swept > 0)
        app.Logger.LogWarning("Voice reconciliation: {Count} stale calls marked FAILED (SERVER_RESTART).", swept);
}

if (corsOrigins.Length > 0)
    app.UseCors("voice-cors");

app.MapGet("/v1/health", () => Results.Ok(new { status = "ok", service = "vanan-voice" }));

// Pilot/ops metrics (SRS §14): aggregate call statistics for the Gate-1 baseline.
app.MapGet("/v1/metrics/summary", async (VoiceMetricsService metrics, CancellationToken ct) =>
    Results.Ok(await metrics.SummaryAsync(DateTime.UtcNow, ct)));

app.MapHub<VoiceHub>("/hubs/voice");

// Admin: per-tenant kill-switch + open-calling flags (SRS §14.4). In-memory in P1
// (persistence via admin UI lands in P4).
app.MapGet("/v1/tenants/{tenantId:guid}/voice-config", (Guid tenantId, TenantFeatureStore store) =>
    Results.Ok(store.Get(tenantId)));

app.MapPut("/v1/tenants/{tenantId:guid}/voice-config", (Guid tenantId, VoiceTenantConfig config, TenantFeatureStore store) =>
    Results.Ok(store.Set(tenantId, config)));

// FR-014: callee reads its own missed-call events on next app open (authenticated — the user
// may only read their own events).
app.MapGet("/v1/missed-calls/{userId:guid}", async (
    Guid userId, HttpContext http, VoiceIdentityResolver identityResolver, VoiceDbContext db, CancellationToken ct) =>
{
    var identity = await identityResolver.ResolveAsync(http, ct);
    if (identity == null) return Results.Unauthorized();
    if (identity.UserId != userId)
        return Results.Json(new { error = "forbidden" }, statusCode: StatusCodes.Status403Forbidden);

    return Results.Ok(await db.VoiceCallEvents
        .Where(e => e.EventType == "missed-call" && e.ActorUserId == userId)
        .OrderByDescending(e => e.OccurredAt)
        .Take(50)
        .Select(e => new { e.CallId, e.OccurredAt, e.MetadataJson })
        .ToListAsync(ct));
});

// TURN credentials (SRS §8.2, SEC-004): short-lived, HMAC-signed with the server-side secret.
// Authenticated like the hub (query access_token or Authorization header); rate-limited 30/h/user.
app.MapGet("/v1/turn/credentials", async (
    HttpContext http, VoiceIdentityResolver identityResolver, TurnCredentialService turn,
    RateLimiter rateLimiter, CancellationToken ct) =>
{
    var identity = await identityResolver.ResolveAsync(http, ct);
    if (identity == null) return Results.Unauthorized();

    if (!rateLimiter.TryAcquire($"turn:{identity.UserId}", max: 30, window: TimeSpan.FromHours(1)))
        return Results.Json(new { error = "rate limit exceeded" }, statusCode: StatusCodes.Status429TooManyRequests);

    var credentials = turn.Issue(identity.UserId, DateTime.UtcNow);
    return credentials == null
        ? Results.Json(new { error = "turn_not_configured" }, statusCode: StatusCodes.Status503ServiceUnavailable)
        : Results.Ok(credentials);
});

await app.RunAsync();

// Exposed for WebApplicationFactory-based tests (SRS deliverable: integration tests).
namespace VanAn.Voice.Api
{
    public partial class VoiceProgram;
}