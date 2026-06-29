# TASK CARD: ADR-001 - Wave 2 - Architecture Drift Fix

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Align production deployment với ADR-001 (SQLite + NATS + PostgreSQL)
- **Nghiệp vụ áp dụng:** Offline-first architecture implementation

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `.devin/workflows/newfeaturebuild.md`
- **Execution Mode:** IMPLEMENT (sửa production deployment)

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
  - `docs/decisions/ADR-001-SQLite-Offline-First.md`
  - `docker-compose.prod.yml`
  - `3_CoreHub/Services/` (NATS sync worker)
  - `5_WebApps/ShopERP/Program.cs` (SQLite configuration)
  - `6_Tests/VanAn.Architecture.Tests/` (verify test passes)
- **Boundary Rules (Nghiêm cấm):**
  - KHÔNG sửa Domain layer
  - KHÔNG sửa CoreHub business logic
  - CHỈ sửa deployment configuration + NATS sync worker

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **ADR-001 Alignment:** Deployment MUST match ADR-001 exactly
- [ ] **Backward Compatibility:** Existing production data must not be lost
- [ ] **Incremental Rollout:** Test locally before production deployment
- [ ] **Test Must Pass:** ADR-001 compliance test MUST pass after fix
- [ ] **CI Pipeline:** CI must pass with new deployment

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC-1:** SQLite local station architecture designed and documented
- [ ] **SC-2:** NATS sync worker service implemented
- [ ] **SC-3:** docker-compose.prod.yml includes SQLite station service
- [ ] **SC-4:** docker-compose.prod.yml includes NATS sync worker service
- [ ] **SC-5:** Outbox pattern configured for NATS publish
- [ ] **SC-6:** ShopERP configured to use SQLite local
- [ ] **SC-7:** Local deployment tested (docker-compose up)
- [ ] **SC-8:** ADR-001 compliance test PASSES
- [ ] **SC-9:** CI pipeline passes
- [ ] **SC-10:** Build: 0 errors
- [ ] **SC-11:** PostgreSQL migration strategy documented
- [ ] **SC-12:** Rollback plan documented

**Implementation Date:** 2026-06-29
**Branch:** feature/adr001-wave2-arch-drift

## 6. ACTIVE SKILLS (MAX 3)
- `nats-sqlite-deployment-validation` — Validate SQLite + NATS deployment
- `outbox-pattern-implementation` — Configure Outbox for NATS sync
- `domain-integrity-validation` — Ensure no Domain layer changes

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 4
- **Verified Facts:**
  - Fact 1: ADR-001 requires SQLite local stations
  - Fact 2: NATS.Client package already installed
  - Fact 3: Outbox pattern already implemented
  - Fact 4: Wave 1 test currently FAILS (drift confirmed)
- **Assumptions:**
  - Assumption 1: NATS sync worker can be added as separate service
  - Assumption 2: SQLite stations can be deployed as Docker volumes
  - Assumption 3: PostgreSQL data migration not required (sync target only)
- **Open Questions:**
  - Q1: How many SQLite stations needed per tenant?
  - Q2: Conflict resolution strategy for multi-station sync?
  - Q3: Rollback strategy if deployment fails?
- **Recommended Action:** IMPLEMENT - Start with single SQLite station per service

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| docker-compose.prod.yml | High risk - production deployment | Test locally first, rollback plan |
| 3_CoreHub/Services/NatsSyncWorker.cs | New service - no existing impact | Isolated service, no core logic changes |
| 5_WebApps/ShopERP/Program.cs | SQLite config change | Configurable via environment variable |
| 6_Tests/VanAn.Architecture.Tests/ | Test verification only | Test must pass after fix |

## 9. TDD & E2E TESTING STRATEGY
- **Deployment Test Strategy:**
  - Test docker-compose.prod.yml locally
  - Verify SQLite station containers start
  - Verify NATS sync worker connects
  - Verify Outbox publishes to NATS
- **Test boundary:**
  - Unit tests: NatsSyncWorker unit tests
  - Integration tests: NATS publish/subscribe tests
  - E2E tests: Full deployment smoke test

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Chiến lược thực thi: JIT Planning + Pure Execution

Wave 2 involves production deployment changes, so Planning phase critical:
- S1: Design SQLite station architecture → Document design
- S2: Implement NATS sync worker → Unit test
- S3: Update docker-compose.prod.yml → Local test
- S4: Configure ShopERP SQLite → Verify connection
- S5: Run ADR-001 test → Verify PASSES
- S6: Document migration + rollback → Production ready

### Micro-phase breakdown cho Wave 2

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | Design SQLite station architecture (volumes, persistence) | Create design doc, decide on station count |
| **S2** | Design NATS sync worker (Outbox → NATS publish) | Implement NatsSyncWorker service |
| **S3** | Plan docker-compose.prod.yml changes | Add SQLite station + NATS worker services |
| **S4** | Plan ShopERP SQLite configuration | Update ShopERP Program.cs for SQLite local |
| **S5** | Plan local deployment test | Run docker-compose up, verify all services start |
| **S6** | Plan migration + rollback strategy | Document migration steps, rollback procedure |

### Rules
- Test locally before production
- ADR-001 test MUST pass after each step
- Document rollback plan before deployment
- KHÔNG modify Domain layer

## 11. ESTIMATED EFFORT
- 2-3 days (production deployment changes)
- 3-5 sessions theo JIT Planning
- **BLOCKER:** PostgreSQL data migration strategy unclear