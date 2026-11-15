using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using VanAn.Atrs.Domain;
using VanAn.Atrs.Infra;

namespace VanAn.Atrs.Api;

/// <summary>
/// vanan-atrs — Account Trust & Recovery Service (SRS v1.0, ADR-001).
/// Wave 1 skeleton: health + DbContext + JWT auth wiring (staff/admin) + services.
/// Auth chi tiết (StaffJwtValidator clone + TokenVersionValidator + customer
/// token forward) được bổ sung ở wave ATRS-3.
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

        builder.Services.AddMemoryCache();
        builder.Services.Configure<RecoveryCodeOptions>(builder.Configuration.GetSection("Atrs:RecoveryCodes"));
        builder.Services.AddScoped<IRecoveryCodeService, RecoveryCodeService>();
        builder.Services.AddSingleton<IRiskPolicyEngine, RiskPolicyEngine>();
        builder.Services.AddScoped<IRecoveryCaseManager, RecoveryCaseManager>();

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

        app.Run();
    }
}