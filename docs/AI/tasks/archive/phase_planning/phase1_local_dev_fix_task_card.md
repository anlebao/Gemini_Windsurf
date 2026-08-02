# TASK CARD: ARCHITECTURE - PHASE 1 - Local Development Environment Fix

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Fix local development environment để match với monolithic architecture
- **Nghiệp vụ áp dụng:** Align local development với actual architecture (CoreHub as shared library, not standalone service)

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `newfeaturebuild.md` (7-step ANALYZE → IMPLEMENT)
- **Execution Mode:** ANALYZE → IMPLEMENT

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
  - `scripts/start-apps.ps1` (Main file to modify)
  - `3_CoreHub/Program.cs` (Read-only - verify architecture)
  - `2_Gateway/Program.cs` (Read-only - verify dependencies)
  - `docs/Architecture/Monolithic_Architecture/01-Monolithic-Architecture-Analysis.md` (Reference)
- **Boundary Rules (Nghiêm cấm):**
  - KHÔNG sửa CoreHub Program.cs (architecture correct)
  - KHÔNG sửa Gateway Program.cs (dependencies correct)
  - KHÔNG sửa docker-compose files (Phase 2)
  - KHÔNG sửa CI/CD files (Phase 3)
  - KHÔNG sửa database schemas

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **Architecture Consistency:** Must align with monolithic architecture (CoreHub as shared library)
- [ ] **Environment Variables:** Must use correct naming convention (`__` for `:`)
- [ ] **Database Alignment:** Must use VanAnLocal database (match local infrastructure)
- [ ] **JWT Secret:** Must provide valid JWT Secret for Gateway
- [ ] **NATS Configuration:** Must align NATS URL across all services

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** CoreHub removed from start-apps.ps1 (no standalone startup)
- [ ] **SC2:** Gateway starts successfully on port 5001
- [ ] **SC3:** Gateway health endpoint returns 200 OK
- [ ] **SC4:** KhachLink can call Gateway APIs successfully
- [ ] **SC5:** CoreHub services load in Gateway process (in-process)
- [ ] **SC6:** Environment variables correctly set (JWT Secret, NATS URL)
- [ ] **SC7:** Database connection uses VanAnLocal database
- [ ] **SC8:** No startup errors in Gateway
- [ ] **SC9:** Local development simplified (3 processes instead of 4)
- [ ] **SC10:** Build: 0 errors
- [ ] **SC11:** Infrastructure dependency check passes
- [ ] **SC12:** Documentation updated

**Implementation Date:** 2026-06-30
**Branch:** feature/architecture-refactor-phase1-local-dev

## 6. ACTIVE SKILLS (MAX 3)
- `domain-integrity-validation` — Ensure architecture changes don't violate domain rules
- `system-refactor-safety` — Ensure safe refactoring of startup scripts
- `pattern-based-fixing` — Apply consistent patterns to environment variable fixes

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 5
- **Verified Facts:**
  - Fact 1: CoreHub Program.cs uses `Host.CreateDefaultBuilder` (background service)
  - Fact 2: Gateway Program.cs has project reference to CoreHub (in-process)
  - Fact 3: docker-compose.prod.yml configures CoreHub with HTTP endpoint (mismatch)
  - Fact 4: start-apps.ps1 tries to start CoreHub with `--urls` parameter (incorrect)
  - Fact 5: Architecture analysis confirms monolithic with shared libraries
- **Assumptions:**
  - [ASSUMPTION_1] Gateway can load CoreHub services via project reference
  - [ASSUMPTION_2] VanAnLocal database has correct schema for Gateway
  - [ASSUMPTION_3] JWT Secret can use development value for local testing
- **Open Questions:**
  - Q1: Does Gateway have all necessary CoreHub service registrations?
  - Q2: Are there any hardcoded CoreHub HTTP calls in Gateway?
  - Q3: Will removing CoreHub standalone startup break any local development workflows?
- **Recommended Action:** Proceed with Phase 1 after verifying Gateway service registrations

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| scripts/start-apps.ps1 | Local development workflow changes | Update documentation, test thoroughly |
| 3_CoreHub/Program.cs | None (read-only) | N/A |
| 2_Gateway/Program.cs | None (read-only) | N/A |
| docker-compose files | None (Phase 2) | N/A |
| CI/CD files | None (Phase 3) | N/A |

## 9. TDD & E2E TESTING STRATEGY
- **Local Development Testing:**
  - Verify Gateway starts successfully
  - Verify Gateway health endpoint responds
  - Verify KhachLink can call Gateway APIs
  - Verify CoreHub services load in Gateway
- **Integration Testing:**
  - Test database connectivity (VanAnLocal)
  - Test NATS connectivity
  - Test service-to-service communication
- **Test boundary:**
  - Unit tests: Not needed (script changes only)
  - Integration tests: Manual testing of service startup
  - E2E tests: Not needed (Phase 5)

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Chiến lược thực thi: JIT Planning + Pure Execution

Phase 1 focuses on local development environment fix only. No production changes. Low risk, high value.

### Micro-phase breakdown cho Phase 1 (Local Development Fix)

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | Analyze current start-apps.ps1, identify CoreHub startup logic, plan removal strategy | Remove CoreHub startup from start-apps.ps1, update Gateway environment variables |
| **S2** | Plan database connection string alignment, plan JWT Secret configuration, plan testing strategy | Implement database alignment, add JWT Secret, test Gateway startup, validate KhachLink communication |

### Rules
- [RULE_1] Only modify start-apps.ps1 (no other files)
- [RULE_2] Test Gateway startup after changes
- [RULE_3] Verify KhachLink can call Gateway APIs
- [RULE_4] Document all changes

## 11. ESTIMATED EFFORT
- 2 sessions (1-2 hours per session)
- Total: 2-4 hours
- **BLOCKER:** None (local environment only, no dependencies)