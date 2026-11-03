# TASK CARD PH2 — ENTRY FORM VOUCHER COVERAGE & SMART-APPLY FIX

> Status: ✅ **DONE + DEPLOYED `e7536810`+`e0dca5f7`+`1cc3d014` (2026-11-03)** — CD Multi-VPS SUCCESS ×3 · guard ALL PASSED · sln build 0 errors · Core.Tests **2678/2678** + ShopERP.Tests **283/283** (fix 21 pre-existing bUnit) · **RV L1-L4 PASS**: L1 CoreHub markers LoanPostingStrategy/LoanRepayment ✓ · L2 routes 302 (loan-in/loan-repayment/5 generic) ✓ · L3 Playwright prod **7/7** (`ph2-voucher-coverage-rv.spec.ts` — menu 3 nơi · tạo phiếu vay preview Nợ 111/Có 341 → lưu → success · trả nợ vay · HostPage guard · smart vay → route · Unknown chặn · đặt cọc alert) ✓ · L4 PG JE `J20261103-8AC8AD59` + `J20261103-042B9CE2` Nợ 111/Có 341 ✓ · **2 fix RV:** prerender KeyNotFound `e0dca5f7` + sau-save circuit chết `1cc3d014` (latent bug PH G1 — sweep cũ chỉ render read-only). **Còn:** L5 user manual (§7 dưới — 5 bước).
> Root cause verified: dropdown hardcode + `ApplyParse` catch-all `_ =>` (Unknown → Thu/Chi = định khoản sai) + Advance ép "thu nợ" + thiếu phiếu Vay/Trả nợ vay
> **Hoàn thành 100% = UI đầy đủ + menu link 3 nơi + RV test chạy được trên VPS như người dùng — KHÔNG chỉ API/service**

---

## 1. MỤC TIÊU
1. **Registry = single source of truth** cho intent→phiếu + dropdown 2 form (bỏ hardcode `GetVoucherTypeOptions`)
2. **`ApplyParse` refactor** (Revenue/Expense): `ResolveByIntent` thay switch hardcode · **Unknown → chặn cứng** (alert đỏ, không điền form)
3. **Parser**: intents `Loan`/`LoanRepayment` + thứ tự classify (Loan trước transfer · LoanRepayment trước công nợ)
4. **Phiếu mới**: `loan-in` (Nợ {cash}/Có 341) + `loan-repayment` (Nợ 341/Có {cash}) — UI generic page **hoàn chỉnh**: Account select COA + JE preview + in 01-TT/02-TT + lưu/success/form sạch
5. **Menu sync 3 nơi**: NavMenu (`ShopErpMenuService`) + `Sitemap` + `AccountingIndex` quick actions — link sẵn sàng cho RV
6. **Q3**: Advance giữ mapping "Thu tiền khách trả nợ" + FIFO — thêm alert cảnh báo sau apply
7. **RV-ready**: spec prod `ph2-voucher-coverage-rv.spec.ts` + config + auth — chạy trên VPS như người dùng

## 2. FILES
- **Tạo**: `3_CoreHub/Services/Vouchers/LoanPostingStrategy.cs` · tests `LoanPostingStrategyTests.cs` (+ mở rộng `VoucherTextParseServiceTests`/`VoucherFrameworkTests`) · `6_Testing/e2e-tests/ph2-voucher-coverage.spec.ts` (local) · `6_Testing/e2e-tests/ph2-voucher-coverage-rv.spec.ts` + `6_Testing/playwright-rv-ph2.config.ts` + `6_Testing/rv-ph2-auth.cjs` (RV prod)
- **Sửa**: `VoucherFramework.cs` (Register strategy nullable + `HostPage` + `VoucherField.Options`) · `VoucherTextParseService.cs` (enum + classify order) · `LlmEnhanceVoucherTextParseService.cs` (IntentMap + prompt schema) · `Program.cs` (register hosted + loan types) · `RevenueEntry.razor` · `ExpenseEntry.razor` (ApplyParse + dropdown registry + Unknown/Advance alert) · `SmartEntryBar.razor` (IntentLabel) · `VoucherEntry.razor` (HostPage guard + Account/Select renderer + **JE preview + in phiếu**) · **`ShopErpMenuService.cs` · `Sitemap.razor` · `AccountingIndex.razor`** (menu 3 nơi)
- **KHÔNG**: Domain, migration, DB (form thuần + strategy → CTGS qua `JournalVoucherService` có sẵn)

## 3. CHI TIẾT

### P1 — Registry alignment
- `Register(...)` signature: `IVoucherPostingStrategy? strategy` (hosted types = null) — additive
- `VoucherTypeDefinition` + `string? HostPage` ("revenue" | "expenses" | "transfers" | null=generic)
- Đăng ký definition có sẵn (route/host/intent): revenue·receivable·receivable-payment(+Advance)·capital → `/accounting/revenue` · expense·payable·payable-payment → `/accounting/expenses` · transfer-in/out → `/accounting/transfers` · purchase → route-only · advance/stock-issue/asset-disposal giữ nguyên
- `GetOptionsForHost(hostPage)` → dropdown options (thứ tự = thứ tự Register — giữ hành vi form cũ)
- `VoucherEntry`: `def.HostPage != null` → thông báo "Loại phiếu này nhập tại trang ..." + chặn submit

### P2 — ApplyParse refactor (2 trang) + UI states
```
var def = VoucherRegistry.ResolveByIntent(p.Intent);
if (def == null) { _applyError = "Không nhận diện được loại phiếu — nhập thủ công."; return; }  // Q2 — VanAAlert đỏ
if (def.Route != "/accounting/revenue") { _smartRouteUrl = def.Route + "?desc=" + ...; return; } // route link
_voucherType = def.Code; // prefill như cũ (amount/date/description/doiTuong/paymentMethod + JS push)
```
- Revenue: sau apply + `p.Intent == Advance` → VanAAlert warning: "Khách ứng trước/cọc — ghi trả trước (Có 131), không phải doanh thu. Loại phiếu 'Thu tiền khách trả nợ' + FIFO."
- Bỏ 2 switch hardcode (route + voucherType) ở cả 2 trang

### P3 — Parser
- enum: thêm `Loan`, `LoanRepayment` cuối enum (additive)
- ClassifyIntent:
  1. ĐẦU hàm: `lãi vay`/`trả lãi vay` → **Expense** · `vay` && !`cho vay` → **Loan**
  2. `trả nợ vay`/`trả tiền vay`/`hoàn trả vay`/`trả nợ ... vay` → **LoanRepayment** (TRƯỚC nhánh công nợ `traNo`)
  3. Phần còn lại giữ nguyên (transfer vẫn sau Loan — fix "vay ... nhập quỹ" thành Loan)
- MissingFields: Loan/LoanRepayment không đòi đối tượng

### P4 — Generic page UI + LLM + MENU SYNC (3 nơi)
- `LoanPostingStrategy`: đọc `s.ToAccountCode` (loan-in) / `s.FromAccountCode` (loan-repayment) = TK vay (311/341 + chi tiết COA) · `s.CashAccount` theo paymentMethod · lines: loan-in `Nợ {cash}/Có {TK}` · loan-repayment `Nợ {TK}/Có {cash}`
- `VoucherEntry` renderer hoàn chỉnh:
  - nhánh `VoucherFieldType.Account` — **select COA tenant** (inject `IAccountChartService`/`ITenantProvider`)
  - `VoucherFieldType.Select` dùng `Options` configurable (stock-issue giữ production/sale)
  - **JE preview** (`journal-preview` — strategy khi đổi field)
  - **In phiếu**: modal 01-TT (loan-in) / 02-TT (loan-repayment) — reuse `Tt71ReceiptVoucher`/`Tt71PaymentVoucher` (đóng gap in cho advance/stock-issue/asset-disposal luôn)
- LLM: `IntentMap` + VN alias vay/trả nợ vay · prompt schema + `Loan|LoanRepayment`
- `IntentLabel`: Loan → "Vay nhận tiền" · LoanRepayment → "Trả nợ vay"
- **Menu 3 nơi (RV-ready):**
  1. `ShopErpMenuService.cs` nhóm Kế Toán: + "Phiếu Vay Nhận Tiền" `/vouchers/loan-in` + "Phiếu Trả Nợ Vay" `/vouchers/loan-repayment`
  2. `Sitemap.razor`: + `link-accounting-loan-in` + `link-accounting-loan-repayment`
  3. `AccountingIndex.razor` "Chứng Từ / Phiếu": + nút Vay Nhận Tiền + Trả Nợ Vay + `NavigateToLoanIn`/`NavigateToLoanRepayment`

### P5 — Tests (TDD — viết trước code)
- Parser: 6 case mới (Loan ×2 · LoanRepayment · lãi vay→Expense · cho vay→Unknown · regression TransferOut/ReceivablePayment/Advance)
- Registry: ResolveByIntent all intents (trừ Unknown) non-null + route đúng · GetOptionsForHost revenue=4/expenses=3
- Strategy: loan-in/loan-repayment lines + cash theo method
- bUnit: dropdown registry · Unknown chặn + alert · Advance alert · VoucherEntry Account select + preview
- **E2E local** `ph2-voucher-coverage.spec.ts`: menu 3 nơi assert · tạo phiếu vay · Unknown chặn · Advance alert
- **RV prod** `ph2-voucher-coverage-rv.spec.ts` + `playwright-rv-ph2.config.ts` (baseURL app2 + host-resolver — pattern `playwright-rv-voucher.config.ts`) + `rv-ph2-auth.cjs`:
  - Login tenant thật → menu Kế Toán thấy 2 link → mở `/vouchers/loan-in` → TK vay + tiền → **preview Nợ 111/Có 341** → Lưu → success → **verify JE trong Sổ Nhật ký chung**
  - Smart Entry "vay ngân hàng 100 triệu nhập quỹ" → route đúng phiếu · Unknown → alert chặn · "đặt cọc" → alert Advance

### P6 — Deploy + RV (5 lớp, STOP at first failure)
- guard + sln build + Core.Tests/ShopERP.Tests → FAST PUSH → CD Multi-VPS
- **L1** markers production (LoanPostingStrategy + enum trong DLL) · **L2** routes 302 (loan-in/loan-repayment) · **L3** Playwright prod spec (như người dùng) · **L4** JE loan thật trong PG (ReferenceType JournalVoucher, lines 111/341) · **L5** user manual 5 bước (ghi cuối task card + cập nhật `smart-entry-user-guide.md` nếu cần)

## 4. ACCEPTANCE — 100% (UI + Menu + RV-ready)
- [ ] **UI**: Mọi intent (trừ Unknown) → đúng phiếu/route · Unknown chặn cứng + alert đỏ · Advance alert · phiếu vay đủ form (Account select COA, JE preview, lưu, in 01-TT/02-TT, success, form sạch)
- [ ] **Menu**: NavMenu + Sitemap + AccountingIndex — **cả 3** có link phiếu vay (E2E assert từng nơi)
- [ ] Parser: "vay ngân hàng 100 triệu nhập quỹ" → Loan (không TransferOut) · "trả nợ vay 20 triệu" → LoanRepayment (không PayablePayment)
- [ ] Dropdown 2 form registry-driven, thứ tự không đổi (regression) · phiếu vay qua registry + strategy
- [ ] `/vouchers/loan-in` + `/vouchers/loan-repayment` render + lưu → JE Nợ 111/Có 341 · Nợ 341/Có 111 (CTGS) + in được
- [ ] **RV-ready**: spec prod + config + auth trong repo, chạy được trên VPS như người dùng (L3) · L1/L2/L4 pass · L5 user manual
- [ ] guard + build + Core.Tests/ShopERP.Tests PASS · E2E local PASS · CD + RV production

## 5. QUYẾT ĐỊNH (chốt)
| # | Q | Chốt |
|---|---|---|
| Q1 | Phạm vi | FULL — thêm Vay/Trả nợ vay qua registry |
| Q2 | Unknown khi áp | Chặn cứng, không điền form + alert |
| Q3 | Advance | Giữ mapping thu nợ + FIFO; thêm alert cảnh báo |
| D4 | Host loan | Generic page `/vouchers/loan-in` + `/vouchers/loan-repayment` (pattern advance/stock-issue) |

## 6. DEFER (ghi nhận, không làm)
- "Cho vay" (LoanOut — Nợ 128/138, Có 111) — để Unknown chặn an toàn
- Migrate 5 phiếu cũ sang generic page (G3 master plan voucher-framework — vẫn tùy chọn)
- TK vay tự động theo loại hình (311 vs 341) — user chọn tay qua dropdown COA

## 7. L5 USER MANUAL (ghi vào task card sau deploy)
1. Đăng nhập app2.khachvip.online (tenant thật, vai Owner/Kế toán)
2. Menu Kế Toán → "Phiếu Vay Nhận Tiền" (hoặc Sitemap/Quick Action) → nhập TK vay (311/341), số tiền, ngày → xem preview Nợ 111/Có 341 → Lưu
3. Kiểm tra Sổ Nhật ký chung / Lịch Sử Giao Dịch thấy JE vay (CTGS)
4. Smart Entry: gõ "vay ngân hàng 100 triệu nhập quỹ" → bấm Phân tích → thẻ "Vay nhận tiền" → Áp vào form → route đúng phiếu vay
5. Gõ câu không nhận diện được → bấm Áp vào form → thấy alert chặn "Không nhận diện được loại phiếu" — form không bị điền sai