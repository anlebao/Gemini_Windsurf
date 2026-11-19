using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using VanAn.Metering.Contracts;
using VanAn.Metering.Domain;
using VanAn.Metering.Infra;
using Xunit;

namespace VanAn.Metering.Tests;

/// <summary>
/// P8 (task_p8_tingee_topup) — Tingee signature/webhook/pay-link matrix.
/// Webhook tests sign requests with the same HMAC-SHA512 scheme Tingee uses;
/// the pay-link test stubs HttpMessageHandler so no real network call happens.
/// </summary>
public sealed class TingeeTests
{
    private static readonly DateTime Now = new(2026, 11, 18, 12, 0, 0, DateTimeKind.Utc);
    private const string Secret = "test-secret-token";
    private static readonly Guid Tenant = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private static (MeteringDbContext db, WalletService wallet, TopupService topups) NewSut(
        decimal vndPerCredit = 1000, TingeePaymentClient? tingee = null)
    {
        var db = new MeteringDbContext(new DbContextOptionsBuilder<MeteringDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var rates = new RateCardService(db);
        var wallet = new WalletService(db, rates);
        db.TopupConfigs.Add(TopupConfig.Create("970436", "VietinBank", "123", "TEST", "sysadmin", Now, vndPerCredit));
        db.SaveChanges();
        var topups = new TopupService(db, wallet, TimeSpan.FromHours(72), tingee);
        return (db, wallet, topups);
    }

    private static TingeeWebhookService NewWebhook(TopupService topups, string secret = Secret)
        => new(topups, Cfg(secret), NullLogger<TingeeWebhookService>.Instance);

    private static IConfiguration Cfg(string secret = Secret, bool enabled = true) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Tingee:ClientId"] = enabled ? "cid" : null,
            ["Tingee:ClientSecret"] = enabled ? secret : null,
            ["Tingee:BaseUrl"] = "https://uat-open-api.tingee.vn",
        }).Build();

    private static async Task<TopupRequest> SeedTopupAsync(TopupService topups, decimal? amount = 50000)
    {
        var r = await topups.CreateAsync("shoperp-a", $"k-{Guid.NewGuid():N}",
            new TopupCreateRequest(Tenant, amount), Now);
        r.Request.Should().NotBeNull();
        return r.Request!;
    }

    private static async Task<(int Status, object Body)> SignedWebhookAsync(
        TingeeWebhookService wh, string body, string? statusOverride = null)
    {
        var ts = TingeeSignature.TimestampUtc7(Now);
        return await wh.HandleAsync(null, body, ts, TingeeSignature.SignRequest(ts, body, Secret), default);
    }

    // ---------- Signature ----------

    [Fact]
    public void Signature_Roundtrip_And_Tamper()
    {
        var ts = TingeeSignature.TimestampUtc7(Now);
        var body = "{\"orderId\":\"TOPUP-1\"}";
        var sig = TingeeSignature.SignRequest(ts, body, Secret);
        TingeeSignature.VerifySignature(ts, body, sig, Secret).Should().BeTrue();
        TingeeSignature.VerifySignature(ts, body + "x", sig, Secret).Should().BeFalse();
        TingeeSignature.VerifySignature(ts, body, sig, "other-secret").Should().BeFalse();
        TingeeSignature.VerifySignature(ts, body, null, Secret).Should().BeFalse();
    }

    [Fact]
    public void EncryptedToken_IsHmacSha256Hex()
    {
        var token = TingeeSignature.EncryptedToken("plain-123", Secret);
        token.Should().HaveLength(64).And.MatchRegex("^[0-9a-f]+$");
        TingeeSignature.EncryptedToken("plain-123", Secret).Should().Be(token); // deterministic
    }

    // ---------- Webhook ----------

    [Fact]
    public async Task Webhook_UrlValidation_ReturnsEncryptedToken()
    {
        var (_, _, topups) = NewSut();
        var wh = NewWebhook(topups);
        var (status, body) = await wh.HandleAsync("url-validation",
            "{\"plainToken\":\"abc123\"}", null, null);
        status.Should().Be(200);
        var prop = body.GetType().GetProperty("encryptedToken")!;
        prop.GetValue(body).Should().Be(TingeeSignature.EncryptedToken("abc123", Secret));
    }

    [Fact]
    public async Task Webhook_BadSignature_401()
    {
        var (_, _, topups) = NewSut();
        var wh = NewWebhook(topups);
        var (status, _) = await wh.HandleAsync(null, "{}", "20261118120000000", "deadbeef", default);
        status.Should().Be(401);
    }

    [Fact]
    public async Task Webhook_Success_ConfirmsPaidAmount_CreditsByRate()
    {
        var (db, wallet, topups) = NewSut(vndPerCredit: 1000);
        var t = await SeedTopupAsync(topups, amount: 50000);
        var wh = NewWebhook(topups);

        var body = $$"""{"orderId":"{{t.ReferenceCode}}","status":"success","statusCode":"00","paidAmount":50000,"amount":50000,"providerTransactionCode":"TXN1"}""";
        var (status, _) = await SignedWebhookAsync(wh, body);

        status.Should().Be(200);
        var tracked = await db.TopupRequests.FindAsync(t.Id);
        tracked!.Status.Should().Be(TopupStatus.Confirmed);
        tracked.CreditsGranted.Should().Be(50m);            // 50000 / 1000
        tracked.PaidAmountVnd.Should().Be(50000m);
        tracked.ProviderTransactionId.Should().Be("TXN1");
        tracked.ConfirmedBy.Should().Be("tingee:webhook");
        (await wallet.GetWalletAsync(Tenant)).Balance.Should().Be(50m);
    }

    [Fact]
    public async Task Webhook_PartialPayment_GrantsPaidAmountOnly()
    {
        var (db, wallet, topups) = NewSut(vndPerCredit: 1000);
        var t = await SeedTopupAsync(topups, amount: 50000);
        var wh = NewWebhook(topups);

        var body = $$"""{"orderId":"{{t.ReferenceCode}}","status":"success","statusCode":"00","paidAmount":30000,"amount":50000}""";
        var (status, _) = await SignedWebhookAsync(wh, body);

        status.Should().Be(200);
        var tracked = await db.TopupRequests.FindAsync(t.Id);
        tracked!.CreditsGranted.Should().Be(30m);            // user chốt: confirm theo paidAmount thực trả
        (await wallet.GetWalletAsync(Tenant)).Balance.Should().Be(30m);
    }

    [Fact]
    public async Task Webhook_Replay_NoDoubleGrant()
    {
        var (db, _, topups) = NewSut(vndPerCredit: 1000);
        var t = await SeedTopupAsync(topups, 50000);
        var wh = NewWebhook(topups);
        var body = $$"""{"orderId":"{{t.ReferenceCode}}","status":"success","statusCode":"00","paidAmount":50000,"amount":50000}""";
        var (s1, _) = await SignedWebhookAsync(wh, body);
        var (s2, _) = await SignedWebhookAsync(wh, body);
        s1.Should().Be(200);
        s2.Should().Be(200);
        db.CreditLedgerEntries.Count(e => e.TopupRequestId == t.Id).Should().Be(1);
    }

    [Fact]
    public async Task Webhook_ExpiredStatus_Ignored_StaysPending()
    {
        var (db, _, topups) = NewSut();
        var t = await SeedTopupAsync(topups, 50000);
        var wh = NewWebhook(topups);
        var body = $$"""{"orderId":"{{t.ReferenceCode}}","status":"expired","statusCode":"01","paidAmount":0,"amount":50000}""";
        var (status, _) = await SignedWebhookAsync(wh, body);
        status.Should().Be(200);
        (await db.TopupRequests.FindAsync(t.Id))!.Status.Should().Be(TopupStatus.Pending);
    }

    [Fact]
    public async Task Webhook_VndPerCreditZero_RecordsPaid_StaysPending()
    {
        var (db, _, topups) = NewSut(vndPerCredit: 0);
        var t = await SeedTopupAsync(topups, 50000);
        var wh = NewWebhook(topups);
        var body = $$"""{"orderId":"{{t.ReferenceCode}}","status":"success","statusCode":"00","paidAmount":50000,"amount":50000}""";
        await SignedWebhookAsync(wh, body);
        var tracked = await db.TopupRequests.FindAsync(t.Id);
        tracked!.Status.Should().Be(TopupStatus.Pending);   // auto-confirm TẮT — admin confirm tay
        tracked.PaidAmountVnd.Should().Be(50000m);
        tracked.PaymentProvider.Should().Be("tingee");
    }

    [Fact]
    public async Task Webhook_UnknownReference_404()
    {
        var (_, _, topups) = NewSut();
        var wh = NewWebhook(topups);
        var body = """{"orderId":"TOPUP-NOPE99","status":"success","statusCode":"00","paidAmount":1000,"amount":1000}""";
        var (status, _) = await SignedWebhookAsync(wh, body);
        status.Should().Be(404);
    }

    // ---------- CreatePayLinkAsync ----------

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

    private static TingeePaymentClient NewClient(StubHandler handler, bool enabled = true)
        => new(new HttpClient(handler) { BaseAddress = new Uri("https://uat-open-api.tingee.vn/") },
            Cfg(Secret, enabled), NullLogger<TingeePaymentClient>.Instance);

    [Fact]
    public async Task PayLink_Success_ReturnsCheckoutUrl_AndSignsRequest()
    {
        var handler = new StubHandler(HttpStatusCode.OK,
            """{"code":"00","message":"Success","data":"https://sandbox.tingee.vn/checkout/abc"}""");
        var client = NewClient(handler);
        var (_, _, topups) = NewSut(tingee: client);
        var t = await SeedTopupAsync(topups, 50000);

        var r = await topups.CreatePayLinkAsync(t.Id, Now);

        r.CheckoutUrl.Should().Be("https://sandbox.tingee.vn/checkout/abc");
        r.OrderId.Should().Be(t.ReferenceCode);
        // Request was signed per Tingee spec: headers + HMAC over "ts:body".
        handler.LastRequest.Should().NotBeNull();
        var sig = handler.LastRequest!.Headers.GetValues("x-signature").Single();
        var ts = handler.LastRequest.Headers.GetValues("x-request-timestamp").Single();
        TingeeSignature.VerifySignature(ts, handler.LastBody!, sig, Secret).Should().BeTrue();
        handler.LastBody.Should().Contain(t.ReferenceCode); // orderId = ReferenceCode
        // Regression: Tingee verifies signature over decoded JSON — body must keep
        // UTF-8 literal, never \uXXXX escapes (code 97 Invalid signature, prod 2026-11-19).
        handler.LastBody.Should().Contain("Nạp phí nền tảng");
        handler.LastBody.Should().NotContain("\\u");
    }

    [Fact]
    public async Task PayLink_DecimalAmount_SerializesCanonicalInteger()
    {
        // Regression (prod 2026-11-19): Tingee canonicalizes the JSON body before
        // verifying x-signature - "amount":50000.0 fails with code 97 while
        // "amount":50000 passes. .NET decimal keeps scale, so 50000.0m must be
        // written without the decimal point.
        var handler = new StubHandler(HttpStatusCode.OK,
            """{"code":"00","message":"Success","data":"https://payment-gateway.tingee.vn/checkout/x"}""");
        var client = NewClient(handler);
        var (_, _, topups) = NewSut(tingee: client);
        var t = await SeedTopupAsync(topups, 50000.0m); // scale-1 decimal

        var r = await topups.CreatePayLinkAsync(t.Id, Now);

        r.CheckoutUrl.Should().NotBeNull();
        handler.LastBody.Should().Contain("\"amount\":50000");
        handler.LastBody.Should().NotContain("50000.0");
    }

    [Fact]
    public async Task PayLink_TopupWithoutAmount_Fails()
    {
        var client = NewClient(new StubHandler(HttpStatusCode.OK, "{}"));
        var (_, _, topups) = NewSut(tingee: client);
        var t = await SeedTopupAsync(topups, amount: null);

        var r = await topups.CreatePayLinkAsync(t.Id, Now);

        r.CheckoutUrl.Should().BeNull();
        r.ErrorCode.Should().Be("amount_required");
    }

    [Fact]
    public async Task PayLink_TopupNotPending_Fails()
    {
        var client = NewClient(new StubHandler(HttpStatusCode.OK, "{}"));
        var (_, _, topups) = NewSut(tingee: client);
        var t = await SeedTopupAsync(topups, 50000);
        await topups.ConfirmAsync(t.Id, new TopupConfirmRequest(10m), "sysadmin", Now);

        var r = await topups.CreatePayLinkAsync(t.Id, Now);

        r.CheckoutUrl.Should().BeNull();
        r.ErrorCode.Should().Be("topup_not_pending");
    }

    [Fact]
    public async Task PayLink_TingeeDisabled_Fails()
    {
        var client = NewClient(new StubHandler(HttpStatusCode.OK, "{}"), enabled: false);
        var (_, _, topups) = NewSut(tingee: client);
        var t = await SeedTopupAsync(topups, 50000);

        var r = await topups.CreatePayLinkAsync(t.Id, Now);

        r.CheckoutUrl.Should().BeNull();
        r.ErrorCode.Should().Be("tingee_disabled");
    }

    [Fact]
    public async Task PayLink_TopupNotFound_Fails()
    {
        var client = NewClient(new StubHandler(HttpStatusCode.OK, "{}"));
        var (_, _, topups) = NewSut(tingee: client);
        var r = await topups.CreatePayLinkAsync(Guid.NewGuid(), Now);
        r.CheckoutUrl.Should().BeNull();
        r.ErrorCode.Should().Be("not_found");
    }
}