# TASK CARD: PWA-OFFLINE - Phase 3 - Offline API Fallback Hardening

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Cập nhật `dynamicCachePatterns` trong service worker → cache đúng Gateway endpoints (Option C) → mỗi page works offline với cached data.
- **Nghiệp vụ áp dụng:** Khách offline mở Store Finder → thấy cached stores. Mở Home → thấy cached catalog. Mở Order Tracking → thấy cached order.

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `.devin/workflows/newfeaturebuild.md`
- **Execution Mode:** IMPLEMENT

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md`
  - `5_WebApps/KhachLink/wwwroot/service-worker.js` (dynamicCachePatterns)
  - `5_WebApps/KhachLink/Pages/StoreFinder.razor` (verify endpoints)
  - `5_WebApps/KhachLink/Pages/Home.razor` (verify endpoints)
  - `5_WebApps/KhachLink/Pages/Store.razor` (verify endpoints)
  - `5_WebApps/KhachLink/Pages/OrderTracking.razor` (verify endpoints)
  - `5_WebApps/KhachLink/Pages/OrderHistory.razor` (verify endpoints)
- **Boundary Rules:**
  - KHÔNG thay đổi Gateway endpoints.
  - KHÔNG cache POST/PUT/DELETE (chỉ GET).
  - Cache expiration 24h (avoid stale data forever).

## 4. TECHNICAL & REGULATORY CONSTRAINTS
- [ ] **Endpoints cập nhật:** `/api/tenants/search`, `/api/tenants/nearby`, `/api/tenants/{id}/store-info`, `/api/catalog/recommended`, `/api/campaigns/by-tenant/{id}`, `/api/orders/{id}`, `/api/orders/history`.
- [ ] **Stale-while-revalidate:** Cho catalog/campaigns (show cached, refresh background).
- [ ] **Cache expiration:** 24h cho API responses.
- [ ] **No POST caching:** Service worker chỉ intercept GET (đã có line 53-55).

## 5. SUCCESS CRITERIA
- [ ] SC1: `dynamicCachePatterns` updated với 7 endpoints trên.
- [ ] SC2: Stale-while-revalidate strategy cho catalog/campaigns.
- [ ] SC3: Cache expiration 24h implemented.
- [ ] SC4: `dotnet build` PASS.
- [ ] SC5: RV offline Store Finder: shows cached stores.
- [ ] SC6: RV offline Home: shows cached catalog + campaigns.
- [ ] SC7: RV offline Order Tracking: shows cached order.
- [ ] SC8: RV offline Order History: shows cached orders.

**Implementation Date:** _TBD_
**Branch:** `feature/khachlink-wasm`

## 6. ACTIVE SKILLS (MAX 3)
- `pattern-based-fixing` — service worker cache pattern

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 3
- **Verified Facts:**
  - Fact 1: dynamicCachePatterns hiện tại = `/api/menu`, `/api/products`, `/api/orders` (outdated)
  - Fact 2: Option C endpoints = `/api/tenants/*`, `/api/catalog/*`, `/api/campaigns/*`
  - Fact 3: service-worker.js line 88-126 đã có network-first + cache fallback cho `/api/`
- **Assumptions:**
  - A1: Tất cả endpoints trên return JSON 200 (verify via curl).
- **Open Questions:**
  - Q1: `/api/orders/history` endpoint có tồn tại không? → verify Gateway controllers.
- **Recommended Action:** Proceed to IMPLEMENT.

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| service-worker.js | Cache patterns change → user thấy stale data 24h | Acceptable cho offline UX |

## 9. TDD & E2E TESTING STRATEGY
- **Manual RV:** Offline mỗi page → verify cached data shows.
- **Cache expiration test:** Wait 24h (or mock time) → verify cache evicted.

## 10. JIT PLANNING + PURE EXECUTION
| Session | JIT Planning | Pure Execution |
|---|---|---|
| S1 | Verify tất cả endpoints tồn tại (curl Gateway) | Sửa dynamicCachePatterns + stale-while-revalidate + expiration + RV |

## 12. ESTIMATED EFFORT
- 1-2 sessions. **BLOCKER:** Phase 2 must be complete.
