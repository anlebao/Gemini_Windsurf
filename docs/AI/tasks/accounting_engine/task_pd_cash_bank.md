# TASK CARD D — BANK ACCOUNTS · INTERNAL TRANSFER · SỔ QUỸ/TGNH · CASH-BALANCE WARN

> Status: ⏳ PENDING — chờ Phase C
> Master plan: `docs/AI/plans/accounting-engine-master-plan.md` (§4.D) · Gap đóng: **G-M1 · G-M2 · FR-TRF-001→005 · FR-RPT-003/004 · FR-PAY-006 · FR-SUB-001 · AT-011**
> ⚠️ Domain/persistence entities mới (BankAccount, InternalTransfer) → Gate 5 nếu đặt trong Domain; ưu tiên Infrastructure entities nếu data-only.

---

## 1. MỤC TIÊU

Mô hình tài khoản tiền (quỹ 111 + ngân hàng 112x per account) · chuyển tiền nội bộ đúng bản chất (không doanh thu/chi phí, phí riêng, trạng thái đang chuyển) · sổ quỹ + sổ TGNH · cảnh báo chi vượt quỹ.

## 2. THIẾT KẾ

### 2.1. `BankAccounts` (M9)

- `(TenantId, AccountCode` — `112`/`1121`/`112A`... unique per tenant, `BankName, AccountNumber, DisplayName, IsActive, CreatedAt, EffectiveFrom)`.
- Seed: mỗi tenant 1 quỹ mặc định `111 "Tiền mặt"` + optional 112 rows do tenant thêm (admin settings — **backend-first, UI settings đơn giản sau nếu cần**).
- `AccountingEntry.CashAccountCode` (từ Phase C) — receipt/payment/transfer set account thực; default `111`.

### 2.2. `InternalTransfers` (M9)

- `(Id, TenantId, VoucherNumber[CT-*], FromAccountCode, ToAccountCode, Amount, FeeAmount, FeeAccountCode, Status, TransferDate, ReceivedDate, BankReference, Description, ReversedById?)`
- Status: `Draft`(0) · `InTransit`(1) · `Received`(2) · `Discrepancy`(3) · `Cancelled`(4) — AT-011: `Received` mới chốt số dư; `InTransit` dùng **113 Tiền đang chuyển**.
- Posting qua account-map (Phase B): `transfer-out→({to=113 or đích},{from})` · `transfer-in→({đích},113)` · `transfer-fee→(642,{from})` — phí **riêng** (FR-TRF-004); seed rules mới vào `AccountMappingRules`.
- FR-TRF-005: tổng tài sản tiền không đổi (Nợ 11x/Có 11x) — test invariant.
- Sửa/đảo: không sửa transfer đã Received — tạo đảo (reversal pattern); Discrepancy → xử lý manual + audit.

### 2.3. Sổ quỹ + sổ TGNH (FR-RPT-003/004)

- `CashBookService`/`BankBookService` (hoặc mở rộng `GeneralLedgerService`): entries lọc `CashAccountCode=111` vs `112x`, running balance đầu kỳ → phát sinh → cuối kỳ.
- Pages: `SubLedger.razor` đã render per-account — thêm nhánh format sổ quỹ/TGNH (dư đầu, thu, chi, tồn) hoặc 2 trang riêng `/accounting/cash-book` + `/accounting/bank-book` (**UX change nhỏ — thêm trang báo cáo, không đổi form nhập → OK theo §18.1 vì là report mới, vẫn note cho PO**).

### 2.4. FR-PAY-006 cash-balance warn

- `PaymentComplianceService` (hoặc service mới `CashBalanceService`): khi phiếu chi `CashAccountCode` > số dư hiện tại của account đó → **cảnh báo** (không chặn — precedent Q1 cũ: cảnh báo + lý do); số dư = Σ entries CashBankBook/RevenueBook theo CashAccountCode — **định nghĩa số dư quỹ phải chốt khi IMPLEMENT** (entries-based approximation, ghi rõ limitation).

## 3. FILES TẠO/SỬA

- Tạo: `BankAccountEntity` + `InternalTransferEntity` (+config+seed+migration) · `InternalTransferService`/`IInternalTransferService` · `BankBookService` (hoặc extend GL) · pages `CashBook.razor`/`BankBook.razor` (hoặc nhánh SubLedger) · `BankAccountController` nội bộ (tenant admin) — thiết kế tối thiểu
- Sửa: `AccountMapSeeder` (transfer rules) · `CongNo`/`RevenueEntry`/`ExpenseEntry` (CashAccountCode gán) · `AccountingEntryService` (map method→account: cash→111, bank→112 default) · `PaymentComplianceService` (balance warn hook)
- Tests: transfer invariant · status machine · sổ quỹ/TGNH balances · fee split

## 4. ACCEPTANCE CRITERIA

- [ ] Chuyển 111→112: JE Nợ112/Có111 (hoặc qua 113 khi InTransit→Received) — CK01-05; **không** entry Revenue/Expense nào — AT-011/FR-TRF-001
- [ ] Phí chuyển → JE riêng Nợ642/Có{from}, không gộp — FR-TRF-004
- [ ] Tổng số dư các TK tiền trước/sau chuyển bằng nhau (trừ phí) — FR-TRF-005 invariant
- [ ] Sổ quỹ/TGNH: đầu kỳ + thu − chi = cuối kỳ, khớp SubLedger TK — FR-SUB-004
- [ ] Chi vượt số dư → cảnh báo + bắt lý do (không chặn) — FR-PAY-006
- [ ] guard + build + sweep PASS

## 5. TEST CASES

- T1 CK01 rút NH về quỹ: JE (111,112) đúng, P&L không đổi
- T2 CK02 nộp quỹ vào NH: JE (112,111)
- T3 CK04/05 InTransit: JE (113,112) → Received → JE (112,113); Discrepancy status
- T4 Fee: 2 JE (chuyển + phí), fee account 642
- T5 Sổ quỹ: balance chuỗi đúng qua nhiều entries kỳ
- T6 Warn chi > số dư quỹ (mock balance)
- T7 Reversal transfer Received → đảo, status/audit đủ
