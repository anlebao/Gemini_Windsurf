# TASK CARD G — SMART ENTRY "MODE AI": NHẬP KẾ TOÁN BẰNG CÂU MÔ TẢ TỰ NHIÊN

> Status: ✅ **DONE + DEPLOYED `234d2425`** (2026-11-01) — CD Multi-VPS #41289880204 SUCCESS + CI SUCCESS + Prod Smoke SUCCESS · guard ALL PASSED (`guard-report-20261101-153159`) · sln build 0 errors · Core.Tests **2628/2648** (0 fail, 20 skip) · parser tests 33/33 · **flag `SmartEntryEnabled` widened ALL 142 tenants** (PG INSERT 142 + SQLite mirror shop-a 142 — từ keyset AccountMappingEnabled, TenantId=Guid.Empty) · **RV L1-L3 PASS**: L1 markers (voucher-speech.js + SmartEntryBar/TransferEntry/VoucherTextParse trong image `234d2425`) · L2 health 200 + `/accounting/transfers` 302→Login · L3 Playwright production **3/3** (`smart-entry-rv.spec.ts` — toggle render, parse "một trăm triệu"→112→111+prefill, route-link expense→transfers, deep-link ?desc=) · E2E spec local `e2e-tests/smart-entry-flow.spec.ts` (auto-skip khi flag OFF). **Note:** RV phát hiện amount format theo ambient culture (prod invariant → `100,000,000`; spec assert `/100[.,]000[.,]000/`) — platform-wide, KHÔNG phải bug feature này. **Follow-up G2+ (2026-11-01, deploy `4e3c17b3` + `dbbf0643`):** nút **"Phân tích lại"** + **"🔁 Nói lại"** + **"✨ AI phân tích"** (forceLlm — task G2) · fix **"Áp vào form" hiển thị field rỗng** (Blazor drop DOM sync qua component boundary DynamicFormFields → `vananSetElementValue` JS + await interop; chống click rớt bằng gate `data-blazor-interactive`) · full-matrix RV 10/10 + LLM RV 4/4 PASS (spec chuyển tenant Vạn An Trading cho regression rule-based; Cafe Tân Quy giữ LLM pilot). Còn: L4/L5 user manual · bUnit T6-T8 defer.
> Nguồn: user request 2026-11-01 — 2 mode: **Manual = giữ nguyên form hiện tại** · **AI = nói/gõ câu mô tả → trích xuất → prefill → user sửa → Lưu**
> Liên quan: SRS §18.1.2 (UX contract) · §18.4.1 (không đoán bừa) · FR-TRF (transfer chưa có UI)

---

## 0. UX CHANGE PROPOSAL (SRS §18.1.2 — bắt buộc trước implement)

| Mục | Nội dung |
|---|---|
| Lý do | User phải tự "dịch" ngôn ngữ đời → khái niệm kế toán (chọn loại phiếu, TK, PTTT). NL input giảm thao tác cho người không rành kế toán. |
| Không giải quyết được ở backend? | Giải quyết được — nhưng input của user là câu tự nhiên, cần 1 điểm nhập mới (không thay thế form). |
| Thao tác tăng thêm | **0 bắt buộc** — mode AI là opt-in (toggle). Manual mode giữ nguyên 100%. |
| Phương án ít tác động hơn | Đã xét: autocomplete đối tượng, template nhanh — kém hơn NL cho use-case "nói 1 câu". |
| Ảnh hưởng đào tạo | Không — Manual không đổi; AI mode có help text ví dụ. |
| Rollback | Feature flag `SmartEntryEnabled` per tenant (pattern `AccountMappingEnabled`) — OFF = form gốc. |

## 1. MỤC TIÊU

- Mode **Manual** (default): `RevenueEntry`/`ExpenseEntry` giữ nguyên như hiện tại.
- Mode **AI**: ô nhập câu mô tả (+ mic tuỳ chọn) → parser trích xuất → prefill đúng form/route → user chỉnh → **Lưu thủ công** (không auto-save).
- Đóng luôn gap **chuyển tiền nội bộ chưa có UI** (W3/D backend-only) — vì ví dụ đầu tiên của user là CK01.

## 2. KIẾN TRÚC

```
SmartEntryBar (textarea + mic + "Phân tích" + parse-result card)
   → IVoucherTextParseService.ParseAsync(text, tenantId)  [CoreHub — deterministic, in-process DI]
   → ParsedVoucher { Intent, Amount?, Date, PaymentMethod?,
                     FromAccountCode?, ToAccountCode?, DoiTuong?,
                     Category?, Description, Confidence, MissingFields[], Warnings[] }
   → ROUTE theo Intent:
       receipt-* / capital   → prefill RevenueEntry (set _voucherType + fields)
       expense-* / payable-* → prefill ExpenseEntry (+ category → SuggestExpenseAccount)
       transfer              → TransferEntry form MỚI (IInternalTransferService)
       ambiguous             → prefill tối đa + MissingFields highlight — KHÔNG đoán (§18.4.1.5)
   → user chỉnh → Lưu (mọi guard hiện có giữ nguyên: dup-5', balance-warn, period, preview JE)
```

**Mode toggle:** segmented control đầu trang "Nhập thủ công | ✨ Nhập bằng mô tả" — AI mode hiển thị SmartEntryBar trên cùng, form giữ nguyên bên dưới (đã prefill). Parse service inject trực tiếp (Blazor Server in-process — không cần endpoint HTTP).

## 3. `VoucherTextParseService` (rule-based v1 — KHÔNG LLM)

### 3.1. Số tiền tiếng Việt
`"một trăm triệu"` · `"100 triệu"` · `"100tr"` · `"100 củ"` · `"100k"` · `"1 tỷ"` · `"một tỷ rưỡi"` · `"hai mươi lăm nghìn"` · `"lẻ"` → decimal. Bảng từ: không..mười/trăm/nghìn/triệu/tỷ + viết tắt (k, tr, củ, lít, ngàn, chai). Ambiguous → MissingFields["amount"].

### 3.2. Ngày
`hôm nay` · `hôm qua` · `hôm kia` · `ngày DD/MM[/YYYY]` · `sáng/chiều nay` → DateTime; default = hôm nay.

### 3.3. Intent classifier (keyword tables, thứ tự ưu tiên transfer > công nợ > vốn/vay > thu/chi)

| Intent | Trigger ví dụ | Route |
|---|---|---|
| `transfer-out` | "rút tiền (gửi) ngân hàng về/nhập quỹ" · "chuyển từ TK .. sang/về quỹ" | TransferEntry from=112x to=111 |
| `transfer-in` | "nộp tiền mặt vào ngân hàng/tài khoản" | TransferEntry from=111 to=112x |
| `receivable-payment` | "khách (X) trả nợ/trả tiền hàng" · "thu nợ" | Phiếu thu loại thu-nợ + đối tượng |
| `receivable` | "bán chịu" · "khách nợ" | Phiếu thu ghi-nhận-phải-thu |
| `capital` | "góp vốn" · "chủ góp thêm vốn" | Phiếu thu góp-vốn (411) |
| `advance` | "khách ứng trước/đặt cọc" | Phiếu thu thu-nợ (đối tượng âm → trả trước) — cảnh báo confirm |
| `revenue` | "thu tiền bán hàng/dịch vụ" · "doanh thu" | Phiếu thu doanh thu |
| `payable-payment` | "trả (nợ) NCC/người bán" | Phiếu chi trả-người-bán |
| `payable` | "mua chịu (phi hàng)" | Phiếu chi ghi-nhận-phải-trả (+hint purchase-receipt) |
| `expense` | "chi/trả tiền thuê·điện·nước·lương·mặt bằng..." → category map | Phiếu chi + category + SuggestExpenseAccount |
| `purchase` | "nhập hàng/mua hàng hóa (chịu)" | hint → PurchaseReceipt (không prefill phiếu chi) |
| `unknown` | không khớp | MissingFields + user chọn tay |

### 3.4. Field extraction
- **PTTT:** "tiền mặt/quỹ" → cash · "chuyển khoản/ngân hàng/TK NH" → bank · "QR/ví" → qr.
- **Đối tượng:** cụm sau `khách|anh|chị|công ty|NCC|người bán|ông|bà` (stop tại giới từ số tiền/ngày).
- **TK tiền:** rút về quỹ → ToAccountCode="111"; "vào tài khoản/ngân hàng" → ToAccountCode = TK 112x active đầu tiên (`IBankAccountService.ResolveCashAccountCodeAsync` reuse).
- **Description:** câu gốc (giữ nguyên làm diễn giải — truy vết tốt).
- **Confidence:** High (intent+amount+date đủ) · Medium (thiếu 1 field) · Low (thiếu nhiều/intent yếu) → badge màu + MissingFields.

### 3.5. Không đoán (§18.4.1.5)
Thiếu amount/đối tượng bắt buộc/intent mơ hồ → để trống field đó + highlight + message, KHÔNG chọn TK/đối tượng mặc định tùy tiện.

## 4. UI

### 4.1. `SmartEntryBar` (component share — `Components/Accounting/`)
- Textarea + placeholder ví dụ theo trang · nút **Phân tích** · mic button (Web Speech API `vi-VN` qua `speechToText.js`, fallback ẩn khi browser không hỗ trợ).
- Parse-result card: bảng field đã trích xuất + badge confidence + list MissingFields + nút **Áp vào form**.
- Áp vào form = set `_voucherType` + `formData.Values` + field.Value + `RefreshPreviewAsync()` (JE preview hiển thị ngay — user thấy bút toán trước khi lưu).

### 4.2. `TransferEntry.razor` `/accounting/transfers` (MỚI — đóng gap W3)
- Fields: Từ TK (111 + các 112x active — `IBankAccountService.ListAsync`) · Đến TK · Số tiền · Phí (optional, JE riêng 642) · Ngày · Diễn giải · Tham chiếu NH · checkbox "Tiền đang chuyển" (InTransit qua 113).
- List transfers gần đây + action Receive/Cancel/Discrepancy/Reverse cho InTransit (backend đã có đủ API).
- Mode AI: intent transfer → prefill from/to/amount/fee/desc.

### 4.3. Tích hợp 2 form hiện tại
- Thêm toggle + `<SmartEntryBar Target="revenue|expense" OnParsed="ApplyParse" />` — **0 thay đổi** logic Manual (mode flag chỉ ẩn/hiện bar).
- `ApplyParse(ParsedVoucher)`: map Intent→`_voucherType`, set fields, `RebuildFields()` (giữ giá trị), auto `LoadOpenKhoanAsync` nếu có đối tượng + loại trả-nợ, `RefreshPreviewAsync`.

## 5. FILES TẠO/SỬA

- Tạo: `3_CoreHub/Services/VoucherTextParseService.cs` (+`IVoucherTextParseService`, `ParsedVoucher`) · `Components/Accounting/SmartEntryBar.razor` · `Pages/Accounting/TransferEntry.razor` · `wwwroot/js/speechToText.js` · tests `VoucherTextParseServiceTests` + `SmartEntryBarTests` + `TransferEntryTests`
- Sửa: `RevenueEntry.razor` + `ExpenseEntry.razor` (toggle + SmartEntryBar + ApplyParse) · `Program.cs` DI · `NavMenu`/Sitemap (`/accounting/transfers`) · feature flag `SmartEntryEnabled` (`IShopFeatureSettingsService`, default ON sau pilot)
- **Không** entity/migration/Domain mới — 100% service + UI.

## 6. ACCEPTANCE CRITERIA

- [ ] Mode Manual: 2 form y nguyên, regression tests pass — flag OFF = UI cũ
- [ ] "hôm nay rút tiền gửi ngân hàng một trăm triệu nhập vào quỹ tiền mặt" → intent `transfer-out`, amount 100.000.000, from=112x, to=111 → mở TransferEntry prefill → Lưu → JE Nợ111/Có112 + voucher CT (KHÔNG doanh thu/chi phí)
- [ ] "khách anh Hùng trả nợ 5 triệu bằng chuyển khoản" → receivable-payment + đối tượng "anh Hùng" + bank → auto-load khoản nợ
- [ ] "chi tiền thuê mặt bằng 15 triệu hôm qua" → expense + category rent + TK gợi ý + ngày hôm qua
- [ ] Câu thiếu số tiền/mơ hồ → prefill phần có + MissingFields highlight, không đoán
- [ ] Prefill xong vẫn qua đủ guard: preview JE, dup-check, balance-warn, Lưu thủ công
- [ ] Mic: hỗ trợ thì nút 🎤 hoạt động; không hỗ trợ → ẩn, text input vẫn dùng được
- [ ] guard + build + Core.Tests + bUnit PASS

## 7. TEST CASES (tối thiểu)

- T1 Amount matrix: 15 biến thể số (chữ/viết tắt/hỗn hợp) → decimal đúng · "lẻ năm" → 5.000.000 khi có "triệu" trước
- T2 Intent matrix: ≥20 câu đại diện mọi intent + 5 câu nhiễu (transfer-vs-thu, trả-nợ-vs-trả-NCC)
- T3 Date: hôm qua/kia + "ngày 25/10" + default hôm nay
- T4 Đối tượng: "khách anh Hùng" → "anh Hùng"; "công ty TNHH ABC" đủ tên; không có → MissingFields
- T5 Ambiguous: "chuyển tiền 2 triệu" (không rõ hướng) → MissingFields[from/to], KHÔNG đoán
- T6 ApplyParse RevenueEntry: set đúng voucherType + fields + preview JE render
- T7 TransferEntry: create → JE (to,from) · InTransit → Receive → JE (đích,113)
- T8 Flag OFF → SmartEntryBar không render

## G2+ (2026-11-01) — FIX DBCONTEXT CONCURRENCY TRÊN REVENUE/EXPENSE ENTRY

> Status: ✅ **IMPLEMENTED (2026-11-01)** — `await ApplyParse(parsed)` deep-link (Revenue+Expense) · `LoadTenantTypeAsync` retry 1 lần trên InvalidOperationException (tránh fallback regime DN sai) · build 0 errors — chờ deploy + RV log sạch.

### Symptom (production log, RV probe)
```
[14:51:34 ERR] A second operation was started on this context instance before a previous
operation completed (InvalidOperationException — DbContext dùng song song từ nhiều luồng)
   at VasFeatureFlagService.GetTenantTypeAsync(...) line 66
   at RevenueEntry.LoadTenantTypeAsync() line 379
[14:51:34 WRN] Không lấy được TenantType — dùng options DN mặc định
```
Hậu quả: tenant HKD/HTX đôi khi load trang với options **DN mặc định** (sai regime) → báo cáo/định khoản có thể lệch. Xác suất thấp nhưng nguy hiểm cho kế toán.

### Root cause (đã xác minh bằng đọc code)
1. **Fire-and-forget `ApplyParse(parsed)` trong nhánh deep-link `?desc=`** — `OnInitializedAsync` (RevenueEntry L298-300, ExpenseEntry L370-372) gọi `ApplyParse(parsed)` KHÔNG await. Từ G2+ (2026-11-01) ApplyParse là `async Task` + `await JSRuntime.InvokeVoidAsync(...)` + `_ = RefreshPreviewAsync()` → chạy **song song** với `await RefreshPreviewAsync()` cuối `OnInitializedAsync` → **2 EF query đồng thời trên cùng scoped DbContext** → InvalidOperationException.
2. **`LoadTenantTypeAsync` fallback ngay** khi gặp bất kỳ exception (kể cả transient concurrency) — không retry → tenant bị gán sai regime mặc định.
3. Nguồn race thứ hai có thể là EF từ layout/circuit khác — không xác định được từ log; fix theo hướng phòng thủ: loại fire-and-forget EF + retry.

### Solution (đã chốt)
1. **`await ApplyParse(parsed)`** trong OnInitializedAsync deep-link (cả RevenueEntry + ExpenseEntry) — tuần tự hoá init, hết race với RefreshPreviewAsync.
2. **`LoadTenantTypeAsync` retry 1 lần** khi gặp `InvalidOperationException` (concurrency transient) trước khi fallback DN — nếu retry thành công thì giữ regime đúng.
3. Giữ nguyên mọi await tuần tự khác (đã đúng).

### Acceptance
- [ ] Deep-link `?desc=` không còn log "A second operation was started"
- [ ] Không còn "Không lấy được TenantType — dùng options DN mặc định" khi load nhanh (retry cứu)
- [ ] RV production: tenant HKD (vd Cafe Tân Quy) load /accounting/revenue → regime đúng; deep-link parse + prefill vẫn OK
- [ ] guard + build + tests PASS

## 8. DECISIONS (chờ PO chốt)

| # | Câu hỏi | Đề xuất |
|---|---|---|
| D1 | Toggle trong 2 form (1 trang 2 mode) vs trang riêng? | Toggle trong form + TransferEntry riêng (transfer cần home riêng dù sao) |
| D2 | Parser rule-based v1, LLM fallback defer? | YES — deterministic, testable, zero cost; interface `IVoucherTextParseService` để LLM cắm sau (flag) |
| D3 | TransferEntry trong scope card này? | YES — bắt buộc để ví dụ CK01 của user chạy end-to-end |
| D4 | Mic (Web Speech API) trong v1? | Có — JS nhỏ, fallback graceful; không hỗ trợ → chỉ text |
| D5 | Auto-save sau prefill? | **KHÔNG** — luôn user xác nhận Lưu (tiền + kế toán) |
| D6 | Default mode khi vào trang? | Manual (AI opt-in qua toggle) — hoặc nhớ lựa chọn per user? |
