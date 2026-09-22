# Task Card TC-07: Wallet → Accounting bridge (settlement events → bút toán)

> **Status:** ⬜ PENDING (ANALYZE → cần decision Q5: mapping + ghi ở tenant nào)
> **Severity:** P1 — sổ sách (toàn bộ nghĩa vụ settlement vô hình với kế toán)
> **Findings:** B4
> **Files:** `3_CoreHub/Services/WalletService.cs` (toàn file — 0 gọi IAccountingService), `3_CoreHub/Services/AccountingEntryService.cs`, `3_CoreHub/Services/HKDBookService.cs` (account map ~L29-48), `3_CoreHub/Services/CommunityFundService.cs`

## Root cause (verified)

`WalletService` không inject bất kỳ accounting service nào → 13 `WalletTransactionType` không tạo bút toán:

| WalletTransactionType | Bút toán kỳ vọng (đề xuất — Q5) | Tenant bookset |
|---|---|---|
| CODCollection (shipper giữ hộ) | Nợ 138 (phải thu shipper) / Có 131 hoặc đối ứng tiền hàng | Supplier tenant |
| Settlement → shop | Nợ 131/138 / Có 111-đối ứng (tùy Q1 semantics) | Supplier tenant |
| AdvancePayment | Nợ 141 (tạm ứng) | Shop/Platform tùy mode |
| Commission | Nợ 641 (chi phí bán hàng — hoa hồng) | Platform hoặc supplier (ai chịu?) |
| DeliveryFee | Nợ 641/642 (phí giao hàng) | Platform |
| PlatformFee | Có 511 (doanh thu dịch vụ nền tảng) | Platform tenant |
| CommunityFund | Có 3388/338 (quỹ phải chi) | Platform tenant |
| CommunityFundSpend | Nợ 3388 / Có 111 | Platform tenant |
| ExternalPayment | Nợ 112 | Platform tenant |
| Withdrawal | Nợ (khoản phải trả CTV) / Có 111-đối ứng | Platform tenant |
| Deposit / SmsOtpFee | 131/641 tương ứng | Platform tenant |

## Scope

1. **ANALYZE trước:** chốt Q5 mapping với user (bảng trên là draft) — HKD single-entry có thể chỉ cần AccountingEntry (type+accountCode) không cần JournalEntry đầy đủ, hoặc cần cả 2 — quyết định theo convention `OrderService` (hiện tạo cả AccountingEntry lẫn JournalEntry).
2. Điểm nối: sau mỗi `CreateTransactionAsync` thành công (hoặc nghe wallet event qua Outbox — đề xuất **Outbox/NATS event `WalletTransactionCreated`** để accounting là async consumer, không chặn flow tiền).
3. CorrelationId = wallet tx Id (hoặc RelatedOrderId) → refund reversal tự bắt được qua `GetByCorrelationIdAsync`.
4. Idempotent: skip khi đã có entry cùng correlation+type.
5. Reversal tx → tạo reversal accounting entry tương ứng (map qua RelatedTransactionId).

## Open questions (cho user)

- Hoa hồng salesman là chi phí của **platform** hay của **supplier tenant**? (ảnh hưởng BCTC từng bên)
- HKD đơn entry đủ hay cần JournalEntry đối ứng đầy đủ (Nợ/Có)?
- Platform tenant = `PlatformAccountingTenantId` setting (B5) — bắt buộc config trước khi bật bridge?
- Backfill: có tạo entries cho wallet tx lịch sử không, hay chỉ từ cutover?

## Acceptance (draft — finalize sau Q5)

- [ ] Mỗi wallet tx trong mapping tạo đúng 1 accounting entry, đúng tenant, đúng accountCode.
- [ ] Reversal wallet tx → reversal entry.
- [ ] Idempotent (retry/NATS redelivery không double).
- [ ] Build 0 errors · guard-check PASS · Core.Tests PASS.
