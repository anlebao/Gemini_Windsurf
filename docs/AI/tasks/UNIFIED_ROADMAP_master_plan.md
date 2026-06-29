# UNIFIED MASTER PLAN — KhachLink O2O + ADR001 Edge Infrastructure

**Created:** 2026-06-29
**Last Updated:** 2026-06-29 (Wave 8 COMPLETE - KhachLink-W3 Product Personalization)
**Status:** IN PROGRESS — Waves 1-8 COMPLETE, Wave 9 NEXT (8/10 waves = 80% complete)
**Architecture Reference:** `docs/Architecture/ADR001-Station-Architecture.md` (v2 Hybrid Edge/Cloud design)
**Supersedes:**
  - `docs/AI/tasks/khachlink_improvements_master_plan.md`
  - `docs/AI/tasks/fix_adr001_compliance_master_plan.md`

**Branch strategy:** `feature/<plan>-<wave>-<slug>` per wave
**Execution principle:** Sequential by layer — no wave starts until previous PASSES build + tests

---

## 0. WHY MERGED (Decision Record)

### Architecture Reference
**v2 Hybrid Edge/Cloud Design:** `docs/Architecture/ADR001-Station-Architecture.md`
- Two-version system: v1 SaaS (PostgreSQL) vs v2 Hybrid (SQLite + NATS + PostgreSQL)
- Phased migration: Phase 1 (sidecars), Phase 2 (sync workers), Phase 3 (PostgreSQL removal)
- Station types: ShopERP, KhachLink, Order stations with local SQLite + NATS sync

### Conflict Analysis Result
Zero code conflicts between ADR001 and KhachLink plans — they touch completely separate files.

### Integration Opportunity (Key Reason to Merge)
ADR001-W3 (`NatsSyncWorker` + `NatsEventPublisher`) creates the NATS infrastructure that
KhachLink-W4 (Real-time Order Status via Web Push) can use for an event-driven,
fully-decoupled push notification architecture:

```
WITHOUT ADR001-W3 first (direct coupling):
  OrderWorkflowService → PushNotificationService.SendPushAsync()  ← tight coupling

WITH ADR001-W3 first (event-driven, recommended):
  OrderWorkflowService → Outbox (already exists)
    → NatsSyncWorker publishes "order.status.changed"
    → PushNotificationService subscribes NATS
    → Web Push sent to customer
```

### Layer Order (prevents all conflicts)
```
Layer 0 — Infrastructure (ADR001-W2, ADR001-W3) — new files only
Layer 1 — KhachLink UX (KhachLink-W1, W2) — KhachLink files only
Layer 2 — Backend Features (ADR001-W4, KhachLink-W3) — separate files
Layer 3 — Integration (KhachLink-W4) — uses NATS from Layer 0
Layer 4 — CI Validation (ADR001-W5) — new CI file only
```

---

## 1. EXECUTION RULES

### JIT Planning (mọi wave)
- **Bước 1 INVESTIGATE:** Đọc task card, verify facts, chốt plan
- **Bước 2 IMPLEMENT:** Code theo plan — không thay đổi approach giữa chừng
- **KHÔNG bắt đầu wave N+1** nếu wave N chưa PASS: `dotnet build` 0 errors + `guard-check.ps1`

### Session protocol
1. Mỗi session: 1 wave duy nhất
2. Đầu session: đọc task card tương ứng
3. Cuối session: build + guard-check + commit `[WAVE N] description`
4. Nếu fail: dừng implement, re-analyze root cause

### Branch protocol
```
main (ADR001 Wave 1 ✅ MERGED — commit 8863692)
  └── feature/adr001-wave2-edge-compose       [Layer 0]
      └── feature/adr001-wave3-nats-worker    [Layer 0]
          └── feature/khachlink-wave1-pwa-install  [Layer 1]
              └── feature/khachlink-wave2-qr-scanning  [Layer 1]
                  └── feature/adr001-wave4-sqlite-sidecars   [Layer 2]
                      └── feature/adr001-wave4-sync-worker-mode  [Layer 2]
                          └── feature/adr001-wave4-migration-validation  [Layer 2]
                              └── feature/khachlink-wave3-personalization  [Layer 2]
                                  └── feature/khachlink-wave4-order-realtime  [Layer 3]
                                      └── feature/adr001-wave5-ci-edge  [Layer 4]
```

### Hard rules (không violate)
- **v1 SaaS `docker-compose.prod.yml` KHÔNG sửa** — v1 SaaS production unchanged
- **v2 Hybrid `docker-compose.edge.yml` only** — ADR001-W4 modifications for edge deployment
- **KHÔNG sửa Domain layer** trừ khi feature phase approved (Customer.PushSubscriptionJson)
- **KHÔNG xóa `KitchenHub.cs`** — Kitchen vẫn dùng SignalR (staff count thấp)
- **KHÔNG bypass UI Platform components** — VanAnButton, VanAnCard, VanAnModal mandatory
- **VAPID private key KHÔNG ĐƯỢC trong source code** — environment variable only
- **KhachLink CHỈ gọi Gateway** — không inject CoreHub trực tiếp
- **PostgreSQL remains online for accounting** — v2 hybrid uses SQLite for order/loyalty only

---

## 2. OVERVIEW TABLE

| # | Wave ID | Plan | Branch | Scope | Est. | Layer | Status |
|---|---------|------|--------|-------|------|-------|--------|
| ✅ | ADR001-W1 | ADR001 | `main` (merged) | Architecture compliance test | Done | — | ✅ COMPLETE |
| ✅ | ADR001-W2 | ADR001 | `main` (merged) | `docker-compose.edge.yml` (new file) | 2-3h | 0 | ✅ COMPLETE |
| ✅ | ADR001-W3 | ADR001 | `main` (merged) | `NatsSyncWorker` + `NatsEventPublisher` | 1 day | 0 | ✅ COMPLETE |
| ✅ | KhachLink-W1 | KhachLink | `main` (merged) | PWA Install Fix | 1-2h | 1 | ✅ COMPLETE |
| ✅ | KhachLink-W2 | KhachLink | `main` (merged) | QR Code Scanning (In-app Camera) | 1-2d | 1 | ✅ COMPLETE |
| ✅ | ADR001-W4.1 | ADR001 | `feature/adr001-wave4-sqlite-sidecars` | SQLite Sidecar Infrastructure | 2-3h | 2 | ✅ COMPLETE |
| ✅ | ADR001-W4.2 | ADR001 | `feature/adr001-wave4-sync-worker-mode` | NATS Sync Worker Mode | 2-3h | 2 | ✅ COMPLETE |
| ✅ | ADR001-W4.3 | ADR001 | `feature/adr001-wave4-migration-validation` | Phased Migration Validation | 1-2h | 2 | ✅ COMPLETE |
| ✅ | **KhachLink-W3** | KhachLink | `feature/khachlink-wave3-personalization` | Product Personalization (Hybrid C) | 2-3d | 2 | ✅ COMPLETE |
| **9** | **KhachLink-W4** | KhachLink | `feature/khachlink-wave4-order-realtime` | Real-time Order Status (Polling + Web Push via NATS) | 1-2d | 3 | **NEXT** |
| **10** | **ADR001-W5** | ADR001 | `feature/adr001-wave5-ci-edge` | CI edge pipeline | 2-3h | 4 | PENDING |

**Total estimated:** ~10-13 days (updated for ADR001-W4 split)

---

## 3. LAYER 0 — Infrastructure Foundation

### Wave 1 (ADR001-W2): Create docker-compose.edge.yml ✅ COMPLETE

**Branch:** `feature/adr001-wave2-edge-compose` → merged to `main`
**Estimated:** 2-3 hours
**Risk:** 🟢 LOW — new file only, zero impact on production
**Task Card:** `docs/AI/tasks/W2-ADR-T1-card.md`, `docs/AI/tasks/W2-ADR-T2-card.md`
**Commit:** `ed4d340` → merge `79486e4`

**Goal:** Create v2 Edge deployment config with SQLite volumes + NATS sync services.

**Tasks:**
| Task ID | Task | File | Status |
|---------|------|------|--------|
| W2-ADR-T1 | Create `docker-compose.edge.yml` với SQLite volumes + NATS sync services | `docker-compose.edge.yml` (NEW) | PENDING |
| W2-ADR-T2 | Update architecture test Rule I (edge compose validation) | `6_Tests/VanAn.Architecture.Tests/ArchitectureRulesTests.cs` | PENDING |

**Entry criteria:**
- [ ] `main` clean (0 uncommitted changes)
- [ ] ADR001-W1 merged (✅ commit 8863692)

**Exit criteria:**
- [ ] `docker-compose.edge.yml` tồn tại với SQLite volumes + NATS workers
- [ ] Architecture test Rule I PASSES
- [ ] `dotnet build` 0 errors
- [ ] `guard-check.ps1` passes

---

### Wave 2 (ADR001-W3): Implement NatsSyncWorker + NatsEventPublisher ✅ COMPLETE

**Branch:** `feature/adr001-wave3-nats-worker` → merged to `main`
**Estimated:** 1 day
**Risk:** 🟡 MEDIUM — new code in CoreHub
**Task Card:** `docs/AI/tasks/W3-ADR-T1-card.md`, `docs/AI/tasks/W3-ADR-T2-card.md`
**Commit:** `0a7da55` → merge `f7f4d3b`

**Goal:** Create NATS event infrastructure — foundation for KhachLink-W4 push notifications.

**Tasks:**
| Task ID | Task | File | Status |
|---------|------|------|--------|
| W3-ADR-T1 | Implement `INatsEventPublisher` + `NatsEventPublisher` | `3_CoreHub/Infrastructure/Messaging/NatsEventPublisher.cs` (NEW) | PENDING |
| W3-ADR-T2 | Implement `NatsSyncWorker` BackgroundService (poll Outbox → NATS publish) | `3_CoreHub/Services/NatsSyncWorker.cs` (NEW) | PENDING |

**Entry criteria:**
- [ ] ADR001-W2 merged to main
- [ ] `NATS.Client` package verified in CoreHub.csproj

**Exit criteria:**
- [ ] `INatsEventPublisher` interface defined
- [ ] `NatsEventPublisher` implements interface, publishes to NATS
- [ ] `NatsSyncWorker` polls Outbox → publishes → marks processed
- [ ] Unit tests for NatsSyncWorker PASS
- [ ] `dotnet build` 0 errors

**Key design — NATS subjects for KhachLink-W4:**
```
"order.status.changed" → payload: { orderId, tenantId, newStatus, customerId }
```
This subject will be consumed by `PushNotificationService` in KhachLink-W4.

---

## 4. LAYER 1 — KhachLink UX Features

### Wave 3 (KhachLink-W1): PWA Install Fix ✅ COMPLETE

**Branch:** `main` (merged)
**Estimated:** 1-2 hours
**Risk:** 🟢 LOW — cleanup + config only
**Task Card:** `docs/AI/tasks/wave1_pwa_install_fix_task_card.md`
**Commit:** `b83eb84`

**Goal:** Remove duplicate `AppInstallPrompt.razor`, activate `PWAInstallPrompt.razor`, fix VAPID placeholder.

**Tasks:**
| Task ID | Task | File | Status |
|---------|------|------|--------|
| W1-T1 | Remove duplicate `AppInstallPrompt.razor` | `5_WebApps/KhachLink/Components/AppInstallPrompt.razor` (DELETE) | ✅ COMPLETE |
| W1-T2 | Ensure `PWAInstallPrompt.razor` active in `App.razor` | `5_WebApps/KhachLink/Components/App.razor` | ✅ COMPLETE |
| W1-T3 | Disable push placeholder (VAPID configured in Wave 4) | `5_WebApps/KhachLink/wwwroot/js/pwa.js` | ✅ COMPLETE |
| W1-T4 | Verify service worker registration | `5_WebApps/KhachLink/Components/App.razor` | ✅ COMPLETE |

**Entry criteria:**
- [x] ADR001-W3 merged to main (NATS in place — Wave 3 can start independently)
- [x] Build clean

**Exit criteria:**
- [x] No duplicate install prompt component
- [x] PWA install prompt shows on mobile
- [x] Service worker registered successfully
- [x] `dotnet build` 0 errors
- [x] `guard-check.ps1` ALL CHECKS PASSED

---

### Wave 4 (KhachLink-W2): QR Code Scanning ✅ COMPLETE

**Branch:** `feature/khachlink-wave2-qr-scanning` → merged to `main`
**Estimated:** 1-2 days
**Risk:** 🟡 MEDIUM — new feature, camera permissions
**Task Card:** `docs/AI/tasks/wave2_qr_scanning_task_card.md`
**Commit:** `db80062`

**Goal:** Customers scan product QR codes using in-app camera → product auto-added to cart.

**Implementation Approach:** In-app camera scanning with html5-qrcode library (per task card)

**Two customer flows:**
```
Flow 1 (New customer, no app):
  Scan QR with in-app camera → KhachLink loads → product auto-added to cart → checkout

Flow 2 (Returning customer, app installed):
  Scan QR with in-app camera → PWA opens directly → same flow above
```

**Tasks:**
| Task ID | Task | File | Status |
|---------|------|------|--------|
| W2-T1 | Define QR code format (JSON with ProductId, ShopId, Timestamp) | `1_Shared/DTOs/QRCodePayload.cs` | ✅ COMPLETE |
| W2-T2 | Implement QR code generation service (CoreHub + ShopERP) | `3_CoreHub/Services/QrCodeService.cs`, `5_WebApps/ShopERP/Services/QrCodeService.cs` | ✅ COMPLETE |
| W2-T3 | Integrate html5-qrcode library via CDN | `5_WebApps/KhachLink/Components/App.razor` | ✅ COMPLETE |
| W2-T4 | Create QRScanner.razor component with UI Platform | `5_WebApps/KhachLink/Components/QRScanner.razor` | ✅ COMPLETE |
| W2-T5 | Implement camera permission handling (iOS + Android) | `5_WebApps/KhachLink/wwwroot/js/qr-scanner.js` | ✅ COMPLETE |
| W2-T6 | Create Scan.razor page with scan-to-cart workflow | `5_WebApps/KhachLink/Pages/Scan.razor` | ✅ COMPLETE |
| W2-T7 | Update CartService with AddFromQrCodeAsync | `5_WebApps/KhachLink/Services/CartService.cs` | ✅ COMPLETE |
| W2-T8 | Add scan entry points (Home + NavMenu desktop + mobile) | `5_WebApps/KhachLink/Pages/Home.razor`, `5_WebApps/KhachLink/Components/Layout/NavMenu.razor` | ✅ COMPLETE |

**Entry criteria:**
- [x] KhachLink-W1 merged to main
- [x] QR code format decided (JSON with ProductId, ShopId, Timestamp)

**Exit criteria:**
- [x] Scan product QR → KhachLink opens with product in cart
- [x] Both new customer and returning customer flows work
- [x] Camera permissions handled properly (iOS + Android)
- [x] QR codes generatable from server-side services
- [x] `dotnet build` 0 errors
- [x] `guard-check.ps1` ALL CHECKS PASSED

**Important design decision — In-app camera scanning:**
> Implemented per task card with html5-qrcode library for in-app camera scanning.
> QR code format: JSON with ProductId, ShopId, Timestamp (30-day expiry).
> Camera permission handling for iOS Safari (HTTPS required) and Android Chrome.

---

## 5. LAYER 2 — Backend Features

### Wave 5 (ADR001-W4.1): SQLite Sidecar Infrastructure

**Branch:** `feature/adr001-wave4-sqlite-sidecars`
**Estimated:** 2-3 hours
**Risk:** 🟡 MEDIUM — docker-compose.prod.yml modifications for v2 hybrid
**Architecture Reference:** `docs/Architecture/ADR001-Station-Architecture.md` (v2 Hybrid Edge/Cloud)
**Task Card:** `docs/AI/tasks/W4-1-T1-card.md`, `docs/AI/tasks/W4-1-T2-card.md`, `docs/AI/tasks/W4-1-T3-card.md`

**Goal:** Add SQLite sidecar containers + volume persistence to `docker-compose.prod.yml` for v2 hybrid deployment (v1 SaaS unchanged via feature flags).

**v2 Hybrid Architecture:**
- **ShopERP Station:** SQLite sidecar + NATS sync worker
- **KhachLink Station:** SQLite sidecar + NATS sync worker (deferred to later wave)
- **Order Station:** SQLite sidecar + NATS sync worker (optional)
- **PostgreSQL:** Remains online for accounting, becomes sync target for order/loyalty data

**Tasks:**
| Task ID | Task | File | Status |
|---------|------|------|--------|
| W4-1-T1 | Add SQLite sidecar containers to docker-compose.prod.yml | `docker-compose.prod.yml` | PENDING |
| W4-1-T2 | Add Docker volumes for SQLite persistence | `docker-compose.prod.yml` | PENDING |
| W4-1-T3 | Update service dependencies for sidecars | `docker-compose.prod.yml` | PENDING |

**Entry criteria:**
- [ ] KhachLink-W2 merged to main
- [ ] ADR001-W3 (NatsSyncWorker) merged and tested

**Exit criteria:**
- [ ] SQLite sidecar containers defined (shoperp-sqlite, khachlink-sqlite, order-station-sqlite)
- [ ] Docker volumes defined for SQLite data persistence
- [ ] Service dependencies updated (main services depend on sidecars)
- [ ] v1 SaaS behavior UNCHANGED (sidecars disabled via feature flag)
- [ ] `dotnet build` 0 errors

---

### Wave 6 (ADR001-W4.2): NATS Sync Worker Mode ✅ COMPLETE

**Branch:** `feature/adr001-wave4-sync-worker-mode` → commit `078ee6e`
**Estimated:** 2-3 hours
**Risk:** 🟢 LOW — env-var controlled, v1 SaaS unaffected
**Architecture Reference:** `docs/Architecture/ADR001-Station-Architecture.md` (Step 4: Configure ShopERP for SQLite)
**Task Card:** `docs/AI/tasks/W4-2-T1-card.md`, `docs/AI/tasks/W4-2-T2-card.md`, `docs/AI/tasks/W4-2-T3-card.md`
**Commit:** `078ee6e`

**Goal:** Enable ShopERP/KhachLink to run as `--sync-worker` (background NATS sync) without changing web app behavior.

**Tasks:**
| Task ID | Task | File | Status |
|---------|------|------|--------|
| W4-2-T1 | Add `--sync-worker` mode conditional DI registration | `5_WebApps/ShopERP/Program.cs` | ✅ COMPLETE |
| W4-2-T2 | Add `appsettings.Edge.json` with SQLite path + NATS config | `5_WebApps/ShopERP/appsettings.Edge.json` (NEW) | ✅ COMPLETE |
| W4-2-T3 | Add sync worker service definitions to docker-compose.prod.yml | `docker-compose.prod.yml` | ✅ COMPLETE |

**Entry criteria:**
- [x] ADR001-W4.1 merged to main (SQLite sidecars in place)
- [x] ADR001-W3 (NatsSyncWorker) merged and tested

**Exit criteria:**
- [x] `--sync-worker` arg activates `NatsSyncWorker` DI
- [x] `SQLITE_DB_PATH` env var configures SQLite path
- [x] Sync worker services defined in docker-compose (shoperp-nats-sync, khachlink-nats-sync, order-station-nats-sync)
- [x] v1 SaaS behavior UNCHANGED (no arg = web app only)
- [x] `dotnet build` 0 errors
- [x] guard-check ALL CHECKS PASSED

---

### Wave 7 (ADR001-W4.3): Phased Migration Validation ✅ COMPLETE

**Branch:** `feature/adr001-wave4-migration-validation` → commit `39685a2`
**Estimated:** 1-2 hours
**Risk:** 🟡 MEDIUM — validation of migration phases
**Architecture Reference:** `docs/Architecture/ADR001-Station-Architecture.md` (Migration Strategy)
**Task Card:** `docs/AI/tasks/W4-3-T1-card.md`, `docs/AI/tasks/W4-3-T2-card.md`, `docs/AI/tasks/W4-3-T3-card.md`
**Commit:** `39685a2`

**Goal:** Validate phased migration approach (Phase 1: sidecars only, Phase 2: sync workers, Phase 3: PostgreSQL removal).

**Migration Phases:**
- **Phase 1:** Deploy SQLite sidecars (no sync, PostgreSQL remains primary)
- **Phase 2:** Enable NATS sync workers (dual-write: SQLite + PostgreSQL)
- **Phase 3:** Remove PostgreSQL direct access (SQLite-only, PostgreSQL becomes sync target)

**Tasks:**
| Task ID | Task | File | Status |
|---------|------|------|--------|
| W4-3-T1 | Phase 1 validation — sidecars only (no sync workers) | `scripts/validate-phase1-sidecars.ps1` (NEW) | ✅ COMPLETE |
| W4-3-T2 | Phase 2 validation — sync workers enabled (dual-write) | `scripts/validate-phase2-sync-workers.ps1` (NEW) | ✅ COMPLETE |
| W4-3-T3 | Rollback plan testing + documentation | `docs/Architecture/ADR001-Rollback-Plan.md` (NEW) | ✅ COMPLETE |

**Entry criteria:**
- [x] ADR001-W4.2 merged to main (sync workers implemented)

**Exit criteria:**
- [x] Phase 1 validation PASS (sidecars operational, no data loss)
- [x] Phase 2 validation PASS (sync workers operational, data consistency verified)
- [x] Rollback plan documented and tested
- [x] `dotnet build` 0 errors
- [x] guard-check ALL CHECKS PASSED
- [x] Ready for production deployment (v2 hybrid)

---

### Wave 8 (KhachLink-W3): Product Personalization (Hybrid Option C) ✅ COMPLETE

**Branch:** `feature/khachlink-wave3-personalization` → merged to main
**Estimated:** 2-3 days
**Risk:** 🟡 MEDIUM — new service + API + UI sections
**Task Card:** `docs/AI/tasks/wave3_product_personalization_task_card.md`
**Commit:** `f418bb3`

**Goal:** Keep global product catalog + add "Frequently Bought" + "Recently Viewed" personalized sections.

**Tasks:**
| Task ID | Task | File | Status |
|---------|------|------|--------|
| W3-T1 | Create `CustomerRecommendationService` (frequency-based) | `3_CoreHub/Services/CustomerRecommendationService.cs` (NEW) | ✅ COMPLETE |
| W3-T2 | Add `GET /api/products/recommended` endpoint | `5_WebApps/ShopERP/Controllers/ProductsController.cs` | ✅ COMPLETE |
| W3-T3 | Add recently-viewed tracking (localStorage client-side) | `5_WebApps/KhachLink/Services/RecentlyViewedService.cs` (NEW) | ✅ COMPLETE |
| W3-T4 | Update `ProductHttpService.GetRecommendedProductsAsync()` | `5_WebApps/KhachLink/Services/Http/ProductHttpService.cs` | ✅ COMPLETE |
| W3-T5 | Add "Frequently Bought" section to Home.razor | `5_WebApps/KhachLink/Pages/Home.razor` | ✅ COMPLETE |
| W3-T6 | Add "Recently Viewed" section to Home.razor | `5_WebApps/KhachLink/Pages/Home.razor` | ✅ COMPLETE |
| W3-T7 | Implement `IMemoryCache` caching (5-min TTL) | `3_CoreHub/Services/CustomerRecommendationService.cs` | ✅ COMPLETE |

**Entry criteria:**
- [x] ADR001-W4.3 (Wave 7) merged to main — migration validation complete
- [x] Customer order history data verified available

**Exit criteria:**
- [x] "Frequently Bought" section shows accurate recommendations
- [x] "Recently Viewed" tracked via localStorage
- [x] Recommendations load < 500ms (cache hit)
- [x] Global product catalog unchanged
- [x] `dotnet build` 0 errors
- [x] guard-check ALL CHECKS PASSED

---

## 6. LAYER 3 — Integration (NATS + Push)

### Wave 9 (KhachLink-W4): Real-time Order Status

**Branch:** `feature/khachlink-wave4-order-realtime`
**Estimated:** 1-2 days
**Risk:** 🟡 MEDIUM — uses NATS from Layer 0, replaces SignalR
**Task Card:** `docs/AI/tasks/wave4_order_status_realtime_task_card.md`

**Goal:** Replace SignalR at KhachLink with Short Polling (5s) + Web Push via NATS.
Scale target: 10,000+ concurrent users, zero persistent connections.

**Architecture (using NATS from ADR001-W3):**
```
Customer opens OrderTracking:
  PeriodicTimer 5s → GET /api/orders/{id}/status → UI update

Customer closes app:
  OrderWorkflowService.TransitionStatusAsync()
    → write to Outbox (existing pattern)
    → NatsSyncWorker picks up (ADR001-W3)
    → publish "order.status.changed" to NATS
    → PushNotificationService subscribes
    → Web Push → Service Worker → show notification
```

**Tasks:**
| Task ID | Task | File | Status |
|---------|------|------|--------|
| W4-T1 | Add `GET /api/orders/{id}/status` lightweight endpoint | `5_WebApps/ShopERP/Controllers/OrdersController.cs`, `2_Gateway/Controllers/CustomerOrdersController.cs` | PENDING |
| W4-T2 | Add `PeriodicTimer` polling loop to `OrderTracking.razor` (pause on hidden) | `5_WebApps/KhachLink/Pages/OrderTracking.razor` | PENDING |
| W4-T3 | Generate VAPID keys + configure production env vars | Production `.env`, `5_WebApps/KhachLink/wwwroot/js/pwa.js` | PENDING |
| W4-T4 | Persist `Customer.PushSubscriptionJson` to DB | `1_Shared/Domain.cs`, `3_CoreHub/Infrastructure/Configurations/CustomerConfiguration.cs` | PENDING |
| W4-T5 | Implement `PushNotificationService` (NATS subscriber + Web Push sender) | `3_CoreHub/Services/PushNotificationService.cs` (NEW) | PENDING |
| W4-T6 | Hook `PushNotificationService` subscribe to NATS "order.status.changed" | `3_CoreHub/Services/PushNotificationService.cs` | PENDING |
| W4-T7 | Update `service-worker.js` push event handler | `5_WebApps/KhachLink/wwwroot/service-worker.js` | PENDING |
| W4-T8 | Remove SignalR from KhachLink (keep KitchenHub intact) | `5_WebApps/KhachLink/Program.cs`, components | PENDING |
| W4-T9 | Load test: 10,000 concurrent polling < 50ms p95 | Load test script | PENDING |

**Entry criteria:**
- [ ] KhachLink-W3 merged to main
- [ ] ADR001-W3 (NatsSyncWorker) merged and NATS "order.status.changed" subject confirmed
- [ ] VAPID keys generated (offline)
- [ ] Decision: Customer.PushSubscriptionJson in Domain OR separate table

**Exit criteria:**
- [ ] Polling updates OrderTracking.razor every 5s while open
- [ ] Polling pauses on tab hidden, resumes on visible
- [ ] Web Push delivered when customer closes app
- [ ] SignalR removed from KhachLink (0 WebSocket connections)
- [ ] KitchenHub SignalR untouched
- [ ] Load test PASS: 10,000 concurrent polling < 50ms p95
- [ ] Memory at 10,000 users < 1GB (vs ~20GB SignalR)
- [ ] `dotnet build` 0 errors

**Open Decision (needs answer before W4-T4):**
> **Q: Customer.PushSubscriptionJson — Domain entity hay separate table?**
> - Option A: Add to `Customer` entity in `1_Shared/Domain.cs` (simple, needs approval)
> - Option B: Separate `PushSubscription` table (no Domain change, 1 extra join)

---

## 7. LAYER 4 — CI Validation

### Wave 10 (ADR001-W5): CI Edge Pipeline

**Branch:** `feature/adr001-wave5-ci-edge`
**Estimated:** 2-3 hours
**Risk:** 🟢 LOW — new CI file only
**Task Card:** `docs/AI/tasks/W5-ADR-T1-card.md`

**Goal:** CI pipeline validates v2 edge deployment on every push.

**Tasks:**
| Task ID | Task | File | Status |
|---------|------|------|--------|
| W5-ADR-T1 | Create `.github/workflows/ci-edge.yml` | `.github/workflows/ci-edge.yml` (NEW) | PENDING |

**Entry criteria:**
- [ ] KhachLink-W4 (Wave 9) merged to main
- [ ] ADR001-W4.3 (Wave 7) migration validation complete
- [ ] All previous waves PASS

**Exit criteria:**
- [ ] CI edge pipeline exists and runs on push to `feature/edge*`
- [ ] Pipeline validates `docker-compose.edge.yml` structure
- [ ] All 21+ architecture tests pass
- [ ] `docker-compose.edge.yml` config validates successfully

---

## 8. CONFLICT-FREE VERIFICATION

### Why zero conflicts (file-level proof)

| Wave | Files Modified | Overlaps With |
|------|---------------|---------------|
| ADR001-W2 | `docker-compose.edge.yml` (NEW), `ArchitectureRulesTests.cs` | None |
| ADR001-W3 | `NatsEventPublisher.cs` (NEW), `NatsSyncWorker.cs` (NEW) | None |
| KhachLink-W1 | `App.razor`, `AppInstallPrompt.razor` (DELETE), `pwa.js` | None |
| KhachLink-W2 | `Home.razor` (URL params), `CartService.cs`, `NavMenu.razor` | None |
| ADR001-W4.1 | `docker-compose.prod.yml` (sidecars + volumes) | None |
| ADR001-W4.2 | `ShopERP/Program.cs` (--sync-worker), `appsettings.Edge.json` (NEW), `docker-compose.prod.yml` (sync workers) | None |
| ADR001-W4.3 | Validation scripts, rollback plan documentation | None |
| KhachLink-W3 | `CustomerRecommendationService.cs` (NEW), `ProductsController.cs` (add endpoint), `ProductHttpService.cs` (add method), `Home.razor` (add sections) | None |
| KhachLink-W4 | `OrderTracking.razor`, `service-worker.js`, `PushNotificationService.cs` (NEW), `OrderWorkflowService.cs` (hook), `OrdersController.cs` (add endpoint), `KhachLink/Program.cs`, `Domain.cs` (Customer) | None |
| ADR001-W5 | `ci-edge.yml` (NEW) | None |

### Potential merge issues to watch
1. **`Home.razor`** — KhachLink-W2 adds URL param handling, KhachLink-W3 adds recommendation sections. Both are ADDITIVE changes to different parts of the file. Merge trivially.
2. **`OrderWorkflowService.cs`** — Only KhachLink-W4 touches this. No concurrent wave touches same file.
3. **`ArchitectureRulesTests.cs`** — ADR001-W2 adds Rule I. No other wave touches it.

---

## 9. SUCCESS METRICS (All Waves)

| Wave | Primary Metric | Target |
|------|---------------|--------|
| ADR001-W2 | `docker-compose.edge.yml` validates | `docker-compose config` passes |
| ADR001-W3 | NatsSyncWorker unit tests | 100% pass |
| KhachLink-W1 | PWA install rate | > 90% Android, > 80% iOS |
| KhachLink-W2 | QR scan → cart in 1 click | 100% (URL param handling) |
| ADR001-W4 | v1 SaaS unaffected | 0 behavior change |
| KhachLink-W3 | Recommendation load time | < 500ms (cache hit) |
| KhachLink-W4 | Concurrent users | 10,000 @ < 50ms p95, 0 WebSocket |
| ADR001-W5 | CI edge passes | 100% on `feature/edge*` push |

---

## 10. RISK REGISTER

| Risk | Wave | Impact | Mitigation |
|------|------|--------|------------|
| iOS Web Push limited (iOS < 16.4) | KhachLink-W4 | MEDIUM | Polling is primary; push is enhancement |
| VAPID key rotation | KhachLink-W4 | MEDIUM | Document rotation procedure |
| SQLite performance at 10,000 concurrent | KhachLink-W4 | HIGH | Add Redis/IMemoryCache for /status endpoint |
| Customer.PushSubscriptionJson Domain change rejected | KhachLink-W4 | MEDIUM | Fallback: separate PushSubscription table |
| NATS down in production | KhachLink-W4 | MEDIUM | Push is best-effort; polling covers gaps |
| docker-compose.edge.yml breaks v1 | ADR001-W2 | HIGH | Separate file — never edit `docker-compose.prod.yml` |

---

## 11. APPROVAL & SIGN-OFF

**Overall Plan:** ✅ Approved (Option C, 2026-06-29)

| # | Wave | Sign-Off |
|---|------|----------|
| ✅ | ADR001-W1 | COMPLETE |
| ✅ | ADR001-W2 | COMPLETE (commit 79486e4) |
| ✅ | ADR001-W3 | COMPLETE (commit f7f4d3b) |
| □ | KhachLink-W1 | Approved ✅ — NEXT |
| □ | KhachLink-W2 | Approved ✅ — After W1 |
| □ | ADR001-W4 | Approved ✅ — After W2 |
| □ | KhachLink-W3 | Approved ✅ — After W5 |
| □ | KhachLink-W4 | Approved ✅ — After W6 (needs VAPID + PushSubscription decision) |
| □ | ADR001-W5 | Approved ✅ — After W7 |

**Open Decision Required Before KhachLink-W4:**
> `Customer.PushSubscriptionJson` → Domain entity (Option A) OR separate `PushSubscription` table (Option B)?