# TASK CARD: Tenant Onboarding - Wave 1 - Generic Abstraction & Foundation

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Tạo nền tảng generic abstraction cho tenant onboarding multi-industry
- **Nghiệp vụ áp dụng:** Tạo tenant mới cho bất kỳ ngành nào (F&B trước, SPA/Hotel/Barber/Clothes/Healthy/Pet Shop sau)
- **Status:** PENDING — Planning & Approval
- **Branch:** `feature/tenant-onboarding-wave1-abstraction`
- **Estimated Sessions:** 1

---

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `newfeaturebuild.md` (new feature - multi-session)
- **Execution Mode:** ANALYZE → IMPLEMENT
- **Current Phase:** Wave 1 of 6

---

## 3. RELEVANT FILES (CONTEXT BOUNDARY)

### Files được phép đọc/sửa
- `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
- `3_CoreHub/Services/Onboarding/IIndustrySeedStrategy.cs` (CREATE)
- `3_CoreHub/Services/Onboarding/ITenantOnboardingService.cs` (CREATE)
- `3_CoreHub/Services/Onboarding/Dtos/OnboardTenantRequest.cs` (CREATE)
- `3_CoreHub/Services/Onboarding/Dtos/TenantOnboardingResult.cs` (CREATE)
- `3_CoreHub/Services/Onboarding/Dtos/IndustrySeedResult.cs` (CREATE)
- `3_CoreHub/Services/Onboarding/Strategies/SpaSeedStrategy.cs` (CREATE - stub)
- `3_CoreHub/Services/Onboarding/Strategies/HotelSeedStrategy.cs` (CREATE - stub)
- `3_CoreHub/Services/Onboarding/Strategies/BarberSeedStrategy.cs` (CREATE - stub)
- `3_CoreHub/Services/Onboarding/Strategies/ClothesSeedStrategy.cs` (CREATE - stub)
- `3_CoreHub/Services/Onboarding/Strategies/HealthySeedStrategy.cs` (CREATE - stub)
- `3_CoreHub/Services/Onboarding/Strategies/PetShopSeedStrategy.cs` (CREATE - stub)
- `6_Tests/VanAn.Core.Tests/Services/Onboarding/OnboardingDtoTests.cs` (CREATE)
- `6_Tests/VanAn.Core.Tests/Services/Onboarding/SeedStrategyStubTests.cs` (CREATE)

### Boundary Rules (Nghiêm cấm)
- KHÔNG sửa `1_Shared/Domain.cs` trong wave này
- KHÔNG viết implementation business logic (chỉ interfaces + DTOs + stubs)
- KHÔNG tạo controller/UI trong wave này
- KHÔNG bypass kiến trúc Clean Architecture

---

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **Domain Protection:** Chỉ sử dụng entities sẵn có, không sửa domain
- [ ] **Generic Design:** `IIndustrySeedStrategy` phải cho phép thêm ngành mới không sửa orchestrator
- [ ] **Immutable DTOs:** DTOs phải là `record` với init-only properties
- [ ] **No EF Core in DTOs:** DTOs không chứa `DbContext` hay `IQueryable`
- [ ] **Cross-Tenant Safety:** DTOs phải validate `TenantId` không empty
- [ ] **Naming Convention:** Namespace `VanAn.CoreHub.Services.Onboarding`

---

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** `IIndustrySeedStrategy` interface defined với `IndustryCode`, `IndustryName`, `SeedAsync`
- [ ] **SC2:** `ITenantOnboardingService` interface defined với `OnboardAsync`
- [ ] **SC3:** `OnboardTenantRequest` record created (tenant info + industry code + owner info)
- [ ] **SC4:** `TenantOnboardingResult` record created (tenant id, owner id, counts, warnings)
- [ ] **SC5:** `IndustrySeedResult` record created (counts + warnings)
- [ ] **SC6:** Stub strategies cho 6 ngành tương lai created (return empty result with warning)
- [ ] **SC7:** Unit tests for DTOs pass
- [ ] **SC8:** Unit tests for stub strategies pass
- [ ] **SC9:** Build: 0 errors
- [ ] **SC10:** No regression in existing tests

---

## 6. ACTIVE SKILLS (MAX 3)
- `domain-integrity-validation` — Ensure DTOs/interfaces align with domain
- `build-error-analysis` — Verify build passes after adding new namespace
- `test-system-upgrade` — Add unit tests for new contracts

---

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 5
- **Verified Facts:**
  - Fact 1: `TenantManagementService` exists and creates tenants
  - Fact 2: `UserManagementService` exists and creates users with BCrypt
  - Fact 3: `PermissionGroupService` exists and creates groups
  - Fact 4: `RoleAssignmentService` exists and assigns roles/groups
  - Fact 5: No generic onboarding abstraction currently exists
- **Assumptions:**
  - `IIndustrySeedStrategy` is the right abstraction for multi-industry seeding
  - `ITenantOnboardingService` should orchestrate but not directly use `DbContext`
  - DTOs should be immutable records
- **Open Questions:**
  - Q1: Should `IndustryCode` be enum or string?
  - Q2: Should `OnboardTenantRequest` include optional owner details or use defaults?
  - Q3: Should stub strategies throw `NotImplementedException` or return empty result?

---

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| `3_CoreHub/Services/Onboarding/IIndustrySeedStrategy.cs` | NEW - defines contract | Keep minimal and stable |
| `3_CoreHub/Services/Onboarding/ITenantOnboardingService.cs` | NEW - defines orchestrator contract | Keep minimal and stable |
| `3_CoreHub/Services/Onboarding/Dtos/*.cs` | NEW - data contracts | Use immutable records |
| `3_CoreHub/Services/Onboarding/Strategies/*SeedStrategy.cs` | NEW - stubs | Return empty results, no side effects |
| `6_Tests/VanAn.Core.Tests/Services/Onboarding/*.cs` | NEW - tests | Fast unit tests only |

---

## 9. TDD & TESTING STRATEGY
- **Unit tests:**
  - DTO creation and validation
  - Stub strategy return values
  - Industry code uniqueness
- **Integration tests:** Không trong wave này
- **E2E tests:** Không trong wave này

---

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | - Chốt interface contracts<br>- Chốt DTO shapes<br>- Chốt stub strategy behavior | - Create interfaces and DTOs<br>- Create stub strategies<br>- Add unit tests<br>- Run build |

---

## 11. DETAILED CODING STEPS

### 11.1 Create `IIndustrySeedStrategy`
```csharp
public interface IIndustrySeedStrategy
{
    string IndustryCode { get; }
    string IndustryName { get; }
    Task<IndustrySeedResult> SeedAsync(
        TenantId tenantId,
        IVanAnDbContext dbContext,
        CancellationToken ct = default);
}
```

### 11.2 Create `ITenantOnboardingService`
```csharp
public interface ITenantOnboardingService
{
    Task<TenantOnboardingResult> OnboardAsync(
        OnboardTenantRequest request,
        CancellationToken ct = default);
}
```

### 11.3 Create DTOs
```csharp
public record OnboardTenantRequest(
    string Name,
    BusinessType BusinessType,
    HKDGroup? HKDGroup,
    string? ContactEmail,
    string? ContactPhone,
    string? Address,
    string? TaxCode,
    string IndustryCode,
    string OwnerUsername,
    string OwnerPassword,
    string OwnerDisplayName);

public record TenantOnboardingResult(
    Guid TenantId,
    Guid OwnerUserId,
    int ProductsCreated,
    int IngredientsCreated,
    int RecipesCreated,
    int ShopsCreated,
    int PermissionGroupsCreated,
    List<string> Warnings);

public record IndustrySeedResult(
    int ProductsCreated,
    int IngredientsCreated,
    int RecipesCreated,
    int ShopsCreated,
    List<string> Warnings);
```

### 11.4 Create stub strategies
Tất cả các stub strategy return `IndustrySeedResult` with all zeros and a warning: `"{IndustryName} seeding not yet implemented"`.

### 11.5 Add tests
- Verify DTO properties
- Verify stub strategies return correct warnings
- Verify industry codes are unique

---

## 12. EXIT CHECKLIST
- [ ] All files created
- [ ] Unit tests pass
- [ ] `dotnet build VanAn.sln` 0 errors
- [ ] `guard-check.ps1` pass
- [ ] Commit với message `[WAVE 1] Tenant onboarding generic abstraction`
- [ ] Ready for Wave 2
