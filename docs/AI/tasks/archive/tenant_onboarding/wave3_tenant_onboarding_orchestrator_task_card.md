# TASK CARD: Tenant Onboarding - Wave 3 - Onboarding Orchestrator Service

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Tạo service orchestrate toàn bộ flow onboarding: tenant → owner → seed → permission groups → role assignment
- **Nghiệp vụ áp dụng:** Tạo tenant mới cho F&B (và các ngành khác tương lai) trong một lời gọi duy nhất
- **Status:** PENDING — Planning & Approval
- **Branch:** `feature/tenant-onboarding-wave3-orchestrator`
- **Estimated Sessions:** 1-2

---

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `newfeaturebuild.md` (new feature - multi-session)
- **Execution Mode:** ANALYZE → IMPLEMENT
- **Current Phase:** Wave 3 of 6
- **Dependency:** Wave 2 must be merged

---

## 3. RELEVANT FILES (CONTEXT BOUNDARY)

### Files được phép đọc/sửa
- `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
- `docs/AI/tasks/wave1_tenant_onboarding_abstraction_task_card.md` (READ)
- `docs/AI/tasks/wave2_fnb_seed_strategy_task_card.md` (READ)
- `3_CoreHub/Services/Onboarding/ITenantOnboardingService.cs` (READ)
- `3_CoreHub/Services/Onboarding/TenantOnboardingService.cs` (CREATE)
- `3_CoreHub/Services/Onboarding/Dtos/OnboardTenantRequest.cs` (READ)
- `3_CoreHub/Services/Onboarding/Dtos/TenantOnboardingResult.cs` (READ)
- `3_CoreHub/Services/TenantManagementService.cs` (READ)
- `3_CoreHub/Services/UserManagementService.cs` (READ)
- `3_CoreHub/Services/PermissionGroupService.cs` (READ)
- `3_CoreHub/Services/RoleAssignmentService.cs` (READ)
- `6_Tests/VanAn.Core.Tests/Services/Onboarding/TenantOnboardingServiceTests.cs` (CREATE)
- `6_Tests/VanAn.Integration.Tests/TenantOnboardingIntegrationTests.cs` (CREATE - optional, can defer to Wave 6)

### Boundary Rules (Nghiêm cấm)
- KHÔNG sửa `1_Shared/Domain.cs`
- KHÔNG sửa các service thành phần (chỉ gọi)
- KHÔNG tạo controller/UI trong wave này
- KHÔNG để `TenantOnboardingService` trực tiếp dùng `DbContext` ngoài việc pass vào seed strategy

---

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **Transaction Safety:** Sử dụng `DbContext` transaction nếu có thể; nếu không, rollback manual không khả thi — ghi nhận warning
- [ ] **No Circular Dependency:** `TenantOnboardingService` không được làm `TenantManagementService` phụ thuộc ngược lại
- [ ] **Owner Password:** Phải truyền plain text đến `UserManagementService` để hash (BCrypt)
- [ ] **Cross-Tenant Safety:** Tất cả operations phải cùng `TenantId`
- [ ] **Error Handling:** Throw rõ ràng nếu industry code không tồn tại
- [ ] **Default Groups:** Tạo ít nhất 4 default permission groups cho F&B

---

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** `TenantOnboardingService` implements `ITenantOnboardingService`
- [ ] **SC2:** Một lời gọi tạo được tenant + owner user + seed data + groups
- [ ] **SC3:** Owner user được gán role `Owner`
- [ ] **SC4:** Owner user được gán vào group `Quản lý` (hoặc tương đương)
- [ ] **SC5:** Tạo ít nhất 4 default permission groups
- [ ] **SC6:** Trả về `TenantOnboardingResult` với đầy đủ counts
- [ ] **SC7:** Unit tests pass với mocked dependencies
- [ ] **SC8:** Build: 0 errors
- [ ] **SC9:** No regression in existing tests

---

## 6. ACTIVE SKILLS (MAX 3)
- `domain-integrity-validation` — Ensure orchestration respects domain rules
- `build-error-analysis` — Verify build passes with new service dependencies
- `test-system-upgrade` — Add unit tests for orchestrator

---

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 5
- **Verified Facts:**
  - Fact 1: `TenantManagementService.CreateTenantAsync` tạo tenant và gửi welcome email
  - Fact 2: `UserManagementService.CreateUserAsync` hash BCrypt và tạo user
  - Fact 3: `PermissionGroupService.CreateGroupAsync` tạo permission group
  - Fact 4: `RoleAssignmentService.AssignRoleToUserAsync` gán role
  - Fact 5: `RoleAssignmentService.AssignUserToGroupAsync` gán user vào group
- **Assumptions:**
  - `TenantOnboardingService` sẽ gọi `TenantManagementService` thay vì tự tạo tenant
  - Default password cho owner sẽ được truyền từ request
  - Permission groups sẽ dùng `UserRole` values (Owner, StoreKeeper, Staff, Guard)
- **Open Questions:**
  - Q1: Nên tạo bao nhiêu default permission groups?
  - Q2: Có nên tạo `UserTenant` trực tiếp hay qua `UserManagementService`?
  - Q3: Có cần rollback nếu một bước fail không?

---

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| `3_CoreHub/Services/Onboarding/TenantOnboardingService.cs` | NEW - orchestrator | Inject existing services, no new abstractions |
| `6_Tests/VanAn.Core.Tests/Services/Onboarding/TenantOnboardingServiceTests.cs` | NEW - tests | Mock all dependencies |
| Existing CoreHub services | READ ONLY | Không sửa |

---

## 9. TDD & TESTING STRATEGY
- **Unit tests:**
  - Verify orchestrator calls `TenantManagementService.CreateTenantAsync`
  - Verify orchestrator calls `UserManagementService.CreateUserAsync` with Owner role
  - Verify orchestrator calls seed strategy
  - Verify orchestrator creates permission groups
  - Verify orchestrator assigns owner to manager group
  - Verify throws when industry code not found
- **Integration tests:** Có thể defer đến Wave 6
- **E2E tests:** Không trong wave này

---

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | - Chốt dependency injection<br>- Chốt default permission groups<br>- Chốt error handling strategy | - Implement `TenantOnboardingService`<br>- Add unit tests<br>- Run build |

---

## 11. DETAILED CODING STEPS

### 11.1 Service Constructor
```csharp
public class TenantOnboardingService(
    ITenantManagementService tenantService,
    IUserManagementService userService,
    IPermissionGroupService permissionGroupService,
    IRoleAssignmentService roleAssignmentService,
    IEnumerable<IIndustrySeedStrategy> seedStrategies,
    ILogger<TenantOnboardingService> logger) : ITenantOnboardingService
```

### 11.2 Default Permission Groups (F&B)
| Group | Roles | Mô tả |
|---|---|---|
| Quản lý | Owner, StoreKeeper | Full access |
| Thu ngân | Staff | Order + Payment |
| Bếp | Staff, Masterchef | Kitchen operations |
| Kho | StoreKeeper | Inventory management |

### 11.3 OnboardAsync Flow
1. Validate `IndustryCode` exists
2. Create tenant via `TenantManagementService.CreateTenantAsync`
3. Create owner user via `UserManagementService.CreateUserAsync` với role `Owner`
4. Assign role `Owner` to user via `RoleAssignmentService.AssignRoleToUserAsync`
5. Seed industry data via selected `IIndustrySeedStrategy.SeedAsync`
6. Create default permission groups
7. Assign owner to `Quản lý` group
8. Return `TenantOnboardingResult`

### 11.4 Error Handling
- `IndustryNotFoundException` nếu không tìm thấy strategy
- `InvalidOperationException` nếu tenant/user creation fail
- Ghi warnings vào result nếu seed strategy return warnings

---

## 12. EXIT CHECKLIST
- [ ] `TenantOnboardingService` implemented
- [ ] Unit tests pass
- [ ] `dotnet build VanAn.sln` 0 errors
- [ ] `guard-check.ps1` pass
- [ ] Commit với message `[WAVE 3] Tenant onboarding orchestrator`
- [ ] Ready for Wave 4
