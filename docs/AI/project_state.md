# Project State

> **Mục đích:** Single Source of Truth cho AI về trạng thái dự án. BẮT BUỘC đọc đầu mỗi phiên.
> **Archived:** 2026-07-26 — All completed objectives + full history/maintenance log moved to `docs/AI/project_state_archive.md`

---

## 0. Maintenance Rules

1. One-and-only-one: Mỗi section chỉ tồn tại 1 lần.
2. No contradiction: Một hạng mục chỉ có 1 trạng thái.
3. Ground Truth first: Verify path/branch với codebase trước khi ghi.
4. Now over History: Section 2-4 chỉ mô tả việc ĐANG làm và KẾ TIẾP. Việc xong gom vào archive.
5. Actionable Next Actions: Xóa action đã quá hạn/sai bối cảnh.
6. Stamp every edit: Cập nhật Section 9 mỗi lần sửa.

---

## 1. Project Overview

**Dự án:** Vạn An Accounting System MVP — giải pháp kế toán HKD theo TT 152/2025/TT-BTC.
**Stack:** .NET 8 — EF Core — SQLite — Blazor Server (ShopERP) — Blazor WebAssembly (KhachLink PWA) — SignalR — YARP Gateway — xUnit — Playwright.
**Kiến trúc:** Clean Architecture + DDD + Multi-tenancy. Data flow: `KhachLink WASM (5002) -> Gateway (5001) -> ShopERP (5003) -> SQLite`.

**Modules:** `1_Shared` (Domain + Services contracts) — `2_Gateway` (YARP) — `3_CoreHub` (Services, in-process) — `5_WebApps/ShopERP` (Blazor Server) — `5_WebApps/KhachLink` (Blazor WASM, served by nginx) — `UI.Platform` (Shared components) — `6_Tests`.

**Hard stops:** Domain PURE — `AccountingEntry` immutable — Gateway = Order Creator + Routed Async Delivery (Option C) — KhachLink HTTP-only — ShopERP SQLite (Business) + PostgreSQL (Accounting) — ALWAYS dùng UI Platform components.

---

## 2. Current Objective

**Community Commerce Sprint 0 — Foundation (COMPLETE 2026-07-26)**

11 Domain entities + 9 enums + 8 Order fields + IdentityLevel expansion + 11 EF configurations + 11 DbSets + RiskScoringService + WalletService base + FingerprintJS stub + 42 unit tests + 2 architecture tests. All COMPLETE, merged to main, VPS deployed, RV 18/18 PASS.

Branch: `feature/community-sprint0-foundation` (merged to `main`). Commits: `e1a75bbf` + `f563e415`.

---

## 3. Current Status

- **Branch:** `main`
- **Last commit:** `f563e415` docs(state): Sprint 0 COMPLETE
- **.NET SDK:** 8.0.422
- **DB:** SQLite `vanan_shoperp.db` (business) + PostgreSQL `VanAnCoreHub` (accounting + Gateway + Community tables)
- **Build (2026-07-26):** 0 errors, 1 warning. guard-check ALL PASSED. 1009 unit + 39 arch tests PASS. CD #30201482750 ALL PASS. VPS RV 18/18 PASS.
- **Sprint 0 (2026-07-26 COMPLETE + MERGED + DEPLOYED):** 11 entities + 42 tests + migration `20260726105331_CommunitySprint0` applied to local + VPS PG. RiskScoringService (8-factor deterministic) + WalletService base (atomic SELECT FOR UPDATE). FingerprintJS stub vendored.
- **VPS:** Live at `diemthuong.khachvip.online` (KhachLink), `app.khachvip.online` (ShopERP), `api.khachvip.online` (Gateway). 7 containers healthy. CD deploys automatically on push to main.
- **Local infra:** Docker PostgreSQL 15-alpine (5432) + NATS 2-alpine (4222) + ShopERP 5003 + KhachLink 5002 + Gateway 5001.
- **Tech debt:** TD-MVPS-001 through TD-MVPS-004 (see `docs/AI/tasks/tech_debt_multi_vps_checkout.md`). TD-PWA-001 (WASM conversion complete). Tier 5 — True Offline Edge (post-PoC).

---

## 4. Next Actions

1. **Community Commerce Sprint 1 — Nearby Orders** — per `task_cc_sprint1_nearby_orders-2c5017.md` + `sprint1_nearby_orders_detailed_plan-2c5017.md`. Requires Domain Modification #2: OrderStatuses.Default[] + "delivering" status + OrderWorkflowService transitions. **Needs user approval for Domain Modification.**
2. **Replace FingerprintJS stub** — Download real FingerprintJS v4 (MIT) before production deployment.
3. **Sprint 7+ Edge Migration** — trigger khi >1M users. 15 tasks. See master plan Section 13.
4. **Phase 8 — Multi-VPS E2E Validation (Playwright)** — per `phase8_multi_vps_e2e_task_card.md`. 7 E2E scenarios.
5. **Tech debt cleanup** — TD-MVPS-001 through TD-MVPS-004.
6. **(Cosmetic)** Fix `?` in Checkout.razor (18/63 lines). Fix `isTabVisible` display bug in OrderTracking.razor.
7. **(Env)** Fix local DB role mismatch — ShopERP `vanan_admin` vs Gateway `vanan_dev`.

---

## 5. Active Architecture Decisions

| Decision | Lý do |
|---|---|
| Gateway = Order Creator + Routed Async Delivery (Option C) | Multi-VPS support, PG source of truth, NATS routed by ShopInstanceId |
| CoreHub = in-process background service trong Gateway | Monolith Phase 1-2 |
| ShopERP = SQLite (Business) + PostgreSQL (Accounting) | ADR-001: accounting always online |
| CustomerToken = `IDataProtector` | Tránh library mới |
| `AccountingEntry` immutable, Reversal Entry | Audit trail bắt khu xâm phạm |
| Multi-tenancy `TenantId` filter mọi layer | Data isolation per HKD |
| EF Core Migrations = official schema management | Stream E |
| Dual Deployment Modes: SaaS (all-in-one) + Edge (tách biệt) | See Section 5a |

### 5a. Deployment Modes

**SaaS:** `docker-compose.prod.yml` — all modules on 1 VPS. Gateway → PG. ShopERP → SQLite. KhachLink → Gateway (HTTP).

**Edge:** `docker-compose.edge.yml` — Server A (Edge): ShopERP + SQLite + NATS sync. Server B (Central): Gateway + PG + KhachLink. Sync via NATS Outbox.

---

## 6. History Log (compressed — see archive + git log)

* [2026-07-26] **SPRINT 0 COMPLETE.** 11 entities + 42 tests + migration. Merged + deployed. RV 18/18.
* [2026-07-26] **DOC v1.4-v1.1 COMPLETE.** 4 doc-only sessions. Hybrid architecture + cost + review fixes + anti-fraud.
* [2026-07-24] **LOYALTY L-C COMPLETE.** RV 57/57. Gamification + config UI + notification jobs.
* [2026-07-24] **LOYALTY L-B COMPLETE.** RV 13/13. Redemption system.
* [2026-07-24] **LOYALTY L-A + PHASE 5 PUSH COMPLETE.** Configurable formula + push notifications.
* [2026-07-23] **PRODUCT PICKER + ORDER STATUS UNIFICATION.** RV 4/4.
* [2026-07-23] **FONT FIX + FREEZE FIX.** Double-encoding + IAsyncDisposable.
* [2026-07-22] **THEME + PWA PHASES 1-3.** 5 themes. Blazor Server → WASM. Offline caching.
* [2026-07-20] **MULTI-VPS OPTION C PHASES 1-7 COMPLETE.** ShopInstance + Order Creator + NATS routed.
* [2026-07-18] **MULTI-TENANT BUG FIX + QUICK-SETUP REAL.** 5 commits.
* [2026-07-17] **SINGLE-IDENTITY REFACTOR COMPLETE.** All 5 entities. VPS verified.
* [2026-07-16] **UUIDv7 REFACTOR + DATA SYNC HARDENING.**
* [2026-07-15] **ORDER SYNC TRACK E1 COMPLETE.** Option D. RC-1/2/3 fixed.
* [2026-07-14] **KHACHLINK E2E VPS PASS + UI/UX FIX BATCH.**
* [2026-07-13] **TIERED AUTH P1-P3 RV COMPLETE.** 14/14.
* [2026-07-09-10] **ACCOUNTING POSTGRESQL ONLINE.** 3 waves. 1223/1223.
* **Older:** See `docs/AI/project_state_archive.md`.

---

## 7. Active Files Reference

| File | Role |
|---|---|
| `docs/AI/tasks/task_cc_sprint0_foundation-2c5017.md` | Sprint 0 task card (COMPLETE) |
| `docs/AI/tasks/task_cc_sprint1_nearby_orders-2c5017.md` | Sprint 1 task card (NEXT) |
| `docs/AI/tasks/sprint1_nearby_orders_detailed_plan-2c5017.md` | Sprint 1 detailed plan |
| `docs/AI/tasks/community-commerce-master-plan-2c5017.md` | Community Commerce master plan |
| `docs/AI/tasks/community-commerce-requirements-spec-2c5017.md` | Requirements spec v1.4 |
| `docs/AI/tasks/tech_debt_multi_vps_checkout.md` | Tech debt register |
| `docs/Architecture/ADR001-Station-Architecture.md` | ADR-001 v3 (Option C) |
| `docs/AI/project_state_archive.md` | Archived history |

---

## 8. Architecture Quick Reference

```
=== SaaS Mode (docker-compose.prod.yml) ===
KhachLink (5002) → Gateway (5001) → ShopERP (5003) → SQLite (local)
                       ↓
              [in-process CoreHub]
                       ↓
                  PostgreSQL (central)

=== Edge Mode (docker-compose.edge.yml) ===
Server A (Edge):              Server B (Central):
  ShopERP → SQLite              Gateway → PostgreSQL
  NATS sync worker              [in-process CoreHub]
       ↓ NATS ↓
  ---------------→ Gateway
                   KhachLink → Gateway (HTTP)
```

**Auth:** Cookie (Blazor Server) + JWT Bearer (API). `DevLoginController` (`#if DEBUG`) for E2E.
**Roles:** `UserRole` (tenant-scoped) + `PlatformRole` (cross-tenant: SystemAdmin).

---

## 9. AI Health Check

- **Assumptions:** 0
- **Verified Facts:** Branch=main, Build=0 errors, 42 tests PASS, CD PASS, RV 18/18 PASS, 7 containers healthy
- **Open Questions:** 0
- **Gate 6 Status:** ✅ Assumptions < Verified Facts, Open Questions < 3

---

## 10. Maintenance Log

* **2026-07-26 — PROJECT STATE ARCHIVED.** Reduced from 627 → ~170 lines. All Previous Objectives (Doc v1.1-v1.4, Phase 5, Loyalty L-A/L-B/L-C, Product Picker, Font/Freeze Fix, Theme, PWA Phases 1-3, Multi-VPS Option C) + full History Log + full Maintenance Log moved to `docs/AI/project_state_archive.md` (Section "Archived 2026-07-26"). Branch: `main`.
* **2026-07-26 — SPRINT 0 REVIEW + PARTIAL FIX.** Review-only audit found 8 items marked COMPLETE but not 100% production. Part 1: F2/F4/F5a (dead code pending callers) added to correct downstream sprint task cards (Sprint 1: AssignShipper/SetDeliveryLocation; Sprint 4: AssignSalesman + RiskScoringService caller + WalletService app-install caller; Sprint 5: MarkCodCollected + WalletService COD/Advance/Settlement caller). Sprint 4 + Sprint 5 task cards fixed: WalletService "Files cần CREATE" → "MODIFY" (Sprint 0 đã tạo base). Part 2 in progress: F5b (WalletService SQLite comment fixed), F6 (migration scope note added to SC9), F7 (SC13 phrasing fixed). Branch: `main`.
