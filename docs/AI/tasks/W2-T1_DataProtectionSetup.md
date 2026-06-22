# TASK CARD: INFRASTRUCTURE - WAVE 2 - Data Protection Setup

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Cập nhật `AddDataProtection()` trong cả `3_CoreHub/Program.cs` và `5_WebApps/ShopERP/Program.cs` — persist encryption keys ra filesystem và thiết lập `ApplicationName` đồng nhất (`"VanAnEcosystem"`) để cross-service encryption/decryption hoạt động. Đảm bảo `./keys/` folder không commit vào git.
- **Nghiệp vụ áp dụng:** Data Protection được dùng để encrypt/decrypt sensitive data (ví dụ: anti-forgery tokens, protected cookies, encrypted user data). Khi `ApplicationName` không đồng nhất giữa `CoreHub` và `ShopERP`, encrypted data từ service này không decrypt được ở service kia — đây là bug tiềm ẩn trong cross-service scenarios. `PersistKeysToFileSystem` đảm bảo keys không bị mất khi app restart.

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `.devin/workflows/newfeaturebuild.md`
- **Execution Mode:** IMPLEMENT

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
  - `3_CoreHub/Program.cs` (cập nhật `AddDataProtection()`)
  - `5_WebApps/ShopERP/Program.cs` (cập nhật `AddDataProtection()`)
  - `3_CoreHub/appsettings.json` (thêm `DataProtection:KeysPath` config key)
  - `3_CoreHub/appsettings.Development.json` (override path cho Development)
  - `.gitignore` ở root solution (thêm `**/keys/` pattern)
- **Boundary Rules (Nghiêm cấm):**
  - KHÔNG thêm package mới — `Microsoft.AspNetCore.DataProtection` 2.3.0 đã có trong `Directory.Packages.props`
  - KHÔNG sửa Domain.cs hay bất kỳ entity nào
  - KHÔNG thay đổi authentication configuration (đó là Wave 0 scope)
  - KHÔNG sửa `2_Gateway/Program.cs` — Gateway là stateless Reverse Proxy, không cần Data Protection keys
  - KHÔNG commit `./keys/` folder hay bất kỳ `.xml` key files nào vào git

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] `SetApplicationName("VanAnEcosystem")` — GIỐNG NHAU ở cả 2 projects (CoreHub và ShopERP) — bất kỳ sự khác biệt nào (kể cả 1 ký tự) sẽ phá cross-service decrypt
- [ ] `PersistKeysToFileSystem(new DirectoryInfo("./keys"))` trong Development environment
- [ ] `./keys/` path cần có trong `.gitignore` — pattern `**/keys/` hoặc tương đương
- [ ] Keys KHÔNG được commit vào git (verify với `git status` sau khi thêm vào .gitignore)
- [ ] Sau app restart, encrypted data từ session trước vẫn decrypt được (keys persist)
- [ ] `dotnet build VanAn.sln` → 0 errors
- [ ] `guard-check.ps1` phải PASS
- [ ] Architecture tests `6_Tests/VanAn.Architecture.Tests` vẫn 7/7 PASS

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC-1:** Sau first run của app, `./keys/` folder tồn tại và chứa ít nhất 1 file XML key
- [ ] **SC-2:** Sau khi restart app (dotnet stop + start lại), protected data từ trước restart vẫn decrypt được (không bị "key not found" exception)
- [ ] **SC-3:** `grep -r "VanAnEcosystem"` trong cả 2 `Program.cs` files → đều tìm thấy string giống nhau (case-sensitive match)
- [ ] **SC-4:** `git status` sau khi chạy app — `./keys/` folder KHÔNG xuất hiện trong untracked files (đã bị .gitignore)
- [ ] **SC-5:** `dotnet build VanAn.sln` → 0 errors
- [ ] **SC-6:** `guard-check.ps1` exits 0
- [ ] **SC-7:** Architecture tests 7/7 PASS

**Implementation Date:** 2026-06-23
**Branch:** `feature/wave2-data-protection`

## 6. ACTIVE SKILLS (MAX 3)
- `system-refactor-safety` — xác nhận thay đổi `AddDataProtection()` không làm hỏng existing Cookie authentication hay anti-forgery tokens
- `domain-integrity-validation` — xác nhận không sửa Domain layer, chỉ sửa infrastructure configuration
- `build-error-analysis` — xử lý nếu `DirectoryInfo` path resolve khác nhau giữa Development và Production environments

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Verified Facts:**
  - `Microsoft.AspNetCore.DataProtection` 2.3.0 đã có trong `Directory.Packages.props` — KHÔNG cần thêm package
  - `5_WebApps/ShopERP/Program.cs` lines 161-169: có policies `OwnerOnly`, `StoreManagement`, `GuardOnly`, `StaffOrAbove` — không sửa policies
  - Gateway MUST remain pure stateless Reverse Proxy — không thêm Data Protection vào Gateway
  - `3_CoreHub` là pure Class Library — nếu CoreHub không có `Program.cs` (vì là Class Library), thì DI setup của CoreHub's Data Protection cần được thực hiện trong `5_WebApps/ShopERP/Program.cs` thông qua service registration extension method
  - Architecture tests: 7/7 phải PASS
  - `guard-check.ps1` phải PASS sau mỗi wave
- **Assumptions:**
  - `./keys/` path được resolve relative to working directory khi app runs (thường là project root trong Development)
  - `appsettings.Development.json` override cho Development-specific path (có thể dùng absolute path cho Docker/production)
  - Hiện tại `AddDataProtection()` có thể đã được gọi mà không có `PersistKeysToFileSystem` (dùng in-memory keys mặc định)
- **Open Questions:**
  - `3_CoreHub` có `Program.cs` không? Nếu là pure Class Library thì Data Protection config của nó được setup ở đâu? → Cần đọc `3_CoreHub/` project structure. Nếu không có `Program.cs`, chỉ cần update `5_WebApps/ShopERP/Program.cs`.
- **Recommended Action:** IMPLEMENT (đọc 2 `Program.cs` files để xác nhận current DataProtection setup trước khi sửa)

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| `5_WebApps/ShopERP/Program.cs` | Thay đổi `ApplicationName` có thể invalidate existing in-memory keys → anti-forgery tokens và cookies hiện tại bị invalid | Chấp nhận được trong Development (user phải login lại). Trong Production, cần key rotation plan. |
| `3_CoreHub/Program.cs` (nếu có) | Tương tự — key change invalidates existing protected payloads | Document rõ: first deploy cần user logout/login lại |
| `.gitignore` | Thêm `**/keys/` — nếu ai đó đã commit keys trước đó, chúng sẽ vẫn tracked | Kiểm tra `git ls-files keys/` — nếu đã tracked thì `git rm -r --cached keys/` |
| `3_CoreHub/appsettings.json` | Thêm `DataProtection` section — các apps khác đọc file này sẽ thấy config mới | Config chỉ ảnh hưởng khi code gọi `.PersistKeysToFileSystem()` — không break anything tự động |

## 9. TDD & E2E TESTING STRATEGY
- **Unit Tests:** Data Protection configuration không có unit tests riêng. Validation qua integration test và manual verification.
- **Integration Tests:**
  - Test key persistence: (1) Encrypt string. (2) Restart app process. (3) Decrypt string → must succeed.
  - Test cross-service: nếu có integration test environment chạy cả CoreHub và ShopERP, verify một service encrypt được service kia decrypt.
- **E2E Tests:** Không trực tiếp test Data Protection trong E2E. Nhưng Cookie authentication (phụ thuộc Data Protection) được test gián tiếp qua login E2E tests. Nếu Data Protection keys thay đổi → login phải thành công sau khi keys được persist.

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)
| Session | JIT Planning | Pure Execution |
|---|---|---|
| Session 1 (duy nhất) | (1) Đọc `5_WebApps/ShopERP/Program.cs` để tìm existing `AddDataProtection()` call (nếu có) và xác nhận vị trí thêm config. (2) Đọc `3_CoreHub/Program.cs` (hoặc service extension) để làm tương tự. (3) Kiểm tra `.gitignore` hiện tại có `keys` pattern chưa. | (1) Cập nhật `AddDataProtection()` trong `5_WebApps/ShopERP/Program.cs`: `.SetApplicationName("VanAnEcosystem").PersistKeysToFileSystem(new DirectoryInfo(builder.Configuration["DataProtection:KeysPath"] ?? "./keys"))`. (2) Làm tương tự cho `3_CoreHub` nếu có Program.cs/service extension. (3) Thêm `"DataProtection": { "KeysPath": "./keys" }` vào `appsettings.json`. (4) Thêm `**/keys/` vào `.gitignore`. (5) `dotnet build VanAn.sln`. (6) `dotnet run` để kiểm tra `./keys/` folder tạo ra. (7) Verify `git status` không show keys folder. (8) `guard-check.ps1`. |

## 11. ESTIMATED EFFORT
- **1 session** (~20 phút)
- **DEPENDENCY:** Không có dependency từ Wave 0 hay Wave 1 (independent infrastructure task). Tuy nhiên nên làm sau Wave 0 merge để tránh conflict trên `5_WebApps/ShopERP/Program.cs`.
- **BLOCKS:** W2-T2 và các tasks Wave 2 khác cần Data Protection keys ổn định trước khi implement encrypted storage features.
- **NOTE:** Nếu `3_CoreHub` không có `Program.cs` (pure Class Library không có host), chỉ cần update `5_WebApps/ShopERP/Program.cs` — 1 file duy nhất. Đây là trường hợp có thể xảy ra và phải xử lý trong JIT Planning phase.
