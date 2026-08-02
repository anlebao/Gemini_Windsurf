# TASK CARD: CI/CD INTEGRATION - WAVE 5 - CI/CD Integration & Documentation

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Integrate test suite vào CI/CD pipeline với coverage reporting và comprehensive documentation
- **Nghiệp vụ áp dụng:** Enable automated testing trong CI/CD, provide documentation cho test maintenance

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `newfeaturebuild.md` (7-step ANALYZE → IMPLEMENT)
- **Execution Mode:** IMPLEMENT (CI/CD config và documentation, không test logic changes)

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
  - `.github/workflows/test-accounting.yml` (Create new)
  - `6_Testing/playwright.config.ts` (Update parallelization config)
  - `6_Tests/README.md` (Update documentation)
  - `docs/testing/test-maintenance.md` (Create new)
  - `docs/testing/test-onboarding.md` (Create new)
- **Boundary Rules (Nghiêm cấm):**
  - KHÔNG modify test files (Wave 5 chỉ CI/CD config và documentation)
  - KHÔNG modify production code
  - KHÔNG change test logic hoặc assertions
  - KHÔNG bypass CI/CD checks - tests phải pass trong CI

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **CI Execution Time:** Test execution time < 10 minutes trong CI environment
- [ ] **Coverage Reporting:** Coverage report generated và uploaded (codecov hoặc similar)
- [ ] **Branch Protection:** Failed tests phải block PR merges
- [ ] **Test Parallelization:** Tests configured để run parallel trong CI
- [ ] **Documentation Completeness:** Documentation complete và up-to-date

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** GitHub Actions workflow runs all accounting tests (unit, integration, E2E)
- [ ] **SC2:** Test execution time < 10 minutes trong CI environment
- [ ] **SC3:** Coverage report generated và uploaded (codecov hoặc similar)
- [ ] **SC4:** Failed tests block PR merges (branch protection configured)
- [ ] **SC5:** 6_Tests/README.md updated với how-to-run tests guide
- [ ] **SC6:** test-maintenance.md created với test maintenance guidelines
- [ ] **SC7:** test-onboarding.md created với onboarding guide cho new developers
- [ ] **SC8:** Playwright config updated với test parallelization
- [ ] **SC9:** All tests pass trong CI environment
- [ ] **SC10:** Documentation reviewed và approved
- [ ] **SC11:** Test suite integrated vào release process
- [ ] **SC12:** Final merge to main successful, all tests passing

**Implementation Date:** 2026-06-26
**Branch:** feature/test-wave5-cicd-integration

## 6. ACTIVE SKILLS (MAX 3)
- `load-context` — Load project context để understand current CI/CD setup
- `update-state` — Update project_state.md sau khi Wave 5 complete
- `devin-for-terminal` — Lookup GitHub Actions documentation nếu cần

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 2
- **Verified Facts:**
  - Fact 1: GitHub Actions workflow không exist cho accounting tests
  - Fact 2: 6_Tests/README.md tồn tại nhưng có thể outdated
- **Assumptions:**
  - GitHub Actions available cho repository
  - Codecov hoặc similar service available cho coverage reporting
  - Branch protection có thể configured trong repository settings
- **Open Questions:**
  - Q1: Repository có GitHub Actions enabled không?
  - Q2: Coverage reporting service nào available (codecov, coveralls, etc.)?
  - Q3: Branch protection rules hiện tại là gì?
- **Recommended Action:** Start với ANALYZE phase để check current CI/CD setup và repository configuration

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| .github/workflows/test-accounting.yml | New file, no existing impact | Delete file if needed |
| playwright.config.ts | Update parallelization config | Git revert config changes |
| 6_Tests/README.md | Update documentation | Git revert documentation changes |
| test-maintenance.md | New file, no existing impact | Delete file if needed |
| test-onboarding.md | New file, no existing impact | Delete file if needed |

## 9. TDD & E2E TESTING STRATEGY
- **CI/CD Integration Strategy:**
  - Create GitHub Actions workflow để run all tests
  - Configure test parallelization để optimize CI execution time
  - Add coverage reporting với codecov hoặc similar
  - Configure branch protection để block PRs với failing tests
- **Test boundary:**
  - Unit tests: Run trong CI với coverage reporting
  - Integration tests: Run trong CI với real test database
  - E2E tests: Run trong CI với docker-compose services

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Chiến lược thực thi: JIT Planning + Pure Execution

Wave 5 là CI/CD integration và documentation nên sẽ follow approach:
1. **ANALYZE phase:** Check current CI/CD setup, plan GitHub Actions workflow, plan documentation structure
2. **IMPLEMENT phase:** Create GitHub Actions workflow, update Playwright config, create documentation, verify CI pipeline

### Micro-phase breakdown cho Wave 5

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | Check current CI/CD setup, plan GitHub Actions workflow structure, identify test execution order | Create .github/workflows/test-accounting.yml skeleton, plan test stages |
| **S2** | Plan test parallelization strategy, identify optimization opportunities, plan coverage reporting integration | Implement GitHub Actions workflow với test stages, add coverage reporting |
| **S3** | Plan documentation structure (README, maintenance guide, onboarding guide), identify key sections to document | Update 6_Tests/README.md với how-to-run guide, create test-maintenance.md skeleton |
| **S4** | Plan onboarding guide content, identify common issues và solutions, plan troubleshooting section | Complete test-maintenance.md, create test-onboarding.md, update Playwright config với parallelization |
| **S5** | Plan final integration test, identify merge strategy, plan rollback strategy | Run full CI pipeline locally, verify all tests pass, merge all waves to main |

### Rules
- Test GitHub Actions workflow locally trước khi push
- Verify coverage report generation working
- Review documentation với team trước khi finalize
- Commit sau mỗi session với message format `[WAVE5] Task description`
- Final merge to main chỉ khi tất cả tests passing trong CI

## 11. ESTIMATED EFFORT
- 3-4 sessions theo JIT Planning
- **BLOCKER:** GitHub Actions not available hoặc coverage reporting service not accessible
