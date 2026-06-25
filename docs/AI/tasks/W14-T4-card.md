# TASK CARD: PRODUCTION_HYGIENE - WAVE14 - Implement Key Revocation + Rate Limiting

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Implement auto-block khi phát hiện brute-force signature attempts + manual key revocation
- **Nghiệp vụ áp dụng:** Chống brute-force — tự động khóa API Key khi có nhiều lần ký sai liên tục
- **SRS gốc:** `docs/requirements/Van_An_Solution_SRS_Lightweight_Key_Management_Protocol.md` (Section 4.2 — simplified)

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `newfeaturebuild.md`
- **Execution Mode:** IMPLEMENT (after approved plan)

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
  - `2_Gateway/Middleware/HmacSignatureMiddleware.cs` (thêm rate-limiting logic)
  - `3_CoreHub/Services/ApiKeyService.cs` (thêm IncrementFailedAttempts, CheckBlocked)
  - `3_CoreHub/Services/IApiKeyService.cs` (thêm interface methods)
  - `1_Shared/Security/ApiKey.cs` (FailedAttempts + BlockedUntil đã defined trong W14-T3)
  - `docs/AI/tasks/PRODUCTION_HYGIENE_master_plan.md` (cập nhật status)
- **Boundary Rules (Nghiêm cấm):**
  - KHÔNG sửa Domain layer
  - KHÔNG permanent-ban (chỉ temporary block 15 phút)
  - KHÔNG block legitimate requests khi key đã unblock

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)

### Rate Limiting Logic
```csharp
// Trong HmacSignatureMiddleware, sau khi signature verification FAIL:
async Task OnSignatureFailure(string apiKeyId)
{
    var failCount = await _apiKeyService.IncrementFailedAttempts(apiKeyId);
    if (failCount >= 5)
    {
        await _apiKeyService.BlockKey(apiKeyId, TimeSpan.FromMinutes(15));
        _logger.LogWarning("API Key {KeyId} blocked: {Count} failed attempts", apiKeyId, failCount);
    }
}

// Trong middleware, TRƯỚC signature verification:
var blockStatus = await _apiKeyService.IsBlocked(apiKeyId);
if (blockStatus.IsBlocked)
{
    context.Response.StatusCode = 429; // Too Many Requests
    await context.Response.WriteAsJsonAsync(new { error = "API key temporarily blocked", retryAfter = blockStatus.RetryAfterSeconds });
    return;
}
```

### Auto-Reset Logic
```csharp
// Khi signature verification SUCCESS:
await _apiKeyService.ResetFailedAttempts(apiKeyId);
// → FailedAttempts = 0, BlockedUntil = null
```

### Revocation (Manual via Admin)
```csharp
// Already covered by DELETE /api/admin/api-keys/{id} in W14-T3
// Revoked key → IsActive = false → Middleware rejects with 401 "API key revoked"
```

### Constraints
- [ ] **Threshold:** 5 failed attempts trong sliding window → block 15 phút
- [ ] **Auto-unblock:** Sau 15 phút, key tự động unblock (check BlockedUntil < UtcNow)
- [ ] **Reset on success:** Successful request → reset FailedAttempts về 0
- [ ] **429 response:** Blocked key → HTTP 429 với `Retry-After` header
- [ ] **Audit logging:** Mọi block/unblock event phải được log
- [ ] **Không cascade:** Block 1 key KHÔNG ảnh hưởng keys khác cùng tenant
- [ ] **DB persistence:** FailedAttempts + BlockedUntil persisted (survive app restart)

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** 4 failed attempts → request vẫn pass (chưa đạt threshold)
- [ ] **SC2:** 5th failed attempt → key blocked, response 429
- [ ] **SC3:** Request tới blocked key → 429 với Retry-After
- [ ] **SC4:** Sau 15 phút → key unblocked, request pass
- [ ] **SC5:** Successful request → FailedAttempts reset về 0
- [ ] **SC6:** Revoked key (IsActive=false) → 401 "API key revoked"
- [ ] **SC7:** `dotnet build VanAn.sln` → 0 errors
- [ ] **SC8:** PRODUCTION_HYGIENE_master_plan.md updated W14-T4 = ✅ DONE

**Implementation Date:** TBD
**Branch:** feature/wave14-api-request-signing

## 6. ACTIVE SKILLS (MAX 3)
- `domain-integrity-validation` — Verify rate-limiting logic correct
- `build-error-analysis` — Fix compile errors
- `pattern-based-fixing` — Follow established service patterns

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 3
- **Verified Facts:**
  - Fact 1: ApiKey entity sẽ có FailedAttempts + BlockedUntil (from W14-T3 design)
  - Fact 2: Middleware exists (from W14-T1) → chỉ cần add rate-limit logic
  - Fact 3: EF Core SaveChangesAsync pattern established
- **Assumptions:**
  - DB update cho FailedAttempts không gây bottleneck
- **Open Questions:**
  - Q1: Nên dùng DB write mỗi failed attempt hay batch?
- **Recommended Action:** IMPLEMENT — straightforward increment + check pattern. DB write mỗi attempt acceptable cho low-volume

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| `2_Gateway/Middleware/HmacSignatureMiddleware.cs` | Thêm rate-limit branches | Test cả happy path VÀ blocked path |
| `3_CoreHub/Services/ApiKeyService.cs` | Thêm methods | Additive change, no existing code modified |

## 9. TDD & E2E TESTING STRATEGY
- **Unit tests:** Test rate limiting logic in isolation
- **Test boundary:**
  - Unit tests: `6_Tests/VanAn.Core.Tests/Security/ApiKeyRateLimitTests.cs`
  - Integration tests: Covered by W14-T5
  - E2E tests: N/A
- **Minimum test coverage:** 6 unit tests (under threshold, at threshold, blocked, auto-unblock, reset on success, revoked key)

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | Confirm ApiKey fields available (from W14-T3) | Add IncrementFailedAttempts, IsBlocked, ResetFailedAttempts to service |
| **S2** | Confirm middleware integration point | Wire rate-limiting into HmacSignatureMiddleware |

### Rules
- Use UTC for all time comparisons
- Log: `"API Key blocked: {KeyPrefix}*** after {Count} failed attempts"`
- NEVER log the full API Key ID in production (use prefix only)
- 429 response MUST include `Retry-After` header (seconds)

## 11. ESTIMATED EFFORT
- Low-Medium effort — straightforward increment/check pattern
- 1-2 sessions theo JIT Planning
- **BLOCKER:** W14-T1 (middleware) + W14-T3 (entity + service) phải exist trước
