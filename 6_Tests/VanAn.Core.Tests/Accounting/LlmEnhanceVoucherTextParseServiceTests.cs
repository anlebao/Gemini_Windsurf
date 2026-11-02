using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;
using VanAn.Shared.Services;
using Xunit;

namespace VanAn.Core.Tests.Accounting;

/// <summary>Task G2 — LLM hybrid decorator: trigger hẹp, schema validate, merge, fallback an toàn.</summary>
public class LlmEnhanceVoucherTextParseServiceTests
{
    private static readonly TenantId Tenant = new(Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"));

    private sealed class StubHandler : HttpMessageHandler
    {
        public int Calls;
        public Func<HttpRequestMessage, HttpResponseMessage> Responder = _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json"),
        };

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(Responder(request));
        }
    }

    private static string GeminiJson(string inner) =>
        $"{{\"candidates\":[{{\"content\":{{\"parts\":[{{\"text\":{System.Text.Json.JsonSerializer.Serialize(inner)}}}]}}}}]}}";

    private static (LlmEnhanceVoucherTextParseService Svc, StubHandler Handler) Build(
        bool flagEnabled = true, string apiKey = "test-key", string? payload = null,
        HttpStatusCode status = HttpStatusCode.OK)
    {
        var handler = new StubHandler
        {
            Responder = _ => new HttpResponseMessage(status)
            {
                Content = new StringContent(payload ?? "{}", Encoding.UTF8, "application/json"),
            },
        };
        var httpFactory = new Mock<IHttpClientFactory>();
        httpFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new HttpClient(handler));
        var feature = new Mock<IShopFeatureSettingsService>();
        feature.Setup(f => f.IsEnabledAsync(It.IsAny<Guid>(), "SmartEntryLlmEnabled", It.IsAny<CancellationToken>()))
            .ReturnsAsync(flagEnabled);
        var opts = Options.Create(new SmartEntryLlmOptions { ApiKey = apiKey });
        var svc = new LlmEnhanceVoucherTextParseService(
            new VoucherTextParseService(bankAccounts: null), httpFactory.Object, opts, feature.Object,
            NullLogger<LlmEnhanceVoucherTextParseService>.Instance);
        return (svc, handler);
    }

    private static Task<ParsedVoucher> Parse(string text, bool flagEnabled = true, string apiKey = "test-key",
        string? payload = null, HttpStatusCode status = HttpStatusCode.OK)
    {
        var (svc, _) = Build(flagEnabled, apiKey, payload, status);
        return svc.ParseAsync(Tenant, text);
    }

    [Fact]
    public async Task FlagOff_NoApiCall_ReturnsRuleBased()
    {
        var (svc, handler) = Build(flagEnabled: false);
        var r = await svc.ParseAsync(Tenant, "khách trả tiền 5 triệu");
        Assert.Equal(VoucherIntent.ReceivablePayment, r.Intent);
        Assert.Contains("đối tượng", r.MissingFields); // chưa được lấp
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task ApiKeyEmpty_NoApiCall()
    {
        var (svc, handler) = Build(apiKey: "");
        await svc.ParseAsync(Tenant, "khách trả tiền 5 triệu");
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task HighConfidence_NoApiCall()
    {
        var (svc, handler) = Build();
        var r = await svc.ParseAsync(Tenant, "thu tiền bán hàng 2 triệu tiền mặt");
        Assert.Equal(VoucherIntent.Revenue, r.Intent);
        Assert.Equal("High", r.Confidence);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task ForceLlm_EvenWhenHigh_CallsApi()
    {
        // Nút "AI phân tích": câu rule-based đã High vẫn gọi LLM khi forceLlm=true.
        var payload = GeminiJson("{\"intent\":\"Revenue\",\"amount\":2000000,\"doiTuong\":null,\"date\":null,\"category\":null}");
        var (svc, handler) = Build(payload: payload);
        var r = await svc.ParseAsync(Tenant, "thu tiền bán hàng 2 triệu tiền mặt", forceLlm: true);
        Assert.Equal(VoucherIntent.Revenue, r.Intent);
        Assert.Equal(2_000_000m, r.Amount); // rule-based giữ nguyên
        Assert.Equal(1, handler.Calls);
        Assert.Contains(r.Warnings, w => w.Contains("Đề xuất từ AI"));
    }

    [Fact]
    public async Task ForceLlm_FlagOff_NoApiCall()
    {
        var (svc, handler) = Build(flagEnabled: false);
        var r = await svc.ParseAsync(Tenant, "thu tiền bán hàng 2 triệu tiền mặt", forceLlm: true);
        Assert.Equal(0, handler.Calls);
        Assert.DoesNotContain(r.Warnings, w => w.Contains("Đề xuất từ AI"));
    }

    [Fact]
    public async Task MissingDoiTuong_MergedFromLlm()
    {
        var payload = GeminiJson("{\"intent\":\"ReceivablePayment\",\"amount\":5000000,\"doiTuong\":\"anh Hùng\",\"date\":null,\"category\":null}");
        var (svc, handler) = Build(payload: payload);
        var r = await svc.ParseAsync(Tenant, "khách trả tiền 5 triệu");
        Assert.Equal("anh Hùng", r.DoiTuong);
        Assert.Equal(5_000_000m, r.Amount);
        Assert.Equal("High", r.Confidence);
        Assert.Contains(r.Warnings, w => w.Contains("Đề xuất từ AI"));
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task UnknownIntent_LlmFillsIntent()
    {
        // "mua hàng 30 triệu" → base Unknown (thiếu intent) → LLM điền Purchase.
        var payload = GeminiJson("{\"intent\":\"Purchase\",\"amount\":30000000,\"doiTuong\":null,\"date\":null,\"category\":null}");
        var (svc, handler) = Build(payload: payload);
        var r = await svc.ParseAsync(Tenant, "mua hàng 30 triệu");
        Assert.Equal(VoucherIntent.Purchase, r.Intent);
        Assert.Equal(30_000_000m, r.Amount); // amount rule-based giữ nguyên
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task RuleBasedHigh_NotOverriddenByLlm()
    {
        // Rule-based đã High (doanh thu + amount đủ) → không gọi LLM, giữ nguyên.
        var payload = GeminiJson("{\"intent\":\"Expense\",\"amount\":999999,\"doiTuong\":null,\"date\":null}");
        var (svc, handler) = Build(payload: payload);
        var r = await svc.ParseAsync(Tenant, "thu doanh thu 15 triệu hôm qua chuyển khoản");
        Assert.Equal(VoucherIntent.Revenue, r.Intent);
        Assert.Equal(15_000_000m, r.Amount);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task BadJson_FallsBackToRuleBased_WithWarning()
    {
        var r = await Parse("khách trả tiền 5 triệu", payload: "not-json{{{");
        Assert.Equal(VoucherIntent.ReceivablePayment, r.Intent);
        Assert.Contains("đối tượng", r.MissingFields);
        Assert.Contains(r.Warnings, w => w.Contains("LLM tạm lỗi"));
    }

    [Fact]
    public async Task ApiError_FallsBackToRuleBased_WithWarning()
    {
        var r = await Parse("khách trả tiền 5 triệu", payload: "{}", status: HttpStatusCode.InternalServerError);
        Assert.Equal(VoucherIntent.ReceivablePayment, r.Intent);
        Assert.Contains(r.Warnings, w => w.Contains("LLM tạm lỗi"));
    }

    [Fact]
    public async Task InvalidValues_Rejected()
    {
        // amount âm + category lạ + intent không tồn tại → không merge field nào.
        var payload = GeminiJson("{\"intent\":\"BOGUS\",\"amount\":-5,\"category\":\"xyz\",\"date\":null,\"doiTuong\":null}");
        var r = await Parse("khách trả tiền 5 triệu", payload: payload);
        Assert.Equal(VoucherIntent.ReceivablePayment, r.Intent); // intent giữ rule-based
        Assert.Contains("đối tượng", r.MissingFields);           // doiTuong LLM null → vẫn thiếu
    }

    [Fact]
    public async Task FutureDate_Rejected()
    {
        // Base Unknown ("chuyển tiền 2 triệu" thiếu intent) → LLM đề xuất ngày tương lai → bị chặn.
        var future = DateTime.Today.AddDays(5).ToString("yyyy-MM-dd");
        var payload = GeminiJson($"{{\"intent\":\"Expense\",\"amount\":2000000,\"date\":\"{future}\",\"doiTuong\":null}}");
        var r = await Parse("chuyển tiền 2 triệu", payload: payload);
        Assert.Equal(DateTime.Today, r.Date);
    }

    [Fact]
    public async Task RecentPastDate_Accepted()
    {
        var past = DateTime.Today.AddDays(-2).ToString("yyyy-MM-dd");
        var payload = GeminiJson($"{{\"intent\":\"Expense\",\"amount\":2000000,\"date\":\"{past}\",\"doiTuong\":null}}");
        var r = await Parse("chuyển tiền 2 triệu", payload: payload);
        Assert.Equal(DateTime.Today.AddDays(-2), r.Date);
    }

    [Fact]
    public async Task TransferDirection_Validated()
    {
        // LLM trả hướng sai (In với 112→111) → không merge account.
        var payload = GeminiJson("{\"intent\":\"TransferIn\",\"amount\":50000000,\"fromAccountCode\":\"112\",\"toAccountCode\":\"111\",\"date\":null,\"doiTuong\":null}");
        var (svc, handler) = Build(payload: payload);
        var r = await svc.ParseAsync(Tenant, "chuyển tiền 2 triệu");
        Assert.Equal(VoucherIntent.TransferIn, r.Intent);
        Assert.Null(r.FromAccountCode); // hướng LLM sai (112→111 là Out) → không merge
        Assert.Null(r.ToAccountCode);
        Assert.Equal(1, handler.Calls);
    }

    // ---- Task G4: multi-voucher (1 câu → 2-3 phiếu) ----

    private static string GeminiMultiJson(string inner) =>
        $"{{\"candidates\":[{{\"content\":{{\"parts\":[{{\"text\":{System.Text.Json.JsonSerializer.Serialize(inner)}}}]}}}}]}}";

    [Fact]
    public async Task Multi_TwoVouchers_FromLlm()
    {
        var payload = GeminiMultiJson("{\"vouchers\":[" +
            "{\"intent\":\"Revenue\",\"amount\":5000000,\"date\":null,\"paymentMethod\":\"cash\",\"doiTuong\":null,\"category\":null,\"fromAccountCode\":null,\"toAccountCode\":null,\"feeAmount\":null,\"description\":\"thu tiền bán hàng 5 triệu\"}," +
            "{\"intent\":\"Expense\",\"amount\":3000000,\"date\":null,\"paymentMethod\":\"cash\",\"doiTuong\":null,\"category\":\"utilities\",\"fromAccountCode\":null,\"toAccountCode\":null,\"feeAmount\":null,\"description\":\"chi 3 triệu tiền điện\"}]}");
        var (svc, handler) = Build(payload: payload);
        var list = await svc.ParseMultipleAsync(Tenant, "thu 5 triệu và chi 3 triệu tiền điện");
        Assert.Equal(2, list.Count);
        Assert.Equal(VoucherIntent.Revenue, list[0].Intent);
        Assert.Equal(5_000_000m, list[0].Amount);
        Assert.Equal(VoucherIntent.Expense, list[1].Intent);
        Assert.Equal("utilities", list[1].Category);
        Assert.Contains(list[0].Warnings, w => w.Contains("Đề xuất từ AI"));
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Multi_TscdExample_SplitsRevenueAndTransfer()
    {
        // "thanh lý TSCĐ được 100 triệu, mang 50 triệu gửi ngân hàng còn lại nhập quỹ"
        var payload = GeminiMultiJson("{\"vouchers\":[" +
            "{\"intent\":\"Revenue\",\"amount\":100000000,\"date\":null,\"paymentMethod\":null,\"doiTuong\":null,\"category\":null,\"fromAccountCode\":null,\"toAccountCode\":null,\"feeAmount\":null,\"description\":\"thanh lý tài sản cố định\"}," +
            "{\"intent\":\"TransferIn\",\"amount\":50000000,\"date\":null,\"paymentMethod\":null,\"doiTuong\":null,\"category\":null,\"fromAccountCode\":\"111\",\"toAccountCode\":\"112\",\"feeAmount\":null,\"description\":\"gửi 50 triệu vào ngân hàng\"}]}");
        var (svc, handler) = Build(payload: payload);
        var list = await svc.ParseMultipleAsync(Tenant, "thanh lý tài sản cố định được 100 triệu, mang 50 triệu gửi ngân hàng còn lại nhập quỹ tiền mặt");
        Assert.Equal(2, list.Count);
        Assert.Equal(VoucherIntent.Revenue, list[0].Intent);
        Assert.Equal(100_000_000m, list[0].Amount);
        Assert.Equal(VoucherIntent.TransferIn, list[1].Intent);
        Assert.Equal(50_000_000m, list[1].Amount);
        Assert.Equal("111", list[1].FromAccountCode);
        Assert.Equal("112", list[1].ToAccountCode);
    }

    [Fact]
    public async Task Multi_InvalidVouchers_FilteredToValid()
    {
        // intent sai + amount âm → lọc bỏ; còn 1 hợp lệ
        var payload = GeminiMultiJson("{\"vouchers\":[" +
            "{\"intent\":\"BOGUS\",\"amount\":-5,\"date\":null,\"paymentMethod\":null,\"doiTuong\":null,\"category\":null,\"fromAccountCode\":null,\"toAccountCode\":null,\"feeAmount\":null,\"description\":\"x\"}," +
            "{\"intent\":\"Expense\",\"amount\":3000000,\"date\":null,\"paymentMethod\":null,\"doiTuong\":null,\"category\":null,\"fromAccountCode\":null,\"toAccountCode\":null,\"feeAmount\":null,\"description\":\"chi 3 triệu\"}]}");
        var (svc, handler) = Build(payload: payload);
        var list = await svc.ParseMultipleAsync(Tenant, "chi phí 3 triệu và thu 5 triệu");
        Assert.Single(list);
        Assert.Equal(VoucherIntent.Expense, list[0].Intent);
        Assert.Equal(3_000_000m, list[0].Amount);
    }

    [Fact]
    public async Task Multi_MoreThan3_CappedAt3()
    {
        var payload = GeminiMultiJson("{\"vouchers\":[" +
            "{\"intent\":\"Expense\",\"amount\":1000000,\"date\":null,\"paymentMethod\":null,\"doiTuong\":null,\"category\":null,\"fromAccountCode\":null,\"toAccountCode\":null,\"feeAmount\":null,\"description\":\"1\"}," +
            "{\"intent\":\"Expense\",\"amount\":2000000,\"date\":null,\"paymentMethod\":null,\"doiTuong\":null,\"category\":null,\"fromAccountCode\":null,\"toAccountCode\":null,\"feeAmount\":null,\"description\":\"2\"}," +
            "{\"intent\":\"Expense\",\"amount\":3000000,\"date\":null,\"paymentMethod\":null,\"doiTuong\":null,\"category\":null,\"fromAccountCode\":null,\"toAccountCode\":null,\"feeAmount\":null,\"description\":\"3\"}," +
            "{\"intent\":\"Revenue\",\"amount\":4000000,\"date\":null,\"paymentMethod\":null,\"doiTuong\":null,\"category\":null,\"fromAccountCode\":null,\"toAccountCode\":null,\"feeAmount\":null,\"description\":\"4\"}]}");
        var (svc, handler) = Build(payload: payload);
        var list = await svc.ParseMultipleAsync(Tenant, "thu 4 triệu và chi 1 triệu, chi 2 triệu, chi 3 triệu");
        Assert.Equal(3, list.Count);
    }

    [Fact]
    public async Task Multi_FlagOff_ReturnsSingle()
    {
        var (svc, handler) = Build(flagEnabled: false);
        var list = await svc.ParseMultipleAsync(Tenant, "thu 5 triệu và chi 3 triệu tiền điện");
        Assert.Single(list);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task Multi_NotMultiLooking_ReturnsSingle()
    {
        var (svc, handler) = Build();
        var list = await svc.ParseMultipleAsync(Tenant, "thu tiền bán hàng 2 triệu tiền mặt");
        Assert.Single(list);
        Assert.Equal(0, handler.Calls); // không có marker nhiều nghiệp vụ → không gọi LLM
    }

    [Fact]
    public async Task Multi_BadJson_FallsBackSingle()
    {
        var (svc, handler) = Build(payload: "not-json{{{");
        var list = await svc.ParseMultipleAsync(Tenant, "thu 5 triệu và chi 3 triệu tiền điện");
        Assert.Single(list);
    }

    [Fact]
    public async Task Multi_VietnameseIntents_Resolved()
    {
        // LLM thật hay trả intent tiếng Việt — alias phải resolve đúng
        var payload = GeminiMultiJson("{\"vouchers\":[" +
            "{\"intent\":\"thu bán hàng\",\"amount\":5000000,\"date\":null,\"paymentMethod\":\"cash\",\"doiTuong\":null,\"category\":null,\"fromAccountCode\":null,\"toAccountCode\":null,\"feeAmount\":null,\"description\":\"thu bán hàng 5 triệu\"}," +
            "{\"intent\":\"chi phí tiền điện\",\"amount\":3000000,\"date\":null,\"paymentMethod\":\"cash\",\"doiTuong\":null,\"category\":\"utilities\",\"fromAccountCode\":null,\"toAccountCode\":null,\"feeAmount\":null,\"description\":\"chi 3 triệu tiền điện\"}]}");
        var (svc, handler) = Build(payload: payload);
        var list = await svc.ParseMultipleAsync(Tenant, "thu bán hàng 5 triệu, chi 3 triệu thanh toán tiền điện");
        Assert.Equal(2, list.Count);
        Assert.Equal(VoucherIntent.Revenue, list[0].Intent);
        Assert.Equal(VoucherIntent.Expense, list[1].Intent);
        Assert.Equal("utilities", list[1].Category);
    }
}