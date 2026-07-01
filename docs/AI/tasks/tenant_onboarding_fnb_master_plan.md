# MASTER IMPLEMENTATION PLAN — Tenant Onboarding for F&B (Generic Multi-Industry)

> **Status:** COMPLETE ✅ — All 6 waves delivered
> **Created:** 2026-07-01
> **Last Updated:** 2026-07-02
> **Target Workflow:** `newfeaturebuild.md` (ANALYZE → IMPLEMENT)
> **Branch strategy:** `main` → feature branches per wave
> **Execution principle:** JIT Planning + Pure Execution

---

## 0. EXECUTION RULES

### JIT Planning Strategy
**Nguyên tắc cốt lõi:** KHÔNG code mò mẫm — Investigate trước, Implement sau

**Bước 1: INVESTIGATE & ANALYZE (Planning Phase)**
- Đọc và hiểu rõ hiện trạng implementation
- Identify gaps và requirements
- Lập detailed coding plan với specific steps
- Chốt approach trước khi viết bất kỳ dòng code nào

**Bước 2: IMPLEMENT (Execution Phase)**
- Thực hiện viết code theo plan đã chốt
- KHÔNG thay đổi approach khi đang implement
- Mỗi wave xong, chạy `guard-check.ps1` + `dotnet build VanAn.sln`

### Session protocol
1. **Mỗi session chỉ làm 1 wave**
2. **Bắt đầu mỗi session:** Đọc `project_state.md` + task card wave đang làm
3. **Sau khi plan chốt:** Execution Phase
4. **Trước khi session end:** Build + test
5. **Sau mỗi wave:** Commit với message format `[WAVE X] Task description`

### Branch protocol
```
main
  └── feature/tenant-onboarding-wave1-abstraction
      └── feature/tenant-onboarding-wave2-fnb-seed
          └── feature/tenant-onboarding-wave3-orchestrator
              └── feature/tenant-onboarding-wave4-gateway-api
                  └── feature/tenant-onboarding-wave5-shoperp-ui
                      └── feature/tenant-onboarding-wave6-validation-docs
```
- Mỗi wave có branch riêng
- Merge wave vào branch trước đó (cherry-pick hoặc rebase)
- Final merge vào `main` khi tất cả waves complete

### Hard rules
- **Domain layer KHÔNG được sửa để fix Service/UI issues**
- **Sử dụng factory methods và entities sẵn có trong `1_Shared/Domain`**
- **Business logic nằm trong `3_CoreHub/Services`**
- **Gateway chỉ là thin adapter**
- **UI Platform components MUST be used**
- **TDD: tests trước, code sau cho mỗi wave**
- **Playwright DISABLED cho đến khi build pass + implementation complete**

---

## 1. CURRENT ISSUES SUMMARY

### Issue 1: No Integrated Tenant Onboarding
**Status:** ❌ NOT IMPLEMENTED
**Priority:** 1 (High)
**Estimated Time:** 4-6 sessions

**Current State:**
- ✅ `TenantManagementService` tạo tenant được
- ✅ `UserManagementService` tạo user được
- ✅ `RoleAssignmentService` gán role được
- ✅ `PermissionGroupService` tạo nhóm quyền được
- ❌ **KHÔNG có flow tích hợp** tạo tenant + owner + seed data + permission groups
- ❌ `OnboardingService` hiện tại là **dummy implementation** (trả fake templates)
- ❌ `OnboardingController` chỉ trả hardcoded response
- ❌ Không có abstraction ngành (industry) để mở rộng

**Files liên quan:**
- `3_CoreHub/Services/OnboardingService.cs` (dummy)
- `3_CoreHub/Services/TenantManagementService.cs`
- `3_CoreHub/Services/UserManagementService.cs`
- `3_CoreHub/Services/RoleAssignmentService.cs`
- `3_CoreHub/Services/PermissionGroupService.cs`
- `2_Gateway/Controllers/OnboardingController.cs`
- `5_WebApps/ShopERP/Program.cs`

### Issue 2: No Industry-Specific Seed Data
**Status:** ❌ NOT IMPLEMENTED
**Priority:** 1 (High)

**Current State:**
- ❌ Không có seed data cho F&B
- ❌ Không có cơ chế generic để thêm SPA, Hotel, Barber, Clothes, Healthy, Pet Shop
- ❌ `ShopERP/Program.cs` chỉ seed DemoUsers cho tenant default

**Required:**
- Generic abstraction `IIndustrySeedStrategy`
- F&B implementation với products, ingredients, recipes, default shop
- Stub/extension points cho các ngành khác

---

## 2. WAVE 1 — Foundation & Generic Abstraction

**Branch:** `feature/tenant-onboarding-wave1-abstraction`
**Estimated sessions:** 1
**Conflict risk:** LOW
**Priority:** 1
**Task Card:** `docs/AI/tasks/wave1_tenant_onboarding_abstraction_task_card.md`

### Tasks
| # | Task ID | Task | Files | Status |
|---|---|---|---|---|
| 1 | W1-T1 | Create `IIndustrySeedStrategy` interface | `3_CoreHub/Services/Onboarding/IIndustrySeedStrategy.cs` | COMPLETE ✅ |
| 2 | W1-T2 | Create `ITenantOnboardingService` interface | `3_CoreHub/Services/Onboarding/ITenantOnboardingService.cs` | COMPLETE ✅ |
| 3 | W1-T3 | Create onboarding DTOs | `3_CoreHub/Services/Onboarding/Dtos/` | COMPLETE ✅ |
| 4 | W1-T4 | Create stub strategies for future industries | `3_CoreHub/Services/Onboarding/Strategies/` | COMPLETE ✅ |
| 5 | W1-T5 | Add unit tests for interfaces/DTOs | `6_Tests/VanAn.Core.Tests/Services/Onboarding/` | COMPLETE ✅ |

### Entry criteria
- [x] Project builds successfully
- [x] Git status clean
- [x] Existing domain entities understood

### Exit criteria
- [x] Interfaces defined with clear contracts
- [x] DTOs immutable (records)
- [x] Build: 0 errors
- [x] Unit tests for DTOs pass

### Why first
- Làm nền tảng generic cho tất cả các wave sau
- Không có risk vì chỉ là interfaces + DTOs
- Dễ review và chốt kiến trúc

---

## 3. WAVE 2 — F&B Seed Strategy

**Branch:** `feature/tenant-onboarding-wave2-fnb-seed`
**Estimated sessions:** 1-2
**Conflict risk:** LOW
**Priority:** 2
**Task Card:** `docs/AI/tasks/wave2_fnb_seed_strategy_task_card.md`

### Tasks
| # | Task ID | Task | Files | Status |
|---|---|---|---|---|
| 1 | W2-T1 | Implement `FnbSeedStrategy` | `3_CoreHub/Services/Onboarding/Strategies/FnbSeedStrategy.cs` | COMPLETE ✅ |
| 2 | W2-T2 | Seed default F&B shop | `Shop` entity | COMPLETE ✅ |
| 3 | W2-T3 | Seed F&B products (cafe, tea, food) | `Product` entity | COMPLETE ✅ |
| 4 | W2-T4 | Seed F&B ingredients | `Ingredient` entity | COMPLETE ✅ |
| 5 | W2-T5 | Seed recipes (product ↔ ingredient mapping) | `Recipe` entity | COMPLETE ✅ |
| 6 | W2-T6 | Add unit tests for FnbSeedStrategy | `6_Tests/VanAn.Core.Tests/Services/Onboarding/FnbSeedStrategyTests.cs` | COMPLETE ✅ |

### Entry criteria
- [x] Wave 1 merged
- [x] `IIndustrySeedStrategy` interface stable

### Exit criteria
- [x] F&B seed strategy creates shop + products + ingredients + recipes
- [x] Seed data validated by unit tests
- [x] Build: 0 errors
- [x] No direct SQL — dùng DbContext

### Why second
- Cần generic abstraction từ Wave 1
- Là nghiệp vụ cốt lõi của F&B
- Có thể test độc lập

---

## 4. WAVE 3 — Tenant Onboarding Orchestrator

**Branch:** `feature/tenant-onboarding-wave3-orchestrator`
**Estimated sessions:** 1-2
**Conflict risk:** MEDIUM
**Priority:** 3
**Task Card:** `docs/AI/tasks/wave3_tenant_onboarding_orchestrator_task_card.md`

### Tasks
| # | Task ID | Task | Files | Status |
|---|---|---|---|---|
| 1 | W3-T1 | Implement `TenantOnboardingService` | `3_CoreHub/Services/Onboarding/TenantOnboardingService.cs` | COMPLETE ✅ |
| 2 | W3-T2 | Orchestrate tenant creation | Gọi `TenantManagementService` | COMPLETE ✅ |
| 3 | W3-T3 | Create default owner user | Gọi `UserManagementService` | COMPLETE ✅ |
| 4 | W3-T4 | Create default permission groups | Gọi `PermissionGroupService` | COMPLETE ✅ |
| 5 | W3-T5 | Assign owner to manager group | Gọi `RoleAssignmentService` | COMPLETE ✅ |
| 6 | W3-T6 | Add unit tests for orchestrator | `6_Tests/VanAn.Core.Tests/Services/Onboarding/TenantOnboardingServiceTests.cs` | COMPLETE ✅ |

### Entry criteria
- [x] Wave 2 merged
- [x] F&B seed strategy functional
- [x] User/Role/Permission services understood

### Exit criteria
- [x] One call creates tenant + owner + seed + groups + role assignment
- [x] Owner user có thể đăng nhập
- [x] Build: 0 errors
- [x] Unit tests pass

### Why third
- Tích hợp các service thành một flow duy nhất
- Cần F&B seed strategy để orchestrate

---

## 5. WAVE 4 — Gateway API Integration

**Branch:** `feature/tenant-onboarding-wave4-gateway-api`
**Estimated sessions:** 1
**Conflict risk:** LOW
**Priority:** 4
**Task Card:** `docs/AI/tasks/wave4_tenant_onboarding_gateway_api_task_card.md`

### Tasks
| # | Task ID | Task | Files | Status |
|---|---|---|---|---|
| 1 | W4-T1 | Update `OnboardingController` | `2_Gateway/Controllers/OnboardingController.cs` | COMPLETE ✅ |
| 2 | W4-T2 | Add `POST /api/v1/onboarding/tenants` endpoint | `OnboardingController` | COMPLETE ✅ |
| 3 | W4-T3 | Add request/response DTOs for controller | `2_Gateway/Controllers/OnboardingController.cs` | COMPLETE ✅ |
| 4 | W4-T4 | Register DI in Gateway | `2_Gateway/Program.cs` | COMPLETE ✅ |
| 5 | W4-T5 | Add integration tests | `6_Tests/VanAn.Integration.Tests/TenantOnboardingApiTests.cs` | COMPLETE ✅ |

### Entry criteria
- [x] Wave 3 merged
- [x] `TenantOnboardingService` functional

### Exit criteria
- [x] API endpoint tạo tenant onboarding qua Gateway
- [x] Integration tests pass
- [x] Build: 0 errors
- [x] Gateway vẫn stateless (không business logic)

### Why fourth
- Cung cấp API surface cho external callers
- Dễ dàng test qua HTTP

---

## 6. WAVE 5 — ShopERP Admin UI

**Branch:** `feature/tenant-onboarding-wave5-shoperp-ui`
**Estimated sessions:** 1-2
**Conflict risk:** MEDIUM
**Priority:** 5
**Task Card:** `docs/AI/tasks/wave5_tenant_onboarding_shoperp_ui_task_card.md`

### Tasks
| # | Task ID | Task | Files | Status |
|---|---|---|---|---|
| 1 | W5-T1 | Add industry selection to TenantManagement | `5_WebApps/ShopERP/Components/Pages/Admin/TenantManagement.razor` | COMPLETE ✅ |
| 2 | W5-T2 | Add owner credentials form | `TenantManagement.razor` | COMPLETE ✅ |
| 3 | W5-T3 | Call onboarding API from UI | `TenantManagement.razor` + `HttpClient` | COMPLETE ✅ |
| 4 | W5-T4 | Display onboarding result | `TenantManagement.razor` | COMPLETE ✅ |
| 5 | W5-T5 | Add KhachLinkStartupTests assertion | `6_Tests/VanAn.Integration.Tests/KhachLinkStartupTests.cs` (nếu cần service mới) | COMPLETE ✅ |

### Entry criteria
- [x] Wave 4 merged
- [x] API endpoint functional

### Exit criteria
- [x] SystemAdmin có thể tạo tenant với industry selection
- [x] UI hiển thị kết quả onboarding
- [x] Build: 0 errors
- [x] Sử dụng UI Platform components

### Why fifth
- Cung cấp giao diện cho người dùng cuối
- Có thể bỏ qua nếu chỉ cần API, nhưng cần cho completeness

---

## 7. WAVE 6 — Validation, Tests & Documentation

**Branch:** `feature/tenant-onboarding-wave6-validation-docs`
**Estimated sessions:** 1
**Conflict risk:** LOW
**Priority:** 6
**Task Card:** `docs/AI/tasks/wave6_tenant_onboarding_validation_docs_task_card.md`

### Tasks
| # | Task ID | Task | Files | Status |
|---|---|---|---|---|
| 1 | W6-T1 | Run `ci-local.ps1` | Local CI | COMPLETE ✅ |
| 2 | W6-T2 | Fix any build/test failures | Various | COMPLETE ✅ |
| 3 | W6-T3 | Add integration test for full onboarding flow | `6_Tests/VanAn.Integration.Tests/TenantOnboardingIntegrationTests.cs` | COMPLETE ✅ |
| 4 | W6-T4 | Update `docs/ShopERP_Documentation.md` | `docs/ShopERP_Documentation.md` | COMPLETE ✅ |
| 5 | W6-T5 | Update `project_state.md` | `docs/AI/project_state.md` | COMPLETE ✅ |
| 6 | W6-T6 | Architecture test review | `6_Tests/VanAn.Architecture.Tests/` | COMPLETE ✅ (28/28 PASS) |

### Entry criteria
- [x] All previous waves merged
- [x] Code complete

### Exit criteria
- [x] `ci-local.ps1` pass
- [x] `dotnet build VanAn.sln` 0 errors
- [x] Architecture tests pass
- [x] Documentation updated
- [x] `project_state.md` updated

**WAVE 6 STATUS: COMPLETE ✅**

### Why last
- Đảm bảo toàn bộ feature hoạt động đúng
- Cập nhật tài liệu và trạng thái

---

## 8. CROSS-WAVE CONCERNS

### Domain Protection
- Không sửa `Domain.cs` trừ khi có lý do domain modeling chính đáng
- Sử dụng `Tenant.Create...`, `DemoUser.Create`, `Product` constructor, `PermissionGroup` constructor hiện có

### Multi-Tenancy
- Mọi seed operation phải gắn `TenantId`
- Không cross-tenant

### Security
- Owner password phải được hash (BCrypt) qua `UserManagementService`
- API endpoint yêu cầu `SystemAdmin` policy
- Không log password

### Extensibility
- Mỗi ngành mới chỉ cần implement `IIndustrySeedStrategy`
- Không cần sửa `TenantOnboardingService`
- Không cần sửa controller

### Testing Strategy
- Unit tests cho mỗi wave
- Integration test cho full flow
- Không chạy Playwright cho đến khi build pass

---

## 9. APPROVAL CHECKLIST

- [ ] Master plan reviewed
- [ ] 6 task cards reviewed
- [ ] Kiến trúc generic được chốt
- [ ] DTOs và interfaces được chốt
- [ ] F&B seed data được chốt
- [ ] Branch strategy được chốt
- [ ] Sẵn sàng implement Wave 1
