# TASK CARD: ARCHITECTURE - PHASE 1 - Revert Approach 1 + Add TenantId to Product DTOs

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Revert Approach 1 (order-based) remnants và thêm TenantId vào Product DTOs để chuẩn bị cho product-based ShopConfig loading
- **Nghiệp vụ áp dụng:** KhachLink customer-facing app — product là touchpoint đầu tiên, cần TenantId để derive ShopConfig

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `.devin/workflows/newfeaturebuild.md`
- **Execution Mode:** IMPLEMENT

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
  - `5_WebApps/ShopERP/Controllers/CustomerOrdersController.cs` (revert TenantId)
  - `5_WebApps/ShopERP/Controllers/ProductsController.cs` (add TenantId to DTO + projection)
  - `5_WebApps/KhachLink/Models/ProductDto.cs` (add TenantId field)
- **Boundary Rules (Nghiêm cấm):**
  - KHÔNG sửa `1_Shared/Domain.cs` — ShopConfig record stays as-is
  - KHÔNG sửa `1_Shared/Domain/Common.cs` — BaseEntity already has TenantId
  - KHÔNG sửa Gateway controllers trong phase này
  - KHÔNG tạo mới ShopConfigHttpService trong phase này (Phase 2)
  - KHÔNG sửa KhachLink Program.cs / Layout / Pages trong phase này (Phase 3)

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **Domain Protection:** KHÔNG sửa Domain layer — ShopConfig record, Product entity, BaseEntity đều không được touch
- [ ] **Backward Compatibility:** Thêm TenantId vào DTO phải backward compatible (new field, không break existing consumers)
- [ ] **Multi-Tenancy:** ProductCatalogItem phải expose TenantId để KhachLink biết product thuộc tenant nào
- [ ] **Revert Completeness:** CustomerOrderDto phải revert hoàn toàn về trạng thái gốc (không TenantId)
- [ ] **Build Must Pass:** 0 errors sau phase này

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** CustomerOrderDto không còn TenantId field (reverted to original)
- [ ] **SC2:** CustomerOrdersController GetMyOrders projection không còn map TenantId
- [ ] **SC3:** ProductCatalogItem có `public Guid TenantId { get; set; }` field
- [ ] **SC4:** ProductsController GetProducts projection maps `TenantId = p.TenantId.Value`
- [ ] **SC5:** ProductDto (KhachLink) có `public Guid TenantId { get; set; }` field
- [ ] **SC6:** RecommendedProductDto (inherits ProductDto) tự động có TenantId
- [ ] **SC7:** Build ShopERP: 0 errors
- [ ] **SC8:** Build KhachLink: 0 errors
- [ ] **SC9:** Build Gateway: 0 errors (no changes expected)
- [ ] **SC10:** No Domain layer files modified
- [ ] **SC11:** No Gateway files modified
- [ ] **SC12:** `dotnet build VanAn.sln` — 0 errors

**Implementation Date:** [DATE]
**Branch:** feature/shopconfig-product-tenant-phase1-dto

## 6. ACTIVE SKILLS (MAX 3)
- `domain-integrity-validation` — Ensure Domain layer not modified
- `build-error-analysis` — Verify build after DTO changes
- `pattern-based-fixing` — Follow existing DTO patterns

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 8
- **Verified Facts:**
  - Fact 1: CustomerOrderDto currently has TenantId field (added during Approach 1, line 68)
  - Fact 2: CustomerOrdersController projection maps TenantId (line 55)
  - Fact 3: ProductCatalogItem does NOT have TenantId (line 149-158, verified)
  - Fact 4: ProductDto (KhachLink) does NOT have TenantId (ProductDto.cs, verified)
  - Fact 5: Product entity has TenantId via BaseEntity (Domain.cs line 526 + Common.cs line 79)
  - Fact 6: ProductCatalogItem is used in GetProducts + GetProduct + recommendations
  - Fact 7: RecommendedProductDto inherits ProductDto (ProductDto.cs line 18)
  - Fact 8: GetProducts projection at line 45-54 maps fields but NOT TenantId
- **Assumptions:**
  - Adding TenantId to ProductCatalogItem is backward compatible (JSON consumers ignore unknown fields)
  - Existing tests don't assert ProductCatalogItem field count
- **Open Questions:**
  - Q1: Có test nào assert ProductCatalogItem exact field set không? (verify trước khi implement)
  - Q2: ProductDto TenantId có cần default value (Guid.Empty) hay không? (likely yes for backward compat)
- **Recommended Action:** PROCEED — low risk, backward compatible DTO addition

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| `CustomerOrdersController.cs` | Revert — OrderHistory.razor không dùng TenantId | OrderHistory.razor không reference TenantId (verified) |
| `ProductsController.cs` (ProductCatalogItem) | New field — all consumers get TenantId automatically | Backward compatible — JSON deserialization ignores extra fields |
| `ProductsController.cs` (projection) | Query now maps TenantId | No performance impact — TenantId already in entity |
| `ProductDto.cs` | New field — KhachLink consumers | Backward compatible — default Guid.Empty |

## 9. TDD & E2E TESTING STRATEGY
- **Unit Tests:** Not applicable for DTO changes (no logic)
- **Integration Tests:**
  - Verify GetProducts API returns TenantId in response
  - Verify CustomerOrders API does NOT return TenantId (reverted)
- **Test boundary:**
  - Unit tests: N/A
  - Integration tests: ShopERP controller tests (if exist)
  - E2E tests: Not required for DTO changes

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Chiến lược thực thi: JIT Planning + Pure Execution
Phase 1 đơn giản — 3 file thay đổi, 1 revert + 2 DTO additions. Execute trực tiếp sau khi verify open questions.

### Micro-phase breakdown cho Phase 1

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | Verify Q1 (test assertions), Q2 (default value) | Revert CustomerOrderDto + Add TenantId to ProductCatalogItem + ProductDto + Build |

### Rules
- Revert CustomerOrderDto TRƯỚC, add TenantId SAU (để build verify từng bước)
- KHÔNG touch Domain layer
- Build verify sau mỗi file change

## 11. ESTIMATED EFFORT
- ~30-45 minutes
- 1 session theo JIT Planning
- **BLOCKER:** None — straightforward DTO changes
