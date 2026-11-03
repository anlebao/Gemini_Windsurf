# TASK CARD G5 — SMART ENTRY VẤN ĐÁP: HỎI–ĐÁP LÀM RÕ ĐIỂM MÙ INPUT

> Status: ✅ **P1 IMPLEMENTED + TESTS PASS (2026-11-03 — chưa push, chờ duyệt deploy).** PO approve hướng + "go p1" — Q1-Q5 chốt theo đề xuất §9. **Đã làm:** `SmartEntryDialogService` (slot matrix intent/direction/amount/doiTuong/category + merge đa-slot + cap 2/slot·6/session + skip/abandon + snapshot restore) · `VoucherTextParseService.ExtractSlots` (fragment API + AmountCandidates + direction detection) · `SmartEntryBar` dialog thread (chips + ô trả lời + mic answer + skip/bỏ qua) · codec `SerializeDialog` + JS `vanan.smartEntryDialog` (persist reload) · `ShowResult→ShowResultAsync` (await) · DI. **Fix trong implement:** DirectionUnclear theo consistency (tránh loop hỏi mãi) · PTTT đáp án đè default "cash" · filler "không biết" không nuốt thành tên. **Tests:** `SmartEntryDialogServiceTests` **21/21** · regression parser+LLM+framework **90/90** · ShopERP.Tests **300/300** · sln build 0 errors · **guard ALL PASSED** (`guard-report-20261103-223639`). **Còn:** P2 (chips đối tượng từ công nợ) · P3 (LLM interpret câu trả lời lạ) · E2E spec + RV production · guide §14 đã update.
> Nguồn: user request 2026-11-03 — "nâng cấp smart entry lên mức vấn đáp (hỏi-trả lời) với kế toán viên nhập liệu để tìm ra điểm chưa rõ trong nội dung input, hoàn chỉnh đầu vào của 1 hoặc nhiều nghiệp vụ kế toán".
> Liên quan: task G (`task_pg_smart_entry_ai.md`) · G2 (`task_pg2_llm_hybrid.md`) · PH3 (`task_ph3_smart_entry_session_coverage.md` — session persist) · SRS §18.4.1.5 (không đoán bừa → hỏi thay vì đoán).

---

## 0. UX CHANGE PROPOSAL (SRS §18.1.2 — bắt buộc trước implement)

| Mục | Nội dung |
|---|---|
| Lý do | Khi parse ra `MissingFields`/`Unknown`/ambiguous, hệ thống chỉ hiển thị "⚠ Thiếu" rồi đẩy về form — user phải tự "dịch" phần còn thiếu. Vấn đáp hỏi đúng chỗ mù, điền tiếp trong ngữ cảnh câu gốc. |
| Không giải quyết được ở backend? | `MissingFields` contract đã có sẵn — đây là vòng lặp tương tác (state machine), phải ở UI + service. |
| Thao tác tăng thêm | **0 bắt buộc** — dialog chỉ hiện khi parse thiếu/mơ hồ; nút "Bỏ qua — tự nhập form" + "Áp vào form" luôn sẵn. Câu đầy đủ → hành vi y hệt hiện tại. |
| Phương án ít tác động hơn | Đã xét: inline guided fields trên card (Option C) — ~70% giá trị nhưng không phải vấn đáp; giữ làm fallback hiển thị MissingFields như cũ. |
| Ảnh hưởng đào tạo | Không — câu hỏi tiếng Việt tự nhiên + chips gợi ý 1-chạm. |
| Rollback | Hành vi mặc định fallback về card "⚠ Thiếu" hiện tại khi session hết lượt hỏi / lỗi. Không cần flag mới (P1 thuần rule-based, không external dep). |

## 1. MỤC TIÊU

1. Parse xong còn `MissingFields`/intent mơ hồ → hệ thống **hỏi câu làm rõ** (text + quick-reply chips) → user trả lời (gõ/nói/chips) → merge vào `ParsedVoucher` → hỏi tiếp hoặc `Ready`.
2. Hỗ trợ **multi-voucher** (G4): hỏi tuần tự từng phiếu, badge "Phiếu i/N".
3. Câu trả lời gộp nhiều slot ("5 triệu trả cho anh Hùng bằng ck") → điền đồng thời nhiều field còn thiếu.
4. Giữ nguyên luật vàng: **KHÔNG auto-save** — dialog chỉ hoàn thiện `ParsedVoucher`; user vẫn "Áp vào form" → kiểm → Lưu, mọi guard giữ nguyên.

## 2. KIẾN TRÚC

```
SmartEntryBar (textarea + mic + thread hội thoại)
   → ISmartEntryDialogService.StartAsync(tenantId, text)
        → Parser.ParseMultipleAsync (tái dùng — rule + LLM decorator y nguyên)
        → SlotResolver: với mỗi voucher thiếu → ClarifyingQuestion(slot, text, chips, voucherIndex)
   → AnswerAsync(sessionId, answer)
        → chips → điền thẳng Value (bỏ NLP — deterministic 100%)
        → free text → SlotExtractors (bộ trích xuất fragment hiện có, expose ra)
             · điền slot đang hỏi trước → opportunistic các slot còn thiếu
             · fail slot đang hỏi → hỏi lại 1 lần có hint → vẫn fail → bỏ qua slot
        → re-validate chéo (hướng transfer, required matrix) → MissingFields/Confidence mới
        → hỏi tiếp | Ready (đủ / hết lượt / user skip)
   → Ready → card phiếu hoàn chỉnh → "Áp vào form" (OnParsed — tái dùng y nguyên)
```

- DI: `AddScoped<ISmartEntryDialogService, SmartEntryDialogService>` — inject `IVoucherTextParseService` (đã là decorator LLM) + `SmartEntryDialogSessionStore` (scoped per circuit).
- Blazor Server in-process — KHÔNG endpoint HTTP mới, KHÔNG entity/migration/Domain.
- **P3 (defer):** `ILlmAnswerInterpreter` — câu trả lời free-text fail → Gemini interpret `{question, answer} → field JSON` → schema validation + merge pattern G2. Gate `SmartEntryLlmEnabled` + ApiKey. P1 không phụ thuộc LLM.

## 3. CONTRACT + SLOT MATRIX

```csharp
public sealed record QuickReply(string Label, string Value);
public sealed record ClarifyingQuestion(
    int VoucherIndex, string Slot, string QuestionText,
    IReadOnlyList<QuickReply> QuickReplies, bool Required);
public sealed record SlotExtraction(           // kết quả fragment parse
    decimal? Amount, DateTime? Date, string? PaymentMethod,
    string? DoiTuong, string? Category, VoucherIntent? IntentHint,
    string? TransferDirection);                // "out"=NH→quỹ | "in"=quỹ→NH
public sealed class SmartEntryDialogSession
{
    public Guid Id; public string OriginalText;
    public List<ParsedVoucher> Vouchers;
    public ClarifyingQuestion? Pending;
    public int AskedCount;                     // chống loop: ≤6/session
    public DialogState State;                  // Asking | Ready | Abandoned
}
public interface ISmartEntryDialogService
{
    Task<SmartEntryDialogSession> StartAsync(TenantId tenantId, string text, CancellationToken ct = default);
    Task<SmartEntryDialogSession> AnswerAsync(Guid sessionId, string answer, CancellationToken ct = default);
    SmartEntryDialogSession? Get(Guid sessionId);
    void Abandon(Guid sessionId);              // "Bỏ qua — tự nhập form"
}
```

### Slot bắt buộc theo intent → câu hỏi

| Intent | Required | Câu hỏi + chips |
|---|---|---|
| Revenue/Capital/Advance/Expense | amount | "Số tiền bao nhiêu?" (free text) |
| Receivable*/Payable*/AdvanceOut | amount + doiTuong | "Khách/người bán nào?" — **P2:** chips đối tượng có khoản nợ mở (`ICongNoService`/`ICustomerService`) |
| TransferIn/Out hoặc transfer mơ hồ | amount + direction | chips "Quỹ → Ngân hàng (nộp vào NH)" / "Ngân hàng → Quỹ (rút về)" |
| Unknown intent | intent | chips ý định: Thu / Chi / Chuyển quỹ / Công nợ khách / Công nợ NCC / Vay / CTGS |
| 1 phiếu, ≥2 amount xung đột | amount | chips liệt kê các số đã trích + "Số khác…" |
| Expense thiếu category (ảnh hưởng TK) | category (soft) | chips 6 nhóm + "Để tôi chọn TK trên form" |

**Không hỏi** (im lặng dùng default): date (hôm nay), paymentMethod (heuristic hiện có), fee. Chỉ hỏi slot *bắt buộc để lưu an toàn* — tránh làm phiền (Q4: KHÔNG hỏi lại khi đủ field dù Confidence=Low — badge vẫn hiện cho user quyết).

### Merge semantics
1. QuickReply → `Value` vào slot trực tiếp.
2. Free text → `ExtractSlots(answer)` (expose fragment extractors từ `VoucherTextParseService` — method internal mới, KHÔNG parse lại nguyên câu gốc để tránh re-interpretation drift): điền slot đang hỏi trước, rồi fill các slot `null` khác.
3. Description voucher giữ = `OriginalText` (+ append transcript ngắn vào Warnings, KHÔNG đổi diễn giải).
4. Re-validate: transfer from≠to + hướng khớp intent; required matrix; Confidence tính lại.
5. Caps: ≤2 câu hỏi/slot · ≤6 câu hỏi/session · mọi câu có chip "Bỏ qua — tự nhập" → `Ready`/`Abandoned` rơi về MissingFields-fallback hiện tại.

## 4. UI

- `SmartEntryBar` thêm **dialog thread** dưới textarea: bubbles `user`/`system`, câu hỏi kèm chip row + ô input + mic (tái dùng `vananSpeech` — trả lời bằng giọng nói). Nút "Áp vào form" per voucher vẫn hiển thị ngay khi voucher Ready (không bắt đợi hết hỏi).
- Multi: badge "Phiếu i/N — loại phiếu" trên câu hỏi; phiếu đủ field bỏ qua hỏi.
- Session: `SmartEntrySessionState` + `SmartEntrySessionCodec` mở rộng lưu `{dialogSessionId, transcript tóm tắt, pendingQuestion}` — khôi phục deterministic sau navigation/full-reload. **AWAIT mọi JS write** (bài học PH3: fire-and-forget mất state khi full-reload).
- Reset: parse câu mới → `Abandon` session cũ; lưu phiếu xong → `NotifySavedAsync` flow giữ nguyên.

## 5. FILES TẠO/SỬA

- Tạo: `3_CoreHub/Services/SmartEntryDialogService.cs` (+`ISmartEntryDialogService`, `ClarifyingQuestion`, `QuickReply`, `SlotExtraction`, `SmartEntryDialogSession`, `DialogState`, `SmartEntryDialogSessionStore`) · `ExtractSlots` fragment API trong `VoucherTextParseService` (internal/public method mới — KHÔNG đổi `ParseAsync` hiện có) · tests `SmartEntryDialogServiceTests` + `SmartEntryBarDialogTests` (bUnit)
- Sửa: `SmartEntryBar.razor` (thread + chips + answer input + mic answer) · `SmartEntrySessionState`/`SmartEntrySessionCodec` (dialog fields) · `Program.cs` (1 dòng DI) · `smart-entry-user-guide.md` (mục vấn đáp)
- **Không** entity/migration/Domain/endpoint mới — 100% Services + UI.

## 6. ACCEPTANCE CRITERIA

- [ ] "rút tiền 50 triệu" (thiếu hướng) → hỏi chips hướng → chọn "Ngân hàng → Quỹ" → Ready TransferOut 50M 112→111 → Áp form → Lưu JE đúng
- [ ] "khách trả nợ" (thiếu amount + đối tượng) → hỏi amount → trả "5 triệu anh Hùng ck" → điền amount+đối tượng+PTTT 1 lượt → Ready
- [ ] Câu đủ field → KHÔNG hỏi, hành vi y hệt hiện tại (regression toàn bộ parse suite)
- [ ] Multi "thu 5tr, chi tiền điện" → hỏi amount phiếu 2 ("Phiếu 2/2 — Chi phí") → Ready cả 2 → áp từng phiếu
- [ ] Trả lời vô nghĩa 2 lần → bỏ qua slot, Ready với MissingFields còn lại (fallback card cũ)
- [ ] "Bỏ qua — tự nhập" → Abandoned → form trống prefill phần có, không hỏi nữa
- [ ] Hết 6 câu → Ready tự động (chống loop)
- [ ] Reload giữa dialog → F5 → pending question + transcript khôi phục
- [ ] guard + sln build + Core.Tests + bUnit PASS · E2E spec mới + RV production

## 7. TEST CASES (tối thiểu)

- D1 Slot matrix: mỗi intent → đúng câu hỏi đầu tiên; đủ field → 0 câu hỏi
- D2 Merge gộp: "5 triệu anh Hùng ck" trả lời câu hỏi amount → amount+doiTuong+paymentMethod cùng điền
- D3 Chips: value điền thẳng, không qua NLP; direction chips → from/to đúng hướng
- D4 Ambiguous amount: câu 2 số → chips đúng 2 giá trị trích được
- D5 Loop: 2 lần fail cùng slot → skip; 6 câu → force Ready
- D6 Multi order: hỏi theo thứ tự voucherIndex; phiếu đủ → skip
- D7 Session codec round-trip: session Asking → serialize/deserialize → pending question nguyên vẹn
- D8 Re-validate: answer đổi direction trái intent → correction hoặc hỏi lại (không để from==to)
- D9 Regression: `VoucherTextParseServiceTests` 33/33 + multi G4 + LLM decorator tests y nguyên

## 8. PHÂN KỲ

| Phase | Scope | LLM? |
|---|---|---|
| **P1 (card này)** | Dialog core thuần rule-based: service + slot matrix + merge + chips + UI thread + session persist | KHÔNG — offline 100% |
| **P2** | Smart suggestions: chips đối tượng từ khoản nợ mở (`GetOpenKhoanNoAsync`), category theo lịch sử tenant | KHÔNG |
| **P3** | `ILlmAnswerInterpreter`: free-text fail → Gemini interpret (flag `SmartEntryLlmEnabled` + key) — tái dùng schema validation + merge pattern G2 | CÓ (flag) |
| **P4 (defer)** | TTS đọc câu hỏi (voice-loop), cross-voucher questions ("còn lại"→phiếu 1), metrics | — |

## 9. DECISIONS — ĐÃ CHỐT (PO approved 2026-11-03, theo đề xuất)

| # | Quyết định | Chốt |
|---|---|---|
| Q1 | Dialog UI | Thread chat nhỏ **trong SmartEntryBar** — không trang riêng |
| Q2 | LLM trong vấn đáp | P1 thuần rule-based; LLM chỉ **interpret câu trả lời lạ** ở P3 (không điều khiển hội thoại) |
| Q3 | Cap rounds | ≤2 câu hỏi/slot · ≤6 câu hỏi/session → force Ready |
| Q4 | Hỏi lại khi Confidence=Low nhưng đủ field | KHÔNG — Low chỉ là badge; user confirm ở form như hiện tại |
| Q5 | Suggest đối tượng từ công nợ | P2 (ngay sau P1) — use-case giá trị nhất ("khách nào?" → chips đang nợ) |
| D-khác | Auto-save | KHÔNG — giữ nguyên "user bấm Lưu" |
| D-khác | Flag riêng cho dialog | KHÔNG cần — P1 rule-based offline; fallback tự nhiên = card MissingFields cũ |

## 10. RỦI RO & GIẢM THIỂU

| Rủi ro | Giảm thiểu |
|---|---|
| Loop hỏi mãi | Cap + escape hatch + Ready-fallback §3.5 |
| Trả lời lạc đề | Parse đa-slot → hỏi lại 1 lần có hint → skip slot |
| State rơi khi nav/reload | Codec mở rộng + AWAIT JS write (bài học PH3) |
| UX mệt vì hỏi nhiều | Chỉ hỏi slot bắt buộc; date/PTTT default im lặng; chip 1-chạm |
| LLM bịa field (P3) | Schema validation + rule-based-high-wins merge (đã có sẵn G2) |
| Privacy | P1 không gửi gì ra ngoài; P3 chỉ khi flag ON (boundary hiện tại) |
