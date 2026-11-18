using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VanAn.CoreHub.Domain.Repositories;
using VanAn.ShopERP.Controllers;
using VanAn.ShopERP.Filters;
using VanAn.ShopERP.Services;
using VanAn.Shared.Domain;
using Xunit;

namespace VanAn.ShopERP.Tests.Controllers;

/// <summary>
/// ATRS-21 — InternalCustomerTokenController (atrs → ShopERP mint customer token sau passkey login).
/// Auth = [InternalApiKey] (X-Internal-Api-Key == InternalLoyalty:ApiKey). by-device find-or-create
/// stub (DeviceVerified), by-id resolve sẵn. Token = ICustomerTokenService (DataProtection).
/// </summary>
public class InternalCustomerTokenControllerTests
{
    private static readonly Guid DeviceId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private readonly Mock<ICustomerRepository> _repo = new();
    private readonly CustomerTokenService _tokenService;
    private readonly IConfiguration _config;

    public InternalCustomerTokenControllerTests()
    {
        // Real token service — round-trip ValidateToken proves minted token is usable by /me.
        _tokenService = new CustomerTokenService(new EphemeralDataProtectionProvider());
        _config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Seed:TenantId"] = TenantId.ToString() })
            .Build();
    }

    private InternalCustomerTokenController CreateController()
        => new(_repo.Object, _tokenService, _config, NullLogger<InternalCustomerTokenController>.Instance);

    // ── Auth (InternalApiKey attribute — same contract as MirrorOrders) ──────

    private static AuthorizationFilterContext BuildFilterContext(string? headerKey, string? configKey)
    {
        var httpContext = new DefaultHttpContext();
        if (headerKey is not null)
            httpContext.Request.Headers["X-Internal-Api-Key"] = headerKey;
        var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["InternalLoyalty:ApiKey"] = configKey })
                .Build())
            .BuildServiceProvider();
        httpContext.RequestServices = services;
        return new AuthorizationFilterContext(new ActionContext(httpContext, new RouteData(), new ActionDescriptor()), []);
    }

    [Fact]
    public async Task InternalApiKey_Missing_Returns401()
    {
        var attr = new InternalApiKeyAttribute();
        var ctx = BuildFilterContext(headerKey: null, configKey: "secret");
        await attr.OnAuthorizationAsync(ctx);
        ctx.Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task InternalApiKey_Wrong_Returns401()
    {
        var attr = new InternalApiKeyAttribute();
        var ctx = BuildFilterContext(headerKey: "wrong", configKey: "secret");
        await attr.OnAuthorizationAsync(ctx);
        ctx.Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task InternalApiKey_Valid_Passes()
    {
        var attr = new InternalApiKeyAttribute();
        var ctx = BuildFilterContext(headerKey: "secret", configKey: "secret");
        await attr.OnAuthorizationAsync(ctx);
        ctx.Result.Should().BeNull();
    }

    // ── by-device ───────────────────────────────────────────────────────────

    [Fact]
    public async Task ByDevice_ExistingRealAccount_ReturnsToken_NoCreate()
    {
        var customer = new Customer(new TenantId(TenantId), "Nguyen Van A", string.Empty);
        customer.UpdateCustomerDetails("Nguyen Van A", string.Empty, null, "Bronze", DeviceId, true);
        _repo.Setup(r => r.GetRealAccountByDeviceIdAsync(DeviceId)).ReturnsAsync(customer);

        var result = await CreateController().ByDevice(new InternalCustomerTokenController.ByDeviceRequest(DeviceId, null), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<InternalCustomerTokenController.TokenResponse>(ok.Value);
        Assert.Equal(customer.Id, body.CustomerId);
        Assert.False(body.Created);
        Assert.Equal(customer.Id, _tokenService.ValidateToken(body.CustomerToken)); // round-trip
        _repo.Verify(r => r.AddAsync(It.IsAny<Customer>()), Times.Never);
    }

    [Fact]
    public async Task ByDevice_NoRealAccount_GuestStubExists_UsesStub()
    {
        var stub = new Customer(new TenantId(TenantId), "Khách hàng", string.Empty);
        stub.UpdateCustomerDetails("Khách hàng", string.Empty, null, "Bronze", DeviceId, true);
        _repo.Setup(r => r.GetRealAccountByDeviceIdAsync(DeviceId)).ReturnsAsync((Customer?)null);
        _repo.Setup(r => r.GetByDeviceIdAsync(DeviceId)).ReturnsAsync(stub);

        var result = await CreateController().ByDevice(new InternalCustomerTokenController.ByDeviceRequest(DeviceId, null), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<InternalCustomerTokenController.TokenResponse>(ok.Value);
        Assert.Equal(stub.Id, body.CustomerId);
        Assert.False(body.Created);
        _repo.Verify(r => r.AddAsync(It.IsAny<Customer>()), Times.Never);
    }

    [Fact]
    public async Task ByDevice_NoCustomer_CreatesGuestStub()
    {
        _repo.Setup(r => r.GetRealAccountByDeviceIdAsync(DeviceId)).ReturnsAsync((Customer?)null);
        _repo.Setup(r => r.GetByDeviceIdAsync(DeviceId)).ReturnsAsync((Customer?)null);
        _repo.Setup(r => r.AddAsync(It.IsAny<Customer>()))
            .ReturnsAsync((Customer c) => c);

        var result = await CreateController().ByDevice(new InternalCustomerTokenController.ByDeviceRequest(DeviceId, TenantId), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<InternalCustomerTokenController.TokenResponse>(ok.Value);
        Assert.True(body.Created);
        _repo.Verify(r => r.AddAsync(It.Is<Customer>(c =>
            c.DeviceId == DeviceId
            && c.IdentityLevel == IdentityLevel.Guest // passkey = device possession, không claim verified
            && c.TenantId.Value == TenantId)), Times.Once);
    }

    [Fact]
    public async Task ByDevice_NoTenantId_UsesSeedDefault()
    {
        _repo.Setup(r => r.GetRealAccountByDeviceIdAsync(DeviceId)).ReturnsAsync((Customer?)null);
        _repo.Setup(r => r.GetByDeviceIdAsync(DeviceId)).ReturnsAsync((Customer?)null);
        _repo.Setup(r => r.AddAsync(It.IsAny<Customer>())).ReturnsAsync((Customer c) => c);

        var result = await CreateController().ByDevice(new InternalCustomerTokenController.ByDeviceRequest(DeviceId, null), CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        _repo.Verify(r => r.AddAsync(It.Is<Customer>(c => c.TenantId.Value == TenantId)), Times.Once);
    }

    [Fact]
    public async Task ByDevice_EmptyDeviceId_Returns400()
    {
        var result = await CreateController().ByDevice(
            new InternalCustomerTokenController.ByDeviceRequest(Guid.Empty, null), CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    // ── by-id ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task ById_Existing_ReturnsToken()
    {
        var customerId = Guid.NewGuid();
        var customer = new Customer(new TenantId(TenantId), "A", string.Empty);
        typeof(Customer).GetProperty("Id")!.SetValue(customer, customerId);
        _repo.Setup(r => r.GetByIdAsync(customerId)).ReturnsAsync(customer);

        var result = await CreateController().ById(new InternalCustomerTokenController.ByIdRequest(customerId), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<InternalCustomerTokenController.TokenResponse>(ok.Value);
        Assert.Equal(customerId, body.CustomerId);
        Assert.Equal(customerId, _tokenService.ValidateToken(body.CustomerToken));
    }

    [Fact]
    public async Task ById_NotFound_Returns404()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Customer?)null);
        var result = await CreateController().ById(new InternalCustomerTokenController.ByIdRequest(Guid.NewGuid()), CancellationToken.None);
        Assert.IsType<NotFoundResult>(result);
    }
}
