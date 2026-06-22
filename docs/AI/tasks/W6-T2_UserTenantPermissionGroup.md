# TASK CARD: DOMAIN - WAVE 6 - UserTenant Upgrade & PermissionGroup

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Tạo các domain types hỗ trợ RBAC trong `UserAggregate/`: `UserRole` enum (file riêng), `UserTenant` upgraded (Role từ `string` → `UserRole`), `PermissionGroup` class (bundle roles), và `UserPermissionGroup` mapping entity — cơ sở cho hệ thống phân quyền bundle-based (Phán quyết D2).
- **Nghiệp vụ áp dụng:** RBAC theo Phán quyết D2 — dùng `PermissionGroup` bundle roles thay vì granular permissions. Một Owner có thể tạo PermissionGroup "Kho + Bếp" với roles [StoreKeeper, Masterchef], rồi assign nhiều users vào group đó. `GetEffectiveRoles()` trả về union của direct roles + group roles.

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `.devin/workflows/newfeaturebuild.md`
- **Execution Mode:** IMPLEMENT

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md`
  - `1_Shared/Domain/Aggregates/UserAggregate/UserRole.cs` — TẠO MỚI (copy enum từ Domain.cs)
  - `1_Shared/Domain/Aggregates/UserAggregate/UserTenant.cs` — TẠO MỚI (upgraded version)
  - `1_Shared/Domain/Aggregates/UserAggregate/PermissionGroup.cs` — TẠO MỚI
  - `1_Shared/Domain/Aggregates/UserAggregate/UserPermissionGroup.cs` — TẠO MỚI
  - `1_Shared/Domain.cs` — ĐỌC để xem `enum UserRole` (line 399), `class UserTenant` (line 946) — KHÔNG SỬA trong task này
- **Boundary Rules (Nghiêm cấm):**
  - KHÔNG xóa `enum UserRole` trong `Domain.cs` — chỉ `[Obsolete]` mark sau khi file mới compile OK
  - KHÔNG xóa `class UserTenant` trong `Domain.cs` — để W6-T3 (migration task) xử lý
  - KHÔNG import EF Core vào Domain layer
  - KHÔNG thêm `[Obsolete]` vào `Domain.cs` trong task này — chỉ sau khi file mới confirmed compile
  - KHÔNG tạo granular permissions — chỉ bundle roles (Phán quyết D2)

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **Domain Purity:** Tất cả 4 files mới KHÔNG import EF Core, DataAnnotations, hoặc infrastructure libraries.
- [ ] **Namespace:** `VanAn.Shared.Domain` cho tất cả files — đồng nhất với Domain.cs.
- [ ] **UserRole Duplicate:** Tạm thời sẽ có 2 `UserRole` enum definitions (Domain.cs và UserRole.cs). Cần dùng fully-qualified name nếu có ambiguity. `[Obsolete]` tag vào Domain.cs version CHỈ SAU KHI build thành công.
- [ ] **PermissionGroup Immutable Roles List:** `_roles` là private `List<UserRole>` — chỉ có thể modify qua `AddRole()`/`RemoveRole()` methods. `GetEffectiveRoles()` trả về `IReadOnlyList<UserRole>`.
- [ ] **UserTenant Role Field:** `UserRole Role` thay vì `string Role` — EF Core mapping cần update (W6-T3 task).

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC-1:** `UserRole.cs` contains enum với 6 values: `None, Owner, StoreKeeper, Guard, Staff, Masterchef` — identical to Domain.cs line 399.
- [ ] **SC-2:** `new UserTenant { UserId = ..., TenantId = ..., Role = UserRole.Owner }` — compile OK với `UserRole` type (không phải `string`).
- [ ] **SC-3:** `new PermissionGroup(tenantId, "Group A", "Description")` → `GetEffectiveRoles()` returns empty list.
- [ ] **SC-4:** `group.AddRole(UserRole.StoreKeeper); group.AddRole(UserRole.Masterchef)` → `GetEffectiveRoles()` returns [StoreKeeper, Masterchef].
- [ ] **SC-5:** `group.AddRole(UserRole.StoreKeeper); group.AddRole(UserRole.StoreKeeper)` → `GetEffectiveRoles()` returns distinct [StoreKeeper] (no duplicates).
- [ ] **SC-6:** `group.RemoveRole(UserRole.Masterchef)` → role removed from list.
- [ ] **SC-7:** `UserPermissionGroup` có `UserId`, `GroupId`, `TenantId`, `AssignedAt` properties.
- [ ] **SC-8:** `dotnet build VanAn.sln` → 0 errors (có thể có CS0618 warnings sau khi add [Obsolete] vào Domain.cs UserRole).
- [ ] **SC-9:** Architecture tests 7/7 PASS.

**Implementation Date:** 2026-06-23
**Branch:** feature/wave6-user-rbac-mgmt

## 6. ACTIVE SKILLS (MAX 3)
- `domain-integrity-validation` — Enforce domain purity, no infrastructure imports
- `system-refactor-safety` — Parallel enum definition, [Obsolete] strategy
- `build-error-analysis` — Handle duplicate UserRole ambiguity

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Verified Facts:**
  - Fact 1: `enum UserRole` tại `1_Shared/Domain.cs` line 399: `None, Owner, StoreKeeper, Guard, Staff, Masterchef`
  - Fact 2: `class UserTenant` tại `1_Shared/Domain.cs` line 946: `UserId (Guid), TenantId (Guid), Role (string), AssignedAt, IsActive`
  - Fact 3: Phán quyết D2: `PermissionGroup` bundle roles (không granular permissions)
  - Fact 4: Governance: Domain layer pure — NO EF Core, NO DataAnnotations
  - Fact 5: Namespace `VanAn.Shared.Domain` — tất cả files mới trong `UserAggregate/` dùng cùng namespace
  - Fact 6: `AggregateRoot` base class đã có (W5-T1) — `PermissionGroup` nên kế thừa `AggregateRoot` (nó là AR của PermissionGroup bounded context)
  - Fact 7: `UserTenant` là join table (User ↔ Tenant relationship) — không phải AR, có thể kế thừa `BaseEntity`
- **Assumptions:**
  - `PermissionGroup` là Aggregate Root (có Id, TenantId, domain methods) → kế thừa `AggregateRoot`
  - `UserPermissionGroup` là mapping entity → kế thừa `BaseEntity` (có Id, TenantId)
  - `UserTenant` upgraded → kế thừa `BaseEntity` (không cần AR — không có domain events riêng)
- **Open Questions:**
  - Q1: `UserTenant.Role` là `UserRole` (single role) hay `List<UserRole>` (multiple roles per tenant)? Spec nói string → UserRole enum (single) — confirm OK?
  - Q2: `PermissionGroup` có cần phát domain events (GroupRoleAddedEvent, etc.) không? Hay chỉ PermissionGroup data entity?
- **Recommended Action:** IMPLEMENT — 4 files, 1 session

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| `UserRole.cs` (mới) | Duplicate với Domain.cs UserRole — potential CS0104 ambiguous reference | Verify both in same namespace, add [Obsolete] to Domain.cs version sau build OK |
| `UserTenant.cs` (mới) | Domain.cs `class UserTenant` vẫn tồn tại — parallel definition | W6-T3 migration task sẽ [Obsolete] Domain.cs version |
| `PermissionGroup.cs` (mới) | W6-T5 service sẽ depend on this — interface phải stable | Freeze PermissionGroup API trước W6-T5 |
| `UserPermissionGroup.cs` (mới) | EF Core mapping cần update (W6-T3) | N/A trong task này |

## 9. TDD & E2E TESTING STRATEGY
- **Unit Test — PermissionGroup:**
  - Test: new PermissionGroup → GetEffectiveRoles() returns empty
  - Test: AddRole(StoreKeeper) → GetEffectiveRoles() returns [StoreKeeper]
  - Test: AddRole(StoreKeeper) twice → GetEffectiveRoles() returns [StoreKeeper] (distinct)
  - Test: AddRole(StoreKeeper), AddRole(Masterchef) → GetEffectiveRoles() returns 2 roles
  - Test: RemoveRole(Masterchef) → GetEffectiveRoles() returns [StoreKeeper] only
- **Unit Test — UserTenant:**
  - Test: UserTenant.Role is UserRole type (not string) — compile-time test
- **Test boundary:**
  - Unit tests: minimum 5 cases cho PermissionGroup
  - Integration tests: N/A
  - E2E tests: N/A

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Chiến lược thực thi: JIT Planning + Pure Execution

Task này là SINGLE-SESSION: 4 files, tất cả domain types đơn giản. Không có complex state machine.

### Micro-phase breakdown cho W6-T2

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1 (phase A)** | Đọc `Domain.cs` lines 395-410 (UserRole exact values) và 944-960 (UserTenant properties). Xác định PermissionGroup kế thừa AggregateRoot hay BaseEntity. Xác định UserPermissionGroup có `AssignedAt DateTime` không | Tạo `UserRole.cs` (copy exact enum). Tạo `UserTenant.cs` (upgraded: Role string→UserRole). Verify compile |
| **S1 (phase B)** | Xác định PermissionGroup._roles backing field type. Xác định distinct logic trong AddRole | Tạo `PermissionGroup.cs`: constructor, _roles backing field, AddRole/RemoveRole/GetEffectiveRoles. Tạo `UserPermissionGroup.cs`. Add [Obsolete] to Domain.cs UserRole. Run `dotnet build`. Run unit tests |

### Rules
- Bước cuối cùng: thêm [Obsolete] vào Domain.cs UserRole — chỉ sau khi 4 files mới compile OK
- `PermissionGroup.GetEffectiveRoles()` phải return distinct — dùng `_roles.Distinct().ToList().AsReadOnly()`
- `UserTenant.AssignedAt` phải có default value `DateTime.UtcNow` trong constructor

## 11. ESTIMATED EFFORT
- 1 session (45-60 phút)
- **Phụ thuộc:** W5-T1 (AggregateRoot), W6-T1 (UserAggregate folder setup)
- **BLOCKER:** Nếu duplicate `UserRole` trong cùng namespace gây CS0104 compile error (không phải warning) → cần rename enum mới hoặc dùng partial namespace — escalate nếu cần
