# TASK CARD: SERVICE - WAVE 6 - Role Assignment & Permission Group Service

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Implement `IRoleAssignmentService` + `RoleAssignmentService` (quản lý UserTenant relationships và group assignments) và `IPermissionGroupService` + `PermissionGroupService` (CRUD PermissionGroups, add/remove roles). Cung cấp `GetEffectiveRolesAsync` = union direct roles + group roles.
- **Nghiệp vụ áp dụng:** Owner của Tenant assign roles cho users trực tiếp (UserTenant) hoặc qua PermissionGroup. `GetEffectiveRolesAsync` là operation quan trọng nhất — được gọi mỗi request để check authorization. Cross-tenant isolation: Owner của Tenant A KHÔNG được thao tác trên User/Group thuộc Tenant B.

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `.devin/workflows/newfeaturebuild.md`
- **Execution Mode:** IMPLEMENT

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md`
  - `3_CoreHub/Services/IRoleAssignmentService.cs` — TẠO MỚI
  - `3_CoreHub/Services/RoleAssignmentService.cs` — TẠO MỚI
  - `3_CoreHub/Services/IPermissionGroupService.cs` — TẠO MỚI
  - `3_CoreHub/Services/PermissionGroupService.cs` — TẠO MỚI
  - `3_CoreHub/Program.cs` hoặc `5_WebApps/ShopERP/Program.cs` — SỬA: DI registrations (4 services)
  - `1_Shared/Domain/Aggregates/UserAggregate/PermissionGroup.cs` — ĐỌC (từ W6-T2)
  - `3_CoreHub/Infrastructure/VanAnDbContext.cs` — ĐỌC để verify DbSets (UserTenants, PermissionGroups, UserPermissionGroups)
- **Boundary Rules (Nghiêm cấm):**
  - KHÔNG inject `VanAnDbContext` trực tiếp — chỉ `IVanAnDbContext`
  - KHÔNG set PermissionGroup._roles backing field trực tiếp — gọi `group.AddRole()` / `group.RemoveRole()`
  - KHÔNG bypass cross-tenant guard trong bất kỳ method nào
  - KHÔNG thêm caching trong task này (performance optimization là future task)
  - KHÔNG implement granular permissions — chỉ bundle roles (Phán quyết D2)

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **Cross-Tenant Guard — TẤT CẢ methods:** Mỗi method trong `IRoleAssignmentService` và `IPermissionGroupService` phải verify userId/groupId thuộc về tenantId. Nếu không match → `throw new UnauthorizedAccessException("Cross-tenant operation is not allowed")`.
- [ ] **GetEffectiveRolesAsync = Union:** Direct roles (từ UserTenant) + Group roles (qua UserPermissionGroup → PermissionGroup) → UNION → DISTINCT. Không duplicate roles trong result.
- [ ] **Soft Delete UserTenant:** `RevokeRoleAsync` → set `UserTenant.IsActive = false` — KHÔNG xóa record (audit trail).
- [ ] **PermissionGroup Tenant Verification:** `GetGroupAsync(groupId, tenantId)` phải verify `group.TenantId == tenantId` — không chỉ check by ID.
- [ ] **Async All The Way:** KHÔNG dùng `.Result`, `.Wait()`, hay synchronous EF Core methods.

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC-1:** `AssignRoleToUserAsync(userId, tenantId, UserRole.StoreKeeper)` → `UserTenant` record created/updated với Role=StoreKeeper.
- [ ] **SC-2:** `RevokeRoleAsync(userId, tenantId, UserRole.StoreKeeper)` → UserTenant.IsActive = false (soft delete).
- [ ] **SC-3:** `GetUserRolesAsync(userId, tenantId)` → list roles từ active UserTenant records.
- [ ] **SC-4:** `GetEffectiveRolesAsync` với user có direct role Owner + group có roles [StoreKeeper, Masterchef] → returns [Owner, StoreKeeper, Masterchef] (union distinct).
- [ ] **SC-5:** `GetEffectiveRolesAsync` với user có direct role Owner + group có role Owner → returns [Owner] (no duplicate).
- [ ] **SC-6:** `AssignUserToGroupAsync(userId, groupId, tenantId)` khi groupId thuộc tenant khác → `UnauthorizedAccessException`.
- [ ] **SC-7:** `CreateGroupAsync(tenantId, "Group A", "desc")` → PermissionGroup created với empty roles.
- [ ] **SC-8:** `AddRoleToGroupAsync(groupId, tenantId, UserRole.Guard)` → role added to group.
- [ ] **SC-9:** DI registrations: 4 services added. `guard-check.ps1` PASS.
- [ ] **SC-10:** Unit tests minimum 6 cases cho `RoleAssignmentService` PASS. `dotnet build` → 0 errors.

**Implementation Date:** 2026-06-23
**Branch:** feature/wave6-user-rbac-mgmt

## 6. ACTIVE SKILLS (MAX 3)
- `domain-integrity-validation` — Enforce cross-tenant guard, domain method calls only
- `build-error-analysis` — Handle 4 new files + DI registration
- `test-system-upgrade` — Unit tests với mock IVanAnDbContext, cross-tenant scenarios

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Verified Facts:**
  - Fact 1: `class UserTenant` tại `Domain.cs` line 946: `UserId (Guid), TenantId (Guid), Role (string → UserRole sau W6-T2), AssignedAt, IsActive`
  - Fact 2: `PermissionGroup` class (W6-T2): methods `AddRole(UserRole)`, `RemoveRole(UserRole)`, `GetEffectiveRoles() → IReadOnlyList<UserRole>`
  - Fact 3: `UserPermissionGroup` class (W6-T2): `UserId, GroupId, TenantId, AssignedAt`
  - Fact 4: Phán quyết D2: bundle roles (không granular permissions)
  - Fact 5: Governance: `No business logic allowed in Controllers, Gateway, or Hubs`
  - Fact 6: Cross-tenant isolation là Hard Stop Rule — Owner Tenant A KHÔNG thao tác trên Tenant B
  - Fact 7: `GetEffectiveRoles` = UNION of (direct UserTenant roles) + (roles from all PermissionGroups user belongs to)
- **Assumptions:**
  - `IVanAnDbContext` sẽ có `DbSet<UserTenant> UserTenants`, `DbSet<PermissionGroup> PermissionGroups`, `DbSet<UserPermissionGroup> UserPermissionGroups` (cần verify + thêm nếu thiếu)
  - `UserTenant` upgraded (W6-T2) với `UserRole` enum type đã có trước khi task này bắt đầu
- **Open Questions:**
  - Q1: Một user có thể có NHIỀU UserTenant records với cùng Role không? (nếu assign Role.StoreKeeper 2 lần → 1 record hay 2?) → Service phải handle upsert
  - Q2: `PermissionGroup` cần thêm vào DbContext không? EF tracking cho `PermissionGroup` cần OwnsMany pattern cho `_roles` list?
- **Recommended Action:** IMPLEMENT — nhưng verify DbContext DbSets trước (Q2 critical)

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| `IRoleAssignmentService.cs` (mới) | W6-T7 Controller depend on interface | Freeze interface trước W6-T7 |
| `IPermissionGroupService.cs` (mới) | W6-T7 Controller depend on interface | Freeze interface trước W6-T7 |
| `RoleAssignmentService.cs` (mới) | Không có downstream impact ngay | N/A |
| `PermissionGroupService.cs` (mới) | Không có downstream impact ngay | N/A |
| `Program.cs` (4 DI registrations) | Potential registration conflict | Verify không duplicate registration names |
| `IVanAnDbContext` (có thể cần thêm DbSets) | All services using IVanAnDbContext bị ảnh hưởng | Interface extension — backward compatible |

## 9. TDD & E2E TESTING STRATEGY
- **Unit Test — RoleAssignmentService (minimum 6):**
  - TC01: AssignRole → UserTenant created
  - TC02: AssignRole twice same role → upsert (no duplicate record)
  - TC03: RevokeRole → UserTenant.IsActive = false
  - TC04: GetEffectiveRoles — direct only → correct list
  - TC05: GetEffectiveRoles — direct + group → union distinct
  - TC06: AssignUserToGroup cross-tenant → UnauthorizedAccessException
- **Unit Test — PermissionGroupService:**
  - TC07: CreateGroup → empty PermissionGroup persisted
  - TC08: AddRoleToGroup → role added, GetEffectiveRoles updated
  - TC09: ListGroups(tenantId) → only groups for that tenant
  - TC10: GetGroup(id, wrongTenantId) → UnauthorizedAccessException
- **Test boundary:**
  - Unit tests: mock IVanAnDbContext, in-memory collections
  - Integration tests: N/A trong task này
  - E2E tests: N/A

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Chiến lược thực thi: JIT Planning + Pure Execution

Task này cần 2 sessions: 4 files + DI. Session 1 interfaces + RoleAssignment. Session 2 PermissionGroup + tests.

### Micro-phase breakdown cho W6-T5

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | Đọc `VanAnDbContext.cs` → verify/add DbSets (UserTenants, PermissionGroups, UserPermissionGroups). Xác định upsert pattern cho AssignRole. Xác định cross-tenant guard reusable pattern | Tạo `IRoleAssignmentService.cs` (6 methods). Tạo `RoleAssignmentService.cs`: implement AssignRole, RevokeRole, GetUserRoles, AssignUserToGroup, RemoveUserFromGroup, GetEffectiveRoles. Verify `dotnet build` |
| **S2** | Xác định PermissionGroup EF tracking (OwnsMany cho roles list? hay separate entity?). Review GetEffectiveRoles LINQ query | Tạo `IPermissionGroupService.cs` (6 methods). Tạo `PermissionGroupService.cs`. DI registrations (4). Viết unit tests (10+). Run `guard-check.ps1` |

### Rules
- GetEffectiveRoles LINQ: `var directRoles = await context.UserTenants.Where(ut => ut.UserId == userId && ut.TenantId == tenantId && ut.IsActive).Select(ut => ut.Role).ToListAsync()`
- Cross-tenant guard: extract helper method `VerifyTenantAccess(entityTenantId, requestedTenantId)` — call từ mọi public method
- PermissionGroup._roles storage: nếu EF không support `List<UserRole>` OwnsMany → store as CSV string với conversion

## 11. ESTIMATED EFFORT
- 2 sessions (90-120 phút total)
- **Phụ thuộc:** W6-T2 (PermissionGroup, UserPermissionGroup domain types), W6-T4 (IVanAnDbContext pattern established)
- **BLOCKER:** EF Core không hỗ trợ `List<UserRole>` OwnsMany natively → cần EF ValueConverter để serialize List<UserRole> → string (JSON hoặc CSV). Giải pháp: `HasConversion<string>` với custom converter — cần implement trong W6-T3 (migration task)
