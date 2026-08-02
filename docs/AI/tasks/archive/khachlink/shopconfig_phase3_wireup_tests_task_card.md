# TASK CARD: ARCHITECTURE - PHASE 3 - Wire-up Components + Integration Tests

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Wire ShopConfigHttpService vào KhachLinkLayout + Home.razor (với SocialHub), remove IShopConfigService (CoreHub direct), thêm integration test assertions, verify end-to-end build
- **Nghiệp vụ áp dụng:** KhachLink hiển thị real shop branding (name, phone, address, social links) cho khách hàng — bao gồm anonymous visitors

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `.devin/workflows/newfeaturebuild.md`
- **Execution Mode:** IMPLEMENT

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
  - `5_WebApps/KhachLink/Components/Layout/KhachLinkLayout.razor` (switch to ShopConfigHttpService)
  - `5_WebApps/KhachLink/Pages/Home.razor` (add ShopConfigHttpService + SocialHub)
  - `5_WebApps/KhachLink/Program.cs` (remove IShopConfigService DI — final cleanup)
  - `6_Tests/VanAn.Integration.Tests/KhachLinkStartupTests.cs` (add DI assertion)
  - `5_WebApps/KhachLink/Services/Http/ShopConfigHttpService.cs` (read-only — verify API from Phase 2)
  - `5_WebApps/KhachLink/Components/SocialHub.razor` (read-only — understand [Parameter] ShopConfig)
- **Boundary Rules (Nghiêm cấm):**
  - KHÔNG sửa Domain layer
  - KHÔNG sửa Gateway hoặc ShopERP controllers
  - KHÔNG sửa ShopConfigHttpService (Phase 2 đã xong)
  - KHÔNG sửa ProductHttpService hoặc ProductDto (Phase 1 đã xong)
  - KHÔNG tạo mới components — chỉ wire-up existing

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **UI Platform Compliance:** Dùng VanAnCard, VanAnButton, VanAnAlert — không custom HTML/CSS khi component tồn tại
- [ ] **Architectural Boundary:** KhachLink MUST NOT inject IShopConfigService from CoreHub — chỉ ShopConfigHttpService
- [ ] **Domain Protection:** KHÔNG sửa Domain layer
- [ ] **DI Registration:** ShopConfigHttpService phải được đăng ký DI (verify từ Phase 2)
- [ ] **KhachLink Wave Development Checklist:** Nếu thêm @inject → đăng ký DI + thêm StartupTest assertion
- [ ] **Build Must Pass:** 0 errors + guard-check.ps1 passes

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** KhachLinkLayout.razor `@inject ShopConfigHttpService` thay vì `@inject IShopConfigService`
- [ ] **SC2:** KhachLinkLayout OnInitializedAsync gọi `ShopConfigHttpService.GetShopConfigByTenantIdAsync(tenantId)` hoặc `GetShopConfigFromProductsAsync(products)`
- [ ] **SC3:** KhachLinkLayout render đúng shop name, phone, address, social links từ real data
- [ ] **SC4:** KhachLinkLayout fallback to default khi ShopConfig null (không crash)
- [ ] **SC5:** Home.razor `@inject ShopConfigHttpService` + load ShopConfig từ products
- [ ] **SC6:** Home.razor render `<SocialHub ShopConfig="@_shopConfig" />` trước `</KhachLinkLayout>`
- [ ] **SC7:** Program.cs không còn `AddScoped<IShopConfigService, ShopConfigService>()`
- [ ] **SC8:** Program.cs có `AddScoped<ShopConfigHttpService>()` (verify từ Phase 2)
- [ ] **SC9:** KhachLinkStartupTests có assertion `Assert.NotNull(sp.GetRequiredService<ShopConfigHttpService>())`
- [ ] **SC10:** Build KhachLink: 0 errors
- [ ] **SC11:** `dotnet build VanAn.sln` — 0 errors
- [ ] **SC12:** guard-check.ps1 passes
- [ ] **SC13:** No IShopConfigService reference trong KhachLink (grep verify)
- [ ] **SC14:** project_state.md updated với Phase 3 completion

**Implementation Date:** 2026-07-02
**Branch:** feature/shopconfig-product-tenant-phase3-wireup
**Status:** ✅ COMPLETE

## 6. ACTIVE SKILLS (MAX 3)
- `domain-integrity-validation` — Ensure Domain layer not modified
- `build-error-analysis` — Verify build after wire-up
- `ui-platform-compliance-review` — Ensure UI Platform components used

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 8
- **Verified Facts:**
  - Fact 1: KhachLinkLayout.razor line 7: `@inject IShopConfigService ShopConfigService` (needs change)
  - Fact 2: KhachLinkLayout.razor line 126-131: `_shopConfig = await ShopConfigService.GetShopConfigAsync(_currentTenantId)` (needs change)
  - Fact 3: KhachLinkLayout.razor line 130: `_currentTenantId = TenantService.GetCurrentTenantId()` (TenantService stays)
  - Fact 4: SocialHub.razor line 97-98: `[Parameter] public ShopConfig? ShopConfig { get; set; }` (accepts ShopConfig)
  - Fact 5: Home.razor currently does NOT inject ShopConfigService (was reverted)
  - Fact 6: Home.razor currently does NOT render SocialHub (was reverted)
  - Fact 7: KhachLinkStartupTests.cs exists (verified from KhachLink Wave Development Checklist)
  - Fact 8: Program.cs line 65-67: IShopConfigService DI (needs removal in Phase 3)
- **Assumptions:**
  - ShopConfigHttpService from Phase 2 has `GetShopConfigByTenantIdAsync(Guid)` method
  - ShopConfigHttpService from Phase 2 has `GetShopConfigFromProductsAsync(List<ProductDto>)` method
  - Home.razor already loads products (line 220: `products = await ProductService.GetProductsAsync()`)
- **Open Questions:**
  - Q1: KhachLinkLayout nên dùng `GetShopConfigByTenantIdAsync(tenantId)` hay `GetShopConfigFromProductsAsync(products)`? (RECOMMEND: by-tenant — layout không có products context)
  - Q2: Home.razor nên dùng `GetShopConfigFromProductsAsync(products)` hay `GetShopConfigByTenantIdAsync`? (RECOMMEND: from-products — Home đã load products)
- **Recommended Action:** PROCEED after Phase 2 — wire-up components

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| `KhachLinkLayout.razor` | Switch inject — tất cả pages dùng layout affected | Fallback to DefaultShopConfig khi null |
| `Home.razor` | Add inject + SocialHub render | SocialHub tự ẩn khi ShopConfig null (line 5) |
| `Program.cs` | Remove IShopConfigService DI | Verify no other component injects IShopConfigService (grep) |
| `KhachLinkStartupTests.cs` | New assertion | Additive — không break existing tests |

## 9. TDD & E2E TESTING STRATEGY
- **Integration Tests:**
  - KhachLinkStartupTests: `Assert.NotNull(sp.GetRequiredService<ShopConfigHttpService>())`
  - Verify IShopConfigService NOT registered (optional: `Assert.Throws<InvalidOperationException>(() => sp.GetRequiredService<IShopConfigService>())`)
- **E2E Tests:** Not required for this phase (UI layout changes covered by existing E2E if any)
- **Test boundary:**
  - Unit tests: N/A
  - Integration tests: KhachLinkStartupTests DI assertions
  - E2E tests: Deferred (Playwright governance — not during IMPLEMENT)

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Chiến lược thực thi: JIT Planning + Pure Execution
Phase 3 = wire-up 2 components + remove DI + add test assertion + build verify. Execute theo thứ tự: Layout → Home → Program.cs → Tests → Build.

### Micro-phase breakdown cho Phase 3

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | Verify Q1 (Layout method), Q2 (Home method), grep IShopConfigService consumers | Update KhachLinkLayout + Home.razor + Program.cs + KhachLinkStartupTests + Build + guard-check + project_state.md |

### Execution order (critical)
1. **KhachLinkLayout.razor** — switch inject first (will break build temporarily)
2. **Home.razor** — add inject + SocialHub
3. **Program.cs** — remove IShopConfigService DI (now safe — no consumers left)
4. **KhachLinkStartupTests.cs** — add assertion
5. **Build verify** — `dotnet build VanAn.sln`
6. **guard-check.ps1**
7. **Grep verify** — `grep IShopConfigService` in KhachLink → expect 0 results
8. **project_state.md** — update progress

### Rules
- Sửa KhachLinkLayout TRƯỚC (break build) → Home.razor SAU → Program.cs LAST (fix build)
- Build verify cuối cùng — chấp nhận build break tạm thời giữa bước 1-3
- KHÔNG chạy Playwright (governance — IMPLEMENT mode)
- guard-check.ps1 MUST PASS

## 11. ESTIMATED EFFORT
- ~60-90 minutes
- 1 session theo JIT Planning
- **BLOCKER:** Phase 2 phải complete (ShopConfigHttpService registered in DI)
