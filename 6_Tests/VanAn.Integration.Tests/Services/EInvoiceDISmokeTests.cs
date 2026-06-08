using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Domain.Repositories;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Infrastructure.Messaging;
using VanAn.CoreHub.Infrastructure.Repositories;
using VanAn.CoreHub.Repositories;
using VanAn.CoreHub.Services.Orchestration;
using VanAn.CoreHub.Services.Resilience;
using VanAn.CoreHub.Services;
using VanAn.Integration.Tests.Infrastructure;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Common;
using Xunit;
using FluentAssertions;

namespace VanAn.Integration.Tests.Services;

public class EInvoiceDISmokeTests
{
    private static IServiceProvider BuildEInvoiceServices()
    {
        var services = new ServiceCollection();

        services.AddLogging(b => b.AddConsole());
        services.AddMemoryCache();

        services.AddDbContext<VanAnDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddScoped<IVanAnDbContext>(sp => sp.GetRequiredService<VanAnDbContext>());

        services.AddScoped<ITenantProvider, TestTenantProvider>();

        services.AddScoped<IOutboxRepository, OutboxRepository>();
        services.AddScoped<IInvoicePolicyService, InvoicePolicyService>();
        services.AddScoped<IRetryPolicyService, RetryPolicyService>();
        services.AddScoped<IComplianceService, ComplianceService>();
        services.AddScoped<IWebhookService, WebhookService>();
        services.AddScoped<IAccountingEntryRepository, AccountingEntryRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IAuditTrailService, AuditTrailService>();
        services.AddScoped<IAccountingService, AccountingEntryService>();
        services.AddScoped<IHKDRevenueClassificationService, HKDRevenueClassificationService>();
        services.AddScoped<ITenantProviderConfigurationService, TenantProviderConfigurationService>();
        services.AddScoped<IProviderManager, ProviderManager>();
        services.AddScoped<IFallbackService, FallbackService>();
        services.AddScoped<IEInvoiceOrchestrator, EInvoiceOrchestrator>();
        services.AddSingleton<ICircuitBreakerService, CircuitBreakerService>();
        services.AddHostedService<EInvoiceWorker>();

        return services.BuildServiceProvider();
    }

    [Fact]
    public void IEInvoiceOrchestrator_ShouldResolveWithoutException()
    {
        var provider = BuildEInvoiceServices();
        using var scope = provider.CreateScope();

        var act = () => scope.ServiceProvider.GetRequiredService<IEInvoiceOrchestrator>();

        act.Should().NotThrow();
    }

    [Fact]
    public void IOutboxRepository_ShouldResolveWithoutException()
    {
        var provider = BuildEInvoiceServices();
        using var scope = provider.CreateScope();

        var act = () => scope.ServiceProvider.GetRequiredService<IOutboxRepository>();

        act.Should().NotThrow();
    }

    [Fact]
    public void ICircuitBreakerService_ShouldResolveSingleton_SameInstanceBothTimes()
    {
        var provider = BuildEInvoiceServices();

        var instance1 = provider.GetRequiredService<ICircuitBreakerService>();
        var instance2 = provider.GetRequiredService<ICircuitBreakerService>();

        instance1.Should().BeSameAs(instance2);
    }

    [Fact]
    public void EInvoiceWorker_ShouldBeRegisteredAsHostedService()
    {
        var provider = BuildEInvoiceServices();

        var hostedServices = provider.GetServices<IHostedService>();

        hostedServices.Should().Contain(s => s.GetType() == typeof(EInvoiceWorker));
    }

    [Fact]
    public void AllEInvoiceDeps_ResolveInSameScope_ShouldNotThrow()
    {
        var provider = BuildEInvoiceServices();
        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;

        var act = () =>
        {
            sp.GetRequiredService<IOutboxRepository>();
            sp.GetRequiredService<IInvoicePolicyService>();
            sp.GetRequiredService<IRetryPolicyService>();
            sp.GetRequiredService<IComplianceService>();
            sp.GetRequiredService<IWebhookService>();
            sp.GetRequiredService<IHKDRevenueClassificationService>();
            sp.GetRequiredService<ITenantProviderConfigurationService>();
            sp.GetRequiredService<IProviderManager>();
            sp.GetRequiredService<IFallbackService>();
            sp.GetRequiredService<IEInvoiceOrchestrator>();
            sp.GetRequiredService<ICircuitBreakerService>();
        };

        act.Should().NotThrow();
    }
}
