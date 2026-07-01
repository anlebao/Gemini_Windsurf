# TASK CARD: ARCHITECTURE - PHASE 3 - CI/CD Pipeline Fix

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Fix CI/CD pipeline to align with new architecture (reduced containers)
- **Nghiệp vụ áp dụng:** Update GitHub Actions workflow to match Phase 2 Docker changes

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `newfeaturebuild.md` (7-step ANALYZE → IMPLEMENT)
- **Execution Mode:** ANALYZE → IMPLEMENT

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
  - `.github/workflows/cd.yml` (Main file to modify)
  - `docker-compose.prod.yml` (Reference - understand new architecture)
  - `scripts/deploy.sh` (Reference - understand deployment logic)
- **Boundary Rules (Nghiêm cấm):**
  - KHÔNG sửa docker-compose files (Phase 2 complete)
  - KHÔNG sửa application code (architecture changes complete)
  - KHÔNG sửa database schemas
  - KHÔNG modify GitHub Secrets without approval
  - KHÔNG deploy to production without staging validation

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **CI Pipeline Integrity:** Must ensure CI pipeline continues to build correctly
- [ ] **CD Pipeline Safety:** Must ensure CD pipeline deploys correctly
- [ ] **GitHub Secrets:** Must not expose secrets in workflow files
- [ ] **Staging Validation:** Must test CD pipeline on staging before production
- [ ] **Rollback Capability:** Must maintain ability to rollback quickly

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** CI pipeline updated to build correct number of images (3 or 4)
- [ ] **SC2:** CD pipeline updated to deploy correct containers
- [ ] **SC3:** Health checks updated to match new architecture
- [ ] **SC4:** CI pipeline passes (dry-run)
- [ ] **SC5:** CD pipeline passes (staging)
- [ ] **SC6:** GitHub Secrets aligned with new config
- [ ] **SC7:** No pipeline failures
- [ ] **SC8:** Deployment time optimized (if containers reduced)
- [ ] **SC9:** Build time optimized (if images reduced)
- [ ] **SC10:** Documentation updated
- [ ] **SC11:** Rollback plan documented
- [ ] **SC12:** Ready for production deployment

**Implementation Date:** 2026-06-30
**Branch:** feature/architecture-refactor-phase3-ci-cd

## 6. ACTIVE SKILLS (MAX 3)
- `domain-integrity-validation` — Ensure CI/CD changes don't violate deployment rules
- `system-refactor-safety` — Ensure safe refactoring of CI/CD pipeline
- `pattern-based-fixing` — Apply consistent patterns to workflow updates

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 4
- **Verified Facts:**
  - Fact 1: Current CI pipeline builds 4 images (CoreHub, Gateway, ShopERP, KhachLink)
  - Fact 2: Current CD pipeline deploys 4 containers
  - Fact 3: Phase 2 may reduce containers to 3 (remove CoreHub)
  - Fact 4: Health checks currently validate all containers
- **Assumptions:**
  - [ASSUMPTION_1] Phase 2 architecture decision is final
  - [ASSUMPTION_2] Staging environment is available for testing
  - [ASSUMPTION_3] GitHub Secrets can be updated safely
- **Open Questions:**
  - Q1: How many containers after Phase 2? (3 or 4)
  - Q2: Are there any hardcoded container references in CI/CD?
  - Q3: Will health check logic need significant changes?
- **Recommended Action:** Wait for Phase 2 completion before implementing Phase 3

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| .github/workflows/cd.yml | CI/CD pipeline changes | Test dry-run, test staging, have rollback |
| GitHub Secrets | Secret management changes | Update carefully, document changes |
| Deployment logic | Deployment procedure changes | Test thoroughly, document rollback |
| Health checks | Monitoring changes | Update monitoring dashboards |
| Build time | Build optimization | Measure improvement, document metrics |

## 9. TDD & E2E TESTING STRATEGY
- **CI Pipeline Testing:**
  - Test CI pipeline with dry-run
  - Verify build step works correctly
  - Verify image push works correctly
- **CD Pipeline Testing:**
  - Test CD pipeline on staging
  - Verify deployment works correctly
  - Verify health checks pass
- **Integration Testing:**
  - Test service startup after deployment
  - Test service-to-service communication
- **Test boundary:**
  - Unit tests: Not needed (workflow changes only)
  - Integration tests: Staging deployment testing
  - E2E tests: Not needed (Phase 5)

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Chiến lược thực thi: JIT Planning + Pure Execution

Phase 3 depends on Phase 2 completion. Must wait for final architecture decision before implementing CI/CD changes.

### Micro-phase breakdown cho Phase 3 (CI/CD Fix)

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | Analyze current CI/CD pipeline, identify CoreHub references, plan build step changes (3 vs 4 images), plan deploy step changes, plan health check updates | Update CI workflow build step, update CD workflow deploy step, update health check logic, test CI dry-run |
| **S2** | Plan GitHub Secrets updates, plan staging validation, plan rollback procedure, plan documentation updates | Update GitHub Secrets (if needed), test CD pipeline on staging, validate all services, document changes, document rollback plan |

### Rules
- [RULE_1] Must wait for Phase 2 completion before starting
- [RULE_2] Must test CI pipeline with dry-run first
- [RULE_3] Must test CD pipeline on staging before production
- [RULE_4] Must document rollback procedure

## 11. ESTIMATED EFFORT
- 2-3 sessions (2-3 hours per session)
- Total: 4-9 hours
- **BLOCKER:** Phase 2 completion (must know final architecture)