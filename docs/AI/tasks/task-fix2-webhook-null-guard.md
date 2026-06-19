# TASK CARD: [FIX-2] — WebhookService Null callbackData Guard

**Created:** 2026-06-19
**Priority:** P2 — Test suite integrity + defensive production behavior
**Effort:** TRIVIAL (~5 min, 1 line in production code, 0 test changes)
**Branch:** `fix/webhook-null-callbackdata-guard`

---

## 1. GOAL & CONTEXT

- **Mục tiêu cốt lõi:** Thêm `ArgumentNullException` guard cho `callbackData = null` trong `WebhookService.ProcessWebhookAsync()`. Sửa 1 failing test `ProcessWebhookAsync_NullCallbackData_ShouldThrowArgumentNullException`.
- **Nghiệp vụ áp dụng:** Webhook từ nhà cung cấp hóa đơn điện tử (Viettel/MISA) — null payload phải bị reject rõ ràng để tránh silent no-op trong audit trail.

### Root Cause (đã verify)

```
Test expects: ArgumentNullException khi callbackData = null
Thực tế:      ProcessWebhookAsync() không có null-guard cho callbackData
              → null chạy vào ParseWebhookPayload()
              → string.IsNullOrWhiteSpace(null) = true → return (null, null)
              → processingSucceeded = true → KHÔNG throw exception
```

**Signature hiện tại:**
```csharp
public async Task ProcessWebhookAsync(
    string providerId,
    string providerInvoiceNumber,
    string callbackData,          // non-nullable string — nhưng không guard null
    CancellationToken cancellationToken = default)
{
    if (string.IsNullOrWhiteSpace(providerId)) throw ...     // ✅ has guard
    if (string.IsNullOrWhiteSpace(providerInvoiceNumber)) throw ...  // ✅ has guard
    // ❌ NO guard for callbackData
```

**Failing test:**
```
VanAn.Core.Tests.Services.WebhookServiceTests
  .ProcessWebhookAsync_NullCallbackData_ShouldThrowArgumentNullException
  
Assert.Throws() Failure: No exception was thrown
Expected: typeof(System.ArgumentNullException)
```

### Why this is the correct production behavior

- `null` callbackData từ external provider = malformed/broken webhook request
- Silent no-op (current behavior) tạo false audit trail entry ("processed" but payload was null)
- Explicit `ArgumentNullException` = fail-fast, caller biết payload invalid
- Consistent với pattern của `providerId` và `providerInvoiceNumber` guards đã có

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
| `3_CoreHub/Services/Orchestration/WebhookService.cs` | **Edit** | Thêm null-guard cho `callbackData` |
| `6_Tests/VanAn.Core.Tests/Services/WebhookServiceTests.cs` | Read only | Verify test không cần thay đổi |

**Boundary Rules — Nghiêm cấm:**
- CẤM sửa `WebhookServiceTests.cs` (test đúng, production code thiếu guard)
- CẤM thay đổi signature của `ProcessWebhookAsync`
- CẤM sửa bất kỳ interface `IWebhookService` nào
- CẤM thêm logic xử lý ngoài null-guard

---

## 4. TECHNICAL CONSTRAINTS

- [ ] **Minimal change:** 1 dòng thêm vào đầu `ProcessWebhookAsync`, trước các guards hiện có.
- [ ] **Exception type:** Phải là `ArgumentNullException` (không phải `ArgumentException`) — test assert đúng type.
- [ ] **Vị trí:** Thêm SAU `if (string.IsNullOrWhiteSpace(providerInvoiceNumber))` hoặc TRƯỚC — miễn là trước `BuildKey()` và `ParseWebhookPayload()`.
- [ ] **Không thay đổi empty string behavior:** `callbackData = ""` hiện xử lý thành công (test `ProcessWebhookAsync_EmptyCallbackData_ShouldProcessWithoutError` PASS) — chỉ reject `null`.

---

## 5. IMPLEMENTATION PLAN

### Thay đổi duy nhất — `WebhookService.cs`

Thêm 1 dòng null-guard sau các guard hiện có:

```csharp
public async Task ProcessWebhookAsync(
    string providerId,
    string providerInvoiceNumber,
    string callbackData,
    System.Threading.CancellationToken cancellationToken = default)
{
    if (string.IsNullOrWhiteSpace(providerId))
        throw new ArgumentException("ProviderId is required.", nameof(providerId));
    if (string.IsNullOrWhiteSpace(providerInvoiceNumber))
        throw new ArgumentException("ProviderInvoiceNumber is required.", nameof(providerInvoiceNumber));

    // ADD THIS LINE:
    if (callbackData is null)
        throw new ArgumentNullException(nameof(callbackData), "CallbackData cannot be null.");

    var key = BuildKey(providerId, providerInvoiceNumber);
    // ... rest unchanged
```

---

## 6. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)

- [ ] **SC1:** `ProcessWebhookAsync_NullCallbackData_ShouldThrowArgumentNullException` → PASS ✅
- [ ] **SC2:** `ProcessWebhookAsync_EmptyCallbackData_ShouldProcessWithoutError` vẫn PASS (không regression) ✅
- [ ] **SC3:** Tất cả 18/18 `WebhookServiceTests` PASS ✅
- [ ] **SC4:** Chỉ 1 dòng thêm vào `WebhookService.cs` (verify bằng `git diff`)
- [ ] **SC5:** `dotnet build VanAn.sln --configuration Release` → 0 errors
- [ ] **SC6:** `guard-check.ps1` → EXIT 0

---

## 7. AI HEALTH CHECK MATRIX

**Evidence Count:** 4 verified facts, 0 open questions

**Verified Facts:**
- Fact 1: `ProcessWebhookAsync` không có null-guard cho `callbackData` — confirmed by reading `WebhookService.cs` lines 44-53 (2026-06-19)
- Fact 2: `ParseWebhookPayload` xử lý null silently qua `IsNullOrWhiteSpace` check — confirmed lines 150-156
- Fact 3: Test asserts `ArgumentNullException` — confirmed reading `WebhookServiceTests.cs` line 315 (2026-06-19)
- Fact 4: Empty string test (`ProcessWebhookAsync_EmptyCallbackData`) PASS — null guard không ảnh hưởng

**Open Questions:** None

**Recommended Action:** IMPLEMENT — trivial 1-line fix, zero risk.

---

## 8. REVERSE IMPACT ANALYSIS

| Thay đổi | Impact | Mitigation |
|---|---|---|
| `if (callbackData is null) throw ArgumentNullException` | Callers passing `null` sẽ nhận exception thay vì silent no-op | Tất cả known callers dùng `string` không null — search confirms không có `null` call site |
| `WebhookController.ReceiveWebhook` (caller) | `[FromBody] string callbackData` → ASP.NET sẽ bind empty string nếu body rỗng, không bind null | Không bị ảnh hưởng |

**Verify call sites trước khi implement:**
```
grep -r "ProcessWebhookAsync" 2_Gateway/ 3_CoreHub/ --include="*.cs"
```
Expected: chỉ `WebhookController.cs` gọi service — `callbackData` từ `[FromBody]` không null khi ASP.NET bind.
