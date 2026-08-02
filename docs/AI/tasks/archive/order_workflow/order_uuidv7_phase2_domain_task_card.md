# TASK CARD — Phase 2: Domain — Order.Create Sync OrderId = Id

> **Master plan:** `docs/AI/tasks/order_uuidv7_identity_master_plan.md` (Section 3)
> **Branch:** `feature/order-uuidv7-phase2-domain`
> **Priority:** 0 (Critical — BLOCKING Phase 3+)
> **Mode:** IMPLEMENT (Domain Phase active, user approval granted 2026-07-16)
> **Prerequisite:** Phase 1 merged (UUIDNext dependency available)

---

## 0. CONTEXT & DECISIONS (locked)

### Domain facts (verified 2026-07-16)
- `Order` entity tại <ref_snippet file="C:/VibeCoding/Gemini_Windsurf/1_Shared/Domain.cs" lines="909-1040" />
  - Line 911: `public OrderId OrderId { get; protected set; } = new OrderId(Guid.NewGuid());` — **default random Guid, never set in `Order.Create`**
  - Line 1017-1040: `Order.Create(Guid id, TenantId tenantId, Guid? customerId, List<OrderItem> items)` — set `Id` via reflection (line 1025-1026), **KHÔNG set `OrderId`**
- `BaseEntity` tại <ref_snippet file="C:/VibeCoding/Gemini_Windsurf/1_Shared/Domain/Common.cs" lines="75-117" />
  - Line 77: `public Guid Id { get; protected set; } = Guid.NewGuid();` — PK, auto-generated default
- `record OrderId(Guid Value)` tại <ref_snippet file="C:/VibeCoding/Gemini_Windsurf/1_Shared/Domain.cs" lines="421" /> — **KHÔNG xóa** (ElectronicInvoice.OrderId + PendingInvoiceQueue.OrderId vẫn dùng)
- `OrderItem.OrderId` tại <ref_snippet file="C:/VibeCoding/Gemini_Windsurf/1_Shared/Domain.cs" lines="779" /> — `Guid` FK đến `Order.Id` (PK), KHÔNG phải `Order.OrderId` record

### DB proof (verified 2026-07-16)
4 orders trong SQLite, `Id` ≠ `OrderId` 100%:
```
Id=D99E6AEE-..., OrderId=66637777-...  Equal=False
Id=0707E206-..., OrderId=187520BA-...  Equal=False
Id=BCB4855D-..., OrderId=23EEFC07-...  Equal=False
Id=7FBA15B7-..., OrderId=A8395EA2-...  Equal=False
```

### User decisions (locked 2026-07-16)
- **Approach:** UUIDv7 (giữ Guid PK, thay cách sinh) — approved
- **Single identity:** `Order.OrderId.Value == Order.Id` luôn (sau `Order.Create`)
- **Backward compat:** GIỮ `Order.OrderId` property — code cũ đọc `order.OrderId.Value` vẫn hoạt động
- **Record `OrderId`:** GIỪNG — ElectronicInvoice/PendingInvoiceQueue vẫn dùng

---

## 1. TASKS

| # | Task ID | Task | Files | Status |
|---|---------|------|-------|--------|
| 1 | P2-T1 | Trong `Order.Create` (line 1017-1040), sau line 1026 (`idProperty?.SetValue(order, id);`), thêm 2 dòng để sync `OrderId = new OrderId(id)`: <br/>```csharp<br/>// Sync OrderId domain value object to PK Id (single identity)<br/>System.Reflection.PropertyInfo? orderIdProperty = orderType.GetProperty("OrderId");<br/>orderIdProperty?.SetValue(order, new OrderId(id));<br/>``` | `1_Shared/Domain.cs` (line 1026-1027, insert after) | ⬜ |
| 2 | P2-T2 | Verify build: `dotnet build VanAn.sln` 0 errors + `guard-check.ps1` pass | Solution-wide | ⬜ |

---

## 2. EXIT CRITERIA

- [ ] `Order.Create(id, ...)` → `order.OrderId.Value == order.Id` (single identity)
- [ ] Record `OrderId(Guid Value)` vẫn tồn tại (KHÔNG xóa)
- [ ] `Order.OrderId` property vẫn tồn tại (backward compat)
- [ ] `OrderItem.OrderId` (Guid FK) KHÔNG bị sửa
- [ ] Domain layer vẫn pure (no EF Core, no DbContext, no DataAnnotations)
- [ ] `dotnet build VanAn.sln` — 0 errors
- [ ] `guard-check.ps1` — PASS

---

## 3. ANTI-PATTERNS (KHÔNG làm)

- ❌ Xóa record `OrderId(Guid Value)` (line 421) — ElectronicInvoice/PendingInvoiceQueue dùng
- ❌ Xóa property `Order.OrderId` (line 911) — backward compat
- ❌ Thêm `using Microsoft.EntityFrameworkCore` vào Domain.cs
- ❌ Thêm `[Required]`, `[Column]`, `[Table]` attributes (DataAnnotations)
- ❌ Sửa `BaseEntity.Id` (Common.cs line 77)
- ❌ Sửa `OrderItem.OrderId` (line 779) — FK đến `Order.Id`, không liên quan `Order.OrderId`
- ❌ Thay `Guid.NewGuid()` trong `Order.OrderId` default (line 911) — sẽ bị override bởi `Order.Create` anyway
- ❌ Public setter cho `OrderId` (giữ `protected set` — encapsulation)

---

## 4. ROLLBACK PLAN

Nếu Phase 2 fail sau 3 rounds:
1. Revert `Domain.cs` về commit trước phase
2. Report: compile error cụ thể, evidence
3. **KHÔNG** sửa BaseEntity để workaround
4. **KHÔNG** xóa `Order.OrderId` property để workaround

---

## 5. VERIFICATION CHECKLIST

```powershell
# 1. Build
dotnet build VanAn.sln
# Expected: 0 errors

# 2. Guard check
.\scripts\guard-check.ps1
# Expected: PASS

# 3. Domain purity check (manual)
# - Mở 1_Shared/Domain.cs
# - Verify Order.Create có 2 dòng mới set OrderId = new OrderId(id)
# - Verify record OrderId(Guid Value) vẫn tồn tại (line 421)
# - Verify Order.OrderId property vẫn tồn tại (line 911)
# - Verify KHÔNG có using EF Core / DataAnnotations

# 4. Single identity check (manual reasoning)
# - Order.Create(id, ...) → order.Id = id (line 1026)
# - Order.Create(id, ...) → order.OrderId = new OrderId(id) (new line)
# - Therefore: order.OrderId.Value == order.Id == id ✅
```
