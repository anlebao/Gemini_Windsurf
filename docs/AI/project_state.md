# Project State

> **Mục đích:** Single Source of Truth cho AI về trạng thái dự án. BẮT BUỘC đọc đầu mỗi phiên.

---

## 0. Maintenance Rules

1. One-and-only-one: Mỗi section chỉ tồn tại 1 lần.
2. No contradiction: Một hạng mục chỉ có 1 trạng thái.
3. Ground Truth first: Verify path/branch với codebase trước khi ghi.
4. Now over History: Section 2-4 chỉ mô tả việc ĐANG làm và KẾ TIẾP. Việc xong → gom vào Section 6.
5. Actionable Next Actions: Xóa action đã quá hạn/sai bối cảnh.
6. Stamp every edit: Cập nhật Section 11 mỗi lần sửa.

---

## 1. Project Overview

**Dự án:** Vạn An Accounting System MVP — giải pháp kế toán HKD theo TT 152/2025/TT-BTC.
**Stack:** .NET 8 · EF Core · SQLite · Blazor Server (ShopERP) · Blazor WebAssembly (KhachLink PWA) · SignalR · YARP Gateway · xUnit · Playwright.
**Kiến trúc:** Clean Architecture + DDD + Multi-tenancy. Data flow: `KhachLink (5002) → Gateway (5001) → ShopERP (5003) → SQLite`.

**Modules:** `1_Shared` (Domain) · `2_Gateway` (YARP) · `3_CoreHub` (Services, in-process) · `5_WebApps/ShopERP` (Blazor Server) · `5_WebApps/KhachLink` (Blazor WASM) · `UI.Platform` (Shared components) · `6_Tests/6_Testing`.

**Hard stops:** Domain PURE · `AccountingEntry` immutable · Gateway STATELESS · KhachLink HTTP-only · ShopERP SQLite-only · ALWAYS dùng UI Platform components.

---

## 2. Current Objective

**[STREAM C: SHOPERP UI FIX — IMPLEMENTING]**

Fix 23 .razor files trong `5_WebApps/ShopERP/Components/Pages/` — 14 dead pages, 18 unstyled, 3 broken layouts. Pattern-based batch fix, 6 waves.

- **Master plan:** `docs/AI/tasks/shoperp_ui_fix_master_plan.md`
- **Commit planning:** `51dd7ff`
- **Root cause:** UI.Platform (VanALayout/VanANavigation) chưa hoàn thiện — zero CSS, sai slot structure.
- **Target:** 14 dead → 0, 18 unstyled → 0, 3 broken layouts → 0.

### 6 Patterns (Waves)
| Wave | Pattern | Description | Files | Status |
|---|---|---|---|---|
| 1 | P | UI.Platform infra: VanALayout CSS + slot + VanANavigation CSS + icons | 7 | ✅ COMPLETE (`3b893e8`) |
| 2 | R | Add `@rendermode InteractiveServer` | 14 | PENDING |
| 3 | C | Page CSS isolation (shared + per-page) | 19 | PENDING |
| 4 | V | Component consolidation (VanAnX → VanX) | 7 | PENDING |
| 5 | L | Admin layout consistency (create AdminLayout) | 5 | PENDING |
| 6 | G | Governance cleanup (inline style, eval, demo) | 6 | PENDING |

### Parked Streams (awaiting approval)
- **Stream A: EInvoice Provider Rewrite** — Planning complete (`59b60fe`). Blocker: Wave 0 sandbox credentials (1-2 tuần).
- **Stream B: E2E Test Cleanup** — Planning complete (`51dd7ff`). 8 waves, 7 anti-patterns.

---

## 3. Current Status

- **Branch:** `feature/shoperp-ui-fix-wave1-platform-infra`
- **Last commit:** `3b893e8` [UI-FIX WAVE 1] Pattern P: UI.Platform infra
- **Build:** `dotnet build VanAn.sln` → 0 errors ✅
- **Guard-check:** PASSED ✅
- **Uncommitted changes:** None
- **Completed features (merged to main):** Tenant Onboarding (6 waves) · ShopConfig Refactor (3 phases) · Architecture Test Fixes · CI/CD Hotfix.
- **In-progress (feature branch):** Wave 0 ✅ · Wave 1 ✅ · Wave 2-6 PENDING.

---

## 4. Next Actions

**Stream C — ShopERP UI Fix (active):**
1. ~~Wave 0 (pre-flight)~~ ✅ — Build pass, git clean, UI.Platform 0 .razor.css confirmed.
2. ~~Wave 1: UI.Platform infra~~ ✅ — VanALayout CSS + slot fix + VanANavigation CSS + Bootstrap Icons CDN. Commit `3b893e8`.
3. **Wave 2:** Add `@rendermode InteractiveServer` (14 files, mechanical).
4. **Wave 3:** Page CSS isolation (shared `pages.css` + 18 `.razor.css`).
5. **Wave 4:** Component consolidation (VanAnAlert → VanAAlert, VanAnModal → VanAModal).
6. **Wave 5:** Admin layout (create `AdminLayout.razor` + add `@layout` to 4 files).
7. **Wave 6:** Governance cleanup (inline style, eval logout, delete Counter, fix Home).

---

## 5. Active Architecture Decisions

| Decision | R lý do |
|---|---|
| CoreHub = in-process background service trong Gateway | Monolith Phase 1-2 |
| Gateway = DI composition root cho CoreHub | Program.cs đăng ký CoreHub DbContext/Services |
| ShopERP = SQLite-only edge node | Edge deployment offline-first |
| CustomerToken = `IDataProtector` | Tránh library mới |
| `AccountingEntry` immutable, Reversal Entry | Audit trail bất khả xâm phạm |
| Multi-tenancy `TenantId` filter mọi layer | Data isolation per HKD |

---

## 6. History Log (compressed — see git log for details)

* [2026-07-03] **Wave 0 + Wave 1 COMPLETE** — Pre-flight verified. Wave 1: VanALayout.razor.css + VanANavigation.razor.css (NEW), icon fix (`<i class="bi bi-@icon">`), Bootstrap Icons CDN, 3 layout files slot fix, VanADashboard emoji→BI icons. Commit `3b893e8` on `feature/shoperp-ui-fix-wave1-platform-infra`.
* [2026-07-02] **ShopERP UI Fix + E2E Cleanup — PLANNING COMPLETE** — 2 master plans + 14 task cards (`51dd7ff`). UI: 23 files, 6 patterns (P/R/C/V/L/G). E2E: 20 spec files, 7 anti-patterns.
* [2026-07-02] **EInvoice Provider Rewrite — PLANNING COMPLETE** — Master plan + 4 task cards (`59b60fe`). 20 Viettel + 10 MISA API spec mismatches. Wave 0 credential request parallel.
* [2026-07-02] **ShopConfig Refactor — 3 PHASES COMPLETE** — Product→tenant refactor, KhachLink HTTP-only, merged to main.
* [2026-07-02] **Tenant Onboarding — 6 WAVES COMPLETE & MERGED** — Generic multi-industry onboarding (F&B enabled), orchestrator, Gateway API, ShopERP UI, integration tests. Commit `3123b6b`.
* [2026-07-02] **Architecture Test Fixes + CI/CD Hotfix** — 28/28 arch tests PASS, remote CI fixed.
* [2026-07-01] **Tenant Onboarding Waves 1-4** — Abstraction + F&B seed + orchestrator + Gateway API.
* [2026-07-01] **Documentation Added** — KhachLink + ShopERP module docs.

---

## 7. Active Files Reference

### Stream C — ShopERP UI Fix
| File | Role |
|---|---|
| `docs/AI/tasks/shoperp_ui_fix_master_plan.md` | Master plan (6 waves) |
| `docs/AI/tasks/wave1_shoperp_ui_platform_infra_task_card.md` | Wave 1: Pattern P (UI.Platform) |
| `docs/AI/tasks/wave2_shoperp_rendermode_task_card.md` | Wave 2: Pattern R (rendermode) |
| `docs/AI/tasks/wave3_shoperp_page_css_task_card.md` | Wave 3: Pattern C (CSS) |
| `docs/AI/tasks/wave4_shoperp_component_consolidation_task_card.md` | Wave 4: Pattern V (versions) |
| `docs/AI/tasks/wave5_shoperp_admin_layout_task_card.md` | Wave 5: Pattern L (Admin layout) |
| `docs/AI/tasks/wave6_shoperp_governance_cleanup_task_card.md` | Wave 6: Pattern G (governance) |

### Parked Streams
| File | Role |
|---|---|
| `docs/AI/tasks/einvoice_provider_rewrite_master_plan.md` | Stream A (parked) |
| `docs/AI/tasks/e2e_test_cleanup_master_plan.md` | Stream B (parked) |

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

---

## 9. Maintenance Log

* **Last Updated:** 2026-07-03 — Wave 0 + Wave 1 COMPLETE. Branch: `feature/shoperp-ui-fix-wave1-platform-infra`. 8 files changed (+213/-23). Build 0 errors, guard PASSED. Next: Wave 2 (rendermode).
* **Current Branch:** `feature/shoperp-ui-fix-wave1-platform-infra`
* **Current Objective:** Stream C: ShopERP UI Fix — Wave 2 next (add `@rendermode InteractiveServer` to 14 files).
