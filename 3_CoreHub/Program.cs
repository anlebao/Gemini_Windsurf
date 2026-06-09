using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Services.Events;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Repositories;
using VanAn.CoreHub.Domain.Repositories;
using VanAn.CoreHub.Infrastructure.Repositories;
using VanAn.CoreHub.Interfaces;
using VanAn.CoreHub.Hubs;
using VanAn.CoreHub.Infrastructure.Messaging;
using VanAn.CoreHub.Services.Orchestration;
using VanAn.CoreHub.Services.Resilience;
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
                _ = await context.Database.EnsureCreatedAsync();
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

                    _ = services.AddDbContext<VanAnDbContext>(options =>
                        options.UseNpgsql(connectionString));

                    // Repository layer
                    _ = services.AddScoped<IAccountingEntryRepository, AccountingEntryRepository>();
                    _ = services.AddScoped<IJournalTemplateRepository, JournalTemplateRepository>();
                    _ = services.AddScoped<IOrderRepository, OrderRepository>();
                    _ = services.AddScoped<IHKDBookRepository, HKDBookRepository>();
                    _ = services.AddScoped<IAuditLogRepository, AuditLogRepository>();

                    // Core services
                    _ = services.AddScoped<IAccountingService, AccountingEntryService>();
                    _ = services.AddScoped<IHKDBookService, HKDBookService>();
                    _ = services.AddScoped<IOrderService, OrderService>();
                    _ = services.AddScoped<IAuditTrailService, AuditTrailService>();

                    // Background task queue
                    _ = services.AddSingleton<IBackgroundTaskQueue, BackgroundTaskQueue>();
                    _ = services.AddHostedService<OrderQueueService>();

                    // Enhanced order services
                    _ = services.AddScoped<IOrderQueueService, OrderQueueService>();

                    // SignalR
                    _ = services.AddSignalR();

                    // Template factory (if not already registered)
                    _ = services.AddScoped<ITemplateFactory, TemplateFactory>();

                    // Order hub
                    _ = services.AddScoped<OrderHub>();

                    // Event handling services
                    _ = services.AddHostedService<SimpleAccountingEventHandler>();

                    // E-Invoice Services (Sprint 3 — R4 DI wiring)
                    _ = services.AddMemoryCache();
                    _ = services.AddScoped<IOutboxRepository, OutboxRepository>();
                    _ = services.AddScoped<IInvoicePolicyService, InvoicePolicyService>();
                    _ = services.AddScoped<IRetryPolicyService>(sp =>
                    {
                        Func<VanAn.Shared.Domain.ElectronicInvoiceId, CancellationToken, Task> submitAction =
                            (invoiceId, ct) => Task.CompletedTask; // TODO(F4): Wire to real provider submission
                        return new RetryPolicyService(submitAction, sp.GetRequiredService<ILogger<RetryPolicyService>>());
                    });
                    _ = services.AddScoped<IComplianceService, ComplianceService>();
                    _ = services.AddScoped<IWebhookService, WebhookService>();
                    _ = services.AddScoped<IHKDRevenueClassificationService, HKDRevenueClassificationService>();
                    _ = services.AddScoped<ITenantProviderConfigurationService, TenantProviderConfigurationService>();
                    _ = services.AddScoped<IProviderManager, ProviderManager>();
                    _ = services.AddScoped<IFallbackService, FallbackService>();
                    _ = services.AddScoped<IEInvoiceOrchestrator, EInvoiceOrchestrator>();
                    _ = services.AddSingleton<ICircuitBreakerService, CircuitBreakerService>();
                    _ = services.AddHostedService<EInvoiceWorker>();

                    // Logging
                    _ = services.AddLogging(builder => builder.AddConsole());
                });
        }
    }
}
