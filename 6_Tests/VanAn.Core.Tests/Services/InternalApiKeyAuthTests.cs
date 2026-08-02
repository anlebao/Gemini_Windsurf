using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using VanAn.Gateway.Filters;
using Xunit;

namespace VanAn.Tests.Services;

/// <summary>
/// Loyalty Consistency Fix Phase 0 — tests for InternalApiKeyAttribute.
/// Verifies service-to-service auth: Gateway internal endpoints reject missing/wrong API key.
/// Uses DefaultHttpContext + mocked IConfiguration (no full MVC pipeline needed).
/// </summary>
public class InternalApiKeyAuthTests
{
    private const string ValidKey = "test-internal-key-2026";

    private static AuthorizationFilterContext BuildContext(string? headerKey, string? configKey)
    {
        var services = new ServiceCollection();
        var configMock = new Mock<IConfiguration>();
        // InternalApiKeyAttribute reads config["InternalLoyalty:ApiKey"]
        configMock.Setup(c => c["InternalLoyalty:ApiKey"]).Returns(configKey);
        services.AddSingleton<IConfiguration>(configMock.Object);
        var provider = services.BuildServiceProvider();

        var httpContext = new DefaultHttpContext { RequestServices = provider };
        if (headerKey is not null)
        {
            httpContext.Request.Headers["X-Internal-Api-Key"] = headerKey;
        }

        var actionContext = new ActionContext(httpContext, new(), new());
        return new AuthorizationFilterContext(actionContext, []);
    }

    [Fact(DisplayName = "LC-KEY-1: Missing X-Internal-Api-Key header → 401 Unauthorized")]
    public async Task MissingKey_Returns401()
    {
        var attr = new InternalApiKeyAttribute();
        var ctx = BuildContext(headerKey: null, configKey: ValidKey);
        await attr.OnAuthorizationAsync(ctx);
        ctx.Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact(DisplayName = "LC-KEY-2: Wrong X-Internal-Api-Key header → 401 Unauthorized")]
    public async Task WrongKey_Returns401()
    {
        var attr = new InternalApiKeyAttribute();
        var ctx = BuildContext(headerKey: "wrong-key", configKey: ValidKey);
        await attr.OnAuthorizationAsync(ctx);
        ctx.Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact(DisplayName = "LC-KEY-3: Valid X-Internal-Api-Key header → no result (authorized)")]
    public async Task ValidKey_Authorized()
    {
        var attr = new InternalApiKeyAttribute();
        var ctx = BuildContext(headerKey: ValidKey, configKey: ValidKey);
        await attr.OnAuthorizationAsync(ctx);
        ctx.Result.Should().BeNull("valid key → no blocking result → request proceeds");
    }

    [Fact(DisplayName = "LC-KEY-4: Empty config (deployment misconfiguration) → 503 Service Unavailable")]
    public async Task EmptyConfig_Returns503()
    {
        var attr = new InternalApiKeyAttribute();
        var ctx = BuildContext(headerKey: ValidKey, configKey: null);
        await attr.OnAuthorizationAsync(ctx);
        ctx.Result.Should().BeOfType<StatusCodeResult>()
            .Which.StatusCode.Should().Be(503);
    }
}
