# TASK CARD: PRODUCTION_HYGIENE - WAVE14 - Implement API Key Registration & Management

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Implement API Key entity, storage, và CRUD management endpoints cho admin
- **Nghiệp vụ áp dụng:** Quản lý API Key per-tenant — tạo, xem, thu hồi, xoay vòng key cho external clients
- **SRS gốc:** Simplified from Device Enrollment (SRS Section 2.1) → API Key per-tenant thay vì per-device

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `newfeaturebuild.md`
- **Execution Mode:** IMPLEMENT (after approved plan)

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
  - `1_Shared/Security/ApiKey.cs` (TẠO MỚI — entity)
  - `3_CoreHub/Infrastructure/Configurations/ApiKeyConfiguration.cs` (TẠO MỚI — EF config)
  - `3_CoreHub/Infrastructure/VanAnDbContext.cs` (thêm DbSet<ApiKey>)
  - `3_CoreHub/Services/ApiKeyService.cs` (TẠO MỚI)
  - `3_CoreHub/Services/IApiKeyService.cs` (TẠO MỚI — interface)
  - `2_Gateway/Controllers/ApiKeyController.cs` (TẠO MỚI — admin CRUD)
  - `2_Gateway/Program.cs` (register DI)
  - `docs/AI/tasks/PRODUCTION_HYGIENE_master_plan.md` (cập nhật status)
- **Boundary Rules (Nghiêm cấm):**
  - KHÔNG sửa `1_Shared/Domain.cs` (ApiKey là Security entity, KHÔNG phải Domain entity)
  - KHÔNG expose API Key secret sau lần tạo đầu tiên
  - KHÔNG store plaintext secret — chỉ store hash (SHA256 hoặc BCrypt)
  - ApiKeyController BẮT BUỘC `[Authorize(Roles = "Admin")]`

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)

### Entity Design
```csharp
// 1_Shared/Security/ApiKey.cs
public class ApiKey
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; }           // "KhachLink Production", "Mobile App Dev"
    public string KeyPrefix { get; set; }      // First 8 chars for identification (e.g., "va_live_")
    public string SecretHash { get; set; }     // SHA256(secret) — never store plaintext
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }   // null = never expires
    public DateTime? LastUsedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public int FailedAttempts { get; set; }    // For rate limiting (W14-T4)
    public DateTime? BlockedUntil { get; set; } // For rate limiting (W14-T4)
}
```

### API Endpoints
```
POST   /api/admin/api-keys         → Create (returns secret ONCE)
GET    /api/admin/api-keys         → List (tenant-filtered, no secrets)
GET    /api/admin/api-keys/{id}    → Get details (no secret)
DELETE /api/admin/api-keys/{id}    → Revoke (soft-delete: IsActive=false)
POST   /api/admin/api-keys/{id}/rotate → Generate new secret, invalidate old
```

### Key Generation
```csharp
// Generate: 32 bytes crypto-random → Base64Url → prefix with "va_live_" or "va_test_"
var secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
var fullKey = $"va_live_{secret}";  // Display to user ONCE
var hash = SHA256.HashData(Encoding.UTF8.GetBytes(fullKey));
// Store only hash in DB
```

### Multi-tenancy
- [ ] **TenantId filtering:** Mọi query phải filter by TenantId (from JWT claim)
- [ ] **Admin only:** Tất cả endpoints `[Authorize(Roles = "Admin")]`
- [ ] **Tenant isolation:** Admin chỉ thấy API Keys của tenant mình

### Constraints
- [ ] **Secret hiển thị 1 lần:** Response tạo key chứa secret, response sau KHÔNG BAO GIỜ
- [ ] **Prefix identification:** Key prefix giúp identify key type mà không cần decrypt
- [ ] **Soft delete:** Revoke = set IsActive=false + RevokedAt, không xóa record
- [ ] **Expiration:** Optional, default 90 ngày, configurable per key
- [ ] **Max keys per tenant:** 10 (prevent abuse)

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** `ApiKey` entity compiled, EF migration tạo table thành công
- [ ] **SC2:** POST /api/admin/api-keys → 201 + secret trong response
- [ ] **SC3:** GET /api/admin/api-keys → list keys (không có secret)
- [ ] **SC4:** DELETE /api/admin/api-keys/{id} → 204, key bị revoke
- [ ] **SC5:** IApiKeyService.ValidateAndGetSecret(apiKeyId) → trả về secret hash cho middleware
- [ ] **SC6:** Unauthorized user gọi /api/admin/api-keys → 403
- [ ] **SC7:** `dotnet build VanAn.sln` → 0 errors
- [ ] **SC8:** PRODUCTION_HYGIENE_master_plan.md updated W14-T3 = ✅ DONE

**Implementation Date:** TBD
**Branch:** feature/wave14-api-request-signing

## 6. ACTIVE SKILLS (MAX 3)
- `domain-integrity-validation` — Verify entity placement correct (Security, not Domain)
- `build-error-analysis` — Fix compile errors
- `accounting-ui-implementation` — Admin CRUD pattern reuse

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 4
- **Verified Facts:**
  - Fact 1: VanAnDbContext exists, supports DbSet additions
  - Fact 2: EF Configuration pattern established (14 existing configurations)
  - Fact 3: JWT + Role-based auth đã hoạt động (Wave 0, 4, 6)
  - Fact 4: TenantId filtering pattern established across codebase
- **Assumptions:**
  - EF migration compatible với existing SQLite schema
- **Open Questions:**
  - Q1: Migration strategy — auto-migrate hoặc manual script?
- **Recommended Action:** IMPLEMENT — well-established patterns to follow

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| `3_CoreHub/Infrastructure/VanAnDbContext.cs` | Thêm DbSet → migration needed | Follow existing migration pattern |
| `2_Gateway/Controllers/ApiKeyController.cs` | New controller, new routes | [Authorize(Roles="Admin")] protect |
| `2_Gateway/Program.cs` | DI registration | Low risk |

## 9. TDD & E2E TESTING STRATEGY
- **Unit tests:** Test ApiKeyService (create, validate, revoke)
- **Test boundary:**
  - Unit tests: `6_Tests/VanAn.Core.Tests/Security/ApiKeyServiceTests.cs`
  - Integration tests: Covered by W14-T5
  - E2E tests: N/A
- **Minimum test coverage:** 7 unit tests (create, list, get, revoke, rotate, validate-active, validate-expired)

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | Review existing entity + config patterns | Create ApiKey entity + EF Configuration |
| **S2** | Review existing service patterns | Create IApiKeyService + ApiKeyService |
| **S3** | Review existing controller patterns | Create ApiKeyController + DI registration |
| **S4** | Build verification | Run build, fix errors, verify endpoints |

### Rules
- NEVER store plaintext secret
- NEVER return secret after initial creation
- ALWAYS filter by TenantId
- Follow existing Controller → Service → Repository pattern

## 11. ESTIMATED EFFORT
- Medium effort — standard CRUD pattern nhưng cần careful security implementation
- 2-3 sessions theo JIT Planning
- **BLOCKER:** Không có — có thể bắt đầu song song với W14-T1
