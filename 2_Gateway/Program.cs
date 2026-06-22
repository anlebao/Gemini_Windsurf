using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using VanAn.Shared.Services;
using VanAn.Shared.Domain.Common;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Domain.Repositories;
using VanAn.CoreHub.Repositories;
using VanAn.CoreHub.Infrastructure.Repositories;
using VanAn.Gateway.Middleware;
using VanAn.Gateway.Hubs;
using VanAn.Gateway.Services;
using Serilog;
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("VanAn.Tests")]

namespace VanAn.Gateway
{
    public partial class Program
    {
        public static void Main(string[] args)
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

            // Architect: Dynamic file logging configuration
            _ = builder.Host.UseSerilog((context, config) =>
            {
                _ = config.WriteTo.Console();

                // Architect: Only enable Disk I/O logging if explicitly turned on in appsettings
                if (context.Configuration.GetValue<bool>("LoggingConfig:EnableFileLogging"))
                {
                    string? appName = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;
                    _ = config.WriteTo.File(
                        path: Path.Combine(AppContext.BaseDirectory, "Logs", $"{appName}-.txt"),
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 2
                    );
                }
            });

            // Add services to the container.
            _ = builder.Services.AddControllers();
            _ = builder.Services.AddSignalR();

            // Wave 0: JWT + Cookie dual-scheme authentication
            // Cookie is default scheme (keeps Blazor UI working).
            // JwtBearer is secondary scheme for API endpoints — validate tokens issued by ShopERP.
            var jwtSecret = builder.Configuration["Jwt:Secret"]
                ?? throw new InvalidOperationException("Jwt:Secret configuration is required in Gateway.");
            var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "VanAnShopERP";
            var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "VanAnApi";

            _ = builder.Services.AddAuthentication(options =>
            {
                // Cookie remains the default scheme — Blazor UI continues to work unchanged
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            })
                .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
                {
                    options.LoginPath = "/login";
                    options.ExpireTimeSpan = TimeSpan.FromHours(8);
                })
                .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
                        ValidateIssuer = true,
                        ValidIssuer = jwtIssuer,
                        ValidateAudience = true,
                        ValidAudience = jwtAudience,
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.Zero
                    };
                });

            _ = builder.Services.AddAuthorizationBuilder()
                .AddPolicy("RequireTenantAccess", policy =>
                    policy.RequireAuthenticatedUser()
                           .RequireClaim("tenant_id"));

            // Wave 1 Phase 2: Register ITenantProvider for Gateway controllers
            _ = builder.Services.AddHttpContextAccessor();
            _ = builder.Services.AddScoped<ITenantProvider, HttpContextTenantProvider>();

            // Add YARP Reverse Proxy
            _ = builder.Services.AddReverseProxy()
                .LoadFromConfig(builder.Configuration);

            // Register VietQR Service
            _ = builder.Services.AddHttpClient<IVietQrService, VietQrService>();
            _ = builder.Services.AddScoped<IVietQrService, VietQrService>();

            // Register MST Lookup Service (Business Lookup Proxy for KhachLink)
            _ = builder.Services.AddHttpClient("VietQR", client =>
            {
                client.BaseAddress = new Uri("https://api.vietqr.io/v2/");
                client.Timeout = TimeSpan.FromSeconds(3);
            });
            _ = builder.Services.AddScoped<IMstLookupService, MstLookupService>();

            // Register Swagger for API documentation
            _ = builder.Services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new()
                {
                    Title = "VanAn Gateway API",
                    Version = "v1",
                    Description = "VanAn Ecosystem Gateway Service API Documentation"
                });
            });

            // Register ShopConfig Service
            _ = builder.Services.AddScoped<IShopConfigService, ShopConfigService>();

            // Register Onboarding Service
            _ = builder.Services.AddHttpClient<IOnboardingService, OnboardingService>();
            _ = builder.Services.AddScoped<IOnboardingService, OnboardingService>();

            // Register Voice Command Services
            _ = builder.Services.AddScoped<IVoiceCommandService, VoiceCommandService>();
            _ = builder.Services.AddScoped<IAudioStorageService, AudioStorageService>();
            _ = builder.Services.AddMemoryCache();
            _ = builder.Services.AddScoped<ILocalizationService, LocalizationService>();

            // CORS for frontend
            _ = builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAll", policy =>
                {
                    _ = policy.AllowAnyOrigin()
                          .AllowAnyMethod()
                          .AllowAnyHeader();
                });
            });

            WebApplication app = builder.Build();

            try
            {
                Log.Information("🚀 Starting Vạn An Gateway Service...");

                // Configure the HTTP request pipeline.
                if (app.Environment.IsDevelopment())
                {
                    _ = app.UseSwagger();
                    _ = app.UseSwaggerUI();
                }

                // Add unified error handling middleware
                _ = app.UseMiddleware<UnifiedErrorHandler>();

                // Local-First: DISABLE HTTPS REDIRECTION for development
                // app.UseHttpsRedirection();
                _ = app.UseCors("AllowAll");

                // Wave 1 Phase 2: Authentication & Authorization middleware
                _ = app.UseAuthentication();
                _ = app.UseAuthorization();

                // Add Localization Middleware
                _ = app.UseMiddleware<LocalizationMiddleware>();

                // Add YARP Reverse Proxy
                _ = app.MapReverseProxy();

                _ = app.MapControllers();
                _ = app.MapHub<OrderHub>("/orderHub");
                _ = app.MapHub<KitchenHub>("/kitchenhub");

                // Health check endpoint
                _ = app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Service = "VanAn Gateway", Timestamp = DateTime.UtcNow }));

                // ÉP CỨNG BINDING - Fix 404
                // Respect ASPNETCORE_URLS env (Docker: http://+:80). Fallback to 5001 for local dev.
                var aspUrls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
                if (!string.IsNullOrEmpty(aspUrls))
                {
                    app.Run();
                }
                else
                {
                    app.Urls.Add("http://0.0.0.0:5001");
                    app.Run("http://0.0.0.0:5001");
                }
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "❌ Gateway Service terminated unexpectedly");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }
    }

    public partial class Program { }
}
