using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using VanAn.Voice.Api.Auth;
using VanAn.Voice.Domain;

namespace VanAn.Voice.Tests;

/// <summary>
/// CustomerTokenValidator transport coverage (2026-11-12): the SDK sends the host's token
/// through SignalR accessTokenFactory → "access_token" query, and TURN/missed-calls fetches
/// use "Authorization: Bearer". The validator must accept those channels in addition to the
/// dedicated customerToken / X-Customer-Token pair.
/// </summary>
public class CustomerTokenValidatorTests
{
    private static readonly Guid CustomerId = Guid.NewGuid();

    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public string? SeenCustomerTokenHeader { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            SeenCustomerTokenHeader = request.Headers.TryGetValues("X-Customer-Token", out var v)
                ? string.Join(',', v) : null;
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class StubFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler) { BaseAddress = new Uri("https://shoperp.test") };
    }

    private static CustomerTokenValidator CreateValidator(bool configured = true, StubHandler? handler = null)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Voice:ShopErpBaseUrl"] = configured ? "https://shoperp.test" : null
        }).Build();
        handler ??= new StubHandler(HttpStatusCode.OK, $$"""{"customerId":"{{CustomerId}}"}""");
        return new CustomerTokenValidator(
            new StubFactory(handler), config, NullLogger<CustomerTokenValidator>.Instance);
    }

    [Fact]
    public async Task Customer_token_via_access_token_query_resolves_identity()
    {
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString("?access_token=cust-token-123");

        var identity = await CreateValidator().ValidateAsync(context);

        identity.Should().NotBeNull();
        identity!.UserId.Should().Be(CustomerId);
        identity.Kind.Should().Be(VoiceIdentityKind.Customer);
    }

    [Fact]
    public async Task Customer_token_via_authorization_bearer_resolves_identity()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = "Bearer cust-token-123";

        var identity = await CreateValidator().ValidateAsync(context);

        identity.Should().NotBeNull();
        identity!.UserId.Should().Be(CustomerId);
    }

    [Fact]
    public async Task Dedicated_customerToken_query_still_works()
    {
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString("?customerToken=cust-token-123");

        var identity = await CreateValidator().ValidateAsync(context);

        identity.Should().NotBeNull();
        identity!.UserId.Should().Be(CustomerId);
    }

    [Fact]
    public async Task Missing_ShopErpBaseUrl_returns_null()
    {
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString("?access_token=cust-token-123");

        (await CreateValidator(configured: false).ValidateAsync(context)).Should().BeNull();
    }

    [Fact]
    public async Task No_credential_returns_null()
    {
        (await CreateValidator().ValidateAsync(new DefaultHttpContext())).Should().BeNull();
    }

    [Fact]
    public async Task ShopErp_rejects_token_returns_null()
    {
        var handler = new StubHandler(HttpStatusCode.Unauthorized, """{"error":"invalid"}""");
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString("?access_token=bad-token");

        (await CreateValidator(handler: handler).ValidateAsync(context)).Should().BeNull();
    }
}
