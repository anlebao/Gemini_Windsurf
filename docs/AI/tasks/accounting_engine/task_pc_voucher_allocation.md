# TASK CARD C — VOUCHER NUMBERING · ALLOCATION LINK-FIELD · ORDERPAYMENTS · UNAPPLIED · ATTACHMENTS

> Status: ⏳ PENDING — chờ Phase B
> Master plan: `docs/AI/plans/accounting-engine-master-plan.md` (§4.C) · Gap đóng: **G-H3 · G-H4 · G-C1 phần 2 (R3) · FR-REC-004/005 · FR-VCH-003/004/007 · AT-005/006 · ACC12-003/006/007**
> ⚠️ Domain changes (AccountingEntry fields + OrderPayments entity) → **Gate 5 approval**
> Có thể tách **C1** (voucher+allocation+attachments) và **C2** (OrderPayments — refactor lớn).

---

## 1. MỤC TIÊU

Số chứng từ tuần tự duy nhất · phân bổ thu/trả nợ truy vết được (link-field, KHÔNG bảng riêng — R4) · multi-payment per order qua `OrderPayments` event riêng · trả thừa = unapplied balance · đính kèm chứng từ.

## 2. THIẾT KẾ

### 2.1. `VoucherSequences` + `VoucherNumber` (M6)

- `VoucherSequences(TenantId, VoucherType, Year, LastNumber)` — unique (tenant,type,year); atomic increment (EF concurrency token hoặc `SELECT ... FOR UPDATE`/SQLite transaction).
- `VoucherType`: `PT` (phiếu thu doanh thu) · `PT-CN` (ghi nhận/thu nợ 131) · `PC` · `PC-CN` (331) · `CT` (chuyển tiền — dùng Phase D).
- `AccountingEntry.VoucherNumber` (nullable string, indexed tenant+type+year unique) — gán khi tạo phiếu tay/công nợ; format `PT-2026-00001`. Order-driven entries: null (order là chứng từ gốc — FR-VCH-004 áp cho voucher).
- Backfill: null cho entries cũ (không renumber lịch sử); hiển thị fallback `Reference` như hiện nay.
- UI: hiển thị VoucherNumber trên TransactionHistory/CongNoLedger/print voucher (Tt71 voucher `SoPhieu` dùng số này — không đổi form).

### 2.2. Allocation link-field (M7 — R4)

- `AccountingEntry.AllocatedToEntryId` (Guid?) — payment entry link tới khoản nợ gốc.
- `CongNoService`: khi user chọn khoản cụ thể (sổ đối tượng → trả khoản này) set `AllocatedToEntryId`; FIFO auto như hiện tại khi không chọn.
- **Không bảng PaymentAllocation** — allocation derive từ entries + link field + FIFO (single truth). `GetKhoanNoPaymentsAsync` đọc thêm link field (ưu tiên explicit trước FIFO).
- FR-PAY-004/FR-REC-004: payment cho nhiều khoản = nhiều payment entries (1 per khoản, cùng phiếu cha? — **thiết kế:** 1 phiếu nhiều khoản → 1 entry tổng + N allocation-entry con (Adjustment, CashBankBook, Amount=phần phân bổ, AllocatedToEntryId=khoản). Đơn giản hơn: khuyến nghị 1 phiếu/khoản như hiện tại; multi-invoice 1 lần thu → entries con — chốt khi IMPLEMENT, ưu tiên ít entity nhất đáp ứng AT-005).

### 2.3. Unapplied / trả thừa (§18.7)

- Payment > nợ còn mở → phần dư ghi thành trả trước: đối tượng FIFO balance âm — đã hoạt động tự nhiên; **cần:** (a) CongNoReport/Ledger hiển thị nhãn "Trả trước" khi số dư đối tượng âm (không hiển thị như nợ âm lạ); (b) JE cho phần trả trước khi DN: Nợ 111/Có 131 (dư Có = advance — cùng TK, đúng chuẩn); (c) không tự ghi doanh thu — ACC12-006.
- FR-REC-008 thu chưa rõ: purpose `unclassified-receipt` (131-detail "Chưa phân loại" hoặc 3387 tùy chế độ — chốt: dùng đối tượng đặc biệt "CHUA-PHAN-LOAI" + cảnh báo reconcile) — tối thiểu cho Phase C.

### 2.4. `OrderPayments` (M8 — C2, R3)

- Entity: `(Id, OrderId, TenantId, Amount, Method, PaidAt, SourceEventId unique, Reference?)` — mỗi lần thu tiền của order = 1 payment event riêng (partial/multi-method/multi-lần — FR-SAL-001/004/006, ACC12-003).
- `GenerateAccountingEntriesAsync` refactor → `GenerateForPaymentAsync(order, payment)` — SourceEventId=payment.Id, purpose per-payment; idempotency theo payment chứ không order (fix G-C1 đúng nghĩa).
- Backfill: 1 synthetic `OrderPayments` row per order đã Paid (Amount=PaidAmount tổng, Method="legacy") — giữ tương thích; `ExistsByReference` cũ vẫn chặn re-emit.
- Order.TotalPaidAmount = Σ payments; mark-paid khi ≥ total (partial payment cho phép dưới threshold cấu hình? — đơn giản: payment cuối đủ → paid; entries per payment theo tỉ lệ revenue/vat/cogs allocation — **chiến lược phân bổ:** revenue/VAT theo tỉ lệ payment/total, COGS ghi 1 lần payment đầu đạt fulfillment — chốt chi tiết khi IMPLEMENT).

### 2.5. `PaymentMethod`/`CashAccountCode` + `Attachments` (M7/M10)

- Entries += `PaymentMethod` (`cash`/`bank`/`qr`/`other`) + `CashAccountCode` (default `111`; Phase D mở 112x).
- `Attachments(TenantId, EntityType, EntityId, FileUrl, MimeType, UploadedBy, CreatedAt)` — link tới AccountingEntry/Voucher; storage R2/Cloudinary reuse (`IR2StorageService` precedent); upload endpoint sau — trước mắt field + API nội bộ.

## 3. FILES TẠO/SỬA

- Tạo: `Infrastructure/Entities/VoucherSequenceEntity.cs` + `OrderPayment` (Domain — business logic entity) + `Attachment` entity · `VoucherNumberService` · configurations + migrations
- Sửa: `AccountingEntry` (+VoucherNumber/AllocatedToEntryId/PaymentMethod/CashAccountCode) · `CongNoService` (link + trả trước label) · `RevenueEntry`/`ExpenseEntry` (gán số phiếu — KHÔNG thêm field bắt buộc) · `OrderService`/`Order` (payments) · CongNoReport/Ledger (nhãn trả trước) · Tt71 voucher (SoPhieu=VoucherNumber)
- Tests: sequence concurrency · allocation link + FIFO fallback · trả thừa → advance không revenue · OrderPayments multi-payment idempotent

## 4. ACCEPTANCE CRITERIA

- [ ] Số phiếu duy nhất (tenant,type,year), tuần tự, 2 phiếu đồng thời không trùng — FR-VCH-004
- [ ] Thu nợ link khoản cụ thể → `GetKhoanNoPaymentsAsync` đúng lịch sử
- [ ] Thu nhiều hóa đơn: phân bổ đúng, không vượt từng khoản — AT-005/ACC12-007
- [ ] Trả thừa → trả trước (đối tượng âm, nhãn "Trả trước"), KHÔNG doanh thu — AT-006/ACC12-006
- [ ] Order 2 lần thanh toán → 2 payment events, entries độc lập — ACC12-003
- [ ] Retry cùng payment → không trùng — ACC12-004
- [ ] Attachment lưu/link được với phiếu — FR-VCH-007
- [ ] guard + build + sweep PASS

## 5. TEST CASES

- T1 Sequence: 2 phiếu song song → số khác nhau liên tiếp; năm mới reset
- T2 AllocatedToEntryId explicit + FIFO fallback khi null
- T3 Trả thừa: đối tượng balance âm + report nhãn đúng + không JE doanh thu
- T4 Multi-payment: partial → paid khi đủ; entries per-payment; retry payment → 1 bộ JE
- T5 Thu chưa rõ → đối tượng CHUA-PHAN-LOAI + reconcile cảnh báo
- T6 Backfill synthetic payments: paid order cũ không re-emit entries
