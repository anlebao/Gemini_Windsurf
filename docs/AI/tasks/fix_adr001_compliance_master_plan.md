# MASTER IMPLEMENTATION PLAN — Fix ADR-001 Compliance

**Created:** 2026-06-29
**Last Updated:** 2026-06-29
**Current Status:** PLANNING
**Branch strategy:** feature/adr001-compliance (per wave)
**Execution principle:** Incremental validation - each wave must pass before next

---

## 0. EXECUTION RULES

### JIT Planning Strategy (Áp dụng cho mọi wave)
**Nguyên tắc cốt lõi:** KHÔNG code mò mẫm - Investigate trước, Implement sau

**Bước 1: INVESTIGATE & ANALYZE (Planning Phase)**
- Đọc ADR-001 để hiểu rõ requirements (SQLite local + NATS sync + PostgreSQL cloud)
- Đọc production code để hiểu current deployment
- Identify root cause: architecture drift vs ADR alignment
- Lập detailed coding plan với specific steps
- Chốt approach trước khi viết bất kỳ dòng code nào

**Bước 2: IMPLEMENT (Execution Phase)**
- Thực hiện viết code theo plan đã chốt ở Bước 1
- KHÔNG thay đổi approach khi đang implement
- Mỗi bước implement xong, run CI/test để verify
- Nếu fail theo cách khác, DỪNG LẠI và quay lại Bước 1

### Session protocol
1. **Mỗi session chỉ làm 1 wave** - không跳步
2. **Bắt đầu mỗi session:** Planning Phase
3. **Sau khi plan chốt:** Execution Phase
4. **Trước khi session end**: Chạy CI tests, đảm bảo pass
5. **Sau mỗi session**: Commit với message format `[WAVE X] Task description`

### Branch protocol
```
main
  └── feature/adr001-wave1-ci-check (Wave 1)
      └── feature/adr001-wave2-arch-drift (Wave 2)
```

### Hard rules (không violate)
- **KHÔNG sửa Domain layer** trong Wave 2
- **Test MUST fail initially** trong Wave 1 (drift detection)
- **Wave 1 chỉ thêm tests**, không sửa production code
- **Wave 2 test locally trước** production deployment

---

## 1. CURRENT ARCHITECTURE DRIFT

### ADR-001 Requirements
- **SQLite local stations:** Mỗi station có SQLite local để offline operation
- **NATS message broker:** Sync events giữa stations
- **PostgreSQL cloud:** Central storage cho sync target
- **Outbox pattern:** Events persisted trước NATS publish
- **Conflict resolution:** Handle multi-station sync conflicts

### Current Production Deployment
- **docker-compose.prod.yml:** PostgreSQL direct (KHÔNG SQLite stations)
- **NATS:** Package installed nhưng KHÔNG sync workers deployed
- **Outbox pattern:** Implemented nhưng KHÔNG dùng cho NATS sync
- **CI/CD:** KHÔNG check ADR-001 compliance

### Drift Summary
| Requirement | ADR-001 | Current | Status |
|-------------|---------|---------|--------|
| SQLite local stations | Required | None | ❌ Missing |
| NATS sync workers | Required | None | ❌ Missing |
| PostgreSQL role | Sync target | Primary DB | ⚠️ Wrong purpose |
| CI ADR check | Required | None | ❌ Missing |

---

## 2. WAVE 1 — Fix CI Check (ADR-001 Compliance Test)

**Branch:** feature/adr001-wave1-ci-check
**Estimated sessions:** 1-2
**Conflict risk:** LOW (thêm tests, không sửa production code)
**Priority:** HIGH (foundation cho Wave 2)
**Task Card:** `docs/AI/tasks/wave1_ci_adr001_check_task_card.md`

### Tasks
| # | Task ID | Task | Files | Status |
|---|---|---|---|---|
| 1 | W1-T1 | Add ADR-001 compliance test to Architecture Tests | 6_Tests/VanAn.Architecture.Tests/ArchitectureRulesTests.cs | PENDING |
| 2 | W1-T2 | Add docker-compose validation test | 6_Tests/VanAn.Architecture.Tests/ArchitectureRulesTests.cs | PENDING |
| 3 | W1-T3 | Update architecture-guard.ps1 to check ADR-001 | architecture-guard.ps1 | PENDING |
| 4 | W1-T4 | Run CI pipeline to verify ADR-001 test | .github/workflows/ci.yml | PENDING |

### Entry criteria
- [ ] Project builds successfully (`dotnet build`)
- [ ] Git status clean (no uncommitted changes)
- [ ] ADR-001 document reviewed

### Exit criteria — ALL PASSED
- [ ] ADR-001 compliance test added to Architecture Tests
- [ ] Test validates docker-compose.prod.yml vs ADR-001 requirements
- [ ] architecture-guard.ps1 updated to check ADR-001
- [ ] CI pipeline passes with new ADR-001 test
- [ ] Test should FAIL initially (since architecture drift exists)
- [ ] Build: 0 errors

### Why first
- Low risk (thêm tests, không sửa production)
- Foundation cho Wave 2 (test sẽ detect drift khi fix)
- CI enforcement prevents future drift

---

## 3. WAVE 2 — Fix Architecture Drift (Deployment Alignment)

**Branch:** feature/adr001-wave2-arch-drift
**Estimated sessions:** 3-5
**Conflict risk:** HIGH (sửa production deployment)
**Priority:** HIGH (align với ADR-001)
**Task Card:** `docs/AI/tasks/wave2_arch_drift_fix_task_card.md`

### Tasks
| # | Task ID | Task | Files | Status |
|---|---|---|---|---|
| 1 | W2-T1 | Design SQLite local station architecture | docs/Architecture/ADR001-Station-Architecture.md | PENDING |
| 2 | W2-T2 | Create NATS sync worker service | 3_CoreHub/Services/NatsSyncWorker.cs | PENDING |
| 3 | W2-T3 | Update docker-compose.prod.yml with SQLite stations | docker-compose.prod.yml | PENDING |
| 4 | W2-T4 | Configure Outbox pattern for NATS publish | 3_CoreHub/Infrastructure/Outbox/NatsPublisher.cs | PENDING |
| 5 | W2-T5 | Update ShopERP to use SQLite local in production | 5_WebApps/ShopERP/Program.cs | PENDING |
| 6 | W2-T6 | Test deployment locally (docker-compose up) | docker-compose.prod.yml | PENDING |
| 7 | W2-T7 | Run ADR-001 compliance test to verify fix | 6_Tests/VanAn.Architecture.Tests/ | PENDING |

### Entry criteria
- [ ] Wave 1 complete (CI ADR-001 test added)
- [ ] ADR-001 test currently FAILING (drift confirmed)
- [ ] Git status clean
- [ ] NATS package installed (verified)

### Exit criteria — ALL PASSED
- [ ] SQLite local station architecture designed and documented
- [ ] NATS sync worker service implemented
- [ ] docker-compose.prod.yml includes SQLite stations
- [ ] Outbox pattern configured for NATS publish
- [ ] ShopERP configured to use SQLite local
- [ ] Local deployment tested (docker-compose up)
- [ ] ADR-001 compliance test PASSES
- [ ] CI pipeline passes
- [ ] Build: 0 errors

### Why second
- Wave 1 provides test foundation
- High risk deployment changes need test coverage
- Incremental validation prevents production breakage

---

## 4. SUCCESS CRITERIA (OVERALL)

- [ ] ADR-001 compliance test added to CI pipeline
- [ ] Test initially FAILS (detects drift)
- [ ] Architecture drift fixed
- [ ] Test PASSES after fix
- [ ] Production deployment aligns with ADR-001
- [ ] CI pipeline enforces ADR-001 compliance
- [ ] No future architecture drift possible without test failure

---

## 5. REFERENCES

- ADR-001: SQLite + NATS Offline First (`docs/decisions/ADR-001-SQLite-Offline-First.md`)
- Current docker-compose.prod.yml
- CI workflow (`.github/workflows/ci.yml`)
- Architecture Tests (`6_Tests/VanAn.Architecture.Tests/`)