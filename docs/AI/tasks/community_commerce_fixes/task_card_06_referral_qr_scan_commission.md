# Task Card #6: Salesman Referral QR — Correct Domain + Scan-to-Buy + Commission

> **Status:** ✅ COMPLETE + DEPLOYED + RV PASS (session 2026-09-16/17)
> **Commit:** `aceab325`
> **CD run:** 35166349140 (SUCCESS — all jobs + post-deploy smoke test)
> **Domain approval:** Domain.cs addition approved by user (option A) before implementation

## Reported problems

1. The salesman referral QR scanned to the **wrong domain** (`diemthuong.khachvip.online/...`)
   instead of the instance the salesman was using (`diemthuong2.khachvip.online`).
2. Customer scanning a referral QR should be able to **buy the referred product** and the
   **salesman should get commission** — mirroring the product-QR scan → order flow.
3. The QR image **overflowed its frame on smartphones**.

## Root causes found

| # | Defect | Detail |
|---|---|---|
| 1 | Hardcoded QR host | `SalesmanService.GetCompositeSalesmanQrAsync` emitted `https://diemthuong.khachvip.online/r/{code}` — the Oracle VPS host, wrong for every other instance. |
| 2 | No landing route | `/r/{code}` is **not** a KhachLink route and nginx has no `/r/` rule → a phone-camera scan landed nowhere. Also `\|` is not URL-safe in a path. |
| 3 | Referral never reached the order | `Order` had `SalesmanId`/`ReferralCode`/`ReferralProductId` but **no domain method to set them** (same gap as `ShipperId` — see the comment at `Domain.cs:1725`). `CheckoutOrderRequest` had no `ReferralCode`, `Checkout.razor` never sent one. |
| 4 | Commission never created | `ISalesmanService.CreateCommissionAsync` was **never called outside tests** — no production call site at all. |
| 5 | Scan did nothing useful | `Scan.razor` only saved the referral code for *app-install* attribution — it never resolved or added the referred product to the cart. |
| 6 | QR overflow | `<canvas style="max-width:300px">` inside a `p-4` card overflowed a 360px viewport. |

## Implementation

**QR URL (correct domain + working landing page)**
- `ISalesmanService.GetCompositeSalesmanQrAsync(..., string? khachLinkBaseUrl = null)`; the
  service builds `{origin}/scan?ref={Uri.EscapeDataString(compositeCode)}` and falls back to
  config `ExternalUrls:KhachLink`.
- `CommunityController.GetSalesmanQr` accepts `?sourceDomain=`; `SalesmanQR.razor` passes
  `Navigation.Uri.Host`; `CommunityHttpService.GetSalesmanQrAsync` forwards it.

**Scan → buy → commission**
- New **anonymous** `GET /api/community/referral/{code}` →
  `ISalesmanService.ResolveReferralForScanAsync` → product display info from PG `FeaturedProducts`
  (same source as the product-QR fast path; the operational product lives in ShopERP SQLite).
- `Scan.razor` handles `?ref=` (and legacy `/r/`) → resolve → `CartService.AddItemAsync` →
  `vananAppInstall.saveReferralCode` → shows a "referral applied" banner. Mirrors `ProcessQrPayload`.
- `Checkout.razor` sends `ReferralCode` from localStorage `vanan_referral_code`.
- `CheckoutOrderRequest.ReferralCode` → `PublicOrdersController` resolves it via
  `ISalesmanService.ResolveCompositeReferralCodeAsync` (safe-fail — an invalid code never blocks
  checkout) → `CreateOrderCommand.SalesmanId/ReferralProductId/ReferralCode` →
  `OrderService.CreateOrderFromCommandAsync` → `Order.SetSalesmanReferral(...)`.
- `OrderWorkflowService.HandleOrderCompletedAsync` creates the `SalesReferral` commission
  (safe-fail side-effect, matching the loyalty/stats side-effects). `ISalesmanService` is an
  **optional** ctor param because it is registered in Gateway only — ShopERP/test scopes get null.

**Domain (approved)**
- `Order.SetSalesmanReferral(Guid salesmanId, Guid referralProductId, string referralCode)` —
  throws on empty ids, follows the existing `AssignShipper`/`SetDeliveryLocation` precedent.

**QR size on mobile**
- New `.qr-canvas-wrap` CSS in `SalesmanQR.razor`: canvas is fluid (`width:100%`,
  `max-width:260px`, `aspect-ratio:1/1`, centred; `200px` under 400px) and the composite code is
  `word-break: break-all`.

## Validation

- Build: 0 errors · Guard: ALL CHECKS PASSED
- Tests: **21/21 PASS** — T5 updated (URL format), new T14/T14b (caller host), T15/T16
  (`ResolveReferralForScan`), 4 new `OrderSetSalesmanReferralTests`.

## RV (production)

| Layer | Evidence | Result |
|---|---|---|
| QR URL | `GET /api/community/salesman/qr?productId=38df680d-…&sourceDomain=diemthuong2.khachvip.online` → `qrUrl = https://diemthuong2.khachvip.online/scan?ref=DL9ZMQ%7CCOMRV1` | ✅ correct host, `\|` escaped |
| QR URL (2nd instance) | same call with `sourceDomain=commienphi.timlathay.com` → 200 | ✅ |
| Anonymous resolve | `GET /api/community/referral/DL9ZMQ%7CCOMRV1` → 200 `{salesmanCode DL9ZMQ, productShortCode COMRV1, name "Com Trua RV", price 50000, vatRate 0.1}` | ✅ |
| Checkout attribution | `POST /api/public/orders/checkout` with `ReferralCode` → order `01a0accb-…` has `SalesmanId=e77ad484…`, `ReferralProductId=38df680d…`, `ReferralCode=DL9ZMQ\|COMRV1` | ✅ |
| Commission | shipper accept → pickup → delivering → delivered → order `completed`; `SalesReferrals` row created: `CommissionRate=0.03`, `CommissionAmount=1650.00` (= 55000 × 0.03), `CommissionStatus=Pending`, `RiskScore=0` | ✅ |
| Salesman view | `GET /api/community/salesman/commissions` → `totalCommission 1650`, `pending 1650`, 1 record for the order | ✅ |

> RV used a temporary `DevToken__Secret` (removed afterwards — see
> `task_card_04_dev_otp_gate.md`). The test order was set `pending → confirmed` via SQL so the
> shipper flow could run (that transition is the shop owner's action and is not what was under test).

## Notes / follow-ups

- Commission is computed from `Order.TotalAmount × CommissionRate` (existing design), not from the
  referred line item only. A referral for a free/charity product yields 0 commission.
- The free/charity quick-order paths (`Cart.razor`, `KhachLinkLayout.razor`) still do not send
  `ReferralCode` — deliberate (those are 0-value orders).
- Self-referral (salesman buys their own referral) is not blocked — `CreateCommissionAsync` relies on
  risk scoring + fraud flags instead.
