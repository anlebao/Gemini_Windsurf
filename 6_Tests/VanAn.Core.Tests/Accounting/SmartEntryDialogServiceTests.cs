using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;
using Xunit;

namespace VanAn.Core.Tests.Accounting;

/// <summary>Task G5 (P1): vấn đáp rule-based — hỏi theo slot thiếu, merge đáp án, chống loop.
/// KHÔNG LLM — parser rule-based thật; multi-voucher qua stub (LLM multi không test ở đây).</summary>
public class SmartEntryDialogServiceTests
{
    private static readonly TenantId Tenant = new(Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"));

    private sealed class StubParser(IReadOnlyList<ParsedVoucher> result) : IVoucherTextParseService
    {
        public Task<ParsedVoucher> ParseAsync(TenantId tenantId, string text, CancellationToken ct = default, bool forceLlm = false)
            => Task.FromResult(result[0]);
        public Task<IReadOnlyList<ParsedVoucher>> ParseMultipleAsync(TenantId tenantId, string text, CancellationToken ct = default, bool forceLlm = false)
            => Task.FromResult(result);
    }

    private static SmartEntryDialogService Svc(IVoucherTextParseService? parser = null)
    {
        var rule = new VoucherTextParseService();
        return new SmartEntryDialogService(parser ?? rule, rule);
    }

    private static Task<SmartEntryDialogSession> Start(string text) => Svc().StartAsync(Tenant, text);

    /// <summary>Cùng 1 svc cho cả dialog — _sessions là per-instance.</summary>

    // ---------- D1: đủ field → KHÔNG hỏi (regression hành vi cũ) ----------

    [Fact]
    public async Task Complete_Input_NoQuestion_Ready()
    {
        var s = await Start("chi tiền điện 2 triệu");
        Assert.Equal(DialogState.Ready, s.State);
        Assert.Null(s.Pending);
        Assert.Equal(VoucherIntent.Expense, s.Vouchers[0].Intent);
        Assert.Equal(2_000_000m, s.Vouchers[0].Amount);
    }

    [Fact]
    public async Task Complete_Transfer_NoQuestion_Ready()
    {
        var s = await Start("rút tiền gửi ngân hàng 50 triệu nhập vào quỹ tiền mặt");
        Assert.Equal(DialogState.Ready, s.State);
        Assert.Null(s.Pending);
        Assert.Equal(VoucherIntent.TransferOut, s.Vouchers[0].Intent);
    }

    // ---------- D1: slot matrix — intent Unknown → hỏi intent với chips ----------

    [Fact]
    public async Task Unknown_Intent_AsksIntentWithChips()
    {
        var s = await Start("abc xyz 5 triệu");
        Assert.Equal(DialogState.Asking, s.State);
        Assert.NotNull(s.Pending);
        Assert.Equal("intent", s.Pending!.Slot);
        Assert.True(s.Pending.QuickReplies.Count >= 4);
    }

    // ---------- direction: transfer mơ hồ → hỏi hướng → chip điền đúng ----------

    [Fact]
    public async Task Transfer_UnclearDirection_AsksThenChipResolves()
    {
        // "sang quỹ" không khớp toCash/toBank rõ → parser warning "hướng chưa rõ" → dialog hỏi.
        var svc = Svc();
        var s = await svc.StartAsync(Tenant, "chuyển 50 triệu từ ngân hàng sang quỹ");
        Assert.Equal(DialogState.Asking, s.State);
        Assert.NotNull(s.Pending);
        Assert.Equal("direction", s.Pending!.Slot);
        Assert.Equal(2, s.Pending.QuickReplies.Count);

        s = await svc.AnswerAsync(s.Id, "out");
        Assert.Equal(DialogState.Ready, s.State);
        var v = s.Vouchers[0];
        Assert.Equal(VoucherIntent.TransferOut, v.Intent);
        Assert.Equal("112", v.FromAccountCode);
        Assert.Equal("111", v.ToAccountCode);
        Assert.Equal(50_000_000m, v.Amount);
        Assert.DoesNotContain(v.Warnings, w => w.Contains("hướng chưa rõ"));
        Assert.Empty(v.MissingFields);
    }

    [Fact]
    public async Task Transfer_FreeTextAnswer_ResolvesDirection()
    {
        var svc = Svc();
        var s = await svc.StartAsync(Tenant, "chuyển 50 triệu từ ngân hàng sang quỹ");
        Assert.Equal("direction", s.Pending?.Slot);
        s = await svc.AnswerAsync(s.Id, "từ ngân hàng về quỹ");
        Assert.Equal(DialogState.Ready, s.State);
        Assert.Equal(VoucherIntent.TransferOut, s.Vouchers[0].Intent);
    }

    // ---------- D2: merge gộp — 1 đáp án điền nhiều slot ----------

    [Fact]
    public async Task Answer_FillsMultipleSlots_AtOnce()
    {
        var svc = Svc();
        var s = await svc.StartAsync(Tenant, "khách trả nợ");
        Assert.Equal(DialogState.Asking, s.State);
        Assert.Equal("amount", s.Pending!.Slot);

        s = await svc.AnswerAsync(s.Id, "5 triệu anh Hùng chuyển khoản");
        Assert.Equal(DialogState.Ready, s.State);
        var v = s.Vouchers[0];
        Assert.Equal(5_000_000m, v.Amount);
        Assert.Equal("Hùng", v.DoiTuong);
        Assert.Equal("bank", v.PaymentMethod);
        Assert.Equal("High", v.Confidence);
        Assert.Empty(v.MissingFields);
    }

    // ---------- đối tượng: hỏi riêng + tên trần + filler ----------

    [Fact]
    public async Task DoiTuong_RawName_Filled()
    {
        var svc = Svc();
        var s = await svc.StartAsync(Tenant, "khách trả nợ 5 triệu");
        Assert.Equal("doiTuong", s.Pending?.Slot);
        s = await svc.AnswerAsync(s.Id, "Nguyễn Văn A");
        Assert.Equal(DialogState.Ready, s.State);
        Assert.Equal("Nguyễn Văn A", s.Vouchers[0].DoiTuong);
    }

    [Fact]
    public async Task DoiTuong_UnknownFiller_NotSwallowedAsName()
    {
        var svc = Svc();
        var s = await svc.StartAsync(Tenant, "khách trả nợ 5 triệu");
        s = await svc.AnswerAsync(s.Id, "không biết");
        // Filler không được điền thành tên → hỏi lại (retry hint), không skip.
        Assert.Equal(DialogState.Asking, s.State);
        Assert.Equal("doiTuong", s.Pending?.Slot);
        Assert.Null(s.Vouchers[0].DoiTuong);
        s = await svc.AnswerAsync(s.Id, "anh Hùng");
        Assert.Equal("Hùng", s.Vouchers[0].DoiTuong);
        Assert.Equal(DialogState.Ready, s.State);
    }

    // ---------- D5: loop — 2 lần fail → skip slot; cap session ----------

    [Fact]
    public async Task Gibberish_Twice_SkipsSlot_ThenNextSlotOrReady()
    {
        var svc = Svc();
        var s = await svc.StartAsync(Tenant, "chi phí linh tinh");
        Assert.Equal(DialogState.Asking, s.State);
        Assert.Equal("amount", s.Pending!.Slot);

        s = await svc.AnswerAsync(s.Id, "không rõ lắm bạn ơi");
        Assert.Equal(DialogState.Asking, s.State);
        Assert.Equal("amount", s.Pending?.Slot); // hỏi lại lần 2 (có hint)
        s = await svc.AnswerAsync(s.Id, "thật sự không nhớ");
        Assert.Null(s.Vouchers[0].Amount);
        Assert.Contains("amount", s.Vouchers[0].MissingFields); // fallback MissingFields như cũ
        // amount bị skip → slot tiếp theo (category soft) được hỏi; skip nốt → Ready.
        if (s.State == DialogState.Asking) s = await svc.AnswerAsync(s.Id, "bỏ qua");
        Assert.Equal(DialogState.Ready, s.State);
    }

    [Fact]
    public async Task Retry_Question_HasHint()
    {
        var svc = Svc();
        var s = await svc.StartAsync(Tenant, "chi phí linh tinh");
        s = await svc.AnswerAsync(s.Id, "chưa biết nữa");
        Assert.Equal(DialogState.Asking, s.State);
        Assert.Contains("Chưa đọc được", s.Pending!.QuestionText);
    }

    // ---------- skip/abandon ----------

    [Fact]
    public async Task SkipText_SkipsSlot()
    {
        var svc = Svc();
        var s = await svc.StartAsync(Tenant, "chi phí linh tinh");
        s = await svc.AnswerAsync(s.Id, "bỏ qua");            // skip amount → category hỏi tiếp
        if (s.State == DialogState.Asking) s = await svc.AnswerAsync(s.Id, "bỏ qua");
        Assert.Equal(DialogState.Ready, s.State);
        Assert.Null(s.Vouchers[0].Amount);
    }

    [Fact]
    public async Task Skip_Method_SkipsSlot()
    {
        var svc = Svc();
        var s = await svc.StartAsync(Tenant, "chi phí linh tinh");
        s = await svc.SkipAsync(s.Id);
        Assert.NotNull(s);
        if (s!.State == DialogState.Asking) s = await svc.SkipAsync(s.Id); // skip nốt category nếu được hỏi
        Assert.Equal(DialogState.Ready, s!.State);
    }

    [Fact]
    public async Task Abandon_StopsDialog_KeepsVoucher()
    {
        var svc = Svc();
        var s = await svc.StartAsync(Tenant, "khách trả nợ");
        s = svc.Abandon(s.Id);
        Assert.NotNull(s);
        Assert.Equal(DialogState.Abandoned, s!.State);
        Assert.Null(s.Pending);
        Assert.Equal(VoucherIntent.ReceivablePayment, s.Vouchers[0].Intent);
    }

    // ---------- expense category (soft) ----------

    [Fact]
    public async Task Expense_NoCategory_AsksSoftQuestion()
    {
        var svc = Svc();
        var s = await svc.StartAsync(Tenant, "chi 2 triệu");
        Assert.Equal(DialogState.Asking, s.State);
        Assert.Equal("category", s.Pending!.Slot);
        Assert.False(s.Pending.Required);
        s = await svc.AnswerAsync(s.Id, "utilities");
        Assert.Equal(DialogState.Ready, s.State);
        Assert.Equal("utilities", s.Vouchers[0].Category);
    }

    // ---------- D6: multi-voucher order (stub parser — G4 LLM multi ngoài scope) ----------

    [Fact]
    public async Task Multi_AsksPerVoucher_SkipsComplete()
    {
        var complete = new ParsedVoucher(VoucherIntent.Revenue, 5_000_000m, DateTime.Today,
            "cash", null, null, null, null, null, "thu 5 triệu", "High", [], []);
        var incomplete = new ParsedVoucher(VoucherIntent.Expense, null, DateTime.Today,
            null, null, null, null, "utilities", null, "chi điện", "Low", ["amount"], []);
        var svc = Svc(new StubParser([complete, incomplete]));

        var s = await svc.StartAsync(Tenant, "thu 5 triệu và chi tiền điện");
        Assert.Equal(DialogState.Asking, s.State);
        Assert.NotNull(s.Pending);
        Assert.Equal(1, s.Pending!.VoucherIndex);       // phiếu 1 đủ → hỏi phiếu 2
        Assert.Equal("amount", s.Pending.Slot);
        Assert.Contains("Phiếu 2/2", s.Pending.QuestionText);

        s = await svc.AnswerAsync(s.Id, "3 triệu");
        Assert.Equal(DialogState.Ready, s.State);
        Assert.Equal(3_000_000m, s.Vouchers[1].Amount);
    }

    // ---------- MaxQuestions cap ----------

    [Fact]
    public async Task Cap_SixQuestions_ForcesReady()
    {
        // 3 phiếu thiếu amount+đối tượng → tiềm năng 6+ câu; skip hết → cap 6 → Ready.
        var incomplete = new ParsedVoucher(VoucherIntent.ReceivablePayment, null, DateTime.Today,
            null, null, null, null, null, null, "khách trả nợ", "Low", ["amount", "đối tượng"], []);
        var svc = Svc(new StubParser([incomplete, incomplete, incomplete]));
        var s = await svc.StartAsync(Tenant, "khách trả nợ");
        int turns = 0;
        while (s.State == DialogState.Asking && turns < 20)
        {
            s = await svc.AnswerAsync(s.Id, "bỏ qua");
            turns++;
        }
        Assert.Equal(DialogState.Ready, s.State);
        Assert.True(s.AskedCount <= SmartEntryDialogService.MaxQuestions);
    }

    // ---------- D7: snapshot restore ----------

    [Fact]
    public async Task Snapshot_Restore_ContinuesDialog()
    {
        var svc = Svc();
        var s = await svc.StartAsync(Tenant, "khách trả nợ");
        var snap = s.ToSnapshot();
        var s2 = svc.Restore(snap);
        Assert.Equal(DialogState.Asking, s2.State);
        Assert.Equal("amount", s2.Pending?.Slot);

        // Trả lời tiếp trên session đã restore (id mới nhưng state giữ nguyên).
        s2 = await svc.AnswerAsync(s2.Id, "5 triệu");
        Assert.Equal("doiTuong", s2.Pending?.Slot);
    }

    // ---------- ExtractSlots (fragment API) ----------

    [Fact]
    public void ExtractSlots_AmountCandidates_TwoNumbers()
    {
        // "và" ngắt chuỗi số — "5 triệu 3 triệu" liền nhau là chain hợp lệ (5,3 triệu).
        var ex = new VoucherTextParseService().ExtractSlots("thu 5 triệu và 3 triệu");
        Assert.Equal(2, ex.AmountCandidates.Count);
        Assert.Contains(5_000_000m, ex.AmountCandidates);
        Assert.Contains(3_000_000m, ex.AmountCandidates);
    }

    [Fact]
    public void ExtractSlots_DateOnlyWhenMarked()
    {
        var rule = new VoucherTextParseService();
        Assert.Null(rule.ExtractSlots("5 triệu").Date);
        Assert.Equal(DateTime.Today.AddDays(-1), rule.ExtractSlots("5 triệu hôm qua").Date);
    }

    [Fact]
    public void ExtractSlots_Direction_Fragments()
    {
        var rule = new VoucherTextParseService();
        Assert.Equal("out", rule.ExtractSlots("ngân hàng về quỹ").TransferDirection);
        Assert.Equal("in", rule.ExtractSlots("quỹ gửi vào ngân hàng").TransferDirection);
        Assert.Null(rule.ExtractSlots("năm triệu").TransferDirection);
    }

    [Fact]
    public void ExtractSlots_NoDefaults_WhenEmpty()
    {
        var ex = new VoucherTextParseService().ExtractSlots("   ");
        Assert.Null(ex.Amount);
        Assert.Null(ex.PaymentMethod);
        Assert.Null(ex.DoiTuong);
        Assert.Equal(VoucherIntent.Unknown, ex.IntentHint);
    }

    // ---------- P2: smart suggestions ----------

    private sealed class StubCongNo(List<(string Account, string Name, decimal CuoiKy)> rows)
        : VanAn.CoreHub.Services.CongNo.ICongNoService
    {
        public Task<VanAn.Shared.DTOs.CongNoReportDto> GetCongNoReportAsync(
            TenantId tenantId, int year, int month, CancellationToken ct = default)
            => Task.FromResult(new VanAn.Shared.DTOs.CongNoReportDto
            {
                Groups = rows.GroupBy(r => r.Account).Select(g => new VanAn.Shared.DTOs.CongNoGroupDto
                {
                    AccountCode = g.Key,
                    Rows = g.Select(r => new VanAn.Shared.DTOs.CongNoRowDto
                    { DoiTuong = r.Name, CuoiKy = r.CuoiKy }).ToList()
                }).ToList()
            });

        public Task<VanAn.Shared.DTOs.AccountingEntryDto> CreateReceivableAsync(TenantId t, decimal a, string d, string? desc, DateTime dt, bool ip, string? m = null, string? r = null, Guid? al = null, string? pm = null, CancellationToken c = default) => throw new NotImplementedException();
        public Task<VanAn.Shared.DTOs.AccountingEntryDto> CreatePayableAsync(TenantId t, decimal a, string d, string? desc, DateTime dt, bool ip, string? m = null, string? r = null, Guid? al = null, string? pm = null, CancellationToken c = default) => throw new NotImplementedException();
        public Task<VanAn.Shared.DTOs.DoiTuongLedgerDto> GetDoiTuongLedgerAsync(TenantId t, string a, string d, int y, int m, CancellationToken c = default) => throw new NotImplementedException();
        public Task<VanAn.Shared.DTOs.KhoanNoPaymentsDto?> GetKhoanNoPaymentsAsync(TenantId t, Guid id, CancellationToken c = default) => throw new NotImplementedException();
        public Task<List<VanAn.Shared.DTOs.KhoanNoOpenItemDto>> GetOpenKhoanNoAsync(TenantId t, string a, string d, CancellationToken c = default) => throw new NotImplementedException();
    }

    [Fact]
    public async Task P2_DoiTuong_Chips_FromOpenDebt()
    {
        var rule = new VoucherTextParseService();
        var congNo = new StubCongNo([("131", "Anh Hùng", 8_000_000m), ("131", "Chị Lan", 3_000_000m)]);
        var svc = new SmartEntryDialogService(rule, rule, congNo: congNo);
        var s = await svc.StartAsync(Tenant, "khách trả nợ 5 triệu");
        Assert.Equal("doiTuong", s.Pending?.Slot);
        Assert.Equal(2, s.Pending!.QuickReplies.Count);
        Assert.Equal("Anh Hùng", s.Pending.QuickReplies[0].Value); // dư lớn nhất trước

        s = await svc.AnswerAsync(s.Id, "Chị Lan");  // chip value điền thẳng
        Assert.Equal("Chị Lan", s.Vouchers[0].DoiTuong);
        Assert.Equal(DialogState.Ready, s.State);
    }

    [Fact]
    public async Task P2_DoiTuong_Payable_UsesAccount331()
    {
        var rule = new VoucherTextParseService();
        var congNo = new StubCongNo([("331", "NCC Minh", 12_000_000m)]);
        var svc = new SmartEntryDialogService(rule, rule, congNo: congNo);
        var s = await svc.StartAsync(Tenant, "trả tiền người bán 2 triệu");
        Assert.Equal("doiTuong", s.Pending?.Slot);
        Assert.Single(s.Pending!.QuickReplies);
        Assert.Equal("NCC Minh", s.Pending.QuickReplies[0].Value);
    }

    [Fact]
    public async Task P2_CongNo_Failsafe_NoService()
    {
        // Không có ICongNoService → câu hỏi đối tượng vẫn hiện, không chips.
        var s = await Svc().StartAsync(Tenant, "khách trả nợ 5 triệu");
        Assert.Equal("doiTuong", s.Pending?.Slot);
        Assert.Empty(s.Pending!.QuickReplies);
    }

    // ---------- P3: LLM interpret ----------

    private sealed class StubFeatures(bool enabled) : VanAn.Shared.Services.IShopFeatureSettingsService
    {
        public Task<VanAn.Shared.Services.ShopFeatureSettingsDto> GetSettingsAsync(Guid t, CancellationToken c = default) => throw new NotImplementedException();
        public Task<VanAn.Shared.Services.ShopFeatureSettingsDto> UpdateSettingsAsync(Guid t, VanAn.Shared.Services.ShopFeatureSettingsDto s, CancellationToken c = default) => throw new NotImplementedException();
        public Task<bool> IsEnabledAsync(Guid tenantId, string toggleName, CancellationToken ct = default) => Task.FromResult(enabled);
    }

    private sealed class StubFactory(string responseJson, Action<int>? onCall = null) : IHttpClientFactory
    {
        private int _calls;
        public HttpClient CreateClient(string name)
        {
            _calls++; onCall?.Invoke(_calls);
            return new HttpClient(new StubHandler(responseJson));
        }
        public int Calls => _calls;
    }

    private sealed class StubHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken c)
            => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") });
    }

    private static SmartEntryDialogService SvcLlm(bool flagOn, string llmJson, out StubFactory factory)
    {
        var rule = new VoucherTextParseService();
        factory = new StubFactory(
            $"{{\"candidates\":[{{\"content\":{{\"parts\":[{{\"text\":{System.Text.Json.JsonSerializer.Serialize(llmJson)}}}]}}}}]}}");
        return new SmartEntryDialogService(rule, rule, httpClientFactory: factory,
            llmOptions: Microsoft.Extensions.Options.Options.Create(new SmartEntryLlmOptions { ApiKey = "test-key" }),
            featureSettings: new StubFeatures(flagOn));
    }

    [Fact]
    public async Task P3_Llm_FillsSlot_WhenRuleFails()
    {
        var svc = SvcLlm(true, "{\"amount\":5000000}", out var factory);
        var s = await svc.StartAsync(Tenant, "chi phí linh tinh");
        Assert.Equal("amount", s.Pending?.Slot);
        s = await svc.AnswerAsync(s.Id, "cái đó lúc nãy ông chủ nói rồi á"); // rule không đọc được
        Assert.True(factory.Calls >= 1);
        Assert.Equal(5_000_000m, s.Vouchers[0].Amount);
        Assert.Contains(s.Vouchers[0].Warnings, w => w.Contains("AI"));
    }

    [Fact]
    public async Task P3_Llm_FlagOff_NeverCalled()
    {
        var svc = SvcLlm(false, "{\"amount\":5000000}", out var factory);
        var s = await svc.StartAsync(Tenant, "chi phí linh tinh");
        s = await svc.AnswerAsync(s.Id, "không rõ");
        Assert.Equal(0, factory.Calls);
        Assert.Null(s.Vouchers[0].Amount);
    }

    [Fact]
    public async Task P3_Llm_BadJson_Ignored()
    {
        var svc = SvcLlm(true, "không phải json", out _);
        var s = await svc.StartAsync(Tenant, "chi phí linh tinh");
        s = await svc.AnswerAsync(s.Id, "không rõ");
        Assert.Null(s.Vouchers[0].Amount);          // LLM rác → bỏ, retry path như thường
        Assert.Equal(DialogState.Asking, s.State);
    }
}
