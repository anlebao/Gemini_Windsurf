# TASK CARD: PRODUCTION_HYGIENE - WAVE14 - Integration Tests for Request Signing Pipeline

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Viết integration tests end-to-end cho toàn bộ HMAC request signing pipeline
- **Nghiệp vụ áp dụng:** Verify rằng middleware, anti-replay, key management, và rate-limiting hoạt động đúng khi kết hợp
- **SRS gốc:** Validate implementation against simplified VA-LKR requirements

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `newfeaturebuild.md`
- **Execution Mode:** IMPLEMENT (tests after features)

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
  - `6_Tests/VanAn.Core.Tests/Security/HmacSigningIntegrationTests.cs` (TẠO MỚI)
  - `6_Tests/VanAn.Core.Tests/Security/HmacSignatureMiddlewareTests.cs` (verify unit tests)
  - `6_Tests/VanAn.Core.Tests/Security/ReplayProtectionServiceTests.cs` (verify unit tests)
  - `6_Tests/VanAn.Core.Tests/Security/ApiKeyServiceTests.cs` (verify unit tests)
  - `6_Tests/VanAn.Core.Tests/Security/ApiKeyRateLimitTests.cs` (verify unit tests)
  - `docs/AI/tasks/PRODUCTION_HYGIENE_master_plan.md` (cập nhật status)
- **Boundary Rules (Nghiêm cấm):**
  - KHÔNG sửa production code trong task này (chỉ test code)
  - KHÔNG skip tests vì "khó setup"
  - Mọi test phải deterministic (no flaky)

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)

### Test Scenarios (Minimum 8 integration tests)

```csharp
// Scenario 1: Happy path — valid signature
[Fact] ValidSignature_ReturnsOk()

// Scenario 2: Missing headers
[Fact] MissingSignatureHeaders_Returns401()

// Scenario 3: Invalid signature (tampered body)
[Fact] TamperedBody_InvalidSignature_Returns401()

// Scenario 4: Expired timestamp
[Fact] ExpiredTimestamp_Returns401()

// Scenario 5: Replay attack (duplicate nonce)
[Fact] DuplicateNonce_ReplayDetected_Returns401()

// Scenario 6: Revoked API key
[Fact] RevokedApiKey_Returns401()

// Scenario 7: Rate-limited (blocked key)
[Fact] BlockedApiKey_Returns429_WithRetryAfter()

// Scenario 8: Key rotation — old key invalid, new key valid
[Fact] RotatedKey_OldSecretInvalid_NewSecretValid()

// Bonus Scenario 9: Body-less request (GET)
[Fact] GetRequest_NoBody_ValidSignature_ReturnsOk()

// Bonus Scenario 10: Large body
[Fact] LargeBody_ValidSignature_ReturnsOk()
```

### Test Infrastructure
```csharp
// Use WebApplicationFactory<Program> for integration tests
// Setup: Create test API Key in DB, use known secret for signing
// Helper: HmacTestHelper.SignRequest(httpMethod, path, apiKeyId, secret, body)
```

### Constraints
- [ ] **WebApplicationFactory:** Sử dụng in-memory test server (không cần external deps)
- [ ] **Deterministic:** Tất cả tests phải pass 100% mỗi lần chạy
- [ ] **Isolated:** Mỗi test tự setup/teardown data
- [ ] **Fast:** Tất cả tests < 5 seconds total
- [ ] **Helper reusable:** `HmacTestHelper` class để sign requests trong tests
- [ ] **No real crypto secrets:** Test dùng fixed known secrets

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** Minimum 8 integration tests PASS
- [ ] **SC2:** Tất cả unit tests từ W14-T1→T4 vẫn PASS (no regression)
- [ ] **SC3:** Tests cover: happy path, auth failures, replay, rate-limit, rotation
- [ ] **SC4:** `dotnet test` → all Security tests green
- [ ] **SC5:** `dotnet build VanAn.sln` → 0 errors
- [ ] **SC6:** `HmacTestHelper` utility class reusable cho future tests
- [ ] **SC7:** PRODUCTION_HYGIENE_master_plan.md updated W14-T5 = ✅ DONE
- [ ] **SC8:** Wave 14 exit criteria MET → ready for PR

**Implementation Date:** TBD
**Branch:** feature/wave14-api-request-signing

## 6. ACTIVE SKILLS (MAX 3)
- `test-system-upgrade` — Comprehensive test coverage
- `build-error-analysis` — Fix test compilation errors
- `pattern-based-fixing` — Follow existing test patterns

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 4
- **Verified Facts:**
  - Fact 1: Existing test projects use xUnit + WebApplicationFactory pattern
  - Fact 2: W14-T1→T4 đã implement production code (dependency met)
  - Fact 3: SQLite in-memory available cho test DB
  - Fact 4: Existing test patterns in `6_Tests/VanAn.Core.Tests/`
- **Assumptions:**
  - WebApplicationFactory compatible với middleware pipeline
- **Open Questions:**
  - Không có
- **Recommended Action:** IMPLEMENT — all dependencies met, well-established patterns

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| `6_Tests/VanAn.Core.Tests/Security/*.cs` | New test files — no reverse impact | N/A |
| Test infrastructure | May need test helpers | Create reusable HmacTestHelper |

## 9. TDD & E2E TESTING STRATEGY
- **This IS the testing task** — write integration tests for W14-T1→T4
- **Test boundary:**
  - Integration tests: Full pipeline test via WebApplicationFactory
  - Regression: Verify unit tests still pass
  - Coverage target: All critical paths exercised
- **Test naming convention:** `MethodUnderTest_Scenario_ExpectedResult`

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | Review WebApplicationFactory pattern + existing test setup | Create `HmacTestHelper`, setup test infrastructure |
| **S2** | List all scenarios to test | Write 8-10 integration tests |
| **S3** | Run all tests, fix failures | Green all tests, verify no regression |

### Rules
- KHÔNG skip edge cases
- Mỗi test phải have clear Arrange/Act/Assert
- Test names phải self-documenting
- Helper methods cho signing — DRY principle

## 11. ESTIMATED EFFORT
- Medium effort — nhiều test scenarios nhưng pattern repetitive
- 2 sessions theo JIT Planning
- **BLOCKER:** W14-T1, T2, T3, T4 phải COMPLETE trước (production code must exist)
