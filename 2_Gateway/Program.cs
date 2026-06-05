using VanAn.Shared.Services;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Domain.Repositories;
using VanAn.CoreHub.Infrastructure.Repositories;
using VanAn.Gateway.Middleware;
using VanAn.Gateway.Hubs;
using Microsoft.EntityFrameworkCore;
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

            // Add YARP Reverse Proxy
            _ = builder.Services.AddReverseProxy()
                .LoadFromConfig(builder.Configuration);

            // Register VietQR Service
            _ = builder.Services.AddHttpClient<IVietQrService, VietQrService>();
            _ = builder.Services.AddScoped<IVietQrService, VietQrService>();

            // Register Order Services - UPDATE to use CoreHub
            _ = builder.Services.AddScoped<IOrderService, OrderService>();

            // Register Build Service
            _ = builder.Services.AddScoped<IBuildService, BuildService>();

            // Register Customer Services (Domain-First Implementation)
            _ = builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
            _ = builder.Services.AddScoped<ICustomerService, CustomerService>();

            // Register Audit Trail Services (Phase 2.9.4)
            _ = builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();
            _ = builder.Services.AddScoped<IAuditTrailService, AuditTrailService>();

            // Register Social Campaign Service
            _ = builder.Services.AddScoped<ISocialCampaignService, SocialCampaignService>();

            // Register Loyalty Rewards Service
            _ = builder.Services.AddScoped<ILoyaltyRewardsService, LoyaltyRewardsService>();

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

            // Add DbContext (PostgreSQL for development)
            string? connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
            _ = builder.Services.AddDbContext<CoreHub.Infrastructure.VanAnDbContext>(options =>
                options.UseNpgsql(connectionString));

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
                app.Urls.Add("http://0.0.0.0:5001");
                app.Run("http://0.0.0.0:5001");
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
