# TASK CARD: SERVICE - WAVE 5 - Tenant Management Service

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Implement `ITenantManagementService` interface và `TenantManagementService` class trong `3_CoreHub/Services/` — cung cấp business operations cho Tenant aggregate lifecycle (Create, Read, List, UpdateProfile, Suspend, Deactivate).
- **Nghiệp vụ áp dụng:** Quản lý vòng đời Tenant trong VanAn ERP: SystemAdmin tạo tenant mới (Status=Pending), kích hoạt (Active), đình chỉ (Suspended khi vi phạm), và vô hiệu hóa (Inactive khi đóng cửa). Service layer là duy nhất được gọi domain methods — không có code nào khác set properties trực tiếp.

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `.devin/workflows/newfeaturebuild.md`
- **Execution Mode:** IMPLEMENT

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md`
  - `3_CoreHub/Services/ITenantManagementService.cs` — TẠO MỚI
  - `3_CoreHub/Services/TenantManagementService.cs` — TẠO MỚI
  - `3_CoreHub/Program.cs` — SỬA: thêm DI registration
  - `1_Shared/Domain/Aggregates/TenantAggregate/Tenant.cs` — ĐỌC (domain methods)
  - `3_CoreHub/Infrastructure/VanAnDbContext.cs` — ĐỌC để biết `IVanAnDbContext` interface hoặc verify DbSet
- **Boundary Rules (Nghiêm cấm):**
  - KHÔNG inject `VanAnDbContext` trực tiếp vào service — chỉ dùng `IVanAnDbContext` interface
  - KHÔNG set Tenant properties trực tiếp (`tenant.Status = ...`) — chỉ gọi domain methods
  - KHÔNG catch `InvalidOperationException` từ domain methods — propagate lên caller (W5-T6 Controller sẽ handle)
  - KHÔNG thêm business logic vào Controller layer
  - KHÔNG import presentation layer concerns vào service

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **IVanAnDbContext:** Service PHẢI inject `IVanAnDbContext` (interface), không inject concrete `VanAnDbContext`. Nếu interface chưa tồn tại → tạo `IVanAnDbContext` trước.
- [ ] **Domain Method Only:** `CreateTenantAsync` gọi `Tenant.Create(...)` factory, KHÔNG `new Tenant { Name = ... }`.
- [ ] **DomainException Propagation:** `DeactivateTenantAsync` và `SuspendTenantAsync` KHÔNG wrap exceptions — let them propagate (Controller sẽ catch và return 422).
- [ ] **Multi-tenancy NOT Applied:** Tenant table là cross-tenant entity — `ListTenantsAsync` KHÔNG filter theo current tenant context (đây là SystemAdmin-level operation).
- [ ] **Async All The Way:** Tất cả methods phải `async Task<>` — KHÔNG dùng `.Result` hoặc `.Wait()`.

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC-1:** `CreateTenantAsync("ShopA", BusinessType.Restaurant, null, "owner@test.com")` → tenant được persist với `Status == TenantStatus.Pending`.
- [ ] **SC-2:** `GetTenantByIdAsync(nonExistentId)` → trả về `null` (không throw).
- [ ] **SC-3:** `ListTenantsAsync(includeInactive: false)` → chỉ trả về tenants có Status ∈ {Pending, Active, Suspended}.
- [ ] **SC-4:** `ListTenantsAsync(includeInactive: true)` → trả về tất cả tenants kể cả Inactive/Terminated.
- [ ] **SC-5:** `SuspendTenantAsync(id, "reason")` khi tenant Active → tenant.Status == Suspended, SaveChanges thành công.
- [ ] **SC-6:** `DeactivateTenantAsync(id, "reason")` khi tenant Suspended → propagates `InvalidOperationException` (KHÔNG swallow exception).
- [ ] **SC-7:** DI registration trong `Program.cs`: `builder.Services.AddScoped<ITenantManagementService, TenantManagementService>()`.
- [ ] **SC-8:** Unit tests minimum 8 cases PASS (Create, GetById null, List filter, Suspend happy, Suspend wrong state, Deactivate from Suspended throws, UpdateProfile success, UpdateProfile wrong state).
- [ ] **SC-9:** `dotnet build VanAn.sln` → 0 errors.
- [ ] **SC-10:** `guard-check.ps1` PASS.

**Implementation Date:** 2026-06-23
**Branch:** feature/wave5-tenant-mgmt

## 6. ACTIVE SKILLS (MAX 3)
- `domain-integrity-validation` — Enforce: chỉ domain methods thay đổi state, không direct property set
- `build-error-analysis` — Handle IVanAnDbContext resolution, DbSet<Tenant> type ambiguity
- `test-system-upgrade` — Viết 8 unit test cases với mock IVanAnDbContext

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Verified Facts:**
  - Fact 1: `DbSet<Tenant> Tenants` trong `VanAnDbContext.cs` — đã tồn tại
  - Fact 2: `Tenant` class (W5-T2) có domain methods: `Activate()`, `Suspend(reason)`, `Deactivate(reason)`, `UpdateProfile(name, email, address)`, `UpdateSettings(settings)`, static factory `Create(...)`
  - Fact 3: `Tenant.Deactivate()` từ Suspended → throws `InvalidOperationException` (domain guard)
  - Fact 4: Governance: `3_CoreHub MUST remain pure Class Library (.dll). NO <OutputType>Exe</OutputType>`
  - Fact 5: Governance: `No business logic allowed in Controllers, Gateway, or Hubs`
  - Fact 6: `TenantStatus` enum: `Pending, Active, Suspended, Inactive, Terminated` (từ W5-T2)
  - Fact 7: `CreateTenantAsync` phát `TenantCreatedEvent` — sẽ handle ở W5-T7 (event handler, chưa implement)
- **Assumptions:**
  - `IVanAnDbContext` interface đã tồn tại hoặc cần tạo với `DbSet<Tenant> Tenants` property
  - `Program.cs` của `3_CoreHub` là nơi đăng ký DI (cần verify — CoreHub là class library, có thể DI ở ShopERP)
- **Open Questions:**
  - Q1: `IVanAnDbContext` interface có sẵn chưa? Nếu chưa, task này có phép tạo không? (Tạo interface là clean architecture requirement — có thể tạo trong task này)
  - Q2: DI đăng ký ở `3_CoreHub/Program.cs` hay `5_WebApps/ShopERP/Program.cs`? (CoreHub là class library — DI thường ở host project)
- **Recommended Action:** IMPLEMENT — nhưng cần đọc `VanAnDbContext.cs` trước để resolve Q1 và Q2

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| `ITenantManagementService.cs` (mới) | W5-T6 Controller phụ thuộc interface này | Interface phải stable trước khi W5-T6 bắt đầu |
| `TenantManagementService.cs` (mới) | Không có downstream impact ngay | N/A |
| `Program.cs` (DI registration) | Nếu DI ở ShopERP, cần verify không conflict với existing registrations | Dùng `TryAddScoped` nếu muốn idempotent |
| `IVanAnDbContext.cs` (tạo mới nếu cần) | Tất cả services inject interface thay vì concrete | Là Clean Architecture improvement — no breaking change nếu implement đúng |

## 9. TDD & E2E TESTING STRATEGY
- **Unit Test Cases (minimum 8):**
  - UC1: `CreateTenantAsync` → tenant saved, Status=Pending, TenantCreatedEvent in DomainEvents
  - UC2: `GetTenantByIdAsync` với valid ID → trả về tenant
  - UC3: `GetTenantByIdAsync` với không tồn tại ID → trả về null
  - UC4: `ListTenantsAsync(false)` → không include Inactive/Terminated
  - UC5: `ListTenantsAsync(true)` → include tất cả
  - UC6: `SuspendTenantAsync` khi Active → Success, Status=Suspended
  - UC7: `SuspendTenantAsync` khi Suspended → propagates InvalidOperationException
  - UC8: `DeactivateTenantAsync` khi Suspended → propagates InvalidOperationException (domain guard)
  - UC9: `UpdateProfileAsync` khi Active → Success
  - UC10: `UpdateProfileAsync` khi Inactive → propagates domain exception
- **Test Setup:**
  - Mock `IVanAnDbContext` với in-memory list hoặc dùng EF Core In-Memory provider
  - Tenant instances tạo qua `Tenant.Create(...)` factory
- **Test boundary:**
  - Unit tests: `6_Tests/` — mock IVanAnDbContext
  - Integration tests: N/A trong task này
  - E2E tests: N/A

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Chiến lược thực thi: JIT Planning + Pure Execution

Task này cần 2 sessions: Session 1 tạo interface + service skeleton. Session 2 implement đầy đủ + unit tests.

### Micro-phase breakdown cho W5-T5

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | Đọc `VanAnDbContext.cs` → verify IVanAnDbContext tồn tại hay không. Đọc existing services pattern trong `3_CoreHub/Services/`. Xác định DI location (ShopERP hay CoreHub extension method) | Tạo `IVanAnDbContext.cs` nếu cần (với `DbSet<Tenant> Tenants`). Tạo `ITenantManagementService.cs` với 6 method signatures. Tạo `TenantManagementService.cs` skeleton (constructor inject). Verify `dotnet build` |
| **S2** | Review `Tenant.cs` domain methods → map từng service method sang domain call. Xác nhận exception propagation pattern | Implement đầy đủ 6 methods trong `TenantManagementService`. Thêm DI registration. Viết 8+ unit tests. Run `guard-check.ps1` |

### Rules
- Mỗi service method phải: (1) Find tenant by ID, (2) Guard null check, (3) Gọi domain method, (4) SaveChanges, (5) Return/throw
- KHÔNG thêm try-catch cho domain exceptions — let them propagate
- Unit tests phải mock IVanAnDbContext — không dùng real DB

## 11. ESTIMATED EFFORT
- 2 sessions (75-90 phút total)
- **Phụ thuộc:** W5-T2 (Tenant aggregate), W5-T4 (EF mapping updated)
- **BLOCKER:** Nếu `IVanAnDbContext` chưa tồn tại và có nhiều DbSets phức tạp → tạo interface riêng chỉ expose `DbSet<Tenant>` và `Task<int> SaveChangesAsync()` là đủ cho task này
