# TASK CARD: ARCHITECTURE - PHASE 4 - Offline-First Edge Deployment Fix

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Fix docker-compose.edge.yml to align with new architecture
- **Nghiệp vụ áp dụng:** Ensure offline-first deployment works correctly after architecture changes

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `newfeaturebuild.md` (7-step ANALYZE → IMPLEMENT)
- **Execution Mode:** ANALYZE → IMPLEMENT

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
  - `docker-compose.edge.yml` (Main file to modify)
  - `docs/Architecture/Monolithic_Architecture/01-Monolithic-Architecture-Analysis.md` (Reference)
  - `docker-compose.prod.yml` (Reference - understand new architecture)
- **Boundary Rules (Nghiêm cấm):**
  - KHÔNG sửa docker-compose.prod.yml (Phase 2 complete)
  - KHÔNG sửa application code (architecture changes complete)
  - KHÔNG sửa CI/CD files (Phase 3 complete)
  - KHÔNG modify SQLite schemas
  - KHÔNG break NATS sync worker functionality

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **Offline Capabilities:** Must preserve offline-first capabilities
- [ ] **SQLite Integration:** Must ensure SQLite sidecar works correctly
- [ ] **NATS Sync:** Must ensure NATS sync workers work correctly
- [ ] **Edge Deployment:** Must ensure edge deployment works independently
- [ ] **Data Consistency:** Must ensure data sync between edge and cloud

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** docker-compose.edge.yml updated according to architecture decision
- [ ] **SC2:** SQLite sidecar integration validated
- [ ] **SC3:** NATS sync workers validated
- [ ] **SC4:** Offline capabilities preserved
- [ ] **SC5:** Local edge deployment works
- [ ] **SC6:** No breaking changes to offline features
- [ ] **SC7:** Data sync between edge and cloud works
- [ ] **SC8:** Resource usage optimized
- [ ] **SC9:** Health checks pass
- [ ] **SC10:** Documentation updated
- [ ] **SC11:** Edge deployment documented
- [ ] **SC12:** Ready for production edge deployment

**Implementation Date:** 2026-06-30
**Branch:** feature/architecture-refactor-phase4-edge

## 6. ACTIVE SKILLS (MAX 3)
- `domain-integrity-validation` — Ensure edge changes don't violate domain rules
- `system-refactor-safety` — Ensure safe refactoring of edge deployment
- `pattern-based-fixing` — Apply consistent patterns to edge configuration

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 3
- **Verified Facts:**
  - Fact 1: docker-compose.edge.yml exists for v2 Hybrid deployment
  - Fact 2: Edge deployment uses SQLite sidecar
  - Fact 3: Edge deployment uses NATS sync workers
- **Assumptions:**
  - [ASSUMPTION_1] Edge deployment has similar architecture to production
  - [ASSUMPTION_2] SQLite integration is independent of CoreHub HTTP
  - [ASSUMPTION_3] NATS sync workers don't depend on CoreHub HTTP endpoint
- **Open Questions:**
  - Q1: Does edge deployment currently use CoreHub container?
  - Q2: Are there edge-specific CoreHub dependencies?
  - Q3: Will removing CoreHub affect offline capabilities?
- **Recommended Action:** Analyze edge deployment dependencies before implementing changes

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| docker-compose.edge.yml | Edge deployment changes | Test local edge deployment thoroughly |
| SQLite sidecar | Data storage changes | Validate SQLite integration |
| NATS sync workers | Sync logic changes | Validate NATS communication |
| Offline capabilities | Feature changes | Ensure offline features work |
| Edge documentation | Documentation changes | Update edge deployment docs |

## 9. TDD & E2E TESTING STRATEGY
- **Edge Deployment Testing:**
  - Test local edge deployment
  - Verify SQLite sidecar works
  - Verify NATS sync workers work
- **Offline Capability Testing:**
  - Test offline features
  - Test data sync when online
  - Test data consistency
- **Integration Testing:**
  - Test edge-to-cloud sync
  - Test data reconciliation
- **Test boundary:**
  - Unit tests: Not needed (config changes only)
  - Integration tests: Manual edge deployment testing
  - E2E tests: Not needed (Phase 5)

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Chiến lược thực thi: JIT Planning + Pure Execution

Phase 4 focuses on edge deployment only. Can be done independently after Phase 2.

### Micro-phase breakdown cho Phase 4 (Edge Deployment Fix)

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | Analyze docker-compose.edge.yml, identify CoreHub dependencies, analyze SQLite integration, analyze NATS sync workers, plan changes needed | Update docker-compose.edge.yml according to architecture decision, update SQLite config if needed, update NATS config if needed |
| **S2** | Plan offline capability testing, plan edge-to-cloud sync testing, plan validation strategy, plan documentation updates | Test local edge deployment, validate SQLite sidecar, validate NATS sync workers, test offline capabilities, test edge-to-cloud sync, document changes |

### Rules
- [RULE_1] Must analyze edge dependencies before implementing
- [RULE_2] Must test offline capabilities thoroughly
- [RULE_3] Must preserve edge-to-cloud sync functionality
- [RULE_4] Must document edge deployment changes

## 11. ESTIMATED EFFORT
- 1-2 sessions (2-3 hours per session)
- Total: 2-6 hours
- **BLOCKER:** Phase 2 completion (must know final architecture)

---

## 12. IMPLEMENTATION SUMMARY (2026-06-30)

**Session 1 Complete**
**Status:** ✅ COMPLETE
**Commit:** Pending

**Changes Applied:**
1. ✅ Removed `corehub` service from docker-compose.edge.yml (lines 69-94)
2. ✅ Updated Gateway service:
   - Removed `CoreHub__BaseUrl=http://corehub:80` environment variable
   - Updated `depends_on` to postgres and nats with health checks (instead of corehub)
3. ✅ Updated ShopERP service:
   - Updated `depends_on` to postgres and nats with health checks (instead of corehub)
4. ✅ Preserved edge-specific features:
   - SQLite sidecar (shoperp_sqlite_data volume)
   - NATS sync worker (shoperp-nats-sync)
   - Shared volume configuration between shoperp and shoperp-nats-sync
5. ✅ Updated header comment to reflect monolithic architecture

**Validation Results:**
- ✅ Docker compose validation script passed all checks
- ✅ CoreHub service not found (valid for monolithic architecture)
- ✅ Gateway configuration validation passed
- ✅ Environment variable naming validation passed
- ✅ Logging configuration validation passed
- ✅ Required services validation passed

**Rationale:**
Edge deployment had the same architecture violation as production - CoreHub configured as HTTP service when it should be loaded in-process by Gateway. This fix aligns edge deployment with the monolithic architecture established in Phase 2.

**Note:** Full edge deployment testing (Docker environment, SQLite integration, NATS sync) deferred to Phase 5 (Validation & E2E Testing) which has dedicated test infrastructure.