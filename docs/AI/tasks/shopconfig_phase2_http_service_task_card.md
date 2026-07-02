# TASK CARD: ARCHITECTURE - PHASE 2 - Rewrite ShopConfigHttpService (Product-Based)

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Rewrite ShopConfigHttpService để load ShopConfig từ product data (products → TenantId → shop entity) thay vì order-based approach
- **Nghiệp vụ áp dụng:** KhachLink cần real shop data (name, address, phone) để hiển thị đúng branding cho khách hàng — bao gồm anonymous visitors

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `.devin/workflows/newfeaturebuild.md`
- **Execution Mode:** IMPLEMENT

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
  - `5_WebApps/KhachLink/Services/Http/ShopConfigHttpService.cs` (rewrite — product-based)
  - `5_WebApps/KhachLink/Program.cs` (replace IShopConfigService DI with ShopConfigHttpService)
  - `5_WebApps/KhachLink/Services/Http/ProductHttpService.cs` (read-only — understand existing API)
  - `5_WebApps/KhachLink/Models/ProductDto.cs` (read-only — verify TenantId exists from Phase 1)
- **Boundary Rules (Nghiêm cấm):**
  - KHÔNG sửa Domain layer
  - KHÔNG sửa KhachLinkLayout.razor hoặc Home.razor (Phase 3)
  - KHÔNG sửa Gateway hoặc ShopERP controllers (Phase 1 đã xong)
  - KHÔNG inject IShopConfigService từ CoreHub nữa — thay bằng ShopConfigHttpService
  - KHÔNG sửa ProductHttpService (read-only reference)

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **Architectural Boundary:** KhachLink MUST use HTTP via Gateway — no direct CoreHub DI
- [ ] **Domain Protection:** KHÔNG sửa Domain layer
- [ ] **Fallback Required:** ShopConfigHttpService phải fallback to DefaultShopConfig khi không có products / shop not found
- [ ] **No IShopConfigService:** Program.cs không còn đăng ký `IShopConfigService, ShopConfigService` từ CoreHub
- [ ] **Build Must Pass:** 0 errors sau phase này

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** ShopConfigHttpService có method `GetShopConfigFromProductsAsync(List<ProductDto> products)` — nhận products, extract TenantId, load shop
- [ ] **SC2:** ShopConfigHttpService có method `GetShopConfigByTenantIdAsync(Guid tenantId)` — gọi GET /api/shops/by-tenant/{tenantId}
- [ ] **SC3:** ShopConfigHttpService fallback to DefaultShopConfig khi products empty / shop not found / API error
- [ ] **SC4:** ShopConfigHttpService build ShopConfig từ real Shop entity data (Name, Address, Phone, Email, Latitude, Longitude)
- [ ] **SC5:** Branding fields (PrimaryColor, SecondaryColor, Theme) giữ default values (không stored trong Shop entity)
- [ ] **SC6:** Program.cs đăng ký `ShopConfigHttpService` (Scoped) — không còn `IShopConfigService, ShopConfigService`
- [ ] **SC7:** Program.cs không còn `using VanAn.CoreHub.Services` cho IShopConfigService (nếu không dùng service khác từ namespace này)
- [ ] **SC8:** Build KhachLink: 0 errors
- [ ] **SC9:** Build Gateway: 0 errors (no changes)
- [ ] **SC10:** No Domain layer files modified
- [ ] **SC11:** No Gateway/ShopERP files modified
- [ ] **SC12:** `dotnet build VanAn.sln` — 0 errors

**Implementation Date:** [DATE]
**Branch:** feature/shopconfig-product-tenant-phase2-service

## 6. ACTIVE SKILLS (MAX 3)
- `domain-integrity-validation` — Ensure Domain layer not modified
- `build-error-analysis` — Verify build after service rewrite
- `outbox-pattern-implementation` — Reference for HTTP service patterns (if needed)

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 7
- **Verified Facts:**
  - Fact 1: ShopConfigHttpService.cs currently exists (Approach 1 — order-based, needs rewrite)
  - Fact 2: ProductHttpService.GetProductsAsync() calls `shoperp/api/products` (line 18)
  - Fact 3: GET /api/shops/by-tenant/{tenantId} endpoint exists (Gateway + ShopERP — from Approach 1 remnants, KEEP)
  - Fact 4: ShopConfig record has: ShopId, ShopName, Address, Phone, Email, Latitude, Longitude, PrimaryColor, SecondaryColor, Theme, etc.
  - Fact 5: Shop entity has: Name, Address, Phone, Email, Latitude, Longitude (Domain.cs line 480-512)
  - Fact 6: Program.cs line 65-67 currently registers `IShopConfigService, ShopConfigService` (CoreHub direct)
  - Fact 7: KhachLinkLayout.razor line 7 injects `IShopConfigService` (will change in Phase 3)
- **Assumptions:**
  - ProductDto will have TenantId after Phase 1 complete
  - ShopConfigHttpService can be injected without IJSRuntime (no localStorage needed for product-based approach)
  - HttpClient "gateway" client is registered (Program.cs line 83-95)
- **Open Questions:**
  - Q1: ShopConfigHttpService có cần inject ProductHttpService hay nhận products as parameter? (RECOMMEND: parameter — tách concerns)
  - Q2: Có service nào khác trong KhachLink đang dùng IShopConfigService không? (verify — KhachLinkLayout line 7)
- **Recommended Action:** PROCEED after Phase 1 — rewrite service, update DI

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| `ShopConfigHttpService.cs` | Rewrite — existing order-based logic replaced | KhachLinkLayout/Home.razor chưa dùng service này (Phase 3 mới wire-up) |
| `Program.cs` (DI) | Remove IShopConfigService, add ShopConfigHttpService | KhachLinkLayout still @inject IShopConfigService — sẽ break build → Phase 3 fix |

**⚠️ CRITICAL NOTE:** Removing IShopConfigService from DI sẽ break KhachLinkLayout.razor (line 7: `@inject IShopConfigService`). Hai options:
1. **Option A:** Giữ IShopConfigService DI tạm + thêm ShopConfigHttpService DI song song → Phase 3 mới remove IShopConfigService
2. **Option B:** Sửa KhachLinkLayout trong Phase 2 luôn (break phase boundary nhưng pragmatic)

**RECOMMENDATION:** Option A — giữ song song, Phase 3 mới switch. Tránh break build giữa phase.

## 9. TDD & E2E TESTING STRATEGY
- **Unit Tests:** ShopConfigHttpService logic (mock HttpClient)
  - Test: products empty → returns DefaultShopConfig
  - Test: products with TenantId → calls by-tenant endpoint
  - Test: by-tenant API returns 404 → returns DefaultShopConfig
- **Integration Tests:** DI registration verification (Phase 3)
- **Test boundary:**
  - Unit tests: ShopConfigHttpService (if test infrastructure exists)
  - Integration tests: KhachLinkStartupTests (Phase 3)
  - E2E tests: Not required for service-only changes

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Chiến lược thực thi: JIT Planning + Pure Execution
Phase 2 = rewrite 1 file + update 1 DI registration. Service logic rõ ràng. Execute trực tiếp.

### Micro-phase breakdown cho Phase 2

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | Verify Q1 (parameter vs inject ProductHttpService), Q2 (other IShopConfigService consumers) | Rewrite ShopConfigHttpService + Update Program.cs DI (additive — keep IShopConfigService tạm) + Build |

### Rules
- ShopConfigHttpService nhận `List<ProductDto>` as parameter (tách concerns — caller load products, service load shop)
- KHÔNG inject ProductHttpService vào ShopConfigHttpService (tránh circular dependency)
- Fallback to DefaultShopConfig cho mọi error case
- Giữ IShopConfigService DI tạm (Option A) — Phase 3 mới remove

## 11. ESTIMATED EFFORT
- ~45-60 minutes
- 1 session theo JIT Planning
- **BLOCKER:** Phase 1 phải complete (ProductDto có TenantId)
