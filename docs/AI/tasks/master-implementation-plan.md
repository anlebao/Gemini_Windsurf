# MASTER IMPLEMENTATION PLAN — Wave-by-Wave Execution

**Created:** 2026-06-18  
**Last Updated:** 2026-06-18  
**Current Status:** Wave 0 ✅ COMPLETED → Wave 1 READY TO START  
**Branch strategy:** Multiple feature branches, merge to `main` (align-consumer-phase4) between waves  
**Execution principle:** Sequential waves, separate sessions per wave, JIT Planning + Pure Execution

---

## 0. EXECUTION RULES

### Session protocol
1. **Mỗi wave = 1+ sessions** (không rigid 1:1 — wave lớn có thể 2-3 sessions, wave nhỏ có thể 1 session)
2. **Session bắt đầu:** Load context (`load-context` skill) → đọc master plan này → đọc task card của wave
3. **Session kết thúc khi:** Wave SC pass HOẶC context đầy (whichever first)
4. **Sau mỗi session:** Update `project_state.md` (Section 4 + 10 + 11) + commit
5. **Giữa các wave:** Verify `dotnet build VanAn.sln --configuration Release` + `guard-check.ps1` pass trước khi sang wave kế

### Branch protocol (UPDATED 2026-06-18)
```
main (align-consumer-phase4) ← Wave 0 merged ✅
  └── fix/tenantid-remediation (Wave 1 + Wave 3) — NEXT
  └── fix/einvoice-cleanup (Wave 2 + Wave 4) — AFTER Wave 1
```
- ~~Wave 0: trên branch `fix/shoperp-audit-trail-di`~~ → **MERGED to main**
- Wave 1+3: branch mới `fix/tenantid-remediation` từ `main` (tạo ngay bây giờ)
- Wave 2+4: branch mới `fix/einvoice-cleanup` từ `main` (sau Wave 1 merged)
- Wave 5: branch riêng theo task

### Hard rules (không violate)
- CẤM chạy 2 wave song song trên cùng 1 branch (conflict risk)
- CẤM sang wave kế nếu wave hiện tại chưa merge + build pass
- CẤM skip `project_state.md` update sau mỗi session
- CẤM mở wave mới nếu Open Questions của wave đó chưa resolve

---

## 1. WAVE 0 — Quick Wins, Isolated ✅ COMPLETED

**Branch:** `fix/shoperp-audit-trail-di` (merged to `main` via commit `1cccd4c`)
**Completed:** 2026-06-18
**Sessions:** 1 session

### Tasks
| # | Task ID | Task | Files | Task card | Status |
|---|---|---|---|---|---|
| 1 | P0-3 | Fix `VanAnDashboard.razor` DI crash | `VanAnDashboard.razor` (1 file) | `task-p0-3-dashboard-di-crash.md` | ✅ DONE |
| 2 | P0-7 | EInvoice test coverage — write missing tests | `EInvoiceOrchestratorTests.cs` (9 new tests), `Core.Tests/WebhookServiceTests.cs` (rewritten) | task_sprint3b_provider_integration.md §5 | ✅ DONE |

### Entry criteria (Wave 0)
- [x] Branch `fix/shoperp-audit-trail-di` active
- [x] EInvoice review audit committed (3e25c00)

### Exit criteria (Wave 0) — ALL PASSED
- [x] P0-3: Dashboard navigate không crash
- [x] P0-7: CircuitBreakerTests verified existing, HTTP mock tests verified, EInvoiceOrchestratorTests CreateInvoiceAsync flow (6 tests), WebhookServiceTests rewritten (18 tests)
- [x] `dotnet build VanAn.sln --configuration Release` → 0 errors
- [x] `guard-check.ps1` → PASS
- [x] `project_state.md` updated + committed
- [x] Merge to `main` → **COMPLETED**

### Why first
- 0 dependency on TenantId work
- 0 file overlap with Wave 1-4
- Test coverage (P0-7) tạo safety net trước khi sửa production code ở Wave 1
- Dashboard crash (P0-3) là production risk, fix nhanh

---

## 2. WAVE 1 — TenantId Foundation (IN PROGRESS — Phase 1 ✅, Phase 2 NEXT)

**Branch:** `fix/tenantid-remediation` (created from `main` — Wave 0 merged ✅)
**Estimated sessions:** 2-3 (Phase 1 = 1 session ✅, Phase 2 = 1-2 sessions)
**Conflict risk:** HIGH (TenantProvider.cs, Gateway controllers, VanAnDbContext)

### Tasks (sequential — Phase 2 re-touches Phase 1 files)
| # | Task ID | Task | Depends on | Task card | Status |
|---|---|---|---|---|---|
| 3 | P0-1a | TenantId Phase 1 — stop bleeding | — | task-tenantid-phase1-stop-bleeding.md | ✅ DONE |
| 4 | P0-1b | TenantId Phase 2 — tenant foundation | Phase 1 merged | task-tenantid-phase2-tenant-foundation.md | ⏳ NEXT |

### Entry criteria
- [x] Wave 0 merged to `main`
- [x] Branch `fix/tenantid-remediation` created from `main`
- [x] Phase 1 Open Questions resolved (Q1/Q2 đã resolve 2026-06-18 per card)
- [x] Phase 1 implemented and committed

### Exit criteria Phase 1 — ✅ COMPLETED
- [x] SC1-SC10 pass (per task card)
- [x] Build + arch tests pass (11/11 PASS)
- [x] Commit + ready to continue Phase 2

### Exit criteria Phase 2
- [ ] SC1-SC12 pass (per task card)
- [ ] UserTenant entity + configuration + Login DB lookup + claim `tenant_id` + `[Authorize(Policy="RequireTenantAccess")]` trên tất cả Gateway controllers + Accounting pages
- [ ] Build + guard-check + arch tests + integration tests pass
- [ ] `project_state.md` updated + committed
- [ ] Merge to `main`

### Why here
- Phase 2 SC4 chuẩn hóa claim name `TenantId`→`tenant_id` re-touch `TenantProvider.cs` (Phase 1 vừa fix) → phải sequential
- Phase 2 thêm auth policy lên TẤT CẢ Gateway controllers bao gồm `WebhookController` → phải trước EInvoice cleanup (Wave 2 fix WebhookController)
- TenantId là root cause của 3 backlogs (P0-1 + E2E auth T-20 + manual test fail §9) → fix sớm unblock nhiều thứ

---

## 3. WAVE 2 — EInvoice API Layer (sau Wave 1, new branch)

**Branch:** `fix/einvoice-cleanup` (tạo từ `main` sau Wave 1 merged)
**Estimated sessions:** 2 (Phase A = 1 session, Phase B = 1 session)
**Conflict risk:** MEDIUM (WebhookController — nhưng Phase 2 đã merged nên không conflict)

### Tasks (sequential — Phase B cần Phase A cleanup trước)
| # | Task ID | Task | Depends on | Task card |
|---|---|---|---|---|
| 5 | P0-6a | EInvoice cleanup Phase A — dead code | Phase 2 merged (WebhookController auth) | task-einvoice-deadcode-cleanup.md Phase A |
| 6 | P0-6b | EInvoice cleanup Phase B — controller | Phase A + Phase 2 tenant pattern | task-einvoice-deadcode-cleanup.md Phase B |

### Entry criteria
- [ ] Wave 1 merged to `main`
- [ ] Branch `fix/einvoice-cleanup` created from `main`
- [ ] Open Questions resolved: Q1 (controller location — Gateway vs ShopERP), Q2 (route convention plural vs singular)

### Exit criteria Phase A
- [ ] SC1-SC4 pass: DELETE/rewrite `EInvoiceE2ETests.cs`, fix `WebhookController` route + body shape
- [ ] Build pass

### Exit criteria Phase B
- [ ] SC5-SC7 pass: HKDElectronicInvoiceController tạo lại + DTOs đầy đủ + DI wiring
- [ ] Build + guard-check pass
- [ ] `project_state.md` updated + committed
- [ ] Merge to `main`

### Why here (not earlier)
- Controller mới cần JWT claim tenant pattern (từ Phase 1+2)
- WebhookController fix route/body phải sau Phase 2 (Phase 2 thêm auth policy lên WebhookController)
- Nếu làm trước Phase 2 → conflict + phải retrofit tenant pattern

---

## 4. WAVE 3 — TenantId Completion (parallel với Wave 2, trên branch tenantid)

**Branch:** `fix/tenantid-remediation` (sau Phase 2 merged, tiếp tục trên branch này hoặc tạo branch mới từ main)
**Estimated sessions:** 2 (Phase 3 = 1 session, Phase 4 = 1 session)
**Conflict risk:** LOW (KhachLink + Accounting Razor pages — không đụng Gateway/EInvoice)

### Tasks (sequential — Phase 4 cần Phase 3)
| # | Task ID | Task | Depends on | Task card |
|---|---|---|---|---|
| 7 | P0-1c | TenantId Phase 3 — KhachLink tenant | Phase 2 merged | task-tenantid-phase3-khachlink-tenant.md |
| 8 | P0-1d | TenantId Phase 4 — cleanup & unification | Phase 2 + Phase 3 merged | task-tenantid-phase4-cleanup.md |

### Entry criteria
- [ ] Wave 1 (Phase 2) merged to `main`
- [ ] Branch từ `main` (có thể dùng lại `fix/tenantid-remediation` hoặc tạo mới)

### Exit criteria Phase 3
- [ ] SC1-SC8 pass: KhachLink resolve tenant từ shop URL, SignalR auth, remove demo data, OfflineOrderService tenant from context
- [ ] Build + guard-check + arch tests (VA-KHACHLINK-004) pass

### Exit criteria Phase 4
- [ ] SC1-SC10 pass: 0 hardcoded fallbacks, 6 Razor pages dùng ITenantProvider, 0 manual FindFirst
- [ ] Build + guard-check + arch tests + all existing tests pass (no regression)
- [ ] `project_state.md` updated + committed
- [ ] Merge to `main`

### Why parallel with Wave 2
- Phase 3+4 đụng KhachLink + Accounting Razor pages
- KHÔNG đụng Gateway controllers hay EInvoice files
- → Không conflict với Wave 2, có thể làm song song

---

## 5. WAVE 4 — EInvoice UI + E2E (sau Wave 2 + Wave 3)

**Branch:** `fix/einvoice-cleanup` (tiếp tục) hoặc branch mới từ `main`
**Estimated sessions:** 2 (Phase C = 1 session, Phase D = 1 session)
**Conflict risk:** LOW (new files only)

### Tasks (sequential — Phase D cần Phase C)
| # | Task ID | Task | Depends on | Task card |
|---|---|---|---|---|
| 9 | P0-6c | EInvoice cleanup Phase C — 6 Razor pages | Phase B controller + Phase 2 auth | task-einvoice-deadcode-cleanup.md Phase C |
| 10 | P0-6d | EInvoice cleanup Phase D — 3 Playwright specs | Phase C pages + Phase 2 E2E auth | task-einvoice-deadcode-cleanup.md Phase D |

### Entry criteria
- [ ] Wave 2 merged (controller exists)
- [ ] Wave 3 merged (auth pattern stable)
- [ ] Branch from `main`

### Exit criteria Phase C
- [ ] SC8-SC10 pass: 6 Razor pages với VanAn components, mobile-first responsive
- [ ] Build pass

### Exit criteria Phase D
- [ ] SC11-SC13 pass: 3 Playwright specs test real UI flow, re-enable E2E in CI
- [ ] `project_state.md` updated + committed
- [ ] Merge to `main`

### Why here
- UI pages cần controller endpoint (Phase B) + auth policy (Phase 2)
- Playwright specs cần pages (Phase C) + E2E auth setup (Phase 2 dev login endpoint)

---

## 6. WAVE 5 — Remaining Backlog (sau tất cả)

**Branch:** Theo task cụ thể
**Estimated sessions:** Variable
**Conflict risk:** LOW (dependent trên tất cả waves trước)

### Tasks (priority order, có thể parallel)
| # | Task ID | Task | Depends on |
|---|---|---|---|
| 11 | P0-2 | E2E false-positive specs (T-17/18/19/21) | — (isolated) |
| 12 | P0-4 | AccountCode not saved | Phase 2 (tenant pattern) |
| 13 | P0-5 | Entry timing (CreateOrder→PaymentWebhook) | Phase 2 |
| 14 | P1-1 | E2E auth global-setup | Phase 2 (dev login) |
| 15 | P1-2 to P1-5 | Various | various |
| 16 | P2-1 to P2-5, P3-1 to P3-3 | Various | various |

---

## 7. FILE CONFLICT MATRIX (tại sao thứ tự này)

| File zone | Wave 0 | Wave 1 | Wave 2 | Wave 3 | Wave 4 | Conflict mitigation |
|---|---|---|---|---|---|---|
| `TenantProvider.cs` | — | ✅ Phase 1+2 | — | — | — | Sequential Phase 1→2 |
| Gateway controllers | — | ✅ Phase 1+2 | ✅ Phase A+B | ✅ Phase 3 (Orders) | — | Wave 1 trước Wave 2 |
| `VanAnDbContext.cs` | — | ✅ Phase 1+2 | — | — | — | Isolated trong Wave 1 |
| `Program.cs` (Gateway) | — | ✅ Phase 2 | ✅ Phase B DI | — | — | Wave 1 trước Wave 2 |
| Accounting Razor pages | — | ✅ Phase 2 auth | — | ✅ Phase 4 refactor | — | Wave 1 trước Wave 3 |
| `WebhookController.cs` | — | ✅ Phase 2 auth | ✅ Phase A fix | — | — | Wave 1 trước Wave 2 |
| KhachLink pages/hubs | — | — | — | ✅ Phase 3 | — | Isolated trong Wave 3 |
| EInvoice Razor pages | — | — | — | — | ✅ Phase C (new) | New files, no conflict |
| Test files | ✅ Wave 0 | ✅ Phase 2 tests | ✅ Phase A tests | ✅ Phase 3-4 tests | ✅ Phase D specs | Separate files |
| CI workflows | — | — | — | — | ✅ Phase D re-enable | Isolated |

---

## 8. VISUAL TIMELINE

```
Week 1:  [Wave 0: tests + dashboard] ──→ merge
              │
Week 2:  [Wave 1: TenantId Phase 1→2] ──→ merge
              │
Week 3:  ┌────[Wave 2: EInvoice Phase A→B] ──→ merge
         │         │
         └────[Wave 3: TenantId Phase 3→4] ──→ merge (parallel)
                   │
Week 4:  [Wave 4: EInvoice Phase C→D] ──→ merge
                   │
Week 5+: [Wave 5: remaining backlog]
```

---

## 9. SESSION CHECKLIST (cho mỗi session)

### Before session start
- [ ] `load-context` skill → đọc `project_state.md`
- [ ] Đọc master plan này → xác định wave hiện tại
- [ ] Đọc task card của wave hiện tại
- [ ] Verify branch đúng
- [ ] Verify wave trước đã merged (git log)

### During session
- [ ] JIT Planning: đọc boundary files 1 lần, chốt file cần sửa/tạo
- [ ] Pure Execution: viết code, không re-explore
- [ ] Run build + tests sau mỗi micro-phase

### Before session end
- [ ] Wave SC pass HOẶC context gần đầy
- [ ] `dotnet build VanAn.sln --configuration Release` → 0 errors
- [ ] `guard-check.ps1` → PASS
- [ ] Update `project_state.md` (Section 4 + 10 + 11)
- [ ] Commit với message format: `<type>(<wave>): <description>`
- [ ] Nếu wave hoàn tất: merge to `main` + verify build trên `main`

---

## 10. ROLLBACK PLAN

Nếu wave fail/conflict không resolve:
1. **STOP** — không cố fix tiếp
2. `git stash` uncommitted changes
3. `git checkout main` — về baseline sạch
4. Document failure trong `project_state.md` Section 7 (Known Risks)
5. Tạo task card mới cho retry với approach khác
6. Không sang wave kế cho đến khi wave hiện tại resolve

---

## REFERENCES
- TenantId cards: `task-tenantid-phase1-stop-bleeding.md`, `task-tenantid-phase2-tenant-foundation.md`, `task-tenantid-phase3-khachlink-tenant.md`, `task-tenantid-phase4-cleanup.md`
- EInvoice cards: `task_sprint3_einvoice.md` (SUPERSEDED), `task_sprint3b_provider_integration.md`, `task-einvoice-deadcode-cleanup.md`
- Guard check: `task-upgrade-guard-check.md`
- Consolidated backlog: `project_state.md` Section 4
