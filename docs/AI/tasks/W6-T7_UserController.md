# TASK CARD: API - WAVE 6 - User Controller & Permission Group Controller

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Tạo `UserController` và `PermissionGroupController` trong API layer — REST endpoints cho user management và permission group management, sử dụng `IUserManagementService`, `IRoleAssignmentService`, `IPermissionGroupService`. Map domain exceptions sang HTTP codes phù hợp.
- **Nghiệp vụ áp dụng:** Owner của Tenant quản lý users (tạo, sửa profile, deactivate, assign role) và PermissionGroups (tạo, add/remove roles) trong tenant của họ. StoreManagement có thể xem danh sách users. Tenant ID được lấy từ JWT claims để enforce multi-tenancy.

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `.devin/workflows/newfeaturebuild.md`
- **Execution Mode:** IMPLEMENT

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md`
  - `5_WebApps/ShopERP/Controllers/UserController.cs` — TẠO MỚI (xác nhận location là ShopERP, không phải Gateway)
  - `5_WebApps/ShopERP/Controllers/PermissionGroupController.cs` — TẠO MỚI
  - `3_CoreHub/Services/IUserManagementService.cs` — ĐỌC (interface từ W6-T4)
  - `3_CoreHub/Services/IRoleAssignmentService.cs` — ĐỌC (interface từ W6-T5)
  - `3_CoreHub/Services/IPermissionGroupService.cs` — ĐỌC (interface từ W6-T5)
  - `5_WebApps/ShopERP/Program.cs` — ĐỌC để xem existing policy patterns và JWT claims config
- **Boundary Rules (Nghiêm cấm):**
  - KHÔNG inject `IVanAnDbContext`, `VanAnDbContext`, hoặc bất kỳ EF Core type vào Controllers
  - KHÔNG inject services trực tiếp trừ 3 interfaces: `IUserManagementService`, `IRoleAssignmentService`, `IPermissionGroupService`
  - KHÔNG đặt business logic trong Controller — chỉ HTTP concerns (request parsing, response mapping, exception handling)
  - KHÔNG trả về `DemoUser` domain object trực tiếp — wrap trong `UserDto` response
  - Governance Hard Stop: `Gateway MUST remain pure stateless Reverse Proxy` → Controllers tại ShopERP, KHÔNG tại 2_Gateway

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **Tenant ID từ JWT:** `TenantId tenantId = TenantId.From(User.FindFirst("tenant_id")?.Value ?? throw new UnauthorizedAccessException())` — KHÔNG accept tenantId từ request body.
- [ ] **Policy Mapping:** UserController: `OwnerOnly` cho write ops, `StoreManagement` cho GET. PermissionGroupController: `OwnerOnly` cho tất cả.
- [ ] **Exception HTTP Mapping:**
  - `InvalidOperationException` → 422 Unprocessable Entity với `{ "error": ex.Message }`
  - `UnauthorizedAccessException` → 403 Forbidden với `{ "error": ex.Message }`
  - `ArgumentException` / `ArgumentNullException` → 400 Bad Request
- [ ] **Async Actions:** Tất cả action methods phải `async Task<IActionResult>`.
- [ ] **DTO Pattern:** Response objects phải là DTOs (anonymous objects hoặc dedicated response records) — KHÔNG expose domain objects.

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC-1:** `POST /api/users` với OwnerOnly JWT → 201 Created với user details (userId, username, role).
- [ ] **SC-2:** `POST /api/users` với Staff JWT → 403 Forbidden.
- [ ] **SC-3:** `GET /api/users` với StoreManagement JWT → 200 với user list.
- [ ] **SC-4:** `PATCH /api/users/{id}` với OwnerOnly JWT → 200 OK.
- [ ] **SC-5:** `POST /api/users/{id}/deactivate` khi user là last Owner → 422 với `{ "error": "Cannot deactivate the last Owner of a tenant" }` (không phải 500).
- [ ] **SC-6:** `POST /api/users/{id}/roles` với body `{ "role": "StoreKeeper" }` → 200 OK.
- [ ] **SC-7:** `DELETE /api/users/{id}/roles/StoreKeeper` → 200 OK (role revoked).
- [ ] **SC-8:** `POST /api/permission-groups` với OwnerOnly JWT → 201 Created.
- [ ] **SC-9:** Cross-tenant access (JWT tenant_id mismatch) → `UnauthorizedAccessException` → 403 Forbidden.
- [ ] **SC-10:** `dotnet build VanAn.sln` → 0 errors. `guard-check.ps1` PASS.

**Implementation Date:** 2026-06-23
**Branch:** feature/wave6-user-rbac-mgmt

## 6. ACTIVE SKILLS (MAX 3)
- `domain-integrity-validation` — Verify Controllers không chứa business logic, đúng layer placement
- `build-error-analysis` — Handle Controller registration, JWT claims parsing, TenantId type conversion
- `pattern-based-fixing` — Consistent exception-to-HTTP mapping (single try-catch pattern per action)

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Verified Facts:**
  - Fact 1: Policies hiện có tại `ShopERP/Program.cs`: `OwnerOnly`, `StoreManagement`, `GuardOnly`, `StaffOrAbove`
  - Fact 2: Governance Hard Stop: `Gateway MUST remain pure stateless Reverse Proxy (YARP). NO DbContext, NO EF Core namespaces, NO business logic/services`
  - Fact 3: Governance: `5_WebApps/ShopERP` là main Web API Host — Controllers thuộc về đây
  - Fact 4: `enum UserRole`: `None, Owner, StoreKeeper, Guard, Staff, Masterchef` (Domain.cs line 399)
  - Fact 5: `IUserManagementService` methods (W6-T4): Create, GetById, List, UpdateProfile, ChangePassword, Deactivate, Reactivate
  - Fact 6: `IRoleAssignmentService` methods (W6-T5): AssignRole, RevokeRole, GetUserRoles, AssignUserToGroup, RemoveUserFromGroup, GetEffectiveRoles
  - Fact 7: Exception mapping: `InvalidOperationException` → 422, `UnauthorizedAccessException` → 403
- **Assumptions:**
  - JWT claims include `tenant_id` claim — cần verify tên claim chính xác trong existing JWT config
  - `TenantId.From(string)` factory method tồn tại — hoặc `new TenantId(Guid.Parse(str))`
- **Open Questions:**
  - Q1: JWT claim name cho tenant ID là gì chính xác? (`tenant_id`, `tenantId`, hay `tid`?) — cần đọc JwtTokenService hoặc Program.cs
  - Q2: `UserRole` parse từ string trong route (`DELETE /api/users/{id}/roles/{role}`) — dùng `Enum.Parse<UserRole>(role, ignoreCase: true)` → cần handle parse exception → 400
- **Recommended Action:** IMPLEMENT — verify JWT claim name trước → implement Controllers

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| `UserController.cs` (mới) | W6-T10 UI depend on endpoints | Freeze endpoint URLs |
| `PermissionGroupController.cs` (mới) | W6-T10 UI depend on endpoints | Freeze endpoint URLs |
| Routing namespace collision | Potential 404 nếu routing attribute sai | Test routes via Swagger sau khi implement |

## 9. TDD & E2E TESTING STRATEGY
- **Integration Test — UserController:**
  - Test: POST /api/users OwnerOnly JWT → 201
  - Test: POST /api/users Staff JWT → 403
  - Test: POST /api/users/{id}/deactivate — mock service throw InvalidOperationException → 422
  - Test: GET /api/users StoreManagement JWT → 200
- **Unit Test — Controller:**
  - Mock services → verify correct service method called
  - Mock service throw InvalidOperationException → verify 422 response
  - Mock service throw UnauthorizedAccessException → verify 403 response
  - JWT tenant_id missing → verify UnauthorizedAccessException thrown
- **Test boundary:**
  - Unit tests: mock all 3 services — 6+ test cases
  - Integration tests: WebApplicationFactory với JWT middleware
  - E2E tests: N/A trong task này

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Chiến lược thực thi: JIT Planning + Pure Execution

Task này là SINGLE-SESSION: 2 Controller files. Pattern-based approach — define exception mapping helper once, reuse across all actions.

### Micro-phase breakdown cho W6-T7

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1 (phase A)** | Đọc `ShopERP/Program.cs` → verify JWT config, claim names. Đọc existing Controller (nếu có) → lấy pattern (ControllerBase, inject pattern). Xác định TenantId parsing helper | Tạo `UserController.cs`: inject 2 services (IUserManagementService, IRoleAssignmentService). Implement 6 endpoints với [Authorize] attributes. Extract `GetTenantId()` helper từ JWT claims. Exception mapping (try-catch per action). |
| **S1 (phase B)** | Review PermissionGroupController endpoints (4 endpoints, OwnerOnly). Verify `UserRole` string-to-enum parse pattern | Tạo `PermissionGroupController.cs`: inject IPermissionGroupService. Implement 4 endpoints. Add UserRole parse guard (400 for invalid enum). Run `dotnet build`. Run `guard-check.ps1` |

### Rules
- Extract `TenantId GetCurrentTenantId()` helper method trong BaseController hoặc private method
- Exception handling pattern: một try-catch mỗi action (không wrap toàn bộ — chỉ wrap service call)
- UserRole string parse từ route: `if (!Enum.TryParse<UserRole>(roleStr, true, out var role)) return BadRequest("Invalid role")`

## 11. ESTIMATED EFFORT
- 1 session (60-75 phút)
- **Phụ thuộc:** W6-T4 (IUserManagementService), W6-T5 (IRoleAssignmentService, IPermissionGroupService)
- **BLOCKER:** Nếu JWT claim name cho tenant_id không đúng → 401/403 errors trên tất cả endpoints. Phải verify claim name trước khi implement `GetCurrentTenantId()` helper.
