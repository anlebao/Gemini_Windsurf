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
/// P9 (task_p9_dynamic_qr) — Tingee Dynamic QR: generate-dynamic-qr request signing,
/// billId persistence, and Payment Callback webhook (transactionCode/type/vaAccountNumber/
/// additionalData[].billId) confirmation incl. partial payment + idempotent replay.
/// </summary>
public sealed class TingeeQrTests
{
    private static readonly DateTime Now = new(2026, 11, 19, 12, 0, 0, DateTimeKind.Utc);
    private const string Secret = "test-secret-token";
    private const string VaAccount = "VA-TEST-001";
    private const string VaBin = "970422";
    private static readonly Guid Tenant = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    private static (MeteringDbContext db, WalletService wallet, TopupService topups) NewSut(
        decimal vndPerCredit = 1000, TingeePaymentClient? tingee = null)
    {
        var db = new MeteringDbContext(new DbContextOptionsBuilder<MeteringDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var rates = new RateCardService(db);
        var wallet = new WalletService(db, rates);
        db.TopupConfigs.Add(TopupConfig.Create("970422", "MB Bank", "123", "TEST", "sysadmin", Now, vndPerCredit));
        db.SaveChanges();
        var topups = new TopupService(db, wallet, TimeSpan.FromHours(72), tingee);
        return (db, wallet, topups);
    }

    private static TingeeWebhookService NewWebhook(TopupService topups)
        => new(topups, Cfg(), NullLogger<TingeeWebhookService>.Instance);

    private static IConfiguration Cfg(bool enabled = true,
        string? vaAccount = VaAccount, string? vaBin = VaBin) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Tingee:ClientId"] = enabled ? "cid" : null,
            ["Tingee:ClientSecret"] = enabled ? Secret : null,
            ["Tingee:BaseUrl"] = "https://uat-open-api.tingee.vn",
            ["Tingee:VaAccountNumber"] = vaAccount,
            ["Tingee:VaBankBin"] = vaBin,
        }).Build();

    private static async Task<TopupRequest> SeedTopupAsync(TopupService topups, decimal? amount = 50000)
    {
        var r = await topups.CreateAsync("shoperp-a", $"k-{Guid.NewGuid():N}",
            new TopupCreateRequest(Tenant, amount), Now);
        r.Request.Should().NotBeNull();
        return r.Request!;
    }

    private static async Task<(int Status, object Body)> SignedWebhookAsync(
        TingeeWebhookService wh, string body)
    {
        var ts = TingeeSignature.TimestampUtc7(Now);
        return await wh.HandleAsync(null, body, ts, TingeeSignature.SignRequest(ts, body, Secret), default);
    }

    // ---------- stubs ----------

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

    private static TingeePaymentClient NewClient(StubHandler handler, bool enabled = true,
        string? vaAccount = VaAccount, string? vaBin = VaBin)
        => new(new HttpClient(handler) { BaseAddress = new Uri("https://uat-open-api.tingee.vn/") },
            Cfg(enabled, vaAccount, vaBin), NullLogger<TingeePaymentClient>.Instance);

    private static StubHandler QrOkHandler(string billId = "QRD-TEST-1", string qrPayload = "00020101021238")
        => new(HttpStatusCode.OK,
            "{\"code\":\"00\",\"message\":\"Success\",\"data\":{\"qrCode\":\"" + qrPayload
            + "\",\"qrAccount\":\"" + VaAccount + "\",\"billId\":\"" + billId + "\"}}");

    /// <summary>Seed a top-up and mint a dynamic QR for it (billId stored on the request).</summary>
    private static async Task<(TopupRequest Topup, string BillId)> SeedQrTopupAsync(
        TopupService topups, decimal amount = 50000, string billId = "QRD-TEST-1")
    {
        var t = await SeedTopupAsync(topups, amount);
        var qr = await topups.CreateQrAsync(t.Id, Now);
        qr.QrPayload.Should().NotBeNull();
        qr.BillId.Should().Be(billId);
        return (t, billId);
    }

    private static string QrCallbackBody(string? billId, string? content, decimal amount,
        string type = "credit", string txnCode = "FT123")
    {
        var additional = billId == null ? "[]" : "[{\"billId\":\"" + billId + "\"}]";
        var contentJson = content == null ? "null" : "\"" + content + "\"";
        return "{\"transactionCode\":\"" + txnCode + "\",\"amount\":" + amount
            + ",\"content\":" + contentJson + ",\"bank\":\"MB\",\"accountNumber\":\"" + VaAccount
            + "\",\"vaAccountNumber\":\"" + VaAccount + "\",\"transactionDate\":\"2026-11-19T10:00:00+07:00\""
            + ",\"type\":\"" + type + "\",\"additionalData\":" + additional + "}";
    }

    // ---------- generate-dynamic-qr ----------

    [Fact]
    public async Task Qr_Success_ReturnsPayload_StoresBillId()
    {
        var client = NewClient(QrOkHandler());
        var (db, _, topups) = NewSut(tingee: client);
        var t = await SeedTopupAsync(topups, 50000);

        var r = await topups.CreateQrAsync(t.Id, Now);

        r.QrPayload.Should().Be("00020101021238");
        r.BillId.Should().Be("QRD-TEST-1");
        r.QrAccount.Should().Be(VaAccount);
        (await db.TopupRequests.FindAsync(t.Id))!.QrBillId.Should().Be("QRD-TEST-1");
    }

    [Fact]
    public async Task Qr_Request_Signed_CanonicalAmount_AndVaFields()
    {
        var handler = QrOkHandler();
        var client = NewClient(handler);
        var (_, _, topups) = NewSut(tingee: client);
        var t = await SeedTopupAsync(topups, 50000.0m); // scale-1 decimal — must serialize canonical

        var r = await topups.CreateQrAsync(t.Id, Now);

        r.QrPayload.Should().NotBeNull();
        handler.LastRequest.Should().NotBeNull();
        handler.LastRequest!.RequestUri!.ToString().Should().EndWith("v1/generate-dynamic-qr");
        var sig = handler.LastRequest.Headers.GetValues("x-signature").Single();
        var ts = handler.LastRequest.Headers.GetValues("x-request-timestamp").Single();
        TingeeSignature.VerifySignature(ts, handler.LastBody!, sig, Secret).Should().BeTrue();
        handler.LastBody.Should().Contain("\"vaAccountNumber\":\"" + VaAccount + "\"");
        handler.LastBody.Should().Contain("\"bankBin\":\"" + VaBin + "\"");
        handler.LastBody.Should().Contain("\"amount\":50000");
        handler.LastBody.Should().NotContain("50000.0");
    }

    [Fact]
    public async Task Qr_TopupNotPending_Fails()
    {
        var client = NewClient(QrOkHandler());
        var (_, _, topups) = NewSut(tingee: client);
        var t = await SeedTopupAsync(topups, 50000);
        await topups.ConfirmAsync(t.Id, new TopupConfirmRequest(10m), "sysadmin", Now);

        var r = await topups.CreateQrAsync(t.Id, Now);
        r.QrPayload.Should().BeNull();
        r.ErrorCode.Should().Be("topup_not_pending");
    }

    [Fact]
    public async Task Qr_TopupNotFound_Fails()
    {
        var client = NewClient(QrOkHandler());
        var (_, _, topups) = NewSut(tingee: client);
        var r = await topups.CreateQrAsync(Guid.NewGuid(), Now);
        r.QrPayload.Should().BeNull();
        r.ErrorCode.Should().Be("not_found");
    }

    [Fact]
    public async Task Qr_TopupWithoutAmount_Fails()
    {
        var client = NewClient(QrOkHandler());
        var (_, _, topups) = NewSut(tingee: client);
        var t = await SeedTopupAsync(topups, amount: null);

        var r = await topups.CreateQrAsync(t.Id, Now);
        r.QrPayload.Should().BeNull();
        r.ErrorCode.Should().Be("amount_required");
    }

    [Fact]
    public async Task Qr_TingeeDisabled_Fails()
    {
        var client = NewClient(QrOkHandler(), enabled: false);
        var (_, _, topups) = NewSut(tingee: client);
        var t = await SeedTopupAsync(topups, 50000);

        var r = await topups.CreateQrAsync(t.Id, Now);
        r.QrPayload.Should().BeNull();
        r.ErrorCode.Should().Be("tingee_disabled");
    }

    [Fact]
    public async Task Qr_NoVaConfig_Fails()
    {
        var client = NewClient(QrOkHandler(), vaAccount: null, vaBin: null);
        var (_, _, topups) = NewSut(tingee: client);
        var t = await SeedTopupAsync(topups, 50000);

        var r = await topups.CreateQrAsync(t.Id, Now);
        r.QrPayload.Should().BeNull();
        r.ErrorCode.Should().Be("qr_disabled");
    }

    // ---------- Payment Callback webhook (QR/VA transaction) ----------

    [Fact]
    public async Task Webhook_QrPayment_ByBillId_Confirms()
    {
        var client = NewClient(QrOkHandler("QRD-TEST-1"));
        var (db, wallet, topups) = NewSut(vndPerCredit: 1000, tingee: client);
        var (t, _) = await SeedQrTopupAsync(topups, 50000, "QRD-TEST-1");
        var wh = NewWebhook(topups);

        var body = QrCallbackBody("QRD-TEST-1", "CK", 50000);
        var (status, _) = await SignedWebhookAsync(wh, body);

        status.Should().Be(200);
        var tracked = await db.TopupRequests.FindAsync(t.Id);
        tracked!.Status.Should().Be(TopupStatus.Confirmed);
        tracked.CreditsGranted.Should().Be(50m);
        tracked.PaidAmountVnd.Should().Be(50000m);
        tracked.ProviderTransactionId.Should().Be("FT123");
        (await wallet.GetWalletAsync(Tenant)).Balance.Should().Be(50m);
    }

    [Fact]
    public async Task Webhook_QrPayment_Partial_UsesActualAmount()
    {
        var client = NewClient(QrOkHandler("QRD-TEST-2"));
        var (db, wallet, topups) = NewSut(vndPerCredit: 1000, tingee: client);
        var (t, _) = await SeedQrTopupAsync(topups, 50000, "QRD-TEST-2");
        var wh = NewWebhook(topups);

        var body = QrCallbackBody("QRD-TEST-2", "CK", 30000);
        var (status, _) = await SignedWebhookAsync(wh, body);

        status.Should().Be(200);
        var tracked = await db.TopupRequests.FindAsync(t.Id);
        tracked!.CreditsGranted.Should().Be(30m); // user chốt: theo amount thực nhận
        (await wallet.GetWalletAsync(Tenant)).Balance.Should().Be(30m);
    }

    [Fact]
    public async Task Webhook_QrPayment_ByContentRef_Fallback()
    {
        var client = NewClient(QrOkHandler("QRD-TEST-3"));
        var (db, _, topups) = NewSut(vndPerCredit: 1000, tingee: client);
        var (t, _) = await SeedQrTopupAsync(topups, 50000, "QRD-TEST-3");
        var wh = NewWebhook(topups);

        // No billId in additionalData — fall back to TOPUP-XXX inside transfer content.
        var body = QrCallbackBody(null, "NAP TIEN " + t.ReferenceCode, 50000);
        var (status, _) = await SignedWebhookAsync(wh, body);

        status.Should().Be(200);
        (await db.TopupRequests.FindAsync(t.Id))!.Status.Should().Be(TopupStatus.Confirmed);
    }

    [Fact]
    public async Task Webhook_QrPayment_Debit_Ignored()
    {
        var client = NewClient(QrOkHandler("QRD-TEST-4"));
        var (db, _, topups) = NewSut(vndPerCredit: 1000, tingee: client);
        var (t, _) = await SeedQrTopupAsync(topups, 50000, "QRD-TEST-4");
        var wh = NewWebhook(topups);

        var body = QrCallbackBody("QRD-TEST-4", "CK", 50000, type: "debit");
        var (status, _) = await SignedWebhookAsync(wh, body);

        status.Should().Be(200);
        (await db.TopupRequests.FindAsync(t.Id))!.Status.Should().Be(TopupStatus.Pending);
    }

    [Fact]
    public async Task Webhook_QrPayment_Replay_NoDoubleGrant()
    {
        var client = NewClient(QrOkHandler("QRD-TEST-5"));
        var (db, _, topups) = NewSut(vndPerCredit: 1000, tingee: client);
        var (t, _) = await SeedQrTopupAsync(topups, 50000, "QRD-TEST-5");
        var wh = NewWebhook(topups);
        var body = QrCallbackBody("QRD-TEST-5", "CK", 50000);

        var (s1, _) = await SignedWebhookAsync(wh, body);
        var (s2, _) = await SignedWebhookAsync(wh, body);

        s1.Should().Be(200);
        s2.Should().Be(200);
        db.CreditLedgerEntries.Count(e => e.TopupRequestId == t.Id).Should().Be(1);
    }

    [Fact]
    public async Task Webhook_QrPayment_UnknownBillId_404()
    {
        var (_, _, topups) = NewSut(tingee: NewClient(QrOkHandler()));
        var wh = NewWebhook(topups);

        var body = QrCallbackBody("QRD-UNKNOWN", "CK linh tinh", 50000);
        var (status, _) = await SignedWebhookAsync(wh, body);

        status.Should().Be(404);
    }
}
