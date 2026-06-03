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
            builder.Host.UseSerilog((context, config) =>
            {
                config.WriteTo.Console();

                // Architect: Only enable Disk I/O logging if explicitly turned on in appsettings
                if (context.Configuration.GetValue<bool>("LoggingConfig:EnableFileLogging"))
                {
                    string? appName = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;
                    config.WriteTo.File(
                        path: Path.Combine(AppContext.BaseDirectory, "Logs", $"{appName}-.txt"),
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 2
                    );
                }
            });

            // Add services to the container.
            builder.Services.AddControllers();
            builder.Services.AddSignalR();

            // Add YARP Reverse Proxy
            builder.Services.AddReverseProxy()
                .LoadFromConfig(builder.Configuration);

            // Register VietQR Service
            builder.Services.AddHttpClient<IVietQrService, VietQrService>();
            builder.Services.AddScoped<IVietQrService, VietQrService>();

            // Register Order Services - UPDATE to use CoreHub
            builder.Services.AddScoped<IOrderService, OrderService>();

            // Register Build Service
            builder.Services.AddScoped<IBuildService, BuildService>();

            // Register Customer Services (Domain-First Implementation)
            builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
            builder.Services.AddScoped<ICustomerService, CustomerService>();

            // Register Social Campaign Service
            builder.Services.AddScoped<ISocialCampaignService, SocialCampaignService>();

            // Register Loyalty Rewards Service
            builder.Services.AddScoped<ILoyaltyRewardsService, LoyaltyRewardsService>();

            // Register Swagger for API documentation
            builder.Services.AddSwaggerGen(c =>
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
            builder.Services.AddDbContext<CoreHub.Infrastructure.VanAnDbContext>(options =>
                options.UseNpgsql(connectionString));

            // Register ShopConfig Service
            builder.Services.AddScoped<IShopConfigService, ShopConfigService>();

            // Register Onboarding Service
            builder.Services.AddHttpClient<IOnboardingService, OnboardingService>();
            builder.Services.AddScoped<IOnboardingService, OnboardingService>();

            // Register Voice Command Services
            builder.Services.AddScoped<IVoiceCommandService, VoiceCommandService>();
            builder.Services.AddScoped<IAudioStorageService, AudioStorageService>();
            builder.Services.AddMemoryCache();
            builder.Services.AddScoped<ILocalizationService, LocalizationService>();

            // CORS for frontend
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAll", policy =>
                {
                    policy.AllowAnyOrigin()
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
                    app.UseSwagger();
                    app.UseSwaggerUI();
                }

                // Add unified error handling middleware
                app.UseMiddleware<UnifiedErrorHandler>();

                // Local-First: DISABLE HTTPS REDIRECTION for development
                // app.UseHttpsRedirection();
                app.UseCors("AllowAll");

                // Add Localization Middleware
                app.UseMiddleware<LocalizationMiddleware>();

                // Add YARP Reverse Proxy
                app.MapReverseProxy();

                app.MapControllers();
                app.MapHub<OrderHub>("/orderHub");
                app.MapHub<KitchenHub>("/kitchenhub");

                // Health check endpoint
                app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Service = "VanAn Gateway", Timestamp = DateTime.UtcNow }));

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
