# TASK CARD — Phase 0: ANALYZE (BR spec + verify integration points)

> **Status:** 📋 PENDING
> **Priority:** P0 — Must complete before Phase 1
> **Branch:** `feature/valcn-v2-phase0-analyze`
> **Estimated sessions:** 1
> **Mode:** ANALYZE (no code changes)
> **Domain modification:** NO

## Objective
Draft 14 BR spec (simplified from BOM 16 — drop BR-007 FundingSource, BR-014 Tiering) + verify 4 integration points. Output: `phase0_findings.md` approved trước khi vào Phase 1.

## Prerequisites
- [ ] BOM v2.0 đã đọc
- [ ] Master plan v2 đã review (scope cuts applied)
- [ ] Verification report đã review

## Investigation Tasks

### Task 1: Verify ShopFeatureSettingsEntity.PlatformFeeRate
**Context:** Phase 2 sẽ reuse field này. Cần confirm sẵn sàng.

**Steps:**
1. Read `ShopFeatureSettingsEntity` — list all fields, confirm `PlatformFeeRate` exists
2. Grep `PlatformFeeRate` usage — ai đọc field này hiện tại?
3. Check EF config — mapped per-tenant?
4. Check admin UI — có UI edit hiện không?
5. Default value — hiện default bao nhiêu? BOM nói 5%.

**Output:** Confirm ready / flag gap.

### Task 2: Investigate LoyaltyRewards.History JSON format
**Context:** Phase 1 sẽ add `LoyaltyIssuanceRecord` entity. Cần hiểu History JSON format để:
- Confirm LoyaltyIssuanceRecord có đủ fields (OrderId, CustomerId, PointsIssued, IssuedAt, IsReversed)
- Hoặc phát hiện cần thêm fields (e.g. Reason, RewardType)

**Steps:**
1. Grep `UpdateHistory` calls — ai gọi, truyền JSON gì?
2. Grep `History` reads — ai parse JSON, format gì?
3. Find sample History JSON (test data hoặc seed)
4. Confirm LoyaltyIssuanceRecord field list đủ cho Phase 4 reversal query

**Output:** LoyaltyIssuanceRecord field list (final, confirmed).

### Task 3: Confirm AccountingEntry factory chain modification approach
**Context:** Phase 1 sẽ modify AccountingEntry factory chain (6 files). Cần confirm approach.

**Steps:**
1. Read `AccountingEntry` full class (Domain.cs:287-397) — confirm sealed + private constructor
2. List all factory methods: `CreateRevenue`, `CreateExpense`, `CreateReversal`, `CreateReversalWithId`
3. Read `AccountingEntryDto` — confirm no CorrelationId field
4. Read `AccountingService.CreateEntryAsync` — confirm pass-through path
5. Read `SimpleAccountingEventHandler.HandleOrderCompletedEventAsync` (line 92-125) — confirm creation call
6. Confirm: `correlationId` param optional (default null) → existing callers không break

**Output:** 6-file modification checklist (file:line, exact change).

### Task 4: Draft 14 BR spec
**Context:** BOM Section 33 list 16 BRs. Drop BR-007 (FundingSource — Phase 8 dropped) + BR-014 (Merchant Tiering — Phase 6 dropped). 14 BRs remain.

**BR list (v2 — 14 BRs):**
```
BR-001  Tenant Eligibility & Onboarding (simplified, no merchant approval gate)
BR-002  Product Onboarding (tenant self-serve + soft compliance check)
BR-003  Pricing (tenant sets Price/CostPrice, Vạn An sets PlatformFeeRate)
BR-004  Order Ownership (Tenant = seller, Vạn An = orchestrator)
BR-005  Payment & Settlement (Gateway collects, settle to tenant)
BR-006  Refund & Reversal (coordinate payment + accounting + loyalty reversal)
BR-008  Loyalty Issuance (budget-capped, eligibility-checked)
BR-009  Loyalty Redemption (Alliance cross-tenant)
BR-010  Reward Reversal (on refund/cancel/fraud)
BR-011  Referral Commission (qualified purchase only, anti-MLM)
BR-012  Community Seller Roles (4 roles, capability-based)
BR-013  Delivery & Shipping (customer pays, free-delivery = campaign)
BR-015  Unit Economics Reporting (category + tenant + micro-market, read-only)
BR-016  Loyalty Budget Enforcement (monthly/daily/per-customer/per-order caps)
```

**Spec format per BR:**
```markdown
### BR-XXX: <title>
- **Rule:** <1-2 câu>
- **Enforcement:** <code mechanism — service/field/job>
- **Phase:** <which phase implements>
- **Invariant:** <INV-XXX if applicable>
- **Edge cases:** <list>
```

## Output Deliverable
File: `docs/AI/tasks/valcn_v2_platform_light/phase0_findings.md`

### Section 1: ShopFeatureSettingsEntity Verification
### Section 2: LoyaltyIssuanceRecord Field List (final)
### Section 3: AccountingEntry Factory Chain 6-File Checklist
### Section 4: 14 BR Spec
### Section 5: Phase 1 Field List (final — confirm no drift)

## Verification
- [ ] `phase0_findings.md` written
- [ ] User approves 14 BR spec
- [ ] Phase 1 field list confirmed

## Rollback
N/A — investigation only.

---

## ANALYZE UPDATE (to be filled after investigation)
### Verified Accurate
- (fill after investigation)
### DRIFT
- (fill if investigation finds drift)
