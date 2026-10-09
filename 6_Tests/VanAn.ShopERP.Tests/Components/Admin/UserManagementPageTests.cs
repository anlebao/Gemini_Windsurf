using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VanAn.CoreHub.Services;
using VanAn.ShopERP.Services;
using VanAn.Shared.Domain.Aggregates.TenantAggregate;
using Xunit;

namespace VanAn.ShopERP.Tests.Components.Admin;

/// <summary>
/// Fix 2026-10-09 (bug: combo tenant chỉ 5-6 tenant cũ): SystemAdmin tạo user → tenant selector
/// phải load từ Gateway PG (source of truth) + lọc ĐÃ VERIFY (Status != Pending) — không đọc SQLite mirror.
/// </summary>
public class UserManagementPageTests : ComponentTestBase
{
    private sealed class FakeHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(response);
    }

    private sealed class SysAdminAuthProvider : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            var identity = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.Name, "Sysadmin"),
                new Claim(ClaimTypes.NameIdentifier, "sysadmin-id"),
                new Claim("TenantId", Guid.Empty.ToString())
            }, "TestAuthentication");
            identity.AddClaim(new Claim(ClaimTypes.Role, "SystemAdmin"));
            return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(identity)));
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private void RegisterTenantApi(List<TenantApiDto> tenants)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(tenants, JsonOptions), Encoding.UTF8, "application/json")
        };
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new HttpClient(new FakeHandler(response)));
        Services.AddSingleton(factory.Object);

        var config = new ConfigurationBuilder().Build();
        Services.AddSingleton<IConfiguration>(config);
        Services.AddSingleton<AuthenticationStateProvider, SysAdminAuthProvider>();

        var jwt = new Mock<IJwtTokenService>();
        jwt.Setup(j => j.GenerateToken(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<IEnumerable<Claim>?>()))
           .Returns("test-jwt");
        Services.AddSingleton(jwt.Object);

        Services.AddSingleton(sp => new TenantApiClient(
            factory.Object, config, jwt.Object,
            sp.GetRequiredService<AuthenticationStateProvider>(),
            NullLogger<TenantApiClient>.Instance));

        // Các deps còn lại của page.
        Services.AddSingleton(new Mock<IUserManagementService>().Object);
        Services.AddSingleton(new Mock<IRoleAssignmentService>().Object);
        Services.AddSingleton(new Mock<VanAn.CoreHub.Infrastructure.IVanAnDbContext>().Object);
    }

    [Fact]
    public void SystemAdmin_Combo_ShowsOnlyVerifiedTenants_FromGateway()
    {
        RegisterTenantApi(new List<TenantApiDto>
        {
            new() { Id = Guid.NewGuid(), Name = "Tenant A (Active)", Status = TenantStatus.Active },
            new() { Id = Guid.NewGuid(), Name = "Tenant B (Suspended)", Status = TenantStatus.Suspended },
            new() { Id = Guid.NewGuid(), Name = "Tenant Pending", Status = TenantStatus.Pending },
        });

        var cut = RenderComponent<ShopERP.Components.Pages.Admin.UserManagement>();

        cut.WaitForAssertion(() => cut.Find("h1").TextContent.Should().Contain("Quản lý người dùng"));
        cut.FindAll("button").First(b => b.TextContent.Contains("Tạo người dùng")).Click();
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Thuộc tenant"));

        var options = cut.FindAll("#user-tenant option");
        options.Should().HaveCount(2); // Pending bị loại
        options.Select(o => o.TextContent).Should().Contain("Tenant A (Active)");
        options.Select(o => o.TextContent).Should().Contain("Tenant B (Suspended)");
        options.Select(o => o.TextContent).Should().NotContain("Tenant Pending");
    }

    [Fact]
    public void SystemAdmin_Combo_Empty_WhenNoVerifiedTenants()
    {
        RegisterTenantApi(new List<TenantApiDto>
        {
            new() { Id = Guid.NewGuid(), Name = "Tenant Pending Only", Status = TenantStatus.Pending },
        });

        var cut = RenderComponent<ShopERP.Components.Pages.Admin.UserManagement>();
        cut.WaitForAssertion(() => cut.Find("h1").TextContent.Should().Contain("Quản lý người dùng"));

        cut.FindAll("button").First(b => b.TextContent.Contains("Tạo người dùng")).Click();
        // Không có tenant verified → hiển thị input disabled (không phải select).
        cut.WaitForAssertion(() => cut.FindAll("#user-tenant").Should().BeEmpty());
    }
}
