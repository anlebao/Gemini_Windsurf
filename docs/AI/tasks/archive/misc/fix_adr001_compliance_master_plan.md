# MASTER IMPLEMENTATION PLAN — Fix ADR-001 Compliance

> ⚠️ **SUPERSEDED (2026-06-29)**
> This plan has been merged into the unified roadmap:
> **`docs/AI/tasks/UNIFIED_ROADMAP_master_plan.md`** (Option C — Merged, Layer-ordered)
> Do NOT use this file for implementation. Use the unified plan instead.
> ADR001-W1 is COMPLETE (commit `8863692`). Remaining waves W2–W5 are tracked in unified plan.

**Created:** 2026-06-29
**Last Updated:** 2026-06-29 (refactored: Wave 2 split into W2–W5; then merged into unified plan)
**Current Status:** WAVE 1 COMPLETE → SUPERSEDED (W2–W5 in unified plan)
**Branch strategy:** See unified plan
**Execution principle:** See unified plan

---

## 0. EXECUTION RULES

### Session protocol
1. **Mỗi session chỉ làm 1 wave** — không skip
2. **Bắt đầu mỗi session:** Load context → đọc task card của wave → chốt plan
3. **Trước khi session end:** Chạy `dotnet build` + architecture tests, đảm bảo pass
4. **Sau mỗi session:** Commit `[WAVE N] <task description>`

### Branch protocol
```
main
 └── feature/adr001-wave1-ci-check       ✅ MERGED
 └── feature/adr001-wave2-edge-compose   (tạo docker-compose.edge.yml)
 └── feature/adr001-wave3-nats-worker    (NatsSyncWorker service)
 └── feature/adr001-wave4-sqlite-config  (ShopERP SQLite + feature flag)
 └── feature/adr001-wave5-ci-edge        (CI edge pipeline + integration test)
```

### Hard rules (không violate)
- **KHÔNG sửa Domain layer** trong bất kỳ wave nào
- **KHÔNG sửa docker-compose.prod.yml** — v1 SaaS không thay đổi
- **Wave N+1 không bắt đầu** nếu Wave N chưa pass build + tests

---

## 1. ARCHITECTURE CONTEXT

### Two-Version Strategy
| Version | File | DB Strategy | NATS |
|---------|------|-------------|------|
| v1 SaaS Online | `docker-compose.prod.yml` | PostgreSQL | Không cần sync worker |
| v2 Edge Offline | `docker-compose.edge.yml` | SQLite local + NATS + PostgreSQL | Required |

### Codebase hiện tại (đã verified)
| Asset | Trạng thái |
|-------|-----------|
| `NATS.Client` package trong CoreHub | ✅ Đã cài |
| `Microsoft.EntityFrameworkCore.Sqlite` trong ShopERP | ✅ Đã cài |
| `IOutboxRepository` + `OutboxRepository` | ✅ Đã implement |
| `OutboxMessage` entity với retry + exponential backoff | ✅ Đã implement |
| `ShopERP/Program.cs` dùng SQLite (`vanan_shoperp.db`) | ✅ Đã dùng |
| `docker-compose.edge.yml` | ❌ Chưa tồn tại |
| `NatsSyncWorker` service | ❌ Chưa tồn tại |
| CI edge pipeline | ❌ Chưa tồn tại |

---

## 2. WAVE 1 — Fix CI Check ✅ COMPLETE

**Status:** COMPLETE (commit `8863692`)
**Branch:** `feature/adr001-wave1-ci-check` → merged to `main`
**Task Card:** `docs/AI/tasks/wave1_ci_adr001_check_task_card.md`

### Đã làm
- [x] Thêm Rule H: ADR-001 compliance test vào `ArchitectureRulesTests.cs`
- [x] Cập nhật `architecture-guard.ps1` để check ADR-001

---

## 3. WAVE 2 — Create docker-compose.edge.yml

**Branch:** `feature/adr001-wave2-edge-compose`
**Estimated sessions:** 1
**Conflict risk:** LOW (file mới, không đụng prod)
**Dependency:** Wave 1 ✅

| Task ID | Task | File | Task Card |
|---------|------|------|-----------|
| W2-ADR-T1 | Tạo docker-compose.edge.yml với SQLite volumes + NATS | `docker-compose.edge.yml` | `W2-ADR-T1-card.md` |
| W2-ADR-T2 | Thêm ADR-001 compliance test cho edge compose | `ArchitectureRulesTests.cs` | `W2-ADR-T2-card.md` |

**Exit criteria:**
- [ ] `docker-compose.edge.yml` tồn tại với đúng cấu trúc
- [ ] Architecture test Rule I (edge compose) PASSES
- [ ] `dotnet build` 0 errors

---

## 4. WAVE 3 — Implement NatsSyncWorker

**Branch:** `feature/adr001-wave3-nats-worker`
**Estimated sessions:** 2
**Conflict risk:** MEDIUM (code mới trong CoreHub)
**Dependency:** Wave 2 ✅

| Task ID | Task | File | Task Card |
|---------|------|------|-----------|
| W3-ADR-T1 | Implement `INatsEventPublisher` + `NatsEventPublisher` | `3_CoreHub/Infrastructure/Messaging/NatsEventPublisher.cs` | `W3-ADR-T1-card.md` |
| W3-ADR-T2 | Implement `NatsSyncWorker` BackgroundService | `3_CoreHub/Services/NatsSyncWorker.cs` | `W3-ADR-T2-card.md` |

**Exit criteria:**
- [ ] `NatsEventPublisher` implement đúng interface
- [ ] `NatsSyncWorker` poll Outbox → publish → mark processed
- [ ] Unit tests cho NatsSyncWorker pass
- [ ] `dotnet build` 0 errors

---

## 5. WAVE 4 — ShopERP SQLite + Feature Flag

**Branch:** `feature/adr001-wave4-sqlite-config`
**Estimated sessions:** 1
**Conflict risk:** LOW (env-var controlled, không break v1)
**Dependency:** Wave 3 ✅

| Task ID | Task | File | Task Card |
|---------|------|------|-----------|
| W4-ADR-T1 | Add `--sync-worker` mode + conditional DI registration | `5_WebApps/ShopERP/Program.cs` | `W4-ADR-T1-card.md` |
| W4-ADR-T2 | Add `appsettings.Edge.json` + SQLite volume config | `5_WebApps/ShopERP/appsettings.Edge.json` | `W4-ADR-T2-card.md` |

**Exit criteria:**
- [ ] `--sync-worker` arg activates NatsSyncWorker DI
- [ ] SQLite path configurable via env var `SQLITE_DB_PATH`
- [ ] Không ảnh hưởng v1 SaaS khi không có arg
- [ ] `dotnet build` 0 errors

---

## 6. WAVE 5 — CI Edge Pipeline + Integration Test

**Branch:** `feature/adr001-wave5-ci-edge`
**Estimated sessions:** 1
**Conflict risk:** LOW (CI config mới)
**Dependency:** Wave 4 ✅

| Task ID | Task | File | Task Card |
|---------|------|------|-----------|
| W5-ADR-T1 | Tạo `.github/workflows/ci-edge.yml` validate edge deploy | `.github/workflows/ci-edge.yml` | `W5-ADR-T1-card.md` |

**Exit criteria:**
- [ ] CI edge pipeline tồn tại và pass
- [ ] Pipeline validate docker-compose.edge.yml structure
- [ ] All architecture tests pass (21+ rules)
- [ ] Toàn bộ ADR-001 compliance đạt

---

## 7. SUCCESS CRITERIA (OVERALL)

- [ ] `docker-compose.edge.yml` tồn tại và đúng cấu trúc ADR-001
- [ ] `NatsSyncWorker` implemented, unit tested
- [ ] ShopERP hỗ trợ `--sync-worker` mode không break v1
- [ ] Architecture tests cover cả v1 và v2 edge
- [ ] CI edge pipeline (`ci-edge.yml`) pass
- [ ] `docker-compose.prod.yml` KHÔNG thay đổi (v1 SaaS preserved)

---

## 8. REFERENCES

- ADR-001: `docs/Architecture/ADR001-Station-Architecture.md`
- Architecture Tests: `6_Tests/VanAn.Architecture.Tests/ArchitectureRulesTests.cs`
- Outbox: `3_CoreHub/Infrastructure/Messaging/IOutboxRepository.cs`
- CI: `.github/workflows/ci.yml` (v1), `.github/workflows/ci-edge.yml` (v2 - to create)
- Superseded: `docs/AI/tasks/wave2_arch_drift_fix_task_card.md` (replaced by W2–W5)
