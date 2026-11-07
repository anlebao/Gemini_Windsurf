using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using VanAn.KhachLink.Services.Http;
using Xunit;

namespace VanAn.Core.Tests.KhachLink;

/// <summary>
/// SF-W2 (A2): TenantAiPublicHttpService.SearchAiAsync — GET /api/public/tenant-ai/search
/// mapping (query escaped + tenantId query param) + fail→null contract (NFR-3).
/// </summary>
public class TenantAiPublicHttpServiceTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return send(request);
        }
    }

    private sealed class FakeFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private static TenantAiPublicHttpService NewService(HttpMessageHandler handler)
        => new(new FakeFactory(new HttpClient(handler) { BaseAddress = new Uri("https://gateway.test/") }),
            NullLogger<TenantAiPublicHttpService>.Instance);

    private static HttpResponseMessage OkJson(string body)
        => new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private const string SearchOkJson =
        "{\"answer\":\"Quán có phở chay 80k.\",\"products\":[{\"productId\":\"11111111-1111-1111-1111-111111111111\",\"name\":\"Phở chay\",\"price\":80000,\"imageUrl\":null}],\"sourceIds\":[],\"confidence\":0.9,\"refused\":false,\"refuseReason\":null,\"staleData\":false,\"degraded\":false}";

    [Fact]
    public async Task SearchAiAsync_GetsSearchEndpoint_AndMapsResult()
    {
        var handler = new StubHandler(req =>
        {
            req.Method.Should().Be(HttpMethod.Get);
            req.RequestUri!.AbsolutePath.Should().Be("/api/public/tenant-ai/search");
            req.RequestUri.Query.Should().Contain("query=ph%E1%BB%9F%20chay")
                .And.Contain($"tenantId={TenantId}");
            return Task.FromResult(OkJson(SearchOkJson));
        });

        var result = await NewService(handler).SearchAiAsync(TenantId, "phở chay");

        result.Should().NotBeNull();
        result!.Answer.Should().Be("Quán có phở chay 80k.");
        result.Products.Should().HaveCount(1);
        result.Products[0].Name.Should().Be("Phở chay");
        result.Products[0].Price.Should().Be(80_000m);
    }

    [Fact]
    public async Task SearchAiAsync_NonSuccess_ReturnsNull()
    {
        var handler = new StubHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));

        var result = await NewService(handler).SearchAiAsync(TenantId, "phở");

        result.Should().BeNull("fail-open: upstream error → null → host hides results");
    }

    [Fact]
    public async Task SearchAiAsync_TransportError_ReturnsNull()
    {
        var handler = new StubHandler(_ => throw new HttpRequestException("gateway down"));

        var result = await NewService(handler).SearchAiAsync(TenantId, "phở");

        result.Should().BeNull();
    }
}
