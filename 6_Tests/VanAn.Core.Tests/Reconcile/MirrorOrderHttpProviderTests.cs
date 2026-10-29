using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using VanAn.CoreHub.Services.Reconcile;
using Xunit;

namespace VanAn.Core.Tests.Reconcile;

/// <summary>
/// P2 — MirrorOrderHttpProvider (Gateway → ShopERP mirror API).
/// Fail-open bắt buộc: HTTP lỗi / transport throw / JSON hỏng → null (R11 skip Info).
/// </summary>
public class MirrorOrderHttpProviderTests
{
    private const string ValidJson = """
        [{"orderId":"11111111-1111-1111-1111-111111111111","sourceType":"booking","status":"confirmed","paymentStatus":"Paid","depositPaidAmount":10000,"totalAmount":110000}]
        """;

    private sealed class StubHandler(HttpResponseMessage? response = null, Exception? toThrow = null) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (toThrow is not null) throw toThrow;
            return Task.FromResult(response ?? new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]", Encoding.UTF8, "application/json")
            });
        }
    }

    private static MirrorOrderHttpProvider Build(HttpMessageHandler handler)
        => new(new HttpClient(handler) { BaseAddress = new Uri("http://shoperp/") },
            NullLogger<MirrorOrderHttpProvider>.Instance);

    private static HttpResponseMessage Json(string body, HttpStatusCode code = HttpStatusCode.OK)
        => new(code) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task Ok_Data_ReturnsList()
    {
        var p = Build(new StubHandler(Json(ValidJson)));

        var r = await p.GetMirrorOrdersAsync("2026-10");

        r.Should().NotBeNull();
        r!.Should().HaveCount(1);
        r[0].OrderId.Should().Be(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        r[0].SourceType.Should().Be("booking");
        r[0].PaymentStatus.Should().Be("Paid");
        r[0].DepositPaidAmount.Should().Be(10_000m);
    }

    [Fact]
    public async Task HttpErrorStatus_ReturnsNull()
    {
        var p = Build(new StubHandler(new HttpResponseMessage(HttpStatusCode.InternalServerError)));

        var r = await p.GetMirrorOrdersAsync("2026-10");

        r.Should().BeNull("non-2xx → fail-open null (R11 skip)");
    }

    [Fact]
    public async Task TransportException_ReturnsNull()
    {
        var p = Build(new StubHandler(toThrow: new HttpRequestException("network boom")));

        var r = await p.GetMirrorOrdersAsync("2026-10");

        r.Should().BeNull("transport exception → fail-open null");
    }

    [Fact]
    public async Task BadJson_ReturnsNull()
    {
        var p = Build(new StubHandler(Json("not-json{{{")));

        var r = await p.GetMirrorOrdersAsync("2026-10");

        r.Should().BeNull("malformed JSON → fail-open null");
    }
}
