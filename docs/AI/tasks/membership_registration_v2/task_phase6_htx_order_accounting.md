# TASK CARD: Phase 6 — HTX Order Accounting (kết nối membership → kế toán TT 71)

> **Master plan:** `docs/AI/plans/membership-registration-v2-master-plan.md`
> **Status:** ⏳ PENDING — CHECK 2026-10-02 (user directive): chưa kết nối, thiếu tag nội bộ/ngoài, định khoản sai cho HTX (511/632 hardcode)

## 1. VẤN ĐỀ (check)

- `OrderService.CreateRevenueEntryAsync` HARDCODE `"511"` + COGS `"632"` — **632 không tồn tại trong TT 71** (HTX dùng 611/612).
- `Order` KHÔNG có thông tin nội bộ/ngoài; member (tenant) không tham gia order flow.
- B02-HTX template (TT 71 S2) tách 01a/01b (511/512) + 11a/11b (611/612) — nhưng KHÔNG có dữ liệu đúng từ order.

## 2. CHANGES

| File | Change |
|---|---|
| `1_Shared/.../OrderAggregate/Order.cs` | + `IsInternalToHtx` (bool?) — tag tại creation (Domain change — đã duyệt plan) |
| `3_CoreHub/Infrastructure/Configurations/OrderConfiguration.cs` + migration PG | map cột mới |
| `3_CoreHub/Services/OrderService.CreateOrderAsync` | Nếu tenant bán là HTX (TT71): xác định buyer — `Customer.OwnerCustomerId` → tenant sở hữu bởi buyer → nếu là **Member active của HTX** (`Members` TenantId==HTX && MemberTenantId==buyerTenant) → `IsInternalToHtx = true`; ngược lại false |
| `3_CoreHub/Services/OrderService` (ConfirmPayment accounting) | Branch theo `AccountingStandard` của tenant bán: TT71 → revenue **512** (nội)/**511** (ngoài) · COGS **612** (nội)/**611** (ngoài) · VAT 3331 giữ · khác → giữ 511/632 (không regress) |
| `2_Gateway` OrderCreated payload | sync `IsInternalToHtx` (nếu có) sang ShopERP SQLite (OrderSyncSubscriber) |

## 3. LƯU Ý

- Chỉ áp dụng cho đơn của tenant bán = HTX (Marketplace). Reseller path KHÔNG mở rộng cho HTX (note — HTX reseller chưa hỗ trợ).
- Buy-to-tenant link: dùng `Tenants.OwnerCustomerId == Order.CustomerId` (pattern có sẵn) → member lookup `Members` (IgnoreQueryFilters, Active, MemberTenantId).
- LN nội/ngoài (4211/4212) theo dõi qua tài khoản — không cần entry riêng MVP.
- JournalEntry path (HKD books) không áp dụng cho HTX — giữ nguyên.

## 4. ACCEPTANCE

- [ ] Đơn HTX bán cho buyer là member tenant → IsInternalToHtx=true → định khoản 512/612; buyer ngoài → 511/611
- [ ] Đơn tenant DN → giữ 511/632 (không regress)
- [ ] B02-HTX 01a/01b + 11a/11b có số liệu từ order (service tests + report test)
- [ ] Build sln 0 errors · Core.Tests PASS
