# TASK CARD: PWA-OFFLINE - Phase 4 - Offline Write Queue (Checkout POST)

> **Status: DESCOPE (2026-07-22).** Checkout is online-only. `navigator.onLine` guard in Checkout.razor blocks offline submission. See master plan "Architecture Decision: Phase 4 Descope" for rationale.

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** ~~Queue checkout POST trong IndexedDB khi offline → replay via Background Sync API khi có mạng lại.~~ **DESCOPE.**
- **Nghiệp vụ áp dụng:** ~~Khách trong quán mất mạng → vẫn đặt hàng được → order queue → có mạng lại → auto-sync.~~ **Replaced by:** `navigator.onLine` guard — if offline, show error "no connection, check 4G/Wifi to send order". Customer must reconnect to checkout.

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** N/A — task descope
- **Execution Mode:** DESCOPE (not implemented, not planned)

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
**N/A — task descope.** All constraints below are archived for reference only.

- [~] ~~Client-side UUIDv7~~ — DESCOPE
- [~] ~~Idempotency-Key header~~ — DESCOPE
- [~] ~~Background Sync API~~ — DESCOPE
- [~] ~~Service worker `sync` event~~ — DESCOPE
- [~] ~~iOS Safari fallback~~ — DESCOPE (moot — no Background Sync needed)
- [~] ~~Domain protection~~ — N/A (no Gateway change)

## 5. SUCCESS CRITERIA
**N/A — task descope.** All criteria below are archived for reference only.

- [~] ~~SC1-SC11~~ — DESCOPE

**Replaced by (commit `51b7e624`):**
- [x] `navigator.onLine` guard in Checkout.razor — blocks offline submission with clear error
- [x] Tier 0 price sanity checks at Gateway — rejects invalid prices instantly
- [x] Tier 1 FeaturedProducts cross-check — rejects stale/manipulated prices for featured products

**Implementation Date:** N/A (DESCOPE 2026-07-22)
**Branch:** N/A

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
~~3-4 sessions.~~ **DESCOPE — 0 sessions.** Savings: 3-4 sessions.

### Descope Rationale (2026-07-22)
Architecture review concluded offline checkout creates unacceptable risks for financial integrity:
1. **Ghost orders:** Offline order timestamp ≠ Gateway creation timestamp → accounting period ambiguity
2. **Price validation:** Tier 0+1 requires real-time Gateway PG access — cannot run offline
3. **Inventory overselling:** No real-time inventory check → overbooking risk
4. **Token expiry:** Background Sync replay may fire after auth token expires → silent 401
5. **F&B UX:** Time-sensitive orders — "saved, will send later" is confusing for customers

**Replacement:** `navigator.onLine` guard (commit `51b7e624`) — clear error message, customer reconnects to checkout. Offline READ still works (Phase 2+3).
