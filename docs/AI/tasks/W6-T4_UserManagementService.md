# TASK CARD: SERVICE - WAVE 6 - User Management Service

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Implement `IUserManagementService` interface và `UserManagementService` class — business operations cho DemoUser aggregate: Create (BCrypt hash in service), GetById, List, UpdateProfile, ChangePassword, Deactivate (với last-Owner guard), Reactivate.
- **Nghiệp vụ áp dụng:** Owner của một Tenant quản lý users trong tenant của họ. Owner không thể deactivate chính họ nếu là Owner duy nhất còn active (tránh tenant bị "mồ côi"). Cross-tenant isolation: user của Tenant A không thể bị quản lý bởi Owner của Tenant B.

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `.devin/workflows/newfeaturebuild.md`
- **Execution Mode:** IMPLEMENT

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md`
  - `3_CoreHub/Services/IUserManagementService.cs` — TẠO MỚI
  - `3_CoreHub/Services/UserManagementService.cs` — TẠO MỚI
  - `3_CoreHub/Program.cs` hoặc `5_WebApps/ShopERP/Program.cs` — SỬA: DI registration
  - `1_Shared/Domain/Aggregates/UserAggregate/DemoUser.cs` — ĐỌC (domain methods)
  - `3_CoreHub/Infrastructure/VanAnDbContext.cs` — ĐỌC để biết DbSets và IVanAnDbContext
- **Boundary Rules (Nghiêm cấm):**
  - KHÔNG inject `VanAnDbContext` trực tiếp — chỉ dùng `IVanAnDbContext`
  - KHÔNG set DemoUser properties trực tiếp — chỉ gọi domain methods
  - KHÔNG hash password trong DemoUser domain class — BCrypt.HashPassword PHẢI ở service layer
  - KHÔNG propagate `BCryptException` sang caller — wrap trong `InvalidOperationException` với user-friendly message
  - KHÔNG catch `InvalidOperationException` từ domain methods (last-owner guard được throw trước khi gọi domain, domain exception propagate)

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **BCrypt Work Factor:** `BCrypt.HashPassword(plainPassword, workFactor: 12)` — KHÔNG dùng default work factor (< 10 là insecure).
- [ ] **Last Owner Guard (Service Layer):** Trước khi gọi `user.Deactivate()`, kiểm tra: `count active Owner users trong tenant == 1 AND user.Role == Owner` → throw `InvalidOperationException("Cannot deactivate the last Owner of a tenant")`. KHÔNG để domain handle này.
- [ ] **Cross-Tenant Guard:** `GetUserByIdAsync`, `DeactivateUserAsync`, `UpdateProfileAsync`, `ChangePasswordAsync` phải verify `user.TenantId == tenantId` — nếu không match → throw `UnauthorizedAccessException("Access to user from different tenant is not allowed")`.
- [ ] **Duplicate Username Guard:** `CreateUserAsync` với `username` đã tồn tại trong cùng `tenantId` → throw `InvalidOperationException("Username already exists in this tenant")`.
- [ ] **Async All The Way:** Tất cả methods phải `async Task<>` — KHÔNG dùng `.Result` hoặc `.Wait()`.

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC-1:** `CreateUserAsync(tenantId, "alice", "plainpass", "Alice", UserRole.Staff)` → user được persist với BCrypt hash (không plain text), `IsActive == true`.
- [ ] **SC-2:** `CreateUserAsync(tenantId, "alice", ...)` lần 2 với cùng username/tenantId → `InvalidOperationException("Username already exists...")`.
- [ ] **SC-3:** `GetUserByIdAsync(userId, tenantId)` với user thuộc tenant khác → `UnauthorizedAccessException`.
- [ ] **SC-4:** `GetUserByIdAsync(userId, tenantId)` với ID không tồn tại → trả về `null`.
- [ ] **SC-5:** `DeactivateUserAsync` với user là Owner duy nhất active trong tenant → `InvalidOperationException("Cannot deactivate the last Owner...")`.
- [ ] **SC-6:** `DeactivateUserAsync` với user là 1 trong 2 Owners → Success, user.IsActive == false.
- [ ] **SC-7:** `ChangePasswordAsync(userId, tenantId, "newplainpass")` → PasswordHash được update với BCrypt hash của "newplainpass".
- [ ] **SC-8:** DI registration thêm vào Program.cs: `AddScoped<IUserManagementService, UserManagementService>()`.
- [ ] **SC-9:** Unit tests minimum 10 cases PASS (SC1-SC7 + thêm các edge cases).
- [ ] **SC-10:** `dotnet build VanAn.sln` → 0 errors. `guard-check.ps1` PASS.

**Implementation Date:** 2026-06-23
**Branch:** feature/wave6-user-rbac-mgmt

## 6. ACTIVE SKILLS (MAX 3)
- `domain-integrity-validation` — Enforce: chỉ domain methods thay đổi state, BCrypt ở service layer
- `build-error-analysis` — Handle BCrypt package availability, IVanAnDbContext resolution
- `test-system-upgrade` — 10 unit test cases với mock IVanAnDbContext, BCrypt mock

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Verified Facts:**
  - Fact 1: `class DemoUser : BaseEntity` tại `Domain.cs` line 930: `Username, PasswordHash, DisplayName, Role (UserRole), IsActive`
  - Fact 2: `DemoUser` aggregate (W6-T1) có domain methods: `Deactivate()`, `Reactivate()`, `ChangePassword(hash)`, `AssignRole(role)`, `UpdateProfile(displayName)`
  - Fact 3: `DemoUser.Deactivate()` khi `IsActive == false` → throws `InvalidOperationException` (guard trong domain)
  - Fact 4: Governance: `3_CoreHub MUST remain pure Class Library (.dll)`
  - Fact 5: `class UserTenant` tại `Domain.cs` line 946: `UserId (Guid), TenantId (Guid), Role (string), AssignedAt, IsActive`
  - Fact 6: Last-Owner guard phải ở SERVICE layer (không phải domain) — service query count Active Owners trước khi gọi Deactivate
  - Fact 7: BCrypt work factor = 12 (security requirement stated in spec)
- **Assumptions:**
  - BCrypt.Net-Next NuGet package đã có trong `3_CoreHub.csproj` (cần verify)
  - `IVanAnDbContext` có `DbSet<DemoUser> Users` và `DbSet<UserTenant> UserTenants` (cần verify khi đọc VanAnDbContext.cs)
- **Open Questions:**
  - Q1: BCrypt.Net-Next đã installed trong project chưa? Nếu chưa → thêm package reference là phần của task này?
  - Q2: `ListUsersAsync(TenantId tenantId)` có cần pagination (skip/take) không? Hay return all users trong tenant?
- **Recommended Action:** IMPLEMENT — đọc VanAnDbContext.cs để verify DbSets → implement service → tests

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| `IUserManagementService.cs` (mới) | W6-T7 Controller depend on interface | Interface phải stable trước W6-T7 |
| `UserManagementService.cs` (mới) | Không có downstream impact ngay | N/A |
| `Program.cs` (DI) | Registration phải không conflict với existing | Dùng `TryAddScoped` hoặc verify không duplicate |
| BCrypt package (nếu thêm mới) | Build time tăng nhẹ, NuGet restore needed | Verify `dotnet restore` sau khi thêm package |

## 9. TDD & E2E TESTING STRATEGY
- **Unit Test Cases (minimum 10):**
  - TC01: CreateUser → user saved, PasswordHash != plainPassword (BCrypt applied)
  - TC02: CreateUser duplicate username same tenant → InvalidOperationException
  - TC03: CreateUser duplicate username different tenant → OK (different tenant)
  - TC04: GetUser valid ID + correct tenant → returns user
  - TC05: GetUser valid ID + wrong tenant → UnauthorizedAccessException
  - TC06: GetUser nonexistent ID → returns null
  - TC07: ListUsers → all users in tenant returned
  - TC08: DeactivateUser — last Owner → InvalidOperationException(last owner message)
  - TC09: DeactivateUser — 2nd Owner → Success
  - TC10: ChangePassword → BCrypt hash updated
  - TC11: ReactivateUser → IsActive=true
  - TC12: UpdateProfile → DisplayName updated
- **Test Setup:**
  - Mock `IVanAnDbContext` với in-memory collections
  - BCrypt: không mock (use real BCrypt for integration confidence), hoặc interface wrap nếu needed
- **Test boundary:**
  - Unit tests: `6_Tests/` — mock IVanAnDbContext
  - Integration tests: N/A trong task này
  - E2E tests: N/A

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Chiến lược thực thi: JIT Planning + Pure Execution

Task này cần 2 sessions: Session 1 interface + skeleton + guard logic. Session 2 full implementation + unit tests.

### Micro-phase breakdown cho W6-T4

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | Đọc `VanAnDbContext.cs` → verify DbSet<DemoUser>, DbSet<UserTenant>. Verify BCrypt package. Xác định IVanAnDbContext interface (add DbSets nếu cần). Xác định Last-Owner guard query (LINQ count Active Owners) | Tạo `IUserManagementService.cs` với 7 method signatures. Tạo `UserManagementService.cs` skeleton với constructor. Implement CreateUserAsync với BCrypt + duplicate check. Implement GetUserByIdAsync với cross-tenant guard. Verify `dotnet build` |
| **S2** | Review remaining methods. Xác định Last-Owner guard LINQ query: `context.UserTenants.CountAsync(ut => ut.TenantId == tenantId && ut.Role == UserRole.Owner && ut.IsActive)` | Implement ListUsersAsync, UpdateProfileAsync, ChangePasswordAsync, DeactivateUserAsync (with last-owner guard), ReactivateUserAsync. DI registration. Viết 10+ unit tests. Run `guard-check.ps1` |

### Rules
- BCrypt.HashPassword PHẢI ở service, KHÔNG trong DemoUser constructor/methods
- Cross-tenant guard phải kiểm tra TRƯỚC khi load entity (query filter ngay từ đầu)
- Last-owner guard: query TRƯỚC khi gọi `user.Deactivate()` — hai operations riêng biệt

## 11. ESTIMATED EFFORT
- 2 sessions (75-90 phút total)
- **Phụ thuộc:** W6-T1 (DemoUser aggregate), W6-T2 (UserRole enum, UserTenant type)
- **BLOCKER:** Nếu BCrypt package không có trong project → thêm `<PackageReference Include="BCrypt.Net-Next" Version="4.0.3" />` vào `3_CoreHub.csproj` trước khi implement
