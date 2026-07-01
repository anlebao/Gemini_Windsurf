# TASK CARD: Tenant Onboarding - Wave 6 - Validation, Tests & Documentation

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Validate toàn bộ feature, thêm integration tests, cập nhật documentation và project state
- **Nghiệp vụ áp dụng:** Đảm bảo tenant onboarding hoạt động end-to-end cho F&B
- **Status:** PENDING — Planning & Approval
- **Branch:** `feature/tenant-onboarding-wave6-validation-docs`
- **Estimated Sessions:** 1

---

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `newfeaturebuild.md` (new feature - multi-session)
- **Execution Mode:** ANALYZE → IMPLEMENT
- **Current Phase:** Wave 6 of 6
- **Dependency:** All previous waves merged

---

## 3. RELEVANT FILES (CONTEXT BOUNDARY)

### Files được phép đọc/sửa
- `docs/AI/project_state.md` (UPDATE)
- `docs/ShopERP_Documentation.md` (UPDATE)
- `docs/KhachLink_Documentation.md` (UPDATE nếu cần)
- `docs/AI/tasks/tenant_onboarding_fnb_master_plan.md` (UPDATE status)
- `6_Tests/VanAn.Integration.Tests/TenantOnboardingIntegrationTests.cs` (CREATE/UPDATE)
- `6_Tests/VanAn.Architecture.Tests/` (READ - kiểm tra violations)
- `scripts/ci-local.ps1` (READ - run)
- `scripts/guard-check.ps1` (READ - run)
- Các files từ Wave 1-5 (READ + FIX nếu test fail)

### Boundary Rules (Nghiêm cấm)
- KHÔNG thêm feature mới trong wave này (chỉ fix/test/docs)
- KHÔNG sửa architecture nếu không cần thiết
- KHÔNG chạy Playwright full suite nếu chưa được approve

---

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **Local CI Pass:** `scripts/ci-local.ps1` phải pass
- [ ] **Build Clean:** `dotnet build VanAn.sln` 0 errors
- [ ] **Architecture Tests:** `28/28` PASS
- [ ] **Domain Integrity:** Không có domain violations mới
- [ ] **No Secrets:** Không để hardcoded password trong production code (seed/test data OK nếu documented)
- [ ] **Documentation:** Cập nhật `ShopERP_Documentation.md` với onboarding module

---

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** `scripts/ci-local.ps1` pass
- [ ] **SC2:** `dotnet build VanAn.sln` 0 errors
- [ ] **SC3:** Architecture tests pass
- [ ] **SC4:** Integration test full onboarding flow pass
- [ ] **SC5:** Unit tests cho Wave 1-5 pass
- [ ] **SC6:** `docs/ShopERP_Documentation.md` updated with onboarding section
- [ ] **SC7:** `docs/AI/project_state.md` updated with new files and status
- [ ] **SC8:** Master plan status updated to COMPLETE or IN PROGRESS
- [ ] **SC9:** No new warnings/errors
- [ ] **SC10:** Final commit with all changes

---

## 6. ACTIVE SKILLS (MAX 3)
- `ci-build-debug` — Diagnose and fix CI failures
- `test-system-upgrade` — Ensure test coverage adequate
- `system-refactor-safety` — Any final cleanup without breaking architecture

---

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 4
- **Verified Facts:**
  - Fact 1: `ci-local.ps1` đã pass trước khi bắt đầu feature
  - Fact 2: Architecture tests đang 28/28 PASS
  - Fact 3: Unit tests đang 678/678 PASS
  - Fact 4: Có 2 integration tests pre-existing failures (non-blocking)
- **Assumptions:**
  - Feature sẽ không làm tăng failures trong architecture tests
  - Integration tests mới sẽ pass hoặc được mark explicit
- **Open Questions:**
  - Q1: Có cần thêm E2E test cho onboarding flow không?
  - Q2: Có cần cập nhật `DEPLOYMENT.md` không?
  - Q3: Có cần cập nhật `docker-compose` để seed default tenant không?

---

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| `docs/AI/project_state.md` | UPDATE status | Ground truth first |
| `docs/ShopERP_Documentation.md` | UPDATE docs | Append onboarding section |
| `6_Tests/VanAn.Integration.Tests/TenantOnboardingIntegrationTests.cs` | NEW tests | Use SQLite in-memory |
| Various source files | FIX only if tests fail | Minimal changes |

---

## 9. TDD & TESTING STRATEGY
- **Integration test full flow:**
  - Create tenant onboarding via Gateway API
  - Verify tenant exists in DB
  - Verify owner user exists với role Owner
  - Verify products/ingredients/recipes exist for tenant
  - Verify permission groups exist
  - Verify owner can authenticate
- **Regression tests:**
  - Run full local CI
  - Verify no new failures
- **E2E tests:** Defer đến khi user yêu cầu

---

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | - Chốt test scenarios<br>- Chốt docs update scope | - Add integration tests<br>- Run ci-local<br>- Fix failures<br>- Update docs |

---

## 11. DETAILED STEPS

### 11.1 Integration Test Full Flow
```csharp
[Fact]
public async Task Onboard_FnB_Creates_Tenant_Owner_Seed_Data_Groups()
{
    // Arrange
    var request = new OnboardTenantRequest(
        "Vạn An F&B Test",
        BusinessType.HouseholdBusiness,
        HKDGroup.Group1,
        "test@vanan.vn",
        "0901234567",
        "123 Test",
        "1234567890",
        "F&B",
        "owner@test.vn",
        "Password123!",
        "Chủ Quán Test");

    // Act
    var response = await _client.PostAsJsonAsync("api/v1/onboarding/tenants", request);

    // Assert
    response.EnsureSuccessStatusCode();
    var result = await response.Content.ReadFromJsonAsync<TenantOnboardingResult>();
    Assert.NotEqual(Guid.Empty, result.TenantId);
    Assert.NotEqual(Guid.Empty, result.OwnerUserId);
    Assert.True(result.ProductsCreated > 0);
    Assert.True(result.IngredientsCreated > 0);
    Assert.True(result.PermissionGroupsCreated > 0);
}
```

### 11.2 Documentation Updates
- Thêm section "Tenant Onboarding" vào `docs/ShopERP_Documentation.md`
- Mô tả: generic abstraction, F&B seed data, API endpoint, ShopERP UI
- Cập nhật `docs/AI/project_state.md`:
  - Thêm files vào Key File References
  - Thêm history log entry
  - Cập nhật Current Status

### 11.3 Final Validation
- Run `scripts/ci-local.ps1`
- Run `dotnet build VanAn.sln`
- Run architecture tests
- Commit all changes

---

## 12. EXIT CHECKLIST
- [ ] `ci-local.ps1` pass
- [ ] `dotnet build VanAn.sln` 0 errors
- [ ] Architecture tests pass
- [ ] Integration tests pass
- [ ] Documentation updated
- [ ] `project_state.md` updated
- [ ] Master plan status updated
- [ ] Final commit với message `[WAVE 6] Tenant onboarding validation and docs`
- [ ] Feature complete, ready for final review
