# Phase 0 Findings — VALCN v2.0 PLATFORM-LIGHT

> **Date:** 2026-08-09
> **Status:** ✅ COMPLETE — awaiting user approval before Phase 1
> **Branch:** `feature/valcn-v2-phase0-analyze`
> **Investigation:** 3 tasks verified against codebase via subagent + 1 user decision

---

## Section 1: ShopFeatureSettingsEntity.PlatformFeeRate — GAP FOUND + RESOLVED

### Finding
`ShopFeatureSettingsEntity.PlatformFeeRate` **DOES NOT EXIST**. PlatformFeeRate is a **global SystemSetting** (key `"DefaultPlatformFeeRate"`, default **30%**), managed by `CommerceModeService`.

| Aspect | BOM v2.0 says | Codebase actual (pre-Phase 1) |
|--------|---------------|-------------------------------|
| Where stored | Per-tenant (TenantSettings or ShopFeatureSettings) | Global `SystemSetting` |
| Default | 5% | 30% |
| Per-tenant? | YES | NO |
| Admin UI | (new, per-tenant) | `/admin/commerce-mode` (global) |

**Evidence:**
- `ShopFeatureSettingsEntity` fields list: <ref_file file="C:\VibeCoding\Gemini_Windsurf\3_CoreHub\Infrastructure\Entities\ShopFeatureSettingsEntity.cs" /> — NO PlatformFeeRate
- Global SystemSetting: <ref_snippet file="C:\VibeCoding\Gemini_Windsurf\3_CoreHub\Services\CommerceModeService.cs" lines="25-31" /> — key `DefaultPlatformFeeRate`, default 0.30m
- Admin UI (global): <ref_snippet file="C:\VibeCoding\Gemini_Windsurf\5_WebApps\ShopERP\Components\Pages\Admin\CommerceMode.razor" lines="58-60" />

### Decision (user-approved 2026-08-09)
**Per-tenant (BOM intent).** Add `PlatformFeeRate` (decimal?, default 0.05m = 5%) to `ShopFeatureSettingsEntity` in Phase 1. Global SystemSetting.DefaultPlatformFeeRate serves as fallback when per-tenant field is null.

### Impact
- **Phase 1:** +1 field (`ShopFeatureSettingsEntity.PlatformFeeRate`, decimal?, default 0.05m). Total fields: 12 → 13. Migration +1 column.
- **Phase 2:** Read per-tenant rate with fallback to global. `GetPlatformFeeRateAsync` checks ShopFeatureSettings first, falls back to `CommerceModeService.GetDefaultRatesAsync` if null.

---

## Section 2: LoyaltyRewards.History JSON Format — LoyaltyIssuanceRecord CONFIRMED NEEDED

### Finding
`LoyaltyHistoryEntry` structure (<ref_file file="C:\VibeCoding\Gemini_Windsurf\1_Shared\Models\LoyaltyHistoryEntry.cs" />):
```csharp
public class LoyaltyHistoryEntry
{
    public string Type { get; set; }    // EARN or SPEND
    public int Points { get; set; }
    public string Reason { get; set; }  // e.g. "Order completed #123"
    public DateTime Timestamp { get; set; }
}
```

**CRITICAL:** NO structured `OrderId` field. OrderId only embedded in `Reason` string as text. Not queryable. <ref_snippet file="C:\VibeCoding\Gemini_Windsurf\3_CoreHub\Services\OrderWorkflowService.cs" lines="338-340" /> shows `h.Reason.Contains($"#{order.Id}")` — string matching, fragile.

### Conclusion
`LoyaltyIssuanceRecord` entity (Phase 1) is **NOT redundant** with History. It provides:
- Structured `OrderId` field for per-order reversal queries (Phase 4)
- Queryable database table (vs JSON string parsing)
- `IsReversed` flag for tracking reversal state
- `IssuedAt` for budget period calculation

### LoyaltyIssuanceRecord final field list
```csharp
public class LoyaltyIssuanceRecord : BaseEntity, IMustHaveTenant
{
    public Guid OrderId { get; protected set; }
    public Guid CustomerId { get; protected set; }
    public int PointsIssued { get; protected set; }
    public DateTime IssuedAt { get; protected set; }
    public bool IsReversed { get; protected set; }
}
```
Confirmed sufficient for Phase 4 reversal query: `GetByOrderIdAsync(orderId, tenantId)`.

---

## Section 3: AccountingEntry Factory Chain — 6-File Modification CONFIRMED SAFE

### Finding
- **Sealed class**, private constructor (<ref_snippet file="C:\VibeCoding\Gemini_Windsurf\1_Shared\Domain.cs" lines="287-357" />)
- **4 factory methods**: `CreateRevenue`, `CreateExpense`, `CreateReversal`, `CreateReversalWithId`
- **~147 total callers** (80 + 32 + 35)
- `AccountingEntryDto` has NO CorrelationId (<ref_file file="C:\VibeCoding\Gemini_Windsurf\1_Shared\DTOs\AccountingEntryDto.cs" />)
- `AccountingEntryService.CreateEntryAsync` (line 82-84) only passes basic fields, NOT optional params

### Backward compatibility CONFIRMED
Adding `Guid? correlationId = null` optional param to factory methods = **safe**:
- Default value null → existing 147 callers don't need changes
- C# optional params are backward compatible

### CreateReversal field preservation (current)
**Preserves:** TenantId, Amount (negated), EntryType, VatRate, AccountingBookType, PeriodYear, PeriodMonth, AccountCode, IndustrySector
**Drops:** Vendor, Category, Reference, ReferenceId, ReferenceType
**Needs fix (Phase 1):** Add CorrelationId to preserved list (copy from original)

### 6-file modification checklist (confirmed)
| # | File | Change |
|---|------|--------|
| 1 | `1_Shared/Domain.cs:287-357` (AccountingEntry constructor) | Add `correlationId` param |
| 2 | `1_Shared/Domain.cs:360-414` (4 factory methods) | Add `correlationId` optional param |
| 3 | `1_Shared/Domain.cs:378-395` (CreateReversal) | Preserve `correlationId` from original |
| 4 | `1_Shared/DTOs/AccountingEntryDto.cs` | Add `CorrelationId` field |
| 5 | `3_CoreHub/Services/AccountingEntryService.cs:82-84` | Pass `CorrelationId` from DTO to factory |
| 6 | `3_CoreHub/Services/Events/SimpleAccountingEventHandler.cs:114-125` | Set `CorrelationId = orderEvent.OrderId` |

---

## Section 4: 14 BR Spec (v2 — simplified from BOM 16)

### BR-001: Tenant Eligibility & Onboarding
- **Rule:** Tenant self-register via ShopERP. No merchant approval gate. Skin-in-the-game = tenant gánh inventory (Drop-ship default).
- **Enforcement:** Existing `Tenant` entity + `TenantSettings` (no new logic)
- **Phase:** (existing — 0 change)
- **Edge cases:** Tenant bán lỗ chronic → flag trong v3.0 (Phase 9 dropped)

### BR-002: Product Onboarding
- **Rule:** Tenant tự nhập ProductName, Category, CostPrice, Price, VatRate. Soft compliance check (VAT valid, margin = Price - CostPrice). Auto-assign loyalty rate by category. Publish to KhachLink.
- **Enforcement:** Existing `Product` entity + `ShopERP` product management
- **Phase:** (existing — 0 change)
- **Edge cases:** Price < CostPrice → allow publish, flag in v3.0

### BR-003: Pricing
- **Rule:** Tenant sets CostPrice + Price. Vạn An sets PlatformFeeRate per-tenant (default 5%, fallback global 30%). Customer pays Price + VAT + ShippingFee - DiscountAmount.
- **Enforcement:** `ShopFeatureSettingsEntity.PlatformFeeRate` (Phase 1 field) + `Order.PlatformFeeAmount` (Phase 1 field) + `OrderService.SnapshotCommerceModeAsync` (Phase 2)
- **Phase:** 1 (field) + 2 (logic)
- **Edge cases:** PlatformFeeRate null → fallback global DefaultPlatformFeeRate

### BR-004: Order Ownership
- **Rule:** Tenant = Seller of Record (Order.TenantId). Vạn An = orchestrator + channel + loyalty issuer. Not seller.
- **Enforcement:** Existing `Order.TenantId` + `CommerceMode.Marketplace` default
- **Phase:** (existing — 0 change)

### BR-005: Payment & Settlement
- **Rule:** Gateway collects payment. Settlement: Platform Fee → Vạn An, remainder → Tenant. Tenant tự ghi nhận Revenue/COGS trong per-tenant accounting.
- **Enforcement:** Existing Gateway payment + `AccountingEntry` per-tenant
- **Phase:** (existing) + Phase 2 (PlatformFeeAmount snapshot) + Phase 4 (reversal on refund)
- **Edge cases:** Refund → see BR-006

### BR-006: Refund & Reversal (UC-06 — 4 steps)
- **Rule:** Order cancel/refund → (2a) payment refund hoặc accrual liability entry, (2b) accounting reversal, (2c) loyalty reversal, (2d) referral commission reversal. Idempotent. Full refund only (no partial).
- **Enforcement:** `RefundOrchestrationService` (Phase 4) + `LoyaltyIssuanceRecord` (Phase 1) + `AccountingEntry.CorrelationId` (Phase 1) + `WalletService.ReverseTransactionAsync` (existing) + `IdempotentOperation` (existing)
- **Phase:** 1 (infra) + 4 (logic)
- **Invariant:** INV-002
- **Edge cases:** Order with no rewards → skip 2c. Order with no referral → skip 2d. Payment integration missing → accrual liability entry (TK 331).

### BR-008: Loyalty Issuance (budget-capped)
- **Rule:** Issue reward only if: order completed + not refunded + budget available. Budget caps: monthly, daily, per-customer daily, per-order rate. Budget exhausted → reward rate = 0 (tenant still sells, just no reward).
- **Enforcement:** `LoyaltyBudgetService.CheckAndAdjustPointsAsync` (Phase 3) + `LoyaltyTenantConfig` 6 budget fields (Phase 1) + `LoyaltyIssuanceRecord` (Phase 1)
- **Phase:** 1 (fields + entity) + 3 (logic)
- **Invariant:** INV-007, INV-009
- **Edge cases:** PlatformFeeAmount null (Phase 2 OFF) → skip INV-009 check

### BR-009: Loyalty Redemption (Alliance cross-tenant)
- **Rule:** Customer earns at Tenant A, redeems at Tenant B. AllianceWallet cross-tenant. Gateway records RedemptionRecord.
- **Enforcement:** Existing `AllianceWallet` + `RedemptionRecord` + Loyalty Alliance 7 phases
- **Phase:** (existing — 0 change)

### BR-010: Reward Reversal
- **Rule:** Order cancel/refund/fraud → reward reversed via DeductPoints + LoyaltyIssuanceRecord.IsReversed + budget counter decrement.
- **Enforcement:** `RefundOrchestrationService` (Phase 4) + `LoyaltyBudgetService.DecrementIssuanceAsync` (Phase 3)
- **Phase:** 3 (decrement method) + 4 (orchestration)
- **Invariant:** INV-002

### BR-011: Referral Commission (qualified purchase only)
- **Rule:** Commission paid only after qualified purchase (order completed + fraud check pass + cooling period). Anti-MLM: commission from real commerce, not recruitment.
- **Enforcement:** Existing `SalesReferral` + `FraudFlag` + `CoolingPeriodJob` + `WalletTransaction`
- **Phase:** (existing — 0 change) + Phase 4 (reversal on refund)

### BR-012: Community Seller Roles
- **Rule:** 4 roles: Customer, Referral Partner, Delivery Partner, Community Seller. Capability-based.
- **Enforcement:** Existing `CommunityRole` + `CommunityRoleType` enum
- **Phase:** (existing — 0 change)
- **Edge cases:** CommunityRoleType has Shipper + Salesman (no "Delivery Partner" by name — Shipper = Delivery Partner functionally)

### BR-013: Delivery & Shipping
- **Rule:** Customer pays ShippingFee. Free-delivery = optional campaign (funded by campaign budget). Vạn An orchestrates delivery, không gánh delivery cost mặc định.
- **Enforcement:** Existing `Order.ShippingFee` + `DeliveryTask` + `DeliveryTracking`
- **Phase:** (existing — 0 change)

### BR-015: Unit Economics Reporting
- **Rule:** Network Dashboard: GMV, active tenants, active customers, repeat rate, platform revenue, loyalty cost, loyalty ROI, contribution profit. Read-only, SystemAdmin-only, cache 10 min.
- **Enforcement:** `NetworkDashboardService` (Phase 7) + cross-tenant query (IgnoreQueryFilters)
- **Phase:** 7
- **Edge cases:** Ops Cost excluded (defer v3.0). Tier Distribution excluded (Phase 6 dropped).

### BR-016: Loyalty Budget Enforcement
- **Rule:** Monthly/daily/per-customer/per-order caps. Reset daily (PointsIssuedToday) + monthly (PointsIssuedThisMonth). Counter increment atomic (ExecuteUpdateAsync).
- **Enforcement:** `LoyaltyBudgetService` (Phase 3) + `LoyaltyBudgetDailyResetJob` + `LoyaltyBudgetMonthlyResetJob` (Phase 3) + `LoyaltyTenantConfig` 6 fields (Phase 1)
- **Phase:** 1 (fields) + 3 (logic + jobs)
- **Invariant:** INV-007, INV-009

---

## Section 5: Phase 1 Field List (FINAL — updated with PlatformFeeRate per-tenant)

### 12 Additive Fields (decreased from 13 — District dropped, no MVP consumer)
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

**DROP District** — Phase 9 (District clustering) dropped, no MVP consumer. Defer v3.0 (with VN administrative seeding if needed).

### 1 New Entity: LoyaltyIssuanceRecord
(LoyaltyIssuanceRecordId business key VO ignored in EF per Single-Identity Pattern)

### 3 Feature Flags (SystemSetting keys, default OFF)
- `Features:EnableValcnV2_PlatformFee` (Phase 2)
- `Features:EnableValcnV2_LoyaltyBudget` (Phase 3)
- `Features:EnableValcnV2_RefundReversal` (Phase 4)

### 1 Migration
- Add 10 nullable/default columns (existing entities)
- Add 1 new table `LoyaltyIssuanceRecords`
- Add 1 column `PlatformFeeRate` on `ShopFeatureSettings` (default 0.05m)
- **District dropped** — no MVP consumer

---

## Approval

- [ ] User approves 14 BR spec
- [ ] User approves Phase 1 field list (12 fields + 1 entity + 3 feature flags + 1 migration)
- [ ] User approves per-tenant PlatformFeeRate (ShopFeatureSettingsEntity, default 5%)
- [ ] Ready to proceed to Phase 1
