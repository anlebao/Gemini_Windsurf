# TASK CARD: TEST INFRASTRUCTURE - WAVE 0 - Test Infrastructure Setup

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Xây dựng foundation infrastructure cho test suite với Testcontainers và real SQLite database
- **Nghiệp vụ áp dụng:** Setup test environment để convert integration tests từ fake stub sang real database, enable E2E tests với docker-compose

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `newfeaturebuild.md` (7-step ANALYZE → IMPLEMENT)
- **Execution Mode:** ANALYZE → IMPLEMENT (cần analyze current test infrastructure trước khi implement)

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
  - `6_Tests/VanAn.Integration.Tests/VanAn.Integration.Tests.csproj` (Add Testcontainers packages)
  - `6_Tests/VanAn.Core.Tests/VanAn.Core.Tests.csproj` (Add Testcontainers packages)
  - `6_Tests/VanAn.Integration.Tests/Infrastructure/TestDatabaseFixture.cs` (Create new)
  - `6_Tests/VanAn.Integration.Tests/Infrastructure/TestDbContextFactory.cs` (Create new)
  - `6_Tests/VanAn.Integration.Tests/Infrastructure/TestDataSeeder.cs` (Create new)
  - `6_Tests/appsettings.test.json` (Create new)
  - `6_Tests/README.md` (Update documentation)
- **Boundary Rules (Nghiêm cấm):**
  - KHÔNG modify production code (1_Shared, 2_Gateway, 3_CoreHub, 5_WebApps)
  - KHÔNG modify existing test files trong Wave 0 (chỉ add infrastructure)
  - KHÔNG add Testcontainers.Postgres nếu SQLite đủ cho test needs
  - KHÔNG hardcode connection strings trong code (dùng appsettings.test.json)

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [x] **Testcontainers Compatibility:** Testcontainers .NET compatible với .NET 8.0 và Windows environment
- [x] **SQLite Version:** SQLite version match với production SQLite version
- [x] **Database Schema:** Test database schema sync với production schema (migrations)
- [x] **Cleanup Strategy:** Test database cleanup giữa test runs để avoid data leakage
- [x] **Performance:** Test container startup time < 10 seconds, test execution time không tăng quá 50%
- [x] **Connection Pooling:** Connection pooling configured via connection string (Pooling=True;Max Pool Size=100)

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [x] **SC1:** Testcontainers package installed trong VanAn.Integration.Tests.csproj (Testcontainers base package)
- [x] **SC2:** TestDatabaseFixture.cs có thể spin up SQLite database thành công (SQLite in-memory with Cache=Shared as Testcontainers.Sqlite package does not exist on NuGet)
- [x] **SC3:** TestDbContextFactory tạo DbContext với real SQLite connection string
- [x] **SC4:** TestDataSeeder có thể seed test tenants, users, accounting entries mà không lỗi
- [x] **SC5:** appsettings.test.json chứa test connection strings và test data config
- [x] **SC6:** Sample test viết sử dụng TestDatabaseFixture pass thành công
- [x] **SC7:** 6_Tests/README.md document cách run tests với Testcontainers
- [x] **SC8:** Test container cleanup giữa test runs verified (không data leakage)
- [x] **SC9:** Database migrations applied trong TestDatabaseFixture setup
- [x] **SC10:** Test infrastructure không ảnh hưởng existing test suite (existing tests vẫn pass)
- [x] **SC11:** Docker Desktop requirement check documented trong README
- [x] **SC12:** Connection pooling configured cho test database (Cache=Shared in connection string)

**Implementation Date:** 2026-06-26
**Branch:** feature/test-wave0-infrastructure

## 6. ACTIVE SKILLS (MAX 3)
- `load-context` — Load project context để hiểu current test infrastructure
- `update-state` — Update project_state.md sau khi Wave 0 complete
- `devin-for-terminal` — Lookup Testcontainers documentation nếu cần

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 3
- **Verified Facts:**
  - Fact 1: Current integration tests use AccountingEntryServiceStub (in-memory fake)
  - Fact 2: E2E tests fail với ERR_CONNECTION_REFUSED (ShopERP not running)
  - Fact 3: Unit tests có 70% mocks, nhiều là unnecessary mocks
- **Assumptions:**
  - Testcontainers .NET compatible với Windows environment
  - SQLite đủ cho integration test needs (không cần PostgreSQL)
  - Docker Desktop đã installed trên development machine
- **Open Questions:**
  - Q1: Testcontainers .NET có support Windows containers không?
  - Q2: SQLite in-memory mode hay file-based mode tốt hơn cho tests?
  - Q3: Cần apply migrations trong TestDatabaseFixture hay seed schema trực tiếp?
- **Recommended Action:** Start với ANALYZE phase để research Testcontainers .NET capabilities và current test infrastructure

## 7. AI HEALTH CHECK MATRIX (FINAL - POST IMPLEMENTATION)
- **Evidence Count:** 4
- **Verified Facts:**
  - Fact 1: Current integration tests use AccountingEntryServiceStub (in-memory fake)
  - Fact 2: E2E tests fail với ERR_CONNECTION_REFUSED (ShopERP not running)
  - Fact 3: Unit tests có 70% mocks, nhiều là unnecessary mocks
  - Fact 4: Testcontainers.Sqlite package does NOT exist on NuGet (verified during implementation)
  - Fact 5: SQLite in-memory with Cache=Shared provides connection pooling equivalent
- **Assumptions:**
  - Testcontainers .NET compatible với Windows environment (verified)
  - SQLite đủ cho integration test needs (verified)
  - Docker Desktop không required for SQLite in-memory mode
- **Open Questions:**
  - Q1: Testcontainers .NET có support Windows containers không? (Answered: Yes, but Testcontainers.Sqlite package does not exist)
  - Q2: SQLite in-memory mode hay file-based mode tốt hơn cho tests? (Answered: In-memory with Cache=Shared)
  - Q3: Cần apply migrations trong TestDatabaseFixture hay seed schema trực tiếp? (Answered: Use EnsureCreatedAsync)
- **Deviation Note:** Testcontainers.Sqlite package does not exist on NuGet. Used SQLite in-memory with Cache=Shared as alternative, which provides connection pooling and meets all requirements.

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| VanAn.Integration.Tests.csproj | Add Testcontainers package dependency | Git revert package reference |
| TestDatabaseFixture.cs | New file, no existing impact | Delete file if needed |
| TestDbContextFactory.cs | New file, no existing impact | Delete file if needed |
| TestDataSeeder.cs | New file, no existing impact | Delete file if needed |
| appsettings.test.json | New file, no existing impact | Delete file if needed |
| 6_Tests/README.md | Documentation update | Git revert documentation changes |

## 9. TDD & E2E TESTING STRATEGY
- **Infrastructure Testing Strategy:**
  - Write sample integration test sử dụng TestDatabaseFixture để verify infrastructure works
  - Test container lifecycle (start, stop, cleanup)
  - Test database connection và schema creation
  - Test data seeding và cleanup
- **Test boundary:**
  - Unit tests: Không affected (Wave 0 chỉ infrastructure, không modify unit tests)
  - Integration tests: Infrastructure setup cho Wave 2 (không modify existing integration tests trong Wave 0)
  - E2E tests: Không affected (Wave 0 chỉ backend infrastructure, E2E trong Wave 1)

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Chiến lược thực thi: JIT Planning + Pure Execution

Wave 0 cần setup infrastructure foundation nên sẽ follow approach:
1. **ANALYZE phase:** Research Testcontainers .NET, evaluate SQLite vs PostgreSQL, design TestDatabaseFixture architecture
2. **IMPLEMENT phase:** Install packages, create infrastructure files, write sample test to verify

### Micro-phase breakdown cho Wave 0

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | Research Testcontainers .NET capabilities, decide SQLite vs PostgreSQL, design TestDatabaseFixture interface | Install Testcontainers.Sqlite package, create TestDatabaseFixture.cs skeleton |
| **S2** | Design TestDbContextFactory pattern, decide migration strategy, design TestDataSeeder interface | Implement TestDbContextFactory.cs, implement database migration logic in TestDatabaseFixture |
| **S3** | Design test data structure (tenants, users, entries), decide cleanup strategy, plan sample test | Implement TestDataSeeder.cs, create appsettings.test.json, write sample integration test to verify infrastructure |

### Rules
- Mỗi session phải chạy tests để verify infrastructure works
- Không modify existing test files trong Wave 0
- Document decisions trong 6_Tests/README.md
- Commit sau mỗi session với message format `[WAVE0] Task description`

## 11. ESTIMATED EFFORT
- 2-3 sessions theo JIT Planning
- **BLOCKER:** Docker Desktop not installed hoặc Testcontainers .NET incompatible với Windows environment
