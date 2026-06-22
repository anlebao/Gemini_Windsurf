# TASK CARD: TESTING - WAVE 0 - E2E Playwright Update (JWT Token in DevLogin)

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Cập nhật `DevLoginController` để response của `POST /dev/login` bao gồm thêm field `token: string` chứa JWT. Cập nhật E2E Playwright tests nếu cần để hỗ trợ Bearer token flow — trong khi đó Cookie flow hiện tại vẫn hoạt động đầy đủ.
- **Nghiệp vụ áp dụng:** E2E tests cần có cách lấy JWT token hợp lệ (Development env only). `DevLoginController` là cổng duy nhất cho E2E authentication — việc thêm `token` field cho phép Playwright tests test cả Bearer token flow (ví dụ API calls qua Gateway) không chỉ Cookie-based UI flow.

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `.devin/workflows/newfeaturebuild.md`
- **Execution Mode:** IMPLEMENT

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
  - `5_WebApps/ShopERP/Controllers/DevLoginController.cs` (SỬA — thêm `token` field vào response)
  - `6_Testing/e2e-tests/` (CẬP NHẬT existing specs nếu có spec nào assert response shape của `/dev/login`)
- **Boundary Rules (Nghiêm cấm):**
  - KHÔNG xóa bất kỳ existing E2E test nào — chỉ extend
  - KHÔNG xóa hay disable guard `if (!_env.IsDevelopment()) return NotFound()` trong `DevLoginController`
  - KHÔNG thay đổi Cookie issuance logic hiện tại trong `DevLoginController` — chỉ THÊM token generation
  - KHÔNG sửa production authentication flow
  - KHÔNG sửa files ngoài `DevLoginController.cs` và các E2E spec files liên quan
  - Gate 3 Playwright Isolation: KHÔNG chạy Playwright diện rộng trong IMPLEMENT mode — chỉ chạy test spec cụ thể sau khi implement xong

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] `DevLoginController` CHỈ hoạt động trong Development environment — guard `if (!_env.IsDevelopment()) return NotFound()` PHẢI giữ nguyên
- [ ] JWT token trong response phải được generate bởi `JwtTokenService` (W0-T2) — không tự generate thủ công
- [ ] Response object format: `{ success: true, token: "<jwt_string>", message: "Dev login successful" }` (thêm `token` field, không xóa fields cũ)
- [ ] Existing E2E Cookie-based tests KHÔNG được fail sau thay đổi này
- [ ] `dotnet build VanAn.sln` → 0 errors
- [ ] `guard-check.ps1` phải PASS

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC-1:** `POST /dev/login` (Development env) → response JSON có field `token` là string không rỗng, có JWT format (`header.payload.signature`)
- [ ] **SC-2:** Decode JWT từ response — claims có `sub`, `role=Owner`, `tenant_id=11111111-...`, `exp` hợp lệ
- [ ] **SC-3:** Existing E2E Playwright tests tại `6_Testing/e2e-tests/` PASS (chạy `npx playwright test` hoặc command tương đương — chỉ chạy sau khi implement xong)
- [ ] **SC-4:** `DevLoginController` trả về 404 khi chạy ngoài Development environment (guard vẫn work)
- [ ] **SC-5:** `dotnet build VanAn.sln` → 0 errors
- [ ] **SC-6:** `guard-check.ps1` exits 0

**Implementation Date:** 2026-06-23
**Branch:** `feature/wave0-jwt-auth`

## 6. ACTIVE SKILLS (MAX 3)
- `playwright_cost_optimizer` — chạy Playwright chỉ sau implement xong, chỉ specs liên quan đến login flow
- `playwright_guard` — isolate Playwright khỏi IMPLEMENT phase, không chạy browser automation trong khi đang code
- `system-refactor-safety` — xác nhận existing E2E tests không bị break bởi response shape change

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Verified Facts:**
  - `DevLoginController.cs` tại `5_WebApps/ShopERP/Controllers/DevLoginController.cs`
  - `DevLoginController` chỉ trong Development env — guard đã có, phải giữ nguyên
  - `DevLoginController` hiện issue Cookie với fixed TenantId `11111111-...`, role=Owner
  - E2E tests tại `6_Testing/e2e-tests/`
  - `JwtTokenService` sẽ có sẵn sau W0-T2 (prerequisite)
  - Gate 3 Playwright Isolation: Playwright DISABLED during IMPLEMENT mode
  - Gate 4: UI layout change → BẮT BUỘC viết E2E test — task này không thay đổi UI layout, chỉ API response
- **Assumptions:**
  - `DevLoginController` hiện trả về một object JSON có `success`, `message` fields (hoặc redirect — cần verify)
  - E2E tests hiện tại chủ yếu dùng Cookie flow (navigate đến trang → assert page content)
  - `JwtTokenService` có thể được injected vào `DevLoginController` qua constructor DI
- **Open Questions:**
  - Response format hiện tại của `DevLoginController` là gì (JSON object hay redirect)? → Cần đọc controller để biết
- **Recommended Action:** IMPLEMENT (cần W0-T2 trước)

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| `DevLoginController.cs` | E2E tests đang assert exact response shape có thể fail nếu shape thay đổi | Chỉ THÊM `token` field vào response, không xóa/rename fields cũ. Check E2E specs trước. |
| `DevLoginController.cs` | `JwtTokenService` DI injection — cần `JwtTokenService` đã registered trong DI container | Verify `JwtTokenService` được registered trong `Program.cs` (W0-T2's job) trước khi inject vào controller |
| E2E spec files (nếu cần update) | Cập nhật fixture/helper để extract `token` từ response | Tạo helper function `getDevToken()` trong E2E test utilities — không modify test logic, chỉ thêm utility |

## 9. TDD & E2E TESTING STRATEGY
- **Unit Tests:** Không viết unit test riêng cho controller trong task này (controller logic đơn giản — chỉ thêm token field).
- **Integration Tests:** Manual test `POST /dev/login` với curl/Postman trong Development mode để verify response có `token` field.
- **E2E Tests (Playwright — chỉ chạy SAU implement):**
  - Verify existing login specs vẫn PASS (`auth.spec.ts` hoặc tương đương)
  - Nếu có spec nào assert response của `/dev/login` — update để accept `token` field mới
  - Optionally thêm 1 spec mới: `api-bearer-auth.spec.ts` — dùng `token` từ `/dev/login` để gọi API endpoint với `Authorization: Bearer` header → assert 200
  - Playwright command: `npx playwright test --project=chromium auth.spec.ts` (single spec, single browser)

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)
| Session | JIT Planning | Pure Execution |
|---|---|---|
| Session 1 (duy nhất) | (1) Đọc `DevLoginController.cs` để biết current response format và constructor. (2) Scan `6_Testing/e2e-tests/` để tìm tests liên quan đến `/dev/login` response shape. | (1) Inject `IJwtTokenService` vào `DevLoginController` constructor. (2) Sau khi issue Cookie, generate JWT: `var token = _jwtService.GenerateToken(userId, role, tenantId)`. (3) Thêm `token` field vào response object. (4) `dotnet build VanAn.sln`. (5) Kiểm tra E2E specs — cập nhật nếu cần (chỉ extend, không xóa). (6) `guard-check.ps1`. (7) SAU KHI BUILD PASS: `npx playwright test --project=chromium auth.spec.ts` (Playwright chỉ chạy ở bước cuối). |

## 11. ESTIMATED EFFORT
- **1 session** (~25 phút)
- **DEPENDENCY:** W0-T2 (JwtTokenService phải implement xong và registered trong DI).
- **BLOCKS:** Không block task nào. Đây là task cuối cùng của Wave 0 — sau khi PASS, có thể merge `feature/wave0-jwt-auth` vào `main`.
