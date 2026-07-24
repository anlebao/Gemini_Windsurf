# TASK CARD: LOYALTY-A - Guard Fix + Configurable Points Formula

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Sửa 2 vấn đề trong loyalty award-on-purchase (audit 2026-07-23, 85% complete):
  1. **Guard TrackingCode** — orders không qua SocialCampaign KHÔNG được tích điểm. Cần quyết định: bỏ guard (tất cả order tích điểm) hay giữ (chỉ campaign orders tích điểm).
  2. **Hardcoded formula** — `Math.Max(10, (int)(order.TotalAmount * 0.1m))` hardcoded trong `OrderWorkflowService.cs:245`. Cần làm configurable qua appsettings.
- **Nghiệp vụ áp dụng:** Tenant owner cấu hình công thức tích điểm riêng (10%, 5%, điểm tối thiểu, có/không giới hạn) per tenant hoặc global.

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `.devin/workflows/newfeaturebuild.md`
- **Execution Mode:** ANALYZE → IMPLEMENT
- **Prerequisite:** Phase 5.4 COMPLETE (cùng sửa `OrderWorkflowService.ProcessLoyaltyPointsAsync` — Phase 5.4 thêm `customer.UpdateOrderStats()`, L-A sửa guard + formula)

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `3_CoreHub/Services/OrderWorkflowService.cs` — `ProcessLoyaltyPointsAsync` (line 221-260): sửa guard + formula
  - `2_Gateway/appsettings.json` + `3_CoreHub/appsettings.json` — thêm `LoyaltyConfig` section
  - `1_Shared/Domain.cs` — thêm `LoyaltyPointsConfig` record (config DTO, KHÔNG phải entity — không cần migration)
  - `6_Tests/OrderWorkflowServiceTests.cs` — update tests cho configurable formula
- **Boundary Rules:**
  - KHÔNG sửa Domain entity (LoyaltyRewards, Customer) — chỉ thêm config record.
  - KHÔNG sửa LoyaltyRewardsService — chỉ sửa OrderWorkflowService (caller).
  - KHÔNG thêm migration (config record, không phải entity).

## 4. TECHNICAL & REGULATORY CONSTRAINTS
- [x] **Decision: Guard TrackingCode** — **CONFIGURABLE PER TENANT** (chốt 2026-07-23). `LoyaltyConfig.AwardOnAllOrders` per tenant (default true = bỏ guard, tất cả order tích điểm). Tenant owner chọn qua admin UI.
- [x] **Configurable formula:** `LoyaltyPointsConfig` record:
  ```csharp
  public class LoyaltyPointsConfig
  {
      public decimal PointsRate { get; set; } = 0.1m;        // 10% default
      public int MinPointsPerOrder { get; set; } = 10;       // min 10 points
      public int? MaxPointsPerOrder { get; set; } = null;    // null = no cap
      public bool AwardOnAllOrders { get; set; } = true;     // true = bỏ TrackingCode guard
  }
  ```
- [x] **appsettings.json:** thêm section `LoyaltyPoints` với các field trên. (Gateway + CoreHub + ShopERP)
- [x] **OrderWorkflowService:** inject `IOptions<LoyaltyPointsConfig>` — thay hardcoded formula bằng config values.

## 5. SUCCESS CRITERIA (6)
- [x] SC1: `LoyaltyPointsConfig` record thêm vào `1_Shared/Domain.cs` (config DTO, không phải entity).
- [x] SC2: `appsettings.json` có section `LoyaltyPoints` (PointsRate, MinPointsPerOrder, MaxPointsPerOrder, AwardOnAllOrders).
- [x] SC3: `OrderWorkflowService.ProcessLoyaltyPointsAsync` dùng config thay vì hardcoded `0.1m` + `Math.Max(10, ...)`.
- [x] SC4: Guard TrackingCode xử lý theo `AwardOnAllOrders` config (true = bỏ guard, false = giữ guard).
- [x] SC5: `OrderWorkflowServiceTests` update — test configurable formula (different rates, min, max, guard on/off).
- [x] SC6: `dotnet build VanAn.sln` PASS + `guard-check.ps1` PASS.

**Implementation Date:** 2026-07-24
**Branch:** `main` (commit `aae5fba2`)

## 6. ACTIVE SKILLS (MAX 3)
- `domain-integrity-validation` — verify config record không phá entity
- `pattern-based-fixing` — config injection pattern
- `test-system-upgrade` — update tests cho configurable formula

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 5 (from audit 2026-07-23)
- **Verified Facts:**
  - Fact 1: `OrderWorkflowService.cs:245` hardcoded `Math.Max(10, (int)(order.TotalAmount * 0.1m))`.
  - Fact 2: `OrderWorkflowService.cs:114-118` guard `string.IsNullOrEmpty(order.TrackingCode)` → return (skip points).
  - Fact 3: `LoyaltyRewardsService.AddPointsAsync` REAL — persist DB + history.
  - Fact 4: Tests `OrderCompleted_ShouldAwardLoyaltyPoints_WhenFromSocialCampaign` + `OrderCompleted_ShouldNotAwardPoints_WhenNotFromSocialCampaign` exist.
  - Fact 5: `LoyaltyUpgradeConfig` (Domain.cs:1316-1324) đã có precedent cho config record pattern.
- **Assumptions:**
  - A1: `IOptions<T>` pattern đã dùng trong codebase (verify trước implement).
- **Open Questions:** 0 (tất cả đã chốt 2026-07-23).
  - Q1: Guard TrackingCode → **CONFIGURABLE PER TENANT** (AwardOnAllOrders default true).
- **Recommended Action:** Proceed to IMPLEMENT (sau Phase 5.4).

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| OrderWorkflowService.cs (ProcessLoyaltyPointsAsync) | Sửa formula + guard — ảnh hưởng tất cả order complete flow | Test coverage + RV |
| appsettings.json | Thêm section — không ảnh hưởng existing | None |
| Domain.cs (thêm config record) | THÊM record, không sửa entity | None |
| OrderWorkflowServiceTests.cs | Update tests — có thể temporarily fail | Rewrite trước verify |

## 9. TDD & E2E TESTING STRATEGY
- **Unit test:** Update `OrderCompleted_ShouldAwardLoyaltyPoints` với configurable formula (test 5%, 10%, 15% rates).
- **Unit test:** Test `AwardOnAllOrders=true` → orders without tracking code CÓ được điểm.
- **Unit test:** Test `AwardOnAllOrders=false` → orders without tracking code KHÔNG được điểm (giữ behavior cũ).
- **Unit test:** Test `MaxPointsPerOrder` cap (e.g., max 1000 points dù order 100 triệu).

## 10. JIT PLANNING + PURE EXECUTION
| Session | JIT Planning | Pure Execution |
|---|---|---|
| S1 | User chốt Q1 (guard decision) + verify IOptions pattern | Add config record + appsettings |
| S2 | Implement configurable formula + guard logic | Code + update tests + build verify |

## 12. ESTIMATED EFFORT
- 2 sessions. **NO BLOCKER** (Q1 đã chốt 2026-07-23: Configurable per tenant). Phụ thuộc Phase 5.4 COMPLETE.
