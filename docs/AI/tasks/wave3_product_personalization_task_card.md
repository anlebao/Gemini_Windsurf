# TASK CARD: KhachLink Improvements - Wave 3 - Product Personalization (Option C - Hybrid)

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Implement hybrid product personalization - keep global catalog + add "Frequently Bought" and "Recently Viewed" sections
- **Nghiệp vụ áp dụng:** KhachLink customer experience - increase conversion through personalized recommendations

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `newfeaturebuild.md` (enhancement - multi-session)
- **Execution Mode:** ANALYZE → IMPLEMENT

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
  - `3_CoreHub/Services/CustomerRecommendationService.cs` (CREATE)
  - `3_CoreHub/Services/ProductViewTrackingService.cs` (CREATE)
  - `3_CoreHub/Infrastructure/Caching/RecommendationCache.cs` (CREATE)
  - `5_WebApps/ShopERP/Controllers/ProductsController.cs` (UPDATE - add personalized endpoint)
  - `5_WebApps/KhachLink/Services/Http/ProductHttpService.cs` (UPDATE - add GetRecommendedProductsAsync)
  - `5_WebApps/KhachLink/Pages/Home.razor` (UPDATE - add personalized sections)
  - `5_WebApps/KhachLink/Models/ProductDto.cs` (UPDATE - add recommendation metadata)
  - `1_Shared/Domain.cs` (READ ONLY - verify Customer, Order entities)
- **Boundary Rules (Nghiêm cấm):**
  - KHÔNG sửa Domain entities (Customer, Order, Product) - chỉ đọc để hiểu structure
  - KHÔNG làm chậm performance của main product catalog (caching là bắt buộc)
  - KHÔNG thay đổi behavior hiện tại của GetProductsAsync (chỉ add new methods)
  - KHÔNG bypass UI Platform components trong Home.razor
  - KHÔNG store sensitive customer data without encryption (nếu cần)

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **UI Platform Compliance:** New sections in Home.razor MUST use UI Platform components (VanAnCard, VanAnButton)
- [ ] **Performance:** Recommendation load time MUST be < 500ms (with caching)
- [ ] **Caching:** Recommendations MUST be cached (5-10 minute TTL) to avoid performance impact
- [ ] **No Regression:** Main product catalog MUST continue showing all products (not replaced)
- [ ] **Data Privacy:** Customer order history access MUST be properly authorized
- [ ] **Fallback Behavior:** If no order history, show global catalog or "New customer" message

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** CustomerRecommendationService created and working
- [ ] **SC2:** Personalized products API endpoint working (GET /api/products/recommended)
- [ ] **SC3:** Recently viewed tracking functional
- [ ] **SC4:** ProductHttpService updated with GetRecommendedProductsAsync()
- [ ] **SC5:** "Frequently Bought" section displays correctly in Home.razor
- [ ] **SC6:** "Recently Viewed" section displays correctly in Home.razor
- [ ] **SC7:** Main product catalog still shows all products (not replaced)
- [ ] **SC8:** Caching implemented (RecommendationCache)
- [ ] **SC9:** Recommendation load time < 500ms (with cache hit)
- [ ] **SC10:** Recommendations accurate based on order history
- [ ] **SC11:** Fallback behavior works for new customers (no order history)
- [ ] **SC12:** Build: 0 errors
- [ ] **SC13:** No performance degradation on main product catalog load

**Implementation Date:** 2026-06-29
**Branch:** feature/khachlink-wave3-product-personalization

## 6. ACTIVE SKILLS (MAX 3)
- `ui-platform-compliance-review` — Ensure new sections in Home.razor use UI Platform components
- `domain-integrity-validation` — Ensure no Domain entity modifications
- `sqlite-concurrency-analysis` — Ensure caching doesn't cause data inconsistency

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 6
- **Verified Facts:**
  - Fact 1: Products currently loaded from ShopERP API (ProductHttpService.GetProductsAsync)
  - Fact 2: ProductsController returns all active products for tenant/shop (no personalization)
  - Fact 3: NO recommendation service exists (grep search returned no results)
  - Fact 4: NO recently viewed tracking exists (grep search returned no results)
  - Fact 5: Home.razor exists and can be extended with new sections
  - Fact 6: Customer and Order entities exist in Domain.cs (for order history analysis)
- **Assumptions:**
  - Customer order history is available in SQLite database
  - Simple frequency-based algorithm is sufficient for "Frequently Bought"
  - Caching can be implemented with IMemoryCache or Redis
  - Recently viewed can be stored in localStorage (client-side) or database
- **Open Questions:**
  - Q1: What recommendation algorithm should we use? (frequency-based, collaborative filtering, etc.)
  - Q2: Where should recently viewed be stored? (localStorage, database, both?)
  - Q3: What caching strategy should we use? (IMemoryCache, Redis, CDN?)
  - Q4: What is the expected customer order history volume? (affects caching strategy)
- **Recommended Action:** Start with simple frequency-based algorithm, use IMemoryCache for simplicity, store recently viewed in localStorage for MVP

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| 3_CoreHub/Services/CustomerRecommendationService.cs | NEW - recommendation logic | Keep algorithm simple (frequency-based) |
| 3_CoreHub/Services/ProductViewTrackingService.cs | NEW - view tracking | Use localStorage first, migrate to DB if needed |
| 3_CoreHub/Infrastructure/Caching/RecommendationCache.cs | NEW - caching layer | Use IMemoryCache for simplicity |
| 5_WebApps/ShopERP/Controllers/ProductsController.cs | Add new endpoint | Keep existing endpoint unchanged (no breaking change) |
| 5_WebApps/KhachLink/Services/Http/ProductHttpService.cs | Add new method | Keep GetProductsAsync unchanged (no breaking change) |
| 5_WebApps/KhachLink/Pages/Home.razor | Add new sections | Keep existing product catalog (append new sections) |

## 9. TDD & E2E TESTING STRATEGY
- **Unit Tests:**
  - CustomerRecommendationService (recommendation logic)
  - ProductViewTrackingService (tracking logic)
  - RecommendationCache (cache hit/miss logic)
- **Integration Tests:**
  - ProductsController personalized endpoint
  - ProductHttpService GetRecommendedProductsAsync
- **E2E Tests:**
  - Manual testing with real customer data
  - Verify recommendations are accurate
  - Verify performance (load time < 500ms)
- **Test boundary:**
  - Unit tests: Service layer logic
  - Integration tests: API endpoints
  - E2E tests: Manual testing with real data

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Chiến lược thực thi: JIT Planning + Pure Execution

This is an enhancement requiring multiple sessions:
- Session 1: Research & Planning (algorithm, caching strategy, data structure)
- Session 2: Implement recommendation service (frequency-based algorithm)
- Session 3: Implement view tracking service
- Session 4: Implement caching layer
- Session 5: Update API endpoints and HTTP service
- Session 6: Update UI (Home.razor) with new sections
- Session 7: Testing with real data & performance optimization

### Micro-phase breakdown cho Wave 3 (Product Personalization)

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | - Research recommendation algorithms (frequency-based vs others)<br>- Decide caching strategy (IMemoryCache vs Redis)<br>- Decide view tracking storage (localStorage vs DB)<br>- Define data structures for recommendations | - Document algorithm choice<br>- Document caching strategy<br>- Document view tracking approach<br>- Create technical design document |
| **S2** | - Plan CustomerRecommendationService implementation<br>- Define recommendation query logic (order history aggregation)<br>- Plan fallback for new customers (no order history) | - Create 3_CoreHub/Services/CustomerRecommendationService.cs<br>- Implement frequency-based algorithm<br>- Add fallback logic for new customers<br>- Write unit tests |
| **S3** | - Plan ProductViewTrackingService implementation<br>- Define view tracking data structure<br>- Plan view tracking API endpoint (if server-side) | - Create 3_CoreHub/Services/ProductViewTrackingService.cs<br>- Implement view tracking logic<br>- Add view tracking to ProductHttpService<br>- Write unit tests |
| **S4** | - Plan caching layer implementation<br>- Define cache key strategy<br>- Define cache TTL (5-10 minutes) | - Create 3_CoreHub/Infrastructure/Caching/RecommendationCache.cs<br>- Implement IMemoryCache wrapper<br>- Add cache invalidation logic<br>- Write unit tests |
| **S5** | - Plan personalized API endpoint<br>- Define DTO for recommended products<br>- Plan ProductHttpService extension | - Update ProductsController (add /recommended endpoint)<br>- Update ProductHttpService (add GetRecommendedProductsAsync)<br>- Update ProductDto (add recommendation metadata)<br>- Write integration tests |
| **S6** | - Plan Home.razor UI changes<br>- Plan "Frequently Bought" section layout<br>- Plan "Recently Viewed" section layout | - Update Home.razor (add "Frequently Bought" section)<br>- Update Home.razor (add "Recently Viewed" section)<br>- Ensure UI Platform components used<br>- Test UI rendering |
| **S7** | - Plan performance testing scenarios<br>- Plan A/B testing approach (if needed) | - Test with real customer data<br>- Verify recommendation accuracy<br>- Verify performance (load time < 500ms)<br>- Fix performance issues<br>- Optimize caching strategy |

### Rules
- MUST implement caching (no direct database queries for recommendations)
- MUST keep main product catalog unchanged (no breaking changes)
- MUST use UI Platform components in Home.razor
- MUST handle new customers gracefully (fallback behavior)
- MUST verify performance (load time < 500ms)
- MUST NOT modify Domain entities (Customer, Order, Product)

## 11. ESTIMATED EFFORT
- 2-3 days total
- 4-7 sessions theo JIT Planning
- **BLOCKER:** Access to real customer order history data for testing