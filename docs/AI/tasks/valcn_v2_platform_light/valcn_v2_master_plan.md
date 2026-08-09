# MASTER PLAN — VẠN AN LOCAL COMMERCE NETWORK v2.0 (PLATFORM-LIGHT)

> **Status:** ✅ COMPLETE — All 6 phases implemented + committed (2026-08-09). Pending: VPS deploy + Runtime verify.
> **Created:** 2026-08-09 · **Last Updated:** 2026-08-09 (v2 — Wave 3 COMPLETE)
> **Workflow:** `newfeaturebuild.md` (ANALYZE → IMPLEMENT) · **Branch:** `main` (always-green, per-wave commits)
> **Source:** User request 2026-08-09 — đề xuất lộ trình hiện thực hóa BOM v2.0 PLATFORM-LIGHT
> **BOM document:** `docs/requirements/VAN_AN_LOCAL_COMMERCE_NETWORK_BOM_v2.0_PLATFORM_LIGHT.md`
> **Codebase verification:** 2 subagents + 1 deep review (4 critical + 7 important + 3 minor findings)

## IMPLEMENTATION STATUS (2026-08-09)

| Wave | Phases | Status | Commit |
|------|--------|--------|--------|
| Wave 1 | Phase 0 (Analyze) + Phase 1 (Foundation) | ✅ COMPLETE | `af09b8d0` |
| Wave 2 | Phase 2 (Platform Fee) ‖ Phase 3 (Loyalty Budget) | ✅ COMPLETE | `f1d46f24` + `7edf589a` |
| Wave 3 | Phase 4 (Refund Reversal) ‖ Phase 7 (Network Dashboard) | ✅ COMPLETE | `9a4d0e9b` |

**Build:** 0 errors · **guard-check:** ALL PASSED · **All flags default OFF** — zero production impact until admin enables via `/admin/valcn-features`.

**Pending:** Push `main` → CI/CD → GCP VPS deploy → Runtime verify (feature flags UI + network dashboard UI + optional feature ON test).

---

## 0. SCOPE CUTS (v2 — 2026-08-09)

### Cuts applied (user-approved)
| Cut | Bỏ gì | Lý do |
|-----|-------|-------|
| Merge Phase 5 → Phase 1 | ReferenceId/CorrelationId propagation | Tránh dependency issue (C1) + gộp Domain changes |
| Bỏ Phase 6 | Merchant Tiering (S/A/B/C) | Defer v3.0 — MVP không cần tier |
| Bỏ Phase 8 | FundingSource (PromoFundingSource) | Defer v3.0 — INV-006 không enforce ở MVP |
| Bỏ Phase 9 | Soft Gates + Monitoring | Defer v3.0 — negative-margin flag + District defer |

### Decisions applied (user-approved)
| Decision | Chọn | Impact |
|----------|------|--------|
| AccountingEntry trace field | Add `CorrelationId` mới (Option B) | +1 field, factory chain vẫn phải modify (sealed class) |
| LoyaltyRewards Silo OrderId tracking | Add `LoyaltyIssuanceRecord` entity (C3 fix) | +1 entity (breaks BOM "0 new entities" — unavoidable) |

### Net result vs v1 plan
- Phases: 10 → **6** (Phase 0, 1, 2, 3, 4, 7)
- Sessions: 14-21 → **10-15** (~5-8 tuần)
- Fields: 10 → **12** (+1 CorrelationId, +1 LoyaltyIssuanceRecord fields)
- Entities: 0 new → **1 new** (LoyaltyIssuanceRecord)
- Enums: 2 → **1** (MerchantTier dropped, PromoFundingSource dropped)
- Services: 5 → **3** (LoyaltyBudgetService, RefundOrchestrationService, NetworkDashboardService)
- Jobs: 3 → **2** (LoyaltyBudgetDailyResetJob, LoyaltyBudgetMonthlyResetJob)

---

## 1. EXECUTION RULES

### JIT Planning Strategy (NON-NEGOTIABLE)
**Nguyên tắc:** KHÔNG code mò mẫm — **Investigate trước, Implement sau**.

```
INVESTIGATE: Đọc task card + verify codebase hiện tại
  → Confirm file paths, signatures, dependencies
  → Grep usage của methods/symbols sẽ touch
  → Identify blast radius
  → Output: confirm task card accurate, hoặc flag drift

PLAN: Detail coding plan (file:line, old→new, test files, DI)

IMPLEMENT: Code + verify (build + guard + tests + commit)
```

### Anti-Guessing Gate (Gate 1)
- Assumptions ≥ Verified Facts → CẤM code, chuyển Investigate
- Mỗi phase phải có ≥ 3 verified facts trước khi implement

### Domain Protection (HARD STOP)
- `AccountingEntry` **immutable** — chỉ thêm `CorrelationId` field + modify factory chain (additive param, không break existing callers)
- Domain layer **pure** — không thêm EF Core attributes vào Domain
- Single Source of Truth: `1_Shared/Domain.cs`
- Single-Identity Pattern: `LoyaltyIssuanceRecord` inherit `BaseEntity`, constructor sync `Id = LoyaltyIssuanceRecordId.Value`, EF config `Ignore(e => e.LoyaltyIssuanceRecordId)`

### Session Protocol
1. Mỗi phase 1-3 sessions (Phase 1, 4 có thể 3 sessions)
2. Bắt đầu session: đọc `project_state.md` + task card phase
3. Trước session end: `dotnet build VanAn.sln` Release pass + `guard-check.ps1` pass + commit
4. Commit format: `[VALCN-V2 P{N}] <short description>`

### Branch Protocol
```
main ← feature/valcn-v2-phase0-analyze
main ← feature/valcn-v2-phase1-domain-fields
main ← feature/valcn-v2-phase2-platform-fee
main ← feature/valcn-v2-phase3-loyalty-budget
main ← feature/valcn-v2-phase4-refund-reversal
main ← feature/valcn-v2-phase7-network-dashboard
```

---

## 2. VERIFICATION REPORT (2026-08-09)

### Methodology
3 rounds:
1. **Round 1 (subagents):** Verify 54 BOM claims — 32 EXISTS, 4 PARTIAL, 18 MISSING (correctly predicted), 0 CONTRADICTED
2. **Round 1 (subagents):** Map 10 integration points (loyalty flow, refund flow, platform fee, AccountingEntry, OutboxEvent, cross-tenant queries, dashboards, jobs)
3. **Round 2 (deep review):** Trace AccountingEntry factory chain, LoyaltyRewards schema, CreateReversal behavior — found 4 critical + 7 important + 3 minor issues

### Critical findings (fixed in v2 plan)
| # | Finding | Fix |
|---|---------|-----|
| C1 | Phase 5 → Phase 4 dependency (query by ReferenceId/CorrelationId) | Merge Phase 5 into Phase 1 |
| C2 | Phase 5 scope: AccountingEntry sealed + factory chain → 6 files, Domain mod YES | Honest scope in Phase 1 |
| C3 | LoyaltyRewards (Silo) không có OrderId → không query issuance per order | Add LoyaltyIssuanceRecord entity |
| C4 | Phase 7 LoyaltyROI formula bug (repeatCustomers cancel out) | Fix formula: filter orders by repeat customer IDs |

### Important findings (fixed in task cards)
| # | Finding | Fix |
|---|---------|-----|
| I1 | Phase 3 counter race condition (concurrent AddPoints) | Use `ExecuteUpdateAsync` atomic increment |
| I2 | Phase 3 reset jobs need IServiceScopeFactory (singleton inject scoped) | Inject IServiceScopeFactory + create scope per execution |
| I3 | Phase 7 Ops Cost undefined | MVP exclude Ops Cost (Contribution Profit = Revenue - Loyalty Cost). Tech debt for v3.0 |
| I4 | Phase 6 tier thresholds undefined | Phase 6 dropped (defer v3.0) |
| I5 | Phase 1 vs master plan enum inconsistency | PromoFundingSource dropped (Phase 8 dropped) |
| I6 | Phase 4 idempotency uses wrong abstraction | Use existing `IdempotentOperation` entity, not invented IOutboxService methods |
| I7 | Phase 9 CategoryHealthJob not in "3 jobs" list | Phase 9 dropped (defer v3.0) |

---

## 3. DEPENDENCY CHAIN (v2)

```
Phase 0 (ANALYZE) ─────────────────────────────┐
                                                ▼
Phase 1 (Fields + LoyaltyIssuanceRecord + AccountingEntry factory mod + CorrelationId set)
                                    ┌───────────┴───────────┐
                                    ▼                       ▼
                              Phase 2                 Phase 3
                         (Platform fee)          (Loyalty budget)
                                    │                       │
                                    └───────────┬───────────┘
                                                ▼
                                         Phase 4
                                   (Refund reversal)
                                                │
                                                ▼
                                         Phase 7
                                  (Network dashboard)
```

### Critical path
`Phase 0 → 1 → 2 → 3 → 4 → 7` (6 phases, 10-15 sessions)

### Parallelization
- Phase 2 + Phase 3 có thể chạy song song (cả 2 chỉ cần Phase 1)
- Phase 4 cần Phase 2 (platform fee cho INV-009) + Phase 3 (budget counters)
- Phase 7 cần Phase 2 (revenue) + Phase 3 (loyalty cost) + Phase 4 (reversal working)

---

## 4. ADDITIVE CHANGES SUMMARY (v2 FINAL)

### 12 Additive Fields (District dropped, PlatformFeeRate added)
| Entity | Field | Type | Default | Phase |
|--------|-------|------|---------|-------|
| `LoyaltyTenantConfig` | `MonthlyPointsBudget` | `int?` | null | 1 |
| `LoyaltyTenantConfig` | `DailyPointsBudget` | `int?` | null | 1 |
| `LoyaltyTenantConfig` | `PerCustomerDailyLimit` | `int?` | null | 1 |
| `LoyaltyTenantConfig` | `PerOrderRateCap` | `decimal?` | null | 1 |
| `LoyaltyTenantConfig` | `PointsIssuedThisMonth` | `int` | 0 | 1 |
| `LoyaltyTenantConfig` | `PointsIssuedToday` | `int` | 0 | 1 |
| `ShopFeatureSettingsEntity` | `PlatformFeeRate` | `decimal?` | 0.05m (5%) | 1 |
| `AccountingEntry` | `CorrelationId` | `Guid?` | null | 1 |
| `OutboxEvent` | `CorrelationId` | `Guid?` | null | 1 |
| `Order` | `PlatformFeeAmount` | `decimal?` | null | 1 |
| `LoyaltyIssuanceRecord` (NEW) | `OrderId` | `Guid` | — | 1 |
| `LoyaltyIssuanceRecord` (NEW) | `CustomerId` | `Guid` | — | 1 |

**REUSED (0 migration):**
- `ShopFeatureSettingsEntity.PlatformFeeRate` (existing, used in Phase 2)
- `AccountingEntry.ReferenceId` (existing, unused — NOT reused, CorrelationId added instead)

### 1 New Entity: LoyaltyIssuanceRecord
```csharp
public class LoyaltyIssuanceRecord : BaseEntity, IMustHaveTenant
{
    public Guid OrderId { get; protected set; }
    public Guid CustomerId { get; protected set; }
    public int PointsIssued { get; protected set; }
    public DateTime IssuedAt { get; protected set; }
    public bool IsReversed { get; protected set; }
    // Constructor + Create factory + Reverse method
}
```
**Purpose:** Track loyalty issuance per order (Silo mode). Alliance mode đã có `AllianceTransaction.SourceOrderId`. Cần entity tương đương cho Silo để Phase 4 query `GetByOrderIdAsync`.

### 1 New Enum: MerchantTier — DROPPED
### 1 New Enum: PromoFundingSource — DROPPED

### 3 New Services
| Service | Purpose | Phase |
|---------|---------|-------|
| `LoyaltyBudgetService` | Check budget before AddPoints + reset counters + decrement on reversal | 3 |
| `RefundOrchestrationService` | Coordinate reversal on order cancel/refund | 4 |
| `NetworkDashboardService` | Cross-tenant aggregate metrics (investor-facing) | 7 |

### 2 New Background Jobs
| Job | Schedule | Purpose | Phase |
|-----|----------|---------|-------|
| `LoyaltyBudgetDailyResetJob` | Daily 00:00 | Reset `PointsIssuedToday` | 3 |
| `LoyaltyBudgetMonthlyResetJob` | 1st of month 00:00 | Reset `PointsIssuedThisMonth` | 3 |

### 1 Migration (Phase 1)
- Add 9 nullable/default columns trên existing entities
- Add 1 new table `LoyaltyIssuanceRecords` (LoyaltyIssuanceRecord entity)
- Backward compat: existing rows unchanged
- **District dropped** — no MVP consumer (Phase 9 dropped)

### AccountingEntry Factory Chain Modification (Phase 1)
**Unavoidable** — sealed class với private constructor + factory methods không accept CorrelationId:
1. `AccountingEntry` private constructor — add `correlationId` param
2. `CreateRevenue` + `CreateExpense` + `CreateReversal` + `CreateReversalWithId` — add `correlationId` param
3. `AccountingEntryDto` — add `CorrelationId` field
4. `AccountingService.CreateEntryAsync` — pass CorrelationId from DTO to factory
5. `SimpleAccountingEventHandler` — set `CorrelationId = orderEvent.OrderId`
6. `CreateReversal` — preserve `CorrelationId` từ original (fix M3)

**Backward compat:** `correlationId` param là optional (default null) → existing callers không break.

### Feature Flag Infrastructure (Phase 1 — user requirement 2026-08-09)
**Purpose:** SystemAdmin toggle ON/OFF từng VALCN v2.0 feature runtime. Default = **OFF** (existing behavior preserved). Không overwrite existing.

| Component | File |
|-----------|------|
| `IFeatureFlagService` + impl | `3_CoreHub/Services/FeatureFlagService.cs` (copy pattern from `BackgroundServiceToggleService`) |
| `FeatureFlagsController` | `2_Gateway/Controllers/FeatureFlagsController.cs` (SystemAdmin JWT) |
| `FeatureFlagHttpService` | `5_WebApps/ShopERP/Services/FeatureFlagHttpService.cs` |
| Admin UI | `5_WebApps/ShopERP/Components/Pages/Admin/ValcnFeatures.razor` (`/admin/valcn-features`) |
| NavMenu link | `5_WebApps/ShopERP/Components/Layout/NavMenu.razor` — "VALCN v2.0 Features" |

**3 feature flags (SystemSetting keys, default OFF):**
| Key | Feature | Phase | Hook point |
|-----|---------|-------|------------|
| `Features:EnableValcnV2_PlatformFee` | Platform fee on Marketplace | 2 | `OrderService.SnapshotCommerceModeAsync` |
| `Features:EnableValcnV2_LoyaltyBudget` | Loyalty budget cap | 3 | `OrderWorkflowService.ProcessLoyaltyPointsAsync` |
| `Features:EnableValcnV2_RefundReversal` | 4-step refund reversal | 4 | `OrderWorkflowService` cancel hook |

**CRITICAL: Default OFF** — opposite of `BackgroundServiceToggleService` (which defaults ON). Ensures existing behavior preserved until admin explicitly enables.

---

## 5. PHASE DETAILS (v2 — 6 phases)

### Phase 0 — ANALYZE: Resolve modeling questions + BR spec
**Task card:** `phase0_task_card.md`
**Objective:** (a) Draft 16 BR spec (BOM Section 33, simplified — drop BR-007 FundingSource, BR-014 Tiering). (b) Verify `ShopFeatureSettingsEntity.PlatformFeeRate`. (c) Investigate `LoyaltyRewards.History` JSON format (fallback nếu LoyaltyIssuanceRecord cần thêm fields). (d) Confirm `AccountingEntry` factory chain modification approach.
**Files:** 0 code changes — investigation + document
**Effort:** 1 session

### Phase 1 — Domain Additive Fields + LoyaltyIssuanceRecord + AccountingEntry Factory Mod + CorrelationId Set + Feature Flag Infra
**Task card:** `phase1_task_card.md`
**Objective:** (a) Add 10 additive fields + 1 new entity (LoyaltyIssuanceRecord) + 1 migration. (b) Modify AccountingEntry factory chain (6 files) để accept + set CorrelationId. (c) Set CorrelationId tại creation sites (SimpleAccountingEventHandler + OutboxEvent). (d) Create LoyaltyIssuanceRecord khi AddPoints (hook trong OrderWorkflowService). (e) **Feature flag infrastructure:** `IFeatureFlagService` + Controller + Admin UI `/admin/valcn-features` + NavMenu link (3 feature flags, default OFF).
**Files:** `1_Shared/Domain.cs`, `1_Shared/DTOs/AccountingEntryDto.cs`, `3_CoreHub/Services/AccountingService.cs`, `3_CoreHub/Services/Events/SimpleAccountingEventHandler.cs`, `3_CoreHub/Services/OrderService.cs` (OutboxEvent), `3_CoreHub/Services/OrderWorkflowService.cs` (LoyaltyIssuanceRecord creation), NEW `FeatureFlagService.cs` + `FeatureFlagsController.cs` + `FeatureFlagHttpService.cs` + `ValcnFeatures.razor` + NavMenu, EF configs, migration
**Domain mod:** YES (additive + 1 new entity + factory param extension)
**Effort:** 3-4 sessions (tăng từ 2-3 — add feature flag infra + UI)
**Verification:** Build pass + migration apply + existing tests pass + new entries có CorrelationId + LoyaltyIssuanceRecord created on AddPoints + FeatureFlag UI renders 3 toggles (all OFF) + API 401/403/200

### Phase 2 — Platform Fee on Marketplace Orders (feature-flagged, default OFF)
**Task card:** `phase2_task_card.md`
**Objective:** Extend `SnapshotCommerceModeAsync` để set `PlatformFeeRate` + `PlatformFeeAmount` trên Marketplace orders — **wrapped in `ValcnV2_PlatformFee` toggle**. Default OFF = existing behavior (no-op). ON = set PlatformFeeAmount.
**Files:** `3_CoreHub/Services/OrderService.cs` (inject IFeatureFlagService + toggle wrap), tests
**Domain mod:** NO (logic only)
**Effort:** 1-2 sessions
**Verification:** Feature OFF → existing behavior. Feature ON → Marketplace order có PlatformFeeAmount > 0

### Phase 3 — Loyalty Budget Enforcement (feature-flagged, default OFF)
**Task card:** `phase3_task_card.md`
**Objective:** `LoyaltyBudgetService` check budget trước AddPoints + 2 reset jobs — **wrapped in `ValcnV2_LoyaltyBudget` toggle**. Default OFF = existing behavior (AddPoints trực tiếp). ON = budget check + atomic counter + 2 reset jobs.
**Files:** New `LoyaltyBudgetService.cs` + 2 jobs, `OrderWorkflowService` (inject check + toggle wrap), DI registration
**Domain mod:** NO
**Effort:** 2-3 sessions
**Verification:** Feature OFF → existing behavior. Feature ON → Budget exhausted → reward = 0 + counters atomic + jobs reset đúng

### Phase 4 — Refund Orchestration + Full Reversal (UC-06, INV-002, feature-flagged, default OFF) — ✅ COMPLETE (commit `9a4d0e9b`)
**Task card:** `phase4_task_card.md`
**Objective:** `RefundOrchestrationService` coordinate **đầy đủ UC-06 (4 steps)** khi order cancel — **wrapped in `ValcnV2_RefundReversal` toggle**. Default OFF = existing behavior (silent cancel). ON = (2a) payment refund HOẶC accrual liability entry (Cash = Accounting, TT 152/2025), (2b) accounting reversal, (2c) loyalty reversal, (2d) referral commission reversal. Idempotency via `IdempotentOperation` entity (fix I6).
**Why full 4 steps:** Verification phát hiện bỏ 2a → **BCTC sai** (Cash ≠ Accrual, vi phạm TT 152/2025). Bỏ 2d → wallet balance + CAC metric sai. UC spec UC-06 require 4 steps.
**Files:** New `RefundOrchestrationService.cs`, `OrderWorkflowService` (hook + toggle wrap), tests
**Domain mod:** NO
**Effort:** 3-4 sessions (tăng từ 2-3 — add 2a payment refund + 2d referral reversal + toggle wrap)
**Verification:** Feature OFF → existing behavior. Feature ON → 4 steps đều chạy + Cash = Accounting + INV-002 enforced
**Implementation notes (Wave 3):** Option B (accrual liability entry accountCode "331") — no payment integration. Natural idempotency (checks existing reversal entries by CorrelationId — no IdempotentOperation table needed). Direct DbContext for LoyaltyIssuanceRecords + WalletTransactions (matches FraudReviewService pattern). `SubtractPointsAsync` instead of `DeductPointsForOrderAsync` (missing). `IAccountingEntryRepository.GetByCorrelationIdAsync` added.

### Phase 7 — Network Dashboard (investor-facing) — ✅ COMPLETE (commit `9a4d0e9b`)
**Task card:** `phase7_task_card.md`
**Objective:** `NetworkDashboardService` cross-tenant aggregate (8 metrics — bỏ Ops Cost fix I3, bỏ Tier Distribution fix I4). Admin UI `/admin/network-dashboard`.
**Files:** New `NetworkDashboardService.cs` + controller + Razor page, DI registration
**Domain mod:** NO (read-only)
**Effort:** 2-3 sessions
**Verification:** Dashboard hiển thị 8 metrics + LoyaltyROI formula đúng (fix C4) + SystemAdmin-only + cache 10 min
**Implementation notes (Wave 3):** Fallback 1000 VND/point (INV-009 deferred — LoyaltyGlobalConfig.PointValue missing). `DateRange` record defined in interface file. `Order.CustomerId` is `Guid?` → filter nulls. `NetworkDashboardController` uses `[InternalApiKey]` (same as LoyaltyBudgetController) — added to W12-G7 exempt list. `VanAMetricsCard` UI Platform component used for 8 metric cards.

---

## 6. INVARIANT ENFORCEMENT MAP (v2)

| Invariant | Phase | Mechanism | Status |
|-----------|-------|-----------|--------|
| INV-001 Order.Completed → Revenue recognized | (existing) | AccountingEntry on completed | ✅ |
| **INV-002 Refunded → Reward reversed** | **Phase 4 ✅** | RefundOrchestrationService + LoyaltyIssuanceRecord | ✅ Enforced (feature-flagged, default OFF) |
| INV-003 No Supplier Deposit → No Point Liability | (existing) | No deposit flow | ✅ |
| INV-004 Point Balance ≠ Cash Balance | (existing) | LoyaltyRewards ≠ Wallet | ✅ |
| INV-005 Negative Margin → flag | **DEFER v3.0** | Phase 9 dropped | ❌ Defer |
| **INV-006 Every Reward → Funding Source** | **DEFER v3.0** | Phase 8 dropped | ❌ Defer |
| **INV-007 Every Promotion → Budget** | **Phase 3 ✅** | LoyaltyBudgetService | ✅ Enforced (loyalty budget only, not promo budget — feature-flagged, default OFF) |
| INV-008 Every Order → Seller + Economics | (existing) + Phase 2 ✅ | Order.TenantId + PlatformFeeAmount | ✅ |
| **INV-009 Platform Fee ≥ Loyalty Cost** | **Phase 3** | Budget check before AddPoints | ⚠️ Partial (no PointValue field — loyalty cost in VND not calculated, fallback 1000 VND/point) |

**MVP scope:** INV-001, 002, 003, 004, 008 fully enforced. INV-007, 009 partial. INV-005, 006 defer v3.0.

---

## 7. RISK REGISTER (v2)

| Rủi ro | Mitigation | Phase |
|--------|-----------|-------|
| AccountingEntry factory chain modification break existing callers | `correlationId` param optional (default null) → existing callers không break | 1 |
| LoyaltyIssuanceRecord new entity — Single-Identity Pattern compliance | Constructor sync `Id = LoyaltyIssuanceRecordId.Value`, EF config `Ignore(e => e.LoyaltyIssuanceRecordId)` | 1 |
| Counter race condition (concurrent AddPoints) | `ExecuteUpdateAsync` atomic increment (EF Core 7+) | 3 |
| RefundOrchestrationService partial failure | IdempotentOperation + Outbox pattern | 4 |
| Network Dashboard cross-tenant query chậm | Cache 10 min, SystemAdmin-only, read-only | 7 |
| LoyaltyROI formula complexity | Filter orders by repeat customer IDs trước khi sum (fix C4) | 7 |
| BackgroundServiceToggleService — 2 new jobs cần toggle | Add 2 jobs vào toggleable list (existing pattern từ Hybrid Strategy Bước 1) | 3 |

---

## 8. ESTIMATED TIMELINE (v2)

| Phase | Sessions | Cumulative |
|-------|----------|------------|
| 0 | 1 | 1 |
| 1 | 3-4 | 4-5 |
| 2 | 1-2 | 5-7 |
| 3 | 2-3 | 7-10 |
| 4 | 3-4 | 10-14 |
| 7 | 2-3 | 12-17 |

**Total: 12-17 sessions (~6-9 tuần)** — core value: feature-flagged loyalty budget + full refund reversal (UC-06 4 steps) + investor dashboard. All features default OFF, admin toggle ON/OFF runtime.

---

## 9. DROPPED SCOPE (DEFER v3.0)

| Item | BOM Section | Lý do defer |
|------|-------------|-------------|
| Merchant Tiering (S/A/B/C) | Section 15 | MVP không cần featured placement automation |
| FundingSource (PromoFundingSource) | Section 9 | INV-006 defer — MVP track discount amount only |
| Negative-margin flag | Section 7 | Monitoring defer — tenant tự quyết giá |
| TenantSettings.District geo clustering | Section 27 | Phase 9 dropped — District field dropped (no MVP consumer, defer v3.0) |
| CategoryHealthReportService | Section 27 | Read-only reporting defer |
| Soft offboard flow | Section 15 | Tier C handling defer (cần Phase 6) |
| PromoCampaign.FundingSource | Section 9 | Phase 8 dropped |
| Tenant.Tier + MerchantTier enum | Section 15 | Phase 6 dropped |

**Rationale:** MVP focus trên 3 core value: (1) platform fee revenue model, (2) loyalty budget cap + refund reversal, (3) investor dashboard. Everything else là "nice-to-have" có thể defer mà không block commercial validation.

---

## 10. ARCHIVE TRIGGER

Khi 6 phases COMPLETE + VPS RV PASS:
- Move master plan + task cards → `docs/AI/tasks/archive/valcn_v2_platform_light/`
- Update `project_state.md` Section 2 + 3 + 4
- BOM v2.0 → `docs/requirements/archive/` (superseded by v3.0)

---

**End of Master Plan v2 — VALCN v2.0 PLATFORM-LIGHT (scope-cut).**
