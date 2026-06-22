# TASK CARD: INFRASTRUCTURE - WAVE 0 - Gateway JWT Bearer Configuration

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Thêm `AddJwtBearer` vào `2_Gateway/Program.cs` để Gateway có thể validate JWT tokens do ShopERP issue. Gateway chỉ VALIDATE token (không issue). Cấu hình dual-scheme: Cookie cho Blazor UI, JWT Bearer cho API endpoints.
- **Nghiệp vụ áp dụng:** API endpoints của Gateway (ví dụ `GET /api/orders`) yêu cầu Bearer token hợp lệ. Blazor Server UI tiếp tục dùng Cookie authentication như hiện tại. Client apps (mobile, third-party) dùng Bearer token để gọi API qua Gateway.

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `.devin/workflows/newfeaturebuild.md`
- **Execution Mode:** IMPLEMENT

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
  - `2_Gateway/Program.cs` (FILE DUY NHẤT được sửa)
  - `2_Gateway/appsettings.json` (đọc để xác nhận keys `Jwt:Secret`, `Jwt:Issuer`, `Jwt:Audience` tồn tại)
- **Boundary Rules (Nghiêm cấm):**
  - KHÔNG sửa bất kỳ file nào ngoài `2_Gateway/Program.cs`
  - KHÔNG thêm DbContext, business logic, hay service classes vào Gateway (Hard Stop: Gateway phải là pure stateless Reverse Proxy)
  - KHÔNG xóa hay thay thế `AddCookie()` hiện có — chỉ THÊM `AddJwtBearer()`
  - KHÔNG hardcode JWT secret trong `Program.cs` — phải đọc từ `IConfiguration`
  - KHÔNG thêm EF Core hay bất kỳ data access layer nào vào Gateway

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] Gateway MUST remain pure stateless Reverse Proxy — không có DbContext, không có business logic
- [ ] `AddAuthentication()` giữ default scheme = Cookie (để Blazor UI không bị break)
- [ ] JwtBearer scheme thêm như scheme phụ: `.AddJwtBearer(options => { ... })`
- [ ] `TokenValidationParameters` phải validate: `ValidateIssuerSigningKey=true`, `ValidateIssuer=true`, `ValidateAudience=true`, `ValidateLifetime=true`
- [ ] `Jwt:Secret` phải đọc từ `IConfiguration` (không hardcode)
- [ ] `Jwt:Issuer` và `Jwt:Audience` phải khớp với giá trị mà ShopERP dùng khi issue token (cùng config keys)
- [ ] `guard-check.ps1` phải PASS
- [ ] `dotnet build VanAn.sln` → 0 errors

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC-1:** `GET /api/orders` với header `Authorization: Bearer <valid_token>` → HTTP 200
- [ ] **SC-2:** `GET /api/orders` không có Authorization header → HTTP 401
- [ ] **SC-3:** `GET /api/orders` với token bị tamper (signature invalid) → HTTP 401
- [ ] **SC-4:** Cookie-based requests từ Blazor UI vẫn work bình thường (không bị 401)
- [ ] **SC-5:** `dotnet build VanAn.sln` → 0 errors, 0 warnings về auth config
- [ ] **SC-6:** `guard-check.ps1` exits 0

**Implementation Date:** 2026-06-23
**Branch:** `feature/wave0-jwt-auth`

## 6. ACTIVE SKILLS (MAX 3)
- `build-error-analysis` — xử lý compile errors nếu JwtBearer namespace chưa resolve
- `domain-integrity-validation` — xác nhận Gateway không acquire business logic dependencies
- `system-refactor-safety` — backup `Program.cs` state hiện tại trước khi sửa, đảm bảo Cookie scheme không bị break

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Verified Facts:**
  - `2_Gateway/Program.cs` line 44: `AddAuthentication().AddCookie(...)` — hiện chỉ có Cookie scheme, không có AddJwtBearer
  - `Microsoft.AspNetCore.Authentication.JwtBearer` sẽ có sẵn sau W0-T1 (prerequisite)
  - Gateway MUST remain pure stateless Reverse Proxy (Hard Stop từ `.windsurfrules`)
  - ShopERP là nơi issue JWT token (W0-T2) — Gateway chỉ validate
  - Config keys cần: `Jwt:Secret`, `Jwt:Issuer`, `Jwt:Audience` (phải khớp giữa Gateway và ShopERP)
  - `5_WebApps/ShopERP/Program.cs` có các policies: `OwnerOnly`, `StoreManagement`, `GuardOnly`, `StaffOrAbove` — Gateway không cần duplicate các policies này
- **Assumptions:**
  - `appsettings.json` của Gateway đã có hoặc sẽ được thêm section `Jwt` với các keys `Secret`, `Issuer`, `Audience`
  - API endpoints trong Gateway có attribute `[Authorize]` hoặc dùng `RequireAuthorization()` trong route config
- **Open Questions:**
  - Gateway có route config YARP riêng hay dùng minimal API endpoints trực tiếp? → Cần đọc `Program.cs` để xác nhận cách áp dụng `[Authorize]`
- **Recommended Action:** IMPLEMENT (sau khi đọc `Program.cs` để xác nhận current auth setup tại line 44)

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| `2_Gateway/Program.cs` | Blazor UI requests hiện đang dùng Cookie có thể bị fail nếu default scheme thay đổi | Giữ `JwtBearerDefaults.AuthenticationScheme` KHÔNG phải default — Cookie vẫn là default scheme. Dùng `[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]` chỉ trên API routes |
| `2_Gateway/Program.cs` | YARP reverse proxy routes có thể bị require auth ngoài ý muốn | Chỉ thêm `RequireAuthorization()` trên các routes cụ thể cần bảo vệ, không apply globally |
| `2_Gateway/appsettings.json` | Cần thêm `Jwt` section nếu chưa có | Thêm với placeholder values rõ ràng; production values qua environment variables |

## 9. TDD & E2E TESTING STRATEGY
- **Unit Tests:** Gateway config không có unit test riêng — validated qua integration test.
- **Integration Tests:** `6_Tests/VanAn.Integration.Tests/` — thêm test case: (1) valid Bearer → 200, (2) no auth → 401, (3) tampered token → 401, (4) expired token → 401.
- **E2E Tests:** Sau khi W0-T7 hoàn thành, E2E test sẽ dùng `DevLoginController` để lấy token rồi gọi API qua Gateway với Bearer header.

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)
| Session | JIT Planning | Pure Execution |
|---|---|---|
| Session 1 (duy nhất) | Đọc `2_Gateway/Program.cs` từ line 40-60 để xác nhận current auth setup. Đọc `2_Gateway/appsettings.json` để kiểm tra Jwt section. | (1) Thêm `Jwt` section vào `appsettings.json` nếu chưa có. (2) Trong `Program.cs`, sửa `AddAuthentication()` block để thêm `.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options => { options.TokenValidationParameters = new TokenValidationParameters { ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Secret"]!)), ValidateIssuer = true, ValidIssuer = config["Jwt:Issuer"], ValidateAudience = true, ValidAudience = config["Jwt:Audience"], ValidateLifetime = true }; })`. (3) Chạy `dotnet build VanAn.sln`. (4) Test thủ công hoặc integration test. (5) `guard-check.ps1`. |

## 11. ESTIMATED EFFORT
- **1 session** (~20 phút)
- **DEPENDENCY:** W0-T1 (PackageSetup) phải hoàn thành trước — cần `JwtBearer` package. W0-T2 (JwtTokenService trong ShopERP) phải hoàn thành trước để có token để test Gateway validation.
- **BLOCKS:** W0-T6 (integration tests cần Gateway configured đúng)
