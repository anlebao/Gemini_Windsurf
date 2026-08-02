# TASK CARD: E2E TESTS - WAVE 1 - Fix E2E Tests (Playwright)

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Fix E2E tests để chạy thành công với real ShopERP/Gateway/KhachLink services
- **Nghiệp vụ áp dụng:** Enable E2E test suite cho CI/CD pipeline, validate full-stack accounting flows

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `Fix_Errors.md` (pattern-based FIX_ONLY)
- **Execution Mode:** FIX_ONLY (fix E2E test failures, không add new features)

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
  - `6_Testing/docker-compose.test.yml` (Create new)
  - `6_Testing/global-setup.ts` (Update service startup logic)
  - `6_Testing/e2e-tests/accounting-entry-flow.spec.ts` (Fix auth and selectors)
  - `6_Testing/e2e-tests/accounting-flow.spec.ts` (Fix selectors and assertions)
  - `6_Testing/playwright.config.ts` (Add retry logic and timeout config)
  - `6_Testing/utils/env-config.ts` (Review and update if needed)
- **Boundary Rules (Nghiêm cấm):**
  - KHÔNG modify production code (5_WebApps/ShopERP, 2_Gateway)
  - KHÔNG bypass test failures với `test.skip()` hoặc similar
  - KHÔNG hardcode credentials trong test files (dùng environment variables)
  - KHÔNG modify test logic trừ khi cần để fix selectors/auth

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **Service Health Check:** docker-compose.test.yml phải include health checks cho tất cả services
- [ ] **Startup Timeout:** global-setup.ts phải handle service startup timeout gracefully
- [ ] **Auth Flow:** E2E tests phải use proper login flow, không hardcoded credentials
- [ ] **Selector Stability:** Selectors phải stable và unique (avoid dynamic selectors)
- [ ] **Retry Strategy:** Retry logic chỉ cho network flakiness, không cho logic failures

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** docker-compose.test.yml spins up ShopERP (5003), Gateway (5001), KhachLink (5002) thành công
- [ ] **SC2:** Services pass health checks trước khi tests start
- [ ] **SC3:** global-setup.ts waits cho services ready với timeout handling
- [ ] **SC4:** accounting-entry-flow.spec.ts: 4/4 tests pass (create revenue, validation errors, duplicate detection)
- [ ] **SC5:** accounting-flow.spec.ts: All accounting E2E tests pass
- [ ] **SC6:** No hardcoded credentials trong test files (dùng TEST_EMAIL, TEST_PASSWORD env vars)
- [ ] **SC7:** Playwright config có retry logic cho network-dependent tests
- [ ] **SC8:** E2E tests có thể run trong CI environment (GitHub Actions hoặc local CI)
- [ ] **SC9:** Test report generated (HTML + JSON) sau khi tests complete
- [ ] **SC10:** Manual curl test to ShopERP succeeds: `curl http://localhost:5003/health`
- [ ] **SC11:** Test execution time < 5 minutes cho full E2E suite
- [ ] **SC12:** No flaky tests (tests pass consistently 3/3 runs)

**Implementation Date:** 2026-06-26
**Branch:** feature/test-wave1-e2e-fix

## 6. ACTIVE SKILLS (MAX 3)
- `load-context` — Load project context để hiểu current E2E test failures
- `update-state` — Update project_state.md sau khi Wave 1 complete
- `devin-for-terminal` — Lookup Playwright documentation nếu cần

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 5
- **Verified Facts:**
  - Fact 1: E2E tests fail với ERR_CONNECTION_REFUSED (ShopERP not running)
  - Fact 2: accounting-entry-flow.spec.ts có 4 tests, tất cả fail
  - Fact 3: global-setup.ts viết empty storageState khi login fails
  - Fact 4: Playwright config có storageState: 'auth/admin.json' nhưng file empty
  - Fact 5: docker-compose.test.yml không tồn tại (cần tạo mới)
- **Assumptions:**
  - Docker Desktop installed và running
  - ShopERP project có health check endpoint
  - Gateway và KhachLink có thể start với docker-compose
- **Open Questions:**
  - Q1: ShopERP có health check endpoint không? Nếu không, cần implement không?
  - Q2: E2E tests cần admin credentials hay accounting role credentials?
  - Q3: Cần start services với docker-compose hay dùng Testcontainers?
- **Recommended Action:** Start với ANALYZE phase để check ShopERP health check endpoint và current service startup configuration

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| docker-compose.test.yml | New file, no existing impact | Delete file if needed |
| global-setup.ts | Update service startup logic | Git revert global-setup.ts changes |
| accounting-entry-flow.spec.ts | Fix auth and selectors | Git revert test file changes |
| accounting-flow.spec.ts | Fix selectors and assertions | Git revert test file changes |
| playwright.config.ts | Add retry logic | Git revert config changes |

## 9. TDD & E2E TESTING STRATEGY
- **E2E Test Fix Strategy:**
  - Fix infrastructure trước (docker-compose, service startup)
  - Fix auth flow sau (login với proper credentials)
  - Fix selectors cuối (update selectors để match actual UI)
  - Add retry logic cho network flakiness
- **Test boundary:**
  - Unit tests: Không affected (Wave 1 chỉ E2E tests)
  - Integration tests: Không affected (Wave 1 chỉ E2E tests)
  - E2E tests: Fix existing E2E tests để pass với real services

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Chiến lược thực thi: JIT Planning + Pure Execution

Wave 1 là FIX_ONLY mode nên sẽ follow approach:
1. **ANALYZE phase:** Check ShopERP health check, analyze current E2E failures, design docker-compose structure
2. **IMPLEMENT phase:** Create docker-compose, fix global-setup, fix test files, add retry logic

### Micro-phase breakdown cho Wave 1

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | Check ShopERP health check endpoint, design docker-compose.test.yml structure, identify service dependencies | Create docker-compose.test.yml, test service startup locally |
| **S2** | Analyze global-setup.ts failures, design service health check logic, plan auth flow fix | Update global-setup.ts với service health checks, implement proper login flow |
| **S3** | Analyze accounting-entry-flow.spec.ts failures, identify selector issues, plan auth credential strategy | Fix accounting-entry-flow.spec.ts selectors and auth, update playwright.config.ts với retry logic |
| **S4** | Analyze accounting-flow.spec.ts failures, identify selector issues, plan fixes | Fix accounting-flow.spec.ts selectors and assertions, run full E2E suite to verify |

### Rules
- Mỗi session phải run E2E tests để verify fixes work
- Không modify production code (ShopERP, Gateway, KhachLink)
- Use environment variables cho credentials (TEST_EMAIL, TEST_PASSWORD)
- Commit sau mỗi session với message format `[WAVE1] Task description`

## 11. ESTIMATED EFFORT
- 3-4 sessions theo JIT Planning
- **BLOCKER:** ShopERP không có health check endpoint hoặc Docker Desktop not installed
