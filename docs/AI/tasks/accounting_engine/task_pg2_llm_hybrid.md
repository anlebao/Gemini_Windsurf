# TASK CARD G2 — SMART ENTRY LLM HYBRID: PARSER + LLM API FALLBACK

> Status: ✅ **IMPLEMENTED (2026-11-01, chờ deploy)** — PO duyệt "Duyệt + triển khai luôn" · parser fixes P0 DONE (word-boundary marker: "thanh toán…cho X" trích đúng đối tượng; từ-số tên Ba/Tư không còn thành amount; "ông Ba" giữ tên đầy đủ — 4 test mới) · `LlmEnhanceVoucherTextParseService` + `SmartEntryLlmOptions` (Gemini **3.8 Flash** free tier, trigger Unknown/Low/thiếu field, schema validate, merge rule-based-High thắng, ngày ±3 ngày, hướng transfer validate, fallback an toàn kèm warning "Đề xuất từ AI") · DI ShopERP + appsettings `SmartEntryLlm` (ApiKey rỗng = tắt) · tests **49/49** (parser 37 + LLM 12) — còn: guard/build/CD → RV regression production → bật flag pilot + key → RV LLM thật
> Nguồn: user hỏi "thêm LLM (Gemma 4 local) hay gọi API free?" → phân tích: VPS hiện tại e2-micro/e2-small (1-2GB RAM) KHÔNG chạy nổi Gemma 4 local (E2B quantized ~2.5GB) → chọn **Hybrid: giữ rule-based + LLM API (Gemini Flash free) làm fallback chỉ cho câu Unknown/Low**. Gemma 4 local (Apache 2.0) để dành khi có VPS riêng ≥8GB + yêu cầu privacy cứng.
> Liên quan: task G `task_pg_smart_entry_ai.md` (D2: "LLM fallback cắm sau qua interface, flag") · SRS §18.4.1 (không đoán bừa — LLM chỉ prefill, user Lưu)

---

## 0. VÌ SAO HYBRID (quyết định đã chốt)

- Rule-based 33/33 test + RV 10/10 production — giữ làm nền (deterministic, 0 chi phí, offline, privacy 100%).
- LLM chỉ xử lý **đuôi dài**: intent `Unknown` / confidence `Low` / thiếu field — nơi rule-based yếu (đã verify qua RV):
  1. `thanh toán nợ ... cho X` → mất đối tượng (bug parser)
  2. Tên trùng từ số (`ông Ba`=3đ, `tư vấn`=4) → amount sai + tên cắt
  3. Câu lạ: "tuần trước", "đầu tháng", câu dài nhiều ý, tiếng lóng của shop
- LLM chiếm ~vài % input → chi phí free tier thoải mái; **không auto-save** — mọi kết quả vẫn qua card + user bấm Lưu → worst case LLM sai = user sửa form, không ghi gì.

## 1. MỤC TIÊU

1. `LlmEnhanceVoucherTextParseService` — decorator `IVoucherTextParseService`: parser → nếu cần → gọi Gemini API → validate schema → merge kết quả.
2. Flag `SmartEntryLlmEnabled` per tenant (pattern `SmartEntryEnabled`) + env config `SmartEntryLlm__ApiKey`/`Model`/`TimeoutSec`.
3. Audit log mỗi lần dùng LLM (tenant, intent rule-based, intent LLM, có merge không — KHÔNG lưu prompt/description thô).
4. Fix nhanh 2 bug parser đã biết (P0, độc lập LLM).

## 2. KIẾN TRÚC

```
SmartEntryBar → IVoucherTextParseService (DI: LlmEnhanceVoucherTextParseService)
                   │
                   ├─ VoucherTextParseService (rule-based) — LUÔN chạy trước
                   │
                   └─ NẾU (flag ON && (Intent==Unknown || Confidence==Low || Missing non-empty))
                       └─ Gemini API generateContent (Flash free tier)
                            └─ JSON strict schema → validate từng field
                                 └─ merge: LLM lấp field thiếu/Unknown; field rule-based đã
                                    High KHÔNG bị đè (rule-based win khi chắc chắn)
```

- DI ShopERP Program.cs: `AddScoped<VoucherTextParseService>()` (concrete) + wrapper scoped đăng ký theo interface (1 dòng đổi khi chuyển local model sau này).
- KHÔNG endpoint HTTP mới; Blazor Server in-process như hiện tại.

## 3. PROMPT + SCHEMA

**Prompt (system):** "Bạn là trợ lý nhập liệu kế toán tiếng Việt. Từ câu mô tả nghiệp vụ, trả JSON ĐÚNG schema. Không thêm chú thích. Nếu không chắc chắn field nào → để null, KHÔNG bịa."

**JSON schema (mirror `ParsedVoucher`):**
```json
{
  "intent": "Revenue|Receivable|ReceivablePayment|Advance|Capital|Expense|Payable|PayablePayment|TransferOut|TransferIn|Purchase|null",
  "amount": 1000000,            // decimal — nếu không chắc → null
  "date": "2026-11-01",          // ISO — mặc định hôm nay
  "paymentMethod": "cash|bank|qr|null",
  "fromAccountCode": "112|null", "toAccountCode": "111|null",
  "doiTuong": "anh Hùng|null",
  "category": "rent|utilities|labor|materials|marketing|maintenance|null",
  "feeAmount": 30000, "description": "câu gốc"
}
```

**Validate (bất kỳ lỗi nào → bỏ field đó, giữ rule-based):**
- intent ∈ enum (mapping tên); amount > 0, ≤ 1e12; date parse được, ±3 ngày so với hôm nay cho phép chênh (LLM nhầm năm → chặn); category ∈ whitelist; from/to ∈ {111,112,113,*}; doiTuong ≤ 60 ký tự.
- Transfer: from ≠ to; đúng hướng (112→111 = TransferOut, 111→112 = TransferIn) — sai → bỏ.
- API error/timeout (mặc định 8s) → trả nguyên kết quả rule-based + warning "LLM tạm lỗi".

## 4. TRIGGER VÀ ƯU TIÊN (quyết định cần PO chốt)

| # | Câu hỏi | Đề xuất |
|---|---|---|
| Q1 | Gọi LLM khi nào? | Chỉ khi Unknown / Low / thiếu field — High rule-based không gọi (tiết kiệm + ổn định) |
| Q2 | LLM có đè được field rule-based High? | KHÔNG — rule-based High giữ nguyên; LLM chỉ lấp chỗ trống |
| Q3 | Confidence sau merge? | High nếu đủ field sau merge; gắn badge "AI" để user biết nguồn |
| Q4 | Privacy/opt-in? | Flag per-tenant; mô tả phiếu gửi ra ngoài chỉ khi flag ON (ghi rõ trong guide) |
| Q5 | Audit? | Ghi bảng audit: tenant, thời điểm, intent trước/sau, ok/fail — KHÔNG lưu text gốc |

## 5. FILES

- Tạo: `3_CoreHub/Services/LlmEnhanceVoucherTextParseService.cs` (+`SmartEntryLlmOptions`, schema validator) · `LlmParseAuditEntity`? → **không entity/migration** — dùng `SystemSetting`/log có sẵn? → audit qua `ILogger` + counter (quyết định Q5: defer bảng audit, dùng log cấu trúc) · tests `LlmEnhanceVoucherTextParseServiceTests`
- Sửa: `ShopERP/Program.cs` (DI + options) · `SmartEntryBar.razor` (badge "AI" khi merge LLM) · guide `smart-entry-user-guide.md` (mục LLM fallback + privacy) · task G status
- Không Domain/entity/migration mới.

## 6. P0 — FIX 2 BUG PARSER (kèm batch này, độc lập LLM)

1. `thanh toán ... cho X` mất đối tượng → fix `ExtractDoiTuong` (marker scan vùng trước/sau "cho"; test `thanh toán nợ 5tr ck cho nhà cung cấp Nam` → "nhà cung cấp Nam").
2. Tên trùng từ số (`ông Ba` → amount=3): token họ-tên sau marker không được quét thành amount — fix `ExtractAmounts` skip cụm sau ObjectMarker nếu nó nằm trong chuỗi tên (test `khách ông Ba trả nợ 2tr5` → 2.500.000 + "ông Ba").

## 7. ACCEPTANCE

- [ ] Flag OFF → hành vi y hệt hiện tại (0 cuộc gọi API, test regression)
- [ ] Flag ON + câu Unknown ("tuần trước chi tiền điện khoảng 2 triệu") → LLM → card prefill + badge AI + user Lưu
- [ ] LLM trả JSON sai schema → field bị loại, rule-based giữ nguyên, không crash
- [ ] API lỗi/timeout → trả rule-based + warning, không crash
- [ ] Rule-based High KHÔNG gọi API (assert 0 request qua mock)
- [ ] guard + sln build + Core.Tests mới (môck HttpClient) PASS
- [ ] RV production pilot 1 tenant: câu cũ vẫn đúng (regression) + 1 câu lạ đi LLM

## 8. DEPLOY/FLAG

- Push → CD → RV L1-L3 → bật `SmartEntryLlmEnabled` pilot Cafe Tân Quy (PG + SQLite mirror, pattern cũ) → verify → widen nếu OK.
- Key API: `SmartEntryLlm__ApiKey` qua docker compose env (KHÔNG commit, KHÔNG echo) — Google AI Studio free key.

## 9. QUYẾT ĐỊNH CUỐI (chờ PO)

| # | Quyết định | Đề xuất |
|---|---|---|
| D1 | LLM nào? | Gemini API — model Flash free tier hiện tại (tên model trong config, đổi được) |
| D2 | Chạy local Gemma 4? | DEFER — chỉ khi có VPS riêng ≥8GB + privacy bắt buộc (đổi 1 dòng DI) |
| D3 | Auto-save? | KHÔNG — giữ nguyên luật "user bấm Lưu" |
| D4 | Gửi dữ liệu ra ngoài? | Chỉ khi flag tenant ON; mô tả phiếu (không kèm PII khác) |
| D5 | Fix 2 bug parser trong batch? | YES — P0, có test riêng |