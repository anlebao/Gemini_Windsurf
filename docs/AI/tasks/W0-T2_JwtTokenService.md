# TASK CARD: SECURITY - Wave 0 - JwtTokenService Implementation

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Tạo `JwtTokenService` có khả năng issue JWT token chứa claims `sub`, `email`, `role`, `tenant_id`, `exp` — được verify bởi Gateway's `AddJwtBearer` middleware.
- **Nghiệp vụ áp dụng:** Authentication foundation cho toàn bộ VanAn Ecosystem. Token phải stateless, expire sau 8 giờ, hỗ trợ role-based claims để Wave 4 enforce RBAC tại UI.

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `.devin/workflows/newfeaturebuild.md`
- **Execution Mode:** IMPLEMENT (plan đã approved trong master plan)

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
  - `3_CoreHub/Services/JwtTokenService.cs` (TẠO MỚI)
  - `3_CoreHub/Services/IJwtTokenService.cs` (TẠO MỚI)
  - `3_CoreHub/Program.cs` (thêm DI registration)
  - `1_Shared/Domain.cs` (đọc — xem UserRole enum, DemoUser entity)
  - `3_CoreHub/VanAn.CoreHub.csproj` (thêm PackageReference)
  - `3_CoreHub/appsettings.json` + `3_CoreHub/appsettings.Development.json` (thêm `Jwt:Secret`, `Jwt:Issuer`, `Jwt:Audience`)

- **Boundary Rules (Nghiêm cấm):**
  - KHÔNG sửa `1_Shared/Domain.cs` — UserRole enum đã đủ
  - KHÔNG sửa `Directory.Packages.props` trong task này — đó là W0-T1's job (package setup phải xong trước)
  - KHÔNG sửa bất kỳ Controller nào trong Wave này
  - KHÔNG sửa `VanAnDbContext.cs`
  - KHÔNG sửa `Login.cshtml.cs` trong task này (đó là W0-T3)

> ⚠️ **DEPENDENCY:** W0-T1 (PackageSetup) phải complete trước — `JwtBearer` và `BCrypt.Net-Next` phải có trong `Directory.Packages.props`

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **Secret key strength:** `Jwt:Secret` tối thiểu 32 ký tự (256-bit) — validate trong constructor, throw nếu ngắn hơn
- [ ] **Token expiry:** Hard-coded `8 hours` cho `AccessToken`, `7 days` cho `RefreshToken` nếu cần sau này
- [ ] **Claims chuẩn OIDC:** Dùng `ClaimTypes.Role` (`http://schemas.microsoft.com/ws/2008/06/identity/claims/role`) cho role claim — tương thích với `RequireRole()` policy
- [ ] **tenant_id claim:** Phải là lowercase snake_case (`tenant_id`) — match với `HttpContextTenantProvider.cs` line 26
- [ ] **Algorithm:** HS256 — symmetric key, đủ cho monolith. Không dùng RS256 (cần KeyPair phức tạp hơn)

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC-1:** `IJwtTokenService.GenerateToken(userId, email, role, tenantId)` → trả về JWT string không null/empty
- [ ] **SC-2:** Token decode bằng jwt.io → thấy đúng claims: `sub`, `email`, `role`, `tenant_id`, `exp`
- [ ] **SC-3:** Token hết hạn sau đúng 8 tiếng (verify `exp` claim)
- [ ] **SC-4:** Token bị tamper (sửa payload) → `JwtSecurityTokenHandler.ValidateToken()` throws `SecurityTokenException`
- [ ] **SC-5:** Token với secret sai → validation fails
- [ ] **SC-6:** DI registration trong `3_CoreHub/Program.cs` → `IJwtTokenService` resolvable
- [ ] **SC-7:** `appsettings.Development.json` có section `Jwt` với placeholder values
- [ ] **SC-8:** Unit test `JwtTokenServiceTests` — minimum 5 test cases tất cả PASS

**Implementation Date:** 2026-06-23
**Branch:** `feature/wave0-jwt-auth`

## 6. ACTIVE SKILLS (MAX 3)
- `build-error-analysis` — phân tích nếu có compile errors với JwtBearer packages
- `domain-integrity-validation` — verify không vi phạm domain purity khi thêm service
- `test-system-upgrade` — đảm bảo test project có đủ packages để test JWT

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 7
- **Verified Facts:**
  - Fact 1: `Directory.Packages.props` không có `JwtBearer` hay `BCrypt` packages (W0-T1 sẽ add)
  - Fact 2: `UserRole` enum tồn tại tại `1_Shared/Domain.cs` line 399: `None, Owner, StoreKeeper, Guard, Staff, Masterchef`
  - Fact 3: `HttpContextTenantProvider.cs` đọc claim `tenant_id` (snake_case) tại line 26
  - Fact 4: `3_CoreHub/Program.cs` không có `AddAuthentication` hay JWT setup
  - Fact 5: `appsettings.json` trong CoreHub không có `Jwt` section
  - Fact 6: `Microsoft.Extensions.DependencyInjection` 9.0.3 đã có trong packages
  - Fact 7: `DevLoginController.cs` issue Cookie với claims `tenant_id` (snake_case) + `TenantId` (PascalCase) — dual claim support. JWT phải match format này.
- **Assumptions:**
  - `System.IdentityModel.Tokens.Jwt` 7.x compatible với .NET 8 (likely true — standard Microsoft package)
  - Secret key sẽ được user cung cấp qua `.env` hoặc `appsettings.Development.json`
- **Open Questions:**
  - Q1: JWT secret lưu ở đâu trên production — file system hay environment variable? (Recommend: env var `VANAN_JWT_SECRET`)
  - Q2: Cần `RefreshToken` flow không? (Recommend: không trong Wave 0 — thêm sau nếu cần)
- **Recommended Action:** IMPLEMENT — Assumptions < Verified Facts, Open Questions < 3

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| `3_CoreHub/Program.cs` | Thêm DI registration — không xóa gì | Đọc lại file trước khi sửa, append-only |
| `Directory.Packages.props` | Tất cả projects trong solution thấy packages mới | Chỉ thêm vào `<ItemGroup>`, không sửa version existing |
| `appsettings.json` (CoreHub) | Dev environment cần cập nhật | Provide sensible development defaults |
| `3_CoreHub/VanAn.CoreHub.csproj` | Thêm `<PackageReference>` | Verify build sau khi thêm |

## 9. TDD & E2E TESTING STRATEGY
- **Unit Tests (viết trước implementation):**
  - `6_Tests/VanAn.Core.Tests/Services/JwtTokenServiceTests.cs`
  - Test cases: valid token generation, expired token, tampered signature, wrong secret, correct claims extraction
  - Framework: xUnit + FluentAssertions (đã có trong solution)
- **Integration Tests:**
  - Không cần integration test riêng cho task này
  - Integration verified qua W0-T4 (Gateway JWT validation)
- **E2E Tests:**
  - Không trực tiếp — covered bởi W0-T7

- **Test boundary:**
  - Unit tests: `JwtTokenService` in isolation với `IConfiguration` mock
  - Integration tests: không applicable cho task này
  - E2E tests: không applicable cho task này

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Chiến lược thực thi
Viết failing tests trước (TDD), sau đó implement service để tests pass.

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | Xác nhận package versions compatible với .NET 8, design `IJwtTokenService` interface | Thêm packages vào `Directory.Packages.props` + `.csproj`. Tạo `IJwtTokenService.cs` interface. Viết 5 unit tests (failing). |
| **S2** | Review failing tests, plan `JwtTokenService` implementation | Implement `JwtTokenService.cs`. Register DI. Thêm `Jwt` section vào appsettings. Run tests → all green. |

### Rules
- Mỗi session kết thúc bằng `dotnet build VanAn.sln` → 0 errors
- Không commit nếu tests còn failing
- Secret không được hardcode trong source code — luôn đọc từ `IConfiguration`

## 11. ESTIMATED EFFORT
- 2 sessions (S1: interface + failing tests, S2: implementation + green tests)
- **DEPENDENCY:** W0-T1 phải complete — packages cần available trước khi viết service
- **BLOCKER:** User cần cung cấp JWT secret value cho Development env (minimum 32 chars). Gợi ý: `VanAn-Dev-Secret-Key-2026-@#$%^&*()`
- **NOTE (Wave 5/6 awareness):** `GenerateToken` signature sẽ cần thêm claim `SystemAdmin` khi Wave 5 implement Tenant management. Thiết kế interface với `IEnumerable<Claim> additionalClaims = null` để extensible.
