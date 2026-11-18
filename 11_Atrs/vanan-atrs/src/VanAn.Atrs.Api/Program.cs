using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using VanAn.Atrs.Api.Auth;
using VanAn.Atrs.Api.Services;
using VanAn.Atrs.Contracts;
using VanAn.Atrs.Domain;
using VanAn.Atrs.Infra;

namespace VanAn.Atrs.Api;

/// <summary>
/// vanan-atrs — Account Trust & Recovery Service (SRS v1.0, ADR-001).
/// Wave 1: health + DbContext (PG) + JWT auth (staff/admin) + customer token forward
/// (ShopERP /me) + recovery codes + recovery requests + token-version bump hook.
/// </summary>
public static class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddDbContext<AtrsDbContext>(options =>
            options.UseNpgsql(
                    builder.Configuration.GetConnectionString("Atrs")
                    ?? throw new InvalidOperationException("ConnectionStrings:Atrs is required."))
                .UseSnakeCaseNamingConvention());

        builder.Services.AddControllers();
        builder.Services.AddMemoryCache();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.Configure<RecoveryCodeOptions>(builder.Configuration.GetSection("Atrs:RecoveryCodes"));
        builder.Services.Configure<WebAuthnOptions>(builder.Configuration.GetSection("Atrs:WebAuthn"));
        builder.Services.Configure<AtrsRecoveryLimits>(builder.Configuration.GetSection("Atrs:Recovery"));
        builder.Services.AddScoped<IRecoveryCodeService, RecoveryCodeService>();
        builder.Services.AddSingleton<IRiskPolicyEngine, RiskPolicyEngine>();
        builder.Services.AddScoped<IRecoveryCaseManager, RecoveryCaseManager>();
        builder.Services.AddScoped<ITrustProfileService, TrustProfileService>();
        builder.Services.AddScoped<IAtrsCustomerContextService, AtrsCustomerContextService>();
        builder.Services.AddScoped<IWebAuthnService, WebAuthnService>();
        builder.Services.AddScoped<IDeviceService, DeviceService>();
        builder.Services.AddScoped<ISecurityCenterService, SecurityCenterService>();
        builder.Services.AddSingleton<IStepUpService, StepUpService>();
        // ATRS-9/ATRS-10 (Wave 3): dispute vertical + staff RBAC + ledger gateway client.
        builder.Services.AddScoped<IDisputeCaseManager, DisputeCaseManager>();
        builder.Services.AddScoped<IAtrsAuthorizationService, AtrsAuthorizationService>();
        if (!string.IsNullOrEmpty(builder.Configuration["Atrs:GatewayBaseUrl"]))
            builder.Services.AddScoped<ILoyaltyGatewayClient, LoyaltyGatewayClient>();
        else
            builder.Services.AddScoped<ILoyaltyGatewayClient, UnconfiguredLoyaltyGatewayClient>();
        // ATRS-21: passkey login → ShopERP mint customer_token (named client "shoperp" + X-Internal-Api-Key).
        builder.Services.AddScoped<IPasskeyLoginTokenIssuer, ShopErpTokenIssuer>();

        // Wave 4: ATRS-6 subset (personal evidence + consent) + D9 attachments (R2).
        builder.Services.AddScoped<IPersonalEvidenceService, PersonalEvidenceService>();
        builder.Services.AddScoped<IDisputeAttachmentService, DisputeAttachmentService>();
        // Wave 5 W5b: A20 audit explorer query service.
        builder.Services.AddScoped<IAuditQueryService, AuditQueryService>();
        // Wave 5 W5c: A21 security policy (versioned + approval).
        builder.Services.AddScoped<ISecurityPolicyService, SecurityPolicyService>();
        // Wave 5 W5d: ATRS-13 notifications (in-app) + metrics.
        builder.Services.AddScoped<IAtrsNotificationWriter, AtrsNotificationService>();
        builder.Services.AddScoped<IAtrsNotificationQueryService, AtrsNotificationService>();
        builder.Services.AddSingleton<AtrsMetrics>();
        var r2Endpoint = builder.Configuration["Atrs:R2:Endpoint"];
        var r2AccessKey = builder.Configuration["Atrs:R2:AccessKey"];
        var r2SecretKey = builder.Configuration["Atrs:R2:SecretKey"];
        if (!string.IsNullOrEmpty(r2Endpoint) && !string.IsNullOrEmpty(r2AccessKey) && !string.IsNullOrEmpty(r2SecretKey))
            builder.Services.AddSingleton<IAtrsObjectStorage, R2AtrsObjectStorage>();
        else
            builder.Services.AddSingleton<IAtrsObjectStorage, UnconfiguredAtrsObjectStorage>();

        // Customer token validation → forward ShopERP /api/customer-identity/me (pattern Gateway).
        builder.Services.AddHttpClient("shoperp", client =>
        {
            client.BaseAddress = new Uri(builder.Configuration["Atrs:ShopErpBaseUrl"]
                ?? throw new InvalidOperationException("Atrs:ShopErpBaseUrl is required."));
            client.Timeout = TimeSpan.FromSeconds(10);
        });

        // Token-version bump hook → Gateway internal API (revoke token cũ sau recovery).
        var gatewayBaseUrl = builder.Configuration["Atrs:GatewayBaseUrl"];
        if (!string.IsNullOrEmpty(gatewayBaseUrl))
        {
            builder.Services.AddHttpClient("gateway-internal", client =>
            {
                client.BaseAddress = new Uri(gatewayBaseUrl);
                client.Timeout = TimeSpan.FromSeconds(10);
                var apiKey = builder.Configuration["InternalLoyalty:ApiKey"];
                if (!string.IsNullOrEmpty(apiKey))
                    client.DefaultRequestHeaders.Add("X-Internal-Api-Key", apiKey);
            });
            builder.Services.AddScoped<ITokenVersionBumpNotifier, GatewayTokenVersionBumpNotifier>();
        }
        else
        {
            builder.Services.AddScoped<ITokenVersionBumpNotifier, NoopTokenVersionBumpNotifier>();
        }

        var jwtSecret = builder.Configuration["Jwt:Secret"];
        if (!string.IsNullOrEmpty(jwtSecret))
        {
            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
                        ValidateIssuer = true,
                        ValidIssuer = builder.Configuration["Jwt:Issuer"],
                        ValidateAudience = true,
                        ValidAudience = builder.Configuration["Jwt:Audience"],
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.Zero
                    };
                });
            builder.Services.AddAuthorization();
        }

        var app = builder.Build();

        // Apply EF migrations on startup (convention: ShopERP/Gateway/TenantAI đều MigrateAsync).
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AtrsDbContext>();
            await db.Database.MigrateAsync();
        }

        // Health endpoint — cơ chế probe cho CD/smoke test (pattern metering/voice).
        app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "vanan-atrs" }))
            .AllowAnonymous();

        // ATRS-13 (NFR-006): metrics Prometheus text format — internal-only (:5080).
        // Counters từ AtrsMetrics; gauges live từ DB (recovery pending / dispute open / SLA overdue).
        app.MapGet("/metrics", async (AtrsMetrics metrics, IServiceScopeFactory scopeFactory) =>
        {
            var sb = new System.Text.StringBuilder();
            foreach (var (evt, count) in metrics.Snapshot())
            {
                sb.Append("atrs_events_total{event_type=\"").Append(evt).Append("\"} ").Append(count).Append('\n');
            }
            using (var scope = scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AtrsDbContext>();
                var now = DateTime.UtcNow;
                var terminal = new[] { 4, 5, 6, 7, 8 }; // RecoveryRequestStatus terminal ints
                var disputeTerminal = new[] { 6, 7, 8 }; // DisputeCaseStatus terminal ints
                sb.Append("atrs_recovery_pending ").Append(
                    await db.RecoveryRequests.CountAsync(r => !terminal.Contains((int)r.Status))).Append('\n');
                sb.Append("atrs_dispute_open ").Append(
                    await db.DisputeCases.CountAsync(c => !disputeTerminal.Contains((int)c.Status))).Append('\n');
                sb.Append("atrs_sla_overdue ").Append(
                    await db.RecoveryRequests.CountAsync(r => !terminal.Contains((int)r.Status) && r.CreatedAt < now.AddHours(-24))).Append('\n');
            }
            return Results.Text(sb.ToString(), "text/plain; version=0.0.4; charset=utf-8");
        }).AllowAnonymous();

        if (!string.IsNullOrEmpty(jwtSecret))
        {
            app.UseAuthentication();
            app.UseAuthorization();
        }

        app.MapControllers();

        app.Run();
    }
}