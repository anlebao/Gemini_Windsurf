# Task FIX-07: KhachLink push toggle OFF — DELETE /api/notifications/push/subscribe không xoá subscription

> **Nguồn:** user report + RV 2026-11-17 (finding phụ) · **Mode:** FIX_ONLY · **Ưu tiên:** trung bình (UX: tắt nhưng vẫn nhận push)

## 1. TRIỆU CHỨNG (prod evidence)
- `GET push/status` → `{enabled:true, count:1}` · `DELETE push/subscribe` → `{removedCount:0}` · `GET push/status` → vẫn `{enabled:true, count:1}`.
- PG `PushSubscriptions` = 0 rows → row ở **ShopERP SQLite** (`vanan_shoperp.db`).

## 2. ROOT CAUSE (cần verify — chưa kết luận)
Hypothesis (chưa confirm):
1. **Tenant mismatch**: `PushSubscriptionRepository.GetByCustomerIdAsync` chịu global filter `TenantId == CurrentTenantId`; `ResolveCustomerTenant` set tenant từ `Customers` (IgnoreQueryFilters). Nếu tenant của customer 43094916 resolve không ổn định giữa requests → POST ghi row dưới tenant A, DELETE query dưới tenant B → 0 rows.
2. **SoftDelete/IsActive**: `GetByIdAsync` filter `!IsDeleted` (không `IsActive`) — nếu row đã inactive (expired?) thì status không nên count. Nhưng status count:1 → row active.
3. `ValidateToken` khác id giữa requests (ít khả năng).

**Verify bước 1 (bắt buộc trước khi sửa — Gate 1):** SSH shop-a (plink flaky — retry) → docker cp `vanan_shoperp.db` → query `SELECT Id, TenantId, CustomerId, IsActive, IsDeleted FROM PushSubscriptions` → đối chiếu tenant vs `Customers.TenantId` của customer 43094916.

## 3. FIX (theo kết quả verify)
- Nếu tenant mismatch: `ResolveCustomerTenant` ổn định hoá (cache customer→tenant per token trong request) hoặc repository bỏ phụ thuộc tenant cho PushSubscriptions (bảng per-customer, không cần tenant filter) — chọn theo verify.
- Nếu `SoftDeleteAsync` không persist: kiểm tra `MarkAsInactive` + SaveChanges.

## 4. TESTS + RV
- [ ] Unit: subscribe → status 1 → delete → status 0
- [ ] RV prod: POST → status 1 → DELETE → removedCount ≥1 → status 0

## 5. STATUS
- 2026-11-17: card tạo (chờ verify root cause qua prod DB).