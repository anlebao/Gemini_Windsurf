# Task Card #7: Commission Base = Referred Product + Self-Referral Blocking

> **Status:** ✅ COMPLETE + DEPLOYED + RV PASS (session 2026-09-17)
> **Commit:** `05443115`
> **CD run:** 35172286668 (SUCCESS — all jobs + post-deploy smoke test)
> **Origin:** two follow-ups recorded in `task_card_06_referral_qr_scan_commission.md`
> **User decision:** base = referred line **SubTotal** (pre-VAT, shipping excluded)

## Defect 1 — commission was computed on the WHOLE order

`SalesmanService.CreateCommissionAsync` passed `order.TotalAmount` to
`SalesReferral.AttachToOrder`. `Order.TotalAmount = SubTotal + TotalVatAmount + ShippingFee − Discount`,
so a customer buying the referred product (50k) plus 500k of unrelated items paid commission on 550k —
including VAT and the shipping fee.

Additionally `WalletService` (COD + external payment, Reseller split) reserved
`margin × rate` while the commission used `orderTotal × rate` → the reserved amount could disagree
with the `SalesReferral` actually created.

### Fix

- New `3_CoreHub/Services/ReferralCommissionCalculator.cs` — single source of truth:
  - `ReferredSubTotal(order)` = sum of the referred line items' `SubTotal` (Quantity × UnitPrice, pre-VAT).
  - `ComputeBase(order, config)`:
    - referred product **not in the order** → `0` → **no commission** created (the customer scanned a
      referral QR but bought something else);
    - Marketplace / `OnOrderTotal` → the referred lines' SubTotal;
    - Reseller / `OnMargin` → order-level margin **pro-rated** by the referred lines' share of
      `order.SubTotal` (per-item cost is not tracked; falls back to the referred SubTotal when
      `order.SubTotal == 0`, e.g. POS-created orders).
- `SalesReferral` gains **`CommissionBaseAmount`** (audited base) + an `AttachToOrder(..., baseAmount, rate, commissionBase)`
  overload; the two legacy overloads also populate it.
- EF config + migration `20260917011937_AddSalesReferralCommissionBaseAmount`
  (`numeric(18,2)`, not null, default 0).
- `CreateCommissionAsync` now `.Include(o => o.Items)`.
- `WalletService.ConfirmCodAsync` / `ConfirmExternalPaymentAsync` now `.Include(o => o.Items)` and use
  the same calculator → the Reseller balance invariant matches the payout.
- `CoolingPeriodJob` pays `referral.CommissionAmount`, so fixing the stored amount is sufficient.

## Defect 2 — self-referral was never blocked

- `CreateCommissionAsync` hardcoded `SameFingerprint: false` (comment: *"no fingerprint data in
  commission flow"*) and never compared salesman vs buyer → a salesman buying their own referral code
  produced a **Pending** commission with `RiskScore 0` that `CoolingPeriodJob` auto-paid after 24h.
  (`SameFingerprint` weight 50 alone was also below the 60 hold / 80 reject thresholds.)

### Fix (layered)

| Layer | Where | Behaviour |
|---|---|---|
| 4 | `PublicOrdersController` checkout | If the referral resolves to the buyer's own `CustomerId` → the attribution is **dropped** (order still created, no `SalesmanId`/`ReferralProductId`/`ReferralCode`). |
| 1 | `CreateCommissionAsync` | `order.CustomerId == order.SalesmanId` → self-referral. |
| 2 | `CreateCommissionAsync` → `DetectSelfReferralAsync` | Matches the buyer's `DeviceRegistrations` (fingerprint hash / device token) against the salesman's — mirrors `AppInstallAttributionService`. Also matches the order's `CustomerDeviceId` for **guest checkout** (no `CustomerId`). |
| 3 | `RiskScoringService` | New `RiskScoreInput.SelfReferral` factor, weight **100** → score 100 → `CommissionStatus.Rejected` (never paid) + `FraudFlag(SelfDeal)` for admin review. Added as an optional positional param (default `false`) so existing callers are unchanged. |

Recording (rather than silently dropping) the self-referral keeps it auditable and surfaces it to the
fraud-review queue; the commission is still never payable.

## Validation

- Build 0 errors · Guard ALL CHECKS PASSED
- Tests **50/50 PASS**: T17 (referred line only), T18 (product not in order → null), T19 (self-referral
  rejected), T20/T21 (`ReferralCommissionCalculator` — VAT excluded / missing product), risk cases
  36 (SelfReferral alone → 100) & 37 (default false). Existing order seeds updated to include the
  referred line (plus `Products`/`Customers` rows for the FKs).

## RV (production)

| Layer | Evidence | Result |
|---|---|---|
| Migration | `SalesReferrals.CommissionBaseAmount numeric(18,2)`; `__EFMigrationsHistory` has `20260917011937_AddSalesReferralCommissionBaseAmount` | ✅ |
| Per-product base | Order `01a0ad21` (referred 50,000 + other 200,000; SubTotal 250,000; TotalAmount 275,000) → `CommissionBaseAmount = 50000.00`, `CommissionAmount = 1500.00` (= 50,000 × 0.03, **not** 275,000 × 0.03 = 8,250) | ✅ |
| Self-referral @ checkout | Order `01a0ad22` with `CustomerId` = the salesman → created with `SalesmanId`/`ReferralProductId`/`ReferralCode` all NULL | ✅ |
| Self-referral @ commission | Forced buyer == salesman on an attributed guest order → `CommissionStatus = 3 (Rejected)`, `RiskScore = 100`, `RiskFactors = {"factors":["SelfReferral:+100"]}`, `FraudFlag(FlagType=1 SelfDeal, RiskScore=100)` | ✅ |
| Salesman view | `GET /api/community/salesman/commissions` → `totalCommission 4650`, `pending 3150`, **`rejected 1500`** (the self-referral is not payable) | ✅ |

> RV used a temporary `DevToken__Secret` (removed afterwards — see `task_card_04_dev_otp_gate.md`).
> Orders were set `pending → confirmed` via SQL so the shipper flow could run (that transition is the
> shop owner's action, not what was under test).

## Notes / follow-ups

- Commission remains a percentage of the referred line's **SubTotal**; VAT and shipping are excluded by
  design. Changing the base later requires a policy decision (it changes payouts).
- Existing `SalesReferrals` created before this change keep their old (whole-order) amounts.
- Reseller `OnMargin` pro-rating assumes margin scales with the sub-total share — revisit if per-item
  cost prices are ever tracked.
- `AppInstallAttributionService` still has its own (correct) self-deal check; it was not refactored to
  share `DetectSelfReferralAsync`.
