# TASK CARD: PWA-OFFLINE - Phase 4 - Offline Write Queue (Checkout POST)

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Queue checkout POST trong IndexedDB khi offline → replay via Background Sync API khi có mạng lại. Idempotency đảm bảo không duplicate orders.
- **Nghiệp vụ áp dụng:** Khách trong quán mất mạng → vẫn đặt hàng được → order queue → có mạng lại → auto-sync → Gateway tạo order → NATS → ShopERP kitchen display.

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `.devin/workflows/newfeaturebuild.md`
- **Execution Mode:** IMPLEMENT (involves Gateway change — Idempotency-Key)

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md`
  - `5_WebApps/KhachLink/Pages/Checkout.razor` (submit handler)
  - `5_WebApps/KhachLink/Services/OfflineQueueService.cs` (NEW)
  - `5_WebApps/KhachLink/Services/IndexedDBService.cs` (existing — extend)
  - `5_WebApps/KhachLink/wwwroot/js/pwa.js` (sync.register + replayQueuedCheckouts)
  - `5_WebApps/KhachLink/wwwroot/service-worker.js` (add `sync` event handler)
  - `2_Gateway/Controllers/PublicOrdersController.cs` (Idempotency-Key check)
  - `3_CoreHub/Services/OrderService.cs` (idempotency logic — if needed)
- **Boundary Rules:**
  - **Hard Stop:** KHÔNG modify Domain layer (Order entity) cho idempotency — dùng Gateway-level check (cache hoặc DB lookup by Idempotency-Key).
  - KHÔNG thay đổi Order creation logic (OrderService.CreateOrderFromCommandAsync).
  - Idempotency check = lookup existing order by Idempotency-Key header → return existing order if found.

## 4. TECHNICAL & REGULATORY CONSTRAINTS
- [ ] **Client-side UUIDv7:** Order ID generated on client BEFORE queue (stable across retries).
- [ ] **Idempotency-Key header:** Mỗi queued order có `Idempotency-Key` (UUIDv7). Gateway check duplicate.
- [ ] **Background Sync API:** `serviceWorkerRegistration.sync.register('vanan-checkout-sync')` via JS interop.
- [ ] **Service worker `sync` event:** Add handler (currently missing).
- [ ] **iOS Safari fallback:** Replay queue on `online` event + `visibilitychange` (no Background Sync on iOS).
- [ ] **Domain protection:** Idempotency logic ở Gateway controller level, KHÔNG sửa Domain.

## 5. SUCCESS CRITERIA
- [ ] SC1: `OfflineQueueService.cs` created — wraps IndexedDB `sync-queue` store.
- [ ] SC2: `Checkout.razor` submit handler: if offline → queue + register sync + show toast.
- [ ] SC3: Service worker `sync` event handler added — replays queued POSTs.
- [ ] SC4: `replayQueuedCheckouts()` in pwa.js — reads IndexedDB, POST to Gateway with `Idempotency-Key`, marks sent on 2xx.
- [ ] SC5: Gateway `PublicOrdersController.checkout` — checks `Idempotency-Key` header, returns existing order if duplicate.
- [ ] SC6: iOS Safari fallback — `online` event + `visibilitychange` triggers replay.
- [ ] SC7: `dotnet build VanAn.sln` PASS.
- [ ] SC8: RV: queue checkout offline → reconnect → order appears in Gateway PG.
- [ ] SC9: **Idempotency RV:** Background Sync fires 3x → only 1 order in PG.
- [ ] SC10: Order syncs to ShopERP SQLite via NATS (verify kitchen display).
- [ ] SC11: Toast UI: "Đơn hàng đã lưu, sẽ gửi khi có mạng".

**Implementation Date:** _TBD_
**Branch:** `feature/khachlink-wasm`

## 6. ACTIVE SKILLS (MAX 3)
- `outbox-pattern-implementation` — queue + replay pattern tương tự Outbox
- `domain-integrity-validation` — ensure không break Domain
- `pattern-based-fixing` — JS interop + service worker patterns

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 5
- **Verified Facts:**
  - Fact 1: pwa.js line 310 đã define IndexedDB `sync-queue` store
  - Fact 2: pwa.js line 274 `syncData()` function exists (placeholder, not wired)
  - Fact 3: service-worker.js KHÔNG có `sync` event handler
  - Fact 4: PublicOrdersController.checkout hiện không check Idempotency-Key
  - Fact 5: Order ID hiện generate server-side (OrderService.CreateOrderFromCommandAsync)
- **Assumptions:**
  - A1: Client-side UUIDv7 generation possible (UUIDNext package đã có trong KhachLink).
  - A2: Gateway có thể store Idempotency-Key (cần table hoặc cache — quyết định IMPLEMENT).
- **Open Questions:**
  - Q1: Idempotency-Key store ở đâu? PG table `IdempotencyKeys` (new entity) hay Redis cache? → PG table đơn giản hơn (no new infra).
  - Q2: Client-side order ID có conflict với server-side generation không? → OrderService cần accept client-generated ID.
  - Q3: Nếu user queue 2 orders offline, rồi reconnect — thứ tự replay đúng không? → IndexedDB `sync-queue` có timestamp index (pwa.js line 312).
- **Recommended Action:** Proceed to ANALYZE (Q1-Q3) → IMPLEMENT.

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| Checkout.razor | Submit logic thay đổi | Keep online path unchanged (only branch if offline) |
| PublicOrdersController.cs | New Idempotency-Key check | Backward compatible (header optional — old clients vẫn work) |
| service-worker.js | New `sync` handler | Isolated, không affect existing fetch handlers |
| pwa.js | New replayQueuedCheckouts | Isolated function |

## 9. TDD & E2E TESTING STRATEGY
- **Unit test:** `OfflineQueueService` — queue/dequeue/mark-sent logic.
- **Integration test:** Gateway Idempotency-Key check — POST 2x same key → 1 order.
- **Manual RV:** Offline checkout → reconnect → verify order + no duplicate.

## 10. JIT PLANNING + PURE EXECUTION
| Session | JIT Planning | Pure Execution |
|---|---|---|
| S1 | Chốt Idempotency-Key storage (PG table vs cache) + client-side order ID | Report plan |
| S2 | Implement OfflineQueueService + Checkout.razor offline branch | Code + build |
| S3 | Implement service worker `sync` handler + pwa.js replay | Code + RV offline |
| S4 | Implement Gateway Idempotency-Key check | Code + integration test |
| S5 | RV idempotency (fire 3x → 1 order) + NATS sync verify | RV |

## 12. ESTIMATED EFFORT
- 3-4 sessions. **BLOCKER:** Phase 3 must be complete. **Risk:** Gateway change cần careful idempotency design.
