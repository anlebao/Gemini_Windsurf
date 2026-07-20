# Task Card: Phase 7 — Verification + Governance + State Update

> **Master plan:** `gateway_router_multi_vps_master_plan.md`
> **Workflow:** `newfeaturebuild.md` (final phase — verification + deployment)
> **Phase:** 7 of 8 (originally 7 of 7 — Phase 8 Multi-VPS E2E added per Round 2)
> **Depends on:** Phases 1, 2, 3, 3.5, 4, 5, 3.6, 6 ALL COMPLETE ✅

---

## 1. Use Case & Business Design

**Problem:** After Phases 1-6 + 3.6, code is implemented + deployed + RV-verified on VPS, but:
1. Governance documents still describe Option B (Monolithic in-process) — must update to Option C (Router + Multi-VPS).
2. `project_state.md` already partially updated during each phase — needs final consolidation pass.
3. ADR-001 must be updated with v3 addendum (Option C supersedes Option B).
4. End-to-end multi-tenant checkout already verified on VPS (Phase 5 RV 9/9 + Phase 6 RV 8/8) — Phase 7 re-confirms no regressions.
5. Payment webhook known issue (Phase 3 Q4) — RESOLVED per user decision 2026-07-18 (order stays in PG, webhook unchanged). Document as closed.
6. NATS sync dead code (commented `SyncProductUpsertAsync`) — document as tech debt for future cleanup.
7. Phase 8 (Multi-VPS E2E) is the next phase after Phase 7 — placeholder task card needed.

**Goal:** Production-ready state with updated governance docs, ADR-001 v3 addendum, final verification pass, and clear handoff to Phase 8 (Multi-VPS E2E).

**Out of scope:** Payment webhook refactor (RESOLVED — no work needed), Playwright E2E suite (Phase 8), multi-VPS production rollout (post-Phase 8).

**Note:** VPS is already deployed + healthy (vanan-gateway + vanan-shoperp + vanan-khachlink all healthy as of 2026-07-20). Phase 7 is primarily documentation + final verification — NOT a re-deploy.

---

## 2. Reverse Impact Analysis

### Documentation
- **`.devin/rules/governance.md`** — UPDATE:
  - "Gateway operates in MONOLITHIC MODE (Option B approved 2026-07-05)" → "Gateway operates in ORDER CREATOR + ROUTED ASYNC DELIVERY MODE (Option C approved 2026-07-18 — supersedes Option B). Gateway PG is source of truth for Orders + Accounting + Tenants + ShopInstances + Users. Products live in ShopERP per-tenant SQLite. Orders async-delivered to ShopERP via NATS (routed by ShopInstanceId) for kitchen/POS display. Multi-VPS supported via ShopInstances routing table. Client (KhachLink) provides ProductName + VatRate snapshot at checkout — Gateway does NOT query Products table."
  - Update "Critical Architectural Boundaries → Data Flow" diagram:
    ```
    KhachLink (5002) → Gateway (5001, order creator) → NATS (routed) → ShopERP-A/B/C... (per-tenant SQLite)
    Customer-facing     PG: Orders + Accounting        Subject:             Kitchen/POS display
                        + Tenants + ShopInstances      vanan.cloud.order.   (replica, not source)
                        + Auth                          created.{shopInstId}
    ```
  - Note: Gateway PG no longer receives product sync (DataSyncSubscriber product cases disabled). Order sync direction is PG→SQLite (routed by ShopInstanceId). Order status sync SQLite→PG kept for kitchen/POS updates.
- **`docs/Architecture/ADR-001-Station-Architecture.md`** — ADD v3 addendum section:
  - Date: 2026-07-18
  - Decision: Option C (Router) supersedes Option B (Monolithic in-process).
  - Rationale: Multi-VPS deployment requirement. Option B assumed co-located Gateway + ShopERP. Production reality is N ShopERP instances on separate VPS.
  - Trade-offs: Gateway becomes stateless for product/order (good for scaling). Loses low-latency in-process access (acceptable — HTTP forward adds ~5-10ms per checkout, well within UX budget).
- **`docs/AI/project_state.md`** — UPDATE:
  - Section 2 (Current Objective): mark Phase 1-7 complete, set next objective.
  - Section 3 (Current Status): add "Gateway Router Option C — COMPLETE (2026-07-18)".
  - Section 4 (Next Actions): remove completed items, add new (payment webhook refactor, Playwright E2E validation, multi-VPS production rollout).
  - Section 6 (Completed Streams): add "Gateway Router Option C Multi-VPS" entry.
  - Section 11 (Maintenance Log): update Last Updated + branch.
- **NEW: `docs/AI/tasks/playwright_e2e_gateway_router_validation_task_card.md`** — placeholder for post-implementation Playwright validation. Per governance, Playwright runs AFTER implementation completes, not during.
- ~~`payment_webhook_option_c_migration_task_card.md`~~ — **NOT NEEDED** (per user decision 2026-07-18, order stays in PG, webhook unchanged).

### Verification
- **`guard-check.ps1`** — run, must PASS.
- **`dotnet build VanAn.sln`** — 0 errors.
- **Full test suite:** `dotnet test` across all test projects. No regressions. New tests from Phases 1-6 all pass.
- **Migration dry-run on VPS:** backup PG → run migration → verify table created + backfill → if fails, restore from backup.

### Production Deploy
- **Deploy steps (documented runbook):**
  1. SSH to VPS: `ssh -i "C:\VibeCoding\CD\SSH\vanan.pem" ubuntu@161.118.212.110`
  2. Backup Gateway PG: `docker exec vanan-postgres pg_dump -U vanan_admin VanAnCoreHub > /tmp/vanan_pg_backup_$(date +%Y%m%d).sql`
  3. Pull latest code (CD pipeline triggers on main branch push).
  4. Verify CD pipeline success.
  5. Run migration (auto-applied on Gateway startup via `MigrateAsync` — verify logs show "AddShopInstancesAndTenantFk applied").
  6. Verify `\d "ShopInstances"` exists + 1 row seeded + all tenants backfilled.
  7. Smoke test 1: KhachLink checkout 1 product from tenant A → 1 order created in SQLite (verify via `sqlite3 /tmp/vanan_shoperp.db "SELECT count(*) FROM Orders WHERE ..."`).
  8. Smoke test 2: KhachLink checkout 2 products from tenant A + 1 product from tenant B → 2 orders created, both in SQLite.
  9. Smoke test 3: `/admin/shop-instances` page loads, shows 1 instance "Healthy".
  10. Smoke test 4: `/admin/tenants` page loads, shows ShopERP Instance column.
- **Rollback plan:** If smoke tests fail, restore PG from backup + revert code to previous commit. Document failure in `project_state.md`.

### Tech Debt Documentation
- **Payment webhook:** **RESOLVED per user decision 2026-07-18** — order stays in Gateway PG, webhook loads from PG as before. No new task card needed. (Original Phase 3 Q4 concern was based on pure-router architecture, which was superseded.)
- **NATS sync cleanup:** Phase 3 commented out `SyncProductUpsertAsync` on Gateway's DataSyncSubscriber. Phase 7 should remove dead code entirely OR leave commented with clear TODO. **Decision: leave commented for one release cycle, then remove.** Document in tech debt.
- **Order routing index (Phase 3 Q1):** if Q1 option (a) was chosen (KhachLink stores tenantId), no Gateway `OrderRoutingIndex` table needed. If option (b) was chosen, table must be created — fold into Phase 7 or separate task card.

---

## 3. Detailed Coding Plan

### Implementation Steps
**Step 1 — Update governance.md (1 modified file):**
- Update Option B → Option C language.
- Update data flow diagram.
- Update NATS sync direction note.
- Commit: `[GOVERNANCE] Update Gateway from Option B (Monolithic) to Option C (Router) — multi-VPS support`.

**Step 2 — Add ADR-001 v3 addendum (1 modified file):**
- Append v3 section to `docs/Architecture/ADR-001-Station-Architecture.md`.
- Date + decision + rationale + trade-offs.

**Step 3 — Create placeholder task cards (2 new files):**
- `payment_webhook_option_c_migration_task_card.md` (PLANNING status).
- `playwright_e2e_gateway_router_validation_task_card.md` (PLANNING status).

**Step 4 — Run full verification suite:**
- `dotnet build VanAn.sln`
- `dotnet test` (all test projects)
- `./guard-check.ps1`
- All must pass. If failures, fix in respective phase or document as debt.

**Step 5 — Update project_state.md (1 modified file):**
- Section 2: mark complete, set next objective.
- Section 3: add Gateway Router Option C entry.
- Section 4: next actions.
- Section 6: completed stream entry.
- Section 11: maintenance log stamp.
- Commit: `Update project_state.md — 2026-07-18 Gateway Router Option C multi-VPS complete`.

**Step 6 — VPS deploy (runbook execution):**
- Follow deploy steps in §2.
- Document results (success/failure) in this task card.
- If smoke tests fail, execute rollback plan.

**Step 7 — Final report to user:**
- Summary of what changed.
- Production status (healthy/degraded).
- Known issues (payment webhook).
- Next recommended work (payment webhook refactor, Playwright validation, multi-VPS rollout).

### Active Skills
- `system-refactor-safety` (final verification of architectural shift)
- `domain-integrity-validation` (final Domain layer review)
- `test-system-upgrade` (full test suite run)

---

## 4. Validation Gates

| Gate | Command | Expected |
|---|---|---|
| Build | `dotnet build VanAn.sln` | 0 errors |
| All tests | `dotnet test` | No regressions, all new tests pass |
| Guard check | `./guard-check.ps1` | PASS |
| Governance | Read `.devin/rules/governance.md` | Option C documented |
| ADR | Read `ADR-001` | v3 addendum present |
| VPS migration | `psql` queries | Table exists, 1 ShopInstance, all tenants backfilled |
| VPS smoke 1 | KhachLink checkout 1 product | 1 order in SQLite |
| VPS smoke 2 | KhachLink checkout 2 tenants | 2 orders in SQLite |
| VPS smoke 3 | `/admin/shop-instances` | Page loads, 1 Healthy instance |
| VPS smoke 4 | `/admin/tenants` | New column shows correct instance |

---

## 5. Deliverables

- Modified: `.devin/rules/governance.md`
- Modified: `docs/Architecture/ADR-001-Station-Architecture.md` (v3 addendum)
- Modified: `docs/AI/project_state.md`
- New: `docs/AI/tasks/payment_webhook_option_c_migration_task_card.md` (placeholder)
- New: `docs/AI/tasks/playwright_e2e_gateway_router_validation_task_card.md` (placeholder)
- VPS deploy runbook executed + documented results.

---

## 6. Approval Gate

**Phase 7 is documentation + final verification — no production deploy needed (VPS already deployed + healthy per Phase 6 RV 8/8).**
- [x] All Phases 1, 2, 3, 3.5, 4, 5, 3.6, 6 marked COMPLETE
- [ ] All verification gates PASS (build + tests + guard-check)
- [ ] Governance + ADR + project_state updated
- [ ] User acknowledges payment webhook issue RESOLVED (no work needed)
- [ ] User acknowledges NATS sync dead code as tech debt (future cleanup)
- [ ] User approves Phase 8 (Multi-VPS E2E) as next phase

---

## 7. Post-Phase 7 (Future Work — NOT in this master plan)

1. **Phase 8 — Multi-VPS E2E validation** — `phase8_multi_vps_e2e_task_card.md` (placeholder — to be created in Phase 7). Playwright E2E suite for full multi-tenant checkout flow across multiple ShopERP instances.
2. **Multi-VPS production rollout** — when first real customer needs separate VPS. Requires deploying additional ShopERP VPS instances with distinct `SHOP_INSTANCE_ID` env vars + creating `ShopInstance` rows in Gateway PG via admin UI (Phase 6 `/admin/shop-instances` page).
3. **NATS sync dead code cleanup** — remove commented `SyncProductUpsertAsync` after one release cycle. Document as tech debt in Phase 7.
4. **ShopInstance auto-provisioning** — when creating new ShopInstance, auto-spin VPS via Terraform/Docker. Future.
5. **Order status bidirectional sync review** — verify `SyncOrderStatusAsync` + `SyncOrderCompletedAsync` still needed once kitchen/POS workflows are re-audited under Option C.
6. **CustomerRecommendationService retirement** — Phase 6 added `CatalogController` as replacement. Mark `CustomerRecommendationService` as `[Obsolete]` or delete after Phase 8 E2E verifies `CatalogController` works end-to-end.

---

## 8. COMPLETION SUMMARY

**Phase 7 COMPLETE** — commit `<PENDING>` on `main`.

### Files created (2 new)
| File | Purpose |
|------|---------|
| `docs/AI/tasks/phase8_multi_vps_e2e_task_card.md` | Phase 8 placeholder — 7 Playwright E2E scenarios for multi-VPS checkout validation |
| `docs/AI/tasks/tech_debt_multi_vps_checkout.md` | Tech debt register — 4 active items (NATS dead code, CustomerRecommendationService, Integration.Tests infra, UserTenant mapping) + 1 resolved (payment webhook) |

### Files modified (4)
| File | Change |
|------|--------|
| `.devin/rules/governance.md` | Option B → Option C language (Gateway is Order Creator + Routed Async Delivery, PG source of truth, multi-VPS) |
| `.windsurfrules` | Reference copy updated — Option C language + new data flow diagram (KhachLink → Gateway PG → NATS routed → ShopERP-A/B/C) |
| `docs/Architecture/ADR001-Station-Architecture.md` | v3 addendum appended — Option C decision, rationale, trade-offs table, all 8 implementation phases with commits + VR results, tech debt references |
| `docs/AI/project_state.md` | Section 2 status + Section 3 last commit + Section 4 next actions (Phase 8) + Section 6 completed streams + Section 9 maintenance log |

### Issues fixed during implementation
- None — Phase 7 is documentation + verification only (no code changes).

### Verification

#### Static Verification (compile-time)
- **Build:** 0 errors (no code changes — build unchanged from Phase 6)
- **Unit tests:** Core.Tests 1044/0/16 PASS. Architecture 38/38 PASS.
- **guard-check.ps1:** ALL PASSED (untracked files, windsurf-guard, architecture-guard, Roslyn analyzers, build, fast test gate, CircuitBreaker integration gate)
- **Integration.Tests:** CircuitBreaker 6/6 PASS (guard-check gate). 43 pre-existing failures require full local app stack (Gateway + ShopERP + KhachLink running) — confirmed NOT Phase 7 regressions via git stash comparison (same 43 failures before and after Phase 7 doc changes). Documented as TD-MVPS-003.

#### Live Runtime Verification
> Phase 7 is documentation + verification only — no VPS re-deploy needed.
> VPS already deployed + healthy per Phase 6 RV 8/8 (2026-07-20):
> - vanan-gateway + vanan-shoperp + vanan-khachlink all healthy
> - CatalogController 200 OK, FeaturedProductsController 401 (auth required), ShopInstancesController 401
> - FeaturedProducts table exists in PG, KhachLink Home.razor 200, ShopERP admin 302
> - Phase 5 regression: Gateway health 200 + products forwarding 200 (16456 bytes)

| # | Test | Status | Evidence |
|---|------|--------|----------|
| RV1 | Core.Tests full suite | PASS | 1044 passed, 0 failed, 16 skipped |
| RV2 | Architecture.Tests full suite | PASS | 38 passed, 0 failed |
| RV3 | guard-check.ps1 | PASS | ALL CHECKS PASSED (untracked + windsurf-guard + architecture-guard + Roslyn + build + fast test gate + CircuitBreaker gate) |
| RV4 | Integration.Tests CircuitBreaker | PASS | 6 passed (guard-check gate subset) |
| RV5 | Integration.Tests full suite (pre-existing) | KNOWN | 163 passed, 43 failed (require full local app stack — TD-MVPS-003, not Phase 7 regressions) |
| RV6 | VPS health (Phase 6 RV carryover) | PASS | All 3 containers healthy (vanan-gateway + vanan-shoperp + vanan-khachlink) |
