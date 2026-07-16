# TASK CARD — Phase 4: EF Core Config + DB Migration (Drop Dead Column)

> **Master plan:** `docs/AI/tasks/order_uuidv7_identity_master_plan.md` (Section 5)
> **Branch:** `feature/order-uuidv7-phase4-ef-migration`
> **Priority:** 1 (High)
> **Mode:** IMPLEMENT
> **Prerequisite:** Phase 3 merged (UUIDv7 generation + RevenueExcelReport fix)

---

## 0. CONTEXT & DECISIONS (locked)

### EF Core mapping facts (verified 2026-07-16)
- `OrderConfiguration` tại <ref_snippet file="C:/VibeCoding/Gemini_Windsurf/3_CoreHub/Infrastructure/Configurations/OrderConfiguration.cs" lines="30-33" />
  - Line 30-33: `builder.Property(o => o.OrderId).IsRequired().HasConversion(id => id.Value, value => new OrderId(value))` — map `Order.OrderId` thành column riêng
  - **Cần xóa** block này (Phase 4)
- `ShopERPDbContext` tại <ref_snippet file="C:/VibeCoding/Gemini_Windsurf/5_WebApps/ShopERP/Infrastructure/ShopERPDbContext.cs" lines="119-120" />
  - Line 119-120: `Properties<OrderId>().HaveConversion<OrderIdConverter>()` — **GIỮ** (cần cho ElectronicInvoice.OrderId + PendingInvoiceQueue.OrderId)
- `VanAnDbContext` tại <ref_snippet file="C:/VibeCoding/Gemini_Windsurf/3_CoreHub/Infrastructure/VanAnDbContext.cs" lines="117" />
  - Line 117: `modelBuilder.Ignore<OrderId>()` — **GIỮ** (ngăn EF tạo table cho record)
- `OrderIdConverter` tại <ref_snippet file="C:/VibeCoding/Gemini_Windsurf/3_CoreHub/Infrastructure/ValueConverters/OrderIdConverter.cs" />
  - **GIỮ** file — vẫn cần cho ElectronicInvoice/PendingInvoiceQueue

### DB schema facts (verified 2026-07-16)
- SQLite migration `20260709075842_InitialCreate.cs` line 592: `OrderId = table.Column<Guid>(type: "TEXT", nullable: false)` — column trong `Orders` table
- PostgreSQL migration `20260707125151_InitialCreate.cs` line 666: `OrderId = table.Column<Guid>(type: "uuid", nullable: false)` — column trong `Orders` table
- **No FK, no index** trên `Orders.OrderId` — confirmed dead column
- DB proof: 4 orders, `Id` ≠ `OrderId` 100% — dead data

### Entities dùng record `OrderId` (KHÔNG phải `Order.OrderId`) — GIỮ NGUYÊN
| Entity | Field | EF Config | Action |
|--------|-------|-----------|--------|
| `ElectronicInvoice` | `OrderId` (record) | `ElectronicInvoiceConfiguration` line 23: `.HasConversion(id => id.Value, value => new OrderId(value))` | GIỮ |
| `PendingInvoiceQueue` | `OrderId` (record) | `PendingInvoiceQueueConfiguration` line 17: `.HasConversion(id => id.Value, value => new OrderId(value))` | GIỮ |

---

## 1. TASKS

| # | Task ID | Task | Files | Status |
|---|---------|------|-------|--------|
| 1 | P4-T1 | Xóa block line 30-33: `builder.Property(o => o.OrderId).IsRequired().HasConversion(id => id.Value, value => new OrderId(value));` + comment line 27-29 nếu cần | `3_CoreHub/Infrastructure/Configurations/OrderConfiguration.cs` | ⬜ |
| 2 | P4-T2 | **GIỮ** line 119-120: `Properties<OrderId>().HaveConversion<OrderIdConverter>()` — KHÔNG sửa | `5_WebApps/ShopERP/Infrastructure/ShopERPDbContext.cs` | ⬜ (verify only) |
| 3 | P4-T3 | **GIỮ** line 117: `modelBuilder.Ignore<OrderId>()` — KHÔNG sửa | `3_CoreHub/Infrastructure/VanAnDbContext.cs` | ⬜ (verify only) |
| 4 | P4-T4 | **GIỮ** file `OrderIdConverter.cs` — KHÔNG xóa | `3_CoreHub/Infrastructure/ValueConverters/OrderIdConverter.cs` | ⬜ (verify only) |
| 5 | P4-T5 | Tạo migration SQLite: `dotnet ef migrations add DropOrderOrderIdColumn --context ShopERPDbContext --project 5_WebApps/ShopERP --output-dir Migrations` | `5_WebApps/ShopERP/Migrations/{ts}_DropOrderOrderIdColumn.cs` (NEW) | ⬜ |
| 6 | P4-T6 | Tạo migration PostgreSQL: `dotnet ef migrations add DropOrderOrderIdColumn --context VanAnDbContext --project 3_CoreHub --output-dir Infrastructure/Migrations` | `3_CoreHub/Infrastructure/Migrations/{ts}_DropOrderOrderIdColumn.cs` (NEW) | ⬜ |
| 7 | P4-T7 | Review migration SQL: `dotnet ef migrations script --context ShopERPDbContext` + `dotnet ef migrations script --context VanAnDbContext` — verify `DROP COLUMN "OrderId"` | Solution-wide | ⬜ |
| 8 | P4-T8 | Verify build: `dotnet build VanAn.sln` 0 errors + `guard-check.ps1` pass | Solution-wide | ⬜ |

---

## 2. EXIT CRITERIA

- [ ] `OrderConfiguration` KHÔNG còn mapping cho `Order.OrderId` (line 30-33 xóa)
- [ ] `ShopERPDbContext` vẫn có `Properties<OrderId>().HaveConversion<OrderIdConverter>()` (line 119-120)
- [ ] `VanAnDbContext` vẫn có `modelBuilder.Ignore<OrderId>()` (line 117)
- [ ] `OrderIdConverter.cs` vẫn tồn tại
- [ ] `ElectronicInvoiceConfiguration` + `PendingInvoiceQueueConfiguration` KHÔNG bị sửa
- [ ] 2 migration files tạo thành công (SQLite + PostgreSQL)
- [ ] Migration SQL contains `DROP COLUMN "OrderId"` (PG) / table rebuild (SQLite)
- [ ] `dotnet build VanAn.sln` — 0 errors
- [ ] `guard-check.ps1` — PASS

---

## 3. ANTI-PATTERNS (KHÔNG làm)

- ❌ Xóa `OrderIdConverter.cs` — vẫn cần cho ElectronicInvoice/PendingInvoiceQueue
- ❌ Xóa `Properties<OrderId>().HaveConversion<OrderIdConverter>()` trong ShopERPDbContext — sẽ break ElectronicInvoice mapping
- ❌ Xóa `modelBuilder.Ignore<OrderId>()` trong VanAnDbContext — EF sẽ tạo table cho record
- ❌ Sửa `ElectronicInvoiceConfiguration` hoặc `PendingInvoiceQueueConfiguration` — out of scope
- ❌ Chạy `dotnet ef database update` trực tiếp — chỉ tạo migration, KHÔNG apply (Phase 5 sẽ apply runtime)
- ❌ Drop column `OrderItem.OrderId` — đó là FK đến `Order.Id`, KHÔNG phải `Order.OrderId`
- ❌ Drop column `ElectronicInvoices.OrderId` hoặc `PendingInvoiceQueues.OrderId` — vẫn dùng

---

## 4. ROLLBACK PLAN

Nếu Phase 4 fail sau 3 rounds:
1. Revert `OrderConfiguration.cs` về commit trước phase
2. Delete 2 migration files: `git clean -f 5_WebApps/ShopERP/Migrations/{ts}_DropOrderOrderIdColumn.*` + `git clean -f 3_CoreHub/Infrastructure/Migrations/{ts}_DropOrderOrderIdColumn.*`
3. Report: migration error cụ thể, evidence
4. **KHÔNG** force-apply migration nếu `dotnet ef migrations add` fail

---

## 5. VERIFICATION CHECKLIST

```powershell
# 1. Build
dotnet build VanAn.sln
# Expected: 0 errors

# 2. Guard check
.\scripts\guard-check.ps1
# Expected: PASS

# 3. EF config verification (manual)
# - Mở OrderConfiguration.cs — verify KHÔNG còn builder.Property(o => o.OrderId)
# - Mở ShopERPDbContext.cs — verify line 119-120 vẫn có Properties<OrderId>()
# - Mở VanAnDbContext.cs — verify line 117 vẫn có modelBuilder.Ignore<OrderId>()
# - Mở OrderIdConverter.cs — verify file vẫn tồn tại

# 4. Migration SQL review
dotnet ef migrations script --context ShopERPDbContext --project 5_WebApps/ShopERP
# Expected: contains "DROP COLUMN" or table rebuild for Orders.OrderId

dotnet ef migrations script --context VanAnDbContext --project 3_CoreHub
# Expected: ALTER TABLE "Orders" DROP COLUMN "OrderId"

# 5. Migration files exist
# - Check 5_WebApps/ShopERP/Migrations/ for {ts}_DropOrderOrderIdColumn.cs
# - Check 3_CoreHub/Infrastructure/Migrations/ for {ts}_DropOrderOrderIdColumn.cs
```
