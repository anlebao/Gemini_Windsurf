# TASK CARD: [FIX-1] — AccountingEntriesControllerTests JWT Claim Migration Fix

**Created:** 2026-06-19
**Priority:** P1 — Test suite integrity (9 failing tests blocking CI confidence)
**Effort:** LOW (~30 min, 1 file, no production code change)
**Branch:** `fix/test-jwt-tenantid-accounting-controller`

---

## 1. GOAL & CONTEXT

- **Mục tiêu cốt lõi:** Sửa 9/10 test trong `AccountingEntriesControllerTests.cs` đang fail do test dùng `X-Tenant-Id` HTTP header đã bị deprecated; và sửa 1 test assert message sai. Không thay đổi production code.
- **Nghiệp vụ áp dụng:** Wave 1 Phase 2 (Security) đã migrate `AccountingEntriesController` từ header-based TenantId sang JWT claim `tenant_id`. Tests không được cập nhật theo, dẫn đến `UnauthorizedObjectResult` trên tất cả requests vì `GetTenantIdFromClaim()` không tìm thấy claim trong `DefaultHttpContext`.

### Root Cause (đã verify)

```
Controller (production — đúng):
  GetTenantIdFromClaim() → reads User.FindFirst("tenant_id") from ClaimsPrincipal
  → Guid.Empty nếu không có claim → return Unauthorized({ error = "Tenant ID required in JWT claim" })

Tests (sai — chưa update):
  _controller.ControllerContext.HttpContext.Request.Headers["X-Tenant-Id"] = tenantId
  → DefaultHttpContext.User là anonymous, không có "tenant_id" claim
  → GetTenantIdFromClaim() = Guid.Empty → tất cả return Unauthorized
```

### Failing tests (9 tests đều return `UnauthorizedObjectResult`)

| Test | Expected | Actual |
|---|---|---|
| `CreateRevenueEntry_ShouldReturnCreated_WhenValidRequest` | `CreatedAtActionResult` | `UnauthorizedObjectResult` |
| `CreateExpenseEntry_ShouldReturnCreated_WhenValidRequest` | `CreatedAtActionResult` | `UnauthorizedObjectResult` |
| `GetEntryById_ShouldReturnOk_WhenEntryExists` | `OkObjectResult` | `UnauthorizedObjectResult` |
| `GetEntryById_ShouldReturnNotFound_WhenEntryDoesNotExist` | `NotFoundResult` | `UnauthorizedObjectResult` |
| `CreateReversalEntry_ShouldReturnCreated_WhenValidRequest` | `CreatedAtActionResult` | `UnauthorizedObjectResult` |
| `CreateReversalEntry_ShouldReturnBadRequest_WhenEntryCannotBeReversed` | `BadRequestObjectResult` | `UnauthorizedObjectResult` |
| `GetRevenueSummary_ShouldReturnSummary_WhenValidRequest` | `OkObjectResult` | `UnauthorizedObjectResult` |
| `GetProfitSummary_ShouldReturnSummary_WhenValidRequest` | `OkObjectResult` | `UnauthorizedObjectResult` |
| `GetEntryById_ShouldReturnUnauthorized_WhenTenantIdMissing` | message `"Tenant ID required"` | `{ error = "Tenant ID required in JWT claim" }` |

---

## 2. ACTIVE WORKFLOW ROUTING

- **Target Workflow:** `.devin/workflows/Fix_Errors.md`
- **Execution Mode:** FIX_ONLY

---

## 3. RELEVANT FILES (CONTEXT BOUNDARY)

**Files được phép đọc/sửa:**

| File | Action | Lý do |
|---|---|---|
| `docs/AI/project_state.md` | Read | Bắt buộc đầu phiên |
| `6_Tests/VanAn.Core.Tests/Accounting/AccountingEntriesControllerTests.cs` | **Edit** | Fix JWT claim setup thay cho X-Tenant-Id header |
| `2_Gateway/Controllers/AccountingEntriesController.cs` | Read only | Verify `GetTenantIdFromClaim()` signature + message text |

**Boundary Rules — Nghiêm cấm:**
- CẤM sửa `AccountingEntriesController.cs` (production code đúng, chỉ test sai)
- CẤM sửa Domain layer
- CẤM thêm feature mới
- CẤM sửa bất kỳ file nào ngoài `AccountingEntriesControllerTests.cs`

---

## 4. TECHNICAL CONSTRAINTS

- [ ] **No production code change:** Chỉ sửa file test — controller logic đúng, test sai.
- [ ] **JWT Claim name:** Controller dùng dual-read: `"tenant_id"` (primary) hoặc `"TenantId"` (legacy). Test helper phải set `"tenant_id"`.
- [ ] **ClaimsIdentity setup:** Dùng `System.Security.Claims.ClaimsIdentity` + `ClaimsPrincipal` — không dùng mock auth middleware.
- [ ] **DefaultHttpContext:** `ControllerContext.HttpContext = new DefaultHttpContext()` → gán thêm `.User = new ClaimsPrincipal(identity)`.
- [ ] **Message assertion:** 1 test assert đúng message `"Tenant ID required in JWT claim"` (không phải `"Tenant ID required"`).

---

## 5. IMPLEMENTATION PLAN

### Step 1 — Thêm `using` statement

```csharp
using System.Security.Claims;
```

### Step 2 — Thêm private helper method vào test class

```csharp
/// <summary>
/// Sets up JWT tenant_id claim on the controller's HttpContext.
/// Replaces deprecated X-Tenant-Id header approach (Wave 1 Phase 2 migration).
/// </summary>
private void SetTenantClaim(Guid tenantId)
{
    var claims = new[] { new Claim("tenant_id", tenantId.ToString()) };
    var identity = new ClaimsIdentity(claims, "TestAuth");
    _controller.ControllerContext.HttpContext = new DefaultHttpContext
    {
        User = new ClaimsPrincipal(identity)
    };
}
```

### Step 3 — Thay thế header setup trong 8 test cases

Mỗi test có đoạn:
```csharp
// Old (stale — không hoạt động)
_controller.ControllerContext.HttpContext = new DefaultHttpContext();
_controller.ControllerContext.HttpContext.Request.Headers["X-Tenant-Id"] = request.TenantId.ToString();
```

Thay bằng:
```csharp
// New (correct — JWT claim)
SetTenantClaim(request.TenantId);
```

Các tests dùng `tenantId` thay vì `request.TenantId`:
```csharp
// Old
_controller.ControllerContext.HttpContext.Request.Headers["X-Tenant-Id"] = tenantId.ToString();
// New
SetTenantClaim(tenantId);
```

### Step 4 — Fix message assertion trong 1 test

Test: `GetEntryById_ShouldReturnUnauthorized_WhenTenantIdMissing`

```csharp
// Old (stale message)
Assert.Equal("Tenant ID required", unauthorizedResult.Value);

// New (match controller's actual message)
// Controller returns: new { error = "Tenant ID required in JWT claim" }
// Test phải dùng anonymous object hoặc dynamic để match
var value = unauthorizedResult.Value;
Assert.NotNull(value);
// Verify the error property matches
```

> **Note:** Controller trả `new { error = "..." }` — anonymous object. Test hiện assert `unauthorizedResult.Value` là string đơn giản. Cần đổi sang dynamic read hoặc serialize-compare.

---

## 6. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)

- [ ] **SC1:** `AccountingEntriesControllerTests` — 10/10 tests PASS (hiện 1/10)
- [ ] **SC2:** `CreateRevenueEntry_ShouldReturnCreated_WhenValidRequest` → `CreatedAtActionResult` ✅
- [ ] **SC3:** `CreateExpenseEntry_ShouldReturnCreated_WhenValidRequest` → `CreatedAtActionResult` ✅
- [ ] **SC4:** `GetEntryById_ShouldReturnOk_WhenEntryExists` → `OkObjectResult` ✅
- [ ] **SC5:** `GetEntryById_ShouldReturnNotFound_WhenEntryDoesNotExist` → `NotFoundResult` ✅
- [ ] **SC6:** `CreateReversalEntry_ShouldReturnCreated_WhenValidRequest` → `CreatedAtActionResult` ✅
- [ ] **SC7:** `CreateReversalEntry_ShouldReturnBadRequest_WhenEntryCannotBeReversed` → `BadRequestObjectResult` ✅
- [ ] **SC8:** `GetRevenueSummary_ShouldReturnSummary_WhenValidRequest` → `OkObjectResult` ✅
- [ ] **SC9:** `GetProfitSummary_ShouldReturnSummary_WhenValidRequest` → `OkObjectResult` ✅
- [ ] **SC10:** `GetEntryById_ShouldReturnUnauthorized_WhenTenantIdMissing` → asserts correct message ✅
- [ ] **SC11:** Không có production code nào bị thay đổi (verify `git diff --stat` chỉ có test file)
- [ ] **SC12:** `dotnet build VanAn.sln --configuration Release` → 0 errors
- [ ] **SC13:** `guard-check.ps1` → EXIT 0

---

## 7. AI HEALTH CHECK MATRIX

**Evidence Count:** 5 verified facts, 0 open questions

**Verified Facts:**
- Fact 1: `GetTenantIdFromClaim()` reads `User.FindFirst("tenant_id")` — confirmed by reading controller line 336 (2026-06-19)
- Fact 2: Controller error message là `new { error = "Tenant ID required in JWT claim" }` — confirmed line 52
- Fact 3: Tests set `Request.Headers["X-Tenant-Id"]` — confirmed by reading test file lines 81, 167 (2026-06-19)
- Fact 4: `DefaultHttpContext.User` là anonymous `ClaimsPrincipal` — không có claims → `GetTenantIdFromClaim()` = `Guid.Empty`
- Fact 5: Wave 1 Phase 2 committed at `c4d6acc` đã migrate controller sang JWT claim — tests không update theo

**Open Questions:** None

**Recommended Action:** IMPLEMENT — tất cả facts đủ, không có assumptions.

---

## 8. REVERSE IMPACT ANALYSIS

| Thay đổi | Impact | Mitigation |
|---|---|---|
| Thêm `SetTenantClaim()` helper | Chỉ trong test class, không ảnh hưởng production | N/A |
| Thay `Headers["X-Tenant-Id"]` → `SetTenantClaim()` | Test coverage chính xác hơn (đúng auth flow) | Verify tất cả 10 tests pass |
| Fix message assertion | Align test với actual contract | N/A |

**Không có reverse impact lên production code.**
