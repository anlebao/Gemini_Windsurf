# MASTER IMPLEMENTATION PLAN — Order UUIDv7 Single Identity Refactor

> **Status:** APPROVED — 5 task cards created, ready for IMPLEMENT
> **Created:** 2026-07-16
> **Reviewed:** 2026-07-16 — deep reverse-impact analysis complete, root cause verified with DB data
> **Target Workflow:** `newfeaturebuild.md` (ANALYZE → IMPLEMENT)
> **Branch strategy:** `main` → feature branches per phase (sequential)
> **Execution principle:** JIT Planning + Pure Execution + Domain-First
> **Prerequisite:** Verify session (2026-07-16) — `Order.OrderId` dead column confirmed, dual-identity defect root-caused
> **Reference:** `docs/AI/tasks/quicksetup_product_management_master_plan.md` (format template)
>
> **Task cards (locked 2026-07-16):**
> - Phase 1: `docs/AI/tasks/order_uuidv7_phase1_dependency_task_card.md`
> - Phase 2: `docs/AI/tasks/order_uuidv7_phase2_domain_task_card.md`
> - Phase 3: `docs/AI/tasks/order_uuidv7_phase3_uuidv7_generation_task_card.md`
> - Phase 4: `docs/AI/tasks/order_uuidv7_phase4_ef_migration_task_card.md`
> - Phase 5: `docs/AI/tasks/order_uuidv7_phase5_tests_runtime_task_card.md`

---

## 0. EXECUTION RULES

### JIT Planning Strategy
**Nguyên tắc:** Investigate trước, Implement sau. KHÔNG code mò mẫm.

**Bước 1: INVESTIGATE** — Verify existing code structure, service signatures, EF mappings
**Bước 2: IMPLEMENT** — Theo plan đã chốt, mỗi phase xong chạy `guard-check.ps1` + `dotnet build`

### Session protocol
1. Mỗi session chỉ làm 1 phase
2. Bắt đầu session: Đọc `project_state.md` + task card phase đang làm
3. Sau khi plan chốt: Execution Phase
4. Trước session end: Build + test
5. Sau mỗi phase: Commit `[REFACTOR P{N}] Task description`

### Branch protocol
```
main
  └── feature/order-uuidv7-phase1-dependency
      └── feature/order-uuidv7-phase2-domain
          └── feature/order-uuidv7-phase3-generation
              └── feature/order-uuidv7-phase4-ef-migration
                  └── feature/order-uuidv7-phase5-tests-runtime
```

### Hard rules
- **Domain layer:** Phase 2 được phép sửa `Domain.cs` (`Order.Create` thêm 2 dòng sync `OrderId = new OrderId(id)`) — có user approval (UUIDv7 approach approved 2026-07-16)
- **BaseEntity:** KHÔNG sửa `BaseEntity.Id` — giữ Guid PK, chỉ thay cách sinh (UUIDv7 thay Guid.NewGuid)
- **record `OrderId`:** KHÔNG xóa record `OrderId(Guid Value)` — `ElectronicInvoice.OrderId` + `PendingInvoiceQueue.OrderId` vẫn dùng
- **`OrderIdConverter`:** KHÔNG xóa — vẫn cần cho `ElectronicInvoice.OrderId` + `PendingInvoiceQueue.OrderId` EF mapping
- **UUIDNext library:** Phiên bản 4.2.4 (published 2026-04-04, >3 months, pass 7-day governance). Dùng `Uuid.NewDatabaseFriendly(Database.PostgreSql)` cho cả SQLite + PostgreSQL (UUIDv7 format same, sort đúng trên cả 2 DB).
- **CPM:** Thêm `UUIDNext` vào `Directory.Packages.props` (Central Package Management), KHÔNG thêm version vào từng .csproj
- **Cross-DB sync:** KHÔNG sửa `DataSyncSubscriber` + `OrderSyncSubscriber` — chúng đọc `orderId` từ event payload (ID đã tạo bên kia). UUIDv7 deterministic khi seed cùng timestamp.
- **Migration:** Drop column `Orders.OrderId` — dead data (verified: 4 rows, `Id` ≠ `OrderId` 100%). Backup trước khi migrate.
- **AccountingEntry:** KHÔNG đụng (immutable, out of scope)
- **Playwright DISABLED** — đây là refactor, không có UI change
- **Multi-tenancy:** KHÔNG thay đổi tenant filter logic

### Critical context
- **Architecture:** KhachLink (5002) → Gateway (5001) → ShopERP (5003) → SQLite (business) + PostgreSQL (accounting)
- **Dual-identity defect:** `Order` entity có 2 Guid:
  - `Id` (PK from `BaseEntity`, line 77 `Common.cs`) — set bằng reflection trong `Order.Create` (line 1025-1026 `Domain.cs`)
  - `OrderId` (record `OrderId`, line 911 `Domain.cs`) — **default `new OrderId(Guid.NewGuid())`, KHÔNG BAO GIỜ được set trong `Order.Create`** → dead column
- **DB proof (verified 2026-07-16):** 4 orders trong SQLite, `Id` ≠ `OrderId` 100%:
  ```
  Id=D99E6AEE-..., OrderId=66637777-...  Equal=False
  Id=0707E206-..., OrderId=187520BA-...  Equal=False
  Id=BCB4855D-..., OrderId=23EEFC07-...  Equal=False
  Id=7FBA15B7-..., OrderId=A8395EA2-...  Equal=False
  ```
- **OrderItem.OrderId:** `Guid` (line 779 `Domain.cs`) — FK đến `Order.Id` (PK), KHÔNG phải `Order.OrderId` record. `OrderItemConfiguration` line 49: `HasForeignKey(e => e.OrderId)`.
- **EF mapping:** `OrderConfiguration` line 31-33: `builder.Property(o => o.OrderId).HasConversion(...)` — map `Order.OrderId` thành column riêng. Cần xóa mapping này (Phase 4).
- **ShopERPDbContext** line 119-120: `Properties<OrderId>().HaveConversion<OrderIdConverter>()` — **GIỮ NGUYÊN** (cần cho ElectronicInvoice/PendingInvoiceQueue)
- **VanAnDbContext** line 117: `modelBuilder.Ignore<OrderId>()` — **GIỮ NGUYÊN**
- **Cross-DB sync:** `DataSyncSubscriber` line 232: `Guid orderId = orderIdProp.GetGuid()` — đọc từ NATS event payload, tạo lại Order trong PostgreSQL với cùng `orderId`. `OrderSyncSubscriber` line 106: tương tự SQLite. **KHÔNG sửa 2 file này.**
- **Order creation sites (6 total):**
  - `OrderService.cs:563` — `Guid orderId = Guid.NewGuid()` → cần đổi UUIDv7
  - `OrdersController.cs:169` — `Guid orderId = Guid.NewGuid()` → cần đổi UUIDv7
  - `OmnichannelOrderService.cs:28` — `Guid orderId = Guid.NewGuid()` → cần đổi UUIDv7
  - `DataSyncSubscriber.cs:232` — đọc từ payload → KHÔNG đổi
  - `OrderSyncSubscriber.cs` — đọc từ payload → KHÔNG đổi
  - `OfflineOrderDto.cs:63` — `Guid.Parse(Id)` từ client → KHÔNG đổi
- **Production code dùng `Order.OrderId` (record):** chỉ 1 chỗ — `RevenueExcelReport.cs:123` `order.OrderId.Value.ToString()`. Sửa thành `order.Id.ToString()`.
- **Tests dùng `Order.OrderId` (record):** 3 file — `OrderApiTests.cs` (3 chỗ query `o.OrderId.Value ==`), `OrderWorkflowServiceTests.cs` (2 chỗ set `OrderId = new OrderId(...)`), `OrderFinancialCalculationTests.cs` (5 chỗ set `OrderId = new OrderId(...)`).
- **UUIDNext API:** `Uuid.NewDatabaseFriendly(Database.PostgreSql)` → `Guid` (UUIDv7: 48-bit timestamp + 74-bit random, batch-safe — mỗi UUID > previous ngay cả khi cùng ms)
- **CPM:** `Directory.Packages.props` line 3: `<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>`

---

## 1. CURRENT ISSUES SUMMARY

### Issue A1: Dual-identity defect — `Order.OrderId` dead column
**Status:** ❌ DEFECT (verified with DB data)
**Priority:** 0 (Critical — architectural smell, data integrity risk)

`Order` entity có 2 Guid identity:
- `Id` (PK) — set trong `Order.Create` via reflection
- `OrderId` (record) — **default `new OrderId(Guid.NewGuid())`, never set in `Order.Create`** → sinh Guid random độc lập, không liên quan `Id`

DB proof: 4 orders, `Id` ≠ `OrderId` 100%. Cột `Orders.OrderId` chứa dead data (Guid random không dùng cho query/FK/index nào).

### Issue A2: DTO naming lie — field `OrderId` = PK `Id`
**Status:** ⚠️ TECH DEBT (out of scope — tracked separately)
**Priority:** 2 (Low — không gây bug, chỉ gây confusion)

50+ DTO map `OrderId = order.Id` (PK). Field tên `OrderId` nhưng giá trị là PK `Id`. **KHÔNG sửa trong plan này** — sẽ track trong technical debt backlog (Option B: DTO rename).

### Issue A3: Tests query bằng `Order.OrderId.Value` — trái ngược production
**Status:** ❌ FALSE SAFETY
**Priority:** 1 (High — test không phản ánh production)

`OrderApiTests.cs` line 80, 142, 176: query `o.OrderId.Value == orderId.Value` — **trái ngược production** (production query `o.Id == orderId`). Tests có `Skip = "Requires live PostgreSQL"` → không chạy CI → không phát hiện inconsistency.

---

## 2. PHASE 1 — Add UUIDNext Dependency (CPM)

**Branch:** `feature/order-uuidv7-phase1-dependency`
**Priority:** 0 (Critical — BLOCKING Phase 2+)
**Task Card:** `docs/AI/tasks/order_uuidv7_phase1_dependency_task_card.md`

### Mục tiêu
Thêm UUIDNext 4.2.4 vào CPM + 5 project references. UUIDNext cung cấp UUIDv7 generation (timestamp-prefixed, collision-free, batch-safe).

### Tasks
| # | Task ID | Task | Files | Status |
|---|---------|------|-------|--------|
| 1 | P1-T1 | Add `<PackageVersion Include="UUIDNext" Version="4.2.4" />` vào CPM | `Directory.Packages.props` | ⬜ |
| 2 | P1-T2 | Add `<PackageReference Include="UUIDNext" />` vào 5 projects: Shared, CoreHub, ShopERP, Gateway, KhachLink | 5 .csproj files | ⬜ |
| 3 | P1-T3 | `dotnet restore` — verify tất cả packages resolve | Solution-wide | ⬜ |
| 4 | P1-T4 | Verify build: 0 errors + guard-check.ps1 pass | Solution-wide | ⬜ |

### Exit criteria
- [ ] `Directory.Packages.props` có `<PackageVersion Include="UUIDNext" Version="4.2.4" />`
- [ ] 5 .csproj files có `<PackageReference Include="UUIDNext" />` (không có Version attribute — CPM quản lý)
- [ ] `dotnet restore` thành công — không có NU1xxx warnings
- [ ] Build: 0 errors

---

## 3. PHASE 2 — Domain: Order.Create Sync OrderId = Id

**Branch:** `feature/order-uuidv7-phase2-domain`
**Priority:** 0 (Critical — BLOCKING Phase 3+)
**Task Card:** `docs/AI/tasks/order_uuidv7_phase2_domain_task_card.md`

### Mục tiêu
Fix dual-identity defect: trong `Order.Create`, sau khi set `Id` via reflection, set thêm `OrderId = new OrderId(id)` → `Order.OrderId.Value == Order.Id` luôn. Single identity.

### Tasks
| # | Task ID | Task | Files | Status |
|---|---------|------|-------|--------|
| 1 | P2-T1 | Trong `Order.Create` (line 1017-1040), sau line 1026 (`idProperty?.SetValue(order, id)`), thêm 2 dòng: `PropertyInfo? orderIdProperty = orderType.GetProperty("OrderId"); orderIdProperty?.SetValue(order, new OrderId(id));` | `1_Shared/Domain.cs` | ⬜ |
| 2 | P2-T2 | Verify build: 0 errors + guard-check.ps1 pass | Solution-wide | ⬜ |

### Exit criteria
- [ ] `Order.Create(id, ...)` → `order.OrderId.Value == order.Id` (single identity)
- [ ] Record `OrderId(Guid Value)` vẫn tồn tại (KHÔNG xóa — ElectronicInvoice/PendingInvoiceQueue vẫn dùng)
- [ ] `Order.OrderId` property vẫn tồn tại (backward compat — code cũ đọc `order.OrderId.Value` vẫn hoạt động)
- [ ] Domain layer vẫn pure (no EF Core, no DbContext, no DataAnnotations)
- [ ] Build: 0 errors

---

## 4. PHASE 3 — UUIDv7 Generation at Order Creation Sites

**Branch:** `feature/order-uuidv7-phase3-generation`
**Priority:** 1 (High)
**Task Card:** `docs/AI/tasks/order_uuidv7_phase3_generation_task_card.md`

### Mục tiêu
Thay `Guid.NewGuid()` → `Uuid.NewDatabaseFriendly(Database.PostgreSql)` tại 3 sites tạo Order ID mới. 3 sites đọc từ payload/client KHÔNG đổi.

### Tasks
| # | Task ID | Task | Files | Status |
|---|---------|------|-------|--------|
| 1 | P3-T1 | `OrderService.cs:563` — `Guid.NewGuid()` → `Uuid.NewDatabaseFriendly(Database.PostgreSql)` + add `using UUIDNext;` | `3_CoreHub/Services/OrderService.cs` | ⬜ |
| 2 | P3-T2 | `OrdersController.cs:169` — `Guid.NewGuid()` → `Uuid.NewDatabaseFriendly(Database.PostgreSql)` + add `using UUIDNext;` | `5_WebApps/ShopERP/Controllers/OrdersController.cs` | ⬜ |
| 3 | P3-T3 | `OmnichannelOrderService.cs:28` — `Guid.NewGuid()` → `Uuid.NewDatabaseFriendly(Database.PostgreSql)` + add `using UUIDNext;` | `3_CoreHub/Services/OmnichannelOrderService.cs` | ⬜ |
| 4 | P3-T4 | `RevenueExcelReport.cs:123` — `order.OrderId.Value.ToString()` → `order.Id.ToString()` | `3_CoreHub/Services/Reports/RevenueExcelReport.cs` | ⬜ |
| 5 | P3-T5 | Verify build: 0 errors + guard-check.ps1 pass | Solution-wide | ⬜ |

### Exit criteria
- [ ] 3 Order creation sites dùng `Uuid.NewDatabaseFriendly(Database.PostgreSql)` (UUIDv7)
- [ ] `DataSyncSubscriber` + `OrderSyncSubscriber` + `OfflineOrderDto` KHÔNG bị sửa (đọc từ payload/client)
- [ ] `RevenueExcelReport` dùng `order.Id` (KHÔNG dùng `order.OrderId.Value`)
- [ ] Build: 0 errors
- [ ] New order ID có prefix `019...` hoặc `01A...` (UUIDv7 timestamp 2026+)

---

## 5. PHASE 4 — EF Core Config + DB Migration (Drop Dead Column)

**Branch:** `feature/order-uuidv7-phase4-ef-migration`
**Priority:** 1 (High)
**Task Card:** `docs/AI/tasks/order_uuidv7_phase4_ef_migration_task_card.md`

### Mục tiêu
Xóa EF Core mapping cho `Order.OrderId` ( KHÔNG xóa `OrderIdConverter` — vẫn cần cho ElectronicInvoice). Tạo migration drop column `Orders.OrderId` cho cả SQLite + PostgreSQL.

### Tasks
| # | Task ID | Task | Files | Status |
|---|---------|------|-------|--------|
| 1 | P4-T1 | Xóa block `builder.Property(o => o.OrderId).IsRequired().HasConversion(...)` (line 30-33) | `3_CoreHub/Infrastructure/Configurations/OrderConfiguration.cs` | ⬜ |
| 2 | P4-T2 | **GIỮ** `Properties<OrderId>().HaveConversion<OrderIdConverter>()` trong ShopERPDbContext (line 119-120) — cần cho ElectronicInvoice/PendingInvoiceQueue | `5_WebApps/ShopERP/Infrastructure/ShopERPDbContext.cs` | ⬜ |
| 3 | P4-T3 | **GIỮ** `modelBuilder.Ignore<OrderId>()` trong VanAnDbContext (line 117) | `3_CoreHub/Infrastructure/VanAnDbContext.cs` | ⬜ |
| 4 | P4-T4 | **GIỮ** file `OrderIdConverter.cs` — vẫn cần | `3_CoreHub/Infrastructure/ValueConverters/OrderIdConverter.cs` | ⬜ |
| 5 | P4-T5 | Tạo migration SQLite: `dotnet ef migrations add DropOrderOrderIdColumn --context ShopERPDbContext --project 5_WebApps/ShopERP` | `5_WebApps/ShopERP/Migrations/{ts}_DropOrderOrderIdColumn.cs` (NEW) | ⬜ |
| 6 | P4-T6 | Tạo migration PostgreSQL: `dotnet ef migrations add DropOrderOrderIdColumn --context VanAnDbContext --project 3_CoreHub` | `3_CoreHub/Infrastructure/Migrations/{ts}_DropOrderOrderIdColumn.cs` (NEW) | ⬜ |
| 7 | P4-T7 | Review migration SQL: `dotnet ef migrations script --context ShopERPDbContext` + `dotnet ef migrations script --context VanAnDbContext` | Solution-wide | ⬜ |
| 8 | P4-T8 | Verify build: 0 errors + guard-check.ps1 pass | Solution-wide | ⬜ |

### Exit criteria
- [ ] `OrderConfiguration` KHÔNG còn mapping cho `Order.OrderId`
- [ ] `ShopERPDbContext` vẫn có `Properties<OrderId>().HaveConversion<OrderIdConverter>()` (cho ElectronicInvoice)
- [ ] `VanAnDbContext` vẫn có `modelBuilder.Ignore<OrderId>()`
- [ ] `OrderIdConverter.cs` vẫn tồn tại
- [ ] 2 migration files tạo thành công (SQLite + PostgreSQL)
- [ ] Migration SQL: `ALTER TABLE "Orders" DROP COLUMN "OrderId"` (PG) / table rebuild (SQLite)
- [ ] Build: 0 errors

---

## 6. PHASE 5 — Test Fixes + Runtime Verification

**Branch:** `feature/order-uuidv7-phase5-tests-runtime`
**Priority:** 2 (Final validation)
**Task Card:** `docs/AI/tasks/order_uuidv7_phase5_tests_runtime_task_card.md`

### Mục tiêu
Sửa 3 test files (align với production — query `o.Id`, xóa set `OrderId = new OrderId(...)`). Add new test verify single identity. Runtime verify: order transition + OutboxEvent + DB schema.

### Tasks
| # | Task ID | Task | Files | Status |
|---|---------|------|-------|--------|
| 1 | P5-T1 | `OrderApiTests.cs` line 48, 51 — xóa `var orderId = new OrderId(...)` + `OrderId = orderId` set (Order.Create tự sync) | `6_Tests/VanAn.OrderFlow.Tests/OrderApiTests.cs` | ⬜ |
| 2 | P5-T2 | `OrderApiTests.cs` line 80, 142, 176 — query `o.OrderId.Value == orderId.Value` → `o.Id == orderId` | same | ⬜ |
| 3 | P5-T3 | `OrderWorkflowServiceTests.cs` line 110, 208 — xóa `OrderId = new OrderId(Guid.NewGuid())` | `6_Tests/OrderWorkflowServiceTests.cs` | ⬜ |
| 4 | P5-T4 | `OrderFinancialCalculationTests.cs` line 38, 93, 137, 178, 219 — xóa `OrderId = new OrderId(Guid.NewGuid())` | `6_Tests/OrderFinancialCalculationTests.cs` | ⬜ |
| 5 | P5-T5 | (Optional) Add test: `Order.Create(id, ...)` → `Assert.Equal(order.Id, order.OrderId.Value)` | `6_Tests/VanAn.Core.Tests/Domain/OrderIdentityTests.cs` (NEW) | ⬜ |
| 6 | P5-T6 | `dotnet test --filter "Order"` — all pass | Solution-wide | ⬜ |
| 7 | P5-T7 | Start ShopERP server — no migration errors | `5_WebApps/ShopERP` | ⬜ |
| 8 | P5-T8 | Runtime: `POST /dev/login` → `PUT /api/orderworkflow/{id}/status` (valid transition: pending→preparing) | Runtime | ⬜ |
| 9 | P5-T9 | Check server logs: "Enqueued OrderStatusChanged event to Outbox for order {UUIDv7-id}: pending → preparing" | Runtime | ⬜ |
| 10 | P5-T10 | DB check: `SELECT Id FROM Orders LIMIT 1` — column `OrderId` no longer exists | SQLite | ⬜ |
| 11 | P5-T11 | DB check: new order created → `Id` starts with `019...` or `01A...` (UUIDv7 prefix) | SQLite | ⬜ |

### Exit criteria
- [ ] 3 test files sửa: query `o.Id`, xóa set `OrderId = new OrderId(...)`
- [ ] `dotnet test --filter "Order"` — all pass
- [ ] Server starts, no migration errors
- [ ] Order transition API: 200 OK (or 500 JSON cycle — separate tech debt)
- [ ] OutboxEvent enqueued (log confirmed)
- [ ] DB: column `Orders.OrderId` dropped
- [ ] New order ID: UUIDv7 prefix (`019...` or `01A...`)
- [ ] Build: 0 errors

---

## 7. PHASE DEPENDENCY GRAPH

```
PHASE 1 (Dependency) ← BLOCKING Phase 2+
      │
      └── PHASE 2 (Domain: Order.Create sync)
            │
            └── PHASE 3 (UUIDv7 Generation + RevenueExcelReport fix)
                  │
                  └── PHASE 4 (EF Config + Migration)
                        │
                        └── PHASE 5 (Tests + Runtime)
```

**Critical path:** P1 → P2 → P3 → P4 → P5 (sequential, no parallelism)

---

## 8. RISK ASSESSMENT

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|------------|
| Domain modification (`Order.Create`) — Hard Stop | Low (approved) | High | User approved UUIDv7 approach 2026-07-16. Change minimal (2 lines). |
| UUIDNext dependency supply chain | Low | Medium | Published 2026-04-04 (>3 months), 381 GitHub stars, 0BSD license, .NET 8 compatible. Pinned 4.2.4. |
| DB migration drop column — data loss | Low | Low | Column `Orders.OrderId` is dead data (verified: 4 rows, random Guid, no FK/index). Backup before migrate. |
| `OrderIdConverter` broken | None | High | Kept converter + `Properties<OrderId>()` registration (for ElectronicInvoice/PendingInvoiceQueue). |
| Cross-DB sync (SQLite ↔ PostgreSQL) broken | None | High | UUIDv7 same format both DBs. `DataSyncSubscriber` + `OrderSyncSubscriber` read orderId from payload — unchanged. |
| Existing 4 orders in SQLite | Low | Low | `Id` stays (PK). `OrderId` column dropped — dead data loss only. |
| Test compile errors | Low | Low | Build will catch — 3 test files mapped (Phase 5). |
| UUIDv7 sort order khác UUIDv4 | None | Low | UUIDv7 sort ascending = chronological. Better for DB index. No code depends on Guid sort order. |
| `RevenueExcelReport` hiển thị mã khác sau refactor | Low | Low | Trước: `order.OrderId.Value` (random Guid). Sau: `order.Id` (PK). Cả 2 đều là Guid string — user không để ý sự khác biệt. |

---

## 9. FILE INVENTORY

### Files to CREATE (NEW)
| File | Phase | Purpose |
|------|-------|---------|
| `5_WebApps/ShopERP/Migrations/{ts}_DropOrderOrderIdColumn.cs` | P4 | SQLite migration drop dead column |
| `3_CoreHub/Infrastructure/Migrations/{ts}_DropOrderOrderIdColumn.cs` | P4 | PostgreSQL migration drop dead column |
| `6_Tests/VanAn.Core.Tests/Domain/OrderIdentityTests.cs` | P5 | (Optional) Test single identity invariant |

### Files to MODIFY (EXISTING)
| File | Phase | Changes |
|------|-------|---------|
| `Directory.Packages.props` | P1 | Add `<PackageVersion Include="UUIDNext" Version="4.2.4" />` |
| `1_Shared/VanAn.Shared.csproj` | P1 | Add `<PackageReference Include="UUIDNext" />` |
| `3_CoreHub/VanAn.CoreHub.csproj` | P1 | Add `<PackageReference Include="UUIDNext" />` |
| `5_WebApps/ShopERP/VanAn.ShopERP.csproj` | P1 | Add `<PackageReference Include="UUIDNext" />` |
| `2_Gateway/VanAn.Gateway.csproj` | P1 | Add `<PackageReference Include="UUIDNext" />` |
| `5_WebApps/KhachLink/VanAn.KhachLink.csproj` | P1 | Add `<PackageReference Include="UUIDNext" />` |
| `1_Shared/Domain.cs` | P2 | `Order.Create` thêm 2 dòng sync `OrderId = new OrderId(id)` |
| `3_CoreHub/Services/OrderService.cs` | P3 | `Guid.NewGuid()` → `Uuid.NewDatabaseFriendly(Database.PostgreSql)` + `using UUIDNext;` |
| `5_WebApps/ShopERP/Controllers/OrdersController.cs` | P3 | Same as above |
| `3_CoreHub/Services/OmnichannelOrderService.cs` | P3 | Same as above |
| `3_CoreHub/Services/Reports/RevenueExcelReport.cs` | P3 | `order.OrderId.Value.ToString()` → `order.Id.ToString()` |
| `3_CoreHub/Infrastructure/Configurations/OrderConfiguration.cs` | P4 | Xóa block `builder.Property(o => o.OrderId)...` (line 30-33) |
| `6_Tests/VanAn.OrderFlow.Tests/OrderApiTests.cs` | P5 | Query `o.Id`, xóa set `OrderId = new OrderId(...)` |
| `6_Tests/OrderWorkflowServiceTests.cs` | P5 | Xóa set `OrderId = new OrderId(...)` |
| `6_Tests/OrderFinancialCalculationTests.cs` | P5 | Xóa set `OrderId = new OrderId(...)` |

### Files NOT MODIFIED (verified safe)
| File | Reason |
|------|--------|
| `1_Shared/Domain/Common.cs` (BaseEntity) | KHÔNG sửa BaseEntity.Id |
| `1_Shared/Domain.cs` line 421 (record `OrderId`) | GIỮ — ElectronicInvoice/PendingInvoiceQueue dùng |
| `1_Shared/Domain.cs` line 911 (`Order.OrderId` property) | GIỮ — backward compat, sẽ = `Order.Id` sau Phase 2 |
| `3_CoreHub/Infrastructure/ValueConverters/OrderIdConverter.cs` | GIỮ — cần cho ElectronicInvoice/PendingInvoiceQueue |
| `5_WebApps/ShopERP/Infrastructure/ShopERPDbContext.cs` line 119-120 | GIỮ `Properties<OrderId>().HaveConversion<OrderIdConverter>()` |
| `3_CoreHub/Infrastructure/VanAnDbContext.cs` line 117 | GIỮ `modelBuilder.Ignore<OrderId>()` |
| `2_Gateway/Services/DataSyncSubscriber.cs` | KHÔNG sửa — đọc orderId từ payload |
| `5_WebApps/ShopERP/Services/OrderSyncSubscriber.cs` | KHÔNG sửa — đọc orderId từ payload |
| `5_WebApps/KhachLink/Models/OfflineOrderDto.cs` | KHÔNG sửa — đọc Id từ client |

---

## 10. SUCCESS METRICS

| Metric | Target |
|--------|--------|
| Single identity | ✅ `Order.OrderId.Value == Order.Id` luôn (sau `Order.Create`) |
| UUIDv7 generation | ✅ New order ID prefix `019...` or `01A...` (timestamp 2026+) |
| Dead column dropped | ✅ `Orders.OrderId` column không tồn tại sau migration |
| Cross-DB sync | ✅ SQLite → PostgreSQL sync vẫn hoạt động (DataSyncSubscriber unchanged) |
| OutboxEvent | ✅ OrderStatusChanged event enqueued sau transition |
| Build | ✅ 0 errors |
| Tests | ✅ `dotnet test --filter "Order"` all pass |
| Domain purity | ✅ Domain.cs không có EF Core / DataAnnotations |
| BaseEntity untouched | ✅ `Common.cs` không bị sửa |
| AccountingEntry untouched | ✅ Immutable, out of scope |

---

## 11. TECH DEBT TRACKED (OUT OF SCOPE)

| # | Debt | Severity | Recommendation | Future Plan |
|---|------|----------|----------------|-------------|
| TD1 | DTO field `OrderId` = PK `Id` (naming lie) | Low | Track trong technical debt backlog | Option B: DTO rename (separate PR) |
| TD2 | JSON serialization cycle — `TransitionStatus` returns raw `Order` entity → 500 | Medium | Track | Fix: return DTO or `ReferenceHandler.IgnoreCycles` |
| TD3 | Ambiguous 404 — `NotFound()` cho cả "order not found" + "invalid transition" | Low | Track | Fix: `BadRequest()` with descriptive message |

---

## 12. GOVERNANCE COMPLIANCE

| Rule | Status |
|------|--------|
| Hard Stop: Domain modification | ✅ User approved UUIDv7 approach 2026-07-16 |
| BaseEntity.Id untouched | ✅ No change to BaseEntity |
| AccountingEntry immutable | ✅ No change |
| Single Source of Truth (Domain.cs) | ✅ Change in Domain.cs only |
| CPM (Directory.Packages.props) | ✅ Add to CPM, not individual .csproj |
| Minimum release age 7 days | ✅ UUIDNext 4.2.4 published 2026-04-04 (>3 months) |
| No floating ranges | ✅ Pinned to 4.2.4 |
| TDD: Retrofit tests | ✅ Phase 5 updates tests + optional new identity test |
| Build validation | ✅ Phase 5 includes `dotnet build VanAn.sln` |
| Clean Architecture | ✅ No layer violation — change in Domain + Services + Config |
| Multi-tenancy | ✅ No tenant filter change |

**Status:** Plan ready for IMPLEMENT. Awaiting user go-ahead.
