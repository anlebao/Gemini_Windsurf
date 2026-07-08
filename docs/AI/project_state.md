# Project State

> **Mục đích:** Single Source of Truth cho AI về trạng thái dự án. BẮT BUỘC đọc đầu mỗi phiên.
> **Archived:** 2026-07-08 — completed waves moved to `docs/AI/project_state_archive.md`

---

## 0. Maintenance Rules

1. One-and-only-one: Mỗi section chỉ tồn tại 1 lần.
2. No contradiction: Một hạng mục chỉ có 1 trạng thái.
3. Ground Truth first: Verify path/branch với codebase trước khi ghi.
4. Now over History: Section 2-4 chỉ mô tả việc ĐANG làm và KẾ TIẾP. Việc xong → gom vào Section 6.
5. Actionable Next Actions: Xóa action đã quá hạn/sai bối cảnh.
6. Stamp every edit: Cập nhật Section 9 mỗi lần sửa.

---

## 1. Project Overview

**Dự án:** Vạn An Accounting System MVP — giải pháp kế toán HKD theo TT 152/2025/TT-BTC.
**Stack:** .NET 8 · EF Core · SQLite · Blazor Server (ShopERP) · Blazor WebAssembly (KhachLink PWA) · SignalR · YARP Gateway · xUnit · Playwright.
**Kiến trúc:** Clean Architecture + DDD + Multi-tenancy. Data flow: `KhachLink (5002) → Gateway (5001) → ShopERP (5003) → SQLite`.

**Modules:** `1_Shared` (Domain) · `2_Gateway` (YARP) · `3_CoreHub` (Services, in-process) · `5_WebApps/ShopERP` (Blazor Server) · `5_WebApps/KhachLink` (Blazor WASM) · `UI.Platform` (Shared components) · `6_Tests/6_Testing`.

**Hard stops:** Domain PURE · `AccountingEntry` immutable · Gateway STATELESS · KhachLink HTTP-only · ShopERP SQLite-only · ALWAYS dùng UI Platform components.

---

## 2. Current Objective

**[PLATFORM SYSTEMADMIN — F1-F5 FIX COMPLETE ✅ + ACCESS MATRIX PLAN PLANNED 🟡]**

Post-implementation review (2026-07-08) phát hiện 5 deviations từ `platform_systemadmin_task_card.md`. F1-F5 fixed + verified:
- F1: `[AllowAnonymous]` on `Login` action — fix auth deadlock
- F2: Integration test `SeedPlatformUserAsync` idempotent — fix UNIQUE constraint fail
- F3: Unit tests re-created (5/5 PASS, SQLite in-memory) — fix deleted tests
- F4: `Seed:SysAdminPassword` config + production guard — fix hardcoded password
- F5: `AuditTrail.razor` `Roles="Admin"` → `Policy="SystemAdmin"` — fix role mismatch

**Access Matrix master plan** (`platform_systemadmin_access_matrix_master_plan.md`) created — 4 phases (ANALYZE→DESIGN→IMPLEMENT→VERIFY), 12 tasks, 5 EDR-AM rules. Awaiting user approval.

**Local CD Flow changes** still uncommitted on `main`.

---

## 3. Current Status

- **Branch:** `main`
- **Last commit (pre-fix):** `0748109` [PLATFORM-ADMIN] Add [Authorize] to PlatformUserLoginController
- **Next commit:** F1-F5 fix + docs (pending)
- **.NET SDK:** 8.0.422 (system path, CVEs patched, global.json pinned)
- **DB:** SQLite `vanan_shoperp.db` (local dev) · PostgreSQL (Docker `vanan-postgres`)
- **Tests (Debug):** 1174/1174 PASS (Core 957 + Arch 34 + Integration 183) — verified 2026-07-08 post F1-F5 fix
- **Completed streams (all merged to main):**
  - Platform SystemAdmin ✅ (commit `dde219e`) — F1-F5 fix + docs pending commit
  - Stream G: SaaS Production Hardening W0-W7 ✅ (W8 pending — final regression + tag)
  - Stream F: VAS Enterprise Reports W0-W9 ✅ (tag `saas-production-v1.0`)
  - Stream D: HKD Book Accounting Fix W0-W8 ✅
  - Stream C: ShopERP UI Fix W0-W6 ✅
  - Stream B: E2E Test Cleanup W0-W8 ✅
  - Order Lifecycle W-1→W5 + edge cases ✅
  - Bucket A: Guest Checkout + PostgreSQL migration ✅
  - E2E Fix: qr-payment-ui 6/6 PASS ✅

---

## 4. Next Actions

**Immediate:**
1. **Commit F1-F5 fix + docs** — 5 code files + 4 docs files + project_state.md (pending)
2. **Access Matrix Phase 1: ANALYZE** — khi user approve `platform_systemadmin_access_matrix_master_plan.md`

**Deferred:**
3. **Commit local CD flow changes** — 5 modified + 1 new file still uncommitted on `main`
4. **W8: Final Regression + Production Tag** — full regression + `saas-production-v1.0` tag
5. **W6-T2 (user-side):** Email Viettel + MISA for sandbox credentials (1-2 tuần bottleneck)
6. **W6-T6:** Staging integration tests — gated by `EINVOICE_STAGING_ENABLED=true`, blocked by W6-T2
7. **KhachLink→Gateway QR auth forwarding** — architectural, `QrPaymentModal.razor` needs JWT forwarding

---

## 5. Active Architecture Decisions

| Decision | Lý do |
|---|---|
| CoreHub = in-process background service trong Gateway | Monolith Phase 1-2 (Option B approved 2026-07-05) |
| Gateway = DI composition root cho CoreHub | Program.cs đăng ký CoreHub DbContext/Services |
| ShopERP = SQLite-only edge node | Edge deployment offline-first |
| CustomerToken = `IDataProtector` | Tránh library mới |
| `AccountingEntry` immutable, Reversal Entry | Audit trail bất khả xâm phạm |
| Multi-tenancy `TenantId` filter mọi layer | Data isolation per HKD |
| EF Core Migrations = official schema management | Stream E — replace `EnsureCreated` for production |
| HKD Data Source = Option A (query AccountingEntries directly) | Wave 0.5 — AccountingEntry is immutable SSoT |
| DOCX export = DocumentFormat.OpenXml + XLSX = EPPlus 7.6.1 | Wave 0 T9 — user approved |
| **[NEW] PlatformUser = Infrastructure entity (non-tenant)** | Precedent: AccountChartEntity — cross-tenant admin, no BaseEntity |
| **[NEW] Execution Discipline Rules (EDR)** | 8 EDR rules in `platform_systemadmin_master_plan.md` Section 7 — ràng buộc execution chống tái diễn deviations |
| **[NEW] Access Matrix = verification plan riêng** | `platform_systemadmin_access_matrix_master_plan.md` — 4 phases, 5 EDR-AM rules, depends on F1-F5 COMPLETE |

---

## 6. History Log (compressed — see git log + archive for details)

* [2026-07-08] **PLATFORM SYSTEMADMIN REVIEW + F1-F5 FIX** — Post-implementation review phát hiện 5 deviations. Fixed F1-F5: `[AllowAnonymous]` on Login (auth deadlock), integration test idempotent (UNIQUE constraint), unit tests re-created (5/5 PASS SQLite in-memory), `Seed:SysAdminPassword` config + production guard, AuditTrail `Policy="SystemAdmin"`. Updated master plan with EDR-1..EDR-8 (Execution Discipline Rules). Created Access Matrix master plan + task card (4 phases, 12 tasks, EDR-AM-1..EDR-AM-5). Build 0 errors Debug+Release. Tests: 1174/1174 PASS (Core 957 + Arch 34 + Integration 183). Pending commit.
* [2026-07-08] **PLATFORM SYSTEMADMIN IMPLEMENT COMPLETE** — Implemented T1-T9: PlatformUser entity (non-tenant Infrastructure entity), PlatformUserConfiguration, 3 DbContext DbSet registrations, EF Migration (AddPlatformUsersTable), PlatformUserLoginService (BCrypt verify + JWT mint), PlatformUserLoginController (POST /api/platform/login, production, no #if DEBUG), DI registration + 3 policy updates (OwnerOnly, StoreManagement, StaffOrAbove add SystemAdmin) + seed sysadmin@vanan.vn, unit + integration tests. Build 0 errors, guard pass. Commit `dde219e`.
* [2026-07-08] **PLATFORM SYSTEMADMIN PLANNING COMPLETE** — Investigated 2 role systems (`UserRole` tenant-scoped vs `PlatformRole` cross-tenant), `DevLoginController` (`#if DEBUG`), `DemoUser` (rejects `TenantId=Empty`). User chose pattern 2 lớp. Created master plan + task card (9 tasks, 12 files). Commit `792cc3f`.
* [2026-07-07] **SDK 8.0.422 + TRIAGE + E2E FIX + BUCKET A + W6 GOLDEN TESTS** — 14 commits total: SDK to system path (CVEs patched), 5 pre-existing issues triaged, qr-payment-ui 6/6 PASS (`24718b8`), guest checkout + PostgreSQL migration (`310f3da`+`8867dbc`), 21/22 golden tests PASS (`fd7b038`). See archive for details.
* [2026-07-05] **STREAM G W0-W7 + STREAM F W0-W9 COMPLETE** — SaaS hardening (Gateway Option B, secrets, package security, CI restore, UI tests, period closing, e-invoice rewrite, tech debt, Docker hardening). VAS reports (10 waves, 4 BCTC, 124 accounts, feature flag, conversion service). All merged to main. 1152/1152 tests PASS.
* [2026-07-04] **STREAM D W0-W8 + STREAM E COMPLETE** — HKD Book Accounting Fix (12 waves, TT 152 compliance, 7 templates, DOCX/XLSX export). DB Migration Strategy (EF Core Migrations enabled). All merged to main.
* [2026-07-03] **STREAM B W0-W8 + STREAM C W0-W6 COMPLETE** — E2E Test Cleanup (8 waves, 7 anti-patterns). ShopERP UI Fix (6 waves, 23 .razor files, UI Platform compliance). All merged to main.
* [2026-07-02] **ORDER LIFECYCLE + ONBOARDING + PLANNING** — Order Lifecycle W-1→W5 merged. Tenant Onboarding 6 waves merged. ShopConfig Refactor 3 phases. EInvoice + UI Fix + E2E Cleanup planning.
* **Older:** See `docs/AI/project_state_archive.md` for full history (Wave 8-16, auth, CI/CD, etc.)

---

## 7. Active Files Reference

### Stream G (SaaS Hardening)
| File | Role |
|---|---|
| `docs/AI/tasks/saas_production_hardening_master_plan.md` | Master plan (W0-W8, 3 sprints) |
| `docs/AI/tasks/saas_w{0-8}_task_card.md` | 9 task cards |

### Stream F (VAS Reports)
| File | Role |
|---|---|
| `docs/AI/tasks/vas_enterprise_reports_master_plan.md` | Master plan (W0-W9, COMPLETE) |

---

## 8. Architecture Quick Reference

```
KhachLink (5002) → Gateway (5001) → ShopERP (5003) → SQLite
                        ↓
              [in-process CoreHub services]
                        ↓
                  PostgreSQL (prod) / SQLite (edge)
```

**Docker (prod):** postgres · nats · seq · gateway · shoperp · khachlink · nginx · certbot
**Docker (edge):** postgres · nats · gateway · shoperp · shoperp-nats-sync
**CoreHub:** NOT a Docker service — runs in-process inside Gateway.

**Auth:** Cookie (Blazor Server) + JWT Bearer (API). `DevLoginController` (`#if DEBUG`) for E2E. BCrypt work factor 12.
**Roles:** `UserRole` (tenant-scoped: Owner/StoreKeeper/Guard/Staff/Masterchef) · `PlatformRole` (cross-tenant: SystemAdmin)

---

## 9. Maintenance Log

* **2026-07-08 — PLATFORM SYSTEMADMIN REVIEW + F1-F5 FIX.** Post-implementation review: 5 deviations found. Fixed F1-F5 (AllowAnonymous, idempotent test, unit tests re-created, config password, AuditTrail role). Master plan updated with EDR-1..EDR-8. Access Matrix master plan + task card created (4 phases, 12 tasks). Build 0 errors Debug+Release. Tests: 1174/1174 PASS (Core 957 + Arch 34 + Integration 183). Pending commit. **Branch:** `main`.

* **2026-07-08 — PLATFORM SYSTEMADMIN IMPLEMENT COMPLETE.** Implemented T1-T9: PlatformUser entity (non-tenant Infrastructure entity), PlatformUserConfiguration, 3 DbContext DbSet registrations, EF Migration (AddPlatformUsersTable), PlatformUserLoginService (BCrypt verify + JWT mint), PlatformUserLoginController (POST /api/platform/login, production, no #if DEBUG), DI registration + 3 policy updates (OwnerOnly, StoreManagement, StaffOrAbove add SystemAdmin) + seed sysadmin@vanan.vn, unit + integration tests. Build 0 errors, guard pass. Commit `dde219e`. **Branch:** `main`.

* **2026-07-08 — PROJECT STATE ARCHIVED.** Reduced `project_state.md` from 528→~200 lines. Moved completed waves (Stream G W0-W7, Stream F W0-W9, Stream D W0-W8, Stream C W0-W6, Stream B W0-W8, Order Lifecycle, Bucket A, E2E Fix, Golden Tests, older waves) to `docs/AI/project_state_archive.md`. Kept: current objectives, active decisions, next actions, recent history (2026-07-02 onward). **Branch:** `main`.

* **2026-07-07 — SDK 8.0.422 + TRIAGE + E2E FIX + BUCKET A.** 14 commits: SDK to system path (CVEs patched), 5 pre-existing issues triaged, qr-payment-ui 6/6 PASS, guest checkout + PostgreSQL migration, 21/22 golden tests PASS. See archive for full details. **Branch:** `main`.
