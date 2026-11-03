# MASTER PLAN — SMART ENTRY SESSION FIX + VOUCHER COVERAGE PH3

> Status: 📋 PLAN — user chốt 4 mục (2026-11-03) · chờ duyệt trước IMPLEMENT
> Root cause session bug đã verify (REVIEW 2026-11-03): tầng khôi phục "full reload" của G4f re-parse raw text qua LLM decorator (LLM-dependent) — khi collapse về 1 phiếu → `vananClearSmartText()` **wipe sessionStorage** → multi mất + không hiển thị kết quả + không `StateHasChanged` → bar ẩn. Kèm thiết kế: sessionStorage chỉ lưu raw text (không lưu multi JSON) + `ShowMulti` xoá `_savedIndices` → rủi ro áp lại phiếu đã lưu.
> Liên quan: task PH2 (`entry-form-voucher-coverage-fix-master-plan.md`) · task PG/G4 (`task_pg_smart_entry_ai.md`) · SRS `SRS_ShopERP_VoucherFramework_v1.0.md` · governance Task PH (registry = bắt buộc cho phiếu mới)

---

## ĐỊNH NGHĨA HOÀN THÀNH 100%
| # | Tiêu chí | Giao trong PH3 |
|---|---|---|
| ✅ UI | Session multi khôi phục deterministic sau full reload (không mất, không phụ thuộc LLM) · bar không ẩn · item đã lưu đánh dấu đúng · nộp thuế/lãi vay gợi ý TK đúng (333/635) · CTGS + Khấu hao + Ký quỹ có Smart Entry route + form đủ (preview, lưu, in nếu có) | P1-P4 |
| ✅ Menu | 2 phiếu mới (Khấu Hao, Ký Quỹ) ở đủ 3 nơi: NavMenu (`ShopErpMenuService`) + `Sitemap` + `AccountingIndex` quick actions | P4 |
| ✅ RV | Spec prod `ph3-smart-session-coverage-rv.spec.ts` + `playwright-rv-ph3.config.ts` chạy được trên VPS như người dùng (parse multi → lưu thu → sang chi → multi còn + item thu đã đánh dấu) | P5/P6 |

---

## QUYẾT ĐỊNH (user chốt + đề xuất D — chờ duyệt)

| # | Q | Chốt |
|---|---|---|
| Q1 | Fix session | **Đầy đủ 5 điểm** (review): lưu multi JSON + processed indices vào sessionStorage (deterministic) · KHÔNG wipe khi collapse · hiển thị single khi recovery · `StateHasChanged` đủ · giữ `_savedIndices` qua trang |
| Q2 | Nhóm 3 (khẩn) | Thêm category `tax`→**333** + `interest`→**635** vào `ExpenseCategories` + `SuggestExpenseAccount` + wiring cần thiết (options filter + validation cho 333) |
| Q3 | CTGS | Intent `JournalAdjustment` → route link + **prefill desc** (thêm `?desc=` cho `JournalVoucher.razor` — điền Diễn Giải + Ngày; KHÔNG auto tạo dòng CTGS — TK điều chỉnh không đoán an toàn) |
| D1 | Khấu hao | Intent `Depreciation` + phiếu generic `depreciation` (`/vouchers/depreciation`) — strategy Nợ {TK chi phí 642/627/641, user chọn}/Có 214 |
| D2 | Ký quỹ | 1 intent `Deposit` + phiếu generic `deposit` (`/vouchers/deposit`) — Select mode **Nhận** (Nợ {cash}/Có 338) / **Hoàn trả** (Nợ 338/Có {cash}) — pattern stock-issue; parser nhận diện "nhận/đặt" vs "hoàn trả/trả lại" set mode ban đầu |
| D3 | Menu | 2 phiếu mới vào 3 nơi (Task PH sync bắt buộc) |

---

## GIAI ĐOẠN

### P1 — Fix session multi (root cause đã verify)
- **JS (App.razor)**: `vananSaveSmartText(text, multiJson?, processedIndices?)` — lưu kèm multi JSON + indices; `vananLoadSmartMulti()` / `vananLoadSmartProcessed()`; giữ `vananLoadSmartText` (single/backward-compat); `vananClearSmartText` chỉ gọi khi parse câu mới/hết phiếu chờ (KHÔNG trong collapse)
- **SmartEntryBar**: `ParseAsync` multi → save cả JSON + indices; `ShowMulti(list, text, savedIndices?)` GIỮ `_savedIndices` (bỏ `Clear()`); `NotifySaved` cập nhật indices đã lưu lên sessionStorage
- **SmartEntrySessionState**: thêm `ProcessedIndices` (cùng vòng đời Set/Clear)
- **RevenueEntry + ExpenseEntry `OnAfterRenderAsync`** (tầng recovery full-reload): ưu tiên đọc multi JSON → `_pendingMulti` + `_pendingSavedIndices` + `_aiMode = true` + `StateHasChanged()` — **deterministic, không gọi LLM**; fallback cũ (re-parse) chỉ khi multi JSON vắng (dữ liệu cũ): **bỏ wipe**, single → hiển thị `_result` + `StateHasChanged()`
- Regression test (bUnit): ExpenseEntry recovery với JSInterop mock trả multi JSON → bar hiện 2 phiếu + item 0 "✓ Đã lưu" · single recovery → result hiển thị (không wipe)

### P2 — Expense category tax/interest (khẩn — fix định khoản gợi ý sai hiện hữu)
- `VoucherTextParseService.ExpenseCategories`: + `(["thuế", "thuế gtgt", "thuế tncn", "thuế môn bài", "thuế tndn"], "tax")` + `(["lãi vay", "lãi ngân hàng", "lãi tiền vay"], "interest")`
- `EntryFormAccountingHelper.SuggestExpenseAccount`: + `"tax" => "333"` + `"interest" => "635"` (mọi loại hình — 333/635 có trong chart HKD/DN/HTX; fallback cũ nếu chart tenant thiếu)
- Wiring ExpenseEntry (bắt buộc để gợi ý thực sự áp dụng được):
  - `LoadAccountOptionsAsync` filter `["6"]` → `["6", "333"]` (đưa 333/333x vào options)
  - `IsValidExpenseAccountCode` / validation HandleSubmit: cho phép `333`/`333x` khi `category == "tax"` (trước đây chặn "chi phí phải thuộc nhóm 6xx")
- Parser test: "nộp thuế 5 triệu" → Expense + category tax + account 333 · "trả lãi vay 5 triệu" → Expense + interest + 635

### P3 — CTGS Smart Entry (intent JournalAdjustment)
- `VoucherIntent` enum: + `JournalAdjustment` (cuối enum — additive)
- `ClassifyIntent`: `kết chuyển` / `phân bổ` / `điều chỉnh` / `trích trước` / `chuyển sổ` / `kết chuyển lãi lỗ` → **JournalAdjustment** (TRƯỚC nhánh Expense — "phân bổ chi phí trả trước" không được rơi vào Expense)
- Registry: `journal-adjustment` — Route `/accounting/journal-voucher`, HostPage = route (hosted — strategy null) → ApplyParse route link `?desc=`
- `JournalVoucher.razor`: + `[SupplyParameterFromQuery] desc` → prefill `_description` (+ `_entryDate` từ parser) — không auto dòng
- LLM: IntentMap `["kết chuyển","phân bổ","điều chỉnh","ctgs","chứng từ ghi sổ"] → JournalAdjustment` + prompt schema
- `IntentLabel`: "Phiếu hạch toán (CTGS)"

### P4 — Khấu hao + Ký quỹ (generic registry + strategy)
- `VoucherIntent` enum: + `Depreciation`, `Deposit` (cuối enum)
- `ClassifyIntent`: `khấu hao`/`trích khấu hao` → **Depreciation** · `ký quỹ`/`ký cược` → **Deposit** (TRƯỚC Expense/Revenue)
- Registry:
  - `depreciation` `/vouchers/depreciation` PrintTemplate null — fields: amount, date, account (Account — TK chi phí 627/641/642 từ COA ["6"], default 642), party(optional "Tên TSCĐ") — `DepreciationPostingStrategy`: Nợ {expenseAccount}/Có **214**
  - `deposit` `/vouchers/deposit` PrintTemplate null — fields: amount, date, mode (Select ["receive","return"]), party(optional đối tượng) — `DepositPostingStrategy`: receive Nợ {cash}/Có **338** · return Nợ 338/Có {cash}
  - VoucherEntry: `_mode` khởi tạo theo desc (parser "hoàn trả ký cược" → "return") — cải tiến nhỏ
- LLM: IntentMap `["khấu hao"] → Depreciation` · `["ký quỹ","ký cược"] → Deposit` + prompt schema
- `IntentLabel`: "Khấu hao TSCĐ" · "Ký quỹ / Ký cược"
- **Menu 3 nơi**: ShopErpMenuService + Sitemap (`link-accounting-depreciation`/`link-accounting-deposit`) + AccountingIndex quick actions + `NavigateTo*`

### P5 — Tests (TDD — viết trước)
- Parser: session-related không có — category tax/interest ×2 · JournalAdjustment ("kết chuyển lãi lỗ cuối tháng", "phân bổ chi phí trả trước") · Depreciation ("trích khấu hao TSCĐ 10 triệu") · Deposit ("nhận ký quỹ 5 triệu" / "hoàn trả ký cược 2 triệu") + regression toàn matrix
- Registry/Strategy: DepreciationPostingStrategy (Nợ 642/Có 214) · DepositPostingStrategy (receive/return) · ResolveByIntent 3 intent mới
- bUnit: session recovery (multi JSON + savedIndices · single không wipe · bar hiện) · ExpenseEntry category tax → account 333
- E2E local `ph3-smart-session-coverage.spec.ts` (flag-gated như pattern hiện có) + **RV prod** `ph3-smart-session-coverage-rv.spec.ts` + `playwright-rv-ph3.config.ts`:
  - "như người dùng": parse multi (LLM — tenant pilot) → áp thu → Lưu → NavMenu sang Chi → **multi còn + item thu "✓ Đã lưu"** · nộp thuế → preview Nợ 333/Có 111 · kết chuyển → route CTGS + desc prefill · khấu hao/ký quỹ: menu + tạo phiếu + JE
- Cập nhật `smart-entry-user-guide.md` (công thức mới)

### P6 — Deploy + RV (5 lớp, STOP at first failure)
- guard + sln build + Core.Tests/ShopERP.Tests → FAST PUSH → CD Multi-VPS
- L1 markers (strategies + intents trong DLL) · L2 routes 302 (depreciation/deposit/journal-voucher) · L3 Playwright prod spec (như người dùng — session qua NavMenu) · L4 PG JE (khấu hao Nợ 642/Có 214 · ký quỹ Nợ 111/Có 338) · L5 user manual (ghi cuối task card)

---

## RỦI RO & KIỂM SOÁT
| Rủi ro | Giảm thiểu |
|---|---|
| Fix session phá flow SPA-nav đang chạy (tầng 1) | Chỉ đổi tầng 2 (recovery) + serialization; tầng 1 (SmartSessionState) giữ nguyên; bUnit regression |
| Wipe bỏ → sessionStorage cũ mắc kẹt (text cũ hiện lại) | Clear chỉ khi: parse câu mới · hết phiếu chờ · user xoá input — giữ đúng chỗ cũ đã định |
| `tax→333` gây validation chặn lưu (6xx check) | Wiring P2: options filter + validation allowance kèm theo — không chỉ 2 dòng category |
| JournalAdjustment "điều chỉnh" đụng từ khóa khác ("điều chỉnh tăng giá bán"?) | Keyword guard: `điều chỉnh`/`phân bổ`/`kết chuyển` kèm ngữ cảnh kế toán; test regression |
| Khấu hao/ký quỹ TK không có trong chart tenant | Strategy fail an toàn + account select từ COA (pattern loan PH2) |
| Multi JSON version cũ (text-only) khi deploy | Fallback re-parse cũ GIỮ LẠI (không xoá) — backward-compat, chỉ bỏ wipe |

---

## ACCEPTANCE CHUNG — 100%
- [ ] **Session**: lưu phiếu thu (multi) → NavMenu sang Chi → multi còn nguyên + item thu "✓ Đã lưu" · bar hiện · deterministic (không phụ thuộc LLM) · single recovery hiển thị kết quả, không wipe
- [ ] **Nhóm 3**: "nộp thuế 5 triệu" → preview **Nợ 333/Có 111** (không 621) · "trả lãi vay 5 triệu" → **Nợ 635/Có 111** (không 621) — lưu được (validation cho 333)
- [ ] **CTGS**: "kết chuyển lãi lỗ cuối tháng" → route `/accounting/journal-voucher?desc=` + Diễn Giải prefill
- [ ] **Khấu hao + Ký quỹ**: intent mới → phiếu generic render (Account/Select) + strategy JE đúng + menu 3 nơi + Smart Entry route
- [ ] guard + build + Core.Tests/ShopERP.Tests PASS · E2E local PASS · CD + RV production L1-L5