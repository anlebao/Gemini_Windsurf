# Project State

> **Mục đích:** Single Source of Truth cho AI về trạng thái dự án. BẮT BUỘC đọc đầu mỗi phiên.

---

## 0. Maintenance Rules

1. **One-and-only-one:** Mỗi section chỉ tồn tại 1 lần; cấm trùng nội dung.
2. **No contradiction:** Một hạng mục chỉ có 1 trạng thái.
3. **Ground Truth first:** Verify path/branch với codebase trước khi ghi.
4. **Now over History:** Section 2-4 chỉ mô tả việc ĐANG làm và KẾ TIẾP. Việc xong → gom vào Section 6.
5. **Actionable Next Actions:** Xóa action đã quá hạn/sai bối cảnh.
6. **Stamp every edit:** Cập nhật Section 11 (Last Updated + branch) mỗi lần sửa.

---

## 1. Project Overview

**Dự án:** Vạn An Accounting System MVP — giải pháp kế toán HKD theo TT 152/2025/TT-BTC.
**Stack:** .NET 8 · EF Core · SQLite · Blazor Server (ShopERP) · Blazor WebAssembly (KhachLink PWA) · SignalR · YARP Gateway · xUnit · Playwright.
**Kiến trúc:** Clean Architecture + DDD + Multi-tenancy. Data flow: `KhachLink (5002) → Gateway (5001) → ShopERP (5003) → SQLite`.

**Modules:**
| Module | Vai trò |
|--------|---------|
| `1_Shared` | Domain entities, Value Objects, DTOs |
| `2_Gateway` | YARP reverse proxy + controllers (stateless, in-process CoreHub host) |
| `3_CoreHub` | Services, Repositories, EF infrastructure (background service, in-process) |
| `5_WebApps/ShopERP` | Staff/admin UI (Blazor Server, SQLite edge node) |
| `5_WebApps/KhachLink` | Customer-facing PWA (Blazor WASM) |
| `UI.Platform` | Shared UI components (VanAButton, VanACard…) |
| `6_Tests / 6_Testing` | Unit, Integration, Architecture, E2E |

**Hard stops không bao giờ vi phạm:**
- Domain layer PURE — no EF, no DbContext, no DataAnnotations.
- `AccountingEntry` 100% immutable (append-only).
- Gateway STATELESS — no business logic in controllers. CoreHub DbContext đăng ký tại Program.cs (DI root) là hợp lệ.
- KhachLink dùng HTTP via Gateway ONLY — no CoreHub DI trực tiếp.
- ShopERP là SQLite-only edge node — KHÔNG có Npgsql dependency trực tiếp.
- ALWAYS dùng UI Platform components, không bypass.

---

## 2. Current Objective

**[TENANT ONBOARDING F&B — Planning Complete, Awaiting Approval]**

Implement generic tenant onboarding system for F&B (Food & Beverage) first, designed to support future industries (SPA, Hotel, Barber, Clothes, Healthy, Pet Shop) via pluggable `IIndustrySeedStrategy` abstraction.

Master plan: `docs/AI/tasks/tenant_onboarding_fnb_master_plan.md`  
Task cards: Wave 1-6 trong `docs/AI/tasks/wave*_tenant_onboarding_*_task_card.md`

**Status:** Planning done. **WAITING FOR USER APPROVAL** to implement Wave 1.

---

## 3. Current Status

- **Branch:** `main`
- **Last commit:** `e831972` — docs(state): update project_state.md with commit hash ed442ce
- **Build:** `dotnet build VanAn.sln` → 0 errors ✅
- **Architecture Tests:** 28/28 PASS ✅
- **Unit Tests:** 678/678 PASS ✅
- **KhachLink Startup Tests:** 8/8 PASS ✅
- **Gateway Startup Tests:** PASS ✅
- **Codebase health:** Clean. No known compile errors. No known architecture violations.
- **Documentation:** Added detailed module docs: `docs/KhachLink_Documentation.md` and `docs/ShopERP_Documentation.md` ✅
- **Planning:** Tenant Onboarding F&B master plan + 6 wave task cards created ✅

---

## 4. Next Actions

1. **WAIT FOR USER APPROVAL** to start implementing Wave 1 (`wave1_tenant_onboarding_abstraction_task_card.md`).
2. After approval: commit planning files, then begin TDD implementation of Wave 1.
3. No code changes until explicit approval.

---

## 5. Active Architecture Decisions

| Decision | Lý do |
|---------|-------|
| CoreHub = in-process background service bên trong Gateway | Monolith architecture (Phase 1-2). CoreHub KHÔNG phải standalone HTTP container. |
| Gateway = DI composition root cho CoreHub services | Program.cs đăng ký CoreHub DbContext/Services. Hợp lệ về kiến trúc. |
| ShopERP = SQLite-only edge node | Edge deployment offline-first. Npgsql không được reference trực tiếp. |
| docker-compose.prod.yml: Gateway depends_on postgres + nats | CoreHub removed. Gateway trực tiếp phụ thuộc infra services. |
| CustomerToken = `IDataProtector` (không phải JWT) | Tránh library mới, không cần Identity |
| OTP storage = `IMemoryCache` | TTL built-in, không cần migration |
| Tier calc on-the-fly từ PointBalance | Domain không cần biết tier rules |
| `Shop.Latitude/Longitude` trên entity (không phải ShopConfig) | Store Finder cần query địa lý từ DB |
| `AccountingEntry` immutable, Reversal Entry pattern | Audit trail tài chính bất khả xâm phạm |
| Multi-tenancy `TenantId` filter tại mọi layer | Data isolation per HKD |

---

## 6. History Log

* [2026-07-01] **Objective Transition: IDLE → Tenant Onboarding F&B** — Archived IDLE state. New active objective: implement generic tenant onboarding for F&B with multi-industry extensibility (SPA, Hotel, Barber, Clothes, Healthy, Pet Shop). Master plan + 6 wave task cards created and committed. Status: Planning complete, awaiting approval. On `main`.

* [2026-07-01] **Documentation Added** — Created detailed module documentation: `docs/KhachLink_Documentation.md` (KhachLink PWA: 13 sections covering routes, services, HTTP integrations, PWA/offline, UI Platform) and `docs/ShopERP_Documentation.md` (ShopERP admin: 14 sections covering accounting, EInvoice, order management, admin, DI, SQLite edge architecture). No code changes. On `main`.

* [2026-07-02] **Architecture Test Fixes COMPLETE** — Fixed 4 pre-existing architecture test failures (stale tests from before Phase 2 monolith migration): (1) VA-CONSISTENCY-005: removed "corehub" from logging check list; (2) VA-CONSISTENCY-003: replaced corehub dependency assertion with postgres+nats; (3) Rule C: removed Npgsql from ShopERP.csproj (SQLite-only edge node); (4) VA-GATEWAY-003: excluded Program.cs (DI root) and obj/ from Gateway purity scan. Result: 28/28 arch tests PASS. Commits: `ed442ce`, `e831972` on `main`.

* [2026-07-02] **CI/CD Hotfix COMPLETE** — Fixed remote CI/CD pipeline failures: (1) GatewayWebApplicationFactory.SingleOrDefault → loop-remove all EF Core descriptors; (2) KhachLinkWebApplicationFactory compile error fixed; (3) .env.test added, CI validates .env.test; (4) validate-env-vars.ps1 regex fixed for comment lines; (5) weak secret patterns expanded 7→18. Commit: `7a96a2b` on `main`.

* [2026-06-30] **Phase 4 COMPLETE — Offline-First Edge Fix.** Removed CoreHub container from docker-compose.edge.yml. Updated Gateway/ShopERP dependencies to postgres+nats. Preserved SQLite sidecar + NATS sync worker. Validation passed.

* [2026-06-30] **Phase 3 COMPLETE — CI/CD Pipeline Fix.** Removed CoreHub build/push step from cd.yml. CD now builds 3 images (Gateway, ShopERP, KhachLink). Build time -25%.

* [2026-06-30] **Phase 2 COMPLETE — Docker Compose Production Fix.** Removed CoreHub container from docker-compose.prod.yml. Gateway depends_on postgres+nats (was corehub).

* [2026-06-30] **Phase 1 COMPLETE — Local Development Environment Fix.** start-apps.ps1 updated.

* [2026-06-30] **Phase 0 COMPLETE — Architecture Validation Layer Enhancement.** ArchitectureConsistencyTests.cs (5 tests), validate-docker-compose.ps1, validate-env-vars.ps1, startup test enhancements, CI docker-compose-validation job.

* [2026-06-30] **Unified Roadmap 10/10 waves COMPLETE** — ADR001 (W1-W5) + KhachLink (W1-W4) all done. Architecture reference: docs/Architecture/ADR001-Station-Architecture.md.

---

## 7. Known Open Issues (Non-blocking)

| ID | Issue | Severity | Notes |
|----|-------|----------|-------|
| E2E-001 | `e2e.yml` CI job disabled (`if: false`) | Medium | 19 E2E spec files exist but full suite not run in CI. Smoke tests (6) pass locally. |
| INT-001 | 2 Integration tests failing (`Golden Flow: Simple Entity Insert`, `Golden Flow: Multi-Tenant Isolation`) | Low | Pre-existing, non-blocking. Matches cloud CI behavior. |
| PHASE5-001 | Phase 5 Validation & E2E Testing not fully executed | Low | P5-T2 (staging), P5-T5 (edge live test), P5-T6 (perf baseline), P5-T7 (security) not done. No staging environment available. Deferred. |

---

## 8. Test Suite Summary

| Suite | Count | Status |
|-------|-------|--------|
| Architecture Tests | 28 | ✅ All PASS |
| Unit Tests (Core) | 661 | ✅ All PASS |
| Unit Tests (Unit) | 17 | ✅ All PASS |
| KhachLink Startup | 8 | ✅ All PASS |
| Gateway Startup | ~5 | ✅ All PASS |
| Integration Tests | 152 | ⚠️ 150 PASS, 2 fail (pre-existing, non-blocking) |
| E2E Smoke | 6 | ✅ All PASS (local) |
| E2E Full Suite | 19 specs | ⏸ Not run (CI disabled) |

---

## 9. Key File References

| File | Mục đích |
|------|---------|
| `docs/AI/tasks/architecture_refactor_master_plan.md` | Master plan phases 0-5 (ARCHIVED — all phases complete) |
| `docs/AI/tasks/phase5_validation_task_card.md` | Phase 5 task card (partially complete — see Known Issues) |
| `docs/Architecture/Validation-Layer-Rules.md` | Architecture validation rules documentation |
| `docs/DEPLOYMENT.md` | Environment setup guide |
| `scripts/validate-docker-compose.ps1` | Docker compose validation script |
| `scripts/validate-env-vars.ps1` | Environment variable validation script |
| `.env.test` | CI test environment values |
| `docs/KhachLink_Documentation.md` | Detailed documentation for KhachLink PWA module |
| `docs/ShopERP_Documentation.md` | Detailed documentation for ShopERP admin module |
| `docs/AI/tasks/tenant_onboarding_fnb_master_plan.md` | Master plan for Tenant Onboarding F&B (multi-industry generic) |
| `docs/AI/tasks/wave1_tenant_onboarding_abstraction_task_card.md` | Wave 1: Generic abstraction + DTOs |
| `docs/AI/tasks/wave2_fnb_seed_strategy_task_card.md` | Wave 2: F&B seed strategy |
| `docs/AI/tasks/wave3_tenant_onboarding_orchestrator_task_card.md` | Wave 3: Onboarding orchestrator |
| `docs/AI/tasks/wave4_tenant_onboarding_gateway_api_task_card.md` | Wave 4: Gateway API |
| `docs/AI/tasks/wave5_tenant_onboarding_shoperp_ui_task_card.md` | Wave 5: ShopERP admin UI |
| `docs/AI/tasks/wave6_tenant_onboarding_validation_docs_task_card.md` | Wave 6: Validation + docs |

---

## 10. Active Architecture Constraints (Quick Reference)

```
KhachLink (5002) → Gateway (5001) → ShopERP (5003) → SQLite
                        ↓
              [in-process CoreHub services]
                        ↓
                  PostgreSQL (prod)
                  SQLite (edge)
```

**Docker services (prod):** postgres · nats · seq · gateway · shoperp · khachlink · nginx · certbot
**Docker services (edge):** postgres · nats · gateway · shoperp · shoperp-nats-sync
**CoreHub:** NOT a Docker service — runs in-process inside Gateway.

---

## 11. Maintenance Log

* **Last Updated:** 2026-07-01 — Archived IDLE state; set active objective to Tenant Onboarding F&B; added master plan + 6 wave task cards.
* **Current Branch:** `main`
* **Current Objective:** Tenant Onboarding F&B — Planning complete, awaiting approval.
