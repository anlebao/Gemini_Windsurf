# TASK CARD: Tenant Onboarding - Wave 2 - F&B Seed Strategy

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Implement seed data chi tiết cho ngành F&B (Drink & Food)
- **Nghiệp vụ áp dụng:** Quán cà phê, trà sữa, fast food, nhà hàng
- **Status:** PENDING — Planning & Approval
- **Branch:** `feature/tenant-onboarding-wave2-fnb-seed`
- **Estimated Sessions:** 1-2

---

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `newfeaturebuild.md` (new feature - multi-session)
- **Execution Mode:** ANALYZE → IMPLEMENT
- **Current Phase:** Wave 2 of 6
- **Dependency:** Wave 1 must be merged

---

## 3. RELEVANT FILES (CONTEXT BOUNDARY)

### Files được phép đọc/sửa
- `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
- `docs/AI/tasks/wave1_tenant_onboarding_abstraction_task_card.md` (READ)
- `3_CoreHub/Services/Onboarding/IIndustrySeedStrategy.cs` (READ)
- `3_CoreHub/Services/Onboarding/Dtos/IndustrySeedResult.cs` (READ)
- `3_CoreHub/Services/Onboarding/Strategies/FnbSeedStrategy.cs` (CREATE)
- `1_Shared/Domain.cs` (READ - Product, Ingredient, Recipe, Shop, Inventory)
- `3_CoreHub/Infrastructure/IVanAnDbContext.cs` (READ)
- `6_Tests/VanAn.Core.Tests/Services/Onboarding/FnbSeedStrategyTests.cs` (CREATE)

### Boundary Rules (Nghiêm cấm)
- KHÔNG sửa `1_Shared/Domain.cs` để thêm entity mới
- KHÔNG sửa interface từ Wave 1 (chỉ dùng)
- KHÔNG tạo controller/UI trong wave này
- KHÔNG bypass multi-tenancy (mọi entity phải có TenantId)

---

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **Domain Protection:** Chỉ sử dụng entities sẵn có (`Product`, `Ingredient`, `Recipe`, `Shop`, `Inventory`)
- [ ] **Multi-Tenancy:** Mọi entity seed phải có `TenantId` đúng
- [ ] **Idempotency:** Strategy nên kiểm tra tránh duplicate nếu chạy lại (optional cho wave này, ghi nhận warning)
- [ ] **Realistic Data:** Giá cả, tên sản phẩm phải hợp lý với thị trường VN
- [ ] **VAT Compliance:** Mặc định VAT 10% (theo `Product` constructor)
- [ ] **No Business Logic in Controller:** Seed logic nằm trong service

---

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** `FnbSeedStrategy` implements `IIndustrySeedStrategy`
- [ ] **SC2:** Tạo ít nhất 1 default `Shop`
- [ ] **SC3:** Tạo ít nhất 8 `Product` (đồ uống + đồ ăn)
- [ ] **SC4:** Tạo ít nhất 10 `Ingredient`
- [ ] **SC5:** Tạo ít nhất 5 `Recipe` (mapping product ↔ ingredient)
- [ ] **SC6:** Tạo ít nhất 5 `Inventory` record cho ingredients
- [ ] **SC7:** Unit tests verify counts match expected
- [ ] **SC8:** Unit tests verify all entities have correct TenantId
- [ ] **SC9:** Build: 0 errors
- [ ] **SC10:** No regression in existing tests

---

## 6. ACTIVE SKILLS (MAX 3)
- `domain-integrity-validation` — Ensure seed data aligns with domain entities
- `build-error-analysis` — Verify build passes after adding strategy
- `test-system-upgrade` — Add unit tests for F&B seed strategy

---

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 5
- **Verified Facts:**
  - Fact 1: `Product` entity có constructor `(TenantId, name, description, price, category, ...)`
  - Fact 2: `Ingredient` entity có public setters (Name, Unit, CurrentStock, MinStockThreshold, PricePerUnit)
  - Fact 3: `Recipe` entity có public setters (ProductId, IngredientId, QuantityNeeded)
  - Fact 4: `Shop` entity có constructor `(TenantId, name, address, phone, email)`
  - Fact 5: `Inventory` entity có constructor `(TenantId, ingredientId, quantity)`
- **Assumptions:**
  - F&B seed data sẽ bao gồm cả cafe, trà, và đồ ăn nhẹ
  - Giá cả sẽ dùng VND (decimal)
  - Mỗi product có thể có 1-3 ingredients
- **Open Questions:**
  - Q1: Nên bao nhiêu products/ingredients cho demo hợp lý?
  - Q2: Có cần seed categories riêng biệt không?
  - Q3: Có cần idempotency check trong wave này không?

---

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| `3_CoreHub/Services/Onboarding/Strategies/FnbSeedStrategy.cs` | NEW - F&B seed logic | Keep seed data in private static methods |
| `6_Tests/VanAn.Core.Tests/Services/Onboarding/FnbSeedStrategyTests.cs` | NEW - tests | Use in-memory SQLite or mocks |
| `1_Shared/Domain.cs` | READ ONLY | Không sửa |

---

## 9. TDD & TESTING STRATEGY
- **Unit tests:**
  - Verify `IndustryCode` is `"F&B"`
  - Verify seed creates expected counts
  - Verify all entities have correct `TenantId`
  - Verify products are active
  - Verify recipes link to valid product/ingredient IDs
- **Integration tests:** Không trong wave này
- **E2E tests:** Không trong wave này

---

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | - Chốt danh sách F&B products/ingredients<br>- Chốt recipe mappings<br>- Chốt default shop info | - Implement `FnbSeedStrategy`<br>- Add unit tests<br>- Run build |

---

## 11. DETAILED SEED DATA SPECIFICATION

### 11.1 Default Shop
```csharp
new Shop(tenantId, "Vạn An F&B", "123 Nguyễn Huệ, Q1, TP.HCM", "1900-1234", "fnb@vanan.vn")
```

### 11.2 Products (Drinks)
| # | Tên | Giá | Category | VAT |
|---|---|---|---|---|
| 1 | Cà phê đen | 25,000 | Đồ uống | 10% |
| 2 | Cà phê sữa | 30,000 | Đồ uống | 10% |
| 3 | Trà đào | 35,000 | Đồ uống | 10% |
| 4 | Trà sữa trân châu | 40,000 | Đồ uống | 10% |
| 5 | Sinh tố bơ | 45,000 | Đồ uống | 10% |

### 11.3 Products (Food)
| # | Tên | Giá | Category | VAT |
|---|---|---|---|---|
| 6 | Bánh mì thịt nguội | 35,000 | Đồ ăn | 10% |
| 7 | Cơm gà xối mỡ | 55,000 | Đồ ăn | 10% |
| 8 | Mì ý bò bằm | 65,000 | Đồ ăn | 10% |

### 11.4 Ingredients
| # | Tên | Đơn vị | Tồn kho tối thiểu | Giá/đơn vị |
|---|---|---|---|---|
| 1 | Cà phê bột | kg | 5 | 200,000 |
| 2 | Sữa đặc | lon | 10 | 25,000 |
| 3 | Đường | kg | 5 | 15,000 |
| 4 | Trà đào | gói | 10 | 8,000 |
| 5 | Bột trân châu | kg | 3 | 120,000 |
| 6 | Bơ | trái | 10 | 15,000 |
| 7 | Bánh mì | ổ | 20 | 5,000 |
| 8 | Thịt nguội | kg | 3 | 180,000 |
| 9 | Gà | con | 5 | 80,000 |
| 10 | Cơm | kg | 10 | 20,000 |
| 11 | Mì ý | gói | 15 | 12,000 |
| 12 | Thịt bò bằm | kg | 3 | 220,000 |

### 11.5 Recipes (Product → Ingredient)
| Product | Ingredient | Số lượng |
|---|---|---|
| Cà phê đen | Cà phê bột | 0.02 kg |
| Cà phê sữa | Cà phê bột | 0.02 kg |
| Cà phê sữa | Sữa đặc | 0.05 lon |
| Trà đào | Trà đào | 1 gói |
| Trà đào | Đường | 0.05 kg |
| Trà sữa trân châu | Bột trân châu | 0.05 kg |
| Trà sữa trân châu | Sữa đặc | 0.05 lon |
| Sinh tố bơ | Bơ | 1 trái |
| Bánh mì thịt nguội | Bánh mì | 1 ổ |
| Bánh mì thịt nguội | Thịt nguội | 0.05 kg |
| Cơm gà xối mỡ | Gà | 0.5 con |
| Cơm gà xối mỡ | Cơm | 0.2 kg |
| Mì ý bò bằm | Mì ý | 1 gói |
| Mì ý bò bằm | Thịt bò bằm | 0.1 kg |

### 11.6 Inventory
- Mỗi ingredient có initial stock = 100 × unit (ví dụ: 100 kg cà phê, 100 lon sữa)

---

## 12. EXIT CHECKLIST
- [ ] `FnbSeedStrategy` implemented
- [ ] Seed data matches specification
- [ ] Unit tests pass
- [ ] `dotnet build VanAn.sln` 0 errors
- [ ] `guard-check.ps1` pass
- [ ] Commit với message `[WAVE 2] F&B seed strategy`
- [ ] Ready for Wave 3
