# TASK CARD: Tenant Onboarding - Wave 4 - Gateway API Integration

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Expose tenant onboarding API qua Gateway để external callers (ShopERP UI, mobile, CLI) có thể gọi
- **Nghiệp vụ áp dụng:** SystemAdmin tạo tenant onboarding qua HTTP API
- **Status:** PENDING — Planning & Approval
- **Branch:** `feature/tenant-onboarding-wave4-gateway-api`
- **Estimated Sessions:** 1

---

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `newfeaturebuild.md` (new feature - multi-session)
- **Execution Mode:** ANALYZE → IMPLEMENT
- **Current Phase:** Wave 4 of 6
- **Dependency:** Wave 3 must be merged

---

## 3. RELEVANT FILES (CONTEXT BOUNDARY)

### Files được phép đọc/sửa
- `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
- `docs/AI/tasks/wave3_tenant_onboarding_orchestrator_task_card.md` (READ)
- `2_Gateway/Controllers/OnboardingController.cs` (UPDATE)
- `2_Gateway/Program.cs` (UPDATE - register DI)
- `3_CoreHub/Services/Onboarding/ITenantOnboardingService.cs` (READ)
- `3_CoreHub/Services/Onboarding/Dtos/OnboardTenantRequest.cs` (READ)
- `3_CoreHub/Services/Onboarding/Dtos/TenantOnboardingResult.cs` (READ)
- `6_Tests/VanAn.Integration.Tests/TenantOnboardingApiTests.cs` (CREATE)

### Boundary Rules (Nghiêm cấm)
- KHÔNG để business logic vào controller (chỉ call service + map DTOs)
- KHÔNG sửa CoreHub service trong wave này
- KHÔNG tạo UI trong wave này
- Gateway KHÔNG được inject `DbContext` trực tiếp (chỉ service từ CoreHub)

---

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **Gateway Stateless:** Controller chỉ là thin adapter, không chứa business logic
- [ ] **Authorization:** Endpoint yêu cầu `SystemAdmin` policy
- [ ] **Input Validation:** ModelState validation, required fields
- [ ] **No Password Logging:** Không log password trong request/response
- [ ] **DTO Mapping:** Controller có thể có DTO riêng (request/response) hoặc reuse CoreHub DTOs
- [ ] **Error Response:** Trả về `ProblemDetails` hoặc structured error

---

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** `POST /api/v1/onboarding/tenants` endpoint available
- [ ] **SC2:** Endpoint requires `SystemAdmin` authorization
- [ ] **SC3:** Request DTO validated (ModelState)
- [ ] **SC4:** Response trả về `TenantOnboardingResult`
- [ ] **SC5:** DI registered in `2_Gateway/Program.cs`
- [ ] **SC6:** Integration tests pass
- [ ] **SC7:** Build: 0 errors
- [ ] **SC8:** No regression in existing tests

---

## 6. ACTIVE SKILLS (MAX 3)
- `build-error-analysis` — Verify Gateway build passes
- `test-system-upgrade` — Add integration tests for API
- `domain-integrity-validation` — Ensure DTOs align with domain

---

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 4
- **Verified Facts:**
  - Fact 1: `OnboardingController` hiện tại có endpoint `/api/v1/onboarding/...`
  - Fact 2: `2_Gateway/Program.cs` đăng ký `IOnboardingService` và `ICustomerOnboardingService`
  - Fact 3: `SystemAdmin` policy exists trong ShopERP authorization
  - Fact 4: Gateway uses `RequireTenantAccess` policy hiện tại
- **Assumptions:**
  - Endpoint onboarding tenant chỉ dành cho `SystemAdmin` (platform-level admin)
  - Có thể reuse `OnboardTenantRequest` từ CoreHub hoặc tạo Gateway DTO
- **Open Questions:**
  - Q1: Nên để endpoint trong `OnboardingController` hay tạo `TenantOnboardingController`?
  - Q2: Có cần version API khác (`/api/v2/...`) không?
  - Q3: Có cần auto-generate owner password nếu không provided?

---

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| `2_Gateway/Controllers/OnboardingController.cs` | UPDATE - add endpoint | Keep controller thin |
| `2_Gateway/Program.cs` | UPDATE - register DI | Register `ITenantOnboardingService` + strategies |
| `6_Tests/VanAn.Integration.Tests/TenantOnboardingApiTests.cs` | NEW - tests | Use GatewayWebApplicationFactory |

---

## 9. TDD & TESTING STRATEGY
- **Integration tests:**
  - POST onboarding tenant with valid request returns 201
  - POST without auth returns 401
  - POST with non-SystemAdmin returns 403
  - Response contains tenantId and ownerUserId
  - Owner can authenticate after onboarding
- **Unit tests:** Không trong wave này (controller logic rất ít)
- **E2E tests:** Không trong wave này

---

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | - Chốt endpoint path<br>- Chốt authorization policy<br>- Chốt request/response DTOs | - Update OnboardingController<br>- Register DI<br>- Add integration tests<br>- Run build |

---

## 11. DETAILED CODING STEPS

### 11.1 Endpoint
```csharp
[HttpPost("tenants")]
[Authorize(Policy = "SystemAdmin")]
public async Task<ActionResult<TenantOnboardingResult>> CreateTenantOnboarding(
    [FromBody] OnboardTenantRequest request,
    CancellationToken ct = default)
{
    if (!ModelState.IsValid)
        return BadRequest(ModelState);

    try
    {
        TenantOnboardingResult result = await _onboardingService.OnboardAsync(request, ct);
        return CreatedAtAction(
            nameof(GetTenantOnboarding),
            new { tenantId = result.TenantId },
            result);
    }
    catch (ArgumentException ex)
    {
        return BadRequest(new { error = ex.Message });
    }
    catch (InvalidOperationException ex)
    {
        return UnprocessableEntity(new { error = ex.Message });
    }
}
```

### 11.2 DI Registration in Gateway
```csharp
_ = builder.Services.AddScoped<ITenantOnboardingService, TenantOnboardingService>();
_ = builder.Services.AddScoped<IIndustrySeedStrategy, FnbSeedStrategy>();
// Add stub strategies as needed
```

### 11.3 Authorization
- Endpoint sử dụng `[Authorize(Policy = "SystemAdmin")]`
- Nếu policy chưa có trong Gateway, cần đăng ký trong `2_Gateway/Program.cs`

---

## 12. EXIT CHECKLIST
- [ ] Endpoint implemented
- [ ] DI registered in Gateway
- [ ] Integration tests pass
- [ ] `dotnet build VanAn.sln` 0 errors
- [ ] `guard-check.ps1` pass
- [ ] Commit với message `[WAVE 4] Tenant onboarding Gateway API`
- [ ] Ready for Wave 5
