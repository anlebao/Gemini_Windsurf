using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using VanAn.Metering.Infra;
using Xunit;

namespace VanAn.Metering.Tests;

/// <summary>P12 — transaction/get-paging client used by the VA poller fallback.</summary>
public class TingeeVaPollTests
{
    private const string Secret = "test-secret-key-0123456789abcdef0123456789";

    private sealed class StubHandler(HttpStatusCode code, string json) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest;
        public string? LastBody;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            LastRequest = request;
            LastBody = request.Content == null ? null : await request.Content.ReadAsStringAsync(ct);
            return new HttpResponseMessage(code)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        }
    }

    private static TingeePaymentClient NewClient(StubHandler handler) => new(
        new HttpClient(handler) { BaseAddress = new Uri("https://uat-open-api.tingee.vn/") },
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Tingee:ClientId"] = "cid",
            ["Tingee:ClientSecret"] = Secret,
            ["Tingee:BaseUrl"] = "https://uat-open-api.tingee.vn",
            ["Tingee:VaAccountNumber"] = "VA-TEST-001",
            ["Tingee:VaBankBin"] = "970422",
        }).Build(),
        NullLogger<TingeePaymentClient>.Instance);

    [Fact]
    public async Task GetVaTransactions_ParsesCreditItemsWithBillId()
    {
        var json = """
        {"code":"00","data":{"totalCount":2,"items":[
          {"transactionId":"txn-1","code":"FT001","amount":10000,"type":"credit",
           "content":"Nap phi","vaAccountNumber":"VA-TEST-001",
           "transactionDate":"20261119120000","billId":"QRD-ABC"},
          {"transactionId":"txn-2","code":"FT002","amount":5000,"type":"debit",
           "content":"Phi","vaAccountNumber":"VA-TEST-001",
           "transactionDate":"20261119120100","billId":null}
        ]}}
        """;
        var handler = new StubHandler(HttpStatusCode.OK, json);
        var txns = await NewClient(handler).GetVaTransactionsAsync(50);

        txns.Should().HaveCount(2);
        txns[0].BillId.Should().Be("QRD-ABC");
        txns[0].Amount.Should().Be(10000m);
        txns[0].Type.Should().Be("credit");
        txns[1].BillId.Should().BeNull();
        handler.LastBody.Should().Contain("VA-TEST-001");
        handler.LastRequest!.RequestUri!.ToString().Should().Contain("transaction/get-paging");
    }

    [Fact]
    public async Task GetVaTransactions_NonZeroCode_ReturnsEmpty()
    {
        var handler = new StubHandler(HttpStatusCode.OK, """{"code":"99","message":"bad"}""");
        var txns = await NewClient(handler).GetVaTransactionsAsync(50);
        txns.Should().BeEmpty();
    }

    [Fact]
    public async Task GetVaTransactions_TingeeDisabled_ReturnsEmpty()
    {
        var client = new TingeePaymentClient(
            new HttpClient(new StubHandler(HttpStatusCode.OK, "{}")) { BaseAddress = new Uri("https://x/") },
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build(),
            NullLogger<TingeePaymentClient>.Instance);
        (await client.GetVaTransactionsAsync(50)).Should().BeEmpty();
    }
}
