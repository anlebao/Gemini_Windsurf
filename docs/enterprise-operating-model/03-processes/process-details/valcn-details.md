# VALCN Process Details — D1 deep-dive (Community Commerce)

> Metadata in `registries/activities.yaml` ACTV-610..621 · coverage anchors ACTV-606..608 · captured at `69496de4`.

## Verified mechanics

- `CommerceModeService` (`CommunityOrderService`/`OrderService` consumer): global mode in `SystemSetting("GlobalCommerceMode")` + default rates `0.30 / 0.05 / 15000`; tenant override via `TenantSettings.CommerceModeOverride` (`Inherit` → global). `SetGlobalModeAsync` validates rates 0-1 and `fee+fund ≤ 1`, rejects global `Inherit`. Snapshot at order creation → future orders only.
- `CommunityOrderService.GetNearbyOrdersAsync`: DELIVERY orders cross-tenant, status confirmed/ready, no active DeliveryTask; distance = Haversine(shipper → `DeliveryLat/Lng` fallback shop coords); `radiusKm ≤ 0` = unlimited; sorted by distance.
- `AcceptOrderAsync`: order + status re-check + active-task re-check → `DeliveryTask` + `Order.AssignShipper` + `delivering` + `OrderStatusHistory("shipper_accept")` + backfills `Conversation.ShipperId` when previously empty.
- `DeliveryWorkflowService.TransitionStatusAsync`: domain methods `MarkPickedUp/OutForDelivery/Delivered/Failed` validate transitions; **Delivered** sets tenant context then delegates to `OrderWorkflowService` → completed + loyalty + accounting + outbox. If delegate returns null → warn-log, task still Delivered (no compensating action).
- `CommunityFundService`: balance computed from `WalletTransactions` on `SystemWalletIds.CommunityFund` (Σ CommunityFund − Σ |CommunityFundSpend|); `SpendAsync` validates fields (reason ≤500, recipient ≤200, approvedBy ≠ empty), blocks overspend, then atomic `WalletService.SpendCommunityFundAsync` + immutable `CommunityFundSpendRecord`. `GetHistoryAsync` paged ≤100.
- `WalletService` Reseller COD confirm legs: `CODCollection + Settlement(costPrice) + DeliveryFee + PlatformFee + CommunityFund` atomic; external (non-COD) path adds `ExternalPayment` with server-side `amount == SellPrice + DeliveryFee` check, Reseller-only. Remittance: pair `Remittance(−cod, shipper) + Settlement(+cod, shop wallet | PlatformWallet)` atomic, max 1 per order.
- `OrderService` Reseller accounting (R2.2): 3 booksets — Supplier (rev=costPrice), Reseller (rev=sellPrice, COGS=costPrice, VAT input 1331), Platform (rev=PlatformFee+CommunityFund, only when Reseller ≠ Vạn An via `PlatformAccountingTenantId`); idempotent via reference-suffix check.
- `CommunityAdminService`: system path defaults `IdentityLevel ≥ Verified && LoyaltyPoints ≥ 1000`; tenant path (R2.1) reads `ShopFeatureSettings` thresholds, list uses `min(Salesman, Shipper)` thresholds; `includeIneligible` shows all active; PII (`PhoneNumber`/`Email` encrypted converter) never projected — masked `"***"`.
- `CommunityController`: `[AllowAnonymous]` class + manual `X-Customer-Token` validation per endpoint + `CheckShipperRoleAsync`; salesman QR/referral/commission + wallet ops (COD/advance/remit/withdraw) + app-install attribution + `LocationHub`/`ChatHub` push.
- `SalesmanService` + `ReferralCommissionCalculator`: composite referral QR → attribution; commission base = referred subtotal, Reseller+`OnMargin` uses margin proxy (per-item cost untracked); `CommissionStatus`/`AttributionStatus` include Rejected (risk ≥80) / Held (60–79, 48h) — FraudReview path.

## Execution split

MANUAL: ACTV-611/612/613/614/617/618/620 (7 — human decisions) · AUTOMATED: ACTV-610/615/616/619/621 (5) · HYBRID: none (decompose-first holds).

## INFERRED items

- ACTV-621 commission lifecycle (create-on-delivered trigger point + payout) — service exists, exact call chain partially traced.
- `PublishDeliveryStatusUpdateAsync` channel (SignalR vs NATS) on status endpoints — verified GPS uses `LocationHub`; status push mechanism not fully traced.
- SMS OTP deposit wallet (`Deposit`/`SmsOtpFee` tx types) — enum verified, flow not traced.

## Draft gaps → gap register candidates

| Candidate | Class | Status |
|---|---|---|
| Community fund spend: single approver, no maker-checker | FRAUD | → RISK-008 registered |
| Orphaned DeliveryTask when order-completion delegate fails | OPERATIONAL | → RISK-011 registered (no control) |
| Ephemeral DP key ring breaks encrypted PII across restarts | OPERATIONAL | → RISK-009 registered (projection workaround only) |
| `GetNearbyOrdersAsync` loads all DELIVERY orders to memory (in-memory status filter `Status.Value` untranslatable) | OPERATIONAL | draft — perf scale risk, unquantified |
