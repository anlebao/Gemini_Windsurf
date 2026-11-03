# MASTER PLAN — ENTRY FORM VOUCHER COVERAGE & SMART-APPLY FIX (PH2)

> Status: 📋 PLAN — user chốt Q1-Q3 (2026-11-03) · chờ duyệt trước khi IMPLEMENT
> Nguồn: user report — "2 form nhập doanh thu/chi phí không bao phủ đủ nghiệp vụ kế toán; 'Áp vào form' không nhận diện đúng loại phiếu → định khoản sai"
> Root cause đã verify (báo cáo điều tra 2026-11-03): dropdown loại phiếu **hardcode** (4+3 options, không registry-driven) · `ApplyParse` **hardcode switch + catch-all `_ =>`** (Unknown âm thầm thành Thu doanh thu/Chi phí → định khoản sai) · intent `Advance` ép vào "Thu tiền khách trả nợ" (label lệch) · nghiệp vụ vay/trả nợ vay → Unknown (không có phiếu) · parser edge: "vay ... nhập quỹ" bị transfer detection nuốt
> Liên quan: `voucher-framework-master-plan.md` (G1-G3) · SRS `docs/specs/SRS_ShopERP_VoucherFramework_v1.0.md` · governance Task PH (registry = bắt buộc cho mọi phiếu mới)

---

## ĐỊNH NGHĨA HOÀN THÀNH 100% (user yêu cầu — không chỉ API/service)

| # | Tiêu chí | Giao trong PH2 |
|---|---|---|
| ✅ UI | Đầy đủ UI end-to-end cho mọi nghiệp vụ trong phạm vi | Dropdown 2 form registry-driven · Unknown block UI · Advance alert · 2 phiếu Vay/Trả nợ vay render + lưu + **JE preview + in phiếu** · form sạch sau lưu |
| ✅ Menu | Link menu sẵn sàng — đủ 3 nơi | `ShopErpMenuService.cs` (NavMenu) + `Sitemap.razor` (data-testid `link-accounting-loan-*`) + `AccountingIndex.razor` (quick actions "Chứng Từ / Phiếu" + `NavigateTo*`) |
| ✅ RV | RV test chạy được trên VPS **như người dùng** | Spec prod `ph2-voucher-coverage-rv.spec.ts` + `playwright-rv-ph2.config.ts` + `rv-ph2-auth.cjs` (login tenant thật → menu → tạo phiếu → verify JE) + L5 user manual steps |

---

## QUYẾT ĐỊNH (user chốt 2026-11-03)

| # | Q | Chốt |
|---|---|---|
| Q1 | Phạm vi | **FULL** — thêm phiếu **Vay nhận tiền** (`loan-in`, Có 341) + **Trả nợ vay** (`loan-repayment`, Nợ 341) qua registry |
| Q2 | Intent Unknown khi bấm "Áp vào form" | **Chặn cứng** — không điền form, báo "Không nhận diện được loại phiếu — nhập thủ công" (không đoán bừa, SRS §18.4.1.5) |
| Q3 | Intent Advance (khách ứng trước/cọc) | **Giữ mapping** vào "Thu tiền khách trả nợ" (Nợ {cash}/Có 131 + FIFO) — chỉ thêm cảnh báo rõ ràng sau apply |
| D4 | Vay/Trả nợ vay host ở đâu | **Generic page** `/vouchers/loan-in` + `/vouchers/loan-repayment` (pattern advance/stock-issue — strategy C# test được, KHÔNG phình 2 form) |

---

## GIAI ĐOẠN

### P1 — Registry = single source of truth (intent → phiếu + dropdown)
- `VoucherTypeRegistry.Register` → strategy **nullable** (`IVoucherPostingStrategy?`) — additive, hosted types không có strategy riêng
- `VoucherTypeDefinition` thêm `string? HostPage` (null = generic page `/vouchers/{code}`; "revenue"/"expenses"/"transfers" = trang host)
- Đăng ký **definition có sẵn** (KHÔNG migrate posting — G3 master plan cũ defer):
  - Host `/accounting/revenue`: `revenue` (Thu doanh thu, intent Revenue) · `receivable` (Ghi nhận phải thu, Receivable) · `receivable-payment` (Thu tiền khách trả nợ, ReceivablePayment + **Advance** — Q3) · `capital` (Góp vốn chủ sở hữu, Capital)
  - Host `/accounting/expenses`: `expense` (Chi phí, Expense) · `payable` (Ghi nhận phải trả, Payable) · `payable-payment` (Trả tiền người bán, PayablePayment)
  - Host `/accounting/transfers`: `transfer-in`/`transfer-out` (TransferIn/TransferOut) · route-only: `purchase` (Purchase → `/accounting/purchase-receipts/new`)
  - Giữ nguyên 3 phiếu generic đã có: `advance`/`stock-issue`/`asset-disposal`
- **Mới**: `loan-in` (Phiếu Vay Nhận Tiền, intent Loan) + `loan-repayment` (Phiếu Trả Nợ Vay, intent LoanRepayment) + `LoanPostingStrategy`
- Helper mới `GetOptionsForHost(string hostPage)` — dropdown 2 form sinh từ registry (bỏ `GetVoucherTypeOptions()` hardcode)
- `VoucherEntry.razor`: `HostPage != null` → hiện "Loại phiếu này nhập tại trang X" (chặn submit sai đường qua `/vouchers/{code}`)

### P2 — ApplyParse refactor (RevenueEntry + ExpenseEntry) — UI states
- Bỏ switch hardcode intent→route + intent→voucherType → dùng `VoucherRegistry.ResolveByIntent(p.Intent)`:
  - `null` (Unknown) → **chặn cứng**: `_applyError` state + **VanAAlert đỏ trên form** "Không nhận diện được loại phiếu — nhập thủ công", KHÔNG điền form (Q2)
  - `def.Route != trang hiện tại` → route link `?desc=` (pattern cũ, giữ nguyên UX)
  - `def.Route == trang hiện tại` → set `_voucherType = def.Code` + prefill như cũ
- Q3: intent Advance sau apply → **VanAAlert warning state**: *"Khách ứng trước/cọc — ghi trả trước (Có 131), không phải doanh thu. Loại phiếu 'Thu tiền khách trả nợ' + FIFO."*

### P3 — Parser: intents Loan/LoanRepayment + thứ tự classify
- `VoucherIntent` enum: thêm `Loan`, `LoanRepayment` (thêm CUỐI — không đổi giá trị cũ, an toàn serialization)
- `ClassifyIntent` — thứ tự mới (quan trọng):
  1. **Loan TRƯỚC transfer**: `norm.Contains("vay")` (guard: loại `cho vay` → Unknown-defer · `lãi vay`/`trả lãi vay` → **Expense** 635) → fix "vay ngân hàng 100 triệu nhập quỹ" bị thành TransferOut
  2. **LoanRepayment TRƯỚC công nợ**: `trả nợ vay`/`trả tiền vay`/`hoàn trả vay`/`trả nợ ... vay` → fix "trả nợ vay 20 triệu" bị thành PayablePayment
  3. Transfer / công nợ / purchase / vốn / advance / tạm ứng / xuất kho / thanh lý / chi / thu — **giữ nguyên**
- MissingFields: Loan/LoanRepayment không yêu cầu đối tượng

### P4 — Generic page UI hoàn chỉnh + LLM + MENU SYNC (3 nơi)
- `LoanPostingStrategy` (`3_CoreHub/Services/Vouchers/`): loan-in Nợ {cash}/Có {TK vay} · loan-repayment Nợ {TK vay}/Có {cash} — TK vay từ `VoucherSubmission.ToAccountCode`/`FromAccountCode` (reuse field có sẵn, không phá record)
- `VoucherEntry.razor` renderer hoàn chỉnh:
  - Thêm nhánh `VoucherFieldType.Account` — **select COA tenant** (311/341 + chi tiết, inject `IAccountChartService`/`ITenantProvider`) — bắt buộc cho loan
  - `VoucherFieldType.Select` bỏ hardcode production/sale → `Options` configurable (additive, stock-issue giữ default)
  - **JE preview** trên form (giống ExpenseEntry `journal-preview`) — strategy chạy khi đổi field → user thấy bút toán trước Lưu
  - **In phiếu sau lưu**: modal in 01-TT (loan-in) / 02-TT (loan-repayment) — reuse `Tt71ReceiptVoucher`/`Tt71PaymentVoucher` (đóng luôn gap in cho 3 phiếu generic cũ advance/stock-issue/asset-disposal)
- **Menu sync 3 nơi (bắt buộc — user yêu cầu link sẵn sàng):**
  1. `ShopErpMenuService.cs` nhóm Kế Toán: + "Phiếu Vay Nhận Tiền" (`/vouchers/loan-in`) + "Phiếu Trả Nợ Vay" (`/vouchers/loan-repayment`)
  2. `Sitemap.razor`: + link `data-testid="link-accounting-loan-in"` + `link-accounting-loan-repayment`
  3. `AccountingIndex.razor` quick actions "Chứng Từ / Phiếu": + nút "🏦 Vay Nhận Tiền" + "🏦 Trả Nợ Vay" + `NavigateToLoanIn`/`NavigateToLoanRepayment`
- `LlmEnhanceVoucherTextParseService`: `IntentMap` thêm `["vay"]/["vay nhận tiền"] → Loan` + `["trả nợ vay"]/["trả tiền vay"] → LoanRepayment` · schema prompt `BuildPrompt`/`BuildMultiPrompt` thêm `Loan|LoanRepayment`
- `SmartEntryBar.IntentLabel`: Loan → "Vay nhận tiền" · LoanRepayment → "Trả nợ vay"

### P5 — Tests (TDD — viết trước, theo thứ tự)
- Parser (`VoucherTextParseServiceTests`): "vay ngân hàng 100 triệu nhập quỹ" → **Loan** (không TransferOut) · "vay vốn 50 triệu" → Loan · "trả nợ vay 20 triệu" → **LoanRepayment** (không PayablePayment) · "trả lãi vay 5 triệu" → Expense · "cho công ty X vay 100 triệu" → Unknown (chặn an toàn, defer) · regression: "khách trả nợ 5 triệu" → ReceivablePayment · "rút tiền ngân hàng về quỹ" → TransferOut · "đặt cọc" → Advance
- Registry (`VoucherFrameworkTests`): `ResolveByIntent` mọi intent (trừ Unknown) → non-null + Route đúng · `GetOptionsForHost` revenue = 4 options / expenses = 3
- Strategy (`LoanPostingStrategyTests`): loan-in Nợ 111/Có 341 · repayment Nợ 341/Có 111 · cash account theo paymentMethod
- **UI/bUnit**: dropdown registry-driven · Unknown không prefill + alert hiện · Advance alert hiện · VoucherEntry Account select render + preview
- **E2E local** (`6_Testing/e2e-tests/ph2-voucher-coverage.spec.ts` — testDir local, flag OFF auto-skip pattern): menu 3 nơi hiển thị · loan tạo phiếu · Unknown chặn · Advance alert
- **RV production spec sẵn sàng** (chạy trên VPS như người dùng):
  - `6_Testing/e2e-tests/ph2-voucher-coverage-rv.spec.ts` + `6_Testing/playwright-rv-ph2.config.ts` (baseURL app2 + host-resolver — pattern `playwright-rv-voucher.config.ts`) + `6_Testing/rv-ph2-auth.cjs` (storageState login tenant thật)
  - Flow "như người dùng": login → menu Kế Toán thấy 2 link mới → mở `/vouchers/loan-in` → nhập TK vay + tiền → **preview Nợ 111/Có 341** → Lưu → success → **verify JE trong Sổ Nhật ký chung** · Smart Entry "vay ngân hàng 100 triệu nhập quỹ" → route đúng phiếu · Unknown → alert chặn · đặt cọc → alert Advance

### P6 — Deploy + RV (5 lớp, STOP at first failure)
- guard-check.ps1 + `dotnet build VanAn.sln` + Core.Tests/ShopERP.Tests → FAST PUSH → CD Multi-VPS
- **L1** markers production: `LoanPostingStrategy` + `Loan/LoanRepayment` trong CoreHub.dll · ShopERP DLL (`ph2` code + menu strings)
- **L2** health 200 + routes `/vouchers/loan-in` · `/vouchers/loan-repayment` · `/accounting/revenue` · `/accounting/expenses` → 302→Login
- **L3** Playwright production `ph2-voucher-coverage-rv.spec.ts` (config `playwright-rv-ph2`) — chạy như người dùng (login tenant → menu → tạo phiếu → JE)
- **L4** API/sổ: JE loan thật trong PG (ReferenceType JournalVoucher, lines 111/341) sau khi tạo qua UI
- **L5** user manual: hướng dẫn 5 bước (đăng nhập app2 → menu → tạo phiếu vay → kiểm tra sổ → smart entry test) — ghi vào task card + cập nhật `smart-entry-user-guide.md` nếu cần

---

## RỦI RO & KIỂM SOÁT

| Rủi ro | Giảm thiểu |
|---|---|
| Hosted types trong registry → user mở nhầm `/vouchers/{code}` | `HostPage` guard trong VoucherEntry (thông báo + chặn submit) |
| Regression transfer/công nợ khi đổi thứ tự classify | Loan/LoanRepayment check chỉ THÊM trước 2 nhánh cũ; full parser tests regression (33 cũ + mới) |
| "cho vay" (cho vay tiền) chưa có phiếu | Để Unknown → chặn cứng an toàn (không đoán); ghi nhận DEFER — thêm `LoanOut` sau nếu cần |
| Advance label vẫn lệch so với badge intent | Alert rõ ràng sau apply (Q3) — không đổi posting |
| Generic page thiếu preview/in (gap PH G1 cũ) | P4 đóng gap: JE preview + in 01-TT/02-TT cho mọi phiếu generic (lợi ích cả advance/stock-issue/asset-disposal) |
| Dropdown registry làm đổi thứ tự/hành vi form cũ | Giữ thứ tự Register = thứ tự options hiện tại; E2E regression full-matrix |
| Menu thêm 3 nơi lệch nhau (thiếu 1 nơi) | Acceptance checklist riêng cho từng file (ShopErpMenuService/Sitemap/AccountingIndex) + E2E assert cả 3 |

---

## ACCEPTANCE CHUNG — 100% (UI + Menu + RV-ready)
- [ ] **UI**: Mọi intent (trừ Unknown) → đúng phiếu/route; Unknown chặn cứng + alert; Advance alert; 2 phiếu vay đủ form (Account select, preview, lưu, in, form sạch)
- [ ] **Menu**: NavMenu + Sitemap + AccountingIndex quick actions — cả 3 có link phiếu vay (E2E assert)
- [ ] **Parser**: "vay ngân hàng 100 triệu nhập quỹ" → Loan · "trả nợ vay 20 triệu" → LoanRepayment (không lệch transfer/công nợ)
- [ ] **Registry**: Dropdown 2 form sinh từ registry — hết hardcode · phiếu vay qua registry + strategy (Task PH compliance)
- [ ] **RV-ready**: `ph2-voucher-coverage-rv.spec.ts` + config + auth nằm trong repo, chạy được trên VPS như người dùng (L3) — không chỉ API
- [ ] guard + build + Core.Tests/ShopERP.Tests PASS · E2E local PASS · CD + RV production L1-L5