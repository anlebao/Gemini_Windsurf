# TASK CARD — Phase 3: UUIDv7 Generation at Order Creation Sites

> **Master plan:** `docs/AI/tasks/order_uuidv7_identity_master_plan.md` (Section 4)
> **Branch:** `feature/order-uuidv7-phase3-generation`
> **Priority:** 1 (High)
> **Mode:** IMPLEMENT
> **Prerequisite:** Phase 2 merged (Domain `Order.Create` syncs `OrderId = Id`)

---

## 0. CONTEXT & DECISIONS (locked)

### Order creation sites (verified 2026-07-16)
**6 total sites** — 3 cần đổi UUIDv7, 3 KHÔNG đổi:

| # | File | Line | Current | Action |
|---|------|------|---------|--------|
| 1 | `3_CoreHub/Services/OrderService.cs` | 563 | `Guid orderId = Guid.NewGuid();` | **Đổi UUIDv7** |
| 2 | `5_WebApps/ShopERP/Controllers/OrdersController.cs` | 169 | `Guid orderId = Guid.NewGuid();` | **Đổi UUIDv7** |
| 3 | `3_CoreHub/Services/OmnichannelOrderService.cs` | 28 | `Guid orderId = Guid.NewGuid();` | **Đổi UUIDv7** |
| 4 | `2_Gateway/Services/DataSyncSubscriber.cs` | 232 | `Guid orderId = orderIdProp.GetGuid();` | **KHÔNG đổi** (đọc từ NATS payload) |
| 5 | `5_WebApps/ShopERP/Services/OrderSyncSubscriber.cs` | ~100 | `Guid orderId = root.GetProperty("Id").GetGuid();` | **KHÔNG đổi** (đọc từ payload) |
| 6 | `5_WebApps/KhachLink/Models/OfflineOrderDto.cs` | 63 | `Guid.Parse(Id)` | **KHÔNG đổi** (đọc từ client offline) |

### UUIDNext API
```csharp
using UUIDNext;
Guid orderId = Uuid.NewDatabaseFriendly(Database.PostgreSql);
// UUIDv7: 48-bit timestamp (ms since 1970) + 74-bit random
// Batch-safe: mỗi UUID > previous ngay cả khi cùng ms
// Format: 019xxxxx-xxxx-7xxx-xxxx-xxxxxxxxxxxx (version nibble = 7)
```

### Why `Database.PostgreSql` not `Database.Sqlite`?
- UUIDv7 format **giống nhau** cho cả 2 DB (timestamp big-endian + random)
- PostgreSQL lưu `uuid` type — UUIDv7 sort đúng binary
- SQLite lưu Guid as TEXT — UUIDv7 sort đúng lexicographically
- **Cùng format, sync OK** — DataSyncSubscriber đọc orderId từ payload, tạo lại bên PostgreSQL với cùng UUIDv7

### RevenueExcelReport fix
- <ref_snippet file="C:/VibeCoding/Gemini_Windsurf/3_CoreHub/Services/Reports/RevenueExcelReport.cs" lines="123" /> — `order.OrderId.Value.ToString()`
- Sau Phase 2: `order.OrderId.Value == order.Id` → dùng trực tiếp `order.Id.ToString()` rõ nghĩa hơn

---

## 1. TASKS

| # | Task ID | Task | Files | Status |
|---|---------|------|-------|--------|
| 1 | P3-T1 | Add `using UUIDNext;` vào top of file + đổi line 563: `Guid orderId = Guid.NewGuid();` → `Guid orderId = Uuid.NewDatabaseFriendly(Database.PostgreSql);` | `3_CoreHub/Services/OrderService.cs` | ⬜ |
| 2 | P3-T2 | Add `using UUIDNext;` vào top of file + đổi line 169: `Guid orderId = Guid.NewGuid();` → `Guid orderId = Uuid.NewDatabaseFriendly(Database.PostgreSql);` | `5_WebApps/ShopERP/Controllers/OrdersController.cs` | ⬜ |
| 3 | P3-T3 | Add `using UUIDNext;` vào top of file + đổi line 28: `Guid orderId = Guid.NewGuid();` → `Guid orderId = Uuid.NewDatabaseFriendly(Database.PostgreSql);` | `3_CoreHub/Services/OmnichannelOrderService.cs` | ⬜ |
| 4 | P3-T4 | Đổi line 123: `order.OrderId.Value.ToString()` → `order.Id.ToString()` | `3_CoreHub/Services/Reports/RevenueExcelReport.cs` | ⬜ |
| 5 | P3-T5 | Verify build: `dotnet build VanAn.sln` 0 errors + `guard-check.ps1` pass | Solution-wide | ⬜ |

---

## 2. EXIT CRITERIA

- [ ] 3 Order creation sites dùng `Uuid.NewDatabaseFriendly(Database.PostgreSql)` (UUIDv7)
- [ ] 3 sites KHÔNG bị sửa: `DataSyncSubscriber`, `OrderSyncSubscriber`, `OfflineOrderDto`
- [ ] `RevenueExcelReport` dùng `order.Id.ToString()` (KHÔNG dùng `order.OrderId.Value`)
- [ ] `using UUIDNext;` added to 3 files (OrderService, OrdersController, OmnichannelOrderService)
- [ ] `dotnet build VanAn.sln` — 0 errors
- [ ] `guard-check.ps1` — PASS

---

## 3. ANTI-PATTERNS (KHÔNG làm)

- ❌ Đổi `DataSyncSubscriber` line 232 — đọc từ NATS payload, KHÔNG generate
- ❌ Đổi `OrderSyncSubscriber` — đọc từ payload, KHÔNG generate
- ❌ Đổi `OfflineOrderDto.ToDomain` — đọc từ client offline, KHÔNG generate
- ❌ Dùng `Guid.NewGuid()` thay UUIDv7 (mất sequential benefit)
- ❌ Dùng `Guid.CreateVersion7()` (.NET 9+ only — project target net8.0)
- ❌ Dùng `Database.Sqlite` thay `Database.PostgreSql` (format khác — sync sẽ break)
- ❌ Thay `Guid.NewGuid()` ở các entity khác (Product, Customer, etc.) — out of scope, chỉ Order

---

## 4. ROLLBACK PLAN

Nếu Phase 3 fail sau 3 rounds:
1. Revert 4 files về commit trước phase
2. Report: compile error cụ thể, evidence
3. **KHÔNG** revert Phase 2 (Domain sync) — Phase 2 độc lập với Phase 3

---

## 5. VERIFICATION CHECKLIST

```powershell
# 1. Build
dotnet build VanAn.sln
# Expected: 0 errors

# 2. Guard check
.\scripts\guard-check.ps1
# Expected: PASS

# 3. UUIDv7 verification (manual)
# - Mở 3 files: OrderService.cs, OrdersController.cs, OmnichannelOrderService.cs
# - Verify mỗi file có `using UUIDNext;`
# - Verify mỗi file dùng `Uuid.NewDatabaseFriendly(Database.PostgreSql)`
# - Mở RevenueExcelReport.cs
# - Verify line 123 dùng `order.Id.ToString()`

# 4. Unchanged files verification (manual)
# - Mở DataSyncSubscriber.cs — verify line 232 vẫn `orderIdProp.GetGuid()`
# - Mở OrderSyncSubscriber.cs — verify vẫn đọc từ payload
# - Mở OfflineOrderDto.cs — verify line 63 vẫn `Guid.Parse(Id)`
```
