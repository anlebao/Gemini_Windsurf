# TASK CARD: ARCHITECTURE - PHASE 2 - Docker Compose Production Fix

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Fix docker-compose.prod.yml to match monolithic architecture
- **Nghiệp vụ áp dụng:** Align production deployment with actual architecture (CoreHub as shared library or background service)

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `newfeaturebuild.md` (7-step ANALYZE → IMPLEMENT)
- **Execution Mode:** ANALYZE → IMPLEMENT

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
  - `docker-compose.prod.yml` (Main file to modify)
  - `scripts/deploy.sh` (Deployment script)
  - `docs/Architecture/Monolithic_Architecture/01-Monolithic-Architecture-Analysis.md` (Reference)
  - `3_CoreHub/Program.cs` (Read-only - verify architecture)
- **Boundary Rules (Nghiêm cấm):**
  - KHÔNG sửa CoreHub Program.cs (architecture correct)
  - KHÔNG sửa Gateway Program.cs (dependencies correct)
  - KHÔNG sửa start-apps.ps1 (Phase 1 complete)
  - KHÔNG sửa CI/CD files (Phase 3)
  - KHÔNG sửa database schemas

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **Architecture Decision:** Must decide: remove CoreHub container OR reconfigure as background service
- [ ] **Production Safety:** Must have backup before deployment
- [ ] **Service Continuity:** Must ensure Gateway can function without CoreHub HTTP endpoint
- [ ] **Resource Optimization:** Must reduce resource usage if removing container
- [ ] **Health Checks:** Must update health check logic

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** Architecture decision documented (remove or reconfigure CoreHub)
- [ ] **SC2:** docker-compose.prod.yml updated according to decision
- [ ] **SC3:** Gateway container config updated (if needed)
- [ ] **SC4:** Environment variables aligned (JWT Secret, NATS config)
- [ ] **SC5:** Docker compose builds successfully
- [ ] **SC6:** Local Docker deployment works
- [ ] **SC7:** All services start correctly
- [ ] **SC8:** Health checks pass
- [ ] **SC9:** No resource conflicts
- [ ] **SC10:** Deployment script updated
- [ ] **SC11:** Documentation updated
- [ ] **SC12:** Rollback plan documented

**Implementation Date:** 2026-06-30
**Branch:** feature/architecture-refactor-phase2-docker-compose

## 6. ACTIVE SKILLS (MAX 3)
- `domain-integrity-validation` — Ensure architecture changes don't violate domain rules
- `system-refactor-safety` — Ensure safe refactoring of production deployment
- `pattern-based-fixing` — Apply consistent patterns to Docker configuration

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 5
- **Verified Facts:**
  - Fact 1: docker-compose.prod.yml configures CoreHub with HTTP endpoint (line 77)
  - Fact 2: CoreHub Program.cs is background service (no HTTP server)
  - Fact 3: Gateway has project reference to CoreHub (in-process communication)
  - Fact 4: Current CI/CD builds and deploys 4 containers
  - Fact 5: Production deployment may be failing due to architecture mismatch
- **Assumptions:**
  - [ASSUMPTION_1] Gateway can function without CoreHub HTTP endpoint
  - [ASSUMPTION_2] Production database has correct schema
  - [ASSUMPTION_3] NATS communication is independent of CoreHub HTTP
- **Open Questions:**
  - Q1: Should CoreHub be removed entirely OR kept as background service?
  - Q2: Are there any production dependencies on CoreHub HTTP endpoint?
  - Q3: Will removing CoreHub container break any production workflows?
- **Recommended Action:** Analyze production dependencies before making architecture decision

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| docker-compose.prod.yml | Production deployment changes | Test thoroughly, have rollback plan |
| scripts/deploy.sh | Deployment script changes | Update deployment logic |
| Gateway container config | Service startup changes | Update health checks |
| Environment variables | Config changes | Update secrets management |
| CI/CD pipeline | May need updates (Phase 3) | Coordinate with Phase 3 |

## 9. TDD & E2E TESTING STRATEGY
- **Docker Build Testing:**
  - Verify Docker compose builds successfully
  - Verify all images pull correctly
  - Verify no build errors
- **Local Deployment Testing:**
  - Test local Docker deployment
  - Verify all services start
  - Verify health checks pass
- **Integration Testing:**
  - Test service-to-service communication
  - Test database connectivity
  - Test NATS communication
- **Test boundary:**
  - Unit tests: Not needed (config changes only)
  - Integration tests: Manual Docker deployment testing
  - E2E tests: Not needed (Phase 5)

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Chiến lược thực thi: JIT Planning + Pure Execution

Phase 2 requires careful architecture decision making. Must analyze production dependencies before implementing changes.

### Micro-phase breakdown cho Phase 2 (Docker Compose Fix)

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | Analyze docker-compose.prod.yml, identify CoreHub dependencies, research production usage patterns, make architecture decision (remove vs reconfigure) | Implement architecture decision in docker-compose.prod.yml, update Gateway config, update environment variables |
| **S2** | Plan deployment script changes, plan health check updates, plan testing strategy, plan rollback procedure | Update deployment script, update health checks, test local Docker deployment, validate all services start, document changes |

### Rules
- [RULE_1] Must make architecture decision before implementation
- [RULE_2] Must have backup of current production config
- [RULE_3] Must test local Docker deployment before production
- [RULE_4] Must document rollback procedure

## 11. ESTIMATED EFFORT
- 2-3 sessions (2-3 hours per session)
- Total: 4-9 hours
- **BLOCKER:** Architecture decision (must analyze production dependencies first)