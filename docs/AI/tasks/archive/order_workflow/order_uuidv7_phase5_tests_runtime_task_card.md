# TASK CARD — Phase 5: Test Fixes + Runtime Verification

> **Master plan:** `docs/AI/tasks/order_uuidv7_identity_master_plan.md` (Section 6)
> **Branch:** `feature/order-uuidv7-phase5-tests-runtime`
> **Priority:** 2 (Final validation)
> **Mode:** IMPLEMENT
> **Prerequisite:** Phase 4 merged (EF config + migration created)

---

## 0. CONTEXT & DECISIONS (locked)

### Test facts (verified 2026-07-16)
**3 test files** dùng `Order.OrderId` (record) — cần sửa:

| File | Lines | Issue | Fix |
|------|-------|-------|-----|
| `6_Tests/VanAn.OrderFlow.Tests/OrderApiTests.cs` | 48, 51 | `var orderId = new OrderId(Guid.NewGuid())` + `OrderId = orderId` set | Xóa set `OrderId` (Order.Create tự sync sau Phase 2) |
| same | 80, 142, 176 | Query `o.OrderId.Value == orderId.Value` | Query `o.Id == orderId` (align production) |
| `6_Tests/OrderWorkflowServiceTests.cs` | 110, 208 | `OrderId = new OrderId(Guid.NewGuid())` | Xóa line set `OrderId` |
| `6_Tests/OrderFinancialCalculationTests.cs` | 38, 93, 137, 178, 219 | `OrderId = new OrderId(Guid.NewGuid())` | Xóa line set `OrderId` |

**Note:** `OrderApiTests` có `Skip = "Requires live PostgreSQL — run manually"` — không chạy CI. Sửa cho đúng nhưng không ảnh hưởng CI.

### Runtime verification facts
- ShopERP server: `http://localhost:5003`
- Dev login: `POST /dev/login` (no body needed)
- Order transition: `PUT /api/orderworkflow/{id}/status` with body `{"status":"preparing"}`
- Valid transition (kitchen ON): `pending → preparing` (KHÔNG phải `pending → confirmed`)
- OutboxEvent: log "Enqueued OrderStatusChanged event to Outbox for order {id}: pending → preparing"
- DB: `vanan_shoperp.db` tại `5_WebApps/ShopERP/vanan_shoperp.db`

### UUIDv7 prefix verification
- UUIDv7 timestamp prefix cho 2026-07-16: `019...` (48-bit ms timestamp since 1970)
- New order ID sẽ bắt đầu với `019` hoặc `01A` (tùy ms thời điểm tạo)

---

## 1. TASKS

| # | Task ID | Task | Files | Status |
|---|---------|------|-------|--------|
| 1 | P5-T1 | `OrderApiTests.cs` line 48 — xóa `var orderId = new OrderId(Guid.NewGuid());` (dùng `Guid orderId = Guid.NewGuid();` thay thế nếu cần) | `6_Tests/VanAn.OrderFlow.Tests/OrderApiTests.cs` | ⬜ |
| 2 | P5-T2 | `OrderApiTests.cs` line 51 — xóa `OrderId = orderId` set trong object initializer (Order.Create tự sync) | same | ⬜ |
| 3 | P5-T3 | `OrderApiTests.cs` line 80, 142, 176 — query `o.OrderId.Value == orderId.Value` → `o.Id == orderId` (align production) | same | ⬜ |
| 4 | P5-T4 | `OrderWorkflowServiceTests.cs` line 110, 208 — xóa `OrderId = new OrderId(Guid.NewGuid())` trong object initializer | `6_Tests/OrderWorkflowServiceTests.cs` | ⬜ |
| 5 | P5-T5 | `OrderFinancialCalculationTests.cs` line 38, 93, 137, 178, 219 — xóa `OrderId = new OrderId(Guid.NewGuid())` trong object initializer | `6_Tests/OrderFinancialCalculationTests.cs` | ⬜ |
| 6 | P5-T6 | (Optional) Tạo `OrderIdentityTests.cs` — test `Order.Create(id, ...)` → `Assert.Equal(order.Id, order.OrderId.Value)` | `6_Tests/VanAn.Core.Tests/Domain/OrderIdentityTests.cs` (NEW) | ⬜ |
| 7 | P5-T7 | `dotnet build VanAn.sln` — 0 errors | Solution-wide | ⬜ |
| 8 | P5-T8 | `dotnet test --filter "Order"` — all pass (except skipped PostgreSQL tests) | Solution-wide | ⬜ |
| 9 | P5-T9 | Start ShopERP server: `dotnet run --project 5_WebApps/ShopERP` — no migration errors, server starts on 5003 | `5_WebApps/ShopERP` | ⬜ |
| 10 | P5-T10 | Runtime: `POST /dev/login` → `PUT /api/orderworkflow/{id}/status` with `{"status":"preparing"}` — verify 200 OK (or 500 JSON cycle — separate tech debt TD2) | Runtime | ⬜ |
| 11 | P5-T11 | Check server logs: "Enqueued OrderStatusChanged event to Outbox for order {UUIDv7-id}: pending → preparing" | Runtime | ⬜ |
| 12 | P5-T12 | DB check: `SELECT Id FROM Orders LIMIT 1` — column `OrderId` no longer exists (dropped by migration) | SQLite | ⬜ |
| 13 | P5-T13 | DB check: create new order → `Id` starts with `019...` or `01A...` (UUIDv7 prefix) | SQLite | ⬜ |

---

## 2. EXIT CRITERIA

- [ ] 3 test files sửa: query `o.Id`, xóa set `OrderId = new OrderId(...)`
- [ ] `dotnet build VanAn.sln` — 0 errors
- [ ] `dotnet test --filter "Order"` — all pass (except skipped PostgreSQL tests)
- [ ] (Optional) `OrderIdentityTests.cs` — `Assert.Equal(order.Id, order.OrderId.Value)` pass
- [ ] Server starts on 5003, no migration errors
- [ ] Order transition API: 200 OK (or 500 JSON cycle — TD2)
- [ ] OutboxEvent enqueued (log confirmed)
- [ ] DB: column `Orders.OrderId` dropped (no longer exists)
- [ ] New order ID: UUIDv7 prefix (`019...` or `01A...`)
- [ ] `guard-check.ps1` — PASS

---

## 3. ANTI-PATTERNS (KHÔNG làm)

- ❌ Xóa test files — chỉ sửa query + xóa set `OrderId`
- ❌ Thay `Skip = "Requires live PostgreSQL"` attribute — giữ nguyên skip status
- ❌ Sửa test để query `o.OrderId.Value` (trái ngược production — đây chính là bug cần fix)
- ❌ Chạy `dotnet ef database update` trực tiếp — migration sẽ apply tự động khi server start (EF auto-migrate)
- ❌ Force-create new order nếu server chưa start — cần server running để test runtime
- ❌ Sửa `OrderWorkflowController` để fix JSON cycle (TD2) — out of scope, track separately

---

## 4. ROLLBACK PLAN

Nếu Phase 5 fail sau 3 rounds:
1. Revert 3 test files về commit trước phase
2. Delete `OrderIdentityTests.cs` if created
3. Report: test failure cụ thể, evidence
4. **KHÔNG** revert Phase 4 migration — migration đã tạo, chỉ chưa apply
5. Nếu migration apply fail runtime: revert Phase 4 migration file + restart server

---

## 5. VERIFICATION CHECKLIST

```powershell
# 1. Build
dotnet build VanAn.sln
# Expected: 0 errors

# 2. Guard check
.\scripts\guard-check.ps1
# Expected: PASS

# 3. Tests
dotnet test --filter "Order"
# Expected: all pass (except skipped PostgreSQL tests)

# 4. Runtime — start server
dotnet run --project 5_WebApps/ShopERP
# Expected: server starts on http://localhost:5003, no migration errors

# 5. Runtime — dev login + order transition
$session = $null
Invoke-WebRequest -Uri "http://localhost:5003/dev/login" -Method POST -SessionVariable session
$body = @{ status = "preparing" } | ConvertTo-Json
# Get an existing order ID first (from DB or API)
$pkId = "<existing-order-id>"
Invoke-WebRequest -Uri "http://localhost:5003/api/orderworkflow/$pkId/status" -Method PUT -Body $body -ContentType "application/json" -WebSession $session
# Expected: 200 OK (or 500 JSON cycle — TD2)

# 6. Check server logs for OutboxEvent
# Expected: "Enqueued OrderStatusChanged event to Outbox for order {id}: pending → preparing"

# 7. DB check — column dropped
# Use SQLite check script or EF Core query
# Expected: column "OrderId" no longer exists in "Orders" table

# 8. DB check — UUIDv7 prefix
# Create new order via API, check Id
# Expected: Id starts with "019" or "01A" (UUIDv7 timestamp 2026+)
```
