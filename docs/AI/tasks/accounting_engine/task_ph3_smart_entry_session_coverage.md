# TASK CARD PH3 — SMART ENTRY SESSION FIX + VOUCHER COVERAGE

> Status: ✅ **DONE + DEPLOYED `552ec611`→`f184d17d` (6 commits, 2026-11-03)** — CD Multi-VPS SUCCESS ×6 · guard ALL PASSED · sln build 0 errors · Core.Tests **2689/2689** + ShopERP.Tests **300/300** (gồm regression session bUnit) · **RV L1-L4 PASS**: L1 markers (DepreciationPostingStrategy/DepositPostingStrategy/JournalAdjustment) ✓ · L2 routes 302 (depreciation/deposit/journal-voucher) ✓ · L3 Playwright prod **6/6** (`ph3-smart-session-coverage-rv.spec.ts` — session multi parse→lưu thu→NavMenu Chi→multi còn + badge "✓ Đã lưu" · nộp thuế preview Nợ 333/Có 111 · kết chuyển→CTGS desc prefill · khấu hao Nợ 642/Có 214 · ký quỹ Nợ 338/Có 111) ✓ · L4 PG JE: khấu hao Nợ 642/Có 214 · ký quỹ Nợ 338/Có 111 ✓ · **6 fix RV:** `ab1d316c` (NotifySaved/ShowMulti fire-and-forget JS mất khi navigate) · `6bcd6357` (SPA-nav restore mất ProcessedIndices — root cause thật) · `08ca1af5` (depreciation account options sai — hiện TK vay 311/341, JE Nợ 341 sai → fix 6xx) · `f184d17d` (**deep-link `?desc=` multi → NRE `_smartEntryBar!` ở OnInitializedAsync → 500** — fix: pending mechanism, bar render sau first render) · spec resilient ×2. **Còn:** L5 user manual (§7 dưới — 5 bước).
> Root cause session verified (REVIEW 2026-11-03): G4f recovery full-reload re-parse LLM-dependent → collapse 1 phiếu → `vananClearSmartText()` wipe sessionStorage → multi mất + không hiển thị + bar ẩn (thiếu StateHasChanged)
> **100% = UI đầy đủ + menu 3 nơi + RV prod spec chạy được trên VPS như người dùng**

---

## 1. MỤC TIÊU
1. **Fix session multi**: khôi phục deterministic sau full reload (multi JSON + processed indices trong sessionStorage) · KHÔNG wipe khi collapse · single recovery hiển thị · bar không ẩn · `_savedIndices` giữ qua trang (chống áp lại phiếu đã lưu)
2. **Nhóm 3 (khẩn)**: category `tax`→333 · `interest`→635 (parser + SuggestExpenseAccount + wiring options/validation)
3. **CTGS**: intent `JournalAdjustment` → route link + prefill `?desc=` cho JournalVoucher
4. **Khấu hao + Ký quỹ**: 2 intent mới qua registry (generic page + strategy + menu 3 nơi)
5. **RV-ready**: spec prod `ph3-smart-session-coverage-rv.spec.ts` + config — chạy như người dùng

## 2. FILES
- **Tạo**: `3_CoreHub/Services/Vouchers/DepreciationPostingStrategy.cs` + `DepositPostingStrategy.cs` (hoặc thêm vào VoucherFramework.cs — nhất quán với PH2) · tests parser/strategy/registry + bUnit session · `6_Testing/e2e-tests/ph3-smart-session-coverage.spec.ts` + `ph3-smart-session-coverage-rv.spec.ts` + `playwright-rv-ph3.config.ts`
- **Sửa**: `App.razor` (JS session v2) · `SmartEntryBar.razor` (save multi JSON/indices + ShowMulti giữ indices + IntentLabel ×3) · `SmartEntrySessionState.cs` (+ProcessedIndices) · `RevenueEntry.razor` + `ExpenseEntry.razor` (recovery deterministic + no-wipe + options filter 333 + validation) · `VoucherTextParseService.cs` (categories tax/interest + intents JournalAdjustment/Depreciation/Deposit + classify order) · `EntryFormAccountingHelper.cs` (SuggestExpenseAccount) · `VoucherFramework.cs` (2 strategies) · `Program.cs` (registry: journal-adjustment + depreciation + deposit) · `LlmEnhanceVoucherTextParseService.cs` (IntentMap + prompt) · `JournalVoucher.razor` (desc param + prefill) · `ShopErpMenuService.cs` + `Sitemap.razor` + `AccountingIndex.razor` (menu 3 nơi) · `docs/guides/smart-entry-user-guide.md`
- **KHÔNG**: Domain entity, migration, DB (form thuần + strategy → CTGS/entry có sẵn)

## 3. CHI TIẾT

### P1 — Fix session multi (root cause verified)
- App.razor JS: `vananSaveSmartText(text, multiJson?, processedIndices?)` · `vananLoadSmartMulti()` · `vananLoadSmartProcessed()` — clear chỉ khi parse câu mới/hết phiếu chờ (giữ nguyên call-site cũ, bỏ qua trong collapse)
- SmartEntryBar: ParseAsync multi → save kèm JSON + indices; ShowMulti(list, text, savedIndices?) giữ `_savedIndices`; NotifySaved → cập nhật processed indices lên sessionStorage
- SmartEntrySessionState: + `List<int>? ProcessedIndices` (Set/Clear đồng bộ)
- Recovery ExpenseEntry/RevenueEntry OnAfterRenderAsync (ưu tiên): đọc `vananLoadSmartMulti` → `_pendingMulti` + `_pendingSavedIndices` + `_aiMode = true` + `StateHasChanged()` — không gọi parser; fallback text-only (dữ liệu cũ): re-parse KHÔNG wipe; single → `_result` + StateHasChanged
- bUnit: JSInterop mock `vananLoadSmartMulti` → bar 2 phiếu + item 0 "✓ Đã lưu" · `vananLoadSmartText` single → result hiện + sessionStorage không bị clear

### P2 — Category tax/interest (khẩn)
- ExpenseCategories: `["thuế","thuế gtgt","thuế tncn","thuế môn bài","thuế tndn"] → tax` · `["lãi vay","lãi ngân hàng","lãi tiền vay"] → interest`
- SuggestExpenseAccount: `"tax" => "333"` · `"interest" => "635"` (mọi loại hình)
- ExpenseEntry wiring: `LoadAccountOptionsAsync` filter `["6","333"]` · validation cho phép 333/333x khi category tax
- Tests: "nộp thuế 5 triệu" → category tax + account 333 · "trả lãi vay 5 triệu" → interest + 635 · lưu được (không bị chặn 6xx)

### P3 — CTGS (JournalAdjustment)
- enum + JournalAdjustment (cuối) · ClassifyIntent TRƯỚC Expense: `kết chuyển|phân bổ|điều chỉnh|trích trước|chuyển sổ|kết chuyển lãi lỗ` → JournalAdjustment
- Registry: `journal-adjustment` Route `/accounting/journal-voucher` HostPage=route strategy null → ApplyParse route link `?desc=`
- JournalVoucher.razor: + `[SupplyParameterFromQuery] desc` → `_description` + `_entryDate` prefill (KHÔNG auto dòng)
- LLM: IntentMap + prompt · IntentLabel "Phiếu hạch toán (CTGS)"

### P4 — Khấu hao + Ký quỹ (generic + strategy)
- enum + Depreciation, Deposit (cuối) · ClassifyIntent TRƯỚC Expense/Revenue: `khấu hao|trích khấu hao` · `ký quỹ|ký cược` (mode: `nhận|đặt` vs `hoàn trả|trả lại`)
- Registry: `depreciation` (Account TK chi phí 642/627/641 + party TSCĐ) · `deposit` (Select receive/return + party) — strategy: Nợ {TK}/Có 214 · Nợ {cash}/Có 338 / Nợ 338/Có {cash}
- VoucherEntry: `_mode` khởi tạo theo desc cho deposit (parser "hoàn trả" → return)
- LLM IntentMap + prompt · IntentLabel ×2
- **Menu 3 nơi**: ShopErpMenuService (+2) · Sitemap (`link-accounting-depreciation`/`link-accounting-deposit`) · AccountingIndex (+2 nút + NavigateTo*)

### P5 — Tests (TDD — viết trước)
- Parser: tax/interest · JournalAdjustment ×2 ("kết chuyển lãi lỗ cuối tháng", "phân bổ chi phí trả trước") · Depreciation · Deposit receive/return · regression full matrix
- Registry/Strategy: 3 intent resolve đúng route · DepreciationPostingStrategy Nợ 642/Có 214 · DepositPostingStrategy receive/return
- bUnit: session recovery (multi+indices · single no-wipe · bar hiện) · tax→333
- E2E local `ph3-smart-session-coverage.spec.ts` (flag-gated) · **RV prod** `ph3-smart-session-coverage-rv.spec.ts` + `playwright-rv-ph3.config.ts`: parse multi (tenant pilot LLM) → áp thu → Lưu → NavMenu Chi → multi còn + item thu đã lưu · nộp thuế preview Nợ 333 · kết chuyển → CTGS desc · khấu hao/ký quỹ tạo phiếu + JE
- Cập nhật `smart-entry-user-guide.md`

### P6 — Deploy + RV
- guard + sln build + Core.Tests/ShopERP.Tests → FAST PUSH → CD Multi-VPS
- L1 markers · L2 routes 302 (depreciation/deposit/journal-voucher) · L3 Playwright prod (session qua NavMenu — như người dùng) · L4 PG JE (642/214 · 111/338 · 338/111) · L5 user manual

## 4. ACCEPTANCE — 100%
- [ ] **Session**: multi khôi phục deterministic sau NavMenu (full reload) · item thu "✓ Đã lưu" · bar hiện · single không wipe · không áp lại phiếu đã lưu
- [ ] **Nhóm 3**: nộp thuế → 333 · lãi vay → 635 — preview + lưu OK
- [ ] **CTGS**: intent → route + prefill desc
- [ ] **Khấu hao/Ký quỹ**: registry + strategy + form generic (Account/Select) + JE đúng + menu 3 nơi + Smart Entry route
- [ ] guard + build + tests PASS · E2E local · CD + RV L1-L5

## 5. QUYẾT ĐỊNH (chốt)
| # | Q | Chốt |
|---|---|---|
| Q1 | Fix session | Đầy đủ 5 điểm (deterministic + no-wipe + single hiển thị + StateHasChanged + giữ indices) |
| Q2 | Nhóm 3 | tax→333 · interest→635 + wiring options/validation |
| Q3 | CTGS | JournalAdjustment → route link + prefill desc (không auto dòng) |
| D1 | Khấu hao | Generic `depreciation` — Nợ {TK chi phí}/Có 214 |
| D2 | Ký quỹ | Generic `deposit` — mode Nhận/Hoàn trả (338) |
| D3 | Menu | 2 phiếu mới vào 3 nơi |

## 6. DEFER (ghi nhận, không làm)
- Auto tạo dòng CTGS từ câu mô tả (không đoán TK điều chỉnh — unsafe)
- Ký quỹ theo dõi chi tiết từng đối tượng/khế ước (chỉ ghi 338 tổng)
- Khấu hao tự động định kỳ theo tháng (chỉ phiếu tay như hiện tại)
- "Cho vay" (LoanOut) — vẫn Unknown chặn an toàn

## 7. L5 USER MANUAL (ghi sau deploy)
1. Nhập Thu AI mode: gõ "thu tiền bán hàng 2 triệu và chi tiền điện 1 triệu" → Phân tích → 2 phiếu
2. Áp phiếu thu → Lưu → menu "Nhập Chi Phí" → **block trích xuất còn 2 phiếu, phiếu thu đánh dấu "✓ Đã lưu"**
3. Áp phiếu chi → Lưu → hết phiếu chờ → ô nhập sẵn sàng câu mới
4. "nộp thuế 5 triệu" → preview Nợ 333/Có 111 (không còn 621) · "trả lãi vay 5 triệu" → Nợ 635
5. "kết chuyển lãi lỗ cuối tháng" → nút mở Phiếu Hạch Toán CTGS (Diễn Giải điền sẵn) · "trích khấu hao TSCĐ 10 triệu" → Phiếu Khấu Hao · "nhận ký quỹ 5 triệu" → Phiếu Ký Quỹ (menu "Chứng Từ")