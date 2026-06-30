# Project State

> **Mục đích:** Single Source of Truth cho AI về trạng thái dự án. BẮT BUỘC đọc đầu mỗi phiên.

---

## 0. Maintenance Rules

1. **One-and-only-one:** Mỗi section chỉ tồn tại 1 lần; cấm trùng nội dung.
2. **No contradiction:** Một hạng mục chỉ có 1 trạng thái.
3. **Ground Truth first:** Verify path/branch với codebase trước khi ghi.
4. **Now over History:** Section 2-4 chỉ mô tả việc ĐANG làm và KẾ TIẾP. Việc xong → gom vào Section 6.
5. **Actionable Next Actions:** Xóa action đã quá hạn/sai bối cảnh.
6. **Stamp every edit:** Cập nhật Section 7 (Last Updated + branch) mỗi lần sửa.

---

## 1. Project Overview

**Dự án:** Vạn An Accounting System MVP — giải pháp kế toán HKD theo TT 152/2025/TT-BTC.
**Stack:** .NET 8 · EF Core · SQLite · Blazor Server (ShopERP) · Blazor WebAssembly (KhachLink PWA) · SignalR · YARP Gateway · xUnit · Playwright.
**Kiến trúc:** Clean Architecture + DDD + Multi-tenancy. Data flow: `KhachLink (5002) → Gateway (5001) → ShopERP (5003) → SQLite`.

**Modules:**
| Module | Vai trò |
|--------|---------|
| `1_Shared` | Domain entities, Value Objects, DTOs |
| `2_Gateway` | YARP reverse proxy + controllers (stateless) |
| `3_CoreHub` | Services, Repositories, EF infrastructure |
| `5_WebApps/ShopERP` | Staff/admin UI (Blazor Server) |
| `5_WebApps/KhachLink` | Customer-facing PWA (Blazor WASM) |
| `UI.Platform` | Shared UI components (VanAButton, VanACard…) |
| `6_Tests / 6_Testing` | Unit, Integration, Architecture, E2E |

**Hard stops không bao giờ vi phạm:**
- Domain layer PURE — no EF, no DbContext, no DataAnnotations.
- `AccountingEntry` 100% immutable (append-only).
- Gateway STATELESS — no DbContext, no business logic.
- KhachLink dùng HTTP via Gateway ONLY — no CoreHub DI trực tiếp.
- ALWAYS dùng UI Platform components, không bypass.

---

## 2. Current Objective

**[ACTIVE] Architecture Refactor — CoreHub & Gateway Alignment + Validation Layer Enhancement**

**Background:** Critical architecture mismatch detected — CoreHub is background service but docker-compose.prod.yml configures it as HTTP service. Current validation layer FAILED to detect this mismatch, allowing architecture violations to reach production.

**Root Issues:**
1. CoreHub Program.cs uses `Host.CreateDefaultBuilder` (background service, no HTTP)
2. docker-compose.prod.yml configures CoreHub with `ASPNETCORE_URLS=http://+:80` (HTTP service)
3. Architecture tests only validate code structure, not deployment consistency
4. No cross-layer validation (code → docker-compose → deployment)
5. Gateway direct references CoreHub project (in-process), but deployment expects HTTP

**Scope (6 Phases - 12-18 days):**
- 🔄 Phase 0 (BLOCKING): Architecture Validation Layer Enhancement — Session 1 COMPLETE ✅, Session 2 PENDING
- ⏳ Phase 1: Local Development Environment Fix — Align start-apps.ps1 with monolithic architecture
- ⏳ Phase 2: Docker Compose Production Fix — Decide: remove CoreHub container or reconfigure as background service
- ⏳ Phase 3: CI/CD Pipeline Fix — Update CI/CD to match new architecture
- ⏳ Phase 4: Offline-First Edge Fix — Update edge deployment configuration
- ⏳ Phase 5: Validation & E2E Testing — Comprehensive validation across all environments

**Master Plan:** `docs/AI/tasks/architecture_refactor_master_plan.md`
**Task Cards:** 6 task cards created (phase0-phase5)
**Execution Strategy:** 1 phase per session (2-3 hours), ~12 sessions total to avoid context overflow
**Status:** Phase 0 Session 1 COMPLETE — Validation layer enhanced, ready for Session 2 (CI/CD integration)

---

**[PREVIOUS] UNIFIED ROADMAP — 10 waves (ADR001 + KhachLink, Option C Merged, Layer-ordered) — COMPLETE**

| # | Wave | Layer | Est. | Status |
|---|------|-------|------|--------|
| ✅ | ADR001-W1: Architecture compliance test | — | Done | COMPLETE |
| ✅ | ADR001-W2: `docker-compose.edge.yml` | Infra | 2-3h | COMPLETE |
| ✅ | ADR001-W3: NatsSyncWorker + NatsEventPublisher | Infra | 1d | COMPLETE |
| ✅ | KhachLink-W1: PWA Install Fix | UX | 1-2h | COMPLETE |
| ✅ | KhachLink-W2: QR Code (In-app Camera Scanning) | UX | 1-2d | COMPLETE |
| ✅ | ADR001-W4.1: SQLite Sidecar Infrastructure | Backend | 2-3h | COMPLETE |
| ✅ | ADR001-W4.2: NATS Sync Worker Mode | Backend | 2-3h | COMPLETE |
| ✅ | ADR001-W4.3: Phased Migration Validation | Backend | 1-2h | COMPLETE |
| ✅ | KhachLink-W3: Product Personalization Hybrid C | Backend | 2-3d | COMPLETE |
| ✅ | KhachLink-W4: Real-time Order Status (Polling + NATS Push) | Integration | 1-2d | COMPLETE |
| ✅ | ADR001-W5: CI edge pipeline | CI | 2-3h | COMPLETE |

**Progress:** 10/10 waves complete (100%)

---

## 3. Current Status

- **Branch:** `feature/architecture-refactor-phase0-validation` (Phase 0 in progress)
- **Last commit:** `2e017fc` — [ARCH-PHASE 0] Architecture Validation Layer Enhancement - Session 1 Complete
- **Build:** `dotnet build VanAn.sln` → 0 errors ✅
- **Tests:** Phase 0 Session 1 tests
  - Architecture Consistency Tests: 4/5 passing ✅ (1 expected fail detecting actual bug)
  - VA-CONSISTENCY-002 correctly detects CoreHub HTTP service configuration in docker-compose.prod.yml
- **State:** Phase 0 Session 1 COMPLETE — Validation layer enhanced, ready for Session 2 (CI/CD integration)

---

## 4. Next Actions

1. **[NOW]** Phase 0 Session 2 — Add CI job for docker-compose validation + pre-deployment validation in CD
2. **[Phase 0 Session 2 Tasks]**
   - Add docker-compose validation job to .github/workflows/ci.yml
   - Add pre-deployment validation to .github/workflows/cd.yml
   - Test all validations in CI context
   - Document validation rules in docs/Architecture/
3. **[After Phase 0]** Begin Phase 1 — Local Development Environment Fix
4. **[Reference]** Master plan: `docs/AI/tasks/architecture_refactor_master_plan.md`
5. **[Reference]** Phase 0 task card: `docs/AI/tasks/phase0_validation_layer_enhancement_task_card.md`

---

## 5. Active Architecture Decisions (Wave 17-relevant)

| Decision | Lý do |
|---------|-------|
| CustomerToken = `IDataProtector` (không phải JWT) | Tránh library mới, không cần Identity |
| OTP storage = `IMemoryCache` | TTL built-in, không cần migration |
| Tier calc on-the-fly từ PointBalance | Domain không cần biết tier rules |
| `Shop.Latitude/Longitude` trên entity (không phải ShopConfig) | Store Finder cần query địa lý từ DB |
| Push subscription log-only trong W17 | `Customer.PushSubscriptionJson` chờ W18 approve |
| NavMenu mobile = bottom tab bar | UX pattern chuẩn cho mobile PWA |
| `AccountingEntry` immutable, Reversal Entry pattern | Audit trail tài chính bất khả xâm phạm |
| Multi-tenancy `TenantId` filter tại mọi layer | Data isolation per HKD |

---

## 6. History Log

* [2026-06-30] Phase 0 Session 1 COMPLETE — Architecture Validation Layer Enhancement. Implemented: ArchitectureConsistencyTests.cs (5 tests: code vs docker-compose validation, 4/5 passing, 1 expected fail detecting CoreHub HTTP service bug), validate-docker-compose.ps1 script, validate-env-vars.ps1 script, enhanced GatewayStartupTests.cs and KhachLinkStartupTests.cs with architecture validation (no DbContext checks). Build: 0 errors. Critical test VA-CONSISTENCY-002 correctly detects CoreHub HTTP service configuration in docker-compose.prod.yml despite being background service in code. Commit: `2e017fc`. Branch: `feature/architecture-refactor-phase0-validation`. Next: Session 2 - CI/CD integration (docker-compose validation job, pre-deployment validation).
* [2026-06-30] Architecture Refactor Master Plan CREATED — CoreHub & Gateway Alignment + Validation Layer Enhancement. Root cause: CoreHub background service vs docker-compose HTTP service mismatch not detected by validation layer. Created: 6-phase master plan (Phase 0-5), 6 task cards, context management strategy (1 phase per session, 12 sessions total). Phase 0 (BLOCKING): Architecture Validation Layer Enhancement - add ArchitectureConsistencyTests.cs, docker-compose validation scripts, env var validation scripts, enhance startup tests, add CI/CD validation jobs. Phases 1-5: Sequential architecture fix (local dev → docker-compose → CI/CD → edge → validation). Total estimated: 12-18 days, 20-46 hours. Risk: LOW (enhanced validation layer). Status: READY for execution. Files: architecture_refactor_master_plan.md, phase0-5 task cards.
* [2026-06-30] CI Gap Fix COMPLETE — Startup Tests for KhachLink & Gateway. Root causes: KhachLink DI never booted in CI (missing AddScoped not detected), Gateway DI never validated, integration tests non-blocking. Implemented: KhachLinkWebApplicationFactory + KhachLinkStartupTests (3 blocking tests), GatewayWebApplicationFactory + GatewayStartupTests (3 blocking tests), ci-full.ps1 Step 2b+2c BLOCKING, ci.yml jobs khachlink-startup+gateway-startup, KhachLink Program.cs fix (CoreHub→Http implementations), governance.md checklist. CI pipeline passes (509s), all tests pass. Commit: `207983f`.
* [2026-06-30] Unified Roadmap Wave 10 COMPLETE — ADR001-W5: CI Edge Pipeline. Implemented: .github/workflows/ci-edge.yml with 4 jobs (build, architecture-tests, nats-sync-worker-tests, validate-edge-compose), triggers on feature/edge* and feature/adr001-wave* branches plus manual dispatch, validates docker-compose.edge.yml structure (shoperp-nats-sync service, shoperp_sqlite_data volume, NATS broker), verifies docker-compose.prod.yml NOT modified with edge components (v1 SaaS preserved), runs VanAn.Architecture.Tests (Rule H + Rule I for ADR-001), filters NatsSyncWorker/NatsEventPublisher unit tests. Exit criteria: CI edge pipeline created and validated, YAML syntax verified, dotnet build 0 errors, guard-check ALL CHECKS PASSED. ALL 10 WAVES COMPLETE (100%) — Layer 0-1 infrastructure + UX foundation + Layer 2 (Phase 1-3 + KhachLink-W3) + Layer 3 (KhachLink-W4) + Layer 4 (CI Validation) DONE. Commit: `76d015c`. Branch: `feature/adr001-wave5-ci-edge`.
* [2026-06-29] Unified Roadmap Wave 9 COMPLETE — KhachLink-W4: Real-time Order Status (Polling + NATS Push). Session 1: Polling Infrastructure (Gateway /status forwarding, PeriodicTimer 5s polling in OrderTracking.razor, visibility-aware polling, IAsyncDisposable, VanAnSpinner). Session 2: Push Notification Infrastructure (VAPID key generation, WebPush library v1.0.13, PushNotificationService, pwa.js enablement, service-worker.js enhancement). Session 3: Push Subscription Persistence + NATS Integration (PushSubscription entity separate table, PushSubscriptionConfiguration, IPushSubscriptionRepository, NotificationsController persistence, PushNotificationService database integration, OrderWorkflowService NATS publishing). Session 4: Architecture Decision + Performance Benchmarks (SignalR retained for ShopERP kitchen display, performance analysis, scalability 10K users, battery 70-90% reduction). Architecture decision: KhachLink uses polling+push (customer-facing), ShopERP uses SignalR (staff-facing, sub-second updates needed). VAPID security: private key in environment variable, .gitignore configured. Build: dotnet build 0 errors, guard-check ALL CHECKS PASSED. Commits: cc83107 (S1), df5e6c7 (S2), 6f855f1 (S3), 49f9ac2 (S4). Branch: `feature/khachlink-wave4-order-realtime`.
* [2026-06-29] Unified Roadmap Wave 8 COMPLETE — KhachLink-W3: Product Personalization (Hybrid Option C). Implemented: CustomerRecommendationService (frequency-based algorithm with IMemoryCache 5-min TTL), GET /api/products/recommended endpoint in ProductsController, ProductHttpService.GetRecommendedProductsAsync(), RecentlyViewedService (localStorage tracking), RecommendedProductDto (extends ProductDto with recommendation metadata), Home.razor "Frequently Bought" section, Home.razor "Recently Viewed" section, product view tracking on AddToCart. Hybrid approach: keeps global catalog + adds personalized sections. Fallback for new customers (no order history). UI Platform compliance: VanAnCard, VanAnButton used. dotnet build 0 errors, guard-check ALL CHECKS PASSED. Commit: `f418bb3`. Branch: `feature/khachlink-wave3-personalization`.
* [2026-06-29] Unified Roadmap Wave 7 COMPLETE — ADR001-W4.3: Phased Migration Validation (Phase 3). Implemented: Phase 1 validation script (validate-phase1-sidecars.ps1) for sidecar-only deployment, Phase 2 validation script (validate-phase2-sync-workers.ps1) for sync worker dual-write mode, sync lag monitor placeholder (monitor-sync-lag.ps1), rollback documentation (ADR001-Rollback-Plan.md) with 3 rollback scenarios, rollback testing script (test-rollback.ps1) in simulation mode. All validation scripts executed successfully. Phase 1 validation: sidecars deployed, sync workers inactive, PostgreSQL primary. Phase 2 validation: sync workers configured with hybrid profile, NATS connectivity, volume mounts, dependencies. Rollback procedures documented for Phase 1 (sidecars only), Phase 2 (sync workers active), and Emergency scenarios. dotnet build 0 errors, guard-check ALL CHECKS PASSED. Commit: `39685a2`. Branch: `feature/adr001-wave4-migration-validation`.
* [2026-06-29] Unified Roadmap Wave 6 COMPLETE — ADR001-W4.2: NATS Sync Worker Mode (Phase 2). Implemented: --sync-worker conditional DI registration in ShopERP/Program.cs (IOutboxRepository, INatsEventPublisher, NatsSyncWorker), SQLITE_DB_PATH env var override for Docker volume mounting, appsettings.Edge.json for local development testing, 3 NATS sync worker services in docker-compose.prod.yml (shoperp-nats-sync, khachlink-nats-sync, order-station-nats-sync) with hybrid profile, NATS dependencies, and resource limits. Phase 2: sync workers active when DEPLOYMENT_MODE=hybrid, v1 SaaS unchanged (sync workers disabled by default). dotnet build 0 errors, guard-check ALL CHECKS PASSED. Commit: `078ee6e`. Branch: `feature/adr001-wave4-sync-worker-mode`.
* [2026-06-29] Unified Roadmap Wave 5 COMPLETE — ADR001-W4.1: SQLite Sidecar Infrastructure (Phase 1). Implemented: 3 SQLite sidecar containers (shoperp-sqlite, khachlink-sqlite, order-station-sqlite) with Alpine 3.19, 3 persistent Docker volumes (shoperp_sqlite_data, khachlink_sqlite_data, order_sqlite_data), DEPLOYMENT_MODE environment variable added to shoperp/khachlink services (default: saas), sidecar dependency comments added. Phase 1: sidecars exist but not actively used in v1 SaaS (PostgreSQL remains primary). docker-compose.prod.yml syntax validated, dotnet build 0 errors, guard-check ALL CHECKS PASSED. Commit: `07a5c14`. Branch: `feature/adr001-wave4-sqlite-sidecars`.
* [2026-06-29] ADR001-W4 Task Cards Created — Split ADR001-W4 into 3 sub-waves per ADR001-Station-Architecture.md: W4.1 (SQLite Sidecar Infrastructure, 3 task cards), W4.2 (NATS Sync Worker Mode, 3 task cards), W4.3 (Phased Migration Validation, 3 task cards). Total 9 task cards created. Roadmap updated to 10 waves (50% complete). Design confirmed: manual deployment switch, no automatic failover. Branch: `main`.
* [2026-06-29] Unified Roadmap Wave 4 COMPLETE — KhachLink-W2: QR Code Scanning (In-app camera scanning per task card). Implemented: html5-qrcode library integration, QRCodePayload format, QrCodeService (CoreHub + ShopERP), QRScanner.razor component with UI Platform, Scan.razor page, camera permission handling (iOS + Android), CartService.AddFromQrCodeAsync, navigation updates (desktop + mobile). Build 0 errors, guard-check ALL CHECKS PASSED. Commit: `db80062`. Branch: `main`.
* [2026-06-29] Unified Roadmap Waves 1-3 COMPLETE — ADR001-W2 (docker-compose.edge.yml, 23/23 arch tests) + ADR001-W3 (NatsEventPublisher + NatsSyncWorker, 9/9 Nats tests) + KhachLink-W1 (PWA Install Fix). Layer 0-1 infrastructure + UX foundation DONE (50% complete). Commit: `b83eb84`. Branch: `main`.
* [2026-06-29] UNIFIED ROADMAP — Merged ADR001 (5 waves) + KhachLink improvements (4 waves) into 8-wave unified plan (Option C, Layer-ordered). Zero code conflicts verified. KhachLink-W4 uses NATS from ADR001-W3 for event-driven push. Master plan: `UNIFIED_ROADMAP_master_plan.md`. Old plans marked superseded.
* [2026-06-29] Production BootstrapAdapter Fix — Fixed ButtonSize.Medium NotImplementedException in BootstrapAdapter (changed to return empty string). Committed and pushed to trigger CD pipeline. Commit: `ae18a88`. Branch: `main`.
* [2026-06-29] Wave 17 — KhachLink Retention & Loyalty COMPLETE. Implemented: Customer Identity (OTP login, DeviceId upgrade), Loyalty Dashboard, Order History, Store Finder, PWA bug fixes, NavMenu mobile tab bar, KhachLink Layout dynamic themes. Build 0 errors. Architecture tests 21/21 PASS. Branch: `feature/wave17-khachlink-retention`.
* [2026-06-28] Wave 17 preparation — archived Waves 8–16 to `project_state_archive.md`; 144/144 tests PASS; branch `main`.
* [2026-06-28] CI DI Fix — 144/144 integration tests PASS (commits `ddd31ed`, `884de3b`, `e4fc298`).
* [2026-06-28] Wave 16 verify + Components review — 6/12 components production ready; 6 dead/broken → Wave 17 backlog.
* [2026-06-28] Production 502 fix — ShopERP stale volume + KhachLink `Gateway__BaseUrl` + Nginx reload.
* [2026-06-26] Wave 15 — KhachLink page cleanup + Blazor routing (commit `26abd83`).
* [2026-06-26] Wave 14 — HMAC Request Signing (commit `5462759`, PR #63).
* Waves 0–14 details: `docs/AI/project_state_archive.md`

---

## 7. Maintenance Log

* **Last Updated:** 2026-06-30 — Phase 0 Session 1 COMPLETE. Architecture Validation Layer Enhancement implemented: ArchitectureConsistencyTests.cs (5 tests, 4/5 passing, 1 expected fail detecting CoreHub HTTP service bug), validate-docker-compose.ps1, validate-env-vars.ps1, enhanced startup tests. Build: 0 errors. Critical test VA-CONSISTENCY-002 correctly detects CoreHub architecture mismatch. Commit: `2e017fc`. Next: Session 2 - CI/CD integration.
* **Current Branch:** `feature/architecture-refactor-phase0-validation`
* **Current Objective:** Architecture Refactor — CoreHub & Gateway Alignment + Validation Layer Enhancement (Phase 0 Session 1 COMPLETE, Session 2 PENDING)
* **Unified Roadmap (2026-06-30):** 10/10 waves complete (100%). All waves done. Architecture reference: docs/Architecture/ADR001-Station-Architecture.md (v2 Hybrid Edge/Cloud design).
