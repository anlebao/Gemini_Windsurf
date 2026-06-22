# TASK CARD: SECURITY - WAVE 0 - Seed BCrypt Password Hashes

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Cập nhật seed data trong `5_WebApps/ShopERP/Program.cs` — thay thế password hardcode (plain text hoặc simple hash) bằng BCrypt hash thực tính toán tại runtime với work factor 12. `DemoUser.PasswordHash` sẽ chứa BCrypt hash của `VanAn@2026`.
- **Nghiệp vụ áp dụng:** Đảm bảo development database có users với password được hash đúng chuẩn bảo mật, để `BCrypt.Verify("VanAn@2026", user.PasswordHash)` trả về `true` khi login. Sau task này, toàn bộ flow login → BCrypt.Verify sẽ hoạt động end-to-end.

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `.devin/workflows/newfeaturebuild.md`
- **Execution Mode:** IMPLEMENT

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
  - `5_WebApps/ShopERP/Program.cs` (chỉ phần seed data — thường trong block `if (app.Environment.IsDevelopment())` hoặc `SeedData()` method)
- **Boundary Rules (Nghiêm cấm):**
  - KHÔNG sửa `DemoUser` entity definition trong `1_Shared/Domain.cs` (Hard Stop: Domain layer protection)
  - KHÔNG sửa login controller/service logic trong task này (đó là W0-T3's job)
  - KHÔNG hardcode BCrypt hash string trực tiếp (ví dụ: `PasswordHash = "$2a$12$abc..."`) — phải compute bằng `BCrypt.HashPassword("VanAn@2026", 12)` trong code
  - KHÔNG sửa production appsettings hay production database
  - KHÔNG sửa bất kỳ file nào khác ngoài `Program.cs`

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] Work factor = 12 (không dùng default 10, không dùng cao hơn 14 vì seed data performance)
- [ ] Hash được COMPUTE trong code: `BCrypt.HashPassword("VanAn@2026", 12)` — không paste hash string cứng
- [ ] Dev DB phải drop + recreate sau khi thay đổi (nếu dùng EF Core migrations/seed: `dotnet ef database drop --force` rồi `dotnet run` để recreate)
- [ ] Chỉ áp dụng cho Development environment (seed data không chạy trong Production)
- [ ] `BCrypt.Net-Next` package phải đã có (prerequisite: W0-T1)
- [ ] `dotnet build VanAn.sln` → 0 errors
- [ ] `guard-check.ps1` phải PASS

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC-1:** Sau khi chạy app, query DB: `DemoUser` records có `PasswordHash` bắt đầu bằng `$2a$12$` (BCrypt work factor 12 prefix)
- [ ] **SC-2:** `BCrypt.Verify("VanAn@2026", user.PasswordHash)` trả về `true` cho tất cả DemoUser records được seed
- [ ] **SC-3:** Login flow với username/password `VanAn@2026` hoạt động thành công (trả về 200 + Cookie/Token)
- [ ] **SC-4:** Login với password sai (ví dụ `WrongPass`) trả về 401
- [ ] **SC-5:** `dotnet build VanAn.sln` → 0 errors
- [ ] **SC-6:** `guard-check.ps1` exits 0

**Implementation Date:** 2026-06-23
**Branch:** `feature/wave0-jwt-auth`

## 6. ACTIVE SKILLS (MAX 3)
- `domain-integrity-validation` — xác nhận không sửa `DemoUser` entity trong Domain layer
- `build-error-analysis` — xử lý nếu BCrypt namespace chưa được import đúng
- `system-refactor-safety` — backup DB trước khi drop, confirm seed data structure đúng với entity

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Verified Facts:**
  - `DemoUser` entity có các fields: `Username`, `PasswordHash`, `DisplayName`, `Role (UserRole)`, `IsActive`
  - `UserRole` enum: `None, Owner, StoreKeeper, Guard, Staff, Masterchef`
  - `BCrypt.Net-Next` sẽ có sẵn sau W0-T1 (prerequisite)
  - Seed data nằm trong `5_WebApps/ShopERP/Program.cs`
  - `DevLoginController.cs` issue Cookie với fixed TenantId `11111111-...`, role=Owner — seed data phải có user với role Owner
  - `DevLoginController.cs` chỉ hoạt động trong Development environment (đã có guard)
  - W0-T3 (BCrypt.Verify trong login service) là prerequisite để verify password work
- **Assumptions:**
  - Seed data hiện tại dùng plain text password hoặc simple hash (không phải BCrypt format `$2a$`)
  - Có ít nhất 1 DemoUser với role `Owner` trong seed data (để DevLoginController có thể login)
  - Seed data chạy trong `if (app.Environment.IsDevelopment())` block
- **Open Questions:**
  - Seed data hiện tại có bao nhiêu DemoUser records và với roles nào? → Cần đọc `Program.cs` phần seed để biết
- **Recommended Action:** IMPLEMENT (sau khi đọc phần seed data trong `Program.cs`)

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| `5_WebApps/ShopERP/Program.cs` (seed data) | Dev DB cũ sẽ có data không hợp lệ nếu không drop | Bắt buộc drop + recreate DB sau khi thay đổi seed. Document rõ trong task. |
| `5_WebApps/ShopERP/Program.cs` (seed data) | Nếu seed chạy lại trên DB đã có data → duplicate key error | Dùng `AddOrUpdate` pattern hoặc check `if (!context.DemoUsers.Any())` trước khi seed |
| Dev DB file (SQLite) | BCrypt hash dài hơn (~60 chars) so với plain text — DB column `PasswordHash` phải `nvarchar(100)` hoặc lớn hơn | Kiểm tra EF Core migration cho `DemoUser.PasswordHash` column length. BCrypt hash = 60 chars. `nvarchar(100)` là đủ. |

## 9. TDD & E2E TESTING STRATEGY
- **Unit Tests:** W0-T6 (`LoginPasswordTests.cs`) sẽ test `BCrypt.Verify` trực tiếp — không cần unit test riêng cho seed data.
- **Integration Tests:** Manual verification: query DB sau seed, check `PasswordHash` format. Thêm integration test trong `VanAn.Integration.Tests` để verify login flow end-to-end nếu thời gian cho phép.
- **E2E Tests:** W0-T7 Playwright tests — `POST /dev/login` sẽ sử dụng seeded DemoUser. Existing E2E tests phải vẫn PASS sau task này.

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)
| Session | JIT Planning | Pure Execution |
|---|---|---|
| Session 1 (duy nhất) | Đọc `5_WebApps/ShopERP/Program.cs` phần seed data để xác nhận: (1) current password format, (2) danh sách DemoUsers và roles, (3) cách seed được invoke (direct EF, helper method, v.v.) | (1) Thêm `using BCrypt.Net;` vào Program.cs nếu chưa có. (2) Thay thế tất cả `PasswordHash = "..."` bằng `PasswordHash = BCrypt.HashPassword("VanAn@2026", 12)`. (3) Chạy `dotnet build VanAn.sln`. (4) Drop và recreate dev DB: `dotnet ef database drop --force --project 5_WebApps/ShopERP` rồi `dotnet run --project 5_WebApps/ShopERP` để trigger seed. (5) Query DB để verify hash format. (6) `guard-check.ps1`. |

## 11. ESTIMATED EFFORT
- **1 session** (~25 phút, bao gồm DB drop+recreate)
- **DEPENDENCY:** W0-T1 (BCrypt.Net-Next package), W0-T3 (BCrypt.Verify trong login service — để SC-3 test được). Có thể làm song song với W0-T3 nếu chỉ check hash format (SC-1, SC-2) mà chưa cần test full login flow (SC-3).
- **BLOCKS:** W0-T6 (unit test LoginPasswordTests cần seeded data), W0-T7 (E2E tests login)
