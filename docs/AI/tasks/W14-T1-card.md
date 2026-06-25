# TASK CARD: PRODUCTION_HYGIENE - WAVE14 - Implement HMAC Signing Middleware

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Implement HMAC-SHA256 request signing middleware tại Gateway layer
- **Nghiệp vụ áp dụng:** Chống Data Tampering — verify mọi external API request có chữ ký hợp lệ trước khi xử lý nghiệp vụ
- **SRS gốc:** `docs/requirements/Van_An_Solution_SRS_Lightweight_Key_Management_Protocol.md` (Section 3.3, 3.4 — simplified)

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `newfeaturebuild.md`
- **Execution Mode:** IMPLEMENT (after approved plan)

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
  - `2_Gateway/Middleware/HmacSignatureMiddleware.cs` (TẠO MỚI)
  - `2_Gateway/Program.cs` (thêm middleware registration)
  - `2_Gateway/Services/IApiKeyService.cs` (interface — consume from W14-T3)
  - `1_Shared/Security/HmacSigningConstants.cs` (TẠO MỚI — shared constants)
  - `docs/AI/tasks/PRODUCTION_HYGIENE_master_plan.md` (cập nhật status)
- **Boundary Rules (Nghiêm cấm):**
  - KHÔNG sửa `1_Shared/Domain.cs`
  - KHÔNG thêm Redis dependency
  - KHÔNG thay đổi existing JWT authentication flow
  - KHÔNG apply middleware cho internal Blazor Server routes (ShopERP)

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)

### Signing String Format (chuẩn hóa)
```
SigningString = HTTP_Method + "\n" +
               Request_Path + "\n" +
               X-VanAn-ApiKeyId + "\n" +
               X-VanAn-Timestamp + "\n" +
               X-VanAn-Nonce + "\n" +
               SHA256(Request_Body_Raw)
```

### Middleware Logic
```csharp
// Pseudo-code
1. Extract headers: X-VanAn-ApiKeyId, X-VanAn-Timestamp, X-VanAn-Nonce, X-VanAn-Signature
2. If any header missing → 401 "Missing signature headers"
3. Validate timestamp (delegate to W14-T2)
4. Validate nonce (delegate to W14-T2)
5. Lookup API Key secret by ApiKeyId (delegate to W14-T3 service)
6. Reconstruct signing string from request
7. Compute HMAC-SHA256(secret, signing_string)
8. Compare computed signature vs provided signature (constant-time comparison)
9. If mismatch → 401 "Invalid signature"
10. If valid → call next(context)
```

### Constraints
- [ ] **Async/await hoàn toàn:** Không block thread (ReadBodyAsync)
- [ ] **Constant-time comparison:** Dùng `CryptographicOperations.FixedTimeEquals()` — chống timing attack
- [ ] **Body buffering:** Enable request body buffering để đọc body cho signing + business logic
- [ ] **Pipeline order:** Middleware đặt SAU JWT auth, TRƯỚC routing/controllers
- [ ] **Selective apply:** Chỉ apply cho routes có attribute `[RequireHmacSignature]` hoặc config-based route matching
- [ ] **Logging:** Log failed attempts (ApiKeyId, IP, reason) — KHÔNG log secret hoặc signature

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** `HmacSignatureMiddleware.cs` compiled, registered trong Pipeline
- [ ] **SC2:** Request không có signature headers → 401 với message rõ ràng
- [ ] **SC3:** Request có signature sai → 401
- [ ] **SC4:** Request có signature đúng → pass-through tới controller
- [ ] **SC5:** Body không bị consume (controller vẫn đọc được body sau middleware)
- [ ] **SC6:** `dotnet build VanAn.sln` → 0 errors
- [ ] **SC7:** PRODUCTION_HYGIENE_master_plan.md updated W14-T1 = ✅ DONE

**Implementation Date:** TBD
**Branch:** feature/wave14-api-request-signing

## 6. ACTIVE SKILLS (MAX 3)
- `domain-integrity-validation` — Verify middleware không violate architecture
- `build-error-analysis` — Fix compile errors nhanh
- `pattern-based-fixing` — Follow existing middleware patterns trong Gateway

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 3
- **Verified Facts:**
  - Fact 1: Gateway đã có JWT middleware (Program.cs line 46-79)
  - Fact 2: .NET 8 có `CryptographicOperations.FixedTimeEquals()` built-in
  - Fact 3: Không có Redis trong codebase — phải dùng IMemoryCache
- **Assumptions:**
  - Gateway pipeline cho phép thêm middleware mới
- **Open Questions:**
  - Q1: Route matching strategy — attribute-based hay config-based?
  - Q2: Body size limit cho HMAC computation?
- **Recommended Action:** IMPLEMENT — đủ verified facts, assumptions < facts

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| `2_Gateway/Program.cs` | Thêm middleware → pipeline order change | Đặt sau JWT, trước routing. Test pipeline order |
| `2_Gateway/Middleware/HmacSignatureMiddleware.cs` | File mới — no reverse | N/A |
| `1_Shared/Security/HmacSigningConstants.cs` | File mới — no reverse | N/A |

## 9. TDD & E2E TESTING STRATEGY
- **Unit tests:** Mock IApiKeyService, test signing logic isolation
- **Test boundary:**
  - Unit tests: `6_Tests/VanAn.Core.Tests/Security/HmacSignatureMiddlewareTests.cs`
  - Integration tests: Covered by W14-T5
  - E2E tests: N/A (middleware-level, not UI)
- **Minimum test coverage:** 5 unit tests (happy path, missing headers, bad sig, expired key, body buffering)

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Micro-phase breakdown

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | Xác nhận Pipeline order, chọn route matching strategy | Tạo `HmacSigningConstants.cs` với header names, signing format |
| **S2** | Review existing middleware patterns | Implement `HmacSignatureMiddleware.cs` core logic |
| **S3** | Integrate with IApiKeyService interface | Register middleware, add `[RequireHmacSignature]` attribute |
| **S4** | Validate build + basic manual test | Fix compile errors, verify pipeline |

### Rules
- KHÔNG thêm dependency mới (chỉ dùng System.Security.Cryptography built-in)
- Body buffering: `context.Request.EnableBuffering()` ở đầu middleware
- Constant-time comparison bắt buộc — KHÔNG dùng `==` cho byte arrays

## 11. ESTIMATED EFFORT
- Medium effort — standard middleware pattern nhưng cần careful crypto implementation
- 2 sessions theo JIT Planning
- **BLOCKER:** Cần W14-T3 (IApiKeyService interface) để lookup secret — có thể mock trước
