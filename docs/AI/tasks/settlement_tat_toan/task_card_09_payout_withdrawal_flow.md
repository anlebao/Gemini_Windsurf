# Task Card TC-09: Payout/Withdrawal flow — đường tiền RA khỏi ledger

> **Status:** ✅ DONE (2026-09-22 — Settlement Batch-3)
> **Decisions:** Q4a — entity `WithdrawalRequest` + flow Pending→Approved/Rejected→Paid ✅ approved. Q4b — pay bằng **bank ref thủ công** (admin nhập tay, KYC entity deferred).
> **Severity:** P1 — feature thiếu (docs hứa rút tiền nhưng không có code; "tất toán" chỉ ghi sổ 1 chiều)
> **Findings:** C8
> **Files:** `1_Shared/Domain.cs` (WalletTransactionType.Withdrawal=4 ~L3734 — không có production caller), `3_CoreHub/Services/WalletService.cs`, `2_Gateway/Controllers/CommunityController.cs` (không có endpoint withdraw), `docs/user-guide/community-commerce/03-salesman.md` §7.2, `04-shipper.md` §10.3

## Root cause (verified)

- Enum `Withdrawal` + label UI "Rút tiền" (`Wallet.razor:169`) + docs "KYC bank + min 500.000đ" — nhưng **không có endpoint, service, entity, hay flow nào** tạo Withdrawal tx (chỉ có test dùng trực tiếp `CreateTransactionAsync`).
- Không có: withdrawal request, admin approve/reject, bằng chứng chi (bank transfer ref), payout batch, remittance schedule.
- Hệ quả: không role nào rút được tiền; platform cũng không có cơ chế trả shipper/salesman/tenant → ledger tích lũy 1 chiều, "tất toán" chưa tồn tại như 1 quy trình.

## Design decision Q4 (cần user chốt trước IMPLEMENT)

| Câu hỏi | Đề xuất |
|---|---|
| Ai được rút? | Salesman, Shipper (phần thực nhận — sau TC-08 tách khỏi tiền giữ hộ), Tenant owner, Platform (nội bộ) |
| Flow | `WithdrawalRequest` entity: Pending → Approved/Rejected (SystemAdmin) → Paid (kèm bank ref + ảnh chứng từ?) → WalletTransaction(Withdrawal, −amount) |
| Min amount | 500.000đ theo docs |
| KYC | cần thiết kế bank account info trên Customer — chưa có entity |
| Số dư khả dụng | = balance ledger − COD đang giữ hộ (phụ thuộc TC-08) − pending withdrawals |
| Chống double-spend | check `pending requests` khi tạo mới; lock tx khi approve; idempotent pay |
| Accounting | Withdrawal → bút toán chi tiền (link TC-07 bridge) |

## Scope (sau Q4)

- Domain mới: `WithdrawalRequest` entity + enum status — **domain approval required**.
- `WalletService`: `RequestWithdrawalAsync` (validate min + khả dụng), `ApproveAsync`, `RejectAsync`, `MarkPaidAsync(bankRef)`.
- Endpoints: customer `POST /api/community/wallet/withdraw` + `GET /withdrawals`; admin `GET/POST /api/admin/withdrawals/{id}/approve|reject|pay`.
- UI: Wallet.razor nút rút tiền + trạng thái request; ShopERP admin page quản lý payout.
- Audit + notification khi approve/pay.

## Tests

- Rút > balance khả dụng → reject; < 500k → reject; pending request tồn tại → chặn tạo mới.
- Approve → pay (có bankRef) → Withdrawal tx tạo đúng 1 lần, balance giảm.
- Reject → không tx; cancel own pending request được.
- Idempotent pay (double-click/retry không trừ 2 lần).

## Acceptance

- [ ] End-to-end: request → approve → pay → wallet tx + audit.
- [ ] Số dư khả dụng không gồm tiền giữ hộ COD.
- [ ] Không path nào tạo Withdrawal ngoài flow duyệt.
- [ ] Build 0 errors · guard-check PASS · Core.Tests PASS.
