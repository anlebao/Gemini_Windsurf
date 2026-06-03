using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Services.Events;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Repositories;
using VanAn.CoreHub.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace VanAn.CoreHub
{
    /// <summary>
    /// CoreHub Service Host for background processing
    /// Handles accounting events and HKD book generation
    /// </summary>
    public class Program
    {
        public static async Task Main(string[] args)
        {
            IHost host = CreateHostBuilder(args).Build();

            // Ensure database is created
            using (IServiceScope scope = host.Services.CreateScope())
            {
                VanAnDbContext context = scope.ServiceProvider.GetRequiredService<VanAnDbContext>();
                await context.Database.EnsureCreatedAsync();
            }

            await host.RunAsync();
        }

        public static IHostBuilder CreateHostBuilder(string[] args)
        {
            return Host.CreateDefaultBuilder(args)
                .ConfigureServices((context, services) =>
                {
                    // Database configuration
                    string connectionString = context.Configuration.GetSection("ConnectionStrings")["DefaultConnection"]
                        ?? "Host=localhost;Database=VanAnCoreHub;Username=vanan_admin;Password=VanAn@2024!";

                    services.AddDbContext<VanAnDbContext>(options =>
                        options.UseNpgsql(connectionString));

                    // Repository layer
                    services.AddScoped<IAccountingEntryRepository, AccountingEntryRepository>();
                    services.AddScoped<IJournalTemplateRepository, JournalTemplateRepository>();
                    services.AddScoped<IOrderRepository, OrderRepository>();
                    services.AddScoped<IHKDBookRepository, HKDBookRepository>();

                    // Core services
                    services.AddScoped<IAccountingService, AccountingEntryService>();
                    services.AddScoped<IHKDBookService, HKDBookService>();
                    services.AddScoped<IOrderService, OrderService>();

                    // Background task queue
                    services.AddSingleton<IBackgroundTaskQueue, BackgroundTaskQueue>();
                    services.AddHostedService<OrderQueueService>();

                    // Enhanced order services
                    services.AddScoped<IOrderQueueService, OrderQueueService>();

                    // SignalR
                    services.AddSignalR();

                    // Template factory (if not already registered)
                    services.AddScoped<ITemplateFactory, TemplateFactory>();

                    // Order hub
                    services.AddScoped<OrderHub>();

                    // Event handling services
                    services.AddHostedService<SimpleAccountingEventHandler>();

                    // Logging
                    services.AddLogging(builder => builder.AddConsole());
                });
        }
    }
}
