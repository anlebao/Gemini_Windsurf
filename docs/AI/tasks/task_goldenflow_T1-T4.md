# TASK CARD: CI FIX - GoldenFlow Tests T1–T4 Activation

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Activate 4 skipped GoldenFlow tests để chạy thực sự và verify DB + EF Core + multi-tenancy smoke coverage
- **Nghiệp vụ áp dụng:** Integration test infrastructure — database connection, entity insert, multi-tenant isolation, app startup health

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `.devin/workflows/Fix_Tests.md`
- **Execution Mode:** FIX_ONLY_TESTS

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
  - `6_Tests/VanAn.Integration.Tests/GoldenFlowSystemTests.cs`
  - `6_Tests/VanAn.Integration.Tests/Infrastructure/CustomWebApplicationFactory.cs`
  - `6_Tests/VanAn.Integration.Tests/Infrastructure/TestEntityBuilder.cs`
  - `3_CoreHub/Infrastructure/Configurations/OrderConfiguration.cs`
  - `5_WebApps/ShopERP/Program.cs` (seed query fix — đã apply một phần)
- **Boundary Rules (Nghiêm cấm):**
  - KHÔNG sửa Domain.cs để fix test
  - KHÔNG thêm logic nghiệp vụ mới vào production services
  - KHÔNG sửa EF Core configurations ngoài scope Order/Customer/DemoUser
  - KHÔNG activate Test 5 (T5 có task card riêng)

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **EF Value Object Translation:** `TenantId` là value object — KHÔNG dùng `c.TenantId == tenantId` trong LINQ; phải dùng pattern client-side hoặc raw Guid
- [ ] **SQLite In-Memory:** Mỗi test dùng `DataSource=:memory:` với connection riêng — phải `OpenConnection()` trước `EnsureCreated()`
- [ ] **Domain Integrity:** Không duplicate domain entities trong test project
- [ ] **Guard Check:** `guard-check.ps1` phải PASS sau fix
- [ ] **Build:** `dotnet build VanAn.sln` 0 errors trước khi commit

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **T1 Pass:** `GoldenFlow_DatabaseConnection_IsHealthy` — `_dbContext.Orders.CountAsync()` chạy không throw, count >= 0
- [ ] **T2 Pass:** `GoldenFlow_HealthCheck_ReturnsHealthy` — `_factory.CreateClient()` boots ShopERP thành công, `GET /health` trả response không null
- [ ] **T3 Pass:** `GoldenFlow_SimpleEntityInsert_WithBehavior_Works` — Insert 1 Order, query lại được bằng `Id`, các audit fields đúng
- [ ] **T4 Pass:** `GoldenFlow_MultiTenant_WithBusinessRules_Isolation_Works` — 2 tenants, mỗi tenant chỉ thấy Order của mình
- [ ] **144/144:** Toàn bộ integration test suite pass sau khi activate T1–T4
- [ ] **Build 0 errors:** `dotnet build` sạch
- [ ] **Guard check PASS:** `guard-check.ps1` không có violations

**Implementation Date:** TBD
**Branch:** `main` (hoặc `fix/goldenflow-tests-t1-t4`)

## 6. ACTIVE SKILLS (MAX 3)
- `build-error-analysis` — Phân tích EF Core SQLite config issues
- `pattern-based-fixing` — Apply pattern value-object-safe LINQ từ Wave 3
- `test-system-upgrade` — Upgrade test setup để chạy thực sự thay vì `return;` early exit

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 5
- **Verified Facts:**
  - Fact 1: T1 bị skip vì "Orders table has configuration issues" — comment trong code
  - Fact 2: T2 fail do `Program.cs:292` — `TenantId == seedTenantId` LINQ không translate được (fix đã apply nhưng chưa verify rebuild)
  - Fact 3: T3/T4 dùng `o.TenantId.Value == tenantId` trong LINQ — cùng root cause Wave 3 (value object translation)
  - Fact 4: `TestEntityBuilder.CreateOrder/CreateShop/CreateCustomer` tồn tại (dùng trong tests khác)
  - Fact 5: `_dbContext.Database.OpenConnection()` + `EnsureCreated()` pattern đã có trong `ConfigureTestDatabase()`
- **Assumptions:**
  - `OrderConfiguration.cs` có thể thiếu một số config tương thích SQLite
  - Program.cs có thể còn queries khác dùng TenantId value object
- **Open Questions:**
  - Q1: `OrderConfiguration.cs` cụ thể thiếu gì gây T1 fail? (cần đọc file)
  - Q2: Program.cs còn query TenantId nào khác sau line 292 không?
- **Recommended Action:** IMPLEMENT — Verified Facts > Assumptions, Open Questions < 3

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| `GoldenFlowSystemTests.cs` | Xóa `return;` → tests chạy thực sự, có thể fail với assert mới | Fix từng test theo batch, re-run sau mỗi batch |
| `Program.cs` (seed) | Thay đổi seed logic → production seed behavior thay đổi | Chỉ fix LINQ translation, không thay đổi business logic seed |
| `OrderConfiguration.cs` | Thêm SQLite-compatible config → có thể affect existing order tests | Verify 144 tests sau khi sửa |
| `TestEntityBuilder.cs` | Nếu cần thêm helper methods | Chỉ thêm, không sửa existing methods |

## 9. TDD & E2E TESTING STRATEGY
- **Approach:** Tests đã tồn tại — chỉ remove `return;` và fix infrastructure
- **Test T1 — DB Connection:**
  - Verify `OrderConfiguration.cs` tương thích SQLite in-memory
  - Nếu thiếu config: thêm vào, re-run
- **Test T2 — Health Check:**
  - Verify `Program.cs` fix đã rebuild
  - Scan toàn bộ Program.cs cho `TenantId ==` patterns còn lại
- **Test T3/T4 — Insert + Multi-Tenant:**
  - Replace LINQ `TenantId.Value ==` với client-side pattern (consistent với Wave 3 fix)
- **Test boundary:**
  - Unit tests: N/A (đây là integration tests)
  - Integration tests: `GoldenFlowSystemTests` — 4 tests cần activate
  - E2E tests: N/A

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Chiến lược thực thi: JIT Planning + Pure Execution
1 session duy nhất, scope nhỏ, fix theo thứ tự T2 → T1 → T3 → T4

### Micro-phase breakdown

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | Scan Program.cs TenantId patterns + verify OrderConfiguration | Fix Program.cs remaining queries, remove `return;` T2 |
| **S2** | Đọc OrderConfiguration.cs, xác định thiếu gì | Fix config, remove `return;` T1 |
| **S3** | Xác định LINQ patterns trong T3/T4 | Replace với client-side pattern, remove `return;` T3+T4 |
| **S4** | Run 144/144 | Fix nếu còn fail, commit |

### Rules
- Dừng lại sau mỗi session nếu failure count tăng
- Không sửa quá 3 files trong 1 batch
- Verify `guard-check.ps1` trước commit

## 11. ESTIMATED EFFORT
- ~1.5–2 giờ tổng (4 sessions ngắn)
- 1 session theo JIT Planning
- **BLOCKER:** OrderConfiguration.cs có thể cần investigation trước khi biết scope fix chính xác
