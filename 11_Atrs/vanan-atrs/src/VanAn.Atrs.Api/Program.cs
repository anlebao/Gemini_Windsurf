using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using VanAn.Atrs.Api.Auth;
using VanAn.Atrs.Api.Services;
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
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddDbContext<AtrsDbContext>(options =>
            options.UseNpgsql(
                    builder.Configuration.GetConnectionString("Atrs")
                    ?? throw new InvalidOperationException("ConnectionStrings:Atrs is required."))
                .UseSnakeCaseNamingConvention());

        builder.Services.AddControllers();
        builder.Services.AddMemoryCache();
        builder.Services.Configure<RecoveryCodeOptions>(builder.Configuration.GetSection("Atrs:RecoveryCodes"));
        builder.Services.AddScoped<IRecoveryCodeService, RecoveryCodeService>();
        builder.Services.AddSingleton<IRiskPolicyEngine, RiskPolicyEngine>();
        builder.Services.AddScoped<IRecoveryCaseManager, RecoveryCaseManager>();
        builder.Services.AddScoped<ITrustProfileService, TrustProfileService>();
        builder.Services.AddScoped<IAtrsCustomerContextService, AtrsCustomerContextService>();

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

        // Health endpoint — cơ chế probe cho CD/smoke test (pattern metering/voice).
        app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "vanan-atrs" }))
            .AllowAnonymous();

        if (!string.IsNullOrEmpty(jwtSecret))
        {
            app.UseAuthentication();
            app.UseAuthorization();
        }

        app.MapControllers();

        app.Run();
    }
}