# TASK CARD: TESTING - WAVE 0 - JWT Unit Tests

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Viết unit tests đầy đủ cho `JwtTokenService` (minimum 5 test cases) và password verification với BCrypt (minimum 3 test cases). Tạo 2 file test mới trong project `6_Tests/VanAn.Core.Tests/`.
- **Nghiệp vụ áp dụng:** Đảm bảo token generation/validation và password hash/verify hoạt động đúng theo spec bảo mật trước khi deploy. TDD retrofit cho W0-T2 và W0-T3.

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `.devin/workflows/newfeaturebuild.md`
- **Execution Mode:** IMPLEMENT

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
  - `6_Tests/VanAn.Core.Tests/Services/JwtTokenServiceTests.cs` (TẠO MỚI)
  - `6_Tests/VanAn.Core.Tests/Services/LoginPasswordTests.cs` (TẠO MỚI)
  - `6_Tests/VanAn.Core.Tests/VanAn.Core.Tests.csproj` (đọc để xác nhận xUnit + FluentAssertions đã có)
  - `3_CoreHub/Services/JwtTokenService.cs` (đọc để biết interface/implementation — không sửa)
- **Boundary Rules (Nghiêm cấm):**
  - KHÔNG sửa `JwtTokenService.cs` hay bất kỳ source code nào trong `3_CoreHub/` hay `5_WebApps/`
  - KHÔNG mock BCrypt — test phải dùng real `BCrypt.Net.BCrypt.HashPassword()` và `BCrypt.Net.BCrypt.Verify()`
  - KHÔNG thêm dependencies mới vào test project ngoài những gì đã có (xUnit, FluentAssertions, Moq/NSubstitute)
  - KHÔNG tạo file test trong thư mục khác ngoài `6_Tests/VanAn.Core.Tests/Services/`

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] xUnit framework (không dùng NUnit hay MSTest)
- [ ] FluentAssertions cho assertions (không dùng `Assert.Equal` thuần)
- [ ] Mock `IConfiguration` để inject `Jwt:Secret`, `Jwt:Issuer`, `Jwt:Audience` — không dùng real config files
- [ ] BCrypt tests dùng REAL hash (không mock) — work factor thấp hơn (4) cho test speed
- [ ] Minimum 5 test cases cho `JwtTokenServiceTests`
- [ ] Minimum 3 test cases cho `LoginPasswordTests`
- [ ] Tất cả tests PASS: `dotnet test 6_Tests/VanAn.Core.Tests/`
- [ ] `dotnet build VanAn.sln` → 0 errors
- [ ] `guard-check.ps1` phải PASS

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC-1:** `dotnet test 6_Tests/VanAn.Core.Tests/` → tất cả tests PASS (0 failures)
- [ ] **SC-2:** `JwtTokenServiceTests.cs` chứa đủ 5 test cases như đã spec (xem section 9)
- [ ] **SC-3:** `LoginPasswordTests.cs` chứa đủ 3 test cases như đã spec (xem section 9)
- [ ] **SC-4:** Test `TamperedToken_ShouldThrowOrReturnFalse` xác nhận token bị modify không pass validation
- [ ] **SC-5:** Test `WrongSecret_ShouldFailValidation` xác nhận token được sign bằng secret khác không pass
- [ ] **SC-6:** `dotnet build VanAn.sln` → 0 errors
- [ ] **SC-7:** `guard-check.ps1` exits 0

**Implementation Date:** 2026-06-23
**Branch:** `feature/wave0-jwt-auth`

## 6. ACTIVE SKILLS (MAX 3)
- `test-system-upgrade` — tạo test structure đúng convention của project, đảm bảo test discovery hoạt động
- `build-error-analysis` — xử lý nếu test project không tham chiếu đúng source project
- `pattern-based-fixing` — áp dụng Arrange-Act-Assert pattern nhất quán cho tất cả tests

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Verified Facts:**
  - Test project location: `6_Tests/VanAn.Core.Tests/`
  - xUnit và FluentAssertions đã có trong test project
  - `JwtTokenService` được implement trong W0-T2 (prerequisite)
  - BCrypt.Net-Next được thêm trong W0-T1 (prerequisite)
  - `INotificationService` không liên quan đến task này
  - Architecture tests tại `6_Tests/VanAn.Architecture.Tests` — 7/7 phải vẫn PASS
  - Integration tests project: `6_Tests/VanAn.Integration.Tests/`
- **Assumptions:**
  - `JwtTokenService` có method `GenerateToken(userId, role, tenantId)` → trả về JWT string
  - `JwtTokenService` có method `ValidateToken(token)` → trả về `ClaimsPrincipal` hoặc throw exception nếu invalid
  - Test project có ProjectReference đến `VanAn.CoreHub` (nơi `JwtTokenService` sống)
  - `BCrypt.Net-Next` package đã được thêm vào test project (hoặc via CoreHub reference)
- **Open Questions:**
  - `JwtTokenService` interface/API chính xác là gì? Tên methods và signature cụ thể? → Cần đọc `JwtTokenService.cs` từ W0-T2
- **Recommended Action:** IMPLEMENT (cần W0-T2 hoàn thành trước để biết API)

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| `JwtTokenServiceTests.cs` (mới) | Không có reverse impact trực tiếp — test file chỉ đọc source, không modify | N/A |
| `LoginPasswordTests.cs` (mới) | Không có reverse impact — pure unit tests | N/A |
| `VanAn.Core.Tests.csproj` | Nếu cần thêm ProjectReference hay PackageReference mới | Kiểm tra trước khi sửa csproj — chỉ thêm nếu thực sự cần thiết |

## 9. TDD & E2E TESTING STRATEGY
- **Unit Tests — JwtTokenServiceTests (minimum 5 cases):**
  1. `GenerateToken_ValidInput_ShouldReturnNonEmptyJwtString` — token được generate không null/empty, có format 3 phần `header.payload.signature`
  2. `GenerateToken_ShouldContainRequiredClaims` — decode token, verify claims: `sub` (userId), `role`, `tenant_id`, `exp` đều có mặt
  3. `ValidateToken_ExpiredToken_ShouldThrowOrReturnNull` — generate token với expiry -1 phút, validate → exception hoặc null
  4. `ValidateToken_TamperedSignature_ShouldFail` — lấy valid token, sửa 1 char ở phần signature, validate → exception
  5. `ValidateToken_WrongSecret_ShouldFail` — generate token với secret khác, validate bằng `JwtTokenService` với secret gốc → fail

- **Unit Tests — LoginPasswordTests (minimum 3 cases):**
  1. `BCryptVerify_CorrectPassword_ShouldReturnTrue` — hash `"VanAn@2026"` với work factor 4, verify với `"VanAn@2026"` → `true`
  2. `BCryptVerify_WrongPassword_ShouldReturnFalse` — hash `"VanAn@2026"`, verify với `"WrongPassword"` → `false`
  3. `BCryptHash_ShouldProduceValidHashFormat` — `BCrypt.HashPassword("VanAn@2026", 4)` → result starts with `$2a$04$`

- **Integration Tests:** Không trong scope của task này — xem W0-T4 integration tests.
- **E2E Tests:** Không trong scope — xem W0-T7.

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)
| Session | JIT Planning | Pure Execution |
|---|---|---|
| Session 1 (duy nhất) | (1) Đọc `6_Tests/VanAn.Core.Tests/VanAn.Core.Tests.csproj` để xác nhận packages và ProjectReferences. (2) Đọc `3_CoreHub/Services/JwtTokenService.cs` để biết exact API (method names, return types, constructor params). | (1) Tạo thư mục `6_Tests/VanAn.Core.Tests/Services/` nếu chưa có. (2) Viết `JwtTokenServiceTests.cs` với 5 test cases — mock `IConfiguration` với test values (`Jwt:Secret="test-secret-256-bit-key-for-testing"`, `Jwt:Issuer="VanAnTest"`, `Jwt:Audience="VanAnApiTest"`). (3) Viết `LoginPasswordTests.cs` với 3 test cases — BCrypt work factor 4 cho speed. (4) `dotnet build VanAn.sln`. (5) `dotnet test 6_Tests/VanAn.Core.Tests/`. (6) Fix failures nếu có. (7) `guard-check.ps1`. |

## 11. ESTIMATED EFFORT
- **1 session** (~30 phút)
- **DEPENDENCY:** W0-T2 (JwtTokenService phải được implement), W0-T3 (BCrypt login verify phải có). W0-T1 (BCrypt.Net-Next package).
- **BLOCKS:** Không block task nào khác trong Wave 0. Nhưng là yêu cầu quality gate trước khi merge `feature/wave0-jwt-auth` vào `main`.
