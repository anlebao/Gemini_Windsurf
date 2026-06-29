# UNIFIED MASTER PLAN — KhachLink O2O + ADR001 Edge Infrastructure

**Created:** 2026-06-29
**Last Updated:** 2026-06-29
**Status:** IN PROGRESS — Waves 1-2 COMPLETE, Wave 3 NEXT
**Supersedes:**
  - `docs/AI/tasks/khachlink_improvements_master_plan.md`
  - `docs/AI/tasks/fix_adr001_compliance_master_plan.md`

**Branch strategy:** `feature/<plan>-<wave>-<slug>` per wave
**Execution principle:** Sequential by layer — no wave starts until previous PASSES build + tests

---

## 0. WHY MERGED (Decision Record)

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
                  └── feature/adr001-wave4-sqlite-config  [Layer 2]
                      └── feature/khachlink-wave3-personalization  [Layer 2]
                          └── feature/khachlink-wave4-order-realtime  [Layer 3]
                              └── feature/adr001-wave5-ci-edge  [Layer 4]
```

### Hard rules (không violate)
- **KHÔNG sửa `docker-compose.prod.yml`** — v1 SaaS không bao giờ thay đổi
- **KHÔNG sửa Domain layer** trừ khi feature phase approved (Customer.PushSubscriptionJson)
- **KHÔNG xóa `KitchenHub.cs`** — Kitchen vẫn dùng SignalR (staff count thấp)
- **KHÔNG bypass UI Platform components** — VanAnButton, VanAnCard, VanAnModal mandatory
- **VAPID private key KHÔNG ĐƯỢC trong source code** — environment variable only
- **KhachLink CHỈ gọi Gateway** — không inject CoreHub trực tiếp

---

## 2. OVERVIEW TABLE

| # | Wave ID | Plan | Branch | Scope | Est. | Layer | Status |
|---|---------|------|--------|-------|------|-------|--------|
| ✅ | ADR001-W1 | ADR001 | `main` (merged) | Architecture compliance test | Done | — | ✅ COMPLETE |
| ✅ | ADR001-W2 | ADR001 | `main` (merged) | `docker-compose.edge.yml` (new file) | 2-3h | 0 | ✅ COMPLETE |
| ✅ | ADR001-W3 | ADR001 | `main` (merged) | `NatsSyncWorker` + `NatsEventPublisher` | 1 day | 0 | ✅ COMPLETE |
| **3** | **KhachLink-W1** | KhachLink | `feature/khachlink-wave1-pwa-install` | PWA Install Fix | 1-2h | 1 | **NEXT** |
| **4** | **KhachLink-W2** | KhachLink | `feature/khachlink-wave2-qr-scanning` | QR Code Scanning (O2O) | 1-2d | 1 | PENDING |
| **5** | **ADR001-W4** | ADR001 | `feature/adr001-wave4-sqlite-config` | ShopERP `--sync-worker` mode | 2-3h | 2 | PENDING |
| **6** | **KhachLink-W3** | KhachLink | `feature/khachlink-wave3-personalization` | Product Personalization (Hybrid C) | 2-3d | 2 | PENDING |
| **7** | **KhachLink-W4** | KhachLink | `feature/khachlink-wave4-order-realtime` | Real-time Order Status (Polling + Web Push via NATS) | 1-2d | 3 | PENDING |
| **8** | **ADR001-W5** | ADR001 | `feature/adr001-wave5-ci-edge` | CI edge pipeline | 2-3h | 4 | PENDING |

**Total estimated:** ~7-10 days

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

### Wave 3 (KhachLink-W1): PWA Install Fix

**Branch:** `feature/khachlink-wave1-pwa-install`
**Estimated:** 1-2 hours
**Risk:** 🟢 LOW — cleanup + config only
**Task Card:** `docs/AI/tasks/wave1_pwa_install_fix_task_card.md`

**Goal:** Remove duplicate `AppInstallPrompt.razor`, activate `PWAInstallPrompt.razor`, fix VAPID placeholder.

**Tasks:**
| Task ID | Task | File | Status |
|---------|------|------|--------|
| W1-T1 | Remove duplicate `AppInstallPrompt.razor` | `5_WebApps/KhachLink/Components/AppInstallPrompt.razor` (DELETE) | PENDING |
| W1-T2 | Ensure `PWAInstallPrompt.razor` active in `App.razor` | `5_WebApps/KhachLink/Components/App.razor` | PENDING |
| W1-T3 | Disable push placeholder (VAPID configured in Wave 4) | `5_WebApps/KhachLink/wwwroot/js/pwa.js` | PENDING |
| W1-T4 | Verify service worker registration | `5_WebApps/KhachLink/Components/App.razor` | PENDING |

**Entry criteria:**
- [ ] ADR001-W3 merged to main (NATS in place — Wave 3 can start independently)
- [ ] Build clean

**Exit criteria:**
- [ ] No duplicate install prompt component
- [ ] PWA install prompt shows on mobile
- [ ] Service worker registered successfully
- [ ] `dotnet build` 0 errors

---

### Wave 4 (KhachLink-W2): QR Code Scanning

**Branch:** `feature/khachlink-wave2-qr-scanning`
**Estimated:** 1-2 days
**Risk:** 🟡 MEDIUM — new feature, camera permissions
**Task Card:** `docs/AI/tasks/wave2_qr_scanning_task_card.md`

**Goal:** Customers scan product QR codes (3rd-party app) → link opens KhachLink cart with product pre-loaded.

**Two customer flows:**
```
Flow 1 (New customer, no app):
  Scan QR → Browser opens https://diemthuong.vanantech.io.vn/menu?shopId=xxx&productId=yyy
  → KhachLink loads → product auto-added to cart → checkout

Flow 2 (Returning customer, app installed):
  Scan QR → PWA opens directly → same flow above
```

**Tasks:**
| Task ID | Task | File | Status |
|---------|------|------|--------|
| W2-T1 | Define QR URL schema: `?shopId=&productId=` | docs (decision) | PENDING |
| W2-T2 | Add QR URL param handling to Home.razor / Menu page | `5_WebApps/KhachLink/Pages/Home.razor` | PENDING |
| W2-T3 | Auto-add product to cart from URL params | `5_WebApps/KhachLink/Services/CartService.cs` | PENDING |
| W2-T4 | Generate product QR codes in ShopERP (admin UI) | `5_WebApps/ShopERP/Pages/` (admin feature) | PENDING |
| W2-T5 | Add scan entry point in NavMenu | `5_WebApps/KhachLink/Components/Layout/NavMenu.razor` | PENDING |

**Entry criteria:**
- [ ] KhachLink-W1 merged to main
- [ ] QR URL schema decided

**Exit criteria:**
- [ ] Scan product QR → KhachLink opens with product in cart
- [ ] Both new customer (browser) and returning customer (PWA) flows work
- [ ] QR codes generatable from ShopERP admin
- [ ] `dotnet build` 0 errors

**Important design decision — NO in-app camera scanning needed:**
> Customers use their native camera app or any 3rd-party QR scanner.
> KhachLink only needs to handle the resulting URL with `?shopId=&productId=` params.
> This eliminates the need for `html5-qrcode` library entirely — much simpler.

---

## 5. LAYER 2 — Backend Features

### Wave 5 (ADR001-W4): ShopERP Sync-Worker Mode

**Branch:** `feature/adr001-wave4-sqlite-config`
**Estimated:** 2-3 hours
**Risk:** 🟢 LOW — env-var controlled, v1 SaaS unaffected
**Task Card:** `docs/AI/tasks/W4-ADR-T1-card.md`, `docs/AI/tasks/W4-ADR-T2-card.md`

**Goal:** Enable ShopERP to run as `--sync-worker` (background NATS sync) without changing web app behavior.

**Tasks:**
| Task ID | Task | File | Status |
|---------|------|------|--------|
| W4-ADR-T1 | Add `--sync-worker` mode conditional DI registration | `5_WebApps/ShopERP/Program.cs` | PENDING |
| W4-ADR-T2 | Add `appsettings.Edge.json` with SQLite path + NATS config | `5_WebApps/ShopERP/appsettings.Edge.json` (NEW) | PENDING |

**Entry criteria:**
- [ ] KhachLink-W2 merged to main
- [ ] ADR001-W3 (NatsSyncWorker) merged and tested

**Exit criteria:**
- [ ] `--sync-worker` arg activates `NatsSyncWorker` DI
- [ ] `SQLITE_DB_PATH` env var configures SQLite path
- [ ] v1 SaaS behavior UNCHANGED (no arg = web app only)
- [ ] `dotnet build` 0 errors

---

### Wave 6 (KhachLink-W3): Product Personalization (Hybrid Option C)

**Branch:** `feature/khachlink-wave3-personalization`
**Estimated:** 2-3 days
**Risk:** 🟡 MEDIUM — new service + API + UI sections
**Task Card:** `docs/AI/tasks/wave3_product_personalization_task_card.md`

**Goal:** Keep global product catalog + add "Frequently Bought" + "Recently Viewed" personalized sections.

**Tasks:**
| Task ID | Task | File | Status |
|---------|------|------|--------|
| W3-T1 | Create `CustomerRecommendationService` (frequency-based) | `3_CoreHub/Services/CustomerRecommendationService.cs` (NEW) | PENDING |
| W3-T2 | Add `GET /api/products/recommended` endpoint | `5_WebApps/ShopERP/Controllers/ProductsController.cs` | PENDING |
| W3-T3 | Add recently-viewed tracking (localStorage client-side) | `5_WebApps/KhachLink/Pages/Home.razor` | PENDING |
| W3-T4 | Update `ProductHttpService.GetRecommendedProductsAsync()` | `5_WebApps/KhachLink/Services/Http/ProductHttpService.cs` | PENDING |
| W3-T5 | Add "Frequently Bought" section to Home.razor | `5_WebApps/KhachLink/Pages/Home.razor` | PENDING |
| W3-T6 | Add "Recently Viewed" section to Home.razor | `5_WebApps/KhachLink/Pages/Home.razor` | PENDING |
| W3-T7 | Implement `IMemoryCache` caching (5-min TTL) | `3_CoreHub/Services/CustomerRecommendationService.cs` | PENDING |

**Entry criteria:**
- [ ] ADR001-W4 merged to main
- [ ] Customer order history data verified available

**Exit criteria:**
- [ ] "Frequently Bought" section shows accurate recommendations
- [ ] "Recently Viewed" tracked via localStorage
- [ ] Recommendations load < 500ms (cache hit)
- [ ] Global product catalog unchanged
- [ ] `dotnet build` 0 errors

---

## 6. LAYER 3 — Integration (NATS + Push)

### Wave 7 (KhachLink-W4): Real-time Order Status

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

### Wave 8 (ADR001-W5): CI Edge Pipeline

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
- [ ] KhachLink-W4 merged to main
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
| ADR001-W4 | `ShopERP/Program.cs` (--sync-worker), `appsettings.Edge.json` (NEW) | None |
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