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

**UNIFIED ROADMAP — 10 waves (ADR001 + KhachLink, Option C Merged, Layer-ordered)**
**Master Plan:** `docs/AI/tasks/UNIFIED_ROADMAP_master_plan.md`
**Architecture Reference:** `docs/Architecture/ADR001-Station-Architecture.md` (v2 Hybrid Edge/Cloud design)

| # | Wave | Layer | Est. | Status |
|---|------|-------|------|--------|
| ✅ | ADR001-W1: Architecture compliance test | — | Done | COMPLETE |
| ✅ | ADR001-W2: `docker-compose.edge.yml` | Infra | 2-3h | COMPLETE |
| ✅ | ADR001-W3: NatsSyncWorker + NatsEventPublisher | Infra | 1d | COMPLETE |
| ✅ | KhachLink-W1: PWA Install Fix | UX | 1-2h | COMPLETE |
| ✅ | KhachLink-W2: QR Code (In-app Camera Scanning) | UX | 1-2d | COMPLETE |
| ✅ | ADR001-W4.1: SQLite Sidecar Infrastructure | Backend | 2-3h | COMPLETE |
| ✅ | ADR001-W4.2: NATS Sync Worker Mode | Backend | 2-3h | COMPLETE |
| 7 | **ADR001-W4.3: Phased Migration Validation** | Backend | 1-2h | **NEXT** |
| 8 | KhachLink-W3: Product Personalization Hybrid C | Backend | 2-3d | PENDING |
| 9 | KhachLink-W4: Real-time Order Status (Polling + NATS Push) | Integration | 1-2d | PENDING |
| 10 | ADR001-W5: CI edge pipeline | CI | 2-3h | PENDING |

**Progress:** 6/10 waves complete (60%), Layer 0-1 infrastructure + UX foundation + Layer 2 (Phase 1-2) DONE
**Total estimate:** ~10-13 days → ~4-7 days remaining
**Open Decision (required before Wave 9):** Customer.PushSubscriptionJson → Domain entity OR separate table?

---

## 3. Current Status

- **Branch:** `feature/adr001-wave4-sync-worker-mode` (verified 2026-06-29)
- **Last commit:** `078ee6e` — [WAVE 6/10] ADR001-W4.2: NATS Sync Worker Mode - Phase 2 complete
- **Build:** `dotnet build VanAn.sln` → 0 errors
- **Tests:** Architecture tests 23/23 PASS; Core tests (Nats*) 9/9 PASS; integration tests 144/144 PASS; guard-check ALL CHECKS PASSED
- **State:** Layer 2 Phase 2 (ADR001-W4.2: NATS Sync Worker Mode) COMPLETE → Layer 2 Phase 3 (ADR001-W4.3: Phased Migration Validation) next
- **Architecture Update:** ADR001-W4 split into 3 sub-waves per ADR001-Station-Architecture.md (v2 Hybrid Edge/Cloud design)

---

## 4. Next Actions

1. **[NEXT — Start now]** ADR001-W4.3: Phased Migration Validation (branch: `feature/adr001-wave4-migration-validation`)
2. **[After W4.3]** KhachLink-W3: Product Personalization Hybrid C
3. **[After W8]** KhachLink-W4: Real-time Order Status (uses NATS from ADR001-W3)
4. **[After W9]** ADR001-W5: CI edge pipeline
5. **[Decision before W9]** Customer.PushSubscriptionJson → Domain entity (A) OR separate table (B)?

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

* **Last Updated:** 2026-06-29 — Wave 6 (ADR001-W4.2) COMPLETE: NATS Sync Worker Mode Phase 2 done. --sync-worker conditional DI registration, SQLITE_DB_PATH env var override, appsettings.Edge.json created, 3 NATS sync worker services added to docker-compose.prod.yml with hybrid profile. Build 0 errors, guard-check ALL CHECKS PASSED. 6/10 waves complete (60%).
* **Current Branch:** `feature/adr001-wave4-sync-worker-mode`
* **Unified Roadmap (2026-06-29):** 6/10 waves complete (60%). Layer 0 (ADR001-W2, W3) + Layer 1 (KhachLink-W1, W2) + Layer 2 Phase 1 (ADR001-W4.1) DONE. Next: ADR001-W4.2 (NATS Sync Worker Mode). Architecture reference: docs/Architecture/ADR001-Station-Architecture.md (v2 Hybrid Edge/Cloud design).
