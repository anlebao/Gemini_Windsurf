# TASK CARD: SECURITY - Wave 0 - Login Migration (BCrypt + JWT Issue)

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Thay thế plain-text password comparison (`Password == "VanAn@2026"`) bằng BCrypt hash verify, và issue JWT token sau khi verify thành công — thay vì chỉ tạo Cookie session.
- **Nghiệp vụ áp dụng:** Authentication entry point của ShopERP. Mọi user (Owner, StoreKeeper, Guard, Staff) đều đi qua `Login.cshtml.cs`. Sau task này, Cookie sẽ chứa JWT token (Cookie vẫn giữ để Blazor UI hoạt động, JWT bên trong để API calls authenticate).

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `.devin/workflows/newfeaturebuild.md`
- **Execution Mode:** IMPLEMENT

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md`
  - `5_WebApps/ShopERP/Pages/Login.cshtml.cs` (SỬA CHÍNH)
  - `5_WebApps/ShopERP/Program.cs` (thêm BCrypt + JwtTokenService DI)
  - `5_WebApps/ShopERP/VanAn.ShopERP.csproj` (thêm BCrypt.Net-Next)
  - `3_CoreHub/Services/IJwtTokenService.cs` (đọc — interface từ W0-T2)
  - `1_Shared/Domain.cs` (đọc — DemoUser entity, UserRole enum)

- **Boundary Rules (Nghiêm cấm):**
  - KHÔNG sửa `VanAnDbContext.cs` hay bất kỳ EF configuration nào
  - KHÔNG sửa `DemoUser` domain entity trong `Domain.cs`
  - KHÔNG xóa `DevLoginController.cs` — giữ nguyên cho E2E tests
  - KHÔNG thay đổi URL routes của login page

## 4. TECHNICAL & REGULATORY CONSTRAINTS
- [ ] **BCrypt work factor:** Minimum `12` (balance giữa security và performance trên server VN). `BCrypt.Verify(password, hash)` phải được gọi — không tự implement hashing
- [ ] **Timing attack resistance:** BCrypt tự xử lý constant-time comparison — KHÔNG dùng `string.Compare` trực tiếp
- [ ] **Seed hashed passwords:** `DemoUser` records trong DB seed phải có BCrypt hash thực. `BCrypt.HashPassword("VanAn@2026", 12)` trong Program.cs seed
- [ ] **Backward compatibility:** `DevLoginController.cs` giữ nguyên — vẫn issue Cookie không đổi (dành cho E2E dev)
- [ ] **JWT trong Cookie:** Set Cookie name `.VanAn.Jwt` với value = JWT string. Cookie HttpOnly + SameSite=Strict. Lý do: Blazor server-side cần Cookie, API calls dùng Bearer từ JWT bên trong

## 5. SUCCESS CRITERIA
- [ ] **SC-1:** Login với `admin@vanan.vn` / `VanAn@2026` → 302 redirect → `/Index` (không lỗi 500)
- [ ] **SC-2:** Response set-cookie chứa JWT token (decode được bằng jwt.io)
- [ ] **SC-3:** JWT claims có đúng: `role=Owner`, `tenant_id=<guid>`, `email=admin@vanan.vn`
- [ ] **SC-4:** Login với password sai → stay on Login page + error message "Email hoặc password không đúng"
- [ ] **SC-5:** Login với `baove@vanan.vn` / `VanAn@2026` → JWT role = `Guard` → redirect `/Guard/Scan`
- [ ] **SC-6:** `dotnet build VanAn.sln` → 0 errors sau khi sửa
- [ ] **SC-7:** Existing E2E tests không bị break (`DevLoginController` vẫn hoạt động)
- [ ] **SC-8:** DB seed: `DemoUser.PasswordHash` chứa BCrypt hash, không phải plain text

**Implementation Date:** 2026-06-23
**Branch:** `feature/wave0-jwt-auth`

## 6. ACTIVE SKILLS (MAX 3)
- `build-error-analysis` — nếu BCrypt.Net-Next có conflict với existing packages
- `pattern-based-fixing` — login flow là standard pattern
- `domain-integrity-validation` — verify không sửa Domain.cs

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Verified Facts:**
  - Fact 1: `Login.cshtml.cs` line 42: `Password == "VanAn@2026"` — plain text confirm (đã đọc file)
  - Fact 2: `5_WebApps/ShopERP/Program.cs` lines 161-169: policies `OwnerOnly`, `StoreManagement`, `GuardOnly`, `StaffOrAbove` đã có. CHƯA CÓ `SystemAdmin`.
  - Fact 3: `DemoUser` entity tại `Domain.cs` line 930: `Username, PasswordHash, DisplayName, Role (UserRole), IsActive`
  - Fact 4: `DevLoginController.cs` line 12: "SECURITY: This controller ONLY registers in Development" — giữ nguyên
  - Fact 5: `DevLoginController.cs` issue claims: `tenant_id` (snake_case) + `TenantId` (PascalCase) dual — JWT phải issue cùng format
  - Fact 6: `IJwtTokenService` sẽ exist sau W0-T2 completes
- **Assumptions:**
  - BCrypt.Net-Next 4.0.x compatible với .NET 8 (standard library — high confidence)
  - ShopERP project có reference tới CoreHub (nơi `JwtTokenService` sẽ live)
- **Open Questions:**
  - Q1: Seed passwords: giữ nguyên `VanAn@2026` cho tất cả roles trong dev, hay mỗi role password khác nhau?
- **Recommended Action:** IMPLEMENT — safe, well-scoped

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| `Login.cshtml.cs` | E2E tests dùng login flow → có thể break | Verify `DevLoginController` path vẫn work; run auth E2E tests sau khi sửa |
| `ShopERP/Program.cs` | DI registration thêm BCrypt service | Append-only, không xóa gì |
| DB seed (Program.cs) | Existing dev DB có plain-text hash → mismatch | Drop dev DB + recreate khi test (EnsureCreated đã có) |

## 9. TDD & E2E TESTING STRATEGY
- **Unit Tests:**
  - `6_Tests/VanAn.ShopERP.Tests/LoginPasswordTests.cs`
  - Test: BCrypt hash verify với đúng password → true
  - Test: BCrypt hash verify với sai password → false
  - Test: Login flow với mock DbContext — verify redirect URL theo role
- **Integration Tests:** Không cần — covered bởi E2E
- **E2E Tests:** Chạy lại existing `order-flow.spec.ts` để confirm login vẫn work

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

| Session | JIT Planning | Pure Execution |
|---|---|---|
| **S1** | Verify BCrypt.Net-Next API, plan seed data changes | Thêm BCrypt package. Viết failing tests. Update Program.cs seed với `BCrypt.HashPassword()`. |
| **S2** | Review test failures, plan Login.cshtml.cs changes | Sửa `Login.cshtml.cs`: thay switch/password compare bằng DB lookup + `BCrypt.Verify()`. Issue JWT sau verify. Run tests → green. |

## 11. ESTIMATED EFFORT
- 2 sessions
- **DEPENDENCY:** Phải complete W0-T2 (JwtTokenService) trước session S2
- **RISK:** DB seed update sẽ invalidate existing dev database — cần `dotnet ef database drop` trên dev env hoặc delete SQLite file
