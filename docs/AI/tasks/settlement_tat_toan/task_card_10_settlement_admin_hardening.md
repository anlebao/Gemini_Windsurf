# Task Card TC-10: Settlement admin + hardening (gom các lỗi mức trung bình)

> **Status:** ⬜ PENDING (có thể tách sub-tasks theo batch)
> **Severity:** P2 — correctness/UX/security phụ
> **Findings:** D1, D2, D3, D4, D6, D7, D8, D9, D10, C10
> **Files:** `2_Gateway/Controllers/CommunityController.cs` (L79-97), `2_Gateway/Controllers/SettlementAdminController.cs` (L42-43), `3_CoreHub/Services/WalletService.cs` (L242-268, L460-506, L541-672), `3_CoreHub/Services/CoolingPeriodJob.cs` (L66-87), `3_CoreHub/Services/SalesmanService.cs` (L223, L237), `5_WebApps/ShopERP/Components/Pages/Admin/Settlements.razor`

## Sub-tasks

### S1 — `isShopOwner` sai phân quyền (D1)
`CommunityController.cs:79-97`: isShopOwner = "tenant của customer có ≥1 Settlement tx" → mọi khách hàng của shop đều được cờ owner. Fix: derive từ ownership thật (User/owner link tới tenant, vd `Tenant.OwnerUserId` hoặc role) — verify model ownership hiện có trước khi chọn field.

### S2 — SettlementAdminController LINQ Pattern #8 (D2)
`SettlementAdminController.cs:43`: `.Where(t => t.TenantId.Value == tenantId.Value)` — member access trên converted VO → nghi EF translation fail → 500 khi filter tenantId. Fix theo Pattern #8: `new TenantId(tenantId.Value)` trước khi compare. **Verify runtime trước** (test endpoint với ?tenantId=).

### S3 — Margin invariant + rounding + net profit (D3)
`WalletService.cs:242-268, 572-573`: `margin × rate` không làm tròn (VND lẻ), không enforce `commission + platformFee + communityFund ≤ margin`, phần dư (net profit Vạn An) không có tx. Fix: Math.Round theo convention (verify chuẩn rounding đang dùng), validate tổng rate, và quyết định có ghi tx `PlatformMargin` cho phần dư không (minh bạch hóa split — cần duyệt).

### S4 — CoolingPeriodJob partial failure (D4)
`CoolingPeriodJob.cs:71-79`: wallet tx commit trong transaction riêng TRƯỚC, `MarkCommissionPaid` + save sau → crash giữa chừng → trả trùng vòng sau. Fix: check-tồn-tại commission tx trước khi tạo (dedup tự nhiên) hoặc gộp vào 1 transaction.

### S5 — Commission display sai (D6)
`SalesmanService.cs:223,237`: `OrderTotal = CommissionAmount / CommissionRate` — rate=0 → chia 1 → hiển thị sai; Reseller OnMargin → hiển thị margin base như "order total". Fix: dùng `CommissionBaseAmount` đã snapshot + label đúng.

### S6 — `GetPendingAdvancesAsync` perf (D7)
`WalletService.cs:460-506`: load toàn bộ bảng AdvancePayment + Settlement vào memory mỗi request. Fix: filter server-side (join Orders theo tenant, left-join settled ids).

### S7 — Audit trail cho wallet movements (D8)
WalletService không ghi `AuditLog` (AccountingEntryService có `LogCreateAsync`). Fix: audit mỗi tx tạo/reverse (owner, type, amount, orderId, correlation) — financial data cần audit.

### S8 — First-transaction race (D9)
`WalletService.cs:60-82`: owner chưa có tx → 2 concurrent create cùng balanceBefore=0 → BalanceAfter sai. Fix: upsert/lock theo owner (vd unique sequence column per owner, hoặc lock bằng wallet row riêng — cần thiết kế nhẹ, có thể domain approval nếu thêm entity Wallet).

### S9 — `ConfirmExternalPaymentAsync` (C10)
`WalletService.cs:541-672`: `paymentRef` không unique-check; DeliveryFee trả `FirstOrDefault(DeliveryTask)` (có thể task cancelled → trả sai shipper); không check order status; race trên `CodCollectedAt`. Fix: unique constraint/check paymentRef, chọn task `Status == Delivered`, order status guard, gộp transaction.

### S10 — Settlements.razor UI Platform (D10)
Dùng raw `<table>`/`<input>` (`Settlements.razor:35-45, 63-100`) — kiểm tra UI Platform có table/date-picker component tương đương; migrate nếu có (UI Platform compliance).

### S11 — `ConfirmAdvanceReceived` status code (phát hiện trong RV Batch-1, 2026-09-22)
`CommunityController.ConfirmAdvanceReceived` không catch `UnauthorizedAccessException` → cross-tenant rejection trả **500 "Lỗi server"** thay vì 403 (guard vẫn chặn đúng — không tạo Settlement). Fix: thêm `catch (UnauthorizedAccessException) → 403` như `ConfirmCod`/`ConfirmAdvance`.

## Acceptance

- [ ] Mỗi sub-task có test/verify riêng; list trong PR từng batch.
- [ ] Build 0 errors · guard-check PASS · Core.Tests PASS.
