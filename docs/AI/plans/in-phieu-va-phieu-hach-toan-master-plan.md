# MASTER PLAN — IN PHIẾU ĐẦY ĐỦ + PHIẾU HẠCH TOÁN (CTGS)

> Ngày: 2026-11-01 · Nguồn: phân tích SRS v1.2 (§7.3/§7.4) + user chốt sau đề xuất 3 tầng
> Task card: `docs/AI/tasks/task_in_phieu_va_phieu_hach_toan.md`

## 1. Bối cảnh & tinh thần SRS

SRS v1.2 §7.3/§7.4: **giữ nguyên biểu mẫu thu/chi**, engine suy luận loại nghiệp vụ,
chỉ hỏi user khi không suy luận được. 12 THU + 13 CHI là mã nghiệp vụ cho engine —
KHÔNG phải 25 option trên form. Kiến trúc 3 tầng đã trình bày và được duyệt:

- **Tầng 1** — engine tự tạo từ chứng từ nguồn (đã có: order, booking, purchase, cong-nợ)
- **Tầng 2** — 2 form thu/chi hiện có (giữ nguyên UX)
- **Tầng 3** — form "Phiếu hạch toán" cho bút toán tay phức tạp (MỚI)

## 2. Quyết định user đã chốt

| # | Quyết định |
|---|---|
| D1 | KHÔNG thêm loại phiếu mới vào dropdown thu/chi — giữ loại hiện có + **cho phép in phiếu** |
| D2 | Mã chứng từ Phiếu hạch toán = **CTGS** (`CTGS-2026-00001`) |
| D3 | Phiếu hạch toán dùng policy **AccountingFullAccess** (Owner + ChiefAccountant) |
| D4 (từ #198 S3) | VAT-out phiếu thu tay — defer (form không có field VAT đầu ra) |
| D5 (từ #198 S3) | `payable` giữ no-rule global — kích hoạt qua tenant rule trên `/accounting/mapping-rules` |

## 3. Hiện trạng đã verify (code-level)

- Print 01-TT/02-TT đã có component `Tt71ReceiptVoucher`/`Tt71PaymentVoucher` (mẫu chuẩn,
  `vananPrintBill` + CSS print vùng phiếu) — nhưng **gate `_tenantType == HTX`** cả 2 form
  và **chỉ in phiếu vừa lưu trong session** (`_lastVoucher`).
- `TransactionHistory` detail modal chưa có nút in.
- `JournalService` + `JournalEntry` + `EnsureBalanced` + `AddToBookAsync` sẵn.
- `VoucherNumberService` đã có PT/PC/PT-CN/PC-CN/CT/NM — thêm constant là đủ, không migration.
- `AuditableEntityType` — dùng `AccountingEntry` (tránh thêm enum = Domain change Gate 5).
- COA dropdown: `EntryFormAccountingHelper.LoadAccountOptionsAsync` + `IAccountChartService.GetTenantAccountsAsync`.

## 4. Scope

### P1 — In phiếu đầy đủ (phiếu hiện có)

| # | Việc | File |
|---|---|---|
| P1a | Gỡ gate HTX → in 01-TT mọi tenant; mở rộng in cho phiếu **thu nợ** (receivable-payment → Nợ {cash}/Có 131, Người nộp = đối tượng) | `RevenueEntry.razor` (gate nút ~L51 + capture ~L846) |
| P1b | Tương tự phiếu chi: **trả nợ** (payable-payment → Nợ 331/Có {cash}, Người nhận = đối tượng) | `ExpenseEntry.razor` (gate ~L62 + capture ~L1135) |
| P1c | **In lại từ lịch sử**: nút "In phiếu" trong detail modal `TransactionHistory` → rebuild voucher từ `AccountingEntryDto` (VoucherNumber, TransactionDate, EntryType, AccountCode, CashAccountCode, Vendor, Description, Amount, Reference) | `TransactionHistory.razor` |
| | KHÔNG in: phiếu ghi nhận nợ (non-cash, amount>0 131/331), DepositOffset, VAT-133 entry | — |

Quy tắc map in lại:
- `Revenue` → 01-TT: Nợ `CashAccountCode??"111"` / Có `AccountCode`
- `Expense` → 02-TT: Nợ `AccountCode` / Có `CashAccountCode??"111"`
- `Adjustment` + `Amount<0` + `131` → 01-TT (thu nợ)
- `Adjustment` + `Amount<0` + `331` → 02-TT (trả nợ)
- Field không persist (Địa chỉ, Kèm theo) → để trống; Người nộp/nhận = `Vendor`.

### P2 — Phiếu hạch toán (CTGS) — form Tầng 3

| # | Việc | File |
|---|---|---|
| P2a | `TypeJournalVoucher = "CTGS"` | `VoucherNumberService.cs` (sau TypePurchaseReceipt) |
| P2b | `IManualJournalVoucherService` + `ManualJournalVoucherService` — validate (≥2 dòng, TK ≥3 digits, Nợ xor Có >0, ΣNợ=ΣCó), period-closing guard, voucher CTGS (fail-safe), JE `ReferenceType="JournalVoucher"` + `ReferenceId=voucherId` + `PostingPurpose=Other`, `AddToBookAsync`, audit `AuditableEntityType.AccountingEntry` | `3_CoreHub/Services/Journal/ManualJournalVoucherService.cs` (MỚI) |
| P2c | DI | `5_WebApps/ShopERP/Program.cs` (sau dòng IAccountMapRuleService ~L250) |
| P2d | Trang `/accounting/journal-voucher`: ngày + diễn giải + grid dòng động (select TK từ COA tenant full chart · input Nợ · input Có · diễn giải dòng · xóa dòng) · nút Thêm dòng · tổng Nợ/Có realtime + badge cân/lệch · Lưu (disabled khi lệch hoặc <2 dòng) · sau lưu: số phiếu + nút In CTGS | `JournalVoucher.razor` (MỚI) |
| P2e | Component in mẫu CTGS (header + bảng dòng + tổng + chữ ký), `vananPrintBill` | `Components/Accounting/JournalVoucherPrint.razor` (MỚI) |
| P2f | Nav link "🧾 Phiếu hạch toán" trong Quick Actions | `AccountingIndex.razor` (+NavigateToJournalVoucher) |
| P2g | Tests: service xUnit (balanced/unbalanced/period-closed/line-invalid/voucher-seq/audit) + bUnit trang (render, cân đối, add/remove dòng) | `ManualJournalVoucherServiceTests.cs` (MỚI) + `JournalVoucherPageTests.cs` (MỚI) |
| P2h | E2E spec: route render + balance UI | mở rộng `accounting-issue198.spec.ts` |

## 5. Ràng buộc kỹ thuật / non-goals

- **Không Domain change** — dùng `JournalEntry`, `AccountingPostingPurposes.Other`, `AuditableEntityType.AccountingEntry` có sẵn (Gate 5).
- **Không migration** — `JournalEntries` + `VoucherSequences` đã có.
- Phiếu hạch toán tạo **JournalEntry trực tiếp** (không qua AccountingEntry → không hiện trong TransactionHistory; thấy trong `/accounting/journal` + Sổ Cái — đúng bản chất chứng từ ghi sổ).
- Đảo phiếu CTGS: **defer** — v1 chỉ tạo + in; đảo qua `CreateReversalEntryAsync` nếu cần sau.
- In lại CTGS từ Journal report: **defer** — v1 in ngay sau lưu.
- Idempotent: retry worker không đụng (phiếu hạch toán không có EntryPostingStatus — không có AccountingEntry.Id); double-submit chặn bằng `_saving` flag + unique (Tenant,RefType,RefId,Purpose) nếu ReferenceId giữ ổn định qua retry — v1 không retry.
- Validate TK tồn tại trong COA tenant (dropdown limit) + server check `^[1-9]\d{0,3}$` (reuse semantic AccountClassifier — không cần gọi, regex inline đủ).

## 6. Test matrix tối thiểu

| Test | Mong đợi |
|---|---|
| Create balanced 3 dòng | JE ghi sổ, ReferenceType=JournalVoucher, purpose=Other, VoucherNumber CTGS-* |
| ΣNợ≠ΣCó | throw ValidationException, không AddToBook |
| <2 dòng / TK lỗi / Nợ+Có cùng >0 / cả 2 =0 | throw |
| Kỳ đã đóng | throw InvalidOperationException |
| Voucher service lỗi | vẫn ghi sổ, VoucherNumber=null (fail-safe) |
| bUnit trang | render form + add/remove dòng + badge lệch/cân + Lưu disabled khi lệch |

## 7. Deploy + RV (sweep-first)

1. guard-check + build + Core.Tests targeted + bUnit → FAST PUSH → CD poll
2. RV L1: image tag shop-a + marker `JournalVoucher` trong DLL
3. RV L2: `/accounting/journal-voucher` + `/accounting/history` → 302 (route live)
4. RV L3 DB: tạo JE qua API thật nếu có, hoặc verify sau E2E — `ReferenceType='JournalVoucher'` trên PG
5. RV L4: Playwright spec (render trang, in phiếu 01-TT trên tenant non-HTX — trước chỉ HTX thấy nút)
6. L5 manual user

## 8. Risk

| Risk | Giảm thiểu |
|---|---|
| Re-print từ history thiếu Địa chỉ/Người nộp chuẩn | dùng Vendor; ghi chú trong phiếu trống hợp lệ (ký tay) |
| JE CTGS không hiện TransactionHistory → user "mất phiếu" | thông báo sau lưu: "xem lại ở Nhật ký chung" |
| Nhầm 01-TT khi in phiếu thu nợ | Co cố định 131, đúng bản chất thu tiền |
| Component Tt71* tên gây hiểu nhầm "chỉ HTX" | mẫu 01/02-TT là mẫu chung — giữ tên file, comment rõ |
