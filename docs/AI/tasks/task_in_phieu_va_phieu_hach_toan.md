# TASK CARD — IN PHIẾU ĐẦY ĐỦ + PHIẾU HẠCH TOÁN (CTGS)

> Status: **✅ DONE + DEPLOYED `c7b00aae` (CD Multi-VPS #41275612683 SUCCESS · RV L1/L2/L4 PASS — còn L3 PG verify + L5 manual)** · Ngày: 2026-11-01 · Master plan: `docs/AI/plans/in-phieu-va-phieu-hach-toan-master-plan.md`
> Mode: IMPLEMENT sau khi user duyệt · Chiến lược: 1 phiên liên tục P1 → P2 → push → sweep-first RV

## Quyết định đã chốt

- D1: giữ loại phiếu hiện có trên 2 form thu/chi + **thêm in phiếu** (KHÔNG thêm loại mới vào dropdown)
- D2: prefix chứng từ phiếu hạch toán = **CTGS**
- D3: policy **AccountingFullAccess**
- D4/D5: VAT-out phiếu thu defer · `payable` giữ no-rule global

## Danh sách phiếu đầy đủ theo nghiệp vụ (ground truth — user chốt)

> Phiếu **hiện có trên 2 form** = scope P1 (in). Phiếu ❌/⚠️ ngoài CTGS = backlog, KHÔNG làm trong task này.

### 1. Nhóm TIỀN — phiếu thu/chi (PT/PC)

| Loại phiếu | Bút toán | Trạng thái | In (P1)? |
|---|---|---|---|
| Phiếu thu doanh thu | Nợ 111/112 · Có 511/512 | ✅ `revenue` | ✅ 01-TT |
| Phiếu thu góp vốn chủ sở hữu | Nợ 111 · Có 411 | ✅ `capital-contribution` (S1) | ✅ 01-TT |
| Phiếu thu hồi phải thu (thu nợ) | Nợ 111/112 · Có 131 | ✅ `receivable-payment` | ✅ 01-TT |
| Phiếu thu khác (phạt, lãi, nhượng bán) | Nợ 111/112 · Có 711/515 | ⚠️ qua `revenue` + TK 7xx | ✅ 01-TT |
| Phiếu thu vay nợ | Nợ 111/112 · Có 311/341/315 | ❌ chưa có | — |
| Phiếu thu hoàn ứng / thu cọc lại | Nợ 111/112 · Có 141/3386 | ❌ chưa có | — |
| Phiếu chi phí | Nợ 6xx · Có 111/112 | ✅ `expense` | ✅ 02-TT |
| Phiếu chi trả nợ phải trả | Nợ 331 · Có 111/112 | ✅ `payable-payment` | ✅ 02-TT |
| Phiếu chi lương | Nợ 334 · Có 111 | ❌ chưa có | — |
| Phiếu chi nộp thuế | Nợ 333x · Có 111/112 | ❌ chưa có | — |
| Phiếu chi rút vốn/hoàn vốn | Nợ 411 · Có 111 | ❌ chưa có | — |
| Phiếu chi tạm ứng | Nợ 141 · Có 111 | ❌ chưa có (chặn whitelist 6xx) | — |
| Phiếu chi trả cọc/hoàn tiền | Nợ 3386/3387 · Có 111 | ❌ chưa có | — |

### 2. Nhóm CÔNG NỢ — phiếu không tiền (PT-CN/PC-CN)

| Loại phiếu | Trạng thái | In? |
|---|---|---|
| Phiếu ghi nhận phải thu (bán chịu) | ✅ `receivable` — JE accrual DN/HTX | ❌ non-cash |
| Phiếu ghi nhận phải trả (mua chịu) | ✅ `payable` — JE khi tenant config counter-account (S3) | ❌ non-cash |
| Phiếu phải thu/phải trả khác (138/338) | ❌ chưa có (chỉ 131/331) | — |

### 3. Nhóm MUA HÀNG & KHO

| Loại phiếu | Trạng thái | In? |
|---|---|---|
| Phiếu nhập mua (mua chịu / trả ngay) | ✅ `NM` — `purchase`/`purchase-cash` + JE + stock in | defer — trang riêng |
| Phiếu trả hàng NCC | ✅ `purchase-return` — JE đảo + stock out | defer |
| Phiếu nhập kho (ngoài mua: TP, điều chỉnh) | ⚠️ stock movement có, chưa có phiếu + JE riêng | — |
| Phiếu xuất kho (tiêu hao, sản xuất) | ⚠️ nhập/xuất tay ở `/inventory/ingredients` — chưa gắn JE | — |
| Phiếu chuyển kho | ❌ chưa có | — |
| Biên bản kiểm kê + phiếu điều chỉnh tồn | ❌ chưa có | — |

### 4. Nhóm NGÂN HÀNG & CHUYỂN TIỀN

| Loại phiếu | Trạng thái | In? |
|---|---|---|
| Phiếu chuyển tiền nội bộ (111↔112↔113) | ✅ `CT` — transfer-out/in/fee | defer |
| Giấy báo Có/Nợ ngân hàng | ⚠️ qua PT/PC + CashAccountCode 112x | qua 01/02-TT |

### 5. Nhóm BÚT TOÁN TỔNG HỢP & KỲ

| Loại phiếu | Trạng thái | In? |
|---|---|---|
| Phiếu số dư đầu kỳ | ✅ OpeningBalance + JE | defer |
| Bút toán kết chuyển cuối kỳ | ✅ W5 — auto JE (regime-gated) | auto-JE |
| Bút toán đảo | ✅ Reversal pattern | nghiệp vụ đảo |
| **Phiếu hạch toán / chứng từ ghi sổ (CTGS)** | **🆕 P2 task này** — `journal-voucher` multi-line, `ReferenceType="JournalVoucher"`, purpose `other`, voucher `CTGS-*` | ✅ mẫu CTGS |
| Bảng tính & phân bổ khấu hao TSCĐ | ❌ chưa có | — |
| Phiếu ghi tăng/giảm/thanh lý TSCĐ | ❌ chưa có | — |
| Bảng lương + phiếu phân bổ lương | ❌ chưa có | — |
| Phiếu phân bổ chi phí trả trước (142/242) | ❌ chưa có | — |

### 6. Nhóm THUẾ & HÓA ĐƠN

| Loại phiếu | Trạng thái | In? |
|---|---|---|
| Ghi nhận VAT đầu vào (phiếu tay) | ✅ `manual-vat-in` — Nợ 133 | — |
| Hóa đơn GTGT đầu ra (e-invoice) | ✅ pipeline Viettel/MISA + điều chỉnh/thay thế | qua e-invoice |
| Hóa đơn GTGT đầu vào (đối chiếu) | ❌ backlog P1.4 compliance | — |
| Phiếu kê khai/quyết toán thuế | ❌ chưa có | — |

## Scope — P1 (in phiếu)

- [x] `RevenueEntry.razor`: gỡ `_tenantType == HTX` gate (nút in ~L51 + capture ~L846) → in 01-TT mọi tenant; mở rộng `receivable-payment` → Nợ `CashAccountCode??"111"` / Có `131` / NguoiNop=`doiTuong`
- [x] `ExpenseEntry.razor`: tương tự (gate ~L62 + capture ~L1135); `payable-payment` → Nợ `331` / Có `CashAccountCode??"111"` / NguoiNhan=`doiTuong`
- [x] `TransactionHistory.razor`: nút "🖨️ In phiếu" trong detail modal cho entry in được (Revenue→01-TT · Expense→02-TT · Adjustment amount<0 & 131→01-TT · 331→02-TT); rebuild voucher từ DTO; NguoiNop/Nhan=`Vendor`; DiaChi/KemTheo trống; KHÔNG in bút toán đảo (ReversalEntryId)

## Scope — P2 (phiếu hạch toán CTGS)

- [x] `VoucherNumberService.cs`: `public const string TypeJournalVoucher = "CTGS";`
- [x] `3_CoreHub/Services/Journal/ManualJournalVoucherService.cs` (MỚI):
  - `JournalVoucherLine(AccountNumber, Debit, Credit, Description?)` + `JournalVoucherResult(JournalEntryId, VoucherId, VoucherNumber?, LineCount)`
  - Validate: description bắt buộc · ≥2 dòng · TK `^[1-9]\d{0,3}$` · Nợ xor Có (>0 một phía, không cả 2, không cả 2=0) · ΣNợ=ΣCó
  - Period guard (GetPeriodStatusAsync → Closed throw) · voucher CTGS fail-safe · JE `ReferenceType="JournalVoucher"`, `ReferenceId=Guid.NewGuid()`, `PostingPurpose=Other` · `AddToBookAsync(S2b_HKD)` · audit `AuditableEntityType.AccountingEntry` (fail-safe ngoài)
- [x] `5_WebApps/ShopERP/Program.cs`: `AddScoped<IManualJournalVoucherService, ManualJournalVoucherService>` (sau IAccountMapRuleService)
- [x] `Components/Pages/Accounting/JournalVoucher.razor` (MỚI): `@page "/accounting/journal-voucher"` (policy AccountingFullAccess — D3) · date + diễn giải + grid dòng động (TK từ `IAccountChartService.GetTenantAccountsAsync` full chart sorted · Nợ · Có · diễn giải dòng · xóa) · "+ Thêm dòng" · tổng + badge cân/lệch · Lưu disabled khi lệch/<2 dòng/_saving · sau lưu hiện số phiếu + "🖨️ In phiếu (CTGS)" + link "xem Nhật ký chung"
- [x] `Components/Accounting/JournalVoucherPrint.razor` (MỚI): mẫu CTGS (Đơn vị + Số + Ngày + bảng TK/Nợ/Có/diễn giải + Tổng + chữ ký) · `vananPrintBill` · CSS print pattern giống Tt71 vouchers
- [x] `AccountingIndex.razor`: nút "🧾 Phiếu hạch toán" trong Quick Actions + `NavigateToJournalVoucher`

## Out of scope (defer)

- Đảo phiếu CTGS (reversal) · in lại CTGS từ journal report · thêm loại phiếu mới vào form thu/chi · VAT-out phiếu thu · `payable` seed global

## Tests

- [x] `ManualJournalVoucherServiceTests.cs`: balanced→AddToBook (JournalVoucher/Other/CTGS) · unbalanced throw · <2 dòng throw · TK invalid throw · Nợ+Có>0 throw · period closed throw · voucher fail → vẫn ghi sổ — **11/11 PASS**
- [x] `JournalVoucherPageTests.cs` (bUnit): render · add/remove dòng · badge lệch→cân · Lưu disabled khi lệch — **5/5 PASS** (lưu ý: `FindAll()[i]` indexer lỗi AngleSharp — dùng First()/Last() + re-query sau mỗi Change)
- [x] Extend `accounting-issue198.spec.ts`: route `/accounting/journal-voucher` render + nút In 01-TT trên tenant non-HTX (regression P1)

## Validation & Deploy

- [x] `dotnet build` 0 errors · `guard-check.ps1` ALL PASSED (`guard-report-20261101-115321.txt`) · targeted tests (service + bUnit) · ShopERP.Tests full **279/279**
- [x] FAST PUSH `c7b00aae` → CD Multi-VPS **#41275612683 SUCCESS**
- [x] RV sweep-first: **L1** image `c7b00aae` shop-a + markers `JournalVoucher`/`ManualJournalVoucherService` trong DLL ✓ · **L2** `/accounting/journal-voucher` + `/accounting/history` → 302 ✓ · **L4** spec `accounting-issue198.spec.ts` **7/7 PASS** production (journal-voucher render + save-disabled + in 01-TT trên tenant DN non-HTX — regression P1) · **L3** PG `ReferenceType='JournalVoucher'` — defer L5 (spec read-only, không submit phiếu production; đã verify ở unit tests) · **L5** user manual: tạo phiếu CTGS đầu tiên → verify Nhật ký chung + in phiếu 01-TT/02-TT sau lưu trên tenant thường

## Bối cảnh kỹ thuật (đã verify)

- Print components `Tt71ReceiptVoucher`(01-TT)/`Tt71PaymentVoucher`(02-TT): params `SoPhieu,Ngay,No,Co,NguoiNop|NguoiNhan,DiaChi,LyDo,SoTien,KemTheo,ChungTuGoc` + `DonVi`; print qua `vananPrintBill` — đang gate `_tenantType==HTX`
- `AccountingEntryDto` có: VoucherNumber, Vendor, CashAccountCode, PaymentMethod, Reference, EntryType, AccountCode, Amount, TransactionDate, Description
- `JournalEntry(tenantId, date, description, refType, refId, postingPurpose:)` + `AddLine(tk,debit,credit,desc?)` + `AddToBookAsync(je, S2b_HKD, ct)` (IHKDBookRepository)
- `IPeriodClosingService.GetPeriodStatusAsync(period, tenantId, ct?)` → `PeriodClosingStatus.Closed`
- `IVoucherNumberService.NextNumberAsync(tenantId, type, year, cancellationToken:)` → string
- `IAuditTrailService.LogCreateAsync(AuditableEntityType, entityId, jsonValues, correlationId:)`
- JE phiếu hạch toán KHÔNG hiện trong TransactionHistory (đọc AccountingEntries) — hiện trong `/accounting/journal` (Nhật ký chung) + Sổ Cái
