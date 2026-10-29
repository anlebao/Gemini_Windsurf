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
builder.Services.AddScoped<WalletService>();
builder.Services.AddScoped<EnforcementConfigService>();
builder.Services.AddScoped(s => new TopupService(
    s.GetRequiredService<MeteringDbContext>(),
    s.GetRequiredService<WalletService>(),
    TimeSpan.FromHours(builder.Configuration.GetValue("Metering:TopupTtlHours", 72))));
builder.Services.AddScoped<UsageService>();

var app = builder.Build();

// Init store (P1 skeleton: EnsureCreated; real EF migrations land with the P13 PG move).
var dbDir = Path.GetDirectoryName(Path.GetFullPath(dbPath));
if (!string.IsNullOrEmpty(dbDir)) Directory.CreateDirectory(dbDir);
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MeteringDbContext>();
    db.Database.EnsureCreated();
    EnsureP7Schema(db);
    SeedBootstrapCaller(app.Configuration, db, "Metering:BootstrapCaller", isAdmin: false);
    SeedBootstrapCaller(app.Configuration, db, "Metering:BootstrapAdmin", isAdmin: true);
    // P4: additional non-admin callers ("gateway","directory") — semicolon list of "id:key".
    var extraCallers = app.Configuration.GetValue<string>("Metering:BootstrapCallers");
    if (!string.IsNullOrWhiteSpace(extraCallers))
    {
        foreach (var pair in extraCallers.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            SeedBootstrapCallerValue(db, pair, isAdmin: false);
    }
}

static void SeedBootstrapCaller(IConfiguration config, MeteringDbContext db, string configKey, bool isAdmin)
{
    // "callerId:apiKey" — seeds credentials so a fresh container is callable before
    // CallerKey management exists (P7+). BootstrapAdmin seeds the rate-card admin (D5-A).
    var bootstrap = config.GetValue<string>(configKey);
    if (string.IsNullOrWhiteSpace(bootstrap)) return;
    SeedBootstrapCallerValue(db, bootstrap, isAdmin);
}

static void SeedBootstrapCallerValue(MeteringDbContext db, string pair, bool isAdmin)
{
    var sep = pair.IndexOf(':', StringComparison.Ordinal);
    if (sep <= 0) return;
    var callerId = pair[..sep].Trim();
    var apiKey = pair[(sep + 1)..].Trim();
    if (callerId.Length == 0 || apiKey.Length == 0) return;
    if (db.CallerKeys.Any(k => k.CallerId == callerId)) return;
    db.CallerKeys.Add(CallerKey.Create(callerId, Sha256Hex(apiKey), isAdmin));
    db.SaveChanges();
}

// ---------- P7: wallet / top-up endpoints ----------

app.MapGet("/v1/wallets/{tenantId:guid}", async (
    Guid tenantId, WalletService wallet, CancellationToken ct) =>
    Results.Ok(await wallet.GetWalletAsync(tenantId, ct)));

app.MapGet("/v1/wallets/{tenantId:guid}/check", async (
    Guid tenantId, string serviceCode, decimal? estimate, HttpContext http,
    WalletService wallet, CancellationToken ct) =>
{
    var (type, status) = TenantAttributes(http);
    return Results.Ok(await wallet.CheckAsync(
        tenantId, serviceCode ?? string.Empty, estimate, DateTime.UtcNow, type, status, ct));
});

app.MapPost("/v1/wallets/{tenantId:guid}/grants", async (
    Guid tenantId, WalletGrantRequest req, HttpContext http,
    WalletService wallet, CancellationToken ct) =>
{
    if (!IsAdmin(http)) return Forbidden();
    if (req == null || req.Credits <= 0)
        return Results.BadRequest(new { error = "credits must be > 0" });
    var actor = (string?)http.Items["CallerId"] ?? "unknown";
    await wallet.GrantAsync(tenantId, req.Credits, req.Reason ?? "admin-grant",
        actor, DateTime.UtcNow, ct: ct);
    return Results.Created($"/v1/wallets/{tenantId}", new { tenantId });
});

app.MapGet("/v1/wallets/{tenantId:guid}/ledger", async (
    Guid tenantId, WalletService wallet, int page = 1, int pageSize = 50,
    CancellationToken ct = default) =>
    Results.Ok(await wallet.LedgerAsync(tenantId, page, pageSize, ct)));

app.MapGet("/v1/topup-config", async (HttpContext http, TopupService topups, CancellationToken ct) =>
{
    if (!IsAdmin(http)) return Forbidden();
    var cfg = await topups.GetConfigAsync(ct);
    return cfg == null ? Results.NotFound(new { error = "topup_config_not_set" }) : Results.Ok(cfg);
});

// Issue #197 — master switch "phí nền tảng" toàn hệ thống (SystemAdmin).
app.MapGet("/v1/config/enforcement", async (EnforcementConfigService svc, CancellationToken ct) =>
    Results.Ok(await svc.GetAsync(ct)));

app.MapPut("/v1/config/enforcement", async (
    EnforcementConfigPutRequest? req, HttpContext http,
    EnforcementConfigService svc, CancellationToken ct) =>
{
    if (!IsAdmin(http)) return Forbidden();
    var actor = (string?)http.Items["CallerId"] ?? "unknown";
    return Results.Ok(await svc.SetAsync(req?.Enabled ?? true, actor, DateTime.UtcNow, ct));
});

app.MapPut("/v1/topup-config", async (
    TopupConfigPutRequest req, HttpContext http,
    TopupService topups, CancellationToken ct) =>
{
    if (!IsAdmin(http)) return Forbidden();
    if (req == null || string.IsNullOrWhiteSpace(req.BankCode)
        || string.IsNullOrWhiteSpace(req.AccountNumber))
        return Results.BadRequest(new { error = "bankCode and accountNumber are required" });
    var actor = (string?)http.Items["CallerId"] ?? "unknown";
    return Results.Ok(await topups.PutConfigAsync(req, actor, DateTime.UtcNow, ct));
});

app.MapPost("/v1/topups", async (
    TopupCreateRequest req, HttpContext http,
    TopupService topups, CancellationToken ct) =>
{
    var callerId = (string?)http.Items["CallerId"] ?? "unknown";
    var idem = http.Request.Headers["Idempotency-Key"].FirstOrDefault();
    var r = await topups.CreateAsync(callerId, idem, req, DateTime.UtcNow, ct);
    if (r.Request == null) return Results.BadRequest(new { error = r.ErrorCode, message = r.ErrorMessage });
    var cfg = await topups.GetConfigEntityAsync(ct);
    return Results.Created($"/v1/topups/{r.Request.Id}", ToTopupDto(r.Request, cfg));
});

app.MapGet("/v1/topups", async (
    HttpContext http, TopupService topups, Guid? tenantId, string? status,
    CancellationToken ct = default) =>
    Results.Ok(await topups.ListAsync(status, tenantId, DateTime.UtcNow, ct)));

app.MapPost("/v1/topups/{id:guid}/confirm", async (
    Guid id, TopupConfirmRequest req, HttpContext http,
    TopupService topups, CancellationToken ct) =>
{
    if (!IsAdmin(http)) return Forbidden();
    var actor = (string?)http.Items["CallerId"] ?? "unknown";
    var r = await topups.ConfirmAsync(id, req ?? new TopupConfirmRequest(0), actor, DateTime.UtcNow, ct);
    if (r.Request != null) return Results.Ok(new { r.Request.Id, status = StatusName(r.Request.Status) });
    return r.ErrorCode == "not_found"
        ? Results.NotFound(new { error = r.ErrorCode })
        : r.ErrorCode == "invalid_credits"
            ? Results.BadRequest(new { error = r.ErrorCode, message = r.ErrorMessage })
            : Results.Conflict(new { error = r.ErrorCode, message = r.ErrorMessage });
});

app.MapPost("/v1/topups/{id:guid}/reject", async (
    Guid id, TopupRejectRequest req, HttpContext http,
    TopupService topups, CancellationToken ct) =>
{
    if (!IsAdmin(http)) return Forbidden();
    var actor = (string?)http.Items["CallerId"] ?? "unknown";
    var r = await topups.RejectAsync(id, req ?? new TopupRejectRequest(), actor, DateTime.UtcNow, ct);
    if (r.Request != null) return Results.Ok(new { r.Request.Id, status = StatusName(r.Request.Status) });
    return r.ErrorCode == "not_found"
        ? Results.NotFound(new { error = r.ErrorCode })
        : Results.Conflict(new { error = r.ErrorCode, message = r.ErrorMessage });
});

static string StatusName(TopupStatus s) => s switch
{
    TopupStatus.Confirmed => "confirmed",
    TopupStatus.Rejected => "rejected",
    TopupStatus.Expired => "expired",
    _ => "pending",
};

static TopupDto ToTopupDto(TopupRequest t, TopupConfig? cfg) => new(
    t.Id, t.TenantId, t.ReferenceCode, t.AmountVnd, t.CreditsGranted,
    StatusName(t.Status),
    cfg != null ? VietQrPayloadBuilder.Build(cfg, t.ReferenceCode, t.AmountVnd) : null,
    t.Note, t.ConfirmedBy, t.ConfirmedAtUtc, t.CreatedAtUtc, t.ExpiresAtUtc);

static string Sha256Hex(string input)
    => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input))).ToUpperInvariant();

// P7 — schema upgrade for pre-P7 SQLite DBs (EnsureCreated never alters an existing
// database). Idempotent DDL: creates the 5 wallet/topup tables + 2 UsageEvent columns
// when missing; no-op on fresh DBs (EnsureCreated already made everything) and on
// repeat runs. Real EF migrations land with the P13 PG move.
static void EnsureP7Schema(MeteringDbContext db)
{
    if (!db.Database.IsRelational()) return;
    var conn = db.Database.GetDbConnection();
    var wasOpen = conn.State == System.Data.ConnectionState.Open;
    if (!wasOpen) conn.Open();
    try
    {
        var tables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table'";
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) tables.Add(reader.GetString(0));
        }
        var indexes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='index'";
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) indexes.Add(reader.GetString(0));
        }

#pragma warning disable CA2100 // DDL below is static literals only — never user input
        void Exec(string sql)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }
#pragma warning restore CA2100

        if (!tables.Contains("CreditWallets"))
        {
            Exec("""
                CREATE TABLE "CreditWallets" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_CreditWallets" PRIMARY KEY,
                    "TenantId" TEXT NOT NULL,
                    "BalancePurchased" TEXT NOT NULL,
                    "BalancePool" TEXT NOT NULL,
                    "BalanceEarned" TEXT NOT NULL,
                    "GrantedTotal" TEXT NOT NULL,
                    "CycleKey" TEXT NOT NULL,
                    "UpdatedAtUtc" TEXT NOT NULL
                )
                """);
            Exec("""CREATE UNIQUE INDEX "IX_CreditWallets_TenantId" ON "CreditWallets" ("TenantId")""");
            Exec("""
                CREATE TABLE "CreditLedgerEntries" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_CreditLedgerEntries" PRIMARY KEY,
                    "TenantId" TEXT NOT NULL,
                    "Type" INTEGER NOT NULL,
                    "Source" INTEGER NOT NULL,
                    "Credits" TEXT NOT NULL,
                    "BalanceAfter" TEXT NOT NULL,
                    "UsageEventId" TEXT NULL,
                    "TopupRequestId" TEXT NULL,
                    "Reason" TEXT NOT NULL,
                    "CreatedBy" TEXT NOT NULL,
                    "CreatedAtUtc" TEXT NOT NULL
                )
                """);
            Exec("""CREATE INDEX "IX_CreditLedgerEntries_TenantId_CreatedAtUtc" ON "CreditLedgerEntries" ("TenantId", "CreatedAtUtc")""");
            Exec("""CREATE INDEX "IX_CreditLedgerEntries_UsageEventId" ON "CreditLedgerEntries" ("UsageEventId")""");
            Exec("""CREATE UNIQUE INDEX "IX_CreditLedgerEntries_TopupRequestId" ON "CreditLedgerEntries" ("TopupRequestId") WHERE "TopupRequestId" IS NOT NULL""");
            Exec("""
                CREATE TABLE "WalletAlerts" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_WalletAlerts" PRIMARY KEY,
                    "TenantId" TEXT NOT NULL,
                    "Level" INTEGER NOT NULL,
                    "CycleKey" TEXT NOT NULL,
                    "FiredAtUtc" TEXT NOT NULL,
                    "NotifiedChannels" TEXT NOT NULL
                )
                """);
            Exec("""CREATE UNIQUE INDEX "IX_WalletAlerts_TenantId_Level_CycleKey" ON "WalletAlerts" ("TenantId", "Level", "CycleKey")""");
            Exec("""
                CREATE TABLE "TopupConfigs" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_TopupConfigs" PRIMARY KEY,
                    "BankCode" TEXT NOT NULL,
                    "BankName" TEXT NOT NULL,
                    "AccountNumber" TEXT NOT NULL,
                    "AccountName" TEXT NOT NULL,
                    "UpdatedBy" TEXT NOT NULL,
                    "UpdatedAtUtc" TEXT NOT NULL
                )
                """);
            Exec("""
                CREATE TABLE "TopupRequests" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_TopupRequests" PRIMARY KEY,
                    "TenantId" TEXT NOT NULL,
                    "ReferenceCode" TEXT NOT NULL,
                    "AmountVnd" TEXT NULL,
                    "CreditsGranted" TEXT NULL,
                    "Status" INTEGER NOT NULL,
                    "ConfirmedBy" TEXT NULL,
                    "ConfirmedAtUtc" TEXT NULL,
                    "Note" TEXT NULL,
                    "CreatedAtUtc" TEXT NOT NULL,
                    "ExpiresAtUtc" TEXT NOT NULL,
                    "CallerId" TEXT NOT NULL,
                    "IdempotencyKey" TEXT NOT NULL
                )
                """);
            Exec("""CREATE UNIQUE INDEX "IX_TopupRequests_ReferenceCode" ON "TopupRequests" ("ReferenceCode")""");
            Exec("""CREATE UNIQUE INDEX "IX_TopupRequests_CallerId_IdempotencyKey" ON "TopupRequests" ("CallerId", "IdempotencyKey")""");
            Exec("""CREATE INDEX "IX_TopupRequests_TenantId_Status" ON "TopupRequests" ("TenantId", "Status")""");
        }

        // Issue #197 — master switch singleton (created standalone for DBs that
        // already ran P7; fresh DBs get it via EnsureCreated).
        if (!tables.Contains("EnforcementConfigs"))
        {
            Exec("""
                CREATE TABLE "EnforcementConfigs" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_EnforcementConfigs" PRIMARY KEY,
                    "Enabled" INTEGER NOT NULL,
                    "UpdatedBy" TEXT NOT NULL,
                    "UpdatedAtUtc" TEXT NOT NULL
                )
                """);
        }

        if (tables.Contains("UsageEvents"))
        {
            var cols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "PRAGMA table_info('UsageEvents')";
                using var reader = cmd.ExecuteReader();
                while (reader.Read()) cols.Add(reader.GetString(1));
            }
            if (!cols.Contains("CreditsApplied"))
                Exec("""ALTER TABLE "UsageEvents" ADD COLUMN "CreditsApplied" TEXT NULL""");
            if (!cols.Contains("UnpaidCredits"))
                Exec("""ALTER TABLE "UsageEvents" ADD COLUMN "UnpaidCredits" TEXT NULL""");
        }

        // Issue #197 — rate-card scope (append-only additive; existing rows = Global).
        if (tables.Contains("RateCardVersions"))
        {
            var cols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "PRAGMA table_info('RateCardVersions')";
                using var reader = cmd.ExecuteReader();
                while (reader.Read()) cols.Add(reader.GetString(1));
            }
            if (!cols.Contains("Scope"))
                Exec("""ALTER TABLE "RateCardVersions" ADD COLUMN "Scope" INTEGER NOT NULL DEFAULT 0""");
            if (!cols.Contains("ScopeValue"))
                Exec("""ALTER TABLE "RateCardVersions" ADD COLUMN "ScopeValue" TEXT NULL""");
            // Unique index now covers (ServiceCode, Scope, EffectiveFrom) — the old
            // index would reject two chains on different scopes sharing EffectiveFrom.
            if (indexes.Contains("IX_RateCardVersions_ServiceCode_EffectiveFrom"))
            {
                Exec("DROP INDEX \"IX_RateCardVersions_ServiceCode_EffectiveFrom\"");
                Exec("""CREATE UNIQUE INDEX "IX_RateCardVersions_ServiceCode_Scope_EffectiveFrom" ON "RateCardVersions" ("ServiceCode", "Scope", "EffectiveFrom")""");
            }
            else if (!indexes.Contains("IX_RateCardVersions_ServiceCode_Scope_EffectiveFrom"))
            {
                Exec("""CREATE UNIQUE INDEX "IX_RateCardVersions_ServiceCode_Scope_EffectiveFrom" ON "RateCardVersions" ("ServiceCode", "Scope", "EffectiveFrom")""");
            }
        }
    }
    finally
    {
        if (!wasOpen) conn.Close();
    }
}

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
    v.EffectiveFrom, v.EffectiveTo, v.IsActive, v.Label, v.ChangedBy, v.ChangedAtUtc, v.AuditNote,
    v.Scope.ToString(), v.ScopeValue);

// Issue #197 — caller-supplied tenant attributes (D1=A): optional headers that scope
// rate resolution (Tenant > TenantType > TenantStatus > Global). Absent → Global-only.
static (string? type, string? status) TenantAttributes(HttpContext ctx) => (
    ctx.Request.Headers["X-Tenant-Type"].FirstOrDefault(),
    ctx.Request.Headers["X-Tenant-Status"].FirstOrDefault());

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

app.MapGet("/v1/rate-cards/resolve", async (string serviceCode, DateTime? atUtc, Guid? tenantId, HttpContext ctx, RateCardService svc, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(serviceCode))
        return Results.Json(new { error = new { code = "invalid_serviceCode", message = "serviceCode is required.", retryable = false } },
            statusCode: StatusCodes.Status400BadRequest);
    var (type, status) = TenantAttributes(ctx);
    var v = await svc.ResolveForTenantAsync(serviceCode.Trim(), tenantId, type, status, atUtc ?? DateTime.UtcNow, ct);
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
    var (type, status) = TenantAttributes(ctx);
    var r = await svc.IngestAsync((string)ctx.Items["CallerId"]!, idemKey, req, DateTime.UtcNow, type, status, ct);
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
    var (type, status) = TenantAttributes(ctx);
    return Results.Ok(await svc.IngestBatchAsync((string)ctx.Items["CallerId"]!, items, DateTime.UtcNow, type, status, ct));
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

app.MapGet("/v1/usage/summary", async (Guid tenantId, string period, HttpContext ctx, UsageService svc, CancellationToken ct) =>
{
    var (type, status) = TenantAttributes(ctx);
    var (r, errCode, errMsg) = await svc.GetSummaryAsync(tenantId, period, type, status, ct);
    return r != null ? Results.Ok(r) : BadRequest(errCode!, errMsg!);
});

app.Run();
