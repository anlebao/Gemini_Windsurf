# Task Card #5: Chat + GPS + Salesman QR — 3 Production Defects (FIXED + RV PASS)

> **Status:** ✅ COMPLETE + DEPLOYED + RV PASS (session 2026-09-16)
> **Priority:** P1 (chat + GPS are core to "customer chat + track")
> **Commit:** `26dc9e62`
> **CD run:** 35116954907 (SUCCESS — all jobs + post-deploy smoke test)
> **Affected domains:** both `diemthuong2.khachvip.online` and `commienphi.timlathay.com` (identical failures)

## Symptom

Reported: chat and GPS tracking "không hoạt động" on both KhachLink domains; the salesman
product-referral QR showed a blank white frame instead of a QR image.

## Root causes

### 1. Chat — string parameter passed without `@` (literal token)

`DeliveryTracking.razor:234` and `OrderTracking.razor:202`:

```razor
<ChatPanel OrderId="OrderId" CurrentUserId="_customerId.Value" CustomerToken="_customerToken" ... />
```

`CustomerToken` is a **string** parameter. Per Razor semantics, a bare value on a string
parameter is a **string literal** — so ChatPanel received the literal text `"_customerToken"`
instead of the token value. (`OrderId`/`CurrentUserId` are `Guid`, which Razor parses as C#
expressions, so only the string parameter was affected.)

Production evidence (Gateway log):

```
GET /hubs/chat?customerToken=_customerToken&id=sRaQyjuL4hH5X13mC6MUGg
GET /api/community/chat/conversations/01a0a926-... → 401
ShopERP GET /api/customer-identity/me → 401
Gateway HubException: Invalid customerToken   (ChatHub.cs:32)
```

**Fix:** `CustomerToken="@_customerToken"`.

### 2. GPS — OrderId sent instead of DeliveryTaskId

`DeliveryTracking.razor` `StartGpsTracking()` passed `OrderId.ToString()` as the
`deliveryTaskId`. Gateway `RecordLocationAsync` looks up `DeliveryTasks.Id` (PK), so every
ping was silently discarded (endpoint still returns 200).

Production evidence:

```
[13:43:27 WRN] RecordLocation: DeliveryTask 01a0a926-2cf8-74a5-948c-e7360109ca13 not found
```

(`01a0a926` is the **Order** id; the real DeliveryTask id is `f07c5f6e-e8e0-477c-ad75-c94839a1be64`.)

**Fix:** track `_deliveryTaskId` (from `my-deliveries` + pickup response) and pass it.

### 3. Salesman QR — JS invoked on the wrong render

`SalesmanQR.razor`:

```csharp
if (firstRender && _qr != null)
    await JSRuntime.InvokeVoidAsync("vananQR.generate", "qrCanvas", _qr.QrUrl, 300, 300);
```

`_qr` loads asynchronously, so `firstRender` fires while the loading spinner is shown (no
canvas in the DOM). When the data arrives, the render has `firstRender == false`, so
`vananQR.generate` was never called → blank canvas.

**Fix:** generate on the first render where `_qr` is set, guarded by a `_qrRendered` flag.

## Files changed

| File | Change |
|---|---|
| `5_WebApps/KhachLink/Pages/DeliveryTracking.razor` | `@` on CustomerToken; `_deliveryTaskId` field + wiring; StartGpsTracking uses it |
| `5_WebApps/KhachLink/Pages/OrderTracking.razor` | `@` on CustomerToken |
| `5_WebApps/KhachLink/Pages/SalesmanQR.razor` | `_qrRendered` flag; generate on data-ready render |

## Validation

- Build: 0 errors
- Guard: ALL CHECKS PASSED
- CD Multi-VPS: SUCCESS (Build → Pre-Deploy → Gateway → ShopERP → KhachLink → Smoke Test)

## RV — deployment verification (bundle)

Downloaded `/_framework/VanAn.KhachLink.wasm` from `diemthuong2.khachvip.online`:

```
_customerToken  : utf16LE=0   ← literal string GONE (was the cause; strings live in UTF-16 #US heap)
_qrRendered     : ascii=1     ← new field present
_deliveryTaskId : ascii=1     ← new field present
delivery-chat-scroll: utf16LE=1  ← control: a real literal is still UTF-16
```

## RV — runtime (server-side, with a temporary dev-token for test customer `e77ad484`)

Dev-token was enabled temporarily on the ShopERP VPS, then **removed** (see
`task_card_04_dev_otp_gate.md` → "TEMPORARY DEV-TOKEN SECRET").

**Chat:**
```
GET /api/customer-identity/me                          → 200  (customerId e77ad484…)
GET /api/community/chat/conversations/01a0a926-…       → 200  (conversationId + 2 messages)
```
ChatHub validates the token via this exact `/me` call, so `Invalid customerToken` is resolved.

**GPS:**
```
POST /api/community/location/update {deliveryTaskId: 01a0a926-…(OrderId)} → 200 + WRN "not found"   (old bug reproduced)
POST /api/community/location/update {deliveryTaskId: f07c5f6e-…(real)}   → 200, no warning
SELECT … FROM "DeliveryTrackings" → 1 row: f07c5f6e | 10.966 | 106.594   (was empty before)
```

**QR:**
```
GET /api/community/salesman/qr?productId=38df680d-…  → 200
qrUrl = https://diemthuong.khachvip.online/r/DL9ZMQ|COMRV1
vananQR.generate('qrCanvas', qrUrl, 300, 300)  → canvas 268x268, 569 black module rects  (QR renders)
```
(Headless check of `wwwroot/js/qrcode.js` with a canvas stub — proves the JS payload encodes;
the bug was purely the call-site timing.)

## Known follow-ups (NOT fixed in this card)

1. `SalesmanService.GetCompositeSalesmanQrAsync` hardcodes the QR URL host to
   `https://diemthuong.khachvip.online` — QR scanned on `commienphi.timlathay.com` (or any
   other instance) points at the wrong domain. Should use the requesting instance's host.
2. The composite code contains a `|` (`DL9ZMQ|COMRV1`) which is not URL-safe in a path.
